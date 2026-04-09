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
                // Lógica de localização do arquivo XML (Windows vs Linux)
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    string systemFolder = Utils.GetSpecialFolder(Environment.SpecialFolder.System);
                    caminhoConfig = Path.Combine(systemFolder, "advice.xml");
                    string caminhoSysWow64 = caminhoConfig.ToUpper().Replace("SYSTEM32", "SYSWOW64");

                    if (File.Exists(caminhoSysWow64)) caminhoConfig = caminhoSysWow64;
                    else if (!File.Exists(caminhoConfig)) caminhoConfig = Utils.FnLeChaveRegistro("caminho_config");
                }
                else
                {
                    string[] caminhosLinux = {
                        "/app/config_advice/Advice.xml",
                        "/app/config_advice/advice.xml",
                        "/app/config/Advice.xml",
                        "/app/Advice.xml"
                    };

                    caminhoConfig = Environment.GetEnvironmentVariable("ADVICE_XML_PATH")
                                  ?? caminhosLinux.FirstOrDefault(c => File.Exists(c))
                                  ?? "/app/config_advice/Advice.xml";
                }

                if (!File.Exists(caminhoConfig))
                {
                    throw new FileNotFoundException($"Arquivo XML não encontrado: {caminhoConfig}");
                }

                XmlDocument domParam = new XmlDocument();
                domParam.Load(caminhoConfig);

                XmlNode? bancoDadosNode = domParam.SelectSingleNode($"//{sistema}/EMPRESA/{empresa}/BANCO_DADOS");

                if (bancoDadosNode != null)
                {
                    // O segredo está no .Trim() e em manter a string original do XML
                    string servidor = (bancoDadosNode["NOME_SERVIDOR"]?.InnerText ?? "").Trim();
                    string nomeBanco = (bancoDadosNode["NOME_BD"]?.InnerText ?? "").Trim();
                    string usuario = (bancoDadosNode["USUARIO"]?.InnerText ?? "").Trim();
                    string caminhoSenha = (bancoDadosNode["SENHA"]?.InnerText ?? "").Trim();

                    // Pega a senha descriptografada
                    string senha = PasswordHelper.GetPassword(caminhoSenha);

                    // MONTAGEM TOTALMENTE DINÂMICA
                    // 1. Usamos exatamente o que está no XML (seja IP, Host, ou IP\Instancia)
                    // 2. Encrypt=False: Fundamental para o seu cenário sem SSL oficial (conforme DBeaver)
                    // 3. TrustServerCertificate=True: Essencial para o aperto de mão no Linux

                    // No ConnectionStringBuilder.cs
                    connectionString = $"Server={servidor};Database={nomeBanco};User Id={usuario};Password={senha};Encrypt=False;TrustServerCertificate=True;Integrated Security=False;Connect Timeout=30;MultiSubnetFailover=True;";

                    Console.WriteLine($"[DB] Conexão montada via XML para: {servidor}");
                }
                else
                {
                    throw new Exception($"Configuração não encontrada no XML para {sistema}/{empresa}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao montar string: {ex.Message}");
                throw;
            }

            return connectionString;
        }
    }
}