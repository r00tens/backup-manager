using System;
using System.IO;
using System.Reflection;
using System.Timers;
using BackupManager.Service;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BackupManager.Tests
{
    [TestClass]
    public class ServiceTests
    {
        private BackupManagerService _service;

        /// <summary>
        /// Metoda inicjalizuje instancję BackupManagerService przed każdym testem.
        ///
        /// This method initializes the BackupManagerService instance before each test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            _service = new BackupManagerService();
        }

        /// <summary>
        /// Test sprawdza, czy metoda InitializeFileSystemWatcher prawidłowo konfiguruje obserwatora systemu plików.
        ///
        /// This test verifies that the InitializeFileSystemWatcher method correctly sets up the file system watcher.
        /// </summary>
        [TestMethod]
        public void InitializeFileSystemWatcher_ShouldSetupWatcherCorrectly()
        {
            var methodInfo = typeof(BackupManagerService).GetMethod("InitializeFileSystemWatcher",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (methodInfo != null)
            {
                methodInfo.Invoke(_service, new object[] { true });
            }

            Assert.IsNotNull(_service.ConfigWatcher);
            Assert.AreEqual(NotifyFilters.LastWrite, _service.ConfigWatcher.NotifyFilter);
        }

        /// <summary>
        /// Test sprawdza, czy metoda InitializeFileSystemWatcher rzuca wyjątek ArgumentException w przypadku nieprawidłowej ścieżki.
        ///
        /// This test verifies that the InitializeFileSystemWatcher method throws an ArgumentException when an invalid path is provided.
        /// </summary>
        [TestMethod]
        public void InitializeFileSystemWatcher_ShouldThrowArgumentException_ForInvalidPath()
        {
            var methodInfo = typeof(BackupManagerService).GetMethod("InitializeFileSystemWatcher",
                BindingFlags.NonPublic | BindingFlags.Instance);

            try
            {
                if (methodInfo != null)
                {
                    methodInfo.Invoke(_service, new object[] { false });
                }

                Assert.Fail("Expected exception was not thrown.");
            }
            catch (TargetInvocationException ex)
            {
                Assert.IsInstanceOfType(ex.InnerException, typeof(ArgumentException));
            }
        }

        /// <summary>
        /// Test sprawdza, czy metoda OnConfigChanged inicjalizuje debounceTimer, jeśli nie jest on ustawiony.
        ///
        /// This test verifies that the OnConfigChanged method initializes the debounceTimer if it is not set.
        /// </summary>
        [TestMethod]
        public void OnConfigChanged_ShouldInitializeDebounceTimer_IfNull()
        {
            // Arrange
            Assert.IsNull(GetPrivateField<Timer>(_service, "_debounceTimer"));

            var methodInfo =
                typeof(BackupManagerService).GetMethod("OnConfigChanged",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            var eventArgs = new FileSystemEventArgs(WatcherChangeTypes.Changed, "path", "file");

            // Act
            if (methodInfo != null)
            {
                methodInfo.Invoke(_service, new object[] { this, eventArgs });
            }

            // Assert
            var debounceTimer = GetPrivateField<Timer>(_service, "_debounceTimer");

            Assert.IsNotNull(debounceTimer);
            Assert.IsTrue(debounceTimer.Enabled);
        }

        /// <summary>
        /// Test sprawdza, czy metoda OnConfigChanged ponownie uruchamia debounceTimer, jeśli jest on już zainicjalizowany.
        ///
        /// This test verifies that the OnConfigChanged method restarts the debounceTimer if it is already initialized.
        /// </summary>
        [TestMethod]
        public void OnConfigChanged_ShouldRestartDebounceTimer_IfAlreadyInitialized()
        {
            // Arrange
            var debounceTimer = new Timer(5000);

            SetPrivateField(_service, "_debounceTimer", debounceTimer);

            var methodInfo =
                typeof(BackupManagerService).GetMethod("OnConfigChanged",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            var eventArgs = new FileSystemEventArgs(WatcherChangeTypes.Changed, "path", "file");

            // Act
            if (methodInfo != null)
            {
                methodInfo.Invoke(_service, new object[] { this, eventArgs });
            }

            // Assert
            var updatedDebounceTimer = GetPrivateField<Timer>(_service, "_debounceTimer");

            Assert.AreSame(debounceTimer, updatedDebounceTimer);
            Assert.IsTrue(debounceTimer.Enabled);
        }

        /// <summary>
        /// Metoda pomocnicza do uzyskiwania wartości prywatnego pola z obiektu.
        ///
        /// Helper method to retrieve the value of a private field from an object.
        /// </summary>
        private static T GetPrivateField<T>(object obj, string fieldName)
        {
            var fieldInfo = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);

            if (fieldInfo != null)
            {
                return (T)fieldInfo.GetValue(obj);
            }

            return default;
        }

        /// <summary>
        /// Metoda pomocnicza do ustawiania wartości prywatnego pola w obiekcie.
        ///
        /// Helper method to set the value of a private field in an object.
        /// </summary>
        private static void SetPrivateField(object obj, string fieldName, object value)
        {
            var fieldInfo = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);

            if (fieldInfo != null)
            {
                fieldInfo.SetValue(obj, value);
            }
        }
    }
}
