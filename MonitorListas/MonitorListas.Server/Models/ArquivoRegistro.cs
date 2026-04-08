using System;
using System.ComponentModel.DataAnnotations;

namespace MonitorListas.Server.Models
{
    public class ArquivoRegistro
    {
        [Key]
        public int Id { get; set; }
        public string Nome { get; set; }
        public DateTime DataGeracao { get; set; }
        public DateTime DataProcessamento { get; set; }
        public bool IsValido { get; set; }
        public string ErrosFormatados { get; set; }
    }
}