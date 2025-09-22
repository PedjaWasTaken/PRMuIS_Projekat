using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using Library;



namespace Client
{
    internal class Client
    {
        public static Random random = new Random();
        public static string RandomString(int length)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
        static void Main(string[] args)
        {

            Console.WriteLine("Client Zapocet");

            

            Console.Write("Unesite pocetnu X koordinatu: ");
            int startX = int.Parse(Console.ReadLine());
            Console.Write("Unesite pocetnu Y koordinatu: ");
            int startY = int.Parse(Console.ReadLine());
            int startZ = 1; // Uvek 1

            Console.Write("Unesite krajnu X koordinatu: ");
            int endX = int.Parse(Console.ReadLine());
            Console.Write("Unesite krajnu Y koordinatu: ");
            int endY = int.Parse(Console.ReadLine());


            Socket udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint serverUdpEP = new IPEndPoint(IPAddress.Loopback, 1234);

            try
            {
                // UDP slanje
                string request = $"REQUEST;{startX};{startY};{startZ};{endX};{endY};";
                byte[] requestBytes = Encoding.UTF8.GetBytes(request);
                udpSocket.SendTo(requestBytes, serverUdpEP);

                // UDP odgovor
                udpSocket.ReceiveTimeout = 3000;
                EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                byte[] responseBytes = new byte[1024];
                int received = udpSocket.ReceiveFrom(responseBytes, ref remoteEP);
                string response = Encoding.UTF8.GetString(responseBytes, 0, received);
                Console.WriteLine("UDP odgovor: " + response);

                string[] parts = response.Split(';');
                if (parts[0] == "OK" || parts[0] == "CORRECTED")
                {
                    // Potvrda, i povratak informacija
                    int confirmedX = int.Parse(parts[1]);
                    int confirmedY = int.Parse(parts[2]);
                    int confirmedZ = int.Parse(parts[3]);

                    int maxPutnika = random.Next(50, 200);

                    // TCP komunikacija
                    using (Socket tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
                    {
                        tcpSocket.Connect(serverUdpEP.Address, 2345);
                        using (NetworkStream ns = new NetworkStream(tcpSocket))
                        {

                            //Avion je nasumicno generisan
                            Letelica letelica = new Letelica
                            {
                                imeLetelice = RandomString(15),
                                imePilota = RandomString(10),
                                registracijaLetelice = RandomString(20),
                                maxPutnika = maxPutnika,
                                trenutnoPutnika = random.Next(1,maxPutnika)
                            };
                            Let let = new Let
                            {
                                letelica = letelica,
                                axisStartX = confirmedX,
                                axisStartY = confirmedY,
                                axisStartZ = confirmedZ,
                                axisEndX = endX,
                                axisEndY = endY,
                                
                            };

                            // Send Let object
                            BinaryFormatter formatter = new BinaryFormatter();
                            formatter.Serialize(ns, let);

                            // 4. Receive next positions from server
                            byte[] buffer = new byte[1024];
                            while (true)
                            {
                                int bytesRead = ns.Read(buffer, 0, buffer.Length);
                                string nextPos = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                                Console.WriteLine("Next position: " + nextPos);
                                if (nextPos.StartsWith("ARRIVED")) break;
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Flight request denied or invalid response.");
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
            udpSocket.Close();
            Console.WriteLine("Client closed.");
            Console.ReadLine();
        }
    }
}
    
