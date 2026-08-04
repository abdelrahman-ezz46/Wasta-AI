using Microsoft.EntityFrameworkCore;
using Wasta.SupportChat.Domain;

namespace Wasta.SupportChat.Data;

public class SupportChatDbContext : DbContext
{
    public SupportChatDbContext(DbContextOptions<SupportChatDbContext> options) : base(options)
    {
    }

    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var session = modelBuilder.Entity<ChatSession>();
        session.ToTable("ChatSessions");
        session.HasKey(s => s.Id);
        session.HasIndex(s => s.PublicId).IsUnique();
        session.HasIndex(s => s.StudentId);
        session.HasIndex(s => s.VisitorId);
        session.Property(s => s.VisitorId).HasMaxLength(64);
        session.HasMany(s => s.Messages)
            .WithOne(m => m.Session)
            .HasForeignKey(m => m.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        var message = modelBuilder.Entity<ChatMessage>();
        message.ToTable("ChatMessages");
        message.HasKey(m => m.Id);
        message.Property(m => m.Role).HasConversion<string>().HasMaxLength(16).IsRequired();
        message.HasIndex(m => new { m.SessionId, m.CreatedAt });
        message.HasIndex(m => new { m.StudentId, m.CreatedAt });
    }
}
