using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;

namespace BackupManager.Library
{
    public class BackupService
    {
        public delegate void ProgressHandler(int percentage);

        public event ProgressHandler OnBackupProgressChanged;
        public event ProgressHandler OnRestoreProgressChanged;

        public BackupResult CreateBackup(string[] items, string destinationPath, bool compress = false)
        {
            BackupResult result = new BackupResult
            {
                BackupDate = DateTime.Now,
                BackupName = Path.GetFileNameWithoutExtension(destinationPath),
                BackupType = compress ? "ZIP" : "Folder"
            };

            int totalItems = CountItems(items);
            int processedItems = 0;

            if (compress)
            {
                CreateCompressedBackup(items, destinationPath, result, ref processedItems, totalItems);
            }
            else
            {
                CreateFolderBackup(items, destinationPath, result, ref processedItems, totalItems);
            }

            ReportBackupProgress(processedItems, totalItems);

            return result;
        }

        public void RestoreBackup(string backupFilePath, string restoreDestination)
        {
            bool isCompressed = Path.GetExtension(backupFilePath) == ".zip";

            if (!VerifyBackupIntegrity(backupFilePath, isCompressed))
            {
                throw new InvalidOperationException("Backup integrity verification failed. Restore operation aborted.");
            }

            int totalItems = CountRestoreItems(backupFilePath, isCompressed);
            int processedItems = 0;

            if (isCompressed)
            {
                using (var archive = ZipFile.OpenRead(backupFilePath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        string destinationPath = Path.Combine(restoreDestination, entry.FullName);

                        // Utworzenie brakujących katalogów
                        string directoryPath = Path.GetDirectoryName(destinationPath);
                        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                        {
                            Directory.CreateDirectory(directoryPath);
                        }

                        if (string.IsNullOrEmpty(entry.Name)) // To jest katalog
                        {
                            Directory.CreateDirectory(destinationPath);
                        }
                        else
                        {
                            entry.ExtractToFile(destinationPath, true);
                        }

                        processedItems++;
                        ReportRestoreProgress(processedItems, totalItems);
                    }
                }
            }
            else
            {
                DirectoryCopy(backupFilePath, restoreDestination, true, ref processedItems, totalItems);
            }

            ReportRestoreProgress(processedItems, totalItems);
        }

        private int CountRestoreItems(string backupPath, bool isCompressed)
        {
            if (isCompressed)
            {
                using (var archive = ZipFile.OpenRead(backupPath))
                {
                    return archive.Entries.Count;
                }
            }
            else
            {
                return CountDirectoryItems(backupPath);
            }
        }

        public bool VerifyBackupIntegrity(string backupPath, bool isCompressed)
        {
            string backupDirectory = isCompressed ? Path.GetDirectoryName(backupPath) : backupPath;
            string backupName = Path.GetFileNameWithoutExtension(backupPath);

            string checksumFilePath = isCompressed
                ? Path.Combine(backupDirectory, backupName + "-checksums.txt")
                : Path.Combine(Path.GetDirectoryName(backupDirectory), backupName + "-checksums.txt");

            if (!File.Exists(checksumFilePath))
            {
                throw new FileNotFoundException($"Checksum file not found: {checksumFilePath}");
            }

            var checksums = File.ReadAllLines(checksumFilePath);

            foreach (var checksumEntry in checksums)
            {
                var parts = checksumEntry.Split(' ');
                var expectedChecksum = parts[0];
                var fileName = string.Join(" ", parts.Skip(1));

                if (isCompressed)
                {
                    // Obsługa dla plików w archiwum ZIP
                    using (var archive = ZipFile.OpenRead(backupPath))
                    {
                        var entry = archive.GetEntry(fileName);
                        if (entry == null)
                        {
                            Console.WriteLine($"File not found in ZIP: {fileName}");
                            return false;
                        }

                        using (var entryStream = entry.Open())
                        {
                            var actualChecksum = CalculateChecksum(entryStream);
                            Console.WriteLine($"Verifying file in ZIP: {fileName}");
                            Console.WriteLine(
                                $"Expected checksum: {expectedChecksum}, Actual checksum: {actualChecksum}");

                            if (expectedChecksum != actualChecksum)
                            {
                                Console.WriteLine($"Checksum mismatch for file in ZIP: {fileName}");
                                return false;
                            }
                        }
                    }
                }
                else
                {
                    // Obsługa dla plików w folderze
                    string filePath = Path.Combine(backupPath, fileName);

                    if (!File.Exists(filePath))
                    {
                        Console.WriteLine($"File not found: {filePath}");
                        return false;
                    }

                    var actualChecksum = CalculateChecksum(filePath);
                    Console.WriteLine($"Verifying file: {filePath}");
                    Console.WriteLine($"Expected checksum: {expectedChecksum}, Actual checksum: {actualChecksum}");

                    if (expectedChecksum != actualChecksum)
                    {
                        Console.WriteLine($"Checksum mismatch for file: {filePath}");
                        return false;
                    }
                }
            }

            return true;
        }

