using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using MonitorListas.Server.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MonitorListas.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly IConfiguration _config;

        public AuthController(AuthService authService, IConfiguration config)
        {
            _authService = authService;
            _config = config;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { mensagem = "Usuário e senha são obrigatórios." });

            // Chama nosso serviço que bate na base CORP
            var resultado = await _authService.ValidarUsuarioAsync(request.Username, request.Password);

            if (!resultado.Sucesso)
            {
                // Retorna 401 Unauthorized com o motivo (Bloqueado, Incorreto, Expirado, etc)
                return Unauthorized(new { mensagem = resultado.Mensagem });
            }

            // Se chegou aqui, logou! Vamos gerar o Token JWT
            var token = GerarTokenJwt(resultado.Usuario.Login, resultado.Usuario.Nome);

            return Ok(new
            {
                mensagem = resultado.Mensagem,
                token = token,
                nome = resultado.Usuario.Nome
            });
        }

        private string GerarTokenJwt(string login, string nome)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_config["Jwt:Key"]);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                // Embutimos o login e o nome dentro do Token
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, login),
                    new Claim(ClaimTypes.Name, nome)
                }),
                Expires = DateTime.UtcNow.AddHours(8), // Token vale por 8 horas
                Issuer = _config["Jwt:Issuer"],
                Audience = _config["Jwt:Audience"],
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }

    // DTO simples para receber o JSON do React
    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }
}
