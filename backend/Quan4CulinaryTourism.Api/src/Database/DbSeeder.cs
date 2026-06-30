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

    public DbSeeder(
        MongoDbContext context,
        IOptions<DefaultAdminSettings> defaultAdmin,
        PasswordHasher passwordHasher)
    {
        _context = context;
        _defaultAdmin = defaultAdmin.Value;
        _passwordHasher = passwordHasher;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
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
        await SeedToursAsync(cancellationToken);
        await SeedQrActivationsAsync(cancellationToken);
        await SeedMapPacksAsync(cancellationToken);
    }

    private Task<List<Poi>> GetOrderedPoisAsync(CancellationToken cancellationToken) =>
        _context.Pois.Find(FilterDefinition<Poi>.Empty)
            .SortByDescending(x => x.Priority)
            .ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);

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
            OwnerStatus = SharedConstants.OwnerStatuses.Approved,
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
                Roles = seed.OwnerStatus == SharedConstants.OwnerStatuses.Approved
                    ? [SharedConstants.UserRoles.Owner, SharedConstants.UserRoles.User]
                    : [SharedConstants.UserRoles.User],
                IsActive = true,
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
        var pois = await GetOrderedPoisAsync(cancellationToken);
        foreach (var (poi, index) in pois.Select((poi, index) => (poi, index)))
        {
            var existingEnglish = await _context.PoiLocalizations
                .Find(x => x.PoiId == poi.Id && x.Lang == "en")
                .FirstOrDefaultAsync(cancellationToken);

            if (existingEnglish is not null)
            {
                var shouldUpdate = false;
                if (string.IsNullOrWhiteSpace(existingEnglish.Description))
                {
                    existingEnglish.Description = BuildEnglishDescription(poi, index);
                    shouldUpdate = true;
                }

                if (string.IsNullOrWhiteSpace(existingEnglish.TtsScript))
                {
                    existingEnglish.TtsScript = BuildEnglishNarrationScript(poi.Name);
                    shouldUpdate = true;
                }

                if (shouldUpdate)
                {
                    existingEnglish.UpdatedAt = DateTime.UtcNow;
                    await _context.PoiLocalizations.ReplaceOneAsync(
                        x => x.Id == existingEnglish.Id,
                        existingEnglish,
                        cancellationToken: cancellationToken);
                }
            }
            else
            {
                await _context.PoiLocalizations.InsertOneAsync(
                    new PoiLocalization
                    {
                        PoiId = poi.Id,
                        Lang = "en",
                        Name = poi.Name,
                        Description = BuildEnglishDescription(poi, index),
                        TtsScript = BuildEnglishNarrationScript(poi.Name),
                        IsFallback = false,
                        CreatedAt = DateTime.UtcNow.AddDays(-(index + 2)),
                        UpdatedAt = DateTime.UtcNow.AddDays(-(index % 3))
                    },
                    cancellationToken: cancellationToken);
            }
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
        var pois = await GetOrderedPoisAsync(cancellationToken);
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
                Builders<Poi>.Update.Set(x => x.AudioStatus, SharedConstants.AudioStatuses.Pending),
                cancellationToken: cancellationToken);
        }
    }

    private async Task SeedAudioTasksAsync(CancellationToken cancellationToken)
    {
        var pois = await GetOrderedPoisAsync(cancellationToken);

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
                Languages = [.. SharedConstants.Languages.Supported],
                ProgressPercent = status switch
                {
                    SharedConstants.AudioTaskStatuses.Done => 100,
                    SharedConstants.AudioTaskStatuses.Running => 65,
                    SharedConstants.AudioTaskStatuses.Paused => 48,
                    SharedConstants.AudioTaskStatuses.Failed => 72,
                    _ => 5
                },
                ErrorMessage = status == SharedConstants.AudioTaskStatuses.Failed ? "Cloud TTS timeout on demo seed." : null,
                PauseRequested = status == SharedConstants.AudioTaskStatuses.Paused,
                CancelRequested = false,
                HeartbeatAt = DateTime.UtcNow.AddMinutes(-(index + 2)),
                StartedAt = DateTime.UtcNow.AddHours(-(index + 1)),
                FinishedAt = status == SharedConstants.AudioTaskStatuses.Done ? DateTime.UtcNow.AddHours(-index) : null,
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
            var reviewed = seed.OwnerStatus != SharedConstants.OwnerStatuses.Pending;
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
                    SharedConstants.OwnerStatuses.Approved => "Ho so day du, da duyet de dua vao he thong demo.",
                    SharedConstants.OwnerStatuses.Rejected => "Ho so con thieu giay to hoac thong tin lien he.",
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
        var pois = await GetOrderedPoisAsync(cancellationToken);
        var categories = await _context.Categories.Find(FilterDefinition<Category>.Empty)
            .SortBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

        if (users.Count == 0 || categories.Count == 0 || pois.Count == 0)
        {
            return;
        }

        var existingKeys = await _context.OwnerSubmissions.Find(FilterDefinition<OwnerSubmission>.Empty)
            .Project(x => x.OwnerId + "|" + x.PoiName)
            .ToListAsync(cancellationToken);

        var submissions = new List<OwnerSubmission>();
        var submissionCount = Math.Max(20, Math.Min(32, pois.Count));
        for (var index = 0; index < submissionCount; index++)
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
                SubmissionType = SharedConstants.SubmissionTypes.Values[index % SharedConstants.SubmissionTypes.Values.Length],
                PoiName = poiName,
                Description = $"Ch? quán ð? xu?t c?p nh?t n?i dung, h?nh ?nh và thông tin thu hút khách cho ði?m {OwnerSubmissionAreas[index % OwnerSubmissionAreas.Length]}.",
                CategoryId = category.Id,
                Location = GeoLocationFactory.Create(106.7005 + index * 0.00033, 10.7520 + index * 0.00024),
                Address = $"{20 + index} {OwnerSubmissionStreets[index % OwnerSubmissionStreets.Length]}",
                Ward = $"Phý?ng {(index % 4) + 1}",
                District = "Qu?n 4",
                City = "TP. H? Chí Minh",
                PriceRange = SharedConstants.PriceRanges.Values[index % SharedConstants.PriceRanges.Values.Length],
                Priority = 20 - index,
                MapUrl = $"https://www.google.com/maps/search/?api=1&query={10.7520 + index * 0.00024},{106.7005 + index * 0.00033}",
                TtsScript = $"N?i dung ð? xu?t cho ði?m ?m th?c {OwnerSubmissionAreas[index % OwnerSubmissionAreas.Length]}.",
                GeofenceRadiusMeters = 90 + index * 5,
                AutoNarrationEnabled = index % 4 != 0,
                Images =
                [
                    new PoiImage
                    {
                        Url = PoiSeeds[index % PoiSeeds.Length].ImageUrl,
                        Caption = "?nh ð? xu?t t? ch? quán",
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
                    SharedConstants.SubmissionStatuses.Approved => "Ð? thông qua ð? ch? c?p nh?t vào kho d? li?u demo.",
                    SharedConstants.SubmissionStatuses.Rejected => "Can bo sung hinh anh va thong tin gia.",
                    _ => null
                },
                ReviewedBy = status == SharedConstants.SubmissionStatuses.Pending ? null : admin?.Id,
                ReviewedAt = status == SharedConstants.SubmissionStatuses.Pending ? null : DateTime.UtcNow.AddDays(-(index % 6 + 1)),
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
        var pois = await GetOrderedPoisAsync(cancellationToken);

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
        var pois = await GetOrderedPoisAsync(cancellationToken);

        var existingPageViewIds = await _context.AnalyticsEvents.Find(FilterDefinition<AnalyticsEvent>.Empty)
            .Project(x => x.PageViewId)
            .ToListAsync(cancellationToken);

        var events = new List<AnalyticsEvent>();
        foreach (var (poi, index) in pois.Select((poi, index) => (poi, index)))
        {
            for (var scenarioIndex = 0; scenarioIndex < 4; scenarioIndex++)
            {
                var pageViewId = $"seed-pageview-{index + 1:00}-{scenarioIndex + 1:00}";
                if (existingPageViewIds.Contains(pageViewId, StringComparer.Ordinal))
                {
                    continue;
                }

                var analyticsIndex = index * 4 + scenarioIndex;
                var eventName = SharedConstants.AnalyticsEvents.Values[analyticsIndex % SharedConstants.AnalyticsEvents.Values.Length];
                events.Add(new AnalyticsEvent
                {
                    AnonymousId = $"seed-guest-{index + 1:00}",
                    SessionId = $"seed-session-{index / 2 + 1:00}",
                    PageViewId = pageViewId,
                    EventName = eventName,
                    PoiId = eventName is "search_executed" or "nearby_requested" ? null : poi.Id,
                    Lang = analyticsIndex % 2 == 0 ? "vi" : "en",
                    Latitude = poi.Location.Coordinates.Latitude,
                    Longitude = poi.Location.Coordinates.Longitude,
                    AccuracyMeters = 8 + analyticsIndex,
                    ListenDurationSeconds = eventName is "audio_played" or "narration_completed" ? 28 + analyticsIndex * 2 : null,
                    IsBackground = eventName is "location_sample" or "geofence_triggered" ? analyticsIndex % 2 == 0 : null,
                    TrackingSource = eventName.Contains("location", StringComparison.Ordinal) || eventName.Contains("geofence", StringComparison.Ordinal)
                        ? "background-service"
                        : "web-user",
                    ContentType = eventName.Contains("audio", StringComparison.Ordinal) ? SharedConstants.AnalyticsContentTypes.Audio : SharedConstants.AnalyticsContentTypes.Poi,
                    Metadata = BuildAnalyticsMetadata(eventName, analyticsIndex),
                    CreatedAt = DateTime.UtcNow.AddMinutes(-(analyticsIndex * 11 + 6))
                });
            }
        }

        if (events.Count > 0)
        {
            await _context.AnalyticsEvents.InsertManyAsync(events, cancellationToken: cancellationToken);
        }
    }

    private async Task SeedToursAsync(CancellationToken cancellationToken)
    {
        var pois = await GetOrderedPoisAsync(cancellationToken);

        var existingTitles = await _context.Tours.Find(FilterDefinition<Tour>.Empty)
            .Project(x => x.Title + "|" + x.Lang)
            .ToListAsync(cancellationToken);

        var tours = new List<Tour>();
        var tourCount = Math.Max(20, Math.Min(32, pois.Count));
        for (var index = 0; index < tourCount && pois.Count >= 3; index++)
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
                    ? $"Hành tr?nh khám phá ch? ð? {TourThemes[index % TourThemes.Length].ToLowerInvariant()} qua các ði?m n?i b?t ? Qu?n 4."
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
        var pois = await GetOrderedPoisAsync(cancellationToken);

        var existingCodes = await _context.QrActivations.Find(FilterDefinition<QrActivation>.Empty)
            .Project(x => x.Code)
            .ToListAsync(cancellationToken);

        var activations = new List<QrActivation>();
        foreach (var (poi, index) in pois.Take(QrCodes.Length).Select((poi, index) => (poi, index)))
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
                    ? "Quét là m? ngay n?i dung thuy?t minh t?i ði?m d?ng."
                    : "M? QR dành cho khách du l?ch mu?n nghe audio nhanh.",
                ScanMode = SharedConstants.QrScanModes.Values[index % SharedConstants.QrScanModes.Values.Length],
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
                Name = $"Gói ngo?i tuy?n Qu?n 4 {versionLabel}",
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
        District = "Qu?n 4",
        City = "TP. H? Chí Minh",
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
        AudioStatus = SharedConstants.AudioStatuses.Done,
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
                new OpeningHour { DayOfWeek = "Th? Hai", OpenTime = "", CloseTime = "", IsClosed = true },
                new OpeningHour { DayOfWeek = "Th? Ba - Ch? Nh?t", OpenTime = "09:30", CloseTime = "22:00" }
            ];
        }

        if (index % 3 == 0)
        {
            return
            [
                new OpeningHour { DayOfWeek = "Th? Hai - Th? Sáu", OpenTime = "07:00", CloseTime = "21:30" },
                new OpeningHour { DayOfWeek = "Th? B?y - Ch? Nh?t", OpenTime = "08:00", CloseTime = "22:30" }
            ];
        }

        return
        [
            new OpeningHour { DayOfWeek = "Th? Hai - Ch? Nh?t", OpenTime = "10:00", CloseTime = "22:30" }
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
        SharedConstants.AudioTaskStatuses.Done,
        SharedConstants.AudioTaskStatuses.Running,
        SharedConstants.AudioTaskStatuses.Queued,
        SharedConstants.AudioTaskStatuses.Paused,
        SharedConstants.AudioTaskStatuses.Done,
        SharedConstants.AudioTaskStatuses.Failed
    ];

    private static readonly string[] SubmissionStatuses =
    [
        SharedConstants.SubmissionStatuses.Approved,
        SharedConstants.SubmissionStatuses.Pending,
        SharedConstants.SubmissionStatuses.Rejected,
        SharedConstants.SubmissionStatuses.Approved
    ];

    private static readonly string[] SearchKeywords =
    [
        "?c",
        "cõm t?m",
        "bún",
        "chè",
        "cà phê",
        "h?i s?n",
        "tráng mi?ng",
        "ãn ðêm"
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
        "V?nh Khánh",
        "Ãn ðêm",
        "B?a sáng",
        "Cà phê và bánh",
        "Món Vi?t",
        "H?i s?n",
        "Ãn v?t",
        "Ven kênh"
    ];

    private static readonly string[] OwnerSubmissionAreas =
    [
        "V?nh Khánh",
        "Khánh H?i",
        "Xóm Chi?u",
        "Ðoàn Vãn Bõ",
        "Tôn Ð?n"
    ];

    private static readonly string[] OwnerSubmissionStreets =
    [
        "V?nh Khánh",
        "Khánh H?i",
        "Xóm Chi?u",
        "Ðoàn Vãn Bõ",
        "Tôn Ð?n",
        "Hoàng Di?u"
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
        "Q4FOOD-20",
        "TONTHATTHUYET-01",
        "KHANHHOI-03",
        "TONDAN-03",
        "NGUYENKHOAI-02",
        "XOMCHIEU-03",
        "HOANGDIEU-03",
        "VINHKHANH-04",
        "BENVANDON-03",
        "NGUYENTATTHANH-01",
        "DOANNHUHAI-01",
        "XOMCHIEU-04",
        "VINHHOI-03"
    ];

    private static readonly string[] QrTitles =
    [
        "Tr?m xe bu?t Khánh H?i",
        "Tr?m xe bu?t V?nh H?i",
        "Tr?m xe bu?t Xóm Chi?u",
        "Ði?m d?ng V?nh Khánh",
        "Ði?m d?ng Ðoàn Vãn Bõ",
        "Ði?m d?ng Tôn Ð?n",
        "Ði?m d?ng Hoàng Di?u",
        "Ði?m d?ng Kênh T?",
        "Ði?m d?ng B?n Vân Ð?n",
        "Tr?m Khánh H?i hý?ng c?u",
        "Ði?m check-in V?nh Khánh",
        "Ði?m ch? Xóm Chi?u",
        "Ðoàn Vãn Bõ giao l?",
        "Ði?m xe bu?t Tôn Ð?n",
        "Hoàng Di?u khu ?m th?c",
        "B?n Vân Ð?n ven kênh",
        "Tr?m V?nh H?i hý?ng ch?",
        "Phý?ng 3 - c?ng ch?",
        "Phý?ng 13 - khu ãn ðêm",
        "Ði?m gi?i thi?u ?m th?c Qu?n 4",
        "Ði?m d?ng Tôn Th?t Thuy?t",
        "Tr?m Khánh H?i m? r?ng",
        "Ði?m ãn v?t Tôn Ð?n",
        "Nguy?n Khoái - khu món ný?c",
        "Ch? Xóm Chi?u m? r?ng",
        "Hoàng Di?u - cõm t?i",
        "V?nh Khánh - h?i s?n ný?ng",
        "B?n Vân Ð?n - món Vi?t",
        "Nguy?n T?t Thành - qu?y ð? u?ng",
        "Ðoàn Nhý Hài - b?a sáng",
        "Xóm Chi?u - hàng rong chi?u",
        "V?nh H?i - l?u ðêm"
    ];

    private static readonly string[] QrZones =
    [
        "Khánh H?i",
        "V?nh H?i",
        "Xóm Chi?u",
        "V?nh Khánh",
        "Ðoàn Vãn Bõ",
        "Tôn Ð?n",
        "Hoàng Di?u",
        "Kênh T?",
        "B?n Vân Ð?n",
        "Khánh H?i",
        "V?nh Khánh",
        "Xóm Chi?u",
        "Ðoàn Vãn Bõ",
        "Tôn Ð?n",
        "Hoàng Di?u",
        "B?n Vân Ð?n",
        "V?nh H?i",
        "Phý?ng 3",
        "Phý?ng 13",
        "Trung tâm Qu?n 4",
        "Tôn Th?t Thuy?t",
        "Khánh H?i",
        "Tôn Ð?n",
        "Nguy?n Khoái",
        "Xóm Chi?u",
        "Hoàng Di?u",
        "V?nh Khánh",
        "B?n Vân Ð?n",
        "Nguy?n T?t Thành",
        "Ðoàn Nhý Hài",
        "Xóm Chi?u",
        "V?nh H?i"
    ];

    private static readonly string[] QrAddresses =
    [
        "B?n Vân Ð?n - c?u Khánh H?i",
        "Tôn Ð?n - khu V?nh H?i",
        "Ch? Xóm Chi?u - ðý?ng Xóm Chi?u",
        "Ph? ?m th?c V?nh Khánh",
        "Ng? tý Ðoàn Vãn Bõ - Nguy?n Khoái",
        "Khu dân cý Tôn Ð?n",
        "Hoàng Di?u - ðo?n ch? chi?u",
        "L?i xu?ng Kênh T?",
        "B? kênh B?n Vân Ð?n",
        "C?u Khánh H?i hý?ng Qu?n 1",
        "Ð?u ðý?ng V?nh Khánh",
        "C?ng ch? Xóm Chi?u",
        "Ðoàn Vãn Bõ g?n trý?ng h?c",
        "B?n xe bu?t Tôn Ð?n",
        "V?a hè Hoàng Di?u",
        "B?n Vân Ð?n hý?ng c?u Calmette",
        "Tr?m V?nh H?i g?n chung cý",
        "Phý?ng 3 - khu dân cý",
        "Phý?ng 13 - khu hàng quán",
        "Ði?m t?ng h?p thông tin du l?ch",
        "Tôn Th?t Thuy?t g?n c?u Tân Thu?n",
        "Khánh H?i ðo?n dân cý ðông",
        "Tôn Ð?n g?n trý?ng h?c",
        "Nguy?n Khoái khu món ný?c",
        "C?ng ph? ch? Xóm Chi?u",
        "Hoàng Di?u g?n khu cõm t?i",
        "V?nh Khánh ðo?n h?i s?n ný?ng",
        "B?n Vân Ð?n hý?ng chung cý",
        "Nguy?n T?t Thành sát b? kênh",
        "Ðoàn Nhý Hài ð?u h?m ãn sáng",
        "Xóm Chi?u khu hàng rong",
        "V?nh H?i khu l?u t?i"
    ];

    private static readonly DemoUserSeed[] DemoUsers =
    [
        new(1, "tran.minh.hieu@quan4tourism.local", "Tr?n Minh Hi?u", "0908000101", SharedConstants.OwnerStatuses.Approved, "?c Ðêm Hi?u", "102 V?nh Khánh", "Quán h?i s?n b?nh dân m? khuya.", "https://images.unsplash.com/photo-1500648767791-00dcc994a43e?auto=format&fit=crop&w=400&q=80"),
        new(2, "nguyen.thao.my@quan4tourism.local", "Nguy?n Th?o My", "0908000102", SharedConstants.OwnerStatuses.Approved, "B?p Nhà My", "18 Ðoàn Vãn Bõ", "Quán cõm gia ð?nh chuyên món Vi?t.", "https://images.unsplash.com/photo-1494790108377-be9c29b29330?auto=format&fit=crop&w=400&q=80"),
        new(3, "pham.quoc.an@quan4tourism.local", "Ph?m Qu?c An", "0908000103", SharedConstants.OwnerStatuses.Approved, "Bún Ph? An", "29 Xóm Chi?u", "Ti?m bún sáng ph?c v? dân ð?a phýõng.", "https://images.unsplash.com/photo-1506794778202-cad84cf45f1d?auto=format&fit=crop&w=400&q=80"),
        new(4, "le.kim.ngan@quan4tourism.local", "Lê Kim Ngân", "0908000104", SharedConstants.OwnerStatuses.Approved, "Chè Ngân 1988", "44 Tôn Ð?n", "Qu?y chè và tráng mi?ng lâu nãm.", "https://images.unsplash.com/photo-1488426862026-3ee34a7d66df?auto=format&fit=crop&w=400&q=80"),
        new(5, "vo.huu.phuc@quan4tourism.local", "V? H?u Phúc", "0908000105", SharedConstants.OwnerStatuses.Approved, "Cà phê B? Kênh Phúc", "75 B?n Vân Ð?n", "Quán cà phê nh? nh?n ra kênh.", "https://images.unsplash.com/photo-1507591064344-4c6ce005b128?auto=format&fit=crop&w=400&q=80"),
        new(6, "doan.nhat.linh@quan4tourism.local", "Ðoàn Nh?t Linh", "0908000106", SharedConstants.OwnerStatuses.Approved, "Bánh Tráng Linh", "12 Khánh H?i", "Qu?y bánh tráng tr?n và ð? ãn v?t.", "https://images.unsplash.com/photo-1544005313-94ddf0286df2?auto=format&fit=crop&w=400&q=80"),
        new(7, "truong.bao.chau@quan4tourism.local", "Trýõng B?o Châu", "0908000107", SharedConstants.OwnerStatuses.Approved, "L?u Ný?ng B?o Châu", "131 Hoàng Di?u", "Quán l?u ný?ng cho nhóm b?n tr?.", "https://images.unsplash.com/photo-1546961329-78bef0414d7c?auto=format&fit=crop&w=400&q=80"),
        new(8, "bui.gia.khanh@quan4tourism.local", "Bùi Gia Khánh", "0908000108", SharedConstants.OwnerStatuses.Approved, "M? Khuya Gia Khánh", "9 Nguy?n Khoái", "Quán m? và h? ti?u m? t?i.", "https://images.unsplash.com/photo-1504257432389-52343af06ae3?auto=format&fit=crop&w=400&q=80"),
        new(9, "hoang.mai.phuong@quan4tourism.local", "Hoàng Mai Phýõng", "0908000109", SharedConstants.OwnerStatuses.Pending, "Bún B? Mai Phýõng", "22 V?nh H?i", "Ðang xin duy?t gian hàng bún b?.", "https://images.unsplash.com/photo-1438761681033-6461ffad8d80?auto=format&fit=crop&w=400&q=80"),
        new(10, "dang.thanh.tung@quan4tourism.local", "Ð?ng Thanh Tùng", "0908000110", SharedConstants.OwnerStatuses.Pending, "Cõm T?i Thanh Tùng", "48 Tôn Ð?n", "Quán cõm t?i ph?c v? dân vãn ph?ng.", "https://images.unsplash.com/photo-1500648767791-00dcc994a43e?auto=format&fit=crop&w=400&q=80"),
        new(11, "phan.ngoc.ha@quan4tourism.local", "Phan Ng?c Hà", "0908000111", SharedConstants.OwnerStatuses.Pending, "Kem D?a Ng?c Hà", "63 Xóm Chi?u", "Qu?y kem d?a và món mát.", "https://images.unsplash.com/photo-1494790108377-be9c29b29330?auto=format&fit=crop&w=400&q=80"),
        new(12, "ngo.minh.duc@quan4tourism.local", "Ngô Minh Ð?c", "0908000112", SharedConstants.OwnerStatuses.Pending, "Bánh Canh Minh Ð?c", "88 Khánh H?i", "Bánh canh và súp nóng ph?c v? sáng.", "https://images.unsplash.com/photo-1506794778202-cad84cf45f1d?auto=format&fit=crop&w=400&q=80"),
        new(13, "ly.thu.trang@quan4tourism.local", "L? Thu Trang", "0908000113", SharedConstants.OwnerStatuses.Pending, "Ti?m Ãn Thu Trang", "37 Ðoàn Vãn Bõ", "Ð? xu?t m? ði?m ãn gia ð?nh.", "https://images.unsplash.com/photo-1488426862026-3ee34a7d66df?auto=format&fit=crop&w=400&q=80"),
        new(14, "cao.viet.long@quan4tourism.local", "Cao Vi?t Long", "0908000114", SharedConstants.OwnerStatuses.Pending, "?c Long Sài G?n", "118 V?nh Khánh", "Quán ?c c?n b? sung gi?y phép.", "https://images.unsplash.com/photo-1507591064344-4c6ce005b128?auto=format&fit=crop&w=400&q=80"),
        new(15, "lam.gia.han@quan4tourism.local", "Lâm Gia Hân", "0908000115", SharedConstants.OwnerStatuses.Rejected, "Trà S?a Gia Hân", "11 Hoàng Di?u", "H? sõ chýa ð? minh ch?ng ngu?n g?c.", "https://images.unsplash.com/photo-1544005313-94ddf0286df2?auto=format&fit=crop&w=400&q=80"),
        new(16, "ta.huu.nghia@quan4tourism.local", "T? H?u Ngh?a", "0908000116", SharedConstants.OwnerStatuses.Rejected, "B? Lá L?t H?u Ngh?a", "55 Tôn Ð?n", "Thông tin liên h? chýa xác th?c.", "https://images.unsplash.com/photo-1546961329-78bef0414d7c?auto=format&fit=crop&w=400&q=80"),
        new(17, "mai.khanh.ly@quan4tourism.local", "Mai Khánh Ly", "0908000117", SharedConstants.OwnerStatuses.Rejected, "Ti?m Chè Khánh Ly", "70 B?n Vân Ð?n", "?nh minh h?a chýa ðúng ð?a ði?m.", "https://images.unsplash.com/photo-1438761681033-6461ffad8d80?auto=format&fit=crop&w=400&q=80"),
        new(18, "duong.anh.khoa@quan4tourism.local", "Dýõng Anh Khoa", "0908000118", SharedConstants.OwnerStatuses.Rejected, "Bánh M? Anh Khoa", "94 Xóm Chi?u", "Thi?u mô t? s?n ph?m và gi? m? c?a.", "https://images.unsplash.com/photo-1504257432389-52343af06ae3?auto=format&fit=crop&w=400&q=80"),
        new(19, "chau.ngoc.nhu@quan4tourism.local", "Châu Ng?c Nhý", "0908000119", SharedConstants.OwnerStatuses.Rejected, "Ti?m Ãn Ng?c Nhý", "19 V?nh H?i", "Chýa c?p nh?t b?ng giá r? ràng.", "https://images.unsplash.com/photo-1494790108377-be9c29b29330?auto=format&fit=crop&w=400&q=80"),
        new(20, "luu.minh.quan@quan4tourism.local", "Lýu Minh Quân", "0908000120", SharedConstants.OwnerStatuses.Rejected, "Bún M?m Minh Quân", "101 Nguy?n T?t Thành", "Ð?a ch? ðãng k? chýa kh?p th?c t?.", "https://images.unsplash.com/photo-1506794778202-cad84cf45f1d?auto=format&fit=crop&w=400&q=80")
    ];

    private static readonly CategorySeed[] CategorySeeds =
    [
        new("street_food", "Ãn v?t", "Các món ãn nhanh, ãn chõi ð?c trýng khu ph?.", 1),
        new("rice", "Cõm", "Cõm t?m, cõm gia ð?nh và các ph?n cõm quen thu?c.", 2),
        new("noodles", "Bún / Ph? / M?", "Các món ný?c và món s?i ph? bi?n.", 3),
        new("seafood", "H?i s?n", "?c, tôm, cua, nghêu và các món h?i s?n ðêm.", 4),
        new("coffee", "Cà phê", "Quán cà phê và ði?m ngh? chân ven kênh.", 5),
        new("dessert", "Tráng mi?ng", "Chè, bánh ng?t, kem và món ng?t.", 6),
        new("drink", "Ð? u?ng", "Ný?c ép, trà s?a, ð? u?ng gi?i khát.", 7),
        new("hotpot_bbq", "L?u / Ný?ng", "Nhóm món l?u và ný?ng t? t?p b?n bè.", 8),
        new("vietnamese_food", "Món Vi?t", "Nh?ng quán món Vi?t ð?a phýõng.", 9),
        new("night_food", "Quán ðêm", "Ði?m ãn khuya và hàng quán m? mu?n.", 10),
        new("banh_mi", "Bánh m?", "Bánh m? ch?o, bánh m? th?t và bi?n t?u ðý?ng ph?.", 11),
        new("porridge_soup", "Cháo / Súp", "Cháo, súp cua và món nóng nh? b?ng.", 12),
        new("snails", "?c & nghêu", "Các quán ?c xào, h?p, rang mu?i ð?c trýng.", 13),
        new("tea_dessert", "Chè & trà", "Chè truy?n th?ng, trà trái cây và món mát.", 14),
        new("bakery", "Bánh & ti?m ný?ng", "Ti?m bánh m?n, ng?t và ð? ný?ng nhanh.", 15),
        new("vegetarian", "Ãn chay", "Ði?m ãn chay và món thanh ð?m.", 16),
        new("seafood_hotpot", "L?u h?i s?n", "Nhóm quán l?u t?p trung quanh khu ðêm.", 17),
        new("broken_rice", "Cõm t?m chuyên bi?t", "Nh?ng quán cõm t?m n?i b?t.", 18),
        new("regional", "Ð?c s?n vùng mi?n", "Món Hu?, mi?n Tây và ð?c s?n ð?a phýõng.", 19),
        new("family_restaurant", "Quán gia ð?nh", "Ði?m ng?i l?i lâu, phù h?p nhóm và gia ð?nh.", 20)
    ];

    private static readonly PoiSeed[] PoiSeeds =
    [
        new(0, "?c Oanh", "Quán ?c b?nh dân n?i ti?ng v?i nhi?u món xào bõ, rang mu?i và s?t me.", "seafood", "534 V?nh Khánh", "Phý?ng 13", 10.7592, 106.7045, "$$", 4.6, 1240, 20, "https://images.unsplash.com/photo-1559737558-2f5a35f4523b?auto=format&fit=crop&w=1200&q=85", "Ð?a ?c xào bõ t?i nóng h?i.", ["h?i s?n", "v?nh khánh", "bu?i t?i"]),
        new(1, "Bánh m? ch?o Cô 3 H?u", "Bánh m? ch?o nóng h?i, phù h?p cho b?a sáng và b?a trýa nhanh.", "banh_mi", "36 Nguy?n H?u Hào", "Phý?ng 13", 10.7580, 106.7018, "$", 4.4, 630, 19, "https://images.unsplash.com/photo-1601050690597-df0568f70950?auto=format&fit=crop&w=1200&q=85", "Ch?o bánh m? v?i tr?ng, pate và xúc xích.", ["bánh m?", "b?a sáng", "ð?a phýõng"]),
        new(2, "Cõm t?m Cây Ði?p", "Cõm t?m sý?n ný?ng thõm, ph?c v? nhanh trong không khí Qu?n 4 thân thu?c.", "broken_rice", "140/1 Ðoàn Vãn Bõ", "Phý?ng 13", 10.7548, 106.7049, "$", 4.5, 890, 18, "https://images.unsplash.com/photo-1515003197210-e0cd71810b5f?auto=format&fit=crop&w=1200&q=85", "Ph?n cõm t?m sý?n b? ch? ð?y ð?n.", ["cõm t?m", "sý?n ný?ng", "trýa"]),
        new(3, "Súp cua Cô Bông", "Súp cua nóng v?i th?t cua, tr?ng cút và n?m, món ãn v?t quen thu?c.", "porridge_soup", "22 Ðoàn Vãn Bõ", "Phý?ng 13", 10.7566, 106.7032, "$", 4.3, 410, 17, "https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=1200&q=85", "Chén súp cua nóng có tr?ng cút.", ["súp cua", "ãn v?t", "chi?u t?i"]),
        new(4, "Cà phê b? kênh Khánh H?i", "Không gian cà phê thoáng bên b? kênh, thích h?p ngh? chân sau hành tr?nh khám phá.", "coffee", "B?n Vân Ð?n", "Phý?ng 13", 10.7610, 106.7068, "$$", 4.2, 245, 16, "https://images.unsplash.com/photo-1495474472287-4d71bcdd2085?auto=format&fit=crop&w=1200&q=85", "Ly cà phê và góc ng?i nh?n ra b? kênh.", ["cà phê", "view kênh", "thý gi?n"]),
        new(5, "Bún b? Khánh H?i", "Quán bún b? v? ð?m, ný?c dùng thõm s? và ðông khách bu?i sáng.", "regional", "91 Khánh H?i", "Phý?ng 3", 10.7602, 106.7059, "$", 4.4, 380, 15, "https://images.unsplash.com/photo-1544025162-d76694265947?auto=format&fit=crop&w=1200&q=85", "Tô bún b? ð?y topping và rau s?ng.", ["bún b?", "hu?", "bu?i sáng"]),
        new(6, "Bánh tráng tr?n Chú Vi", "Xe bánh tráng tr?n nhi?u topping, v? chua cay ð?m ðà ki?u h?c sinh sinh viên.", "street_food", "17 Tôn Ð?n", "Phý?ng 13", 10.7555, 106.7026, "$", 4.1, 210, 14, "https://images.unsplash.com/photo-1504754524776-8f4f37790ca0?auto=format&fit=crop&w=1200&q=85", "H?p bánh tráng tr?n v?i khô b? và tr?ng cút.", ["bánh tráng", "ãn v?t", "takeaway"]),
        new(7, "H? ti?u m?c Ông Già Cali", "H? ti?u m?c v? ng?t thanh, topping m?c gi?n và tôm týõi.", "noodles", "45 V?nh Khánh", "Phý?ng 8", 10.7587, 106.7051, "$$", 4.5, 570, 13, "https://images.unsplash.com/photo-1617093727343-374698b1b08d?auto=format&fit=crop&w=1200&q=85", "Tô h? ti?u m?c v?i m?c ?ng và tôm.", ["h? ti?u", "m?c", "ð?c s?n"]),
        new(8, "Chè d?a d?m V?nh H?i", "Ly chè d?a d?m mát l?nh, v? béo nh? phù h?p bu?i chi?u nóng.", "tea_dessert", "12 V?nh H?i", "Phý?ng 4", 10.7571, 106.7080, "$", 4.0, 165, 12, "https://images.unsplash.com/photo-1488477181946-6428a0291777?auto=format&fit=crop&w=1200&q=85", "C?c chè d?a d?m v?i ðá bào và th?ch.", ["chè", "d?a", "gi?i nhi?t"]),
        new(9, "L?u cá kèo Bà Huy?n", "Quán l?u cá kèo chua cay, h?p nhóm b?n và gia ð?nh ãn t?i.", "seafood_hotpot", "102 Hoàng Di?u", "Phý?ng 9", 10.7519, 106.7040, "$$$", 4.3, 290, 11, "https://images.unsplash.com/photo-1517248135467-4c7edcad34c4?auto=format&fit=crop&w=1200&q=85", "N?i l?u cá kèo nghi ngút khói.", ["l?u", "cá kèo", "gia ð?nh"]),
        new(10, "G?i cu?n Cô Sáu", "G?i cu?n cu?n tay m?i ngày, rau týõi và ný?c ch?m ð?u ph?ng ð?m v?.", "vietnamese_food", "58 Xóm Chi?u", "Phý?ng 15", 10.7545, 106.7071, "$", 4.2, 142, 10, "https://images.unsplash.com/photo-1559847844-5315695dadae?auto=format&fit=crop&w=1200&q=85", "Ð?a g?i cu?n tôm th?t ch?m s?t ð?u.", ["g?i cu?n", "nh? b?ng", "bu?i chi?u"]),
        new(11, "Phá l?u b? Ch? 200", "Phá l?u ný?c c?t d?a thõm béo, ãn kèm bánh m? ho?c m? gói.", "night_food", "200 Xóm Chi?u", "Phý?ng 14", 10.7528, 106.7062, "$", 4.4, 335, 9, "https://images.unsplash.com/photo-1559847844-d721426d6edc?auto=format&fit=crop&w=1200&q=85", "Tô phá l?u nóng v?i bánh m? gi?n.", ["phá l?u", "ãn khuya", "xóm chi?u"]),
        new(12, "Cõm niêu Khói B?p", "Quán cõm niêu ph?c v? món nhà, phù h?p nhóm gia ð?nh mu?n ng?i lâu.", "family_restaurant", "84 B?n Vân Ð?n", "Phý?ng 1", 10.7606, 106.7085, "$$$", 4.5, 198, 8, "https://images.unsplash.com/photo-1563379091339-03246963d96c?auto=format&fit=crop&w=1200&q=85", "Bàn cõm niêu nhi?u món m?n truy?n th?ng.", ["cõm niêu", "gia ð?nh", "món vi?t"]),
        new(13, "Bánh flan Xóm Chi?u", "Qu?y bánh flan m?m m?n, thêm cà phê và ðá bào theo ki?u Sài G?n.", "dessert", "73 Xóm Chi?u", "Phý?ng 16", 10.7535, 106.7057, "$", 4.1, 188, 7, "https://images.unsplash.com/photo-1482049016688-2d3e1b311543?auto=format&fit=crop&w=1200&q=85", "Ly bánh flan cà phê và ðá bào.", ["flan", "tráng mi?ng", "giá m?m"]),
        new(14, "Bánh canh cua H?m 48", "Bánh canh cua s?i dày, ný?c dùng ng?t và topping ch? cua ð?y ð?.", "porridge_soup", "48/7 Tôn Ð?n", "Phý?ng 13", 10.7560, 106.7040, "$$", 4.3, 256, 6, "https://images.unsplash.com/photo-1512058564366-18510be2db19?auto=format&fit=crop&w=1200&q=85", "Tô bánh canh cua v?i ch? cua và th?t.", ["bánh canh", "cua", "t?i"]),
        new(15, "?c len xào d?a Ch? Mý?i", "?c len xào d?a béo thõm, ãn cùng bánh m? nóng r?t b?t v?.", "snails", "109 V?nh Khánh", "Phý?ng 8", 10.7597, 106.7038, "$$", 4.6, 610, 5, "https://images.unsplash.com/photo-1467003909585-2f8a72700288?auto=format&fit=crop&w=1200&q=85", "Ð?a ?c len xào d?a và bánh m? ný?ng.", ["?c len", "xào d?a", "ð?c s?n"]),
        new(16, "Trà trái cây C?u Calmette", "Xe trà trái cây và ný?c ép mát l?nh g?n khu b? kênh, h?p khách ði b?.", "drink", "28 B?n Vân Ð?n", "Phý?ng 12", 10.7622, 106.7034, "$", 4.0, 120, 4, "https://images.unsplash.com/photo-1499636136210-6f4ee915583e?auto=format&fit=crop&w=1200&q=85", "Ly trà trái cây v?i cam, dâu và b?c hà.", ["trà trái cây", "ný?c ép", "gi?i khát"]),
        new(17, "Bún m?m C?u Ông L?nh", "Bún m?m ð?m v? mi?n Tây, topping h?i s?n và heo quay ð?y tô.", "regional", "16 Nguy?n Khoái", "Phý?ng 1", 10.7589, 106.7089, "$$", 4.2, 174, 3, "https://images.unsplash.com/photo-1526318896980-cf78c088247c?auto=format&fit=crop&w=1200&q=85", "Tô bún m?m ð?y rau s?ng và h?i s?n.", ["bún m?m", "mi?n tây", "ð?m v?"]),
        new(18, "Bánh xèo Tôm Nh?y 46", "Bánh xèo ð? gi?n, nhân tôm th?t và rau s?ng ãn kèm phong phú.", "vietnamese_food", "46 Khánh H?i", "Phý?ng 6", 10.7599, 106.7075, "$$", 4.3, 230, 2, "https://images.unsplash.com/photo-1625944524160-6cf6b4d5ad28?auto=format&fit=crop&w=1200&q=85", "Bánh xèo vàng gi?n cu?n rau s?ng.", ["bánh xèo", "tôm nh?y", "món vi?t"]),
        new(19, "Ti?m chay An Nhiên", "Quán chay nh? yên t?nh v?i cõm ph?n, bún và món xào thanh ð?m.", "vegetarian", "11 Hoàng Di?u", "Phý?ng 10", 10.7512, 106.7067, "$$", 4.1, 98, 1, "https://images.unsplash.com/photo-1547592180-85f173990554?auto=format&fit=crop&w=1200&q=85", "Mâm cõm chay nhi?u rau và ð?u h?.", ["ãn chay", "thanh ð?m", "gia ð?nh"]),
        new(20, "Bánh m? ný?ng mu?i ?t Cô Tý", "? bánh m? ný?ng gi?n ph? sa t?, s?t bõ và ru?c, r?t h?p khách d?o ph? t?i.", "bakery", "29 Tôn Th?t Thuy?t", "Phý?ng 18", 10.7583, 106.7009, "$", 4.2, 154, 0, "https://images.unsplash.com/photo-1509440159596-0249088772ff?auto=format&fit=crop&w=1200&q=85", "? bánh m? ný?ng ð? gi?n ph? mu?i ?t.", ["bánh m? ný?ng", "ãn v?t", "bu?i t?i"]),
        new(21, "Cháo hàu Khánh H?i", "Cháo hàu n?u nóng, v? ng?t thanh và thý?ng ðông khách vào cu?i chi?u.", "porridge_soup", "64 Khánh H?i", "Phý?ng 5", 10.7607, 106.7071, "$$", 4.3, 212, -1, "https://images.unsplash.com/photo-1512058564366-18510be2db19?auto=format&fit=crop&w=1200&q=85", "Tô cháo hàu nóng r?c tiêu và hành lá.", ["cháo hàu", "?m b?ng", "chi?u t?i"]),
        new(22, "B? lá l?t Tôn Ð?n", "B? lá l?t ný?ng than thõm, ãn kèm rau s?ng và m?m nêm ð?m v?.", "street_food", "87 Tôn Ð?n", "Phý?ng 13", 10.7549, 106.7033, "$", 4.2, 267, -2, "https://images.unsplash.com/photo-1559847844-d721426d6edc?auto=format&fit=crop&w=1200&q=85", "Ph?n b? lá l?t ný?ng v?i bánh tráng và rau s?ng.", ["b? lá l?t", "ný?ng", "ð?m v?"]),
        new(23, "M? v?t ti?m H?m 36", "M? v?t ti?m ný?c dùng thu?c b?c nh?, v?t m?m và m? dai v?a ph?i.", "noodles", "36/12 Nguy?n Khoái", "Phý?ng 1", 10.7576, 106.7093, "$$", 4.4, 301, -3, "https://images.unsplash.com/photo-1617093727343-374698b1b08d?auto=format&fit=crop&w=1200&q=85", "Tô m? v?t ti?m v?i c?i xanh và n?m.", ["m? v?t ti?m", "món ný?c", "bu?i t?i"]),
        new(24, "S?a chua n?p c?m Cô H?nh", "Ly s?a chua n?p c?m mát l?nh, d? ãn và h?p khách tr? sau b?a t?i.", "dessert", "52 Xóm Chi?u", "Phý?ng 16", 10.7538, 106.7064, "$", 4.1, 143, -4, "https://images.unsplash.com/photo-1488477181946-6428a0291777?auto=format&fit=crop&w=1200&q=85", "Ly s?a chua n?p c?m v?i topping d?a s?y.", ["s?a chua", "tráng mi?ng", "gi?i nhi?t"]),
        new(25, "Cõm gà x?i m? 79", "Cõm gà da gi?n, ph?n ãn ð?y ð?n và giá d? ti?p c?n cho khách công s?.", "rice", "79 Hoàng Di?u", "Phý?ng 9", 10.7524, 106.7050, "$$", 4.3, 411, -5, "https://images.unsplash.com/photo-1515003197210-e0cd71810b5f?auto=format&fit=crop&w=1200&q=85", "Ð?a cõm gà x?i m? v?i dýa chua và ný?c m?m g?ng.", ["cõm gà", "ãn trýa", "da gi?n"]),
        new(26, "H?i s?n ný?ng V?nh Khánh 79", "Quán h?i s?n ný?ng than v?i tôm, s? và m?c, h?p nhóm b?n ãn t?i.", "seafood", "79 V?nh Khánh", "Phý?ng 8", 10.7590, 106.7042, "$$$", 4.5, 522, -6, "https://images.unsplash.com/photo-1559737558-2f5a35f4523b?auto=format&fit=crop&w=1200&q=85", "M?t h?i s?n ný?ng than v?i s?, m?c và tôm.", ["h?i s?n ný?ng", "nhóm b?n", "v?nh khánh"]),
        new(27, "Bánh h?i th?t ný?ng B?n Vân Ð?n", "Bánh h?i s?i m?nh ãn cùng th?t ný?ng và m? hành, phù h?p b?a trýa nh?.", "vietnamese_food", "136 B?n Vân Ð?n", "Phý?ng 1", 10.7612, 106.7091, "$$", 4.2, 189, -7, "https://images.unsplash.com/photo-1504754524776-8f4f37790ca0?auto=format&fit=crop&w=1200&q=85", "Ph?n bánh h?i th?t ný?ng kèm rau thõm.", ["bánh h?i", "th?t ný?ng", "món vi?t"]),
        new(28, "Trà ðào cam s? C?u Ông L?nh", "Qu?y ð? u?ng mang ði v?i trà ðào cam s? và ný?c trái cây cho khách ði b?.", "drink", "7 Nguy?n T?t Thành", "Phý?ng 12", 10.7600, 106.7019, "$", 4.0, 96, -8, "https://images.unsplash.com/photo-1499636136210-6f4ee915583e?auto=format&fit=crop&w=1200&q=85", "Ly trà ðào cam s? v?i lát cam týõi và b?c hà.", ["trà ðào", "mang ði", "gi?i khát"]),
        new(29, "Bánh cu?n nóng Phý?ng 2", "Bánh cu?n tráng m?ng, nhân th?t m?c nh? và ch? l?a ãn kèm ný?c m?m chua ng?t.", "vietnamese_food", "18 Ðoàn Nhý Hài", "Phý?ng 2", 10.7581, 106.7101, "$", 4.1, 173, -9, "https://images.unsplash.com/photo-1544025162-d76694265947?auto=format&fit=crop&w=1200&q=85", "Ð?a bánh cu?n nóng ph? hành phi và ch? l?a.", ["bánh cu?n", "b?a sáng", "món vi?t"]),
        new(30, "Cá viên chiên C?ng ch? Xóm Chi?u", "Xe cá viên chiên ð? lo?i v?i týõng ?t, s?t me và giá m?m cho h?c sinh sinh viên.", "street_food", "5 Xóm Chi?u", "Phý?ng 15", 10.7551, 106.7078, "$", 4.0, 208, -10, "https://images.unsplash.com/photo-1504754524776-8f4f37790ca0?auto=format&fit=crop&w=1200&q=85", "Xiên cá viên chiên và xúc xích nóng h?i.", ["cá viên", "hàng rong", "ãn v?t"]),
        new(31, "L?u gà lá é V?nh H?i", "N?i l?u gà lá é thõm d?u, h?p nhóm khách mu?n ãn t?i lâu và tr? chuy?n.", "hotpot_bbq", "24 V?nh H?i", "Phý?ng 4", 10.7568, 106.7087, "$$$", 4.4, 245, -11, "https://images.unsplash.com/photo-1517248135467-4c7edcad34c4?auto=format&fit=crop&w=1200&q=85", "N?i l?u gà lá é b?c khói v?i rau và bún týõi.", ["l?u gà", "nhóm b?n", "bu?i t?i"])
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


