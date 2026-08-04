using System.Security.Claims;

namespace Wasta.SupportChat.Domain;

/// <summary>
/// Resolves the caller's student id when they're logged in, or null for an
/// anonymous website visitor - chat is public, login is optional. Implement
/// against your real auth and register it in DI. (Deliberately a separate,
/// tiny interface from Wasta.CareerCoach's - the two modules don't share a
/// real dependency, and duplicating two lines beats coupling them.)
/// </summary>
public interface ICurrentStudentAccessor
{
    int? GetStudentId(ClaimsPrincipal user);
}
