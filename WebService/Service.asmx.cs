// -----------------------------------------------------------------------
// <copyright file="Service.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
//-----------------------------------------------------------------------
namespace WebService
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Web;
    using System.Web.Services;
    using System.Xml;
    using APSIM.Cloud.Services;
    using System.Xml.Serialization;
    using APSIM.Cloud.Services.Specification;

    /// <summary>
    /// Summary description for Service1
    /// </summary>
    [WebService(Namespace = "http://www.apsim.info/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    public class Service : System.Web.Services.WebService
    {

        [WebMethod]
        public void AddJob(string yieldProphetXML, string rainFileContents)
        {
            // Create a job to store in DB
            XmlDocument doc = new XmlDocument();
            doc.AppendChild(doc.CreateElement("YPJob"));
            //XmlNode ypSpec = doc.DocumentElement.AppendChild(doc.CreateElement("YieldProphetSpec"));
            doc.DocumentElement.InnerXml = yieldProphetXML;
            XmlNode ypNode = Utility.Xml.Find(doc.DocumentElement, "YieldProphet");
            Utility.Xml.ChangeType(ypNode, "YieldProphetSpec");

            XmlNode rainfall = doc.DocumentElement.AppendChild(doc.CreateElement("RainfallFileContents"));
            rainfall.InnerText = rainFileContents;

            // Create a job name to store in DB
            string loginName = Utility.Xml.Value(doc.DocumentElement, "YieldProphetSpec/LoginName");
            string name = DateTime.Now.ToString("yyyy-MM-dd (hh-mm-ss tt) ") + loginName;

            // Create a row in the DB for this job.
            JobsDB jobsDB = new JobsDB();
            jobsDB.Open();
            jobsDB.Add(name, doc.DocumentElement.OuterXml);
            jobsDB.Close();
        }

        /// <summary>Creates the contents of an .apsim file from the specified YP xml file</summary>
        /// <param name="paddockXML">The paddock XML.</param>
        //[WebMethod]
        //public string CreateAPSIMFile(string paddockXML)
        //{
        //    YieldProphet yieldProphetSpec = GetYieldProphetStructure(paddockXML);
        //    XmlNode node = yieldProphetSpec.CreateApsimFile();

        //    return Utility.Xml.FormattedXML(node.OuterXml);
        //}

        /// <summary>Gets the yield prophet structure.</summary>
        /// <param name="paddockXML">The paddock XML.</param>
        /// <returns>The yield prophet strucure</returns>
        private static YieldProphetSpec GetYieldProphetStructure(string paddockXML)
        {
            // Deserialise paddockXML to a YieldProphet instance.
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(paddockXML);
            XmlNodeReader reader = new XmlNodeReader(doc);

            XmlSerializer serial = new XmlSerializer(typeof(YieldProphetSpec));
            object y = serial.Deserialize(reader);
            YieldProphetSpec yieldProphetSpec = y as YieldProphetSpec;
            return yieldProphetSpec;
        }

    }
}