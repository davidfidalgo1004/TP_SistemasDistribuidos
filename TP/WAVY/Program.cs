using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class WavyClient
{
    private static string aggregatorIP = "127.0.0.1"; // IP do AGREGADOR
    private static int port = 4000;                  // Porta do AGREGADOR
    private static string wavyID = "WAVY_001";

    static void Main()
    {
        try
        {
            using (TcpClient client = new TcpClient(aggregatorIP, port))
            using (NetworkStream stream = client.GetStream())
            {
                Console.WriteLine("Conectado ao AGREGADOR!");

                // Enviar registo
                SendMessage(stream, $"REGISTRO|{wavyID}|ACELEROMETRO,GIROSCOPIO");

                // Enviar dados periodicamente
                for (int i = 0; i < 5; i++)
                {
                    SendMessage(stream, $"DADOS|{wavyID}|ACELEROMETRO|{new Random().Next(1, 100)}");
                    Thread.Sleep(2000);
                }

                // Finalizar conexão
                SendMessage(stream, $"FIM|{wavyID}");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Erro: {e.Message}");
        }
    }

    static void SendMessage(NetworkStream stream, string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);
        stream.Write(data, 0, data.Length);
        Console.WriteLine($"Enviado: {message}");
    }
}
