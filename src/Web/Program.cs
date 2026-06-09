using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TimesheetTracker.DataModel;
using TimesheetTracker.Web.Components;
using TimesheetTracker.Web.Services;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services
    .AddIdentity<AppUser, IdentityRole<Guid>>(options =>
    {
        // Security: password, lockout, and account requirements.
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = true;
    })
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

builder.Services.AddScoped<ICurrentUser, DemoCurrentUser>();
builder.Services.AddScoped<ITimesheetData, TimesheetData>();

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

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

// Seed the in-memory dev database with the sample week.
if (useInMemory)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TimesheetDbContext>();
    await db.Database.EnsureCreatedAsync();
    await SeedData.SeedAsync(db);
}

app.Run();
