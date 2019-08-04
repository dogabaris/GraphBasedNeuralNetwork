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
using WebApi.Entities.ReqModels;
using System.Text.RegularExpressions;

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
        public async System.Threading.Tasks.Task<IActionResult> CreateModelAsync([FromBody] CreateModel createModel) //[FromBody] string cypherQuery
        {
            var resultJson = "";

            if (createModel.cypherQuery.Contains("workspace") && !string.IsNullOrWhiteSpace(createModel.cypherQuery))
            {
                using (var session = _driver.Session())
                {
                    try
                    {
                        var cursor = await session.RunAsync(createModel.cypherQuery);
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

                var match = Regex.Match(createModel.cypherQuery, "(?<=workspace:')(.*?)(?=\')");

                var res = _userService.CreateModel(match.Value, createModel.user);
                return Ok(res);
            }
            else
            {
                return BadRequest("Düğümlere 'workspace' özelliği eklenmemiş ya da boş cypher sorgusu ile model oluşturulmaya çalışılmıştır!");
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

        [HttpGet("getallworkspaces")]
        public async System.Threading.Tasks.Task<IActionResult> GetAllWorkspacesAsync(int userId)
        {
            var workspaces = _userService.GetAllWorkspaces(userId);

            if (workspaces == null)
                return BadRequest(workspaces);

            return Ok(workspaces);
        }
    }
}
