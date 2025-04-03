using CorporateSystem.Auth.Infrastructure;
using CorporateSystem.Auth.Infrastructure.Extensions;
using CorporateSystem.Auth.Services.Extensions;
using CorporateSystem.Auth.Services.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CorporateSystem.Auth.Tests;

public class TestFixture : IDisposable
{
    private readonly IServiceProvider _services;

    public TestFixture()
    {
        _services = CreateServiceProvider();
    }

    private IServiceProvider CreateServiceProvider()
    {
        var serviceCollection = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();
        
        serviceCollection.Configure<JwtToken>(configuration.GetSection("JwtToken"));
        serviceCollection.AddDbContext<DataContext>(options =>
            options.UseInMemoryDatabase("TestDb"));
        
        serviceCollection
            .AddAuthInfrastructure()
            .AddAuthServices()
            .AddScoped<IDistributedCache, FakeRedisCache>()
            .AddLogging();
        
        return serviceCollection.BuildServiceProvider();
    }

    public TService GetService<TService>() where TService : notnull
        => _services.GetRequiredService<TService>(); 

    public void Dispose()
    {
        var dataContext = GetService<DataContext>();
        dataContext.Database.EnsureDeleted();
        dataContext.Dispose();
    }
}