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
using WebApi.Entities.H5JsonModel;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using System.Net.Http.Headers;
using ServiceStack.Text;
using Neo4jClient;
using System.Threading.Tasks;
using System.Globalization;
using Neo4jClient.Cypher;
using Node = WebApi.Entities.Neo4j.Node;
using ServiceStack;

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
        public IActionResult Authenticate([FromBody] User userParam)
        {
            var user = _userService.Authenticate(userParam.Username, userParam.Password);

            if (user == null)
                return BadRequest(new { message = "Kullanıcı adı ya da parola hatalı!" });

            return Ok(user);
        }

        //TODO: STRING dataları sayıya çevir
        [AllowAnonymous]
        [HttpPost("importh5model")]
        public IActionResult ImportH5Model([FromBody] User user)
        {
            string cypherQuery = "CREATE ";
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
                                            cypherQuery += string.Format("(`{0}` :input {1}),", inp, "{ workspace: '99', data: 0}");
                                        }
                                        //hidden
                                        for (int hid = numberOfInput; hid < numberOfInput + numberOfHidden; hid++)
                                        {
                                            cypherQuery += string.Format("(`{0}` :hidden {1}),", hid, "{ workspace: '99', data: 0}");
                                        }
                                    }
                                    else
                                        break;
                                }

                            }
                        }

                        cypherQuery += "(`6` :output { workspace: '99', data: 0}),";

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
                                                cypherQuery += string.Format("(`{0}`)-[:`related` {{ kernel: {1}, bias: {2}}}]->(`{3}`),", x + factor, datasets.ElementAt(it).Value.value[x][y].Value.ToString(CultureInfo.InvariantCulture), bias[0].Value<string>(), nodeNumber - 1); //Output noduna bağlantı yapılıyor. , bias: '{2}'
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
                                                cypherQuery += string.Format("(`{0}`)-[:`related` {{ kernel: {1}, bias: {2}}}]->(`{3}`),", x, datasets.ElementAt(it).Value.value[x][y].Value.ToString(CultureInfo.InvariantCulture), bias[x].Value<string>(), datasets.ElementAt(it).Value.value[x].Count + iterator); //, bias: '{2}'
                                                iterator++;
                                            }
                                        }
                                    }
                                }

                            }
                        }
                    }

                    System.IO.File.WriteAllText(@"C:\Users\Public\CnnFeedforwardCypherQuery.txt", cypherQuery);

                    var response = string.Empty;
                    using (var client = new HttpClient())
                    {
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
                        var result = client.PostAsJsonAsync("http://" + Request.Host + "/users/createmodel", new CreateModel
                        {
                            cypherQuery = cypherQuery.TrimEnd(','),
                            user = user,
                            workspace = "99"
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

        [HttpGet("deletemodel")]
        public async Task<IActionResult> DeleteModelAsync(string workspace)
        {
            string cypherQuery = string.Format("match(n) where n.workspace = '{0}' detach delete n", workspace);
            using (var session = _driver.Session())
            {
                try
                {
                    var cursor = await session.RunAsync(cypherQuery);
                }
                catch (Exception ex)
                {
                    return BadRequest(ex);
                }
            }

            return Ok();
        }

        [HttpPost("transfermodel")]
        public async Task<IActionResult> TransferModel([FromBody] User user, string fromWorkspace, string toWorkspace)
        {
            string cypherQuery = string.Format(@"MATCH path=(doc)-[*]-()
                                                WHERE doc.workspace = '{0}'
                                                WITH doc, collect(path) as paths
                                                LIMIT 1
                                                CALL apoc.refactor.cloneSubgraphFromPaths(paths) YIELD input, output, error
                                                SET output.workspace = '{1}'
                                                WITH output
                                                RETURN output", fromWorkspace, toWorkspace);
            using (var session = _driver.Session())
            {
                try
                {
                    var cursor = await session.RunAsync(cypherQuery);
                }
                catch (Exception ex)
                {
                    return BadRequest(ex);
                }
            }

            var res = _userService.CreateModel(toWorkspace, user);
            if (res.Status == "200")
                return Ok();

            return BadRequest();
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
                                                }
                                                else if (it.Value.shape.dims[0].Value == 3 && it.Value.shape.dims[1].Value == 3)
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

                    System.IO.File.WriteAllText(@"C:\Users\Public\CnnImagenetCypherQuery.txt", cypherQuery);

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
        [HttpPost("importmnisth5model")]
        public IActionResult ImportMnistH5Model([FromBody] User user)
        {
            string cypherQuery = "CREATE"; //(`10000` :output {workspace:'101'}),
            var res = RunPython(@"h5tojson.py", "mnisth5.h5");
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
                        List<links> layers = data.groups[start].links.ToObject<List<links>>();
                        layers.RemoveAll(x => !x.title.Contains("model")); // model_weights ve optimizer_weights var, eğitilmiş modelde sadece model_weigts kullanılır

                        var datasets = ((IEnumerable<dynamic>)data.datasets)
                                .Select(x => x).ToList();

                        foreach (links layer in layers)
                        {
                            layer.layerAlias = new List<string>();
                            while (data.groups[layer.id]?.links[0]?.id != null && data.groups[layer.id]?.links.Count < 2)
                                layer.id = data.groups[layer.id]?.links[0]?.id;

                            foreach (var link in data.groups[layer.id].links)
                            {
                                layer.title = data.groups[layer.id].alias[0];
                                layer.layerAlias.Add(link.title.Value);
                            }
                        }

                        cypherQuery += string.Format("(`0` :input {{ workspace: '101', data: '0' }}),");
                        var id = 1;
                        var extraIds = 0;
                        layers[0].layerAlias.Reverse();
                        //düğüm oluşturma
                        foreach (links layer in layers)
                        {
                            foreach (string layerAlias in layer.layerAlias)
                                for (int it = 0; it < datasets.Count(); it++)
                                {
                                    if (datasets.ElementAt(it).Value.alias[0] == layer.title + "/" + layerAlias)
                                    {
                                        long matrix_x;
                                        long matrix_y;
                                        var is2dArray = false;
                                        if (datasets.ElementAt(it).Value.shape.dims.Count > 1)
                                        {
                                            matrix_x = datasets.ElementAt(it).Value.shape.dims[datasets.ElementAt(it).Value.shape.dims.Count - 2].Value;
                                            matrix_y = datasets.ElementAt(it).Value.shape.dims[datasets.ElementAt(it).Value.shape.dims.Count - 1];
                                            is2dArray = true;
                                        }
                                        else
                                        {
                                            matrix_x = datasets.ElementAt(it).Value.shape.dims[datasets.ElementAt(it).Value.shape.dims.Count - 1].Value;
                                            matrix_y = datasets.ElementAt(it).Value.shape.dims.Count;
                                            is2dArray = false;
                                        }

                                        var matrix = datasets.ElementAt(it).Value.value;

                                        for (var matrixNodeX = 0; matrixNodeX < matrix_x; matrixNodeX++)
                                        {
                                            for (var matrixNodeY = 0; matrixNodeY < matrix_y; matrixNodeY++)
                                            {
                                                if (!is2dArray)
                                                    cypherQuery += string.Format("(`{0}` :{1} {{ workspace: '101', data: {2}, x:{3}, y:{4} }}),", id, layerAlias.Replace("/", "_").Replace(":", "_"), matrix[matrixNodeX].ToString(CultureInfo.InvariantCulture), matrixNodeX, matrixNodeY);
                                                else
                                                    cypherQuery += string.Format("(`{0}` :{1} {{ workspace: '101', data: {2}, x:{3}, y:{4} }}),", id, layerAlias.Replace("/", "_").Replace(":", "_"), matrix[matrixNodeX][matrixNodeY].ToString(CultureInfo.InvariantCulture), matrixNodeX, matrixNodeY);

                                                id++;
                                            }
                                        }

                                        //middleOutput
                                        cypherQuery += string.Format("(`{0}` :{1} {{ workspace: '101', data: {2} }}),", id, "middleoutput_" + layerAlias.Replace("/", "_").Replace(":", "_"), 0);
                                        id++;
                                        extraIds++;
                                    }
                                }
                        }

                        long id2 = 0;
                        long cumulativeLayerIdCount = 0;
                        long extraLayer = 1;
                        //relationship oluşturma
                        foreach (links layer in layers)
                        {
                            foreach (string layerAlias in layer.layerAlias)
                            {
                                for (int it = 0; it < datasets.Count(); it++)
                                {
                                    if (datasets.ElementAt(it).Value.alias[0] == layer.title + "/" + layerAlias)
                                    {
                                        long matrix_x;
                                        long matrix_y;
                                        var is2dArray = false;
                                        if (datasets.ElementAt(it).Value.shape.dims.Count > 1)
                                        {
                                            matrix_x = datasets.ElementAt(it).Value.shape.dims[datasets.ElementAt(it).Value.shape.dims.Count - 2].Value;
                                            matrix_y = datasets.ElementAt(it).Value.shape.dims[datasets.ElementAt(it).Value.shape.dims.Count - 1];
                                            is2dArray = true;
                                        }
                                        else
                                        {
                                            matrix_x = datasets.ElementAt(it).Value.shape.dims[datasets.ElementAt(it).Value.shape.dims.Count - 1].Value;
                                            matrix_y = datasets.ElementAt(it).Value.shape.dims.Count;
                                            is2dArray = false;
                                        }

                                        var layerIdCount = matrix_x * matrix_y;
                                        cumulativeLayerIdCount += layerIdCount;

                                        //dağılırken bir node'dan çoğa
                                        for (var iterator = 1; iterator <= layerIdCount; iterator++)
                                            cypherQuery += string.Format("(`{0}`)-[:`related`]->(`{1}`),", id2, id2 + iterator);

                                        id2++;
                                        //toplanırken çok node'dan bire
                                        for (; id2 < cumulativeLayerIdCount + extraLayer; id2++)
                                        {
                                            cypherQuery += string.Format("(`{0}`)-[:`related`]->(`{1}`),", id2, cumulativeLayerIdCount + extraLayer);
                                        }

                                        extraLayer++;
                                    }
                                }
                            }
                        }
                    }
                    System.IO.File.WriteAllText(@"C:\Users\Public\MnistCypherQuery.txt", cypherQuery);

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
        public IActionResult Register([FromBody] User userParam)
        {
            var result = _userService.Register(userParam.Username, userParam.Password, userParam.FirstName, userParam.LastName);

            if (result.Data == null)
                return BadRequest(result);

            return Ok(result);
        }

        [HttpGet("trainbinaryperceptron")]
        public async Task<IActionResult> TrainBinaryPerceptronAsync(string model)
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
                    var cursor = await session.RunAsync(@"MATCH(i: input)
                                                          WHERE i.workspace = '" + model +
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
                    var cursor = await session.RunAsync(@"MATCH(o: output)
                                                          WHERE o.workspace = '" + model +
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
                    var cursor = await session.RunAsync(@"MATCH(s: setting)
                                                          WHERE s.workspace = '" + model +
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
                        attemptCount++;
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
                    catch (Exception ex)
                    {
                        return BadRequest(returnJson);
                    }
                }

            }
        }

        private HashSet<string> FindOrderedLayers(List<string> layers1, List<string> layers2, HashSet<string> ret)
        {
            var counter = 0;
            foreach (var layer in layers1)
            {
                if (layer == ret.Last())
                    break;
                counter++;
            }

            if (layers2.Count() > 0)
            {
                ret.Add(layers2.ElementAt(counter));
                layers2.RemoveAt(counter);
            }
            else
                return ret;

            FindOrderedLayers(layers1, layers2, ret);
            return ret;
        }

        private double[,] ChangeXtoYMatrix(double[] matrix)
        {
            double[,] result = new double[1, matrix.Count()];
            for (int i = 0; i < matrix.Count(); i++)
            {
                result[0, i] = matrix[i];
            }
            return result;
        }
        private double[,] ChangeXtoYMatrix(double[,] matrix)
        {
            double[,] result = new double[matrix.GetLength(1), 1];
            for (int i = 0; i < matrix.GetLength(1); i++)
            {
                result[i, 0] = matrix[0, i];
            }
            return result;
        }

        private double[,] MatrixSum(double[,] matrix1, double[,] matrix2)
        {
            double[,] result = new double[matrix1.GetLength(0), matrix1.GetLength(1)];
            for (int i = 0; i < matrix1.GetLength(0); i++)
            {
                for (int j = 0; j < matrix1.GetLength(1); j++)
                {
                    result[i, j] += matrix1[i, j] + matrix2[i, j];
                }
            }
            return result;
        }

        private double ReLuActivation(double input)
        {
            if (input <= 0)
                return 0;
            return input;
        }

        private double[,] MatrixMultiply(double[,] matrix1, double[,] matrix2)
        {
            double[,] result = new double[matrix1.GetLength(0), matrix2.GetLength(1)];
            for (int i = 0; i < matrix1.GetLength(0); i++)
            {
                for (int j = 0; j < matrix2.GetLength(1); j++)
                {
                    for (int k = 0; k < matrix1.GetLength(1); k++)
                    {
                        result[i, j] += ReLuActivation(matrix1[i, k] * matrix2[k, j]);
                    }
                }
            }
            return result;
        }

        private double[,] TransformNodesToMatrix(List<Entities.Neo4j.Node> nodes)
        {
            var xCount = nodes.Max(n => n.x) + 1;
            var yCount = nodes.Max(n => n.y) + 1;
            double[,] result = new double[xCount, yCount];
            for (int x = 0; x < xCount; x++)
            {
                for (int y = 0; y < yCount; y++)
                {
                    result[x, y] = nodes.First(n => n.x == x && n.y == y).data;
                }
            }
            return result;
        }

        private static double[] Softmax(double[] oSums)

        {
            double max = oSums[0];

            for (int i = 0; i < oSums.Length; ++i)

                if (oSums[i] > max) max = oSums[i];

            double scale = 0.0;

            for (int i = 0; i < oSums.Length; ++i)

                scale += Math.Exp(oSums[i] - max);

            double[] result = new double[oSums.Length];

            for (int i = 0; i < oSums.Length; ++i)

                result[i] = Math.Exp(oSums[i] - max) / scale;

            return result;

        }

        private float Sigmoid(float value)
        {
            return 1.0f / (1.0f + (float)Math.Exp(-value));
        }

        private double[] SoftMax2(double[] input)
        {
            var input_exp = input.Select(Math.Exp).ToArray();

            var sum_input_exp = input_exp.Sum();

            return input_exp.Select(i => i / sum_input_exp).ToArray();
        }

        private double[] To1DArray(double[,] input)
        {
            int size = input.Length;
            double[] result = new double[size];

            int write = 0;
            for (int i = 0; i <= input.GetUpperBound(0); i++)
            {
                for (int z = 0; z <= input.GetUpperBound(1); z++)
                {
                    result[write++] = input[i, z];
                }
            }
            return result;
        }

        [HttpPost("testmodel")]
        public async Task<IActionResult> TestModel([FromBody] TestModel testModel)
        {
            if (testModel.matrix != null && testModel.nodeDatas.Length == 0)
            {

                using (var session = _driver.Session())
                {
                    try
                    {
                        var reversedInputMatrix = ChangeXtoYMatrix(testModel.matrix);
                        var layers1 = new List<string>();
                        var layers2 = new List<string>();
                        var layers = new HashSet<string>();
                        var cursor2 = session.Run(@"CALL apoc.nodes.group(['*'],['workspace']) YIELD nodes, relationships UNWIND nodes as node UNWIND relationships as rel WITH node, rel MATCH p=(node)-[rel]->() WHERE apoc.any.properties(node).workspace = '101' RETURN node, rel, nodes(p)[1]");
                        foreach (var record in cursor2)
                        {
                            layers1.Add(record[0].As<INode>().Labels?.FirstOrDefault());
                            layers2.Add(record[2].As<INode>().Labels?.FirstOrDefault());
                        }
                        layers.Add(layers1.First());
                        layers.Add(layers2.First());
                        layers1.RemoveAt(0);
                        layers2.RemoveAt(0);
                        layers = FindOrderedLayers(layers1, layers2, layers);

                        //layers.Remove(layers.First());
                        // input kernel output bias output 
                        var iterator = 0;
                        var tempMatrix = reversedInputMatrix;

                        foreach (var layer in layers)
                        {
                            var nextNodes = new List<Entities.Neo4j.Node>();

                            var cursorRead = await session.RunAsync(@"MATCH(n:" + layer + ") " +
                                "WHERE n.workspace = '" + testModel.workspace +
                                "' RETURN n");

                            nextNodes = (await cursorRead.ToListAsync())
                                                    .Map<Node>().ToList();
                            if (iterator % 2 == 0) // yazma sırası
                            {
                                var cursorWrite = session.Run(@"MATCH(n:" + layer + " {workspace:'" + testModel.workspace + "'}) " +
                                                                    "set n.data = '" + JsonConvert.SerializeObject(tempMatrix) +
                                                                    "' return n");
                                if (layer == layers.Last())
                                {
                                    var output2 = SoftMax2(To1DArray(tempMatrix));
                                    var maxValue = output2.Min();
                                    var maxIndex = output2.ToList().IndexOf(maxValue);
                                    return Ok(maxIndex);
                                }
                            }
                            else //hesap sırası
                            {
                                var nextMatrix = TransformNodesToMatrix(nextNodes);
                                double[,] outputMatrix;

                                if (layer.Contains("bias") && !layer.Contains("output"))
                                {
                                    var reversedBiasMatrix = ChangeXtoYMatrix(tempMatrix);
                                    outputMatrix = MatrixSum(reversedBiasMatrix, nextMatrix);
                                }
                                else
                                    outputMatrix = MatrixMultiply(tempMatrix, nextMatrix);

                                tempMatrix = outputMatrix;
                            }

                            iterator++;
                        }
                    }
                    catch (Exception ex)
                    {
                        return BadRequest(ex);
                    }
                }
            }
            else
            {
                var nodeIds = new List<int>();

                //idleri topluyor.
                using (var session = _driver.Session())
                {
                    try
                    {
                        var cursor = await session.RunAsync(@"MATCH(n)
                                                          WHERE n.workspace = '" + testModel.workspace +
                                                              "' RETURN id(n)");

                        nodeIds = (await cursor.ToListAsync())
                                                                    .Map<int>().ToList();
                    }
                    catch (Exception ex)
                    {
                        return BadRequest(ex);
                    }
                }

                //önceki verileri temizliyor
                using (var session = _driver.Session())
                {
                    try
                    {
                        var cursor = session.Run(String.Format("MATCH(n) WHERE n.workspace = '{0}' SET n.data = 0", testModel.workspace));
                    }
                    catch (Exception ex)
                    {
                        return BadRequest(ex);
                    }
                }

                //giriş datalarını yazıyor.
                var it = 0;
                foreach (var id in nodeIds)
                {
                    using (var session = _driver.Session())
                    {
                        try
                        {
                            var cursor = session.Run(String.Format("Start n=NODE({0}) MATCH(n)-[r]->(n2) SET n.data = {1}", id, testModel.nodeDatas.GetValue(it)));
                        }
                        catch (Exception ex)
                        {
                            return BadRequest(ex);
                        }
                    }
                    it++;

                    if (it >= testModel.nodeDatas.Count())
                        break;
                }

                //test ediyor
                foreach (var id in nodeIds)
                {
                    using (var session = _driver.Session())
                    {
                        try
                        {
                            var readCursor = session.Run(String.Format("Start n=NODE({0}) MATCH(n)-[r]->(n2) RETURN n, r, n2", id));
                            var records = new List<IRecord>();
                            foreach (var record in readCursor)
                            {
                                records.Add(record);
                                //foreach (var recordval in record.values)
                                //{
                                //    graphjson.add(recordval);
                                //}
                            }

                            var iterator = 0;
                            foreach (var record in records)
                            {
                                var graphJson = record.Values;
                                var n2Data = graphJson.Count > 0 ? ((INode)graphJson.FirstOrDefault(x => x.Key == "n2").Value).Properties.GetValueOrDefault("data") : null;
                                var n2Label = graphJson.Count > 0 ? ((INode)graphJson.FirstOrDefault(x => x.Key == "n2").Value).Labels.FirstOrDefault() : null;
                                var n2Id = ((INode)graphJson.FirstOrDefault(x => x.Key == "n2").Value).Id;
                                var nData = graphJson.Count > 0 ? ((INode)graphJson.FirstOrDefault(x => x.Key == "n").Value).Properties.GetValueOrDefault("data") : null;
                                var rKernel = graphJson.Count > 0 ? ((IRelationship)graphJson.FirstOrDefault(x => x.Key == "r").Value).Properties.GetValueOrDefault("kernel") : null;
                                var rBias = graphJson.Count > 0 ? ((IRelationship)graphJson.FirstOrDefault(x => x.Key == "r").Value).Properties.GetValueOrDefault("bias") : null;
                                double res = 0;
                                if (n2Label == "output" && (n2Data != null || nData != null || rKernel != null || rBias != null))
                                {
                                    res = float.Parse(n2Data.ToString()) + (float.Parse(nData.ToString()) * float.Parse(rKernel.ToString()));
                                    if (iterator == records.Count - 1)
                                        res += float.Parse(rBias.ToString());
                                    res = Sigmoid((float)res);
                                }
                                else if ((n2Data != null || nData != null || rKernel != null || rBias != null))
                                {
                                    res = float.Parse(n2Data.ToString()) + (float.Parse(nData.ToString()) * float.Parse(rKernel.ToString()));
                                    if (iterator == records.Count - 1)
                                        res += float.Parse(rBias.ToString());
                                    res = Math.Tanh(res);
                                }
                                var cursor = session.Run(String.Format("Start n=NODE({0}) MATCH(n) SET n.data = {1}", n2Id, res.ToString(CultureInfo.InvariantCulture)));
                                iterator++;
                            }

                        }
                        catch (Exception ex)
                        {
                            break;
                        }
                    }
                }
            }

            return Ok();
        }

        [HttpPost("updatemodel")]
        public async Task<IActionResult> UpdateModelAsync([FromBody] UpdateModel updateModel)
        {
            var resultJson = "";

            if (updateModel.cypherQuery.Contains("workspace") && !string.IsNullOrWhiteSpace(updateModel.cypherQuery))
            {
                using (var session = _driver.Session())
                {
                    try 
                    {
                        var workspace = Regex.Match(updateModel.cypherQuery, "(?<=workspace:')(.*?)(?=\')")?.Value ?? "";
                        
                        string delCypherQuery = string.Format("match(n) where n.workspace = '{0}' detach delete n", workspace);

                        try
                        {
                            var delCursor = await session.RunAsync(delCypherQuery);
                            var insertCursor = await session.RunAsync(updateModel.cypherQuery);
                        }
                        catch (Exception ex)
                        {
                            return BadRequest(ex);
                        }

                    }
                    catch (Exception ex)
                    {
                        System.IO.File.WriteAllText(@"C:\Users\Public\CnnCypherQueryERROR.txt", ex.ToString());
                        return BadRequest(ex);
                    }
                }

                return Ok();
            }
            else
            {
                return BadRequest("Düğümlere 'workspace' özelliği eklenmemiş ya da boş cypher sorgusu ile model oluşturulmaya çalışılmıştır!");
            }
        }

        [HttpPost("createmodel")]
        public async Task<IActionResult> CreateModelAsync([FromBody] CreateModel createModel) //[FromBody] string cypherQuery
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

                var workspace = "";

                if (createModel.workspace == null)
                    workspace = Regex.Match(createModel.cypherQuery, "(?<=workspace:')(.*?)(?=\')")?.Value ?? "";
                else
                    workspace = createModel.workspace;

                var res = _userService.CreateModel(workspace, createModel.user);
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
