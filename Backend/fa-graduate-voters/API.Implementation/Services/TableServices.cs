using System.Text.Json;
using API.Implementation.Models;
using Azure.Data.Tables;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Azure;
using Response = API.Implementation.Models.Response;

namespace API.Implementation.Services
{
    public sealed class TableService : ITableService, IAsyncDisposable
    {
        private const int ExportBatchSize = 500;
        private const int DefaultStaleQueuedMinutes = 10;
        private readonly TableClient _tableClient;
        private readonly TableClient _adminTableClient;
        private readonly ServiceBusClient _serviceBusClient;
        private readonly ServiceBusSender _sender;
        private readonly ILogger<TableService> _logger;
        private readonly int _staleQueuedMinutes;

        public TableService(IConfiguration configuration, ILogger<TableService> logger)
        {
            _logger = logger;
            _staleQueuedMinutes = int.TryParse(configuration["VoterExportStaleMinutes"], out var configuredStaleMinutes) && configuredStaleMinutes > 0
                ? configuredStaleMinutes
                : DefaultStaleQueuedMinutes;

            var storageConnectionString = configuration["StorageConnectionString"]
                ?? throw new InvalidOperationException("StorageConnectionString is not configured.");
            var tableName = configuration["VoterTableName"]
                ?? throw new InvalidOperationException("VoterTableName is not configured.");
            var adminTableName = configuration["AdminTableName"]
                ?? throw new InvalidOperationException("AdminTableName is not configured.");
            var serviceBusConnectionString = configuration["ServiceBusConnection"]
                ?? throw new InvalidOperationException("ServiceBusConnection is not configured.");
            var queueName = configuration["VoterQueueName"]
                ?? throw new InvalidOperationException("VoterQueueName is not configured.");

            _tableClient = new TableClient(storageConnectionString, tableName);
            _adminTableClient = new TableClient(storageConnectionString, adminTableName);
            _serviceBusClient = new ServiceBusClient(serviceBusConnectionString);
            _sender = _serviceBusClient.CreateSender(queueName);
        }

        public async Task<Response> AddVoterAsync(Voter voter)
        {
            try
            {
                voter.PartitionKey = "Voters";
                voter.RowKey = Guid.NewGuid().ToString();
                voter.NormalizedFullName = NormalizeName(voter.FullName);
                voter.NormalizedPhoneNumber = NormalizePhone(voter.PhoneNumber);
                voter.ExportStatus = "Pending";

                await _tableClient.CreateIfNotExistsAsync();

                var duplicateFilter = TableClient.CreateQueryFilter(
                    $"PartitionKey eq {voter.PartitionKey} and NormalizedFullName eq {voter.NormalizedFullName} and NormalizedPhoneNumber eq {voter.NormalizedPhoneNumber}");
                await foreach (var existing in _tableClient.QueryAsync<Voter>(filter: duplicateFilter))
                {
                    _logger.LogWarning("Duplicate voter registration rejected for {FullName}.", voter.FullName);
                    return new Response { Success = false, Conflict = true, Message = "A registration with this name and phone number already exists." };
                }

                await _tableClient.AddEntityAsync(voter);
                _logger.LogInformation(
                    "Stored voter {FullName} in Azure Table Storage with row key {RowKey}.",
                    voter.FullName,
                    voter.RowKey);

                return new Response { Success = true, Message = "Voter added successfully." };
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to store voter {FullName}.",
                    voter.FullName);
                return new Response { Success = false, Message = ex.Message };
            }
        }

        public async Task<int> QueuePendingVotersAsync(CancellationToken cancellationToken = default)
        {
            // Azure Table Storage does not support null comparisons in OData filters.
            // New registrations are explicitly initialized to Pending before insertion.
            var filter = TableClient.CreateQueryFilter($"PartitionKey eq {"Voters"} and ExportStatus eq {"Pending"}");
            var queued = 0;
            await foreach (var voter in _tableClient.QueryAsync<Voter>(filter: filter, maxPerPage: ExportBatchSize, cancellationToken: cancellationToken))
            {
                if (queued >= ExportBatchSize)
                {
                    break;
                }
                if (await QueueVoterAsync(voter, cancellationToken)) queued++;
            }

            var staleBefore = DateTimeOffset.UtcNow.AddMinutes(-_staleQueuedMinutes);
            var staleFilter = TableClient.CreateQueryFilter(
                $"PartitionKey eq {"Voters"} and ExportStatus eq {"Queued"}");
            var recovered = 0;
            await foreach (var voter in _tableClient.QueryAsync<Voter>(filter: staleFilter, maxPerPage: ExportBatchSize, cancellationToken: cancellationToken))
            {
                if (queued >= ExportBatchSize) break;
                if (voter.ExportQueuedAt.HasValue && voter.ExportQueuedAt.Value >= staleBefore) continue;
                _logger.LogWarning("Voter {FullName} with row key {RowKey} has been queued for over {StaleMinutes} minutes; requeueing it.", voter.FullName, voter.RowKey, _staleQueuedMinutes);
                if (await QueueVoterAsync(voter, cancellationToken))
                {
                    queued++;
                    recovered++;
                }
            }

            _logger.LogInformation("Queued {QueuedCount} voter(s), including {RecoveredCount} stale queued voter(s).", queued, recovered);
            return queued;
        }

