using System;
using NLog;

namespace PosDbUpdater
{
    /// <summary>
    /// </summary>
    public static class LogHelper
    {
        public static void WriteToLog(this Exception error)
        {
            LogManager.GetCurrentClassLogger().Error(error);
        }

        public static void WriteToLog(this Exception error, object sender)
        {
            LogManager.GetCurrentClassLogger().Error("[{0}]: {1}", sender, error);
        }
        
        public static void WriteToLog(this Exception error, object sender, string message)
        {
            LogManager.GetCurrentClassLogger().Error("[{0}]: {1}| {2}", sender, message, error);
        }

        public static void WriteToLog(object sender, string message)
        {
            LogManager.GetCurrentClassLogger().Info("[{0}]: {1}", sender, message);
        }
    }
}
