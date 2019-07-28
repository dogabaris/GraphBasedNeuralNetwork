using Neo4jMapper;
using System.Runtime.Serialization;

namespace WebApi.Entities.Neo4j
{
    public class Node
    {
        [NodeId]
        [IgnoreDataMember]
        public long id { get; set; }

        public string name { get; set; }
        public string data { get; set; }
    }
}