        private int CountItems(string[] items)
        {
            int count = 0;

            foreach (var item in items)
            {
                if (Directory.Exists(item))
                {
                    count += CountDirectoryItems(item);
                }
                else if (File.Exists(item))
                {
                    count++;
                }
            }

            return count;
        }

        private int CountDirectoryItems(string directory)
        {
            int count = 1; // Liczymy bieżący folder jako 1 element

            DirectoryInfo dir = new DirectoryInfo(directory);

            count += dir.GetFiles().Length; // Dodajemy liczbę plików w bieżącym folderze

            foreach (var subDir in dir.GetDirectories())
            {
                count += CountDirectoryItems(subDir.FullName); // Rekurencyjnie zliczamy elementy w podfolderach
            }

            return count;
        }

        private void CreateCompressedBackup(string[] items, string destinationZip, BackupResult result,
            ref int processedItems, int totalItems)
        {
            using (var archive = ZipFile.Open(destinationZip, ZipArchiveMode.Create))
            {
                string checksumFileName = Path.GetFileNameWithoutExtension(destinationZip) + "-checksums.txt";
                string tempFolder = Path.GetDirectoryName(destinationZip);
                string checksumFilePath = Path.Combine(tempFolder, checksumFileName);

                using (var checksumWriter = new StreamWriter(checksumFilePath))
                {
                    foreach (var item in items)
                    {
                        if (Directory.Exists(item))
                        {
                            AddDirectoryToArchive(archive, item, Path.GetFileName(item), result, checksumWriter,
                                ref processedItems, totalItems);
                        }
                        else if (File.Exists(item))
                        {
                            archive.CreateEntryFromFile(item, Path.GetFileName(item));
                            result.TotalFiles++;
                            result.TotalSize += new FileInfo(item).Length;

                            string checksum = CalculateChecksum(item);
                            checksumWriter.WriteLine($"{checksum} {Path.GetFileName(item)}");

                            processedItems++;
                            ReportBackupProgress(processedItems, totalItems);
                        }
                    }
                }
            }
        }

        private void CreateFolderBackup(string[] items, string destinationPath, BackupResult result,
            ref int processedItems, int totalItems)
        {
            // Tworzymy folder docelowy
            Directory.CreateDirectory(destinationPath);

            // Zmieniamy miejsce zapisu pliku checksums na obok folderu backupu
            string backupDirectory = Path.GetDirectoryName(destinationPath);
            string checksumFileName = Path.GetFileName(destinationPath) + "-checksums.txt";
            string checksumFilePath = Path.Combine(backupDirectory, checksumFileName);

            using (var checksumWriter = new StreamWriter(checksumFilePath))
            {
                foreach (var item in items)
                {
                    string dest = Path.Combine(destinationPath, Path.GetFileName(item));

                    if (Directory.Exists(item))
                    {
                        DirectoryCopy(item, dest, true, result, checksumWriter, ref processedItems, totalItems,
                            Path.GetFileName(item));
                    }
                    else if (File.Exists(item))
                    {
                        File.Copy(item, dest, true);
                        result.TotalFiles++;
                        result.TotalSize += new FileInfo(item).Length;

                        string checksum = CalculateChecksum(item);
                        checksumWriter.WriteLine($"{checksum} {Path.GetFileName(item)}");

                        processedItems++;
                        ReportBackupProgress(processedItems, totalItems);
                    }
                }
            }
        }

