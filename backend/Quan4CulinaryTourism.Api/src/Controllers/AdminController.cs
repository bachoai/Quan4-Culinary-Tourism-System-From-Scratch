using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quan4CulinaryTourism.Api.Common;
using Quan4CulinaryTourism.Api.DTOs;
using Quan4CulinaryTourism.Api.Helpers;
using Quan4CulinaryTourism.Api.Models;
using Quan4CulinaryTourism.Api.Services;

namespace Quan4CulinaryTourism.Api.Controllers;

[Route($"{AppConstants.ApiVersionPrefix}/admin")]
public class AdminController : BaseApiController
{
    private readonly AdminService _adminService;
    private readonly PoiService _poiService;
    private readonly ClaimsHelper _claimsHelper;

    public AdminController(AdminService adminService, PoiService poiService, ClaimsHelper claimsHelper)
    {
        _adminService = adminService;
        _poiService = poiService;
        _claimsHelper = claimsHelper;
    }

    [HttpPost("auth/login")]
    public Task<IActionResult> Login([FromBody] LoginRequest request) => ExecuteAsync(() => _adminService.LoginAdminAsync(request), "Đăng nhập admin thành công");

    [Authorize(Roles = SharedConstants.UserRoles.Admin)]
    [HttpGet("dashboard/stats")]
    public Task<IActionResult> DashboardStats() => ExecuteAsync(() => _adminService.GetDashboardStatsAsync(), "Lấy dashboard admin thành công");

    [Authorize(Roles = SharedConstants.UserRoles.Admin)]
    [HttpGet("users")]
    public Task<IActionResult> GetUsers() => ExecuteAsync(() => _adminService.GetUsersAsync(), "Lấy users thành công");

    [Authorize(Roles = SharedConstants.UserRoles.Admin)]
    [HttpPut("users/{id}/status")]
    public Task<IActionResult> UpdateUserStatus(string id, [FromBody] UpdateUserStatusRequest request) => ExecuteAsync(() => _adminService.UpdateUserStatusAsync(id, request), "Cập nhật trạng thái user thành công");

    [Authorize(Roles = SharedConstants.UserRoles.Admin)]
    [HttpPut("users/{id}/roles")]
    public Task<IActionResult> UpdateUserRoles(string id, [FromBody] UpdateUserRolesRequest request) => ExecuteAsync(() => _adminService.UpdateUserRolesAsync(id, request), "Cập nhật roles thành công");

    [Authorize(Roles = SharedConstants.UserRoles.Admin)]
    [HttpGet("owner-registrations")]
    public Task<IActionResult> GetOwnerRegistrations([FromQuery] string? status) => ExecuteAsync(() => _adminService.GetOwnerRegistrationsAsync(status), "Lấy owner registrations thành công");

    [Authorize(Roles = SharedConstants.UserRoles.Admin)]
    [HttpPut("owner-registrations/{id}/approve")]
    public Task<IActionResult> ApproveOwner(string id) =>
        ExecuteAsync(() => _adminService.ApproveOwnerAsync(_claimsHelper.GetUserId(User), id), "Duyệt owner thành công");

    [Authorize(Roles = SharedConstants.UserRoles.Admin)]
    [HttpPut("owner-registrations/{id}/reject")]
    public Task<IActionResult> RejectOwner(string id, [FromBody] RejectOwnerRegistrationRequest request) =>
        ExecuteAsync(() => _adminService.RejectOwnerAsync(_claimsHelper.GetUserId(User), id, request), "Từ chối owner thành công");

    [Authorize(Roles = SharedConstants.UserRoles.Admin)]
    [HttpDelete("owner-registrations/{id}/disable")]
    public Task<IActionResult> DisableOwner(string id) =>
        ExecuteAsync(() => _adminService.DisableOwnerAsync(_claimsHelper.GetUserId(User), id), "Vo hieu hoa owner thanh cong");

    [Authorize(Roles = SharedConstants.UserRoles.Admin)]
    [HttpGet("submissions")]
    public Task<IActionResult> GetSubmissions([FromQuery] string? status) => ExecuteAsync(() => _adminService.GetSubmissionsAsync(status), "Lấy submissions thành công");

    [Authorize(Roles = SharedConstants.UserRoles.Admin)]
    [HttpPut("submissions/{id}/approve")]
    public Task<IActionResult> ApproveSubmission(string id) =>
        ExecuteAsync(() => _adminService.ApproveSubmissionAsync(_claimsHelper.GetUserId(User), id), "Duyệt submission thành công");

    [Authorize(Roles = SharedConstants.UserRoles.Admin)]
    [HttpPut("submissions/{id}/reject")]
    public Task<IActionResult> RejectSubmission(string id, [FromBody] RejectSubmissionRequest request) =>
        ExecuteAsync(() => _adminService.RejectSubmissionAsync(_claimsHelper.GetUserId(User), id, request), "Từ chối submission thành công");

    [Authorize(Roles = SharedConstants.UserRoles.Admin)]
    [HttpPost("pois")]
    public Task<IActionResult> CreatePoi([FromBody] CreatePoiRequest request) => ExecuteAsync(() => _poiService.CreateAsync(request), "Tạo POI thành công");

    [Authorize(Roles = SharedConstants.UserRoles.Admin)]
    [HttpPut("pois/{id}")]
    public Task<IActionResult> UpdatePoi(string id, [FromBody] UpdatePoiRequest request) => ExecuteAsync(() => _poiService.UpdateAsync(id, request), "Cập nhật POI thành công");

    [Authorize(Roles = SharedConstants.UserRoles.Admin)]
    [HttpDelete("pois/{id}")]
    public Task<IActionResult> DeletePoi(string id) => ExecuteAsync(() => _poiService.DeleteAsync(id), "Xóa POI thành công");

    [Authorize(Roles = SharedConstants.UserRoles.Admin)]
    [HttpPatch("pois/{id}/active")]
    public Task<IActionResult> SetPoiActive(string id, [FromQuery] bool isActive) => ExecuteAsync(() => _poiService.SetActiveAsync(id, isActive), "Cập nhật trạng thái POI thành công");
}
