using System;
using System.Diagnostics;
using NAppUpdate.Framework.Common;

namespace PosUpdater.Restart
{
    public class RestartPos : IRestartExternApp
    {
        public bool Start()
        {
            throw new NotImplementedException();
        }

        public bool Stop()
        {
            try
            {
                var psi = new ProcessStartInfo("taskkill", "/f /t /im pos.exe")
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = true
                };
                var proc = Process.Start(psi);
                proc.WaitForExit();
            }
            catch (Exception er)
            {
                er.WriteToLog(this, "Stop");
                return false;
            }
            return true;
        }

        public bool Restart()
        {
            return Stop() && Start();
        }
    }
}