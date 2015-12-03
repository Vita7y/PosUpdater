using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Xml;
using NLog;

namespace PosDbUpdater
{
    internal class DbUpdaterConfig
    {

        private DbUpdaterConfig()
        {
            ConfigFilePath = AppDomain.CurrentDomain.BaseDirectory;
            ScriptsPath = AppDomain.CurrentDomain.BaseDirectory;
            Macros = new Dictionary<string, string>();
        }


        public string SqlServer { get; private set; }

        public string SqlDataBase { get; private set; }

        public string SqlLogin { get; private set; }

        public string SqlPassword { get; private set; }

        public bool SqlTrustedConnection { get; private set; }

        public string ConfigFilePath { get; private set; }

        public string ScriptsPath { get; private set; }

        public string ScriptsPathCre
        {
            get { return Path.Combine(ScriptsPath, "CRE"); }
        }

        public string ScriptsPathQry
        {
            get { return Path.Combine(ScriptsPath, "QRY"); }
        }

        public string ConnectionString { get; private set; }

        public Dictionary<string, string> Macros { get; private set; }

        public void Validate()
        {
            if (string.IsNullOrEmpty(ConnectionString))
                throw new ConfigurationErrorsException("DB connection string is not set");
            
            if (!Directory.Exists(ScriptsPath))
                throw new ConfigurationErrorsException(string.Format("Directory {0} don't exists", ScriptsPath));
            
            if (string.IsNullOrEmpty(SqlServer))
                throw new ConfigurationErrorsException("SQL server is not set");
            
            if (string.IsNullOrEmpty(SqlDataBase))
                throw new ConfigurationErrorsException("Database is not set");
            
            if (!SqlTrustedConnection && (string.IsNullOrEmpty(SqlLogin) || string.IsNullOrEmpty(SqlLogin)))
                throw new ConfigurationErrorsException("Then SQL-authentification mode is not trusted, used login and password should be set");
        }

