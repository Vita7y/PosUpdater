using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using PetaPoco;
using Zeta;

namespace PosUpdater
{
    public static class LoadFromDb
    {
        private const string Fmt = @"10.0
1
1       SQLIMAGE            0       0       """"   1     File_          """"
";
        public enum UpdateStatus
        {
            NotApprove = 0, //000
            Approve = 1,    //001
            Update = 3,     //011
            ErrorUpdate = 5 //101
        }

        public static bool LoadUpdateFromDb()
        {
            try
            {
                LogManager.WriteToLog(typeof (LoadFromDb), "Begin update from DB");
                var config = PosUpdaterConfig.LoadPosConfig();
                if (config.StoreId.Equals("0000"))
                    throw new ArgumentException(@"POS.exe.config is not exist!");
                LogManager.WriteToLog(typeof (LoadFromDb), "DB= " + config.ConnectString);

                var verFromDb = GetVersions(config.ConnectString, new []{1,3});
                if (verFromDb == null || verFromDb.Count == 0)
                    return false;
                LogManager.WriteToLog(typeof (LoadFromDb), "Load Versions from DB");

                var param = PosParameters.Load();
                var needToUpdate =
                    verFromDb.Where(am => am.ReleaseVersion > param.PosVersion).OrderBy(pm => pm.ReleaseVersion);
                LogManager.WriteToLog(typeof (LoadFromDb), "Need too update= " + needToUpdate.Count());

                var tmp = Path.GetTempPath();
                //SqlConnection openConnect;
                //TODO: Доделать проверку на количество пользователей!!!!!!!
                //var countConnect = GetOpenConnection(config.ConnectString, out openConnect);
                //if (countConnect > 3 && verFromDb.FirstOrDefault(am => am.APPROVED == 1) != null)
                //    throw new OperationCanceledException(
                //        "Обновление отменено, необходимо т.к. к базе данных есть подключения.");

                //Обновление должно быть атомарным, одно обновление за раз
                var updItem = needToUpdate.FirstOrDefault();
                if (updItem == null)
                    return true;
                //foreach (var updItem in needToUpdate)
                {
                    var fn = ExportFileFormDataBase(updItem.RELEASEID, tmp, config.ConnectString);
                    
                    LogManager.WriteToLog(typeof(LoadFromDb), "Downloaded file from DB= " + fn);
                    var updPath = string.Format(".\\UpdateVer\\{0}", updItem.RELEASEID);
                    UnZipFile(fn, updPath+"\\");
                    LogManager.WriteToLog(typeof(LoadFromDb), string.Format("Extract files from archive to = {0}", updPath));

                    LogManager.WriteToLog(typeof(LoadFromDb), "Start to copy to folder Update= " + updItem.RELEASEID);
                    var options = new ZetaFolderXCopyOptions
                    {
                        FilesPattern = "*.*",
                        RecurseFolders = true,
                        CopyEmptyFolders = true,
                        CopyHiddenAndSystemFiles = true,
                        OverwriteExistingFiles = true,
                        CopyOnlyIfSourceIsNewer = false,
                        FoldersPattern = "*"
                    };
                    var xc = new ZetaFolderXCopy();
                    xc.Copy(updPath, ".\\update\\", options);
                    LogManager.WriteToLog(typeof(LoadFromDb), "End to copy to folder Update= " + updItem.RELEASEID);

                    if (updItem.APPROVED == 1)
                    {
                        UpdateDataBase(updPath, config.ConnectString);
                        updItem.APPROVED = 3;

                        LogManager.WriteToLog(typeof(LoadFromDb), string.Format("Start to confirm a successful update"));
                        UpdateReleaseRowInDataBase(updItem, config.ConnectString);
                        LogManager.WriteToLog(typeof(LoadFromDb), string.Format("End to confirm a successful update"));
                    }
                }
                return true;
            }
            catch (Exception er)
            {
                er.WriteToLog(typeof (LoadFromDb));
            }
            return false;
        }

