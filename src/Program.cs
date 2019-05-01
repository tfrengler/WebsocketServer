namespace WebsocketServer
{
    class Program
    {
        static void Main(string[] args)
        {
            WebsocketListener server = new WebsocketListener("127.0.0.1", 1324);
            server.Start();
        }
    }
}
