using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Configuration;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Proxy
{
    class Program
    {
        static void Main(string[] args)
        {
            var listener = new TcpListener(IPAddress.Parse("127.0.0.1"), 8001);
            listener.Start();

            try
            {
                while (true)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    var thread = new Thread(() => ProcessRequest(client));
                    thread.Start();
                }
            }
            catch
            {
                listener.Stop();
            }
        }

        public static void ProcessRequest(TcpClient client)
        {
            NetworkStream browserStream = client.GetStream();
            string mainHostRequest;
            var buf = new byte[client.ReceiveBufferSize];
            while (true)
            {
                try
                {
                    var builder = new StringBuilder();
                    int bytes = 0;
                    do
                    {
                        bytes = browserStream.Read(buf, 0, buf.Length);
                        builder.Append(Encoding.ASCII.GetString(buf, 0, bytes));
                    }
                    while (browserStream.DataAvailable);
                    mainHostRequest = builder.ToString();
                    ForwardRequest(browserStream, client, mainHostRequest);
                }
                catch
                {
                    return;
                }
            }
        }

        public static void ForwardRequest(NetworkStream browserStream, TcpClient client, string request)
        {
            try
            {
                string[] lines = request.Trim().Split(new char[] { '\r', '\n' });

                // get { <host>, <port> } array
                string hostHeader = lines.FirstOrDefault(x => x.Contains("Host"));
                if (hostHeader == null) return;
                hostHeader = hostHeader.Substring(hostHeader.IndexOf(":") + 2);                
                string[] host = hostHeader.Trim().Split(':');

                // set up default port if need
                if (host.Length == 1)
                {
                    Array.Resize(ref host, 2);
                    host[1] = "80";
                }

                string[] startingLine = lines[0].Trim().Split(' ');
                string documentUri = startingLine[1];

                // make responsive uri 
                if (startingLine[1].Contains(host[0]))
                {
                    documentUri = startingLine[1].Remove(0, startingLine[1].IndexOf(host[0]));
                //    Console.WriteLine(documentUri);
                    documentUri = documentUri.Remove(0, documentUri.IndexOf('/'));                    
                }

                // form new request (<method> <uri> <version>)
                request = request.Substring(0, request.IndexOf(startingLine[1])) + documentUri + " " + request.Substring(request.IndexOf("HTTP/1.1"));

                var forwardingClient = new TcpClient(host[0], int.Parse(host[1]));
                NetworkStream forwardingClientStream = forwardingClient.GetStream();
                byte[] data = Encoding.ASCII.GetBytes(request);
                forwardingClientStream.Write(data, 0, data.Length);

                byte[] responseBuf = new byte[256];
                forwardingClientStream.Read(responseBuf, 0, 256);
                browserStream.Write(responseBuf, 0, responseBuf.Length);
                
                string[] head = Encoding.ASCII.GetString(responseBuf).Split(new char[] { '\r', '\n' });
                string ResponseCode = head[0].Substring(head[0].IndexOf(" ") + 1);
                Console.WriteLine($"Request to:\n{host[0]}\nResponse:");
                Console.WriteLine($"{host[0]} - {ResponseCode}\n");
                forwardingClientStream.CopyTo(browserStream);
            }
            catch (Exception ep)
            {
           //     Console.WriteLine(ep.ToString());
                return;
            }
            finally
            {
                client.Dispose();
            }

        }

    }

}

