using CorporateSystem.Auth.Services.Services.GrpcServices;
using CorporateSystem.Auth.Services.Services.Implementations;
using CorporateSystem.Auth.Services.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace CorporateSystem.Auth.Services.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAuthServices(this IServiceCollection services)
    {
        return services
            .AddScoped<IAuthService, UserService>()
            .AddScoped<IRegistrationService, UserService>()
            .AddScoped<GrpcNotificationClient>()
            .AddScoped<IUserService, UserService>()
            .AddSingleton<IPasswordHasher, PasswordHasher>();
    }
}