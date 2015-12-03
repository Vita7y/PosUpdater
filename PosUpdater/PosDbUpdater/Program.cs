namespace PosDbUpdater
{
    internal static class Program
    {

        /// <summary>
        ///     The main entry point for the application.
        /// </summary>
        private static void Main(string[] args)
        {
            DbUpdater.Update(args);
        }

    }
}