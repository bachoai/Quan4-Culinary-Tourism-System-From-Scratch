using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quan4CulinaryTourism.Api.Common;
using Quan4CulinaryTourism.Api.DTOs;
using Quan4CulinaryTourism.Api.Models;
using Quan4CulinaryTourism.Api.Services;

namespace Quan4CulinaryTourism.Api.Controllers;

public class TourController : BaseApiController
{
    private readonly TourService _tourService;

    public TourController(TourService tourService) => _tourService = tourService;

    [HttpGet($"{AppConstants.ApiVersionPrefix}/tours")]
    public Task<IActionResult> PublicTours([FromQuery] string? lang) =>
        ExecuteAsync(() => _tourService.GetPublicToursAsync(lang), "Lấy tours công khai thành công");

    [Authorize(Roles = SharedConstants.UserRoles.Admin)]
    [HttpGet($"{AppConstants.ApiVersionPrefix}/admin/tours")]
    public Task<IActionResult> GetAll() => ExecuteAsync(() => _tourService.GetAllAsync(), "Lấy tours thành công");

    [Authorize(Roles = SharedConstants.UserRoles.Admin)]
    [HttpGet($"{AppConstants.ApiVersionPrefix}/admin/tours/{{id}}")]
    public Task<IActionResult> GetById(string id) => ExecuteAsync(() => _tourService.GetByIdAsync(id), "Lấy tour thành công");

    [Authorize(Roles = SharedConstants.UserRoles.Admin)]
    [HttpPost($"{AppConstants.ApiVersionPrefix}/admin/tours")]
    public Task<IActionResult> Create([FromBody] CreateTourRequest request) => ExecuteAsync(() => _tourService.CreateAsync(request), "Tạo tour thành công");

    [Authorize(Roles = SharedConstants.UserRoles.Admin)]
    [HttpPut($"{AppConstants.ApiVersionPrefix}/admin/tours/{{id}}")]
    public Task<IActionResult> Update(string id, [FromBody] UpdateTourRequest request) => ExecuteAsync(() => _tourService.UpdateAsync(id, request), "Cập nhật tour thành công");

    [Authorize(Roles = SharedConstants.UserRoles.Admin)]
    [HttpDelete($"{AppConstants.ApiVersionPrefix}/admin/tours/{{id}}")]
    public Task<IActionResult> Delete(string id) => ExecuteAsync(() => _tourService.DeleteAsync(id), "Xóa tour thành công");
}
