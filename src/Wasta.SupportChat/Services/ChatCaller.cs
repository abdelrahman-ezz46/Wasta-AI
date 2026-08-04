using Wasta.SupportChat.Domain;

namespace Wasta.SupportChat.Services;

/// <summary>
/// Who is making this request, as resolved by the endpoint layer from auth
/// plus the visitor-id header. Every session-scoped operation takes one of
/// these so authorization is a required argument, not something a caller
/// can forget to check.
/// </summary>
public sealed record ChatCaller(int? StudentId, string? VisitorId)
{
    /// <summary>
    /// A session GUID alone is NOT proof of ownership - it lives in
    /// localStorage and travels in URL paths, so it leaks into access logs,
    /// proxy logs, and browser history. Ownership is therefore re-derived
    /// from the caller's identity on every request.
    ///
    /// Logged-in sessions bind to StudentId - this is the one that matters,
    /// because those sessions carry cross-visit memory. Anonymous sessions
    /// bind to the visitor id: weaker (it sits beside the session id in the
    /// same localStorage), but it means a leaked URL alone is not enough,
    /// and anonymous sessions never carry history across visits anyway.
    ///
    /// Fails closed: an anonymous session with no stored visitor id is
    /// unreachable rather than open to everyone.
    /// </summary>
    public bool CanAccess(ChatSession session)
    {
        if (session.StudentId is { } ownerStudentId)
        {
            return StudentId == ownerStudentId;
        }

        return !string.IsNullOrEmpty(session.VisitorId)
            && string.Equals(session.VisitorId, VisitorId, StringComparison.Ordinal);
    }
}
