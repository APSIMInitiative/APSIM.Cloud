// -----------------------------------------------------------------------
// <copyright file="Service.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
//-----------------------------------------------------------------------
namespace APSIM.Cloud.WebService
{
    using System;
    using System.Web.Services;
    using APSIM.Cloud.Services;
    using APSIM.Cloud.Services.Specification;

    /// <summary>
    /// A web service for the APSIM cloud.
    /// </summary>
    [WebService(Namespace = "http://www.apsim.info/")]
    [WebServiceBinding(ConformsTo = WsiProfiles.BasicProfile1_1)]
    [System.ComponentModel.ToolboxItem(false)]
    public class APSIMCloud : System.Web.Services.WebService
    {

        /// <summary>
        /// Adds the job to the APSIM cloud.
        /// </summary>
        /// <param name="yieldProphet">The job specification.</param>
        /// <returns>The unique job name.</returns>
        [WebMethod]
        public string AddJob(YieldProphetSpec yieldProphet)
        {
            DateTime nowDate = DateTime.Now;

            foreach (Paddock paddock in yieldProphet.PaddockList)
                paddock.NowDate = nowDate;
            string newJobName = nowDate.ToString("yyyy-MM-dd (h-mm-ss tt) ") + yieldProphet.ReportName;

            string xml = YieldProphetServices.ToXML(yieldProphet);

            JobsDB jobsDB = new JobsDB();
            jobsDB.Open();
            jobsDB.Add(newJobName, xml);
            jobsDB.Close();
            return newJobName;
        }
    }
}