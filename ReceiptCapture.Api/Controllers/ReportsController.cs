// Controllers/ReportsController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReceiptCapture.Data;

namespace ReceiptCapture.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly ReceiptContext _context;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(ReceiptContext context, ILogger<ReportsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("daily/{telegramUserId:long}")]
    public async Task<IActionResult> GetDaily(long telegramUserId, [FromQuery] DateTime? date, CancellationToken ct)
    {
        var target = DateTime.SpecifyKind(date ?? DateTime.UtcNow.Date, DateTimeKind.Utc);
        var user = await _context.Users.FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct);

        if (user == null) return NotFound(new { error = "User not found" });

        var receipts = await _context.Receipts
            .AsNoTracking()
            .Where(r => r.UserId == user.UserId && r.ReceiptDate == target)
            .Include(r => r.Category)
            .ToListAsync(ct);

        var total = receipts.Sum(r => r.TotalAmount);
        var byCategory = receipts
            .Where(r => r.Category != null)
            .GroupBy(r => r.Category!.Name)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.TotalAmount));

        return Ok(new
        {
            Date = target,
            TotalSpending = total,
            TransactionCount = receipts.Count,
            ByCategory = byCategory,
            TopCategory = byCategory.OrderByDescending(x => x.Value).FirstOrDefault().Key
        });
    }

    [HttpGet("monthly/{telegramUserId:long}")]
    public async Task<IActionResult> GetMonthly(long telegramUserId, [FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
    {
        var y = year ?? DateTime.UtcNow.Year;
        var m = month ?? DateTime.UtcNow.Month;

        var user = await _context.Users.FirstOrDefaultAsync(u => u.TelegramUserId == telegramUserId, ct);
        if (user == null) return NotFound(new { error = "User not found" });

        var start = new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1).AddDays(-1);

        var receipts = await _context.Receipts
            .AsNoTracking()
            .Where(r => r.UserId == user.UserId && r.ReceiptDate >= start && r.ReceiptDate <= end)
            .Include(r => r.Category)
            .ToListAsync(ct);

        var total = receipts.Sum(r => r.TotalAmount);
        var byCategory = receipts
            .Where(r => r.Category != null)
            .GroupBy(r => r.Category!.Name)
            .Select(g => new { Category = g.Key, Amount = g.Sum(r => r.TotalAmount), Count = g.Count() })
            .OrderByDescending(x => x.Amount)
            .ToList();

        return Ok(new
        {
            Year = y,
            Month = m,
            TotalSpending = total,
            TransactionCount = receipts.Count,
            ByCategory = byCategory,
            TopCategory = byCategory.FirstOrDefault()?.Category
        });
    }
}