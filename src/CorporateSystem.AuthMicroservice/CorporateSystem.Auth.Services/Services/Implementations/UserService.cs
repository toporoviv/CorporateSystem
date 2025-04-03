using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text;
using CorporateSystem.Auth.Domain.Entities;
using CorporateSystem.Auth.Domain.Enums;
using CorporateSystem.Auth.Domain.Exceptions;
using CorporateSystem.Auth.Infrastructure.Repositories.Interfaces;
using CorporateSystem.Auth.Services.Extensions;
using CorporateSystem.Auth.Services.Options;
using CorporateSystem.Auth.Services.Services.GrpcServices;
using CorporateSystem.Auth.Services.Services.Interfaces;
using Grpc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

[assembly:InternalsVisibleTo("CorporateSystem.Auth.Tests")]
namespace CorporateSystem.Auth.Services.Services.Implementations;

internal class UserService(
    IContextFactory contextFactory,
    IPasswordHasher passwordHasher,
    IRegistrationCodesRepository registrationCodesRepository,
    IUserRepository userRepository,
    GrpcNotificationClient grpcNotificationClient,
    IOptions<JwtToken> jwtTokenOptions,
    IOptions<NotificationOptions> notificationOptions,
    ILogger<UserService> logger) : IAuthService, IRegistrationService
{
    private readonly JwtToken _jwtToken = jwtTokenOptions.Value;
    private readonly NotificationOptions _notificationOptions = notificationOptions.Value;
    
    public async Task<string> AuthenticateAsync(AuthUserDto dto, CancellationToken cancellationToken = default)
    {
        var user = await contextFactory.ExecuteWithoutCommitAsync(async context =>
        {
            return await context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email, cancellationToken);
        }, cancellationToken: cancellationToken);

        if (user == null || !passwordHasher.VerifyPassword(dto.Password, user.Password))
        {
            throw new ExceptionWithStatusCode("Неправильный логин или пароль", HttpStatusCode.Unauthorized);
        }
        
        return GenerateJwtToken(user);
    }

    public bool ValidateToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_jwtToken.JwtSecret);

            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            }, out var validatedToken);

            return validatedToken != null;
        }
        catch
        {
            return false;
        }
    }
    
    private string GenerateJwtToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(_jwtToken.JwtSecret);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Email),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            ]),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public async Task RegisterAsync(RegisterUserDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.Password != dto.RepeatedPassword)
        {
            throw new ExceptionWithStatusCode("Пароли не совпадают", HttpStatusCode.BadRequest);
        }

        int code;
        do
        {
            code = Random.Shared.Next(1_000_00, 1_000_000);
        } while (await registrationCodesRepository.GetAsync(code, cancellationToken) is not null);
        
        logger.LogInformation($"Created code: {code}");
        
        await registrationCodesRepository.CreateAsync(code, cancellationToken);
        
        logger.LogInformation($"Code {code} was written in redis");
        logger.LogInformation("Writing message to notification microservice");
        
        await grpcNotificationClient.SendMessageAsync(new SendMessageRequest
        {
            Title = "Регистрация в CorporateSystem",
            Message = $"Ваш код подтверждения: {code}",
            ReceiverEmails = { dto.Email },
            Token = _notificationOptions.Token
        }, cancellationToken);
        logger.LogInformation("Sending to notification is completed");
    }

    public async Task SuccessRegisterAsync(SuccessRegisterUserDto dto, CancellationToken cancellationToken = default)
    {
        dto.ShouldBeValid(logger);

        if (await registrationCodesRepository.GetAsync(dto.SuccessCode, cancellationToken) is null)
        {
            throw new ExceptionWithStatusCode("Неверный код", HttpStatusCode.BadRequest);
        }

        await registrationCodesRepository.DeleteAsync(dto.SuccessCode, cancellationToken);
        
        var addUserDto = new AddUserDto
        {
            Email = dto.Email,
            Password = passwordHasher.HashPassword(dto.Password),
            Role = Role.User
        };

        await userRepository.AddUserAsync(addUserDto, cancellationToken);
    }
}