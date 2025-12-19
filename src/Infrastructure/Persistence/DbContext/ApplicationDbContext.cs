using Microsoft.EntityFrameworkCore;
using ably_rest_apis.src.Domain.Entities;

namespace ably_rest_apis.src.Infrastructure.Persistence.DbContext
{
    /// <summary>
    /// Application database context for Entity Framework Core
    /// </summary>
    public class ApplicationDbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Session> Sessions => Set<Session>();
        public DbSet<SessionInstance> SessionInstances => Set<SessionInstance>();
        public DbSet<SessionParticipant> SessionParticipants => Set<SessionParticipant>();
        public DbSet<BreakRequest> BreakRequests => Set<BreakRequest>();
        public DbSet<Flag> Flags => Set<Flag>();
        public DbSet<Room> Rooms => Set<Room>();
        public DbSet<SessionEvent> SessionEvents => Set<SessionEvent>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.Role).HasConversion<string>().HasMaxLength(50);
            });

            // Session configuration
            modelBuilder.Entity<Session>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(300);
                entity.Property(e => e.Description).HasMaxLength(2000);
                entity.HasOne(e => e.CreatedBy)
                      .WithMany()
                      .HasForeignKey(e => e.CreatedById)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // SessionInstance configuration
            modelBuilder.Entity<SessionInstance>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
                entity.HasOne(e => e.Session)
                      .WithMany(s => s.Instances)
                      .HasForeignKey(e => e.SessionId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.StartedBy)
                      .WithMany()
                      .HasForeignKey(e => e.StartedById)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.EndedBy)
                      .WithMany()
                      .HasForeignKey(e => e.EndedById)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // SessionParticipant configuration
            modelBuilder.Entity<SessionParticipant>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Role).HasConversion<string>().HasMaxLength(50);
                entity.HasOne(e => e.SessionInstance)
                      .WithMany(s => s.Participants)
                      .HasForeignKey(e => e.SessionInstanceId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.User)
                      .WithMany(u => u.SessionParticipations)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.CurrentRoom)
                      .WithMany(r => r.Participants)
                      .HasForeignKey(e => e.CurrentRoomId)
                      .OnDelete(DeleteBehavior.SetNull);
                entity.HasIndex(e => new { e.SessionInstanceId, e.UserId }).IsUnique();
            });

            // BreakRequest configuration
            modelBuilder.Entity<BreakRequest>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
                entity.Property(e => e.Reason).HasMaxLength(500);
                entity.Property(e => e.DenialReason).HasMaxLength(500);
                entity.HasOne(e => e.SessionInstance)
                      .WithMany(s => s.BreakRequests)
                      .HasForeignKey(e => e.SessionInstanceId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Student)
                      .WithMany(u => u.BreakRequests)
                      .HasForeignKey(e => e.StudentId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.ApprovedBy)
                      .WithMany()
                      .HasForeignKey(e => e.ApprovedById)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Flag configuration
            modelBuilder.Entity<Flag>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Reason).IsRequired().HasMaxLength(1000);
                entity.Property(e => e.Resolution).HasMaxLength(1000);
                entity.HasOne(e => e.SessionInstance)
                      .WithMany(s => s.Flags)
                      .HasForeignKey(e => e.SessionInstanceId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.Student)
                      .WithMany(u => u.FlagsReceived)
                      .HasForeignKey(e => e.StudentId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.FlaggedBy)
                      .WithMany(u => u.FlagsCreated)
                      .HasForeignKey(e => e.FlaggedById)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(e => e.EscalatedBy)
                      .WithMany()
                      .HasForeignKey(e => e.EscalatedById)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Room configuration
            modelBuilder.Entity<Room>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.HasOne(e => e.SessionInstance)
                      .WithMany(s => s.Rooms)
                      .HasForeignKey(e => e.SessionInstanceId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // SessionEvent configuration
            modelBuilder.Entity<SessionEvent>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Type).HasConversion<string>().HasMaxLength(100);
                entity.Property(e => e.EmittedByRole).HasConversion<string>().HasMaxLength(50);
                entity.Property(e => e.PayloadJson).HasColumnType("json");
                entity.HasOne(e => e.SessionInstance)
                      .WithMany(s => s.Events)
                      .HasForeignKey(e => e.SessionInstanceId)
                      .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.EmittedByUser)
                      .WithMany()
                      .HasForeignKey(e => e.EmittedByUserId)
                      .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(e => new { e.SessionInstanceId, e.Timestamp });
            });
        }
    }
}
