using System;
using System.Data;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.UI.WebControls;
using System.Xml;
using APSIM.Cloud.Shared;
using APSIM.Shared.Soils;
using APSIM.Shared.Utilities;

namespace APSIM.Cloud.Portal
{
    public partial class Upload : System.Web.UI.Page
    {
        /// <summary>
        /// The upload button was click.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected void UploadButtonClick(object sender, EventArgs e)
        {
            // Get the contents of the file and write to a temp file.
            byte[] bytes = new byte[FileUpload.FileContent.Length];
            FileUpload.FileContent.Read(bytes, 0, bytes.Length);
            string tempFile = Path.GetTempFileName();
            FileStream writer = new FileStream(tempFile, FileMode.Create);
            writer.Write(bytes, 0, bytes.Length);
            writer.Close();

            if (Path.GetExtension(FileUpload.FileName) == ".zip")
            {
                File.Move(tempFile, Path.ChangeExtension(tempFile, ".zip"));
                tempFile = Path.ChangeExtension(tempFile, ".zip");
            }

            YieldProphet yieldProphet = YieldProphetUtility.YieldProphetFromFile(tempFile);

            DateTime nowDate = DateTime.Now;
            if (NowEditBox.Text != "")
                nowDate = DateTime.ParseExact(NowEditBox.Text, "d/M/yyyy", CultureInfo.InvariantCulture);

            foreach (Paddock paddock in yieldProphet.Paddock)
                paddock.NowDate = nowDate;

            using (JobsService.JobsSoapClient jobsService = new JobsService.JobsSoapClient())
            {
                jobsService.Add(ToWebServiceYP(yieldProphet));
            }

            File.Delete(tempFile);

            Response.Redirect("Main.aspx");
        }

        /// <summary>Converts an internal yieldprophet instance to a web service instance.</summary>
        /// <param name="yieldProphet">The yield prophet instance to convert.</param>
        /// <returns>The web service version of the yieldprophet instance.</returns>
        private JobsService.YieldProphet ToWebServiceYP(YieldProphet yieldProphet)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(XmlUtilities.Serialise(yieldProphet, false));
            return XmlUtilities.Deserialise(doc.DocumentElement, typeof(JobsService.YieldProphet)) as JobsService.YieldProphet;
        }

    }
}