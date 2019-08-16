using Neo4j.Driver.V1;
using Neo4jClient;
using Neo4jMapper;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebApi.Entities.Neo4j;
using WebApi.Helpers;

namespace WebApi.Trainers
{
    public class BinaryPerceptron
    {
        public double LearningRate { set; get; }

        public double[] Weights { set; get; }

        public double Threshold { set; get; }

        public string Workspace { get; set; }

        public GraphClient Client { get; set; }

        public IDriver _driver;

        public BinaryPerceptron(IDriver driver, double[] weights, string workspace, double learningRate = 0.1, double threshold = 0.5)
        {
            _driver = driver;
            Client = Neo4JHelper.ConnectDb();
            Weights = weights;
            LearningRate = learningRate;
            Threshold = threshold;
            Workspace = workspace;
        }

        public bool GetResult(params double[] inputs)
        {
            if (inputs.Length != Weights.Length)
                throw new ArgumentException("Invalid number of inputs. Expected: " + Weights.Length);

            // perceptronun outputunu hesaplar ve threshold ile bool değer döner
            return DotProduct(inputs, Weights) > Threshold;
        }

        public async Task RefreshWeightsAsync()
        {
            using (var session = _driver.Session())
            {
                Weights = Client
                        .Cypher
                        .Match("(i:input {workspace:'" + Workspace + "'})-[r:related]-(h:hidden)")
                        .Return((r) =>
                            new
                            {
                                Weight = r.As<Weight>()
                            })
                        .Results.Select(x => x.Weight.weight).ToArray();
            }
        }

        public async Task<bool> LearnAsync(bool expectedResult, List<BinaryNode> inputs)
        {
            // get the result
            var inputArray = inputs.Select(inp => inp.data).ToArray();
            bool result = GetResult(inputArray);

            // if the result does not match expected
            if (result != expectedResult)
            {
                // calculate error (need to convert boolean to a number)
                double error = (expectedResult ? 1 : 0) - (result ? 1 : 0);
                foreach (var input in inputs)
                {
                    using (var session = _driver.Session())
                    {
                        var parameters = new Dictionary<string, object>
                        {
                          {"updatedWeight", LearningRate * error * input.data}
                        };

                        try
                        {
                            var cursor = session.Run(@"Start i=NODE(" + input.id + ") " +
                            "MATCH(i:input)-[r]-(h:hidden) " +
                            "SET r.weight = r.weight + $updatedWeight " +
                            "RETURN i,r,h", parameters);

                            await RefreshWeightsAsync();
                        }
                        catch(Exception ex)
                        {
                            break;
                        }
                    }
                }

            }
            return result;
        }

        private double DotProduct(double[] inputs, double[] weights)
        {
            return inputs.Zip(weights, (value, weight) => value * weight).Sum();
        }
    }
}
