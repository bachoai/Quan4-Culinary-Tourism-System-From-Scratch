using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Quan4CulinaryTourism.Api.DTOs;
using Quan4CulinaryTourism.Api.Repositories;

namespace Quan4CulinaryTourism.Api.Services;

public class MapsService
{
    private const string DefaultEntryFile = "index.html";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly MapPackRepository _mapPackRepository;
    private readonly PoiRepository _poiRepository;

    public MapsService(MapPackRepository mapPackRepository, PoiRepository poiRepository)
    {
        _mapPackRepository = mapPackRepository;
        _poiRepository = poiRepository;
    }

    public async Task<MapPackResponse?> GetPackManifestAsync(CancellationToken cancellationToken = default)
    {
        var pack = await _mapPackRepository.GetActiveAsync(cancellationToken);
        if (pack is null)
        {
            return null;
        }

        return new MapPackResponse
        {
            Id = pack.Id,
            Version = pack.Version,
            Name = pack.Name,
            DownloadUrl = ResolveDownloadUrl(pack),
            EntryFile = DefaultEntryFile,
            Sha256 = pack.Sha256,
            SizeBytes = pack.SizeBytes,
            IsActive = pack.IsActive,
            PublishedAt = pack.PublishedAt
        };
    }

    public async Task<(byte[] Content, string FileName)> CreateOfflinePackAsync(CancellationToken cancellationToken = default)
    {
        var pack = await _mapPackRepository.GetActiveAsync(cancellationToken) ?? new Models.MapPack
        {
            Version = "v1",
            Name = "Default Quan 4 Map Pack",
            IsActive = true,
            PublishedAt = DateTime.UtcNow
        };

        var archive = await BuildArchiveAsync(cancellationToken);
        return (archive, $"quan4-offline-map-{SanitizeFileName(pack.Version)}.zip");
    }

    private async Task<byte[]> BuildArchiveAsync(CancellationToken cancellationToken)
    {
        var pois = await _poiRepository.GetPublicPoisAsync(cancellationToken);
        var poiPayload = pois
            .Select(poi => new
            {
                poi.Id,
                poi.Name,
                poi.Description,
                poi.Address,
                poi.Ward,
                poi.District,
                poi.City,
                Latitude = poi.Location.Coordinates.Latitude,
                Longitude = poi.Location.Coordinates.Longitude,
                poi.Priority
            })
            .Where(static poi => poi.Latitude != 0 && poi.Longitude != 0)
            .OrderByDescending(static poi => poi.Priority)
            .ThenBy(static poi => poi.Name)
            .ToList();

        await using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            await WriteEntryAsync(archive, DefaultEntryFile, BuildTemplateHtml(), cancellationToken);
            await WriteEntryAsync(
                archive,
                "pois.json",
                JsonSerializer.Serialize(poiPayload, JsonOptions),
                cancellationToken);
            await WriteEntryAsync(
                archive,
                "metadata.json",
                JsonSerializer.Serialize(new
                {
                    generatedAtUtc = DateTime.UtcNow,
                    entryFile = DefaultEntryFile,
                    poiCount = poiPayload.Count
                }, JsonOptions),
                cancellationToken);
        }

