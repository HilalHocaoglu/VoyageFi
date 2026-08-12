using Microsoft.EntityFrameworkCore;
using VoyageFi.API.Hubs;
using VoyageFi.API.Middlewares;
using VoyageFi.API.Services;
using VoyageFi.Core.Interfaces;
using VoyageFi.Infrastructure.Data;
using VoyageFi.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using VoyageFi.Infrastructure.Authentication;
using VoyageFi.Infrastructure.Caching;
using Microsoft.OpenApi.Models;
using VoyageFi.Infrastructure.BackgroundServices;
using VoyageFi.Infrastructure.AI;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using MassTransit;
using VoyageFi.Infrastructure.Consumers;

var builder = WebApplication.CreateBuilder(args);

// 1. Veritabanı Bağlantısı
builder.Services.AddDbContext<VoyageFiDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. BAĞIMLILIK ENJEKSİYONU 
// "Eğer biri senden ITripRepository isterse, ona TripRepository ver."
builder.Services.AddScoped<ITripRepository, TripRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IRedisCacheService, RedisCacheService>();
builder.Services.AddScoped<IVoyageFiAgentService, VoyageFiAgentService>();

// HttpClient: TripUpdateWorker'ın dış API çağrıları yapabilmesi için
builder.Services.AddHttpClient("AviationApi", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "VoyageFi/1.0");
});

// Background Worker: Saatte bir uçuş durumlarını kontrol eder
builder.Services.AddHostedService<TripUpdateWorker>();

// --- JWT ve Redis Konfigürasyonu Başlangıcı ---
// Redis Cache Konfigürasyonu
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
});

// Rate Limiting Konfigürasyonu
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("AiAssistantPolicy", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
    });
    
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await context.HttpContext.Response.WriteAsync("Lütfen biraz bekleyip tekrar deneyin", token);
    };
});

// JWT Authentication Konfigürasyonu
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new ArgumentNullException("Jwt:Key Ortam değişkeni eksik!");
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.IncludeErrorDetails = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});
// --- JWT ve Redis Konfigürasyonu Sonu ---

// --- MassTransit ve RabbitMQ Konfigürasyonu ---
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<FlightDelayedEventConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
        var rabbitUser = builder.Configuration["RabbitMQ:Username"] ?? "guest";
        var rabbitPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";

        cfg.Host(rabbitHost, "/", h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });

        cfg.ConfigureEndpoints(context);
    });
});

// 3. Controller'ları aktif et
builder.Services.AddControllers();

// --- SignalR Konfigürasyonu ---
// Gerçek zamanlı Admin Dashboard bildirimleri için SignalR servislerini kaydet
builder.Services.AddSignalR();
// IFlightNotificationService → SignalR implementasyonunu bağla
builder.Services.AddScoped<IFlightNotificationService, SignalRFlightNotificationService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVite", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ---> SWAGGER 1: Sistemin kapılarını taraması için gereken servisler <---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "VoyageFi API", Version = "v1" });

    // JWT Authentication for Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "LÜTFEN DİKKAT: Buraya SADECE token'ı yapıştırın. 'Bearer ' kelimesini YAZMAYIN, sistem otomatik olarak ekleyecektir. Örnek: 12345abcdef"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Hata yönetimini (Global Exception Handling) middleware üzerinden başlatıyoruz.
app.UseMiddleware<GlobalExceptionMiddleware>();

// ---> SWAGGER 2: Geliştirme ortamındaysak tarayıcıda arayüzü göster <---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowVite");

app.UseRateLimiter(); // Rate Limiting middleware'i
app.UseAuthentication(); // Kimlik doğrulama
app.UseAuthorization();  // Yetki kontrolü

// JWT'nin Redis üzerinde blacklist'te olup olmadığını kontrol eden Middleware
app.UseMiddleware<TokenBlacklistMiddleware>();



// 4. Gelen HTTP isteklerini Controller'lara yönlendir
app.MapControllers();

// --- SignalR Hub Endpoint Mapping ---
// Admin Dashboard ws://host/hubs/flight-monitor adresine bağlanarak anlık bildirimleri dinleyebilir
app.MapHub<FlightMonitorHub>("/hubs/flight-monitor");

app.Run();