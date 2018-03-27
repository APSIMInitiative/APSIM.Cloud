namespace APSIM.Cloud.Runner
{
    using APSIM.Shared.Utilities;
    using System;
    using System.Collections.Generic;
    using System.ServiceProcess;

    partial class RunnerService : ServiceBase
    {
        private RunJobsInDB jobManager = null;
        private Dictionary<string, string> appSettings;

        /// <summary>Constructor</summary>
        /// <param name="maximumNumberOfProcessors">The maximum number of CPU cores to use.</param>
        public RunnerService(Dictionary<string, string> appSettings)
        {
            InitializeComponent();
            jobManager = null;
            this.appSettings = appSettings;
        }

        protected override void OnStart(string[] args)
        {
            if (jobManager == null)
            {
                jobManager = new RunJobsInDB(appSettings);
                jobManager.Start();
            }
        }

        protected override void OnStop()
        {
            jobManager.Stop();
        }
    }
}
