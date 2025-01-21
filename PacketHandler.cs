using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using ConsoleLogger;
using MessagePack;

namespace WireLink
{
    internal enum ServerType
    {
        Client,
        Server,
    }
    internal static class GuidCodes
    {
        public static readonly Guid All = Guid.NewGuid();
        public static readonly Guid AllButOne = Guid.NewGuid();
    }
    [MessagePackObject(AllowPrivate = true)]
    internal struct NetworkData
    {
        [Key(0)]
        public long messageId;
        [Key(1)]
        public int dataType;
        [Key(2)]
        public byte[] message;

        public NetworkData(long messageId, int dataType, byte[] message)
        {
            this.messageId = messageId;
            this.dataType = dataType;
            this.message = message;
        }
    }

    class unknownThreadException : Exception
    {
        public unknownThreadException() { }
        public unknownThreadException(string message)
        : base(message) { }
    }
    internal class PacketHandler
    {
        /// <summary>
        /// the main packetHandler intance
        /// </summary>
        public static PacketHandler instance = new PacketHandler();
        SocketHepler mainTcpSocket = new SocketHepler();
        Dictionary<Guid, SocketHepler>? clientSockets;
        List<Thread>? clientSockethreads;
        bool run = true;

        /// <summary>
        /// the main port for the server, ie the port the server listens for clients on
        /// </summary>
        public int mainServerListiningPort = 45707;
        public int mainClientListiningPort = 45706;

        /// <summary>
        /// the target updates per second of the main loop, which is responsible for packets, dataCompression, connections, etc
        /// </summary>
        public int targetUpdatesPerSecond = 2;

        Thread? acceptConectionThread;

        // all private methods
        private void InitServerLoops()
        {
            Logger.WriteLine("starting acceptConnectionLoop");
            acceptConectionThread = new Thread(() => { acceptConnectionThread(); });
            acceptConectionThread.Start();
        }
        private void InitClientLoops()
        {

        }

        private bool ConnectToServer()
        {
            mainTcpSocket.Connect();
            bool isVerified = mainTcpSocket.verifyServerConnection();

            if(!isVerified) { Logger.WriteLine("couldn't verify server connection"); return false; }

            

            return true;
        }

        /* /// <summary>
        /// a deligate being called on server shutdown
        /// </summary>
        public List<Action> handleLoopExitsCallBack = new List<Action>(); */
        private void HandleLoopExits()
        {
            if(_serverType == ServerType.Server)
            {
                shouldAcceptConnectionThreadRun = false;
                if(acceptConectionThread != null) { acceptConectionThread.Join(); Logger.WriteLine("acceptConectionThread succesfully shut down"); }
            }

            /* foreach(Action action in handleLoopExitsCallBack)
            {
                action.Invoke();
            } */

            return;
        }

        // |||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        //  fffff   i   x   x   eeeee   dddd        u   u   pppp    dddd        a     ttttttt eeeee
        //  f            x x    e       d   d       u   u   p   p   d   d      a a       t    e
        //  fff     i     x     eee     d   d       u   u   pppp    d   d     aaaaa      t    eee
        //  f       i    x x    e       d   d       u   u   p       d   d    a     a     t    e
        //  f       i   x   x   eeeee   dddd        uuuuu   p       dddd    a       a    t    eeeee
        // |||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||

        private void startFixedUpdate()
        {
            heartBeatTimer.Start();
        }
        /// <summary>
        /// a deligate being called targetUpdatesPerSecond times every second
        /// </summary>
        public List<Action> FixedUpdateCallBack = new List<Action>();
        int incementer = 0;
        Stopwatch heartBeatTimer = new Stopwatch();

        // i dont know what this abomination is, but it might just work
        Queue<NetworkData>? ClientSendQueue;
        Queue<(Guid Client, NetworkData)>? ServerSendQueue;
        Queue<(Guid[] Clients, NetworkData)>? ServerSendToManyQueue;
        Queue<NetworkData>? ServerSendToAllQueue;
        Queue<(Guid exeption, NetworkData)>? ServerSendToAllButOneQueue;

