using System.Collections.Generic;

namespace WebApi.Entities
{
    public class Workspace
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public ICollection<UserWorkspace> UserWorkspaces { get; set; }
    }
}