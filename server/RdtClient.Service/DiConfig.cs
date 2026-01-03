using System.IO.Abstractions;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using RdtClient.Service.BackgroundServices;
using RdtClient.Service.Middleware;
using RdtClient.Service.Services;
using RdtClient.Service.Services.DebridClients;
using RdtClient.Service.Wrappers;

namespace RdtClient.Service;

public static class DiConfig
{
    public const String RD_CLIENT = "RdClient";
    public static readonly String UserAgent = $"rdt-client {Assembly.GetEntryAssembly()?.GetName().Version}";

    public static void RegisterRdtServices(this IServiceCollection services)
    {
        services.AddMemoryCache();

        services.AddSingleton<IAllDebridNetClientFactory, AllDebridNetClientFactory>();
        services.AddScoped<AllDebridDebridClient>();

        services.AddSingleton<IProcessFactory, ProcessFactory>();
        services.AddSingleton<IFileSystem, FileSystem>();

        services.AddScoped<Authentication>();
        services.AddScoped<IDownloads, Downloads>();
        services.AddScoped<Downloads>();
        services.AddScoped<PremiumizeDebridClient>();
        services.AddScoped<QBittorrent>();
        services.AddScoped<Sabnzbd>();
        services.AddScoped<RemoteService>();
        services.AddScoped<RealDebridDebridClient>();
        services.AddScoped<Settings>();
        services.AddScoped<TorBoxDebridClient>();
        services.AddScoped<Torrents>();
        services.AddScoped<TorrentRunner>();
        services.AddScoped<DebridLinkClient>();

        services.AddSingleton<IDownloadableFileFilter, DownloadableFileFilter>();
        services.AddSingleton<ITrackerListGrabber, TrackerListGrabber>();
        services.AddSingleton<IEnricher, Enricher>();

        services.AddSingleton<IAuthorizationHandler, AuthSettingHandler>();
        services.AddScoped<IAuthorizationHandler, SabnzbdHandler>();

        services.AddHostedService<DiskSpaceMonitor>();
        services.AddHostedService<ProviderUpdater>();
        services.AddHostedService<Startup>();
        services.AddHostedService<TaskRunner>();
        services.AddHostedService<UpdateChecker>();
        services.AddHostedService<WatchFolderChecker>();
        services.AddHostedService<WebsocketsUpdater>();
    }

    public static void RegisterHttpClients(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.ConfigureHttpClientDefaults(builder =>
        {
            builder.ConfigureHttpClient(httpClient =>
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
            });
        });

        services.AddHttpClient(RD_CLIENT)
            .AddStandardResilienceHandler(options =>
            {
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
                options.Retry.MaxRetryAttempts = 5;
                options.Retry.BackoffType = DelayBackoffType.Exponential;
                options.Retry.UseJitter = true;
                options.Retry.Delay = TimeSpan.FromSeconds(2);
                options.Retry.DelayGenerator = args =>
                {
                    // Check if we have a result and if it contains the Retry-After header
                    if (args.Outcome.Result is { } response && response.Headers.RetryAfter is { } retryAfter)
                    {
                        // The header can be either a specific date or a delay in seconds
                        if (retryAfter.Delta.HasValue)
                        {
                            return ValueTask.FromResult<TimeSpan?>(retryAfter.Delta.Value);
                        }

                        if (retryAfter.Date.HasValue)
                        {
                            var delay = retryAfter.Date.Value - DateTimeOffset.UtcNow;

                            return ValueTask.FromResult<TimeSpan?>(delay > TimeSpan.Zero ? delay : TimeSpan.Zero);
                        }
                    }

                    // Return null to let Polly use the default BackoffType/Delay specified above
                    return ValueTask.FromResult<TimeSpan?>(null);
                };
            });
    }
}
