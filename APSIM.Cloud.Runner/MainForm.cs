
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

    /// <summary>
    /// Main form for runner application
    /// </summary>
    public partial class MainForm : Form
    {
        /// <summary>The job manager to send our jobs to</summary>
        private JobManager jobManager = null;

        /// <summary>Initializes a new instance of the <see cref="MainForm"/> class.</summary>
        public MainForm()
        {
            InitializeComponent();
        }

        /// <summary>Called when the form is loaded</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void OnLoad(object sender, EventArgs e)
        {
            jobManager = new JobManager();
            jobManager.AddJob(new RunJobsInDB());
            jobManager.Start(waitUntilFinished: false);
        }

        /// <summary>Called when form is closed</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="FormClosingEventArgs"/> instance containing the event data.</param>
        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            jobManager.Stop();
        }
    }
}
