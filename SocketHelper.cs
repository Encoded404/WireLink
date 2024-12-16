using System.Net.Sockets;
using ConsoleLogger;
using System.Net;
using System.Diagnostics;
using System.Threading.Tasks.Dataflow;


namespace WireLink
{
    public enum byteCodes : byte
    {
        terminateConnection,
        messageHeaderStart,
        messageHeaderEnd,
        verifyConnection,
        verifyConnectionCallback,
        connectionVerified,
        recievedInvalidData,
        heartBeat
    }

    class SocketHepler
    {
        private Socket? socket = null;
        bool _isTerminated = true;
        public bool isTerminated
        {
            get { return _isTerminated; }
        }
        public Socket? mainSocket
        {
            get { return socket; }
        }
        IPEndPoint? endPoint;

        public List<Action<byte[]>> recieveDeligates = new List<Action<byte[]>>();
        Thread? recieveThread;
        bool isRecieving = false;
        
        public SocketHepler()
        {
            
        }
        public SocketHepler(Socket socket)
        {
            this.socket = socket;
            _isTerminated = false;
        }
        public SocketHepler(Socket socket, Guid guid)
        {
            this.socket = socket;
            _isTerminated = false;
            myGuid = guid;
        }

        public bool Init(IPEndPoint remoteEndPoint)
        {
            endPoint = remoteEndPoint;
            socket = new Socket(remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            _isTerminated = false;

            return true;
        }
        public bool SetSocket(Socket socket)
        {
            if(socket != null)
            {
                Terminate();
            }
            
            this.socket = socket;
            _isTerminated = false;

            return true;
        }
        public bool Connect()
        {
            if (socket == null || endPoint == null || _isTerminated) { _isTerminated = true; Logger.WriteLine("socket or endpoint was invalid, returning."); return false; }

            socket.Connect(endPoint);

            return true;
        }
        /// <summary>
        /// initializes the socket and sets t in a listening state
        /// </summary>
        /// <param name="port"></param>
        /// <returns></returns>
        public bool CreateAndListen(int port)
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            return Listen(port);
        }
        public bool Listen(int port)
        {
            if(socket == null || _isTerminated) { _isTerminated = true; Logger.WriteLine("[Listen] socket was invalid, returning."); return false; }

            IPEndPoint ep = new IPEndPoint(IPAddress.Any, port);
            socket.Bind(ep);

            Logger.WriteLine("[Listen] listining port created and opened");
            socket.Listen();

            return true;
        }
        public Socket? Accept()
        {
            //return if the current socket is not defined
            if(socket == null || _isTerminated) { _isTerminated = true; Logger.WriteLine("[accept] socket was null, returning. (acceptCall)"); return null; }
            
            Logger.WriteLine($"[accept] ready to accept a new client.");
            Socket returnValue = socket.Accept();
            Logger.WriteLine("[accept] connection accepted");
            return returnValue;
        }
        public bool Send(byte[] data)
        {
            //return if the current socket is not defined
            if(socket == null || _isTerminated || !socket.Connected) { _isTerminated = true; Logger.WriteLine("socket was invalid, returning."); return false; }
            //return if the current socket hasnt been validated
            if(!isConnectionValid) { Logger.WriteLine("connection isnt valid, returning."); return false; }

            return SendRaw(data);
        }
        public bool send(byte data)
        {
            return Send([data]);
        }
        private bool SendRaw(byte[] data)
        {
            //return if the current socket is not defined
            if(socket == null || _isTerminated || !socket.Connected) { _isTerminated = true; handleRemoteTerminate(); Logger.WriteLine("[SendRaw] socket is invalid, returning."); return false; }
            
            if(data.Length > byte.MaxValue) {  Logger.WriteLine("[SendRaw] data array is too large", true, 3); return false;}

            try
            {
                socket.Send([(byte)data.Length]);
                socket.Send(data);
            }
            catch(SocketException ex)
            {
                if(ex.ErrorCode == 32)
                {
                    Logger.WriteLine("remote socket was terminated abrubtly", true); handleRemoteTerminate();
                    return false;
                }

                throw;
            }

            return true;
        }
        private bool SendRaw(byte data)
        {
            //return if the current socket is not defined
            if(socket == null) { Logger.WriteLine("[SendRaw] socket is invalid, returning."); return false; }

            return SendRaw([data]);
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
            if(socket == null || _isTerminated) { Logger.WriteLine("socket was null, returning."); return false; }

            isRecieving = true;
            recieveThread = new Thread(() => recieveFunc());
            recieveThread.Start();

            return true;
        }
        public bool StopRecieve()
        {
            isRecieving = false;
            Logger.WriteLine("stopped recieving", true, 5);
            return true;
        }
        bool recieveFunc()
        {
            Logger.WriteLine("[recieveFunc] starting recievefunc", true, 5);
            while(isRecieving)
            {
                byte[] buffer = new byte[1];
                bool hasRecievedData = false;
                if(socket != null)
                {
                    try
                    {
                        Logger.WriteLine($"[recieveFunc] socket blocking: {socket.Blocking} isterminated: {_isTerminated} is connected: {socket.Connected}", true, 7);
                        socket.Receive(buffer, buffer.Length, 0);
                        if(_isTerminated || !socket.Connected) { return false; }
                        int bufferSize = buffer[0];
                        if(bufferSize <= 0) { continue; }
                        Logger.WriteLine("[recieveFunc] bufferSize is: "+bufferSize, true, 6);
                        buffer = new byte[bufferSize];
                        socket.Receive(buffer, bufferSize, 0);
                        hasRecievedData = true;
                        Logger.WriteLine("[recieveFunc] data recieved", true, 6);
                    }
                    catch (ObjectDisposedException e)
                    {
                        if (e != null)
                        {
                            Logger.WriteLine("[recieveFunc] socket was shutdown whilst in a reading state", true, 1);
                            string output = e.ToString();
                            output.Replace("\n", "\n\t");
                            Logger.WriteLine("exeption: "+output/* , true, 5 */);
                        }
                    }
                    catch(SocketException e)
                    {
                        if (e != null)
                        {
                            Logger.WriteLine("[recieveFunc] unknown socketExeption", true, 1);
                            string output = e.ToString();
                            output.Replace("\n", "\n\t");
                            Logger.WriteLine("exeption: "+output/*, true, 5*/);
                        }
                    }
                }
                else { return false; }
                //check if buffer recieved any data
                if(!hasRecievedData) { continue;}

                bool shouldCallDeligates = true;
                switch (buffer[0])
                {
                    case (byte)byteCodes.heartBeat:
                        Logger.WriteLine("recieved heartbeat", true, 5);
                        shouldCallDeligates = false;
                        break;
                    case (byte)byteCodes.terminateConnection:
                        Logger.WriteLine("remote socket was terminated", true, 5);
                        handleRemoteTerminate();
                        shouldCallDeligates = false;
                        break;
                }
                if(!shouldCallDeligates) { continue; }

                Logger.WriteLine("[recieveFunc] recieved data and invoking callbacks", true, 5);
                foreach (Action<byte[]> func in recieveDeligates)
                {
                    func.Invoke(buffer);
                }
            }
            return true;
        }

