// ReceiptCapture.Core/Services/FallbackOcrService.cs
using Microsoft.Extensions.Logging;

namespace ReceiptCapture.Core.Services;

public class FallbackOcrService : IOcrService
{
    private readonly IOcrService _primary;
    private readonly IOcrService _secondary;
    private readonly ILogger<FallbackOcrService>? _logger;

    public FallbackOcrService(ClaudeOcrService primary, GeminiOcrService secondary, ILogger<FallbackOcrService>? logger = null)
    {
        _primary = primary;
        _secondary = secondary;
        _logger = logger;
    }

    public OcrResult ProcessImage(byte[] imageBytes)
    {
        var result = _primary.ProcessImage(imageBytes);

        if (!result.Success)
        {
            _logger?.LogWarning("Primary OCR failed or hit limit. Switching to Fallback service...");
            return _secondary.ProcessImage(imageBytes);
        }

        return result;
    }
}