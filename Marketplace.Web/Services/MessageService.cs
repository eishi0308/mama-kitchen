using Marketplace.Web.Data;
using Marketplace.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Web.Services;

public class MessageService : IMessageService
{
    private readonly AppDbContext _db;

    public MessageService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<ConversationSummary>> GetConversationsAsync(int userId)
    {
        var messages = await _db.Messages
            .Include(m => m.FoodDrop)
            .Include(m => m.Sender)
            .Include(m => m.Receiver)
            .Where(m => m.SenderId == userId || m.ReceiverId == userId)
            .OrderByDescending(m => m.SentAt)
            .ToListAsync();

        return messages
            .GroupBy(m => new { m.FoodDropId, OtherUserId = m.SenderId == userId ? m.ReceiverId : m.SenderId })
            .Select(g =>
            {
                var last = g.First(); // already ordered desc
                var other = last.SenderId == userId ? last.Receiver : last.Sender;
                return new ConversationSummary
                {
                    FoodDropId = g.Key.FoodDropId,
                    FoodDropTitle = last.FoodDrop?.Title ?? "(food drop removed)",
                    OtherUserId = g.Key.OtherUserId,
                    OtherUserName = other?.Name ?? "Unknown",
                    OtherUserAvatar = other?.Avatar ?? "🙂",
                    LastMessage = last.Body,
                    LastMessageAt = last.SentAt,
                };
            })
            .OrderByDescending(c => c.LastMessageAt)
            .ToList();
    }

    public async Task<List<Message>> GetThreadAsync(int foodDropId, int userId, int otherUserId) =>
        await _db.Messages
            .Include(m => m.Sender)
            .Where(m => m.FoodDropId == foodDropId &&
                        ((m.SenderId == userId && m.ReceiverId == otherUserId) ||
                         (m.SenderId == otherUserId && m.ReceiverId == userId)))
            .OrderBy(m => m.SentAt)
            .ToListAsync();

    public async Task SendAsync(int foodDropId, int senderId, int receiverId, string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return;
        _db.Messages.Add(new Message
        {
            FoodDropId = foodDropId,
            SenderId = senderId,
            ReceiverId = receiverId,
            Body = body.Trim(),
            SentAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }
}
