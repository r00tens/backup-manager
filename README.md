# BackupManager

BackupManager is a simple file and folder backup manager. Created for the needs of a college project.

It allows you to create backups and restore files, as well as schedule automatic backups.

## Features

- making backups of files, folders with checksums (SHA256) in the form of a folder or zip file
- searching for created copies with the possibility of restoring to a target location after checking the integrity
- creating cyclic backups of files, folders performed by the service

## List of used mechanisms

- configuration files
- event log
- file system observer
- compression
- encryption

## Requirements

- .NET Framework ``4.8``

## License

This project is licensed under the MIT License - for details, see the [LICENSE](LICENSE) file.
