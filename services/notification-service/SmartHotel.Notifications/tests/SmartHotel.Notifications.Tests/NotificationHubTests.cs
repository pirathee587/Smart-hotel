using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using SmartHotel.Notifications.API.Hubs;
using SmartHotel.Notifications.Application;
using Xunit;

namespace SmartHotel.Notifications.Tests;

public class NotificationHubTests
{
    private readonly Mock<IChatService> _chatServiceMock;
    private readonly Mock<ILogger<NotificationHub>> _loggerMock;
    private readonly Mock<HubCallerContext> _contextMock;
    private readonly Mock<IGroupManager> _groupManagerMock;
    private readonly Mock<IHubCallerClients<INotificationHubClient>> _clientsMock;
    private readonly Mock<INotificationHubClient> _clientProxyMock;
    private readonly NotificationHub _hub;

    public NotificationHubTests()
    {
        _chatServiceMock = new Mock<IChatService>();
        _loggerMock = new Mock<ILogger<NotificationHub>>();
        _contextMock = new Mock<HubCallerContext>();
        _groupManagerMock = new Mock<IGroupManager>();
        _clientsMock = new Mock<IHubCallerClients<INotificationHubClient>>();
        _clientProxyMock = new Mock<INotificationHubClient>();

        _clientsMock.Setup(c => c.Group(It.IsAny<string>())).Returns(_clientProxyMock.Object);

        _hub = new NotificationHub(_chatServiceMock.Object, _loggerMock.Object)
        {
            Context = _contextMock.Object,
            Groups = _groupManagerMock.Object,
            Clients = _clientsMock.Object
        };
    }

    [Fact]
    public async Task OnConnectedAsync_AutomaticallyJoinsUser_Role_And_DeptGroups()
    {
        // Arrange: User with UserId, Role, and DepartmentId claims
        var userId = Guid.NewGuid();
        var connectionId = "conn-test-123";
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("sub", userId.ToString()),
            new(ClaimTypes.Role, "Housekeeper"),
            new("role", "Housekeeper"),
            new("departmentId", "dept-ops-456")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);

        _contextMock.Setup(c => c.User).Returns(principal);
        _contextMock.Setup(c => c.ConnectionId).Returns(connectionId);

        // Act: Connection established
        await _hub.OnConnectedAsync();

        // Assert: Connection joined all 3 groups
        _groupManagerMock.Verify(g => g.AddToGroupAsync(connectionId, $"user_{userId}", default), Times.Once);
        _groupManagerMock.Verify(g => g.AddToGroupAsync(connectionId, "role_Housekeeper", default), Times.Once);
        _groupManagerMock.Verify(g => g.AddToGroupAsync(connectionId, "dept_dept-ops-456", default), Times.Once);
    }

    [Fact]
    public async Task JoinRoom_AddsConnectionToRoomGroup_And_BroadcastsUserJoined()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var connectionId = "conn-room-01";
        var claims = new List<Claim>
        {
            new("sub", userId.ToString()),
            new(ClaimTypes.Name, "Kamal Staff")
        };
        _contextMock.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(claims)));
        _contextMock.Setup(c => c.ConnectionId).Returns(connectionId);

        // Act
        await _hub.JoinRoom(roomId);

        // Assert
        _groupManagerMock.Verify(g => g.AddToGroupAsync(connectionId, $"room_{roomId}", default), Times.Once);
        _clientProxyMock.Verify(c => c.UserJoinedRoom(roomId, userId, "Kamal Staff"), Times.Once);
    }

    [Fact]
    public async Task LeaveRoom_RemovesConnectionFromRoomGroup_And_BroadcastsUserLeft()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var connectionId = "conn-room-02";
        var claims = new List<Claim>
        {
            new("sub", userId.ToString()),
            new(ClaimTypes.Name, "Kamal Staff")
        };
        _contextMock.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(claims)));
        _contextMock.Setup(c => c.ConnectionId).Returns(connectionId);

        // Act
        await _hub.LeaveRoom(roomId);

        // Assert
        _groupManagerMock.Verify(g => g.RemoveFromGroupAsync(connectionId, $"room_{roomId}", default), Times.Once);
        _clientProxyMock.Verify(c => c.UserLeftRoom(roomId, userId, "Kamal Staff"), Times.Once);
    }

    [Fact]
    public async Task SendMessage_DispatchesToChatService()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var claims = new List<Claim>
        {
            new("sub", userId.ToString()),
            new(ClaimTypes.Name, "Priya"),
            new(ClaimTypes.Role, "Manager")
        };
        _contextMock.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity(claims)));

        // Act
        await _hub.SendMessage(roomId, "Room 304 needs fresh towels");

        // Assert
        _chatServiceMock.Verify(s => s.SendMessageAsync(
            roomId,
            userId,
            "Priya",
            "Manager",
            "Room 304 needs fresh towels",
            default), Times.Once);
    }
}
