using MonitorListas.Server.Helpers;
using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Runtime.InteropServices;

namespace MonitorListas.Server.Security
{
    public static class ConnectionStringBuilder
    {
        public static string Build(string sistema, string empresa)
        {
            string connectionString = "";
            string caminhoConfig = "";

            try
            {
                // Verifica a plataforma (Windows vs Linux/Docker)
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    string systemFolder = Utils.GetSpecialFolder(Environment.SpecialFolder.System);
                    caminhoConfig = Path.Combine(systemFolder, "advice.xml");
                    string caminhoSysWow64 = caminhoConfig.ToUpper().Replace("SYSTEM32", "SYSWOW64");

                    if (File.Exists(caminhoSysWow64))
                    {
                        caminhoConfig = caminhoSysWow64;
                    }
                    else if (!File.Exists(caminhoConfig))
                    {
                        caminhoConfig = Utils.FnLeChaveRegistro("caminho_config");
                    }
                }
                else
                {
                    // Fallbacks baseados na arquitetura Docker da Infraestrutura (Python)
                    string[] caminhosLinux = {
                        "/app/config/Advice.xml",
                        "/app/config/advice.xml",
                        "/app/Advice.xml",
                        "/app/advice.xml",
                        "config/Advice.xml",
                        "Advice.xml",
                        "advice.xml"
                    };

                    caminhoConfig = Environment.GetEnvironmentVariable("ADVICE_XML_PATH")
                                  ?? caminhosLinux.FirstOrDefault(c => File.Exists(c))
                                  ?? "/app/config/Advice.xml";
                }

                if (!File.Exists(caminhoConfig))
                {
                    throw new FileNotFoundException($"Arquivo XML de configuração não encontrado em: {caminhoConfig}");
                }

                XmlDocument domParam = new XmlDocument();
                domParam.Load(caminhoConfig);

                XmlNode? bancoDadosNode = domParam.SelectSingleNode($"//{sistema}/EMPRESA/{empresa}/BANCO_DADOS");

                if (bancoDadosNode != null)
                {
                    string servidor = bancoDadosNode["NOME_SERVIDOR"]?.InnerText ?? "localhost";
                    string nomeBanco = bancoDadosNode["NOME_BD"]?.InnerText ?? "";
                    string usuario = bancoDadosNode["USUARIO"]?.InnerText ?? "";

                    // O nó SENHA no XML original traz o caminho do arquivo CRIPTO (ex: C:\Sistemas\TRIBUNAIS.CRIPTO)
                    string caminhoSenha = bancoDadosNode["SENHA"]?.InnerText ?? "";

                    // A classe PasswordHelper se encarrega de abstrair o caminho caso esteja no Docker
                    string senha = PasswordHelper.GetPassword(caminhoSenha);

                    connectionString = $"Data Source={servidor};Initial Catalog={nomeBanco};User ID={usuario};Password={senha};TrustServerCertificate=True;";
                }
                else
                {
                    throw new Exception($"Nó BANCO_DADOS não encontrado no XML para o sistema {sistema} e empresa {empresa}.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao montar a string de conexão: {ex.Message}");
                throw;
            }

            return connectionString;
        }
    }
}