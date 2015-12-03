using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using NLog;

namespace PosDbUpdater
{
    public class DbUpdaterEngine : IDisposable
    {

        private SqlCommand _command;
        private SqlConnection _connection;

        public DbUpdaterEngine(string connectionString)
        {
            _connection = new SqlConnection(connectionString);
            _connection.Open();
            _command = _connection.CreateCommand();
        }

        public void Dispose()
        {
            if (_command != null)
            {
                _command.Dispose();
                _command = null;
            }

            if (_connection == null)
                return;
            
            if (_connection.State != ConnectionState.Closed)
                _connection.Close();
            
            _connection.Dispose();
            _connection = null;
        }

        private void Execute(string fileName, string fileExtension, string commandText, bool isCre)
        {
            var server = new Server(new ServerConnection(_connection));
            server.ConnectionContext.InfoMessage += ConnectionContext_InfoMessage;
            server.ConnectionContext.LockTimeout = 0;
            server.ConnectionContext.StatementTimeout = 0;
            try
            {
                if (isCre)
                {
                    if (fileExtension.Equals(".CRE", StringComparison.InvariantCultureIgnoreCase)
                        && CheckTableExists(server.ConnectionContext, fileName))
                    {
                        throw new Exception(string.Format("Can't execute CRE-script: table {0} already exists", fileName));
                    }
                    
                    if (fileExtension.Equals(".UPD", StringComparison.InvariantCultureIgnoreCase)
                        && !CheckTableExists(server.ConnectionContext, fileName))
                    {
                        throw new Exception(string.Format("Can't execute UPD-script: table {0} not exists", fileName));
                    }
                }

                server.ConnectionContext.BeginTransaction();
                server.ConnectionContext.ExecuteNonQuery(commandText);
                server.ConnectionContext.CommitTransaction();
            }
            catch (Exception)
            {
                server.ConnectionContext.RollBackTransaction();
                throw;
            }
            finally
            {
                server.ConnectionContext.InfoMessage -= ConnectionContext_InfoMessage;
            }
        }

        private static void ConnectionContext_InfoMessage(object sender, SqlInfoMessageEventArgs e)
        {
            LogManager.GetCurrentClassLogger().Info(e.Message);
        }

        private bool CheckTableExists(string tableName)
        {
            var server = new Server(new ServerConnection(_connection));
            var sqlCommand = string.Format("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE [TABLE_CATALOG] = '{0}' AND [TABLE_NAME] = '{1}'",
                    server.ConnectionContext.DatabaseName, tableName);
            
            return (int) server.ConnectionContext.ExecuteScalar(sqlCommand) > 0;
        }

        private static bool CheckTableExists(ServerConnection connectionContext, string tableName)
        {
            string sqlCommand = string.Format("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE [TABLE_CATALOG] = '{0}' AND [TABLE_NAME] = '{1}'",
                    connectionContext.DatabaseName, tableName);
            return (int) connectionContext.ExecuteScalar(sqlCommand) > 0;
        }

        public IEnumerable<Exception> ProcessScripts(List<string> files, string message, Dictionary<string, string> macros, bool isCre)
        {
            var list = new List<Exception>();
            if (files != null
                && files.Count > 0)
            {
                LogManager.GetCurrentClassLogger().Info(message);
                foreach (var path1 in files)
                {
                    try
                    {
                        string path2 = path1;
                        string withoutExtension = Path.GetFileNameWithoutExtension(path1);
                        string fileExtension = Path.GetExtension(path1);
                        LogManager.GetCurrentClassLogger().Info("Processing " + withoutExtension);
                        if (isCre)
                        {
                            if (!string.IsNullOrEmpty(fileExtension))
                            {
                                path2 = path2.Remove(path2.Length - fileExtension.Length, fileExtension.Length);
                            }

                            bool flag = CheckTableExists(withoutExtension);
                            if (!flag)
                            {
                                path2 = path2 + ".cre";
                                fileExtension = ".cre";
                            }
                            else
                            {
                                path2 = path2 + ".upd";
                                fileExtension = ".upd";
                            }

                            if (!File.Exists(path2))
                            {
                                LogManager.GetCurrentClassLogger().Info(string.Format("Table {0} {1}exists, but file {2} not found", withoutExtension, flag ? "" : "don't ", path2));
                                continue;
                            }
                        }

                        LogManager.GetCurrentClassLogger().Info("Execute {0}", Path.GetFileName(path2));
                        string commandText = File.ReadAllText(path2);
                        commandText = macros.Aggregate(commandText, (current, keyValuePair) => current.Replace("<" + keyValuePair.Key + ">", keyValuePair.Value));
                        Execute(withoutExtension, fileExtension, commandText, isCre);
                        LogManager.GetCurrentClassLogger().Info("Execute {0}; Success", Path.GetFileName(path2));
                    }
                    catch (Exception ex)
                    {
                        list.Add(ex);
                        ex.WriteToLog(this, "ProcessScripts");
                    }
                }
            }
            return list;
        }

    }
}