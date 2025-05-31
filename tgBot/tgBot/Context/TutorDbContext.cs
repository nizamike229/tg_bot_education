using Microsoft.EntityFrameworkCore;
using tgBot.Entities;

namespace tgBot.Context;

public partial class TutorDbContext : DbContext
{
    public TutorDbContext()
    {
    }

    public TutorDbContext(DbContextOptions<TutorDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Lesson> Lessons { get; set; }

    public virtual DbSet<Student> Students { get; set; }

    public virtual DbSet<Teacher> Teachers { get; set; }

    public virtual DbSet<AvailableSlot> AvailableSlots { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlite("Data Source=/Users/nizami/Desktop/botSharp/main.sqlite");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Lesson>(entity =>
        {
            entity.Property(e => e.EndTime).HasColumnType("DATETIME");
            entity.Property(e => e.IsConfirmed)
                .HasDefaultValue(false)
                .HasColumnType("BOOLEAN");
            entity.Property(e => e.StartTime).HasColumnType("DATETIME");

            entity.HasOne(d => d.Student).WithMany(p => p.Lessons)
                .HasForeignKey(d => d.StudentId)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.Teacher).WithMany(p => p.Lessons)
                .HasForeignKey(d => d.TeacherId)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Student>(entity =>
        {
            entity.HasIndex(e => e.TelegramId, "IX_Students_TelegramId").IsUnique();
        });

        modelBuilder.Entity<AvailableSlot>(entity =>
        {
            entity.Property(e => e.StartTime).HasColumnType("DATETIME");
            entity.Property(e => e.EndTime).HasColumnType("DATETIME");
            entity.Property(e => e.IsBooked).HasDefaultValue(false);
            entity.HasOne(d => d.Teacher)
                .WithMany()
                .HasForeignKey(d => d.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
