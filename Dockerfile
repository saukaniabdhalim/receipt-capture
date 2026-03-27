# Multi-stage build for .NET 8
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and projects
COPY ReceiptCaptureSystem.sln ./
COPY ReceiptCapture.Core/ReceiptCapture.Core.csproj ReceiptCapture.Core/
COPY ReceiptCapture.Data/ReceiptCapture.Data.csproj ReceiptCapture.Data/
COPY ReceiptCapture.Api/ReceiptCapture.Api.csproj ReceiptCapture.Api/

RUN dotnet restore ReceiptCapture.Api/ReceiptCapture.Api.csproj

# Copy source
COPY . .

# Build and publish
RUN dotnet publish ReceiptCapture.Api/ReceiptCapture.Api.csproj -c Release -o /app/publish --no-restore

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Non-root user for security
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

COPY --from=build /app/publish .

# Railway sets PORT - don't hardcode
EXPOSE 8080

ENTRYPOINT ["dotnet", "ReceiptCapture.Api.dll"]