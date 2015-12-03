using System;
using System.IO;
using System.Windows.Forms;
using NAppUpdate.Framework;
using NAppUpdate.Framework.Sources;
using NAppUpdate.Framework.Common;
using PosUpdater.Restart;

namespace PosUpdater
{
    public class PosUpdater
    {

        public event EventHandler ComplitUpdate;

        public PosUpdater()
        {
            // UpdateManager initialization
            var updManager = UpdateManager.Instance;
            updManager.BeforeUpdateHandler += BeforeUpdateHandler;
            updManager.BeforeExitUpdateHandler+= BeforeExitUpdateHandler;
            updManager.AfterUpdateHandler += AfterUpdateHandler;
            ComplitUpdate += PosUpdater_ComplitUpdate;
            updManager.UpdateSource = new UncSource(); //new SimpleWebSource( /* update feed URL */);
            updManager.Config.TempFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "POS\\Updates");
            
            // If you don't call this method, the updater.exe will continually attempt to connect the named pipe and get stuck.
            // Therefore you should always implement this method call.
            updManager.ReinstateIfRestarted();

        }

        void PosUpdater_ComplitUpdate(object sender, EventArgs e)
        {
            var updater = sender as UpdateManager;
            if(updater != null && updater.State == UpdateManager.UpdateProcessState.AppliedSuccessfully)
                AfterUpdateHandler(sender, EventArgs.Empty);
        }

        private void BeforeExitUpdateHandler(object sender, EventArgs eventArgs)
        {
            //AfterUpdateHandler(sender, eventArgs);
        }

        private void AfterUpdateHandler(object sender, EventArgs eventArgs)
        {
            if (PosUpdaterConfig.Instance.RestartVisitCounter)
            {
                var vc = new RestartVisitCounter();
                vc.Start();
            }
        }

        private void BeforeUpdateHandler(object sender, EventArgs eventArgs)
        {
            if (PosUpdaterConfig.Instance.RestartPos)
            {
                var pos = new RestartPos();
                pos.Stop();
            }
            if (PosUpdaterConfig.Instance.RestartVisitCounter)
            {
                var vc = new RestartVisitCounter();
                vc.Stop();
            }

            var bat = new RunScriptFile("update.bat");
            bat.Start();
        }

        public void Update()
        {
//#if DEBUG
//            MessageBox.Show(@"Connect to trace.");
//#endif

            if (PosUpdaterConfig.Instance.NotUpdate)
                return;

            var bat = new RunScriptFile("beforeUpdate.bat");
            bat.Start();

            //ClearTmpDirectory();

            if (PosUpdaterConfig.Instance.LoadFromDataBase)
            {
                var posConfig = PosParameters.LoadPosConfig();
                if (!string.IsNullOrEmpty(posConfig.TerminalId)
                    || !posConfig.StoreId.Equals("0000"))
                {
                    LoadFromDb.LoadUpdateFromDb();
                }
            }

            if (!File.Exists(PosUpdaterConfig.Instance.FeedFilePath))
                throw new NullReferenceException(string.Format("Не найден конфигурационный файл {0}", PosUpdaterConfig.Instance.FeedFilePath));
            try
            {
                var feedXml = File.ReadAllText(PosUpdaterConfig.Instance.FeedFilePath);
                IUpdateSource feedSource = new MemorySource(feedXml);
                CheckForUpdates(feedSource);
            }
            catch (Exception er)
            {
                er.WriteToLog(this, "Update");
                throw;
            }
        }

        private void CheckForUpdates(IUpdateSource source)
        {
            // Get a local pointer to the UpdateManager instance
            var updManager = UpdateManager.Instance;

            // Only check for updates if we haven't done so already
            if (updManager.State != UpdateManager.UpdateProcessState.NotChecked)
            {
                LogManager.CurrentLogger.Info("Update process has already initialized; current state: " + updManager.State);
                if (ComplitUpdate != null)
                    ComplitUpdate(UpdateManager.Instance, EventArgs.Empty);
                return;
            }

            try
            {
                // Check for updates - returns true if relevant updates are found (after processing all the tasks and
                // conditions)
                // Throws exceptions in case of bad arguments or unexpected results
                updManager.CheckForUpdates(source);
            }
            catch (Exception ex)
            {
                if (ex is NAppUpdateException)
                {
                    // This indicates a feed or network error; ex will contain all the info necessary
                    // to deal with that
                    ex.WriteToLog(this, "CheckForUpdates");
                }
                else
                {
                    ex.WriteToLog(this, "CheckForUpdates");
                }
                return;
            }


            if (updManager.UpdatesAvailable == 0)
            {
                LogManager.CurrentLogger.Info("Software is up to date");
                if (ComplitUpdate != null)
                    ComplitUpdate(UpdateManager.Instance, EventArgs.Empty);
                return;
            }

            updManager.BeginPrepareUpdates(OnPrepareUpdatesCompleted, null);
        }

        private void OnPrepareUpdatesCompleted(IAsyncResult asyncResult)
        {
            try
            {
                try
                {
                    ((UpdateProcessAsyncResult) asyncResult).EndInvoke();
                }
                catch (Exception ex)
                {
                    ex.WriteToLog(this, "OnPrepareUpdatesCompleted");
                    return;
                }

                // Get a local pointer to the UpdateManager instance
                var updManager = UpdateManager.Instance;

                // This is a synchronous method by design, make sure to save all user work before calling
                // it as it might restart your application
                try
                {
                    updManager.ApplyUpdates(
                        PosUpdaterConfig.Instance.RelaunchApplication, 
                        PosUpdaterConfig.Instance.UpdaterDoLogging, 
                        PosUpdaterConfig.Instance.UpdaterShowConsole,
                        !PosUpdaterConfig.Instance.UpdaterCantExit);
                }
                catch (Exception ex)
                {
                    ex.WriteToLog(this, "OnPrepareUpdatesCompleted");
                }
            }
            finally
            {
                if (ComplitUpdate != null)
                    ComplitUpdate(UpdateManager.Instance, EventArgs.Empty);
            }
        }

        private void ClearTmpDirectory()
        {
            string tempfolder = Path.GetTempPath();
            foreach (var filePath in Directory.GetFiles(tempfolder, "*.*", SearchOption.AllDirectories))
            {
                try
                {
                    FileInfo currentFile = new FileInfo(filePath);
                    currentFile.Delete();
                }
                catch {}
            }
        }
    }
}