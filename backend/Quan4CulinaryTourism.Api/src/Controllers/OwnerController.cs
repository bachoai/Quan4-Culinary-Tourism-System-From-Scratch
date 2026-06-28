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

    [HttpPost("register")]
    public Task<IActionResult> Register([FromBody] CreateOwnerRegistrationRequest request) =>
        ExecuteAsync(() => _ownerService.RegisterAsync(_claimsHelper.GetUserId(User), request), "Gui dang ky owner thanh cong");

    [HttpGet("dashboard")]
    public Task<IActionResult> Dashboard() =>
        ExecuteAsync(() => _ownerService.GetDashboardAsync(_claimsHelper.GetUserId(User)), "Lay dashboard owner thanh cong");

    [HttpGet("pois")]
    public Task<IActionResult> GetPois([FromQuery] string? lang) =>
        ExecuteAsync(() => _ownerService.GetMyPoisAsync(_claimsHelper.GetUserId(User), lang), "Lay danh sach POI cua owner thanh cong");

    [HttpPost("submissions")]
    public Task<IActionResult> CreateSubmission([FromBody] CreateOwnerSubmissionRequest request) =>
        ExecuteAsync(() => _ownerService.CreateSubmissionAsync(_claimsHelper.GetUserId(User), request), "Tao submission thanh cong");

    [HttpGet("submissions")]
    public Task<IActionResult> GetSubmissions() =>
        ExecuteAsync(() => _ownerService.GetMySubmissionsAsync(_claimsHelper.GetUserId(User)), "Lay submissions thanh cong");

    [HttpGet("submissions/{id}")]
    public Task<IActionResult> GetSubmissionById(string id) =>
        ExecuteAsync(() => _ownerService.GetMySubmissionByIdAsync(_claimsHelper.GetUserId(User), id), "Lay submission thanh cong");

    [HttpPut("submissions/{id}")]
    public Task<IActionResult> UpdateSubmission(string id, [FromBody] CreateOwnerSubmissionRequest request) =>
        ExecuteAsync(() => _ownerService.UpdateMySubmissionAsync(_claimsHelper.GetUserId(User), id, request), "Cap nhat submission thanh cong");
}
