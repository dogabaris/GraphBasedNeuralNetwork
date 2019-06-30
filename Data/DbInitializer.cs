using System.Collections.ObjectModel;
using System.Linq;
using WebApi.Entities;
using WebApi.Entities.AppModels;

namespace WebApi.Data
{
    public class DbInitializer
    {
        public static void Initialize(AppDbContext _dbContext)
        {
            var isCreated = _dbContext.Database.EnsureCreated();

            // Hiç user var mı diye kontrol edilir.
            if (_dbContext.User.Any())
            {
                return;
            }

            var users = new[]
            {
                new User
                {
                    FirstName = "test",
                    LastName = "test",
                    Password = "test",
                    Username = "test"
                }
            };

            var addedUsers = new Collection<User>();

            foreach (User user in users)
            {
                addedUsers.Add(_dbContext.User.Add(user).Entity);
            }

            // Hiç workspace var mı diye kontrol edilir.
            if (_dbContext.Workspace.Any())
            {
                return;
            }

            var workspace = new Workspace { Name = "Çalışma Alanı 1" , Users = addedUsers };
            _dbContext.Workspace.Add(workspace);
            _dbContext.SaveChanges();
        }
    }
}
