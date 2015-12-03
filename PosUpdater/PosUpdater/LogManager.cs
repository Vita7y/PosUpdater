using System;
using System.Data;
using System.Data.SqlClient;
using NLog;
using NLog.Targets;

namespace PosUpdater
{
    /// <summary>
    /// </summary>
    public static class LogManager
    {
        static LogManager()
        {
            try
            {
                CurrentLogger = NLog.LogManager.GetLogger("DB");
            }
            catch (Exception)
            {
                CurrentLogger = NLog.LogManager.GetCurrentClassLogger();
            }
        }

        public static void WriteToLog(this Exception error)
        {
            CurrentLogger.Error(error);
        }
        public static void WriteToLog(this Exception error, object sender)
        {
            CurrentLogger.Error("[{0}]: {1}", sender, error);
        }
        public static void WriteToLog(this Exception error, object sender, string message)
        {
            CurrentLogger.Error("[{0}]: {1}| {2}", sender, message, error);
        }

        public static void WriteToLog(object sender, string message)
        {
            CurrentLogger.Info("[{0}]: {1}", sender, message);
        }

        public static Logger CurrentLogger { get; private set; }

    }

    [Target("PosIsLog")]
    public sealed class PosIsLogTarget : TargetWithLayout
    {
        public PosIsLogTarget()
        {
            PosConfig = PosUpdaterConfig.LoadPosConfig();
            
            LogCommand = new SqlCommand(
                            @"INSERT INTO [dbo].[POSISLOG] 
	                            (LOGDATE,
                                CODEUNIT,
	                            LOGLEVEL,
	                            LOGSTRING,
	                            STOREID,
	                            TERMINALID,
	                            DURATIONINMILLISEC,
	                            DATAAREAID,
	                            REPLICATIONCOUNTER,
	                            APPLICATIONID) 
                            VALUES(GETDATE(), @source, @logLevel, @logString, @storeId, @terminalId, 0, @dataAreaId, 0, 1)");
            LogCommand.Parameters.Add(new SqlParameter("@source", typeof (PosIsLogTarget).ToString()));
            LogCommand.Parameters.Add(new SqlParameter("@logLevel", SqlDbType.Int));
            LogCommand.Parameters.Add(new SqlParameter("@logString", SqlDbType.NVarChar));
            LogCommand.Parameters.Add(new SqlParameter("@storeId", PosConfig.StoreId));
            LogCommand.Parameters.Add(new SqlParameter("@terminalId", PosConfig.TerminalId));
            LogCommand.Parameters.Add(new SqlParameter("@dataAreaId", PosConfig.DataAreaId));
        }

        public PosUpdaterConfig.PosConfig PosConfig { get; private set; }

        public string SqlConnect
        {
            get
            {
                return PosConfig.ConnectString;
            }
            set
            {
                PosConfig.ConnectString = value;
            }
        }

        public SqlCommand LogCommand { get; private set; }

        private int ParseLogLevel(LogEventInfo logEvent)
        {
            switch (logEvent.Level.Name.ToLower())
            {
                case "trace":
                case "debug":
                case "info":
                    return 0;
                case "warn":
                    return 1;
                case "error":
                    return 2;
                case "fatal":
                    return 3;
                default:
                    return -1;
            }
        }

        private string GetLogMessage(LogEventInfo logEvent)
        {
            var res = logEvent.FormattedMessage;
            if (logEvent.Exception != null)
                res += "| Exception: " + logEvent.Exception;
            return res;
        }

        protected override void Write(LogEventInfo logEvent)
        {
            SendTheMessageToRemoteHost(ParseLogLevel(logEvent), GetLogMessage(logEvent));
        }

        private void SendTheMessageToRemoteHost(int level, string logMessage)
        {
            try
            {
                if (string.IsNullOrEmpty(PosConfig.ConnectString)) return;
                using (var sql = new SqlConnection(PosConfig.ConnectString))
                {
                    sql.Open();
                    LogCommand.Connection = sql;
                    LogCommand.Parameters["@logLevel"].Value = level;
                    LogCommand.Parameters["@logString"].Value = logMessage;
                    LogCommand.ExecuteNonQuery();
                }
            }
            catch (Exception er)
            {
                var logger = NLog.LogManager.GetLogger("fatal");
                if (logger != null) logger.Log(LogLevel.Error, "Write to DB error. ", er);
            }
        }
    } 
}