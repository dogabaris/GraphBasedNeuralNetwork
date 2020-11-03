using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebApi.Entities.ReqModels
{
    public class UpdateModel
    {
        public User user { get; set; }
        public string cypherQuery { get; set; }
        public string workspace { get; set; }
    }
}
