// ReceiptCapture.Core/Services/ReceiptService.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ReceiptCapture.Data;
using ReceiptCapture.Data.Models;

namespace ReceiptCapture.Core.Services;

public class ReceiptService : IReceiptService
{
    private readonly ReceiptContext _context;
    private readonly IOcrService _ocrService;
    private readonly CloudinaryService _cloudinary;
    private readonly ILogger<ReceiptService> _logger;

    public ReceiptService(ReceiptContext context, IOcrService ocrService, CloudinaryService cloudinary, ILogger<ReceiptService> logger)
    {
        _context = context;
        _ocrService = ocrService;
        _cloudinary = cloudinary;
        _logger = logger;
    }

    public async Task<ReceiptResult> CreateReceiptAsync(long telegramUserId, byte[] imageBytes, string fileId, string? caption, CancellationToken ct = default)
    {
        // Get or create user
        var user = await _context.Users.FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct);
        if (user == null)
        {
            user = new User { TelegramUserId = telegramUserId, Username = telegramUserId.ToString() };
            _context.Users.Add(user);
            await _context.SaveChangesAsync(ct);
        }

        // Parallel processing
        var uploadTask = _cloudinary.UploadImageAsync(imageBytes, $"receipt_{user.UserId}_{DateTime.UtcNow.Ticks}.jpg");
        var ocrTask = Task.Run(() => _ocrService.ProcessImage(imageBytes), ct);

        await Task.WhenAll(uploadTask, ocrTask);

        var imageUrl = await uploadTask;
        var ocrResult = await ocrTask;

        var categoryId = MapCategory(ocrResult.SuggestedCategory);

        var receipt = new Receipt
        {
            UserId = user.UserId,
            CategoryId = categoryId,
            MerchantName = ocrResult.MerchantName,
            TotalAmount = ocrResult.TotalAmount ?? 0,
            TaxAmount = ocrResult.TaxAmount,
            Currency = ocrResult.Currency,
            ReceiptDate = ocrResult.Date ?? DateTime.UtcNow.Date,
            ReceiptTime = ocrResult.Time,
            RawText = ocrResult.RawText,
            ImageUrl = imageUrl,
            ImageFileId = fileId,
            UploadedAt = DateTime.UtcNow,
            ProcessedAt = DateTime.UtcNow,
            ProcessingStatus = ocrResult.Success ? "Processed" : "Failed",
            Notes = caption
        };

        _context.Receipts.Add(receipt);
        await _context.SaveChangesAsync(ct);

        // Add items
        if (ocrResult.Items?.Count > 0)
        {
            foreach (var item in ocrResult.Items)
            {
                _context.ReceiptItems.Add(new ReceiptItem
                {
                    ReceiptId = receipt.ReceiptId,
                    ItemName = item.Name,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    TotalPrice = item.TotalPrice ?? (item.UnitPrice * item.Quantity)
                });
            }
            await _context.SaveChangesAsync(ct);
        }

        _logger.LogInformation("Receipt {ReceiptId} created for user {UserId}", receipt.ReceiptId, user.UserId);

        return MapToResult(receipt, telegramUserId);
    }

    private static int? MapCategory(string? categoryName)
    {
        if (string.IsNullOrEmpty(categoryName)) return 8;

        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Food"] = 1,
            ["Food & Dining"] = 1,
            ["Restaurant"] = 1,
            ["Transport"] = 2,
            ["Transportation"] = 2,
            ["Shopping"] = 3,
            ["Entertainment"] = 4,
            ["Utilities"] = 5,
            ["Healthcare"] = 6,
            ["Education"] = 7
        };

        foreach (var kvp in map)
            if (categoryName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                return kvp.Value;

        return 8;
    }

    public async Task<ReceiptResult?> GetReceiptAsync(int receiptId, CancellationToken ct = default)
    {
        var receipt = await _context.Receipts
            .AsNoTracking()
            .Include(r => r.Category)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.ReceiptId == receiptId, ct);

        return receipt == null ? null : MapToResult(receipt, receipt.User.TelegramUserId);
    }

    public async Task<List<ReceiptResult>> GetUserReceiptsAsync(long telegramUserId, CancellationToken ct = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct);
        if (user == null) return [];

        return await _context.Receipts
            .AsNoTracking()
            .Where(r => r.UserId == user.UserId)
            .Include(r => r.Category)
            .OrderByDescending(r => r.UploadedAt)
            .Select(r => MapToResult(r, telegramUserId))
            .ToListAsync(ct);
    }

    public async Task<List<ReceiptResult>> GetReceiptsByDateRangeAsync(long telegramUserId, DateTime start, DateTime end, CancellationToken ct = default)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct);
        if (user == null) return [];

        return await _context.Receipts
            .AsNoTracking()
            .Where(r => r.UserId == user.UserId && r.ReceiptDate >= start && r.ReceiptDate <= end)
            .Include(r => r.Category)
            .OrderByDescending(r => r.ReceiptDate)
            .Select(r => MapToResult(r, telegramUserId))
            .ToListAsync(ct);
    }

    public async Task UpdateCategoryAsync(int receiptId, int categoryId, CancellationToken ct = default)
    {
        var receipt = await _context.Receipts.FindAsync([receiptId], ct);
        if (receipt != null)
        {
            receipt.CategoryId = categoryId;
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task DeleteReceiptAsync(int receiptId, CancellationToken ct = default)
    {
        var receipt = await _context.Receipts.FindAsync([receiptId], ct);
        if (receipt != null)
        {
            _context.Receipts.Remove(receipt);
            await _context.SaveChangesAsync(ct);
        }
    }

    private static ReceiptResult MapToResult(Receipt r, long telegramUserId) => new()
    {
        ReceiptId = r.ReceiptId,
        TelegramUserId = telegramUserId,
        MerchantName = r.MerchantName,
        TotalAmount = r.TotalAmount,
        ReceiptDate = r.ReceiptDate,
        CategoryName = r.Category?.Name,
        ImageUrl = r.ImageUrl,
        ProcessingStatus = r.ProcessingStatus,
        UploadedAt = r.UploadedAt,
        Currency = r.Currency
    };
}