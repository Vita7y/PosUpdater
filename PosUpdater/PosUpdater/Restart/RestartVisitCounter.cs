using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using NAppUpdate.Framework.Common;
using System.Windows.Forms;

namespace PosUpdater.Restart
{
    public class RestartVisitCounter: IRestartExternApp
    {
        public bool Start()
        {
            try
            {
                LogManager.WriteToLog(this, "RestartVisitCounter.Start()");
                var psi = new ProcessStartInfo()
                {
                    FileName = "sc.exe",
                    Arguments = "start VisitCounterService",
                    //WorkingDirectory = Application.StartupPath,
                    //Domain = RestartParameters.RscUserDomen,
                    //UserName = RestartParameters.RscUser,
                    //Password = RestartParameters.RscPass(),
                    Verb = "runas",
                    //WindowStyle = ProcessWindowStyle.Hidden,
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
                LogManager.WriteToLog(this, "RestartVisitCounter.Stop()");

                var psi = new ProcessStartInfo()
                {
                    FileName = "sc.exe",
                    Arguments = "stop VisitCounterService",
                    //WorkingDirectory = Application.StartupPath,
                    //Domain = RestartParameters.RscUserDomen,
                    //UserName = RestartParameters.RscUser,
                    //Password = RestartParameters.RscPass(),
                    Verb = "runas",
                    //WindowStyle = ProcessWindowStyle.Hidden,
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
            LogManager.WriteToLog(this, "RestartVisitCounter.Restart()");
            return Stop() && Start();
        }
    }
}