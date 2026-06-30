using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Quan4CulinaryTourism.Api.Common;
using Quan4CulinaryTourism.Api.DTOs;
using Quan4CulinaryTourism.Api.Services;

namespace Quan4CulinaryTourism.Api.Controllers;

[Route($"{AppConstants.ApiVersionPrefix}/chat")]
public class ChatController : BaseApiController
{
    private readonly ChatService _chatService;

    public ChatController(ChatService chatService)
    {
        _chatService = chatService;
    }

    [Authorize]
    [HttpPost("suggest")]
    public Task<IActionResult> Suggest([FromBody] ChatSuggestRequest request) =>
        ExecuteAsync(() => _chatService.SuggestAsync(request, HttpContext.RequestAborted), "Gợi ý thành công");
}
