// Controllers/DashboardController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReceiptCapture.Data;

namespace ReceiptCapture.Web.Controllers;

public class DashboardController : Controller
{
    private readonly ReceiptContext _context;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(ReceiptContext context, ILogger<DashboardController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IActionResult> Index(long? userId)
    {
        if (!userId.HasValue)
        {
            var firstUser = await _context.Users.FirstOrDefaultAsync();
            if (firstUser != null)
                return RedirectToAction("Index", new { userId = firstUser.TelegramUserId });
            return View("NoUser");
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.TelegramUserId == userId.Value);
        if (user == null) return NotFound();

        var today = DateTime.UtcNow.Date;
        var startOfMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var model = new DashboardViewModel
        {
            UserId = userId.Value,
            UserName = $"{user.FirstName} {user.LastName}".Trim(),

            TodayTotal = await _context.Receipts
                .AsNoTracking()
                .Where(r => r.UserId == user.UserId && r.ReceiptDate == today)
                .SumAsync(r => (decimal?)r.TotalAmount) ?? 0,
            TodayCount = await _context.Receipts
                .CountAsync(r => r.UserId == user.UserId && r.ReceiptDate == today),

            MonthTotal = await _context.Receipts
                .AsNoTracking()
                .Where(r => r.UserId == user.UserId && r.ReceiptDate >= startOfMonth)
                .SumAsync(r => (decimal?)r.TotalAmount) ?? 0,
            MonthCount = await _context.Receipts
                .CountAsync(r => r.UserId == user.UserId && r.ReceiptDate >= startOfMonth),

            RecentReceipts = await _context.Receipts
                .AsNoTracking()
                .Where(r => r.UserId == user.UserId)
                .Include(r => r.Category)
                .OrderByDescending(r => r.UploadedAt)
                .Take(10)
                .ToListAsync(),

            CategoryBreakdown = await _context.Receipts
                .AsNoTracking()
                .Where(r => r.UserId == user.UserId && r.ReceiptDate >= startOfMonth && r.CategoryId != null)
                .GroupBy(r => r.Category!.Name)
                .Select(g => new CategoryStat
                {
                    Name = g.Key,
                    Amount = g.Sum(r => r.TotalAmount),
                    Count = g.Count(),
                    Color = g.First().Category!.ColorCode ?? "#999"
                })
                .OrderByDescending(x => x.Amount)
                .ToListAsync()
        };

        return View(model);
    }

    public async Task<IActionResult> AllReceipts(long userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.TelegramUserId == userId);
        if (user == null) return NotFound();

        var receipts = await _context.Receipts
            .AsNoTracking()
            .Where(r => r.UserId == user.UserId)
            .Include(r => r.Category)
            .OrderByDescending(r => r.UploadedAt)
            .ToListAsync();

        ViewBag.UserId = userId;
        ViewBag.UserName = $"{user.FirstName} {user.LastName}".Trim();
        return View(receipts);
    }

    public async Task<IActionResult> MonthlyReport(long userId, int? year, int? month)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.TelegramUserId == userId);
        if (user == null) return NotFound();

        var now = DateTime.UtcNow;
        year ??= now.Year;
        month ??= now.Month;

        var start = new DateTime(year.Value, month.Value, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);

        var receipts = await _context.Receipts
            .AsNoTracking()
            .Where(r => r.UserId == user.UserId && r.ReceiptDate >= start && r.ReceiptDate < end)
            .Include(r => r.Category)
            .OrderByDescending(r => r.ReceiptDate)
            .ToListAsync();

        var byCategory = receipts
            .GroupBy(r => r.Category?.Name ?? "Uncategorised")
            .Select(g => new CategoryStat
            {
                Name = g.Key,
                Amount = g.Sum(r => r.TotalAmount),
                Count = g.Count(),
                Color = g.First().Category?.ColorCode ?? "#999"
            })
            .OrderByDescending(x => x.Amount)
            .ToList();

        ViewBag.UserId = userId;
        ViewBag.UserName = $"{user.FirstName} {user.LastName}".Trim();
        ViewBag.Year = year.Value;
        ViewBag.Month = month.Value;
        ViewBag.MonthName = start.ToString("MMMM yyyy");
        ViewBag.Total = receipts.Sum(r => r.TotalAmount);
        ViewBag.CategoryBreakdown = byCategory;
        return View(receipts);
    }

    public async Task<IActionResult> DailyReport(long userId, string? date)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.TelegramUserId == userId);
        if (user == null) return NotFound();

        var day = date != null && DateTime.TryParse(date, out var parsed)
            ? parsed.Date
            : DateTime.UtcNow.Date;
        var dayEnd = day.AddDays(1);

        var receipts = await _context.Receipts
            .AsNoTracking()
            .Where(r => r.UserId == user.UserId && r.ReceiptDate >= day && r.ReceiptDate < dayEnd)
            .Include(r => r.Category)
            .OrderByDescending(r => r.ReceiptDate)
            .ToListAsync();

        ViewBag.UserId = userId;
        ViewBag.UserName = $"{user.FirstName} {user.LastName}".Trim();
        ViewBag.Date = day;
        ViewBag.Total = receipts.Sum(r => r.TotalAmount);
        return View(receipts);
    }

    public class DashboardViewModel
    {
        public long UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public decimal TodayTotal { get; set; }
        public int TodayCount { get; set; }
        public decimal MonthTotal { get; set; }
        public int MonthCount { get; set; }
        public List<ReceiptCapture.Data.Models.Receipt> RecentReceipts { get; set; } = [];
        public List<CategoryStat> CategoryBreakdown { get; set; } = [];
    }

    public class CategoryStat
    {
        public string Name { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int Count { get; set; }
        public string Color { get; set; } = string.Empty;
    }
}