        private void FixedUpdate(float deltaTime)
        {
            // incementer++;
            // if(incementer > 10)
            // {
            //     incementer = 0;
            //     //Logger.WriteLine("test", true);
            // }

            if(heartBeatTimer.ElapsedMilliseconds > 500 && _serverType == ServerType.Server)
            {
                heartBeatTimer.Restart();
                sendHeartBeat();
            }
        }
        private void StartMainLoop()
        {
            MainLoopThread = new Thread(MainLoop);
            MainLoopThread.Start();
        }
        readonly long ticksPerSecond = Stopwatch.Frequency;
        readonly long ticksPerMilisecond = Stopwatch.Frequency / 1000;
        private Thread? MainLoopThread;
        private void MainLoop()
        {
            bool isReadyForShutdown = false;

            Stopwatch stopwatch = new Stopwatch();
            float milisecondsPerUpdate = 1000f / targetUpdatesPerSecond;

            //float exessTime = 0f;

            Queue<float> avrDeltaTimeQueue = new();
            float maxQueueSaveDuration = 8f;
            float maxQueueSaveCount = (float)targetUpdatesPerSecond * maxQueueSaveDuration;

            float avrDeltaTime = 0f;
            float maxDeltaTime = 0f;
            float minDeltaTime = 1f;

            startFixedUpdate();

            Logger.WriteLine($"running main loop at a Frequency of {targetUpdatesPerSecond} updates per second, timer has a reselution of {ticksPerSecond} ticks per seconds");
            stopwatch.Start();
            long currentTimeStartThisIteration = stopwatch.ElapsedTicks - (long)(1f / targetUpdatesPerSecond * ticksPerSecond); // the last part is added to trick deltaTime into thinking the correct time has passed since last update
            long currentDeltaTime = currentTimeStartThisIteration;
            long timeAfterLastIteration = stopwatch.ElapsedTicks;

            while(run || !isReadyForShutdown)
            {
                currentTimeStartThisIteration = stopwatch.ElapsedTicks;

                milisecondsPerUpdate = 1000f / targetUpdatesPerSecond;

                if(!run)
                {
                    Logger.WriteLine("shutting down main loop");
                    HandleLoopExits();
                    isReadyForShutdown = true;
                    continue;
                }

                //get deltatime in seconds since last update
                float deltaTime = (stopwatch.ElapsedTicks - currentDeltaTime) / (float)ticksPerSecond;
                currentDeltaTime = stopwatch.ElapsedTicks;
                //logger.WriteLine($"elapsedTicks: {stopwatch.ElapsedTicks}, currentTime: {currentTime}, ticksPerSecond: {ticksPerSecond} result: {deltaTime}");
                FixedUpdate(deltaTime);
                
                foreach(Action action in FixedUpdateCallBack)
                {
                    action.Invoke();
                }

                // get deltatime statistics
                avrDeltaTimeQueue.Enqueue(deltaTime);
                
                float avrBuffer = 0f;
                int avrCount = 0;

                //if the buffer is bigger than the max amount of values permited, remove 1 till its within the allowed limit 
                if(avrDeltaTimeQueue.Count > maxQueueSaveCount)
                {
                    for(int i = avrDeltaTimeQueue.Count; i > maxQueueSaveCount; i--)
                    {
                        avrDeltaTimeQueue.Dequeue();
                    }
                }

                //count up each itteration
                foreach(float value in avrDeltaTimeQueue)
                {
                    avrCount++;
                    avrBuffer += value;
                }

                // (the rest of mainloop) calculate and wait for the remaining time to forfill targetUpdatesPerSecond

                //compute the avarage
                avrDeltaTime = avrBuffer / avrCount;

                //check if current deltatime is bigger or smaller than the current max and min deltatime
                if(deltaTime > maxDeltaTime) { maxDeltaTime = deltaTime; }
                if(deltaTime < minDeltaTime) { minDeltaTime = deltaTime; }
                //Logger.WriteLine($"deltaTime is {deltaTime}, avr deltaTime in the last {avrDeltaTimeQueue.Count / (float)targetUpdatesPerSecond} seconds is: {avrDeltaTime}, with a target deltaTime of {1f/targetUpdatesPerSecond}, max deltaTime is: {maxDeltaTime} and min deltaTime is: {minDeltaTime}");


                long currenTime = stopwatch.ElapsedTicks;

                //get the ticks elapsed this iteration
                long elapsedTicksSinceLastIteration = currenTime - timeAfterLastIteration;
                //how many miliseconds there are leftover this iteration, goes to negative if it takes more than milisecondsPerUpdate
                float milisecondsRemaining = milisecondsPerUpdate - (elapsedTicksSinceLastIteration / (float)ticksPerMilisecond);

                // keeps track of the time it needs to get back on track
                //exessTime = Math.Min(0, exessTime + extraMilisecondsThisIteration);

                //float elapsedSeconds = elapsedTicksSinceLastIteration / (float)ticksPerSecond;

                //float milisecondsRemaining = Math.Max(0, milisecondsPerUpdate - (elapsedSeconds * 1000));
                
                int sleepTime = (int)Math.Round(milisecondsRemaining);

                long sleepTimer = stopwatch.ElapsedTicks;

                //Logger.WriteLine($"milisecondsRemaining is: {milisecondsRemaining} and sleepTime is: {sleepTime - 1}");

                if(sleepTime > 1)
                {
                    //sleeps for the extra time this iteration, minus a bit to give the operating system time to regive control to the program
                    Thread.Sleep(sleepTime - 1);
                }

                // waits for the remaing time caused by low sleep precision
                while((stopwatch.ElapsedTicks - sleepTimer) / ticksPerMilisecond < milisecondsRemaining) { }
                
                timeAfterLastIteration = stopwatch.ElapsedTicks;
            }
            
            Logger.WriteLine("exiting main loop");
        }
        private bool isConnectionValid(Socket socket)
        {
            return true;
        }
        bool shouldAcceptConnectionThreadRun = true;
        private int acceptConnectionThread()
        {
            bool resultBuffer;
            resultBuffer = mainTcpSocket.Listen(mainServerListiningPort);
            if(resultBuffer == false) { Logger.WriteLine("listen function ran into an error an has returned"); return 14; }
            int totalClientAcceptAttempts = 0;
            int failedAccepts = 0;
            while (shouldAcceptConnectionThreadRun)
            {
                //throws an eception if it failed to accept a connection too many times
                if(failedAccepts >= 10) { throw new unknownThreadException("[acceptConnectionThread] ran into to many errors in a row and quit"); }
                if(mainTcpSocket.isTerminated) { Logger.WriteLine("[acceptConnectionThread] accept connection thread has shoudown"); return -1; }
                
                //accept the next connection
                Socket? tempSocket = null;
                try
                {
                    tempSocket = mainTcpSocket.Accept();
                }
                // socket was closed
                catch (SocketException) { Logger.WriteLine("[acceptConnectionThread] accept socket was shutdown", true, 4); break; }
                
                //if the connection is invalid, increase failedAccept value
                if( tempSocket == null || !isConnectionValid(tempSocket) ) { failedAccepts++; continue; }

                // reset and increment values
                failedAccepts = 0;
                totalClientAcceptAttempts += 1;

                // log
 
                // handles the new socket
                Task handleNewConnectionTask = Task.Run(() => {HandleNewConnection(tempSocket); });
                Logger.WriteLine($"[acceptConnectionThread] accepted {totalClientAcceptAttempts} clients so far!", true, 3);
            }
            return 0;
        }
        private void HandleNewConnection(Socket socket)
        {
            if(clientSockets == null)
            {
                clientSockets = new Dictionary<Guid, SocketHepler>();
            }

            Guid clientGuid = Guid.NewGuid();

            //initialize the socketHelper value
            SocketHepler openClientSocket = new SocketHepler(socket, clientGuid);

            Logger.WriteLine("attempting to verify connection");

            // allow the client to start recieving
            Thread.Sleep(10);

            //test the connection with the new socket
            if(!openClientSocket.verifyClientConnection()) { Logger.WriteLine("connection is invalid"); openClientSocket.Terminate(); return; }

            Logger.WriteLine("connection verified");

            openClientSocket.terminateDeligate.Add(TerminateSingleClient);

            //add the socket to the list of clients
            clientSockets.Add(clientGuid, openClientSocket);

            return;
        }
        private void TerminateClients()
        {
            if(clientSockets == null)
            {
                clientSockets = new Dictionary<Guid, SocketHepler>();
            }

            Logger.WriteLine("starting termination of Clients", true);

            Guid[] ids = clientSockets.Keys.ToArray();

            foreach(Guid id in ids)
            {
                TerminateSingleClient(id);
            }
        }

