using System;
using System.Diagnostics;
using System.IO;
using NAppUpdate.Framework.Common;

namespace PosUpdater.Restart
{
    public class RunScriptFile : IRestartExternApp
    {
        public RunScriptFile()
        {
            FileName = "update.bat";
        }
        public RunScriptFile(string scriptFileName)
        {
            FileName = scriptFileName;
        }
        
        public string FileName { get; set; }

        public bool Start()
        {
            try
            {
                var path = Path.GetDirectoryName(PosUpdaterConfig.Instance.FeedFilePath);
                if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(FileName))
                    return false;
                var fullPath = Path.Combine(path, FileName);
                if (!File.Exists(fullPath))
                    return false;

                var psi = new ProcessStartInfo(fullPath, " >> script.log")
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                var proc = Process.Start(psi);
                proc.WaitForExit();
            }
            catch (Exception er)
            {
                er.WriteToLog(this, "Start");
                return false;
            }
            return true;
        }

        public bool Stop()
        {
            throw new System.NotImplementedException();
        }

        public bool Restart()
        {
            return Stop() && Start();
        }
    }
}