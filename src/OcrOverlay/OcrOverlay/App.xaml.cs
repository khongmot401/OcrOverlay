using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using OcrOverlay.Core;

namespace OcrOverlay;

public partial class App : System.Windows.Application
{
    public static AppSettings Settings { get; private set; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        Settings = config.Get<AppSettings>() ?? new AppSettings();
        base.OnStartup(e);
    }
}
