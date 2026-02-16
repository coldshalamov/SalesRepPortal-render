using LeadManagementPortal.Data;
using LeadManagementPortal.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeadManagementPortal.Controllers
{
    [Authorize(Roles = UserRoles.OrganizationAdmin)]
    public class AuditController : Controller
    {
        private readonly ApplicationDbContext _context;
        public AuditController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Audit/CustomerActivity
        public async Task<IActionResult> CustomerActivity(string? term, DateTime? from, DateTime? to, string? action)
        {
            var query = _context.CustomerAudits.AsQueryable();
            if (!string.IsNullOrWhiteSpace(term))
            {
                var t = term.ToLower();
                query = query.Where(a => a.Term != null && a.Term.ToLower().Contains(t));
            }
            if (!string.IsNullOrWhiteSpace(action))
            {
                query = query.Where(a => a.Action == action);
            }
            if (from.HasValue)
            {
                query = query.Where(a => a.OccurredAtUtc >= from.Value);
            }
            if (to.HasValue)
            {
                query = query.Where(a => a.OccurredAtUtc <= to.Value);
            }

            var items = await query
                .OrderByDescending(a => a.OccurredAtUtc)
                .Take(500)
                .ToListAsync();

            return View(items);
        }

        // GET: /Audit/LeadActivity
        public async Task<IActionResult> LeadActivity(string? leadId, string? userId, DateTime? from, DateTime? to, string? action)
        {
            var query = _context.LeadAudits.AsQueryable();
            if (!string.IsNullOrWhiteSpace(leadId))
            {
                query = query.Where(a => a.LeadId == leadId);
            }
            if (!string.IsNullOrWhiteSpace(userId))
            {
                query = query.Where(a => a.UserId == userId);
            }
            if (!string.IsNullOrWhiteSpace(action))
            {
                query = query.Where(a => a.Action == action);
            }
            if (from.HasValue)
            {
                query = query.Where(a => a.OccurredAtUtc >= from.Value);
            }
            if (to.HasValue)
            {
                query = query.Where(a => a.OccurredAtUtc <= to.Value);
            }

            var items = await query
                .OrderByDescending(a => a.OccurredAtUtc)
                .Take(500)
                .ToListAsync();

            return View(items);
        }
    }
}
