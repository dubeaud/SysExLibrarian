﻿using LiteDB;
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
using MaterialDesignThemes.Wpf;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SysExLibrarian
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ObservableCollection<SysExFile> LibraryCollection { get; set; }
        private SysExFile rowBeingEdited = null;
        private List<InputDevice> recordingInputDevices = null;

        public MainWindow()
        {
            InitializeComponent();

            // Load midi output dropdown with devices
            //MidiOutputComboBox.Items.Add("Select a MIDI output");
   //         MidiOutputComboBox.SelectedIndex = 0;
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

        private void DeleteFileMenuItem_Click(object sender, RoutedEventArgs e)
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
            DeleteFileMenuItem.IsEnabled = RenameMenuItem.IsEnabled = ShowSysExFileMenuItem.IsEnabled = SysExFilesDataGrid.SelectedIndex != -1;
        }

        private void RenameMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SysExFilesDataGrid.CurrentCell = new DataGridCellInfo(SysExFilesDataGrid.SelectedItem, SysExFilesDataGrid.Columns[1]);
            SysExFilesDataGrid.BeginEdit();
        }

        private async void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var aboutDialog = new About();
            await DialogHost.Show(aboutDialog, "RootDialog");
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

        private async void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if(InputDevice.DeviceCount == 0)
                {
                    var messageDialog = new MessageDialog();
                    messageDialog.Message.Text = "You need at least one MIDI input device connected to record.";
                    await DialogHost.Show(new ProgressDialog(), "RootDialog");
                    return;
                }
            
                recordingInputDevices = new List<InputDevice>();

                for (var i = 0; i < InputDevice.DeviceCount; i++)
                {
                    InputDevice inputDevice = new InputDevice(i);
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
                MessageBox.Show(ex.Message);
            }    
        }

        private void RecordingCanceled_ClosingEventHandler(object sender, DialogClosingEventArgs eventArgs)
        {
            var result = Convert.ToBoolean(eventArgs.Parameter);
            if (eventArgs.Session.Content is ProgressDialog && !result)
            { 
                // cancel the close
                eventArgs.Cancel();

                // update
                eventArgs.Session.UpdateContent(new SysExMessageReceivedDialog());
            }
        }

        private void InputDevice_SysExMessageReceived(object sender, SysExMessageEventArgs e)
        {
            DialogHost.CloseDialogCommand.Execute(null, null);

            // get our recorded message
            var message = e.Message as SysExMessage;

            // save it to disk in a new file
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

                    FileInfo fi = new FileInfo(filePath);
                    var sysExFile = new SysExFile()
                    {
                        Id = Guid.NewGuid().ToString(),
                        FileName = Path.GetFileName(filePath),
                        DiskLocation = filePath,
                        Manufacturer = Helpers.MidiManufacturerHelper.GetMidiManufacturerFromSysExFile(filePath),
                        Size = fi.Length
                    };

                    files.Insert(sysExFile);

                    this.LibraryCollection.Add(sysExFile);
                }
            }
            catch (Exception ex)
            {
                // todo: log this
                // display material design dialog
                MessageBox.Show(ex.Message);
            }
        }
    }
}
