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
using WebApi.Helpers;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections;
using WebApi.Entities.H5JsonModel;

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
        [HttpGet("importh5model")]
        public IActionResult ImportH5Model()
        {
            //CREATE 
            //(`0` :input {workspace:'4',data:'0'}) ,
            //(`1` :hidden {workspace:'4',data:'XOR'}) ,
            //(`2` :output {workspace:'4',exceptedoutput:'1'}) ,
            //(`3` :setting {workspace:'4',learningrate:'0.1',treshold:'0.5'}) ,
            //(`5` :input {workspace:'4',data:'0'}) ,
            //(`6` :input {workspace:'4',data:'1'}) ,
            //(`0`)-[:`related` {weight:'0.0'}]->(`1`),
            //(`5`)-[:`related` {weight:'0.0'}]->(`1`),
            //(`6`)-[:`related` {weight:'0.0'}]->(`1`),
            //(`1`)-[:`related` {weight:'0.0'}]->(`2`)
            string cypherQuery = "CREATE (`99` :output {workspace:'99'})";
            var res = RunPython(@"h5tojson.py", "demo_model.h5");
            if (res)
            {
                try
                {
                    var contentPath = Path.GetFullPath("~/Content/H5Files/").Replace("~\\", "") + "model.json";
                    using (StreamReader r = new StreamReader(contentPath))
                    {
                        string json = r.ReadToEnd();
                        dynamic data = JObject.Parse(json);
                        var start = data.root.Value;
                        var layers = data.groups[start].links.ToObject<List<links>>();
                        foreach (links layer in layers)
                        {
                            layer.layerAlias = data.groups[layer.id].attributes[0].value.ToObject<List<string>>();
                        }

                        //foreach (links layer in layers)
                        //{
                        //    for (int j = 0; j < layer.layerAlias.Count; j++)
                        //    {
                        //        var a = data.groups[layer.].links.ToObject<List<links>>();
                        //    }
                        //}

                        
                        foreach (links layer in layers)
                        {
                            var datasets = ((IEnumerable<dynamic>)data.datasets)
                                .Select(x => x);
                            
                            for (int it = 0; it < datasets.Count(); it++)
                            {
                                if(datasets.ElementAt(it).Value.alias[0] == "/" + layer.title + "/" + layer.layerAlias[0])
                                {
                                    var numberOfInput = datasets.ElementAt(it).Value.value.Count;
                                    var numberOfHidden = datasets.ElementAt(it).Value.value[0].Count;

                                    //TODO dışarı taşı
                                    //workspace sayısı saydırılıyor ki queryde kaç eleman olduğu anlaşılsın ve nodelara eklemeye değil 
                                    if (CountStringOccurrences(cypherQuery, "workspace") >= numberOfHidden + numberOfInput) 
                                    {
                                        

                                    }
                                    else
                                    {
                                        //input kernelin array eleman sayısı kadar input, arrayin ilk elemanındaki eleman sayısı kadar hidden node u vardır
                                        for (int inp = 0; inp < numberOfInput; inp++)
                                        {
                                            cypherQuery += string.Format("(`{0}` :input {1})", inp, "{ workspace: '99', data: '0'}");
                                        }
                                        //hidden
                                        for (int hid = numberOfInput; hid < numberOfInput + numberOfHidden; hid++)
                                        {
                                            cypherQuery += string.Format("(`{0}` :hidden {1})", hid, "{ workspace: '99', data: '0'}");
                                        }
                                    }
                                }

                            }
                        }
                    }

                    return Ok();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    return StatusCode(500);
                }
            }


            return StatusCode(500);
        }

        public static int CountStringOccurrences(string text, string pattern)
        {
            // Loop through all instances of the string 'text'.
            int count = 0;
            int i = 0;
            while ((i = text.IndexOf(pattern, i)) != -1)
            {
                i += pattern.Length;
                count++;
            }
            return count;
        }

        public bool RunPython(string cmd, string args) // .\h5tojson.pydemo_model.h5
        {
            ProcessStartInfo start = new ProcessStartInfo();
            var contentPath = Path.GetFullPath("~/Content/H5Files/").Replace("~\\", "");

            //var arg1 = Path.Combine(contentPath, cmd);
            //var arg2 = Path.Combine(contentPath, args);
            start.FileName = "python2";
            start.WorkingDirectory = Path.GetDirectoryName(contentPath);
            start.Arguments = string.Format("\"{0}\" \"{1}\"", cmd, args);
            start.UseShellExecute = false;// Do not use OS shell
            start.CreateNoWindow = true; // We don't need new window
            start.RedirectStandardOutput = true;// Any output, generated by application will be redirected back
            start.RedirectStandardError = true; // Any error in standard output will be redirected back (for example exceptions)
            using (Process process = Process.Start(start))
            {
                using (StreamReader reader = process.StandardOutput)
                {
                    //string stderr = process.StandardError.ReadToEnd(); // Here are the exceptions from our Python script
                    string result = reader.ReadToEnd(); // Here is the result of StdOut(for example: print "test")
                    if (!string.IsNullOrEmpty(result))
                        return true;
                    else
                        return false;
                }
            }
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
                }
                catch (Exception ex)
                {
                    return BadRequest(ex);
                }


                var perceptron = new BinaryPerceptron(_driver, weights, model, settingNode.FirstOrDefault().learningrate, settingNode.FirstOrDefault().threshold);
                var returnJson = new JObject();

                int attemptCount = 0;
                while (true)
                {

                    int errorCount = 0;
                    var exceptedResult = outputPerceptron.Find(o => o.expectedoutput != null).expectedoutput;

                    var output = perceptron.LearnAsync(exceptedResult.Value, inputPerceptron);

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

                    if (errorCount == 0)
                    {
                        var cursor = await session.RunAsync(@"MATCH(h:hidden {workspace:'" + model + "'})-[r]-(o:output) " +
                            "set r.weight = " + Convert.ToInt32(output.Result) +
                            ", o.data = " + Convert.ToInt32(output.Result) +
                            " return h,r,o");
                        return Ok(returnJson);
                    }

                }

            }
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
