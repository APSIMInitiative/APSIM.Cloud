// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------
namespace APSIM.Cloud.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.IO;

    class Program
    {
        /// <summary>Main program</summary>
        /// <param name="args">The arguments.</param>
        /// <returns>True 1 an error occurred</returns>
        static int Main(string[] args)
        {
            try
            {
                Dictionary<string, string> arguments = Utility.String.ParseCommandLine(args);

                string command = string.Empty;
                if (arguments.ContainsKey("Command"))
                    command = arguments["Command"];

                if (command == "ConvertXML" && arguments.ContainsKey("FileName"))
                    ConvertFile(arguments["FileName"]);
                else if (command == "CreateAPSIMFiles" && arguments.ContainsKey("FileName"))
                {
                    Specification.YieldProphetSpec yieldProphet = YieldProphetServices.Create(GetFileContents(arguments["FileName"]));
//                    if (arguments.ContainsKey("FilterFileName"))
//                        YieldProphetServices.CreateFiles(yieldProphet, Directory.GetCurrentDirectory(), arguments["FilterFileName"]);
//                    else
                        YieldProphetServices.ToAPSIM(yieldProphet, Directory.GetCurrentDirectory());
                }
                else if (command == "ReplaceSoilName" && arguments.ContainsKey("FileName"))
                    ReplaceSoilLinkWithSoil.Go(arguments["FileName"]);
                else
                {
                    // Fall through to an error.
                    throw new Exception("Usage: APSIM.Cloud.Services Command=ConvertXML FileName=abc.xml\r\n" +
                                        "                            Command=CreateAPSIMFiles FileName=abc.xml");
                }
            }
            catch (Exception err)
            {
                Console.WriteLine(err.Message);
                return 1;
            }

            return 0;
        }

        /// <summary>Get the contents of the specified file.</summary>
        /// <param name="fileName">Name of the file.</param>
        /// <returns></returns>
        private static string GetFileContents(string fileName)
        {
            StreamReader reader = new StreamReader(fileName);
            string newXMLContents = reader.ReadToEnd();
            reader.Close();
            return newXMLContents;
        }


        /// <summary>Converts the old Yield Prophet XML file format</summary>
        /// <param name="fileName">Name of the file.</param>
        /// <exception cref="System.Exception">Cannot find file:  + fileName</exception>
        public static void ConvertFile(string fileName)
        {
            if (!File.Exists(fileName))
                throw new Exception("Cannot find file: " + fileName);
            StreamReader reader = new StreamReader(fileName);
            string newXML = YieldProphetOld.Convert(reader.ReadToEnd());
            reader.Close();
            StreamWriter writer = new StreamWriter(fileName);
            writer.Write(newXML);
            writer.Close();
        }
    }
}
