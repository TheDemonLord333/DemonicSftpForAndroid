using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using Microsoft.Extensions.Logging;

namespace sftp_for_android
{
    /// <summary>
    /// Configuration class for SFTP connection
    /// </summary>
    public class SftpConfig
    {
        public string Host { get; set; } = "45.133.9.201";
        public int Port { get; set; } = 22;
        public string Username { get; set; } = "root";
        public string Password { get; set; } = "ZGen5ZUUQb4bG1rP5JPjCw";
        public string RemoteDirectory { get; set; } = "/home/nobrainclient/thedemonlord333";
    }

    /// <summary>
    /// Represents a file or directory item in SFTP
    /// </summary>
    public class SftpFileItem
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public string Icon => IsDirectory ? "folder_icon.png" : GetFileIcon(Name);  

        private static string GetFileIcon(string fileName)
        {
            string extension = Path.GetExtension(fileName).ToLowerInvariant();

            return extension switch
            {
                ".pdf" => "pdf_icon.png",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => "image_icon.png",
                ".mp3" or ".wav" or ".ogg" or ".flac" => "audio_icon.png",
                ".mp4" or ".avi" or ".mkv" or ".mov" => "video_icon.png",
                ".doc" or ".docx" => "word_icon.png",
                ".xls" or ".xlsx" => "excel_icon.png",
                ".ppt" or ".pptx" => "powerpoint_icon.png",
                ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "archive_icon.png",
                ".txt" => "text_icon.png",
                _ => "file_icon.png"
            };
        }
    }

    /// <summary>
    /// Service for SFTP operations
    /// </summary>
    public class SftpService
    {
        private SftpConfig _config;
        private readonly ILogger<SftpService>? _logger;

        public SftpService(ILogger<SftpService>? logger = null)
        {
            _logger = logger;
            _config = new SftpConfig();
        }

        public void SetConfig(SftpConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        private SftpClient GetClient()
        {
            if (string.IsNullOrEmpty(_config.Host))
                throw new InvalidOperationException("SFTP host is not configured");

            if (string.IsNullOrEmpty(_config.Username))
                throw new InvalidOperationException("SFTP username is not configured");

            return new SftpClient(_config.Host, _config.Port, _config.Username, _config.Password);
        }

        /// <summary>
        /// Lists files and directories in the specified path
        /// </summary>
        public async Task<List<SftpFileItem>> ListFilesAsync(string? path = null, CancellationToken cancellationToken = default)
        {
            var files = new List<SftpFileItem>();

            return await Task.Run(() =>
            {
                try
                {
                    using var client = GetClient();
                    client.Connect();

                    // Check if connection is successful
                    if (!client.IsConnected)
                    {
                        _logger?.LogError("Failed to connect to SFTP server");
                        throw new Exception("Could not connect to SFTP server");
                    }

                    string remotePath = string.IsNullOrEmpty(path) ?
                        _config.RemoteDirectory : path;

                    foreach (var file in client.ListDirectory(remotePath))
                    {
                        // Skip . and .. directory entries
                        if (file.Name == "." || file.Name == "..")
                            continue;

                        files.Add(new SftpFileItem
                        {
                            Name = file.Name,
                            FullPath = file.FullName,
                            IsDirectory = file.IsDirectory,
                            Size = file.Length,
                            LastModified = file.LastWriteTime
                        });

                        // Check for cancellation
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    client.Disconnect();
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogInformation("SFTP operation was cancelled");
                    throw;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "SFTP Error when listing files");
                    throw;
                }

                return files;
            }, cancellationToken);
        }

        /// <summary>
        /// Downloads a file from the SFTP server
        /// </summary>
        public async Task<bool> DownloadFileAsync(string remotePath, string localPath,
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var client = GetClient();
                    client.Connect();

                    // Ensure directory exists
                    string? directory = Path.GetDirectoryName(localPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // Get file size for progress reporting
                    long totalSize = 0;
                    if (progress != null)
                    {
                        var fileInfo = client.Get(remotePath);
                        totalSize = fileInfo.Length;
                    }

                    using var fileStream = File.Create(localPath);

                    if (progress != null && totalSize > 0)
                    {
                        // Download with progress
                        var buffer = new byte[81920]; // 80KB buffer
                        long totalDownloaded = 0;

                        using var sftpStream = client.OpenRead(remotePath);
                        int bytesRead;

                        while ((bytesRead = sftpStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            fileStream.Write(buffer, 0, bytesRead);
                            totalDownloaded += bytesRead;

                            // Report progress
                            double progressValue = (double)totalDownloaded / totalSize;
                            progress.Report(progressValue);

                            // Check for cancellation
                            cancellationToken.ThrowIfCancellationRequested();
                        }
                    }
                    else
                    {
                        // Simple download without progress
                        client.DownloadFile(remotePath, fileStream);
                    }

                    client.Disconnect();

                    return true;
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogInformation("Download operation was cancelled");
                    throw;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Download Error: {Message}", ex.Message);
                    return false;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Uploads a file to the SFTP server
        /// </summary>
        public async Task<bool> UploadFileAsync(string localPath, string remotePath,
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var client = GetClient();
                    client.Connect();

                    // Upload with progress reporting if requested
                    if (progress != null)
                    {
                        var fileInfo = new FileInfo(localPath);
                        long totalSize = fileInfo.Length;

                        if (totalSize > 0)
                        {
                            using var fileStream = File.OpenRead(localPath);
                            using var sftpStream = client.Create(remotePath);

                            var buffer = new byte[81920]; // 80KB buffer
                            long totalUploaded = 0;
                            int bytesRead;

                            while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                sftpStream.Write(buffer, 0, bytesRead);
                                totalUploaded += bytesRead;

                                // Report progress
                                double progressValue = (double)totalUploaded / totalSize;
                                progress.Report(progressValue);

                                // Check for cancellation
                                cancellationToken.ThrowIfCancellationRequested();
                            }
                        }
                    }
                    else
                    {
                        // Simple upload without progress
                        using var fileStream = File.OpenRead(localPath);
                        client.UploadFile(fileStream, remotePath);
                    }

                    client.Disconnect();

                    return true;
                }
                catch (OperationCanceledException)
                {
                    _logger?.LogInformation("Upload operation was cancelled");
                    throw;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Upload Error: {Message}", ex.Message);
                    return false;
                }
            }, cancellationToken);
        }

        /// <summary>
        /// Deletes a file from the SFTP server
        /// </summary>
        public async Task<bool> DeleteFileAsync(string remotePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var client = GetClient();
                    client.Connect();
                    client.DeleteFile(remotePath);
                    client.Disconnect();

                    return true;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Delete File Error: {Message}", ex.Message);
                    return false;
                }
            });
        }

        /// <summary>
        /// Deletes a directory and all its contents from the SFTP server
        /// </summary>
        public async Task<bool> DeleteDirectoryAsync(string remotePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var client = GetClient();
                    client.Connect();

                    // List all files and directories recursively
                    var files = client.ListDirectory(remotePath);

                    // Delete all files and subdirectories
                    foreach (var file in files)
                    {
                        if (file.Name != "." && file.Name != "..")
                        {
                            if (file.IsDirectory)
                            {
                                // Recursively delete subdirectories
                                DeleteDirectoryRecursive(client, file.FullName);
                            }
                            else
                            {
                                // Delete file
                                client.DeleteFile(file.FullName);
                            }
                        }
                    }

                    // Delete empty directory
                    client.DeleteDirectory(remotePath);

                    client.Disconnect();

                    return true;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Delete Directory Error: {Message}", ex.Message);
                    return false;
                }
            });
        }

        private void DeleteDirectoryRecursive(SftpClient client, string path)
        {
            // List all files and directories
            var files = client.ListDirectory(path);

            foreach (var file in files)
            {
                if (file.Name != "." && file.Name != "..")
                {
                    if (file.IsDirectory)
                    {
                        // Recursively delete subdirectories
                        DeleteDirectoryRecursive(client, file.FullName);
                    }
                    else
                    {
                        // Delete file
                        client.DeleteFile(file.FullName);
                    }
                }
            }

            // Delete empty directory
            client.DeleteDirectory(path);
        }

        /// <summary>
        /// Creates a directory on the SFTP server
        /// </summary>
        public async Task<bool> CreateDirectoryAsync(string remotePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var client = GetClient();
                    client.Connect();
                    client.CreateDirectory(remotePath);
                    client.Disconnect();

                    return true;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Create Directory Error: {Message}", ex.Message);
                    return false;
                }
            });
        }

        /// <summary>
        /// Renames a file or directory on the SFTP server
        /// </summary>
        public async Task<bool> RenameItemAsync(string oldPath, string newPath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var client = GetClient();
                    client.Connect();
                    client.RenameFile(oldPath, newPath);
                    client.Disconnect();

                    return true;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Rename Error: {Message}", ex.Message);
                    return false;
                }
            });
        }

        /// <summary>
        /// Tests the connection to the SFTP server
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var client = GetClient();
                    client.Connect();
                    bool isConnected = client.IsConnected;
                    client.Disconnect();

                    return isConnected;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Connection Test Error: {Message}", ex.Message);
                    return false;
                }
            });
        }

        public async Task<(bool Exists, string ErrorMessage)> CheckDirectoryExistsAsync(string remotePath = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using var client = GetClient();
                    client.Connect();

                    if (!client.IsConnected)
                    {
                        return (false, "Verbindung zum Server konnte nicht hergestellt werden");
                    }

                    string pathToCheck = string.IsNullOrEmpty(remotePath) ? _config.RemoteDirectory : remotePath;

                    // Versuche, das Verzeichnis zu lesen
                    var entries = client.ListDirectory(pathToCheck);

                    // Wenn wir bis hierhin gekommen sind, existiert das Verzeichnis und ist zugänglich
                    bool exists = entries != null;

                    client.Disconnect();

                    return (exists, exists ? string.Empty : "Verzeichnis existiert nicht oder ist nicht zugänglich");
                }
                catch (Renci.SshNet.Common.SftpPathNotFoundException)
                {
                    return (false, "Das angegebene Verzeichnis existiert nicht");
                }
                catch (Renci.SshNet.Common.SshPermissionDeniedException)
                {
                    return (false, "Keine Berechtigung für den Zugriff auf das Verzeichnis");
                }
                catch (Exception ex)
                {
                    return (false, $"Fehler beim Überprüfen des Verzeichnisses: {ex.Message}");
                }
            });
        }
    }
}
