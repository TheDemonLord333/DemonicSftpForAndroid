using Microsoft.Maui.Controls;

namespace sftp_for_android
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Register routes for navigation
            Routing.RegisterRoute(nameof(FileBrowserPage), typeof(FileBrowserPage));
            Routing.RegisterRoute(nameof(FileDetailsPage), typeof(FileDetailsPage));
        }
    }
}