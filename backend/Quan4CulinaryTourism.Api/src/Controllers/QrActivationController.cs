using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quan4CulinaryTourism.Api.Common;
using Quan4CulinaryTourism.Api.DTOs;
using Quan4CulinaryTourism.Api.Models;
using Quan4CulinaryTourism.Api.Services;

namespace Quan4CulinaryTourism.Api.Controllers;

public class QrActivationController : BaseApiController
{
    private readonly QrActivationService _qrActivationService;

    public QrActivationController(QrActivationService qrActivationService) => _qrActivationService = qrActivationService;

    [HttpGet($"{AppConstants.ApiVersionPrefix}/qr-activations/resolve")]
    public Task<IActionResult> Resolve([FromQuery] string code) =>
        ExecuteAsync(() => _qrActivationService.ResolveAsync(code), "Lấy thông tin kích hoạt QR thành công");

    [Authorize(Roles = SharedConstants.UserRoles.Admin)]
    [HttpGet($"{AppConstants.ApiVersionPrefix}/admin/qr-activations")]
    public Task<IActionResult> GetAll() =>
        ExecuteAsync(() => _qrActivationService.GetAllAsync(), "Lấy danh sách mã kích hoạt QR thành công");

    [Authorize(Roles = SharedConstants.UserRoles.Admin)]
    [HttpGet($"{AppConstants.ApiVersionPrefix}/admin/qr-activations/{{id}}")]
    public Task<IActionResult> GetById(string id) =>
        ExecuteAsync(() => _qrActivationService.GetByIdAsync(id), "Lấy chi tiết mã kích hoạt QR thành công");

    [Authorize(Roles = SharedConstants.UserRoles.Admin)]
    [HttpPost($"{AppConstants.ApiVersionPrefix}/admin/qr-activations")]
    public Task<IActionResult> Create([FromBody] CreateQrActivationRequest request) =>
        ExecuteAsync(() => _qrActivationService.CreateAsync(request), "Tạo mã kích hoạt QR thành công");

    [Authorize(Roles = SharedConstants.UserRoles.Admin)]
    [HttpPut($"{AppConstants.ApiVersionPrefix}/admin/qr-activations/{{id}}")]
    public Task<IActionResult> Update(string id, [FromBody] UpdateQrActivationRequest request) =>
        ExecuteAsync(() => _qrActivationService.UpdateAsync(id, request), "Cập nhật mã kích hoạt QR thành công");

    [Authorize(Roles = SharedConstants.UserRoles.Admin)]
    [HttpDelete($"{AppConstants.ApiVersionPrefix}/admin/qr-activations/{{id}}")]
    public Task<IActionResult> Delete(string id) =>
        ExecuteAsync(() => _qrActivationService.DeleteAsync(id), "Xóa mã kích hoạt QR thành công");
}

