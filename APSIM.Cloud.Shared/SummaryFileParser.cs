using APSIM.Shared.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APSIM.Cloud.Shared
{
    /// <summary>
    /// This class can parse a .sum file that has multiple summary files concatenated together.
    /// This type of .sum file comes from APSIM.Cloud.
    /// </summary>
    public class SummaryFileParser
    {
        /// <summary>The contents of the entire summary file.</summary>
        private string contents;

        /// <summary>Each individual summary file</summary>
        private List<int> indexSummaryFiles = new List<int>();

        /// <summary>Open the specified summary file.</summary>
        /// <param name="fileName"></param>
        public void Open(string fileName)
        {
            // Open the summary file and read entire contents.
            using (StreamReader reader = new StreamReader(fileName))
                contents = reader.ReadToEnd();

            // Loop through each summary section and store the start index for each.
            string banner = "     ###     ######     #####   #   #     #";
            int findStartSection = contents.IndexOf(banner);
            while (findStartSection != -1)
            {
                indexSummaryFiles.Add(findStartSection);
                findStartSection = contents.IndexOf(banner, findStartSection+1);
            }
        }

        /// <summary>Return the APSIM revision used to create the summary file.</summary>
        public string GetVersion()
        {
            if (indexSummaryFiles.Count == 0)
                throw new Exception("No summary file found");

            int versionIndex = contents.IndexOf("Version                =");
            if (versionIndex == -1)
                throw new Exception("Cannot find version number in summary file");
            string versionLine = GetLine(ref versionIndex);
            return StringUtilities.SplitOffAfterDelimiter(ref versionLine, "=");
        }


        /// <summary>Return the first APSIM fatal error or null if none.</summary>
        /// <param name="title">The title of the summary section to search.</param>
        public string GetAPSIMError(string title = null)
        {
            string summary = GetSummarySection(title);
            if (summary != null)
            {
                int posFatal = summary.IndexOf("APSIM  Fatal  Error");
                if (posFatal == -1)
                    return null;

                IgnoreLine(ref posFatal);
                IgnoreLine(ref posFatal);

                int posEndError = summary.IndexOf("!!!!!!!!", posFatal);
                if (posEndError != -1)
                {
                    return summary.Substring(posFatal, posEndError - posFatal).Trim();
                }
                return GetLine(ref posFatal).Trim();
            }
            else
                return null;
        }

        /// <summary>Get a summary section.</summary>
        /// <param name="title">The title of the section to retrieve.</param>
        private string GetSummarySection(string title)
        {
            for (int i = 0; i < indexSummaryFiles.Count; i++)
            {
                int startIndex = indexSummaryFiles[i];
                int length;
                if (i == indexSummaryFiles.Count - 1)
                    length = contents.Length - startIndex;
                else
                    length = indexSummaryFiles[i + 1] - indexSummaryFiles[i];

                int titleIndex = contents.IndexOf("Title                  =", startIndex, length);
                if (titleIndex != -1)
                {
                    string summaryTitleLine = GetLine(ref titleIndex);
                    string summaryTitle = StringUtilities.SplitOffAfterDelimiter(ref summaryTitleLine, "=");

                    if (title == null || summaryTitle == title)
                        return contents.Substring(startIndex, length);
                }

            }

            return null;
        }

        /// <summary>Get the next line starting at pos.</summary>
        /// <param name="pos">The position within contents</param>
        private string GetLine(ref int pos)
        {
            int posEOLN = contents.IndexOf("\r\n", pos);
            if (posEOLN == -1)
                posEOLN = contents.Length;
            string st = contents.Substring(pos, posEOLN - pos);
            pos = posEOLN + 2;
            return st;
        }

        /// <summary>Ignore the next line starting at pos.</summary>
        /// <param name="pos">The position within contents</param>
        private void IgnoreLine(ref int pos)
        {
            int posEOLN = contents.IndexOf("\r\n", pos);
            if (posEOLN == -1)
                pos = contents.Length;
            else
                pos = posEOLN + 2;
        }


    }
}