        public static string ExportFileFormDataBase(string releaseId, string pathToExportFile, string connectString)
        {
            if (string.IsNullOrEmpty(releaseId))
                throw new ArgumentNullException("releaseId");
            if (string.IsNullOrEmpty(pathToExportFile))
                throw new ArgumentNullException("pathToExportFile");
            if (string.IsNullOrEmpty(connectString))
                throw new ArgumentNullException("connectString");

            var dir = Path.GetDirectoryName(pathToExportFile);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            const string sql = "SELECT FILE_ FROM dbo.ECC_DRMPOSReleaseLine WHERE DATAAREAID='dat' AND RELEASEID='{0}' ORDER BY LINENUM";

            using (var connect = new SqlConnection(connectString))
            {
                connect.Open();
                using (var cmd = new SqlCommand(string.Format(sql, releaseId)))
                {
                    cmd.Connection = connect;
                    SqlDataReader res = cmd.ExecuteReader();

                    using (var file = File.Create(Path.GetTempFileName()))
                    {
                        while (res.Read())
                        {
                            var dat = res[0];
                            var arr = dat as byte[];
                            if (arr == null) 
                                throw new DataException("Не корректный формат данных в БД.");
                            file.Write(arr, 0, arr.Length);
                        }
                        return file.Name;
                    }
                }
            }

        }

        public static void BcpExportFileFromDataBase(string releaseId, string pathToExportFile, string connectString)
        {
            if (string.IsNullOrEmpty(releaseId))
                throw new ArgumentNullException("releaseId");
            if (string.IsNullOrEmpty(pathToExportFile))
                throw new ArgumentNullException("pathToExportFile");
            if (string.IsNullOrEmpty(connectString))
                throw new ArgumentNullException("connectString");

            var dir = Path.GetDirectoryName(pathToExportFile);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            var fmtfn = Path.Combine(dir, "fmt.fmt");
            using (var fs = File.Create(fmtfn))
            {
                fs.Write(Encoding.ASCII.GetBytes(Fmt), 0, Fmt.Length);
            }

            using (var connect = new SqlConnection(connectString))
            {
                connect.Open();
                using (
                    var cmd =
                        new SqlCommand(string.Format("exec dbo.ecc_fileExport2 @RELEASEID = '{0}', @FILEPATH = '{1}\\'",
                            releaseId,
                            dir)))
                {
                    cmd.Connection = connect;
                    cmd.ExecuteNonQuery();
                }
            }

            File.Delete(fmtfn);

        }

        public static void UnZipFile(string zipFileName, string unZipPath)
        {
            if (string.IsNullOrEmpty(zipFileName) || !File.Exists(zipFileName))
                throw new ArgumentNullException("zipFileName");
            if (string.IsNullOrEmpty(unZipPath))
                throw new ArgumentNullException("unZipPath");

            var unZipDirPath = Path.GetDirectoryName(unZipPath);
            if (!Directory.Exists(unZipDirPath))
                Directory.CreateDirectory(unZipDirPath);

            // Open an existing zip file for reading
            var zip = ZipStorer.Open(zipFileName, FileAccess.Read);

            // Read the central directory collection
            var dir = zip.ReadCentralDir();

            // Look for the desired file
            foreach (var entry in dir)
            {
                zip.ExtractFile(entry, Path.Combine(unZipDirPath, entry.FilenameInZip));
            }
            zip.Close();
        }

        public static int GetSqlConnectCount(string connectString, string dbName)
        {
            if (string.IsNullOrEmpty(connectString))
                throw new ArgumentNullException("connectString");

            using (var connect = new SqlConnection(connectString))
            {
                connect.Open();
                using (
                    var cmd =
                        new SqlCommand(
                            string.Format("select count(1) from master..sysprocesses where dbid=db_id('{0}');",
                                dbName)))
                {
                    cmd.Connection = connect;
                    return cmd.ExecuteScalar() is int ? (int) cmd.ExecuteScalar() : -1;
                }
            }

        }

