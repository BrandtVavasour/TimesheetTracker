using Amazon;
using Amazon.SimpleEmail;
using Delta;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using TimesheetTracker.DataModel;
using TimesheetTracker.Web.Components;
using TimesheetTracker.Web.Components.Account;
using TimesheetTracker.Web.Services;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Structured logging — Console always, Seq when SEQ_URL is configured.
    builder.Host.UseSerilog((_, _, configuration) =>
    {
        configuration
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .Enrich.WithProperty("Application", "TimesheetTracker")
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}");

        var seqUrl = Environment.GetEnvironmentVariable("SEQ_URL");
        if (!string.IsNullOrEmpty(seqUrl))
        {
            configuration.WriteTo.Seq(seqUrl);
        }
    });

    builder.Services.AddRazorComponents().AddInteractiveServerComponents();

    // ---- Configuration (env vars override appsettings; the deploy fills these) ----
    var connectionString = Environment.GetEnvironmentVariable("DATABASE_CONNECTION_STRING")
        ?? builder.Configuration.GetConnectionString("Default");

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
            options.UseNpgsql(
                connectionString ?? throw new InvalidOperationException(
                    "No database connection string. Set DATABASE_CONNECTION_STRING (or ConnectionStrings:Default)."),
                npgsql => npgsql.EnableRetryOnFailure());
        }
    });

    // Persist Data Protection keys (auth cookie + antiforgery) across restarts.
    var keysPath = Environment.GetEnvironmentVariable("DATA_PROTECTION_KEYS_PATH");
    if (!string.IsNullOrEmpty(keysPath))
    {
        builder.Services.AddDataProtection().PersistKeysToFileSystem(new(keysPath));
    }

    // Trust the reverse proxy (cloudflared / NPM) for scheme + client IP.
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });

    // ---- Email (AWS SES when configured; otherwise a no-op for local dev) ----
    var useSes = string.Equals(Environment.GetEnvironmentVariable("EMAIL_PROVIDER"), "ses", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SES_FROM_EMAIL"));
    if (useSes)
    {
        var region = Environment.GetEnvironmentVariable("SES_REGION")
            ?? Environment.GetEnvironmentVariable("AWS_REGION") ?? "ap-southeast-2";
        builder.Services.AddDefaultAWSOptions(new()
        {
            Region = RegionEndpoint.GetBySystemName(region)
        });
        builder.Services.AddAWSService<IAmazonSimpleEmailService>();
        builder.Services.AddSingleton<IEmailSender<AppUser>, SesEmailSender>();
    }
    else
    {
        builder.Services.AddSingleton<IEmailSender<AppUser>, IdentityNoOpEmailSender>();
    }

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
            // Require a confirmed email only when we can actually send one.
            options.SignIn.RequireConfirmedAccount = useSes;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<TimesheetDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders();

    builder.Services.AddSingleton<IClock, SystemClock>();
    builder.Services.AddScoped<ICurrentUser, CurrentUser>();
    builder.Services.AddScoped<ITimesheetData, TimesheetData>();

    var app = builder.Build();

    app.UseForwardedHeaders();
    app.UseSerilogRequestLogging();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseSecurityHeaders();
    app.UseStaticFiles();
    app.UseAuthentication();
    app.UseAuthorization();

    // ETag/304 response caching keyed on the database's last transaction
    // (Delta by Simon Cropp). Postgres only — the InMemory provider has no
    // transaction tracking. Suffixed per-user so one user's cached response
    // never validates for another; skipped for the Blazor circuit, auth
    // pages, and the health probe.
    if (!useInMemory)
    {
        app.UseDelta<TimesheetDbContext>(
            suffix: ctx => ctx.User.Identity?.Name,
            shouldExecute: ctx =>
            {
                // The per-user suffix requires an authenticated user, so only run
                // Delta for signed-in requests. Anonymous ones (favicon, static
                // assets, pre-login redirects) have no per-user cache to key and
                // would otherwise throw "suffix callback ... user is not authenticated".
                if (ctx.User.Identity?.IsAuthenticated != true)
                    return false;

                var path = ctx.Request.Path.Value ?? "";
                return !path.StartsWith("/_blazor", StringComparison.OrdinalIgnoreCase)
                    && !path.StartsWith("/Account", StringComparison.OrdinalIgnoreCase)
                    && !path.StartsWith("/health", StringComparison.OrdinalIgnoreCase);
            });
    }

    app.UseAntiforgery();

    app.MapGet("/health/live", () => Results.Ok("healthy"));
    app.MapGet("/favicon.ico", () => Results.Redirect("/favicon.svg", permanent: true));

    app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
    app.MapAdditionalIdentityEndpoints();

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
}
catch (Exception ex)
{
    Log.Fatal(ex, "Timesheet Tracker terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
