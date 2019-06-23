using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WebApi.Entities;
using WebApi.Helpers;

namespace WebApi.Services
{
    public class UserService : IUserService
    {
        //private List<User> _users = new List<User>
        //{ 
        //    new User { Id = 1, FirstName = "Test", LastName = "User", Username = "test", Password = "test" } 
        //};

        private readonly AppSettings _appSettings;
        private UserContext _userContext;

        public UserService(IOptions<AppSettings> appSettings, UserContext userContext)
        {
            _userContext = userContext;
            _appSettings = appSettings.Value;
        }

        public User Authenticate(string username, string password)
        {
            //var user = _users.SingleOrDefault(x => x.Username == username && x.Password == password);
            var user = _userContext.Users.SingleOrDefault(x => x.Username == username && x.Password == password);

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
            // return users without passwords
            //return _users.Select(x => {
            //    x.Password = null;
            //    return x;
            //});
            var list = _userContext.Users.ToList().Select(x => {
                x.Password = null;
                return x;
            });

            return list;
        }

        public Result<User> Register(string username, string password, string firstname, string lastname)
        {
            var isAlreadyExist = _userContext.Users.FirstOrDefault(x => x.Username == username);
            if (isAlreadyExist != null)
                return new Result<User>
                {
                    Status = "400",
                    Message = "Kullanýcý zaten kayýtlý!",
                    Data = null
                };

            var addedUser = _userContext.Users.Add(new User
            {
                Username = username,
                Password = password,
                FirstName = firstname,
                LastName = lastname,
            });

            _userContext.SaveChanges();

            return new Result<User>
            {
                Status = "200",
                Message = "Kayýt baþarýlý!",
                Data = addedUser.Entity
            };
        }
    }
}