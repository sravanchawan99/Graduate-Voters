using API.Implementation.Models;

namespace API.Implementation.Services
{
    public interface IBlobService
    {
        Task AddVoterToCsvAsync(
            Voter voter,
            CancellationToken cancellationToken = default);

        Task<(string FileName, Stream Content)?> DownloadLatestCsvAsync(
            string constituency,
            CancellationToken cancellationToken = default);
    }
}
