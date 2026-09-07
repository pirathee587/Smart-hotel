using Microsoft.EntityFrameworkCore;
using SmartHotel.Notifications.Application;
using SmartHotel.Notifications.Domain;
using SmartHotel.Notifications.Infrastructure.Persistence;

namespace SmartHotel.Notifications.Infrastructure.Repositories;

public class ChatRepository : IChatRepository
{
    private readonly NotificationsDbContext _context;

    public ChatRepository(NotificationsDbContext context)
    {
        _context = context;
    }

    public async Task<ChatRoom> CreateRoomAsync(ChatRoom room, CancellationToken ct = default)
    {
        await _context.ChatRooms.AddAsync(room, ct);
        await _context.SaveChangesAsync(ct);
        return room;
    }

    public async Task<ChatRoom?> GetRoomByIdAsync(Guid roomId, CancellationToken ct = default)
    {
        return await _context.ChatRooms
            .Include(r => r.Participants)
            .Include(r => r.Messages.OrderByDescending(m => m.SentAt).Take(1))
            .FirstOrDefaultAsync(r => r.Id == roomId, ct);
    }

    public async Task<ChatRoom?> GetRoomByTaskIdAsync(Guid taskId, CancellationToken ct = default)
    {
        return await _context.ChatRooms
            .Include(r => r.Participants)
            .Include(r => r.Messages.OrderByDescending(m => m.SentAt).Take(1))
            .FirstOrDefaultAsync(r => r.TaskId == taskId, ct);
    }

    public async Task<ChatRoom?> FindDirectRoomAsync(Guid user1Id, Guid user2Id, CancellationToken ct = default)
    {
        return await _context.ChatRooms
            .Include(r => r.Participants)
            .Include(r => r.Messages.OrderByDescending(m => m.SentAt).Take(1))
            .Where(r => r.Type == ChatRoomType.Direct)
            .Where(r => r.Participants.Any(p => p.UserId == user1Id) && r.Participants.Any(p => p.UserId == user2Id))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<ChatRoom>> GetRoomsByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.ChatRooms
            .Include(r => r.Participants)
            .Include(r => r.Messages.OrderByDescending(m => m.SentAt).Take(1))
            .Where(r => r.Participants.Any(p => p.UserId == userId))
            .OrderByDescending(r => r.UpdatedAt)
            .ToListAsync(ct);
    }

    public async Task<ChatMessage> AddMessageAsync(ChatMessage message, CancellationToken ct = default)
    {
        await _context.ChatMessages.AddAsync(message, ct);

        // Update room updated at
        var room = await _context.ChatRooms.FirstOrDefaultAsync(r => r.Id == message.ChatRoomId, ct);
        if (room != null)
        {
            // Trigger update timestamp
            _context.Entry(room).Property(r => r.UpdatedAt).CurrentValue = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
        return message;
    }

    public async Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(Guid roomId, int limit = 50, CancellationToken ct = default)
    {
        return await _context.ChatMessages
            .Where(m => m.ChatRoomId == roomId)
            .OrderByDescending(m => m.SentAt)
            .Take(limit)
            .OrderBy(m => m.SentAt)
            .ToListAsync(ct);
    }

    public async Task MarkRoomMessagesAsReadAsync(Guid roomId, Guid userId, CancellationToken ct = default)
    {
        var participant = await _context.ChatParticipants
            .FirstOrDefaultAsync(p => p.ChatRoomId == roomId && p.UserId == userId, ct);

        if (participant != null)
        {
            participant.UpdateLastRead();
        }

        var unreadMessages = await _context.ChatMessages
            .Where(m => m.ChatRoomId == roomId && m.SenderId != userId && !m.IsRead)
            .ToListAsync(ct);

        foreach (var msg in unreadMessages)
        {
            msg.MarkAsRead();
        }

        await _context.SaveChangesAsync(ct);
    }
}
