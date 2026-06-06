using System;
using System.Collections.Generic;

namespace MiniPainterHub.Server.Services.Interfaces
{
    public interface ISiteActivityTracker
    {
        void RecordRequest(SiteActivityRequest request);
        void RecordSignalRConnected();
        void RecordSignalRDisconnected();
        SiteActivitySnapshot GetSnapshot(TimeSpan window);
    }

    public sealed record SiteActivityRequest(
        DateTime TimestampUtc,
        string Path,
        string Method,
        int StatusCode,
        double DurationMs,
        string? SessionKey,
        bool IsApi,
        bool IsPageView,
        bool IsImageUpload);

    public sealed record SiteActivitySnapshot(
        long ActiveSessions,
        long PageViews,
        long ApiRequests,
        long ApiSuccessCount,
        long ApiErrorCount,
        double AverageResponseTimeMs,
        int SignalRConnections,
        long ImageUploadRequests,
        IReadOnlyList<SiteActivityPoint> Activity);

    public sealed record SiteActivityPoint(
        DateTime TimestampUtc,
        long PageViews,
        long ApiRequests,
        long ApiErrors);
}
