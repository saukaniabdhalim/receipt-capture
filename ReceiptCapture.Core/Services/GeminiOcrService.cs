// ReceiptCapture.Core/Services/GeminiOcrService.cs
using Microsoft.Extensions.Logging;
using Mscc.GenerativeAI;
using Mscc.GenerativeAI.Types;
using System.Text.Json;

namespace ReceiptCapture.Core.Services;

public class GeminiOcrService : IOcrService
{
    private readonly GoogleAI _googleAI;
    private readonly GenerativeModel _model;
    private readonly ILogger<GeminiOcrService>? _logger;

    public GeminiOcrService(string apiKey, string modelName = Model.Gemini25Flash, ILogger<GeminiOcrService>? logger = null)
    {
        //_googleAI = new GoogleAI(apiKey);
        //_model = _googleAI.GenerativeModel(modelName);
        //_logger = logger;
        _googleAI = new GoogleAI(apiKey);
        // Ensure the model name starts with "models/"
        if (!modelName.StartsWith("models/"))
        {
            modelName = $"models/{modelName}";
        }

        _model = _googleAI.GenerativeModel(modelName);
        _logger = logger;
    }

    public OcrResult ProcessImage(byte[] imageBytes)
    {
        return ProcessImageAsync(imageBytes).GetAwaiter().GetResult();
    }
    //public async Task<IEnumerable<string>> ListAvailableModelsAsync()
    //{
    //    var models = await _googleAI.GetModel() .ListModels();
    //    return models
    //        .Where(m => m.SupportedGenerationMethods?.Contains("generateContent") == true)
    //        .Select(m => m.Name);
    //}
    public async Task<OcrResult> ProcessImageAsync(byte[] imageBytes)
    {
        try
        {
            _logger?.LogInformation("Attempting Gemini OCR (Free Tier)");

            var prompt = @"Extract all information from this receipt image and return ONLY a JSON object with this exact structure:
{
    ""merchantName"": ""string"",
    ""totalAmount"": number,
    ""taxAmount"": number or null,
    ""currency"": ""MYR"" or detected currency,
    ""date"": ""YYYY-MM-DD"" or null,
    ""time"": ""HH:MM"" or null,
    ""items"": [{""name"": ""string"", ""quantity"": number, ""unitPrice"": number, ""totalPrice"": number}],
    ""paymentMethod"": ""string or null"",
    ""receiptNumber"": ""string or null"",
    ""category"": ""Food, Transport, Shopping, Entertainment, Utilities, Healthcare, Education, or Others""
}";

            // Using the Mscc library to send a multimodal request
            var request = new GenerateContentRequest(prompt);
            request.Contents[0].Parts.Add(new InlineData { MimeType = "image/jpeg", Data = Convert.ToBase64String(imageBytes) });

            var response = await _model.GenerateContent(request);
            var jsonText = response.Text;

            if (string.IsNullOrEmpty(jsonText))
                return new OcrResult { Success = false, ErrorMessage = "Gemini returned an empty response." };

            // Sanitize response (removes markdown backticks if Gemini adds them)
            var sanitizedJson = jsonText.Replace("```json", "").Replace("```", "").Trim();

            var result = JsonSerializer.Deserialize<ClaudeReceiptResult>(sanitizedJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return new OcrResult
            {
                Success = true,
                RawText = sanitizedJson,
                MerchantName = result?.MerchantName,
                TotalAmount = result?.TotalAmount,
                TaxAmount = result?.TaxAmount,
                Currency = result?.Currency ?? "MYR",
                Date = ParseDate(result?.Date),
                Time = ParseTime(result?.Time),
                Items = result?.Items?.Select(i => new ReceiptItemData
                {
                    Name = i.Name,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.TotalPrice
                }).ToList(),
                PaymentMethod = result?.PaymentMethod,
                ReceiptNumber = result?.ReceiptNumber,
                SuggestedCategory = result?.Category
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Gemini OCR Fallback encountered an error");
            return new OcrResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private static DateTime? ParseDate(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr)) return null;
        return DateTime.TryParse(dateStr, out var date) ? DateTime.SpecifyKind(date, DateTimeKind.Utc) : null;
    }

    private static TimeSpan? ParseTime(string? timeStr)
    {
        if (string.IsNullOrEmpty(timeStr)) return null;
        return TimeSpan.TryParse(timeStr, out var time) ? time : null;
    }
}