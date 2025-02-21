using System.Net.Sockets;
using ConsoleLogger;
using System.Net;
using System.Diagnostics;
using System.Threading.Tasks.Dataflow;


namespace WireLink
{
    internal enum byteCodes : byte
    {
        terminateConnection,
        messageHeaderStart,
        messageHeaderEnd,
        verifyConnectionRequest,
        verifyConnection,
        verifyConnectionResponse,
        connectionVerified,
        recievedInvalidData,
        heartBeat
    }

    internal class SocketHelper
    {
        private Socket? socket = null;
        InternalNetworkingEngine engine;
        bool _isTerminated = true;
        bool _isConnected = false;
        //bool isConnectionValid = false;
        public bool isTerminated
        {
            get { return _isTerminated; }
        }
        public Socket? mainSocket
        {
            get { return socket; }
        }
        IPEndPoint? endPoint;
        int port;
        Thread? recieveThread;
        bool isRecieving = false;
        Guid? myGuid = null;
        SocketType socketType;
        
        public SocketHelper()
        {
            Init(AddressFamily.InterNetwork);
            engine = InternalNetworkingEngine.instance;
        }
        public SocketHelper(InternalNetworkingEngine engine, int port = 0)
        {
            Init(AddressFamily.InterNetwork);
            this.engine = engine;
            
            if(port == 0)
            {
                this.port = InternalNetworkingEngine.instance.mainClientPort;
            }
            else
            {
                this.port = port;
            }
        }
        public SocketHelper(Socket socket, InternalNetworkingEngine? engine = null, int port = 0)
        {
            this.socket = socket;
            _isTerminated = false;

            if(engine == null)
            {
                this.engine = InternalNetworkingEngine.instance;
            }
            else
            {
                this.engine = engine;
            }
            
            if(port == 0)
            {
                this.port = InternalNetworkingEngine.instance.mainClientPort;
            }
            else
            {
                this.port = port;
            }
        }
        public SocketHelper(Socket socket, Guid guid, InternalNetworkingEngine? engine = null, int port = 0)
        {
            this.socket = socket;
            _isTerminated = false;
            myGuid = guid;

            if(engine == null)
            {
                this.engine = InternalNetworkingEngine.instance;
            }
            else
            {
                this.engine = engine;
            }

            if(port == 0)
            {
                this.port = InternalNetworkingEngine.instance.mainClientPort;
            }
            else
            {
                this.port = port;
            }
        }
        public bool Init(IPEndPoint remoteEndPoint)
        {
            endPoint = remoteEndPoint;
            return Init(remoteEndPoint.AddressFamily);
        }
        public bool Init(AddressFamily addressFamily)
        {
            socket = new Socket(addressFamily, SocketType.Dgram, ProtocolType.Udp);

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

        public bool setDefaultRemoteHost()
        {
            if (socket == null || endPoint == null || _isTerminated) { _isTerminated = true; Logger.WriteLine("socket or endpoint was invalid, returning."); return false; }

            socket.Connect(endPoint);

            return true;
        }
        public bool Send(byte[] data)
        {
            //return if the current socket is not defined
            if(socket == null || _isTerminated || !socket.Connected) { _isTerminated = true; handleRemoteTerminate(); Logger.WriteLine($"socket is invalid, returning."); return false; }
            //return if the current socket hasnt been validated
            
            //if(!isConnectionValid) { Logger.WriteLine("connection isnt valid, returning."); return false; }

            return SendRaw(data);
        }
        public bool send(byte data)
        {
            return Send([data]);
        }
        private bool SendRaw(byte[] data)
        {
            //return if the current socket is not defined
            if(socket == null || _isTerminated) { _isTerminated = true; Terminate(); Logger.WriteLine($"[SendRaw] socket is invalid, returning."); return false; }
            if(!socket.Connected) { _isTerminated = true; handleRemoteTerminate(); Logger.WriteLine($"[SendRaw] socket was terminated remotly, returning."); return false; }
            
            try
            {
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
        /* public bool AddReciever(Action<byte[]> functionCallback)
        {
            if(isRecieving) { return false; } 

            receiveDelegates.Add(functionCallback);

            return true;
        } */
        private bool StartRecieve()
        {
            //return if the current socket is not defined
            if(socket == null || _isTerminated) { Logger.WriteLine("socket was null, returning."); return false; }

            isRecieving = true;
            recieveThread = new Thread(() => RecieveFunc());
            recieveThread.Start();

            return true;
        }
        public bool StopRecieve()
        {
            isRecieving = false;
            Logger.WriteLine("stopped recieving", true, 5);

            return true;
        }
        List<Task> tasks = new List<Task>();
        bool RecieveFunc()
        {
            Logger.WriteLine("[recieveFunc] starting recievefunc", true, 5);
            while(isRecieving)
            {
                byte[] buffer = new byte[1024];
                EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                if(socket != null)
                {
                    try
                    {
                        Logger.WriteLine($"[recieveFunc] receiving: socket blocking: {socket.Blocking} isterminated: {_isTerminated} is connected: {socket.Connected}", true, 7);
                        
                        int dataRecieved = 0;
                        if(_isConnected)
                        {
                            // recieves the data from the remote client/server
                            dataRecieved = socket.Receive(buffer);
                        }
                        else
                        {
                            // recieves the data from any remote client/server
                            dataRecieved = socket.ReceiveFrom(buffer, ref remoteEndPoint);
                        }
                        Logger.WriteLine("[recieveFunc] data recieved", true, 6);

                        // resize the array to fit the data 
                        buffer = buffer.AsSpan(0, dataRecieved).ToArray();
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
                    case (byte)byteCodes.verifyConnectionRequest:
                        tasks.Add(Task.Run(() => verifyClientConnection(remoteEndPoint)));
                        break;
                    case (byte)byteCodes.verifyConnection:
                        Logger.WriteLine("received verification message");
                        verifyServerConnectionCallback(buffer, remoteEndPoint);
                        shouldCallDeligates = false;
                        break;
                    case (byte)byteCodes.verifyConnectionResponse:
                        verifyDelegates.TryGetValue(remoteEndPoint, out Action<byte[]>? callback);
                        if(callback != null)
                        {
                            tasks.Add(Task.Run(() => callback(buffer)));
                        }
                        break;
                    case (byte)byteCodes.connectionVerified:
                        Logger.WriteLine("received verification confirmed message");
                        verifyServerConnectionCallback(buffer, remoteEndPoint);
                        shouldCallDeligates = false;
                        break;
                }
                if(!shouldCallDeligates) { continue; }

                Logger.WriteLine("[recieveFunc] recieved data and invoking callbacks", true, 5);
            }
            return true;
        }

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
                    if(socket.Connected)
                    {
                        socket.Shutdown(SocketShutdown.Both);
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
                    if(socket.Connected)
                    {
                        socket.Shutdown(SocketShutdown.Both);
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

        Random randomIdGenerator = new Random();
        bool _isTryingToVerify = false;
        Dictionary<EndPoint, Action<byte[]>> verifyDelegates = new Dictionary<EndPoint, Action<byte[]>>();
        /// <summary>
        /// verifies the connection of the sockethelper to a client
        /// </summary>
        /// <param name="timeoutTime">the timeout time in millseconds, set to 0 to disable timeout</param>
        /// <param name="retries">the retries it has to verify the connection, a value between 0 and 255</param>
        /// <returns></returns>
        private bool verifyClientConnection(EndPoint client, uint timeoutTime = 1500, byte retries = 3)
        {
            byte[] randomId = new byte[4];
            randomIdGenerator.NextBytes(randomId);

            Logger.WriteLine("[verifyClientConnection] verifing client connection", false, 4);
            
            bool? verified = null;
            _isTryingToVerify = true;

            byte[] predefinedMessage = new byte[5];
            
            predefinedMessage[0] = (byte)byteCodes.verifyConnection;
            Array.Copy(randomId, 0, predefinedMessage, 1, 4);

            int delegateId = verifyDelegates.Count;
            //add a function the check if the connection is valid
            verifyDelegates.Add(client, bytes => { verified = verifyClientConnectionCallback(bytes, randomId, ref retries); });
            
            if(!isRecieving)
            {
                if(!StartRecieve()) { return false; }
            }
            
            Logger.WriteLine("[verifyServerConnection] sending verify request", false, 5);
            SendRaw(predefinedMessage);
            Logger.WriteLine("[verifyServerConnection] verify request sent", false, 5);
            
            Stopwatch timeout = Stopwatch.StartNew();
            //checks whether the connection has returned, or the timeout has ran out
            while((verified == null && timeout.ElapsedMilliseconds < timeoutTime) || (verified == null && timeoutTime == 0))
            {
                Thread.Sleep(25);
            }
            timeout.Stop();
            verifyDelegates.Remove(client);

            if(timeout.ElapsedMilliseconds >= timeoutTime) { Logger.WriteLine("[verifyServerConnection] could not verify server connection, connection timed out"); return false; }
            if(retries <= 0) { Logger.WriteLine("[verifyServerConnection] could not verify server connection, couldnt reach or recognize the client"); return false; }

            SendRaw((byte)byteCodes.connectionVerified);

            Logger.WriteLine("[verifyServerConnection] client connection verified", false, 4);
            
            //isConnectionValid = true;
            return true;
        }
        // validates the reply
        private bool? verifyClientConnectionCallback(byte[] bytes, byte[] predefinedMessage, ref byte triesLeft)
        {
            //if no more retries are left return with a failed connection.
            if(triesLeft <= 0) { SendRaw((byte)byteCodes.terminateConnection); return false; }
            
            Logger.WriteLine("[verifyServerConnectionCallback] recieved verification response", true, 5);

            //if the recieved data is the wrong size, retry.
            if(bytes.Length != 5) { VCCSendRetry(predefinedMessage, ref triesLeft, true); return null; }

            if(bytes[0] == (byte)byteCodes.recievedInvalidData) { VCCSendRetry(predefinedMessage, ref triesLeft, false); return null; }
            //if the first byte is the wrong return value, retry.
            if(bytes[0] != (byte)byteCodes.verifyConnectionResponse ) { VCCSendRetry(predefinedMessage, ref triesLeft, true); return null; }

            // if each of the byte in the id doesnt match the original id, retry.
            if(predefinedMessage.AsSpan(1).ToArray() != bytes.AsSpan(1).ToArray()) { VCCSendRetry(predefinedMessage, ref triesLeft, true); return null; }

            return true;
        }
        // sends a retry message to the client
        private void VCCSendRetry(byte[] predefinedMessage, ref byte retries, bool sendInvalidMessage)
        {
            if(sendInvalidMessage) { SendRaw((byte)byteCodes.recievedInvalidData); }
            SendRaw(predefinedMessage);
            retries--;
        }

        public void verifyServerConnection()
        {
            Logger.WriteLine("[verifyClientConnection] verifing client connection", false, 4);
            
            Logger.WriteLine("[verifyServerConnection] sending verify request", false, 5);
            SendRaw((byte)byteCodes.verifyConnectionRequest);
            Logger.WriteLine("[verifyServerConnection] verify request sent", false, 5);
        }
        private void verifyServerConnectionCallback(byte[] bytes, EndPoint clientId)
        {
            Logger.WriteLine("[verifyClientConnectionCallback] test", true, 5);
            //if(retries <= 0) { SendRaw((byte)byteCodes.terminateConnection); return false; }

            if(bytes[0] == (byte)byteCodes.recievedInvalidData) { return; }

            //if the recieved data is the wrong size, retry.
            if(bytes.Length != 5) { SendRaw((byte)byteCodes.recievedInvalidData); /*retries--;*/ return; }

            //if the first byte is the wrong value, retry.
            if(bytes[0] != (byte)byteCodes.verifyConnection && bytes[0] != (byte)byteCodes.connectionVerified) { SendRaw((byte)byteCodes.recievedInvalidData); /*retries--;*/ return; }
            
            Logger.WriteLine("[verifyClientConnectionCallback] recieved server verify request", true, 5);
                        
            if(bytes[0] == (byte)byteCodes.verifyConnection) { SendRaw([(byte)byteCodes.verifyConnectionResponse, bytes[1], bytes[2], bytes[3], bytes[4]]); return; }

            engine.messageHandler.addKnownClient(clientId);
            _isConnected = true;

            Logger.WriteLine("[verifyClientConnectionCallback] server verify request is valid, responding", true, 5);
            //send the reccieved code back
        }

        public void sendHeartBeat()
        {
            SendRaw((byte)byteCodes.heartBeat);
        } 
    }
}