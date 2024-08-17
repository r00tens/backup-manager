using System;

namespace BackupManager.Library
{
    public class BackupResult
    {
        public DateTime BackupDate { get; set; }
        public string BackupName { get; set; }
        public string BackupType { get; set; } // "Folder" or "ZIP"
        public int TotalFiles { get; set; }
        public int TotalFolders { get; set; }
        public long TotalSize { get; set; }

        public string FormatSize()
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = TotalSize;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        public override string ToString()
        {
            return $"date: {BackupDate}, name: {BackupName}, type: {BackupType}, files: {TotalFiles}, folders: {TotalFolders}, size: {FormatSize()}";
        }
    }
}
