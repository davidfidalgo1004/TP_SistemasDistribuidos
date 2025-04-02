using System;
using System.IO;
using System.Text;

namespace Agregador
{
    public class CsvExporter
    {
        public static void ExportarDadosWavys(List<Wavy> wavys, string diretorioDestino)
        {
            Directory.CreateDirectory(diretorioDestino);

            foreach (var wavy in wavys)
            {
                var sb = new StringBuilder();
                sb.AppendLine("WavyID,Sensor,DataHora,Chave,Valor,DadosEnviados");

                foreach (var sensor in wavy.Sensores)
                {
                    foreach (var par in sensor.Valores)
                    {
                        sb.AppendLine($"{wavy.Name},{sensor.Nome},{sensor.DataHora:yyyy-MM-dd HH:mm:ss},{par.Key},{par.Value},{sensor.DadosEnviados}");
                    }
                }

                string nomeFicheiro = Path.Combine(diretorioDestino, $"{wavy.Name}.csv");
                File.WriteAllText(nomeFicheiro, sb.ToString());
            }
        }

        public static void MarcarComoEnviado(List<Wavy> wavys)
        {
            foreach (var wavy in wavys)
            {
                foreach (var sensor in wavy.Sensores)
                {
                    sensor.DadosEnviados = true;
                }
            }
        }

        public static void LimparCsv(string diretorio, string ficheiroEspecifico = "")
        {
            if (ficheiroEspecifico == ".")
            {
                foreach (var path in Directory.GetFiles(diretorio, "*.csv"))
                {
                    LimparFicheiro(path);
                }
            }
            else
            {
                string caminho = Path.Combine(diretorio, ficheiroEspecifico);
                if (File.Exists(caminho))
                {
                    LimparFicheiro(caminho);
                }
                else
                {
                    Console.WriteLine("Nao foi possivel encontrar o ficheiro em questao");
                }
            }
        }

        private static void LimparFicheiro(string path)
        {
            var linhas = File.ReadAllLines(path);
            var sb = new StringBuilder();
            sb.AppendLine(linhas[0]); // cabeçalho

            for (int i = 1; i < linhas.Length; i++)
            {
                if (!linhas[i].Trim().EndsWith("true"))
                {
                    sb.AppendLine(linhas[i]);
                }
            }

            File.WriteAllText(path, sb.ToString());
            Console.WriteLine($"Ficheiro limpo: {Path.GetFileName(path)}");
        }
    }
}