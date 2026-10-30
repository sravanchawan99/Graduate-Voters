using API.Implementation.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace API.Implementation.Services
{
    public sealed class BlobService : IBlobService
    {
        private const string CsvHeader = "FullName,PhoneNumber,Constituency,District,YearOfGraduation,Email";
        private const string HyderabadConstituency = "Mahabubnagar-Ranga Reddy-Hyderabad";
        private const string WarangalConstituency = "Warangal-Khammam-Nalgonda";

        private readonly BlobContainerClient _containerClient;
        private readonly ILogger<BlobService> _logger;

        public BlobService(IConfiguration configuration, ILogger<BlobService> logger)
        {
            _logger = logger;

            var storageConnectionString = configuration["StorageConnectionString"]
                ?? throw new InvalidOperationException("StorageConnectionString is not configured.");
            var containerName = configuration["VoterBlobContainerName"]
                ?? throw new InvalidOperationException("VoterBlobContainerName is not configured.");
            _containerClient = new BlobServiceClient(storageConnectionString)
                .GetBlobContainerClient(containerName);
        }

        public async Task AddVoterToCsvAsync(
            Voter voter,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(voter);
            if (string.IsNullOrWhiteSpace(voter.RowKey))
            {
                throw new ArgumentException("Voter must have a row key.", nameof(voter));
            }

            await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            var activeBlob = GetCsvBlobClient(voter.Constituency);
            var csv = CsvHeader + "\r\n";
            if ((await activeBlob.ExistsAsync(cancellationToken)).Value)
            {
                var existingCsv = (await activeBlob.DownloadContentAsync(cancellationToken)).Value.Content.ToString();
                if (ContainsVoterRow(existingCsv, CreateCsvRow(voter)))
                {
                    _logger.LogInformation(
                        "Voter {VoterRowKey} is already in CSV blob {BlobName}.",
                        voter.RowKey,
                        activeBlob.Name);
                    return;
                }

                csv = AppendVoterRow(existingCsv, CreateCsvRow(voter));
            }
            else
            {
                csv += CreateCsvRow(voter) + "\r\n";
            }

            await UploadCsvAsync(activeBlob, csv, cancellationToken);

            _logger.LogInformation(
                "Updated voter CSV blob {BlobName} with voter row {VoterRowKey}.",
                activeBlob.Name,
                voter.RowKey);
        }

        public async Task<(string FileName, Stream Content)?> DownloadLatestCsvAsync(
            string constituency,
            CancellationToken cancellationToken = default)
        {
            await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            var activeBlob = GetCsvBlobClient(constituency);
            if (!(await activeBlob.ExistsAsync(cancellationToken)).Value)
            {
                _logger.LogInformation("No voter CSV file exists for constituency {Constituency}.", constituency);
                return null;
            }

            var download = await activeBlob.DownloadContentAsync(cancellationToken);
            _logger.LogInformation("Downloaded voter CSV blob {BlobName}.", activeBlob.Name);
            return (Path.GetFileName(activeBlob.Name), download.Value.Content.ToStream());
        }

        private BlobClient GetCsvBlobClient(string constituency)
        {
            var (directory, fileName) = constituency switch
            {
                HyderabadConstituency => ("hyderabad/export", "hyderabad_voters.csv"),
                WarangalConstituency => ("warangal/export", "warangal_voters.csv"),
                _ => throw new ArgumentException("Unsupported voter constituency.", nameof(constituency))
            };
            var blobName = $"{directory}/{fileName}";
            return _containerClient.GetBlobClient(blobName);
        }

        private static async Task UploadCsvAsync(
            BlobClient blobClient,
            string csv,
            CancellationToken cancellationToken)
        {
            await blobClient.UploadAsync(
                BinaryData.FromString(csv),
                new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders { ContentType = "text/csv; charset=utf-8" }
                },
                cancellationToken);
        }

        private static string AppendVoterRow(string existingCsv, string voterRow)
        {
            var csv = string.IsNullOrWhiteSpace(existingCsv)
                ? CsvHeader + "\r\n"
                : existingCsv;

            if (!csv.EndsWith('\n'))
            {
                csv += "\r\n";
            }

            return csv + voterRow + "\r\n";
        }

        private static bool ContainsVoterRow(string csv, string voterRow)
        {
            return csv.Contains(voterRow, StringComparison.Ordinal);
        }

        private static string CreateCsvRow(Voter voter)
        {
            return string.Join(",", new[]
            {
                EscapeCsv(voter.FullName),
                EscapeCsv(voter.PhoneNumber),
                EscapeCsv(voter.Constituency),
                EscapeCsv(voter.District),
                EscapeCsv(voter.YearOfGraduation),
                EscapeCsv(voter.Email)
            });
        }

        private static string EscapeCsv(string? value)
        {
            var safeValue = value ?? string.Empty;
            if (!safeValue.Contains(',') && !safeValue.Contains('"') &&
                !safeValue.Contains('\r') && !safeValue.Contains('\n'))
            {
                return safeValue;
            }

            return $"\"{safeValue.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
        }
    }
}
