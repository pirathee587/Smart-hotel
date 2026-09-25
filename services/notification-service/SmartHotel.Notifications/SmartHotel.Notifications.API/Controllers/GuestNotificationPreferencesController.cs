using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SmartHotel.Notifications.API.Controllers;

public class NotificationPreferencesDto
{
    public bool BookingUpdates { get; set; } = true;
    public bool ServiceRequestUpdates { get; set; } = true;
    public bool Promotions { get; set; } = false;
}

[ApiController]
[Route("api/guests")]
[Produces("application/json")]
public class GuestNotificationPreferencesController : ControllerBase
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, NotificationPreferencesDto> _prefsStore = new();

    [HttpGet("{id}/notification-preferences")]
    [AllowAnonymous]
    public IActionResult GetPreferences([FromRoute] string id)
    {
        var prefs = _prefsStore.TryGetValue(id, out var val) ? val : new NotificationPreferencesDto();
        return Ok(prefs);
    }

    [HttpPatch("{id}/notification-preferences")]
    [AllowAnonymous]
    public IActionResult UpdatePreferences([FromRoute] string id, [FromBody] NotificationPreferencesDto updated)
    {
        _prefsStore[id] = updated;
        return Ok(updated);
    }
}
