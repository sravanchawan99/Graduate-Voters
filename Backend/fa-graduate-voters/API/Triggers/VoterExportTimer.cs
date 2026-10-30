using API.Implementation.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace API.Triggers
{
    public sealed class VoterExportTimer
    {
        private readonly ITableService _tableService;
        private readonly ILogger<VoterExportTimer> _logger;

        public VoterExportTimer(ITableService tableService, ILogger<VoterExportTimer> logger)
        {
            _tableService = tableService;
            _logger = logger;
        }

        [Function("QueuePendingVoterExports")]
        public async Task Run([TimerTrigger("%VoterExportSchedule%")] TimerInfo timer, FunctionContext context)
        {
            var cancellationToken = context.CancellationToken;
            _logger.LogInformation("Voter export timer started at {Timestamp}.", DateTimeOffset.UtcNow);
            try
            {
                var queued = await _tableService.QueuePendingVotersAsync(cancellationToken);
                if (queued == 0)
                {
                    _logger.LogInformation("Voter export timer found no pending voters; no Service Bus messages sent.");
                }
                else
                {
                    _logger.LogInformation("Voter export timer queued {VoterCount} voter(s) for CSV export.", queued);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Voter export timer failed.");
                throw;
            }
            finally
            {
                _logger.LogInformation("Voter export timer finished at {Timestamp}.", DateTimeOffset.UtcNow);
            }
        }
    }
}
