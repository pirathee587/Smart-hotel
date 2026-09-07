using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartHotel.Notifications.Application;

namespace SmartHotel.Notifications.API.Controllers;

[ApiController]
[Route("api/v1/chat")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IChatService _chatService;

    public ChatController(IChatService chatService)
    {
        _chatService = chatService;
    }

    [HttpPost("rooms/direct")]
    public async Task<ActionResult<ChatRoomDto>> CreateDirectRoom(
        [FromBody] CreateDirectChatRequest request,
        CancellationToken ct = default)
    {
        var (userId, userName, userRole) = GetUserDetails();
        var room = await _chatService.GetOrCreateDirectRoomAsync(
            userId,
            userName,
            userRole,
            request.TargetUserId,
            request.TargetUserName,
            request.TargetUserRole,
            ct);

        return Ok(room);
    }

    [HttpPost("rooms/task")]
    public async Task<ActionResult<ChatRoomDto>> CreateTaskRoom(
        [FromBody] CreateTaskChatRequest request,
        CancellationToken ct = default)
    {
        var (userId, userName, userRole) = GetUserDetails();

        // Ensure caller is in participants
        var participants = request.Participants.Select(p => (p.UserId, p.UserName, p.UserRole)).ToList();
        if (!participants.Any(p => p.UserId == userId))
        {
            participants.Add((userId, userName, userRole));
        }

        var room = await _chatService.CreateTaskGroupRoomAsync(
            request.TaskId,
            request.TaskTitle,
            participants,
            ct);

        return Ok(room);
    }

    [HttpGet("rooms/my")]
    public async Task<ActionResult<IReadOnlyList<ChatRoomDto>>> GetMyRooms(CancellationToken ct = default)
    {
        var (userId, _, _) = GetUserDetails();
        var rooms = await _chatService.GetUserRoomsAsync(userId, ct);
        return Ok(rooms);
    }

    [HttpGet("rooms/{roomId:guid}/messages")]
    public async Task<ActionResult<IReadOnlyList<ChatMessageDto>>> GetRoomMessages(
        Guid roomId,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        var (userId, _, _) = GetUserDetails();
        try
        {
            var messages = await _chatService.GetRoomMessagesAsync(roomId, userId, limit, ct);
            return Ok(messages);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"Chat room {roomId} not found." });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("rooms/{roomId:guid}/messages")]
    public async Task<ActionResult<ChatMessageDto>> SendMessage(
        Guid roomId,
        [FromBody] SendChatMessageRequest request,
        CancellationToken ct = default)
    {
        var (userId, userName, userRole) = GetUserDetails();
        try
        {
            var message = await _chatService.SendMessageAsync(
                roomId,
                userId,
                userName,
                userRole,
                request.Content,
                ct);

            return CreatedAtAction(nameof(GetRoomMessages), new { roomId }, message);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = $"Chat room {roomId} not found." });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("rooms/{roomId:guid}/read")]
    public async Task<IActionResult> MarkRoomRead(Guid roomId, CancellationToken ct = default)
    {
        var (userId, _, _) = GetUserDetails();
        await _chatService.MarkRoomAsReadAsync(roomId, userId, ct);
        return Ok(new { message = "Room marked as read." });
    }

    private (Guid UserId, string UserName, string Role) GetUserDetails()
    {
        var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? User.FindFirst("sub")?.Value;

        if (!Guid.TryParse(sub, out var userId))
        {
            throw new UnauthorizedAccessException("Unable to resolve user ID from authentication token.");
        }

        var name = User.FindFirst("name")?.Value
                   ?? User.FindFirst(ClaimTypes.Name)?.Value
                   ?? User.FindFirst(ClaimTypes.Email)?.Value
                   ?? User.FindFirst("email")?.Value
                   ?? "Staff";

        var role = User.FindFirst(ClaimTypes.Role)?.Value
                   ?? User.FindFirst("role")?.Value
                   ?? "Employee";

        return (userId, name, role);
    }
}
