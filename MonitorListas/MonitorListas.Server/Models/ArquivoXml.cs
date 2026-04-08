using System;

namespace MonitorListas.Server.Models
{
    public class ArquivoXml
    {
        public string Nome { get; set; } = string.Empty;
        public DateTime DataGeracao { get; set; }
        // NOSSOS CAMPOS NOVOS AQUI:
        public bool IsValido { get; set; } = true;
        public List<string> Erros { get; set; } = new List<string>();
    }
}
