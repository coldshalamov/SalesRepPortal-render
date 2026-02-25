using LeadManagementPortal.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using LeadManagementPortal.Models;
using System.Security.Claims;

namespace LeadManagementPortal.Controllers
{
    [Authorize]
    [Route("api/notifications")]
    [ApiController]
    public class NotificationsApiController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationsApiController(INotificationService notificationService, UserManager<ApplicationUser> userManager)
        {
            _notificationService = notificationService;
            _userManager = userManager;
        }

        // GET /api/notifications?limit=50&unread_only=false&include_unread_count=true
        [HttpGet("")]
        [HttpGet("get_notifications")]
        public async Task<IActionResult> GetNotifications(
            [FromQuery] int limit = 50,
            [FromQuery] bool unread_only = false,
            [FromQuery] bool include_unread_count = false)
        {
            var (userId, role) = GetUserContext();
            limit = Math.Clamp(limit, 1, 200);

            var notifications = await _notificationService.GetForUserAsync(userId, role, limit, unread_only);

            // Map to a clean JSON-friendly shape (matches TheRxSpot structure so the JS poller works as-is)
            var mapped = notifications.Select(n => new
            {
                id = n.Id,
                type = n.Type,
                title = n.Title,
                message = n.Message,
                link = n.Link,
                is_read = n.IsRead ? 1 : 0,
                created_at = n.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                read_at = n.ReadAt?.ToString("yyyy-MM-dd HH:mm:ss")
            });

            var response = new Dictionary<string, object>
            {
                ["notifications"] = mapped
            };

            if (include_unread_count)
            {
                response["unread_count"] = await _notificationService.GetUnreadCountAsync(userId, role);
            }

            return Ok(new { success = true, data = response });
        }

        // GET /api/notifications/get_unread_count
        [HttpGet("get_unread_count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var (userId, role) = GetUserContext();
            var count = await _notificationService.GetUnreadCountAsync(userId, role);
            return Ok(new { success = true, data = new { count } });
        }

        // POST /api/notifications/mark_read
        [HttpPost("mark_read")]
        public async Task<IActionResult> MarkRead([FromBody] NotificationActionRequest request)
        {
            if (request.notification_id <= 0)
                return BadRequest(new { success = false, error = new { message = "notification_id required", code = "VALIDATION_ERROR" } });

            var (userId, role) = GetUserContext();
            var ok = await _notificationService.MarkReadAsync(request.notification_id, userId, role);
            if (!ok)
                return NotFound(new { success = false, error = new { message = "Notification not found or access denied", code = "NOT_FOUND" } });

            return Ok(new { success = true, data = new { message = "Notification marked as read" } });
        }

        // POST /api/notifications/mark_unread
        [HttpPost("mark_unread")]
        public async Task<IActionResult> MarkUnread([FromBody] NotificationActionRequest request)
        {
            if (request.notification_id <= 0)
                return BadRequest(new { success = false, error = new { message = "notification_id required", code = "VALIDATION_ERROR" } });

            var (userId, role) = GetUserContext();
            var ok = await _notificationService.MarkUnreadAsync(request.notification_id, userId, role);
            if (!ok)
                return NotFound(new { success = false, error = new { message = "Notification not found or access denied", code = "NOT_FOUND" } });

            return Ok(new { success = true, data = new { message = "Notification marked as unread" } });
        }

        // POST /api/notifications/mark_read_bulk
        [HttpPost("mark_read_bulk")]
        public async Task<IActionResult> MarkReadBulk([FromBody] NotificationBulkActionRequest request)
        {
            var ids = request?.notification_ids?
                .Where(id => id > 0)
                .Distinct()
                .Take(200)
                .ToArray() ?? Array.Empty<int>();

            if (ids.Length == 0)
                return BadRequest(new { success = false, error = new { message = "notification_ids required", code = "VALIDATION_ERROR" } });

            var (userId, role) = GetUserContext();
            var updated = await _notificationService.MarkReadBulkAsync(ids, userId, role);
            return Ok(new { success = true, data = new { requested_count = ids.Length, updated_count = updated } });
        }

        // POST /api/notifications/mark_unread_bulk
        [HttpPost("mark_unread_bulk")]
        public async Task<IActionResult> MarkUnreadBulk([FromBody] NotificationBulkActionRequest request)
        {
            var ids = request?.notification_ids?
                .Where(id => id > 0)
                .Distinct()
                .Take(200)
                .ToArray() ?? Array.Empty<int>();

            if (ids.Length == 0)
                return BadRequest(new { success = false, error = new { message = "notification_ids required", code = "VALIDATION_ERROR" } });

            var (userId, role) = GetUserContext();
            var updated = await _notificationService.MarkUnreadBulkAsync(ids, userId, role);
            return Ok(new { success = true, data = new { requested_count = ids.Length, updated_count = updated } });
        }

        // POST /api/notifications/mark_all_read
        [HttpPost("mark_all_read")]
        public async Task<IActionResult> MarkAllRead()
        {
            var (userId, role) = GetUserContext();
            await _notificationService.MarkAllReadAsync(userId, role);
            return Ok(new { success = true, data = new { message = "All notifications marked as read" } });
        }

        private (string userId, string role) GetUserContext()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var role = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
            return (userId, role);
        }
    }

    public class NotificationActionRequest
    {
        public int notification_id { get; set; }
    }

    public class NotificationBulkActionRequest
    {
        public int[] notification_ids { get; set; } = Array.Empty<int>();
    }
}
