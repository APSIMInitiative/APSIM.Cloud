﻿// -----------------------------------------------------------------------
// <copyright file="APSIMPostSimulationJob.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------
namespace APSIM.Cloud.Runner.RunnableJobs
{
    using System.IO;
    using System.Collections.Generic;
    using System.Xml;
    using APSIM.Shared.Utilities;
    using System.Data;
    using System.ComponentModel;
    using System;
    using APSIM.Cloud.Shared;

    /// <summary>
    /// A runnable class to run a series of post simulation cleanup functions.
    /// </summary>
    public class APSIMPostSimulation
    {
        /// <summary>Called to start the job.</summary>
        /// <param name="workingFolder">Folder to work on</param>
        public static DataSet PerformCleanup(string workingFolder)
        {
            // Delete the .sim files.
            string[] simFiles = Directory.GetFiles(workingFolder, "*.sim");
            foreach (string simFile in simFiles)
                File.Delete(simFile);

            bool longtermOutputsFound = false;

            // Cleanup longterm out and sum files. They are named XXX_01 Yearly.out
            // where XXX is the name of the simulation.
            foreach (string apsimFileName in Directory.GetFiles(workingFolder, "*.apsim"))
            {
                List<XmlNode> simulationNodes = new List<XmlNode>();
                XmlDocument doc = new XmlDocument();
                doc.Load(apsimFileName);
                XmlUtilities.FindAllRecursivelyByType(doc.DocumentElement, "simulation", ref simulationNodes);

                // Concatenate summary files.
                foreach (XmlNode simNode in simulationNodes)
                {
                    string simName = XmlUtilities.NameAttr(simNode);
                    string[] sumFiles = Directory.GetFiles(workingFolder, simName + "_*.sum");
                    if (sumFiles.Length > 0)
                    {
                        longtermOutputsFound = true;
                        ConcatenateSummaryFiles(sumFiles, simName + ".sum");
                    }
                }

                // Concatenate output files.
                SortedSet<string> outputTypes = new SortedSet<string>();
                foreach (XmlNode simNode in simulationNodes)
                {
                    string simulationName = XmlUtilities.NameAttr(simNode);
                    foreach (string outputType in Directory.GetFiles(workingFolder, simulationName + "_*1*.out"))
                    {
                        string outputFileType = Path.GetFileNameWithoutExtension(outputType.Replace(simulationName, ""));
                        outputFileType = " " + StringUtilities.SplitOffAfterDelimiter(ref outputFileType, " ");
                        outputTypes.Add(outputFileType);
                    }
                }

                foreach (XmlNode simNode in simulationNodes)
                {
                    string simName = XmlUtilities.NameAttr(simNode);
                    foreach (string outputFileType in outputTypes)
                    {
                        string wildcard = simName + "_*" + outputFileType + ".out";
                        string[] outFiles = Directory.GetFiles(workingFolder, wildcard);
                        string fileNameToWrite = simName + outputFileType + ".csv";
                        ConcatenateOutputFiles(outFiles, fileNameToWrite, outputFileType);
                    }
                }
            }

            if (!longtermOutputsFound)
            {
                // By now the longterm .out and .sum files have been concatenated. Assume
                // all simulations are the same; get the different types of reports for each simulation
                // and concatenate.
                string[] apsimFiles = Directory.GetFiles(workingFolder, "*.apsim");
                if (apsimFiles.Length == 0)
                    apsimFiles = Directory.GetFiles(workingFolder, "*.apsimx");
                string apsimFileName1 = apsimFiles[0];
                string[] allSumFiles = Directory.GetFiles(workingFolder, "*.sum");
                ConcatenateSummaryFiles(allSumFiles, Path.ChangeExtension(apsimFileName1, ".sum"));

                XmlDocument doc1 = new XmlDocument();
                doc1.Load(apsimFileName1);

                foreach (XmlNode simulationNode in XmlUtilities.ChildNodes(doc1.DocumentElement, "simulation"))
                {
                    if (simulationNode != null)
                    {
                        string simulationName = XmlUtilities.NameAttr(simulationNode);
                        string[] outFileTypes = Directory.GetFiles(workingFolder, simulationName + "*.out"); 
                        if (outFileTypes.Length == 0)
                            outFileTypes = Directory.GetFiles(workingFolder, simulationName + "*.csv");
                        foreach (string outputfileName in outFileTypes)
                        {
                            string outputFileType = Path.GetFileNameWithoutExtension(outputfileName.Replace(simulationName, ""));
                            string wildcard = "*" + outputFileType + Path.GetExtension(outputfileName);
                            string[] outFiles = Directory.GetFiles(workingFolder, wildcard);
                            string fileNameToWrite = Path.GetFileNameWithoutExtension(apsimFileName1) + outputFileType + ".csv";
                            ConcatenateOutputFiles(outFiles, fileNameToWrite, outputFileType);
                        }
                    }
                }
            }

            // zip up the met files.
            string[] metFiles = Directory.GetFiles(workingFolder, "*.met");
            ZipFiles(metFiles, Path.Combine(workingFolder, "MetFiles.zip"));

            // Get all outputs
            DataSet dataSet = new DataSet("ReportData");
            foreach (string outFileName in Directory.GetFiles(workingFolder, "*.csv"))
                try
                {
                    dataSet.Tables.Add(ApsimTextFile.ToTable(outFileName));
                }
                catch (Exception)
                {
                    // Sometimes .out files are empty - not an error.
                }
            foreach (string outFileName in Directory.GetFiles(workingFolder, "*.out"))
                try
                {
                    dataSet.Tables.Add(ApsimTextFile.ToTable(outFileName));
                }
                catch (Exception)
                {
                    // Sometimes .out files are empty - not an error.
                }

            // Clean the table names (no spaces or underscores)
            foreach (DataTable table in dataSet.Tables)
            {
                string tableName = table.TableName.Replace(" ", "");
                tableName = tableName.Replace("_", "");
                table.TableName = tableName;
            }
            return dataSet;
        }

