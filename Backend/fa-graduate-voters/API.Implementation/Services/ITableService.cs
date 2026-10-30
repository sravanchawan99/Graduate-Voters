using API.Implementation.Models;

namespace API.Implementation.Services
{
    public interface ITableService
    {
        Task<Response> AddVoterAsync(Voter voter);
        Task<Response> ProcessLoginAsync(Admin admin);
        Task<int> QueuePendingVotersAsync(CancellationToken cancellationToken = default);
        Task<Voter?> GetVoterAsync(string rowKey, CancellationToken cancellationToken = default);
        Task MarkVoterExportedAsync(Voter voter, CancellationToken cancellationToken = default);
        Task MarkVoterPendingAsync(Voter voter, CancellationToken cancellationToken = default);
    }
}
