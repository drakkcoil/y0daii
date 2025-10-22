using System;
using System.Runtime.InteropServices;
using System.Windows;

namespace Y0daiiIRC
{
    public partial class App : Application
    {
        [DllImport("kernel32.dll")]
        static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        static extern bool FreeConsole();

        private bool _consoleAllocated = false;

        protected override void OnStartup(StartupEventArgs e)
        {
            // Check if -debug argument was passed
            bool debugMode = e.Args.Contains("-debug", StringComparer.OrdinalIgnoreCase);
            
            if (debugMode)
            {
                // Allocate a console window for debug output
                _consoleAllocated = AllocConsole();
                if (_consoleAllocated)
                {
                    Console.WriteLine("y0daii IRC Client - Debug Console");
                    Console.WriteLine("=================================");
                    Console.WriteLine("Debug mode enabled. Console will show detailed logging.");
                    Console.WriteLine();
                }
            }
            
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Free the console when the app exits (only if it was allocated)
            if (_consoleAllocated)
            {
                FreeConsole();
            }
            base.OnExit(e);
        }
    }
}
