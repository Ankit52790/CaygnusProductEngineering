using Caygnus.ResumableConversation.Api.Data;
using Caygnus.ResumableConversation.Api.Enums;
using Microsoft.EntityFrameworkCore;

namespace Caygnus.ResumableConversation.Api.Services;

public class RunRecoveryService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public RunRecoveryService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<AppDbContext>();

        var runProcessor = scope.ServiceProvider
            .GetRequiredService<RunProcessor>();

        var interruptedRuns = await dbContext.Runs
            .Where(x => x.Status == RunStatus.Running)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var runId in interruptedRuns)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                await runProcessor.ProcessAsync(runId);
            }
            catch (Exception)
            {
                // Keep recovery of one run from preventing other
                // interrupted runs from being recovered.
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}