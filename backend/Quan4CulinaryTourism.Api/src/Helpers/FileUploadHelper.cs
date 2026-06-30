using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Options;
using Quan4CulinaryTourism.Api.Common;
using Quan4CulinaryTourism.Api.Database;
using AppUploadSettings = Quan4CulinaryTourism.Api.Database.UploadSettings;

namespace Quan4CulinaryTourism.Api.Helpers;

public class FileUploadHelper
{
    private const string CloudinaryStorageProvider = "cloudinary";

    private readonly AppUploadSettings _settings;
    private readonly CloudinarySettings _cloudinarySettings;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<FileUploadHelper> _logger;
    private readonly Cloudinary? _cloudinary;

    public FileUploadHelper(
        IOptions<AppUploadSettings> settings,
        IOptions<CloudinarySettings> cloudinarySettings,
        IWebHostEnvironment environment,
        ILogger<FileUploadHelper> logger)
    {
        _settings = settings.Value;
        _cloudinarySettings = cloudinarySettings.Value;
        _environment = environment;
        _logger = logger;

        if (IsCloudinaryConfigured())
        {
            _cloudinary = new Cloudinary(new Account(
                _cloudinarySettings.CloudName,
                _cloudinarySettings.ApiKey,
                _cloudinarySettings.ApiSecret));
        }
    }

    public void ValidateImage(IFormFile file)
    {
        Validate(file, AppConstants.SupportedImageExtensions, AppConstants.SupportedImageMimeTypes, _settings.MaxImageSizeMb);
    }

    public void ValidateAudio(IFormFile file)
    {
        Validate(file, AppConstants.SupportedAudioExtensions, AppConstants.SupportedAudioMimeTypes, _settings.MaxAudioSizeMb);
    }

    public async Task<StoredFileResult> SaveFileAsync(IFormFile file, string subFolder, CancellationToken cancellationToken = default)
    {
        EnsureCloudinaryConfigured();

        await using var stream = file.OpenReadStream();
        var fileDescription = new FileDescription(file.FileName, stream);
        return await UploadAsync(fileDescription, file.FileName, subFolder, file.ContentType, file.Length, cancellationToken);
    }

    public async Task<StoredFileResult> UploadLocalFileAsync(
        string filePath,
        string subFolder,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        EnsureCloudinaryConfigured();

        var fileInfo = new FileInfo(filePath);
        var fileDescription = new FileDescription(filePath);
        return await UploadAsync(fileDescription, fileInfo.Name, subFolder, contentType, fileInfo.Length, cancellationToken);
    }

