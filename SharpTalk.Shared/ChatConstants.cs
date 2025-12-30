namespace SharpTalk.Shared;

/// <summary>
/// Constants used throughout the chat application
/// </summary>
public static class ChatConstants
{
    /// <summary>
    /// Timeout for typing indicator before it's cleared
    /// </summary>
    public static readonly TimeSpan TypingIndicatorTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Default number of messages to retrieve per channel
    /// </summary>
    public const int DefaultMessageLimit = 50;

    /// <summary>
    /// Maximum number of messages to retrieve per channel
    /// </summary>
    public const int MaxMessageLimit = 200;

    /// <summary>
    /// Maximum length for workspace names
    /// </summary>
    public const int MaxWorkspaceNameLength = 100;

    /// <summary>
    /// Minimum length for workspace names
    /// </summary>
    public const int MinWorkspaceNameLength = 1;

    /// <summary>
    /// Maximum length for channel names
    /// </summary>
    public const int MaxChannelNameLength = 100;

    /// <summary>
    /// Maximum length for descriptions
    /// </summary>
    public const int MaxDescriptionLength = 500;

    /// <summary>
    /// Cache duration for user workspaces in seconds
    /// </summary>
    public const int WorkspaceCacheDurationSeconds = 60;

    /// <summary>
    /// Cache duration for channel lists in seconds
    /// </summary>
    public const int ChannelCacheDurationSeconds = 60;

    /// <summary>
    /// Typing indicator fade-out animation duration in milliseconds
    /// </summary>
    public const int TypingFadeOutDurationMs = 300;

    /// <summary>
    /// Minimum number of members required in a group DM (including creator)
    /// </summary>
    public const int GroupDMMinMembers = 3;

    /// <summary>
    /// Maximum number of members allowed in a group DM (including creator)
    /// </summary>
    public const int GroupDMMaxMembers = 9;

    /// <summary>
    /// Maximum number of additional members that can be added (excluding creator)
    /// </summary>
    public const int GroupDMMaxAdditionalMembers = 8;
}

/// <summary>
/// User status constants
/// </summary>
public static class UserStatus
{
    public const string Online = "Online";
    public const string Away = "Away";
    public const string Offline = "Offline";

    /// <summary>
    /// Gets all valid user statuses
    /// </summary>
    public static readonly string[] ValidStatuses = { Online, Away, Offline };
}