        Guid? myGuid = null;

        /// <summary>
        /// a deligate being called opun termination of the socket
        /// </summary>
        public List<Action<Guid>> terminateDeligate = new List<Action<Guid>>();

        /// <summary>
        /// closes a socket
        /// </summary>
        /// <returns></returns>
        public bool Terminate()
        {
            Logger.WriteLine("[terminate function] terminating socket", true, 4);

            //if the socket is defined, terminate it
            if(socket != null)
            {
                if(_isTerminated)
                {
                    return true;
                }
                SendRaw((byte)byteCodes.terminateConnection);
                _isTerminated = true;
                try
                {
                    if(isRecieving)
                    {
                        StopRecieve();
                    }
                    Logger.WriteLine("[terminate function] shutting down socket", true, 5);
                    socket.Shutdown(SocketShutdown.Both);
                    if(socket.Connected)
                    {
                        socket.Disconnect(false);
                    }
                } catch (Exception e) { Logger.WriteLine("[terminate function] socket failed to terminate: \n" + e); return false; }
                finally
                {
                    Logger.WriteLine("[terminate function] disposing socket", true, 5);
                    socket.Close();
                    socket.Dispose(); //socket.dispose is redundant, but is included for readebility and redundancy
                }
                Logger.WriteLine("[terminate function] socket disposed", true, 5);
            }
            else
            {
                Logger.WriteLine("[terminate function] socket was null on termninate", true, 5);
            }

            foreach(Action<Guid> action in terminateDeligate)
            {
                if(myGuid == null) { break; }
                Task.Run(() => action((Guid)myGuid));
            }

            Logger.WriteLine("[terminate function] returning from terminate function", true, 5);

            return true;
        }

