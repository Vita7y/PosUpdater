using System;
using System.IO;
using System.Diagnostics;
using NAppUpdate.Framework.Common;

namespace NAppUpdate.Framework.Conditions
{
    [Serializable]
    [UpdateConditionAlias("version")]
    public class FileVersionCondition : IUpdateCondition
    {

        [NauField("localPath",
            "The local path of the file to check. If not set but set under a FileUpdateTask, the LocalPath of the task will be used. Otherwise this condition will be ignored."
            , false)]
        public string LocalPath { get; set; }

        private string _version;

        [NauField("version", "Version string to check against", true)]
        public string Version
        {
            get { return _version; }
            set
            {
                _version = VersionNormalization(value);
                ;
            }
        }

        [NauField("what", "Comparison action to perform. Accepted values: above, is, below. Default: below.", false)]
        public string ComparisonType { get; set; }

        #region IUpdateCondition Members

        public bool IsMet(Tasks.IUpdateTask task)
        {
            string localPath = !string.IsNullOrEmpty(LocalPath)
                                   ? LocalPath
                                   : Utils.Reflection.GetNauAttribute(task, "LocalPath") as string;
            if (string.IsNullOrEmpty(localPath)
                || !File.Exists(localPath))
                return true;

            var versionInfo = FileVersionInfo.GetVersionInfo(localPath);
            if (versionInfo.FileVersion == null) return true; // perform the update if no version info is found
            string versionString = VersionNormalization(versionInfo.FileVersion);
            Version localVersion = new Version(versionString);
            Version updateVersion = Version != null ? new Version(Version) : new Version();

            switch (ComparisonType)
            {
                case "above":
                    return updateVersion < localVersion;
                case "is":
                    return updateVersion == localVersion;
                default:
                    return updateVersion > localVersion;
            }
        }

        #endregion

        public static string VersionNormalization(string str)
        {
            if (string.IsNullOrEmpty(str))
                return string.Empty;

            //10.50.1750.9((dac_inplace_upgrade) .101209 - 1053)
            var dotNorm = str.Replace(", ", ".");
            var result = string.Empty;
            foreach (var ch in dotNorm)
            {
                if (Char.IsDigit(ch) || ch == '.')
                {
                    result += ch;
                }
                else
                {
                    break;
                }
            }
            return result;
        }

    }
}
