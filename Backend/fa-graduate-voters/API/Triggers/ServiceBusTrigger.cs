
using System.Text.Json;
using API.Implementation.Models;
using API.Implementation.Services;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace API.Triggers
{
    public sealed class VoterExportTrigger
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly IBlobService _blobService;
        private readonly ITableService _tableService;
        private readonly ILogger<VoterExportTrigger> _logger;

        public VoterExportTrigger(IBlobService blobService, ITableService tableService, ILogger<VoterExportTrigger> logger)
        {
            _blobService = blobService;
            _tableService = tableService;
            _logger = logger;
        }

        [Function("ExportVoterToCsv")]
        public async Task Run(
            [ServiceBusTrigger(
                "%VoterQueueName%",
                Connection = "ServiceBusConnection")]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions,
            FunctionContext context)
        {
            var cancellationToken = context.CancellationToken;
            Voter? processingVoter = null;

            try
            {
                var exportMessage = JsonSerializer.Deserialize<VoterExportMessage>(message.Body.ToString(), JsonOptions);
                if (exportMessage is null || string.IsNullOrWhiteSpace(exportMessage.RowKey))
                {
                    throw new JsonException("The queue message does not contain a voter row key.");
                }

                var voter = await _tableService.GetVoterAsync(exportMessage.RowKey, cancellationToken);
                if (voter is null)
                {
                    _logger.LogWarning("Voter row {RowKey} no longer exists; completing export message {MessageId}.", exportMessage.RowKey, message.MessageId);
                    await messageActions.CompleteMessageAsync(message, cancellationToken);
                    return;
                }
                processingVoter = voter;

                if (voter.ExportStatus == "Exported")
                {
                    _logger.LogInformation("Voter {FullName} with row key {RowKey} is already exported; completing duplicate message {MessageId}.", voter.FullName, voter.RowKey, message.MessageId);
                    await messageActions.CompleteMessageAsync(message, cancellationToken);
                    return;
                }

                await _blobService.AddVoterToCsvAsync(voter, cancellationToken);
                await _tableService.MarkVoterExportedAsync(voter, cancellationToken);
                _logger.LogInformation("Exported voter {FullName} with row key {RowKey} to CSV.", voter.FullName, voter.RowKey);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to export Service Bus message {MessageId}; dead-lettering it.",
                    message.MessageId);

                if (processingVoter is not null)
                {
                    try
                    {
                        await _tableService.MarkVoterPendingAsync(processingVoter, cancellationToken);
                    }
                    catch (Exception statusException)
                    {
                        _logger.LogError(statusException, "Could not return voter {FullName} with row key {RowKey} to pending export status.", processingVoter.FullName, processingVoter.RowKey);
                    }
                }

                await messageActions.DeadLetterMessageAsync(
                    message,
                    deadLetterReason: "VoterCsvExportFailed",
                    deadLetterErrorDescription: ex.Message,
                    cancellationToken: cancellationToken);

                _logger.LogWarning(
                    "Dead-lettered Service Bus message {MessageId} after CSV export failed.",
                    message.MessageId);
                return;
            }

            await messageActions.CompleteMessageAsync(message, cancellationToken);
            _logger.LogInformation(
                "Completed Service Bus message {MessageId} after voter CSV export.",
                message.MessageId);
        }

        private sealed record VoterExportMessage(string RowKey);
    }
}
