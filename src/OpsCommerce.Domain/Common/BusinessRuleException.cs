namespace OpsCommerce.Domain.Common;

/// <summary>
/// Thrown when a domain rule is violated (for example: an invalid status
/// transition, or reserving more stock than is available).
/// The API layer maps this exception to HTTP 422 with a stable error code,
/// so clients can react to the <see cref="Code"/> instead of parsing text.
/// </summary>
public sealed class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message) { }

    public BusinessRuleException(string message, string code) : base(message)
    {
        Code = code;
    }

    public string? Code { get; }
}
