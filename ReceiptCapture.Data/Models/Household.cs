// ReceiptCapture.Data/Models/Household.cs
using ReceiptCapture.Data.Models;
using System.ComponentModel.DataAnnotations;

public class Household
{
    [Key]
    public int HouseholdId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    // For group chats
    public long? GroupChatId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual ICollection<User> Members { get; set; } = new List<User>();
    public virtual ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();
}