using PMS.web.Providers;
using Core.Entities;
using Core.Interfaces.Repositories;
using Core.Interfaces.Services;
using Core.Interfaces.Services.AccessControl;
using Core.Interfaces.Services.Organization;
using Core.Interfaces.Services.Workflow;
using DataAccess;
using DataAccess.Repositories;
using DataAccess.UnitOfWork;
using Core.Interfaces.UnitOfWork;
using PMS.web.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Core.Options;
using Services;
using Services.AccessControl;
using Services.AccessControl.Workers;
using PMS.web.Hosting;

// When hosted as a Windows Service, default cwd is System32 — pin to the publish folder.
Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var builder = WebApplication.CreateBuilder(args);

// Allow running as a Windows Service (always-on hosting on the server).
builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "PMS Web Host";
});

// ZKTeco readers open the connection themselves, so binding must cover every NIC
// (Kestrel:Endpoints in appsettings). Raw TCP logging tells us whether a device
// reached the socket at all, even when its HTTP request is malformed/rejected.
// Host-header injection must run BEFORE Kestrel HTTP parsing (and before TCP logging).
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureEndpointDefaults(listen =>
    {
        listen.UseZkTecoHttpCompatibility();
        if (builder.Configuration.GetValue($"{ZkTecoOptions.SectionName}:LogRawTcp", false))
            listen.UseConnectionLogging("ZkTeco.Tcp");
    });
});

// ---------------- Session (kept for the legacy MVC controllers/views) ----------------
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ---------------- Database ----------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContextFactory<DataContext>(options =>
    options.UseSqlServer(connectionString));

// Identity and some MVC paths still use a scoped DataContext.
builder.Services.AddDbContext<DataContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();


// ---------------- Identity ----------------
builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<DataContext>()
.AddDefaultTokenProviders();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();
builder.Services.AddHttpContextAccessor();

// ---------------- Authentication & Authorization ----------------
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/login";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    options.SlidingExpiration = true;
});

var disableAuthentication = builder.Configuration.GetValue("DesignMode:DisableAuthentication", false);

builder.Services.AddAuthorization(options =>
{
    // Design Mode: open the UI without login so shell/layout can be iterated freely.
    // Re-enable by setting DesignMode:DisableAuthentication = false in appsettings.
    if (!disableAuthentication)
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    }
});

// ---------------- Dependency Injection ----------------
// Transient UnitOfWork: each service gets its own DbContext (via factory) so Blazor
// layout + page can query concurrently without sharing one context instance.
builder.Services.AddTransient<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

// Legacy Phase 2 services (unchanged)
builder.Services.AddTransient<IGates, GateServices>();
builder.Services.AddTransient<IPermitType, PermitTypeServices>();
builder.Services.AddScoped<IPermit, PermitServices>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<IProcedureMovment, ProcedureMovmentService>();
builder.Services.AddScoped<IUserProvider, UserProvider>();

// Phase 1: Organization / Access Control services
builder.Services.AddScoped<IEmployeeService, Services.Organization.EmployeeService>();
builder.Services.AddScoped<IDepartmentMasterService, Services.Organization.DepartmentMasterService>();
builder.Services.AddTransient<IBiometricTemplateService, Services.AccessControl.BiometricTemplateService>();
builder.Services.AddTransient<IBiometricIngestService, Services.AccessControl.BiometricIngestService>();
builder.Services.AddTransient<IReaderService, Services.AccessControl.ReaderService>();
builder.Services.AddTransient<IReaderDowntimeService, Services.AccessControl.ReaderDowntimeService>();
builder.Services.AddTransient<IEmployeePermissionService, Services.AccessControl.EmployeePermissionService>();
builder.Services.AddTransient<IEmployeeDeviceSyncService, Services.AccessControl.EmployeeDeviceSyncService>();
builder.Services.AddTransient<IDeviceCommandService, Services.AccessControl.DeviceCommandService>();
builder.Services.AddTransient<IMovementLogService, Services.AccessControl.MovementLogService>();
builder.Services.AddTransient<IMovementLogIngestService, Services.AccessControl.MovementLogIngestService>();
builder.Services.AddTransient<IZkTecoPushService, ZkTecoPushService>();
builder.Services.AddTransient<IReaderDeviceStatsService, Services.AccessControl.ReaderDeviceStatsService>();
builder.Services.AddSingleton<IReaderConnectionLogService, ReaderConnectionLogService>();
builder.Services.AddSingleton<IDevicePushLogService, DevicePushLogService>();
builder.Services.AddSingleton<IReaderSyncStateService, ReaderSyncStateService>();

builder.Services.Configure<ZkTecoOptions>(builder.Configuration.GetSection(ZkTecoOptions.SectionName));
builder.Services.AddHostedService<DeviceCommandWorker>();
builder.Services.AddHostedService<DeviceHealthMonitorWorker>();
builder.Services.AddHostedService<Services.AccessControl.Workers.ReaderSyncSchedulerWorker>();

// Phase 2: Workflow services
builder.Services.AddScoped<IWorkflowEngine, Services.Workflow.WorkflowEngine>();
builder.Services.AddTransient<INotificationService, Services.Workflow.NotificationService>();

// ---------------- Blazor Server ----------------
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient();

