namespace ParcelTrack.Shared.Common;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount);
