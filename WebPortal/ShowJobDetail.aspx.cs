using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using APSIM.Cloud.Services;
using System.IO;

namespace APSIM.Cloud.WebPortal
{
    public partial class ShowJobDetail : System.Web.UI.Page
    {
        /// <summary>
        /// We're about to render the page - search for the soils and write names to the 
        /// response. The iPad soil app uses this method.
        /// </summary>
        protected void Page_PreRender(object sender, EventArgs e)
        {
            Response.Clear();

            if (Request.QueryString["Name"] != null)
            {

                JobsDB jobsDB = new JobsDB();
                jobsDB.Open();

                string jobName = Request.QueryString["Name"];
                JobsDB.JobDB job = jobsDB.GetJob(jobName);
                jobsDB.Close();

                if (Request.QueryString["Type"] != null)
                {
                    string type = Request.QueryString["Type"];
                    if (type == "XML")
                    {
                        Response.ContentType = "text/xml";
                        Response.ContentEncoding = System.Text.Encoding.UTF8;
                        Response.Output.Write(job.XML);
                    }
                    else
                    {
                        Response.ContentType = "text/plain";
                        Response.Output.Write(job.ErrorText);
                    }

                    Response.End();
                }
            }
        }

        protected void Page_Load(object sender, EventArgs e)
        {

        }


    }
}