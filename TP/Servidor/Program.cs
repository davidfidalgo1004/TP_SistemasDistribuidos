using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

class MyTcpListener
{
    public static void Main()
    {
        try
        {
            int port = 13000;
            IPAddress localAddr = IPAddress.Parse("127.0.0.1");
            TcpListener server = new TcpListener(localAddr, port);
            server.Start();

            Console.WriteLine("Servidor esperando conexão...");
            TcpClient agregador = server.AcceptTcpClient();
            Console.WriteLine("Cliente conectado!"); //Ligação Bilateral, entre apenas o servidor e o agregador

            NetworkStream stream = agregador.GetStream();
            byte[] buffer = new byte[256];
            string msgOK = "100 OK";
            byte[] dataOK = Encoding.ASCII.GetBytes(msgOK);
            stream.Write(dataOK, 0, dataOK.Length);
            // Criar thread para receber mensagens
            Thread receiveThread = new Thread(() =>
            {
                try
                {
                    while (true)
                    {
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead == 0) break;

                        string receivedMessage = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                        Console.WriteLine("\nAgregador: " + receivedMessage);
                        Console.Write("Servidor: ");
                    }
                }
                catch { }
            });
            receiveThread.Start();

            // Loop para envio de mensagens
            while (true)
            {
                Console.Write("Servidor: ");
                string message = Console.ReadLine();
                byte[] data = Encoding.ASCII.GetBytes(message);
                stream.Write(data, 0, data.Length);
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Erro: " + e.Message);
        }
    }
}