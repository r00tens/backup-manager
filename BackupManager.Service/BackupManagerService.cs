using System;
using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using System.Timers;
using BackupManager.Library;

namespace BackupManager.Service
{
    public partial class BackupManagerService : ServiceBase
    {
        private readonly AppConfigManager _configManager;
        private readonly BackupService _backupService;
        private readonly EventLog _eventLog;
        private FileSystemWatcher _configWatcher;
        private Timer _backupTimer;
        
        public BackupManagerService()
        {
            InitializeComponent();
            
            _configManager = new AppConfigManager();
            _backupService = new BackupService();
            _eventLog = new EventLog();
        }
        
        protected override void OnStart(string[] args)
        {
            InitializeEventLog();
            InitializeFileSystemWatcher();
            ScheduleNextBackup();
        }
        
        protected override void OnStop()
        {
            _backupTimer?.Stop();
            _configWatcher?.Dispose();
        }
        
        private void InitializeEventLog()
        {
            if (!EventLog.SourceExists("BackupManagerService"))
            {
                EventLog.CreateEventSource("BackupManagerService", "Application");
            }
            
            _eventLog.Source = "BackupManagerService";
            _eventLog.Log = "Application";
        }
        
        private void InitializeFileSystemWatcher()
        {
            var configFilePath = $@"C:\Users\{Environment.UserName}\Desktop\my-projects\backup-manager\BackupManager.GUI\bin\Debug\BackupManager.GUI.exe.Config";
            
            var directory = Path.GetDirectoryName(configFilePath);
            var fileName = Path.GetFileName(configFilePath);
            
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
            {
                throw new InvalidOperationException("Config file path is invalid.");
            }

            _configWatcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite 
                               | NotifyFilters.FileName
                               | NotifyFilters.LastAccess 
                               | NotifyFilters.CreationTime
                               | NotifyFilters.Size
                               | NotifyFilters.Attributes
                               | NotifyFilters.Security
            };
            
            _configWatcher.Error += (s, e) =>
            {
                LogBackupEvent($"FileSystemWatcher error: {e.GetException().Message}", EventLogEntryType.Error);
            };

            _configWatcher.Changed += OnConfigChanged;
            _configWatcher.EnableRaisingEvents = true;
        }
        
        private void OnConfigChanged(object sender, FileSystemEventArgs e)
        {
            LogBackupEvent($"Config updated. {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            
            ScheduleNextBackup();
        }
        
        private void ScheduleNextBackup()
        {
            var scheduledBackups = _configManager.GetScheduledBackups();
            
            foreach (var backup in scheduledBackups)
            {
                var interval = int.Parse(backup.Schedule) * 60000;
                
                _backupTimer?.Dispose();
                _backupTimer = new Timer(interval);
                _backupTimer.Elapsed += (s, e) => PerformBackup(backup);
                _backupTimer.Start();
            }
        }
        
        private void PerformBackup(ScheduledBackup backup)
        {
            var sourcePaths = backup.SourcePaths.Split(',');
            var destinationPath = Path.Combine(backup.DestinationPath, $"{backup.Name}-{DateTime.Now:yyyyMMdd-HHmmss}");

            try
            {
                if (backup.BackupType.Equals("zip", StringComparison.OrdinalIgnoreCase))
                {
                    _backupService.CreateBackup(sourcePaths, $"{destinationPath}.zip", true);
                }
                else
                {
                    _backupService.CreateBackup(sourcePaths, destinationPath);
                }
                
                LogScheduledBackupResult(backup, true);
            }
            catch (Exception ex)
            {
                LogScheduledBackupResult(backup, false, ex.Message);
            }
        }
        
        private static void LogBackupEvent(string message, EventLogEntryType entryType = EventLogEntryType.Information)
        {
            using (var eventLog = new EventLog("Application"))
            {
                eventLog.Source = "BackupManagerService";
                eventLog.WriteEntry(message, entryType);
            }
        }
        
        private static void LogScheduledBackupResult(ScheduledBackup backup, bool success, string errorMessage = null)
        {
            string message = success
                ? $"Backup {backup.Name} completed successfully."
                : $"Backup {backup.Name} failed: {errorMessage}";

            LogBackupEvent(message, success ? EventLogEntryType.Information : EventLogEntryType.Error);
        }
    }
}
