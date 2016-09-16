using SysExLibrarian.Properties;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

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

            // set initial preferences
            if (string.IsNullOrEmpty(Settings.Default.SysExLibrarianFolder))
            {
                var sysExLibrarianFolder = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\\SysEx Librarian";
                System.IO.Directory.CreateDirectory(sysExLibrarianFolder);
                Settings.Default.SysExLibrarianFolder = sysExLibrarianFolder;
                Settings.Default.Save();
            }
        }
    }
}
