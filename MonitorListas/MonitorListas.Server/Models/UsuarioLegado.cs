using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MonitorListas.Server.Models
{
    [Table("ADUSU_USUARIOS")]
    public class UsuarioLegado
    {
        [Key]
        [Column("CD_USUARIO")]
        public int Id { get; set; }

        [Column("CD_LOGIN")]
        public string Login { get; set; }

        [Column("CD_SENHA")]
        public string SenhaHash { get; set; }

        [Column("CD_SITUACAO")]
        public short Situacao { get; set; } // <--- O ERRO ESTAVA AQUI. Mude de int para short.

        [Column("DS_EMAIL")]
        public string Email { get; set; }

        [Column("NM_USUARIO")]
        public string Nome { get; set; }
    }
}