using System;
using System.Data.SqlClient;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PosUpdater;
using PosUpdateService;

namespace UpdaterUnitTest
{
    [TestClass]
    public class Sql
    {
        private string connectStr =
            @"Data Source=STATION1903132\SQLEXPRESS;Initial Catalog=VPARK;Integrated Security=SSPI;Persist Security Info=false;Pooling=false;TrustServerCertificate=true;Encrypt=TRUE";
        
        //[TestMethod]
        public void TestBackup()
        {
            Assert.IsTrue(BackupMsSqlDb.Backup());
        }

        [TestMethod]
        public void TestExportFiles()
        {
            LoadFromDb.BcpExportFileFromDataBase(
                "1.0.0.0",
                @"C:\tmp\test_01\",
                connectStr);
            //Path.Combine(zipDirPath, releaseId + ".out")
            LoadFromDb.UnZipFile(@"C:\tmp\test_01\1.0.0.0.out", @"C:\tmp\");
        }

        [TestMethod]
        public void TestSqlCount()
        {
            var res = LoadFromDb.GetSqlConnectCount(
                connectStr,
                "VPARK");
            Assert.IsTrue(res > 0);
        }

        [TestMethod]
        public void TestSqlVersions()
        {
            var res = LoadFromDb.GetVersions(connectStr, new[] { 1 });
            Assert.IsTrue(res.Count > 0);
            Assert.IsTrue(res.FirstOrDefault(am=>am.ReleaseVersion.Equals(new Version("1.0.0.0"))) != null);
        }

        [TestMethod]
        public void TestExecSql()
        {
            LoadFromDb.ExecuteSql(@"SELECT 'test'", connectStr);
            try
            {
                LoadFromDb.ExecuteSql(@"SELECT 1 from test", connectStr);
                Assert.Fail();
            }
            catch
            {
            }
        }

        //[TestMethod]
        public void TestUpdateFromDb()
        {
            LoadFromDb.LoadUpdateFromDb();
        }

        [TestMethod]
        public void TestWriteToDb()
        {
            var res = LoadFromDb.GetVersions(connectStr, new[] { 1 });
            Assert.IsTrue(res.Count > 0);
            var row = res.FirstOrDefault(am => am.ReleaseVersion.Equals(new Version("1.0.0.0")));
            Assert.IsTrue( row != null);

            row.APPROVED = 3;
            LoadFromDb.UpdateReleaseRowInDataBase(row, connectStr);

            res = LoadFromDb.GetVersions(connectStr, new[] {3});
            Assert.IsTrue(res.Count > 0);
            row = res.FirstOrDefault(am => am.ReleaseVersion.Equals(new Version("1.0.0.0")));
            Assert.IsTrue(row != null);

            row.APPROVED = 1;
            LoadFromDb.UpdateReleaseRowInDataBase(row, connectStr);

        }

        [TestMethod]
        public void TestGetCountConnectionsToDb()
        {
            SqlConnection openConnect;
            var countConnect = LoadFromDb.GetOpenConnection(connectStr, out openConnect);
            Assert.IsTrue(countConnect>0);

        }

    }
}