using System.Collections.Generic;
using WebApi.Entities;

namespace WebApi.Services
{
    public interface IUserService
    {
        User Authenticate(string username, string password);
        IEnumerable<User> GetAll();
        Result<User> Register(string username, string password, string firstname, string lastname);
    }
}