        public static List<ECC_DRMPOSRELEASETABLE> GetVersions(string connectString, int[] approve)
        {
            if (string.IsNullOrEmpty(connectString))
                throw new ArgumentNullException("connectString");

            try
            {
                using (var db = new Database(connectString, "System.Data.SqlClient"))
                {
                    var r = approve.Select(am=>am.ToString()).Aggregate((prev, next) => prev + ", " + next);
                    var res = db.Query<ECC_DRMPOSRELEASETABLE>(
                        string.Format(@"SELECT [DATAAREAID]
                              ,[RECID]
                              ,[DESCRIPTION]
                              ,[APPROVED]
                              ,[NUMBEROFLINES]
                              ,[RELEASEID]
                          FROM [dbo].[ECC_DRMPOSRELEASETABLE]
                          WHERE [APPROVED] in ({0})", r));
                    return res.ToList();
                }
            }
            catch (Exception er)
            {
                er.WriteToLog();
                return null;
            }

        }

        public static void ExecuteSql(string sql, string connectString)
        {
            if (string.IsNullOrEmpty(sql))
                throw new ArgumentNullException("sql");
            if (string.IsNullOrEmpty(connectString))
                throw new ArgumentNullException("connectString");
            if (sql.Length == 0)
                return;

            var builder = new SqlConnectionStringBuilder(connectString) {UserID = "upd", Password = "uP9"};
            using (var connect = new SqlConnection(builder.ToString()))
            {
                connect.Open();
                var tran = connect.BeginTransaction();
                using (var cmd = connect.CreateCommand())
                {
                    try
                    {
                        LogManager.WriteToLog(typeof(LoadFromDb), "DB update started");
                        cmd.Transaction = tran;
                        cmd.CommandText = "exec ECC_ExecSql_sp @sql";
                        cmd.Parameters.Add(new SqlParameter("@sql", sql));
                        cmd.CommandTimeout = 1000;
                        cmd.ExecuteNonQuery();
                        tran.Commit();
                        LogManager.WriteToLog(typeof(LoadFromDb), "DB update end");
                    }
                    catch (Exception er)
                    {
                        tran.Rollback();
                        er.WriteToLog(typeof (LoadFromDb));
                        throw;
                    }
                }
            }
        }

        public static void UpdateDataBase(string path, string connectString)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException("path");
            if (string.IsNullOrEmpty(connectString))
                throw new ArgumentNullException("connectString");

            var fn = Path.Combine(path, "update.sql");
            if (!File.Exists(fn))
            {
                LogManager.WriteToLog(typeof(LoadFromDb), "File did'n found " + fn + " DB not updated");
                return;
            }
            ExecuteSql(File.ReadAllText(fn), connectString);
        }


        public static void UpdateReleaseRowInDataBase(ECC_DRMPOSRELEASETABLE row, string connectString)
        {
            if (string.IsNullOrEmpty(connectString))
                throw new ArgumentNullException("connectString");

            try
            {
                using (var db = new Database(connectString, "System.Data.SqlClient"))
                {
                    db.Update<ECC_DRMPOSRELEASETABLE>(" SET APPROVED = @0 WHERE RELEASEID = @1 AND DATAAREAID = @2", row.APPROVED, row.RELEASEID, row.DATAAREAID);
                }
            }
            catch (Exception er)
            {
                er.WriteToLog();
            }

        }

        public static int GetOpenConnection(string connectString, out SqlConnection openConnection)
        {
            if (string.IsNullOrEmpty(connectString))
                throw new ArgumentNullException("connectString");

            var builder = new SqlConnectionStringBuilder(connectString) { UserID = "upd", Password = "uP9" };
            openConnection = new SqlConnection(connectString);
            openConnection.Open();
            var cmd = openConnection.CreateCommand();
            cmd.CommandText = @"select count(*) from (SELECT distinct login_name FROM sys.dm_exec_sessions GROUP BY login_name) as t";
            return cmd.ExecuteScalar() is int ? (int) cmd.ExecuteScalar() : 0;
        }
    }
}