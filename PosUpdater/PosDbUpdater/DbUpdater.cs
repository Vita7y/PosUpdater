using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using NLog;

namespace PosDbUpdater
{
    public static class DbUpdater
    {

        static DbUpdater()
        {
            CreDirName = "CRE";
            QryDirName = "QRY";
        }

        private static string CreDirName { get; set; }

        private static string QryDirName { get; set; }

        public static int Update(string[] args)
        {
            var list = new List<Exception>();
            var num = 0;

            try
            {
                LogManager.GetCurrentClassLogger().Info(@"Started");
                LogManager.GetCurrentClassLogger().Info(@"Reading config");

                var config = DbUpdaterConfig.GetConfig("GM_DBUpdate.cfg", args);
                LogManager.GetCurrentClassLogger().Info("Current configuration");
                LogManager.GetCurrentClassLogger().Info("ConfigFilePath: " + config.ConfigFilePath);
                LogManager.GetCurrentClassLogger().Info("ScriptsPath: " + config.ScriptsPath);
                LogManager.GetCurrentClassLogger().Info("ScriptsPathCRE: " + config.ScriptsPathCre);
                LogManager.GetCurrentClassLogger().Info("ScriptsPathQRY: " + config.ScriptsPathQry);
                LogManager.GetCurrentClassLogger().Info("Macros count: " + config.Macros.Count);
                LogManager.GetCurrentClassLogger().Info("SqlServer: " + config.SqlServer);
                LogManager.GetCurrentClassLogger().Info("SqlDataBase: " + config.SqlDataBase);
                LogManager.GetCurrentClassLogger().Info("SqlLogin: " + config.SqlLogin);
                //DbUpdaterCommon.Log("SqlPassword: " + config.SqlPassword, DbUpdaterCommon.ShowDate.Unknown);
                LogManager.GetCurrentClassLogger().Info("SqlTrustedConnection:" + (config.SqlTrustedConnection ? 1 : 0));
                LogManager.GetCurrentClassLogger().Info("ConnectionString: " + config.ConnectionString);
                config.Validate();

                var files1 = new List<string>();
                if (Directory.Exists(config.ScriptsPathCre))
                {
                    if (!File.Exists(Path.Combine(config.ScriptsPathCre, "install.lst")))
                    {
                        throw new ConfigurationErrorsException(string.Format("File {0} don't exists in {1}",
                                                                             "install.lst",
                                                                             config.ScriptsPathCre));
                    }

                    files1 = CorrectPath(new List<string>(File.ReadAllLines(Path.Combine(config.ScriptsPathCre, "install.lst"))), config.ScriptsPathCre);
                }
                else
                {
                    LogManager.GetCurrentClassLogger().Error("Directory CRE not found");
                }

                var files2 = new List<string>();
                if (Directory.Exists(config.ScriptsPathQry))
                {
                    files2 = CorrectPath(!File.Exists(Path.Combine(config.ScriptsPathQry, "install.lst"))
                                             ? new List<string>(Directory.GetFiles(config.ScriptsPathQry))
                                             : new List<string>(File.ReadAllLines(Path.Combine(config.ScriptsPathQry, "install.lst"))),
                                         config.ScriptsPathQry);
                }
                else
                {
                    LogManager.GetCurrentClassLogger().Error("Directory QRY not found");
                }

                LogManager.GetCurrentClassLogger().Info(string.Format("Total {0} scripts to execute in CRE, {1} scripts in QRY", files1.Count, files2.Count));

                using (var gmDbUpdater = new DbUpdaterEngine(config.ConnectionString))
                {
                    list.AddRange(gmDbUpdater.ProcessScripts(files1, "Processing CRE files", config.Macros, true));
                    list.AddRange(gmDbUpdater.ProcessScripts(files2, "Processing QRY files", config.Macros, false));
                }
            }
            catch (Exception ex)
            {
                list.Add(ex);
                ex.WriteToLog("PosDbUpdater.DbUpdater.Update");
                num = -1;
            }

            return num;
        }

        public static int Update(string connectionString, string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath)
                || !Directory.Exists(rootPath))
                throw new ArgumentNullException("rootPath");

            var cre = Path.Combine(rootPath, CreDirName);
            var qry = Path.Combine(rootPath, QryDirName);

            return Update(connectionString, cre, qry);
        }

        private static int Update(string connectionString, string pathCre, string pathQry)
        {
            return Update(connectionString, pathCre, pathQry, new Dictionary<string, string>());
        }

        private static int Update(string connectionString, string pathCre, string pathQry,
                                  Dictionary<string, string> macros)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentNullException("connectionString");
            if (string.IsNullOrEmpty(pathCre)
                || !Directory.Exists(pathCre))
                throw new ArgumentNullException("pathCre");
            if (string.IsNullOrEmpty(pathQry)
                || !Directory.Exists(pathQry))
                throw new ArgumentNullException("pathQry");
            if (macros == null)
                throw new ArgumentNullException("macros");

            var list = new List<Exception>();
            var num = 0;

            try
            {
                var files1 = new List<string>();
                if (Directory.Exists(pathCre))
                {
                    if (!File.Exists(Path.Combine(pathCre, "install.lst")))
                        throw new ConfigurationErrorsException(string.Format("File {0} don't exists in {1}", "install.lst", pathCre));

                    files1 = CorrectPath(new List<string>(File.ReadAllLines(Path.Combine(pathCre, "install.lst"))), pathCre);
                }
                else
                {
                    LogManager.GetCurrentClassLogger().Error("Directory CRE not found");
                }

                var files2 = new List<string>();
                if (Directory.Exists(pathQry))
                {
                    files2 = CorrectPath(!File.Exists(Path.Combine(pathQry, "install.lst"))
                                             ? new List<string>(Directory.GetFiles(pathQry))
                                             : new List<string>(File.ReadAllLines(Path.Combine(pathQry, "install.lst"))),
                                         pathQry);
                }
                else
                {
                    LogManager.GetCurrentClassLogger().Error("Directory QRY not found");
                }

                LogManager.GetCurrentClassLogger().Info(string.Format("Total {0} scripts to execute in CRE, {1} scripts in QRY", files1.Count, files2.Count));

                using (var gmDbUpdater = new DbUpdaterEngine(connectionString))
                {
                    list.AddRange(gmDbUpdater.ProcessScripts(files1, "Processing CRE files", macros, true));
                    list.AddRange(gmDbUpdater.ProcessScripts(files2, "Processing QRY files", macros, false));
                }
            }
            catch (Exception ex)
            {
                list.Add(ex);
                ex.WriteToLog("PosDbUpdater.DbUpdater.Update");
                num = -1;
            }

            return num;
        }

        private static List<string> CorrectPath(List<string> files, string path)
        {
            if (files != null && files.Count > 0)
            {
                for (var index = 0; index < files.Count; ++index)
                {
                    if (!files[index].Contains('\\'))
                        files[index] = Path.Combine(path, files[index]);
                }
            }
            return files;
        }

    }
}