        private void handleRemoteTerminate()
        {
            //if the socket is defined, terminate it
            if(socket != null)
            {
                _isTerminated = true;
                try
                {
                    if(isRecieving)
                    {
                        StopRecieve();
                    }
                    Logger.WriteLine("[remote terminate function] shutting down socket", true, 5);
                    socket.Shutdown(SocketShutdown.Both);
                    if(socket.Connected)
                    {
                        socket.Disconnect(false);
                    }
                } catch (Exception e) { Logger.WriteLine("[remote terminate function] socket failed to terminate: \n" + e); }
                finally
                {
                    Logger.WriteLine("[remote terminate function] disposing socket", true, 5);
                    socket.Close();
                    socket.Dispose(); //socket.dispose is redundant, but is included for readebility and redundancy
                }
                Logger.WriteLine("[remote terminate function] socket disposed", true, 5);
            }

            foreach(Action<Guid> action in terminateDeligate)
            {
                if(myGuid == null) { break; }
                Task.Run(() => action((Guid)myGuid));
            }
        }
        public void SendTerminate()
        {
            SendRaw((byte)byteCodes.terminateConnection);
        }
        bool isConnectionValid = false;
        Random randomIdGenerator = new Random();
        /// <summary>
        /// verifies the connection of the sockethelper to a client
        /// </summary>
        /// <param name="timeoutTime">the timeout time in millseconds, set to 0 to disable timeout</param>
        /// <param name="retries">the retries it has to verify the connection, a value between 0 and 255</param>
        /// <returns></returns>
        public bool verifyClientConnection(uint timeoutTime = 1500, byte retries = 3)
        {
            byte[] randomId = new byte[4];
            randomIdGenerator.NextBytes(randomId);

            Logger.WriteLine("[verifyClientConnection] verifing client connection", false, 4);
            
            bool? verified = null;

            //sets the id of our deligate to the position it will occupy
            int deligateId = recieveDeligates.Count;
            //add a function the check if the connection is valid
            recieveDeligates.Add((byte[] bytes) => { verified = verifySingleClientConnection(bytes, randomId, ref retries); });
            
            if(!isRecieving)
            {
                if(!StartRecieve()) { return false; }
            }
            
            Logger.WriteLine("[verifyClientConnection] sending verify request", false, 5);
            SendRaw([(byte)byteCodes.verifyConnection, randomId[0], randomId[1], randomId[2], randomId[3]]);
            Logger.WriteLine("[verifyClientConnection] verify request sent", false, 5);
            
            Stopwatch timeout = Stopwatch.StartNew();
            //checks whether the connection has returned, or the timeout has ran out
            while((verified == null && timeout.ElapsedMilliseconds < timeoutTime) || (verified == null && timeoutTime == 0))
            {
                Thread.Sleep(25);
            }
            timeout.Stop();
            recieveDeligates.RemoveAt(deligateId);

            if(timeout.ElapsedMilliseconds >= timeoutTime) { Logger.WriteLine("[verifyClientConnection] could not verify server connection, connection timed out"); isConnectionValid = false; return false; }
            if(retries <= 0) { Logger.WriteLine("[verifyClientConnection] could not verify server connection, couldnt reach or recognize the client"); isConnectionValid = false; return false; }

            SendRaw((byte)byteCodes.connectionVerified);

            isConnectionValid = true;
            return true;
        }
        private bool? verifySingleClientConnection(byte[] bytes, byte[] randomId, ref byte triesLeft)
        {
            //if no more retries are left return with a failed connection.
            if(triesLeft <= 0) { SendRaw((byte)byteCodes.terminateConnection); return false; }
            
            Logger.WriteLine("[verifySingleClientConnection] recieved verification response", true, 5);

            //if the recieved data is the wrong size, retry.
            if(bytes.Length != 5) { SendRaw((byte)byteCodes.recievedInvalidData); SendRaw([(byte)byteCodes.verifyConnection, randomId[0], randomId[1], randomId[2], randomId[3]]); triesLeft--; return null; }

            if(bytes[0] == (byte)byteCodes.recievedInvalidData) { SendRaw([(byte)byteCodes.verifyConnection, randomId[0], randomId[1], randomId[2], randomId[3]]); return null; }
            //if the first byte is the wrong return value, retry.
            if(bytes[0] != (byte)byteCodes.verifyConnectionCallback ) { SendRaw((byte)byteCodes.recievedInvalidData); triesLeft--; return null; }

            // if each of the byte in the id doesnt match the original id, retry.
            for(int i = 1; i < bytes.Length; i++)
            {
                if(bytes[i] != randomId[i-1]) { SendRaw((byte)byteCodes.recievedInvalidData); triesLeft--; return null; }
            }

            return true;
        }

