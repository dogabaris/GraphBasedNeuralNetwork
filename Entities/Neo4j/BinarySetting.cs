using Neo4jMapper;
using System.Runtime.Serialization;

namespace WebApi.Controllers
{
    public class BinarySetting
    {
        [NodeId]
        [IgnoreDataMember]
        public long id { get; set; }

        public string workspace { get; set; }
        public double learningrate { get; set; }
        public double threshold { get; set; }
    }
}