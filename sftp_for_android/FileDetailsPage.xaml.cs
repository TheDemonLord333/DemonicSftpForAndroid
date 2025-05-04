using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.PlatformConfiguration;
using Microsoft.Maui.Storage;
using System;
using System.IO;
using System.Threading.Tasks;

namespace sftp_for_android
{
    [QueryProperty(nameof(FileItemJson), "FileItem")]
    [QueryProperty(nameof(SftpConfigJson), "SftpConfig")]
    public partial class FileDetailsPage : ContentPage
    {
        private SftpService _sftpService;
        private SftpFileItem _fileItem;
        private SftpConfig _config;
        private readonly FileSizeConverter _sizeConverter = new FileSizeConverter();

        public string FileItemJson
        {
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    _fileItem = System.Text.Json.JsonSerializer.Deserialize<SftpFileItem>(value);
                    UpdateUI();
                }
            }
        }

        public string SftpConfigJson
        {
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    _config = System.Text.Json.JsonSerializer.Deserialize<SftpConfig>(value);
                    // Erstelle den Service und setze die Konfiguration
                    _sftpService = new SftpService();  // Ohne Parameter
                    _sftpService.SetConfig(_config);   // Setze die Konfiguration
                }
            }
        }

        public FileDetailsPage()
        {
            InitializeComponent();
        }

        private void UpdateUI()
        {
            if (_fileItem != null)
            {
                // Set file details
                FileIconImage.Source = _fileItem.Icon;
                FileNameLabel.Text = _fileItem.Name;
                FileSizeLabel.Text = _sizeConverter.Convert(_fileItem.Size, typeof(string), null, null).ToString();
                LastModifiedLabel.Text = _fileItem.LastModified.ToString("g");
                FilePathLabel.Text = _fileItem.FullPath;

                // Set page title
                Title = _fileItem.Name;
            }
        }

        private async void OnDownloadClicked(object sender, EventArgs e)
        {
            try
            {
                // Show loading indicator
                LoadingIndicator.IsVisible = true;
                DownloadButton.IsEnabled = false;
                DownloadButton.Text = "DOWNLOADING...";

                // Get downloads directory that's accessible to the user
                string localPath;

                if (DeviceInfo.Platform == DevicePlatform.Android)
                {
                    // Use the public downloads folder on Android
                    localPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),
                        _fileItem.Name);
                }
                else
                {
                    // Fallback for other platforms
                    string downloadFolder = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                    localPath = Path.Combine(downloadFolder, "Downloads", _fileItem.Name);
                }

                // Ensure directory exists
                Directory.CreateDirectory(Path.GetDirectoryName(localPath));

                // Download the file
                bool success = await _sftpService.DownloadFileAsync(_fileItem.FullPath, localPath);

                if (success)
                {
                    await DisplayAlert("Download Complete",
                        $"File saved to: {localPath}",
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
                DownloadButton.IsEnabled = true;
                DownloadButton.Text = "DOWNLOAD";
            }
        }

        private async void OnShareClicked(object sender, EventArgs e)
        {
            try
            {
                // Show loading indicator
                LoadingIndicator.IsVisible = true;
                ShareButton.IsEnabled = false;

                // First download the file to a temporary location
                string tempPath = Path.Combine(
                    FileSystem.CacheDirectory,
                    _fileItem.Name);

                bool success = await _sftpService.DownloadFileAsync(_fileItem.FullPath, tempPath);

                if (success)
                {
                    // Share the file
                    await Share.RequestAsync(new ShareFileRequest
                    {
                        Title = _fileItem.Name,
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
                ShareButton.IsEnabled = true;
            }
        }

        private async void OnRenameClicked(object sender, EventArgs e)
        {
            string newName = await DisplayPromptAsync(
                "Rename File",
                "Enter new name:",
                "Rename",
                "Cancel",
                initialValue: _fileItem.Name);

            if (string.IsNullOrWhiteSpace(newName) || newName == _fileItem.Name)
                return;

            try
            {
                // Show loading indicator
                LoadingIndicator.IsVisible = true;
                RenameButton.IsEnabled = false;

                // Create new path
                string directory = Path.GetDirectoryName(_fileItem.FullPath);
                string newPath = Path.Combine(directory, newName).Replace("\\", "/");

                // Rename the file
                bool success = await _sftpService.RenameItemAsync(_fileItem.FullPath, newPath);

                if (success)
                {
                    await DisplayAlert("Success",
                        $"File renamed to {newName}",
                        "OK");

                    // Close this page and return to browser
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await DisplayAlert("Rename Failed",
                        "Could not rename the file",
                        "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error",
                    $"Rename failed: {ex.Message}",
                    "OK");
            }
            finally
            {
                LoadingIndicator.IsVisible = false;
                RenameButton.IsEnabled = true;
            }
        }

        private async void OnDeleteClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert(
                "Confirm Delete",
                $"Are you sure you want to delete {_fileItem.Name}?",
                "Yes", "No");

            if (!confirm)
                return;

            try
            {
                // Show loading indicator
                LoadingIndicator.IsVisible = true;
                DeleteButton.IsEnabled = false;
                DeleteButton.Text = "DELETING...";

                // Delete the file
                bool success = await _sftpService.DeleteFileAsync(_fileItem.FullPath);

                if (success)
                {
                    await DisplayAlert("Success",
                        "File deleted successfully",
                        "OK");

                    // Close this page and return to browser
                    await Shell.Current.GoToAsync("..");
                }
                else
                {
                    await DisplayAlert("Delete Failed",
                        "Could not delete the file",
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
                DeleteButton.IsEnabled = true;
                DeleteButton.Text = "DELETE";
            }
        }
    }
}