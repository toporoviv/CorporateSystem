using System.Net;
using CorporateSystem.Auth.Domain.Entities;
using CorporateSystem.Auth.Domain.Exceptions;
using CorporateSystem.Auth.Infrastructure.Extensions;
using CorporateSystem.Auth.Infrastructure.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CorporateSystem.Auth.Infrastructure.Repositories.Implementations;

internal class UserRepository(IContextFactory contextFactory, ILogger<UserRepository> logger) : IUserRepository
{
    public async Task<User?> GetUserByEmailAsync(string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ExceptionWithStatusCode("Что-то пошло не так", HttpStatusCode.BadRequest);
        }

        return await contextFactory.ExecuteWithoutCommitAsync(
            async context => await context.Users.FirstOrDefaultAsync(user => user.Email == email, cancellationToken),
            cancellationToken: cancellationToken);
    }

    public async Task AddUserAsync(AddUserDto dto, CancellationToken cancellationToken)
    {
        dto.ShouldBeValid(logger);
        
        await contextFactory.ExecuteWithCommitAsync(
            async context =>
                await context.Users.AddAsync(
                    new User
                    {
                        Email = dto.Email,
                        Password = dto.Password,
                        Role = dto.Role
                    }, cancellationToken),
            cancellationToken: cancellationToken);
    }
}