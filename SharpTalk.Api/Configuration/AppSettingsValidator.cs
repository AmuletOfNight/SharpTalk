using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace SharpTalk.Api.Configuration;

/// <summary>
/// Validates application settings on startup
/// </summary>
public class AppSettingsValidator
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AppSettingsValidator> _logger;

    public AppSettingsValidator(IConfiguration configuration, ILogger<AppSettingsValidator> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public void Validate()
    {
        ValidateJwtSettings();
        ValidateDatabaseSettings();
        ValidateFileUploadSettings();
        ValidateCorsSettings();
    }

    private void ValidateJwtSettings()
    {
        var jwtKey = _configuration["Jwt:Key"];
        var jwtIssuer = _configuration["Jwt:Issuer"];
        var jwtAudience = _configuration["Jwt:Audience"];
        var jwtExpiryMinutes = _configuration["Jwt:ExpiryMinutes"];

        if (string.IsNullOrWhiteSpace(jwtKey))
        {
            throw new InvalidOperationException("JWT Key is not configured. Set 'Jwt:Key' in appsettings.json.");
        }

        if (jwtKey.Length < 32)
        {
            _logger.LogWarning("JWT Key is less than 32 characters. Consider using a longer, more secure key.");
        }

        if (string.IsNullOrWhiteSpace(jwtIssuer))
        {
            throw new InvalidOperationException("JWT Issuer is not configured. Set 'Jwt:Issuer' in appsettings.json.");
        }

        if (string.IsNullOrWhiteSpace(jwtAudience))
        {
            throw new InvalidOperationException("JWT Audience is not configured. Set 'Jwt:Audience' in appsettings.json.");
        }

        if (!int.TryParse(jwtExpiryMinutes, out var expiryMinutes) || expiryMinutes <= 0)
        {
            throw new InvalidOperationException("JWT ExpiryMinutes must be a positive integer.");
        }

        if (expiryMinutes > 1440) // 24 hours
        {
            _logger.LogWarning("JWT ExpiryMinutes is greater than 24 hours. Consider using a shorter expiry for security.");
        }

        _logger.LogInformation("JWT settings validated successfully.");
    }

    private void ValidateDatabaseSettings()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Database connection string is not configured. Set 'ConnectionStrings:DefaultConnection' in appsettings.json.");
        }

        _logger.LogInformation("Database settings validated successfully.");
    }

    private void ValidateFileUploadSettings()
    {
        var maxFileSize = _configuration["FileUpload:MaxFileSizeMB"];
        var allowedExtensions = _configuration["FileUpload:AllowedExtensions"];

        if (string.IsNullOrWhiteSpace(maxFileSize))
        {
            throw new InvalidOperationException("FileUpload MaxFileSizeMB is not configured.");
        }

        if (!int.TryParse(maxFileSize, out var maxSize) || maxSize <= 0)
        {
            throw new InvalidOperationException("FileUpload MaxFileSizeMB must be a positive integer.");
        }

        if (maxSize > 100)
        {
            _logger.LogWarning("FileUpload MaxFileSizeMB is greater than 100MB. Consider using a smaller limit for security.");
        }

        if (string.IsNullOrWhiteSpace(allowedExtensions))
        {
            throw new InvalidOperationException("FileUpload AllowedExtensions is not configured.");
        }

        _logger.LogInformation("File upload settings validated successfully.");
    }

    private void ValidateCorsSettings()
    {
        var allowedOrigins = _configuration["Cors:AllowedOrigins"];

        if (string.IsNullOrWhiteSpace(allowedOrigins))
        {
            _logger.LogWarning("CORS AllowedOrigins is not configured. CORS may not work correctly.");
        }

        _logger.LogInformation("CORS settings validated successfully.");
    }
}
