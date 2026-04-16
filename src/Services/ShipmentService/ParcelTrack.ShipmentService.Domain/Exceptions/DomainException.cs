namespace ParcelTrack.ShipmentService.Domain.Exceptions;

/// <summary>
/// Base class for all domain-layer exceptions in the Shipment Service.
/// Domain exceptions represent business rule violations — not infrastructure failures.
/// They are caught at the API layer and translated into appropriate HTTP responses.
/// </summary>
public abstract class DomainException : Exception
{
    /// <summary>
    /// A short, machine-readable code identifying the error type.
    /// Used by API middleware to produce consistent error responses.
    /// </summary>
    public string ErrorCode { get; }

    protected DomainException(string message, string errorCode)
        : base(message)
    {
        ErrorCode = errorCode ?? throw new ArgumentNullException(nameof(errorCode));
    }

    protected DomainException(string message, string errorCode, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode ?? throw new ArgumentNullException(nameof(errorCode));
    }
}