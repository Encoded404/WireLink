using System.Net.Sockets;
using ConsoleLogger;
using System.Net;
using System.Diagnostics;


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
            terminate();
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
                byte[] buffer = new byte[1];
                if(socket != null)
                {
                    try
                    {
                        socket.Receive(buffer, buffer.Length, 0);
                        int bufferSize = buffer[0];
                        Logger.WriteLine("bufferSize is: "+bufferSize, true, 5);
                        buffer = new byte[bufferSize];
                        socket.Receive(buffer, bufferSize, 0);
                        Logger.WriteLine("data recieved", true, 5);
                    }
                    catch (ObjectDisposedException exept1)
                    {
                        if (exept1 != null)
                        {
                            Logger.WriteLine("socket was shutdown wilst in a reading state", true, 1);
                            Logger.WriteLine("exeption: "+exept1, true, 4);
                        }
                    }
                }
                else { return false; }
                foreach (Action<byte[]> func in recieveDeligates)
                {
                    func.Invoke(buffer);
                }
            }
            return true;
        }
        /// <summary>
        /// closes a socket
        /// </summary>
        /// <param name="reuseSocket">whether the socket should be able to be reused</param>
        /// <returns></returns>
        public bool terminate()
        {
            Logger.WriteLine("terminating socket", true, 4);

            //if the socket is defined, terminate it
            if(socket != null)
            {
                StopRecieve();
                SendRaw((byte)byteCodes.terminateConnection);
                try
                {
                    Logger.WriteLine("shutting down socket", true, 4);
                    socket.Shutdown(SocketShutdown.Both);
                    socket.Disconnect(false);
                    Logger.WriteLine("disposing socket", true, 4);
                    socket.Close();
                    socket.Dispose(); //socket.dispose is redundant, but is included for readebility and redundancy
                    Logger.WriteLine("socket disposed", true, 4);
                } catch (Exception e) { Logger.WriteLine("socket failed to terminate: \n" + e); }
            }
            return true;
        }
        bool isConnectionValid = false;
        Random randomIdGenerator = new Random();
        /// <summary>
        /// verifies the connection of the sockethelper
        /// </summary>
        /// <param name="timeoutTime">the timeout time in milseconds, set to 0 to disable timeout</param>
        /// <returns></returns>
        public bool verifyConnection(uint timeoutTime = 1000)
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
            recieveDeligates.Add((byte[] bytes) => { verifySingleConnection(bytes, randomId, ref retries, out returned); });
            int deligateId = recieveDeligates.Count -1;

            SendRaw([(byte)byteCodes.verifyConnection, randomId[0], randomId[1], randomId[2], randomId[3]]);

            Stopwatch timeout = Stopwatch.StartNew();
            //checks whether the connection has returned, or the timeout has ran out
            while( !returned && (timeoutTime == 0 || timeout.ElapsedMilliseconds < timeoutTime) )
            {
                Thread.Sleep(1);
            }
            timeout.Stop();

            recieveDeligates.RemoveAt(deligateId);

            if(retries <= 0 || timeout.ElapsedMilliseconds >= timeoutTime) { isConnectionValid = false; return false; }

            isConnectionValid = true;
            return true;
        }
        private void verifySingleConnection(byte[] bytes, byte[] randomId, ref int triesLeft, out bool returned)
        {
            returned = false;

            //if no more retries are left return with a failed connection
            if(triesLeft <= 0) { SendRaw((byte)byteCodes.recievedInvalidData); triesLeft = -1; returned = true; return; }

            //if the recieved data is the wrong size retry
            if(bytes.Length != 5) { SendRaw((byte)byteCodes.recievedInvalidData); triesLeft--; return; }

            //if the first byte is the correct return value proceed
            if(bytes[0] != (byte)byteCodes.verifyConnectionCallback) { SendRaw((byte)byteCodes.recievedInvalidData); triesLeft--; return; }

            // if the id matches the original id, accept the connection
            for(int i = 1; i < bytes.Length; i++)
            {
                if(bytes[i] != randomId[i-1]) { SendRaw((byte)byteCodes.recievedInvalidData); triesLeft--; return; }
            }
            returned = true;
            return;
        }
    }
}