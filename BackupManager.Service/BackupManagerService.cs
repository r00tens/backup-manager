using System;
using System.Collections.Generic;
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
        private readonly Dictionary<string, Timer> _backupTimers;
        private FileSystemWatcher _configWatcher;
        private Timer _debounceTimer;
        private const int DebounceInterval = 500;
        
        public BackupManagerService()
        {
            InitializeComponent();
            
            _configManager = new AppConfigManager();
            _backupService = new BackupService();
            _eventLog = new EventLog();
            _backupTimers = new Dictionary<string, Timer>();
        }
        
        protected override void OnStart(string[] args)
        {
            InitializeEventLog();
            InitializeFileSystemWatcher();
            ScheduleNextBackups();
        }
        
        protected override void OnStop()
        {
            foreach (var timer in _backupTimers.Values)
            {
                timer?.Stop();
                timer?.Dispose();
            }
            
            _backupTimers.Clear();
            _configWatcher?.Dispose();
            _debounceTimer?.Dispose();
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
            var configFilePath = AppConfigManager.GetConfigFilePath();
            var directory = Path.GetDirectoryName(configFilePath);
            var fileName = Path.GetFileName(configFilePath);
            
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
            {
                throw new InvalidOperationException("Config file path is invalid.");
            }

            _configWatcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite
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
            if (_debounceTimer == null)
            {
                _debounceTimer = new Timer(DebounceInterval);
                _debounceTimer.Elapsed += DebounceTimerElapsed;
                _debounceTimer.AutoReset = false;
            }

            _debounceTimer.Stop();
            _debounceTimer.Start();
        }
        
        private void DebounceTimerElapsed(object sender, ElapsedEventArgs e)
        {
            LogBackupEvent($"Config updated.");
            ScheduleNextBackups();
        }
        
        private void ScheduleNextBackups()
        {
            var scheduledBackups = _configManager.GetScheduledBackups();
            var activeBackupNames = new HashSet<string>();

            foreach (var backup in scheduledBackups)
            {
                activeBackupNames.Add(backup.Name);
                
                if (_backupTimers.TryGetValue(backup.Name, out var backupTimer))
                {
                    backupTimer.Interval = int.Parse(backup.Schedule) * 60000;
                }
                else
                {
                    var interval = int.Parse(backup.Schedule) * 60000;
                    var timer = new Timer(interval);
                    timer.Elapsed += (s, e) => PerformBackup(backup);
                    timer.Start();
                    _backupTimers.Add(backup.Name, timer);
                }
            }
            
            var removedBackups = new List<string>();
            
            foreach (var backupName in _backupTimers.Keys)
            {
                if (!activeBackupNames.Contains(backupName))
                {
                    _backupTimers[backupName].Stop();
                    _backupTimers[backupName].Dispose();
                    removedBackups.Add(backupName);
                }
            }
            
            foreach (var backupName in removedBackups)
            {
                _backupTimers.Remove(backupName);
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
