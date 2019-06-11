using System.Threading.Tasks;

namespace WebsocketServer
{
    class Program
    {
        static int Main(string[] args)
        {
            Server Server = new Server("127.0.0.1", 1234);
            if (!Server.Start())
                return -1;

            Task ListenTask = Task.Run(() => Server.ListenForAndAccecptClients());
            Task MonitorTask = Task.Run(() => Server.MonitorAndManageClients());

            while (!ListenTask.IsCompleted && !MonitorTask.IsCompleted) ;
            return 1;
        }
    }
}
