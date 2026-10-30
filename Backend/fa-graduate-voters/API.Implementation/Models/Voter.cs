using Azure;
using Azure.Data.Tables;

namespace API.Implementation.Models
{
    public class Voter : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        public string FullName {get;set;}
        public string PhoneNumber {get;set;}
        public string Constituency {get;set;}
        public string District {get;set;}
        public string YearOfGraduation {get;set;}
        public string Email {get;set;}
        public string ExportStatus { get; set; } = "Pending";
        public DateTimeOffset? ExportQueuedAt { get; set; }
        public string NormalizedFullName { get; set; } = string.Empty;
        public string NormalizedPhoneNumber { get; set; } = string.Empty;
    }
}
