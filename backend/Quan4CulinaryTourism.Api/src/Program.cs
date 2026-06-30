using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Quan4CulinaryTourism.Api.Database;
using Quan4CulinaryTourism.Api.Helpers;
using Quan4CulinaryTourism.Api.Repositories;
using Quan4CulinaryTourism.Api.Services;

EnvFileLoader.LoadIfPresent(
    Path.Combine(Directory.GetCurrentDirectory(), ".env"),
    Path.Combine(AppContext.BaseDirectory, ".env"));

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection(nameof(MongoDbSettings)));
builder.Services.Configure<DatabaseResetSettings>(builder.Configuration.GetSection(nameof(DatabaseResetSettings)));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(nameof(JwtSettings)));
builder.Services.Configure<UploadSettings>(builder.Configuration.GetSection(nameof(UploadSettings)));
builder.Services.Configure<CloudinarySettings>(builder.Configuration.GetSection(nameof(CloudinarySettings)));
builder.Services.Configure<DefaultAdminSettings>(builder.Configuration.GetSection("DefaultAdmin"));
builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection("Cors"));
builder.Services.Configure<TextToSpeechSettings>(builder.Configuration.GetSection(nameof(TextToSpeechSettings)));
builder.Services.Configure<PublicSiteSettings>(builder.Configuration.GetSection(nameof(PublicSiteSettings)));
builder.Services.AddOptions<AiSettings>()
    .Bind(builder.Configuration.GetSection("Ai"))
    .PostConfigure(settings =>
    {
        ApplyEnvironmentOverride("AI_PROVIDER", value => settings.Provider = value);
        ApplyEnvironmentOverride("AI_BASE_URL", value => settings.BaseUrl = value);
        ApplyEnvironmentOverride("AI_API_KEY", value => settings.ApiKey = value);
        ApplyEnvironmentOverride("AI_MODEL", value => settings.Model = value);

        if (bool.TryParse(Environment.GetEnvironmentVariable("AI_ENABLED"), out var enabled))
        {
            settings.Enabled = enabled;
        }

        if (int.TryParse(Environment.GetEnvironmentVariable("AI_TIMEOUT_SECONDS"), out var timeoutSeconds))
        {
            settings.TimeoutSeconds = timeoutSeconds;
        }
    });

var jwtSettings = builder.Configuration.GetSection(nameof(JwtSettings)).Get<JwtSettings>() ?? new JwtSettings();
var defaultAdminSettings = builder.Configuration.GetSection("DefaultAdmin").Get<DefaultAdminSettings>() ?? new DefaultAdminSettings();
if (!builder.Environment.IsDevelopment() && jwtSettings.SecretKey.StartsWith("__SET_", StringComparison.Ordinal))
{
    throw new InvalidOperationException("JwtSettings:SecretKey must be configured outside source control.");
}
if (!builder.Environment.IsDevelopment() && jwtSettings.SecretKey.Length < 32)
{
    throw new InvalidOperationException("JwtSettings:SecretKey must be at least 32 characters long in non-development environments.");
}
if (!builder.Environment.IsDevelopment() &&
    !string.IsNullOrWhiteSpace(defaultAdminSettings.Email) &&
    string.IsNullOrWhiteSpace(defaultAdminSettings.Password))
{
    throw new InvalidOperationException("DefaultAdmin:Password must be configured in non-development environments when DefaultAdmin:Email is set.");
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Quan4 Culinary Tourism API",
        Version = "v1",
        Description = "Web API for culinary POIs, tours, owner workflows, QR activations, audio, and analytics."
    });

    var bearerScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Nhập JWT theo định dạng: Bearer {token}"
    };

    options.AddSecurityDefinition("Bearer", bearerScheme);

    options.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecuritySchemeReference("Bearer", hostDocument: null, externalResource: null),
            []
        }
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }
});

builder.Services.AddCors(options =>
{
    var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    options.AddPolicy("DefaultCors", policy =>
    {
        policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    });
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddSingleton<DeploymentDatabaseResetService>();
builder.Services.AddSingleton<MongoIndexInitializer>();
builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddSingleton<JwtHelper>();
builder.Services.AddSingleton<DistanceHelper>();
builder.Services.AddScoped<FileUploadHelper>();
builder.Services.AddSingleton<ClaimsHelper>();
builder.Services.AddScoped<DbSeeder>();
builder.Services.AddScoped<IClaimsTransformation, UserRoleClaimsTransformation>();

builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<CategoryRepository>();
builder.Services.AddScoped<PoiRepository>();
builder.Services.AddScoped<PoiLocalizationRepository>();
builder.Services.AddScoped<PoiAudioRepository>();
builder.Services.AddScoped<AudioTaskRepository>();
builder.Services.AddScoped<OwnerRegistrationRepository>();
builder.Services.AddScoped<OwnerSubmissionRepository>();
builder.Services.AddScoped<AnalyticsRepository>();
builder.Services.AddScoped<MediaFileRepository>();
builder.Services.AddScoped<MapPackRepository>();
builder.Services.AddScoped<TourRepository>();
builder.Services.AddScoped<QrActivationRepository>();

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<PoiService>();
builder.Services.AddScoped<OwnerService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<AudioService>();
builder.Services.AddScoped<PythonTextToSpeechService>();
builder.Services.AddScoped<PythonTranslationService>();
builder.Services.AddScoped<LocalizationService>();
builder.Services.AddScoped<MediaService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<MapsService>();
builder.Services.AddScoped<HealthService>();
builder.Services.AddScoped<TourService>();
builder.Services.AddScoped<QrActivationService>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddHttpClient<AiChatClient>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Quan4 Culinary Tourism API v1");
        options.DisplayRequestDuration();
    });
}

app.UseStaticFiles();
app.UseCors("DefaultCors");
app.UseAuthentication();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    var deploymentResetService = scope.ServiceProvider.GetRequiredService<DeploymentDatabaseResetService>();
    await deploymentResetService.ResetIfNeededAsync();

    var indexInitializer = scope.ServiceProvider.GetRequiredService<MongoIndexInitializer>();
    await indexInitializer.InitializeAsync();

    var dbSeeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
    await dbSeeder.SeedAsync();
}

app.MapControllers();

app.Run();

static void ApplyEnvironmentOverride(string key, Action<string> apply)
{
    var value = Environment.GetEnvironmentVariable(key);
    if (!string.IsNullOrWhiteSpace(value))
    {
        apply(value.Trim());
    }
}