        public static DbUpdaterConfig GetConfig(string configFileName, string[] args)
        {
            var gmDbUpdaterConfig1 = new DbUpdaterConfig();
            if (args != null && args.Length > 0)
            {
                for (var index = 0; index < args.Length; ++index)
                {
                    if (IsArgument(args[index], "C")
                        && index < args.Length - 1)
                    {
                        var str = args[index + 1];
                        if (string.IsNullOrEmpty(str))
                        {
                            throw new ConfigurationErrorsException("If /C argument is passed, path to configuration file should be set after it");
                        }
                        
                        var path = str.Trim('"', '\'', '\\');
                        if (!File.Exists(path))
                        {
                            throw new Exception(string.Format("Configuration file {0} don't exists.", path));
                        }

                        gmDbUpdaterConfig1.ConfigFilePath = Path.GetDirectoryName(path);
                        configFileName = Path.GetFileName(path);
                    }
                }
            }

            if (File.Exists(Path.Combine(gmDbUpdaterConfig1.ConfigFilePath, configFileName)))
            {
                var xmlDocument = new XmlDocument();
                xmlDocument.Load(Path.Combine(gmDbUpdaterConfig1.ConfigFilePath, configFileName));
                var elementsByTagName1 = xmlDocument.GetElementsByTagName("macro");
                
                if (elementsByTagName1.Count > 0)
                {
                    foreach (XmlNode xmlNode in elementsByTagName1)
                    {
                        gmDbUpdaterConfig1.Macros.Add(xmlNode.Attributes["name"].Value,xmlNode.Attributes["value"].Value);
                    }
                }
                
                var elementsByTagName2 = xmlDocument.GetElementsByTagName("connectionString");
                if (elementsByTagName2.Count == 1)
                {
                    gmDbUpdaterConfig1.ConnectionString = elementsByTagName2[0].Attributes["value"].Value;
                }

                var elementsByTagName3 = xmlDocument.GetElementsByTagName("scriptsPath");
                if (elementsByTagName3.Count == 1)
                {
                    gmDbUpdaterConfig1.ScriptsPath = elementsByTagName3[0].Attributes["value"].Value;
                }
            }
            else
            {
                LogManager.GetCurrentClassLogger().Error(string.Format("Configuration file {0} don't exists.", Path.Combine(gmDbUpdaterConfig1.ConfigFilePath, configFileName)));
            }

            var connectionStringBuilder = !string.IsNullOrEmpty(gmDbUpdaterConfig1.ConnectionString)
                                              ? new SqlConnectionStringBuilder(gmDbUpdaterConfig1.ConnectionString)
                                              : new SqlConnectionStringBuilder();

            if (connectionStringBuilder.ConnectionString.IndexOf("Data Source", StringComparison.InvariantCultureIgnoreCase) != -1
                && !string.IsNullOrEmpty(connectionStringBuilder.DataSource))
            {
                gmDbUpdaterConfig1.SqlServer = connectionStringBuilder.DataSource;
            }

            if (connectionStringBuilder.ConnectionString.IndexOf("Initial Catalog", StringComparison.InvariantCultureIgnoreCase) != -1
                && !string.IsNullOrEmpty(connectionStringBuilder.InitialCatalog))
            {
                gmDbUpdaterConfig1.SqlDataBase = connectionStringBuilder.InitialCatalog;
            }

            if (connectionStringBuilder.ConnectionString.IndexOf("User ID", StringComparison.InvariantCultureIgnoreCase) != -1
                && !string.IsNullOrEmpty(connectionStringBuilder.UserID))
            {
                gmDbUpdaterConfig1.SqlLogin = connectionStringBuilder.UserID;
            }

            if (connectionStringBuilder.ConnectionString.IndexOf("Password", StringComparison.InvariantCultureIgnoreCase) != -1
                && !string.IsNullOrEmpty(connectionStringBuilder.Password))
            {
                gmDbUpdaterConfig1.SqlPassword = connectionStringBuilder.Password;
            }

            if (connectionStringBuilder.ConnectionString.IndexOf("Integrated Security", StringComparison.InvariantCultureIgnoreCase) != -1)
            {
                gmDbUpdaterConfig1.SqlTrustedConnection = connectionStringBuilder.IntegratedSecurity;
            }

            if (args != null && args.Length > 0)
            {
                for (var index = 0; index < args.Length; ++index)
                {
                    if (IsArgument(args[index], "?"))
                    {
                        Console.WriteLine("Help:\r\n/S – SQL Server name\r\n/D - Database name\r\n/U – SQL login name\r\n/P – SQL password\r\n/E – trusted connection\r\n/M - macros (one ore more pairs macro=\"value\" separated by ';')\r\n/C - path to config file\r\n/R - scripts root catalog\r\nFor example: \r\nGM_DBUpdate.exe /B /S (local) /D AxRetailPOS /U login /P password /E /M macro1=\"value1\";macro2=\"value2\" /C \"C:\\GM_DBUpdate\\GM_DBUpdate.cfg\" /R \"C:\\Scripts\"\r\n");
                        Environment.Exit(0);
                    }
                    else if (IsArgument(args[index], "S") && index < args.Length - 1)
                    {
                        gmDbUpdaterConfig1.SqlServer = args[index + 1];
                    }
                    else if (IsArgument(args[index], "D") && index < args.Length - 1)
                    {
                        gmDbUpdaterConfig1.SqlDataBase = args[index + 1];
                    }
                    else if (IsArgument(args[index], "E"))
                    {
                        gmDbUpdaterConfig1.SqlTrustedConnection = true;
                    }
                    else if (IsArgument(args[index], "U") && index < args.Length - 1)
                    {
                        gmDbUpdaterConfig1.SqlLogin = args[index + 1];
                    }
                    else if (IsArgument(args[index], "P") && index < args.Length - 1)
                    {
                        gmDbUpdaterConfig1.SqlPassword = args[index + 1];
                    }
                    else if (IsArgument(args[index], "C") && index < args.Length - 1)
                    {
                        gmDbUpdaterConfig1.ConfigFilePath = args[index + 1];
                    }
                    else if (IsArgument(args[index], "R") && index < args.Length - 1)
                    {
                        gmDbUpdaterConfig1.ScriptsPath = args[index + 1];
                        if (!string.IsNullOrEmpty(gmDbUpdaterConfig1.ScriptsPath))
                        {
                            gmDbUpdaterConfig1.ScriptsPath = gmDbUpdaterConfig1.ScriptsPath.Trim('"', '\'', '\\');
                        }
                        
                        if (!string.IsNullOrEmpty(gmDbUpdaterConfig1.ScriptsPath)
                            && gmDbUpdaterConfig1.ScriptsPath.Length == 2
                            && gmDbUpdaterConfig1.ScriptsPath[1] == 58)
                        {
                            var dbUpdaterConfig2 = gmDbUpdaterConfig1;
                            var str = dbUpdaterConfig2.ScriptsPath + "\\";
                            dbUpdaterConfig2.ScriptsPath = str;
                        }
                    }
                    else if (IsArgument(args[index], "M") && index < args.Length - 1)
                    {
                        var str1 = args[index + 1];
                        gmDbUpdaterConfig1.Macros.Clear();
                        if (!string.IsNullOrEmpty(str1))
                        {
                            var strArray = str1.Split(new[]{';'});
                            if (strArray != null && strArray.Length > 0)
                            {
                                foreach (var str2 in strArray)
                                {
                                    if (!str2.Contains("=")) continue;
                                    
                                    var key = str2.Split(new[]{'='})[0];
                                    var str3 = str2.Split(new[]{'='})[1].Trim('"', '\'');
                                    gmDbUpdaterConfig1.Macros.Add(key, str3);
                                }
                            }
                        }
                    }
                }
            }

            connectionStringBuilder.DataSource = gmDbUpdaterConfig1.SqlServer;
            connectionStringBuilder.InitialCatalog = gmDbUpdaterConfig1.SqlDataBase;
            if (!string.IsNullOrEmpty(gmDbUpdaterConfig1.SqlLogin))
            {
                connectionStringBuilder.UserID = gmDbUpdaterConfig1.SqlLogin;
            }

            if (!string.IsNullOrEmpty(gmDbUpdaterConfig1.SqlPassword))
            {
                connectionStringBuilder.Password = gmDbUpdaterConfig1.SqlPassword;
            }

            connectionStringBuilder.IntegratedSecurity = gmDbUpdaterConfig1.SqlTrustedConnection;
            if (connectionStringBuilder.ConnectionString.IndexOf("Pooling", StringComparison.InvariantCultureIgnoreCase)== -1)
            {
                connectionStringBuilder.Pooling = false;
            }

            if (connectionStringBuilder.ConnectionString.IndexOf("Persist Security Info", StringComparison.InvariantCultureIgnoreCase) == -1)
            {
                connectionStringBuilder.PersistSecurityInfo = false;
            }

            gmDbUpdaterConfig1.ConnectionString = connectionStringBuilder.ConnectionString;
            return gmDbUpdaterConfig1;
        }

        private static bool IsArgument(string arg, string checkArg)
        {
            return arg.Equals("/" + checkArg, StringComparison.InvariantCultureIgnoreCase)
                   || arg.Equals("-" + checkArg, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}