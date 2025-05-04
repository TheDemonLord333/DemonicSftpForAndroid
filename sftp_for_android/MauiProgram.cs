using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;

namespace sftp_for_android
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            // Register pages for dependency injection
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<FileBrowserPage>();
            builder.Services.AddTransient<FileDetailsPage>();

            // Register services
            builder.Services.AddSingleton<SftpService>();

            // Diese zwei Methoden entfernen oder auskommentieren
            // builder.ConfigureGeneratedBindings();
            // builder.EnableTracing(option => { ... });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}