
namespace APSIM.Cloud.Runner
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Data;
    using System.Drawing;
    using System.Linq;
    using System.Text;
    using System.Windows.Forms;
    using APSIM.Shared.Utilities;
    using System.IO;
    using APSIM.Cloud.Shared;

    /// <summary>
    /// Main form for runner application
    /// </summary>
    public partial class MainForm : Form
    {
        private IJobRunner jobRunner = null;
        private RunJobsInDB jobManager = null;

        /// <summary>Command line arguments</summary>
        private Dictionary<string, string> arguments;

        /// <summary>Initializes a new instance of the <see cref="MainForm"/> class.</summary>
        public MainForm(string[] args)
        {
            InitializeComponent();
            arguments = StringUtilities.ParseCommandLine(args);
        }

        /// <summary>Called when the form is loaded</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void OnLoad(object sender, EventArgs e)
        {
            int maximumNumberOfCores = -1;
            if (arguments.ContainsKey("-MaximumNumberOfCores"))
                maximumNumberOfCores = Convert.ToInt32(arguments["-MaximumNumberOfCores"]);

            jobRunner = new JobRunnerAsync();
            IJobManager jobManager = new RunJobsInDB(jobRunner);
            jobRunner.Run(jobManager, wait: false, numberOfProcessors: maximumNumberOfCores);
        }

        /// <summary>Called when form is closed</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="FormClosingEventArgs"/> instance containing the event data.</param>
        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            jobRunner.Stop();
            jobManager.Stop();
        }

        

    }
}
