using System;
using System.Data.SqlClient;
using System.IO;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using PosUpdater;

namespace PosUpdateService
{
    public class BackupMsSqlDb
    {
        public static bool Backup()
        {
            try
            {
                var connect = new SqlConnection(PosUpdaterConfig.Instance.ConnectionString);
                var server = new Server();
                server.ConnectionContext.DatabaseName = connect.Database;
                server.ConnectionContext.ConnectTimeout = 30;
                var databaseBackupFilePath = Path.Combine(server.Settings.BackupDirectory, DateTime.Now.ToString("Backup-yyyyMMdd-HHmm"));

                var backup = new Backup
                {
                    Action = BackupActionType.Database,
                    Database = connect.Database,
                    Initialize = true, // Все наборы данных бэкапа будут перезаписаны.
                    Incremental = false // Показываем, что это полный бэкап.
                };
                // Устанавливаем файл бэкапа бд.
                var deviceItem = new BackupDeviceItem(databaseBackupFilePath, DeviceType.File);
                backup.Devices.Add(deviceItem);
                backup.SqlBackup(server);
                LogManager.CurrentLogger.Info("Резервное копирование базы данных «{0}» выполнено успешно в «{1}».", 
                    connect.Database, 
                    databaseBackupFilePath);
            }
            catch (Exception er)
            {
                LogManager.CurrentLogger.Error("Резервное копирование базы данных завершилось неудачно.", er);
                return false;
            }
            return true;
        }
    }
}