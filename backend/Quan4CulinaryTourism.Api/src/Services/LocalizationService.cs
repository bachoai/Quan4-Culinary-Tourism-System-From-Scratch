using Microsoft.AspNetCore.Http;
using Quan4CulinaryTourism.Api.Common;
using Quan4CulinaryTourism.Api.DTOs;
using Quan4CulinaryTourism.Api.Models;
using Quan4CulinaryTourism.Api.Repositories;

namespace Quan4CulinaryTourism.Api.Services;

public class LocalizationService
{
    private readonly PoiRepository _poiRepository;
    private readonly PoiLocalizationRepository _repository;

    public LocalizationService(PoiRepository poiRepository, PoiLocalizationRepository repository)
    {
        _poiRepository = poiRepository;
        _repository = repository;
    }

    public async Task<List<PoiLocalizationResponse>> GetPoiLocalizationsAsync(string poiId, CancellationToken cancellationToken = default)
    {
        _ = await _poiRepository.GetByIdAsync(poiId, cancellationToken)
            ?? throw new ApiException("Không tìm thấy POI.", StatusCodes.Status404NotFound);
        return (await _repository.GetByPoiIdAsync(poiId, cancellationToken)).Select(ToResponse).ToList();
    }

    public async Task<PoiLocalizationResponse> CreateAsync(string poiId, CreatePoiLocalizationRequest request, CancellationToken cancellationToken = default)
    {
        _ = await _poiRepository.GetByIdAsync(poiId, cancellationToken)
            ?? throw new ApiException("Không tìm thấy POI.", StatusCodes.Status404NotFound);
        var existing = await _repository.GetByPoiAndLangAsync(poiId, request.Lang, cancellationToken);
        if (existing is not null)
        {
            throw new ApiException("Localization đã tồn tại.");
        }

        var entity = new PoiLocalization
        {
            PoiId = poiId,
            Lang = request.Lang,
            Name = request.Name,
            Description = request.Description,
            AudioUrl = request.AudioUrl,
            TtsScript = string.IsNullOrWhiteSpace(request.TtsScript) ? null : request.TtsScript.Trim(),
            IsFallback = request.IsFallback
        };
        await _repository.CreateAsync(entity, cancellationToken);
        return ToResponse(entity);
    }

    public async Task<PoiLocalizationResponse> UpdateAsync(string poiId, string lang, UpdatePoiLocalizationRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByPoiAndLangAsync(poiId, lang, cancellationToken)
            ?? throw new ApiException("Không tìm thấy localization.", StatusCodes.Status404NotFound);
        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.AudioUrl = request.AudioUrl;
        entity.TtsScript = string.IsNullOrWhiteSpace(request.TtsScript) ? null : request.TtsScript.Trim();
        entity.IsFallback = request.IsFallback;
        await _repository.UpdateAsync(entity, cancellationToken);
        return ToResponse(entity);
    }

    public async Task DeleteAsync(string poiId, string lang, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByPoiAndLangAsync(poiId, lang, cancellationToken)
            ?? throw new ApiException("Không tìm thấy localization.", StatusCodes.Status404NotFound);
        await _repository.DeleteAsync(entity.PoiId, entity.Lang, cancellationToken);
    }

    private static PoiLocalizationResponse ToResponse(PoiLocalization entity) => new()
    {
        Id = entity.Id,
        PoiId = entity.PoiId,
        Lang = entity.Lang,
        Name = entity.Name,
        Description = entity.Description,
        AudioUrl = entity.AudioUrl,
        TtsScript = entity.TtsScript,
        IsFallback = entity.IsFallback
    };
}
