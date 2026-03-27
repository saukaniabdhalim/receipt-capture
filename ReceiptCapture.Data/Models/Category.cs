// Models/Category.cs
namespace ReceiptCapture.Data.Models;

public class Category
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ColorCode { get; set; }
    public string? Icon { get; set; }
    public bool IsDefault { get; set; }
    public int? CreatedByUserId { get; set; }

    public virtual User? CreatedBy { get; set; }
    public virtual ICollection<Receipt> Receipts { get; set; } = [];
}