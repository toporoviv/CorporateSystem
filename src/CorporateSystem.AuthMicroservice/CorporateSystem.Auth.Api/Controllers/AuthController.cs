﻿using System.IdentityModel.Tokens.Jwt;
using CorporateSystem.Auth.Api.Dtos.Auth;
using CorporateSystem.Auth.Services.Exceptions;
using CorporateSystem.Auth.Services.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RefreshTokenResponse = CorporateSystem.Auth.Api.Dtos.Auth.RefreshTokenResponse;

namespace CorporateSystem.Auth.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/auth")]
public class AuthController(ILogger<AuthController> logger) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("sign-in")]
    public async Task<IActionResult> SignIn(
        [FromBody]AuthRequest request,
        [FromServices] IAuthService authService)
    {
        logger.LogInformation($"{nameof(SignIn)}: connectionId={HttpContext.Connection.Id}");
            
        var authResultDto = await authService
            .AuthenticateAsync(new AuthUserDto(request.Email, request.Password, HttpContext.Connection.RemoteIpAddress?.ToString()!));
        
        return Ok(new AuthResponse
        {
            Token = authResultDto.JwtToken
        });
    }
    
    [AllowAnonymous]
    [HttpPost("sign-up")]
    public async Task<IActionResult> SignUp(
        [FromBody] RegisterRequest request, 
        [FromServices] IRegistrationService registrationService)
    {
        logger.LogInformation($"{nameof(SignUp)}: connectionId={HttpContext.Connection.Id}");
            
        await registrationService
            .RegisterAsync(
                new RegisterUserDto(request.Email, request.Password, request.RepeatedPassword));

        return Ok();
    }

    [AllowAnonymous]
    [HttpPost("success-registration")]
    public async Task<IActionResult> SuccessRegistration(
        [FromBody] SuccessRegisterRequest request,
        [FromServices] IRegistrationService registrationService)
    {
        logger.LogInformation($"{nameof(SuccessRegistration)}: connectionId={HttpContext.Connection.Id}");
            
        if (request is null)
        {
            logger.LogError($"{nameof(SuccessRegistration)}: request=null");
            throw new InvalidRegistrationException("Некорректный запрос");
        }

        await registrationService
            .SuccessRegisterAsync(new SuccessRegisterUserDto(
                request.Email,
                request.Password,
                request.SuccessCode,
                request.FirstName,
                request.LastName,
                request.Gender));

        return Ok();
    }
    
    [AllowAnonymous]
    [HttpPost("validate-token")]
    public async Task<IActionResult> ValidateToken(
        [FromBody]TokenValidationRequest request, 
        [FromServices] ITokenService tokenService)
    {
        logger.LogInformation($"{nameof(ValidateToken)}: connectionId={HttpContext.Connection.Id}");
            
        var isValid = await tokenService.ValidateTokenAsync(request.Token);
            
        if (!isValid)
        {
            logger.LogError($"{nameof(ValidateToken)}: Token={request.Token} is not valid");
            return Unauthorized("Invalid token.");
        }

        var userInfo = GetUserInfoByToken(request.Token);
        return Ok(userInfo);
    }

    [AllowAnonymous]
    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromServices] ITokenService authService)
    {
        logger.LogInformation($"{nameof(RefreshToken)}: connectionId={HttpContext.Connection.Id}");

        var userId = GetUserInfoByToken(Request.Headers["Authorization"].ToString()).Id;
        
        var authResult = await authService.RefreshTokenAsync(
            new Services.Services.Interfaces.RefreshTokenRequest(userId, HttpContext.Connection.RemoteIpAddress?.ToString()!));
        
        return Ok(new RefreshTokenResponse
        {
            Token = authResult.Token
        });
    }
    
    [HttpPost("get-user-emails-by-id")]
    public async Task<IActionResult> GetUserEmailsById(
        [FromBody] GetUserEmailsByIdsRequest request,
        [FromServices] IUserService userService)
    {
        logger.LogInformation($"{nameof(GetUserEmailsById)}: connectionId={HttpContext.Connection.Id}");
        
        var users = await userService.GetUsersByExpressionAsync(user => request.UserIds.Contains(user.Id));
        
        var emails = users.Select(user => user.Email).ToArray();

        logger.LogInformation($"{nameof(GetUserEmailsById)}: emails={string.Join(",", emails)}");
            
        return Ok(new GetUserEmailsByIdsResponse
        {
            UserEmails = emails
        });
    }

    [HttpPost("get-user-ids-by-email")]
    public async Task<IActionResult> GetUserIdsByEmail(
        [FromBody] GetUserIdsByEmailsRequest request,
        [FromServices] IUserService userService)
    {
        logger.LogInformation($"{nameof(GetUserIdsByEmail)}: connectionId={HttpContext.Connection.Id}");
        
        var users = await userService.GetUsersByExpressionAsync(user => request.UserEmails.Contains(user.Email));
            
        var ids = users.Select(user => user.Id).ToArray();
            
        logger.LogInformation($"{nameof(GetUserIdsByEmail)}: ids={string.Join(",", ids)}");
            
        return Ok(new GetUserIdsByEmailsResponse
        {
            UserIds = ids
        });
    }
    
    private UserInfo GetUserInfoByToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var jwtToken = tokenHandler.ReadJwtToken(token);

        var userIdClaim = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "nameid");
        var roleClaim = jwtToken.Claims.FirstOrDefault(claim => claim.Type == "role");
        
        if (userIdClaim == null || roleClaim == null)
        {
            logger.LogError($"{nameof(GetUserInfoByToken)}: Token={token}, userIdClaim={userIdClaim?.Value} roleClaim={roleClaim?.Value}");
            throw new InvalidAuthorizationException("Некорректный токен.");
        }

        return new UserInfo
        {
            Id = int.Parse(userIdClaim.Value),
            Role = roleClaim.Value
        };
    }
}