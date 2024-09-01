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
        [TestMethod]
        public void AddFolderButton_Click_ShouldAddFolderToBackupItems()
        {
            // Arrange
            var mainWindow = new MainWindow();
            var initialCount = mainWindow.GetBackupItems().Count;
            var folderPath = @"C:\test-folder";

            // Act
            mainWindow.AddPathToBackupItems(folderPath);

            // Assert
            Assert.AreEqual(initialCount + 1, mainWindow.GetBackupItems().Count);
            Assert.IsTrue(mainWindow.GetBackupItems().Contains(folderPath));
        }

        [TestMethod]
        public void AddPathToBackupItems_ShouldNotAddDuplicatePath()
        {
            // Arrange
            var mainWindow = new MainWindow();
            var folderPath = @"C:\test-folder";

            mainWindow.AddPathToBackupItems(folderPath);
            var initialCount = mainWindow.GetBackupItems().Count;

            // Act
            mainWindow.AddPathToBackupItems(folderPath);

            // Assert
            Assert.AreEqual(initialCount, mainWindow.GetBackupItems().Count);
        }

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

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void AddPathToBackupItems_ShouldThrowArgumentExceptionForInvalidPath()
        {
            // Arrange
            var mainWindow = new MainWindow();
            var invalidPath = "C:\\invalid-path<>";

            // Act
            mainWindow.AddPathToBackupItems(invalidPath);
        }
    }
}