        private void TerminateSingleClient(Guid clientGuid)
        {
            if(clientSockets == null)
            {
                return;
            }

            if(!clientSockets[clientGuid].isTerminated) { clientSockets[clientGuid].Terminate(); }

            clientSockets.Remove(clientGuid);
            Logger.WriteLine("client terminated", true, 4);
        }
        private void initServerValues()
        {
            clientSockethreads = new List<Thread>();
            clientSockets = new Dictionary<Guid, SocketHepler>();
            
            ServerSendQueue = new Queue<(Guid Client, NetworkData)>();
            ServerSendToAllQueue = new Queue<NetworkData>();
            ServerSendToAllButOneQueue = new Queue<(Guid exeption, NetworkData)>();
            ServerSendToManyQueue = new Queue<(Guid[] Clients, NetworkData)>();
        }

        void sendHeartBeat()
        {
            if(clientSockets != null)
            {
                foreach(SocketHepler socket in clientSockets.Values)
                {
                    socket.sendHeartBeat();
                }
            }
        }

        private bool isExeptionHAndlerAttached = false;

        // |||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        // PPPP   U   U  BBBBB   L      III  CCCC       M   M  EEEEE  TTTTTTT  H   H  OOO   DDDD   SSSS
        // p   p  U   U  B    B  L       I   C          MM MM  E         T     H   H O   O  D   D S
        // P   P  U   U  B    B  L       I   C          MM MM  E         T     H   H O   O  D   D S
        // PPPP   U   U  BBBBB   L       I   C          M M M  EEEE      T     HHHH  O   O  D   D  SSS
        // P      U   U  B    B  L       I   C          M   M  E         T     H   H O   O  D   D     S
        // P      U   U  B    B  L       I   C          M   M  E         T     H   H O   O  D   D     S
        // P       UUU   BBBBB   LLLLL  III  CCCC       M   M  EEEEE     T     H   H  OOO   DDDD  SSSS
        // |||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||
        // |||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||||

