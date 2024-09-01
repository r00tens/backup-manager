using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.IO;

namespace BackupManager.Library
{
    public class AppConfigManager
    {
        private readonly ExeConfigurationFileMap _configurationFileMap;

        public AppConfigManager()
        {
            var configFilePath = GetConfigFilePath();
            
            _configurationFileMap = new ExeConfigurationFileMap
            {
                ExeConfigFilename = configFilePath
            };
        }

        public string LastBackupInfo
        {           
            get => GetSetting("LastBackupInfo");
            set => SaveSetting("LastBackupInfo", value);
        }

        public string GetSetting(string key)
        {
            try
            {
                var value = ConfigurationManager.AppSettings[key];
                if (value != null) return value;
                
                var config = ConfigurationManager.OpenMappedExeConfiguration(_configurationFileMap, ConfigurationUserLevel.None);
                var setting = config.AppSettings.Settings[key];

                if (setting != null) return setting.Value;
                
                throw new KeyNotFoundException($"Key '{key}' not found in both appSettings and mapped configuration.");
            }
            catch (ConfigurationErrorsException e)
            {
                throw new InvalidOperationException("Error accessing configuration.", e);
            }
        }

        public void SaveSetting(string key, string value)
        {
            try
            {
                SaveToConfiguration(ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None), key, value);
            }
            catch
            {
                SaveToConfiguration(ConfigurationManager.OpenMappedExeConfiguration(_configurationFileMap, ConfigurationUserLevel.None), key, value);
            }
        }

        private static void SaveToConfiguration(Configuration config, string key, string value)
        {
            if (config.AppSettings.Settings[key] == null)
            {
                config.AppSettings.Settings.Add(key, value);
            }
            else
            {
                config.AppSettings.Settings[key].Value = value;
            }
    
            config.Save(ConfigurationSaveMode.Modified);
            File.SetLastWriteTime(config.FilePath, DateTime.Now);

            ConfigurationManager.RefreshSection("appSettings");
        }
        
        public void AddScheduledBackup(string backupName, string sourcePaths, string destinationPath, string backupType, string schedule)
        {
            Configuration config = ConfigurationManager.OpenMappedExeConfiguration(_configurationFileMap, ConfigurationUserLevel.None);

            string backupKey = "ScheduledBackup" + (config.AppSettings.Settings.Count + 1);
            string backupValue = $"backupKey={backupKey};backupName={backupName};sourcePaths={sourcePaths};destinationPath={destinationPath};backupType={backupType};schedule={schedule}";

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

        public static string GetConfigFilePath(bool isTest = false)
        {
#if DEBUG
            var directoryInfo = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory);

            for (var i = 0; i < 3 && directoryInfo != null; i++) directoryInfo = directoryInfo.Parent;

            if (directoryInfo == null) throw new InvalidOperationException("Project directory not found.");

            var projectDirectory = directoryInfo.FullName;

            return isTest
                ? Path.Combine(projectDirectory, "BackupManager.Tests.dll.config")
                : Path.Combine(projectDirectory, "BackupManager.GUI", "bin", "Debug", "BackupManager.GUI.exe.config");
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