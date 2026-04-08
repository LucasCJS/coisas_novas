namespace MonitorListas.Server.Models
{
    public class Requisicao
    {
        public string? GuidRequisicao { get; set; } // Antigo guid_requisicao
        public string? Tipo { get; set; }
        public string? Tribunal { get; set; }
        public int? IdCliente { get; set; }
        public string? Documento { get; set; }
        public string? NomeParte { get; set; }
        public string? Status { get; set; }
        public DateTime DtRequisicao { get; set; }
        public string? DeErro { get; set; }
        public DateTime? DtStatus { get; set; }
        public string? CdMsgProcessamento { get; set; }
        public string? Cidade { get; set; }
        public int? NumThread { get; set; }
        public string? ThreadServerName { get; set; }
        public DateTime DtRequisicaoInicio { get; set; }
    }
}
