using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.Notifications.Application;

namespace SmartHotel.Notifications.API.Controllers;

[ApiController]
[Route("api/v1/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpGet("my")]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> GetMyNotifications(
        [FromQuery] bool unreadOnly = false,
        CancellationToken ct = default)
    {
        var userId = GetUserId();
        var notifications = await _notificationService.GetUserNotificationsAsync(userId, unreadOnly, ct);
        return Ok(notifications);
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount(CancellationToken ct = default)
    {
        var userId = GetUserId();
        var count = await _notificationService.GetUnreadCountAsync(userId, ct);
        return Ok(new { unreadCount = count });
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id, CancellationToken ct = default)
    {
        var userId = GetUserId();
        await _notificationService.MarkAsReadAsync(id, userId, ct);
        return Ok(new { message = "Notification marked as read." });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken ct = default)
    {
        var userId = GetUserId();
        await _notificationService.MarkAllAsReadAsync(userId, ct);
        return Ok(new { message = "All notifications marked as read." });
    }

    private Guid GetUserId()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? User.FindFirst("sub")?.Value;

        if (Guid.TryParse(sub, out var userId))
        {
            return userId;
        }

        throw new UnauthorizedAccessException("Unable to resolve user ID from authentication token.");
    }
}
