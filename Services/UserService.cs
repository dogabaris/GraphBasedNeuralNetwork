using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WebApi.Entities;
using WebApi.Entities.AppModels;
using WebApi.Helpers;

namespace WebApi.Services
{
    public class UserService : IUserService
    {
        private readonly AppSettings _appSettings;
        private AppDbContext _dbContext;

        public UserService(IOptions<AppSettings> appSettings, AppDbContext dbContext)
        {
            _dbContext = dbContext;
            _appSettings = appSettings.Value;
        }

        public User Authenticate(string username, string password)
        {
            //var user = _users.SingleOrDefault(x => x.Username == username && x.Password == password);
            var user = _dbContext.User.SingleOrDefault(x => x.Username == username && x.Password == password);

            // Kullanýcý bulunamadýðýnda null döndürülüyor.
            if (user == null)
                return null;

            // Kullanýcý bulunduðu için jwt token üretiliyor.
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_appSettings.Secret);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[] 
                {
                    new Claim(ClaimTypes.Name, user.Id.ToString())
                }),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            var token = tokenHandler.CreateToken(tokenDescriptor);
            user.Token = tokenHandler.WriteToken(token);

            // Sonuç dönerken þifre siliniyor.
            user.Password = null;

            return user;
        }

        public IEnumerable<User> GetAll()
        {
            var list = _dbContext.User.ToList().Select(x => {
                x.Password = null;
                return x;
            });

            return list;
        }

        public Result<User> Register(string username, string password, string firstname, string lastname)
        {
            var isAlreadyExist = _dbContext.User.FirstOrDefault(x => x.Username == username);
            if (isAlreadyExist != null)
                return new Result<User>
                {
                    Status = "400",
                    Message = "Kullanýcý zaten kayýtlý!",
                    Data = null
                };

            var addedUser = _dbContext.User.Add(new User
            {
                Username = username,
                Password = password,
                FirstName = firstname,
                LastName = lastname,
            });

            _dbContext.SaveChanges();

            return new Result<User>
            {
                Status = "200",
                Message = "Kayýt baþarýlý!",
                Data = addedUser.Entity
            };
        }
    }
}