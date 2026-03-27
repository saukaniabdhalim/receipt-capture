// ReceiptCapture.Core/Services/IOcrService.cs
namespace ReceiptCapture.Core.Services;

public interface IOcrService
{
    OcrResult ProcessImage(byte[] imageBytes);
}

public class OcrResult
{
    public bool Success { get; set; }
    public string? RawText { get; set; }
    public string? MerchantName { get; set; }
    public decimal? TotalAmount { get; set; }
    public decimal? TaxAmount { get; set; }
    public string Currency { get; set; } = "MYR";
    public DateTime? Date { get; set; }
    public TimeSpan? Time { get; set; }
    public List<ReceiptItemData>? Items { get; set; }
    public string? PaymentMethod { get; set; }
    public string? ReceiptNumber { get; set; }
    public string? SuggestedCategory { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ReceiptItemData
{
    public string? Name { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? TotalPrice { get; set; }
}