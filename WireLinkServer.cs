using System.Net;
using ConsoleLogger;

namespace WireLink
{
    public class WireLinkServer
    {
        // public WireLinkClient()
        // {

        // }

        // private DataConversionHelper dataConversionHelper = new DataConversionHelper();

        public const int defaultServerPort = 45707;
        public const int defaultClientPort = 45706;

        public int serverPort = defaultServerPort;
        public int clientPort = defaultClientPort;

        PacketHandler packetHandler = new PacketHandler();

        IPEndPoint serverEndpoint = new IPEndPoint(IPAddress.Loopback, 0);

        private IPEndPoint? TryParseEndpoint(string host, int port = -1)
        {
            IPEndPoint? temp = null;
            host.ToLower();
            if(host == "loop" || host == "loopback" || host == "local" || host == "localhost")
            {
                temp = new IPEndPoint(IPAddress.Loopback, port);
                Logger.WriteLine("setting host adress to: " + host + ":" + port, true);
            }
            else
            {
                try
                {
                    temp = new IPEndPoint(Dns.GetHostEntry(host).AddressList[0], port);
                    Logger.WriteLine("setting host adress to: " + host + ":" + port, true);
                }
                catch (FormatException)
                {
                    throw new InvalidDataException("please input a valid adress for host, " + host + ":" + port + " is not a valid adress");
                }
            }

            return temp;
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

        /// <summary>
        /// connect to a server indicated by a host and a port
        /// </summary>
        /// <param name=Encoding"port">the port to open on the server, set to -1 to use default server port</param>
        public void StartServer(int port = -1)
        {
            if (port == -1)
            {
                port = defaultServerPort;
            }

            packetHandler.mainServerListiningPort = port;

            packetHandler.StartServer();

            Logger.WriteLine("test end 1");
        }
        /// <summary>
        /// disconnects from the server
        /// </summary>
        public void StopServer()
        {
            packetHandler.Stop();
        }

        // data conversion

        // /// <summary>
        // /// add a conversion function to known conversions
        // /// </summary>
        // /// <typeparam name="T"></typeparam>
        // /// <param name="toByteFunction">a function that takes a certain type and returns a byte</param>
        // /// <param name="fromByteFunction">a function that takes a byte and converts it into a certain type</param>
        // /// <param name="typeShorthand">a 4 chracter string that represents the type</param>
        // /// <returns>whether the function was succesfully added</returns>
        // public bool AddTypeConversion<T>(Func<T, byte[]> toByteFunction, Func<byte[], T> fromByteFunction, string typeShorthand) where T : notnull
        // {
        //     return dataConversionHelper.AddTypeConversion(toByteFunction, fromByteFunction);
        // }
        // /// <summary>
        // /// removes a type conversion
        // /// </summary>
        // /// <param name="type">the type of conversion to remove</param>
        // /// <returns>whether the type was removed succesfully</returns>
        // public bool RemoveTypeConversion(Type type)
        // {
        //     return dataConversionHelper.RemoveTypeConversion(type);
        // }
    }
}