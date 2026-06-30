using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quan4CulinaryTourism.Api.Common;
using Quan4CulinaryTourism.Api.DTOs;
using Quan4CulinaryTourism.Api.Models;
using Quan4CulinaryTourism.Api.Services;

namespace Quan4CulinaryTourism.Api.Controllers;

[Authorize(Roles = SharedConstants.UserRoles.Admin)]
public class LocalizationController : BaseApiController
{
    private readonly LocalizationService _service;

    public LocalizationController(LocalizationService service) => _service = service;

    [HttpGet($"{AppConstants.ApiVersionPrefix}/admin/pois/{{id}}/localizations")]
    public Task<IActionResult> GetLocalizations(string id) =>
        ExecuteAsync(() => _service.GetPoiLocalizationsAsync(id), "Lay localizations thanh cong");

    [HttpPost($"{AppConstants.ApiVersionPrefix}/admin/pois/{{id}}/localizations")]
    public Task<IActionResult> Create(string id, [FromBody] CreatePoiLocalizationRequest request) =>
        ExecuteAsync(() => _service.CreateAsync(id, request), "Tao localization thanh cong");

    [HttpPut($"{AppConstants.ApiVersionPrefix}/admin/pois/{{id}}/localizations/{{lang}}")]
    public Task<IActionResult> Update(string id, string lang, [FromBody] UpdatePoiLocalizationRequest request) =>
        ExecuteAsync(() => _service.UpdateAsync(id, lang, request), "Cap nhat localization thanh cong");

    [HttpPost($"{AppConstants.ApiVersionPrefix}/admin/pois/{{id}}/localizations/translate")]
    public Task<IActionResult> Translate(string id, [FromBody] TranslatePoiLocalizationRequest request) =>
        ExecuteAsync(() => _service.TranslateAsync(id, request), "Dich localization thanh cong");

    [HttpDelete($"{AppConstants.ApiVersionPrefix}/admin/pois/{{id}}/localizations/{{lang}}")]
    public Task<IActionResult> Delete(string id, string lang) =>
        ExecuteAsync(() => _service.DeleteAsync(id, lang), "Xoa localization thanh cong");
}
