using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Net;
using System.IO;
using System.Text;
using System.Security.Authentication;
using APSIM.Cloud.Services;
using System.Xml;
using APSIM.Cloud.Services.Specification;
using System.Globalization;

namespace BobWeb
{
    public partial class Upload : System.Web.UI.Page
    {

        protected void UploadButtonClick(object sender, EventArgs e)
        {
            // Get the contents of the XML file.
            byte[] xmlBytes = new byte[FileUpload.FileContent.Length];
            FileUpload.FileContent.Read(xmlBytes, 0, xmlBytes.Length);

            YieldProphetSpec yieldProphet = YieldProphetServices.Create(Encoding.ASCII.GetString(xmlBytes));

            DateTime nowDate = DateTime.Now;
            if (NowEditBox.Text != "")
                nowDate = DateTime.ParseExact(NowEditBox.Text, "d/M/yyyy", CultureInfo.InvariantCulture);

            foreach (Paddock paddock in yieldProphet.PaddockList)
                paddock.NowDate = nowDate;
            string newJobName = nowDate.ToString("yyyy-MM-dd (h-mm-ss tt) ") + yieldProphet.ReportName;

            string xml = YieldProphetServices.ToXML(yieldProphet);

            JobsDB jobsDB = new JobsDB();
            jobsDB.Open();
            jobsDB.Add(newJobName, xml);
            jobsDB.Close();
            Response.Redirect("Main.aspx");
        }

 
    }
}