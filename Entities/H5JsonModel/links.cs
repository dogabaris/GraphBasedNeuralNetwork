using Newtonsoft.Json;
using System.Collections.Generic;

namespace WebApi.Entities.H5JsonModel
{
    public class links
    {
        public string @class { get; set; }
        public string collection { get; set; }
        public string id { get; set; }
        public string title { get; set; }
        //custom
        public List<string> layerAlias { get; set; }
    }
}
