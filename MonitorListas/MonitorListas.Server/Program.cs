using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MonitorListas.Server.Data;
using MonitorListas.Server.Models;
using MonitorListas.Server.Repositories;
using MonitorListas.Server.Security;
using MonitorListas.Server.Services;
using System.Net;
using System.Text;

ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13;


var builder = WebApplication.CreateBuilder(args);

// 1. REGISTRO DO SQLITE (Banco local do painel)
//builder.Services.AddDbContext<MonitorDbContext>(options =>
//    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// --- 1. REGISTRO DO SQLITE ---
builder.Services.AddDbContext<MonitorDbContext>(options =>
{
    var connString = builder.Configuration.GetConnectionString("DefaultConnection");

    if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
    {
        // No Docker, forçamos para a pasta que tem permissão de escrita
        options.UseSqlite("Data Source=/app/dados/auditoria_xml.db");
    }
    else
    {
        // No seu Windows (Debug), ele usa o "auditoria_xml.db" que está no seu JSON
        options.UseSqlite(connString);
    }
});

// 2. CONFIGURAÇÃO DAS STRINGS DE CONEXÃO (SQL SERVER CORPORATIVO)
string connectionStringAdv = "";
string connectionStringCorp = "";

try
{
    // O ConnectionStringBuilder agora deve retornar a string com Encrypt=False e TrustServerCertificate=True
    connectionStringAdv = ConnectionStringBuilder.Build("INTEGRACAOADVICE", "INTEGRACAOADV");
    connectionStringCorp = ConnectionStringBuilder.Build("CORP", "CORP");

    Console.WriteLine("Strings de conexão montadas com sucesso.");
}
catch (Exception ex)
{
    Console.WriteLine($"AVISO: Erro ao montar strings de conexão: {ex.Message}");
}

// 3. REGISTRO DOS CONTEXTOS SQL SERVER 
// Se por acaso a string vier vazia, usamos um fallback para não quebrar a Injeção de Dependência
if (string.IsNullOrEmpty(connectionStringAdv))
    throw new Exception("ConnectionString ADV não foi montada");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionStringAdv));

//builder.Services.AddDbContext<CorpDbContext>(options =>
//    options.UseSqlServer(!string.IsNullOrEmpty(connectionStringCorp) ? connectionStringCorp : "Server=invalid_config;"));

if (string.IsNullOrEmpty(connectionStringCorp))
    throw new Exception("connectionStringCorp ADV não foi montada");

builder.Services.AddDbContext<CorpDbContext>(options =>
    options.UseSqlServer(connectionStringCorp));

// 4. CONFIGURAÇÕES GERAIS E SERVIÇOS
builder.Services.Configure<MonitorSettings>(builder.Configuration.GetSection("MonitorSettings"));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

// Repositórios e Serviços
builder.Services.AddScoped<RequisicaoRepository>();
builder.Services.AddScoped<TbListaRepository>();
builder.Services.AddScoped<AuthService>();

//builder.Services.AddHostedService<MonitoradorPastaService>();
builder.Services.AddSingleton<MonitoradorPastaService>();

builder.Services.AddHostedService(provider =>
    provider.GetRequiredService<MonitoradorPastaService>());
builder.Services.AddHostedService<MonitorExecutaveisListaService>();

// 5. CONFIGURAÇÃO JWT
var jwtKey = builder.Configuration["Jwt:Key"] ?? "chave_mestra_de_emergencia_32_caracteres";
var keyBytes = Encoding.ASCII.GetBytes(jwtKey);

builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = false;
    x.SaveToken = true;

    x.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateLifetime = true
    };

    x.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];

            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) &&
                path.StartsWithSegments("/monitorHub"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
});

var app = builder.Build();

// 6. AUTO-MIGRAÇÃO SQLITE (Cria as tabelas se não existirem)
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<MonitorDbContext>();
        db.Database.EnsureCreated();
        Console.WriteLine("MonitorDbContext (SQLite) inicializado com sucesso.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erro crítico ao inicializar SQLite: {ex.Message}");
    }
}

// 7. PIPELINE DE REQUISIÇÕES
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Em containers Docker, evite o redirecionamento automático para HTTPS 
// a menos que você tenha configurado certificados SSL no Kestrel.
// app.UseHttpsRedirection(); 

app.UseAuthentication();
app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapHub<MonitorHub>("/monitorHub");
app.MapFallbackToFile("/index.html");

app.Run();