using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReceiptCapture.Data;
using ReceiptCapture.Api.ViewModels;  // or ReceiptCapture.Core.Dtos

namespace ReceiptCapture.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly ReceiptContext _context;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(ReceiptContext context, ILogger<DashboardController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // Get household dashboard by household ID
    [HttpGet("household/{householdId:int}")]
    public async Task<ActionResult<HouseholdDashboardViewModel>> GetHouseholdDashboard(int householdId)
    {
        var household = await _context.Households
            .Include(h => h.Members)
            .Include(h => h.Receipts)
            .ThenInclude(r => r.Category)
            .FirstOrDefaultAsync(h => h.HouseholdId == householdId);

        if (household == null)
        {
            return NotFound(new { error = "Household not found" });
        }

        var today = DateTime.UtcNow.Date;
        var startOfMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var model = new HouseholdDashboardViewModel
        {
            HouseholdId = householdId,
            HouseholdName = household.Name,
            Members = household.Members
                .Select(m => m.FirstName ?? m.Username ?? $"User {m.TelegramUserId}")
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList(),

            TodayTotal = household.Receipts
                .Where(r => r.ReceiptDate == today)
                .Sum(r => r.TotalAmount),
            TodayCount = household.Receipts
                .Count(r => r.ReceiptDate == today),

            MonthTotal = household.Receipts
                .Where(r => r.ReceiptDate >= startOfMonth)
                .Sum(r => r.TotalAmount),
            MonthCount = household.Receipts
                .Count(r => r.ReceiptDate >= startOfMonth),

            RecentReceipts = household.Receipts
                .OrderByDescending(r => r.UploadedAt)
                .Take(20)
                .Select(r => new ReceiptViewModel
                {
                    ReceiptId = r.ReceiptId,
                    UploadedBy = r.User.FirstName ?? r.User.Username ?? $"User {r.User.TelegramUserId}",
                    MerchantName = r.MerchantName,
                    Amount = r.TotalAmount,
                    Date = r.ReceiptDate,
                    Category = r.Category?.Name
                })
                .ToList()
        };

        return Ok(model);
    }

    // Get household by group chat ID (for Telegram groups)
    [HttpGet("group/{groupChatId:long}")]
    public async Task<ActionResult<HouseholdDashboardViewModel>> GetGroupDashboard(long groupChatId)
    {
        var household = await _context.Households
            .Include(h => h.Members)
            .Include(h => h.Receipts)
            .ThenInclude(r => r.Category)
            .FirstOrDefaultAsync(h => h.GroupChatId == groupChatId);

        if (household == null)
        {
            return NotFound(new { error = "No household found for this group. Send a receipt in the group first." });
        }

        // Reuse the same logic - you could extract to a private method
        var today = DateTime.UtcNow.Date;
        var startOfMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var model = new HouseholdDashboardViewModel
        {
            HouseholdId = household.HouseholdId,
            HouseholdName = household.Name,
            Members = household.Members
                .Select(m => m.FirstName ?? m.Username ?? $"User {m.TelegramUserId}")
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList(),

            TodayTotal = household.Receipts
                .Where(r => r.ReceiptDate == today)
                .Sum(r => r.TotalAmount),
            TodayCount = household.Receipts
                .Count(r => r.ReceiptDate == today),

            MonthTotal = household.Receipts
                .Where(r => r.ReceiptDate >= startOfMonth)
                .Sum(r => r.TotalAmount),
            MonthCount = household.Receipts
                .Count(r => r.ReceiptDate >= startOfMonth),

            RecentReceipts = household.Receipts
                .OrderByDescending(r => r.UploadedAt)
                .Take(20)
                .Select(r => new ReceiptViewModel
                {
                    ReceiptId = r.ReceiptId,
                    UploadedBy = r.User.FirstName ?? r.User.Username ?? $"User {r.User.TelegramUserId}",
                    MerchantName = r.MerchantName,
                    Amount = r.TotalAmount,
                    Date = r.ReceiptDate,
                    Category = r.Category?.Name
                })
                .ToList()
        };

        return Ok(model);
    }

    // Get user dashboard (personal receipts only)
    [HttpGet("user/{telegramUserId:long}")]
    public async Task<ActionResult<HouseholdDashboardViewModel>> GetUserDashboard(long telegramUserId)
    {
        var user = await _context.Users
            .Include(u => u.Household)
            .ThenInclude(h => h!.Receipts)
            .ThenInclude(r => r.Category)
            .FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId);

        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }

        // If user has no household, return empty dashboard
        if (user.Household == null)
        {
            return Ok(new HouseholdDashboardViewModel
            {
                HouseholdId = 0,
                HouseholdName = "No Household",
                Members = new List<string> { user.FirstName ?? user.Username ?? "You" },
                TodayTotal = 0,
                TodayCount = 0,
                MonthTotal = 0,
                MonthCount = 0,
                RecentReceipts = new List<ReceiptViewModel>()
            });
        }

        var today = DateTime.UtcNow.Date;
        var startOfMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var model = new HouseholdDashboardViewModel
        {
            HouseholdId = user.Household.HouseholdId,
            HouseholdName = user.Household.Name,
            Members = user.Household.Members
                .Select(m => m.FirstName ?? m.Username ?? $"User {m.TelegramUserId}")
                .Where(n => !string.IsNullOrEmpty(n))
                .ToList(),

            TodayTotal = user.Household.Receipts
                .Where(r => r.ReceiptDate == today)
                .Sum(r => r.TotalAmount),
            TodayCount = user.Household.Receipts
                .Count(r => r.ReceiptDate == today),

            MonthTotal = user.Household.Receipts
                .Where(r => r.ReceiptDate >= startOfMonth)
                .Sum(r => r.TotalAmount),
            MonthCount = user.Household.Receipts
                .Count(r => r.ReceiptDate >= startOfMonth),

            RecentReceipts = user.Household.Receipts
                .OrderByDescending(r => r.UploadedAt)
                .Take(20)
                .Select(r => new ReceiptViewModel
                {
                    ReceiptId = r.ReceiptId,
                    UploadedBy = r.User.FirstName ?? r.User.Username ?? $"User {r.User.TelegramUserId}",
                    MerchantName = r.MerchantName,
                    Amount = r.TotalAmount,
                    Date = r.ReceiptDate,
                    Category = r.Category?.Name
                })
                .ToList()
        };

        return Ok(model);
    }
}