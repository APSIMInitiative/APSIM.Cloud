
namespace APSIM.Cloud.Runner
{
    using System;
    using System.Collections.Generic;
    using System.Windows.Forms;

    /// <summary>
    /// Main form for runner application
    /// </summary>
    public partial class MainForm : Form
    {
        private RunJobsInDB jobManager = null;

        /// <summary>Command line arguments</summary>
        private Dictionary<string, string> appSettings;

        /// <summary>Initializes a new instance of the <see cref="MainForm"/> class.</summary>
        public MainForm(Dictionary<string, string> appSettings)
        {
            InitializeComponent();
            this.appSettings = appSettings;
        }

        /// <summary>Called when the form is loaded</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void OnLoad(object sender, EventArgs e)
        {
            jobManager = new RunJobsInDB(appSettings);
            jobManager.Start();
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
