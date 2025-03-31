using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class WavyClient
{
    private static string aggregatorIP = "127.0.0.1";
    private static int aggregatorPort = 7000;
    private static Timer envioTimer;
    private static TcpClient client;
    private static NetworkStream stream;

    static void Main()
    {
        try
        {
            client = new TcpClient(aggregatorIP, aggregatorPort);
            stream = client.GetStream();

            Console.WriteLine("Ligação estabelecida com o agregador.");

            // Envio imediato e depois a cada 30 minutos
            envioTimer = new Timer(EnviarDadosCallback, null, TimeSpan.Zero, TimeSpan.FromMinutes(30));

            // Mantém o programa vivo
            while (true)
            {
                Thread.Sleep(1000);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao conectar ao agregador: " + ex.Message);
        }
    }

    static void EnviarDadosCallback(object state)
    {
        try
        {
            if (stream != null && stream.CanWrite)
            {
                // Simula um dado de sensor (personalize conforme necessário)
                string dados = $"DADOS {{\"timestamp\":\"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\",\"valor\":{new Random().Next(0, 100)}}}";

                byte[] message = Encoding.UTF8.GetBytes(dados);
                stream.Write(message, 0, message.Length);

                Console.WriteLine($"Dados enviados ao agregador: {dados}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Erro ao enviar dados: " + ex.Message);
        }
    }
}
