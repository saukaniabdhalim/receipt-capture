using Microsoft.AspNetCore.Mvc;
using ReceiptCapture.Core.Services;

namespace ReceiptCapture.Api.Controllers;

[ApiController]
[Route("api/[controller]")]  // This creates /api/receipts
public class ReceiptsController : ControllerBase
{
    private readonly IReceiptService _service;
    private readonly ILogger<ReceiptsController> _logger;

    public ReceiptsController(IReceiptService service, ILogger<ReceiptsController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("user/{telegramUserId:long}")]  // Full path: /api/receipts/user/{id}
    public async Task<IActionResult> GetUserReceipts(long telegramUserId, CancellationToken ct)
    {
        _logger.LogInformation("Getting receipts for user {UserId}", telegramUserId);
        var receipts = await _service.GetUserReceiptsAsync(telegramUserId, ct);
        return Ok(receipts);
    }

    [HttpGet("user/{telegramUserId:long}/range")]
    public async Task<IActionResult> GetByDateRange(long telegramUserId, [FromQuery] DateTime start, [FromQuery] DateTime end, CancellationToken ct)
    {
        var receipts = await _service.GetReceiptsByDateRangeAsync(telegramUserId, start, end, ct);
        return Ok(receipts);
    }

    [HttpPost("{receiptId:int}/category/{categoryId:int}")]
    public async Task<IActionResult> UpdateCategory(int receiptId, int categoryId, CancellationToken ct)
    {
        await _service.UpdateCategoryAsync(receiptId, categoryId, ct);
        return Ok();
    }

    [HttpDelete("{receiptId:int}")]
    public async Task<IActionResult> Delete(int receiptId, CancellationToken ct)
    {
        await _service.DeleteReceiptAsync(receiptId, ct);
        return Ok();
    }
}