using Microsoft.EntityFrameworkCore;

namespace ResumeAnalyzer.Web.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ResumeAnalysis> ResumeAnalyses => Set<ResumeAnalysis>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ResumeAnalysis>(e =>
        {
            e.ToTable("ResumeAnalyses");
            e.Property(a => a.FileName).HasMaxLength(260).IsRequired();
            e.Property(a => a.FileType).HasMaxLength(10).IsRequired();
            e.Property(a => a.CandidateName).HasMaxLength(150).IsRequired();
            e.Property(a => a.AiProvider).HasMaxLength(100).IsRequired();
            e.Property(a => a.ExtractedText).IsRequired();
            e.Property(a => a.ResultJson).IsRequired();
            e.HasIndex(a => a.CreatedAt);
            e.HasIndex(a => a.CandidateName);
        });
    }
}
