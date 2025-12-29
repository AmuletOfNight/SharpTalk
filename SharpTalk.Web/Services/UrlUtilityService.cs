using System.Net.Http;

namespace SharpTalk.Web.Services;

/// <summary>
/// Service for constructing URLs using the configured API base URL
/// </summary>
public class UrlUtilityService
{
    private readonly HttpClient _httpClient;

    public UrlUtilityService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>
    /// Constructs a full URL for an avatar image
    /// </summary>
    /// <param name="avatarUrl">The avatar URL (can be relative, absolute, or data URI)</param>
    /// <param name="size">Optional size suffix: 's' (32px), 'm' (64px), 'l' (128px)</param>
    /// <returns>The full URL to use for the avatar</returns>
    public string GetAvatarUrl(string? avatarUrl, string? size = null)
    {
        if (string.IsNullOrEmpty(avatarUrl))
        {
            return "/images/default-avatar.svg";
        }

        // Return as-is if it's already a data URI or absolute URL
        if (avatarUrl.StartsWith("data:") || avatarUrl.StartsWith("http://") || avatarUrl.StartsWith("https://"))
        {
            return avatarUrl;
        }

        // Apply size suffix if provided and if it's a internal upload
        var finalUrl = avatarUrl;
        if (!string.IsNullOrEmpty(size) && avatarUrl.Contains("/uploads/avatars/") && avatarUrl.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
        {
            var extension = Path.GetExtension(avatarUrl);
            var pathWithoutExtension = avatarUrl.Substring(0, avatarUrl.Length - extension.Length);

            // If the URL already has a size suffix (like _m), we might need to replace it
            if (pathWithoutExtension.Length > 2 && pathWithoutExtension[pathWithoutExtension.Length - 2] == '_')
            {
                pathWithoutExtension = pathWithoutExtension.Substring(0, pathWithoutExtension.Length - 2);
            }

            finalUrl = $"{pathWithoutExtension}_{size}{extension}";
        }

        // Construct full URL using the configured API base URL
        var baseUrl = _httpClient.BaseAddress?.ToString() ?? "https://localhost:7107";
        return $"{baseUrl.TrimEnd('/')}{finalUrl}";
    }

    /// <summary>
    /// Constructs a full URL for a file attachment
    /// </summary>
    /// <param name="fileUrl">The file URL (can be relative or absolute)</param>
    /// <returns>The full URL to use for the file</returns>
    public string GetFileUrl(string? fileUrl)
    {
        if (string.IsNullOrEmpty(fileUrl))
        {
            return string.Empty;
        }

        // Return as-is if it's already an absolute URL
        if (fileUrl.StartsWith("http://") || fileUrl.StartsWith("https://"))
        {
            return fileUrl;
        }

        // Construct full URL using the configured API base URL
        var baseUrl = _httpClient.BaseAddress?.ToString() ?? "https://localhost:7107";
        return $"{baseUrl.TrimEnd('/')}{fileUrl}";
    }
}
