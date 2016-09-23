namespace SysExLibrarian
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Windows;
    using System.Windows.Controls;
    using LiteDB;
    using MaterialDesignThemes.Wpf;
    using Microsoft.Win32;
    using Sanford.Multimedia.Midi;
    using Serilog;
    using SysExLibrarian.Models;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private SysExFile rowBeingEdited = null;
        private List<InputDevice> recordingInputDevices = null;

        public MainWindow()
        {
            InitializeComponent();

            Log.Information("Found {deviceCount} MIDI output device(s)", OutputDevice.DeviceCount);

            // Load midi output dropdown with devices
            for (var i = 0; i < OutputDevice.DeviceCount; i++)
            {
                MidiOutputComboBox.Items.Add(OutputDevice.GetDeviceCapabilities(i).name);
            }

            Log.Debug("Opening database {DataBaseFileName}", "MyData.db");

            using (var db = new LiteDatabase(@"MyData.db"))
            {
                // Get customer collection
                var files = db.GetCollection<SysExFile>("SysExFiles");
                files.EnsureIndex(x => x.FileName, true);

                // Use Linq to query documents
                var results = files.FindAll();

                var sysExFiles = results as IList<SysExFile> ?? results.ToList();
                Log.Debug("Found {RowCount} sysex files in database", sysExFiles.Count());

                LibraryCollection = new ObservableCollection<SysExFile>(sysExFiles);
                SysExFilesDataGrid.ItemsSource = LibraryCollection;
            }
        }

        private ObservableCollection<SysExFile> LibraryCollection { get; }

        private void AddFileButton_Click(object sender, RoutedEventArgs e)
        {
            Log.Debug("Event => AddFileButton_Click");

            var openFileDialog = new OpenFileDialog()
            {
                Filter = "SysEx files (*.syx)|*.syx",
                Multiselect = true
            };

            if (openFileDialog.ShowDialog() != true)
            {
                return;
            }

            Log.Debug("{SelectedFileCount} files selected" , openFileDialog.FileNames.Length);

            foreach (var filename in openFileDialog.FileNames)
            {
                using (var db = new LiteDatabase(@"MyData.db"))
                {
                    // Get files collection
                    var files = db.GetCollection<SysExFile>("SysExFiles");

                    if(files.Exists(f => f.FileName == Path.GetFileName(filename) && f.DiskLocation == filename))
                    {
                        Log.Warning("File {FileName} already exists in library.", filename);
                        MessageBox.Show($"The file \"{Path.GetFileName(filename)}\" already exists in your library and will not be added.", "File Exists", MessageBoxButton.OK, MessageBoxImage.Information);
                        continue;
                    }

                    var fi = new FileInfo(filename);
                    var sysExFile = new SysExFile()
                    {
                        Id = Guid.NewGuid().ToString(),
                        FileName = Path.GetFileName(filename),
                        DiskLocation = Path.GetFullPath(filename),
                        Manufacturer = Helpers.MidiManufacturerHelper.GetMidiManufacturerFromSysExFile(filename),
                        Size = fi.Length,
                        LastModifiedDateTime = fi.LastWriteTime
                    };

                    files.Insert(sysExFile);

                    Log.Debug("Inserted file {@SysExFile}", sysExFile);

                    this.LibraryCollection.Add(sysExFile);
                }
            }
        }

        private void DeleteFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Log.Debug("Event =>  DeleteFileMenuItem_Click");

            var selectedItem = SysExFilesDataGrid.SelectedItem as SysExFile;
            if (selectedItem == null)
            {
                return;
            }

            Log.Debug("Selected {@SysExFile}");

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
            Log.Debug("Event => MenuItem_Click");

            Application.Current.Shutdown();
        }

        private void ShowSysExFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Log.Debug("Event => ShowSysExFileMenuItem_Click");

            var selectedItem = SysExFilesDataGrid.SelectedItem as SysExFile;
            if (selectedItem == null)
            {
                return;
            }

            if (!File.Exists(selectedItem.DiskLocation))
            {
                Log.Warning("Can not show SysEx file {@File} because it has been moved or no longer exists.", selectedItem);
                return;
            }

            // Go to the specified folder and select the file in explorer
            var runExplorer = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = @"/select, " + selectedItem.DiskLocation
            };
            Process.Start(runExplorer);
        }

        private void SysExFilesListView_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            Log.Debug("Event => SysExFilesListView_SelectionChanged");

            DeleteFileMenuItem.IsEnabled = ShowContentsMenuItem.IsEnabled = RenameMenuItem.IsEnabled = ShowSysExFileMenuItem.IsEnabled = SysExFilesDataGrid.SelectedIndex != -1;
        }

        private void RenameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Log.Debug("Event => RenameMenuItem_Click");

            SysExFilesDataGrid.CurrentCell = new DataGridCellInfo(SysExFilesDataGrid.SelectedItem, SysExFilesDataGrid.Columns[1]);
            SysExFilesDataGrid.BeginEdit();
        }

        private async void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Log.Debug("Event => AboutMenuItem_Click");

            await DialogHost.Show(new About(), "RootDialog");
        }

        private void SysExFilesDataGrid_CellEditEnding(object sender, System.Windows.Controls.DataGridCellEditEndingEventArgs e)
        {
            Log.Debug("Event => SysExFilesDataGrid_CellEditEnding");

            var currentSysExFile = e.Row.Item as SysExFile;

            Log.Debug("Editing {@CurrentSysExFile}", currentSysExFile);

            rowBeingEdited = currentSysExFile;
        }

        private void SysExFilesDataGrid_CurrentCellChanged(object sender, EventArgs e)
        {
            Log.Debug("Event => SysExFilesDataGrid_CurrentCellChanged");

            if (rowBeingEdited == null)
            {
                return;
            }

            SysExFile currentFile = null;

            Log.Debug("Opening database {DataBaseFileName}", "MyData.db");

            using (var db = new LiteDatabase(@"MyData.db"))
            {
                // Get files collection
                var files = db.GetCollection<SysExFile>("SysExFiles");
                currentFile = files.FindOne(f => f.Id == rowBeingEdited.Id);
            }

            if (!File.Exists(rowBeingEdited.DiskLocation))
            {
                Log.Warning("Can not show SysEx file {@SysExFile} because it has been moved or no longer exists.", rowBeingEdited);
                return;
            }

            rowBeingEdited.DiskLocation = rowBeingEdited.DiskLocation.Replace(currentFile.FileName, rowBeingEdited.FileName);

            Log.Debug("Renaming {@CurrentFile} to {@NewFile}", currentFile, rowBeingEdited);

            try
            {
                File.Move(currentFile.DiskLocation, rowBeingEdited.DiskLocation);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred renaming the file {@SysExFile}", rowBeingEdited);
            }
        }

        private async void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (InputDevice.DeviceCount == 0)
                {
                    var messageDialog = new MessageDialog
                    {
                        Message = { Text = "You need at least one MIDI input device connected to record."}
                    };
                    await DialogHost.Show(messageDialog, "RootDialog");
                    return;
                }
            
                recordingInputDevices = new List<InputDevice>();

                for (var i = 0; i < InputDevice.DeviceCount; i++)
                {
                    var inputDevice = new InputDevice(i);
                    inputDevice.SysExMessageReceived += InputDevice_SysExMessageReceived;
                    inputDevice.StartRecording();

                    recordingInputDevices.Add(inputDevice);
                }

                await DialogHost.Show(new ProgressDialog(), "RootDialog", RecordingCanceled_ClosingEventHandler);

                // stop recording on all devices and dispose of the resources
                foreach (var inputDevice in recordingInputDevices)
                {
                    inputDevice.StopRecording();
                    inputDevice.Dispose();
                }

                recordingInputDevices = null;
            }
            catch (InputDeviceException ex)
            {
                Log.Error(ex, "An error occurred when recording");

                var messageDialog = new MessageDialog
                {
                    Message = { Text = ex.Message }
                };
                await DialogHost.Show(messageDialog, "RootDialog");
            }    
        }

        private void RecordingCanceled_ClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            var result = Convert.ToBoolean(eventArgs.Parameter);
            if (eventArgs.Session.Content is ProgressDialog && !result)
            { 
                // cancel the close
                eventArgs.Cancel();

                // update the dialog to notify the user we got a message
                eventArgs.Session.UpdateContent(new SysExMessageReceivedDialog());
            }
        }

        private async void InputDevice_SysExMessageReceived(object sender, SysExMessageEventArgs e)
        {
            DialogHost.CloseDialogCommand.Execute(null, null);

            // get our recorded message
            var message = e.Message;

            // save our recoreded sysex message to disk in a new file and add it to the library
            try
            {
                var sysExLibrarianFolder = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\\SysEx Librarian";
                var filePath = $"{sysExLibrarianFolder}\\NewFile_{DateTime.Now.ToString("yyyy_dd_M_HH_mm_ss")}.syx"; 
                File.WriteAllBytes(filePath, message.GetBytes());

                // add file to library database
                using (var db = new LiteDatabase(@"MyData.db"))
                {
                    // Get files collection
                    var files = db.GetCollection<SysExFile>("SysExFiles");

                    var fi = new FileInfo(filePath);
                    var sysExFile = new SysExFile()
                    {
                        Id = Guid.NewGuid().ToString(),
                        FileName = Path.GetFileName(filePath),
                        DiskLocation = filePath,
                        Manufacturer = Helpers.MidiManufacturerHelper.GetMidiManufacturerFromSysExFile(filePath),
                        Size = fi.Length,
                        LastModifiedDateTime = fi.LastWriteTime
                    };

                    files.Insert(sysExFile);

                    Log.Debug("Inserted file {@SysExFile}", sysExFile);

                    this.LibraryCollection.Add(sysExFile);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred when receiving a sysex message");
                
                var messageDialog = new MessageDialog
                {
                    Message = { Text = ex.Message }
                };
                await DialogHost.Show(messageDialog, "RootDialog");
            }
        }

        private void ShowContentsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Log.Debug("Event => ShowContentsMenuItem_Click");
        }
    }
}
