using LupiraHealthApi.Application.Telemetry;
using LupiraHealthApi.Auth;

namespace LupiraHealthApi.Handlers;

/// <summary>The owner-facing location read + tracking-control surface (OIDC-authed). principal = the caller; every query
/// is scoped to the caller's own data.</summary>
public sealed class LocationQueryHandler(CurrentUser user, LocationQueryService q, TripVisitService trips, TrackingStateService tracking)
{
    public async Task<IResult> CurrentAsync(Guid? deviceId, CancellationToken ct)
    {
        var u = await user.GetAsync(ct);
        return TypedResults.Ok((await q.CurrentAsync(u.Id, deviceId, ct)).Value);
    }

    public async Task<IResult> TrackAsync(DateTimeOffset? from, DateTimeOffset? to, Guid? deviceId, CancellationToken ct)
    {
        var u = await user.GetAsync(ct);
        var (f, t) = Range(from, to);
        return TypedResults.Ok((await q.TrackAsync(u.Id, deviceId, f, t, ct)).Value);
    }

    public async Task<IResult> ThinnedAsync(DateTimeOffset? from, DateTimeOffset? to, int? bucketSeconds, Guid? deviceId, CancellationToken ct)
    {
        var u = await user.GetAsync(ct);
        var (f, t) = Range(from, to);
        var bucket = TimeSpan.FromSeconds(bucketSeconds is > 0 ? bucketSeconds.Value : 30);
        return TypedResults.Ok((await q.ThinnedTrackAsync(u.Id, deviceId, f, t, bucket, ct)).Value);
    }

    public async Task<IResult> StatsAsync(DateTimeOffset? from, DateTimeOffset? to, Guid? deviceId, CancellationToken ct)
    {
        var u = await user.GetAsync(ct);
        var (f, t) = Range(from, to);
        return TypedResults.Ok((await q.StatsAsync(u.Id, deviceId, f, t, ct)).Value);
    }

    public async Task<IResult> BboxAsync(double minLat, double maxLat, double minLon, double maxLon, DateTimeOffset? from, DateTimeOffset? to, Guid? deviceId, CancellationToken ct)
    {
        var u = await user.GetAsync(ct);
        var (f, t) = Range(from, to);
        return TypedResults.Ok((await q.BoundingBoxAsync(u.Id, deviceId, f, t, (minLat, maxLat, minLon, maxLon), ct)).Value);
    }

    public async Task<IResult> AtAsync(DateTimeOffset ts, CancellationToken ct)
    {
        var u = await user.GetAsync(ct);
        return TypedResults.Ok((await q.PlaceLabelAtAsync(u.Id, ts, ct)).Value);
    }

    public async Task<IResult> VisitsAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct)
    {
        var u = await user.GetAsync(ct);
        var (f, t) = Range(from, to);
        return TypedResults.Ok((await trips.VisitsAsync(u.Id, f, t, ct)).Value);
    }

    public async Task<IResult> TripsAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct)
    {
        var u = await user.GetAsync(ct);
        var (f, t) = Range(from, to);
        return TypedResults.Ok((await trips.TripsAsync(u.Id, f, t, ct)).Value);
    }

    public async Task<IResult> SummaryAsync(DateOnly date, CancellationToken ct)
    {
        var u = await user.GetAsync(ct);
        return TypedResults.Ok((await trips.SummaryAsync(u.Id, date, ct)).Value);
    }

    public async Task<IResult> PurgeAsync(DateTimeOffset? from, DateTimeOffset? to, Guid? deviceId, CancellationToken ct)
    {
        var u = await user.GetAsync(ct);
        var (f, t) = Range(from, to);
        await q.PurgeRangeAsync(u.Id, deviceId, f, t, ct);
        return TypedResults.NoContent();
    }

    public async Task<IResult> PauseAsync(Guid deviceId, Dtos.Location.PauseTrackingRequest? body, CancellationToken ct)
    {
        var u = await user.GetAsync(ct);
        await tracking.PauseAsync(u.Id, deviceId, body?.Reason, ct);
        return TypedResults.NoContent();
    }

    public async Task<IResult> ResumeAsync(Guid deviceId, CancellationToken ct)
    {
        var u = await user.GetAsync(ct);
        await tracking.ResumeAsync(u.Id, deviceId, ct);
        return TypedResults.NoContent();
    }

    public async Task<IResult> TrackingStateAsync(Guid deviceId, CancellationToken ct)
    {
        var u = await user.GetAsync(ct);
        return TypedResults.Ok((await tracking.StateAsync(u.Id, deviceId, ct)).Value);
    }

    private static (DateTimeOffset From, DateTimeOffset To) Range(DateTimeOffset? from, DateTimeOffset? to)
    {
        var t = to ?? DateTimeOffset.UtcNow;
        var f = from ?? t.AddDays(-1);
        return (f, t);
    }
}
