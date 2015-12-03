using System;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

namespace PosUpdater
{
    public class PosParameters
    {
        static PosParameters()
        {
            PathToConfig = Path.Combine(Application.StartupPath, "PosParameters.xml");
        }

        #region public members

        public string StringPosVersion { get; set; }

        public Version PosVersion
        {
            get
            {
                return new Version(string.IsNullOrEmpty(StringPosVersion) ? "0.0.0.0" : StringPosVersion); 
            }
        }

        public bool PosBlock { get; set; }

        #endregion public members

        public static string PathToConfig { get; set; }

        private static PosParameters _posParameters;

        public static PosParameters Instance
        {
            get { return _posParameters ?? (_posParameters = Load()); }
        }

        #region Serializer

        private static PosParameters DefaultSettings()
        {
            var settings = new PosParameters
            {
                StringPosVersion = "1.0.0.0",
                PosBlock = true
            };
            return settings;
        }

        public static PosParameters Load()
        {
            return !File.Exists(PathToConfig) ? DefaultSettings() : Load(PathToConfig);
        }

        public static PosParameters Load(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException("path");

            using (var reader = File.OpenRead(path))
            {
                var serialize = new XmlSerializer(typeof (PosParameters));
                return (PosParameters) serialize.Deserialize(reader);
            }
        }

        public static void Save(PosParameters posParameters)
        {
            if (posParameters == null)
                throw new ArgumentNullException("posParameters");

            var fileName = PathToConfig;
            using (var writer = new StreamWriter(fileName))
            {
                var serialize = new XmlSerializer(typeof (PosParameters));
                serialize.Serialize(writer, posParameters);
            }
        }

        /// <summary>
        /// Load data from POS.exe.config
        /// </summary>
        /// <returns></returns>
        public static PosConfig LoadPosConfig()
        {
            if (!File.Exists("POS.exe.config"))
                return new PosConfig();

            var config = new XmlTextReader("POS.exe.config");
            var doc = new XmlDocument();
            doc.Load(config);
            var res = doc.GetElementsByTagName("AxRetailPOS");
            var xmlAttributeCollection = res[0].Attributes;
            if (xmlAttributeCollection == null)
                return new PosConfig();

            var conf = new PosConfig
            {
                ConnectString = xmlAttributeCollection["LocalConnectionString"].Value,
                TerminalId    = xmlAttributeCollection["TerminalId"].Value,
                StoreId       = xmlAttributeCollection["StoreId"].Value,
                DataAreaId    = xmlAttributeCollection["DATAAREAID"].Value
            };
            return conf;
        }

        public class PosConfig
        {
            public string ConnectString { get; set; }
            public string StoreId { get; set; }
            public string TerminalId { get; set; }
            public string DataAreaId { get; set; }
        }

        #endregion Serializer
    }
}