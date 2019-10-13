using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebApi.Entities.H5JsonModel;

namespace WebApi.Entities.H5JsonModel
{
    public class H5Model
    {
        public string apiVersion = "1.1.1";
        public KeyValuePair<string, DatasetsKey> datasets { get; set; }
        public KeyValuePair<string, GroupsKey> groups { get; set; }
        public string root { get; set; }
    }

    public class DatasetsKey
    {
        public string[] alias { get; set; }
        public creationProperties creationProperties { get; set; }
        public shape shape { get; set; }
        public type type { get; set; }
        public decimal[] value { get; set; }
    }

    public class creationProperties
    {
        public string allocTime { get; set; } = "H5D_ALLOC_TIME_LATE";
        public string fillTime { get; set; } = "H5D_FILL_TIME_IFSET";
        public layout layout { get; set; }
    }

    public class layout
    {
        public string @class { get; set; } = "H5D_CONTIGUOUS";
    }

    public class shape
    {
        public string @class { get; set; } = "H5S_SIMPLE";
        public int[] dims { get; set; }
    }

    public class type
    {
        public string @base { get; set; } = "H5T_IEEE_F32LE";
        public string @class { get; set; } = "H5T_FLOAT";
    }

    public class GroupsKey
    {
        public string[] alias { get; set; }
        public attributes[] attributes {get;set;}
    }

    public class attributes {
        public string name { get; set; }
        public shape shape { get; set; }
    }



}
