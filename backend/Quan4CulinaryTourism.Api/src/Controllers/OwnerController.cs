using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quan4CulinaryTourism.Api.Common;
using Quan4CulinaryTourism.Api.DTOs;
using Quan4CulinaryTourism.Api.Helpers;
using Quan4CulinaryTourism.Api.Models;
using Quan4CulinaryTourism.Api.Services;

namespace Quan4CulinaryTourism.Api.Controllers;

[Authorize(Roles = SharedConstants.UserRoles.Owner)]
[Route($"{AppConstants.ApiVersionPrefix}/owner")]
public class OwnerController : BaseApiController
{
    private readonly OwnerService _ownerService;
    private readonly ClaimsHelper _claimsHelper;

    public OwnerController(OwnerService ownerService, ClaimsHelper claimsHelper)
    {
        _ownerService = ownerService;
        _claimsHelper = claimsHelper;
    }

    [HttpGet("dashboard")]
    public Task<IActionResult> Dashboard() =>
        ExecuteAsync(() => _ownerService.GetDashboardAsync(_claimsHelper.GetUserId(User)), "Lấy dashboard owner thành công");

    [HttpGet("pois")]
    public Task<IActionResult> GetPois([FromQuery] string? lang) =>
        ExecuteAsync(() => _ownerService.GetMyPoisAsync(_claimsHelper.GetUserId(User), lang), "Lấy danh sách POI của owner thành công");

    [HttpGet("pois/{id}/localizations")]
    public Task<IActionResult> GetPoiLocalizations(string id) =>
        ExecuteAsync(() => _ownerService.GetMyPoiLocalizationsAsync(_claimsHelper.GetUserId(User), id), "Lấy localizations của owner thành công");

    [HttpPut("pois/{id}/localizations/{lang}")]
    public Task<IActionResult> UpsertPoiLocalization(string id, string lang, [FromBody] UpdatePoiLocalizationRequest request)
    {
        request.Lang = lang;
        return ExecuteAsync(
            () => _ownerService.UpsertMyPoiLocalizationAsync(_claimsHelper.GetUserId(User), id, request),
            "Luu localization cua owner thanh cong");
    }

    [HttpPost("pois/{id}/localizations/translate")]
    public Task<IActionResult> TranslatePoiLocalization(string id, [FromBody] TranslatePoiLocalizationRequest request) =>
        ExecuteAsync(
            () => _ownerService.TranslateMyPoiLocalizationAsync(_claimsHelper.GetUserId(User), id, request),
            "Dich localization cua owner thanh cong");

    [HttpPost("pois/{id}/audio")]
    public Task<IActionResult> UploadPoiAudio(string id, [FromForm] UploadPoiAudioRequest request, IFormFile? file) =>
        ExecuteAsync(
            () => _ownerService.UploadOrSetMyPoiAudioAsync(_claimsHelper.GetUserId(User), id, request, file),
            "Cap nhat audio cua owner thanh cong");

    [HttpPost("pois/{id}/audio/generate")]
    public Task<IActionResult> GeneratePoiAudio(string id, [FromBody] GeneratePoiAudioRequest request) =>
        ExecuteAsync(
            () => _ownerService.GenerateMyPoiAudioAsync(_claimsHelper.GetUserId(User), id, request),
            "Tao audio cua owner thanh cong");

    [HttpDelete("pois/{id}/audio")]
    public Task<IActionResult> DeletePoiAudio(string id, [FromQuery] string? lang) =>
        ExecuteAsync(
            () => _ownerService.DeleteMyPoiAudioAsync(_claimsHelper.GetUserId(User), id, lang),
            "Xoa audio cua owner thanh cong");

    [HttpPost("media/upload-image")]
    public Task<IActionResult> UploadImage(IFormFile file) =>
        ExecuteAsync(() => _ownerService.UploadMyImageAsync(_claimsHelper.GetUserId(User), file), "Upload anh cua owner thanh cong");

    [HttpPost("submissions")]
    public Task<IActionResult> CreateSubmission([FromBody] CreateOwnerSubmissionRequest request) =>
        ExecuteAsync(() => _ownerService.CreateSubmissionAsync(_claimsHelper.GetUserId(User), request), "Tạo submission thành công");

    [HttpGet("submissions")]
    public Task<IActionResult> GetSubmissions() =>
        ExecuteAsync(() => _ownerService.GetMySubmissionsAsync(_claimsHelper.GetUserId(User)), "Lấy submissions thành công");

    [HttpGet("submissions/{id}")]
    public Task<IActionResult> GetSubmissionById(string id) =>
        ExecuteAsync(() => _ownerService.GetMySubmissionByIdAsync(_claimsHelper.GetUserId(User), id), "Lấy submission thành công");

    [HttpPut("submissions/{id}")]
    public Task<IActionResult> UpdateSubmission(string id, [FromBody] CreateOwnerSubmissionRequest request) =>
        ExecuteAsync(() => _ownerService.UpdateMySubmissionAsync(_claimsHelper.GetUserId(User), id, request), "Cập nhật submission thành công");
}

