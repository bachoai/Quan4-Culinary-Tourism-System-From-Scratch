using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Quan4CulinaryTourism.Api.Helpers;
using Quan4CulinaryTourism.Api.Models;

namespace Quan4CulinaryTourism.Api.Database;

public class DbSeeder
{
    private readonly MongoDbContext _context;
    private readonly DefaultAdminSettings _defaultAdmin;
    private readonly PasswordHasher _passwordHasher;

    public DbSeeder(MongoDbContext context, IOptions<DefaultAdminSettings> defaultAdmin, PasswordHasher passwordHasher)
    {
        _context = context;
        _defaultAdmin = defaultAdmin.Value;
        _passwordHasher = passwordHasher;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedRolesAsync(cancellationToken);
        await SeedAdminAsync(cancellationToken);
        await SeedCategoriesAsync(cancellationToken);
        await SeedUsersAsync(cancellationToken);
        await SeedPoisAsync(cancellationToken);
        await SeedPoiLocalizationsAsync(cancellationToken);
        await SeedPoiAudiosAsync(cancellationToken);
        await SeedAudioTasksAsync(cancellationToken);
        await SeedOwnerRegistrationsAsync(cancellationToken);
        await SeedOwnerSubmissionsAsync(cancellationToken);
        await SeedMediaFilesAsync(cancellationToken);
        await SeedAnalyticsEventsAsync(cancellationToken);
        await SeedAuditLogsAsync(cancellationToken);
        await SeedToursAsync(cancellationToken);
        await SeedQrActivationsAsync(cancellationToken);
        await SeedMapPacksAsync(cancellationToken);
    }

    private async Task SeedRolesAsync(CancellationToken cancellationToken)
    {
        var roles = new[]
        {
            new Role { Name = SharedConstants.UserRoles.Admin, Description = "System administrator" },
            new Role { Name = SharedConstants.UserRoles.Owner, Description = "Business owner" },
            new Role { Name = SharedConstants.UserRoles.User, Description = "End user" }
        };

        foreach (var role in roles)
        {
            var exists = await _context.Roles.Find(x => x.Name == role.Name).AnyAsync(cancellationToken);
            if (!exists)
            {
                await _context.Roles.InsertOneAsync(role, cancellationToken: cancellationToken);
            }
        }
    }

