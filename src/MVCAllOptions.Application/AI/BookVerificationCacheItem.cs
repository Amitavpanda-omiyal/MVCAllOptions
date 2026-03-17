using System;
using Volo.Abp.Caching;

namespace MVCAllOptions.AI;

/// <summary>
/// Cached result of verifying whether a newly-created book exists in the real world.
/// Stored for 7 days after book creation so the BookContextChatClient can include
/// verification data in every Chat Playground conversation.
/// </summary>
[CacheName("BookVerification")]
public class BookVerificationCacheItem
{
    public string BookName { get; set; } = string.Empty;

    /// <summary>True if the LLM confirmed the book exists.</summary>
    public bool ExistsInRealWorld { get; set; }

    /// <summary>Publisher name if the book was found; otherwise empty.</summary>
    public string PublisherName { get; set; } = string.Empty;

    /// <summary>Publication date if the book was found; otherwise empty.</summary>
    public string PublicationDate { get; set; } = string.Empty;

    /// <summary>Comma-separated list of real alternative book suggestions if the book was not found.</summary>
    public string SuggestedAlternatives { get; set; } = string.Empty;

    /// <summary>Short human-readable summary of the verification result.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the verification was completed.</summary>
    public DateTime VerifiedAt { get; set; }
}
