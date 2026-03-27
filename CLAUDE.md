# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Receipt Capture System — a .NET 8 solution that captures receipt images via Telegram bot, processes them with Claude AI vision (OCR), stores results in PostgreSQL (via Supabase), and exposes the data through a REST API and MVC web dashboard.

## Solution Structure

Five projects with a layered dependency chain:

```
ReceiptCapture.Data       ← EF Core models + DbContext (no internal deps)
        ↑
ReceiptCapture.Core       ← Business logic services (OCR, image upload, receipts)
        ↑
ReceiptCapture.Api        ← ASP.NET Core REST API + Swagger
ReceiptCapture.Bot        ← Telegram bot (Console app)
ReceiptCapture.Web        ← ASP.NET Core MVC dashboard
```

## Build & Run Commands

```bash
# Build entire solution
dotnet build ReceiptCaptureSystem.sln

# Run individual services
dotnet run --project ReceiptCapture.Api
dotnet run --project ReceiptCapture.Bot
dotnet run --project ReceiptCapture.Web

# Database migrations
dotnet ef migrations add <MigrationName> -p ReceiptCapture.Data -s ReceiptCapture.Api
dotnet ef database update -p ReceiptCapture.Data -s ReceiptCapture.Api
```

No test projects exist yet.

## Configuration

Each runnable project requires an `appsettings.json` (gitignored). Templates exist as `appsettings.template.json`. Required keys:

- **API / Core**: `ConnectionStrings:DefaultConnection` (PostgreSQL), `Claude:ApiKey`, `Claude:Model`, `Cloudinary:CloudName/ApiKey/ApiSecret`
- **Bot**: `Telegram:BotToken`, plus the same DB/Claude/Cloudinary keys
- **Web**: `ConnectionStrings:DefaultConnection`

The API auto-runs EF migrations on startup (`db.Database.Migrate()`).

## Architecture & Key Flows

### Receipt Capture (Bot & API)
1. User sends photo → Bot/API receives image bytes
2. **Parallel**: upload to Cloudinary + send to Claude AI for OCR
3. Claude returns structured JSON (merchant, total, items, category suggestion)
4. Receipt + items persisted to PostgreSQL; user prompted to confirm category via Telegram inline buttons

### Claude OCR (`ReceiptCapture.Core/Services/ClaudeOcrService.cs`)
- Uses `claude-3-5-sonnet-20241022` via **Anthropic.SDK**
- Temperature = 0, MaxTokens = 4096
- Returns a `ReceiptOcrResult` with merchant, total, tax, currency, items, date, payment method, and category

### Data Model
Four tables: `Users` (Telegram identity), `Categories` (8 seeded types), `Receipts`, `ReceiptItems`. See `ReceiptCapture.Data/Models/`.

### API Endpoints
- `GET /health` — health check (used by Railway)
- `GET /api/receipts/user/{telegramUserId}/today` — today's summary
- `GET /api/receipts/user/{telegramUserId}/month` — monthly summary
- `GET /api/reports/daily/{telegramUserId}?date=YYYY-MM-DD`
- `GET /api/reports/monthly/{telegramUserId}?year=YYYY&month=M`

## Deployment

- **Railway**: configured via `railway.toml`, Docker-based, starts `dotnet ReceiptCapture.Api.dll`, health check at `/health`
- **Dockerfile** is in `ReceiptCapture.Api/`; multi-stage build targeting `mcr.microsoft.com/dotnet/aspnet:8.0`, exposes port 8080
