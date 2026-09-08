using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SmoPmo.Shared.Integration;
using SmoPmo.Shared.Messaging;

namespace SmoPmo.Worker;

/// <summary>
/// Hosts background processing in the SAME process as /Api during MVP (architecture-mvp.md
/// §8). Polls the debounce queue (<see cref="RollupSignalQueue"/>) and, for whatever has gone
/// quiet for at least the configured window, recomputes and stores health up the graph
/// (project → initiative → objective → strategy) via <see cref="IRollupHealthProcessor"/> —
/// the actual graph walk lives in the Roll-up module (X2), reached only through the mediator,
/// since /Worker project-references nothing but /Shared.
/// </summary>
public sealed class RollupWorker : BackgroundService
{
    private readonly IRollupHealthProcessor _processor;
    private readonly RollupWorkerOptions _options;

    public RollupWorker(IRollupHealthProcessor processor, IOptions<RollupWorkerOptions> options)
    {
        _processor = processor;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _processor.ProcessDueAsync(_options.DebounceWindow, stoppingToken);

            try
            {
                await Task.Delay(_options.PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown path — the host is stopping, not an error.
            }
        }
    }
}

public static class WorkerHostingExtensions
{
    public static IServiceCollection AddBackgroundWorker(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        services.AddOptions<RollupWorkerOptions>();
        if (configuration is not null)
        {
            services.Configure<RollupWorkerOptions>(configuration.GetSection("RollupWorker"));
        }

        services.AddSingleton<RollupSignalQueue>();
        services.AddScoped<IDomainEventHandler<PmoDeliveryHealthInputsChanged>, RollupSignalEventHandler>();
        services.AddSingleton<IRollupHealthProcessor, RollupHealthProcessor>();

        services.AddHostedService<RollupWorker>();
        return services;
    }
}
