using Microsoft.EntityFrameworkCore;
using MonitorListas.Server.Data;
using MonitorListas.Server.Models;
using MonitorListas.Server.Security;

namespace MonitorListas.Server.Services
{
    public class AuthService
    {
        private readonly CorpDbContext _corpDb;

        public AuthService(CorpDbContext corpDb)
        {
            _corpDb = corpDb;
        }
        public async Task<(bool Sucesso, UsuarioLegado Usuario, string Mensagem)> ValidarUsuarioAsync(string login, string senhaPlana)
        {
            // 1. Busca o usuário na base CORP
            var usuario = await _corpDb.Usuarios
                .FirstOrDefaultAsync(u => u.Login == login);

            if (usuario == null)
            {
                return (false, null, "Usuário ou senha inválidos.");
            }

            // 2. Validações de Status da Conta (Idêntico ao VB.NET)
            if (usuario.Situacao == 2)
            {
                return (false, usuario, "Usuário Bloqueado. Contate o administrador.");
            }

            if (usuario.Situacao == 3)
            {
                return (false, usuario, "Usuário Desativado.");
            }

            if (usuario.Situacao == 4)
            {
                return (false, usuario, "Sua senha expirou. É necessário realizar a troca.");
            }

            // 3. Validação do Hash de Senha (SHA1 legado)
            string senhaCriptografada = PasswordHelper.GerarHashLegado(senhaPlana);

            if (usuario.SenhaHash != senhaCriptografada)
            {
                return (false, usuario, "Usuário ou senha inválidos.");
            }

            // Sucesso!
            return (true, usuario, "Login realizado com sucesso.");
        }
    }
}
