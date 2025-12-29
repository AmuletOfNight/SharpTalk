using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using SharpTalk.Api.Controllers;
using SharpTalk.Api.Tests.Helpers;
using SharpTalk.Shared.DTOs;

namespace SharpTalk.Api.Tests.Controllers;

public class AuthControllerTests : IDisposable
{
    private readonly TestDbContextHelper _dbHelper;
    private readonly AuthController _controller;
    private readonly Mock<IConfiguration> _configurationMock;

    public AuthControllerTests()
    {
        _dbHelper = TestDbContextHelper.Create();
        _configurationMock = new Mock<IConfiguration>();
        
        // Setup JWT configuration
        var jwtSection = new Mock<IConfigurationSection>();
        jwtSection.Setup(x => x["Secret"]).Returns("ThisIsAVeryLongSecretKeyForTestingPurposes123456789");
        jwtSection.Setup(x => x["Issuer"]).Returns("TestIssuer");
        jwtSection.Setup(x => x["Audience"]).Returns("TestAudience");
        _configurationMock.Setup(x => x.GetSection("JwtSettings")).Returns(jwtSection.Object);

        _controller = new AuthController(_dbHelper.Context, _configurationMock.Object);
    }

    public void Dispose()
    {
        _dbHelper.Dispose();
    }

    #region Register Tests

    [Fact]
    public async Task Register_WithValidData_ReturnsAuthResponseWithToken()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "newuser",
            Email = "newuser@example.com",
            Password = "SecurePassword123"
        };

        // Act
        var result = await _controller.Register(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var authResponse = okResult.Value.Should().BeOfType<AuthResponse>().Subject;
        
        authResponse.Token.Should().NotBeNullOrEmpty();
        authResponse.Username.Should().Be("newuser");
        authResponse.Email.Should().Be("newuser@example.com");
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsBadRequest()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        
        var request = new RegisterRequest
        {
            Username = "differentuser",
            Email = "test1@example.com", // Already exists
            Password = "SecurePassword123"
        };

        // Act
        var result = await _controller.Register(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Register_WithDuplicateUsername_ReturnsBadRequest()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        
        var request = new RegisterRequest
        {
            Username = "testuser1", // Already exists
            Email = "different@example.com",
            Password = "SecurePassword123"
        };

        // Act
        var result = await _controller.Register(request);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Register_NormalizesEmailToLowercase()
    {
        // Arrange
        var request = new RegisterRequest
        {
            Username = "newuser",
            Email = "NewUser@EXAMPLE.COM",
            Password = "SecurePassword123"
        };

        // Act
        var result = await _controller.Register(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var authResponse = okResult.Value.Should().BeOfType<AuthResponse>().Subject;
        
        authResponse.Email.Should().Be("newuser@example.com");
    }

    #endregion

    #region Login Tests

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsAuthResponse()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        
        var request = new LoginRequest
        {
            Email = "test1@example.com",
            Password = "password123"
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var authResponse = okResult.Value.Should().BeOfType<AuthResponse>().Subject;
        
        authResponse.Token.Should().NotBeNullOrEmpty();
        authResponse.Username.Should().Be("testuser1");
    }

    [Fact]
    public async Task Login_WithInvalidEmail_ReturnsUnauthorized()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        
        var request = new LoginRequest
        {
            Email = "nonexistent@example.com",
            Password = "password123"
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        
        var request = new LoginRequest
        {
            Email = "test1@example.com",
            Password = "wrongpassword"
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_IsCaseInsensitiveForEmail()
    {
        // Arrange
        await _dbHelper.SeedTestDataAsync();
        
        var request = new LoginRequest
        {
            Email = "TEST1@EXAMPLE.COM", // Different case
            Password = "password123"
        };

        // Act
        var result = await _controller.Login(request);

        // Assert
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var authResponse = okResult.Value.Should().BeOfType<AuthResponse>().Subject;
        
        authResponse.Username.Should().Be("testuser1");
    }

    #endregion
}
