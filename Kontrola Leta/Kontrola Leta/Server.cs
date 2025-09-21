using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using Library;



namespace Kontrola_Leta
{
    internal class Server
    {
        static void Main(string[] args)
        {
            int N = 30;
            int M = 10;

            Socket serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint server = new IPEndPoint(IPAddress.Any, 15011);
            serverSocket.Bind(server);
            serverSocket.Blocking = false;
            int maxLetova = 20;
            serverSocket.Listen(maxLetova);


            byte[] buffer = new byte[4096];
            bool kraj = false;
            Random r = new Random();

            List<Socket> letovi = new List<Socket>();

            try
            {
                //Inicijalizacija sektorske mape
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
                            sektori[i, j, k].axisZ = k+1;

                            sektori[i, j, k].zauzet = false;
                            
                        }
                        //Predpostavka da na svakoj visini ima nevreme
                        if(r.Next(0, 100) < 10)
                        {
                            sektori[i, j, 0].meteoroloskiUslovi = true;
                            sektori[i, j, 1].meteoroloskiUslovi = true;
                            sektori[i, j, 2].meteoroloskiUslovi = true;
                        }
                    }
                }

                while(!kraj)
                {



                    //Ispis sektorske mape
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
                    Console.Read();
                }
            }
            catch(SocketException ex)
            {
                Console.WriteLine($"Doslo je do greske: {ex}");
            }


            Console.WriteLine("Server zavrsava sa radom");
            serverSocket.Close();
            Console.ReadKey();
        }
    }
}
