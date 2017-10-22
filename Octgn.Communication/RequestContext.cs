namespace Octgn.Communication
{

    public class RequestContext
    {
        public IConnection Connection { get; set; }
        public string UserId { get; set; }
        public Server Server { get; set; }
        public Client Client { get; set; }
    }
}
