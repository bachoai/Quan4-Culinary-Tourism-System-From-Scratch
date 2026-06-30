using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Quan4CulinaryTourism.Api.Common;
using Quan4CulinaryTourism.Api.DTOs;
using Quan4CulinaryTourism.Api.Helpers;
using Quan4CulinaryTourism.Api.Models;
using Quan4CulinaryTourism.Api.Repositories;

namespace Quan4CulinaryTourism.Api.Services;

public class ChatService
{
    private const int MaxUserMessageLength = 500;
    private const int MaxAiCandidates = 10;
    private static readonly JsonSerializerOptions CandidateJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "toi",
        "minh",
        "muon",
        "can",
        "goi",
        "y",
        "quan",
        "nao",
        "cho",
        "hom",
        "nay",
        "an",
        "uong",
        "di",
        "co",
        "khong",
        "gi",
        "va",
        "roi",
        "sau",
        "them",
        "giup",
        "voi",
        "mot",
        "vai",
        "chay",
        "o",
        "khu",
        "vuc"
    };

    private static readonly string[] NearbyHints =
    [
        "gan toi",
        "gan day",
        "near",
        "quanh day"
    ];

    private static readonly string[] CheapHints =
    [
        "gia re",
        "re",
        "tiet kiem",
        "sinh vien",
        "binh dan",
        "ngan sach"
    ];

    private static readonly string[] DateHints =
    [
        "date",
        "hen ho",
        "nguoi yeu",
        "lang man",
        "romantic"
    ];

    private static readonly string[] GroupHints =
    [
        "nhom",
        "ban be",
        "gia dinh",
        "dong nguoi",
        "team"
    ];

    private static readonly string[] FastHints =
    [
        "an nhanh",
        "nhanh",
        "gap",
        "mang di",
        "take away",
        "takeaway"
    ];

    private static readonly string[] NightHints =
    [
        "an dem",
        "toi muon",
        "khuya",
        "dem"
    ];

    private static readonly string[] CafeHints =
    [
        "cafe",
        "ca phe",
        "coffee",
        "tra sua"
    ];

    private static readonly string[] DateAmbienceHints =
    [
        "cafe",
        "ca phe",
        "coffee",
        "lang man",
        "yen tinh",
        "view",
        "decor",
        "song ao"
    ];

    private static readonly string[] GroupFriendlyHints =
    [
        "nhom",
        "gia dinh",
        "ban be",
        "rong",
        "hop mat",
        "lau",
        "nuong"
    ];

    private static readonly string[] FastServiceHints =
    [
        "nhanh",
        "take away",
        "takeaway",
        "mang di",
        "an vat"
    ];

    private static readonly string[] NightFriendlyHints =
    [
        "dem",
        "khuya",
        "oc",
        "lau",
        "nuong",
        "hai san",
        "an vat"
    ];

    private static readonly string[] BeverageHints =
    [
        "cafe",
        "ca phe",
        "coffee",
        "tra sua",
        "nuoc"
    ];

    private const string SystemPrompt = """
Bạn là trợ lý gợi ý địa điểm ăn uống cho hệ thống Phố Ẩm Thực Vĩnh Khánh.

Luật bắt buộc:
- Chỉ được gợi ý địa điểm nằm trong danh sách POI_CANDIDATES.
- Không được tự bịa tên quán, địa chỉ, giá, khoảng cách hoặc thông tin không có trong dữ liệu.
- Nếu không có địa điểm phù hợp, hãy nói rõ là chưa tìm thấy dữ liệu phù hợp.
- Trả lời bằng tiếng Việt tự nhiên, thân thiện, ngắn gọn.
- Ưu tiên đúng nhu cầu người dùng: món ăn, ngân sách, khoảng cách, đi date, đi nhóm, ăn nhanh, cafe, ăn đêm.
- Nếu người dùng hỏi "gần tôi" nhưng không có vị trí, hãy nhắc người dùng bật vị trí hoặc nói rõ khu vực.
- Nếu dữ liệu chỉ có mức giá dạng $, $$, $$$ thì không được suy diễn thành giá tiền cụ thể.
- Output bắt buộc là JSON hợp lệ, không markdown, không giải thích ngoài JSON.

JSON schema:
{
  "reply": "string",
  "suggestions": [
    {
      "poiId": "string",
      "reason": "string"
    }
  ]
}
""";

    private readonly PoiRepository _poiRepository;
    private readonly CategoryRepository _categoryRepository;
    private readonly AiChatClient _aiChatClient;
    private readonly DistanceHelper _distanceHelper;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        PoiRepository poiRepository,
        CategoryRepository categoryRepository,
        AiChatClient aiChatClient,
        DistanceHelper distanceHelper,
        ILogger<ChatService> logger)
    {
        _poiRepository = poiRepository;
        _categoryRepository = categoryRepository;
        _aiChatClient = aiChatClient;
        _distanceHelper = distanceHelper;
        _logger = logger;
    }

    public async Task<ChatSuggestResponse> SuggestAsync(
        ChatSuggestRequest request,
        CancellationToken cancellationToken = default)
    {
        var message = request.Message?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ApiException("Bạn hãy nhập câu hỏi để mình gợi ý địa điểm phù hợp nhé.");
        }

        if (message.Length > MaxUserMessageLength)
        {
            return new ChatSuggestResponse
            {
                Reply = "Tin nhắn của bạn hơi dài. Bạn thử rút gọn nhu cầu chính như món ăn, ngân sách hoặc khu vực để mình gợi ý sát hơn nhé.",
                Suggestions = []
            };
        }

        var intent = ParseIntent(message);
        var pois = await _poiRepository.GetPublicPoisAsync(cancellationToken);
        if (pois.Count == 0)
        {
            return new ChatSuggestResponse
            {
                Reply = "Hiện mình chưa có dữ liệu POI phù hợp trong hệ thống để gợi ý cho bạn.",
                Suggestions = []
            };
        }

        var categoryLookup = (await _categoryRepository.GetAllActiveAsync(cancellationToken))
            .ToDictionary(category => category.Id, category => category.Name, StringComparer.Ordinal);

        var rankedCandidates = pois
            .Select(poi => RankCandidate(poi, categoryLookup, intent, request))
            .OrderByDescending(candidate => candidate.SignalScore)
            .ThenByDescending(candidate => candidate.TotalScore)
            .ThenBy(candidate => intent.WantsNearby && candidate.DistanceMeters.HasValue ? candidate.DistanceMeters.Value : double.MaxValue)
            .ThenByDescending(candidate => candidate.Poi.Priority)
            .ThenByDescending(candidate => candidate.Poi.Rating)
            .ToList();

        var aiCandidates = BuildAiShortlist(rankedCandidates, intent);
        if (aiCandidates.Count == 0)
        {
            return BuildNoDataResponse(intent, request);
        }

        var aiResponse = await TryBuildAiResponseAsync(message, request, aiCandidates, cancellationToken);
        if (aiResponse is not null)
        {
            return aiResponse;
        }

        return BuildFallbackResponse(intent, request, aiCandidates);
    }

    private async Task<ChatSuggestResponse?> TryBuildAiResponseAsync(
        string message,
        ChatSuggestRequest request,
        IReadOnlyList<RankedPoiCandidate> aiCandidates,
        CancellationToken cancellationToken)
    {
        if (!_aiChatClient.CanUseAi())
        {
            return null;
        }

        string? rawResponse;
        try
        {
            rawResponse = await _aiChatClient.GenerateReplyAsync(
                SystemPrompt,
                BuildUserPrompt(message, request, aiCandidates.Select(candidate => candidate.Candidate).ToList()),
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Chat AI call failed and will fall back to rule-based suggestions.");
            return null;
        }

        var parsed = ParseAiPayload(rawResponse);
        if (parsed is null)
        {
            return null;
        }

        var suggestions = ValidateAiSuggestions(parsed, aiCandidates);
        if (suggestions.Count == 0)
        {
            return null;
        }

        var reply = string.IsNullOrWhiteSpace(parsed.Reply)
            ? ComposeFallbackReply(
                ParseIntent(message),
                request)
            : parsed.Reply.Trim();

        return new ChatSuggestResponse
        {
            Reply = reply,
            Suggestions = suggestions
        };
    }

    private static List<RankedPoiCandidate> BuildAiShortlist(
        IReadOnlyList<RankedPoiCandidate> rankedCandidates,
        ChatIntent intent)
    {
        var relevant = rankedCandidates
            .Where(candidate => candidate.SignalScore > 0)
            .Take(MaxAiCandidates)
            .ToList();

        if (relevant.Count > 0)
        {
            return relevant;
        }

        if (intent.HasExplicitPreferences)
        {
            return [];
        }

        return rankedCandidates.Take(Math.Min(MaxAiCandidates, rankedCandidates.Count)).ToList();
    }

    private RankedPoiCandidate RankCandidate(
        Poi poi,
        IReadOnlyDictionary<string, string> categoryLookup,
        ChatIntent intent,
        ChatSuggestRequest request)
    {
        categoryLookup.TryGetValue(poi.CategoryId, out var categoryName);
        var searchableText = BuildSearchableText(poi, categoryName);
        var nameText = NormalizeForSearch(poi.Name);
        var categoryText = NormalizeForSearch(categoryName);
        var tagText = NormalizeForSearch(string.Join(' ', poi.Tags));
        var distanceMeters = TryCalculateDistanceMeters(request, poi);
        var highlights = new List<string>();
        double signalScore = 0;
        double qualityBonus = 0;

        foreach (var keyword in intent.Keywords)
        {
            if (nameText.Contains(keyword, StringComparison.Ordinal) ||
                categoryText.Contains(keyword, StringComparison.Ordinal))
            {
                signalScore += 8;
                AddHighlightOnce(highlights, "đúng món hoặc kiểu quán bạn đang tìm");
                continue;
            }

            if (tagText.Contains(keyword, StringComparison.Ordinal))
            {
                signalScore += 6;
                AddHighlightOnce(highlights, "có tag khá sát với nhu cầu của bạn");
                continue;
            }

            if (searchableText.Contains(keyword, StringComparison.Ordinal))
            {
                signalScore += 4;
                AddHighlightOnce(highlights, "mô tả hoặc địa chỉ có từ khóa gần với câu hỏi của bạn");
            }
        }

        if (intent.WantsCheap)
        {
            if (poi.PriceRange == "$")
            {
                signalScore += 10;
                AddHighlightOnce(highlights, "mức giá nhẹ trong dữ liệu hiện có");
            }
            else if (poi.PriceRange == "$$")
            {
                signalScore += 5;
                AddHighlightOnce(highlights, "mức giá tương đối dễ tiếp cận");
            }
        }

        if (intent.WantsDate && (ContainsAny(searchableText, DateAmbienceHints) || ContainsAny(categoryText, BeverageHints)))
        {
            signalScore += 8;
            AddHighlightOnce(highlights, "không gian hoặc loại hình khá hợp đi hẹn hò");
        }

        if (intent.WantsGroup && ContainsAny(searchableText, GroupFriendlyHints))
        {
            signalScore += 7;
            AddHighlightOnce(highlights, "phù hợp đi nhóm hoặc đi cùng bạn bè");
        }

        if (intent.WantsFast && (ContainsAny(searchableText, FastServiceHints) || poi.PriceRange == "$"))
        {
            signalScore += 6;
            AddHighlightOnce(highlights, "hợp nhu cầu ăn nhanh và gọn");
        }

        if (intent.WantsNight && ContainsAny(searchableText, NightFriendlyHints))
        {
            signalScore += 5;
            AddHighlightOnce(highlights, "khá hợp khung giờ tối hoặc ăn đêm");
        }

        if (intent.WantsCafe && ContainsAny(searchableText, BeverageHints))
        {
            signalScore += 8;
            AddHighlightOnce(highlights, "đúng nhóm đồ uống hoặc quán cà phê bạn đang tìm");
        }

        if (distanceMeters.HasValue)
        {
            if (intent.WantsNearby)
            {
                if (distanceMeters.Value <= 500)
                {
                    signalScore += 18;
                }
                else if (distanceMeters.Value <= 1500)
                {
                    signalScore += 12;
                }
                else if (distanceMeters.Value <= 3000)
                {
                    signalScore += 8;
                }
                else if (distanceMeters.Value <= 5000)
                {
                    signalScore += 4;
                }

                AddHighlightOnce(highlights, "ở tương đối gần vị trí của bạn");
            }
            else if (distanceMeters.Value <= 1000)
            {
                signalScore += 3;
            }
        }

        if (poi.Images.Count > 0)
        {
            qualityBonus += 1;
        }

        if (HasRouteableLocation(poi) || !string.IsNullOrWhiteSpace(poi.MapUrl))
        {
            qualityBonus += 1;
        }

        if (!string.IsNullOrWhiteSpace(poi.TtsScript))
        {
            qualityBonus += 1;
        }

        var baselineScore = Math.Clamp(poi.Priority, 0, 20) * 0.5
            + Math.Clamp(poi.Rating, 0, 5) * 0.8
            + Math.Min(poi.ReviewCount, 50) * 0.05;

        return new RankedPoiCandidate(
            poi,
            BuildAiCandidate(poi, categoryName, distanceMeters),
            signalScore,
            baselineScore + signalScore + qualityBonus,
            distanceMeters,
            highlights);
    }

    private static AiPoiCandidate BuildAiCandidate(Poi poi, string? categoryName, double? distanceMeters) => new()
    {
        Id = poi.Id,
        Name = poi.Name,
        CategoryName = string.IsNullOrWhiteSpace(categoryName) ? null : categoryName,
        Address = string.IsNullOrWhiteSpace(poi.Address) ? null : poi.Address,
        Ward = string.IsNullOrWhiteSpace(poi.Ward) ? null : poi.Ward,
        Tags = poi.Tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList(),
        Description = Truncate(NormalizeWhitespace(poi.Description), 240),
        DistanceMeters = distanceMeters.HasValue ? Math.Round(distanceMeters.Value, 2) : null,
        PriceHint = string.IsNullOrWhiteSpace(poi.PriceRange) ? null : poi.PriceRange
    };

    private static ChatIntent ParseIntent(string message)
    {
        var normalizedMessage = NormalizeForSearch(message);
        var wantsNearby = ContainsAny(normalizedMessage, NearbyHints);
        var wantsCheap = ContainsAny(normalizedMessage, CheapHints) || Regex.IsMatch(normalizedMessage, @"\b\d{2,4}\s?k\b");
        var wantsDate = ContainsAny(normalizedMessage, DateHints);
        var wantsGroup = ContainsAny(normalizedMessage, GroupHints);
        var wantsFast = ContainsAny(normalizedMessage, FastHints);
        var wantsNight = ContainsAny(normalizedMessage, NightHints);
        var wantsCafe = ContainsAny(normalizedMessage, CafeHints);

        var keywords = normalizedMessage
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 2)
            .Where(token => !StopWords.Contains(token))
            .Where(token => !Regex.IsMatch(token, @"^\d+$"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var hasExplicitPreferences = wantsNearby ||
            wantsCheap ||
            wantsDate ||
            wantsGroup ||
            wantsFast ||
            wantsNight ||
            wantsCafe ||
            keywords.Count > 0;

        return new ChatIntent(
            normalizedMessage,
            wantsNearby,
            wantsCheap,
            wantsDate,
            wantsGroup,
            wantsFast,
            wantsNight,
            wantsCafe,
            hasExplicitPreferences,
            keywords);
    }

    private static ChatSuggestResponse BuildNoDataResponse(ChatIntent intent, ChatSuggestRequest request)
    {
        var builder = new List<string>();
        if (intent.WantsNearby && !HasValidUserLocation(request))
        {
            builder.Add("Bạn đang hỏi quán gần mình nhưng hiện chưa có vị trí.");
            builder.Add("Bạn có thể bật vị trí hoặc nói rõ khu vực để mình lọc sát hơn.");
        }
        else
        {
            builder.Add("Mình chưa tìm thấy địa điểm phù hợp trong dữ liệu hiện có.");
        }

        return new ChatSuggestResponse
        {
            Reply = string.Join(' ', builder),
            Suggestions = []
        };
    }

    private static ChatSuggestResponse BuildFallbackResponse(
        ChatIntent intent,
        ChatSuggestRequest request,
        IReadOnlyList<RankedPoiCandidate> aiCandidates)
    {
        var suggestions = aiCandidates
            .Take(Math.Min(3, aiCandidates.Count))
            .Select(candidate => ToSuggestion(candidate, null))
            .ToList();

        return new ChatSuggestResponse
        {
            Reply = ComposeFallbackReply(intent, request),
            Suggestions = suggestions
        };
    }

    private static string ComposeFallbackReply(
        ChatIntent intent,
        ChatSuggestRequest request)
    {
        var messages = new List<string>();
        if (intent.WantsNearby && !HasValidUserLocation(request))
        {
            messages.Add("Nếu bạn muốn lọc chính xác theo khoảng cách, hãy bật vị trí hoặc nói rõ khu vực nhé.");
        }

        if (intent.WantsCheap)
        {
            messages.Add("Dữ liệu hiện tại chưa có giá chi tiết theo món, nên mình ưu tiên các địa điểm có mức giá tương đối nhẹ trong hệ thống.");
        }

        messages.Add("Mình tìm được vài địa điểm phù hợp trong dữ liệu hiện có. Bạn xem thử các gợi ý dưới đây nhé.");
        return string.Join(' ', messages);
    }

    private static List<ChatPoiSuggestionResponse> ValidateAiSuggestions(
        AiChatSuggestionPayload payload,
        IReadOnlyList<RankedPoiCandidate> aiCandidates)
    {
        var candidateLookup = aiCandidates.ToDictionary(candidate => candidate.Poi.Id, StringComparer.Ordinal);
        var seenPoiIds = new HashSet<string>(StringComparer.Ordinal);
        var suggestions = new List<ChatPoiSuggestionResponse>();

        foreach (var item in payload.Suggestions)
        {
            var poiId = item.PoiId?.Trim();
            if (string.IsNullOrWhiteSpace(poiId) ||
                !seenPoiIds.Add(poiId) ||
                !candidateLookup.TryGetValue(poiId, out var candidate))
            {
                continue;
            }

            suggestions.Add(ToSuggestion(candidate, item.Reason));
            if (suggestions.Count == 5)
            {
                break;
            }
        }

        return suggestions;
    }

    private static ChatPoiSuggestionResponse ToSuggestion(RankedPoiCandidate candidate, string? reason) => new()
    {
        PoiId = candidate.Poi.Id,
        Name = candidate.Poi.Name,
        Address = string.IsNullOrWhiteSpace(candidate.Poi.Address) ? null : candidate.Poi.Address,
        Ward = string.IsNullOrWhiteSpace(candidate.Poi.Ward) ? null : candidate.Poi.Ward,
        ImageUrl = candidate.Poi.Images.FirstOrDefault(image => image.IsThumbnail)?.Url
            ?? candidate.Poi.Images.FirstOrDefault()?.Url,
        Reason = string.IsNullOrWhiteSpace(reason) ? BuildFallbackReason(candidate) : reason.Trim(),
        DistanceMeters = candidate.DistanceMeters.HasValue ? Math.Round(candidate.DistanceMeters.Value, 2) : null,
        DetailUrl = $"/poi/{Uri.EscapeDataString(candidate.Poi.Id)}",
        MapUrl = HasRouteableLocation(candidate.Poi)
            ? $"/map?poi={Uri.EscapeDataString(candidate.Poi.Id)}"
            : candidate.Poi.MapUrl
    };

    private static string BuildFallbackReason(RankedPoiCandidate candidate)
    {
        var reasons = candidate.Highlights.Take(2).ToList();
        if (reasons.Count == 0)
        {
            reasons.Add("đang được ưu tiên trong dữ liệu hiện có");
        }

        if (reasons.Count == 1 && !string.IsNullOrWhiteSpace(candidate.Candidate.PriceHint))
        {
            reasons.Add($"mức giá tham chiếu {candidate.Candidate.PriceHint}");
        }

        return string.Join(", ", reasons);
    }

    private static AiChatSuggestionPayload? ParseAiPayload(string? rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse))
        {
            return null;
        }

        var json = ExtractJsonObject(rawResponse);
        if (json is null)
        {
            return null;
        }

        try
        {
            var payload = JsonSerializer.Deserialize<AiChatSuggestionPayload>(json, CandidateJsonOptions);
            if (payload is null)
            {
                return null;
            }

            payload.Reply = payload.Reply?.Trim() ?? string.Empty;
            payload.Suggestions ??= [];
            return payload;
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractJsonObject(string rawResponse)
    {
        var start = rawResponse.IndexOf('{');
        var end = rawResponse.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        return rawResponse[start..(end + 1)];
    }

    private static string BuildUserPrompt(
        string message,
        ChatSuggestRequest request,
        IReadOnlyList<AiPoiCandidate> candidates)
    {
        var locationText = HasValidUserLocation(request)
            ? $"latitude={request.Latitude!.Value.ToString(CultureInfo.InvariantCulture)}, longitude={request.Longitude!.Value.ToString(CultureInfo.InvariantCulture)}"
            : "not_provided";

        var builder = new StringBuilder();
        builder.AppendLine("USER_MESSAGE:");
        builder.AppendLine(message.Trim());
        builder.AppendLine();
        builder.AppendLine("USER_LOCATION:");
        builder.AppendLine(locationText);
        builder.AppendLine();
        builder.AppendLine("POI_CANDIDATES:");
        builder.AppendLine(JsonSerializer.Serialize(candidates, CandidateJsonOptions));
        builder.AppendLine();
        builder.AppendLine("Hay chon toi da 5 POI phu hop nhat.");
        return builder.ToString();
    }

    private static string BuildSearchableText(Poi poi, string? categoryName)
    {
        var builder = new StringBuilder();
        builder.Append(poi.Name).Append(' ');
        builder.Append(categoryName).Append(' ');
        builder.Append(poi.Description).Append(' ');
        builder.Append(poi.Address).Append(' ');
        builder.Append(poi.Ward).Append(' ');
        builder.Append(poi.District).Append(' ');
        builder.Append(poi.City).Append(' ');
        builder.Append(string.Join(' ', poi.Tags)).Append(' ');
        builder.Append(poi.TtsScript);
        return NormalizeForSearch(builder.ToString());
    }

    private double? TryCalculateDistanceMeters(ChatSuggestRequest request, Poi poi)
    {
        if (!HasValidUserLocation(request) || !HasRouteableLocation(poi))
        {
            return null;
        }

        return _distanceHelper.CalculateDistanceMeters(
            request.Latitude!.Value,
            request.Longitude!.Value,
            poi.Location.Coordinates.Latitude,
            poi.Location.Coordinates.Longitude);
    }

    private static bool HasValidUserLocation(ChatSuggestRequest request) =>
        request.Latitude.HasValue &&
        request.Longitude.HasValue &&
        request.Latitude.Value is >= -90 and <= 90 &&
        request.Longitude.Value is >= -180 and <= 180;

    private static bool HasRouteableLocation(Poi poi)
    {
        var latitude = poi.Location.Coordinates.Latitude;
        var longitude = poi.Location.Coordinates.Longitude;
        return latitude is >= -90 and <= 90 &&
            longitude is >= -180 and <= 180 &&
            (Math.Abs(latitude) > 0.000001 || Math.Abs(longitude) > 0.000001);
    }

    private static bool ContainsAny(string text, IEnumerable<string> hints) =>
        hints.Any(hint => text.Contains(hint, StringComparison.Ordinal));

    private static string NormalizeForSearch(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var decomposed = input.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var previousWasSpace = false;

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var normalizedCharacter = char.ToLowerInvariant(character);
            if (char.IsLetterOrDigit(normalizedCharacter))
            {
                builder.Append(normalizedCharacter);
                previousWasSpace = false;
                continue;
            }

            if (!previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).Trim();
    }

    private static string NormalizeWhitespace(string? input) =>
        string.IsNullOrWhiteSpace(input)
            ? string.Empty
            : string.Join(' ', input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static string? Truncate(string? input, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        return input.Length <= maxLength
            ? input
            : input[..maxLength] + "...";
    }

    private static void AddHighlightOnce(ICollection<string> highlights, string value)
    {
        if (!highlights.Contains(value))
        {
            highlights.Add(value);
        }
    }

    private sealed record ChatIntent(
        string NormalizedMessage,
        bool WantsNearby,
        bool WantsCheap,
        bool WantsDate,
        bool WantsGroup,
        bool WantsFast,
        bool WantsNight,
        bool WantsCafe,
        bool HasExplicitPreferences,
        List<string> Keywords);

    private sealed record RankedPoiCandidate(
        Poi Poi,
        AiPoiCandidate Candidate,
        double SignalScore,
        double TotalScore,
        double? DistanceMeters,
        List<string> Highlights);
}
