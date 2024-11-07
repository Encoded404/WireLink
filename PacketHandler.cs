using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using ConsoleLogger;

namespace WireLink
{
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
        List<SocketHepler> clientSockets = new List<SocketHepler>();
        List<Thread> clientSockethreads = new List<Thread>();
        bool run = true;

        /// <summary>
        /// the main port for the server, ie the port the server listens for clients on
        /// </summary>
        public int mainListiningPort = 45707;

        /// <summary>
        /// the target updates per second of the main loop, which is responsible for packets, dataCompression, connections, etc
        /// </summary>
        public int targetUpdatesPerSecond = 15;

        Thread? acceptConectionThread;

        bool shouldAcceptConnectionThreadRun = true;

        // all private methods
        private void InitLoops()
        {
            Logger.WriteLine("starting acceptConnectionLoop");
            acceptConectionThread = new Thread(() => { acceptConnectionThread(); });
            acceptConectionThread.Start();
        }
        /// <summary>
        /// a deligate being called on server shutdown
        /// </summary>
        public List<Action> handleLoopExitsCallBack = new List<Action>();
        private void handleLoopExits()
        {
            shouldAcceptConnectionThreadRun = false;
            if(acceptConectionThread != null) { acceptConectionThread.Join(); Logger.WriteLine("acceptConectionThread succesfully shut down"); }

            foreach(Action action in handleLoopExitsCallBack)
            {
                action.Invoke();
            }

            return;
        }
        /// <summary>
        /// a deligate being called targetUpdatesPerSecond times every second
        /// </summary>
        public List<Action> FixedUpdateCallBack = new List<Action>();
        int incementer = 0;
        private void FixedUpdate(float deltaTime)
        {
            incementer++;
            if(incementer > 30)
            {
                incementer = 0;
                Logger.WriteLine("test", true);
            }
        }
        private void mainLoop()
        {
            InitLoops();
            Thread.Sleep(2);

            bool[] runningLoops = new bool[] {true};

            bool isReadyForShutdown = false;

            Stopwatch stopwatch = new Stopwatch();
            long ticksPerSecond = Stopwatch.Frequency;
            float milisecondsPerUpdate = 1000f / targetUpdatesPerSecond;

            float exessTime = 0f;

            Queue<float> avrDeltaTimeQueue = new();
            float maxQueueSaveDuration = 8f;
            float maxQueueSaveCount = (float)targetUpdatesPerSecond * maxQueueSaveDuration;
            float avrDeltaTime = 0f;
            float maxDeltaTime = 0f;
            float minDeltaTime = 1f;

            Logger.WriteLine($"running main loop at a Frequency of {targetUpdatesPerSecond} updates per second, timer has a Frequency of {ticksPerSecond} ticks per seconds");
            stopwatch.Start();
            long currentTime = stopwatch.ElapsedTicks - (long)(1f / targetUpdatesPerSecond * ticksPerSecond); // the last part is added to trick deltaTime into thinking the correct time has passed since last update
            long currentDeltaTime = currentTime;
            long currentExessTime = currentTime;
            while(run || !isReadyForShutdown)
            {
                currentTime = stopwatch.ElapsedTicks;

                if(!run)
                {
                    Logger.WriteLine("shutting down main loop");
                    handleLoopExits();
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

                avrDeltaTimeQueue.Enqueue(deltaTime);
                float avrBuffer = 0f;
                int avrCount = 0;
                if(avrDeltaTimeQueue.Count > maxQueueSaveCount)
                {
                    for(int i = avrDeltaTimeQueue.Count; i > maxQueueSaveCount; i--)
                    {
                        avrDeltaTimeQueue.Dequeue();
                    }
                }

                foreach(float value in avrDeltaTimeQueue)
                {
                    avrCount++;
                    avrBuffer += value;
                }

                avrDeltaTime = avrBuffer / avrCount;

                if(deltaTime > maxDeltaTime) { maxDeltaTime = deltaTime; }
                if(deltaTime < minDeltaTime) { minDeltaTime = deltaTime; }
                //Logger.WriteLine($"deltaTime is {deltaTime}, avr deltaTime in the last {avrDeltaTimeQueue.Count / (float)targetUpdatesPerSecond} seconds is: {avrDeltaTime}, with a target deltaTime of {1f/targetUpdatesPerSecond}, max deltaTime is: {maxDeltaTime} and min deltaTime is: {minDeltaTime}");


                long stopwatchBuffer = stopwatch.ElapsedTicks;

                float extraMilisecondsThisIteration = (1f / targetUpdatesPerSecond * 1000) - ((stopwatchBuffer - currentExessTime) / (float)ticksPerSecond * 1000);
                exessTime += extraMilisecondsThisIteration;

                float elapsedSeconds = (stopwatchBuffer - currentTime) / (float)ticksPerSecond;
                float milisecondsRemaining = Math.Max(0, (milisecondsPerUpdate + exessTime) - (elapsedSeconds * 1000));
                int sleepTime = (int)Math.Floor(milisecondsRemaining);


                //logger.WriteLine($"1: {stopwatchBuffer} 2: {currentTime} 3: {ticksPerSecond}");
                //logger.WriteLine($"1: {stopwatchBuffer - currentTime} 2: {elapsedTime}");
                //logger.WriteLine($"waiting for {remainingTime} miliseconds");
                //logger.WriteLine($"1: {milisecondsPerUpdate} 2: {exessTime} 3: {milisecondsPerUpdate - exessTime} 4: {elapsedSeconds * 1000} 5: {milisecondsRemaining}");
                //logger.WriteLine($"1: {(stopwatchBuffer - currentExessTime) / (float)ticksPerSecond} 2: {extraMilisecondsThisIteration}");
                //logger.WriteLine($"update {deltaTime}, which is {(1 / (float)targetUpdatesPerSecond - deltaTime) * 1000} miliseconds away from the target deltaTime");
                //logger.WriteLine($"1: {sleepTime} 2: {exessTime}");
                
                currentExessTime = stopwatchBuffer;

                if(sleepTime >= 0)
                {
                    Thread.Sleep(sleepTime);
                }
            }
            
            Logger.WriteLine("exiting main loop");
        }
        private bool isConnectionValid(Socket socket)
        {
            return true;
        }
        private int acceptConnectionThread()
        {
            bool resultBuffer;
            resultBuffer = mainSocket.Listen(mainListiningPort);
            if(resultBuffer == false) { Logger.WriteLine("listen function ran into an error an has returned"); return 14; }
            int totalClientAcceptAttempts = 0;
            int failedAccepts = 0;
            while (shouldAcceptConnectionThreadRun)
            {
                Logger.WriteLine("shouldAcceptConnectionThreadRun is: "+shouldAcceptConnectionThreadRun, true);

                //throws an eception if it failed to accept a connection too many times
                if(failedAccepts >= 10) { throw new unknownThreadException("acceptConnectionThread ran into to many errors in a row and quit"); }


                
                //accept the next connection
                Socket? tempSocket = mainSocket.Accept();
                
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
            Logger.WriteLine("shouldAcceptConnectionThreadRun is: "+shouldAcceptConnectionThreadRun+" and acceptConnectionThread is shutting down", true);
            return 0;
        }
        private void handleNewConnection(Socket socket)
        {
            //initialize the socketHelper value
            SocketHepler openClientSocket = new SocketHepler();

            // sets the socket to the newly created socket
            openClientSocket.SetSocket(socket);

            Logger.WriteLine("attempting to verify connection");

            //test the connection with the new socket
            if(!openClientSocket.verifyConnection()) { openClientSocket.terminate(); Logger.WriteLine("connection is invalid"); return; }

            Logger.WriteLine("connection verified");

            //add the socket to the list of clients
            clientSockets.Add(openClientSocket);

            return;
        }
        private void TerminateClients()
        {
            Logger.WriteLine("starting termination of Clients", true);
            foreach(SocketHepler clientSocket in clientSockets)
            {
                clientSocket.terminate();
                Logger.WriteLine("client terminated", true);
            }
        }

        // all public methods


        public int Start()
        {
            mainLoop();

            return 0;
        }
        public void Stop()
        {
            run = false;

            // it is being done in handleLoopExits but is included here for completenes
            shouldAcceptConnectionThreadRun = false;


            Logger.WriteLine("closing local socket");

            //connects to the local open socket to unblock the thread.
            Socket clientToUnlockAccept = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, mainListiningPort);
            clientToUnlockAccept.Blocking = true;
            clientToUnlockAccept.Connect(ep);

            if(clientToUnlockAccept.Connected) { Logger.WriteLine("local loopback connection etstablished", true); }
            
            Task terminateClients = Task.Factory.StartNew(() => { TerminateClients(); Logger.WriteLine("TerminateClients completed", true); });


            terminateClients.Wait();

            Logger.WriteLine("Stop Function returning", true);
            return;
        }
        public void Restart()
        {
            //stop the current instance
            Stop();

            //reninit all values
            mainSocket = new SocketHepler();
            clientSockets = new List<SocketHepler>();
            run = true;
            shouldAcceptConnectionThreadRun = true;
            handleLoopExitsCallBack = new List<Action>();
            FixedUpdateCallBack = new List<Action>();
            incementer = 0;

            //start the current instance
            Start();
        }
        public void sendMessage()
        {
            
        }
    }
}