using LiteDB;
using System;
using System.Windows;
using SysExLibrarian.Models;
using Sanford.Multimedia.Midi;
using SysExLibrarian.Properties;
using Microsoft.Win32;
using System.IO;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Data;
using System.Windows.Controls;

namespace SysExLibrarian
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ObservableCollection<SysExFile> LibraryCollection { get; set; }
        private SysExFile rowBeingEdited = null;

        public MainWindow()
        {
            InitializeComponent();

            // Load midi output dropdown with devices
            MidiOutputComboBox.Items.Add("Select a MIDI output");
            MidiOutputComboBox.SelectedIndex = 0;
            for(var i = 0; i < OutputDevice.DeviceCount; i++)
            {
                MidiOutputComboBox.Items.Add(OutputDevice.GetDeviceCapabilities(i).name);
            }

            using (var db = new LiteDatabase(@"MyData.db"))
            {
                // Get customer collection
                var files = db.GetCollection<SysExFile>("SysExFiles");
                files.EnsureIndex(x => x.FileName, true);

                // Use Linq to query documents
                var results = files.FindAll();
                this.LibraryCollection = new ObservableCollection<SysExFile>(results);
                SysExFilesDataGrid.ItemsSource = this.LibraryCollection;

            }
        }

        private void AddFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog()
            {
                Filter = "SysEx files (*.syx)|*.syx",
                Multiselect = true
            };
           
            if (openFileDialog.ShowDialog() == true)
            {
                foreach (string filename in openFileDialog.FileNames)
                {
                    using (var db = new LiteDatabase(@"MyData.db"))
                    {
                        // Get files collection
                        var files = db.GetCollection<SysExFile>("SysExFiles");

                        if(files.Exists(f => f.FileName == Path.GetFileName(filename) && f.DiskLocation == filename))
                        {
                            MessageBox.Show($"The file \"{Path.GetFileName(filename)}\" already exists in your library and will not be added.", "File Exists", MessageBoxButton.OK, MessageBoxImage.Information);
                            continue;
                        }

                        FileInfo fi = new FileInfo(filename);
                        var sysExFile = new SysExFile()
                        {
                            Id = Guid.NewGuid().ToString(),
                            FileName = Path.GetFileName(filename),
                            DiskLocation = Path.GetFullPath(filename),
                            Manufacturer = SysExLibrarian.Helpers.MidiManufacturerHelper.GetMidiManufacturerFromSysExFile(filename),
                            Size = fi.Length
                        };

                        files.Insert(sysExFile);

                        this.LibraryCollection.Add(sysExFile);
                    }
                }      
            }   
        }

        private void RemoveFileButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = SysExFilesDataGrid.SelectedItem as SysExFile;
            if (selectedItem == null)
            {
                return;
            }

            LibraryCollection.Remove(selectedItem);

            using (var db = new LiteDatabase(@"MyData.db"))
            {
                // Get files collection
                var files = db.GetCollection<SysExFile>("SysExFiles");
                files.Delete(f => f.Id == selectedItem.Id);
            }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            App.Current.Shutdown();
        }

        private void ShowSysExFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = SysExFilesDataGrid.SelectedItem as SysExFile;
            if (selectedItem == null)
            {
                return;
            }

            if (!File.Exists(selectedItem.DiskLocation))
            {
                Trace.TraceWarning($"Can not show SysEx file {selectedItem.DiskLocation} because it has been moved or no longer exists.");
                return;
            }

            // Go to the specified folder and select the file in explorer
            var runExplorer = new ProcessStartInfo();
            runExplorer.FileName = "explorer.exe";
            runExplorer.Arguments = @"/select, " + selectedItem.DiskLocation;
            Process.Start(runExplorer);
        }

        private void SysExFilesListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            RenameMenuItem.IsEnabled = ShowSysExFileMenuItem.IsEnabled = SysExFilesDataGrid.SelectedIndex != -1;
        }

        private void RenameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SysExFilesDataGrid.CurrentCell = new DataGridCellInfo(SysExFilesDataGrid.SelectedItem, SysExFilesDataGrid.Columns[0]);
            SysExFilesDataGrid.BeginEdit();
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            About about = new SysExLibrarian.About();
            about.ShowDialog();
        }

        private void SysExFilesDataGrid_CellEditEnding(object sender, System.Windows.Controls.DataGridCellEditEndingEventArgs e)
        {
            var currentSysExFile = e.Row.Item as SysExFile;
            rowBeingEdited = currentSysExFile;
        }

        private void SysExFilesDataGrid_CurrentCellChanged(object sender, EventArgs e)
        {
            if (rowBeingEdited != null)
            {
                SysExFile currentFile = null;
                using (var db = new LiteDatabase(@"MyData.db"))
                {
                    // Get files collection
                    var files = db.GetCollection<SysExFile>("SysExFiles");
                    currentFile = files.FindOne(f => f.Id == rowBeingEdited.Id);
                }

                if (!File.Exists(rowBeingEdited.DiskLocation))
                {
                    Trace.TraceWarning($"Can not show SysEx file {rowBeingEdited.DiskLocation} because it has been moved or no longer exists.");
                    return;
                }

                rowBeingEdited.DiskLocation = rowBeingEdited.DiskLocation.Replace(currentFile?.FileName, rowBeingEdited.FileName);

                try
                {
                    File.Move(currentFile.DiskLocation, rowBeingEdited.DiskLocation);
                }
                catch (Exception ex)
                {
                    Trace.TraceError($"An error occurred renaming the file {rowBeingEdited.DiskLocation} Exception: {ex}");
                }
            }
        }
    }
}
