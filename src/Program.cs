namespace WebsocketServer
{
    class Program
    {
        static void Main(string[] args)
        {
            Server Server = new Server("127.0.0.1", 1337);
            Server.Start();
        }
    }
}
