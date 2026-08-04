using Microsoft.EntityFrameworkCore;
using Wasta.CareerCoach.Domain;

namespace Wasta.CareerCoach.Data;

public class CoachDbContext : DbContext
{
    public CoachDbContext(DbContextOptions<CoachDbContext> options) : base(options)
    {
    }

    public DbSet<StudentCoachPlan> StudentCoachPlans => Set<StudentCoachPlan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var plan = modelBuilder.Entity<StudentCoachPlan>();

        plan.ToTable("StudentCoachPlans");
        plan.HasKey(p => p.Id);

        plan.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(12)
            .IsRequired();

        plan.Property(p => p.PromptVersion).HasMaxLength(10).IsRequired();
        plan.Property(p => p.ProviderUsed).HasMaxLength(30);
        plan.Property(p => p.LastError).HasMaxLength(500);

        plan.Property(p => p.WeeklyPlanJson).HasColumnName("WeeklyPlan").HasColumnType("jsonb");
        plan.Property(p => p.ProjectSkillsJson).HasColumnName("ProjectSkills").HasColumnType("jsonb");

        plan.HasIndex(p => p.AttemptId).IsUnique();
        plan.HasIndex(p => new { p.StudentId, p.Status });
    }
}
