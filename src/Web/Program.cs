using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TimesheetTracker.DataModel;
using TimesheetTracker.Web.Components;
using TimesheetTracker.Web.Components.Account;
using TimesheetTracker.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

// EF Core provider: in-memory for local dev/demo (seeded), Npgsql for production.
var useInMemory = builder.Environment.IsDevelopment()
    && builder.Configuration.GetValue("UseInMemoryDatabase", true);

builder.Services.AddTimesheetDataModel(options =>
{
    if (useInMemory)
    {
        options.UseInMemoryDatabase("timesheet-dev");
    }
    else
    {
        options.UseNpgsql(builder.Configuration.GetConnectionString("Default")!);
    }
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
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleSecret = builder.Configuration["Authentication:Google:ClientSecret"];
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
        // real sender (e.g. AWS SES) before production.
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<TimesheetDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<AppUser>, IdentityNoOpEmailSender>();

// Current-user accessor (reads the signed-in Identity user) + user-scoped data.
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<ITimesheetData, TimesheetData>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapAdditionalIdentityEndpoints();

// Seed the in-memory dev database with the sample week (and a loginable dev user).
if (useInMemory)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TimesheetDbContext>();
    await db.Database.EnsureCreatedAsync();
    await SeedData.SeedAsync(db);
}

app.Run();
