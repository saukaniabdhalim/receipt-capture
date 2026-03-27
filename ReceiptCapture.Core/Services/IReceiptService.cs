// ReceiptCapture.Core/Services/IReceiptService.cs
namespace ReceiptCapture.Core.Services;

public interface IReceiptService
{
    Task<ReceiptResult> CreateReceiptAsync(long telegramUserId, long? telegramChatId, string? chatType, byte[] imageBytes, string fileId, string? caption, CancellationToken ct = default);
    Task<ReceiptResult?> GetReceiptAsync(int receiptId, CancellationToken ct = default);
    Task<List<ReceiptResult>> GetUserReceiptsAsync(long telegramUserId, CancellationToken ct = default);
    Task<List<ReceiptResult>> GetReceiptsByDateRangeAsync(long telegramUserId, DateTime start, DateTime end, CancellationToken ct = default);
    Task UpdateCategoryAsync(int receiptId, int categoryId, CancellationToken ct = default);
    Task DeleteReceiptAsync(int receiptId, CancellationToken ct = default);
}

public class ReceiptResult
{
    public int ReceiptId { get; set; }
    public long TelegramUserId { get; set; }
    public string? MerchantName { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime? ReceiptDate { get; set; }
    public string? CategoryName { get; set; }
    public string? ImageUrl { get; set; }
    public string ProcessingStatus { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
    public string? Currency { get; set; }
}