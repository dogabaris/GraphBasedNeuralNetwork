using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using WebApi.Services;
using WebApi.Entities;
using Neo4j.Driver.V1;
using System;
using WebApi.Entities.Neo4j;
using Neo4jMapper;
using Newtonsoft.Json;
using WebApi.Entities.ReqModels;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using WebApi.Trainers;
using System.Collections.Generic;
using System.Linq;
using ServiceStack.Text;
using WebApi.Helpers;

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

        [HttpGet("trainbinaryperceptron")]
        public async System.Threading.Tasks.Task<IActionResult> TrainBinaryPerceptronAsync(string model) //[FromBody] string cypherQuery
        {
            var client = Neo4JHelper.ConnectDb();

            using (var session = _driver.Session())
            {
                var inputPerceptron = new List<BinaryNode>();
                var outputPerceptron = new List<BinaryNode>();
                var settingNode = new List<BinarySetting>();
                double[] weights;

                try
                {
                    var cursor = await session.RunAsync(@"MATCH(i: input)                                                          WHERE i.workspace = '" + model +
                                                          "' RETURN i");

                    inputPerceptron = (await cursor.ToListAsync())
                                            .Map<BinaryNode>().ToList();
                    //var resultJson = JsonConvert.SerializeObject(inputPerceptron);
                }
                catch (Exception ex)
                {
                    return BadRequest(ex);
                }
                try
                {
                    var cursor = await session.RunAsync(@"MATCH(o: output)                                                          WHERE o.workspace = '" + model +
                                                          "' RETURN o");

                    outputPerceptron = (await cursor.ToListAsync())
                                            .Map<BinaryNode>().ToList();
                }
                catch (Exception ex)
                {
                    return BadRequest(ex);
                }
                try
                {
                    var cursor = await session.RunAsync(@"MATCH(s: setting)                                                          WHERE s.workspace = '" + model +
                                                          "' RETURN s");

                    settingNode = (await cursor.ToListAsync())
                                            .Map<BinarySetting>().ToList();
                }
                catch (Exception ex)
                {
                    return BadRequest(ex);
                }
                try
                {
                    weights = client
                        .Cypher
                        .Match("(i:input {workspace:'" + model + "'})-[r:related]-(h:hidden)")
                        .Return((r) =>
                            new
                            {
                                Weight = r.As<Weight>()
                            })
                        .Results.Select(x => x.Weight.weight).ToArray();

                    //var cursor = session.Run(@"MATCH(i:input {workspace:'" + model + "'})-[r:related]-(h:hidden) return r");

                    //weights = cursor.ToList()
                    //                        .Map<Weight>().Select(x => {
                    //                            return x.weight;
                    //                        }).ToArray();
                }
                catch (Exception ex)
                {
                    return BadRequest(ex);
                }


                var perceptron = new BinaryPerceptron(_driver, weights, model, settingNode.FirstOrDefault().learningrate, settingNode.FirstOrDefault().threshold);
                var returnJson = new JObject();

                int attemptCount = 0;
                // teach the neural network until all the inputs are correctly clasified
                while (true)
                {
                    Console.WriteLine("-- Attempt: " + (++attemptCount));

                    int errorCount = 0;
                    //foreach (var item in inputPerceptron)
                    //{
                    // teach the perceptron to which class given inputs belong
                    //var inputs = inputPerceptron.Select(inp => inp.data).ToArray();
                    var exceptedResult = outputPerceptron.Find(o => o.expectedoutput != null).expectedoutput;

                    var output = perceptron.LearnAsync(exceptedResult.Value, inputPerceptron);
                    // check that the inputs were classified correctly
                    if (output.Result != exceptedResult)
                    {
                        var inpList = inputPerceptron.Select(inp => inp.data.ToString()).ToList();
                        inpList.Add(output.Result.ToString());
                        var data = String.Join(", ", inpList);
                        returnJson.Add(String.Format("Fail {0}", attemptCount), data);
                        errorCount++;
                    }
                    else
                    {
                        var inpList = inputPerceptron.Select(inp => inp.data.ToString()).ToList();
                        inpList.Add(output.Result.ToString());
                        var data = String.Join(", ", inpList);
                        returnJson.Add(String.Format("Pass {0}", attemptCount), data);
                    }
                    //}
                    
                    if (errorCount == 0)
                        return Ok(returnJson);
                }

            }

            //TrainingItem[] inputPerceptron = {
            //    new TrainingItem(true, 1, 0, 0),
            //    //new TrainingItem(true, 1, 0, 1),
            //    //new TrainingItem(true, 1, 1, 0),
            //   // new TrainingItem(false, 1, 1, 1)
            //};

            //// create a perceptron with 3 inputs
            //var perceptron = new BinaryPerceptron(3);
            //var returnJson = new JObject();

            //int attemptCount = 0;
            //// teach the neural network until all the inputs are correctly clasified
            //while (true)
            //{
            //    Console.WriteLine("-- Attempt: " + (++attemptCount));

            //    int errorCount = 0;
            //    foreach (var item in inputPerceptron)
            //    {
            //        // teach the perceptron to which class given inputs belong
            //        var output = perceptron.Learn(item.Output, item.Inputs);
            //        // check that the inputs were classified correctly
            //        if (output != item.Output)
            //        {
            //            returnJson.Add(String.Format("Fail{0}", attemptCount), String.Join(",", item.Inputs[0], item.Inputs[1], item.Inputs[2], output));
            //            Console.WriteLine("Fail\t {0} & {1} & {2} != {3}", item.Inputs[0], item.Inputs[1], item.Inputs[2], output);
            //            errorCount++;
            //        }
            //        else
            //        {
            //            returnJson.Add(String.Format("Pass{0}", attemptCount), String.Join(",", item.Inputs[0], item.Inputs[1], item.Inputs[2], output));
            //            Console.WriteLine("Pass\t {0} & {1} & {2} = {3}", item.Inputs[0], item.Inputs[1], item.Inputs[2], output);
            //        }
            //    }

            //    // only quit when there were no unexpected outputs detected
            //    if (errorCount == 0)
            //    {
            //        return Ok(returnJson);
            //        //break;
            //    }
            //}
            //return null;
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
                        var nodes = await cursor.MapAsync<BinaryNode>();
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
