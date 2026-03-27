// ReceiptCapture.Api/Program.cs
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.EntityFrameworkCore;
using ReceiptCapture.Core.Services;
using ReceiptCapture.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Receipt Capture API", Version = "v1" });
});

// Database
builder.Services.AddDbContext<ReceiptContext>((serviceProvider, options) =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(3);
    });
});

// Application Services
builder.Services.AddSingleton<IOcrService>(sp =>
    new ClaudeOcrService(
        builder.Configuration["Claude:ApiKey"]!,
        builder.Configuration["Claude:Model"] ?? Anthropic.SDK.Constants.AnthropicModels.Claude45Sonnet,
        sp.GetService<ILogger<ClaudeOcrService>>()));

builder.Services.AddSingleton(sp =>
    new CloudinaryService(
        builder.Configuration["Cloudinary:CloudName"]!,
        builder.Configuration["Cloudinary:ApiKey"]!,
        builder.Configuration["Cloudinary:ApiSecret"]!,
        sp.GetService<ILogger<CloudinaryService>>()));

builder.Services.AddScoped<IReceiptService, ReceiptService>();

// CORS for dashboard
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Auto-migrate database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ReceiptContext>();
    try
    {
        db.Database.Migrate();
        app.Logger.LogInformation("Database migrated successfully");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Database migration failed");
    }
}

// Middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "Healthy", timestamp = DateTime.UtcNow, version = "1.0.0" }));

// Minimal API endpoints for common operations
app.MapGet("/api/receipts/user/{telegramUserId:long}/today", async (long telegramUserId, IReceiptService service) =>
{
    var today = DateTime.UtcNow.Date;
    var receipts = await service.GetReceiptsByDateRangeAsync(telegramUserId, today, today);
    return Results.Ok(new { date = today, count = receipts.Count, total = receipts.Sum(r => r.TotalAmount), receipts });
});

app.MapGet("/api/receipts/user/{telegramUserId:long}/month", async (long telegramUserId, IReceiptService service) =>
{
    var start = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    var end = start.AddMonths(1).AddDays(-1);
    var receipts = await service.GetReceiptsByDateRangeAsync(telegramUserId, start, end);
    return Results.Ok(new { year = start.Year, month = start.Month, count = receipts.Count, total = receipts.Sum(r => r.TotalAmount), receipts });
});

app.Run();