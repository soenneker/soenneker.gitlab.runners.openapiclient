using Microsoft.Extensions.DependencyInjection;
using Soenneker.Kiota.Util.Registrars;
using Soenneker.Managers.Runners.Registrars;
using Soenneker.GitLab.Runners.OpenApiClient.Utils;
using Soenneker.GitLab.Runners.OpenApiClient.Utils.Abstract;
using Soenneker.OpenApi.Fixer.Registrars;
using Soenneker.Utils.File.Download.Registrars;
using Soenneker.Utils.Yaml.Registrars;

namespace Soenneker.GitLab.Runners.OpenApiClient;

/// <summary>
/// Console type startup
/// </summary>
public static class Startup
{
    // This method gets called by the runtime. Use this method to add services to the container.
    public static void ConfigureServices(IServiceCollection services)
    {
        services.SetupIoC();
    }

    public static IServiceCollection SetupIoC(this IServiceCollection services)
    {
        services.AddHostedService<ConsoleHostedService>()
                .AddScoped<IFileOperationsUtil, FileOperationsUtil>()
                .AddRunnersManagerAsScoped()
                .AddFileDownloadUtilAsScoped()
                .AddYamlUtilAsScoped()
                .AddOpenApiFixerAsScoped()
                .AddKiotaUtilAsScoped();

        return services;
    }
}