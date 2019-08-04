using Microsoft.EntityFrameworkCore;
using System.Linq;
using WebApi.Entities;
using WebApi.Entities.AppModels;

namespace WebApi.Data
{
    public class DbInitializer
    {
        public static void Initialize(AppDbContext _dbContext)
        {
            if (_dbContext.User.Any() && _dbContext.Workspace.Any() && _dbContext.UserWorkspace.Any())
            {
                _dbContext.Database.Migrate();
            }
            else
            {
                var isDeleted = _dbContext.Database.EnsureDeleted();
                var isCreated = _dbContext.Database.EnsureCreated();

                var user = new User
                {
                    FirstName = "test",
                    LastName = "test",
                    Password = "test",
                    Username = "test"
                };

                user = _dbContext.User.Add(user).Entity;

                var workspace = new Workspace { Name = "1" };
                workspace = _dbContext.Workspace.Add(workspace).Entity;

                var userWorkspace = new UserWorkspace
                {
                    User = user,
                    Workspace = workspace
                };

                _dbContext.UserWorkspace.Add(userWorkspace);
                _dbContext.SaveChanges();
            }
        }
    }
}
