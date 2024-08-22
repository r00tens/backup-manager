using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using BackupManager.Library;
using BackupManager.Library.Models;
using Button = System.Windows.Controls.Button;
using DataGrid = System.Windows.Controls.DataGrid;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Path = System.IO.Path;

namespace BackupManager.GUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private readonly List<string> _backupItems;
        private readonly List<string> _automateBackupItems;
        private readonly BackupService _backupService;
        private readonly AppConfigManager _configManager;

        private ObservableCollection<ScheduledBackup> _scheduledBackups;

        public MainWindow()
        {
            InitializeComponent();

            _backupItems = new List<string>();
            _automateBackupItems = new List<string>();
            _backupService = new BackupService();
            _configManager = new AppConfigManager();

            _backupService.OnBackupProgressChanged += UpdateBackupProgressBar;
            _backupService.OnRestoreProgressChanged += UpdateRestoreProgressBar;

            LoadSettings();
            ApplySettings();
            LoadScheduledBackups();
        }

        // backup tab
        private void AddFolderButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                DialogResult result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    _backupItems.Add(dialog.SelectedPath);
                    UpdateBackupItemsControl();
                }
            }
        }

        private void AddFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (string file in openFileDialog.FileNames)
                {
                    _backupItems.Add(file);
                }

                UpdateBackupItemsControl();
            }
        }

        private void UpdateBackupItemsControl()
        {
            BackupItemsControl.ItemsSource = null;
            BackupItemsControl.ItemsSource = _backupItems;
        }

        private void BrowseDestinationButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                DialogResult result = dialog.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    DestinationPathTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void RemoveItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button.DataContext is string itemToRemove)
                {
                    _backupItems.Remove(itemToRemove);
                    UpdateBackupItemsControl();
                }
            }
        }

        private async void CreateBackupButton_Click(object sender, RoutedEventArgs e)
        {
            CreateBackupButton.IsEnabled = false;

            if (_backupItems.Count == 0)
            {
                MessageBox.Show("Please add files or folders to backup.", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                CreateBackupButton.IsEnabled = true;
                return;
            }

            if (string.IsNullOrWhiteSpace(DestinationPathTextBox.Text))
            {
                MessageBox.Show("Please select a destination path.", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                CreateBackupButton.IsEnabled = true;
                return;
            }
            
            UpdateBackupProgressBar(0);
            
            string backupName = "backup-" + DateTime.Now.ToString("ddMMyyyy-HHmmss");
            string destinationPath = DestinationPathTextBox.Text;
            string destinationZipPath = Path.Combine(destinationPath, backupName + ".zip");
            string destinationFolderPath = Path.Combine(destinationPath, backupName);

            bool compress = CompressCheckBox.IsChecked == true;

            try
            {
                BackupResult result;

                if (compress)
                {
                    result = await Task.Run(() =>
                        _backupService.CreateBackup(_backupItems.ToArray(), destinationZipPath, true));
                }
                else
                {
                    result = await Task.Run(() =>
                        _backupService.CreateBackup(_backupItems.ToArray(), destinationFolderPath));
                }

                LastBackupInfoTextBlock.Text = result.ToString();
                _configManager.LastBackupInfo = result.ToString();

                MessageBox.Show("Backup created successfully.", "Success", MessageBoxButton.OK,
                    MessageBoxImage.Information);
                
                UpdateBackupProgressBar(0);
                
                if (_configManager.GetSetting("AutoVerifyBackupIntegrity") == "true")
                {
                    bool isValid = _backupService.VerifyBackupIntegrity(compress ? destinationZipPath : destinationFolderPath, compress);

                    if (isValid)
                    {
                        MessageBox.Show("Backup integrity verified successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Backup integrity verification failed.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                if (compress)
                {
                    if (File.Exists(destinationZipPath))
                    {
                        File.Delete(destinationZipPath);
                    }
                }
                else
                {
                    if (Directory.Exists(destinationFolderPath))
                    {
                        Directory.Delete(destinationFolderPath, true);
                    }
                }

                MessageBox.Show($"Error during backup: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                
                UpdateBackupProgressBar(0);
            }
            finally
            {
                CreateBackupButton.IsEnabled = true;
            }
        }

        private void UpdateBackupProgressBar(int percentage)
        {
            Dispatcher.Invoke(() =>
            {
                BackupProgressBar.Value = percentage;
                BackupProgressPercentageTextBlock.Text = $"{percentage}%";
            });
        }

        private void UpdateRestoreProgressBar(int percentage)
        {
            Dispatcher.Invoke(() =>
            {
                RestoreProgressBar.Value = percentage;
                RestoreProgressPercentageTextBlock.Text = $"{percentage}%";
            });
        }

        // restore tab
        private void BrowseBackupSearchPathButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                DialogResult result = dialog.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    BackupSearchPathTextBox.Text = dialog.SelectedPath;
                    UpdateBackupList(dialog.SelectedPath);
                }
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            string backupPath = BackupSearchPathTextBox.Text;
            UpdateBackupList(backupPath);
        }

        private void UpdateBackupList(string searchPath)
        {
            if (string.IsNullOrWhiteSpace(searchPath) || !Directory.Exists(searchPath))
            {
                MessageBox.Show("Please select a valid search path.", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            var backups = Directory.EnumerateFileSystemEntries(searchPath)
                .Where(f => IsBackupFileOrFolder(f))
                .ToList();

            BackupsListControl.ItemsSource = backups;
        }

        private bool IsBackupFileOrFolder(string path)
        {
            string fileName = Path.GetFileName(path);

            // Sprawdzamy, czy plik lub folder pasuje do schematu nazwy backupu
            bool isZipBackup = fileName.EndsWith(".zip") && fileName.StartsWith("backup-");
            bool isFolderBackup = Directory.Exists(path) && fileName.StartsWith("backup-");

            // Wyświetlamy tylko foldery backupu lub pliki ZIP
            return isZipBackup || isFolderBackup;
        }

        private async void RestoreBackupButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button.DataContext is string selectedBackup)
                {
                    var restorePathDialog = new FolderBrowserDialog();
                    if (restorePathDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        try
                        {
                            UpdateRestoreProgressBar(0);
                            
                            await Task.Run(() =>
                                _backupService.RestoreBackup(selectedBackup, restorePathDialog.SelectedPath));

                            MessageBox.Show("Backup restored successfully.", "Success", MessageBoxButton.OK,
                                MessageBoxImage.Information);
                            
                            UpdateRestoreProgressBar(0);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error during restore: {ex.Message}", "Error", MessageBoxButton.OK,
                                MessageBoxImage.Error);
                            
                            UpdateRestoreProgressBar(0);
                        }
                    }
                }
            }
        }

        private void RemoveBackupButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button.DataContext is string selectedBackup)
                {
                    MessageBoxResult confirmResult = MessageBox.Show("Are you sure you want to delete this backup?",
                        "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (confirmResult == MessageBoxResult.Yes)
                    {
                        try
                        {
                            if (File.Exists(selectedBackup))
                            {
                                File.Delete(selectedBackup);
                            }
                            else if (Directory.Exists(selectedBackup))
                            {
                                Directory.Delete(selectedBackup, true);
                            }

                            MessageBox.Show("Backup deleted successfully.", "Success", MessageBoxButton.OK,
                                MessageBoxImage.Information);
                            UpdateBackupList(BackupSearchPathTextBox.Text);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Error during delete: {ex.Message}", "Error", MessageBoxButton.OK,
                                MessageBoxImage.Error);
                        }
                    }
                }
            }
        }

        // automation tab
        private void LoadScheduledBackups()
        {
            _scheduledBackups = _configManager.GetScheduledBackups();
            ScheduledBackupsDataGrid.ItemsSource = _scheduledBackups;
        }

        private void DeleteScheduleBackupButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;

            if (button?.DataContext is ScheduledBackup backup)
            {
                _scheduledBackups.Remove(backup);
                
                var config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                config.AppSettings.Settings.Remove(backup.BackupKey);
                config.Save(ConfigurationSaveMode.Modified);
                File.SetLastWriteTime(config.FilePath, DateTime.Now);
                
                ConfigurationManager.RefreshSection("appSettings");
            }
        }
        
        private void RefreshScheduledBackupsButton_Click(object sender, RoutedEventArgs e)
        {
            LoadScheduledBackups();
        }

        private void AutomateAddFolderButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                DialogResult result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    _automateBackupItems.Add(dialog.SelectedPath);
                    UpdateAutomateItemsControl();
                }
            }
        }

        private void AutomateAddFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                foreach (string file in openFileDialog.FileNames)
                {
                    _automateBackupItems.Add(file);
                }

                UpdateAutomateItemsControl();
            }
        }

        private void UpdateAutomateItemsControl()
        {
            AutomateItemsControl.ItemsSource = null;
            AutomateItemsControl.ItemsSource = _automateBackupItems;
        }

        private void AutomateRemoveItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button.DataContext is string itemToRemove)
                {
                    _automateBackupItems.Remove(itemToRemove);
                    UpdateAutomateItemsControl();
                }
            }
        }

        private void AutomateBrowseDestinationButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                DialogResult result = dialog.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    AutomateDestinationPathTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void CreateScheduledBackupButton_Click(object sender, RoutedEventArgs e)
        {
            string backupName = AutomateBackupNameTextBox.Text;
            string sourcePaths = string.Join(",", _automateBackupItems.ToArray());
            string destinationPath = AutomateDestinationPathTextBox.Text;
            
            var selectedItem = (ComboBoxItem)AutomateBackupTypeComboBox.SelectedItem;
            string backupType = selectedItem.Tag.ToString();

            selectedItem = (ComboBoxItem)AutomateScheduleComboBox.SelectedItem;
            string schedule = selectedItem.Tag.ToString();

            if (string.IsNullOrEmpty(backupName) || string.IsNullOrEmpty(sourcePaths) || string.IsNullOrEmpty(destinationPath) || string.IsNullOrEmpty(schedule))
            {
                MessageBox.Show("Please fill in all fields for the scheduled backup.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                _configManager.AddScheduledBackup(backupName, sourcePaths, destinationPath, backupType, schedule);
                LoadScheduledBackups();
                MessageBox.Show("Scheduled backup created successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding backup: {ex}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // settings tab
        private void BrowseDefaultBackupPathButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                DialogResult result = dialog.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    DefaultBackupPathTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void BrowseDefaultBackupSearchPathButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                DialogResult result = dialog.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    DefaultBackupSearchPathTextBox.Text = dialog.SelectedPath;
                }
            }
        }

        private void ClearDefaultBackupPathButton_Click(object sender, RoutedEventArgs e)
        {
            DefaultBackupPathTextBox.Text = string.Empty;
            _configManager.SaveSetting("DefaultBackupPath", string.Empty);
        }

        private void ClearDefaultBackupSearchPathButton_Click(object sender, RoutedEventArgs e)
        {
            DefaultBackupSearchPathTextBox.Text = string.Empty;
            _configManager.SaveSetting("DefaultBackupSearchPath", string.Empty);
        }

        private void LoadSettings()
        {
            DefaultBackupPathTextBox.Text = _configManager.GetSetting("DefaultBackupPath");
            DefaultBackupSearchPathTextBox.Text = _configManager.GetSetting("DefaultBackupSearchPath");
            DefaultCompressCheckBox.IsChecked = bool.Parse(_configManager.GetSetting("DefaultCompress"));
        }

        private void ApplySettings()
        {
            // backup tab
            DestinationPathTextBox.Text = _configManager.GetSetting("DefaultBackupPath");
            CompressCheckBox.IsChecked = bool.Parse(_configManager.GetSetting("DefaultCompress"));
            LastBackupInfoTextBlock.Text = _configManager.LastBackupInfo;

            // restore tab
            BackupSearchPathTextBox.Text = _configManager.GetSetting("DefaultBackupSearchPath");

            if (!string.IsNullOrEmpty(DefaultBackupSearchPathTextBox.Text))
            {
                UpdateBackupList(DefaultBackupSearchPathTextBox.Text);
            }
        }

        private void SaveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            _configManager.SaveSetting("DefaultBackupPath", DefaultBackupPathTextBox.Text);
            _configManager.SaveSetting("DefaultBackupSearchPath", DefaultBackupSearchPathTextBox.Text);
            _configManager.SaveSetting("DefaultCompress", DefaultCompressCheckBox.IsChecked.ToString());

            ApplySettings();

            MessageBox.Show("Settings saved successfully.", "Success", MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void ResetToDefaultSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            _configManager.SaveSetting("DefaultBackupPath", "");
            _configManager.SaveSetting("DefaultBackupSearchPath", "");
            _configManager.SaveSetting("DefaultCompress", "false");

            LoadSettings();
            ApplySettings();

            MessageBox.Show("Settings reset to default values.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
