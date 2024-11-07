using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection.Metadata;
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
            if(acceptConectionThread != null) { acceptConectionThread.Join(); }

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
                Logger.WriteLine("test");
            }
        }
        private void mainLoop()
        {
            InitLoops();
            Thread.Sleep(5);

            bool[] runningLoops = new bool[] {true};

            bool isJoined = true;

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
            while(run || isJoined)
            {
                currentTime = stopwatch.ElapsedTicks;

                if(!run)
                {
                    handleLoopExits();
                    isJoined = false;
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
            int totalAcceptedClients = 0;
            int failedAccepts = 0;
            while (shouldAcceptConnectionThreadRun)
            {
                //throws an eception if it failed to accept a connection too many times
                if(failedAccepts >= 10) { throw new unknownThreadException("acceptConnectionThread ran into to many errors in a row and quit"); }


                
                //accept the next connection
                Socket? tempSocket = mainSocket.Accept();
                
                //if the connection is invalid, increase failedAccept value
                if( tempSocket == null || !isConnectionValid(tempSocket) ) { failedAccepts++; continue; }

                // reset and increment values
                failedAccepts = 0;
                totalAcceptedClients += 1;

                // log
                Logger.WriteLine($"client accepted, adding to client list. accepted {totalAcceptedClients} clients so far");

                // handles the new socket
                Task handleNewConnectionTask = Task.Run(() => {handleNewConnection(tempSocket); });
            }
            return 0;
        }
        private void handleNewConnection(Socket socket)
        {
            //initialize the socketHelper value
            SocketHepler openClientSocket = new SocketHepler();

            // sets the socket to the newly created socket
            openClientSocket.SetSocket(socket);

            //test the connection with the new socket
            if(!openClientSocket.verifyConnection()) { return; }

            //add the socket to the list of clients
            clientSockets.Add(openClientSocket);
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
            shouldAcceptConnectionThreadRun = false;

            Logger.WriteLine("closing socket");

            //connects to the local open socket to unblock the thread.
            Socket clientToUnlockAccept = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, mainListiningPort);
            clientToUnlockAccept.Blocking = true;
            clientToUnlockAccept.Connect(ep);

            if(clientToUnlockAccept.Connected) { Logger.WriteLine("local connection etstablished"); }
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

    public enum byteCodes : byte
    {
        terminateConnection = 0,
        messageHeaderStart = 1,
        messageHeaderEnd = 2,
        verifyConnection = 3,
        verifyConnectionCallback = 4,
        recievedInvalidData = 5,
    }

    class SocketHepler
    {
        private Socket? socket;
        public Socket? mainSocket
        {
            get { return socket; }
        }
        IPEndPoint? endPoint;

        public List<Action<byte[]>> recieveDeligates = new List<Action<byte[]>>();
        Thread? recieveThread;
        bool isRecieving = false;

        public bool Init(IPEndPoint endPoint)
        {
            this.endPoint = endPoint;
            socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            return true;
        }
        public bool SetSocket(Socket socket)
        {
            terminate(true);
            this.socket = socket;

            return true;
        }
        public bool Connect()
        {
            if (socket == null || endPoint == null) { Logger.WriteLine("socket or endpoint was null, returning."); return false; }

            socket.Connect(endPoint);

            return true;
        }
        public bool Listen(int port)
        {
            
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint ep = new IPEndPoint(IPAddress.Any, port);
            socket.Bind(ep);

            Logger.WriteLine("listining port created and opened");
            socket.Listen();

            return true;
        }
        public Socket? Accept()
        {
            //return if the current socket is not defined
            if(socket == null)
            {
                Logger.WriteLine("socket was null, returning. (acceptCall)");
                return null;
            }
            
            Logger.WriteLine($"ready to accept client.");
            Socket returnValue = socket.Accept();
            Logger.WriteLine("connection accepted");
            return returnValue;
        }
        public bool Send(byte[] data)
        {
            //return if the current socket is not defined
            if(socket == null) { Logger.WriteLine("socket was null, returning."); return false; }
            //return if the current socket hasnt been validated
            if(!isConnectionValid) { Logger.WriteLine("connection isnt valid, returning."); return false; }

            return true;
        }
        private void SendRaw(byte[] data)
        {
            //return if the current socket is not defined
            if(socket == null) { Logger.WriteLine("socket was null, returning."); return; }

            socket.Send(new byte[] { (byte)data.Length });
            socket.Send(data);
        }
        private void SendRaw(byte data)
        {
            //return if the current socket is not defined
            if(socket == null) { Logger.WriteLine("socket was null, returning."); return; }

            socket.Send(new byte[] { data });
        }
        public bool Recieve()
        {
            return StartRecieve();
        }
        public bool Recieve(Action<byte[]> functionCallback)
        {
            if(!StartRecieve()) { return false; } 

            recieveDeligates.Add(functionCallback);

            return true;
        }
        private bool StartRecieve()
        {
            //return if the current socket is not defined
            if(socket == null) { Logger.WriteLine("socket was null, returning."); return false; }

            isRecieving = true;
            recieveThread = new Thread(() => recieveFunc());
            recieveThread.Start();

            return true;
        }
        public bool StopRecieve()
        {
            isRecieving = false;

            return true;
        }
        bool recieveFunc()
        {
            while(isRecieving)
            {
                foreach (Action<byte[]> func in recieveDeligates)
                {
                    if(socket != null)
                    {
                        byte[] buffer = new byte[1];
                        socket.Receive(buffer);
                        int bufferSize = buffer[0];
                        buffer = new byte[bufferSize];
                        socket.Receive(buffer);
                        func.Invoke(buffer);
                    }
                    else { return false; }
                }
            }
            return true;
        }
        public bool terminate(bool reuseSocket = false)
        {
            //if the socket is defined, terminate it
            if(socket != null)
            {
                SendRaw((byte)byteCodes.terminateConnection);
                try { socket.Disconnect(reuseSocket); } catch {}
            }
            return true;
        }
        bool isConnectionValid = false;
        Random randomIdGenerator = new Random();
        public bool verifyConnection()
        {
            byte[] randomId = new byte[4];
            randomIdGenerator.NextBytes(randomId);
            if(!isRecieving)
            {
                if(StartRecieve() == false) { return false; }
            }

            int retries = 3;
            bool returned = false;

            //add a function the check if the connection is valid
            recieveDeligates.Add((byte[] bytes) => 
            {
                //if no more retries are left return with a failed connection
                if(retries <= 0) { SendRaw((byte)byteCodes.recievedInvalidData); retries = -1; returned = true; return; }

                //if the recieved data is the wrong size retry
                if(bytes.Length != 5) { SendRaw((byte)byteCodes.recievedInvalidData); retries--; return; }

                //if the first byte is the correct return value proceed
                if(bytes[0] != (byte)byteCodes.verifyConnectionCallback) { SendRaw((byte)byteCodes.recievedInvalidData); retries--; return; }

                // if the id matches the original id, accept the connection
                for(int i = 1; i < bytes.Length; i++)
                {
                    if(bytes[i] != randomId[i-1]) { SendRaw((byte)byteCodes.recievedInvalidData); retries--; return; }
                }
                returned = true;
                return;
            });

            SendRaw([(byte)byteCodes.verifyConnection, randomId[0], randomId[1], randomId[2], randomId[3]]);

            while(!returned)
            {
                Thread.Sleep(1);
            }

            if(retries <= 0) { isConnectionValid = false; return false; }

            isConnectionValid = true;
            return true;
        }
    }
}