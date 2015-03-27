
namespace APSIM.Cloud.Portal
{
    using System;
    using System.Data;
    using System.Web.UI.WebControls;
    using APSIM.Cloud;
    //using APSIM.Cloud.WebPortal.APSIMCloud;

    public partial class Main : System.Web.UI.Page
    {
        /// <summary>The table containing the data being displayed</summary>
        private DataTable table;

        /// <summary>Handles the Load event of the Page control.</summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void Page_Load(object sender, EventArgs e)
        {
            GridView.DataSource = null;
            using (JobsService.JobsClient jobsService = new JobsService.JobsClient())
            {
                JobsService.Job[] jobs = jobsService.GetMany(Convert.ToInt32(NumRowsTextBox.Text));

                table = new DataTable();
                table.Columns.Add("Name", typeof(string));
                table.Columns.Add("Status", typeof(string));
                table.Columns.Add("XML", typeof(string));
                table.Columns.Add("URL", typeof(string));

                // Loop through all jobs
                foreach (JobsService.Job job in jobs)
                {
                    if (ShowAllCheckBox.Checked || job.Status != JobsService.StatusEnum.Completed)
                    {
                        DataRow newRow = table.NewRow();
                        table.Rows.Add(newRow);

                        newRow["Name"] = job.Name;
                        newRow["Status"] = job.Status.ToString();
                        newRow["XML"] = "XML";
                        newRow["URL"] = job.URL;
                    }
                }
            }

            GridView.DataSource = table;
            GridView.DataBind();
        }

        /// <summary>Handles the TextChanged event of the NumRowsTextBox control.</summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void OnNumRowsTextBoxChanged(object sender, EventArgs e)
        {
            Page_Load(null, null);
        }

        /// <summary>User has toggled the show all checkbox.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void OnShowAllCheckBoxChanged(object sender, EventArgs e)
        {
            Page_Load(null, null);
        }

        /// <summary>Called when the user clicks an image or link on a particular row of the grid.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="GridViewCommandEventArgs"/> instance containing the event data.</param>
        protected void OnGridRowCommand(object sender, GridViewCommandEventArgs e)
        {
            // Retrieve the row index stored in the 
            // CommandArgument property.
            int index = Convert.ToInt32(e.CommandArgument);

            if (e.CommandName == "Status")
            {
                string status = table.Rows[index]["Status"].ToString();
                if (status == "Error")
                {
                    string name = table.Rows[index]["Name"].ToString();
                    using (JobsService.JobsClient jobsService = new JobsService.JobsClient())
                    {
                        JobsService.Job job = jobsService.Get(name);
                        Response.Redirect("ShowJobDetail.aspx?Name=" + name + "&Type=Error");
                    }
                }
            }
            else if (e.CommandName == "XML")
            {
                string name = table.Rows[index]["Name"].ToString();
                Response.Redirect("ShowJobDetail.aspx?Name=" + name + "&Type=XML");
            }
            else if (e.CommandName == "ReRun")
            {
                using (JobsService.JobsClient jobsService = new JobsService.JobsClient())
                {
                    string name = table.Rows[index]["Name"].ToString();
                    jobsService.ReRun(name);
                    Response.Redirect("Main.aspx");
                }
            }
            else if (e.CommandName == "Delete")
            {
                using (JobsService.JobsClient jobsService = new JobsService.JobsClient())
                {
                    string name = table.Rows[index]["Name"].ToString();
                    jobsService.Delete(name);
                    Response.Redirect("Main.aspx");
                }
            }
        }

        /// <summary>User has clicked the add button.</summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void OnAddButtonClick(object sender, EventArgs e)
        {
            Response.Redirect("AddJob.aspx");
        }

    }
}