using System.IdentityModel.Tokens.Jwt;
using CorporateSystem.Auth.Api.Dtos.Auth;
using CorporateSystem.Auth.Domain.Exceptions;
using CorporateSystem.Auth.Services.Services.Interfaces;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace CorporateSystem.Auth.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(ILogger<AuthController> logger) : ControllerBase
{
    [HttpPost("sign-in")]
    public async Task<IActionResult> SignIn(
        [FromBody]AuthRequest request,
        [FromServices] IAuthService authService)
    {
        try
        {
            var token = await authService
                .AuthenticateAsync(new AuthUserDto(request.Email, request.Password));
            
            return Ok(new AuthResponse
            {
                Token = token
            });
        }
        catch (ExceptionWithStatusCode e)
        {
            logger.LogError(e.Message);
            return StatusCode((int)e.StatusCode, e.Message);
        }
        catch (Exception e)
        {
            logger.LogError(e.Message);
            return StatusCode(StatusCodes.Status400BadRequest, "Что-то пошло не так");
        }
    }
    
    [HttpPost("sign-up")]
    public async Task<IActionResult> SignUp(
        [FromBody] RegisterRequest request, 
        [FromServices] IRegistrationService registrationService)
    {
        try
        {
            await registrationService
                .RegisterAsync(
                    new RegisterUserDto(request.Email, request.Password, request.RepeatedPassword));

            return Ok();
        }
        catch (ExceptionWithStatusCode e)
        {
            logger.LogError(e.Message);
            return StatusCode((int)e.StatusCode, e.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message);
            return StatusCode(StatusCodes.Status400BadRequest, "Что-то пошло не так");
        }
    }

    [HttpPost("success-registration")]
    public async Task<IActionResult> SuccessRegistration(
        [FromBody] SuccessRegisterRequest request,
        [FromServices] IRegistrationService registrationService)
    {
        try
        {
            if (request is null)
                throw new Exception("Некорректный запрос");

            await registrationService
                .SuccessRegisterAsync(new SuccessRegisterUserDto(request.Email, request.Password, request.SuccessCode));

            return Ok();
        }
        catch (ExceptionWithStatusCode e)
        {
            logger.LogError(e.Message);
            return StatusCode((int)e.StatusCode, e.Message);
        }
        catch (Exception e)
        {
            logger.LogError(e.Message);
            return StatusCode(StatusCodes.Status400BadRequest, "Что-то пошло не так");
        }
    }
    
    [HttpPost("validate-token")]
    public IActionResult ValidateToken(
        [FromBody]TokenValidationRequest request, 
        [FromServices] IAuthService authService)
    {
        try
        {
            var isValid = authService.ValidateToken(request.Token);
            
            if (!isValid)
            {
                return Unauthorized("Invalid token.");
            }

            var userInfo = GetUserInfoByToken(request.Token);
            return Ok(userInfo);
        }
        catch (ExceptionWithStatusCode e)
        {
            logger.LogError(e.Message);
            return StatusCode((int)e.StatusCode, e.Message);
        }
        catch (Exception e)
        {
            logger.LogError(e.Message);
            return StatusCode(StatusCodes.Status401Unauthorized, "Token validation failed.");
        }
    }

    [HttpPost("get-user-emails-by-id")]
    public async Task<IActionResult> GetUserEmailsById(
        [FromBody] GetUserEmailsByIdsRequest request,
        [FromServices] IUserService userService)
    {
        try
        {
            var users = await userService.GetUsersByIdsAsync(request.UserIds);
            var emails = users.Select(user => user.Email).ToArray();

            return Ok(new GetUserEmailsByIdsResponse
            {
                UserEmails = emails
            });
        }
        catch (Exception e)
        {
            logger.LogError(e.Message);
            return StatusCode(StatusCodes.Status400BadRequest, "Что-то пошло не так");
        }
    }

    [HttpPost("get-user-ids-by-email")]
    public async Task<IActionResult> GetUserIdsByEmail(
        [FromBody] GetUserIdsByEmailsRequest request,
        [FromServices] IUserService userService)
    {
        try
        {
            var users = await userService.GetUsersByEmailsAsync(request.UserEmails);
            var ids = users.Select(user => user.Id).ToArray();

            return Ok(new GetUserIdsByEmailsResponse
            {
                UserIds = ids
            });
        }
        catch (Exception e)
        {
            logger.LogError(e.Message);
            return StatusCode(StatusCodes.Status400BadRequest, "Что-то пошло не так");
        }
    }
    
    private UserInfo GetUserInfoByToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var jwtToken = tokenHandler.ReadJwtToken(token);

        var userIdClaim = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "nameid");
        var roleClaim = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "role");
        
        if (userIdClaim == null || roleClaim == null)
        {
            throw new InvalidOperationException("Invalid token claims.");
        }

        return new UserInfo
        {
            Id = int.Parse(userIdClaim.Value),
            Role = roleClaim.Value
        };
    }
}