// -----------------------------------------------------------------------
// <copyright file="Farm4ProphetUtility.cs" company="CSIRO">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace APSIM.Cloud.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Xml;
    using System.Xml.Serialization;
    using System.IO;
    using System.Data;
    using System.Reflection;
    using APSIM.Shared.Utilities;

    public class Farm4ProphetUtility
    {
        /// <summary>Factory method for creating a Farm4Prophet object from a file.</summary>
        /// <param name="fileName">The filename of the xml file</param>
        /// <returns>The newly created object.</returns>
        public static Farm4Prophet Farm4ProphetFromFile(string fileName)
        {
            XmlDocument doc = new XmlDocument();
            doc.Load(fileName);
            XmlReader reader = new XmlNodeReader(doc.DocumentElement);
            reader.Read();
            XmlSerializer serial = new XmlSerializer(typeof(Farm4Prophet));
            return (Farm4Prophet)serial.Deserialize(reader);
        }

        /// <summary>Factory method for creating a Farm4Prophet object.</summary>
        /// <param name="xml">The XML to use to create the object</param>
        /// <returns>The newly created object.</returns>
        public static Farm4Prophet Farm4ProphetFromXML(string xml)
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xml);

            XmlReader reader = new XmlNodeReader(doc.DocumentElement);
            reader.Read();
            XmlSerializer serial = new XmlSerializer(typeof(Farm4Prophet));
            return (Farm4Prophet)serial.Deserialize(reader);
        }

        /// <summary>Convert the Farm4Prophet spec to XML.</summary>
        /// <returns>The XML string.</returns>
        public static string Farm4ProphetToXML(Farm4Prophet f4Prophet)
        {
            XmlSerializer serial = new XmlSerializer(typeof(Farm4Prophet));
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "");
            StringWriter writer = new StringWriter();
            serial.Serialize(writer, f4Prophet, ns);
            string xml = writer.ToString();
            if (xml.Length > 5 && xml.Substring(0, 5) == "<?xml")
            {
                // remove the first line: <?xml version="1.0"?>/n
                int posEol = xml.IndexOf("\n");
                if (posEol != -1)
                    return xml.Substring(posEol + 1);
            }
            return xml;
        }


    }
}
