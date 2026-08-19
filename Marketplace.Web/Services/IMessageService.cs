using Marketplace.Web.Models;

namespace Marketplace.Web.Services;

public class ConversationSummary
{
    public int FoodDropId { get; set; }
    public string FoodDropTitle { get; set; } = "";
    public int OtherUserId { get; set; }
    public string OtherUserName { get; set; } = "";
    public string OtherUserAvatar { get; set; } = "";
    public string LastMessage { get; set; } = "";
    public DateTime LastMessageAt { get; set; }
}

public interface IMessageService
{
    Task<List<ConversationSummary>> GetConversationsAsync(int userId);
    Task<List<Message>> GetThreadAsync(int foodDropId, int userId, int otherUserId);
    Task SendAsync(int foodDropId, int senderId, int receiverId, string body);
}
