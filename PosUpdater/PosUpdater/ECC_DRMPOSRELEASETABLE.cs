using System;

namespace PosUpdater
{
    [PetaPoco.PrimaryKey("RECID")]
    public class ECC_DRMPOSRELEASETABLE
    {
        public string DATAAREAID { get; set; }


        public Int64 RECID { get; set; }

        public string DESCRIPTION { get; set; }

        public int APPROVED { get; set; }

        public int NUMBEROFLINES { get; set; }

        public string RELEASEID { get; set; }

        [PetaPoco.Ignore]
        public Version ReleaseVersion
        {
            get
            {
                return new Version(RELEASEID);
            }
        }
    }
}