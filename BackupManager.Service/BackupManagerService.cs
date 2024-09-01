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
        private readonly Dictionary<string, Timer> _backupTimers;
        private readonly EventLogger _eventLogger;
        public FileSystemWatcher ConfigWatcher;
        private Timer _debounceTimer;
        private const int DebounceInterval = 500;
        
        public BackupManagerService()
        {
            InitializeComponent();
            
            _configManager = new AppConfigManager();
            _backupService = new BackupService();
            _eventLogger = new EventLogger("BackupManagerService");
            _backupTimers = new Dictionary<string, Timer>();
        }
        
        protected override void OnStart(string[] args)
        {
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
            ConfigWatcher?.Dispose();
            _debounceTimer?.Dispose();
        }

        private void InitializeFileSystemWatcher(bool isTest = false)
        {
            var configFilePath = AppConfigManager.GetConfigFilePath(isTest);
            var directory = Path.GetDirectoryName(configFilePath);
            var fileName = Path.GetFileName(configFilePath);
            
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
            {
                throw new InvalidOperationException("Config file path is invalid.");
            }

            ConfigWatcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite
            };
            
            ConfigWatcher.Error += (s, e) =>
            {
                _eventLogger.LogEvent($"FileSystemWatcher error: {e.GetException().Message}", EventLogEntryType.Error);
            };

            ConfigWatcher.Changed += OnConfigChanged;
            ConfigWatcher.EnableRaisingEvents = true;
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
            _eventLogger.LogEvent($"Config updated.");
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
                
                _eventLogger.LogBackupResult(backup, true);
            }
            catch (Exception ex)
            {
                _eventLogger.LogBackupResult(backup, false, ex.Message);
            }
        }
    }
}
