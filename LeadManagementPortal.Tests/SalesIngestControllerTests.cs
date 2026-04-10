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

        [Fact]
        public async Task Ingest_WithWrongApiKey_ReturnsUnauthorized()
        {
            await using var context = CreateContext();
            var controller = new SalesIngestController(
                context,
                new CommissionCalculationService(context, NullLogger<CommissionCalculationService>.Instance),
                CreateConfiguration("expected-key"))
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };

            controller.Request.Headers["X-Api-Key"] = "wrong-key";

            var result = await controller.Ingest(new List<SalesIngestRecordRequest>());

            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public async Task Ingest_WithMissingApiKeyHeader_ReturnsUnauthorized()
        {
            await using var context = CreateContext();
            var controller = new SalesIngestController(
                context,
                new CommissionCalculationService(context, NullLogger<CommissionCalculationService>.Instance),
                CreateConfiguration("expected-key"))
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };

            var result = await controller.Ingest(new List<SalesIngestRecordRequest>
            {
                new SalesIngestRecordRequest
                {
                    AccountId = "rep-1",
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
        public async Task Ingest_WithValidApiKey_PersistsRecords_AndCalculatesLedgers()
        {
            await using var context = CreateContext();

            context.Users.Add(new ApplicationUser
            {
                Id = "rep-1",
                UserName = "rep1@example.com",
                Email = "rep1@example.com"
            });
            context.CommissionDeals.Add(new CommissionDeal
            {
                ApplicationUserId = "rep-1",
                DealType = CommissionDealType.GrossPercent,
                Rate = 10m,
                CalculationBasis = CommissionCalculationBasis.DownlineGross
            });
            await context.SaveChangesAsync();

            var controller = new SalesIngestController(
                context,
                new CommissionCalculationService(context, NullLogger<CommissionCalculationService>.Instance),
                CreateConfiguration("expected-key"))
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };

            controller.Request.Headers["X-Api-Key"] = "expected-key";

            var result = await controller.Ingest(new List<SalesIngestRecordRequest>
            {
                new SalesIngestRecordRequest
                {
                    AccountId = "rep-1",
                    ProductName = "Starter Pack",
                    Quantity = 2,
                    GrossAmount = 500m,
                    CostAmount = 300m,
                    SaleDate = DateTime.UtcNow
                }
            });

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(1, context.SaleRecords.Count());
            Assert.Equal(1, context.CommissionLedgers.Count());

            var recordCountProperty = ok.Value?.GetType().GetProperty("recordCount");
            Assert.NotNull(recordCountProperty);
            Assert.Equal(1, (int?)recordCountProperty!.GetValue(ok.Value));
        }

        [Fact]
        public async Task Ingest_WithNullRecord_ReturnsBadRequest()
        {
            await using var context = CreateContext();
            var controller = new SalesIngestController(
                context,
                new CommissionCalculationService(context, NullLogger<CommissionCalculationService>.Instance),
                CreateConfiguration("expected-key"))
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext()
                }
            };

            controller.Request.Headers["X-Api-Key"] = "expected-key";

            var result = await controller.Ingest(new List<SalesIngestRecordRequest> { null! });

            Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}
