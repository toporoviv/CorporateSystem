using CorporateSystem.Auth.Domain.Entities;

namespace CorporateSystem.Auth.Services.Services.Interfaces;

public interface IUserService
{
    Task<IEnumerable<User>> GetUsersByIdsAsync(int[] ids, CancellationToken cancellationToken = default);
    Task<IEnumerable<User>> GetUsersByEmailsAsync(string[] emails, CancellationToken cancellationToken = default);
}