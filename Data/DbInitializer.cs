using System;
using Microsoft.EntityFrameworkCore.Internal;
using WebApi.Entities;

namespace WebApi.Data
{
    public class DbInitializer
    {
        public static void Initialize(UserContext context)
        {
            context.Database.EnsureCreated();

            // Hiç user var mı diye kontrol edilir.
            if (context.Users.Any())
            {
                return;
            }

            var users = new User[]
            {
                new User{FirstName = "test", LastName = "test", Password = "test", Username = "test"},
            };

            foreach (User s in users)
            {
                context.Users.Add(s);
            }

            context.SaveChanges();
        }
    }
}
