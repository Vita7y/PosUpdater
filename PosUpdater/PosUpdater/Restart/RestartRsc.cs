using System;
using System.Diagnostics;
using NAppUpdate.Framework.Common;

namespace PosUpdater.Restart
{
    public class RestartRsc: IRestartExternApp
    {
        public bool Start()
        {
            try
            {
                var psi = new ProcessStartInfo("sc", "start " + PosUpdaterConfig.Instance.RcsServiceName)
                {
                    Domain = RestartParameters.RscUserDomen,
                    UserName = RestartParameters.RscUser,
                    Password = RestartParameters.RscPass(),
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
            try
            {
                var psi = new ProcessStartInfo("sc", "stop " + PosUpdaterConfig.Instance.RcsServiceName)
                {
                    Domain = RestartParameters.RscUserDomen,
                    UserName = RestartParameters.RscUser,
                    Password = RestartParameters.RscPass(),
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false
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