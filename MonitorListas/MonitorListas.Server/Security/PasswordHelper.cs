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
            // Se estivermos no Linux OU o arquivo original do Windows não existir, busca nas pastas do Docker
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || !File.Exists(caminhoReal))
            {
                // Mesmos caminhos de fallback que a equipe de Infra configurou no Python
                string[] caminhosAlternativos = {
                    "/app/config/INTEGRACAOADV.CRIPTO",
                    "/app/INTEGRACAOADV.CRIPTO",
                    "config/INTEGRACAOADV.CRIPTO",
                    "INTEGRACAOADV.CRIPTO",
                    "./INTEGRACAOADV.CRIPTO"
                };

                caminhoReal = caminhosAlternativos.FirstOrDefault(c => File.Exists(c)) ?? caminhoReal;
            }

            if (!File.Exists(caminhoReal))
            {
                throw new FileNotFoundException($"Arquivo de senha CRIPTO não encontrado. Tentado original: {caminhoOriginalNoXml} e alternativas no Docker.");
            }

            // 2. LÊ O ARQUIVO FÍSICO
            // O Python lê usando "iso-8859-1" (Latin1 no C#). Isso é crucial para os caracteres não quebrarem.
            string encryptedPassword = File.ReadAllText(caminhoReal, Encoding.Latin1);

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
