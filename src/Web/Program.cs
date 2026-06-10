using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TimesheetTracker.DataModel;
using TimesheetTracker.Web.Components;
using TimesheetTracker.Web.Components.Account;
using TimesheetTracker.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// ---- Configuration (env vars override appsettings; the deploy fills these) ----
// DATABASE_CONNECTION_STRING — Npgsql connection string (Portainer/Docker).
// GOOGLE_CLIENT_ID_WEB / GOOGLE_CLIENT_SECRET — Google OAuth.
var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("Default");

// In-memory only for local dev with no real connection string supplied.
var useInMemory = builder.Environment.IsDevelopment()
    && builder.Configuration.GetValue("UseInMemoryDatabase", true)
    && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING"));

builder.Services.AddTimesheetDataModel(options =>
{
    if (useInMemory)
    {
        options.UseInMemoryDatabase("timesheet-dev");
    }
    else
    {
        options.UseNpgsql(connectionString
            ?? throw new InvalidOperationException(
                "No database connection string. Set DATABASE_CONNECTION_STRING (or ConnectionStrings:Default)."));
    }
});

// Persist Data Protection keys (auth cookie + antiforgery) across container restarts.
var keysPath = Environment.GetEnvironmentVariable("DATA_PROTECTION_KEYS_PATH");
if (!string.IsNullOrEmpty(keysPath))
{
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keysPath));
}

// Trust the reverse proxy (cloudflared / NPM) for scheme + client IP, so OAuth
// redirects use https and auth cookies are marked Secure.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ---- Authentication & Identity ----
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
});
var googleClientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID_WEB")
    ?? builder.Configuration["Authentication:Google:ClientId"];
var googleSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET")
    ?? builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleSecret))
{
    authBuilder.AddGoogle(o =>
    {
        o.ClientId = googleClientId;
        o.ClientSecret = googleSecret;
    });
}
authBuilder.AddIdentityCookies();

builder.Services
    .AddIdentityCore<AppUser>(options =>
    {
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.User.RequireUniqueEmail = true;
        // No email sender wired yet — keep registration usable. Re-enable + plug a
        // real sender (e.g. AWS SES) before relying on email confirmation.
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<TimesheetDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<AppUser>, IdentityNoOpEmailSender>();

builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<ITimesheetData, TimesheetData>();

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

// Liveness probe for the container health check (anonymous).
app.MapGet("/health/live", () => Results.Ok("healthy"));

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapAdditionalIdentityEndpoints();

// Apply the schema / seed at startup.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TimesheetDbContext>();
    if (useInMemory)
    {
        await db.Database.EnsureCreatedAsync();
        await SeedData.SeedAsync(db);
    }
    else
    {
        await db.Database.MigrateAsync();
    }
}

app.Run();
