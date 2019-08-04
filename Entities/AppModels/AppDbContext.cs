using Microsoft.EntityFrameworkCore;

namespace WebApi.Entities.AppModels
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserWorkspace>().HasKey(sc => new { sc.UserId, sc.WorkspaceId });

            modelBuilder.Entity<UserWorkspace>()
                .HasOne(sc => sc.User)
                .WithMany(s => s.UserWorkspaces)
                .HasForeignKey(sc => sc.UserId);

            modelBuilder.Entity<UserWorkspace>()
                .HasOne(sc => sc.Workspace)
                .WithMany(s => s.UserWorkspaces)
                .HasForeignKey(sc => sc.WorkspaceId);
        }

        public DbSet<Workspace> Workspace { get; set; }
        public DbSet<User> User { get; set; }
        public DbSet<UserWorkspace> UserWorkspace { get; set; }
    }
}
