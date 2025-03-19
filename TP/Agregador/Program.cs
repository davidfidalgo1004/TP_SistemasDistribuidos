using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class TcpChatClient
{
    static void Main()
    {
        try
        {
            string server = "127.0.0.1";
            int port = 13000; //Porta servidor
            TcpClient agregador = new TcpClient(server, port);
            NetworkStream stream = agregador.GetStream();
            byte[] buffer = new byte[256];

            // Criar thread para receber mensagens
            Thread receiveThread = new Thread(() =>
            {
                try
                {
                    while (true)
                    {
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead == 0) break; //Loop infinito

                        string receivedMessage = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                        Console.WriteLine("\nServidor: " + receivedMessage);
                        Console.Write("Agregador: ");
                    }
                }
                catch { }
            });
            receiveThread.Start();

            // Loop para envio de mensagens
            while (true)
            {
                Console.Write("Agregador: ");
                string message = Console.ReadLine(); //O que se escrever no terminal
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
