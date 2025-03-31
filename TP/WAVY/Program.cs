using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class WavyClient
{
    private static string aggregatorIP = "127.0.0.1";
    private static int aggregatorPort = 7000;
    private static string wavyID = "WAVY_002";

    static void Main()
    {
        try
        {
            using (TcpClient client = new TcpClient(aggregatorIP, aggregatorPort))
            using (NetworkStream stream = client.GetStream())
            {
                Console.WriteLine("Conectado ao AGREGADOR!");

                // Enviar REGISTRO
                SendMessage(stream, $"REGISTRO|{wavyID}|ACELEROMETRO,GIROSCOPIO,HIDROFONE,ACUSTICO,CAMERA");

                // Enviar dados simulados com valores nulos (0) em alguns
                for (int i = 0; i < 5; i++)
                {
                    Random rnd = new Random();
                    string valores = $"{RandomOrZero(rnd)},{RandomOrZero(rnd)},{RandomOrZero(rnd)},{RandomOrZero(rnd)},{RandomOrZero(rnd)}";
                    string mensagem = $"DADOS|{wavyID}|{valores}";
                    SendMessage(stream, mensagem);
                    Thread.Sleep(2000);
                }

                SendMessage(stream, $"FIM|{wavyID}");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"Erro: {e.Message}");
        }
    }

    static string RandomOrZero(Random rnd)
    {
        return rnd.NextDouble() < 0.5 ? rnd.Next(1, 100).ToString() : "0";
    }

    static void SendMessage(NetworkStream stream, string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message + "\n");
        stream.Write(data, 0, data.Length);
        Console.WriteLine($"Enviado: {message}");
    }
}
