using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace SharpTalk.Api.Configuration;

/// <summary>
/// Validates application configuration on startup
/// </summary>
public static class ConfigurationValidator
{
    public static void ValidateConfiguration(IConfiguration configuration)
    {
        var errors = new List<string>();

        // Validate JWT settings
        var jwtSettings = configuration.GetSection("JwtSettings");
        if (string.IsNullOrEmpty(jwtSettings["Secret"]))
        {
            errors.Add("JwtSettings:Secret is required");
        }
        if (string.IsNullOrEmpty(jwtSettings["Issuer"]))
        {
            errors.Add("JwtSettings:Issuer is required");
        }
        if (string.IsNullOrEmpty(jwtSettings["Audience"]))
        {
            errors.Add("JwtSettings:Audience is required");
        }

        // Validate connection strings
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            errors.Add("ConnectionStrings:DefaultConnection is required");
        }

        // Validate Redis connection string (optional but warn if missing)
        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (string.IsNullOrEmpty(redisConnectionString))
        {
            Console.WriteLine("Warning: ConnectionStrings:Redis is not configured. Using default: localhost:6379");
        }

        // Validate file upload settings
        var maxFileSize = configuration.GetValue<int>("FileUploadSettings:MaxFileSizeMB", 10);
        if (maxFileSize <= 0 || maxFileSize > 100)
        {
            errors.Add("FileUploadSettings:MaxFileSizeMB must be between 1 and 100");
        }

        var allowedExtensions = configuration.GetSection("FileUploadSettings:AllowedExtensions").Get<string[]>();
        if (allowedExtensions == null || allowedExtensions.Length == 0)
        {
            errors.Add("FileUploadSettings:AllowedExtensions is required");
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Configuration validation failed:{Environment.NewLine}" +
                string.Join(Environment.NewLine, errors.Select(e => $"  - {e}")));
        }
    }
}
