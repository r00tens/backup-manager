namespace BackupManager.Library
{
    public class ScheduledBackup
    {
        public string BackupKey { get; set; } 
        public string Name { get; set; }
        public string SourcePaths { get; set; }
        public string DestinationPath { get; set; }
        public string BackupType { get; set; }
        public string Schedule { get; set; }
    }
}
