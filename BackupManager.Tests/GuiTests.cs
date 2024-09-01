using System;
using System.Collections.Generic;
using System.Linq;
using BackupManager.GUI;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BackupManager.Tests
{
    [TestClass]
    public class GuiTests
    {
        /// <summary>
        /// Test sprawdza, czy po kliknięciu przycisku dodania folderu do elementów kopii zapasowej
        /// folder jest poprawnie dodawany do listy elementów kopii zapasowej.
        /// 
        /// This test verifies that clicking the "Add Folder" button correctly adds the folder
        /// to the list of backup items.
        /// </summary>
        [TestMethod]
        public void AddFolderButton_Click_ShouldAddFolderToBackupItems()
        {
            // Arrange
            var mainWindow = new MainWindow();
            var initialCount = mainWindow.GetBackupItems().Count;
            const string folderPath = @"C:\test-folder";

            // Act
            mainWindow.AddPathToBackupItems(folderPath);

            // Assert
            Assert.AreEqual(initialCount + 1, mainWindow.GetBackupItems().Count);
            Assert.IsTrue(mainWindow.GetBackupItems().Contains(folderPath));
        }

        /// <summary>
        /// Test sprawdza, czy aplikacja nie dodaje duplikatów ścieżek folderów do elementów kopii zapasowej.
        /// 
        /// This test checks if the application prevents duplicate folder paths from being added
        /// to the backup items.
        /// </summary>
        [TestMethod]
        public void AddPathToBackupItems_ShouldNotAddDuplicatePath()
        {
            // Arrange
            var mainWindow = new MainWindow();
            const string folderPath = @"C:\test-folder";

            mainWindow.AddPathToBackupItems(folderPath);

            var initialCount = mainWindow.GetBackupItems().Count;

            // Act
            mainWindow.AddPathToBackupItems(folderPath);

            // Assert
            Assert.AreEqual(initialCount, mainWindow.GetBackupItems().Count);
        }

        /// <summary>
        /// Test sprawdza, czy aplikacja poprawnie dodaje wiele ścieżek folderów do elementów kopii zapasowej.
        /// 
        /// This test verifies that the application correctly adds multiple folder paths to the
        /// list of backup items.
        /// </summary>
        [TestMethod]
        public void AddPathToBackupItems_ShouldAddMultiplePathsCorrectly()
        {
            // Arrange
            var mainWindow = new MainWindow();
            var paths = new List<string> { @"C:\Folder1", @"C:\Folder2", @"C:\Folder3" };

            // Act
            foreach (var path in paths)
            {
                mainWindow.AddPathToBackupItems(path);
            }

            // Assert
            CollectionAssert.AreEqual(paths, mainWindow.GetBackupItems().ToList());
        }

        /// <summary>
        /// Test sprawdza, czy aplikacja wyrzuca wyjątek ArgumentException w przypadku dodania niepoprawnej ścieżki.
        /// 
        /// This test ensures that the application throws an ArgumentException when an invalid
        /// folder path is added.
        /// </summary>
        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void AddPathToBackupItems_ShouldThrowArgumentExceptionForInvalidPath()
        {
            // Arrange
            var mainWindow = new MainWindow();
            const string invalidPath = @"C:\invalid-path<>";

            // Act
            mainWindow.AddPathToBackupItems(invalidPath);
        }
    }
}
