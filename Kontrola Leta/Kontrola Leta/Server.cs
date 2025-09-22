using Library;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Kontrola_Leta
{
    internal class Server
    {
        
        static void Main(string[] args)
        {
            Server server = new Server();
            server.StartAsync().GetAwaiter().GetResult();
        }

        private const int UdpPort = 1234;
        private const int TcpPort = 2345;

        private UdpClient udpListener;
        private TcpListener tcpListener;

        // Store active flights
        private static readonly List<Let> ActiveFlights = new List<Let>();
        private static readonly object LockObj = new object();

        public int N = 30;
        public int M = 20;

        public async Task StartAsync()
        {
            udpListener = new UdpClient(UdpPort);
            tcpListener = new TcpListener(IPAddress.Any, TcpPort);

            Console.WriteLine($"[SERVER] UDP listening on {UdpPort}");
            Console.WriteLine($"[SERVER] TCP listening on {TcpPort}");

            tcpListener.Start();
            
            Sektor[,,] sektori = makeSectorMap(N, M);

            _ = HandleUdpRequestsAsync(sektori);

            while (true)
            {
               

                var tcpClient = await tcpListener.AcceptTcpClientAsync();
                _ = HandleTcpFlightAsync(tcpClient, sektori);

                 
            }
        }

        private async Task HandleUdpRequestsAsync(Sektor[,,] sektori)
        {
            while (true)
            {
                var result = await udpListener.ReceiveAsync();
                string message = Encoding.UTF8.GetString(result.Buffer);
                Console.WriteLine($"[SERVER] UDP request: {message}");

                string[] parts = message.Split(';');
                if (parts.Length >= 6 && parts[0] == "REQUEST")
                {
                    int startX = int.Parse(parts[1]);
                    int startY = int.Parse(parts[2]);
                    int startZ = int.Parse(parts[3]);
                    int endX = int.Parse(parts[4]);
                    int endY = int.Parse(parts[5]);

                    string response;
                    lock (LockObj)
                    {
                        bool conflict = false;
                        foreach (var flight in ActiveFlights)
                        {
                            
                            if (startX == flight.axisStartY && startY == flight.axisStartX && startX == flight.axisStartZ)
                            {
                                conflict = true;
                                break;
                            }
                        }

                        if (conflict)
                        {
                            startX += 1;
                            startY += 1;
                            response = $"CORRECTED;{startX};{startY};{startZ}";
                        }
                        else
                        {
                            response = $"OK;{startX};{startY};{startZ}";
                        }
                    }

                    byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                    await udpListener.SendAsync(responseBytes, responseBytes.Length, result.RemoteEndPoint);
                }
                else
                {
                    string response = "DENIED";
                    byte[] responseBytes = Encoding.UTF8.GetBytes(response);
                    await udpListener.SendAsync(responseBytes, responseBytes.Length, result.RemoteEndPoint);
                }
            }
        }

        private async Task HandleTcpFlightAsync(TcpClient tcpClient, Sektor[,,] sektori)
        {
            Console.WriteLine("[SERVER] New TCP client connected.");

            using (NetworkStream stream = tcpClient.GetStream())
            {
                BinaryFormatter formatter = new BinaryFormatter();
                Let flight = (Let)formatter.Deserialize(stream);

                lock (LockObj)
                {
                    ActiveFlights.Add(flight);
                }

                Console.WriteLine($"[SERVER] Flight added: {flight.letelica.imeLetelice}, " +
                                  $"Destination: ({flight.axisEndX},{flight.axisEndY})");

                int i = 0;
                int j = 0;
                while ((flight.axisStartX != flight.axisEndX) || (flight.axisStartY != flight.axisEndY))
                {
                    //kretanje aviona ka destinaciji
                    if (flight.axisEndX > flight.axisStartX)
                        flight.axisStartX += 1;
                    if (flight.axisEndX < flight.axisStartX)
                        flight.axisStartX -= 1;
                    if (flight.axisEndY > flight.axisStartY)
                        flight.axisStartY += 1;
                    if (flight.axisEndY < flight.axisStartY)
                        flight.axisStartY -= 1;

                    lock (LockObj)
                    {
                        foreach (var other in ActiveFlights)
                        {
                            if (other != flight &&
                                other.axisStartX == flight.axisStartX &&
                                other.axisStartY == flight.axisStartY &&
                                other.axisStartZ == flight.axisStartZ)
                            {
                                j++;
                                // Collision
                                flight.axisStartX += 1;
                                flight.axisStartY += 1;
                                Console.WriteLine($"[SERVER] Conflict detected! " +
                                                  $"Adjusted {flight.letelica.imeLetelice} to " +
                                                  $"({flight.axisStartX},{flight.axisStartY},{flight.axisStartZ})");
                                break;
                            }
                        }
                    }
                                i++;

                    string update = $"Position update {i}: Current locatiion {flight.axisStartX}, {flight.axisStartY}, {flight.axisStartZ} heading to {flight.axisEndX},{flight.axisEndY}\t Number of course corrections: {j}\n";
                    byte[] buffer = Encoding.UTF8.GetBytes(update);
                    await stream.WriteAsync(buffer, 0, buffer.Length);
                    
                    printSektorMap(sektori, N, M);
                    await Task.Delay(2000);
                    

                }

                printSektorMap(sektori, N, M);

                string finalMsg = "ARRIVED at destination.\n";
                byte[] finalBuffer = Encoding.UTF8.GetBytes(finalMsg);
                await stream.WriteAsync(finalBuffer, 0, finalBuffer.Length);

                Console.WriteLine("[SERVER] Flight arrived.");

                // Remove flight from active list
                lock (LockObj)
                {
                    ActiveFlights.Remove(flight);
                }
            }

            tcpClient.Close();
        }
        Random r = new Random();
        public Sektor[,,] makeSectorMap(int N, int M)
        {
            Sektor[,,] sektori = new Sektor[N, M, 3];
            for (int j = 0; j < M; j++)
            {
                for (int i = 0; i < N; i++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        sektori[i, j, k] = new Sektor();
                        sektori[i, j, k].axisX = i;
                        sektori[i, j, k].axisY = j;
                        sektori[i, j, k].axisZ = k + 1;

                        sektori[i, j, k].zauzet = false;

                    }
                    //Predpostavka da na svakoj visini ima nevreme
                    if (r.Next(0, 100) < 10)
                    {
                        sektori[i, j, 0].meteoroloskiUslovi = true;
                        sektori[i, j, 1].meteoroloskiUslovi = true;
                        sektori[i, j, 2].meteoroloskiUslovi = true;
                    }
                }
            }return sektori;
        }
        public void printSektorMap(Sektor[,,] sektori, int N, int M)
        {
            Console.Clear();
            for (int j = 0; j < M; j++)
            {
                for (int i = 0; i < N; i++)
                {
                    if (sektori[i, j, 0].meteoroloskiUslovi == true)
                        Console.Write(" X ");
                    else
                        Console.Write("[ ]");
                }
                Console.WriteLine();
            }
            
            
        }
    }
}
   
