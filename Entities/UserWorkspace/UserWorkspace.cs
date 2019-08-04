namespace WebApi.Entities
{
    public class UserWorkspace
    {
        public int UserId { get; set; }
        public User User { get; set; }

        public int WorkspaceId { get; set; }
        public Workspace Workspace { get; set; }
    }
}
