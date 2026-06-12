using System.Threading.RateLimiting;
using Amazon;
using Amazon.SimpleEmail;
using Delta;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
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

    // Trust the reverse proxy (cloudflared / NPM) for scheme + client IP, but
    // ONLY from known private networks — otherwise any peer that can reach the
    // container could spoof X-Forwarded-For / -Proto. Pin the exact proxy with
    // FORWARDED_KNOWN_NETWORKS (comma-separated CIDRs); defaults to RFC1918 + loopback.
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        foreach (var net in ProxyNetworks.Resolve(Environment.GetEnvironmentVariable("FORWARDED_KNOWN_NETWORKS")))
        {
            options.KnownIPNetworks.Add(net);
        }
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

    // Harden the auth cookie: always Secure (don't depend on forwarded-proto
    // timing), HttpOnly, SameSite=Lax (required for the OAuth return), and a
    // bounded sliding lifetime instead of the 14-day default.
    builder.Services.ConfigureApplicationCookie(o =>
    {
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
    });

    // NB: do NOT set the antiforgery cookie SecurePolicy = Always — unlike the
    // auth cookie (which merely stamps Secure), the antiforgery system THROWS on
    // any non-SSL request when Always is set, which would 500 every form page if
    // the origin ever saw HTTP. Leave it SameAsRequest; the prod scheme is forced
    // to https below so the cookie still ships Secure in production.

    // Longer HSTS than the 30-day default (the header passes through Cloudflare).
    builder.Services.AddHsts(o => o.MaxAge = TimeSpan.FromDays(365));

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
    builder.Services.AddScoped<IToastService, ToastService>();
    builder.Services.AddScoped<IAccountInfo, AccountInfo>();
    builder.Services.AddScoped<IAdminService, AdminService>();
    builder.Services.AddSingleton<IAssetVersion, AssetVersion>();
    builder.Services.AddScoped<FluentValidation.IValidator<Job>, TimesheetTracker.DataModel.Validation.JobValidator>();
    builder.Services.AddScoped<FluentValidation.IValidator<TimesheetTracker.Web.Validation.ProfileForm>, TimesheetTracker.Web.Validation.ProfileFormValidator>();

    // Throttle the unauthenticated auth POSTs (login / register / forgot- and
    // reset-password / external-login) per client IP — Identity lockout only
    // protects a single known account, not credential stuffing or password-
    // reset email flooding. Keyed on the (now trusted) forwarded client IP.
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        {
            if (HttpMethods.IsPost(ctx.Request.Method)
                && ctx.Request.Path.StartsWithSegments("/Account", StringComparison.OrdinalIgnoreCase))
            {
                // Key on the REAL client IP (CF-Connecting-IP behind the tunnel).
                // Using Connection.RemoteIpAddress would be the single cloudflared
                // IP for everyone and 429 legitimate sign-ups. The limit is a
                // generous backstop — a human auth flow is a handful of POSTs;
                // Cloudflare's WAF is the primary volumetric defence.
                return RateLimitPartition.GetFixedWindowLimiter($"auth:{ClientIp.For(ctx)}", _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                });
            }
            return RateLimitPartition.GetNoLimiter("unlimited");
        });
    });

    // Operator-assigned admin(s). Comma/semicolon separated; granted the Admin
    // role on startup. If unset, no admin is granted (the bootstrap breaks out).
    var adminEmails = Environment.GetEnvironmentVariable("ADMIN_EMAIL")
        ?? builder.Configuration["ADMIN_EMAIL"];

    var app = builder.Build();

    app.UseForwardedHeaders();

    if (!app.Environment.IsDevelopment())
    {
        // The origin is HTTP-only and only ever reached via Cloudflare over HTTPS
        // (TLS terminates at the edge). Force the scheme so Secure cookies, OAuth
        // redirect URIs, and request logging are correct deterministically —
        // without depending on cloudflared reliably sending X-Forwarded-Proto.
        app.Use((ctx, next) =>
        {
            ctx.Request.Scheme = "https";
            return next();
        });
    }

    app.UseSerilogRequestLogging();
    app.UseRateLimiter();

    if (!app.Environment.IsDevelopment())
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseSecurityHeaders();
    // Short-circuit dotfile probes (/.env, /.git/config, …) with a clean 404
    // before they reach static-file serving or routing.
    app.UseHiddenPathGuard();
    app.UseStaticFiles(new StaticFileOptions
    {
        // App.razor stamps ?v=<content-hash> on ts.js/app.css, so cached copies
        // are busted on deploy and a long max-age is safe. Without an explicit
        // Cache-Control, browsers heuristically cached stale assets after deploys.
        OnPrepareResponse = ctx =>
            ctx.Context.Response.Headers.CacheControl = "public,max-age=604800",
    });
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

        // Grant the Admin role to the configured ADMIN_EMAIL(s) now that the
        // schema exists. No-op (and logs a warning) when ADMIN_EMAIL is unset.
        await AdminBootstrap.EnsureAdminsAsync(scope.ServiceProvider, adminEmails, app.Logger);
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
