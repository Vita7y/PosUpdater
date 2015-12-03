using System;
using System.Data;
using System.Data.SqlClient;
using System.Text;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;

namespace PosDbUpdater
{
    public static class CommonSql
    {
        public static string RunSqlScript(string connectString, string sql)
        {
            if(string.IsNullOrEmpty(connectString))
                throw new ArgumentNullException("connectString");
            if (string.IsNullOrEmpty(connectString))
                throw new ArgumentNullException("sql");

            var output = new StringBuilder();
            output.AppendLine("Connect string: " + connectString);
            output.AppendLine("Start execute sql script...");
            var timeCount = new System.Diagnostics.Stopwatch();
            timeCount.Start();

            try
            {
                using (var conn = new SqlConnection(connectString))
                {
                    conn.Open();
                    var comm = new SqlCommand(sql.Replace("GO", ""), conn) {CommandTimeout = 300};
                    var res = comm.ExecuteNonQuery();
                    output.AppendLine("Execute OK. Rows affected: " + res);
                }
            }
            catch (Exception er)
            {
                output.AppendLine(er.ToString());
            }
            finally
            {
                timeCount.Stop();
                output.AppendLine(@"Elapsed time: " + timeCount.Elapsed);//.ToString("HH:mm:ss.ffff"));
                output.AppendLine("----");
            }
            return output.ToString();
        }

        public static string SmoExecuteSqlScript(string connectString, string sql, SqlInfoMessageEventHandler sqlInfo = null, Action<object, string> sqlServerInfo = null)
        {
            var server = new Server(new ServerConnection(new SqlConnection(connectString)));
            if (sqlInfo != null) server.ConnectionContext.InfoMessage += sqlInfo;
            if (sqlServerInfo != null) server.ConnectionContext.ServerMessage += (sender, args) =>  sqlServerInfo(sender, args.Error.ToString());
            server.ConnectionContext.LockTimeout = 0;
            server.ConnectionContext.StatementTimeout = 0;

            var output = new StringBuilder();
            output.AppendLine("Connect string: " + connectString);
            output.AppendLine("Start execute sql script...");
            var timeCount = new System.Diagnostics.Stopwatch();
            timeCount.Start();
            
            try
            {
                server.ConnectionContext.BeginTransaction();
                var res = server.ConnectionContext.ExecuteNonQuery(sql);
                server.ConnectionContext.CommitTransaction();

                output.AppendLine("Execute OK. Rows affected: " + res);
                return output.ToString();
            }
            catch (Exception er)
            {
                server.ConnectionContext.RollBackTransaction();
                output.AppendLine(er.ToString());
            }
            finally
            {
                if (sqlInfo != null) server.ConnectionContext.InfoMessage -= sqlInfo;
                //if (sqlServerInfo != null) server.ConnectionContext.ServerMessage -= sqlServerInfo;
            }
            return output.ToString();
        }

        public static DataSet SmoExecuteSqlQuery(string connectString, string sql, out string info, SqlInfoMessageEventHandler sqlInfo = null, Action<object, string> sqlServerInfo = null)
        {
            var server = new Server(new ServerConnection(new SqlConnection(connectString)));
            if (sqlInfo != null) server.ConnectionContext.InfoMessage += sqlInfo;
            if (sqlServerInfo != null) server.ConnectionContext.ServerMessage += (sender, args) => sqlServerInfo(sender, args.Error.ToString());
            server.ConnectionContext.LockTimeout = 0;
            server.ConnectionContext.StatementTimeout = 0;

            var output = new StringBuilder();
            output.AppendLine("Connect string: " + connectString);
            output.AppendLine("Start execute sql script...");
            var timeCount = new System.Diagnostics.Stopwatch();
            timeCount.Start();

            try
            {
                server.ConnectionContext.BeginTransaction();
                var res = server.ConnectionContext.ExecuteWithResults(sql);
                server.ConnectionContext.CommitTransaction();

                output.AppendLine("Execute OK. Rows affected: " + res);
                info = output.ToString();
                return res;
            }
            catch (Exception er)
            {
                server.ConnectionContext.RollBackTransaction();
                output.AppendLine(er.ToString());
            }
            finally
            {
                if (sqlInfo != null) server.ConnectionContext.InfoMessage -= sqlInfo;
                //if (sqlServerInfo != null) server.ConnectionContext.ServerMessage -= sqlServerInfo;
            }
            info = output.ToString();
            return null;
        }

    }
}