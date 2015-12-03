using System;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Windows.Forms;

namespace PosUpdater
{
    public class PosUpdaterConfig
    {
        static PosUpdaterConfig()
        {
            PathToConfig = Path.Combine(Application.StartupPath, "PosUpdaterConfig.xml");
        }

        #region public members
        /// <summary>
        /// Restart PosUpdater 
        /// </summary>
        public bool RelaunchApplication { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public bool UpdaterCantExit { get; set; }

        /// <summary>
        /// Log updater work
        /// </summary>
        public bool UpdaterDoLogging { get; set; }

        /// <summary>
        /// Show updater console form (for testing)
        /// </summary>
        public bool UpdaterShowConsole { get; set; }

        /// <summary>
        /// Path to Feed.xml
        /// </summary>
        public string FeedFilePath { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// RSC windows service name
        /// </summary>
        public string RcsServiceName { get; set; }

        /// <summary>
        /// Restart VisitCounter windows service
        /// </summary>
        public bool RestartVisitCounter { get; set; }

        /// <summary>
        /// Close POS.exe if it run before start update
        /// </summary>
        public bool RestartPos { get; set; }

        /// <summary>
        /// Restart RSC windows service
        /// </summary>
        public bool RestartRsc { get; set; }

        /// <summary>
        /// Create backup DB before start update
        /// </summary>
        public bool BackupMsSqlDb { get; set; }

        public bool LoadFromDataBase { get; set; }

        public bool NotUpdate { get; set; }

        #endregion public members

        public static string PathToConfig { get; set; }

        private static PosUpdaterConfig _posUpdaterConfig;
        public static PosUpdaterConfig Instance
        {
            get { return _posUpdaterConfig ?? (_posUpdaterConfig = Load()); }
        }

        #region Serializer
        private static PosUpdaterConfig DefaultSettings()
        {
            var settings = new PosUpdaterConfig
                               {
                                   RelaunchApplication = true,
                                   UpdaterShowConsole = false,
                                   UpdaterDoLogging = true,
                                   FeedFilePath = Path.Combine(Application.StartupPath, "UpdateFeed.xml"),
                                   ConnectionString = @"Data Source=.\sqlexpress; Initial Catalog=AxRetailPOS; Integrated Security=SSPI; Persist Security Info=false; Pooling=false;",
                                   RestartVisitCounter = true,
                                   RestartPos = true
                               };
            return settings;
        }

        public static PosUpdaterConfig Load()
        {
            //var fileName = Path.Combine(Application.StartupPath, "PosUpdaterConfig.xml");
            return !File.Exists(PathToConfig) ? DefaultSettings() : Load(PathToConfig);
        }

        public static PosUpdaterConfig Load(string pathToConfig)
        {
            if(string.IsNullOrEmpty(pathToConfig))
                throw new ArgumentNullException("pathToConfig");

            using (var reader = File.OpenRead(pathToConfig))
            {
                var serializer = new XmlSerializer(typeof(PosUpdaterConfig));
                return (PosUpdaterConfig)serializer.Deserialize(reader);
            }
        }

        public static void Save(PosUpdaterConfig posUpdaterConfig)
        {
            if(posUpdaterConfig==null)
                throw new ArgumentNullException("posUpdaterConfig");

            var fileName = Path.Combine(Application.StartupPath, "PosUpdaterConfig.xml");
            using (var writer = new StreamWriter(fileName))
            {
                var serializer = new XmlSerializer(typeof(PosUpdaterConfig));
                serializer.Serialize(writer, posUpdaterConfig);
            }
        }

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
                TerminalId = xmlAttributeCollection["TerminalId"].Value,
                StoreId = xmlAttributeCollection["StoreId"].Value,
                DataAreaId = xmlAttributeCollection["DATAAREAID"].Value
            };
            return conf;
        }

        public class PosConfig
        {
            public PosConfig()
            {
                ConnectString =
                    @"Data Source=.\sqlexpress; Initial Catalog=AxRetailPOS; Integrated Security=SSPI; Persist Security Info=false; Pooling=false;";
                StoreId = "0000";
                TerminalId = "0000";
                DataAreaId = "dat";
            }
            public string ConnectString { get; set; }
            public string StoreId { get; set; }
            public string TerminalId { get; set; }
            public string DataAreaId { get; set; }
        }
        #endregion Serializer
    }
}