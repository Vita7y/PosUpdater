using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace FeedBuilder
{
	public class ArgumentsParser
	{
		public bool HasArgs { get; set; }
		
        public string FileName { get; set; }

        [CommandLine.Option('s', "showgui", Required = true)]
        public bool ShowGui { get; set; }

        [CommandLine.Option('b', "build", Required = true)]
        public bool Build { get; set; }

        [CommandLine.Option('o', "openoutputs", Required = true)]
        public bool OpenOutputsFolder { get; set; }

        [CommandLine.Option('v', "version", Required = true)]
        public Version Version { get; set; }

        [CommandLine.Option('c', "copyto", Required = true)]
        public string InfoCopyTo { get; set; }

		public static ArgumentsParser ParseArguments(string[] args)
		{
            var options = new ArgumentsParser();

            if (CommandLine.Parser.Default.ParseArguments(args, options))
            {
            }

		    foreach (string thisArg in args)
		    {
		        if (thisArg.ToLower() == Application.ExecutablePath.ToLower() || thisArg.ToLower().Contains(".vshost.exe"))
		            continue;

                string arg = options.CleanArg(thisArg);
                var t = options.ParseArg(thisArg);
		        if (arg == "build")
		        {
                    options.Build = true;
                    options.HasArgs = true;
		        }
                else if (arg == "showgui")
		        {
                    options.ShowGui = true;
                    options.HasArgs = true;
		        }
		        else if (arg == "openoutputs")
		        {
                    options.OpenOutputsFolder = true;
                    options.HasArgs = true;
		        }
		        else if (arg=="config")
		        {
		            // keep the same character casing as we were originally provided
                    var param = options.ParseArg(thisArg);
                    if (options.IsValidFileName(param.ToString()))
		            {
                        options.FileName = thisArg;
                        options.HasArgs = true;
		            }
		        }
                else if (arg == "version")
                {
                    var param = options.ParseArg(thisArg);
                    options.Version = new Version(param.ToString());
                }
                else if (arg == "copyto")
                {
                    var param = options.ParseArg(thisArg);
                    options.InfoCopyTo = param.ToString();
                }
                else
		            Console.WriteLine("Unrecognized arg '{0}'", arg);
		    }
		    return options;
		}

		// this merely checks whether the parent folder exists and if it does, 
		// we say the filename is valid
		private bool IsValidFileName(string filename)
		{
			if (File.Exists(filename)) return true;
			try {
				// the URI test... filter out things that aren't even trying to look like filenames
// ReSharper disable UnusedVariable
				Uri u = new Uri(filename);
// ReSharper restore UnusedVariable
				// see if the arg's parent folder exists
				var d = Directory.GetParent(filename);
				if (d.Exists) return true;
			} catch {}
			return false;
		}

		private string CleanArg(string arg)
		{
			const string pattern1 = "^(.*)([=,:](true|0))";
			arg = arg.ToLower();
			if (arg.StartsWith("-") || arg.StartsWith("/")) arg = arg.Substring(1);
			Regex r = new Regex(pattern1);
			arg = r.Replace(arg, "{$1}");
			return arg;
		}

        private Match ParseArg(string arg)
        {
            const string pattern1 = "^(/|-)(?<name>\\w+)(?:\\:(?<value>.+)$|\\:$|$)";
            arg = arg.ToLower();
            if (arg.StartsWith("-") || arg.StartsWith("/")) arg = arg.Substring(1);
            var r = new Regex(pattern1);
            return r.Match(arg);
        }
    }
}