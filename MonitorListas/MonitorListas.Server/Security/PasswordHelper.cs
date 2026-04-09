using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace MonitorListas.Server.Security
{
    internal class PasswordHelper
    {
        //public static string GetPassword(string path)
        //{
        //    string encryptedPassword;
        //    using (StreamReader sr = new StreamReader(path, Encoding.GetEncoding("ISO-8859-1")))
        //    {
        //        encryptedPassword = sr.ReadToEnd();
        //    }
        //    return DecryptDatabasePassword(encryptedPassword);
        //}

        //private static string DecryptDatabasePassword(string encryptedText)
        //{
        //    int keyIndex = 0;
        //    string decryptedPassword = "";
        //    string key = "bancodadossql";
        //    int value = 1;

        //    foreach (char c in encryptedText)
        //    {
        //        int decryptedChar = c - key[keyIndex] - value;
        //        decryptedPassword += (char)decryptedChar;

        //        keyIndex++;
        //        if (keyIndex >= key.Length)
        //            keyIndex = 0;
        //    }
        //    return decryptedPassword;
        //}

        public static string GetPassword(string caminhoOriginalNoXml)
        {
            string caminhoReal = caminhoOriginalNoXml;

            // 1. TRATAMENTO CROSS-PLATFORM (Windows vs Linux/Docker)
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Extraímos o nome do arquivo que está no XML (ex: INTEGRACAOADV.CRIPTO ou CORP.CRIPTO)
                // Usamos Split('\\') porque o caminho no XML vem com padrão Windows
                string nomeArquivo = caminhoOriginalNoXml.Split('\\').Last();

                // Buscamos nos caminhos que realmente existem no seu Docker Compose
                string[] caminhosAlternativos = {
            $"/app/config_advice/{nomeArquivo}", // Onde você mapeou no YAML
            $"/app/config/{nomeArquivo}",
            $"/app/{nomeArquivo}",
            nomeArquivo
        };

                caminhoReal = caminhosAlternativos.FirstOrDefault(c => File.Exists(c)) ?? caminhoReal;
            }

            if (!File.Exists(caminhoReal))
            {
                throw new FileNotFoundException($"Arquivo de senha CRIPTO não encontrado: {caminhoReal}");
            }

            // 2. LÊ O ARQUIVO FÍSICO (Mantendo o Encoding Latin1 para compatibilidade com o legado)
            string encryptedPassword = File.ReadAllText(caminhoReal, Encoding.GetEncoding("iso-8859-1"));

            // 3. DESCRIPTOGRAFA
            return DecryptPassword(encryptedPassword);
        }

        private static string DecryptPassword(string encryptedText)
        {
            string key = "bancodadossql";
            string result = "";

            for (int i = 0; i < encryptedText.Length; i++)
            {
                // Pega o valor numérico (ASCII) da letra atual da chave
                int shift = (int)key[i % key.Length];

                // Subtrai o shift e -1, exatamente como no script Python
                result += (char)((int)encryptedText[i] - shift - 1);
            }

            return result;
        }

        public static string GerarHashLegado(string senhaPlana)
        {
            // O mesmo prefixo usado no VB.NET antigo
            string input = "advice" + senhaPlana;

            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] inputBytes = Encoding.UTF8.GetBytes(input);
                byte[] hashBytes = sha1.ComputeHash(inputBytes);

                // O FormsAuthentication retornava o Hash em Maiúsculo
                StringBuilder sb = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    sb.Append(b.ToString("X2"));
                }
                return sb.ToString();
            }
        }
    }
}
