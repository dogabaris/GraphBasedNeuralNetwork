using System.Collections.Generic;
using WebApi.Entities;
using WebApi.Entities.AppModels;

namespace WebApi.Services
{
    public interface IUserService
    {
        User Authenticate(string username, string password);
        IEnumerable<User> GetAll();
        Result<User> Register(string username, string password, string firstname, string lastname);
    }
}