        /// <summary>
        /// the time in ms between each heartbeat
        /// </summary>
        public float heartBeatInterval = 500;

        private ServerType? _serverType = null;
        internal ServerType? CurrentServerType
        {
            get { return _serverType; }
        }
        /// <summary>
        /// the adress if the server. doesnt do anything of ServerType is Server.
        /// </summary>
        public IPEndPoint? serverAdress = null;

        public int StartServer()
        {
            _serverType = ServerType.Server;

            if(!isExeptionHAndlerAttached)
            {
                Logger.WriteLine("attaching exeptionhandler", true);
                AppDomain currentDomain = AppDomain.CurrentDomain;
                currentDomain.UnhandledException += new UnhandledExceptionEventHandler(CleanUp);
                isExeptionHAndlerAttached = true;
            }

            initServerValues();

            mainTcpSocket = new SocketHepler(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

            InitServerLoops();
            
            Thread.Sleep(2);

            StartMainLoop();
            
            return 0;
        }
        public static readonly IPEndPoint emptyIPEndPoint = new IPEndPoint(IPAddress.Any, 0);
        /// <summary>
        /// starts a client and connects to a server
        /// </summary>
        /// <param name="serverAdress">the ip and port to connect to</param>
        /// <returns></returns>
        public int StartClient(IPEndPoint serverAdress)
        {
            if(serverAdress == emptyIPEndPoint)
            {
                return 2;
            }

            _serverType = ServerType.Client;

            if(!isExeptionHAndlerAttached)
            {
                Logger.WriteLine("attaching exeptionhandler", true);
                AppDomain currentDomain = AppDomain.CurrentDomain;
                currentDomain.UnhandledException += new UnhandledExceptionEventHandler(CleanUp);
                isExeptionHAndlerAttached = true;
            }

            ClientSendQueue = new Queue<NetworkData>();

            InitClientLoops();
            
            Thread.Sleep(2);

            mainTcpSocket.Init(serverAdress);

            bool isConnected = ConnectToServer();

            if(!isConnected) { return 13; }

            StartMainLoop();
            
            return 0;
        }
        public void Stop()
        {
            run = false;

            // it is being done in handleLoopExits but is included here for completenes
            shouldAcceptConnectionThreadRun = false;


            Logger.WriteLine("closing local socket");

            //terminate the listening socket
            mainTcpSocket.Terminate();
            
            Task terminateClients = Task.Factory.StartNew(() => { TerminateClients(); Logger.WriteLine("TerminateClients completed", true); });

            terminateClients.Wait();

            Logger.WriteLine("Stop Function returning", true);
            return;
        }
        public void Restart()
        {
            //stop the current instance
            Stop();

            //reinit all values
            run = true;
            //handleLoopExitsCallBack = new List<Action>();
            FixedUpdateCallBack = new List<Action>();
            incementer = 0;

            if(_serverType == ServerType.Server)
            {
                shouldAcceptConnectionThreadRun = true;
                //start the server
                StartServer();
            }
            if(_serverType == ServerType.Client)
            {
                StartClient(serverAdress ?? emptyIPEndPoint);
            }
        }
        /// <summary>
        /// send a message to the server
        /// </summary>
        /// <param name="messageId">a id that can be used to identify the recieving code</param>
        /// <param name="dataType">a hash of the c# type that is sent</param>
        /// <param name="message">the byte message to send</param>
        public void sendMessage(NetworkData data)
        {
            if(_serverType == ServerType.Server) { throw new InvalidOperationException("you cannot call sendMessage when ServerType is Server"); }
            if(_serverType == null || ClientSendQueue == null) { throw new InvalidOperationException("you cannot call sendMessage without initializing the server"); }

            ClientSendQueue.Enqueue(data);
        }
        /// <summary>
        /// sends the message to all clients
        /// </summary>
        /// <param name="message">the byte message to send</param>
        public void sendMessageToAllClients(NetworkData data)
        {
            if(_serverType == ServerType.Client) { throw new InvalidOperationException("you cannot call sendMessageToAllClients when ServerType is Client"); }
            if(_serverType == null || ServerSendToAllQueue == null) { throw new InvalidOperationException("you cannot call sendMessageToAllClients without initializing the server"); }

            ServerSendToAllQueue.Enqueue(data);
        }
        /// <summary>
        /// sends a message to a specefic client.
        /// </summary>
        /// <param name="message">the byte message to send</param>
        /// <param name="guid">the guid of the client the message should be send to</param>
        public void sendMessageToClient(Guid clientGuid, NetworkData data)
        {
            if(_serverType == ServerType.Client) { throw new InvalidOperationException("you cannot call sendMessageToClient when ServerType is Client"); }
            if(_serverType == null || ServerSendQueue == null) { throw new InvalidOperationException("you cannot call sendMessageToClient without initializing the server"); }

            ServerSendQueue.Enqueue((clientGuid, data));
        }

        /// <summary>
        /// sends a message to an array of client.
        /// </summary>
        /// <param name="message">the byte message to send</param>
        /// <param name="guids">the guids of the clients to send the messages to</param>
        public void sendMessageToMultipleClients(Guid[] clientGuids, NetworkData data)
        {
            if(_serverType == ServerType.Client) { throw new InvalidOperationException("you cannot call sendMessageToMultipleClients when ServerType is Client"); }
            if(_serverType == null || ServerSendToManyQueue == null) { throw new InvalidOperationException("you cannot call sendMessageToMultipleClients without initializing the server"); }
        
            ServerSendToManyQueue.Enqueue((clientGuids, data));
        }

        /// <summary>
        /// sends a message to all but one client.
        /// </summary>
        /// <param name="message">the byte message to send</param>
        /// <param name="guid">the guid of the client the message shouldt be send to</param>
        public void sendMessageToAllButOne(Guid clientGuid, NetworkData data)
        {
            if(_serverType == ServerType.Client) { throw new InvalidOperationException("you cannot call sendMessageToAllButOne when ServerType is Client"); }
            if(_serverType == null || ServerSendToAllButOneQueue == null) { throw new InvalidOperationException("you cannot call sendMessageToAllButOne without initializing the server"); }
        
            ServerSendToAllButOneQueue.Enqueue((clientGuid, data));
        }

        void CleanUp(object sender, UnhandledExceptionEventArgs args)
        {
            //Console.Clear();

            Exception e = (Exception) args.ExceptionObject;
            Logger.WriteLine("CleanUp caught : " + e.Message);
            Logger.WriteLine($"Runtime terminating: {args.IsTerminating}");

            try
            {
                Logger.WriteLine("attemting cleanup");
                Logger.ResetColor();
                Console.TreatControlCAsInput = false;

                Logger.WriteLine("stopping server, caused by cleanup", true, 4);

                Stop();
                
                Logger.WriteLine("full cleanup succesfull");
            }
            catch
            {
                Logger.WriteLine("full cleanup failed, attempting partial cleanup");
                try
                {
                    TerminateClients();
                }
                catch
                {
                    Logger.WriteLine("cleanup failed completly");
                }
            }
            Console.Out.Flush();
        }

        public void SendTerminate()
        {
            mainTcpSocket.SendTerminate();
        }
    }
}