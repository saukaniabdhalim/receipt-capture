# Multi-stage build for .NET 8 console application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files for layer-cached restore
COPY ReceiptCaptureSystem.sln ./
COPY ReceiptCapture.Core/ReceiptCapture.Core.csproj ReceiptCapture.Core/
COPY ReceiptCapture.Data/ReceiptCapture.Data.csproj ReceiptCapture.Data/
COPY ReceiptCapture.Bot/ReceiptCapture.Bot.csproj ReceiptCapture.Bot/

RUN dotnet restore ReceiptCapture.Bot/ReceiptCapture.Bot.csproj

# Copy source and publish
COPY . .

RUN dotnet publish ReceiptCapture.Bot/ReceiptCapture.Bot.csproj -c Release -o /app/publish --no-restore

# Runtime — use dotnet/runtime (not aspnet) for console applications
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app

# Non-root user for security
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

COPY --from=build /app/publish .

ENV DOTNET_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "ReceiptCapture.Bot.dll"]
