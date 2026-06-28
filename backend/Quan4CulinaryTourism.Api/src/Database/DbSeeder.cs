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
        var pois = await _context.Pois.Find(x => seedMap.Keys.Contains(x.Name)).ToListAsync(cancellationToken);

        foreach (var poi in pois)
        {
            if (!seedMap.TryGetValue(poi.Name, out var seed))
            {
                continue;
            }

            var shouldUpdate = false;

            if (string.IsNullOrWhiteSpace(poi.Description))
            {
                poi.Description = seed.Description;
                shouldUpdate = true;
            }

            if (string.IsNullOrWhiteSpace(poi.TtsScript))
            {
                poi.TtsScript = BuildVietnameseNarrationScript(seed.Name, seed.Description);
                shouldUpdate = true;
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

        var existingKeys = await _context.PoiAudios.Find(FilterDefinition<PoiAudio>.Empty)
            .Project(x => x.PoiId + ":" + x.Lang)
            .ToListAsync(cancellationToken);

        var audios = new List<PoiAudio>();
        foreach (var (poi, index) in pois.Select((poi, index) => (poi, index)))
        {
            var key = poi.Id + ":vi";
            if (existingKeys.Contains(key, StringComparer.Ordinal))
            {
                continue;
            }

            audios.Add(new PoiAudio
            {
                PoiId = poi.Id,
                Lang = "vi",
                AudioUrl = SampleAudioUrls[index % SampleAudioUrls.Length],
                VoiceName = "vi-VN-HoaiMyNeural",
                SourceType = "uploaded",
                Status = SharedConstants.AudioDone,
                DurationSeconds = 36 + (index % 8) * 6,
                FileSizeBytes = 180_000 + index * 7_500,
                CreatedAt = DateTime.UtcNow.AddDays(-(index + 1)),
                UpdatedAt = DateTime.UtcNow.AddHours(-index)
            });
        }

        if (audios.Count > 0)
        {
            await _context.PoiAudios.InsertManyAsync(audios, cancellationToken: cancellationToken);
        }

        if (pois.Count > 0)
        {
            var poiIds = pois.Select(static poi => poi.Id).ToList();
            await _context.Pois.UpdateManyAsync(
                x => poiIds.Contains(x.Id),
                Builders<Poi>.Update.Set(x => x.AudioStatus, SharedConstants.AudioDone),
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
                Description = $"Chu quan de xuat cap nhat noi dung, hinh anh va thong tin thu hut khach cho diem {OwnerSubmissionAreas[index % OwnerSubmissionAreas.Length]}.",
                CategoryId = category.Id,
                Location = GeoLocationFactory.Create(106.7005 + index * 0.00033, 10.7520 + index * 0.00024),
                Address = $"{20 + index} {OwnerSubmissionStreets[index % OwnerSubmissionStreets.Length]}",
                Ward = $"Phuong {(index % 4) + 1}",
                District = "Quận 4",
                City = "TP. Hồ Chí Minh",
                PriceRange = SharedConstants.PriceRanges[index % SharedConstants.PriceRanges.Length],
                Priority = 20 - index,
                MapUrl = $"https://www.google.com/maps/search/?api=1&query={10.7520 + index * 0.00024},{106.7005 + index * 0.00033}",
                TtsScript = $"Noi dung de xuat cho diem am thuc {OwnerSubmissionAreas[index % OwnerSubmissionAreas.Length]}.",
                GeofenceRadiusMeters = 90 + index * 5,
                AutoNarrationEnabled = index % 4 != 0,
                Images =
                [
                    new PoiImage
                    {
                        Url = PoiSeeds[index % PoiSeeds.Length].ImageUrl,
                        Caption = "Anh de xuat tu chu quan",
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
                    SharedConstants.SubmissionApproved => "Da thong qua de cho cap nhat vao kho du lieu demo.",
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
                    ? "Quet la mo ngay noi dung thuyet minh tai diem dung."
                    : "Ma QR danh cho khach du lich muon nghe audio nhanh.",
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
        District = "Quận 4",
        City = "TP. Hồ Chí Minh",
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
                new OpeningHour { DayOfWeek = "Thứ Hai", OpenTime = "", CloseTime = "", IsClosed = true },
                new OpeningHour { DayOfWeek = "Thứ Ba - Chủ Nhật", OpenTime = "09:30", CloseTime = "22:00" }
            ];
        }

        if (index % 3 == 0)
        {
            return
            [
                new OpeningHour { DayOfWeek = "Thứ Hai - Thứ Sáu", OpenTime = "07:00", CloseTime = "21:30" },
                new OpeningHour { DayOfWeek = "Thứ Bảy - Chủ Nhật", OpenTime = "08:00", CloseTime = "22:30" }
            ];
        }

        return
        [
            new OpeningHour { DayOfWeek = "Thứ Hai - Chủ Nhật", OpenTime = "10:00", CloseTime = "22:30" }
        ];
    }

    private static string BuildEnglishDescription(Poi poi, int index) =>
        $"{poi.Name} is a popular District 4 food stop near {poi.Address}, known for a local atmosphere and crowd-favorite dishes. This seed translation #{index + 1:00} helps demo multilingual content in the CMS and mobile app.";

    private static string BuildEnglishNarrationScript(string poiName) =>
        $"Welcome to {poiName}, one of the notable food stops in District 4.";

    private static string BuildVietnameseNarrationScript(string poiName, string description) =>
        $"Gioi thieu nhanh ve {poiName}: {description}";

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
        "ốc",
        "cơm tấm",
        "bún",
        "chè",
        "cà phê",
        "hải sản",
        "tráng miệng",
        "ăn đêm"
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
        "Vĩnh Khánh",
        "Khánh Hội",
        "Xóm Chiếu",
        "Đoàn Văn Bơ",
        "Tôn Đản",
        "Hoàng Diệu"
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
        "Trạm xe buýt Khánh Hội",
        "Trạm xe buýt Vĩnh Hội",
        "Trạm xe buýt Xóm Chiếu",
        "Điểm dừng Vĩnh Khánh",
        "Điểm dừng Đoàn Văn Bơ",
        "Điểm dừng Tôn Đản",
        "Điểm dừng Hoàng Diệu",
        "Điểm dừng Kênh Tẻ",
        "Điểm dừng Bến Vân Đồn",
        "Trạm Khánh Hội hướng cầu",
        "Điểm check-in Vĩnh Khánh",
        "Điểm chợ Xóm Chiếu",
        "Đoàn Văn Bơ giao lộ",
        "Điểm xe buýt Tôn Đản",
        "Hoàng Diệu khu ẩm thực",
        "Bến Vân Đồn ven kênh",
        "Trạm Vĩnh Hội hướng chợ",
        "Phường 3 - cổng chợ",
        "Phường 13 - khu ăn đêm",
        "Điểm giới thiệu ẩm thực Quận 4"
    ];

    private static readonly string[] QrZones =
    [
        "Khánh Hội",
        "Vĩnh Hội",
        "Xóm Chiếu",
        "Vĩnh Khánh",
        "Đoàn Văn Bơ",
        "Tôn Đản",
        "Hoàng Diệu",
        "Kênh Tẻ",
        "Bến Vân Đồn",
        "Khánh Hội",
        "Vĩnh Khánh",
        "Xóm Chiếu",
        "Đoàn Văn Bơ",
        "Tôn Đản",
        "Hoàng Diệu",
        "Bến Vân Đồn",
        "Vĩnh Hội",
        "Phường 3",
        "Phường 13",
        "Trung tâm Quận 4"
    ];

    private static readonly string[] QrAddresses =
    [
        "Bến Vân Đồn - cầu Khánh Hội",
        "Tôn Đản - khu Vĩnh Hội",
        "Chợ Xóm Chiếu - đường Xóm Chiếu",
        "Phố ẩm thực Vĩnh Khánh",
        "Ngã tư Đoàn Văn Bơ - Nguyễn Khoái",
        "Khu dân cư Tôn Đản",
        "Hoàng Diệu - đoạn chợ chiều",
        "Lối xuống Kênh Tẻ",
        "Bờ kênh Bến Vân Đồn",
        "Cầu Khánh Hội hướng Quận 1",
        "Đầu đường Vĩnh Khánh",
        "Cổng chợ Xóm Chiếu",
        "Đoàn Văn Bơ gần trường học",
        "Bến xe buýt Tôn Đản",
        "Vỉa hè Hoàng Diệu",
        "Bến Vân Đồn hướng cầu Calmette",
        "Trạm Vĩnh Hội gần chung cư",
        "Phường 3 - khu dân cư",
        "Phường 13 - khu hàng quán",
        "Điểm tổng hợp thông tin du lịch"
    ];

    private static readonly DemoUserSeed[] DemoUsers =
    [
        new(1, "tran.minh.hieu@quan4tourism.local", "Trần Minh Hiếu", "0908000101", SharedConstants.OwnerApproved, "Ốc Đêm Hiếu", "102 Vĩnh Khánh", "Quán hải sản bình dân mở khuya.", "https://images.unsplash.com/photo-1500648767791-00dcc994a43e?auto=format&fit=crop&w=400&q=80"),
        new(2, "nguyen.thao.my@quan4tourism.local", "Nguyễn Thảo My", "0908000102", SharedConstants.OwnerApproved, "Bếp Nhà My", "18 Đoàn Văn Bơ", "Quán cơm gia đình chuyên món Việt.", "https://images.unsplash.com/photo-1494790108377-be9c29b29330?auto=format&fit=crop&w=400&q=80"),
        new(3, "pham.quoc.an@quan4tourism.local", "Phạm Quốc An", "0908000103", SharedConstants.OwnerApproved, "Bún Phố An", "29 Xóm Chiếu", "Tiệm bún sáng phục vụ dân địa phương.", "https://images.unsplash.com/photo-1506794778202-cad84cf45f1d?auto=format&fit=crop&w=400&q=80"),
        new(4, "le.kim.ngan@quan4tourism.local", "Lê Kim Ngân", "0908000104", SharedConstants.OwnerApproved, "Chè Ngân 1988", "44 Tôn Đản", "Quầy chè và tráng miệng lâu năm.", "https://images.unsplash.com/photo-1488426862026-3ee34a7d66df?auto=format&fit=crop&w=400&q=80"),
        new(5, "vo.huu.phuc@quan4tourism.local", "Võ Hữu Phúc", "0908000105", SharedConstants.OwnerApproved, "Cà phê Bờ Kênh Phúc", "75 Bến Vân Đồn", "Quán cà phê nhỏ nhìn ra kênh.", "https://images.unsplash.com/photo-1507591064344-4c6ce005b128?auto=format&fit=crop&w=400&q=80"),
        new(6, "doan.nhat.linh@quan4tourism.local", "Đoàn Nhật Linh", "0908000106", SharedConstants.OwnerApproved, "Bánh Tráng Linh", "12 Khánh Hội", "Quầy bánh tráng trộn và đồ ăn vặt.", "https://images.unsplash.com/photo-1544005313-94ddf0286df2?auto=format&fit=crop&w=400&q=80"),
        new(7, "truong.bao.chau@quan4tourism.local", "Trương Bảo Châu", "0908000107", SharedConstants.OwnerApproved, "Lẩu Nướng Bảo Châu", "131 Hoàng Diệu", "Quán lẩu nướng cho nhóm bạn trẻ.", "https://images.unsplash.com/photo-1546961329-78bef0414d7c?auto=format&fit=crop&w=400&q=80"),
        new(8, "bui.gia.khanh@quan4tourism.local", "Bùi Gia Khánh", "0908000108", SharedConstants.OwnerApproved, "Mì Khuya Gia Khánh", "9 Nguyễn Khoái", "Quán mì và hủ tiếu mở tối.", "https://images.unsplash.com/photo-1504257432389-52343af06ae3?auto=format&fit=crop&w=400&q=80"),
        new(9, "hoang.mai.phuong@quan4tourism.local", "Hoàng Mai Phương", "0908000109", SharedConstants.OwnerPending, "Bún Bò Mai Phương", "22 Vĩnh Hội", "Đang xin duyệt gian hàng bún bò.", "https://images.unsplash.com/photo-1438761681033-6461ffad8d80?auto=format&fit=crop&w=400&q=80"),
        new(10, "dang.thanh.tung@quan4tourism.local", "Đặng Thanh Tùng", "0908000110", SharedConstants.OwnerPending, "Cơm Tối Thanh Tùng", "48 Tôn Đản", "Quán cơm tối phục vụ dân văn phòng.", "https://images.unsplash.com/photo-1500648767791-00dcc994a43e?auto=format&fit=crop&w=400&q=80"),
        new(11, "phan.ngoc.ha@quan4tourism.local", "Phan Ngọc Hà", "0908000111", SharedConstants.OwnerPending, "Kem Dừa Ngọc Hà", "63 Xóm Chiếu", "Quầy kem dừa và món mát.", "https://images.unsplash.com/photo-1494790108377-be9c29b29330?auto=format&fit=crop&w=400&q=80"),
        new(12, "ngo.minh.duc@quan4tourism.local", "Ngô Minh Đức", "0908000112", SharedConstants.OwnerPending, "Bánh Canh Minh Đức", "88 Khánh Hội", "Bánh canh và súp nóng phục vụ sáng.", "https://images.unsplash.com/photo-1506794778202-cad84cf45f1d?auto=format&fit=crop&w=400&q=80"),
        new(13, "ly.thu.trang@quan4tourism.local", "Lý Thu Trang", "0908000113", SharedConstants.OwnerPending, "Tiệm Ăn Thu Trang", "37 Đoàn Văn Bơ", "Đề xuất mở điểm ăn gia đình.", "https://images.unsplash.com/photo-1488426862026-3ee34a7d66df?auto=format&fit=crop&w=400&q=80"),
        new(14, "cao.viet.long@quan4tourism.local", "Cao Việt Long", "0908000114", SharedConstants.OwnerPending, "Ốc Long Sài Gòn", "118 Vĩnh Khánh", "Quán ốc cần bổ sung giấy phép.", "https://images.unsplash.com/photo-1507591064344-4c6ce005b128?auto=format&fit=crop&w=400&q=80"),
        new(15, "lam.gia.han@quan4tourism.local", "Lâm Gia Hân", "0908000115", SharedConstants.OwnerRejected, "Trà Sữa Gia Hân", "11 Hoàng Diệu", "Hồ sơ chưa đủ minh chứng nguồn gốc.", "https://images.unsplash.com/photo-1544005313-94ddf0286df2?auto=format&fit=crop&w=400&q=80"),
        new(16, "ta.huu.nghia@quan4tourism.local", "Tạ Hữu Nghĩa", "0908000116", SharedConstants.OwnerRejected, "Bò Lá Lốt Hữu Nghĩa", "55 Tôn Đản", "Thông tin liên hệ chưa xác thực.", "https://images.unsplash.com/photo-1546961329-78bef0414d7c?auto=format&fit=crop&w=400&q=80"),
        new(17, "mai.khanh.ly@quan4tourism.local", "Mai Khánh Ly", "0908000117", SharedConstants.OwnerRejected, "Tiệm Chè Khánh Ly", "70 Bến Vân Đồn", "Ảnh minh họa chưa đúng địa điểm.", "https://images.unsplash.com/photo-1438761681033-6461ffad8d80?auto=format&fit=crop&w=400&q=80"),
        new(18, "duong.anh.khoa@quan4tourism.local", "Dương Anh Khoa", "0908000118", SharedConstants.OwnerRejected, "Bánh Mì Anh Khoa", "94 Xóm Chiếu", "Thiếu mô tả sản phẩm và giờ mở cửa.", "https://images.unsplash.com/photo-1504257432389-52343af06ae3?auto=format&fit=crop&w=400&q=80"),
        new(19, "chau.ngoc.nhu@quan4tourism.local", "Châu Ngọc Như", "0908000119", SharedConstants.OwnerRejected, "Tiệm Ăn Ngọc Như", "19 Vĩnh Hội", "Chưa cập nhật bảng giá rõ ràng.", "https://images.unsplash.com/photo-1494790108377-be9c29b29330?auto=format&fit=crop&w=400&q=80"),
        new(20, "luu.minh.quan@quan4tourism.local", "Lưu Minh Quân", "0908000120", SharedConstants.OwnerRejected, "Bún Mắm Minh Quân", "101 Nguyễn Tất Thành", "Địa chỉ đăng ký chưa khớp thực tế.", "https://images.unsplash.com/photo-1506794778202-cad84cf45f1d?auto=format&fit=crop&w=400&q=80")
    ];

    private static readonly CategorySeed[] CategorySeeds =
    [
        new("street_food", "Ăn vặt", "Các món ăn nhanh, ăn chơi đặc trưng khu phố.", 1),
        new("rice", "Cơm", "Cơm tấm, cơm gia đình và các phần cơm quen thuộc.", 2),
        new("noodles", "Bún / Phở / Mì", "Các món nước và món sợi phổ biến.", 3),
        new("seafood", "Hải sản", "Ốc, tôm, cua, nghêu và các món hải sản đêm.", 4),
        new("coffee", "Cà phê", "Quán cà phê và điểm nghỉ chân ven kênh.", 5),
        new("dessert", "Tráng miệng", "Chè, bánh ngọt, kem và món ngọt.", 6),
        new("drink", "Đồ uống", "Nước ép, trà sữa, đồ uống giải khát.", 7),
        new("hotpot_bbq", "Lẩu / Nướng", "Nhóm món lẩu và nướng tụ tập bạn bè.", 8),
        new("vietnamese_food", "Món Việt", "Những quán món Việt địa phương.", 9),
        new("night_food", "Quán đêm", "Điểm ăn khuya và hàng quán mở muộn.", 10),
        new("banh_mi", "Bánh mì", "Bánh mì chảo, bánh mì thịt và biến tấu đường phố.", 11),
        new("porridge_soup", "Cháo / Súp", "Cháo, súp cua và món nóng nhẹ bụng.", 12),
        new("snails", "Ốc & nghêu", "Các quán ốc xào, hấp, rang muối đặc trưng.", 13),
        new("tea_dessert", "Chè & trà", "Chè truyền thống, trà trái cây và món mát.", 14),
        new("bakery", "Bánh & tiệm nướng", "Tiệm bánh mặn, ngọt và đồ nướng nhanh.", 15),
        new("vegetarian", "Ăn chay", "Điểm ăn chay và món thanh đạm.", 16),
        new("seafood_hotpot", "Lẩu hải sản", "Nhóm quán lẩu tập trung quanh khu đêm.", 17),
        new("broken_rice", "Cơm tấm chuyên biệt", "Những quán cơm tấm nổi bật.", 18),
        new("regional", "Đặc sản vùng miền", "Món Huế, miền Tây và đặc sản địa phương.", 19),
        new("family_restaurant", "Quán gia đình", "Điểm ngồi lại lâu, phù hợp nhóm và gia đình.", 20)
    ];

    private static readonly PoiSeed[] PoiSeeds =
    [
        new(0, "Ốc Oanh", "Quán ốc bình dân nổi tiếng với nhiều món xào bơ, rang muối và sốt me.", "seafood", "534 Vĩnh Khánh", "Phường 13", 10.7592, 106.7045, "$$", 4.6, 1240, 20, "https://images.unsplash.com/photo-1559737558-2f5a35f4523b?auto=format&fit=crop&w=1200&q=85", "Đĩa ốc xào bơ tỏi nóng hổi.", ["hải sản", "vĩnh khánh", "buổi tối"]),
        new(1, "Bánh mì chảo Cô 3 Hậu", "Bánh mì chảo nóng hổi, phù hợp cho bữa sáng và bữa trưa nhanh.", "banh_mi", "36 Nguyễn Hữu Hào", "Phường 13", 10.7580, 106.7018, "$", 4.4, 630, 19, "https://images.unsplash.com/photo-1601050690597-df0568f70950?auto=format&fit=crop&w=1200&q=85", "Chảo bánh mì với trứng, pate và xúc xích.", ["bánh mì", "bữa sáng", "địa phương"]),
        new(2, "Cơm tấm Cây Điệp", "Cơm tấm sườn nướng thơm, phục vụ nhanh trong không khí Quận 4 thân thuộc.", "broken_rice", "140/1 Đoàn Văn Bơ", "Phường 13", 10.7548, 106.7049, "$", 4.5, 890, 18, "https://images.unsplash.com/photo-1515003197210-e0cd71810b5f?auto=format&fit=crop&w=1200&q=85", "Phần cơm tấm sườn bì chả đầy đặn.", ["cơm tấm", "sườn nướng", "trưa"]),
        new(3, "Súp cua Cô Bông", "Súp cua nóng với thịt cua, trứng cút và nấm, món ăn vặt quen thuộc.", "porridge_soup", "22 Đoàn Văn Bơ", "Phường 13", 10.7566, 106.7032, "$", 4.3, 410, 17, "https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=1200&q=85", "Chén súp cua nóng có trứng cút.", ["súp cua", "ăn vặt", "chiều tối"]),
        new(4, "Cà phê bờ kênh Khánh Hội", "Không gian cà phê thoáng bên bờ kênh, thích hợp nghỉ chân sau hành trình khám phá.", "coffee", "Bến Vân Đồn", "Phường 13", 10.7610, 106.7068, "$$", 4.2, 245, 16, "https://images.unsplash.com/photo-1495474472287-4d71bcdd2085?auto=format&fit=crop&w=1200&q=85", "Ly cà phê và góc ngồi nhìn ra bờ kênh.", ["cà phê", "view kênh", "thư giãn"]),
        new(5, "Bún bò Khánh Hội", "Quán bún bò vị đậm, nước dùng thơm sả và đông khách buổi sáng.", "regional", "91 Khánh Hội", "Phường 3", 10.7602, 106.7059, "$", 4.4, 380, 15, "https://images.unsplash.com/photo-1544025162-d76694265947?auto=format&fit=crop&w=1200&q=85", "Tô bún bò đầy topping và rau sống.", ["bún bò", "huế", "buổi sáng"]),
        new(6, "Bánh tráng trộn Chú Vi", "Xe bánh tráng trộn nhiều topping, vị chua cay đậm đà kiểu học sinh sinh viên.", "street_food", "17 Tôn Đản", "Phường 13", 10.7555, 106.7026, "$", 4.1, 210, 14, "https://images.unsplash.com/photo-1504754524776-8f4f37790ca0?auto=format&fit=crop&w=1200&q=85", "Hộp bánh tráng trộn với khô bò và trứng cút.", ["bánh tráng", "ăn vặt", "takeaway"]),
        new(7, "Hủ tiếu mực Ông Già Cali", "Hủ tiếu mực vị ngọt thanh, topping mực giòn và tôm tươi.", "noodles", "45 Vĩnh Khánh", "Phường 8", 10.7587, 106.7051, "$$", 4.5, 570, 13, "https://images.unsplash.com/photo-1617093727343-374698b1b08d?auto=format&fit=crop&w=1200&q=85", "Tô hủ tiếu mực với mực ống và tôm.", ["hủ tiếu", "mực", "đặc sản"]),
        new(8, "Chè dừa dầm Vĩnh Hội", "Ly chè dừa dầm mát lạnh, vị béo nhẹ phù hợp buổi chiều nóng.", "tea_dessert", "12 Vĩnh Hội", "Phường 4", 10.7571, 106.7080, "$", 4.0, 165, 12, "https://images.unsplash.com/photo-1488477181946-6428a0291777?auto=format&fit=crop&w=1200&q=85", "Cốc chè dừa dầm với đá bào và thạch.", ["chè", "dừa", "giải nhiệt"]),
        new(9, "Lẩu cá kèo Bà Huyện", "Quán lẩu cá kèo chua cay, hợp nhóm bạn và gia đình ăn tối.", "seafood_hotpot", "102 Hoàng Diệu", "Phường 9", 10.7519, 106.7040, "$$$", 4.3, 290, 11, "https://images.unsplash.com/photo-1517248135467-4c7edcad34c4?auto=format&fit=crop&w=1200&q=85", "Nồi lẩu cá kèo nghi ngút khói.", ["lẩu", "cá kèo", "gia đình"]),
        new(10, "Gỏi cuốn Cô Sáu", "Gỏi cuốn cuốn tay mỗi ngày, rau tươi và nước chấm đậu phộng đậm vị.", "vietnamese_food", "58 Xóm Chiếu", "Phường 15", 10.7545, 106.7071, "$", 4.2, 142, 10, "https://images.unsplash.com/photo-1559847844-5315695dadae?auto=format&fit=crop&w=1200&q=85", "Đĩa gỏi cuốn tôm thịt chấm sốt đậu.", ["gỏi cuốn", "nhẹ bụng", "buổi chiều"]),
        new(11, "Phá lấu bò Chợ 200", "Phá lấu nước cốt dừa thơm béo, ăn kèm bánh mì hoặc mì gói.", "night_food", "200 Xóm Chiếu", "Phường 14", 10.7528, 106.7062, "$", 4.4, 335, 9, "https://images.unsplash.com/photo-1559847844-d721426d6edc?auto=format&fit=crop&w=1200&q=85", "Tô phá lấu nóng với bánh mì giòn.", ["phá lấu", "ăn khuya", "xóm chiếu"]),
        new(12, "Cơm niêu Khói Bếp", "Quán cơm niêu phục vụ món nhà, phù hợp nhóm gia đình muốn ngồi lâu.", "family_restaurant", "84 Bến Vân Đồn", "Phường 1", 10.7606, 106.7085, "$$$", 4.5, 198, 8, "https://images.unsplash.com/photo-1563379091339-03246963d96c?auto=format&fit=crop&w=1200&q=85", "Bàn cơm niêu nhiều món mặn truyền thống.", ["cơm niêu", "gia đình", "món việt"]),
        new(13, "Bánh flan Xóm Chiếu", "Quầy bánh flan mềm mịn, thêm cà phê và đá bào theo kiểu Sài Gòn.", "dessert", "73 Xóm Chiếu", "Phường 16", 10.7535, 106.7057, "$", 4.1, 188, 7, "https://images.unsplash.com/photo-1482049016688-2d3e1b311543?auto=format&fit=crop&w=1200&q=85", "Ly bánh flan cà phê và đá bào.", ["flan", "tráng miệng", "giá mềm"]),
        new(14, "Bánh canh cua Hẻm 48", "Bánh canh cua sợi dày, nước dùng ngọt và topping chả cua đầy đủ.", "porridge_soup", "48/7 Tôn Đản", "Phường 13", 10.7560, 106.7040, "$$", 4.3, 256, 6, "https://images.unsplash.com/photo-1512058564366-18510be2db19?auto=format&fit=crop&w=1200&q=85", "Tô bánh canh cua với chả cua và thịt.", ["bánh canh", "cua", "tối"]),
        new(15, "Ốc len xào dừa Chị Mười", "Ốc len xào dừa béo thơm, ăn cùng bánh mì nóng rất bắt vị.", "snails", "109 Vĩnh Khánh", "Phường 8", 10.7597, 106.7038, "$$", 4.6, 610, 5, "https://images.unsplash.com/photo-1467003909585-2f8a72700288?auto=format&fit=crop&w=1200&q=85", "Đĩa ốc len xào dừa và bánh mì nướng.", ["ốc len", "xào dừa", "đặc sản"]),
        new(16, "Trà trái cây Cầu Calmette", "Xe trà trái cây và nước ép mát lạnh gần khu bờ kênh, hợp khách đi bộ.", "drink", "28 Bến Vân Đồn", "Phường 12", 10.7622, 106.7034, "$", 4.0, 120, 4, "https://images.unsplash.com/photo-1499636136210-6f4ee915583e?auto=format&fit=crop&w=1200&q=85", "Ly trà trái cây với cam, dâu và bạc hà.", ["trà trái cây", "nước ép", "giải khát"]),
        new(17, "Bún mắm Cầu Ông Lãnh", "Bún mắm đậm vị miền Tây, topping hải sản và heo quay đầy tô.", "regional", "16 Nguyễn Khoái", "Phường 1", 10.7589, 106.7089, "$$", 4.2, 174, 3, "https://images.unsplash.com/photo-1526318896980-cf78c088247c?auto=format&fit=crop&w=1200&q=85", "Tô bún mắm đầy rau sống và hải sản.", ["bún mắm", "miền tây", "đậm vị"]),
        new(18, "Bánh xèo Tôm Nhảy 46", "Bánh xèo đổ giòn, nhân tôm thịt và rau sống ăn kèm phong phú.", "vietnamese_food", "46 Khánh Hội", "Phường 6", 10.7599, 106.7075, "$$", 4.3, 230, 2, "https://images.unsplash.com/photo-1625944524160-6cf6b4d5ad28?auto=format&fit=crop&w=1200&q=85", "Bánh xèo vàng giòn cuốn rau sống.", ["bánh xèo", "tôm nhảy", "món việt"]),
        new(19, "Tiệm chay An Nhiên", "Quán chay nhỏ yên tĩnh với cơm phần, bún và món xào thanh đạm.", "vegetarian", "11 Hoàng Diệu", "Phường 10", 10.7512, 106.7067, "$$", 4.1, 98, 1, "https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=1200&q=85", "Mâm cơm chay nhiều rau và đậu hũ.", ["ăn chay", "thanh đạm", "gia đình"])
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
