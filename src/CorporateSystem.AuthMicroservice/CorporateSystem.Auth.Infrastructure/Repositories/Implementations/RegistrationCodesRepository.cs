using System.Net;
using CorporateSystem.Auth.Domain.Exceptions;
using CorporateSystem.Auth.Infrastructure.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace CorporateSystem.Auth.Infrastructure.Repositories.Implementations;

internal class RegistrationCodesRepository(
    IConnectionMultiplexer redis,
    ILogger<RegistrationCodesRepository> logger)
    : IRegistrationCodesRepository
{
    private readonly IDatabase _database = redis.GetDatabase();
    private const string KeyPrefix = "registration_codes";
    
    public async Task<int?> GetAsync(int code, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        logger.LogInformation($"Try getting code={code}");
        var key = GetKey(code);
        var value = await _database.StringGetAsync(key);

        logger.LogInformation($"Get value={value}");
        
        if (value.IsNullOrEmpty)
            return null;

        if (!int.TryParse(value, out var result))
        {
            logger.LogError($"Не удается преобразовать {value} в int");
            throw new ExceptionWithStatusCode("Что-то пошло не так", HttpStatusCode.BadRequest);
        }

        return result;
    }

    public async Task CreateAsync(int code, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var key = GetKey(code);
        await _database.StringSetAsync(key, code.ToString(), TimeSpan.FromMinutes(5));
    }

    public async Task DeleteAsync(int code, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        var key = GetKey(code);
        await _database.KeyDeleteAsync(key);
    }
    
    private RedisKey GetKey(int code)
        => new($"{KeyPrefix}:{code}");
}