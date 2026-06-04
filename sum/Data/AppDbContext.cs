using Microsoft.EntityFrameworkCore;
using sum.Models;

namespace sum.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Homework> Homeworks { get; set; }
        public DbSet<HomeworkCompletion> HomeworkCompletions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(u => u.Username).IsUnique();
                entity.HasIndex(u => u.Email).IsUnique();
            });

            modelBuilder.Entity<Message>(entity =>
            {
                entity.HasOne(m => m.Sender)
                    .WithMany()
                    .HasForeignKey(m => m.SenderId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(m => m.Recipient)
                    .WithMany()
                    .HasForeignKey(m => m.RecipientId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(m => m.RecipientId);
                entity.HasIndex(m => m.SenderId);
                entity.HasIndex(m => m.SentAt);
            });

            modelBuilder.Entity<Homework>(entity =>
            {
                entity.HasOne(h => h.Teacher)
                    .WithMany()
                    .HasForeignKey(h => h.TeacherId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(h => h.TeacherId);
                entity.HasIndex(h => h.Deadline);
            });

            modelBuilder.Entity<HomeworkCompletion>(entity =>
            {
                entity.HasIndex(hc => new { hc.StudentId, hc.HomeworkId }).IsUnique();

                entity.HasOne(hc => hc.Student)
                    .WithMany()
                    .HasForeignKey(hc => hc.StudentId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(hc => hc.Homework)
                    .WithMany()
                    .HasForeignKey(hc => hc.HomeworkId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
