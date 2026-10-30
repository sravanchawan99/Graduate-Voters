using System.Net;
using System.Text.Json;
using API.Implementation.Models;
using API.Implementation.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace API.Triggers
{
    public sealed class HttpTriggers
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly ITableService _tableService;
        private readonly IBlobService _blobService;
        private readonly ILogger<HttpTriggers> _logger;

        public HttpTriggers(
            ITableService tableService,
            IBlobService blobService,
            ILogger<HttpTriggers> logger)
        {
            _tableService = tableService;
            _blobService = blobService;
            _logger = logger;
        }

        [Function("AddVoter")]
        public async Task<HttpResponseData> AddVoter(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData request)
        {
            Voter? voter;
            try
            {
                voter = await JsonSerializer.DeserializeAsync<Voter>(
                    request.Body,
                    JsonOptions,
                    request.FunctionContext.CancellationToken);
            }
            catch (JsonException)
            {
                var badJsonResponse = request.CreateResponse(HttpStatusCode.BadRequest);
                await badJsonResponse.WriteAsJsonAsync(
                    new Response { Success = false, Message = "Request body must contain valid voter JSON." });
                return badJsonResponse;
            }

            if (voter is null)
            {
                var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(
                    new Response { Success = false, Message = "A voter must be provided." });
                return badRequest;
            }

            _logger.LogInformation(
                "Received voter registration for {FullName}.",
                voter.FullName);

            var result = await _tableService.AddVoterAsync(voter);
            var response = request.CreateResponse(
                result.Success ? HttpStatusCode.Created : result.Conflict ? HttpStatusCode.Conflict : HttpStatusCode.InternalServerError);
            await response.WriteAsJsonAsync(result);
            return response;
        }

        [Function("Login")]
        public async Task<HttpResponseData> ProcessLogin(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData request)
        {
            Admin? admin;
            try
            {
                admin = await JsonSerializer.DeserializeAsync<Admin>(
                    request.Body,
                    JsonOptions,
                    request.FunctionContext.CancellationToken);
            }
            catch (JsonException)
            {
                var badJson = request.CreateResponse(HttpStatusCode.BadRequest);
                await badJson.WriteAsJsonAsync(
                    new Response { Success = false, Message = "Request body must contain valid login JSON." });
                return badJson;
            }

            if (admin is null || string.IsNullOrWhiteSpace(admin.Username) || string.IsNullOrWhiteSpace(admin.Password))
            {
                var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(
                    new Response { Success = false, Message = "Username and password are required." });
                return badRequest;
            }

            try
            {
                var result = await _tableService.ProcessLoginAsync(admin);
                var response = request.CreateResponse(result.Success ? HttpStatusCode.OK : HttpStatusCode.Unauthorized);
                await response.WriteAsJsonAsync(result);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin login check failed because the admin table could not be queried.");
                var errorResponse = request.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteAsJsonAsync(
                    new Response { Success = false, Message = "Login could not be processed." });
                return errorResponse;
            }
        }

        [Function("DownloadVoterCsv")]
        public async Task<HttpResponseData> DownloadVoterCsv(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData request)
        {
            var constituency = GetQueryParameter(request.Url, "constituency");
            if (constituency is not ("Mahabubnagar-Ranga Reddy-Hyderabad" or "Warangal-Khammam-Nalgonda"))
            {
                var badRequest = request.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(
                    new Response { Success = false, Message = "A supported constituency is required." });
                return badRequest;
            }

            _logger.LogInformation("Received voter CSV download request.");
            var csv = await _blobService.DownloadLatestCsvAsync(constituency, request.FunctionContext.CancellationToken);
            if (csv is null)
            {
                _logger.LogInformation("Voter CSV download request found no completed export.");
                var notFound = request.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteAsJsonAsync(
                    new Response { Success = false, Message = "No voter CSV export exists yet." });
                return notFound;
            }

            var response = request.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/csv; charset=utf-8");
            response.Headers.Add("Content-Disposition", $"attachment; filename=\"{csv.Value.FileName}\"");
            response.Headers.Add("Access-Control-Expose-Headers", "Content-Disposition");
            await using (csv.Value.Content)
            {
                await csv.Value.Content.CopyToAsync(response.Body, request.FunctionContext.CancellationToken);
            }

            _logger.LogInformation("Returned voter CSV file {FileName} to download caller.", csv.Value.FileName);

            return response;
        }

        private static string? GetQueryParameter(Uri url, string name)
        {
            var pair = url.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(item => item.Split('=', 2)[0].Equals(name, StringComparison.OrdinalIgnoreCase));
            if (pair is null) return null;
            var value = pair.Split('=', 2);
            return value.Length == 2 ? Uri.UnescapeDataString(value[1].Replace('+', ' ')) : string.Empty;
        }
    }
}
