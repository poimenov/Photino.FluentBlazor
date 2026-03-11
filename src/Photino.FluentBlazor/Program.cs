using System.Drawing;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.FluentUI.AspNetCore.Components;
using Photino.Blazor;
using Photino.FluentBlazor.Components;
using Photino.FluentBlazor.Services;
using Photino.FluentBlazor.Services.Interfaces;
using log4net.Config;

namespace Photino.FluentBlazor;

public class Program
{
    private const string DATA_DIRECTORY = "DATA_DIRECTORY";
    private const int HTTP_CLIENT_TIMEOUT_SECONDS = 300; // 5 minutes    
    [STAThread]
    public static void Main(string[] args)
    {
        var builder = PhotinoBlazorAppBuilder.CreateDefault(args);

        // Add services to the container.
        builder.Services.AddFluentUIComponents();
        builder.Services.AddLogging(logging =>
        {
            logging.ClearProviders().AddLog4Net();
        });
        builder.RootComponents.Add<App>("app");

        //add json configuration
        var configuration = new ConfigurationBuilder().AddJsonFile(AppSettings.JSON_FILE_NAME, optional: true, reloadOnChange: false).Build();
        builder.Services.AddSingleton<IConfiguration>(configuration);
        builder.Services.Configure<AppSettings>(configuration);
        builder.Services.AddLocalization(options =>
        {
            options.ResourcesPath = "Resources";
        });

        // Configure HttpClient with timeout
        builder.Services.AddSingleton(sp =>
        {
            var handler = new HttpClientHandler();
            var client = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = TimeSpan.FromSeconds(HTTP_CLIENT_TIMEOUT_SECONDS)
            };
            return client;
        });
        builder.Services.AddTransient<IPlatformService, PlatformService>();
        builder.Services.AddTransient<IProcessService, ProcessService>();
        builder.Services.AddTransient<ILinkOpeningService, LinkOpeningService>();

        var cilture = AppCultures.Cultures.FirstOrDefault(x => x.ToString() == configuration.GetSection("DefaultLanguage").Value);
        CultureInfo.DefaultThreadCurrentCulture = (cilture != null) ? cilture : AppCultures.DefaultCulture;
        CultureInfo.DefaultThreadCurrentUICulture = (cilture != null) ? cilture : AppCultures.DefaultCulture;

        var app = builder.Build();
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Application starting...");

        // Get application settings
        var settings = app.Services.GetRequiredService<IOptions<AppSettings>>().Value;

        if (settings == null)
        {
            logger.LogCritical("AppSettings configuration is missing or invalid");
            throw new InvalidOperationException("AppSettings configuration is missing or invalid");
        }

        if (string.IsNullOrWhiteSpace(settings.AppDataPath))
        {
            logger.LogCritical("AppSettings.AppDataPath is not configured");
            throw new InvalidOperationException("AppSettings.AppDataPath must be configured");
        }

        // Ensure app data directory exists
        try
        {
            Directory.CreateDirectory(settings.AppDataPath);
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Failed to create app data directory: {AppDataPath}", settings.AppDataPath);
            throw;
        }

        // Set environment variable if not already set
        var existingDataDirectory = Environment.GetEnvironmentVariable(DATA_DIRECTORY);
        if (string.IsNullOrEmpty(existingDataDirectory))
        {
            Environment.SetEnvironmentVariable(DATA_DIRECTORY, settings.AppDataPath);
        }

        // Set default culture
        var culture = AppCultures.Cultures.FirstOrDefault(x => x.ToString() == settings.DefaultLanguage)
            ?? AppCultures.DefaultCulture;

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        // Configure AppDomain and logging
        AppDomain.CurrentDomain.SetData("DataDirectory", settings.AppDataPath);
        var log4netConfigPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log4net.config");
        if (File.Exists(log4netConfigPath))
        {
            XmlConfigurator.Configure(new FileInfo(log4netConfigPath));
        }
        else
        {
            logger.LogWarning("log4net.config file not found at: {Path}", log4netConfigPath);
        }
        // customize window
        app.MainWindow
            .SetSize(new Size(800, 500))
            .SetIconFile("wwwroot/favicon.ico")
            .SetTitle(AppSettings.APPLICATION_NAME);

        AppDomain.CurrentDomain.UnhandledException += (sender, error) =>
            {
                var ex = (Exception)error.ExceptionObject;
                app.Services.GetRequiredService<ILogger<Program>>().LogError(ex, ex.Message);
                app.MainWindow.ShowMessage("Fatal exception", error.ExceptionObject.ToString());
            };


        app.Run();
    }
}