using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class AggregatorServer
{
    private static int port = 7000;
    private static string dataFile = "dados/dados_agregados.csv";
    private static object fileLock = new object();

    private static string serverIP = "127.0.0.1"; // IP do SERVIDOR
    private static int serverPort = 6000;         // Porta do SERVIDOR

    static void Main()
    {
        // Garante que a pasta e o ficheiro existem
        if (!Directory.Exists("dados"))
            Directory.CreateDirectory("dados");

        if (!File.Exists(dataFile))
        {
            File.WriteAllText(dataFile, "Timestamp,WAVY_ID,Acelerometro,Giroscopio,Hidrofone,Acustico,Camera\n");
        }

        TcpListener server = new TcpListener(IPAddress.Any, port);
        server.Start();
        Console.WriteLine("AGREGADOR pronto para receber conexões...");

        while (true)
        {
            TcpClient client = server.AcceptTcpClient();
            Thread clientThread = new Thread(() => HandleClient(client));
            clientThread.Start();
        }
    }

    static void HandleClient(TcpClient client)
    {
        using (NetworkStream stream = client.GetStream())
        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                Console.WriteLine($"Recebido: {line}");

                if (line.StartsWith("DADOS"))
                {
                    ProcessData(line);
                }
                else if (line.StartsWith("REGISTO"))
                {
                    Console.WriteLine("Nova WAVY registrada.");
                }
                else if (line.StartsWith("FIM"))
                {
                    Console.WriteLine("Fim da comunicação com WAVY.");
                }
                else
                {
                    Console.WriteLine("Mensagem não reconhecida.");
                }
            }
        }
    }

    static void ProcessData(string message)
    {
        // Formato: DADOS|WAVY_ID|valores separados por vírgula
        string[] parts = message.Split('|');
        if (parts.Length == 3)
        {
            string wavyId = parts[1];
            string[] valores = parts[2].Split(',');

            if (valores.Length == 5) // 5 tipos de sensores
            {
                string acel = valores[0];
                string giro = valores[1];
                string hidro = valores[2];
                string acustico = valores[3];
                string camera = valores[4];
                string timestamp = DateTime.UtcNow.ToString("s");

                string linhaCsv = $"{timestamp},{wavyId},{acel},{giro},{hidro},{acustico},{camera}";

                lock (fileLock)
                {
                    File.AppendAllText(dataFile, linhaCsv + "\n");
                }

                Console.WriteLine($"Salvo no CSV: {linhaCsv}");

                // Encaminhar para o servidor
                ForwardToServer(linhaCsv);
            }
            else
            {
                Console.WriteLine("Número incorreto de valores na mensagem.");
            }
        }
        else
        {
            Console.WriteLine("Formato de mensagem inválido.");
        }
    }

    static void ForwardToServer(string data)
    {
        try
        {
            using (TcpClient client = new TcpClient(serverIP, serverPort))
            using (NetworkStream stream = client.GetStream())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(data + "\n");
                stream.Write(bytes, 0, bytes.Length);
                Console.WriteLine($"Encaminhado ao SERVIDOR: {data}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao enviar dados ao servidor: {ex.Message}");
        }
    }
}

