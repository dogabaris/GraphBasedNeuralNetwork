using Neo4jMapper;
using System.Runtime.Serialization;

namespace WebApi.Entities.Neo4j
{
    public class Node
    {
        [NodeId]
        [IgnoreDataMember]
        public long id { get; set; }
        public string workspace { get; set; }
        public dynamic data { get; set; }
        public int x { get; set; }
        public int y { get; set; }
        public bool? expectedoutput { get; set; }
    }
}
