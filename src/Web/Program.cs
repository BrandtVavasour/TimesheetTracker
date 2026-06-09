using Microsoft.AspNetCore.Identity;
using TimesheetTracker.DataModel;
using TimesheetTracker.Web.Components;
using TimesheetTracker.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTimesheetDataModel(builder.Configuration.GetConnectionString("Default")!);

builder.Services
    .AddIdentity<AppUser, IdentityRole<Guid>>()
    .AddEntityFrameworkStores<TimesheetDbContext>()
    .AddDefaultTokenProviders();

var authBuilder = builder.Services.AddAuthentication();
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

// Demo data store (per-circuit) seeded with the sample week.
// Stands in for EF Core + Identity-scoped persistence (follow-up task).
builder.Services.AddScoped<TimesheetStore>();

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

app.Run();
