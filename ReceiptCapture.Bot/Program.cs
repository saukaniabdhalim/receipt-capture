// ReceiptCapture.Bot/Program.cs - Fixed UpdateHandler
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReceiptCapture.Core.Services;
using ReceiptCapture.Data;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

Console.WriteLine("=== Receipt Capture Bot Starting ===");

try
{
    var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: true)
        .AddEnvironmentVariables()
        .Build();

    // Validate config
    var botToken = configuration["Telegram:BotToken"];
    if (string.IsNullOrEmpty(botToken) || botToken == "your-telegram-bot-token")
    {
        Console.WriteLine("❌ ERROR: Telegram Bot Token not configured!");
        Console.WriteLine("Set Telegram__BotToken environment variable");
        Environment.Exit(1);
    }

    Console.WriteLine($"✓ Bot Token: {botToken.Substring(0, 10)}...");

    var connectionString = configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrEmpty(connectionString))
    {
        Console.WriteLine("❌ ERROR: Database connection string not configured!");
        Environment.Exit(1);
    }

    Console.WriteLine($"✓ Database configured");

    // Setup DI
    var services = new ServiceCollection();
    services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

    services.AddDbContext<ReceiptContext>(options =>
        options.UseNpgsql(connectionString));

    //services.AddSingleton<IOcrService>(sp =>
    //    new ClaudeOcrService(
    //        configuration["Claude:ApiKey"]!,
    //        configuration["Claude:Model"] ?? Anthropic.SDK.Constants.AnthropicModels.Claude45Haiku,
    //        sp.GetService<ILogger<ClaudeOcrService>>()));
    // 1. Register the concrete Claude implementation

    services.AddSingleton<ClaudeOcrService>(sp =>
        new ClaudeOcrService(
            configuration["Claude:ApiKey"]!,
            configuration["Claude:Model"] ?? Anthropic.SDK.Constants.AnthropicModels.Claude45Haiku,
            sp.GetRequiredService<ILogger<ClaudeOcrService>>()));

    // 2. Register the concrete Gemini implementation (the fallback)
    services.AddSingleton<GeminiOcrService>(sp =>
        new GeminiOcrService(
            configuration["Gemini:ApiKey"]!,
            "Gemini3Flash",
            sp.GetRequiredService<ILogger<GeminiOcrService>>()));
    // 3. Register IOcrService to use the Fallback wrapper
    // This will automatically inject Claude and Gemini into the FallbackOcrService
    services.AddSingleton<IOcrService, FallbackOcrService>();

    services.AddSingleton(new CloudinaryService(
        configuration["Cloudinary:CloudName"]!,
        configuration["Cloudinary:ApiKey"]!,
        configuration["Cloudinary:ApiSecret"]!,
        null));

    services.AddScoped<IReceiptService, ReceiptService>();

    var serviceProvider = services.BuildServiceProvider();

    // Test DB connection
    Console.WriteLine("Testing database connection...");
    using (var scope = serviceProvider.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<ReceiptContext>();
        var canConnect = await db.Database.CanConnectAsync();
        Console.WriteLine(canConnect ? "✓ Database connected" : "❌ Database connection failed");
    }

    // Initialize bot
    Console.WriteLine("Initializing Telegram Bot...");
    var bot = new TelegramBotClient(botToken);

    var me = await bot.GetMe();
    Console.WriteLine($"✓ Bot connected: @{me.Username} (ID: {me.Id})");

    // Start receiving
    var cts = new CancellationTokenSource();

    var receiverOptions = new ReceiverOptions
    {
        AllowedUpdates = [UpdateType.Message, UpdateType.CallbackQuery],
        DropPendingUpdates = true
    };

    var handler = new UpdateHandler(serviceProvider);

    bot.StartReceiving(
        updateHandler: handler,
        receiverOptions: receiverOptions,
        cancellationToken: cts.Token
    );

    Console.WriteLine("✓ Bot is running and listening for messages...");
    Console.WriteLine("Press Ctrl+C to stop");

    // Keep alive
    await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (Exception ex)
{
    Console.WriteLine($"❌ FATAL ERROR: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    Environment.Exit(1);
}

// FIXED: UpdateHandler with correct IUpdateHandler interface
public class UpdateHandler : IUpdateHandler
{
    private readonly IServiceProvider _services;
    private readonly ILogger<UpdateHandler> _logger;

    public UpdateHandler(IServiceProvider services)
    {
        _services = services;
        _logger = services.GetRequiredService<ILogger<UpdateHandler>>();
    }

    public async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        // Log EVERYTHING for debugging
        _logger.LogInformation("=== UPDATE RECEIVED ===");
        _logger.LogInformation("Update Type: {Type}", update.Type);
        _logger.LogInformation("Update ID: {Id}", update.Id);

        if (update.Message != null)
        {
            _logger.LogInformation("Message ID: {MessageId}", update.Message.MessageId);
            _logger.LogInformation("Chat Type: {ChatType}", update.Message.Chat.Type);
            _logger.LogInformation("Chat ID: {ChatId}", update.Message.Chat.Id);
            _logger.LogInformation("Chat Title: {Title}", update.Message.Chat.Title ?? "N/A");
            _logger.LogInformation("From User: {Username} (ID: {UserId})",
                update.Message.From?.Username ?? "N/A",
                update.Message.From?.Id);
            _logger.LogInformation("Has Photo: {HasPhoto}", update.Message.Photo != null);
            _logger.LogInformation("Text: {Text}", update.Message.Text ?? "N/A");
            _logger.LogInformation("=======================");
        }

        _logger.LogInformation("Received update type: {Type}", update.Type);

        try
        {
            if (update.Message is { } message)
            {
                _logger.LogInformation("Message from {User}: {Text}", message.From?.Username, message.Text ?? "[photo]");
                await HandleMessageAsync(bot, message, ct);
            }
            else if (update.CallbackQuery is { } callback)
            {
                _logger.LogInformation("Callback query: {Data}", callback.Data);
                await HandleCallbackAsync(bot, callback, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling update");
        }
    }

    // FIXED: Correct method signature for IUpdateHandler.HandleErrorAsync
    public Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, HandleErrorSource source, CancellationToken ct)
    {
        _logger.LogError(exception, "Error from {Source}: {Message}", source, exception.Message);

        // Add delay to prevent tight error loops
        return Task.Delay(2000, ct);
    }

    //In HandleMessageAsync method, add this at the beginning:

    private async Task HandleMessageAsync(ITelegramBotClient bot, Message message, CancellationToken ct)
    {
        _logger.LogInformation("Message from {ChatType} - User: {Username} ({UserId})",
            message.Chat.Type,
            message.From?.Username,
            message.From?.Id);

        // Log chat info for debugging
        _logger.LogInformation("Chat ID: {ChatId}, Chat Title: {ChatTitle}",
            message.Chat.Id,
            message.Chat.Title ?? "Private");

        using var scope = _services.CreateScope();
        var receiptService = scope.ServiceProvider.GetRequiredService<IReceiptService>();
        var chatId = message.Chat.Id;
        var chatType = message.Chat.Type.ToString();  // "Private", "Group", "Supergroup"

        // Handle photo (works in both private and group chats)
        if (message.Photo is { Length: > 0 } photos)
        {
            _logger.LogInformation("Processing photo in {ChatType} with {Count} sizes",
                message.Chat.Type,
                photos.Length);

            // Send processing message (mention user in groups)
            var processingText = message.Chat.Type == ChatType.Private
                ? "📸 Processing receipt..."
                : $"📸 Processing receipt from @{message.From?.Username ?? message.From?.FirstName}...";

            await bot.SendMessage(chatId, processingText, cancellationToken: ct);

            try
            {
                var photo = photos[^1]; // Get largest photo
                _logger.LogInformation("Downloading file: {FileId}", photo.FileId);

                var file = await bot.GetFile(photo.FileId, ct);

                using var stream = new MemoryStream();
                await bot.DownloadFile(file.FilePath!, stream, ct);
                var imageBytes = stream.ToArray();

                _logger.LogInformation("Downloaded {Size} bytes, processing with Claude OCR...", imageBytes.Length);

                // IMPORTANT: Use message.From.Id (the sender), not chatId (the group)
                var result = await receiptService.CreateReceiptAsync(
                    message.From!.Id,  // This is the person who sent the photo
                    telegramChatId: chatId,      // Group ID or private chat ID
                    chatType: chatType,          // "private" or "group"
                    imageBytes,
                    photo.FileId,
                    message.Caption,
                    ct);

                _logger.LogInformation("Receipt saved: ID={Id}, Amount={Amount}, User={UserId}",
                    result.ReceiptId,
                    result.TotalAmount,
                    message.From.Id);

                // Build response mentioning user in groups
                var response = message.Chat.Type == ChatType.Private
                    ? $"✅ Receipt saved!\n\n🏪 {result.MerchantName}\n💰 MYR {result.TotalAmount:N2}\n📅 {result.ReceiptDate:dd/MM/yyyy}"
                    : $"✅ Receipt saved for @{message.From?.Username ?? message.From?.FirstName}!\n\n🏪 {result.MerchantName}\n💰 MYR {result.TotalAmount:N2}\n📅 {result.ReceiptDate:dd/MM/yyyy}";

                var buttons = new InlineKeyboardMarkup([
                    [
                        InlineKeyboardButton.WithCallbackData("🍽️ Food & Dining",  $"cat_{result.ReceiptId}_1"),
                        InlineKeyboardButton.WithCallbackData("🚗 Transport",       $"cat_{result.ReceiptId}_2")
                    ],
                    [
                        InlineKeyboardButton.WithCallbackData("🛍️ Shopping",        $"cat_{result.ReceiptId}_3"),
                        InlineKeyboardButton.WithCallbackData("🎮 Entertainment",   $"cat_{result.ReceiptId}_4")
                    ],
                    [
                        InlineKeyboardButton.WithCallbackData("💡 Utilities",       $"cat_{result.ReceiptId}_5"),
                        InlineKeyboardButton.WithCallbackData("💊 Healthcare",      $"cat_{result.ReceiptId}_6")
                    ],
                    [
                        InlineKeyboardButton.WithCallbackData("🎓 Education",       $"cat_{result.ReceiptId}_7"),
                        InlineKeyboardButton.WithCallbackData("📦 Others",          $"cat_{result.ReceiptId}_8")
                    ]
                ]);

                await bot.SendMessage(
                    chatId,
                    response,
                    replyMarkup: buttons,
                    cancellationToken: ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process receipt from {User}", message.From?.Username);
                await bot.SendMessage(chatId, $"❌ Error processing receipt: {ex.Message}", cancellationToken: ct);
            }
            return;
        }

        // Handle commands (need to check for @botname in groups)
        if (message.Text is { } text)
        {
            // In groups, commands might be /start@YourBotName
            var command = text.Split(' ')[0].Split('@')[0].ToLower();

            _logger.LogInformation("Command received: {Command} in {ChatType}", command, message.Chat.Type);

            switch (command)
            {
                case "/start":
                    var welcomeMsg = message.Chat.Type == ChatType.Group || message.Chat.Type == ChatType.Supergroup
                        ? "👋 I'm ready to capture receipts! Send photos here."
                        : "👋 Send me receipt photos!";
                    await bot.SendMessage(chatId, welcomeMsg, cancellationToken: ct);
                    break;

                case "/today":
                    var today = DateTime.UtcNow.Date;
                    var todayReceipts = await receiptService.GetReceiptsByDateRangeAsync(message.From!.Id, today, today, ct);
                    var todayTotal = todayReceipts.Sum(r => r.TotalAmount);
                    await bot.SendMessage(chatId,
                        $"📅 Today ({today:dd/MM/yyyy})\n💰 Total: MYR {todayTotal:N2}\n🧾 Receipts: {todayReceipts.Count}",
                        cancellationToken: ct);
                    break;

                case "/month":
                    var start = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                    var end = start.AddMonths(1).AddDays(-1);
                    var monthReceipts = await receiptService.GetReceiptsByDateRangeAsync(message.From!.Id, start, end, ct);
                    var monthTotal = monthReceipts.Sum(r => r.TotalAmount);
                    await bot.SendMessage(chatId,
                        $"📆 This Month\n💰 Total: MYR {monthTotal:N2}\n🧾 Receipts: {monthReceipts.Count}",
                        cancellationToken: ct);
                    break;

                case "/help":
                    await bot.SendMessage(chatId,
                        "📸 Send receipt photos to track expenses\nCommands: /start, /today, /month, /help",
                        cancellationToken: ct);
                    break;
            }
        }
    }

    private async Task HandleCallbackAsync(ITelegramBotClient bot, CallbackQuery callback, CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var receiptService = scope.ServiceProvider.GetRequiredService<IReceiptService>();

        var data = callback.Data;
        if (data?.StartsWith("cat_") == true)
        {
            var parts = data.Split('_');
            var receiptId = int.Parse(parts[1]);
            var categoryId = int.Parse(parts[2]);

            await receiptService.UpdateCategoryAsync(receiptId, categoryId, ct);
            await bot.AnswerCallbackQuery(callback.Id, "Saved!", cancellationToken: ct);

            if (callback.Message is { } msg)
            {
                await bot.EditMessageText(msg.Chat.Id, msg.MessageId, msg.Text + "\n\n✅ Category saved!", cancellationToken: ct);
            }
        }
    }
}