// ReceiptCapture.Api/Program.cs
using ReceiptCapture.Core.Services;
using ReceiptCapture.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
        builder.Configuration["Claude:Model"] ?? Anthropic.SDK.Constants.AnthropicModels.Claude46Sonnet,
        sp.GetService<ILogger<ClaudeOcrService>>()));

builder.Services.AddSingleton(sp =>
    new CloudinaryService(
        builder.Configuration["Cloudinary:CloudName"]!,
        builder.Configuration["Cloudinary:ApiKey"]!,
        builder.Configuration["Cloudinary:ApiSecret"]!,
        sp.GetService<ILogger<CloudinaryService>>()));

builder.Services.AddScoped<IReceiptService, ReceiptService>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

var app = builder.Build();

// Auto-migrate
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ReceiptContext>();
    try
    {
        db.Database.Migrate();
        app.Logger.LogInformation("Database migrated");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Migration failed");
    }
}

// Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

// Health check for Railway
app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    timestamp = DateTime.UtcNow,
    version = "1.0.0"
}));

// Railway port configuration
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Urls.Clear();
app.Urls.Add($"http://0.0.0.0:{port}");

app.Logger.LogInformation("Starting on port {Port}", port);
app.Run();