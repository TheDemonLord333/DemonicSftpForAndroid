using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System;
using System.Net;
using System.Threading.Tasks;

namespace sftp_for_android
{
    public partial class LoginPage : ContentPage
    {
        private readonly SftpService _sftpService;
        private readonly ILogger<LoginPage>? _logger;
        private bool _isBusy = false;

        public LoginPage(SftpService sftpService, ILogger<LoginPage>? logger = null)
        {
            InitializeComponent();
            _sftpService = sftpService;
            _logger = logger;

            // Load saved password if available
            LoadSavedCredentialsAsync();
        }

        private async void LoadSavedCredentialsAsync()
        {
            try
            {
                string? savedPassword = await SecureStorage.Default.GetAsync("sftp_password");

                if (!string.IsNullOrEmpty(savedPassword))
                {
                    PasswordEntry.Text = savedPassword;
                }
            }
            catch (Exception ex)
            {
                // Secure storage might not be supported on the device
                _logger?.LogError(ex, "Error loading credentials: {Message}", ex.Message);
            }
        }

        private async void OnConnectButtonClicked(object sender, EventArgs e)
        {
            if (_isBusy)
                return;

            _isBusy = true;

            try
            {
                // Validate password is entered
                if (string.IsNullOrWhiteSpace(PasswordEntry.Text))
                {
                    await DisplayAlert("Authentication Required",
                        "Please enter your password to connect",
                        "OK");
                    return;
                }

                // Show loading indicator
                ConnectButton.Text = "CONNECTING...";
                ConnectButton.IsEnabled = false;

                // Save password securely
                try
                {
                    await SecureStorage.Default.SetAsync("sftp_password", PasswordEntry.Text);
                }
                catch (Exception ex)
                {
                    // Ignore if secure storage is not available
                    _logger?.LogWarning(ex, "Could not save password: {Message}", ex.Message);
                }

                // Create config with entered credentials
                var config = new SftpConfig
                {
                    Host = HostEntry.Text,
                    Username = UsernameEntry.Text,
                    Password = "ZGen5ZUUQb4bG1rP5JPjCw", // Hardcoded password
                    RemoteDirectory = PathEntry.Text
                };

                // Set configuration in the service
                _sftpService.SetConfig(config);

                try
                {
                    // Test connection to verify credentials
                    bool isConnected = await _sftpService.TestConnectionAsync();

                    if (isConnected)
                    {
                        // Verzeichnis überprüfen
                        var (dirExists, errorMessage) = await _sftpService.CheckDirectoryExistsAsync();

                        if (!dirExists)
                        {
                            await DisplayAlert("Verzeichnisfehler", errorMessage, "OK");
                            return; // Navigation abbrechen
                        }

                        // Config serialisieren und übergeben
                        var configJson = System.Text.Json.JsonSerializer.Serialize(config); // Hier 'config' statt '_config'

                        // In die Navigation übergeben
                        await Shell.Current.GoToAsync("//FileBrowserPage", new Dictionary<string, object>
                        {
                            { "SftpConfig", configJson }
                        });

                        // ACHTUNG: Der folgende Code wird nie erreicht, weil du bereits navigiert hast
                        // Dieser Code kann entfernt werden, da er redundant ist:
                        /*
                        // Navigation using query properties
                        var navigationParameter = new Dictionary<string, object>
                        {
                            { "Host", config.Host },
                            { "Username", config.Username },
                            { "RemotePath", config.RemoteDirectory }
                        };

                        // Navigate to file browser on success
                        await Shell.Current.GoToAsync("//FileBrowserPage", navigationParameter);
                        */
                    }
                    else
                    {
                        await DisplayAlert("Connection Failed",
                            "Could not connect to the server. Please check your credentials.",
                            "OK");
                    }
                }
                catch (Exception ex)
                {
                    // Connection failed
                    _logger?.LogError(ex, "Connection failed: {Message}", ex.Message);

                    await DisplayAlert("Connection Failed",
                        $"Could not connect to the server: {ex.Message}",
                        "OK");
                }
            }
            finally
            {
                _isBusy = false;
                ConnectButton.Text = "CONNECT";
                ConnectButton.IsEnabled = true;
            }
        }
    }
}