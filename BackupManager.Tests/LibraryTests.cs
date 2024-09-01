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

        /// <summary>
        /// Test sprawdza, czy metoda FormatSize() zwraca poprawnie sformatowany rozmiar na podstawie
        /// wielkości podanej w bajtach.
        /// 
        /// This test verifies that the FormatSize() method returns the correct size format
        /// based on the given size in bytes.
        /// </summary>
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

        /// <summary>
        /// Test sprawdza, czy metoda CreateBackup() zwraca poprawny typ kopii zapasowej.
        /// 
        /// This test checks if the CreateBackup() method returns the correct backup type.
        /// </summary>
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

        /// <summary>
        /// Test sprawdza, czy metoda RestoreBackup() wyrzuca wyjątek InvalidOperationException, gdy
        /// suma kontrolna pliku backupu nie zgadza się z oryginalną.
        /// 
        /// This test checks if the RestoreBackup() method throws an InvalidOperationException when
        /// the checksum of the backup file does not match the original.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void RestoreBackup_ShouldThrowInvalidOperationException_WhenChecksumMismatch()
        {
            // Arrange
            var service = new BackupService();
            string[] items = { "test.txt" };
            
            File.WriteAllText("test.txt", @"Test content");
            
            service.CreateBackup(items, TempBackupFilePath, true);
            
            var checksumFilePath = Path.Combine(Path.GetDirectoryName(TempBackupFilePath) ?? string.Empty,
                Path.GetFileNameWithoutExtension(TempBackupFilePath) + "-checksums.txt");
            
            var checksumLines = File.ReadAllLines(checksumFilePath).ToList();

            if (checksumLines.Count > 0)
            {
                checksumLines[0] = "00000000000000000000000000000000 " + checksumLines[0].Split(' ')[1];
            }

            File.WriteAllLines(checksumFilePath, checksumLines);

            // Act
            service.RestoreBackup(TempBackupFilePath, TempRestoreFolder);
        }

        /// <summary>
        /// Test sprawdza, czy metoda SaveSetting() poprawnie aktualizuje ustawienie w konfiguracji aplikacji.
        /// 
        /// This test checks if the SaveSetting() method correctly updates a setting in the application's configuration.
        /// </summary>
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

        /// <summary>
        /// Test sprząta tymczasowe pliki używane podczas testów, takie jak pliki kopii zapasowej, sum kontrolnych itp.
        /// 
        /// This test cleans up temporary files used during testing, such as backup files, checksum files etc.
        /// </summary>
        [TestCleanup]
        public void Cleanup()
        {
            if (File.Exists(TempBackupFilePath)) File.Delete(TempBackupFilePath);

            if (File.Exists(TempChecksumFilePath)) File.Delete(TempChecksumFilePath);

            if (File.Exists(TempTextFilePath)) File.Delete(TempTextFilePath);
        }
    }
}
