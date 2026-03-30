// ReceiptCapture.Core/Services/ClaudeOcrService.cs
using Anthropic.SDK;
using Anthropic.SDK.Constants;
using Anthropic.SDK.Messaging;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReceiptCapture.Core.Services;

public class ClaudeOcrService : IOcrService
{
    private readonly AnthropicClient _client;
    private readonly string _model;
    private readonly ILogger<ClaudeOcrService>? _logger;

    public ClaudeOcrService(string apiKey, string model = AnthropicModels.Claude46Sonnet, ILogger<ClaudeOcrService>? logger = null)
    {
        _client = new AnthropicClient(apiKey);
        _model = model;
        _logger = logger;
    }

    public OcrResult ProcessImage(byte[] imageBytes)
    {
        return ProcessImageAsync(imageBytes).GetAwaiter().GetResult();
    }

    public async Task<OcrResult> ProcessImageAsync(byte[] imageBytes)
    {
        try
        {
            var base64Image = Convert.ToBase64String(imageBytes);

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
}

Rules:
- Extract the FINAL total amount after tax
- Use ISO date format (YYYY-MM-DD)
- If unclear, use null
- Return ONLY valid JSON, no markdown formatting";

            var message = new Message
            {
                Role = RoleType.User,
                Content = new List<ContentBase>
                {
                    new ImageContent { Source = new ImageSource { MediaType = "image/jpeg", Data = base64Image } },
                    new TextContent { Text = prompt }
                }
            };

            var parameters = new MessageParameters
            {
                Model = _model,
                MaxTokens = 4096,
                Messages = [message],
                Temperature = 0.0m,
                System = new List<SystemMessage>
                {
                    new SystemMessage("You are a precise receipt data extraction assistant. Always respond with valid JSON only.")
                }
            };

            _logger?.LogInformation("Sending image to Claude API for OCR");
            var response = await _client.Messages.GetClaudeMessageAsync(parameters);

            var textContent = response.Content.OfType<TextContent>().FirstOrDefault();
            Console.WriteLine("=== RAW CLAUDE RESPONSE ===");
            Console.WriteLine(textContent?.Text ?? "NULL RESPONSE");
            Console.WriteLine("===========================");
            if (textContent == null)
            {
                return new OcrResult { Success = false, ErrorMessage = "No text response from Claude API" };
            }

            var jsonText = textContent.Text
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();

            _logger?.LogDebug("Claude response: {Response}", jsonText);

            var result = JsonSerializer.Deserialize<ClaudeReceiptResult>(jsonText,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return new OcrResult
            {
                Success = true,
                RawText = jsonText,
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
            _logger?.LogError(ex, "Claude OCR failed");
            return new OcrResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private static DateTime? ParseDate(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr)) return null;
        if (DateTime.TryParse(dateStr, out var date)) return DateTime.SpecifyKind(date, DateTimeKind.Utc);
        return null;
    }

    private static TimeSpan? ParseTime(string? timeStr)
    {
        if (string.IsNullOrEmpty(timeStr)) return null;
        if (TimeSpan.TryParse(timeStr, out var time)) return time;
        return null;
    }
}

public class ClaudeReceiptResult
{
    [JsonPropertyName("merchantName")] public string? MerchantName { get; set; }
    [JsonPropertyName("totalAmount")] public decimal? TotalAmount { get; set; }
    [JsonPropertyName("taxAmount")] public decimal? TaxAmount { get; set; }
    [JsonPropertyName("currency")] public string? Currency { get; set; }
    [JsonPropertyName("date")] public string? Date { get; set; }
    [JsonPropertyName("time")] public string? Time { get; set; }
    [JsonPropertyName("items")] public List<ClaudeItem>? Items { get; set; }
    [JsonPropertyName("paymentMethod")] public string? PaymentMethod { get; set; }
    [JsonPropertyName("receiptNumber")] public string? ReceiptNumber { get; set; }
    [JsonPropertyName("category")] public string? Category { get; set; }
}

public class ClaudeItem
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("quantity")] public decimal Quantity { get; set; }
    [JsonPropertyName("unitPrice")] public decimal UnitPrice { get; set; }
    [JsonPropertyName("totalPrice")] public decimal? TotalPrice { get; set; }
}