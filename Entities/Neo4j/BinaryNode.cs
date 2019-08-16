using Neo4jMapper;
using System.Runtime.Serialization;

namespace WebApi.Entities.Neo4j
{
    public class BinaryNode
    {
        [NodeId]
        [IgnoreDataMember]
        public long id { get; set; }

        public string workspace { get; set; }
        public double data { get; set; }
        public bool? expectedoutput { get; set; }
    }
}
