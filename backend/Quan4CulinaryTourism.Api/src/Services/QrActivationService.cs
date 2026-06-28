using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Quan4CulinaryTourism.Api.Common;
using Quan4CulinaryTourism.Api.DTOs;
using Quan4CulinaryTourism.Api.Database;
using Quan4CulinaryTourism.Api.Models;
using Quan4CulinaryTourism.Api.Repositories;

namespace Quan4CulinaryTourism.Api.Services;

public class QrActivationService
{
    private readonly QrActivationRepository _qrActivationRepository;
    private readonly PoiRepository _poiRepository;
    private readonly PublicSiteSettings _publicSiteSettings;

    public QrActivationService(
        QrActivationRepository qrActivationRepository,
        PoiRepository poiRepository,
        IOptions<PublicSiteSettings> publicSiteSettings)
    {
        _qrActivationRepository = qrActivationRepository;
        _poiRepository = poiRepository;
        _publicSiteSettings = publicSiteSettings.Value;
    }

    public async Task<List<QrActivationResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _qrActivationRepository.GetAllAsync(cancellationToken);
        return await MapManyAsync(entities, cancellationToken);
    }

    public async Task<QrActivationResponse> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await _qrActivationRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ApiException("Không tìm thấy mã kích hoạt QR.", StatusCodes.Status404NotFound);
        return await MapAsync(entity, cancellationToken);
    }

    public async Task<QrActivationResponse> ResolveAsync(string rawCode, CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(rawCode);
        var entity = await _qrActivationRepository.GetByCodeAsync(code, cancellationToken)
            ?? throw new ApiException("Không tìm thấy mã kích hoạt QR.", StatusCodes.Status404NotFound);

        if (!entity.IsActive)
        {
            throw new ApiException("Mã kích hoạt QR hiện đang tạm khóa.", StatusCodes.Status400BadRequest);
        }

        return await MapAsync(entity, cancellationToken);
    }

    public async Task<QrActivationResponse> CreateAsync(CreateQrActivationRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePoiExistsAsync(request.PoiId, cancellationToken);

        var entity = new QrActivation
        {
            Code = NormalizeCode(request.Code),
            PoiId = request.PoiId.Trim(),
            Title = request.Title.Trim(),
            StopZone = NormalizeStopZone(request.StopZone),
            StopAddress = NormalizeOptionalText(request.StopAddress),
            SortOrder = Math.Max(0, request.SortOrder),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ScanMode = NormalizeScanMode(request.ScanMode),
            IsActive = request.IsActive
        };

        await _qrActivationRepository.CreateAsync(entity, cancellationToken);
        return await MapAsync(entity, cancellationToken);
    }

    public async Task<QrActivationResponse> UpdateAsync(string id, UpdateQrActivationRequest request, CancellationToken cancellationToken = default)
    {
        await EnsurePoiExistsAsync(request.PoiId, cancellationToken);

        var entity = await _qrActivationRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ApiException("Không tìm thấy mã kích hoạt QR.", StatusCodes.Status404NotFound);

        entity.Code = NormalizeCode(request.Code);
        entity.PoiId = request.PoiId.Trim();
        entity.Title = request.Title.Trim();
        entity.StopZone = NormalizeStopZone(request.StopZone);
        entity.StopAddress = NormalizeOptionalText(request.StopAddress);
        entity.SortOrder = Math.Max(0, request.SortOrder);
        entity.Description = NormalizeOptionalText(request.Description);
        entity.ScanMode = NormalizeScanMode(request.ScanMode);
        entity.IsActive = request.IsActive;

        await _qrActivationRepository.UpdateAsync(entity, cancellationToken);
        return await MapAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        _ = await _qrActivationRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ApiException("Không tìm thấy mã kích hoạt QR.", StatusCodes.Status404NotFound);
        await _qrActivationRepository.DeleteAsync(id, cancellationToken);
    }

    private async Task EnsurePoiExistsAsync(string poiId, CancellationToken cancellationToken)
    {
        _ = await _poiRepository.GetByIdAsync(poiId.Trim(), cancellationToken)
            ?? throw new ApiException("Không tìm thấy POI để gắn mã QR.", StatusCodes.Status404NotFound);
    }

    private async Task<List<QrActivationResponse>> MapManyAsync(List<QrActivation> entities, CancellationToken cancellationToken)
    {
        if (entities.Count == 0)
        {
            return [];
        }

        var poiLookup = (await _poiRepository.GetManyByIdsAsync(entities.Select(x => x.PoiId), cancellationToken))
            .ToDictionary(x => x.Id, x => x, StringComparer.Ordinal);

        return entities.Select(entity => Map(entity, poiLookup, _publicSiteSettings)).ToList();
    }

    private async Task<QrActivationResponse> MapAsync(QrActivation entity, CancellationToken cancellationToken)
    {
        var poi = await _poiRepository.GetByIdAsync(entity.PoiId, cancellationToken)
            ?? throw new ApiException("POI gắn với mã QR không còn tồn tại.", StatusCodes.Status404NotFound);
        return Map(entity, new Dictionary<string, Poi>(StringComparer.Ordinal) { [poi.Id] = poi }, _publicSiteSettings);
    }

    private static QrActivationResponse Map(
        QrActivation entity,
        IReadOnlyDictionary<string, Poi> poiLookup,
        PublicSiteSettings publicSiteSettings)
    {
        poiLookup.TryGetValue(entity.PoiId, out var poi);

        return new QrActivationResponse
        {
            Id = entity.Id,
            Code = entity.Code,
            PoiId = entity.PoiId,
            PoiName = poi?.Name ?? entity.PoiId,
            PoiAddress = poi?.Address ?? string.Empty,
            PoiWard = poi?.Ward ?? string.Empty,
            Title = entity.Title,
            StopZone = string.IsNullOrWhiteSpace(entity.StopZone) ? "Chưa phân khu" : entity.StopZone,
            StopAddress = entity.StopAddress,
            SortOrder = entity.SortOrder,
            Description = entity.Description,
            ScanMode = entity.ScanMode,
            DeepLink = BuildDeepLink(entity, publicSiteSettings),
            IsActive = entity.IsActive,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private static string BuildDeepLink(QrActivation entity, PublicSiteSettings settings)
    {
        var baseUrl = string.IsNullOrWhiteSpace(settings.BaseUrl)
            ? "http://localhost:5173"
            : settings.BaseUrl.Trim();

        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var siteUri))
        {
            baseUrl = "http://localhost:5173";
            siteUri = new Uri(baseUrl);
        }

        var builder = new UriBuilder(siteUri)
        {
            Path = siteUri.AbsolutePath,
            Query = string.Empty,
            Fragment = $"/poi/{Uri.EscapeDataString(entity.PoiId)}?autoplay={Uri.EscapeDataString(entity.ScanMode)}&source=qr&code={Uri.EscapeDataString(entity.Code)}"
        };

        return builder.Uri.ToString();
    }

    private static string NormalizeCode(string rawCode)
    {
        var normalized = rawCode.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ApiException("Code QR không được để trống.");
        }

        return normalized;
    }

    private static string NormalizeScanMode(string? scanMode)
    {
        var normalized = string.IsNullOrWhiteSpace(scanMode) ? "prefer_audio" : scanMode.Trim().ToLowerInvariant();
        if (!SharedConstants.QrScanModes.Contains(normalized, StringComparer.Ordinal))
        {
            throw new ApiException("ScanMode không hợp lệ.");
        }

        return normalized;
    }

    private static string NormalizeStopZone(string rawStopZone)
    {
        var normalized = rawStopZone.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ApiException("Khu vực điểm dừng không được để trống.");
        }

        return normalized;
    }

    private static string? NormalizeOptionalText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