    public string CreateTemporaryFilePath(string extension)
    {
        var normalizedExtension = extension.StartsWith('.') ? extension : $".{extension}";
        return Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}{normalizedExtension.ToLowerInvariant()}");
    }

    public async Task DeleteManagedFileAsync(
        string? publicUrl,
        string? storageProvider = null,
        string? objectKey = null,
        string? resourceType = null,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(storageProvider, CloudinaryStorageProvider, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(objectKey))
        {
            await TryDeleteCloudinaryAssetAsync(objectKey, resourceType, cancellationToken);
            return;
        }

        TryDeleteLegacyLocalFile(publicUrl);
    }

    private async Task<StoredFileResult> UploadAsync(
        FileDescription fileDescription,
        string originalFileName,
        string subFolder,
        string? contentType,
        long sizeBytes,
        CancellationToken cancellationToken)
    {
        if (IsImageContentType(contentType))
        {
            var uploadParams = new ImageUploadParams
            {
                File = fileDescription,
                PublicId = BuildPublicId(subFolder),
                Overwrite = false,
                UseFilename = false,
                UniqueFilename = false
            };

            var result = await _cloudinary!.UploadAsync(uploadParams, cancellationToken);
            return ToStoredFileResult(result, "image", sizeBytes);
        }

        var extension = GetNormalizedExtension(originalFileName);
        var uploadParamsRaw = new RawUploadParams
        {
            File = fileDescription,
            PublicId = $"{BuildPublicId(subFolder)}{extension}",
            Overwrite = false,
            UseFilename = false,
            UniqueFilename = false
        };

        var rawResult = await _cloudinary!.UploadAsync(uploadParamsRaw, "auto", cancellationToken);
        return ToStoredFileResult(rawResult, string.IsNullOrWhiteSpace(rawResult.ResourceType) ? "raw" : rawResult.ResourceType, sizeBytes);
    }

    private StoredFileResult ToStoredFileResult(UploadResult result, string resourceType, long sizeBytes)
    {
        if (result.Error is not null)
        {
            throw new ApiException($"Cloudinary upload thất bại: {result.Error.Message}", StatusCodes.Status502BadGateway);
        }

        if (string.IsNullOrWhiteSpace(result.PublicId))
        {
            throw new ApiException("Cloudinary upload không trả về public ID.", StatusCodes.Status502BadGateway);
        }

        var url = result.SecureUrl?.ToString() ?? result.Url?.ToString();
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ApiException("Cloudinary upload không trả về URL.", StatusCodes.Status502BadGateway);
        }

        return new StoredFileResult(
            Url: url,
            StorageProvider: CloudinaryStorageProvider,
            ObjectKey: result.PublicId,
            FileName: Path.GetFileName(result.PublicId),
            ResourceType: resourceType,
            SizeBytes: sizeBytes);
    }

    private void EnsureCloudinaryConfigured()
    {
        if (_cloudinary is not null)
        {
            return;
        }

        throw new ApiException(
            "Cloudinary chưa được cấu hình. Hãy thiết lập CloudinarySettings__CloudName, CloudinarySettings__ApiKey, và CloudinarySettings__ApiSecret.",
            StatusCodes.Status500InternalServerError);
    }

    private bool IsCloudinaryConfigured() =>
        !string.IsNullOrWhiteSpace(_cloudinarySettings.CloudName) &&
        !string.IsNullOrWhiteSpace(_cloudinarySettings.ApiKey) &&
        !string.IsNullOrWhiteSpace(_cloudinarySettings.ApiSecret);

    private string BuildPublicId(string subFolder)
    {
        var parts = new[]
        {
            NormalizePathSegment(_cloudinarySettings.RootFolder),
            NormalizePathSegment(subFolder),
            Guid.NewGuid().ToString("N")
        }.Where(static part => !string.IsNullOrWhiteSpace(part));

        return string.Join("/", parts);
    }

    private async Task TryDeleteCloudinaryAssetAsync(string? objectKey, string? resourceType, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(objectKey))
        {
            return;
        }

        if (_cloudinary is null)
        {
            _logger.LogWarning("Skipping Cloudinary delete for {ObjectKey} because Cloudinary settings are missing.", objectKey);
            return;
        }

        try
        {
            var result = await _cloudinary.DestroyAsync(
                new DeletionParams(objectKey)
                {
                    Invalidate = true,
                    ResourceType = ResolveResourceType(resourceType)
                });

            if (result.Error is not null)
            {
                _logger.LogWarning("Cloudinary delete for {ObjectKey} returned an error: {Message}", objectKey, result.Error.Message);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unable to delete Cloudinary asset {ObjectKey}", objectKey);
        }
    }

    private void TryDeleteLegacyLocalFile(string? publicUrl)
    {
        if (string.IsNullOrWhiteSpace(publicUrl))
        {
            return;
        }

        if (!TryResolveLegacyLocalFilePath(publicUrl, out var fullPath))
        {
            return;
        }

        try
        {
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private bool TryResolveLegacyLocalFilePath(string publicUrl, out string fullPath)
    {
        fullPath = string.Empty;

        var candidatePath = publicUrl;
        if (Uri.TryCreate(publicUrl, UriKind.Absolute, out var absoluteUri))
        {
            candidatePath = absoluteUri.AbsolutePath;
        }

        candidatePath = candidatePath.Replace('\\', '/');
        const string uploadsPrefix = "/uploads/";
        if (!candidatePath.StartsWith(uploadsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var relativeAssetPath = candidatePath[uploadsPrefix.Length..]
            .Replace('/', Path.DirectorySeparatorChar);
        var uploadsRoot = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, _settings.UploadPath));
        var resolvedPath = Path.GetFullPath(Path.Combine(uploadsRoot, relativeAssetPath));
        var uploadsRootWithSeparator = uploadsRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

        if (!resolvedPath.StartsWith(uploadsRootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        fullPath = resolvedPath;
        return true;
    }

    private static bool IsImageContentType(string? contentType) =>
        !string.IsNullOrWhiteSpace(contentType) &&
        contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);

    private static string GetNormalizedExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return string.IsNullOrWhiteSpace(extension) ? string.Empty : extension.ToLowerInvariant();
    }

    private static string NormalizePathSegment(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Trim('/').Replace('\\', '/');

    private static ResourceType ResolveResourceType(string? resourceType) =>
        resourceType?.Trim().ToLowerInvariant() switch
        {
            "raw" => ResourceType.Raw,
            "video" => ResourceType.Video,
            "auto" => ResourceType.Auto,
            _ => ResourceType.Image
        };

    private static void Validate(
        IFormFile file,
        IReadOnlyCollection<string> allowedExtensions,
        IReadOnlyCollection<string> allowedMimeTypes,
        int maxSizeMb)
    {
        if (file.Length <= 0)
        {
            throw new ApiException("File tải lên đang rỗng.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension))
        {
            throw new ApiException("Định dạng file không hợp lệ.");
        }

        if (!allowedMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
        {
            throw new ApiException("MIME type không hợp lệ.");
        }

        var maxBytes = maxSizeMb * 1024L * 1024L;
        if (file.Length > maxBytes)
        {
            throw new ApiException($"File vượt quá giới hạn {maxSizeMb}MB.");
        }
    }
}

public sealed record StoredFileResult(
    string Url,
    string StorageProvider,
    string ObjectKey,
    string FileName,
    string ResourceType,
    long SizeBytes);
