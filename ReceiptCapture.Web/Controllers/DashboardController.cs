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