        private void AddDirectoryToArchive(ZipArchive archive, string directory, string entryName, BackupResult result,
            StreamWriter checksumWriter, ref int processedItems, int totalItems)
        {
            DirectoryInfo dir = new DirectoryInfo(directory);

            result.TotalFolders++;
            processedItems++; // Liczymy bieżący folder

            ReportBackupProgress(processedItems, totalItems);

            foreach (FileInfo file in dir.GetFiles())
            {
                string entryPath = Path.Combine(entryName, file.Name);
                archive.CreateEntryFromFile(file.FullName, entryPath);
                result.TotalFiles++;
                result.TotalSize += file.Length;

                string checksum = CalculateChecksum(file.FullName);
                checksumWriter.WriteLine($"{checksum} {entryPath}");

                processedItems++;
                ReportBackupProgress(processedItems, totalItems);
            }

            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                AddDirectoryToArchive(archive, subDir.FullName, Path.Combine(entryName, subDir.Name), result,
                    checksumWriter, ref processedItems, totalItems);
            }
        }

        private void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs, BackupResult result,
            StreamWriter checksumWriter, ref int processedItems, int totalItems, string relativePath = "")
        {
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);
            DirectoryInfo[] dirs = dir.GetDirectories();

            Directory.CreateDirectory(destDirName);
            result.TotalFolders++;
            processedItems++; // Liczymy bieżący folder

            ReportBackupProgress(processedItems, totalItems);

            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string tempPath = Path.Combine(destDirName, file.Name);
                file.CopyTo(tempPath, true);
                result.TotalFiles++;
                result.TotalSize += file.Length;

                string checksum = CalculateChecksum(file.FullName);
                string relativeFilePath = Path.Combine(relativePath, file.Name);
                checksumWriter.WriteLine($"{checksum} {relativeFilePath}");

                processedItems++;
                ReportBackupProgress(processedItems, totalItems);
            }

            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string tempPath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, tempPath, true, result, checksumWriter, ref processedItems,
                        totalItems, Path.Combine(relativePath, subdir.Name));
                }
            }
        }

        private void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs, ref int processedItems,
            int totalItems)
        {
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);
            DirectoryInfo[] dirs = dir.GetDirectories();

            Directory.CreateDirectory(destDirName);

            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string tempPath = Path.Combine(destDirName, file.Name);
                file.CopyTo(tempPath, true);

                processedItems++;
                ReportRestoreProgress(processedItems, totalItems);
            }

            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string tempPath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, tempPath, true, ref processedItems, totalItems);
                }
            }
        }

        private string CalculateChecksum(string filePath)
        {
            using (var sha256 = SHA256.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = sha256.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        private string CalculateChecksum(Stream stream)
        {
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private void ReportBackupProgress(int processedItems, int totalItems)
        {
            int percentage = (int)((double)processedItems / totalItems * 100);

            if (processedItems >= totalItems)
            {
                percentage = 100;
            }

            OnBackupProgressChanged?.Invoke(percentage);
        }

        private void ReportRestoreProgress(int processedItems, int totalItems)
        {
            int percentage = (int)((double)processedItems / totalItems * 100);

            if (processedItems >= totalItems)
            {
                percentage = 100;
            }

            OnRestoreProgressChanged?.Invoke(percentage);
        }
    }
}
