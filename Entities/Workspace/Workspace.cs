using System.Collections.Generic;

namespace WebApi.Entities
{
    public class Workspace
    {
        public int Id { get; set; }
        public string Name { get; set; }
        //navigation
        public ICollection<User> Users { get; set; }
    }
}