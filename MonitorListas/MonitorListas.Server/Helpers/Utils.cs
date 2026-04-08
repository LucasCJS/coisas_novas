using Microsoft.Win32;

namespace MonitorListas.Server.Helpers
{
    public static class Utils
    {
        public static string GetSpecialFolder(Environment.SpecialFolder mFolder)
        {
            return Environment.GetFolderPath(mFolder);
        }

        public static string FnLeChaveRegistro(string pChave)
        {
            // Trava de segurança: Garante que o código não quebre se tentar rodar em um Linux/Docker no futuro
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("O Registro do Windows só é suportado em ambiente Windows.");
            }

            // Abre o LocalMachine forçando a leitura na área de 32-bits (onde o legado costuma gravar)
            using RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
            using RegistryKey adviceKey = baseKey.OpenSubKey(@"SOFTWARE\Advice");

            // Se não achar no 32-bits, tenta no 64-bits normal
            if (adviceKey == null)
            {
                using RegistryKey baseKey64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
                using RegistryKey adviceKey64 = baseKey64.OpenSubKey(@"SOFTWARE\Advice");

                if (adviceKey64 == null)
                {
                    throw new Exception(@"Chave SOFTWARE\Advice não encontrada nem em 32-bits nem em 64-bits no Registro.");
                }

                object? value64 = adviceKey64.GetValue(pChave);
                return value64?.ToString() ?? throw new Exception($"Valor '{pChave}' não encontrado.");
            }

            object? objectValue = adviceKey.GetValue(pChave);
            if (objectValue == null)
            {
                throw new Exception($"Valor '{pChave}' não encontrado dentro de SOFTWARE\\Advice.");
            }

            return objectValue.ToString()!;
        }
    }
}
