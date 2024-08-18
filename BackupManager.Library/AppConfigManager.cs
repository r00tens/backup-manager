using System;
using System.Collections.ObjectModel;
using System.Configuration;
using System.IO;

namespace BackupManager.Library
{
    public class AppConfigManager
    {
        public string GetSetting(string key)
        {
            return ConfigurationManager.AppSettings[key];
        }

        public void SaveSetting(string key, string value)
        {
            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings[key].Value = value;
            config.Save(ConfigurationSaveMode.Modified);
            File.SetLastWriteTime(config.FilePath, DateTime.Now);

            ConfigurationManager.RefreshSection("appSettings");
        }

        public string LastBackupInfo
        {
            get => GetSetting("LastBackupInfo");
            set => SaveSetting("LastBackupInfo", value);
        }

        public void AddScheduledBackup(string backupName, string sourcePaths, string destinationPath, string backupType, string schedule)
        {
            string backupKey = "ScheduledBackup" + (ConfigurationManager.AppSettings.Count + 1);
            string backupValue = $"backupKey={backupKey};backupName={backupName};sourcePaths={sourcePaths};destinationPath={destinationPath};backupType={backupType};schedule={schedule}";

            Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
            config.AppSettings.Settings.Add(backupKey, backupValue);
            config.Save(ConfigurationSaveMode.Modified);
            File.SetLastWriteTime(config.FilePath, DateTime.Now);

            ConfigurationManager.RefreshSection("appSettings");
        }

        public ObservableCollection<ScheduledBackup> GetScheduledBackups()
        {
            var backups = new ObservableCollection<ScheduledBackup>();

            foreach (string key in ConfigurationManager.AppSettings.AllKeys)
            {
                if (key.StartsWith("ScheduledBackup"))
                {
                    var value = ConfigurationManager.AppSettings[key];
                    var backupInfo = ParseBackupInfo(value);

                    if (backupInfo != null)
                    {
                        backups.Add(backupInfo);
                    }
                }
            }

            return backups;
        }

        private ScheduledBackup ParseBackupInfo(string backupInfo)
        {
            var parts = backupInfo.Split(';');

            if (parts.Length != 6) return null;
            
            var backupKey = parts[0].Split('=')[1];
            var backupName = parts[1].Split('=')[1];
            var sourcePaths = parts[2].Split('=')[1];
            var destinationPath = parts[3].Split('=')[1];
            var backupType = parts[4].Split('=')[1];
            var schedule = parts[5].Split('=')[1];
            
            return new ScheduledBackup
            {
                BackupKey = backupKey,
                Name = backupName,
                SourcePaths = sourcePaths,
                DestinationPath = destinationPath,
                BackupType = backupType,
                Schedule = schedule
            };
        }
    }
}
