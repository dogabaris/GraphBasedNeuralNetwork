using Neo4jMapper;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace WebApi.Entities.Neo4j
{
    public class NeuralNetworkModel
    {
        [NodeId]
        [IgnoreDataMember]
        public long id { get; set; }
        public List<Node> node { get; set; }
    }
}
