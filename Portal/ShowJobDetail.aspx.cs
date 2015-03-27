using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using APSIM.Cloud;
using System.IO;

namespace APSIM.Cloud.Portal
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
                using (JobsService.JobsClient jobsService = new JobsService.JobsClient())
                {
                    string jobName = Request.QueryString["Name"];
                    JobsService.Job job = jobsService.Get(jobName);

                    if (Request.QueryString["Type"] != null)
                    {
                        string type = Request.QueryString["Type"];
                        if (type == "XML")
                        {
                            Response.ContentType = "text/xml";
                            Response.ContentEncoding = System.Text.Encoding.UTF8;
                            Response.Output.Write(jobsService.GetJobXML(jobName));
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
        }

        protected void Page_Load(object sender, EventArgs e)
        {

        }


    }
}