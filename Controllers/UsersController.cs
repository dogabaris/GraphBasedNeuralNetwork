﻿using Microsoft.AspNetCore.Mvc;
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
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using System.Net.Http.Headers;
using ServiceStack.Text;
using Neo4jClient;
using Microsoft.EntityFrameworkCore.Internal;
using System.Threading.Tasks;

namespace WebApi.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class UsersController : ControllerBase
    {
        private IUserService _userService;
        private IDriver _driver;
        private readonly IHttpContextAccessor _context;
        public GraphClient Client { get; set; }

        public UsersController(IUserService userService, IDriver driver, IHttpContextAccessor context)
        {
            _userService = userService;
            Client = Neo4JHelper.ConnectDb();
            _driver = driver;
            _context = context;
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
        [HttpPost("importh5model")]
        public IActionResult ImportH5Model([FromBody] User user)
        {
            string cypherQuery = "CREATE (`6` :output {workspace:'99'}),";
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

                        foreach (links layer in layers)
                        {
                            var datasets = ((IEnumerable<dynamic>)data.datasets)
                                .Select(x => x);

                            for (int it = 0; it < datasets.Count(); it++)
                            {
                                if (datasets.ElementAt(it).Value.alias[0] == "/" + layer.title + "/" + layer.layerAlias[0])
                                {
                                    var numberOfInput = datasets.ElementAt(it).Value.value.Count;
                                    var numberOfHidden = datasets.ElementAt(it).Value.value[0].Count;

                                    //workspace sayısı saydırılıyor ki queryde kaç eleman olduğu anlaşılsın ve nodelara eklemeye değil 
                                    if (CountStringOccurrences(cypherQuery, "workspace") < numberOfHidden + numberOfInput)
                                    {
                                        //input kernelin array eleman sayısı kadar input, arrayin ilk elemanındaki eleman sayısı kadar hidden node u vardır
                                        for (int inp = 0; inp < numberOfInput; inp++)
                                        {
                                            cypherQuery += string.Format("(`{0}` :input {1}),", inp, "{ workspace: '99', data: '0'}");
                                        }
                                        //hidden
                                        for (int hid = numberOfInput; hid < numberOfInput + numberOfHidden; hid++)
                                        {
                                            cypherQuery += string.Format("(`{0}` :hidden {1}),", hid, "{ workspace: '99', data: '0'}");
                                        }
                                    }
                                    else
                                        break;
                                }

                            }
                        }

                        //relationships 12 link olmalı 
                        foreach (links layer in layers)
                        {
                            var datasets = ((IEnumerable<dynamic>)data.datasets)
                                .Select(x => x);

                            for (int it = 0; it < datasets.Count(); it++)
                            {
                                if (datasets.ElementAt(it).Value.alias[0] == "/" + layer.title + "/" + layer.layerAlias[0]) // 0 kernel 1 bias
                                {
                                    var bias = new JArray();

                                    //bias listesini alır biası relationlara dağıtır
                                    for (int it2 = 0; it2 < datasets.Count(); it2++)
                                    {
                                        if (datasets.ElementAt(it2).Value.alias[0] == "/" + layer.title + "/" + layer.layerAlias[1]) // 0 kernel 1 bias
                                        {
                                            bias = datasets.ElementAt(it2).Value.value;
                                        }
                                    }

                                    //kernellerdeki toplam eleman sayısı kadar relation oluyor.
                                    if (layer.title.Split('_').Count() > 1) //layerların nodelarının doğru idlerde olması için çarpıyor
                                    {
                                        var factor = int.Parse(layer.title.Split('_').LastOrDefault()) * datasets.ElementAt(it).Value.value.Count;
                                        var nodeNumber = CountStringOccurrences(cypherQuery, "workspace");

                                        for (int x = 0; x < datasets.ElementAt(it).Value.value.Count; x++) //kernel küme
                                        {
                                            for (int y = 0; y < datasets.ElementAt(it).Value.value[x].Count; y++) //kernel eleman
                                            {
                                                cypherQuery += string.Format("(`{0}`)-[:`related` {{ kernel: '{1}', bias: '{2}'}}]->(`{3}`),", x + factor, datasets.ElementAt(it).Value.value[x][y].Value, bias[0], nodeNumber - 1); //Output noduna bağlantı yapılıyor. , bias: '{2}'
                                            }
                                        }
                                    }
                                    else
                                    {
                                        for (int x = 0; x < datasets.ElementAt(it).Value.value.Count; x++) //kernel küme
                                        {
                                            var iterator = 0;
                                            for (int y = 0; y < datasets.ElementAt(it).Value.value[x].Count; y++) //kernel eleman
                                            {
                                                cypherQuery += string.Format("(`{0}`)-[:`related` {{ kernel: '{1}', bias: '{2}'}}]->(`{3}`),", x, datasets.ElementAt(it).Value.value[x][y].Value, bias[x], datasets.ElementAt(it).Value.value[x].Count + iterator); //, bias: '{2}'
                                                iterator++;
                                            }
                                        }
                                    }
                                }

                            }
                        }
                    }

                    var response = string.Empty;
                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
                        var result = client.PostAsJsonAsync("http://" + Request.Host + "/users/createmodel", new CreateModel
                        {
                            cypherQuery = cypherQuery.TrimEnd(','),
                            user = user
                        }).Result;
                        if (result.IsSuccessStatusCode)
                            return Ok();
                    }

                    return BadRequest();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    return StatusCode(500);
                }
            }


            return StatusCode(500);
        }

        [AllowAnonymous]
        [HttpPost("importcnnh5model")]
        public IActionResult ImportCnnH5Model([FromBody] User user)
        {
            string cypherQuery = "CREATE (`999` :output {workspace:'100'}),";
            var res = RunPython(@"h5tojson.py", "mobilenet_2_5_128_tf.h5");
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
                        List<links> layersUnordered = data.groups[start].links.ToObject<List<links>>();
                        List<string> orderList = data.groups[start].attributes[2].value.ToObject<List<string>>();
                        var layers = layersUnordered.OrderBy(item => orderList.IndexOf(item.title)).ToList();

                        foreach (links layer in layers)
                        {
                            if (data.groups[layer.id].attributes[0].value != null)
                                layer.layerAlias = data.groups[layer.id].attributes[0].value.ToObject<List<string>>();
                        }

                        long id = 3;
                        //input 128 * 128 * 3
                        var datasets = ((IEnumerable<dynamic>)data.datasets)
                                .Select(x => x).ToList();
                        foreach (links layer in layers)
                        {
                            for (int it = 0; it < datasets.Count(); it++)
                            {
                                //3 channeldan görseli göndereceği için 3 node input olmalı
                                if (layer.title.Contains("input"))
                                {
                                    cypherQuery += string.Format("(`0` :{0} {{ workspace: '100', data: '0' }}),", layer.title);
                                    cypherQuery += string.Format("(`1` :{0} {{ workspace: '100', data: '0' }}),", layer.title);
                                    cypherQuery += string.Format("(`2` :{0} {{ workspace: '100', data: '0' }}),", layer.title);
                                    break;
                                }
                                else
                                {
                                    if (layer.layerAlias == null)
                                    {
                                        for (long lay = 0; lay < 3; lay++)
                                        {
                                            cypherQuery += string.Format("(`{0}` :{1} {{ workspace: '100', data: '0' }}),", id, layer.title.Replace("/", "_").Replace(":", "_"));

                                            id++;
                                        }
                                        break;
                                    }
                                    else
                                    {
                                        foreach (string layerAlias in layer.layerAlias)
                                        {
                                            if (datasets.ElementAt(it).Value.alias[0] == "/" + layer.title + "/" + layerAlias)
                                            {
                                                long nextNodeCount = 3;//Kernel node u 1 tane atanıyordu yanlışlık düzeltildi.
                                                if (datasets.ElementAt(it).Value.shape.dims.Count == 4 && datasets.ElementAt(it).Value.shape.dims[datasets.ElementAt(it).Value.shape.dims.Count - 3].Value != 1)
                                                    nextNodeCount = datasets.ElementAt(it).Value.shape.dims[datasets.ElementAt(it).Value.shape.dims.Count - 3].Value;

                                                for (long lay = 0; lay < nextNodeCount; lay++)
                                                {
                                                    cypherQuery += string.Format("(`{0}` :{1} {{ workspace: '100', data: '0' }}),", id, layerAlias.Replace("/", "_").Replace(":", "_"));

                                                    id++;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        id = 0;
                        var layerCount = 1;
                        var nextLayerFirst = 3;

                        var orderedDataset = new List<dynamic>();

                        //dataseti sıralayıp olmayan itemları da içeriye gömmek lazım ki sırasında bir önceki item

                        foreach (var order in orderList)
                        {
                            foreach (var dItem in datasets)
                            {
                                if (dItem.Value.alias[0].Value.Contains(order + "/"))
                                {
                                    //var gonnaAddItems = datasets.Select(x => x.Value.alias.Value.Contains(order)).ToList();
                                    orderedDataset.Add(dItem);
                                }
                            }

                            if (orderedDataset.Count(x => x.Value.alias[0].Value.Contains(order)) == 0)
                            {
                                dynamic jsonObject = new JObject();
                                var guid = Guid.NewGuid().ToString();
                                JObject obj = JObject.FromObject(new
                                {
                                    inserted = new
                                    {
                                        alias = new string[] { order },
                                        shape = new
                                        {
                                            dims = new int[0]
                                        },
                                    }
                                });
                                orderedDataset.Add(obj.First);
                            }
                        }

                        //relationships
                        links last = layers.Last();
                        foreach (links layer in layers)
                        {
                            //for (int it = 0; it < datasets.Count(); it++)//layer
                            foreach (var it in orderedDataset)
                            {
                                if (layer.Equals(last))
                                {
                                    for (var nodeIt = 0; nodeIt < 3; nodeIt++)
                                    {
                                        for (var relationIt = 0; relationIt < 3; relationIt++)
                                        {
                                            cypherQuery += string.Format("(`{0}`)-[:`related` {{matrix: 1 }}]->(`{1}`),", id, nextLayerFirst + relationIt);
                                        }
                                        id++;
                                    }
                                    nextLayerFirst += 3;

                                    for (var nodeIt = 0; nodeIt < 3; nodeIt++)
                                    {
                                        for (var relationIt = 0; relationIt < 3; relationIt++)
                                        {
                                            cypherQuery += string.Format("(`{0}`)-[:`related` {{matrix: 1 }}]->(`999`),", id);
                                        }
                                        id++;
                                    }
                                    nextLayerFirst += 3;
                                    break;
                                }
                                else if (layer.layerAlias == null && layer.title != "input_1")
                                {
                                    for (var nodeIt = 0; nodeIt < 3; nodeIt++)
                                    {
                                        for (var relationIt = 0; relationIt < 3; relationIt++)
                                        {
                                            cypherQuery += string.Format("(`{0}`)-[:`related` {{matrix: 1 }}]->(`{1}`),", id, nextLayerFirst + relationIt);
                                        }
                                        id++;
                                    }
                                    nextLayerFirst += 3;
                                    break;
                                }
                                else if (layer.layerAlias != null)
                                {
                                    foreach (var lyrAlias in layer.layerAlias)
                                    {
                                        if (it.Value.alias[0] == "/" + layer.title + "/" + lyrAlias)
                                        {
                                            if (it.Value.shape.dims.Count == 4)
                                            {
                                                if (it.Value.shape.dims[0].Value == 1 && it.Value.shape.dims[1].Value == 1)
                                                {
                                                    for (var nodeIt = 0; nodeIt < 3; nodeIt++)
                                                    {
                                                        for (var relationIt = 0; relationIt < 3; relationIt++) 
                                                        {
                                                            if (it.Value.shape.dims[1].Value != 3)
                                                                cypherQuery += string.Format("(`{0}`)-[:`related` {{matrix: '{1}'}}]->(`{2}`),", id, JsonConvert.SerializeObject(it.Value.value[0][0]), nextLayerFirst + relationIt);
                                                            else
                                                                cypherQuery += string.Format("(`{0}`)-[:`related` {{matrix: '{1}'}}]->(`{2}`),", id, JsonConvert.SerializeObject(it.Value.value[0][0]), nextLayerFirst + relationIt);
                                                        }
                                                        id++;
                                                    }
                                                } else if (it.Value.shape.dims[0].Value == 3 && it.Value.shape.dims[1].Value == 3)
                                                {
                                                    for (var nodeIt = 0; nodeIt < it.Value.shape.dims[0].Value; nodeIt++)
                                                    {
                                                        for (var relationIt = 0; relationIt < it.Value.shape.dims[1].Value; relationIt++) 
                                                        {
                                                            if (it.Value.shape.dims[1].Value != 3)
                                                                cypherQuery += string.Format("(`{0}`)-[:`related` {{matrix: '{1}'}}]->(`{2}`),", id, JsonConvert.SerializeObject(it.Value.value[nodeIt][relationIt]), nextLayerFirst + relationIt);
                                                            else
                                                                cypherQuery += string.Format("(`{0}`)-[:`related` {{matrix: '{1}'}}]->(`{2}`),", id, JsonConvert.SerializeObject(it.Value.value[nodeIt][relationIt]), nextLayerFirst + relationIt);
                                                        }
                                                        id++;
                                                    }
                                                }
                                                
                                                nextLayerFirst += 3;
                                            }
                                            if (it.Value.shape.dims.Count == 1) //layerı null olanlar
                                            {
                                                for (var nodeIt = 0; nodeIt < 3; nodeIt++)
                                                {
                                                    for (var relationIt = 0; relationIt < 3; relationIt++)
                                                    {
                                                        cypherQuery += string.Format("(`{0}`)-[:`related` {{  matrix: '{1}'}}]->(`{2}`),", id, JsonConvert.SerializeObject(it.Value.value), nextLayerFirst + relationIt);
                                                    }
                                                    id++;
                                                }
                                                nextLayerFirst += 3;
                                            }
                                        }
                                    }

                                    layerCount++;
                                }
                            }
                        }
                    }

                    System.IO.File.WriteAllText(@"C:\Users\Public\CnnCypherQuery.txt", cypherQuery);

                    var response = string.Empty;
                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
                        var result = client.PostAsJsonAsync("http://" + Request.Host + "/users/createmodel", new CreateModel
                        {
                            cypherQuery = cypherQuery.TrimEnd(','),
                            user = user
                        }).Result;
                        if (result.IsSuccessStatusCode)
                            return Ok();
                    }

                    return BadRequest();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                    return StatusCode(500);
                }
            }


            return StatusCode(500);
        }

        [AllowAnonymous]
        [HttpPost("exporth5model")]
        public IActionResult ExportH5Model([FromBody] ExportH5Model exportModel)
        {
            var neoModelCypher = "start n=node(*), r=relationship(*) MATCH (n)-[r]->(n2) where(n.workspace = '" + exportModel.workspace + "'and EXISTS (r.kernel)) return n,r,n2";

            var H5json = new { apiVersion = "1.1.1", datasets = new { }, groups = new { }, root = Guid.NewGuid() };

            using (var session = _driver.Session())
            {
                try
                {
                    var cursor = session.Run(neoModelCypher);
                    var graphJson = new List<IReadOnlyDictionary<string, object>>();
                    foreach (var record in cursor)
                    {
                        graphJson.Add(record.Values);
                    }

                }
                catch (Exception ex)
                {
                    return BadRequest();
                }
            }


            return Ok();
        }

        public static int CountStringOccurrences(string text, string pattern)
        {
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

            start.FileName = "python2";
            start.WorkingDirectory = Path.GetDirectoryName(contentPath);
            start.Arguments = string.Format("\"{0}\" \"{1}\"", cmd, args);
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            using (Process process = Process.Start(start))
            {
                using (StreamReader reader = process.StandardOutput)
                {
                    string result = reader.ReadToEnd();
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
        public async System.Threading.Tasks.Task<IActionResult> TrainBinaryPerceptronAsync(string model)
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
                    try
                    {
                        var output = await perceptron.LearnAsync(exceptedResult.Value, inputPerceptron);

                        if (output != exceptedResult)
                        {
                            var inpList = inputPerceptron.Select(inp => inp.data.ToString()).ToList();
                            inpList.Add(output.ToString());
                            var data = String.Join(", ", inpList);
                            returnJson.Add(String.Format("Fail {0}", attemptCount), data);
                            errorCount++;
                        }
                        else
                        {
                            var inpList = inputPerceptron.Select(inp => inp.data.ToString()).ToList();
                            inpList.Add(output.ToString());
                            var data = String.Join(", ", inpList);
                            returnJson.Add(String.Format("Pass {0}", attemptCount), data);
                        }

                        if (errorCount == 0)
                        {
                            var cursor = await session.RunAsync(@"MATCH(h:hidden {workspace:'" + model + "'})-[r]-(o:output) " +
                                "set r.weight = " + Convert.ToInt32(output) +
                                ", o.data = " + Convert.ToInt32(output) +
                                " return h,r,o");
                            return Ok(returnJson);
                        }
                    }
                    catch(Exception ex)
                    {
                        return BadRequest(returnJson);
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
                        //var nodes = await cursor.MapAsync<BinaryNode>();
                        //resultJson = JsonConvert.SerializeObject(nodes);
                    }
                    catch (Exception ex)
                    {
                        System.IO.File.WriteAllText(@"C:\Users\Public\CnnCypherQueryERROR.txt", ex.ToString());
                        return BadRequest(ex);
                    }
                }

                //if (string.IsNullOrWhiteSpace(resultJson))
                //    return BadRequest(resultJson);

                var match = Regex.Match(createModel.cypherQuery, "(?<=workspace:')(.*?)(?=\')");

                var res = _userService.CreateModel(match.Value, createModel.user);
                return Ok(); //res
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
