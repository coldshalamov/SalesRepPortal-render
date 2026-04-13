using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using LeadManagementPortal.Controllers;
using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using LeadManagementPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeadManagementPortal.Tests
{
    public class SalesIngestControllerTests
    {
        private static ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private static IConfiguration CreateConfiguration(string apiKey)
        {
            return new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["SalesIngest:ApiKey"] = apiKey
                })
                .Build();
        }

        private static SalesIngestController CreateController(ApplicationDbContext context)
        {
            return new SalesIngestController(
                new CommissionControlPlaneService(context, NullLogger<CommissionControlPlaneService>.Instance),
                CreateConfiguration("expected-key"))
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };
        }

        [Fact]
        public async Task Ingest_WithWrongApiKey_ReturnsUnauthorized()
        {
            await using var context = CreateContext();
            var controller = CreateController(context);
            controller.Request.Headers["X-Api-Key"] = "wrong-key";

            var result = await controller.Ingest(new List<SalesIngestRecordRequest>());

            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task Ingest_WithMissingApiKeyHeader_ReturnsUnauthorized()
        {
            await using var context = CreateContext();
            var controller = CreateController(context);

            var result = await controller.Ingest(new List<SalesIngestRecordRequest>
            {
                new SalesIngestRecordRequest
                {
                    AccountId = "acct-1",
                    ProductName = "Starter Pack",
                    Quantity = 1,
                    GrossAmount = 100m,
                    SaleDate = DateTime.UtcNow
                }
            });

            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public void Ingest_HasAllowAnonymousAttribute()
        {
            var method = typeof(SalesIngestController).GetMethod(nameof(SalesIngestController.Ingest), BindingFlags.Public | BindingFlags.Instance);

            Assert.NotNull(method);
            Assert.NotEmpty(method!.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true));
        }

        [Fact]
        public async Task Ingest_WithValidApiKey_CreatesRawImportBatch_AndDoesNotPostLedger()
        {
            await using var context = CreateContext();
            var controller = CreateController(context);
            controller.Request.Headers["X-Api-Key"] = "expected-key";

            var result = await controller.Ingest(new List<SalesIngestRecordRequest>
            {
                new SalesIngestRecordRequest
                {
                    AccountId = "acct-1",
                    ProductName = "Starter Pack",
                    Quantity = 2,
                    GrossAmount = 500m,
                    CostAmount = 300m,
                    SaleDate = DateTime.UtcNow,
                    AdditionalData = new Dictionary<string, System.Text.Json.JsonElement>()
                }
            });

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(1, context.ImportBatches.Count());
            Assert.Equal(1, context.ImportRows.Count());
            Assert.Equal(0, context.SaleEvents.Count());
            Assert.Equal(0, context.CommissionLedgerEntries.Count());

            var row = await context.ImportRows.SingleAsync();
            Assert.Equal(ImportRowStatus.PendingReview, row.Status);
            Assert.Contains("acct-1", row.RawPayloadJson, StringComparison.OrdinalIgnoreCase);

            var recordCountProperty = ok.Value?.GetType().GetProperty("recordCount");
            Assert.NotNull(recordCountProperty);
            Assert.Equal(1, (int?)recordCountProperty!.GetValue(ok.Value));
        }

        [Fact]
        public async Task Ingest_WithNullRecord_ReturnsBadRequest()
        {
            await using var context = CreateContext();
            var controller = CreateController(context);
            controller.Request.Headers["X-Api-Key"] = "expected-key";

            var result = await controller.Ingest(new List<SalesIngestRecordRequest> { null! });

            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task IngestRaw_WithValidApiKey_PreservesOpaqueFieldsInRawImportBatch()
        {
            await using var context = CreateContext();
            var controller = CreateController(context);
            controller.Request.Headers["X-Api-Key"] = "expected-key";

            var result = await controller.IngestRaw(new RawSalesImportRequest
            {
                SourceSystem = "apps-script",
                SourceFileName = "sheet-export.json",
                Rows =
                {
                    new Dictionary<string, System.Text.Json.JsonElement>
                    {
                        ["Account"] = System.Text.Json.JsonDocument.Parse("\"Acme Clinic\"").RootElement,
                        ["Gross Sales"] = System.Text.Json.JsonDocument.Parse("\"1200.50\"").RootElement,
                        ["MysteryColumn"] = System.Text.Json.JsonDocument.Parse("\"keep-me\"").RootElement
                    }
                }
            });

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(1, context.ImportBatches.Count());
            Assert.Equal(1, context.ImportRows.Count());

            var batch = await context.ImportBatches.SingleAsync();
            Assert.Equal("apps-script", batch.SourceSystem);
            Assert.Equal("sheet-export.json", batch.SourceFileName);

            var row = await context.ImportRows.SingleAsync();
            Assert.Equal(ImportRowStatus.PendingMapping, row.Status);
            Assert.Contains("MysteryColumn", row.RawPayloadJson, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("keep-me", row.RawPayloadJson, StringComparison.OrdinalIgnoreCase);

            var statusProperty = ok.Value?.GetType().GetProperty("status");
            Assert.NotNull(statusProperty);
            Assert.Equal("PendingReview", statusProperty!.GetValue(ok.Value)?.ToString());
        }
    }
}
