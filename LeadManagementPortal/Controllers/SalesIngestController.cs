using System.Text.Json;
using System.Text.Json.Serialization;
using LeadManagementPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeadManagementPortal.Controllers
{
    [ApiController]
    [Route("api/sales")]
    public class SalesIngestController : ControllerBase
    {
        private readonly ICommissionControlPlaneService _commissionControlPlaneService;
        private readonly IConfiguration _configuration;

        public SalesIngestController(
            ICommissionControlPlaneService commissionControlPlaneService,
            IConfiguration configuration)
        {
            _commissionControlPlaneService = commissionControlPlaneService;
            _configuration = configuration;
        }

        [AllowAnonymous]
        [HttpPost("ingest")]
        public async Task<IActionResult> Ingest([FromBody] IReadOnlyList<SalesIngestRecordRequest>? records, CancellationToken cancellationToken = default)
        {
            var authResult = ValidateApiKey();
            if (authResult != null)
            {
                return authResult;
            }

            if (records == null || records.Count == 0)
            {
                return BadRequest(new
                {
                    status = "invalid",
                    message = "At least one sale record is required."
                });
            }

            if (records.Any(r => r == null || string.IsNullOrWhiteSpace(r.AccountId) || string.IsNullOrWhiteSpace(r.ProductName)))
            {
                return BadRequest(new
                {
                    status = "invalid",
                    message = "Each sale record must include an account id and product name."
                });
            }

            var batch = await _commissionControlPlaneService.CreateBatchFromLegacySalesAsync(records, "legacy-api", cancellationToken);

            return Ok(new
            {
                batchId = batch.Id,
                recordCount = batch.Rows.Count,
                status = "pending_review"
            });
        }

        [AllowAnonymous]
        [HttpPost("raw-ingest")]
        public async Task<IActionResult> IngestRaw([FromBody] RawSalesImportRequest? request, CancellationToken cancellationToken = default)
        {
            var authResult = ValidateApiKey();
            if (authResult != null)
            {
                return authResult;
            }

            if (request == null || request.Rows == null || request.Rows.Count == 0)
            {
                return BadRequest(new
                {
                    status = "invalid",
                    message = "At least one raw row is required."
                });
            }

            var rows = request.Rows
                .Select(row => (IDictionary<string, string?>)row.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value.ValueKind == JsonValueKind.Null ? null : kvp.Value.ToString(),
                    StringComparer.OrdinalIgnoreCase))
                .ToList();

            var batch = await _commissionControlPlaneService.CreateBatchFromRawRowsAsync(
                request.SourceSystem ?? "raw-api",
                rows,
                request.ImportProfileId,
                uploadedById: null,
                sourceFileName: request.SourceFileName,
                cancellationToken);

            return Ok(new
            {
                batchId = batch.Id,
                recordCount = batch.Rows.Count,
                status = batch.Status.ToString()
            });
        }

        private IActionResult? ValidateApiKey()
        {
            var configuredApiKey = _configuration["SalesIngest:ApiKey"];
            if (string.IsNullOrWhiteSpace(configuredApiKey))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    status = "error",
                    message = "Sales ingest API key is not configured."
                });
            }

            if (!Request.Headers.TryGetValue("X-Api-Key", out var providedApiKey) || providedApiKey != configuredApiKey)
            {
                return Unauthorized(new
                {
                    status = "unauthorized",
                    message = "A valid X-Api-Key header is required."
                });
            }

            return null;
        }
    }

    public class RawSalesImportRequest
    {
        public string? SourceSystem { get; set; }
        public string? SourceFileName { get; set; }
        public int? ImportProfileId { get; set; }
        public List<Dictionary<string, JsonElement>> Rows { get; set; } = new();
    }

    public class SalesIngestRecordRequest
    {
        public string AccountId { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal GrossAmount { get; set; }
        public decimal? CostAmount { get; set; }
        public DateTime SaleDate { get; set; }

        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalData { get; set; }
    }
}
