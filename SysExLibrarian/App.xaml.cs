using SysExLibrarian.Properties;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Diagnostics;
using Serilog;

namespace SysExLibrarian
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            
            base.OnStartup(e);

            // configure logging
            Log.Logger = new LoggerConfiguration()
              .MinimumLevel.Debug()
              .WriteTo.File("debug.log")
              .CreateLogger();

            Log.Information("Application started");

            // set initial preferences
            if (string.IsNullOrEmpty(Settings.Default.SysExLibrarianFolder))
            {
                var sysExLibrarianFolder = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\\SysEx Librarian";
                System.IO.Directory.CreateDirectory(sysExLibrarianFolder);
                Settings.Default.SysExLibrarianFolder = sysExLibrarianFolder;
                Settings.Default.Save();

				Log.Information("Created library {Folder}", sysExLibrarianFolder);
			}
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);

            Log.Information("Application shutdown");
        }
    }
}
