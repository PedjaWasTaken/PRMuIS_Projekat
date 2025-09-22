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
using System.Xml.Schema;

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

        private static readonly List<Let> ActiveFlights = new List<Let>();
        private static readonly object LockObj = new object();

        public static int N = 30;
        public static int M = 20;

        Sektor[,,] sektori = makeSectorMap(N, M);

        public async Task StartAsync()
        {
            udpListener = new UdpClient(UdpPort);
            tcpListener = new TcpListener(IPAddress.Any, TcpPort);

            Console.WriteLine($"[SERVER] UDP listening on {UdpPort}");
            Console.WriteLine($"[SERVER] TCP listening on {TcpPort}");

            tcpListener.Start();

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
                            if (startX == flight.axisStartX &&
                                startY == flight.axisStartY &&
                                startZ == flight.axisStartZ)
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
                    sektori[flight.axisStartX, flight.axisStartY, flight.axisStartZ - 1].zauzet = false;

                    int nextX = flight.axisStartX;
                    int nextY = flight.axisStartY;

                    // Kretanje 
                    if (flight.axisEndX > flight.axisStartX)
                        nextX += 1;
                    else if (flight.axisEndX < flight.axisStartX)
                        nextX -= 1;

                    if (flight.axisEndY > flight.axisStartY)
                        nextY += 1;
                    else if (flight.axisEndY < flight.axisStartY)
                        nextY -= 1;

                    // Provera vremena 
                    if (sektori[nextX, nextY, flight.axisStartZ - 1].meteoroloskiUslovi)
                    {
                        j++;
                        Console.WriteLine($"[SERVER] Oluja! ({nextX},{nextY},{flight.axisStartZ})");

                        if (nextX + 1 < N && !sektori[nextX + 1, nextY, flight.axisStartZ - 1].meteoroloskiUslovi)
                            nextY += 1;
                        else if (nextY + 1 < M && !sektori[nextX, nextY + 1, flight.axisStartZ - 1].meteoroloskiUslovi)
                            nextX += 1;
                        else if (nextX - 1 >= 0 && !sektori[nextX - 1, nextY, flight.axisStartZ - 1].meteoroloskiUslovi)
                            nextY -= 1;
                        else if (nextY - 1 >= 0 && !sektori[nextX, nextY - 1, flight.axisStartZ - 1].meteoroloskiUslovi)
                            nextX -= 1;
                        else
                        {
                            //ako nekako zavrsi u olujnom sektoru
                            Console.WriteLine("[SERVER] Plane stuck :(.");
                            sektori[flight.axisStartX, flight.axisStartY, flight.axisStartZ - 1].zauzet = true;
                            await Task.Delay(2000);
                            continue;
                        }
                    }

                    flight.axisStartX = nextX;
                    flight.axisStartY = nextY;

                    // Kolizija
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

                                if (flight.letelica.trenutnoPutnika < other.letelica.trenutnoPutnika &&
                                    flight.axisStartZ < 3)
                                {
                                    flight.axisStartZ += 1;
                                    Console.WriteLine($"[SERVER] Vertical shift: {flight.letelica.imeLetelice} climbed to Z={flight.axisStartZ}");
                                }
                                else if (other.axisStartZ < 3)
                                {
                                    other.axisStartZ += 1;
                                    Console.WriteLine($"[SERVER] Vertical shift: {other.letelica.imeLetelice} climbed to Z={other.axisStartZ}");
                                }
                                else
                                {
                                    flight.axisStartX += 1;
                                    flight.axisStartY += 1;
                                    Console.WriteLine($"[SERVER] Conflict resolved horizontally: {flight.letelica.imeLetelice} moved to ({flight.axisStartX},{flight.axisStartY},{flight.axisStartZ})");
                                }
                                break;
                            }
                        }
                    }

                    sektori[flight.axisStartX, flight.axisStartY, flight.axisStartZ - 1].zauzet = true;

                    lock (LockObj)
                    {
                        int index = ActiveFlights.IndexOf(flight);
                        if (index >= 0)
                            ActiveFlights[index] = flight;
                    }

                    i++;
                    int estimated = Math.Max(Math.Abs(flight.axisStartX - flight.axisEndX), Math.Abs(flight.axisStartX - flight.axisEndX)); 
                    string update = $"Position update {i}: Current location {flight.axisStartX}, {flight.axisStartY}, {flight.axisStartZ} heading to {flight.axisEndX},{flight.axisEndY}\t Course corrections: {j}\t Estimated sectors till goal: {estimated}\n";
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

                lock (LockObj)
                {
                    sektori[flight.axisStartX, flight.axisStartY, flight.axisStartZ - 1].zauzet = false;
                    ActiveFlights.Remove(flight);
                }
                printSektorMap(sektori, N, M);
            }

            tcpClient.Close();
        }

        static Random r = new Random();
        public static Sektor[,,] makeSectorMap(int N, int M)
        {
            Sektor[,,] sektori = new Sektor[N, M, 3];
            for (int j = 0; j < M; j++)
            {
                for (int i = 0; i < N; i++)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        sektori[i, j, k] = new Sektor
                        {
                            axisX = i,
                            axisY = j,
                            axisZ = k + 1,
                            zauzet = false
                        };
                    }

                    if (r.Next(0, 100) < 10)
                    {
                        sektori[i, j, 0].meteoroloskiUslovi = true;
                        sektori[i, j, 1].meteoroloskiUslovi = true;
                        sektori[i, j, 2].meteoroloskiUslovi = true;
                    }
                }
            }
            return sektori;
        }

        public void printSektorMap(Sektor[,,] sektori, int N, int M)
        {
            Console.Clear();
            Console.WriteLine("=== SECTOR MAP ===");

            lock (LockObj)
            {
                for (int j = 0; j < M; j++)
                {
                    for (int i = 0; i < N; i++)
                    {
                        int flightNumber = -1;

                        for (int k = 0; k < 3; k++)
                        {
                            for (int index = 0; index < ActiveFlights.Count; index++)
                            {
                                var flight = ActiveFlights[index];
                                if (flight.axisStartX == i && flight.axisStartY == j && flight.axisStartZ == k + 1)
                                {
                                    flightNumber = index + 1; 
                                    break;
                                }
                            }
                            if (flightNumber != -1)
                                break;
                        }

                        bool hasWeather = false;
                        for (int k = 0; k < 3; k++)
                        {
                            if (sektori[i, j, k].meteoroloskiUslovi)
                            {
                                hasWeather = true;
                                break;
                            }
                        }

                        if (flightNumber != -1)
                            Console.Write($"[{flightNumber}]"); 
                        else if (hasWeather)
                            Console.Write(" X ");
                        else
                            Console.Write("[ ]");
                    }
                    Console.WriteLine();
                }

                // Letovi
                Console.WriteLine("\n=== ACTIVE FLIGHTS ===");
                if (ActiveFlights.Count == 0)
                {
                    Console.WriteLine("No active flights.");
                }
                else
                {
                    for (int i = 0; i < ActiveFlights.Count; i++)
                    {
                        var flight = ActiveFlights[i];
                        Console.WriteLine($"{i + 1}. {flight.letelica.imeLetelice} - Current: ({flight.axisStartX},{flight.axisStartY},{flight.axisStartZ}) " +
                                          $"Destination: ({flight.axisEndX},{flight.axisEndY})");
                    }
                }
            }
        }
    }
}