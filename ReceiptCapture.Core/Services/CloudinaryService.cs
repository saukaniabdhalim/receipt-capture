// ReceiptCapture.Core/Services/CloudinaryService.cs
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Logging;
using System.Security.Principal;

namespace ReceiptCapture.Core.Services;

public class CloudinaryService
{
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<CloudinaryService>? _logger;

    public CloudinaryService(string cloudName, string apiKey, string apiSecret, ILogger<CloudinaryService>? logger = null)
    {
        var account = new Account(cloudName, apiKey, apiSecret);
        _cloudinary = new Cloudinary(account);
        _logger = logger;
    }

    public async Task<string> UploadImageAsync(byte[] imageBytes, string fileName)
    {
        using var stream = new MemoryStream(imageBytes);

        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(fileName, stream),
            Folder = "receipts",
            Transformation = new Transformation()
                .Width(1200)
                .Height(1600)
                .Crop("limit")
                .Quality("auto")
                .FetchFormat("auto")
        };

        _logger?.LogInformation("Uploading image to Cloudinary: {FileName}", fileName);
        var result = await _cloudinary.UploadAsync(uploadParams);

        if (result.Error != null)
        {
            _logger?.LogError("Cloudinary upload failed: {Error}", result.Error.Message);
            throw new Exception($"Cloudinary upload failed: {result.Error.Message}");
        }

        _logger?.LogInformation("Image uploaded successfully: {Url}", result.SecureUrl);
        return result.SecureUrl.ToString();
    }

    public string GetThumbnailUrl(string imageUrl, int width = 300)
    {
        return imageUrl.Replace("/upload/", $"/upload/w_{width},q_auto,f_auto/");
    }
}