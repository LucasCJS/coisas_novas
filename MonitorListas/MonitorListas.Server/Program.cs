using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MonitorListas.Server.Data;
using MonitorListas.Server.Hubs;
using MonitorListas.Server.Models;
using MonitorListas.Server.Repositories;
using MonitorListas.Server.Security;
using MonitorListas.Server.Services;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<MonitorDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// 1. Configura as Settings
builder.Services.Configure<MonitorSettings>(
    builder.Configuration.GetSection("MonitorSettings")
);

try
{
    // 1. BANCO DE DADOS DA APLICAÇÃO (Monitoramento)
    string connectionStringAdv = ConnectionStringBuilder.Build("INTEGRACAOADVICE", "INTEGRACAOADV");
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseSqlServer(connectionStringAdv));

    // 2. BANCO DE DADOS CORPORATIVO (Apenas para Login/Usuários)
    string connectionStringCorp = ConnectionStringBuilder.Build("CORP", "CORP");
    builder.Services.AddDbContext<CorpDbContext>(options =>
        options.UseSqlServer(connectionStringCorp));

    Console.WriteLine("Bancos ADV e CORP configurados com sucesso!");
}
catch (Exception ex)
{
    Console.WriteLine($"ERRO CRÍTICO AO LER CONFIGURAÇÕES DO BANCO: {ex.Message}");
}

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 2. O Rádio
builder.Services.AddSignalR();

// 3. O Radar (Chamado UMA vez só!)
builder.Services.AddScoped<RequisicaoRepository>();
builder.Services.AddScoped<TbListaRepository>();

builder.Services.AddScoped<AuthService>();
builder.Services.AddHostedService<MonitoradorPastaService>();
builder.Services.AddHostedService<MonitorExecutaveisListaService>();

// Configuração do JWT
var jwtKey = builder.Configuration["Jwt:Key"];
var keyBytes = Encoding.ASCII.GetBytes(jwtKey);

builder.Services.AddAuthentication(x =>
{
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(x =>
{
    x.RequireHttpsMetadata = false; // Em prod, mude para true
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
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(); // É aqui que a página /swagger/index.html é gerada
}

app.UseHttpsRedirection();

// A ORDEM É CRÍTICA: Primeiro dizemos QUEM é o usuário, depois O QUE ele pode fazer.
app.UseAuthentication(); // <--- ADICIONE ESTA LINHA
app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapHub<MonitorHub>("/monitorHub");
app.MapFallbackToFile("/index.html");

app.Run();