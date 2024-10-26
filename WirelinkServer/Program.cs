using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

using ConsoleLogger;
using WireLink;

namespace socketTesting
{
    class unknownThreadException : Exception
    {
        public unknownThreadException() { }
        public unknownThreadException(string message)
        : base(message) { }
    }
    class Setup
    {
        public static void Main(string[] args)
        {
#if UNITY
            Logger.WriteLine("hi!");
#else
            Logger.WriteLine("hi number 2");
#endif
            if(args.Length > 0)
            {
                if (args[0] == "Server" || args[0] == "server")
                {
                    Logger.WriteLine("launching server");
                    Server.instance.Start();
                }
                else if (args[0] == "Client" || args[0] == "client")
                {
                    Logger.WriteLine("launching client");
                    Client.instance.Start();
                }
            }
        }
    }
    
    class Client
    {
        static public Client instance = new Client();
        public void Start()
        {
            Socket clientToUnlockAccept = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint ep2 = new IPEndPoint(IPAddress.Loopback, 45707);
            clientToUnlockAccept.Blocking = true;
            clientToUnlockAccept.Connect(ep2);

            Logger.WriteLine("connection success");
            Logger.WriteLine($"{clientToUnlockAccept.Available}");
            Logger.WriteLine($"{clientToUnlockAccept.Connected}");
            Logger.WriteLine($"{clientToUnlockAccept.RemoteEndPoint}");

            while(true);
        }
    }
    class Server
    {
        public static Server instance = new Server();

        Thread? readInputThread;
        Thread? packetHandlerThread;

        public void Start()
        {
            InitThreads();
            PacketHandler.instance.handleLoopExitsCallBack.Add(() => { handleLoopExits(); } );
        }
        void InitThreads()
        {
            Logger.WriteLine("starting readInputThread");
            readInputThread = new Thread(() => { readInputLoop(); });
            readInputThread.Start();

            Logger.WriteLine("starting PacketHandler thread");
            packetHandlerThread = new Thread(() => { PacketHandler.instance.Start(); handleLoopExits([1]); });
            packetHandlerThread.Start();
        }
        int readInputLoop()
        {
            bool shouldReadInputThreadRun = true;
            //Logger.WriteLine("accepting input");
            while(shouldReadInputThreadRun)
            {
                string? result = Logger.ReadLine();
                if(result == null) { Logger.WriteLine("input invalid, please input a valid string"); continue; }
                switch(result)
                {
                    case "stop":
                    case "exit":
                        shouldReadInputThreadRun = false;
                        PacketHandler.instance.Stop();
                        return 0;
                    case "accept":
                        break;
                    default:
                        continue;
                }
            }
            return 1;
        }
        void handleLoopExits(int[]? args = null)
        {
            if(args == null) { args = [0]; }
            
            if(readInputThread != null) { readInputThread.Join(); }
            
            if(args[0] == 1 && packetHandlerThread != null) { packetHandlerThread.Join(); }
            return;
        }
    }
}