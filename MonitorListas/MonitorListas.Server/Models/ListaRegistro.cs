namespace MonitorListas.Server.Models
{
    public class ListaRegistro
    {
        public string? CodigoLista { get; set; }
        public string? NomeLista { get; set; }
        public string? StatusLista { get; set; }
        public DateTime InicioExecucao { get; set; }
        public DateTime FimExecucao { get; set; }
    }
}
