using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using ConsoleLogger;

namespace WireLink
{
    public enum ServerType
    {
        Client,
        Server,
    }
    class unknownThreadException : Exception
    {
        public unknownThreadException() { }
        public unknownThreadException(string message)
        : base(message) { }
    }
    public class PacketHandler
    {
        /// <summary>
        /// the main packetHandler intance
        /// </summary>
        public static PacketHandler instance = new PacketHandler();
        SocketHepler mainSocket = new SocketHepler();
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
            mainSocket.Connect();
            bool isVerified = mainSocket.verifyServerConnection();

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
        private void FixedUpdate(float deltaTime)
        {
            incementer++;
            if(incementer > 10)
            {
                incementer = 0;
                //Logger.WriteLine("test", true);
            }

            if(heartBeatTimer.ElapsedMilliseconds > 500 && ServerType == ServerType.Server)
            {
                heartBeatTimer.Restart();
                sendHeartBeat();
            }
        }
        readonly long ticksPerSecond = Stopwatch.Frequency;
        readonly long ticksPerMilisecond = Stopwatch.Frequency / 1000;
        private void mainLoop()
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
            resultBuffer = mainSocket.Listen(mainServerListiningPort);
            if(resultBuffer == false) { Logger.WriteLine("listen function ran into an error an has returned"); return 14; }
            int totalClientAcceptAttempts = 0;
            int failedAccepts = 0;
            while (shouldAcceptConnectionThreadRun)
            {
                //throws an eception if it failed to accept a connection too many times
                if(failedAccepts >= 10) { throw new unknownThreadException("acceptConnectionThread ran into to many errors in a row and quit"); }
                
                //accept the next connection
                Socket? tempSocket = null;
                try
                {
                    tempSocket = mainSocket.Accept();
                }
                // socket was closed
                catch (ObjectDisposedException) { Logger.WriteLine("accept socket was shutdown", true, 4); }
                
                //if the connection is invalid, increase failedAccept value
                if( tempSocket == null || !isConnectionValid(tempSocket) ) { failedAccepts++; continue; }

                // reset and increment values
                failedAccepts = 0;
                totalClientAcceptAttempts += 1;

                // log
 
                // handles the new socket
                Task handleNewConnectionTask = Task.Run(() => {handleNewConnection(tempSocket); });
                Logger.WriteLine($"accepted {totalClientAcceptAttempts} clients so far!");

            }
            return 0;
        }
        private void handleNewConnection(Socket socket)
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
                clientSockets = new Dictionary<Guid, SocketHepler>();
            }

            if(!clientSockets[clientGuid].isTerminated) { clientSockets[clientGuid].Terminate(); }

            clientSockets.Remove(clientGuid);
            Logger.WriteLine("client terminated", true, 4);
        }
        private void initServerValues()
        {
            clientSockethreads = new List<Thread>();
            clientSockets = new Dictionary<Guid, SocketHepler>();
        }

        void sendHeartBeat()
        {
            if(clientSockets == null) { throw new InvalidOperationException("clientSockets was null"); }
            foreach(SocketHepler socket in clientSockets.Values)
            {
                socket.sendHeartBeat();
            }
        }

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

        private ServerType _serverType;
        public ServerType ServerType
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

            Logger.WriteLine("attaching exeptionhandler", true, 5);
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += new UnhandledExceptionEventHandler(CleanUp);

            initServerValues();

            mainSocket = new SocketHepler(new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp));

            InitServerLoops();
            
            Thread.Sleep(2);

            mainLoop();
            
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

            Logger.WriteLine("attaching exeptionhandler", true);
            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += new UnhandledExceptionEventHandler(CleanUp);

            InitClientLoops();
            
            Thread.Sleep(2);

            mainSocket.Init(serverAdress);

            bool isConnected = ConnectToServer();

            if(!isConnected) { return 13; }

            mainLoop();
            
            return 0;
        }
        public void Stop()
        {
            run = false;

            // it is being done in handleLoopExits but is included here for completenes
            shouldAcceptConnectionThreadRun = false;


            Logger.WriteLine("closing local socket");

            //terminate the listening socket
            mainSocket.Terminate();
            
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
        /// if ServerType is Client send a message to the server, if its a Server send it to all clients 
        /// </summary>
        /// <param name="message">the byte message to send</param>
        public void sendMessage(byte[] message)
        {
            
        }
        /// <summary>
        /// sends a message to a specefic client. returns without doing anything if ServerType is not server.
        /// </summary>
        /// <param name="message">the byte message to send</param>
        /// <param name="guid">the guid of the client the message should be send to</param>
        public void sendMessage(byte[] message, Guid guid)
        {
            if(_serverType != ServerType.Server) { return; }
        }

        /// <summary>
        /// sends a message to an array of client. returns without doing anything if ServerType is not server.
        /// </summary>
        /// <param name="message">the byte message to send</param>
        /// <param name="guids">the guids of the clients to send the messages to</param>
        public void sendMessage(byte[] message, Guid[] guids)
        {

        }

        /// <summary>
        /// sends a message to all but one client. returns without doing anything if ServerType is not server.
        /// </summary>
        /// <param name="message">the byte message to send</param>
        /// <param name="guid">the guid of the client the message shouldt be send to</param>
        public void sendMessageToAllButOne(byte[] message, Guid guid)
        {

        }

        void CleanUp(object sender, UnhandledExceptionEventArgs args)
        {
            //Console.Clear();

            Exception e = (Exception) args.ExceptionObject;
            Logger.WriteLine("CleanUp caught : " + e.Message);
            Logger.WriteLine("Runtime terminating: {0}", args.IsTerminating);

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
        }

        public void SendTerminate()
        {
            mainSocket.SendTerminate();
        }
    }
}