        private async Task<bool> QueueVoterAsync(Voter voter, CancellationToken cancellationToken)
        {
            voter.ExportStatus = "Queued";
            voter.ExportQueuedAt = DateTimeOffset.UtcNow;
            try
            {
                await _tableClient.UpdateEntityAsync(voter, voter.ETag, TableUpdateMode.Replace, cancellationToken);
            }
            catch (RequestFailedException ex) when (ex.Status == 412)
            {
                _logger.LogInformation("Voter {FullName} changed before queueing; skipping this row for this timer run.", voter.FullName);
                return false;
            }

            var message = new ServiceBusMessage(JsonSerializer.Serialize(new VoterExportMessage(voter.RowKey)))
            {
                ContentType = "application/json",
                MessageId = voter.RowKey
            };

            try
            {
                await _sender.SendMessageAsync(message, cancellationToken);
                _logger.LogInformation("Queued voter {FullName} with row key {RowKey} for CSV export.", voter.FullName, voter.RowKey);
                return true;
            }
            catch
            {
                try
                {
                    await MarkVoterPendingAsync(voter, cancellationToken);
                }
                catch (Exception resetException)
                {
                    _logger.LogError(resetException, "Could not reset voter {FullName} with row key {RowKey} after queue send failed.", voter.FullName, voter.RowKey);
                }

                throw;
            }
        }

        public async Task<Voter?> GetVoterAsync(string rowKey, CancellationToken cancellationToken = default)
        {
            try
            {
                var result = await _tableClient.GetEntityAsync<Voter>("Voters", rowKey, cancellationToken: cancellationToken);
                return result.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
        }

        public async Task MarkVoterExportedAsync(Voter voter, CancellationToken cancellationToken = default)
        {
            voter.ExportStatus = "Exported";
            await _tableClient.UpdateEntityAsync(voter, voter.ETag, TableUpdateMode.Replace, cancellationToken);
            _logger.LogInformation("Marked voter {FullName} with row key {RowKey} as exported.", voter.FullName, voter.RowKey);
        }

        public async Task MarkVoterPendingAsync(Voter voter, CancellationToken cancellationToken = default)
        {
            var current = await GetVoterAsync(voter.RowKey, cancellationToken);
            if (current is null || current.ExportStatus == "Exported")
            {
                _logger.LogInformation("Voter {FullName} with row key {RowKey} is already exported or no longer exists; leaving status unchanged.", voter.FullName, voter.RowKey);
                return;
            }

            current.ExportStatus = "Pending";
            await _tableClient.UpdateEntityAsync(current, current.ETag, TableUpdateMode.Replace, cancellationToken);
            _logger.LogWarning("Returned voter {FullName} with row key {RowKey} to pending export status.", current.FullName, current.RowKey);
        }

        private static string NormalizeName(string? name) => string.Join(' ', (name ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
        private static string NormalizePhone(string? phone)
        {
            var digits = new string((phone ?? string.Empty).Where(char.IsDigit).ToArray());
            return digits.Length == 12 && digits.StartsWith("91", StringComparison.Ordinal) ? digits[2..] : digits;
        }

        private sealed record VoterExportMessage(string RowKey);

        public async Task<Response> ProcessLoginAsync(Admin admin)
        {
            if (admin is null || string.IsNullOrWhiteSpace(admin.Username) || string.IsNullOrWhiteSpace(admin.Password))
            {
                return new Response { Success = false, Message = "Username and password are required." };
            }

            try
            {
                var filter = TableClient.CreateQueryFilter($"Username eq {admin.Username}");
                await foreach (var entity in _adminTableClient.QueryAsync<TableEntity>(filter: filter))
                {
                    var storedPassword = entity.GetString("Password");
                    if (string.Equals(storedPassword, admin.Password, StringComparison.Ordinal))
                    {
                        _logger.LogInformation("Admin login succeeded for {Username}.", admin.Username);
                        return new Response { Success = true, Message = "Login successful." };
                    }
                }

                _logger.LogWarning("Admin login failed for {Username}.", admin.Username);
                return new Response { Success = false, Message = "Invalid username or password." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not check admin credentials for {Username}.", admin.Username);
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _sender.DisposeAsync();
            await _serviceBusClient.DisposeAsync();
        }
    }
}