    private async Task SeedAdminAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_defaultAdmin.Password))
        {
            return;
        }

        var existing = await _context.Users.Find(x => x.Email == _defaultAdmin.Email).FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return;
        }

        var admin = new User
        {
            FullName = _defaultAdmin.FullName,
            Email = _defaultAdmin.Email.ToLowerInvariant(),
            PasswordHash = _passwordHasher.HashPassword(_defaultAdmin.Password),
            Roles = [SharedConstants.UserRoles.Admin, SharedConstants.UserRoles.User],
            OwnerStatus = SharedConstants.OwnerApproved,
            EmailVerified = true,
            IsActive = true
        };

        await _context.Users.InsertOneAsync(admin, cancellationToken: cancellationToken);
    }

    private async Task SeedCategoriesAsync(CancellationToken cancellationToken)
    {
        var existingCodes = await _context.Categories.Find(FilterDefinition<Category>.Empty)
            .Project(x => x.Code)
            .ToListAsync(cancellationToken);

        var missing = CategorySeeds
            .Where(seed => !existingCodes.Contains(seed.Code, StringComparer.OrdinalIgnoreCase))
            .Select(seed => new Category
            {
                Code = seed.Code,
                Name = seed.Name,
                Description = seed.Description,
                SortOrder = seed.SortOrder,
                IsActive = true
            })
            .ToList();

        if (missing.Count > 0)
        {
            await _context.Categories.InsertManyAsync(missing, cancellationToken: cancellationToken);
        }
    }

    private async Task SeedUsersAsync(CancellationToken cancellationToken)
    {
        var existingEmails = await _context.Users.Find(FilterDefinition<User>.Empty)
            .Project(x => x.Email)
            .ToListAsync(cancellationToken);

        var missing = DemoUsers
            .Where(seed => !existingEmails.Contains(seed.Email, StringComparer.OrdinalIgnoreCase))
            .Select(seed => new User
            {
                FullName = seed.FullName,
                Email = seed.Email,
                PasswordHash = _passwordHasher.HashPassword("Demo@123456"),
                PhoneNumber = seed.PhoneNumber,
                AvatarUrl = seed.AvatarUrl,
                Roles = seed.OwnerStatus == SharedConstants.OwnerApproved
                    ? [SharedConstants.UserRoles.Owner, SharedConstants.UserRoles.User]
                    : [SharedConstants.UserRoles.User],
                IsActive = true,
                EmailVerified = true,
                OwnerStatus = seed.OwnerStatus,
                LastLoginAt = DateTime.UtcNow.AddDays(-(seed.Index % 10)),
                CreatedAt = DateTime.UtcNow.AddDays(-(seed.Index + 15)),
                UpdatedAt = DateTime.UtcNow.AddDays(-(seed.Index % 5))
            })
            .ToList();

        if (missing.Count > 0)
        {
            await _context.Users.InsertManyAsync(missing, cancellationToken: cancellationToken);
        }
    }

    private async Task SeedPoisAsync(CancellationToken cancellationToken)
    {
        var categories = await _context.Categories.Find(FilterDefinition<Category>.Empty).ToListAsync(cancellationToken);
        var categoryMap = categories.ToDictionary(x => x.Code, x => x.Id, StringComparer.OrdinalIgnoreCase);

        var owners = await _context.Users.Find(x => x.Roles.Contains(SharedConstants.UserRoles.Owner))
            .SortBy(x => x.Email)
            .ToListAsync(cancellationToken);

        var existingNames = await _context.Pois.Find(FilterDefinition<Poi>.Empty)
            .Project(x => x.Name)
            .ToListAsync(cancellationToken);

        var missing = PoiSeeds
            .Where(seed => !existingNames.Contains(seed.Name, StringComparer.OrdinalIgnoreCase))
            .Select(seed =>
            {
                var owner = owners.Count == 0 ? null : owners[seed.Index % owners.Count];
                return CreatePoi(seed, categoryMap, owner?.Id);
            })
            .ToList();

        if (missing.Count > 0)
        {
            await _context.Pois.InsertManyAsync(missing, cancellationToken: cancellationToken);
        }

        await BackfillPoiDescriptionsAsync(cancellationToken);
    }

    private async Task SeedPoiLocalizationsAsync(CancellationToken cancellationToken)
    {
        var pois = await _context.Pois.Find(FilterDefinition<Poi>.Empty)
            .SortByDescending(x => x.Priority)
            .ThenBy(x => x.Name)
            .Limit(20)
            .ToListAsync(cancellationToken);

        var existingKeys = await _context.PoiLocalizations.Find(FilterDefinition<PoiLocalization>.Empty)
            .Project(x => x.PoiId + ":" + x.Lang)
            .ToListAsync(cancellationToken);

        var localizations = new List<PoiLocalization>();
        foreach (var (poi, index) in pois.Select((poi, index) => (poi, index)))
        {
            var key = poi.Id + ":en";
            if (existingKeys.Contains(key, StringComparer.Ordinal))
            {
                var existingLocalization = await _context.PoiLocalizations
                    .Find(x => x.PoiId == poi.Id && x.Lang == "en")
                    .FirstOrDefaultAsync(cancellationToken);

                if (existingLocalization is null)
                {
                    continue;
                }

                var shouldUpdate = false;
                if (string.IsNullOrWhiteSpace(existingLocalization.Description))
                {
                    existingLocalization.Description = BuildEnglishDescription(poi, index);
                    shouldUpdate = true;
                }

                if (string.IsNullOrWhiteSpace(existingLocalization.TtsScript))
                {
                    existingLocalization.TtsScript = BuildEnglishNarrationScript(poi.Name);
                    shouldUpdate = true;
                }

                if (shouldUpdate)
                {
                    existingLocalization.UpdatedAt = DateTime.UtcNow;
                    await _context.PoiLocalizations.ReplaceOneAsync(
                        x => x.Id == existingLocalization.Id,
                        existingLocalization,
                        cancellationToken: cancellationToken);
                }

                continue;
            }

            localizations.Add(new PoiLocalization
            {
                PoiId = poi.Id,
                Lang = "en",
                Name = poi.Name,
                Description = BuildEnglishDescription(poi, index),
                AudioUrl = SampleAudioUrls[index % SampleAudioUrls.Length],
                TtsScript = BuildEnglishNarrationScript(poi.Name),
                IsFallback = false,
                CreatedAt = DateTime.UtcNow.AddDays(-(index + 2)),
                UpdatedAt = DateTime.UtcNow.AddDays(-(index % 3))
            });
        }

        if (localizations.Count > 0)
        {
            await _context.PoiLocalizations.InsertManyAsync(localizations, cancellationToken: cancellationToken);
        }
    }

    private async Task BackfillPoiDescriptionsAsync(CancellationToken cancellationToken)
    {
        var seedMap = PoiSeeds.ToDictionary(seed => seed.Name, StringComparer.OrdinalIgnoreCase);
        var pois = await _context.Pois.Find(FilterDefinition<Poi>.Empty).ToListAsync(cancellationToken);

        foreach (var poi in pois)
        {
            seedMap.TryGetValue(poi.Name, out var seed);

            var shouldUpdate = false;

            if (string.IsNullOrWhiteSpace(poi.Description) && !string.IsNullOrWhiteSpace(seed?.Description))
            {
                poi.Description = seed!.Description;
                shouldUpdate = true;
            }

            var narrationSource = string.IsNullOrWhiteSpace(poi.Description) ? seed?.Description : poi.Description;
            if (ShouldRefreshVietnameseNarrationScript(poi.TtsScript, poi.Name))
            {
                if (!string.IsNullOrWhiteSpace(narrationSource))
                {
                    poi.TtsScript = BuildVietnameseNarrationScript(poi.Name, narrationSource);
                    shouldUpdate = true;
                }
            }

            if (!shouldUpdate)
            {
                continue;
            }

            poi.UpdatedAt = DateTime.UtcNow;
            await _context.Pois.ReplaceOneAsync(
                x => x.Id == poi.Id,
                poi,
                cancellationToken: cancellationToken);
        }
    }

    private async Task SeedPoiAudiosAsync(CancellationToken cancellationToken)
    {
        var pois = await _context.Pois.Find(FilterDefinition<Poi>.Empty)
            .SortByDescending(x => x.Priority)
            .ThenBy(x => x.Name)
            .Limit(20)
            .ToListAsync(cancellationToken);
        if (pois.Count > 0)
        {
            var poiIds = pois.Select(static poi => poi.Id).ToList();
            await _context.PoiAudios.DeleteManyAsync(
                x => poiIds.Contains(x.PoiId)
                     && x.Lang == "vi"
                     && x.AudioUrl.Contains("samplelib.com/lib/preview/mp3"),
                cancellationToken);
            await _context.Pois.UpdateManyAsync(
                x => poiIds.Contains(x.Id),
                Builders<Poi>.Update.Set(x => x.AudioStatus, SharedConstants.AudioPending),
                cancellationToken: cancellationToken);
        }
    }

    private async Task SeedAudioTasksAsync(CancellationToken cancellationToken)
    {
        var pois = await _context.Pois.Find(FilterDefinition<Poi>.Empty)
            .SortByDescending(x => x.Priority)
            .ThenBy(x => x.Name)
            .Limit(20)
            .ToListAsync(cancellationToken);

        var existingTaskIds = await _context.AudioTasks.Find(FilterDefinition<AudioTask>.Empty)
            .Project(x => x.TaskId)
            .ToListAsync(cancellationToken);

        var tasks = new List<AudioTask>();
        foreach (var (poi, index) in pois.Select((poi, index) => (poi, index)))
        {
            var taskId = $"seed-audio-task-{index + 1:00}";
            if (existingTaskIds.Contains(taskId, StringComparer.Ordinal))
            {
                continue;
            }

            var status = AudioTaskStatuses[index % AudioTaskStatuses.Length];
            tasks.Add(new AudioTask
            {
                TaskId = taskId,
                PoiId = poi.Id,
                Status = status,
                Languages = ["vi", "en"],
                ProgressPercent = status switch
                {
                    SharedConstants.AudioTaskDone => 100,
                    SharedConstants.AudioTaskRunning => 65,
                    SharedConstants.AudioTaskPaused => 48,
                    SharedConstants.AudioTaskFailed => 72,
                    _ => 5
                },
                ErrorMessage = status == SharedConstants.AudioTaskFailed ? "Cloud TTS timeout on demo seed." : null,
                PauseRequested = status == SharedConstants.AudioTaskPaused,
                CancelRequested = false,
                HeartbeatAt = DateTime.UtcNow.AddMinutes(-(index + 2)),
                StartedAt = DateTime.UtcNow.AddHours(-(index + 1)),
                FinishedAt = status == SharedConstants.AudioTaskDone ? DateTime.UtcNow.AddHours(-index) : null,
                ExpiresAt = DateTime.UtcNow.AddDays(14 + index),
                CreatedAt = DateTime.UtcNow.AddHours(-(index + 3))
            });
        }

        if (tasks.Count > 0)
        {
            await _context.AudioTasks.InsertManyAsync(tasks, cancellationToken: cancellationToken);
        }
    }

    private async Task SeedOwnerRegistrationsAsync(CancellationToken cancellationToken)
    {
        var admin = await GetAdminUserAsync(cancellationToken);
        var users = await _context.Users.Find(x => DemoUsers.Select(seed => seed.Email).Contains(x.Email))
            .SortBy(x => x.Email)
            .ToListAsync(cancellationToken);

        var existingUserIds = await _context.OwnerRegistrations.Find(FilterDefinition<OwnerRegistration>.Empty)
            .Project(x => x.UserId)
            .ToListAsync(cancellationToken);

        var registrations = new List<OwnerRegistration>();
        foreach (var (user, index) in users.Select((user, index) => (user, index)))
        {
            if (existingUserIds.Contains(user.Id, StringComparer.Ordinal))
            {
                continue;
            }

            var seed = DemoUsers.First(x => string.Equals(x.Email, user.Email, StringComparison.OrdinalIgnoreCase));
            var reviewed = seed.OwnerStatus != SharedConstants.OwnerPending;
            registrations.Add(new OwnerRegistration
            {
                UserId = user.Id,
                BusinessName = seed.BusinessName,
                BusinessAddress = seed.BusinessAddress,
                PhoneNumber = seed.PhoneNumber,
                Description = seed.BusinessDescription,
                Status = seed.OwnerStatus,
                AdminNote = seed.OwnerStatus switch
                {
                    SharedConstants.OwnerApproved => "Ho so day du, da duyet de dua vao he thong demo.",
                    SharedConstants.OwnerRejected => "Ho so con thieu giay to hoac thong tin lien he.",
                    _ => null
                },
                ReviewedBy = reviewed ? admin?.Id : null,
                ReviewedAt = reviewed ? DateTime.UtcNow.AddDays(-(index % 7 + 1)) : null,
                CreatedAt = DateTime.UtcNow.AddDays(-(index + 10)),
                UpdatedAt = DateTime.UtcNow.AddDays(-(index % 4))
            });
        }

        if (registrations.Count > 0)
        {
            await _context.OwnerRegistrations.InsertManyAsync(registrations, cancellationToken: cancellationToken);
        }
    }

    private async Task SeedOwnerSubmissionsAsync(CancellationToken cancellationToken)
    {
        var admin = await GetAdminUserAsync(cancellationToken);
        var users = await _context.Users.Find(x => DemoUsers.Select(seed => seed.Email).Contains(x.Email))
            .SortBy(x => x.Email)
            .ToListAsync(cancellationToken);
        var pois = await _context.Pois.Find(FilterDefinition<Poi>.Empty)
            .SortByDescending(x => x.Priority)
            .ThenBy(x => x.Name)
            .Limit(20)
            .ToListAsync(cancellationToken);
        var categories = await _context.Categories.Find(FilterDefinition<Category>.Empty)
            .SortBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

        var existingKeys = await _context.OwnerSubmissions.Find(FilterDefinition<OwnerSubmission>.Empty)
            .Project(x => x.OwnerId + "|" + x.PoiName)
            .ToListAsync(cancellationToken);

        var submissions = new List<OwnerSubmission>();
        for (var index = 0; index < 20; index++)
        {
            var user = users[index % users.Count];
            var category = categories[index % categories.Count];
            var linkedPoi = pois[index % pois.Count];
            var status = SubmissionStatuses[index % SubmissionStatuses.Length];
            var poiName = $"De xuat am thuc {index + 1:00} - {OwnerSubmissionAreas[index % OwnerSubmissionAreas.Length]}";
            var key = user.Id + "|" + poiName;
            if (existingKeys.Contains(key, StringComparer.Ordinal))
            {
                continue;
            }

            submissions.Add(new OwnerSubmission
            {
                OwnerId = user.Id,
                PoiId = index % 2 == 0 ? linkedPoi.Id : null,
                SubmissionType = SharedConstants.SubmissionTypes[index % SharedConstants.SubmissionTypes.Length],
                PoiName = poiName,
                Description = $"Chủ quán đề xuất cập nhật nội dung, hình ảnh và thông tin thu hút khách cho điểm {OwnerSubmissionAreas[index % OwnerSubmissionAreas.Length]}.",
                CategoryId = category.Id,
                Location = GeoLocationFactory.Create(106.7005 + index * 0.00033, 10.7520 + index * 0.00024),
                Address = $"{20 + index} {OwnerSubmissionStreets[index % OwnerSubmissionStreets.Length]}",
                Ward = $"Phuong {(index % 4) + 1}",
                District = "Quáº­n 4",
                City = "TP. Há»“ ChĂ­ Minh",
                PriceRange = SharedConstants.PriceRanges[index % SharedConstants.PriceRanges.Length],
                Priority = 20 - index,
                MapUrl = $"https://www.google.com/maps/search/?api=1&query={10.7520 + index * 0.00024},{106.7005 + index * 0.00033}",
                TtsScript = $"Nội dung đề xuất cho điểm ẩm thực {OwnerSubmissionAreas[index % OwnerSubmissionAreas.Length]}.",
                GeofenceRadiusMeters = 90 + index * 5,
                AutoNarrationEnabled = index % 4 != 0,
                Images =
                [
                    new PoiImage
                    {
                        Url = PoiSeeds[index % PoiSeeds.Length].ImageUrl,
                        Caption = "Ảnh đề xuất từ chủ quán",
                        IsThumbnail = true
                    }
                ],
                OpeningHours = BuildOpeningHours(index),
                ContactInfo = new ContactInfo
                {
                    Phone = user.PhoneNumber,
                    Email = user.Email,
                    FacebookUrl = $"https://facebook.com/{Slugify(user.FullName)}",
                    WebsiteUrl = $"https://demo-quan4.vn/{Slugify(poiName)}"
                },
                Tags = [$"de-xuat-{index + 1:00}", OwnerSubmissionAreas[index % OwnerSubmissionAreas.Length].ToLowerInvariant(), "chu-quan"],
                Status = status,
                AdminNote = status switch
                {
                    SharedConstants.SubmissionApproved => "Đã thông qua để chờ cập nhật vào kho dữ liệu demo.",
                    SharedConstants.SubmissionRejected => "Can bo sung hinh anh va thong tin gia.",
                    _ => null
                },
                ReviewedBy = status == SharedConstants.SubmissionPending ? null : admin?.Id,
                ReviewedAt = status == SharedConstants.SubmissionPending ? null : DateTime.UtcNow.AddDays(-(index % 6 + 1)),
                CreatedAt = DateTime.UtcNow.AddDays(-(index + 4)),
                UpdatedAt = DateTime.UtcNow.AddDays(-(index % 3))
            });
        }

        if (submissions.Count > 0)
        {
            await _context.OwnerSubmissions.InsertManyAsync(submissions, cancellationToken: cancellationToken);
        }
    }

    private async Task SeedMediaFilesAsync(CancellationToken cancellationToken)
    {
        var pois = await _context.Pois.Find(FilterDefinition<Poi>.Empty)
            .SortByDescending(x => x.Priority)
            .ThenBy(x => x.Name)
            .Limit(20)
            .ToListAsync(cancellationToken);

        var existingFiles = await _context.MediaFiles.Find(FilterDefinition<MediaFile>.Empty)
            .Project(x => x.FileName)
            .ToListAsync(cancellationToken);

        var mediaFiles = new List<MediaFile>();
        foreach (var (poi, index) in pois.Select((poi, index) => (poi, index)))
        {
            var fileName = $"poi-{index + 1:00}.jpg";
            if (existingFiles.Contains(fileName, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var imageUrl = poi.Images.FirstOrDefault()?.Url ?? PoiSeeds[index % PoiSeeds.Length].ImageUrl;
            mediaFiles.Add(new MediaFile
            {
                FileName = fileName,
                OriginalFileName = $"quan4-food-{index + 1:00}.jpg",
                Url = imageUrl,
                ContentType = "image/jpeg",
                FileType = "image",
                SizeBytes = 320_000 + index * 12_500,
                StorageProvider = "local",
                BucketName = null,
                ObjectKey = $"seed/poi/{fileName}",
                UploadedBy = poi.OwnerId,
                CreatedAt = DateTime.UtcNow.AddDays(-(index + 2))
            });
        }

        if (mediaFiles.Count > 0)
        {
            await _context.MediaFiles.InsertManyAsync(mediaFiles, cancellationToken: cancellationToken);
        }
    }

    private async Task SeedAnalyticsEventsAsync(CancellationToken cancellationToken)
    {
        var pois = await _context.Pois.Find(FilterDefinition<Poi>.Empty)
            .SortByDescending(x => x.Priority)
            .ThenBy(x => x.Name)
            .Limit(20)
            .ToListAsync(cancellationToken);

        var existingPageViewIds = await _context.AnalyticsEvents.Find(FilterDefinition<AnalyticsEvent>.Empty)
            .Project(x => x.PageViewId)
            .ToListAsync(cancellationToken);

        var events = new List<AnalyticsEvent>();
        foreach (var (poi, index) in pois.Select((poi, index) => (poi, index)))
        {
            var pageViewId = $"seed-pageview-{index + 1:00}";
            if (existingPageViewIds.Contains(pageViewId, StringComparer.Ordinal))
            {
                continue;
            }

            var eventName = SharedConstants.AnalyticsEvents[index % SharedConstants.AnalyticsEvents.Length];
            events.Add(new AnalyticsEvent
            {
                AnonymousId = $"seed-guest-{index + 1:00}",
                SessionId = $"seed-session-{index / 2 + 1:00}",
                PageViewId = pageViewId,
                EventName = eventName,
                PoiId = eventName is "search_executed" or "nearby_requested" ? null : poi.Id,
                Lang = index % 2 == 0 ? "vi" : "en",
                Latitude = poi.Location.Coordinates.Latitude,
                Longitude = poi.Location.Coordinates.Longitude,
                AccuracyMeters = 8 + index,
                ListenDurationSeconds = eventName is "audio_played" or "narration_completed" ? 28 + index * 2 : null,
                IsBackground = eventName is "location_sample" or "geofence_triggered" ? index % 2 == 0 : null,
                TrackingSource = eventName.Contains("location", StringComparison.Ordinal) || eventName.Contains("geofence", StringComparison.Ordinal)
                    ? "background-service"
                    : "web-user",
                ContentType = eventName.Contains("audio", StringComparison.Ordinal) ? "audio" : "poi",
                Metadata = BuildAnalyticsMetadata(eventName, index),
                CreatedAt = DateTime.UtcNow.AddMinutes(-(index * 18 + 6))
            });
        }

        if (events.Count > 0)
        {
            await _context.AnalyticsEvents.InsertManyAsync(events, cancellationToken: cancellationToken);
        }
    }

    private async Task SeedAuditLogsAsync(CancellationToken cancellationToken)
    {
        var admin = await GetAdminUserAsync(cancellationToken);
        var pois = await _context.Pois.Find(FilterDefinition<Poi>.Empty)
            .SortByDescending(x => x.Priority)
            .ThenBy(x => x.Name)
            .Limit(20)
            .ToListAsync(cancellationToken);

        var existingKeys = await _context.AuditLogs.Find(FilterDefinition<AuditLog>.Empty)
            .Project(x => x.Action + "|" + x.ResourceType + "|" + x.ResourceId)
            .ToListAsync(cancellationToken);

        var logs = new List<AuditLog>();
        foreach (var (poi, index) in pois.Select((poi, index) => (poi, index)))
        {
            var action = AuditActions[index % AuditActions.Length];
            var resourceType = index % 3 == 0 ? "poi" : index % 3 == 1 ? "audio" : "tour";
            var resourceId = resourceType == "tour" ? $"tour-seed-{index + 1:00}" : poi.Id;
            var key = action + "|" + resourceType + "|" + resourceId;
            if (existingKeys.Contains(key, StringComparer.Ordinal))
            {
                continue;
            }

            logs.Add(new AuditLog
            {
                UserId = admin?.Id,
                Action = action,
                ResourceType = resourceType,
                ResourceId = resourceId,
                IpAddress = $"10.0.0.{index + 10}",
                UserAgent = $"SeederBot/1.0 ({resourceType})",
                Details = new Dictionary<string, object>
                {
                    ["resourceName"] = poi.Name,
                    ["status"] = index % 2 == 0 ? "ok" : "reviewed",
                    ["channel"] = index % 2 == 0 ? "admin-web" : "mobile"
                },
                CreatedAt = DateTime.UtcNow.AddMinutes(-(index * 11 + 3))
            });
        }

        if (logs.Count > 0)
        {
            await _context.AuditLogs.InsertManyAsync(logs, cancellationToken: cancellationToken);
        }
    }

    private async Task SeedToursAsync(CancellationToken cancellationToken)
    {
        var pois = await _context.Pois.Find(FilterDefinition<Poi>.Empty)
            .SortByDescending(x => x.Priority)
            .ThenBy(x => x.Name)
            .Limit(20)
            .ToListAsync(cancellationToken);

        var existingTitles = await _context.Tours.Find(FilterDefinition<Tour>.Empty)
            .Project(x => x.Title + "|" + x.Lang)
            .ToListAsync(cancellationToken);

        var tours = new List<Tour>();
        for (var index = 0; index < 20 && pois.Count >= 3; index++)
        {
            var lang = index % 2 == 0 ? "vi" : "en";
            var title = lang == "vi"
                ? $"Tour am thuc {TourThemes[index % TourThemes.Length]} {index + 1:00}"
                : $"District 4 {TourThemes[index % TourThemes.Length]} tour {index + 1:00}";
            var key = title + "|" + lang;
            if (existingTitles.Contains(key, StringComparer.Ordinal))
            {
                continue;
            }

            var stops = new List<TourStop>();
            for (var stopIndex = 0; stopIndex < 3; stopIndex++)
            {
                var poi = pois[(index + stopIndex) % pois.Count];
                stops.Add(new TourStop
                {
                    PoiId = poi.Id,
                    Title = lang == "vi" ? $"Diem dung {stopIndex + 1}: {poi.Name}" : $"Stop {stopIndex + 1}: {poi.Name}",
                    Order = stopIndex + 1,
                    EstimatedStayMinutes = 15 + stopIndex * 10
                });
            }

            tours.Add(new Tour
            {
                Title = title,
                Description = lang == "vi"
                    ? $"Hanh trinh kham pha chu de {TourThemes[index % TourThemes.Length].ToLowerInvariant()} qua cac diem noi bat o Quan 4."
                    : $"A curated District 4 route focused on {TourThemes[index % TourThemes.Length].ToLowerInvariant()} highlights.",
                Lang = lang,
                CoverImageUrl = pois[index % pois.Count].Images.FirstOrDefault()?.Url,
                EstimatedDurationMinutes = 50 + index * 5,
                IsActive = index % 5 != 4,
                Stops = stops,
                CreatedAt = DateTime.UtcNow.AddDays(-(index + 1)),
                UpdatedAt = DateTime.UtcNow.AddHours(-(index + 2))
            });
        }

        if (tours.Count > 0)
        {
            await _context.Tours.InsertManyAsync(tours, cancellationToken: cancellationToken);
        }
    }

    private async Task SeedQrActivationsAsync(CancellationToken cancellationToken)
    {
        var pois = await _context.Pois.Find(FilterDefinition<Poi>.Empty)
            .SortByDescending(x => x.Priority)
            .ThenBy(x => x.Name)
            .Limit(20)
            .ToListAsync(cancellationToken);

        var existingCodes = await _context.QrActivations.Find(FilterDefinition<QrActivation>.Empty)
            .Project(x => x.Code)
            .ToListAsync(cancellationToken);

        var activations = new List<QrActivation>();
        foreach (var (poi, index) in pois.Select((poi, index) => (poi, index)))
        {
            var code = QrCodes[index];
            if (existingCodes.Contains(code, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            activations.Add(new QrActivation
            {
                Code = code,
                PoiId = poi.Id,
                Title = QrTitles[index],
                StopZone = QrZones[index],
                StopAddress = QrAddresses[index],
                SortOrder = (index % 4) + 1,
                Description = index % 2 == 0
                    ? "Quét là mở ngay nội dung thuyết minh tại điểm dừng."
                    : "Mã QR dành cho khách du lịch muốn nghe audio nhanh.",
                ScanMode = SharedConstants.QrScanModes[index % SharedConstants.QrScanModes.Length],
                IsActive = true,
                UpdatedAt = DateTime.UtcNow.AddDays(-(index % 6))
            });
        }

        if (activations.Count > 0)
        {
            await _context.QrActivations.InsertManyAsync(activations, cancellationToken: cancellationToken);
        }
    }

    private async Task SeedMapPacksAsync(CancellationToken cancellationToken)
    {
        var existingVersions = await _context.MapPacks.Find(FilterDefinition<MapPack>.Empty)
            .Project(x => x.Version)
            .ToListAsync(cancellationToken);

        var packs = new List<MapPack>();
        for (var version = 1; version <= 20; version++)
        {
            var versionLabel = $"v{version}";
            if (existingVersions.Contains(versionLabel, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            packs.Add(new MapPack
            {
                Version = versionLabel,
                Name = $"Quan 4 Offline Pack {versionLabel}",
                DownloadUrl = string.Empty,
                Sha256 = string.Empty,
                SizeBytes = 3_500 + version * 25,
                IsActive = false,
                PublishedAt = DateTime.UtcNow.AddDays(-version),
                CreatedAt = DateTime.UtcNow.AddDays(-version)
            });
        }

        if (packs.Count > 0)
        {
            await _context.MapPacks.InsertManyAsync(packs, cancellationToken: cancellationToken);
        }

        await _context.MapPacks.UpdateManyAsync(
            FilterDefinition<MapPack>.Empty,
            Builders<MapPack>.Update.Set(x => x.IsActive, false),
            cancellationToken: cancellationToken);

        await _context.MapPacks.UpdateOneAsync(
            x => x.Version == "v20",
            Builders<MapPack>.Update.Set(x => x.IsActive, true),
            cancellationToken: cancellationToken);
    }

    private async Task<User?> GetAdminUserAsync(CancellationToken cancellationToken) =>
        await _context.Users.Find(x => x.Email == _defaultAdmin.Email).FirstOrDefaultAsync(cancellationToken);

    private static Poi CreatePoi(PoiSeed seed, IReadOnlyDictionary<string, string> categoryMap, string? ownerId) => new()
    {
        Name = seed.Name,
        Description = seed.Description,
        CategoryId = categoryMap.TryGetValue(seed.CategoryCode, out var categoryId) ? categoryId : string.Empty,
        Location = GeoLocationFactory.Create(seed.Longitude, seed.Latitude),
        Address = seed.Address,
        Ward = seed.Ward,
        District = "Quáº­n 4",
        City = "TP. Há»“ ChĂ­ Minh",
        PriceRange = seed.PriceRange,
        Rating = seed.Rating,
        ReviewCount = seed.ReviewCount,
        Priority = seed.Priority,
        MapUrl = $"https://www.google.com/maps/search/?api=1&query={seed.Latitude},{seed.Longitude}",
        TtsScript = BuildVietnameseNarrationScript(seed.Name, seed.Description),
        GeofenceRadiusMeters = 90 + seed.Index * 4,
        AutoNarrationEnabled = seed.Index % 4 != 0,
        Images =
        [
            new PoiImage { Url = seed.ImageUrl, Caption = seed.Caption, IsThumbnail = true }
        ],
        OpeningHours = BuildOpeningHours(seed.Index),
        ContactInfo = new ContactInfo
        {
            Phone = $"028 38{seed.Index + 20:00} 00{seed.Index + 10:00}",
            Email = $"contact{seed.Index + 1:00}@quan4-tour.local",
            FacebookUrl = $"https://facebook.com/{Slugify(seed.Name)}",
            WebsiteUrl = $"https://quan4-tour.local/poi/{Slugify(seed.Name)}"
        },
        OwnerId = ownerId,
        AudioStatus = SharedConstants.AudioDone,
        IsActive = true,
        ActivationRequested = seed.Index % 3 == 0,
        Tags = seed.Tags,
        CreatedAt = DateTime.UtcNow.AddDays(-(seed.Index + 12)),
        UpdatedAt = DateTime.UtcNow.AddDays(-(seed.Index % 5))
    };

    private static List<OpeningHour> BuildOpeningHours(int index)
    {
        if (index % 5 == 0)
        {
            return
            [
                new OpeningHour { DayOfWeek = "Thá»© Hai", OpenTime = "", CloseTime = "", IsClosed = true },
                new OpeningHour { DayOfWeek = "Thá»© Ba - Chá»§ Nháº­t", OpenTime = "09:30", CloseTime = "22:00" }
            ];
        }

        if (index % 3 == 0)
        {
            return
            [
                new OpeningHour { DayOfWeek = "Thá»© Hai - Thá»© SĂ¡u", OpenTime = "07:00", CloseTime = "21:30" },
                new OpeningHour { DayOfWeek = "Thá»© Báº£y - Chá»§ Nháº­t", OpenTime = "08:00", CloseTime = "22:30" }
            ];
        }

        return
        [
            new OpeningHour { DayOfWeek = "Thá»© Hai - Chá»§ Nháº­t", OpenTime = "10:00", CloseTime = "22:30" }
        ];
    }

    private static string BuildEnglishDescription(Poi poi, int index) =>
        $"{poi.Name} is a popular District 4 food stop near {poi.Address}, known for a local atmosphere and crowd-favorite dishes. This seed translation #{index + 1:00} helps demo multilingual content in the CMS and mobile app.";

    private static string BuildEnglishNarrationScript(string poiName) =>
        $"Welcome to {poiName}, one of the notable food stops in District 4.";

    private static string BuildVietnameseNarrationScript(string poiName, string description) =>
        $"Gi\u1edbi thi\u1ec7u nhanh v\u1ec1 {poiName}: {description}";

    private static bool ShouldRefreshVietnameseNarrationScript(string? currentScript, string poiName)
    {
        if (string.IsNullOrWhiteSpace(currentScript))
        {
            return true;
        }

        var normalized = currentScript.Trim();
        if (normalized.StartsWith("Gioi thieu nhanh ve ", StringComparison.Ordinal)
            || normalized.StartsWith("Gi\u1edbi thi\u1ec7u nhanh v\u1ec1 ", StringComparison.Ordinal))
        {
            return true;
        }

        return normalized.StartsWith("G", StringComparison.Ordinal)
            && normalized.Contains(poiName, StringComparison.Ordinal)
            && normalized.Contains(":", StringComparison.Ordinal)
            && normalized.Contains("nhanh", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, object> BuildAnalyticsMetadata(string eventName, int index)
    {
        return eventName switch
        {
            "search_executed" => new Dictionary<string, object>
            {
                ["keyword"] = SearchKeywords[index % SearchKeywords.Length],
                ["categoryCode"] = CategorySeeds[index % CategorySeeds.Length].Code
            },
            "nearby_requested" => new Dictionary<string, object>
            {
                ["radius"] = 1000 + (index % 4) * 2000,
                ["requestSource"] = "seeded-scenario"
            },
            "audio_played" or "tts_played" or "narration_completed" => new Dictionary<string, object>
            {
                ["durationSeconds"] = 24 + index * 2,
                ["source"] = "seeded-audio"
            },
            "location_sample" or "geofence_triggered" => new Dictionary<string, object>
            {
                ["source"] = "background-service",
                ["radius"] = 100 + index * 5
            },
            _ => new Dictionary<string, object>
            {
                ["source"] = "seeded-demo"
            }
        };
    }

    private static string Slugify(string value)
    {
        var builder = new List<char>(value.Length);
        foreach (var character in value.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Add(character);
            }
            else if (builder.Count == 0 || builder[^1] != '-')
            {
                builder.Add('-');
            }
        }

        return new string(builder.ToArray()).Trim('-');
    }

    private static readonly string[] AudioTaskStatuses =
    [
        SharedConstants.AudioTaskDone,
        SharedConstants.AudioTaskRunning,
        SharedConstants.AudioTaskQueued,
        SharedConstants.AudioTaskPaused,
        SharedConstants.AudioTaskDone,
        SharedConstants.AudioTaskFailed
    ];

    private static readonly string[] SubmissionStatuses =
    [
        SharedConstants.SubmissionApproved,
        SharedConstants.SubmissionPending,
        SharedConstants.SubmissionRejected,
        SharedConstants.SubmissionApproved
    ];

    private static readonly string[] AuditActions =
    [
        "poi_created",
        "poi_updated",
        "audio_uploaded",
        "tour_published",
        "qr_activation_updated"
    ];

    private static readonly string[] SearchKeywords =
    [
        "á»‘c",
        "cÆ¡m táº¥m",
        "bĂºn",
        "chĂ¨",
        "cĂ  phĂª",
        "háº£i sáº£n",
        "trĂ¡ng miá»‡ng",
        "Äƒn Ä‘Ăªm"
    ];

    private static readonly string[] SampleAudioUrls =
    [
        "https://samplelib.com/lib/preview/mp3/sample-3s.mp3",
        "https://samplelib.com/lib/preview/mp3/sample-6s.mp3",
        "https://samplelib.com/lib/preview/mp3/sample-9s.mp3",
        "https://samplelib.com/lib/preview/mp3/sample-12s.mp3",
        "https://samplelib.com/lib/preview/mp3/sample-15s.mp3"
    ];

    private static readonly string[] TourThemes =
    [
        "Vinh Khanh",
        "An dem",
        "Bua sang",
        "Ca phe va banh",
        "Mon Viet",
        "Hai san",
        "An vat",
        "Ven kenh"
    ];

    private static readonly string[] OwnerSubmissionAreas =
    [
        "Vinh Khanh",
        "Khanh Hoi",
        "Xom Chieu",
        "Doan Van Bo",
        "Ton Dan"
    ];

    private static readonly string[] OwnerSubmissionStreets =
    [
        "VÄ©nh KhĂ¡nh",
        "KhĂ¡nh Há»™i",
        "XĂ³m Chiáº¿u",
        "ÄoĂ n VÄƒn BÆ¡",
        "TĂ´n Äáº£n",
        "HoĂ ng Diá»‡u"
    ];

    private static readonly string[] QrCodes =
    [
        "KHANHHOI-01",
        "VINHHOI-01",
        "XOMCHIEU-01",
        "VINHKHANH-02",
        "DOANVANBO-01",
        "TONDAN-01",
        "HOANGDIEU-01",
        "KENHTE-01",
        "BENVANDON-01",
        "KHANHHOI-02",
        "VINHKHANH-03",
        "XOMCHIEU-02",
        "DOANVANBO-02",
        "TONDAN-02",
        "HOANGDIEU-02",
        "BENVANDON-02",
        "VINHHOI-02",
        "PHUONG3-01",
        "PHUONG13-01",
        "Q4FOOD-20"
    ];

    private static readonly string[] QrTitles =
    [
        "Tráº¡m xe buĂ½t KhĂ¡nh Há»™i",
        "Tráº¡m xe buĂ½t VÄ©nh Há»™i",
        "Tráº¡m xe buĂ½t XĂ³m Chiáº¿u",
        "Äiá»ƒm dá»«ng VÄ©nh KhĂ¡nh",
        "Äiá»ƒm dá»«ng ÄoĂ n VÄƒn BÆ¡",
        "Äiá»ƒm dá»«ng TĂ´n Äáº£n",
        "Äiá»ƒm dá»«ng HoĂ ng Diá»‡u",
        "Äiá»ƒm dá»«ng KĂªnh Táº»",
        "Äiá»ƒm dá»«ng Báº¿n VĂ¢n Äá»“n",
        "Tráº¡m KhĂ¡nh Há»™i hÆ°á»›ng cáº§u",
        "Äiá»ƒm check-in VÄ©nh KhĂ¡nh",
        "Äiá»ƒm chá»£ XĂ³m Chiáº¿u",
        "ÄoĂ n VÄƒn BÆ¡ giao lá»™",
        "Äiá»ƒm xe buĂ½t TĂ´n Äáº£n",
        "HoĂ ng Diá»‡u khu áº©m thá»±c",
        "Báº¿n VĂ¢n Äá»“n ven kĂªnh",
        "Tráº¡m VÄ©nh Há»™i hÆ°á»›ng chá»£",
        "PhÆ°á»ng 3 - cá»•ng chá»£",
        "PhÆ°á»ng 13 - khu Äƒn Ä‘Ăªm",
        "Äiá»ƒm giá»›i thiá»‡u áº©m thá»±c Quáº­n 4"
    ];

    private static readonly string[] QrZones =
    [
        "KhĂ¡nh Há»™i",
        "VÄ©nh Há»™i",
        "XĂ³m Chiáº¿u",
        "VÄ©nh KhĂ¡nh",
        "ÄoĂ n VÄƒn BÆ¡",
        "TĂ´n Äáº£n",
        "HoĂ ng Diá»‡u",
        "KĂªnh Táº»",
        "Báº¿n VĂ¢n Äá»“n",
        "KhĂ¡nh Há»™i",
        "VÄ©nh KhĂ¡nh",
        "XĂ³m Chiáº¿u",
        "ÄoĂ n VÄƒn BÆ¡",
        "TĂ´n Äáº£n",
        "HoĂ ng Diá»‡u",
        "Báº¿n VĂ¢n Äá»“n",
        "VÄ©nh Há»™i",
        "PhÆ°á»ng 3",
        "PhÆ°á»ng 13",
        "Trung tĂ¢m Quáº­n 4"
    ];

    private static readonly string[] QrAddresses =
    [
        "Báº¿n VĂ¢n Äá»“n - cáº§u KhĂ¡nh Há»™i",
        "TĂ´n Äáº£n - khu VÄ©nh Há»™i",
        "Chá»£ XĂ³m Chiáº¿u - Ä‘Æ°á»ng XĂ³m Chiáº¿u",
        "Phá»‘ áº©m thá»±c VÄ©nh KhĂ¡nh",
        "NgĂ£ tÆ° ÄoĂ n VÄƒn BÆ¡ - Nguyá»…n KhoĂ¡i",
        "Khu dĂ¢n cÆ° TĂ´n Äáº£n",
        "HoĂ ng Diá»‡u - Ä‘oáº¡n chá»£ chiá»u",
        "Lá»‘i xuá»‘ng KĂªnh Táº»",
        "Bá» kĂªnh Báº¿n VĂ¢n Äá»“n",
        "Cáº§u KhĂ¡nh Há»™i hÆ°á»›ng Quáº­n 1",
        "Äáº§u Ä‘Æ°á»ng VÄ©nh KhĂ¡nh",
        "Cá»•ng chá»£ XĂ³m Chiáº¿u",
        "ÄoĂ n VÄƒn BÆ¡ gáº§n trÆ°á»ng há»c",
        "Báº¿n xe buĂ½t TĂ´n Äáº£n",
        "Vá»‰a hĂ¨ HoĂ ng Diá»‡u",
        "Báº¿n VĂ¢n Äá»“n hÆ°á»›ng cáº§u Calmette",
        "Tráº¡m VÄ©nh Há»™i gáº§n chung cÆ°",
        "PhÆ°á»ng 3 - khu dĂ¢n cÆ°",
        "PhÆ°á»ng 13 - khu hĂ ng quĂ¡n",
        "Äiá»ƒm tá»•ng há»£p thĂ´ng tin du lá»‹ch"
    ];

    private static readonly DemoUserSeed[] DemoUsers =
    [
        new(1, "tran.minh.hieu@quan4tourism.local", "Tráº§n Minh Hiáº¿u", "0908000101", SharedConstants.OwnerApproved, "á»c ÄĂªm Hiáº¿u", "102 VÄ©nh KhĂ¡nh", "QuĂ¡n háº£i sáº£n bĂ¬nh dĂ¢n má»Ÿ khuya.", "https://images.unsplash.com/photo-1500648767791-00dcc994a43e?auto=format&fit=crop&w=400&q=80"),
        new(2, "nguyen.thao.my@quan4tourism.local", "Nguyá»…n Tháº£o My", "0908000102", SharedConstants.OwnerApproved, "Báº¿p NhĂ  My", "18 ÄoĂ n VÄƒn BÆ¡", "QuĂ¡n cÆ¡m gia Ä‘Ă¬nh chuyĂªn mĂ³n Viá»‡t.", "https://images.unsplash.com/photo-1494790108377-be9c29b29330?auto=format&fit=crop&w=400&q=80"),
        new(3, "pham.quoc.an@quan4tourism.local", "Pháº¡m Quá»‘c An", "0908000103", SharedConstants.OwnerApproved, "BĂºn Phá»‘ An", "29 XĂ³m Chiáº¿u", "Tiá»‡m bĂºn sĂ¡ng phá»¥c vá»¥ dĂ¢n Ä‘á»‹a phÆ°Æ¡ng.", "https://images.unsplash.com/photo-1506794778202-cad84cf45f1d?auto=format&fit=crop&w=400&q=80"),
        new(4, "le.kim.ngan@quan4tourism.local", "LĂª Kim NgĂ¢n", "0908000104", SharedConstants.OwnerApproved, "ChĂ¨ NgĂ¢n 1988", "44 TĂ´n Äáº£n", "Quáº§y chĂ¨ vĂ  trĂ¡ng miá»‡ng lĂ¢u nÄƒm.", "https://images.unsplash.com/photo-1488426862026-3ee34a7d66df?auto=format&fit=crop&w=400&q=80"),
        new(5, "vo.huu.phuc@quan4tourism.local", "VĂµ Há»¯u PhĂºc", "0908000105", SharedConstants.OwnerApproved, "CĂ  phĂª Bá» KĂªnh PhĂºc", "75 Báº¿n VĂ¢n Äá»“n", "QuĂ¡n cĂ  phĂª nhá» nhĂ¬n ra kĂªnh.", "https://images.unsplash.com/photo-1507591064344-4c6ce005b128?auto=format&fit=crop&w=400&q=80"),
        new(6, "doan.nhat.linh@quan4tourism.local", "ÄoĂ n Nháº­t Linh", "0908000106", SharedConstants.OwnerApproved, "BĂ¡nh TrĂ¡ng Linh", "12 KhĂ¡nh Há»™i", "Quáº§y bĂ¡nh trĂ¡ng trá»™n vĂ  Ä‘á»“ Äƒn váº·t.", "https://images.unsplash.com/photo-1544005313-94ddf0286df2?auto=format&fit=crop&w=400&q=80"),
        new(7, "truong.bao.chau@quan4tourism.local", "TrÆ°Æ¡ng Báº£o ChĂ¢u", "0908000107", SharedConstants.OwnerApproved, "Láº©u NÆ°á»›ng Báº£o ChĂ¢u", "131 HoĂ ng Diá»‡u", "QuĂ¡n láº©u nÆ°á»›ng cho nhĂ³m báº¡n tráº».", "https://images.unsplash.com/photo-1546961329-78bef0414d7c?auto=format&fit=crop&w=400&q=80"),
        new(8, "bui.gia.khanh@quan4tourism.local", "BĂ¹i Gia KhĂ¡nh", "0908000108", SharedConstants.OwnerApproved, "MĂ¬ Khuya Gia KhĂ¡nh", "9 Nguyá»…n KhoĂ¡i", "QuĂ¡n mĂ¬ vĂ  há»§ tiáº¿u má»Ÿ tá»‘i.", "https://images.unsplash.com/photo-1504257432389-52343af06ae3?auto=format&fit=crop&w=400&q=80"),
        new(9, "hoang.mai.phuong@quan4tourism.local", "HoĂ ng Mai PhÆ°Æ¡ng", "0908000109", SharedConstants.OwnerPending, "BĂºn BĂ² Mai PhÆ°Æ¡ng", "22 VÄ©nh Há»™i", "Äang xin duyá»‡t gian hĂ ng bĂºn bĂ².", "https://images.unsplash.com/photo-1438761681033-6461ffad8d80?auto=format&fit=crop&w=400&q=80"),
        new(10, "dang.thanh.tung@quan4tourism.local", "Äáº·ng Thanh TĂ¹ng", "0908000110", SharedConstants.OwnerPending, "CÆ¡m Tá»‘i Thanh TĂ¹ng", "48 TĂ´n Äáº£n", "QuĂ¡n cÆ¡m tá»‘i phá»¥c vá»¥ dĂ¢n vÄƒn phĂ²ng.", "https://images.unsplash.com/photo-1500648767791-00dcc994a43e?auto=format&fit=crop&w=400&q=80"),
        new(11, "phan.ngoc.ha@quan4tourism.local", "Phan Ngá»c HĂ ", "0908000111", SharedConstants.OwnerPending, "Kem Dá»«a Ngá»c HĂ ", "63 XĂ³m Chiáº¿u", "Quáº§y kem dá»«a vĂ  mĂ³n mĂ¡t.", "https://images.unsplash.com/photo-1494790108377-be9c29b29330?auto=format&fit=crop&w=400&q=80"),
        new(12, "ngo.minh.duc@quan4tourism.local", "NgĂ´ Minh Äá»©c", "0908000112", SharedConstants.OwnerPending, "BĂ¡nh Canh Minh Äá»©c", "88 KhĂ¡nh Há»™i", "BĂ¡nh canh vĂ  sĂºp nĂ³ng phá»¥c vá»¥ sĂ¡ng.", "https://images.unsplash.com/photo-1506794778202-cad84cf45f1d?auto=format&fit=crop&w=400&q=80"),
        new(13, "ly.thu.trang@quan4tourism.local", "LĂ½ Thu Trang", "0908000113", SharedConstants.OwnerPending, "Tiá»‡m Ä‚n Thu Trang", "37 ÄoĂ n VÄƒn BÆ¡", "Äá» xuáº¥t má»Ÿ Ä‘iá»ƒm Äƒn gia Ä‘Ă¬nh.", "https://images.unsplash.com/photo-1488426862026-3ee34a7d66df?auto=format&fit=crop&w=400&q=80"),
        new(14, "cao.viet.long@quan4tourism.local", "Cao Viá»‡t Long", "0908000114", SharedConstants.OwnerPending, "á»c Long SĂ i GĂ²n", "118 VÄ©nh KhĂ¡nh", "QuĂ¡n á»‘c cáº§n bá»• sung giáº¥y phĂ©p.", "https://images.unsplash.com/photo-1507591064344-4c6ce005b128?auto=format&fit=crop&w=400&q=80"),
        new(15, "lam.gia.han@quan4tourism.local", "LĂ¢m Gia HĂ¢n", "0908000115", SharedConstants.OwnerRejected, "TrĂ  Sá»¯a Gia HĂ¢n", "11 HoĂ ng Diá»‡u", "Há»“ sÆ¡ chÆ°a Ä‘á»§ minh chá»©ng nguá»“n gá»‘c.", "https://images.unsplash.com/photo-1544005313-94ddf0286df2?auto=format&fit=crop&w=400&q=80"),
        new(16, "ta.huu.nghia@quan4tourism.local", "Táº¡ Há»¯u NghÄ©a", "0908000116", SharedConstants.OwnerRejected, "BĂ² LĂ¡ Lá»‘t Há»¯u NghÄ©a", "55 TĂ´n Äáº£n", "ThĂ´ng tin liĂªn há»‡ chÆ°a xĂ¡c thá»±c.", "https://images.unsplash.com/photo-1546961329-78bef0414d7c?auto=format&fit=crop&w=400&q=80"),
        new(17, "mai.khanh.ly@quan4tourism.local", "Mai KhĂ¡nh Ly", "0908000117", SharedConstants.OwnerRejected, "Tiá»‡m ChĂ¨ KhĂ¡nh Ly", "70 Báº¿n VĂ¢n Äá»“n", "áº¢nh minh há»a chÆ°a Ä‘Ăºng Ä‘á»‹a Ä‘iá»ƒm.", "https://images.unsplash.com/photo-1438761681033-6461ffad8d80?auto=format&fit=crop&w=400&q=80"),
        new(18, "duong.anh.khoa@quan4tourism.local", "DÆ°Æ¡ng Anh Khoa", "0908000118", SharedConstants.OwnerRejected, "BĂ¡nh MĂ¬ Anh Khoa", "94 XĂ³m Chiáº¿u", "Thiáº¿u mĂ´ táº£ sáº£n pháº©m vĂ  giá» má»Ÿ cá»­a.", "https://images.unsplash.com/photo-1504257432389-52343af06ae3?auto=format&fit=crop&w=400&q=80"),
        new(19, "chau.ngoc.nhu@quan4tourism.local", "ChĂ¢u Ngá»c NhÆ°", "0908000119", SharedConstants.OwnerRejected, "Tiá»‡m Ä‚n Ngá»c NhÆ°", "19 VÄ©nh Há»™i", "ChÆ°a cáº­p nháº­t báº£ng giĂ¡ rĂµ rĂ ng.", "https://images.unsplash.com/photo-1494790108377-be9c29b29330?auto=format&fit=crop&w=400&q=80"),
        new(20, "luu.minh.quan@quan4tourism.local", "LÆ°u Minh QuĂ¢n", "0908000120", SharedConstants.OwnerRejected, "BĂºn Máº¯m Minh QuĂ¢n", "101 Nguyá»…n Táº¥t ThĂ nh", "Äá»‹a chá»‰ Ä‘Äƒng kĂ½ chÆ°a khá»›p thá»±c táº¿.", "https://images.unsplash.com/photo-1506794778202-cad84cf45f1d?auto=format&fit=crop&w=400&q=80")
    ];

    private static readonly CategorySeed[] CategorySeeds =
    [
        new("street_food", "Ä‚n váº·t", "CĂ¡c mĂ³n Äƒn nhanh, Äƒn chÆ¡i Ä‘áº·c trÆ°ng khu phá»‘.", 1),
        new("rice", "CÆ¡m", "CÆ¡m táº¥m, cÆ¡m gia Ä‘Ă¬nh vĂ  cĂ¡c pháº§n cÆ¡m quen thuá»™c.", 2),
        new("noodles", "BĂºn / Phá»Ÿ / MĂ¬", "CĂ¡c mĂ³n nÆ°á»›c vĂ  mĂ³n sá»£i phá»• biáº¿n.", 3),
        new("seafood", "Háº£i sáº£n", "á»c, tĂ´m, cua, nghĂªu vĂ  cĂ¡c mĂ³n háº£i sáº£n Ä‘Ăªm.", 4),
        new("coffee", "CĂ  phĂª", "QuĂ¡n cĂ  phĂª vĂ  Ä‘iá»ƒm nghá»‰ chĂ¢n ven kĂªnh.", 5),
        new("dessert", "TrĂ¡ng miá»‡ng", "ChĂ¨, bĂ¡nh ngá»t, kem vĂ  mĂ³n ngá»t.", 6),
        new("drink", "Äá»“ uá»‘ng", "NÆ°á»›c Ă©p, trĂ  sá»¯a, Ä‘á»“ uá»‘ng giáº£i khĂ¡t.", 7),
        new("hotpot_bbq", "Láº©u / NÆ°á»›ng", "NhĂ³m mĂ³n láº©u vĂ  nÆ°á»›ng tá»¥ táº­p báº¡n bĂ¨.", 8),
        new("vietnamese_food", "MĂ³n Viá»‡t", "Nhá»¯ng quĂ¡n mĂ³n Viá»‡t Ä‘á»‹a phÆ°Æ¡ng.", 9),
        new("night_food", "QuĂ¡n Ä‘Ăªm", "Äiá»ƒm Äƒn khuya vĂ  hĂ ng quĂ¡n má»Ÿ muá»™n.", 10),
        new("banh_mi", "BĂ¡nh mĂ¬", "BĂ¡nh mĂ¬ cháº£o, bĂ¡nh mĂ¬ thá»‹t vĂ  biáº¿n táº¥u Ä‘Æ°á»ng phá»‘.", 11),
        new("porridge_soup", "ChĂ¡o / SĂºp", "ChĂ¡o, sĂºp cua vĂ  mĂ³n nĂ³ng nháº¹ bá»¥ng.", 12),
        new("snails", "á»c & nghĂªu", "CĂ¡c quĂ¡n á»‘c xĂ o, háº¥p, rang muá»‘i Ä‘áº·c trÆ°ng.", 13),
        new("tea_dessert", "ChĂ¨ & trĂ ", "ChĂ¨ truyá»n thá»‘ng, trĂ  trĂ¡i cĂ¢y vĂ  mĂ³n mĂ¡t.", 14),
        new("bakery", "BĂ¡nh & tiá»‡m nÆ°á»›ng", "Tiá»‡m bĂ¡nh máº·n, ngá»t vĂ  Ä‘á»“ nÆ°á»›ng nhanh.", 15),
        new("vegetarian", "Ä‚n chay", "Äiá»ƒm Äƒn chay vĂ  mĂ³n thanh Ä‘áº¡m.", 16),
        new("seafood_hotpot", "Láº©u háº£i sáº£n", "NhĂ³m quĂ¡n láº©u táº­p trung quanh khu Ä‘Ăªm.", 17),
        new("broken_rice", "CÆ¡m táº¥m chuyĂªn biá»‡t", "Nhá»¯ng quĂ¡n cÆ¡m táº¥m ná»•i báº­t.", 18),
        new("regional", "Äáº·c sáº£n vĂ¹ng miá»n", "MĂ³n Huáº¿, miá»n TĂ¢y vĂ  Ä‘áº·c sáº£n Ä‘á»‹a phÆ°Æ¡ng.", 19),
        new("family_restaurant", "QuĂ¡n gia Ä‘Ă¬nh", "Äiá»ƒm ngá»“i láº¡i lĂ¢u, phĂ¹ há»£p nhĂ³m vĂ  gia Ä‘Ă¬nh.", 20)
    ];

    private static readonly PoiSeed[] PoiSeeds =
    [
        new(0, "á»c Oanh", "QuĂ¡n á»‘c bĂ¬nh dĂ¢n ná»•i tiáº¿ng vá»›i nhiá»u mĂ³n xĂ o bÆ¡, rang muá»‘i vĂ  sá»‘t me.", "seafood", "534 VÄ©nh KhĂ¡nh", "PhÆ°á»ng 13", 10.7592, 106.7045, "$$", 4.6, 1240, 20, "https://images.unsplash.com/photo-1559737558-2f5a35f4523b?auto=format&fit=crop&w=1200&q=85", "ÄÄ©a á»‘c xĂ o bÆ¡ tá»i nĂ³ng há»•i.", ["háº£i sáº£n", "vÄ©nh khĂ¡nh", "buá»•i tá»‘i"]),
        new(1, "BĂ¡nh mĂ¬ cháº£o CĂ´ 3 Háº­u", "BĂ¡nh mĂ¬ cháº£o nĂ³ng há»•i, phĂ¹ há»£p cho bá»¯a sĂ¡ng vĂ  bá»¯a trÆ°a nhanh.", "banh_mi", "36 Nguyá»…n Há»¯u HĂ o", "PhÆ°á»ng 13", 10.7580, 106.7018, "$", 4.4, 630, 19, "https://images.unsplash.com/photo-1601050690597-df0568f70950?auto=format&fit=crop&w=1200&q=85", "Cháº£o bĂ¡nh mĂ¬ vá»›i trá»©ng, pate vĂ  xĂºc xĂ­ch.", ["bĂ¡nh mĂ¬", "bá»¯a sĂ¡ng", "Ä‘á»‹a phÆ°Æ¡ng"]),
        new(2, "CÆ¡m táº¥m CĂ¢y Äiá»‡p", "CÆ¡m táº¥m sÆ°á»n nÆ°á»›ng thÆ¡m, phá»¥c vá»¥ nhanh trong khĂ´ng khĂ­ Quáº­n 4 thĂ¢n thuá»™c.", "broken_rice", "140/1 ÄoĂ n VÄƒn BÆ¡", "PhÆ°á»ng 13", 10.7548, 106.7049, "$", 4.5, 890, 18, "https://images.unsplash.com/photo-1515003197210-e0cd71810b5f?auto=format&fit=crop&w=1200&q=85", "Pháº§n cÆ¡m táº¥m sÆ°á»n bĂ¬ cháº£ Ä‘áº§y Ä‘áº·n.", ["cÆ¡m táº¥m", "sÆ°á»n nÆ°á»›ng", "trÆ°a"]),
        new(3, "SĂºp cua CĂ´ BĂ´ng", "SĂºp cua nĂ³ng vá»›i thá»‹t cua, trá»©ng cĂºt vĂ  náº¥m, mĂ³n Äƒn váº·t quen thuá»™c.", "porridge_soup", "22 ÄoĂ n VÄƒn BÆ¡", "PhÆ°á»ng 13", 10.7566, 106.7032, "$", 4.3, 410, 17, "https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=1200&q=85", "ChĂ©n sĂºp cua nĂ³ng cĂ³ trá»©ng cĂºt.", ["sĂºp cua", "Äƒn váº·t", "chiá»u tá»‘i"]),
        new(4, "CĂ  phĂª bá» kĂªnh KhĂ¡nh Há»™i", "KhĂ´ng gian cĂ  phĂª thoĂ¡ng bĂªn bá» kĂªnh, thĂ­ch há»£p nghá»‰ chĂ¢n sau hĂ nh trĂ¬nh khĂ¡m phĂ¡.", "coffee", "Báº¿n VĂ¢n Äá»“n", "PhÆ°á»ng 13", 10.7610, 106.7068, "$$", 4.2, 245, 16, "https://images.unsplash.com/photo-1495474472287-4d71bcdd2085?auto=format&fit=crop&w=1200&q=85", "Ly cĂ  phĂª vĂ  gĂ³c ngá»“i nhĂ¬n ra bá» kĂªnh.", ["cĂ  phĂª", "view kĂªnh", "thÆ° giĂ£n"]),
        new(5, "BĂºn bĂ² KhĂ¡nh Há»™i", "QuĂ¡n bĂºn bĂ² vá»‹ Ä‘áº­m, nÆ°á»›c dĂ¹ng thÆ¡m sáº£ vĂ  Ä‘Ă´ng khĂ¡ch buá»•i sĂ¡ng.", "regional", "91 KhĂ¡nh Há»™i", "PhÆ°á»ng 3", 10.7602, 106.7059, "$", 4.4, 380, 15, "https://images.unsplash.com/photo-1544025162-d76694265947?auto=format&fit=crop&w=1200&q=85", "TĂ´ bĂºn bĂ² Ä‘áº§y topping vĂ  rau sá»‘ng.", ["bĂºn bĂ²", "huáº¿", "buá»•i sĂ¡ng"]),
        new(6, "BĂ¡nh trĂ¡ng trá»™n ChĂº Vi", "Xe bĂ¡nh trĂ¡ng trá»™n nhiá»u topping, vá»‹ chua cay Ä‘áº­m Ä‘Ă  kiá»ƒu há»c sinh sinh viĂªn.", "street_food", "17 TĂ´n Äáº£n", "PhÆ°á»ng 13", 10.7555, 106.7026, "$", 4.1, 210, 14, "https://images.unsplash.com/photo-1504754524776-8f4f37790ca0?auto=format&fit=crop&w=1200&q=85", "Há»™p bĂ¡nh trĂ¡ng trá»™n vá»›i khĂ´ bĂ² vĂ  trá»©ng cĂºt.", ["bĂ¡nh trĂ¡ng", "Äƒn váº·t", "takeaway"]),
        new(7, "Há»§ tiáº¿u má»±c Ă”ng GiĂ  Cali", "Há»§ tiáº¿u má»±c vá»‹ ngá»t thanh, topping má»±c giĂ²n vĂ  tĂ´m tÆ°Æ¡i.", "noodles", "45 VÄ©nh KhĂ¡nh", "PhÆ°á»ng 8", 10.7587, 106.7051, "$$", 4.5, 570, 13, "https://images.unsplash.com/photo-1617093727343-374698b1b08d?auto=format&fit=crop&w=1200&q=85", "TĂ´ há»§ tiáº¿u má»±c vá»›i má»±c á»‘ng vĂ  tĂ´m.", ["há»§ tiáº¿u", "má»±c", "Ä‘áº·c sáº£n"]),
        new(8, "ChĂ¨ dá»«a dáº§m VÄ©nh Há»™i", "Ly chĂ¨ dá»«a dáº§m mĂ¡t láº¡nh, vá»‹ bĂ©o nháº¹ phĂ¹ há»£p buá»•i chiá»u nĂ³ng.", "tea_dessert", "12 VÄ©nh Há»™i", "PhÆ°á»ng 4", 10.7571, 106.7080, "$", 4.0, 165, 12, "https://images.unsplash.com/photo-1488477181946-6428a0291777?auto=format&fit=crop&w=1200&q=85", "Cá»‘c chĂ¨ dá»«a dáº§m vá»›i Ä‘Ă¡ bĂ o vĂ  tháº¡ch.", ["chĂ¨", "dá»«a", "giáº£i nhiá»‡t"]),
        new(9, "Láº©u cĂ¡ kĂ¨o BĂ  Huyá»‡n", "QuĂ¡n láº©u cĂ¡ kĂ¨o chua cay, há»£p nhĂ³m báº¡n vĂ  gia Ä‘Ă¬nh Äƒn tá»‘i.", "seafood_hotpot", "102 HoĂ ng Diá»‡u", "PhÆ°á»ng 9", 10.7519, 106.7040, "$$$", 4.3, 290, 11, "https://images.unsplash.com/photo-1517248135467-4c7edcad34c4?auto=format&fit=crop&w=1200&q=85", "Ná»“i láº©u cĂ¡ kĂ¨o nghi ngĂºt khĂ³i.", ["láº©u", "cĂ¡ kĂ¨o", "gia Ä‘Ă¬nh"]),
        new(10, "Gá»i cuá»‘n CĂ´ SĂ¡u", "Gá»i cuá»‘n cuá»‘n tay má»—i ngĂ y, rau tÆ°Æ¡i vĂ  nÆ°á»›c cháº¥m Ä‘áº­u phá»™ng Ä‘áº­m vá»‹.", "vietnamese_food", "58 XĂ³m Chiáº¿u", "PhÆ°á»ng 15", 10.7545, 106.7071, "$", 4.2, 142, 10, "https://images.unsplash.com/photo-1559847844-5315695dadae?auto=format&fit=crop&w=1200&q=85", "ÄÄ©a gá»i cuá»‘n tĂ´m thá»‹t cháº¥m sá»‘t Ä‘áº­u.", ["gá»i cuá»‘n", "nháº¹ bá»¥ng", "buá»•i chiá»u"]),
        new(11, "PhĂ¡ láº¥u bĂ² Chá»£ 200", "PhĂ¡ láº¥u nÆ°á»›c cá»‘t dá»«a thÆ¡m bĂ©o, Äƒn kĂ¨m bĂ¡nh mĂ¬ hoáº·c mĂ¬ gĂ³i.", "night_food", "200 XĂ³m Chiáº¿u", "PhÆ°á»ng 14", 10.7528, 106.7062, "$", 4.4, 335, 9, "https://images.unsplash.com/photo-1559847844-d721426d6edc?auto=format&fit=crop&w=1200&q=85", "TĂ´ phĂ¡ láº¥u nĂ³ng vá»›i bĂ¡nh mĂ¬ giĂ²n.", ["phĂ¡ láº¥u", "Äƒn khuya", "xĂ³m chiáº¿u"]),
        new(12, "CÆ¡m niĂªu KhĂ³i Báº¿p", "QuĂ¡n cÆ¡m niĂªu phá»¥c vá»¥ mĂ³n nhĂ , phĂ¹ há»£p nhĂ³m gia Ä‘Ă¬nh muá»‘n ngá»“i lĂ¢u.", "family_restaurant", "84 Báº¿n VĂ¢n Äá»“n", "PhÆ°á»ng 1", 10.7606, 106.7085, "$$$", 4.5, 198, 8, "https://images.unsplash.com/photo-1563379091339-03246963d96c?auto=format&fit=crop&w=1200&q=85", "BĂ n cÆ¡m niĂªu nhiá»u mĂ³n máº·n truyá»n thá»‘ng.", ["cÆ¡m niĂªu", "gia Ä‘Ă¬nh", "mĂ³n viá»‡t"]),
        new(13, "BĂ¡nh flan XĂ³m Chiáº¿u", "Quáº§y bĂ¡nh flan má»m má»‹n, thĂªm cĂ  phĂª vĂ  Ä‘Ă¡ bĂ o theo kiá»ƒu SĂ i GĂ²n.", "dessert", "73 XĂ³m Chiáº¿u", "PhÆ°á»ng 16", 10.7535, 106.7057, "$", 4.1, 188, 7, "https://images.unsplash.com/photo-1482049016688-2d3e1b311543?auto=format&fit=crop&w=1200&q=85", "Ly bĂ¡nh flan cĂ  phĂª vĂ  Ä‘Ă¡ bĂ o.", ["flan", "trĂ¡ng miá»‡ng", "giĂ¡ má»m"]),
        new(14, "BĂ¡nh canh cua Háº»m 48", "BĂ¡nh canh cua sá»£i dĂ y, nÆ°á»›c dĂ¹ng ngá»t vĂ  topping cháº£ cua Ä‘áº§y Ä‘á»§.", "porridge_soup", "48/7 TĂ´n Äáº£n", "PhÆ°á»ng 13", 10.7560, 106.7040, "$$", 4.3, 256, 6, "https://images.unsplash.com/photo-1512058564366-18510be2db19?auto=format&fit=crop&w=1200&q=85", "TĂ´ bĂ¡nh canh cua vá»›i cháº£ cua vĂ  thá»‹t.", ["bĂ¡nh canh", "cua", "tá»‘i"]),
        new(15, "á»c len xĂ o dá»«a Chá»‹ MÆ°á»i", "á»c len xĂ o dá»«a bĂ©o thÆ¡m, Äƒn cĂ¹ng bĂ¡nh mĂ¬ nĂ³ng ráº¥t báº¯t vá»‹.", "snails", "109 VÄ©nh KhĂ¡nh", "PhÆ°á»ng 8", 10.7597, 106.7038, "$$", 4.6, 610, 5, "https://images.unsplash.com/photo-1467003909585-2f8a72700288?auto=format&fit=crop&w=1200&q=85", "ÄÄ©a á»‘c len xĂ o dá»«a vĂ  bĂ¡nh mĂ¬ nÆ°á»›ng.", ["á»‘c len", "xĂ o dá»«a", "Ä‘áº·c sáº£n"]),
        new(16, "TrĂ  trĂ¡i cĂ¢y Cáº§u Calmette", "Xe trĂ  trĂ¡i cĂ¢y vĂ  nÆ°á»›c Ă©p mĂ¡t láº¡nh gáº§n khu bá» kĂªnh, há»£p khĂ¡ch Ä‘i bá»™.", "drink", "28 Báº¿n VĂ¢n Äá»“n", "PhÆ°á»ng 12", 10.7622, 106.7034, "$", 4.0, 120, 4, "https://images.unsplash.com/photo-1499636136210-6f4ee915583e?auto=format&fit=crop&w=1200&q=85", "Ly trĂ  trĂ¡i cĂ¢y vá»›i cam, dĂ¢u vĂ  báº¡c hĂ .", ["trĂ  trĂ¡i cĂ¢y", "nÆ°á»›c Ă©p", "giáº£i khĂ¡t"]),
        new(17, "BĂºn máº¯m Cáº§u Ă”ng LĂ£nh", "BĂºn máº¯m Ä‘áº­m vá»‹ miá»n TĂ¢y, topping háº£i sáº£n vĂ  heo quay Ä‘áº§y tĂ´.", "regional", "16 Nguyá»…n KhoĂ¡i", "PhÆ°á»ng 1", 10.7589, 106.7089, "$$", 4.2, 174, 3, "https://images.unsplash.com/photo-1526318896980-cf78c088247c?auto=format&fit=crop&w=1200&q=85", "TĂ´ bĂºn máº¯m Ä‘áº§y rau sá»‘ng vĂ  háº£i sáº£n.", ["bĂºn máº¯m", "miá»n tĂ¢y", "Ä‘áº­m vá»‹"]),
        new(18, "BĂ¡nh xĂ¨o TĂ´m Nháº£y 46", "BĂ¡nh xĂ¨o Ä‘á»• giĂ²n, nhĂ¢n tĂ´m thá»‹t vĂ  rau sá»‘ng Äƒn kĂ¨m phong phĂº.", "vietnamese_food", "46 KhĂ¡nh Há»™i", "PhÆ°á»ng 6", 10.7599, 106.7075, "$$", 4.3, 230, 2, "https://images.unsplash.com/photo-1625944524160-6cf6b4d5ad28?auto=format&fit=crop&w=1200&q=85", "BĂ¡nh xĂ¨o vĂ ng giĂ²n cuá»‘n rau sá»‘ng.", ["bĂ¡nh xĂ¨o", "tĂ´m nháº£y", "mĂ³n viá»‡t"]),
        new(19, "Tiá»‡m chay An NhiĂªn", "QuĂ¡n chay nhá» yĂªn tÄ©nh vá»›i cÆ¡m pháº§n, bĂºn vĂ  mĂ³n xĂ o thanh Ä‘áº¡m.", "vegetarian", "11 HoĂ ng Diá»‡u", "PhÆ°á»ng 10", 10.7512, 106.7067, "$$", 4.1, 98, 1, "https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=1200&q=85", "MĂ¢m cÆ¡m chay nhiá»u rau vĂ  Ä‘áº­u hÅ©.", ["Äƒn chay", "thanh Ä‘áº¡m", "gia Ä‘Ă¬nh"])
    ];

    private sealed record DemoUserSeed(
        int Index,
        string Email,
        string FullName,
        string PhoneNumber,
        string OwnerStatus,
        string BusinessName,
        string BusinessAddress,
        string BusinessDescription,
        string AvatarUrl);

    private sealed record CategorySeed(string Code, string Name, string Description, int SortOrder);

    private sealed record PoiSeed(
        int Index,
        string Name,
        string Description,
        string CategoryCode,
        string Address,
        string Ward,
        double Latitude,
        double Longitude,
        string PriceRange,
        double Rating,
        int ReviewCount,
        int Priority,
        string ImageUrl,
        string Caption,
        List<string> Tags);
}

