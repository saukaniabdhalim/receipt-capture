// ReceiptCapture.Data/Models/ReceiptItem.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ReceiptCapture.Data.Models;

public class ReceiptItem
{
    [Key]  // ADD THIS
    public int ItemId { get; set; }

    public int ReceiptId { get; set; }
    public string? ItemName { get; set; }
    public decimal Quantity { get; set; } = 1;
    public decimal? UnitPrice { get; set; }
    public decimal? TotalPrice { get; set; }

    public virtual Receipt Receipt { get; set; } = null!;
}