        return stream.ToArray();
    }

    private static async Task WriteEntryAsync(ZipArchive archive, string entryName, string content, CancellationToken cancellationToken)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var entryStream = entry.Open();
        await using var writer = new StreamWriter(entryStream, Encoding.UTF8);
        await writer.WriteAsync(content.AsMemory(), cancellationToken);
        await writer.FlushAsync(cancellationToken);
    }

    private static string ResolveDownloadUrl(Models.MapPack pack)
    {
        if (!string.IsNullOrWhiteSpace(pack.DownloadUrl))
        {
            return pack.DownloadUrl;
        }

        return $"/api/v1/maps/offline-pack?version={Uri.EscapeDataString(pack.Version)}";
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            builder.Append(invalid.Contains(character) ? '-' : character);
        }

        return builder.ToString();
    }

    private static string BuildTemplateHtml() =>
        """
<!doctype html>
<html lang="vi">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Quan 4 Offline Map</title>
  <style>
    :root { color-scheme: light; }
    * { box-sizing: border-box; }
    body {
      margin: 0;
      font-family: "Segoe UI", sans-serif;
      background: linear-gradient(180deg, #fdf4e7 0%, #fffdf8 100%);
      color: #1f2937;
    }
    .layout {
      display: grid;
      grid-template-rows: auto 1fr auto;
      min-height: 100vh;
    }
    header {
      padding: 14px 16px 10px;
      border-bottom: 1px solid rgba(251, 146, 60, 0.28);
      background: rgba(255, 255, 255, 0.92);
      backdrop-filter: blur(10px);
    }
    header h1 {
      margin: 0;
      font-size: 18px;
      font-weight: 700;
    }
    header p {
      margin: 6px 0 0;
      color: #6b7280;
      font-size: 12px;
    }
    .map-shell {
      position: relative;
      margin: 12px;
      border-radius: 20px;
      overflow: hidden;
      border: 1px solid rgba(148, 163, 184, 0.28);
      background:
        radial-gradient(circle at top left, rgba(251, 146, 60, 0.16), transparent 34%),
        radial-gradient(circle at bottom right, rgba(45, 212, 191, 0.18), transparent 32%),
        linear-gradient(135deg, #eff6ff 0%, #fff7ed 100%);
      box-shadow: 0 22px 48px rgba(15, 23, 42, 0.12);
    }
    .map-shell::before {
      content: "";
      position: absolute;
      inset: 0;
      background-image:
        linear-gradient(rgba(148, 163, 184, 0.10) 1px, transparent 1px),
        linear-gradient(90deg, rgba(148, 163, 184, 0.10) 1px, transparent 1px);
      background-size: 36px 36px;
      pointer-events: none;
    }
    svg {
      display: block;
      width: 100%;
      height: min(66vh, 420px);
    }
    .river {
      fill: none;
      stroke: rgba(59, 130, 246, 0.32);
      stroke-width: 20;
      stroke-linecap: round;
    }
    .district {
      fill: rgba(255, 255, 255, 0.56);
      stroke: rgba(251, 146, 60, 0.42);
      stroke-width: 3;
    }
    .poi {
      cursor: pointer;
      transition: transform 140ms ease;
    }
    .poi circle {
      fill: #f97316;
      stroke: #fff;
      stroke-width: 3;
      filter: drop-shadow(0 6px 10px rgba(249, 115, 22, 0.3));
    }
    .poi:hover { transform: scale(1.05); }
    .poi.active circle { fill: #0f766e; }
    .user circle {
      fill: #2563eb;
      stroke: #fff;
      stroke-width: 3;
    }
    .user .pulse {
      fill: rgba(37, 99, 235, 0.18);
      stroke: none;
    }
    .tag {
      position: absolute;
      padding: 6px 10px;
      border-radius: 999px;
      background: rgba(255, 255, 255, 0.92);
      border: 1px solid rgba(251, 146, 60, 0.24);
      box-shadow: 0 12px 24px rgba(15, 23, 42, 0.08);
      font-size: 12px;
      font-weight: 600;
    }
    .tag.q4 { left: 16px; bottom: 16px; }
    .tag.user { right: 16px; top: 16px; color: #2563eb; }
    .detail {
      padding: 14px 16px 18px;
      background: rgba(255, 255, 255, 0.96);
      border-top: 1px solid rgba(148, 163, 184, 0.18);
    }
    .detail h2 {
      margin: 0;
      font-size: 16px;
    }
    .detail p {
      margin: 6px 0 0;
      font-size: 13px;
      line-height: 1.5;
      color: #4b5563;
    }
    .muted {
      color: #6b7280;
    }
  </style>
</head>
<body>
  <div class="layout">
    <header>
      <h1>Bản đồ offline Quận 4</h1>
      <p>Pack: __PACK_NAME__</p>
    </header>
    <section class="map-shell">
      <div class="tag q4">Quan 4 offline</div>
      <div class="tag user" id="userTag" hidden>Vi tri cua ban</div>
      <svg id="map" viewBox="0 0 1000 700" preserveAspectRatio="xMidYMid meet" aria-label="Offline map">
        <path class="river" d="M80 140 C 260 60, 460 80, 640 190 C 770 270, 860 360, 930 520" />
        <path class="district" d="M160 140 C 330 120, 470 130, 620 220 C 730 285, 790 360, 820 470 C 742 564, 612 620, 466 616 C 312 610, 198 548, 132 444 C 110 326, 118 226, 160 140 Z" />
      </svg>
    </section>
    <section class="detail" id="detail">
      <h2>Chưa chọn điểm POI</h2>
      <p class="muted">Chọn marker trong bản đồ để xem thông tin tóm tắt.</p>
    </section>
  </div>
  <script>
    const pois = __POI_DATA__;
    const userLocation = __USER_LOCATION__;
    const svg = document.getElementById("map");
    const detail = document.getElementById("detail");
    const userTag = document.getElementById("userTag");
    const width = 1000;
    const height = 700;
    const padding = 96;

    function getBounds(items) {
      const points = items.filter(item => Number.isFinite(item.latitude) && Number.isFinite(item.longitude));
      if (!points.length) {
        return { minLat: 10.75, maxLat: 10.765, minLng: 106.698, maxLng: 106.71 };
      }

      return {
        minLat: Math.min(...points.map(item => item.latitude)),
        maxLat: Math.max(...points.map(item => item.latitude)),
        minLng: Math.min(...points.map(item => item.longitude)),
        maxLng: Math.max(...points.map(item => item.longitude))
      };
    }

    function project(latitude, longitude, bounds) {
      const lngRange = Math.max(bounds.maxLng - bounds.minLng, 0.001);
      const latRange = Math.max(bounds.maxLat - bounds.minLat, 0.001);
      const x = padding + ((longitude - bounds.minLng) / lngRange) * (width - padding * 2);
      const y = height - padding - ((latitude - bounds.minLat) / latRange) * (height - padding * 2);
      return { x, y };
    }

    function setActive(poi) {
      document.querySelectorAll(".poi").forEach(node => node.classList.toggle("active", node.dataset.id === poi.id));
      detail.innerHTML =
        "<h2>" + poi.name + "</h2>" +
        "<p>" + (poi.description || "Không có mô tả.") + "</p>" +
        "<p><strong>Địa chỉ:</strong> " + poi.displayAddress + "</p>";
    }

    const normalizedPois = Array.isArray(pois) ? pois : [];
    const userPayload = userLocation && Number.isFinite(userLocation.latitude) && Number.isFinite(userLocation.longitude)
      ? [{ latitude: userLocation.latitude, longitude: userLocation.longitude }]
      : [];
    const bounds = getBounds(normalizedPois.concat(userPayload));

    normalizedPois.forEach(poi => {
      const point = project(poi.latitude, poi.longitude, bounds);
      const group = document.createElementNS("http://www.w3.org/2000/svg", "g");
      group.setAttribute("class", "poi");
      group.dataset.id = poi.id;

      const circle = document.createElementNS("http://www.w3.org/2000/svg", "circle");
      circle.setAttribute("cx", point.x);
      circle.setAttribute("cy", point.y);
      circle.setAttribute("r", "14");
      group.appendChild(circle);

      const label = document.createElementNS("http://www.w3.org/2000/svg", "text");
      label.setAttribute("x", point.x + 18);
      label.setAttribute("y", point.y + 4);
      label.setAttribute("font-size", "14");
      label.setAttribute("font-weight", "700");
      label.setAttribute("fill", "#111827");
      label.textContent = poi.name;
      group.appendChild(label);

      group.addEventListener("click", () => setActive(poi));
      svg.appendChild(group);
    });

    if (userPayload.length) {
      const point = project(userLocation.latitude, userLocation.longitude, bounds);
      const pulse = document.createElementNS("http://www.w3.org/2000/svg", "circle");
      pulse.setAttribute("class", "pulse");
      pulse.setAttribute("cx", point.x);
      pulse.setAttribute("cy", point.y);
      pulse.setAttribute("r", "28");

      const marker = document.createElementNS("http://www.w3.org/2000/svg", "g");
      marker.setAttribute("class", "user");

      const dot = document.createElementNS("http://www.w3.org/2000/svg", "circle");
      dot.setAttribute("cx", point.x);
      dot.setAttribute("cy", point.y);
      dot.setAttribute("r", "12");

      marker.appendChild(pulse);
      marker.appendChild(dot);
      svg.appendChild(marker);
      userTag.hidden = false;
    }

    if (normalizedPois.length) {
      setActive(normalizedPois[0]);
    }
  </script>
</body>
</html>
""";
}

