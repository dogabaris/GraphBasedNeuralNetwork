namespace WebApi.Entities.AppModels
{
    public class Result<T>
    {
        public string Status { get; set; }
        public string Message { get; set; }
        public T Data { get; set; }
    }
}
