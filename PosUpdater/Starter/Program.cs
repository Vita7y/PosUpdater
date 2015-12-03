using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using NAppUpdate.Framework;
using PosUpdater;

namespace Starter
{
    internal static class Program
    {

        private static readonly string PosPath = Path.Combine(Application.StartupPath, "POS.exe");
        private static bool _isClose;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (var splash = new PosUpdateScreen())
            {
                splash.Show();

                if (PosUpdaterConfig.Instance.RestartPos && !File.Exists(PosPath))
                {
                    LogManager.CurrentLogger.Error("Не найдено приложение {0}", PosPath);
                    MessageBox.Show(string.Format("Не найдено приложение {0}", PosPath), @"POS",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error);
                    return;
                }

                try
                {
                    //Обновление ФС
                    var posUpdater = new PosUpdater.PosUpdater();
                    posUpdater.ComplitUpdate += posUpdater_ComplitUpdate;
                    posUpdater.Update();
                }
                catch (Exception er)
                {
                    er.WriteToLog("Starter.Program.Main");
                    MessageBox.Show(
                        string.Format("Ошибка при запуске проверки обновления.\r\nТекст ошибки: {0}", er.Message),
                        @"POS",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                if (!_isClose)
                {
                    Application.Run(new PosUpdateScreen());
                }
            }
        }

        private static void posUpdater_ComplitUpdate(object sender, EventArgs e)
        {
            try
            {
                if (sender == null)
                {
                    MessageBox.Show(@"Произошла ошибка при обновлении, обратитесь к системному администратору.", @"POS",
                                    MessageBoxButtons.OK, 
                                    MessageBoxIcon.Error);
                    return;
                }

                if (PosUpdaterConfig.Instance.RestartPos)
                    Process.Start(PosPath);
            }
            finally
            {
                _isClose = true;
                Application.Exit();
            }
        }

    }
}
