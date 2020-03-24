using Neo4jClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebApi.Helpers
{
    public class Neo4JHelper
    {
        public Neo4JHelper()
        { }

        public static GraphClient ConnectDb()
        {
            var client = new GraphClient(new Uri("http://localhost:7474/db/data"), "neo4j", "password");
            client.Connect();
            Console.WriteLine(client.IsConnected ? "Neo4j DB Connected!" : "Neo4j DB Not Connected!!!");
            return client;
        }
    }
}
