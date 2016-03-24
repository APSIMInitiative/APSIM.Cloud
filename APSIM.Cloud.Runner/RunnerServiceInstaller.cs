using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration.Install;
using System.Linq;

namespace APSIM.Cloud.Runner
{
    [RunInstaller(true)]
    public partial class RunnerServiceInstaller : System.Configuration.Install.Installer
    {
        public RunnerServiceInstaller()
        {
            InitializeComponent();
        }

        private void serviceProcessInstaller1_BeforeInstall(object sender, InstallEventArgs e)
        {
            System.ServiceProcess.ServiceProcessInstaller lInstaller = sender as System.ServiceProcess.ServiceProcessInstaller;
            if (lInstaller != null)
            {
                string lAssemblyPath = lInstaller.Context.Parameters["assemblypath"];
                //Wrap the existing path in quotes if it isn't already
                if (lAssemblyPath.StartsWith("\"") == false)
                {
                    lAssemblyPath = String.Format("\"{0}\"", lAssemblyPath);
                }
                lAssemblyPath += (" -Service");
                //Set the new path
                lInstaller.Context.Parameters["assemblypath"] = lAssemblyPath;
            }
        }
    }
}
