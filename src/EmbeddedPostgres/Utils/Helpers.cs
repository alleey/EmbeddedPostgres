using EmbeddedPostgres.Core.Interfaces;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace EmbeddedPostgres.Utils;

public static class Helpers
{
    public static void WaitForServerStartup(string host, int port, int waitTimeoutMs = 30000)
    {
        bool VerifyReady()
        {
            using var tcpClient = new TcpClient();
            try
            {
                tcpClient.Connect(host, port);
                return true;
            }
            catch (Exception)
            {
                // intentionally left unhandled
            }

            return false;
        }

        Stopwatch watch = new Stopwatch();
        while (watch.ElapsedMilliseconds < waitTimeoutMs)
        {
            // verify if server ready
            if (VerifyReady())
            {
                return;
            }

            Thread.Sleep(100);
        }

        throw new IOException($"Gave up waiting for server to start after {waitTimeoutMs}ms");
    }

    public static int GetAvailablePort(int startingPort=5500)
    {
        List<int> portArray = new List<int>();

        IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();

        //getting active connections
        TcpConnectionInformation[] connections = properties.GetActiveTcpConnections();
        portArray.AddRange(from n in connections
            where n.LocalEndPoint.Port >= startingPort
            select n.LocalEndPoint.Port);

        //getting active tcp listeners
        var endPoints = properties.GetActiveTcpListeners();
        portArray.AddRange(from n in endPoints
            where n.Port >= startingPort
            select n.Port);

        //getting active udp listeners
        endPoints = properties.GetActiveUdpListeners();
        portArray.AddRange(from n in endPoints
            where n.Port >= startingPort
            select n.Port);

        portArray.Sort();

        for (int i = startingPort; i < UInt16.MaxValue; i++)
            if (!portArray.Contains(i))
                return i;

        return 0;
    }
}