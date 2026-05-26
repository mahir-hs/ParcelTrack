namespace ParcelTrack.Shared.Common;

public sealed record ApiErrorResponse(string Type, string Message, string TraceId);
