using System.Net.Sockets;
using ConsoleLogger;
using System.Net;


namespace WireLink
{
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