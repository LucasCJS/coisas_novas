namespace MonitorListas.Server.Models
{
    public class TbLista
    {
        public string? TipoLista { get; internal set; }
        public string? LinkLista { get; internal set; }
        public DateTime? DataProcessado { get; internal set; }
        public string? DescUltimoLog { get; internal set; }
        public DateTime? DataIntegSigna { get; internal set; }
        public string? DescIntecSigna { get; internal set; }
        public DateTime? UltimaExecucao { get; internal set; }
    }
}
