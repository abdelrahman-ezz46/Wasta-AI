using System.Security.Claims;
using Wasta.SupportChat.Domain;

namespace Wasta.SupportChat.Tests.Fakes;

public class FakeCurrentStudentAccessor : ICurrentStudentAccessor
{
    private readonly int? _studentId;

    public FakeCurrentStudentAccessor(int? studentId) => _studentId = studentId;

    public int? GetStudentId(ClaimsPrincipal user) => _studentId;
}
