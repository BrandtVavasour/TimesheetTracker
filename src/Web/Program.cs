using Microsoft.AspNetCore.Identity;
using TimesheetTracker.DataModel;

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

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/", () => "Timesheet Tracker");
app.Run();
