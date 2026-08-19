namespace Marketplace.Web.Models;

public class Message
{
    public int Id { get; set; }
    public int FoodDropId { get; set; }
    public FoodDrop? FoodDrop { get; set; }

    public int SenderId { get; set; }
    public User? Sender { get; set; }

    public int ReceiverId { get; set; }
    public User? Receiver { get; set; }

    public string Body { get; set; } = "";
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}
