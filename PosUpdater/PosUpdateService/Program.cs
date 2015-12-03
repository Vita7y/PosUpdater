using System;
using System.Linq;
using System.ServiceProcess;

namespace PosUpdateService
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        static void Main()
        {
            if (Environment.GetCommandLineArgs().Contains("-?"))
            {
                Console.WriteLine(@"-console      Start server in console mode, without start Service");
                Console.WriteLine(@"-?            Help");
                return;
            }

            var service = new PosUpdateService();

            if (Environment.GetCommandLineArgs().Contains("-console"))
            {
                Console.CancelKeyPress += (x, y) => service.Stop();
                service.Start();
                Console.WriteLine(@"Running service, press a key to stop");
                Console.ReadKey();
                service.Stop();
                Console.WriteLine(@"Service stopped");
            }
            else
            {
                ServiceBase.Run(service);
            }
        }
    }
}
