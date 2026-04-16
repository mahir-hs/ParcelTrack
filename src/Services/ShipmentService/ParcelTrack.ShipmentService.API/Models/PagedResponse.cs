namespace ParcelTrack.ShipmentService.API.Models;

/// <summary>
/// Paginated list response wrapper — consistent shape across all list endpoints.
/// </summary>
public sealed record PagedResponse<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}