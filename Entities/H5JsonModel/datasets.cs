using System.Collections.Generic;

namespace WebApi.Entities.H5JsonModel
{

    public class Layout
    {
        public string @class { get; set; }
    }

    public class CreationProperties
    {
        public string allocTime { get; set; }
        public string fillTime { get; set; }
        public Layout layout { get; set; }
    }

    public class Shape
    {
        public string @class { get; set; }
        public List<int> dims { get; set; }
    }

    public class Type
    {
        public string @base { get; set; }
        public string @class { get; set; }
    }

    public class datasets
    {
        public List<string> alias { get; set; }
        public CreationProperties creationProperties { get; set; }
        public Shape shape { get; set; }
        public Type type { get; set; }
        public List<List<double>> value { get; set; }
    }
}
