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

        protected override void OnStartup(StartupEventArgs e)
        {
            // Allocate a console window for debug output
            AllocConsole();
            Console.WriteLine("Y0daii IRC Client - Debug Console");
            Console.WriteLine("=================================");
            
            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Free the console when the app exits
            FreeConsole();
            base.OnExit(e);
        }
    }
}
