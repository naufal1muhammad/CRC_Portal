using CRC.Web.Infrastructure;
using CRC.Web.Models;
using CRC.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using Serilog.Events;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var logsDirectory = Path.Combine(builder.Environment.ContentRootPath, "Logs");
Directory.CreateDirectory(logsDirectory);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .WriteTo.Logger(lc => lc
        .Filter.ByExcluding(e => e.Properties.ContainsKey("AuditChannel"))
        .WriteTo.File(
            path: Path.Combine(logsDirectory, "app-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 31,
            shared: true,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [Cid:{CorrelationId}] [User:{UserName}] [Ip:{RemoteIp}] {Message:lj} {Exception}{NewLine}"))
    .WriteTo.Logger(lc => lc
        .Filter.ByIncludingOnly(e => e.Properties.ContainsKey("AuditChannel"))
        .WriteTo.File(
            path: Path.Combine(logsDirectory, "audit-.log"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 365,
            shared: true,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [Cid:{CorrelationId}] [User:{UserName}] [Ip:{RemoteIp}] {Message:lj}{NewLine}"))
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

builder.Services.Configure<PasswordPolicyOptions>(
    builder.Configuration.GetSection("Account:Password"));

builder.Services.Configure<SessionTimeoutOptions>(
    builder.Configuration.GetSection("Account:SessionTimeout"));

builder.Services.Configure<LoginLockoutOptions>(
    builder.Configuration.GetSection("Account:LoginLockout"));

var sessionTimeout = builder.Configuration
    .GetSection("Account:SessionTimeout")
    .Get<SessionTimeoutOptions>() ?? new SessionTimeoutOptions();

var lockoutOptions = builder.Configuration
    .GetSection("Account:LoginLockout")
    .Get<LoginLockoutOptions>() ?? new LoginLockoutOptions();

// Add services to the container.
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "__Host-CSRF";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.HttpOnly = true;
    options.Cookie.Path = "/";
});
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AuthorizeFilter());
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
})
    // The Agent API's controller lives in the CRC.Api class library, not in this project. MVC only scans
    // the entry assembly and the ones it is told about, so WITHOUT THIS LINE AgentApiController is never
    // discovered and every /api/agent route answers 404 — with no error, no warning and nothing in a log.
    // Its attribute routes then ride along with the existing app.MapControllerRoute(...) below, which is
    // what creates the controller endpoint data source. See CoreFlow.md §13.2.
    .AddApplicationPart(typeof(CRC.Api.Controllers.AgentApiController).Assembly);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CRC.Data.Data.DatabaseHelper>();

// The Dapper data-access layer: one typed method per stored procedure, the only place in the
// solution that names one. Scoped, because it resolves the current user's id per request for the
// audit-actor parameter. See CoreFlow.md §6.
builder.Services.AddScoped<CRC.Data.Data.IDatabaseData, CRC.Data.Data.SqlData>();

// Patient and staff document storage: a private Azure Blob container, metadata-only in SQL, SAS downloads.
// Bound from the DocumentStorage config section (Azurite locally; the storage-account connection string in
// Azure, supplied as the DocumentStorage__* app settings). The BlobServiceClient inside is thread-safe and
// meant to be reused, so the service is registered as a singleton.
builder.Services.Configure<DocumentStorageOptions>(
    builder.Configuration.GetSection(DocumentStorageOptions.SectionName));
builder.Services.AddSingleton<IDocumentStorage, AzureBlobDocumentStorage>();

// The Agent API's shared key, bound from the "Agent" section exactly as DocumentStorage above is bound
// from its own. appsettings.json carries an empty placeholder and appsettings.Development.json a
// development-only value; the real key is the App Service app setting Agent__ApiKey (TWO underscores),
// set by hand and never in source control. An empty key is treated as a misconfiguration by the filter
// and refuses every request — it never means "no key required". See CoreFlow.md §13.6.
builder.Services.Configure<CRC.Api.AgentApiOptions>(
    builder.Configuration.GetSection(CRC.Api.AgentApiOptions.SectionName));

// 🔴 The only thing standing between /api/agent and an unauthenticated caller: AgentApiController turns
// off the global AuthorizeFilter with [AllowAnonymous] and reaches this filter through [ServiceFilter],
// which resolves it from the container — so a missing registration is a request-time failure, not a
// silent one. SCOPED, because it constructor-injects IDatabaseData (itself scoped) to resolve the
// AGENT_SERVICE actor once per request; a singleton would capture a scoped dependency and a transient
// would build a second one per request for no gain. See CoreFlow.md §13.3.
builder.Services.AddScoped<CRC.Api.Infrastructure.AgentApiKeyFilter>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromSeconds(sessionTimeout.InactivityTimeoutSeconds);
        options.SlidingExpiration = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });
// Per-IP rate limiter for the login endpoint. Mitigates credential stuffing and
// distributed brute-force attempts that target many usernames from a single IP.
var ipRateLimitWindowSeconds = lockoutOptions.IpRateLimitWindowSeconds > 0
    ? lockoutOptions.IpRateLimitWindowSeconds
    : 60;

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        var ip = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        AuditLog.LoginRateLimited(context.HttpContext, ip);

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        if (!context.HttpContext.Response.HasStarted)
        {
            context.HttpContext.Response.Headers["Retry-After"] =
                ipRateLimitWindowSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await context.HttpContext.Response.WriteAsync(
                "Too many login attempts from this address. Please wait and try again.",
                cancellationToken);
        }
    };

    options.AddPolicy("login-ip", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = lockoutOptions.IpRequestsPerWindow > 0 ? lockoutOptions.IpRequestsPerWindow : 10,
            Window = TimeSpan.FromSeconds(ipRateLimitWindowSeconds),
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        });
    });
});

builder.Services.AddAuthorization(options =>
{
    // UserType claim values:
    // 1 = SUPERUSER, 2 = ADMIN, 3 = STAFF
    options.AddPolicy("SuperUserOnly", policy => policy.RequireClaim("UserType", "1"));
    options.AddPolicy("AdminOrSuper", policy => policy.RequireClaim("UserType", "1", "2"));
    options.AddPolicy("AdminOnly", policy => policy.RequireClaim("UserType", "2"));
    options.AddPolicy("StaffOnly", policy => policy.RequireClaim("UserType", "3"));
    options.AddPolicy("AdminOrSuperOrStaff", policy => policy.RequireClaim("UserType", "1", "2", "3"));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// Nothing under /uploads is ever served again. Patient and staff documents live in a PRIVATE Azure
// Blob container (Services/AzureBlobDocumentStorage.cs) and are reached only through the
// authenticated *DocumentUrl endpoints, which mint a 5-minute read SAS.
//
// This branch exists because UseStaticFiles() runs BEFORE authentication and performs no
// authorisation check of its own: any file that ends up under wwwroot is public, permanently, to
// anyone holding the URL. Files uploaded by earlier versions are STILL PHYSICALLY PRESENT on the
// Azure App Service disk — a publish never deletes them ("Remove additional files at destination"
// is deliberately off, to protect logs) — so this 404 is what actually makes them unreachable.
// Do not remove it, and do not narrow it to a sub-path.
app.UseWhen(
    ctx => ctx.Request.Path.StartsWithSegments("/uploads", StringComparison.OrdinalIgnoreCase),
    branch => branch.Run(ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }));

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
