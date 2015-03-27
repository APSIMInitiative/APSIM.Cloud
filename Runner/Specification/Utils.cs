// -----------------------------------------------------------------------
// <copyright file="Utility.cs" company="CSIRO">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace APSIM.Cloud.Specification
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using APSIM.Cloud.Runner.JobsService;

    /// <summary>
    /// TODO: Update summary.
    /// </summary>
    public class Utils
    {

        /// <summary>Return a C:N ratio for the specified stubble type.</summary>
        /// <param name="StubbleType">Type of the stubble.</param>
        /// <returns></returns>
        public static int GetStubbleCNRatio(string StubbleType)
        {
            string[] StubbleTypes = {"barley", "canola", "chickpea", "fababean", "fieldpea", "grass", "lentils",
                                     "lucerne", "lupin", "medic", "oats", "sorghum", "triticale", "vetch",
                                     "weeds",   "wheat"};
            int[] CNRatios = { 80, 120, 42, 42, 29, 80, 42, 42, 42, 42, 80, 80, 80, 42, 80, 80 };

            int PosStubble = Utility.String.IndexOfCaseInsensitive(StubbleTypes, StubbleType);
            if (PosStubble != -1)
                return CNRatios[PosStubble];
            else
                return 80;
        }

        /// <summary>Gets the crop being sown or null if no crop</summary>
        /// <param name="paddock">The paddock.</param>
        /// <returns></returns>
        public static Sow GetCropBeingSown(IEnumerable<Management> managerActions)
        {
            // Loop through all management actions and create an operations list
            foreach (Management management in managerActions)
            {
                if (management is Sow)
                    return (management as Sow);
            }

            return null;
        }

        /// <summary>Calculates the average tillage percent.</summary>
        /// <param name="tillages">The tillages.</param>
        /// <returns>The percentage stubble incorporated in the top 10cm</returns>
        public static double CalculateAverageTillagePercent(IEnumerable<Tillage> tillages)
        {
            double sum = 0;
            foreach (Tillage tillage in tillages)
            {
                if (tillage.Disturbance == Tillage.DisturbanceEnum.Low)
                    sum += 20;
                else if (tillage.Disturbance == Tillage.DisturbanceEnum.Medium)
                    sum += 50;
                else if (tillage.Disturbance == Tillage.DisturbanceEnum.High)
                    sum += 80;
            }
            return sum / tillages.Count();
        }


    }
}
