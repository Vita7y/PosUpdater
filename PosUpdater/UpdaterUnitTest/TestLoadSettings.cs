using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PosUpdater;

namespace UpdaterUnitTest
{
    [TestClass]
    public class TestLoadSettings
    {
        [TestMethod]
        public void TestPosConfigLoad()
        {
            var posConfig = PosUpdaterConfig.LoadPosConfig();
            Assert.IsNotNull(posConfig);
            Assert.AreNotEqual(string.Empty, posConfig.ConnectString);
            Assert.AreEqual(@"Data Source=STATION1903132\SQLEXPRESS;Initial Catalog=VPARK;Integrated Security=SSPI;Persist Security Info=false;Pooling=false;TrustServerCertificate=true;Encrypt=TRUE", 
                posConfig.ConnectString);
            Assert.AreEqual("m004", posConfig.StoreId);
            Assert.AreEqual("0011", posConfig.TerminalId);
            Assert.AreEqual("dat", posConfig.DataAreaId);
        }

        [TestMethod]
        public void TestSaveLog()
        {
            var logger = LogManager.CurrentLogger;
            logger.Info("Test message");
            logger.Error("Test Error");
            logger.ErrorException("Test", new Exception("Test Exception"));
        }
    }
}
