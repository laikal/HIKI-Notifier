using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HikiNotifier.Models;

namespace HikiNotifier.Services
{
    public sealed class ProviderMetadata
    {
        public string Id { get; set; }
        // Stable persisted numeric code. Existing enum names are compatibility aliases;
        // a new DLL can use an unused numeric code without changing Contracts.
        public PlatformType Platform { get; set; }
        public string DisplayName { get; set; }
        public string UrlTemplate { get; set; }
        public PlatformCapabilities Capabilities { get; set; }
        public TimeSpan PollInterval { get; set; }
        public int Order { get; set; }
        public int PollOrder { get; set; }
        public bool PollIndependently { get; set; }
        public bool PersistAfterPoll { get; set; }
        public byte[] LogoPng { get; set; }
    }

    public interface IPlatformProvider : IDisposable
    {
        ProviderMetadata Metadata { get; }
        PlatformType Platform { get; }
        PlatformCapabilities Capabilities { get; }
        TimeSpan PollInterval { get; }
        bool CanHandleUrl(string url);
        void NormalizeStoredProfile(ChannelProfile profile);
        void PrepareForSave(ChannelProfile profile);
        ReplayableNotification GetReplayableNotification(ChannelProfile profile);
        Task<ChannelProfile> CreateAsync(string url, CancellationToken token);
        Task PollAsync(IList<ChannelProfile> channels, Action<ChannelProfile, LiveState, ContentEntry> updated, CancellationToken token);
    }

    public interface IContentCatalogProvider
    {
        Task<IReadOnlyList<ContentEntry>> GetRecentContentAsync(ChannelProfile channel, CancellationToken token);
    }

    public sealed class ContentEntry
    {
        public string ContentId { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public DateTimeOffset Published { get; set; }
        public DateTimeOffset Updated { get; set; }
    }

    public sealed class ReplayableNotification
    {
        public NotificationEventType Type { get; set; }
        public ContentEntry Content { get; set; }
    }

    public sealed class ProviderLookupException : Exception
    {
        public string LanguageKey { get; }
        public ProviderLookupException(string languageKey, Exception inner) : base(languageKey, inner)
        { LanguageKey = languageKey; }
    }
}
