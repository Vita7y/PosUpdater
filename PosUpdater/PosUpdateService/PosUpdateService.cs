using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using NAppUpdate.Framework;
using PosUpdater;
using System.Windows.Forms;
using PosUpdater.Restart;

namespace PosUpdateService
{
    public partial class PosUpdateService : ServiceBase
    {
        private BackgroundWorker _posUpdateWorker;

        public PosUpdateService()
        {
            InitializeComponent();
        }

        /// <summary>
        ///  Started Win Service
        /// </summary>
        /// <param name="args"></param>
        protected override void OnStart(string[] args)
        {
            Start();
        }

        /// <summary>
        /// Stop Win Service
        /// </summary>
        protected override void OnStop()
        {
            Stop();
        }

        /// <summary>
        /// Started Win Service
        /// </summary>
        public void Start()
        {
            var serviceInitializerWorker = new BackgroundWorker();
            serviceInitializerWorker.DoWork += ServiceInitializerWorkerOnDoWork;
            serviceInitializerWorker.RunWorkerCompleted += LogErrorRunWorkerCompleted;
            serviceInitializerWorker.RunWorkerAsync();
        }

        /// <summary>
        /// </summary>
        public void Stop()
        {
            try
            {
                _posUpdateWorker.CancelAsync();
            }
            catch (Exception er)
            {
                er.WriteToLog(this, "Stop");
            }
        }
        
        void LogErrorRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            var worker = sender as BackgroundWorker;
            if (worker == null)
                return;
            if (e.Cancelled)
                return;

            if (e.Error != null)
                LogManager.CurrentLogger.Error(e.Error);
        }


        void ServiceInitializerWorkerOnDoWork(object sender, DoWorkEventArgs e)
        {
            LogManager.CurrentLogger.Info("Initializer started");

            try
            {
                _posUpdateWorker = new BackgroundWorker { WorkerSupportsCancellation = true };
                _posUpdateWorker.DoWork += UpdaterDoWork;
                _posUpdateWorker.RunWorkerCompleted += LogErrorRunWorkerCompleted;
                _posUpdateWorker.RunWorkerAsync();
            }
            catch (Exception er)
            {
                er.WriteToLog(this, "ServiceInitializerWorkerOnDoWork");
            }

            LogManager.CurrentLogger.Info("Initializer finished");
        }

        private void UpdaterDoWork(object sender, DoWorkEventArgs e)
        {
            var worker = sender as BackgroundWorker;

            while (true)
            {
                try
                {
                    try
                    {
                        LogManager.CurrentLogger.Info("Updater do work.");
                        UpdateManager.Instance.BeforeUpdateHandler+= BeforeUpdateHandler;
                        var posUpdater = new PosUpdater.PosUpdater();
                        posUpdater.ComplitUpdate += posUpdater_ComplitUpdate;
                        posUpdater.Update();
                    }
                    catch (Exception er)
                    {
                        er.WriteToLog(this, "UpdaterDoWork");
                    }   

                    if (worker != null && worker.CancellationPending)
                        return;

                }
                catch (Exception er)
                {
                    er.WriteToLog(this, "UpdaterDoWork");
                }

                Thread.Sleep(Properties.Settings.Default.TimeOut);
            }
        }

        private void BeforeUpdateHandler(object sender, EventArgs eventArgs)
        {
            if (PosUpdaterConfig.Instance.RestartRsc)
            {
                LogManager.CurrentLogger.Info("Stop RSC Begin");
                var rsc = new RestartRsc();
                rsc.Stop();
                LogManager.CurrentLogger.Info("Stop RSC End");
            }

            if (PosUpdaterConfig.Instance.BackupMsSqlDb)
            {
                LogManager.CurrentLogger.Info("Backup DB Start");
                BackupMsSqlDb.Backup();
            }
        }

        private static void posUpdater_ComplitUpdate(object sender, EventArgs e)
        {
            if (sender == null) return;
            
            var updater = sender as UpdateManager;
            if (updater == null) return;

            try
            {
                if (updater.State != UpdateManager.UpdateProcessState.AppliedSuccessfully) 
                    return;
                //TODO: Some action on end update

                //TODO: Run Script to SQL Server
                var path = Path.Combine(Application.StartupPath, "SQL");

                LogManager.CurrentLogger.Info("Update DB Begin");
                PosDbUpdater.DbUpdater.Update(PosUpdaterConfig.Instance.ConnectionString, path);
                LogManager.CurrentLogger.Info("Update DB End");


                if (PosUpdaterConfig.Instance.RestartRsc)
                {
                    LogManager.CurrentLogger.Info("Start RSC Begin");
                    var rsc = new RestartRsc();
                    rsc.Start();
                    LogManager.CurrentLogger.Info("Satrt RSC End");
                }
            }
            catch (Exception er)
            {
                er.WriteToLog("PosUpdateService.PosUpdateService.Updater_ComplitUpdate");
            }
            finally
            {
                updater.CleanUp();
            }
        }

    }
}
