using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;

namespace sftp_for_android
{
    // Query property to receive SftpConfig from navigation
    [QueryProperty(nameof(SftpConfigJson), "SftpConfig")]
    public partial class FileBrowserPage : ContentPage
    {
        private SftpService _sftpService;
        private SftpConfig _config;
        private string _currentPath;
        private ObservableCollection<SftpFileItem> _files = new ObservableCollection<SftpFileItem>();

        // Property to receive SftpConfig via navigation
        // In FileBrowserPage.xaml.cs
        public string SftpConfigJson
        {
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    try
                    {
                        _config = System.Text.Json.JsonSerializer.Deserialize<SftpConfig>(value);

                        // Debugging-Ausgabe
                        Console.WriteLine($"Config deserialisiert: {_config?.Host}, {_config?.Username}");

                        InitializeSftp();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Fehler bei Deserialisierung: {ex.Message}");
                    }
                }
            }
        }

        public FileBrowserPage()
        {
            InitializeComponent();
            FileListView.ItemsSource = _files;
        }

        private void InitializeSftp()
        {
            try
            {
                if (_config != null)
                {
                    Console.WriteLine("InitializeSftp wird ausgeführt");

                    // WICHTIG: Überprüfe, ob _sftpService null ist
                    if (_sftpService == null)
                    {
                        _sftpService = new SftpService();
                        Console.WriteLine("SftpService neu erstellt");
                    }

                    _sftpService.SetConfig(_config);
                    Console.WriteLine("Config gesetzt");

                    // Sicherstellen, dass _currentPath nicht null ist
                    _currentPath = _config.RemoteDirectory ?? "/home/nobrainclient/thedemonlord333";
                    Console.WriteLine($"Aktueller Pfad: {_currentPath}");

                    UpdatePathDisplay();

                    // Verwendung von Task.Run, um LoadFilesAsync asynchron auszuführen
                    Task.Run(async () => {
                        try
                        {
                            await LoadFilesAsync();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Fehler beim Laden der Dateien: {ex.Message}");
                            // Anzeige der Fehlermeldung auf der UI
                            MainThread.BeginInvokeOnMainThread(async () => {
                                await DisplayAlert("Fehler", $"Fehler beim Laden der Dateien: {ex.Message}", "OK");
                            });
                        }
                    });
                }
                else
                {
                    Console.WriteLine("Config ist null");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler in InitializeSftp: {ex.Message}");
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            // If config is already set, reload files
            if (_config != null && _sftpService != null)
            {
                LoadFilesAsync();
            }
        }

        private void UpdatePathDisplay()
        {
            // Display readable path
            string displayPath = _currentPath;
            if (displayPath == _config.RemoteDirectory)
            {
                displayPath = "Home Directory";
            }

            CurrentPathLabel.Text = displayPath;
        }

        private async Task LoadFilesAsync()
        {
            LoadingIndicator.IsVisible = true;
            FileListView.IsVisible = false;
            EmptyStateLayout.IsVisible = false;

            try
            {
                var files = await _sftpService.ListFilesAsync(_currentPath);

                // Add "go up" option if not in root directory
                if (_currentPath != _config.RemoteDirectory)
                {
                    files.Insert(0, new SftpFileItem
                    {
                        Name = "..",
                        FullPath = Path.GetDirectoryName(_currentPath).Replace("\\", "/"),
                        IsDirectory = true,
                        //Icon = "up_folder.svg"
                    });
                }

                // Show empty state if no files
                if (files.Count == 0 || (files.Count == 1 && files[0].Name == ".."))
                {
                    EmptyStateLayout.IsVisible = true;
                }

                // Update collection
                _files.Clear();
                foreach (var file in files)
                {
                    _files.Add(file);
                }

                FileListView.IsVisible = true;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Connection Error",
                    $"Could not load files: {ex.Message}",
                    "OK");
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
            }
        }

        private async void OnFileSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.Count > 0 && e.CurrentSelection[0] is SftpFileItem item)
            {
                // Clear selection
                FileListView.SelectedItem = null;

                if (item.IsDirectory)
                {
                    // Navigate to directory
                    _currentPath = item.FullPath;
                    UpdatePathDisplay();
                    await LoadFilesAsync();
                }
                else
                {
                    // Show file options
                    string action = await DisplayActionSheet(
                        item.Name,
                        "Cancel",
                        null,
                        "Download",
                        "Share",
                        "Delete");

                    switch (action)
                    {
                        case "Download":
                            await DownloadFileAsync(item);
                            break;
                        case "Share":
                            await ShareFileAsync(item);
                            break;
                        case "Delete":
                            await DeleteFileAsync(item);
                            break;
                    }
                }
            }
        }

        private async void OnUploadClicked(object sender, EventArgs e)
        {
            try
            {
                // Pick file
                var fileResult = await FilePicker.PickAsync();
                if (fileResult == null)
                    return;

                // Start upload process
                LoadingIndicator.IsVisible = true;

                string remotePath = Path.Combine(_currentPath, fileResult.FileName).Replace("\\", "/");
                bool success = await _sftpService.UploadFileAsync(fileResult.FullPath, remotePath);

                if (success)
                {
                    // Refresh list
                    await LoadFilesAsync();
                }
                else
                {
                    await DisplayAlert("Upload Failed",
                        "Could not upload the file to the server",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Upload failed: {ex.Message}", "OK");
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
            }
        }

        private async void OnNewFolderClicked(object sender, EventArgs e)
        {
            string folderName = await DisplayPromptAsync(
                "New Folder",
                "Enter folder name:",
                "Create",
                "Cancel");

            if (string.IsNullOrWhiteSpace(folderName))
                return;

            try
            {
                LoadingIndicator.IsVisible = true;

                // Stelle sicher, dass _currentPath und folderName nicht null sind
                if (string.IsNullOrEmpty(_currentPath))
                {
                    _currentPath = "/home/nobrainclient/thedemonlord333";
                }

                // Verwende einfache Pfadverkettung statt Path.Combine für Linux-Pfade
                string newPath = _currentPath;
                if (!newPath.EndsWith("/"))
                    newPath += "/";
                newPath += folderName;

                Console.WriteLine($"Erstelle Ordner in: {newPath}");

                bool success = await _sftpService.CreateDirectoryAsync(newPath);


                if (success)
                {
                    await LoadFilesAsync();
                }
                else
                {
                    await DisplayAlert("Error",
                        "Could not create the folder",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error",
                    $"Could not create folder: {ex.Message}",
                    "OK");
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
            }
        }

        private async void OnRefreshClicked(object sender, EventArgs e)
        {
            await LoadFilesAsync();
        }

        private async Task DownloadFileAsync(SftpFileItem item)
        {
            try
            {
                // Start download
                LoadingIndicator.IsVisible = true;

                // Get download folder
                string downloadFolder = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                string localPath = Path.Combine(downloadFolder, "Downloads", item.Name);

                // Ensure directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(localPath));

                bool success = await _sftpService.DownloadFileAsync(item.FullPath, localPath);

                if (success)
                {
                    await DisplayAlert("Download Complete",
                        $"File saved to {localPath}",
                        "OK");
                }
                else
                {
                    await DisplayAlert("Download Failed",
                        "Could not download the file",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error",
                    $"Download failed: {ex.Message}",
                    "OK");
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
            }
        }

        private async Task ShareFileAsync(SftpFileItem item)
        {
            try
            {
                // First download to temp
                LoadingIndicator.IsVisible = true;

                string tempPath = Path.Combine(
                    FileSystem.CacheDirectory,
                    item.Name);

                bool success = await _sftpService.DownloadFileAsync(item.FullPath, tempPath);

                if (success)
                {
                    // Share the file
                    await Share.RequestAsync(new ShareFileRequest
                    {
                        Title = item.Name,
                        File = new ShareFile(tempPath)
                    });
                }
                else
                {
                    await DisplayAlert("Share Failed",
                        "Could not prepare the file for sharing",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error",
                    $"Share failed: {ex.Message}",
                    "OK");
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
            }
        }

        private async Task DeleteFileAsync(SftpFileItem item)
        {
            bool confirm = await DisplayAlert(
                "Confirm Delete",
                $"Are you sure you want to delete {item.Name}?",
                "Yes", "No");

            if (!confirm)
                return;

            try
            {
                LoadingIndicator.IsVisible = true;

                bool success = false;
                if (item.IsDirectory)
                {
                    success = await _sftpService.DeleteDirectoryAsync(item.FullPath);
                }
                else
                {
                    success = await _sftpService.DeleteFileAsync(item.FullPath);
                }

                if (success)
                {
                    await LoadFilesAsync();
                }
                else
                {
                    await DisplayAlert("Delete Failed",
                        "Could not delete the item",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error",
                    $"Delete failed: {ex.Message}",
                    "OK");
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
            }
        }
    }

    // Converter classes
    public class FileSizeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is long size)
            {
                string[] units = { "B", "KB", "MB", "GB", "TB" };
                int unitIndex = 0;
                double fileSize = size;

                while (fileSize >= 1024 && unitIndex < units.Length - 1)
                {
                    fileSize /= 1024;
                    unitIndex++;
                }

                return $"{fileSize:0.##} {units[unitIndex]}";
            }

            return "0 B";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }

            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }

            return value;
        }
    }
}