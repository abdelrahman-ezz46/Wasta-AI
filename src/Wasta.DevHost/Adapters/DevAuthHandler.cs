using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Wasta.DevHost.Adapters;

/// <summary>
/// DEVELOPMENT ONLY. Fakes sign-in from request headers so the endpoints'
/// real authorization policies can be exercised without standing up an
/// identity provider:
///
///   X-Dev-Student-Id: 1     -> authenticated as student 1
///   X-Dev-Admin: true       -> also carries the admin role
///   (neither header)        -> anonymous visitor
///
/// This trusts client-supplied headers completely, which is exactly what
/// you must never ship. Program.cs refuses to start outside the Development
/// environment for that reason.
/// </summary>
public class DevAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "DevHeader";
    public const string StudentIdHeader = "X-Dev-Student-Id";
    public const string AdminHeader = "X-Dev-Admin";

    public DevAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var studentHeader = Request.Headers[StudentIdHeader].FirstOrDefault();
        var isAdmin = string.Equals(Request.Headers[AdminHeader].FirstOrDefault(), "true", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(studentHeader) && !isAdmin)
        {
            // Anonymous is a valid state here - the chat endpoints are public.
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>();

        if (int.TryParse(studentHeader, out var studentId))
        {
            claims.Add(new Claim(DevCurrentStudentAccessor.StudentIdClaim, studentId.ToString()));
            claims.Add(new Claim(ClaimTypes.NameIdentifier, $"student-{studentId}"));
            claims.Add(new Claim(ClaimTypes.Role, "Student"));
        }

        if (isAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            if (claims.All(c => c.Type != ClaimTypes.NameIdentifier))
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, "dev-admin"));
            }
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
