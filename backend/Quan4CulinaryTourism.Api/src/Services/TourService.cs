using Microsoft.AspNetCore.Http;
using Quan4CulinaryTourism.Api.Common;
using Quan4CulinaryTourism.Api.DTOs;
using Quan4CulinaryTourism.Api.Models;
using Quan4CulinaryTourism.Api.Repositories;

namespace Quan4CulinaryTourism.Api.Services;

public class TourService
{
    private readonly TourRepository _tourRepository;
    private readonly PoiRepository _poiRepository;

    public TourService(TourRepository tourRepository, PoiRepository poiRepository)
    {
        _tourRepository = tourRepository;
        _poiRepository = poiRepository;
    }

    public async Task<List<TourResponse>> GetAllAsync(CancellationToken cancellationToken = default) =>
        (await _tourRepository.GetAllAsync(cancellationToken)).Select(Map).ToList();

    public async Task<List<TourResponse>> GetPublicToursAsync(string? lang = null, CancellationToken cancellationToken = default) =>
        (await _tourRepository.GetActiveAsync(lang, cancellationToken)).Select(Map).ToList();

    public async Task<List<TourResponse>> GetUserToursAsync(string userId, CancellationToken cancellationToken = default) =>
        (await _tourRepository.GetByCreatedByUserIdAsync(userId, cancellationToken)).Select(Map).ToList();

    public async Task<TourResponse> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await _tourRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ApiException("Không tìm thấy tour.", StatusCodes.Status404NotFound);
        return Map(entity);
    }

    public async Task<TourResponse> CreateAsync(CreateTourRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new Tour();
        await ApplyRequestAsync(entity, request, publicOnlyPois: false, cancellationToken);
        await _tourRepository.CreateAsync(entity, cancellationToken);
        return Map(entity);
    }

    public async Task<TourResponse> CreateUserTourAsync(string userId, CreateTourRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new Tour
        {
            CreatedByUserId = userId
        };
        await ApplyRequestAsync(entity, request, publicOnlyPois: true, cancellationToken);
        entity.IsActive = true;
        await _tourRepository.CreateAsync(entity, cancellationToken);
        return Map(entity);
    }

    public async Task<TourResponse> UpdateAsync(string id, UpdateTourRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _tourRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ApiException("Không tìm thấy tour.", StatusCodes.Status404NotFound);
        await ApplyRequestAsync(entity, request, publicOnlyPois: false, cancellationToken);
        await _tourRepository.UpdateAsync(entity, cancellationToken);
        return Map(entity);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        _ = await _tourRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ApiException("Không tìm thấy tour.", StatusCodes.Status404NotFound);
        await _tourRepository.DeleteAsync(id, cancellationToken);
    }

    private async Task ApplyRequestAsync(Tour entity, CreateTourRequest request, bool publicOnlyPois, CancellationToken cancellationToken)
    {
        if (request.Stops.Count == 0)
        {
            throw new ApiException("Tour phải có ít nhất một điểm dừng.");
        }

        var poiIds = request.Stops.Select(x => x.PoiId).Distinct().ToList();
        var pois = publicOnlyPois
            ? await _poiRepository.GetPublicManyByIdsAsync(poiIds, cancellationToken)
            : await _poiRepository.GetManyByIdsAsync(poiIds, cancellationToken);
        var poiLookup = pois.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var missingPoiIds = poiIds.Where(id => !poiLookup.ContainsKey(id)).ToList();
        if (missingPoiIds.Count > 0)
        {
            throw new ApiException($"POI không tồn tại trong tour: {string.Join(", ", missingPoiIds)}.");
        }

        entity.Title = request.Title.Trim();
        entity.Description = request.Description.Trim();
        entity.Lang = request.Lang.Trim().ToLowerInvariant();
        entity.CoverImageUrl = string.IsNullOrWhiteSpace(request.CoverImageUrl) ? null : request.CoverImageUrl.Trim();
        entity.EstimatedDurationMinutes = request.EstimatedDurationMinutes;
        entity.IsActive = request.IsActive;
        entity.Stops = request.Stops
            .OrderBy(x => x.Order)
            .Select(stop => new TourStop
            {
                PoiId = stop.PoiId,
                Title = string.IsNullOrWhiteSpace(stop.Title) ? poiLookup[stop.PoiId].Name : stop.Title.Trim(),
                Order = stop.Order,
                EstimatedStayMinutes = stop.EstimatedStayMinutes
            })
            .ToList();
        entity.UpdatedAt = DateTime.UtcNow;
    }

    private static TourResponse Map(Tour entity) => new()
    {
        Id = entity.Id,
        Title = entity.Title,
        Description = entity.Description,
        Lang = entity.Lang,
        CoverImageUrl = entity.CoverImageUrl,
        CreatedByUserId = entity.CreatedByUserId,
        EstimatedDurationMinutes = entity.EstimatedDurationMinutes,
        IsActive = entity.IsActive,
        Stops = entity.Stops
            .OrderBy(x => x.Order)
            .Select(stop => new TourStopResponse
            {
                PoiId = stop.PoiId,
                Title = stop.Title,
                Order = stop.Order,
                EstimatedStayMinutes = stop.EstimatedStayMinutes
            })
            .ToList(),
        UpdatedAt = entity.UpdatedAt
    };
}
