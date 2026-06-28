using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Quan4CulinaryTourism.Api.Database;
using Quan4CulinaryTourism.Api.Helpers;
using Quan4CulinaryTourism.Api.Repositories;
using Quan4CulinaryTourism.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection(nameof(MongoDbSettings)));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(nameof(JwtSettings)));
builder.Services.Configure<UploadSettings>(builder.Configuration.GetSection(nameof(UploadSettings)));
builder.Services.Configure<DefaultAdminSettings>(builder.Configuration.GetSection("DefaultAdmin"));
builder.Services.Configure<CorsSettings>(builder.Configuration.GetSection("Cors"));

var jwtSettings = builder.Configuration.GetSection(nameof(JwtSettings)).Get<JwtSettings>() ?? new JwtSettings();
if (!builder.Environment.IsDevelopment() && jwtSettings.SecretKey.StartsWith("__SET_", StringComparison.Ordinal))
{
    throw new InvalidOperationException("JwtSettings:SecretKey must be configured outside source control.");
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
        Description = "Nhap JWT theo dinh dang: Bearer {token}"
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
builder.Services.AddSingleton<MongoIndexInitializer>();
builder.Services.AddSingleton<PasswordHasher>();
builder.Services.AddSingleton<JwtHelper>();
builder.Services.AddSingleton<DistanceHelper>();
builder.Services.AddScoped<FileUploadHelper>();
builder.Services.AddSingleton<ClaimsHelper>();
builder.Services.AddSingleton<DbSeeder>();

builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<RoleRepository>();
builder.Services.AddScoped<CategoryRepository>();
builder.Services.AddScoped<PoiRepository>();
builder.Services.AddScoped<PoiLocalizationRepository>();
builder.Services.AddScoped<PoiAudioRepository>();
builder.Services.AddScoped<AudioTaskRepository>();
builder.Services.AddScoped<OwnerRegistrationRepository>();
builder.Services.AddScoped<OwnerSubmissionRepository>();
builder.Services.AddScoped<AnalyticsRepository>();
builder.Services.AddScoped<AuditLogRepository>();
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
builder.Services.AddScoped<LocalizationService>();
builder.Services.AddScoped<MediaService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<MapsService>();
builder.Services.AddScoped<HealthService>();
builder.Services.AddScoped<TourService>();
builder.Services.AddScoped<QrActivationService>();

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
    var indexInitializer = scope.ServiceProvider.GetRequiredService<MongoIndexInitializer>();
    await indexInitializer.InitializeAsync();

    var dbSeeder = scope.ServiceProvider.GetRequiredService<DbSeeder>();
    await dbSeeder.SeedAsync();
}

app.MapControllers();

app.Run();
