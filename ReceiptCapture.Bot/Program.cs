// Program.cs - Telegram.Bot v20 compatible
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReceiptCapture.Core.Services;
using ReceiptCapture.Data;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

// Build configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

// Setup DI
var services = new ServiceCollection();

services.AddDbContext<ReceiptContext>(options =>
    options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")!));

services.AddSingleton<IOcrService>(sp =>
    new ClaudeOcrService(
        configuration["Claude:ApiKey"]!,
        configuration["Claude:Model"]!));

services.AddSingleton(new CloudinaryService(
    configuration["Cloudinary:CloudName"]!,
    configuration["Cloudinary:ApiKey"]!,
    configuration["Cloudinary:ApiSecret"]!));

services.AddScoped<IReceiptService, ReceiptService>();

var serviceProvider = services.BuildServiceProvider();

// Initialize bot
var botToken = configuration["Telegram:BotToken"]!;
var bot = new TelegramBotClient(botToken);

var me = await bot.GetMe();  // CHANGED: GetMeAsync -> GetMe
Console.WriteLine($"Bot started: @{me.Username}");

// Start receiving
var cts = new CancellationTokenSource();
var receiverOptions = new ReceiverOptions
{
    AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery]
};

bot.StartReceiving(
    updateHandler: async (bot, update, ct) => await HandleUpdateAsync(serviceProvider, bot, update, ct),
    errorHandler: (bot, ex, ct) =>
    {
        Console.WriteLine($"Error: {ex.Message}");
        return Task.CompletedTask;
    },
    receiverOptions: receiverOptions,
    cancellationToken: cts.Token
);

Console.WriteLine("Press any key to exit...");
Console.ReadKey();
cts.Cancel();

// Handler methods
async Task HandleUpdateAsync(IServiceProvider services, ITelegramBotClient bot, Update update, CancellationToken ct)
{
    try
    {
        if (update.Message is { } message)
            await HandleMessageAsync(services, bot, message, ct);
        else if (update.CallbackQuery is { } callback)
            await HandleCallbackAsync(services, bot, callback, ct);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}

async Task HandleMessageAsync(IServiceProvider services, ITelegramBotClient bot, Message message, CancellationToken ct)
{
    using var scope = services.CreateScope();
    var receiptService = scope.ServiceProvider.GetRequiredService<IReceiptService>();
    var chatId = message.Chat.Id;

    // Handle photo
    if (message.Photo is { Length: > 0 } photos)
    {
        await bot.SendMessage(  // CHANGED: SendTextMessageAsync -> SendMessage
            chatId: chatId,
            text: "📸 Processing receipt with AI...",
            cancellationToken: ct);

        var photo = photos[^1];
        var file = await bot.GetFile(photo.FileId, ct);  // CHANGED: GetFileAsync -> GetFile

        using var stream = new MemoryStream();
        await bot.DownloadFile(file.FilePath!, stream, ct);  // CHANGED: DownloadFileAsync -> DownloadFile
        var imageBytes = stream.ToArray();

        var result = await receiptService.CreateReceiptAsync(message.From!.Id, imageBytes, photo.FileId, message.Caption, ct);

        var buttons = new InlineKeyboardMarkup([
            [InlineKeyboardButton.WithCallbackData("🍽️ Food", $"cat_{result.ReceiptId}_1"),
             InlineKeyboardButton.WithCallbackData("🚗 Transport", $"cat_{result.ReceiptId}_2")],
            [InlineKeyboardButton.WithCallbackData("🛍️ Shopping", $"cat_{result.ReceiptId}_3"),
             InlineKeyboardButton.WithCallbackData("📦 Others", $"cat_{result.ReceiptId}_8")]
        ]);

        await bot.SendMessage(  // CHANGED: SendTextMessageAsync -> SendMessage
            chatId: chatId,
            text: $"✅ Saved!\n🏪 {result.MerchantName}\n💰 MYR {result.TotalAmount:N2}",
            replyMarkup: buttons,
            cancellationToken: ct);
        return;
    }

    // Handle commands
    if (message.Text?.StartsWith("/start") == true)
    {
        await bot.SendMessage(chatId, "👋 Send me receipt photos!", cancellationToken: ct);  // CHANGED
    }
    else if (message.Text?.StartsWith("/today") == true)
    {
        var today = DateTime.UtcNow.Date;
        var receipts = await receiptService.GetReceiptsByDateRangeAsync(message.From!.Id, today, today, ct);
        var total = receipts.Sum(r => r.TotalAmount);
        await bot.SendMessage(chatId, $"📅 Today: MYR {total:N2} ({receipts.Count} receipts)", cancellationToken: ct);
    }
    else if (message.Text?.StartsWith("/month") == true)
    {
        var start = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1).AddDays(-1);
        var receipts = await receiptService.GetReceiptsByDateRangeAsync(message.From!.Id, start, end, ct);
        var total = receipts.Sum(r => r.TotalAmount);
        await bot.SendMessage(chatId, $"📆 This month: MYR {total:N2} ({receipts.Count} receipts)", cancellationToken: ct);
    }
}

async Task HandleCallbackAsync(IServiceProvider services, ITelegramBotClient bot, CallbackQuery callback, CancellationToken ct)
{
    using var scope = services.CreateScope();
    var receiptService = scope.ServiceProvider.GetRequiredService<IReceiptService>();

    var data = callback.Data;
    if (data?.StartsWith("cat_") == true)
    {
        var parts = data.Split('_');
        var receiptId = int.Parse(parts[1]);
        var categoryId = int.Parse(parts[2]);

        await receiptService.UpdateCategoryAsync(receiptId, categoryId, ct);

        // CHANGED: AnswerCallbackQueryAsync -> AnswerCallbackQuery
        await bot.AnswerCallbackQuery(callbackQueryId: callback.Id, text: "Category saved!", cancellationToken: ct);

        // Optional: Edit message to remove buttons
        if (callback.Message is { } msg)
        {
            await bot.EditMessageText(  // CHANGED: EditMessageTextAsync -> EditMessageText
                chatId: msg.Chat.Id,
                messageId: msg.MessageId,
                text: msg.Text + "\n\n✅ Category updated!",
                cancellationToken: ct);
        }
    }
}