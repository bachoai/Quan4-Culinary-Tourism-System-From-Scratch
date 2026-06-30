using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quan4CulinaryTourism.Api.Common;
using Quan4CulinaryTourism.Api.DTOs;
using Quan4CulinaryTourism.Api.Models;
using Quan4CulinaryTourism.Api.Services;

namespace Quan4CulinaryTourism.Api.Controllers;

[Route($"{AppConstants.ApiVersionPrefix}/poi")]
public class PoiController : BaseApiController
{
    private readonly PoiService _poiService;

    public PoiController(PoiService poiService) => _poiService = poiService;

    [HttpGet("load-all")]
    public Task<IActionResult> LoadAll([FromQuery] PoiSearchRequest request) =>
        ExecuteAsync(() => _poiService.LoadAllAsync(request), "Lay POI thanh cong");

    [HttpGet("{id}")]
    public Task<IActionResult> GetById(string id, [FromQuery] string? lang, [FromQuery] string? audioLang) =>
        ExecuteAsync(() => _poiService.GetByIdAsync(id, lang, audioLang), "Lay chi tiet POI thanh cong");

    [HttpGet("nearby")]
    public Task<IActionResult> Nearby(
        [FromQuery] double lat,
        [FromQuery] double lng,
        [FromQuery] int radius = 3000,
        [FromQuery] int limit = 20,
        [FromQuery] string? lang = null,
        [FromQuery] string? audioLang = null) =>
        ExecuteAsync(() => _poiService.NearbyAsync(lat, lng, radius, limit, lang, audioLang), "Lay POI gan day thanh cong");

    [HttpGet("search")]
    public Task<IActionResult> Search([FromQuery] PoiSearchRequest request) =>
        ExecuteAsync(() => _poiService.SearchAsync(request), "Tim kiem POI thanh cong");
}
