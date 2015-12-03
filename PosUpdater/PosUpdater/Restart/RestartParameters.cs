using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Security;
using System.Security.Principal;

namespace PosUpdater.Restart
{
    static internal class RestartParameters
    {
        public static string RscUserDomen { get { return "ecco"; } }

        public static string RscUser
        {
            get { return "rsc"; }
        }

        public static SecureString RscPass()
        {
            const string pass = @"cvD3kl@";
            return pass.ConvertToSecureString();
            //var ss = new SecureString();
            //foreach (var c in pass.ToCharArray()) ss.AppendChar(c);
            //return ss;
        }

        public static SecureString ConvertToSecureString(this string password)
        {
            if (password == null)
                throw new ArgumentNullException("password");

            unsafe
            {
                fixed (char* passwordChars = password)
                {
                    var securePassword = new SecureString(passwordChars, password.Length);
                    securePassword.MakeReadOnly();
                    return securePassword;
                }
            }
        }
    }

    public static class VistaSecurity
    {
        public static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();

            if (null != identity)
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }

            return false;
        }

        public static Process RunProcess(string name, string arguments)
        {
            string path = Path.GetDirectoryName(name);

            if (String.IsNullOrEmpty(path))
            {
                path = Environment.CurrentDirectory;
            }

            ProcessStartInfo info = new ProcessStartInfo
            {
                UseShellExecute = true,
                WorkingDirectory = path,
                FileName = name,
                Arguments = arguments
            };

            if (!IsAdministrator())
            {
                info.Verb = "runas";
            }

            try
            {
                return Process.Start(info);
            }

            catch (Win32Exception ex)
            {
                Trace.WriteLine(ex);
            }

            return null;
        }
    }
}