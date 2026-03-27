// Models/Receipt.cs
namespace ReceiptCapture.Data.Models;

public class Receipt
{
    public int ReceiptId { get; set; }
    public int UserId { get; set; }
    public int? HouseholdId { get; set; }  // NEW: Which household it belongs to
    public int? CategoryId { get; set; }

    public string? MerchantName { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal? TaxAmount { get; set; }
    public string Currency { get; set; } = "MYR";
    public DateTime? ReceiptDate { get; set; }
    public TimeSpan? ReceiptTime { get; set; }

    public string? RawText { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageFileId { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public string ProcessingStatus { get; set; } = "Pending";

    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? Notes { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual Household? Household { get; set; }  // NEW
    public virtual Category? Category { get; set; }
    public virtual ICollection<ReceiptItem> Items { get; set; } = [];
}