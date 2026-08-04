using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Wasta.SupportChat.Data;

/// <summary>Design-time only, used by `dotnet ef migrations add`. The host app supplies
/// the real connection string at runtime via AddSupportChat(...).</summary>
public class SupportChatDbContextFactory : IDesignTimeDbContextFactory<SupportChatDbContext>
{
    public SupportChatDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<SupportChatDbContext>();
        builder.UseNpgsql("Host=localhost;Database=wasta_design_time;Username=postgres;Password=postgres");
        return new SupportChatDbContext(builder.Options);
    }
}
