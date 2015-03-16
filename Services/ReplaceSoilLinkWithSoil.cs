// -----------------------------------------------------------------------
// <copyright file="ReplaceSoilLinkWithSoil.cs" company="APSIM Initiative">
//     Copyright (c) APSIM Initiative
// </copyright>
// -----------------------------------------------------------------------
namespace APSIM.Cloud.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using ApsimFile;
    using System.Xml.Serialization;
    using System.IO;

    /// <summary>
    /// A class the can replace SoilName links in a paddock with a soil object.
    /// </summary>
    class ReplaceSoilLinkWithSoil
    {
        /// <summary>
        /// Loops through all paddocks in the specified yield prophet specification and
        /// replaces SoilName links with a soil object.
        /// </summary>
        public static void Go(string fileName)
        {
            StreamReader reader = new StreamReader(fileName);
            XmlSerializer serial = new XmlSerializer(typeof(Specification.YieldProphetSpec));
            Specification.YieldProphetSpec yieldProphet = serial.Deserialize(reader) as Specification.YieldProphetSpec;
            reader.Close();

            APSOIL.Service apsoilService = new APSOIL.Service();
            foreach (Specification.Paddock paddock in yieldProphet.PaddockList)
            {
                throw new NotImplementedException();
                //APSIMFiles.DoSoil(paddock, apsoilService);
            }

            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "");
            StringWriter writer = new StringWriter();
            serial.Serialize(writer, yieldProphet, ns);
            string xml = writer.ToString();
            if (xml.Length > 5 && xml.Substring(0, 5) == "<?xml")
            {
                // remove the first line: <?xml version="1.0"?>/n
                int posEol = xml.IndexOf("\n");
                if (posEol != -1)
                    xml = xml.Substring(posEol + 1);
            }

            StreamWriter writer2 = new StreamWriter(fileName);
            writer2.Write(xml);
            writer2.Close();
        }
    }
}
