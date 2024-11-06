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
        public static PacketHandler instance = new PacketHandler();
        SocketHepler mainSocket = new SocketHepler();
        List<SocketHepler> clientSockets = new List<SocketHepler>();
        bool run = true;

        public int mainListiningPort = 45707;

        public int targetUpdatesPerSecond = 15;

        Thread? acceptConectionThread;
        public int Start()
        {
            mainLoop();

            return 0;
        }
        bool shouldAcceptConnectionThreadRun = true;
        void InitLoops()
        {
            Logger.WriteLine("starting acceptConnectionLoop");
            acceptConectionThread = new Thread(() => { acceptConnectionThread(); });
            acceptConectionThread.Start();
        }
        public void Stop()
        {
            run = false;
            shouldAcceptConnectionThreadRun = false;

            Logger.WriteLine("closing socket");

            //for some unknown reason the main socket didnt accept the connection and just waited for another one.
            Socket clientToUnlockAccept = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint ep = new IPEndPoint(IPAddress.Loopback, mainListiningPort);
            clientToUnlockAccept.Blocking = true;
            clientToUnlockAccept.Connect(ep);

            /*Socket clientToUnlockAccept = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint ep2 = new IPEndPoint(IPAddress.Loopback, 45707);
            clientToUnlockAccept.Blocking = true;
            clientToUnlockAccept.Connect(ep2);*/

            if(clientToUnlockAccept.Connected) { Logger.WriteLine("local connection etstablished"); }
        }
        public List<Action> handleLoopExitsCallBack = new List<Action>();
        void handleLoopExits()
        {
            shouldAcceptConnectionThreadRun = false;
            if(acceptConectionThread != null) { acceptConectionThread.Join(); }

            foreach(Action action in handleLoopExitsCallBack)
            {
                action.Invoke();
            }

            return;
        }
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
        void mainLoop()
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
        int acceptConnectionThread()
        {
            bool resultBuffer;
            resultBuffer = mainSocket.Listen(mainListiningPort);
            if(resultBuffer == false) { Logger.WriteLine("listen function ran into an error an has returned"); return 14; }
            int totalAcceptedClients = 0;
            int failedAccepts = 0;
            while (shouldAcceptConnectionThreadRun)
            {
                if(failedAccepts >= 20) { return 18; }

                SocketHepler openClientSocket = new SocketHepler();
                Socket? tempSocket = mainSocket.Accept();
                if(tempSocket == null) { failedAccepts++; continue; }
                failedAccepts = 0;
                totalAcceptedClients += 1;
                Logger.WriteLine($"client accepted, adding to client list. accepted {totalAcceptedClients} clients so far");
                openClientSocket.SetSocket(tempSocket);
                clientSockets.Add(openClientSocket);
            }
            return 0;
        }
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
        bool shouldRecieve = false;

        public bool Init(IPEndPoint endPoint)
        {
            this.endPoint = endPoint;
            socket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            return true;
        }
        public bool SetSocket(Socket socket)
        {
            if(socket != null)
            {
                try { socket.Disconnect(true); } catch { }
            }
            this.socket = socket;

            return true;
        }
        public bool Connect()
        {
            if(socket != null && endPoint != null)
            {
                socket.Connect(endPoint);
            }
            else { Logger.WriteLine("socket or endpoint was null, returning."); return false; }

            return true;
        }
        public bool Listen(int port)
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint ep = new IPEndPoint(IPAddress.Any, port);
            socket.Bind(ep);

            socket.Listen();
            Logger.WriteLine("listining port created and opened");

            if(socket == null) { Logger.WriteLine("socket is null, returning false (listenCall)"); return false; }

            return true;
        }
        public Socket? Accept()
        {
            if(socket == null)
            {
                Logger.WriteLine("socket was null, returning. (acceptCall)");
                return null;
            }
            /*int waitBuffer = int.MaxValue;
            int waitBuffer2 = int.MaxValue;
            while(waitBuffer2 > 0)
            {
                while(waitBuffer > 0) { waitBuffer--; }
                waitBuffer = int.MaxValue;
            }*/
            
            Logger.WriteLine($"ready to accept clients.");
            Socket returnValue = socket.Accept();
            Logger.WriteLine("connection accepted");
            return returnValue;
        }
        public bool Send(byte[] data)
        {
            if(socket != null)
            {
                socket.Send(new byte[] { (byte)data.Length });
                socket.Send(data);
            }
            else { Logger.WriteLine("socket was null, returning."); return false; }

            return true;
        }
        public bool StartRecieve()
        {
            if(socket != null)
            {
                shouldRecieve = true;
                recieveThread = new Thread(() => recieveFunc());
                recieveThread.Start();
            }
            else { Logger.WriteLine("socket was null, returning."); return false; }
            return true;
        }
        public bool StopRecieve()
        {
            shouldRecieve = false;

            return true;
        }
        bool recieveFunc()
        {
            while(shouldRecieve)
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
    }
}