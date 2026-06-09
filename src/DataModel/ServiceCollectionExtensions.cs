using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TimesheetTracker.DataModel.Services;
using TimesheetTracker.DataModel.Services.Export;

namespace TimesheetTracker.DataModel;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTimesheetDataModel(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<TimesheetDbContext>(o => o.UseNpgsql(connectionString));
        services.AddScoped<ITimeCalculationService, TimeCalculationService>();
        services.AddScoped<IHolidayService, HolidayService>();
        services.AddScoped<IExportService, ExportService>();
        return services;
    }
}