        /// <summary>
        /// verifies the connection of the sockethelper to a server
        /// </summary>
        /// <param name="timeoutTime">the time in miliseconds before the function returns false if there is no response from the server</param>
        public bool verifyServerConnection(uint timeoutTime = 1500, byte retries = 3)
        {

            Logger.WriteLine("[verifyServerConnection] verifing server connection", true, 4);

            bool? isSuccess = null;
            //sets the id of our deligate to the position it will occupy
            int deligateId = recieveDeligates.Count;
            //add a function the check if the connection is valid
            recieveDeligates.Add((byte[] bytes) => { isSuccess = verifyServerConnectionCallback(bytes, ref retries); });
            
            if(!isRecieving)
            {
                if(StartRecieve() == false) { return false; }
            }


            Stopwatch timeout = Stopwatch.StartNew();
            //checks whether the connection has returned, or the timeout has ran out
            while((isSuccess == null && timeout.ElapsedMilliseconds < timeoutTime) || (isSuccess == null && timeoutTime == 0))
            {
                Thread.Sleep(25);
            }
            timeout.Stop();

            recieveDeligates.RemoveAt(deligateId);

            if(timeout.ElapsedMilliseconds >= timeoutTime) { Logger.WriteLine("[verifyServerConnection] could not verify server connection, connection timed out"); isConnectionValid = false; return false; }
            if(retries <= 0) { Logger.WriteLine("[verifyServerConnection] could not verify server connection, couldnt reach or recognize the server"); isConnectionValid = false; return false; }

            if(isSuccess ?? false) { Logger.WriteLine("[verifyServerConnection] connection verified", true, 4); }

            isConnectionValid = isSuccess ?? false;
            return isSuccess ?? false;
        }
        private bool? verifyServerConnectionCallback(byte[] bytes, ref byte retries)
        {
            Logger.WriteLine("[verifyServerConnectionCallback] test", true, 5);
            if(retries <= 0) { SendRaw((byte)byteCodes.terminateConnection); return false; }

            if(bytes[0] == (byte)byteCodes.recievedInvalidData) { return null; }

            //if the first byte is the wrong value, retry.
            if(bytes[0] != (byte)byteCodes.verifyConnection && bytes[0] != (byte)byteCodes.connectionVerified) { SendRaw((byte)byteCodes.recievedInvalidData); retries--; return null; }
            Logger.WriteLine("[verifyServerConnectionCallback] recieved server verify request", true, 5);

            if(bytes[0] == (byte)byteCodes.connectionVerified) { return true; }

            //if the recieved data is the wrong size, retry.
            if(bytes.Length != 5) { SendRaw((byte)byteCodes.recievedInvalidData); retries--; return null; }

            Logger.WriteLine("[verifyServerConnectionCallback] server verify request is valid, responding", true, 5);
            //send the reccieved code back
            SendRaw([(byte)byteCodes.verifyConnectionCallback, bytes[1], bytes[2], bytes[3], bytes[4]]);
            

            return null;
        }

        public void sendHeartBeat()
        {
            SendRaw((byte)byteCodes.heartBeat);
        } 
    }
}