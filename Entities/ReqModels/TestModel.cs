using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebApi.Entities.ReqModels
{
    public class TestModel
    {
        public User user { get; set; }
        public string workspace { get; set; }
        public int[] nodeDatas { get; set; }
        public double[] matrix { get; set; }
}
}
