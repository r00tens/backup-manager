using System;
using System.IO;
using System.Linq;
using BackupManager.Library;
using BackupManager.Library.Enums;
using BackupManager.Library.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BackupManager.Tests
{
    [TestClass]
    public class LibraryTests
    {
        private const string TempBackupFilePath = "backup.zip";
        private const string TempRestoreFolder = "restoreFolder";
        private const string TempChecksumFilePath = "backup-checksums.txt";
        private const string TempTextFilePath = "test.txt";

        [TestMethod]
        public void FormatSize_ShouldReturnCorrectSizeFormat()
        {
            // Arrange
            var backupResult = new BackupResult { TotalSize = 1048576 }; // 1 MB

            // Act
            var formattedSize = backupResult.FormatSize();

            // Assert
            Assert.AreEqual("1 MB", formattedSize);
        }

        [TestMethod]
        public void CreateBackup_ShouldReturnCorrectBackupType()
        {
            // Arrange
            var service = new BackupService();
            string[] items = { "test.txt" };

            // Act
            var result = service.CreateBackup(items, TempBackupFilePath, true);

            // Assert
            Assert.AreEqual(BackupType.Zip, result.BackupType);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void RestoreBackup_ShouldThrowInvalidOperationException_WhenChecksumMismatch()
        {
            // Arrange
            var service = new BackupService();
            string[] items = { "test.txt" };

            // tworzenie pliku testowego
            File.WriteAllText("test.txt", @"Test content");

            // tworzenie backup w formacie zip
            service.CreateBackup(items, TempBackupFilePath, true);

            // znajdowanie ścieżki do pliku z sumami kontrolnymi
            var checksumFilePath = Path.Combine(Path.GetDirectoryName(TempBackupFilePath) ?? string.Empty,
                Path.GetFileNameWithoutExtension(TempBackupFilePath) + "-checksums.txt");

            // modyfikacja pliku sum kontrolnych, aby spowodować błąd
            var checksumLines = File.ReadAllLines(checksumFilePath).ToList();

            if (checksumLines.Count > 0)
            {
                // modyfikacja pierwszego wpisu, wprowadzając fałszywą sumę kontrolną
                checksumLines[0] = "00000000000000000000000000000000 " + checksumLines[0].Split(' ')[1];
            }

            File.WriteAllLines(checksumFilePath, checksumLines);

            // Act
            service.RestoreBackup(TempBackupFilePath, TempRestoreFolder);
        }

        [TestMethod]
        public void SaveSetting_ShouldUpdateSettingCorrectly()
        {
            // Arrange
            var configManager = new AppConfigManager();
            var originalValue = configManager.GetSetting("DefaultBackupPath");
            const string newValue = "newPath";

            try
            {
                // Act
                configManager.SaveSetting("DefaultBackupPath", newValue);

                var after = configManager.GetSetting("DefaultBackupPath");

                // Assert
                Assert.AreNotEqual(originalValue, after);
                Assert.AreEqual(newValue, after);
            }
            finally
            {
                configManager.SaveSetting("DefaultBackupPath", originalValue);
            }
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (File.Exists(TempBackupFilePath)) File.Delete(TempBackupFilePath);

            if (File.Exists(TempChecksumFilePath)) File.Delete(TempChecksumFilePath);

            if (File.Exists(TempTextFilePath)) File.Delete(TempTextFilePath);
        }
    }
}
