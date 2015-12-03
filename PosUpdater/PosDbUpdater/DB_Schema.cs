using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using System.Xml.Serialization;

namespace PosDbUpdater
{
    [PetaPoco.ExplicitColumns]
    public class DB_Schema
    {
        [PetaPoco.Column, XmlIgnore, ReadOnly(true)]
        public string Name { get; set; }

        [PetaPoco.Column, XmlIgnore, ReadOnly(true)]
        public string Type { get; set; }

        [PetaPoco.Column, XmlIgnore, ReadOnly(true)]
        public string MD5 { get; set; }

        [PetaPoco.Ignore, XmlIgnore, ReadOnly(true)]
        public string Condition { get; set; }

        public override string ToString()
        {
            return string.Format("{0}; {1}; {2}", Condition, Type, Name);
        }
    }

    public static class DB_SchemaHelper
    {
        public static IEnumerable<DB_Schema> GetDbSchema(string connectString)
        {
            using (var connect = new SqlConnection(connectString))
            {
                connect.Open();
                try
                {
                    using (var db = new PetaPoco.Database(connect))
                    {
                        var res = db.Query<DB_Schema>("SELECT NAME, TYPE, MD5 FROM dbo.ECCO_CREATE_DB_SHEM_VW").ToList();
                        return res;
                    }
                }
                catch (Exception er)
                {
                    er.WriteToLog();
                    throw;
                }
            }
        }

    }
}