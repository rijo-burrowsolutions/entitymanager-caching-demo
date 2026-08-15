// PURPOSE: one-line setup, called once from Program.cs at startup, that reads
// the "Redis" section from appsettings.json, connects to Redis, and registers
// that single shared connection in the app's DI container. Nobody calls this
// during a request - only once, when the app boots.
namespace Ag.Cache;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

public static class AgCachingServiceExtensions
{
    public static IServiceCollection AddAgCaching(this IServiceCollection services, IConfiguration configuration)
    {
        var redisSection = configuration.GetSection("Redis");

        var options = new ConfigurationOptions
        {
            EndPoints = { $"{redisSection["Host"]}:{redisSection["Port"]}" },
            Password = redisSection["Password"],
            Ssl = redisSection.GetValue<bool>("Ssl"),
            AbortOnConnectFail = false // don't crash the API at startup if Redis is briefly down
        };

        services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(options));
        return services;
    }
}
