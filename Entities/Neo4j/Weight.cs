using Neo4jMapper;
using System.Runtime.Serialization;

namespace WebApi.Trainers
{
    public class Weight
    {
        [IgnoreDataMember]
        public long id { get; set; }
        public double weight { get; set; }
    }
}