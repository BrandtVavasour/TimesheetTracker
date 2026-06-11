using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TimesheetTracker.DataModel.Services;
using TimesheetTracker.DataModel.Services.Export;

namespace TimesheetTracker.DataModel;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTimesheetDataModel(this IServiceCollection services, Action<DbContextOptionsBuilder> configureDb)
    {
        // Factory + scoped wrapper. The data layer pulls short-lived contexts from
        // the factory so concurrent operations on a Blazor Server circuit never share
        // one context ("A second operation was started on this context instance").
        // A scoped context is still registered (created from the factory) for the
        // Identity EF stores, Delta, and the startup migration.
        services.AddDbContextFactory<TimesheetDbContext>(configureDb);
        services.AddScoped<TimesheetDbContext>(sp =>
            sp.GetRequiredService<IDbContextFactory<TimesheetDbContext>>().CreateDbContext());
        services.AddScoped<ITimeCalculationService, TimeCalculationService>();
        services.AddScoped<IHolidayService, HolidayService>();
        services.AddScoped<IExportService, ExportService>();
        return services;
    }
}
