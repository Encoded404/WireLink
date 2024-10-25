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

        int[] loopStatuses = [];
        bool[] runningLoops = [];
        Thread? readInputThread;
        Thread? packetHandlerThread;

        public void Start()
        {
            InitThreads();
            PacketHandler.instance.handleLoopExitsCallBack.Add(() => { return handleLoopExits(ref runningLoops); } );
        }
        void InitThreads()
        {
            loopStatuses = new int[] {-1, -1};
            runningLoops = new bool[] {true, true};

            Logger.WriteLine("starting readInputThread");
            readInputThread = new Thread(() => { loopStatuses[0] = readInputLoop(); });
            readInputThread.Start();

            Logger.WriteLine("starting PacketHandler thread");
            packetHandlerThread = new Thread(() => { loopStatuses[1] = PacketHandler.instance.Start(); handleLoopExits(ref runningLoops, [1]); });
            packetHandlerThread.Start();
        }
        int readInputLoop()
        {
            bool shouldReadInputThreadRun = true;
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
        bool handleLoopExits(ref bool[] runningLoops, int[]? args = null)
        {
            if(args == null) { args = [0]; }
            if(runningLoops[0] && loopStatuses[0] != -1)
            {
                if(loopStatuses[0] != 0) 
                {
                    Logger.WriteLine($"error in readInputLoop, code: {loopStatuses[0]}");
                    throw new unknownThreadException("unknow thread error in readInputLoop");
                }
                runningLoops[0] = false;
                Logger.WriteLine("readInputLoop shut down succesfully");
            }
            bool allShutDown = false;
            for (int i = 0; i < runningLoops.Length; i++)
            {
                if(i == 1) { continue; }
                allShutDown = allShutDown || runningLoops[i] == false;
            }
            if (allShutDown && args[0] == 1)
            {
                //Logger.WriteLine("checking if packetHandlerThread is shut down");
                if(runningLoops[1] && loopStatuses[1] != -1)
                {
                    if(loopStatuses[1] != 0) 
                    {
                        Logger.WriteLine($"error in PacketHandlerThread, code: {loopStatuses[1]}");
                        throw new unknownThreadException("unknow thread error in PacketHandlerThread");
                    }
                    runningLoops[1] = false;
                    Logger.WriteLine("PacketHandlerThread shut down succesfully");
                }
            }

            bool returnValue = false;
            for(int i = 0; i < runningLoops.Length; i++)
            {
                if(i == 1)
                {
                    if(!allShutDown || args[0] != 1)
                    {
                        continue;
                    }
                }
                returnValue = runningLoops[i] || returnValue;
            }
            return returnValue;
        }
    }
}