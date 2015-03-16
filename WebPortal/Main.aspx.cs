
namespace APSIM.Cloud.WebPortal
{
    using System;
    using System.Data;
    using System.Web.UI.WebControls;
    using APSIM.Cloud.Services;

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
            JobsDB DB = new JobsDB();
            DB.Open();
            JobsDB.JobDB[] jobs = DB.Get(Convert.ToInt32(NumRowsTextBox.Text));
            DB.Close();

            table = new DataTable();
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Status", typeof(string));
            table.Columns.Add("XML", typeof(string));
            table.Columns.Add("URL", typeof(string));

            // Loop through all jobs
            foreach (JobsDB.JobDB job in jobs)
            {
                if (ShowAllCheckBox.Checked || job.Status != JobsDB.StatusEnum.Completed)
                {
                    DataRow newRow = table.NewRow();
                    table.Rows.Add(newRow);

                    newRow["Name"] = job.Name;
                    newRow["Status"] = job.Status.ToString();
                    newRow["XML"] = "XML";
                    newRow["URL"] = "http://www.apsim.info/YP/Archive/" + job.Name + ".zip";
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
                    JobsDB DB = new JobsDB();
                    DB.Open();
                    JobsDB.JobDB job = DB.GetJob(name);
                    DB.Close();
                    Response.Redirect("ShowJobDetail.aspx?Name=" + name + "&Type=Error");
                }
            }
            else if (e.CommandName == "XML")
            {
                string name = table.Rows[index]["Name"].ToString();
                string xml = table.Rows[index]["XML"].ToString();
                Response.Redirect("ShowJobDetail.aspx?Name=" + name + "&Type=XML");
            }
            else if (e.CommandName == "ReRun")
            {
                string name = table.Rows[index]["Name"].ToString();
                JobsDB DB = new JobsDB();
                DB.Open();
                DB.SetJobStatus(name, JobsDB.StatusEnum.Added);
                DB.SetJobURL(name, null);
                DB.Close();
                Response.Redirect("Main.aspx");
            }
            else if (e.CommandName == "Delete")
            {
                string name = table.Rows[index]["Name"].ToString();
                JobsDB DB = new JobsDB();
                DB.Open();
                DB.SetJobStatus(name, JobsDB.StatusEnum.Deleting);
                DB.Close();
                Response.Redirect("Main.aspx");
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