using System;
using System.IO;
using System.Reflection;
using System.Timers;
using BackupManager.Service;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BackupManager.Tests
{
    /// <summary>
    /// Klasa zawiera testy jednostkowe dla BackupManagerService, zapewniające poprawne działanie metod prywatnych.
    /// This class contains unit tests for BackupManagerService, ensuring correct operation of private methods.
    /// </summary>
    [TestClass]
    public class ServiceTests
    {
        private BackupManagerService _service;

        /// <summary>
        /// Inicjalizuje instancję BackupManagerService przed każdym testem.
        /// Initializes an instance of BackupManagerService before each test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            _service = new BackupManagerService();
        }

        /// <summary>
        /// Testuje poprawność ustawienia FileSystemWatcher przez metodę InitializeFileSystemWatcher.
        /// Tests if the InitializeFileSystemWatcher method correctly sets up the FileSystemWatcher.
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

            // Sprawdza, czy ConfigWatcher nie jest null i czy NotifyFilter jest ustawiony na LastWrite.
            // Checks if ConfigWatcher is not null and NotifyFilter is set to LastWrite.
            Assert.IsNotNull(_service.ConfigWatcher);
            Assert.AreEqual(NotifyFilters.LastWrite, _service.ConfigWatcher.NotifyFilter);
        }

        /// <summary>
        /// Testuje, czy metoda InitializeFileSystemWatcher rzuca ArgumentException dla nieprawidłowej ścieżki.
        /// Tests if the InitializeFileSystemWatcher method throws an ArgumentException for an invalid path.
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
                // Sprawdza, czy wyrzucony wyjątek jest typu ArgumentException.
                // Checks if the thrown exception is of type ArgumentException.
                Assert.IsInstanceOfType(ex.InnerException, typeof(ArgumentException));
            }
        }

        /// <summary>
        /// Testuje, czy metoda OnConfigChanged inicjuje timer debouncing, jeśli ten jest null.
        /// Tests if the OnConfigChanged method initializes the debounce timer if it is null.
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

            // Sprawdza, czy timer debouncing jest zainicjowany i uruchomiony.
            // Checks if the debounce timer is initialized and running.
            Assert.IsNotNull(debounceTimer);
            Assert.IsTrue(debounceTimer.Enabled);
        }

        /// <summary>
        /// Testuje, czy metoda OnConfigChanged restartuje timer debouncing, jeśli ten jest już zainicjowany.
        /// Tests if the OnConfigChanged method restarts the debounce timer if it is already initialized.
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

            // Sprawdza, czy ten sam timer został zaktualizowany i jest uruchomiony.
            // Checks if the same timer is updated and running.
            Assert.AreSame(debounceTimer, updatedDebounceTimer);
            Assert.IsTrue(debounceTimer.Enabled);
        }

        /// <summary>
        /// Metoda pomocnicza do uzyskiwania wartości prywatnego pola.
        /// Helper method to get the value of a private field.
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
        /// Metoda pomocnicza do ustawiania wartości prywatnego pola.
        /// Helper method to set the value of a private field.
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
