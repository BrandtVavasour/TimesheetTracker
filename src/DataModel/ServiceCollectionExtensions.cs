using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TimesheetTracker.DataModel.Services;
using TimesheetTracker.DataModel.Services.Export;

namespace TimesheetTracker.DataModel;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTimesheetDataModel(this IServiceCollection services, Action<DbContextOptionsBuilder> configureDb)
    {
        services.AddDbContext<TimesheetDbContext>(configureDb);
        services.AddScoped<ITimeCalculationService, TimeCalculationService>();
        services.AddScoped<IHolidayService, HolidayService>();
        services.AddScoped<IExportService, ExportService>();
        return services;
    }
}
