# Receipt Capture System

A .NET 8 application that captures receipt images via Telegram, extracts data using Claude AI vision, and provides spending insights through a REST API and web dashboard.

## Features

- **Telegram Bot** — send a receipt photo and get instant extraction of merchant, total, items, and category
- **Claude AI OCR** — structured data extraction using Claude 3.5 Sonnet vision
- **Cloud image storage** — receipts stored on Cloudinary
- **PostgreSQL database** — via Supabase
- **REST API** — query receipts and spending reports by day or month
- **Web dashboard** — visualize daily/monthly spending and category breakdowns

## Architecture

```
ReceiptCapture.Data       — EF Core models + PostgreSQL (Supabase)
        ↑
ReceiptCapture.Core       — OCR service (Claude AI), image upload (Cloudinary), receipt processing
        ↑
ReceiptCapture.Api        — ASP.NET Core REST API
ReceiptCapture.Bot        — Telegram bot (Console app)
ReceiptCapture.Web        — ASP.NET Core MVC dashboard
```

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- PostgreSQL database (Supabase recommended)
- [Claude API key](https://console.anthropic.com/)
- [Cloudinary account](https://cloudinary.com/)
- [Telegram bot token](https://t.me/BotFather)

## Setup

1. **Clone the repository**

   ```bash
   git clone https://github.com/saukaniabdhalim/receipt-capture.git
   cd receipt-capture
   ```

2. **Configure each project** by copying the template and filling in your credentials:

   ```bash
   cp ReceiptCapture.Api/appsettings.template.json ReceiptCapture.Api/appsettings.json
   cp ReceiptCapture.Bot/appsettings.template.json ReceiptCapture.Bot/appsettings.json
   ```

   **API (`appsettings.json`)**:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Host=...;Database=...;Username=...;Password=..."
     },
     "Claude": {
       "ApiKey": "sk-ant-...",
       "Model": "claude-3-5-sonnet-20241022"
     },
     "Cloudinary": {
       "CloudName": "...",
       "ApiKey": "...",
       "ApiSecret": "..."
     }
   }
   ```

   **Bot (`appsettings.json`)** — same keys plus:
   ```json
   {
     "Telegram": {
       "BotToken": "..."
     }
   }
   ```

3. **Apply database migrations**

   ```bash
   dotnet ef database update -p ReceiptCapture.Data -s ReceiptCapture.Api
   ```

4. **Run the services**

   ```bash
   dotnet run --project ReceiptCapture.Api
   dotnet run --project ReceiptCapture.Bot
   dotnet run --project ReceiptCapture.Web
   ```

## API Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| `GET` | `/health` | Health check |
| `GET` | `/api/receipts/user/{telegramUserId}/today` | Today's receipts summary |
| `GET` | `/api/receipts/user/{telegramUserId}/month` | This month's receipts summary |
| `GET` | `/api/reports/daily/{telegramUserId}?date=YYYY-MM-DD` | Daily spending report |
| `GET` | `/api/reports/monthly/{telegramUserId}?year=YYYY&month=M` | Monthly spending report |

Swagger UI is available at `/swagger` when running in Development mode.

## Deployment

The API is configured for [Railway](https://railway.app) deployment using the included `Dockerfile` and `railway.toml`. Set all required credentials as environment variables in Railway.

```bash
# Build Docker image locally
docker build -f ReceiptCapture.Api/Dockerfile -t receipt-capture-api .
```

## Tech Stack

- **Runtime**: .NET 8, ASP.NET Core, Entity Framework Core 8
- **AI**: [Anthropic.SDK](https://github.com/tghamm/Anthropic.SDK) (Claude 3.5 Sonnet)
- **Bot**: Telegram.Bot
- **Storage**: Cloudinary, PostgreSQL via Npgsql
- **Reports**: EPPlus (Excel), iTextSharp (PDF)
