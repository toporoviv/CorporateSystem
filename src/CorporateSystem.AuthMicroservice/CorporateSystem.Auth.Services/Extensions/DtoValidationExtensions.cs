using System.Net;
using CorporateSystem.Auth.Domain.Exceptions;
using CorporateSystem.Auth.Services.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace CorporateSystem.Auth.Services.Extensions;

internal static class DtoValidationExtensions
{
    public static void ShouldBeValid<T>(this SuccessRegisterUserDto dto, ILogger<T> logger)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(dto.Email);
            ArgumentException.ThrowIfNullOrWhiteSpace(dto.Password);
        }
        catch (ArgumentException e)
        {
            logger.LogError(e.Message);
            throw new ExceptionWithStatusCode("Что-то пошло не так", HttpStatusCode.BadRequest, e);
        }
    }
}