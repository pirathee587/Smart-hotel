using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using SmartHotel.Notifications.Application;
using SmartHotel.Notifications.Domain;
using SmartHotel.Notifications.Infrastructure.Services;
using Xunit;

namespace SmartHotel.Notifications.Tests;

public class ChatServiceTests
{
    private readonly Mock<IChatRepository> _chatRepoMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly Mock<ISignalRNotificationDispatcher> _dispatcherMock;
    private readonly Mock<ILogger<ChatService>> _loggerMock;
    private readonly ChatService _chatService;

    public ChatServiceTests()
    {
        _chatRepoMock = new Mock<IChatRepository>();
        _notificationServiceMock = new Mock<INotificationService>();
        _dispatcherMock = new Mock<ISignalRNotificationDispatcher>();
        _loggerMock = new Mock<ILogger<ChatService>>();

        _chatService = new ChatService(
            _chatRepoMock.Object,
            _notificationServiceMock.Object,
            _dispatcherMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task GetOrCreateDirectRoomAsync_WhenRoomExists_ReturnsExistingRoom()
    {
        // Arrange
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        var existingRoom = new ChatRoom("Alice & Bob", ChatRoomType.Direct);
        existingRoom.AddParticipant(user1, "Alice", "Housekeeper");
        existingRoom.AddParticipant(user2, "Bob", "Manager");

        _chatRepoMock
            .Setup(r => r.FindDirectRoomAsync(user1, user2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingRoom);

        // Act
        var result = await _chatService.GetOrCreateDirectRoomAsync(
            user1, "Alice", "Housekeeper",
            user2, "Bob", "Manager");

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(existingRoom.Id);
        result.Participants.Should().HaveCount(2);
        _chatRepoMock.Verify(r => r.CreateRoomAsync(It.IsAny<ChatRoom>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetOrCreateDirectRoomAsync_WhenNoRoomExists_CreatesAndReturnsNewRoom()
    {
        // Arrange
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        _chatRepoMock
            .Setup(r => r.FindDirectRoomAsync(user1, user2, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatRoom?)null);

        _chatRepoMock
            .Setup(r => r.CreateRoomAsync(It.IsAny<ChatRoom>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatRoom room, CancellationToken _) => room);

        // Act
        var result = await _chatService.GetOrCreateDirectRoomAsync(
            user1, "Alice", "Housekeeper",
            user2, "Bob", "Manager");

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(ChatRoomType.Direct);
        result.Participants.Should().HaveCount(2);
        result.Participants.Select(p => p.UserId).Should().Contain(new[] { user1, user2 });

        _chatRepoMock.Verify(r => r.CreateRoomAsync(
            It.Is<ChatRoom>(cr => cr.Type == ChatRoomType.Direct && cr.Participants.Count == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateTaskGroupRoomAsync_CreatesGroupRoomWithTaskId()
    {
        // Arrange
        var taskId = Guid.NewGuid();
        var staff1 = Guid.NewGuid();
        var staff2 = Guid.NewGuid();

        _chatRepoMock
            .Setup(r => r.GetRoomByTaskIdAsync(taskId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatRoom?)null);

        _chatRepoMock
            .Setup(r => r.CreateRoomAsync(It.IsAny<ChatRoom>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatRoom room, CancellationToken _) => room);

        var participants = new List<(Guid UserId, string UserName, string Role)>
        {
            (staff1, "Carlos", "Maintenance"),
            (staff2, "Diana", "Manager")
        };

        // Act
        var result = await _chatService.CreateTaskGroupRoomAsync(taskId, "Fix AC in 401", participants);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be(ChatRoomType.Group);
        result.TaskId.Should().Be(taskId);
        result.Title.Should().Be("Task: Fix AC in 401");
        result.Participants.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetRoomMessagesAsync_WhenUserIsNotParticipant_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var memberId = Guid.NewGuid();
        var nonMemberId = Guid.NewGuid();

        var room = new ChatRoom("Test Room", ChatRoomType.Direct);
        room.AddParticipant(memberId, "Member", "Staff");

        _chatRepoMock
            .Setup(r => r.GetRoomByIdAsync(roomId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(room);

        // Act
        var act = async () => await _chatService.GetRoomMessagesAsync(roomId, nonMemberId);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*not a participant*");
    }

    [Fact]
    public async Task SendMessageAsync_WhenUserIsParticipant_PersistsMessageDispatchesSignalRAndNotifiesOtherMembers()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var senderId = Guid.NewGuid();
        var recipientId = Guid.NewGuid();

        var room = new ChatRoom("Alice & Bob", ChatRoomType.Direct);
        room.AddParticipant(senderId, "Alice", "Housekeeper");
        room.AddParticipant(recipientId, "Bob", "Manager");

        _chatRepoMock
            .Setup(r => r.GetRoomByIdAsync(roomId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(room);

        _chatRepoMock
            .Setup(r => r.AddMessageAsync(It.IsAny<ChatMessage>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatMessage msg, CancellationToken _) => msg);

        // Act
        var result = await _chatService.SendMessageAsync(
            roomId, senderId, "Alice", "Housekeeper", "Room 302 is clean and ready!");

        // Assert
        result.Should().NotBeNull();
        result.Content.Should().Be("Room 302 is clean and ready!");
        result.SenderId.Should().Be(senderId);

        // Verify broadcast to SignalR room
        _dispatcherMock.Verify(d => d.BroadcastChatMessageAsync(
            roomId,
            It.Is<ChatMessageDto>(m => m.Content == "Room 302 is clean and ready!"),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify offline fallback notification sent to recipient (NOT sender)
        _notificationServiceMock.Verify(s => s.SendNotificationAsync(
            recipientId,
            "New message from Alice",
            "Room 302 is clean and ready!",
            NotificationType.ChatMessage,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);

        _notificationServiceMock.Verify(s => s.SendNotificationAsync(
            senderId,
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<NotificationType>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendMessageAsync_WhenSenderIsNotParticipant_ThrowsUnauthorizedAccessException()
    {
        // Arrange
        var roomId = Guid.NewGuid();
        var senderId = Guid.NewGuid();

        var room = new ChatRoom("Restricted Room", ChatRoomType.Direct);
        room.AddParticipant(Guid.NewGuid(), "SomeoneElse", "Staff");

        _chatRepoMock
            .Setup(r => r.GetRoomByIdAsync(roomId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(room);

        // Act
        var act = async () => await _chatService.SendMessageAsync(
            roomId, senderId, "Intruder", "Staff", "Hello?");

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*not a participant*");

        _chatRepoMock.Verify(r => r.AddMessageAsync(It.IsAny<ChatMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
