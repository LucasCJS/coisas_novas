using Microsoft.EntityFrameworkCore;
using MonitorListas.Server.Models;

namespace MonitorListas.Server.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Requisicao> Requisicoes { get; set; }

        public DbSet<TbLista> TbLista { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // --- MAPEAMENTO TB_REQUISICOES ---
            modelBuilder.Entity<Requisicao>(entity =>
            {
                entity.ToTable("TB_REQUISICOES"); // Substitui o [Table]
                entity.HasKey(e => e.GuidRequisicao); // Substitui o [Key]

                // Avisando o EF que o nome no banco está diferente do nome no C#
                entity.Property(e => e.GuidRequisicao).HasColumnName("guid_requisicao");
                entity.Property(e => e.IdCliente).HasColumnName("id_cliente");
                entity.Property(e => e.NomeParte).HasColumnName("nome_parte");
                entity.Property(e => e.DtRequisicao).HasColumnName("dt_requisicao");
                entity.Property(e => e.DeErro).HasColumnName("de_erro");
                entity.Property(e => e.DtStatus).HasColumnName("dt_status");
                entity.Property(e => e.CdMsgProcessamento).HasColumnName("cd_msg_processamento");
                entity.Property(e => e.NumThread).HasColumnName("NUM_THREAD");
                entity.Property(e => e.ThreadServerName).HasColumnName("THREAD_SERVER_NAME");
                entity.Property(e => e.DtRequisicaoInicio).HasColumnName("dt_requisicao_inicio");
            });

            // --- MAPEAMENTO TB_LISTA ---
            modelBuilder.Entity<TbLista>(entity =>
            {
                entity.ToTable("TB_LISTA"); // Substitui o [Table]
                entity.HasKey(e => e.TipoLista); // Substitui o [Key]

                // Avisando o EF que o nome no banco está diferente do nome no C#
                entity.Property(e => e.TipoLista).HasColumnName("TIPO");
                entity.Property(e => e.LinkLista).HasColumnName("LINK");
                entity.Property(e => e.DataProcessado).HasColumnName("DT_PROCESSADO");
                entity.Property(e => e.DescUltimoLog).HasColumnName("DE_ULTIMO_LOG");
                entity.Property(e => e.DataIntegSigna).HasColumnName("DT_INTEG_SIGNA");
                entity.Property(e => e.DescIntecSigna).HasColumnName("DE_INTEG_SIGNA");
                entity.Property(e => e.UltimaExecucao).HasColumnName("ULTIMA_EXEC");
            });
        }
    }
}
