using System.Diagnostics;

namespace BackupManager.Library
{
    public class EventLogger
    {
        private readonly EventLog _eventLog;

        public EventLogger(string source, string log = "Application")
        {
            if (!EventLog.SourceExists(source))
            {
                EventLog.CreateEventSource(source, log);
            }

            _eventLog = new EventLog
            {
                Source = source,
                Log = log
            };
        }

        public void LogEvent(string message, EventLogEntryType type)
        {
            _eventLog.WriteEntry(message, type);
        }
    }
}
