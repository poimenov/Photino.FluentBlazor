using System.Drawing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.FluentUI.AspNetCore.Components;
using Photino.Blazor;
using Photino.FluentBlazor.Components;
using log4net;
using System.Reflection;
using Microsoft.Extensions.Options;
using System.Globalization;
using Photino.FluentBlazor.Services;
using Photino.FluentBlazor.Services.Interfaces;

namespace Photino.FluentBlazor;

public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        const string DATA_DIRECTORY = "DATA_DIRECTORY";
        var builder = PhotinoBlazorAppBuilder.CreateDefault(args);

        // Add services to the container.
        builder.Services.AddFluentUIComponents();
        builder.Services.AddLogging(logging =>
        {
            logging.ClearProviders().AddLog4Net();
        });
        builder.RootComponents.Add<App>("app");

        //add json configuration
        var configuration = new ConfigurationBuilder().AddJsonFile(AppSettings.JSON_FILE_NAME, optional: true, reloadOnChange: true).Build();
        builder.Services.AddSingleton<IConfiguration>(configuration);
        builder.Services.Configure<AppSettings>(configuration);
        builder.Services.AddLocalization(options =>
        {
            options.ResourcesPath = "Resources";
        });
        builder.Services.AddSingleton<ILog>(LogManager.GetLogger(MethodBase.GetCurrentMethod()?.DeclaringType ?? typeof(Program)));
        builder.Services.AddScoped(sp => new HttpClient(new HttpClientHandler(), disposeHandler: true));
        builder.Services.AddTransient<IPlatformService, PlatformService>();
        builder.Services.AddTransient<IProcessService, ProcessService>();
        builder.Services.AddTransient<ILinkOpeningService, LinkOpeningService>();

        var cilture = AppCultures.Cultures.FirstOrDefault(x => x.ToString() == configuration.GetSection("DefaultLanguage").Value);
        CultureInfo.DefaultThreadCurrentCulture = (cilture != null) ? cilture : AppCultures.DefaultCulture;
        CultureInfo.DefaultThreadCurrentUICulture = (cilture != null) ? cilture : AppCultures.DefaultCulture;

        var app = builder.Build();
        var dataDirectory = Environment.GetEnvironmentVariable(DATA_DIRECTORY);
        if (dataDirectory == null)
        {
            Environment.SetEnvironmentVariable(DATA_DIRECTORY, app.Services.GetRequiredService<IOptions<AppSettings>>().Value.AppDataPath);
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