using System.Threading.RateLimiting;
using CRC.Api.Security;
using CRC.Api.Swagger;
using CRC.Data.Database;
using CRC.Data.Repositories;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// -----------------------------
// Controllers + API behavior
// -----------------------------
builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        // Make model validation failures predictable for clients.
        options.InvalidModelStateResponseFactory = context =>
        {
            var problem = new ValidationProblemDetails(context.ModelState)
            {
                Title = "Validation failed",
                Status = StatusCodes.Status400BadRequest
            };

            return new BadRequestObjectResult(problem);
        };
    });

builder.Services.AddEndpointsApiExplorer();

// -----------------------------
// Swagger
// -----------------------------
builder.Services.AddSwaggerGen(c =>
{
    c.OperationFilter<SwaggerApiKeyOperationFilter>();
});

// -----------------------------
// Security: API Key auth (secure by default)
// -----------------------------
builder.Services
    .AddAuthentication(ApiKeyAuthenticationOptions.Scheme)
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationOptions.Scheme,
        options =>
        {
            // Defaults; can be overridden in appsettings.
            options.HeaderName = builder.Configuration["Security:ApiKeyHeader"] ?? "X-Api-Key";
            options.ApiKey = builder.Configuration["Security:ApiKey"] ?? string.Empty;
        });

builder.Services.AddAuthorization(options =>
{
    // Require API key for ALL endpoints by default.
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .AddAuthenticationSchemes(ApiKeyAuthenticationOptions.Scheme)
        .RequireAuthenticatedUser()
        .Build();
});

// -----------------------------
// CORS (strict)
// -----------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy("PublicCors", policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>()
            ?? Array.Empty<string>();

        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .WithMethods("POST")
                  .WithHeaders("Content-Type", builder.Configuration["Security:ApiKeyHeader"] ?? "X-Api-Key")
                  .DisallowCredentials();
        }
        // If no origins are configured, we intentionally do NOT allow any browser origin.
    });
});

// -----------------------------
// Rate limiting (ready to tune)
// -----------------------------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    var permitLimit = builder.Configuration.GetValue<int?>("RateLimiting:PermitLimit") ?? 30;
    var windowSeconds = builder.Configuration.GetValue<int?>("RateLimiting:WindowSeconds") ?? 60;
    var queueLimit = builder.Configuration.GetValue<int?>("RateLimiting:QueueLimit") ?? 0;

    options.AddPolicy("PublicApi", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ip,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = TimeSpan.FromSeconds(windowSeconds),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = queueLimit
            });
    });
});

// -----------------------------
// DB helper + repositories (shared)
// -----------------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<DatabaseHelper>();
builder.Services.AddScoped<PatientBasicRepository>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseRouting();
app.UseCors("PublicCors");

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers().RequireRateLimiting("PublicApi");

app.Run();