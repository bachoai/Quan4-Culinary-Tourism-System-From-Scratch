using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quan4CulinaryTourism.Api.Common;
using Quan4CulinaryTourism.Api.DTOs;
using Quan4CulinaryTourism.Api.Models;
using Quan4CulinaryTourism.Api.Services;

namespace Quan4CulinaryTourism.Api.Controllers;

public class AudioController : BaseApiController
{
    private readonly AudioService _audioService;

    public AudioController(AudioService audioService)
    {
        _audioService = audioService;
    }

    [HttpGet($"{AppConstants.ApiVersionPrefix}/audio/languages")]
    public Task<IActionResult> GetLanguages() =>
        ExecuteAsync(() => _audioService.GetLanguagesAsync(), "Lấy ngôn ngữ audio thành công");

    [HttpGet($"{AppConstants.ApiVersionPrefix}/poi/{{id}}/audio")]
    public Task<IActionResult> GetPoiAudio(string id, [FromQuery] string? lang) =>
        ExecuteAsync(() => _audioService.GetPoiAudioAsync(id, lang), "Lấy audio thành công");

    [Authorize(Roles = SharedConstants.UserRoles.Admin)]
    [HttpPost($"{AppConstants.ApiVersionPrefix}/admin/pois/{{id}}/audio")]
    public Task<IActionResult> UploadPoiAudio(string id, [FromForm] UploadPoiAudioRequest request, IFormFile? file) =>
        ExecuteAsync(() => _audioService.UploadOrSetAudioAsync(id, request, file), "Cập nhật audio thành công");

    [Authorize(Roles = SharedConstants.UserRoles.Admin)]
    [HttpPost($"{AppConstants.ApiVersionPrefix}/admin/pois/{{id}}/audio/generate")]
    public Task<IActionResult> GeneratePoiAudio(string id, [FromBody] GeneratePoiAudioRequest request) =>
        ExecuteAsync(() => _audioService.GeneratePoiAudioAsync(id, request), "Tạo audio từ nội dung lời nói thành công");

    [Authorize(Roles = SharedConstants.UserRoles.Admin)]
    [HttpDelete($"{AppConstants.ApiVersionPrefix}/admin/pois/{{id}}/audio")]
    public Task<IActionResult> DeletePoiAudio(string id, [FromQuery] string? lang) =>
        ExecuteAsync(() => _audioService.DeletePoiAudioAsync(id, lang), "Xóa audio thành công");

    [HttpGet($"{AppConstants.ApiVersionPrefix}/audio/pack-manifest")]
    public Task<IActionResult> GetPackManifest() =>
        ExecuteAsync(() => _audioService.GetPackManifestAsync(), "Lấy audio manifest thành công");
}
