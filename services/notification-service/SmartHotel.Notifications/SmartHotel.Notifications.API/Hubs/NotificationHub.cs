using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SmartHotel.Notifications.Application;

namespace SmartHotel.Notifications.API.Hubs;

[Authorize]
public class NotificationHub : Hub<INotificationHubClient>
{
    private readonly IChatService _chatService;
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(IChatService chatService, ILogger<NotificationHub> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        var connectionId = Context.ConnectionId;

        if (user != null)
        {
            // 1. Join user personal group: user_{UserId}
            var sub = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? user.FindFirst("sub")?.Value;

            if (!string.IsNullOrEmpty(sub) && Guid.TryParse(sub, out var userId))
            {
                var userGroup = $"user_{userId}";
                await Groups.AddToGroupAsync(connectionId, userGroup);
                _logger.LogInformation("Connection {ConnectionId} joined personal group {Group}", connectionId, userGroup);
            }

            // 2. Join role groups: role_{RoleName}
            var roles = user.FindAll(ClaimTypes.Role)
                .Concat(user.FindAll("role"))
                .Select(c => c.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var role in roles)
            {
                var roleGroup = $"role_{role}";
                await Groups.AddToGroupAsync(connectionId, roleGroup);
                _logger.LogInformation("Connection {ConnectionId} joined role group {Group}", connectionId, roleGroup);
            }

            // 3. Join department group: dept_{DepartmentId} if claim present
            var deptId = user.FindFirst("departmentId")?.Value
                         ?? user.FindFirst("dept")?.Value;

            if (!string.IsNullOrEmpty(deptId))
            {
                var deptGroup = $"dept_{deptId}";
                await Groups.AddToGroupAsync(connectionId, deptGroup);
                _logger.LogInformation("Connection {ConnectionId} joined department group {Group}", connectionId, deptGroup);
            }
        }

        await base.OnConnectedAsync();
    }

    public async Task JoinRoom(Guid roomId)
    {
        var groupName = $"room_{roomId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);

        var (userId, userName) = GetUserIdentity();
        await Clients.Group(groupName).UserJoinedRoom(roomId, userId, userName);
        _logger.LogInformation("User {UserId} ({UserName}) joined SignalR room group {GroupName}", userId, userName, groupName);
    }

    public async Task LeaveRoom(Guid roomId)
    {
        var groupName = $"room_{roomId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);

        var (userId, userName) = GetUserIdentity();
        await Clients.Group(groupName).UserLeftRoom(roomId, userId, userName);
        _logger.LogInformation("User {UserId} ({UserName}) left SignalR room group {GroupName}", userId, userName, groupName);
    }

    public async Task SendMessage(Guid roomId, string content)
    {
        var (userId, userName) = GetUserIdentity();
        var role = Context.User?.FindFirst(ClaimTypes.Role)?.Value
                   ?? Context.User?.FindFirst("role")?.Value
                   ?? "Employee";

        // Persist and broadcast message
        await _chatService.SendMessageAsync(roomId, userId, userName, role, content);
    }

    private (Guid UserId, string UserName) GetUserIdentity()
    {
        var user = Context.User;
        var sub = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                  ?? user?.FindFirst("sub")?.Value;

        var userId = Guid.TryParse(sub, out var parsed) ? parsed : Guid.Empty;
        var name = user?.FindFirst("name")?.Value
                   ?? user?.FindFirst(ClaimTypes.Name)?.Value
                   ?? user?.FindFirst(ClaimTypes.Email)?.Value
                   ?? user?.FindFirst("email")?.Value
                   ?? "Staff";

        return (userId, name);
    }
}
