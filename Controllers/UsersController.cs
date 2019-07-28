using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApi.Services;
using WebApi.Entities;
using Neo4j.Driver.V1;
using System;
using ServiceStack.Text;
using WebApi.Entities.Neo4j;
using Neo4jMapper;
using Newtonsoft.Json;
using System.IO;
using System.Text;

namespace WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class UsersController : ControllerBase
    {
        private IUserService _userService;
        private IDriver _driver;

        public UsersController(IUserService userService, IDriver driver)
        {
            _userService = userService;
            _driver = driver;
        }

        [AllowAnonymous]
        [HttpPost("authenticate")]
        public IActionResult Authenticate([FromBody]User userParam)
        {
            var user = _userService.Authenticate(userParam.Username, userParam.Password);

            if (user == null)
                return BadRequest(new { message = "Kullanıcı adı ya da parola hatalı!" });

            return Ok(user);
        }

        [AllowAnonymous]
        [HttpPost("register")]
        public IActionResult Register([FromBody]User userParam)
        {
            var result = _userService.Register(userParam.Username, userParam.Password, userParam.FirstName, userParam.LastName);

            if (result.Data == null)
                return BadRequest(result);

            return Ok(result);
        }

        [HttpPost("createmodel")]
        public async System.Threading.Tasks.Task<IActionResult> CreateModelAsync() //[FromBody] string cypherQuery
        {
            using (StreamReader reader = new StreamReader(Request.Body, Encoding.UTF8))
            {
                var cypherQuery = await reader.ReadToEndAsync();

                var resultJson = "";

                using (var session = _driver.Session())
                {
                    try
                    {
                        var cursor = await session.RunAsync(cypherQuery);
                        //TODO: Return node or model as entity
                        var nodes = await cursor.MapAsync<Node>();
                        resultJson = JsonConvert.SerializeObject(nodes);
                    }
                    catch (Exception ex)
                    {
                        return BadRequest(ex);
                    }
                }

                if (string.IsNullOrWhiteSpace(resultJson))
                    return BadRequest(resultJson);

                return Ok(resultJson);
            }
        }

        [HttpGet]
        public async System.Threading.Tasks.Task<IActionResult> GetAllAsync()
        {
            var users = _userService.GetAll();

            if (users == null)
                return BadRequest(users);

            return Ok(users);
        }
    }
}
