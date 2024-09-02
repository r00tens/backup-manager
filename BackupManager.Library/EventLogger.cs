using System.Diagnostics;
using BackupManager.Library.Models;

namespace BackupManager.Library
{
    public class EventLogger
    {
        private readonly string _eventLogSource;
        
        public EventLogger(string eventLogSource)
        {
            _eventLogSource = eventLogSource;
            
            if (!EventLog.SourceExists(_eventLogSource))
            {
                EventLog.CreateEventSource(_eventLogSource, "Application");
            }
        }

        public void LogEvent(string message, EventLogEntryType entryType = EventLogEntryType.Information)
        {
            using (var eventLog = new EventLog("Application"))
            {
                eventLog.Source = _eventLogSource;
                eventLog.WriteEntry(message, entryType);
            }
        }
        
        public void LogBackupResult(ScheduledBackup backup, bool success, string errorMessage = null)
        {
            string message = success
                ? $"Backup {backup.Name} completed successfully."
                : $"Backup {backup.Name} failed: {errorMessage}";

            LogEvent(message, success ? EventLogEntryType.Information : EventLogEntryType.Error);
        }
    }
}