        /// <summary>Concatenates the specified output files into one file.</summary>
        /// <param name="outFiles">The out files.</param>
        private static void ConcatenateOutputFiles(string[] outFiles, string fileName, string outputFileType)
        {
            if (outFiles.Length > 0)
            {
                // Assume they are all structured the same i.e. same headings and units.
                // Read in data from all files.
                DataTable allData = null;
                foreach (string outputFileName in outFiles)
                {
                    ApsimTextFile reader = new ApsimTextFile();
                    reader.Open(outputFileName);

                    List<string> constantsToAdd = new List<string>();
                    constantsToAdd.Add("Title");
                    DataTable data = reader.ToTable(constantsToAdd);
                    reader.Close();

                    if (data.Columns.Count > 0 && data.Rows.Count > 0)
                    {
                        if (allData == null)
                            allData = data;
                        else
                            DataTableUtilities.CopyRows(data, allData);
                    }
                }

                if (allData != null)
                {
                    // Move the title column to be first.
                    allData.Columns["Title"].SetOrdinal(0);

                    // Strip off the outputFileType (e.g. Yearly) from the titles.
                    foreach (DataRow row in allData.Rows)
                        row["Title"] = row["Title"].ToString().Replace(outputFileType, "");

                    // Write data.
                    string workingFolder = Path.GetDirectoryName(outFiles[0]);
                    string singleOutputFileName = Path.Combine(workingFolder, fileName);
                    StreamWriter outWriter = new StreamWriter(singleOutputFileName);

                    DataTableUtilities.DataTableToText(allData, 0, ",  ", true, outWriter);

                    outWriter.Close();
                }

                // Delete the .out files.
                foreach (string outputFileName in outFiles)
                    File.Delete(outputFileName);
            }
        }

        /// <summary>Concatenates the summary files.</summary>
        /// <param name="sumFiles">The sum files to concatenate</param>
        private static void ConcatenateSummaryFiles(string[] sumFiles, string fileName)
        {
            if (sumFiles.Length > 0)
            {
                string workingFolder = Path.GetDirectoryName(sumFiles[0]);
                string singleSummaryFileName = Path.Combine(workingFolder, fileName);
                StreamWriter sumWriter = new StreamWriter(singleSummaryFileName);

                foreach (string summaryFileName in sumFiles)
                {
                    StreamReader sumReader = new StreamReader(summaryFileName);
                    sumWriter.Write(sumReader.ReadToEnd());
                    sumReader.Close();
                }

                sumWriter.Close();

                SummaryFileParser summaryFile = new SummaryFileParser();
                summaryFile.Open(singleSummaryFileName);

                // Delete the .sum files.
                foreach (string summaryFileName in sumFiles)
                    File.Delete(summaryFileName);

                string error = summaryFile.GetAPSIMError();
                if (error != null)
                    throw new Exception(error);

            }
        }

        /// <summary>Zips the files.</summary>
        /// <param name="intoFileName">The name of the file to create.</param>
        /// <param name="fileNames">The file names to zip.</param>
        private static void ZipFiles(string[] fileNames, string intoFileName)
        {
            // Zip up files.
            ZipUtilities.ZipFiles(fileNames, null, intoFileName);

            // Delete the .met files.
            foreach (string fileName in fileNames)
                File.Delete(fileName);
        }

    }
}