using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quan4CulinaryTourism.Api.Common;
using Quan4CulinaryTourism.Api.DTOs;
using Quan4CulinaryTourism.Api.Models;
using Quan4CulinaryTourism.Api.Services;

namespace Quan4CulinaryTourism.Api.Controllers;

public class AnalyticsController : BaseApiController
{
    private readonly AnalyticsService _analyticsService;
    public AnalyticsController(AnalyticsService analyticsService) => _analyticsService = analyticsService;

    [HttpPost($"{AppConstants.ApiVersionPrefix}/analytics/collect")]
    public Task<IActionResult> Collect([FromBody] CollectAnalyticsRequest request) => ExecuteAsync(() => _analyticsService.CollectAsync(request), "Thu thập analytics thành công");

    [Authorize(Roles = SharedConstants.UserRoles.Admin)]
    [HttpGet($"{AppConstants.ApiVersionPrefix}/admin/analytics/summary")]
    public Task<IActionResult> Summary() => ExecuteAsync(() => _analyticsService.GetSummaryAsync(), "Lấy analytics summary thành công");

    [Authorize(Roles = SharedConstants.UserRoles.Admin)]
    [HttpGet($"{AppConstants.ApiVersionPrefix}/admin/analytics/history")]
    public Task<IActionResult> History([FromQuery] UsageHistoryRequest request) =>
        ExecuteAsync(() => _analyticsService.GetUsageHistoryAsync(request), "Lấy lịch sử sử dụng thành công");
}
