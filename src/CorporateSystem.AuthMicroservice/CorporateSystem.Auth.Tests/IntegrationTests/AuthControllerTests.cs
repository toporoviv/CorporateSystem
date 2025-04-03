using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CorporateSystem.Auth.Api.Dtos.Auth;
using CorporateSystem.Auth.Domain.Enums;
using CorporateSystem.Auth.Infrastructure;
using CorporateSystem.Auth.Services.Options;
using CorporateSystem.Auth.Services.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Moq;
using Xunit.Abstractions;

namespace CorporateSystem.Auth.Tests.IntegrationTests;

public class AuthControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly Mock<IAuthService> _mockAuthService;

    public AuthControllerTests(WebApplicationFactory<Program> factory, ITestOutputHelper testOutputHelper)
    {
        _factory = factory;
        _testOutputHelper = testOutputHelper;
        _mockAuthService = new Mock<IAuthService>();
    }

    [Fact]
    public async Task ValidateToken_ValidTokenObtainedFromSignIn_ReturnsUserInfo()
    {
        // Arrange
        var testSecretKey = "a-very-long-and-secure-test-secret-key";
        
        var authRequest = new AuthRequest
        {
            Email = "test@example.com",
            Password = "password123"
        };

        var token = GenerateValidJwtToken(testSecretKey, 1, authRequest.Email);

        _mockAuthService.Setup(service => service.AuthenticateAsync(
                It.IsAny<AuthUserDto>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(token);

        _mockAuthService
            .Setup(x => x.ValidateToken(It.Is<string>(str => str == token)))
            .Returns(true);
        
        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(_mockAuthService.Object);
            });
        }).CreateClient();
        
        var signInResponse = await client.PostAsJsonAsync("/api/auth/sign-in", authRequest);
        signInResponse.EnsureSuccessStatusCode();

        var authResponse = await signInResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResponse);
        Assert.NotNull(authResponse.Token);
        Assert.Equal(token, authResponse.Token);
        
        var validateTokenRequest = new TokenValidationRequest
        {
            Token = authResponse.Token
        };

        // Act
        var validateTokenResponse = await client.PostAsJsonAsync("/api/auth/validate-token", validateTokenRequest);

        _testOutputHelper.WriteLine(await validateTokenResponse.Content.ReadAsStringAsync());
        // Assert
        validateTokenResponse.EnsureSuccessStatusCode();
        var userInfo = await validateTokenResponse.Content.ReadFromJsonAsync<UserInfo>();

        Assert.NotNull(userInfo);
        
        _mockAuthService.Verify(service => service.ValidateToken(validateTokenRequest.Token), Times.Once);
    }
    
    private string GenerateValidJwtToken(string secretKey, int id, string email, Role role = Role.User)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(secretKey);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, id.ToString()),
                new Claim(ClaimTypes.Name, email),
                new Claim(ClaimTypes.Role, role.ToString())
            ]),
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    [Fact]
    public async Task ValidateToken_InvalidToken_ReturnsUnauthorized()
    {
        // Arrange
        var token = "invalid-token";

        _mockAuthService.Setup(service => service.ValidateToken(token))
            .Returns(false);

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services => { services.AddSingleton(_mockAuthService.Object); });
        }).CreateClient();

        var request = new TokenValidationRequest { Token = token };

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/validate-token", request);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.Contains("Invalid token.", responseBody);

        _mockAuthService.Verify(service => service.ValidateToken(token), Times.Once);
    }

    [Fact]
    public async Task ValidateToken_ExceptionDuringValidation_ReturnsUnauthorized()
    {
        // Arrange
        var token = "error-token";

        _mockAuthService.Setup(service => service.ValidateToken(token))
            .Throws(new InvalidOperationException("Validation error"));

        var client = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services => { services.AddSingleton(_mockAuthService.Object); });
        }).CreateClient();

        var request = new TokenValidationRequest { Token = token };

        // Act
        var response = await client.PostAsJsonAsync("/api/auth/validate-token", request);

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);

        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.Contains("Token validation failed.", responseBody);

        _mockAuthService.Verify(service => service.ValidateToken(token), Times.Once);
    }
}