# ReceiptCapture.Api/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first for better layer caching
COPY ReceiptCaptureSystem.sln ./
COPY ReceiptCapture.Core/ReceiptCapture.Core.csproj ReceiptCapture.Core/
COPY ReceiptCapture.Data/ReceiptCapture.Data.csproj ReceiptCapture.Data/
COPY ReceiptCapture.Api/ReceiptCapture.Api.csproj ReceiptCapture.Api/

RUN dotnet restore ReceiptCapture.Api/ReceiptCapture.Api.csproj

# Copy all source
COPY . .

# Build and publish
RUN dotnet publish ReceiptCapture.Api/ReceiptCapture.Api.csproj -c Release -o /app/publish --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "ReceiptCapture.Api.dll"]