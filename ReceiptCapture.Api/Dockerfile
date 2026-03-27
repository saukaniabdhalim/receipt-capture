# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files for layer-cached restore
COPY ReceiptCaptureSystem.sln ./
COPY ReceiptCapture.Core/ReceiptCapture.Core.csproj ReceiptCapture.Core/
COPY ReceiptCapture.Data/ReceiptCapture.Data.csproj ReceiptCapture.Data/
COPY ReceiptCapture.Api/ReceiptCapture.Api.csproj ReceiptCapture.Api/

RUN dotnet restore ReceiptCapture.Api/ReceiptCapture.Api.csproj

# Copy full source and publish
COPY . .
RUN dotnet publish ReceiptCapture.Api/ReceiptCapture.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Run as non-root user
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

COPY --from=build /app/publish .

# Railway standard port
EXPOSE 8080

ENTRYPOINT ["dotnet", "ReceiptCapture.Api.dll"]
