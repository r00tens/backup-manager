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
            var configFilePath = GetConfigFilePath();
            var configFileMap = new ExeConfigurationFileMap
            {
                ExeConfigFilename = configFilePath
            };
            var config = ConfigurationManager.OpenMappedExeConfiguration(configFileMap, ConfigurationUserLevel.None);

            foreach (var key in config.AppSettings.Settings.AllKeys)
            {
                if (key.StartsWith("ScheduledBackup"))
                {
                    var value = config.AppSettings.Settings[key].Value;
                    var backupInfo = ParseBackupInfo(value);

                    if (backupInfo != null)
                    {
                        backups.Add(backupInfo);
                    }
                }
            }

            return backups;
        }
        
        public static string GetConfigFilePath()
        {
#if DEBUG
            var directoryInfo = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory);

            for (var i = 0; i < 3 && directoryInfo != null; i++) directoryInfo = directoryInfo.Parent;

            if (directoryInfo == null) throw new InvalidOperationException("Project directory not found.");

            var projectDirectory = directoryInfo.FullName;
            return Path.Combine(projectDirectory, "BackupManager.GUI", "bin", "Debug", "BackupManager.GUI.exe.config");
#elif TRACE
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "BackupManager", "App.config");
#else
            throw new InvalidOperationException("Unsupported build configuration.");
#endif
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