// Legacy MVC controllers/views are kept reachable under the "/legacy" prefix
// (see MapControllerRoute below) plus the PDF-generation endpoint on PermitController.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

app.UseRouting();

app.UseSession();

// ---------------- Pipeline ----------------
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error", createScopeForErrors: true);
    // Skip HSTS for HTTP-only ADMS deployments (see Hosting:DisableHttpsRedirection).
    if (!app.Configuration.GetValue("Hosting:DisableHttpsRedirection", true))
        app.UseHsts();
}

// ZKTeco ADMS/Push speaks plain HTTP. Keep redirect optional so devices can hit /iclock.
// Set Hosting:DisableHttpsRedirection = false only when terminating TLS in front of the app.
var disableHttpsRedirect = app.Configuration.GetValue("Hosting:DisableHttpsRedirection", true);
if (!app.Environment.IsDevelopment() && !disableHttpsRedirect)
{
    app.UseHttpsRedirection();
}

// Early diagnostic: log device traffic before auth/antiforgery. Remote (non-loopback)
// requests are logged whatever the path, so a reader hitting "/" or an unexpected
// ADMS path is still visible instead of silently 404-ing.
app.Use(async (context, next) =>
{
    var ip = context.Connection.RemoteIpAddress;
    if (ip != null && ip.IsIPv4MappedToIPv6)
        ip = ip.MapToIPv4();

    var isIclock = context.Request.Path.StartsWithSegments("/iclock");
    var isRemote = ip != null && !System.Net.IPAddress.IsLoopback(ip);

    if (isIclock || isRemote)
    {
        var log = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("ZkTeco.Raw");
        var label = isIclock ? "ICLOCK RAW" : "REMOTE HIT";

        log.LogWarning(
            "{Label} {Method} {Path}{Query} from {IP} agent={Agent}",
            label,
            context.Request.Method,
            context.Request.Path.Value,
            context.Request.QueryString.Value,
            ip?.ToString() ?? "unknown",
            context.Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : "-");

        // Also record into the same in-memory log used by /devices/monitor.
        // This ensures we see endpoints even if they were routed as "unhandled /iclock/*".
        try
        {
            var connectionLog = context.RequestServices.GetRequiredService<Core.Interfaces.Services.AccessControl.IReaderConnectionLogService>();

            // لا نعمل أي Lookup على قاعدة البيانات هنا.
            // الهدف الوحيد: إظهار endpoints التي يطلبها الجهاز (getrequest/ping vs فقط cdata/registry).
            var sn = context.Request.Query["SN"].ToString();
            var isRegistered = !string.IsNullOrWhiteSpace(sn);

            connectionLog.Record(new Core.Models.ReaderConnectionEvent(
                ReceivedAt: DateTime.Now,
                DeviceSerial: sn,
                RemoteIp: ip?.ToString() ?? "unknown",
                Endpoint: context.Request.Path.Value ?? "-",
                HttpMethod: context.Request.Method,
                IsRegistered: isRegistered,
                Note: null));
        }
        catch
        {
            // Diagnostics only; never block device traffic.
        }

        await next();

        log.LogWarning(
            "{Label} RESPONSE {Status} for {Path} to {IP}",
            label, context.Response.StatusCode, context.Request.Path.Value, ip?.ToString() ?? "unknown");
        return;
    }

    await next();
});

app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

// Legacy MVC (kept for backward compatibility while pages are ported to Blazor)
app.MapControllerRoute(
    name: "legacy",
    pattern: "legacy/{controller=Permit}/{action=PermitIndex}/{id?}");
app.MapControllers();
app.MapRazorPages();

// Unhandled /iclock/* paths still return OK so firmware keeps trying (logged as UNHANDLED).
app.Map("/iclock/{**rest}", (HttpContext ctx, ILoggerFactory factory) =>
{
    factory.CreateLogger("ZkTeco.Raw").LogWarning(
        "ICLOCK UNHANDLED {Method} {Path}{Query}",
        ctx.Request.Method, ctx.Request.Path.Value, ctx.Request.QueryString.Value);
    return Results.Text("OK", "text/plain");
}).AllowAnonymous();

// Blazor Server is the primary UI
app.MapRazorComponents<PMS.web.Components.App>()
    .AddInteractiveServerRenderMode();

// Print the exact ADMS URLs to configure on the readers (localhost is never usable there).
app.Lifetime.ApplicationStarted.Register(() =>
{
    var log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ZkTeco.Hosting");
    var port = app.Urls
        .Select(u => Uri.TryCreate(u, UriKind.Absolute, out var parsed) ? parsed.Port : 0)
        .FirstOrDefault(p => p > 0);

    log.LogInformation("Listening on: {Urls}", string.Join(", ", app.Urls));

    foreach (var ip in PMS.web.HostAddressInfo.GetLanAddresses())
        log.LogInformation("Reader ADMS URL candidate: http://{Ip}:{Port}/iclock", ip, port);
});

// Clean up stale commands from previous runs
using (var startupScope = app.Services.CreateScope())
{
    var cmdSvc = startupScope.ServiceProvider.GetRequiredService<IDeviceCommandService>();
    var cancelled = await cmdSvc.CancelStaleAsync(TimeSpan.FromMinutes(10));
    if (cancelled > 0)
        app.Logger.LogWarning("Startup: cancelled {Count} stale device commands", cancelled);
}

app.Run();
