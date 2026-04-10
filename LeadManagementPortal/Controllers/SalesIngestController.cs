using System.Text.Json;
using System.Text.Json.Serialization;
using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using LeadManagementPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeadManagementPortal.Controllers
{
    [ApiController]
    [Route("api/sales")]
    public class SalesIngestController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ICommissionCalculationService _commissionCalculationService;
        private readonly IConfiguration _configuration;

        public SalesIngestController(
            ApplicationDbContext context,
            ICommissionCalculationService commissionCalculationService,
            IConfiguration configuration)
        {
            _context = context;
            _commissionCalculationService = commissionCalculationService;
            _configuration = configuration;
        }

        [AllowAnonymous]
        [HttpPost("ingest")]
        public async Task<IActionResult> Ingest([FromBody] IReadOnlyList<SalesIngestRecordRequest>? records, CancellationToken cancellationToken = default)
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

            var accountIds = records
                .Select(r => r.AccountId.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var knownAccountIds = await _context.Users
                .AsNoTracking()
                .Where(u => accountIds.Contains(u.Id))
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

            var missingAccountIds = accountIds
                .Except(knownAccountIds, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (missingAccountIds.Count > 0)
            {
                return BadRequest(new
                {
                    status = "invalid",
                    message = "One or more account ids do not exist.",
                    missingAccountIds
                });
            }

            var batchId = Guid.NewGuid().ToString("N");
            var importedAt = DateTime.UtcNow;
            var sales = records.Select(record => new SaleRecord
            {
                AccountId = record.AccountId.Trim(),
                ProductName = record.ProductName.Trim(),
                Quantity = record.Quantity,
                GrossAmount = record.GrossAmount,
                CostAmount = record.CostAmount,
                SaleDate = record.SaleDate,
                ImportBatchId = batchId,
                ImportedAt = importedAt,
                RawPayload = record.ToRawPayloadJson()
            }).ToList();

            _context.SaleRecords.AddRange(sales);
            await _context.SaveChangesAsync(cancellationToken);

            foreach (var sale in sales)
            {
                await _commissionCalculationService.CalculateForSaleAsync(sale, cancellationToken);
            }

            return Ok(new
            {
                batchId,
                recordCount = sales.Count,
                status = "processed"
            });
        }
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

        public string ToRawPayloadJson()
        {
            if (AdditionalData == null || AdditionalData.Count == 0)
            {
                return "{}";
            }

            return JsonSerializer.Serialize(AdditionalData);
        }
    }
}
