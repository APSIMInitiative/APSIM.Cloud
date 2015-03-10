
namespace APSIM.Cloud.WebPortal
{
    using System;
    using System.Data;
    using System.Web.UI.WebControls;
    using APSIM.Cloud.Services;

    public partial class Main : System.Web.UI.Page
    {
        private DataTable table;

        protected void Page_Load(object sender, EventArgs e)
        {
            GridView.DataSource = null;
            JobsDB DB = new JobsDB();
            DB.Open();
            JobsDB.JobDB[] jobs = DB.Get();
            DB.Close();

            table = new DataTable();
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Status", typeof(string));
            table.Columns.Add("XML", typeof(string));
            table.Columns.Add("URL", typeof(string));

            // Loop through all jobs
            foreach (JobsDB.JobDB job in jobs)
            {
                DataRow newRow = table.NewRow();
                table.Rows.Add(newRow);

                newRow["Name"] = job.Name;
                newRow["Status"] = job.Status.ToString();
                newRow["XML"] = "XML";
                newRow["URL"] = job.URL;
            }

            GridView.DataSource = table;
            GridView.DataBind();
        }

        protected void NumRowsTextBox_TextChanged(object sender, EventArgs e)
        {
            Page_Load(null, null);
            //Response.Redirect("BobWeb.aspx");
        }

        protected void Passes_CheckedChanged(object sender, EventArgs e)
        {
            Page_Load(null, null);
            // Response.Redirect("BobWeb.aspx");
        }

        protected void GridView_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            // Retrieve the row index stored in the 
            // CommandArgument property.
            int index = Convert.ToInt32(e.CommandArgument);

            if (e.CommandName == "XML")
            {
                string name = table.Rows[index]["Name"].ToString();
                string xml = table.Rows[index]["XML"].ToString();
                Response.Redirect("ShowJobXML.aspx?Name=" + name);
            }
            else if (e.CommandName == "ReRun")
            {
                string name = table.Rows[index]["Name"].ToString();
                JobsDB DB = new JobsDB();
                DB.Open();
                DB.SetJobStatus(name, JobsDB.StatusEnum.PendingAdd);
                DB.SetJobURL(name, null);
                DB.Close();
                Response.Redirect("YP.aspx");
            }
            else if (e.CommandName == "Delete")
            {
                string name = table.Rows[index]["Name"].ToString();
                JobsDB DB = new JobsDB();
                DB.Open();
                DB.SetJobStatus(name, JobsDB.StatusEnum.PendingDelete);
                DB.Close();
                Response.Redirect("YP.aspx");
            }
        }
    }
}