using Ag.Cache;
using EntityManager.Domain.Repositories;
using EntityManager.Infrastructure.Persistence;
using EntityManager.Infrastructure.Persistence.Repositories;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// PURPOSE: the one place everything for this module gets wired together at
// startup - the database, Mediator, the caching behavior, and the
// repositories. Called once from Program.cs. This file itself lives in
// Infrastructure, not Application - registering services is a setup/wiring
// concern, not business logic.
//
// NOTE: this now points at the REAL idc_ety SQL Server database (same
// connection string ag-kit itself uses) instead of the SQLite stand-in -
// see appsettings.json's "IDC_ETY" connection string. Set "UseSandboxDb":
// true in appsettings.json (or the ASPNETCORE_UseSandboxDb env var) to
// point this at the local, full-read/write LocalDB copy instead - see
// Tools/SandboxSetup for how that copy gets created.
namespace EntityManager.Infrastructure.DependencyInjection;

public static class EntityManagerModuleServiceExtensions
{
    public static IServiceCollection AddEntityManagerModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionStringName = configuration.GetValue<bool>("UseSandboxDb") ? "IDC_ETY_SANDBOX" : "IDC_ETY";
        services.AddDbContext<EntityManagerDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString(connectionStringName)));

        services.AddMediator(config =>
        {
            config.ServiceLifetime = ServiceLifetime.Scoped;
            // Tells Mediator's source generator which assembly to scan for handlers.
            config.Assemblies = [typeof(EntityManager.Application.Queries.GetAgentQuery)];
        });

        // This one line is the entire caching feature turning on for this module -
        // Mediator will now run CachingPipelineBehavior before every query's real handler.
        services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(CachingPipelineBehavior<,>));

        // GET-only repositories - no write methods exist anywhere in this demo.
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<IOfficeRepository, OfficeRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();

        // Registered directly (not just via Mediator's IRequestHandler<,> wiring) so
        // InternalCacheEndpoints can call a handler's Handle(...) straight, bypassing
        // the cache-check step in CachingPipelineBehavior on purpose for refreshes.
        services.AddScoped<EntityManager.Application.Queries.GetAgentQueryHandler>();
        services.AddScoped<EntityManager.Application.Queries.GetOfficeQueryHandler>();
        services.AddScoped<EntityManager.Application.Queries.GetCompanyQueryHandler>();
        services.AddScoped<EntityManager.Application.Queries.GetAgentListQueryHandler>();
        services.AddScoped<EntityManager.Application.Queries.GetAgentIdListQueryHandler>();
        services.AddScoped<EntityManager.Application.Queries.GetOfficeListQueryHandler>();
        services.AddScoped<EntityManager.Application.Queries.GetOfficeIdListQueryHandler>();
        services.AddScoped<EntityManager.Application.Queries.GetCompanyListQueryHandler>();
        services.AddScoped<EntityManager.Application.Queries.GetCompanyIdListQueryHandler>();

        return services;
    }
}
