using Microsoft.EntityFrameworkCore;

namespace WebApi.Entities.AppModels
{
    public class AppDbContext : DbContext
    {
        public DbSet<Workspace> Workspace { get; set; }
        public DbSet<User> User { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {}

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>().
                HasOne(p => p.Workspace).
                WithMany(p => p.Users).
                HasForeignKey(p => p.WorkspaceId);

            //modelBuilder.Entity<Workspace>().
            //    HasMany(p => p.Users).
            //    WithOne(p => p.Workspace).
            //    HasForeignKey(p => p.WorkspaceId).
            //    OnDelete(DeleteBehavior.Cascade);
        }
    }
}
