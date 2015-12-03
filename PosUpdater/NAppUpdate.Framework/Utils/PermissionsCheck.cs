using System.IO;
using System.Security.Principal;
using System.Security.AccessControl;
using NAppUpdate.Framework.Common;

namespace NAppUpdate.Framework.Utils
{
    public static class PermissionsCheck
    {
        private static readonly IdentityReferenceCollection groups = WindowsIdentity.GetCurrent().Groups;
        private static readonly string sidCurrentUser = WindowsIdentity.GetCurrent().User.Value;

        public static bool IsDirectory(string path)
        {
			if (!Directory.Exists(path)) return false;
            FileAttributes attr = File.GetAttributes(path);
            return ((attr & FileAttributes.Directory) == FileAttributes.Directory);
        }

        public static bool HaveWritePermissionsForFolder(string path) {
            var folder = IsDirectory(path) ? path : Path.GetDirectoryName(path);
            return HaveWritePermissionsForFileOrFolder(folder);
        }

        public static bool HaveWritePermissionsForFileOrFolder(string path)
        {
            var rules = Directory.GetAccessControl(path).GetAccessRules(true, true, typeof(SecurityIdentifier));

            bool allowwrite = false, denywrite = false;
            foreach (FileSystemAccessRule rule in rules)
            {
                if (rule.AccessControlType == AccessControlType.Deny &&
                    (rule.FileSystemRights & FileSystemRights.WriteData) == FileSystemRights.WriteData &&
                    (groups.Contains(rule.IdentityReference) || rule.IdentityReference.Value == sidCurrentUser))
                {
                    denywrite = true;
                    UpdateManager.Instance.Logger.Log(Logger.SeverityLevel.Error, @"File = {0}\r\n
Don't have permissions: denywrite\r\n
rule.AccessControlType =  {1}|  rule.FileSystemRights = {2}| rule.IdentityReference = {3}| rule.IdentityReference.Value = {4}",
                              path, rule.AccessControlType, rule.FileSystemRights, rule.IdentityReference, rule.IdentityReference.Value);
                }
                if (rule.AccessControlType == AccessControlType.Allow &&
                    (rule.FileSystemRights & FileSystemRights.WriteData) == FileSystemRights.WriteData &&
                    (groups.Contains(rule.IdentityReference) || rule.IdentityReference.Value == sidCurrentUser))
                {
                    allowwrite = true;
                }
            }

            // If we have both allow and deny permissions, the deny takes precedent.
            if (allowwrite && !denywrite)
                return true;

            UpdateManager.Instance.Logger.Log(Logger.SeverityLevel.Error, "File = {0}\r\nDon't have permissions: allowwrite = {1} ", path, allowwrite);
            return false;
        }
    }
}
