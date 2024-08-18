using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace BackupManager.Service
{
    [RunInstaller(true)]
    public class ProjectInstaller : Installer
    {
        public ProjectInstaller()
        {
            var serviceProcessInstaller = new ServiceProcessInstaller();
            var serviceInstaller = new ServiceInstaller();
            
            serviceProcessInstaller.Account = ServiceAccount.LocalSystem;
            
            serviceInstaller.ServiceName = "BackupManagerService";
            serviceInstaller.DisplayName = "Backup Manager Service";
            serviceInstaller.StartType = ServiceStartMode.Automatic;
            
            Installers.Add(serviceProcessInstaller);
            Installers.Add(serviceInstaller);
        }
    }
}