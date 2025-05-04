using Microsoft.Maui.Controls;

namespace sftp_for_android
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            MainPage = new AppShell();
        }
    }
}