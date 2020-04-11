using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebApi.Entities.Neo4j
{
    public class Layer
    {
        public int Id { get; set; }
        public List<string> Labels { get; set; }
        public List<Pair> Properties { get; set; }
    }

    public class Pair
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }
}
