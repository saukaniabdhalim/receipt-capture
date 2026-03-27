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
    public async Task<ReceiptResult> CreateReceiptAsync(
    long telegramUserId,
    byte[] imageBytes,
    string fileId,
    string? caption,
    CancellationToken ct = default)
    {
        return await CreateReceiptWithHouseholdAsync(telegramUserId, null, null, imageBytes, fileId, caption, ct);
    }

    public async Task<ReceiptResult> CreateReceiptWithHouseholdAsync(
        long telegramUserId,
        long? telegramChatId,
        string? chatType,
        byte[] imageBytes,
        string fileId,
        string? caption,
        CancellationToken ct = default)
    {
        // Get or create user (with household included)
        var user = await _context.Users
            .Include(u => u.Household)
            .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct);

        if (user == null)
        {
            user = new User
            {
                TelegramUserId = telegramUserId,
                Username = telegramUserId.ToString()
            };
            _context.Users.Add(user);
            // Don't save yet - need to create household first
        }

        // Handle household for group chats
        int? householdId = null;

        if (chatType?.ToLower() == "group" || chatType?.ToLower() == "supergroup")
        {
            // Look for existing household for this group
            var household = await _context.Households
                .FirstOrDefaultAsync(h => h.GroupChatId == telegramChatId, ct);

            if (household == null)
            {
                // Create new household for this group
                household = new Household
                {
                    Name = $"Group {telegramChatId}",
                    GroupChatId = telegramChatId,
                    Description = $"Auto-created for Telegram group"
                };
                _context.Households.Add(household);
                await _context.SaveChangesAsync(ct); // Save to get HouseholdId
            }

            // Add user to household if not already member
            if (user.HouseholdId != household.HouseholdId)
            {
                user.HouseholdId = household.HouseholdId;
            }

            householdId = household.HouseholdId;
        }
        else
        {
            // Private chat - use existing or create personal household
            if (user.HouseholdId == null)
            {
                var household = new Household
                {
                    Name = user.FirstName ?? $"User {telegramUserId}",
                    Description = "Personal household"
                };
                _context.Households.Add(household);
                await _context.SaveChangesAsync(ct); // Save to get HouseholdId
                user.HouseholdId = household.HouseholdId;
            }
            householdId = user.HouseholdId;
        }

        // Save user changes (if new or household updated)
        if (user.UserId == 0 || _context.Entry(user).State == Microsoft.EntityFrameworkCore.EntityState.Modified)
        {
            await _context.SaveChangesAsync(ct);
        }

        // Parallel processing: Upload to Cloudinary + OCR with Claude
        var uploadTask = _cloudinary.UploadImageAsync(imageBytes, $"receipt_{user.UserId}_{DateTime.UtcNow.Ticks}.jpg");
        var ocrTask = Task.Run(() => _ocrService.ProcessImage(imageBytes), ct);

        await Task.WhenAll(uploadTask, ocrTask);

        var imageUrl = await uploadTask;
        var ocrResult = await ocrTask;

        var categoryId = MapCategory(ocrResult.SuggestedCategory);

        var receipt = new Receipt
        {
            UserId = user.UserId,
            HouseholdId = householdId,  // NEW: Link to household
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

        _logger.LogInformation("Receipt {ReceiptId} created for household {HouseholdId} by user {UserId}",
            receipt.ReceiptId, householdId, user.UserId);

        return MapToResult(receipt, telegramUserId);
    }
    //public async Task<ReceiptResult> CreateReceiptAsync(long telegramUserId, byte[] imageBytes, string fileId, string? caption, CancellationToken ct = default)
    //{
    //    // Get or create user
    //    var user = await _context.Users.FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct);
    //    if (user == null)
    //    {
    //        user = new User { TelegramUserId = telegramUserId, Username = telegramUserId.ToString() };
    //        _context.Users.Add(user);
    //        await _context.SaveChangesAsync(ct);
    //    }

    //    // Parallel processing
    //    var uploadTask = _cloudinary.UploadImageAsync(imageBytes, $"receipt_{user.UserId}_{DateTime.UtcNow.Ticks}.jpg");
    //    var ocrTask = Task.Run(() => _ocrService.ProcessImage(imageBytes), ct);

    //    await Task.WhenAll(uploadTask, ocrTask);

    //    var imageUrl = await uploadTask;
    //    var ocrResult = await ocrTask;

    //    var categoryId = MapCategory(ocrResult.SuggestedCategory);

    //    var receipt = new Receipt
    //    {
    //        UserId = user.UserId,
    //        CategoryId = categoryId,
    //        MerchantName = ocrResult.MerchantName,
    //        TotalAmount = ocrResult.TotalAmount ?? 0,
    //        TaxAmount = ocrResult.TaxAmount,
    //        Currency = ocrResult.Currency,
    //        ReceiptDate = ocrResult.Date ?? DateTime.UtcNow.Date,
    //        ReceiptTime = ocrResult.Time,
    //        RawText = ocrResult.RawText,
    //        ImageUrl = imageUrl,
    //        ImageFileId = fileId,
    //        UploadedAt = DateTime.UtcNow,
    //        ProcessedAt = DateTime.UtcNow,
    //        ProcessingStatus = ocrResult.Success ? "Processed" : "Failed",
    //        Notes = caption
    //    };

    //    _context.Receipts.Add(receipt);
    //    await _context.SaveChangesAsync(ct);

    //    // Add items
    //    if (ocrResult.Items?.Count > 0)
    //    {
    //        foreach (var item in ocrResult.Items)
    //        {
    //            _context.ReceiptItems.Add(new ReceiptItem
    //            {
    //                ReceiptId = receipt.ReceiptId,
    //                ItemName = item.Name,
    //                Quantity = item.Quantity,
    //                UnitPrice = item.UnitPrice,
    //                TotalPrice = item.TotalPrice ?? (item.UnitPrice * item.Quantity)
    //            });
    //        }
    //        await _context.SaveChangesAsync(ct);
    //    }

    //    _logger.LogInformation("Receipt {ReceiptId} created for user {UserId}", receipt.ReceiptId, user.UserId);

    //    return MapToResult(receipt, telegramUserId);
    //}
    // ReceiptCapture.Core/Services/ReceiptService.cs
    //public async Task<ReceiptResult> CreateReceiptAsync(
    //    long telegramUserId,
    //    long? telegramChatId,  // NEW: Group chat ID
    //    string? chatType,      // NEW: "private" or "group"
    //    byte[] imageBytes,
    //    string fileId,
    //    string? caption,
    //    CancellationToken ct = default)
    //{
    //    // Get or create user (the person who sent the receipt)
    //    var user = await _context.Users
    //        .Include(u => u.Household)
    //        .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct);

    //    if (user == null)
    //    {
    //        user = new User
    //        {
    //            TelegramUserId = telegramUserId,
    //            Username = telegramUserId.ToString()
    //        };
    //        _context.Users.Add(user);
    //    }

    //    // NEW: Handle household for group chats
    //    int householdId;

    //    if (chatType == "group" || chatType == "supergroup")
    //    {
    //        // Look for existing household for this group
    //        var household = await _context.Households
    //            .FirstOrDefaultAsync(h => h.GroupChatId == telegramChatId, ct);

    //        if (household == null)
    //        {
    //            // Create new household for this group
    //            household = new Household
    //            {
    //                Name = $"Household {telegramChatId}",
    //                GroupChatId = telegramChatId,
    //                Description = $"Auto-created for group chat"
    //            };
    //            _context.Households.Add(household);
    //            await _context.SaveChangesAsync(ct);
    //        }

    //        // Add user to household if not already member
    //        if (user.HouseholdId != household.HouseholdId)
    //        {
    //            user.HouseholdId = household.HouseholdId;
    //        }

    //        householdId = household.HouseholdId;
    //    }
    //    else
    //    {
    //        // Private chat - use personal household or create one
    //        if (user.HouseholdId == null)
    //        {
    //            var household = new Household
    //            {
    //                Name = user.FirstName ?? $"User {telegramUserId}",
    //                Description = "Personal household"
    //            };
    //            _context.Households.Add(household);
    //            await _context.SaveChangesAsync(ct);
    //            user.HouseholdId = household.HouseholdId;
    //        }
    //        householdId = user.HouseholdId.Value;
    //    }

    //    await _context.SaveChangesAsync(ct);

    //    // Process receipt with Claude OCR...
    //   // var ocrResult = await ProcessOcrAsync(imageBytes);

    //    var ocrResult = Task.Run(() => _ocrService.ProcessImage(imageBytes), ct);

    //    var receipt = new Receipt
    //    {
    //        UserId = user.UserId,
    //        HouseholdId = householdId,  // NEW: Link to household                                        
    //        CategoryId = categoryId,
    //        MerchantName = ocrResult.MerchantName,
    //        TotalAmount = ocrResult.TotalAmount ?? 0,
    //        TaxAmount = ocrResult.TaxAmount,
    //        Currency = ocrResult.Currency,
    //        ReceiptDate = ocrResult.Date ?? DateTime.UtcNow.Date,
    //        ReceiptTime = ocrResult.Time,
    //        RawText = ocrResult.RawText,
    //        ImageUrl = imageUrl,
    //        ImageFileId = fileId,
    //        UploadedAt = DateTime.UtcNow,
    //        ProcessedAt = DateTime.UtcNow,
    //        ProcessingStatus = ocrResult.Success ? "Processed" : "Failed",
    //        Notes = caption
    //    };

    //    _context.Receipts.Add(receipt);
    //    await _context.SaveChangesAsync(ct);

    //    // Add items
    //    if (ocrResult.Items?.Count > 0)
    //    {
    //        foreach (var item in ocrResult.Items)
    //        {
    //            _context.ReceiptItems.Add(new ReceiptItem
    //            {
    //                ReceiptId = receipt.ReceiptId,
    //                ItemName = item.Name,
    //                Quantity = item.Quantity,
    //                UnitPrice = item.UnitPrice,
    //                TotalPrice = item.TotalPrice ?? (item.UnitPrice * item.Quantity)
    //            });
    //        }
    //        await _context.SaveChangesAsync(ct);
    //    }

    //    _logger.LogInformation("Receipt {ReceiptId} created for user {UserId}", receipt.ReceiptId, user.UserId);

    //    return MapToResult(receipt, telegramUserId);
    //}
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

    public async Task<ReceiptResult> CreateReceiptAsync(long telegramUserId, long? telegramChatId, string? chatType, byte[] imageBytes, string fileId, string? caption, CancellationToken ct = default)
    {
        return await CreateReceiptWithHouseholdAsync(telegramUserId, telegramChatId, chatType, imageBytes, fileId, caption, ct);
    }
}