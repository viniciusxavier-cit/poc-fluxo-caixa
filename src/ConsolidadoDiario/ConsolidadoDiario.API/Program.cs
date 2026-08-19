using System.Threading.RateLimiting;
using ConsolidadoDiario.API.Endpoints;
using ConsolidadoDiario.Application.Queries.GetConsolidadoPorData;
using ConsolidadoDiario.Domain.Repositories;
using ConsolidadoDiario.Infrastructure;
using ConsolidadoDiario.Infrastructure.Persistence;
using ConsolidadoDiario.Infrastructure.Repositories;
using MediatR;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Auth;

var builder = WebApplication.CreateBuilder(args);

// ── Autenticação JWT ──────────────────────────────────────────────────────────
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddScoped<TokenService>();

// ── Swagger com suporte a Bearer ──────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Fluxo de Caixa — Consolidado Diário API",
        Version = "v1",
        Description = "Serviço de consolidado diário. " +
                      "Cache de 60s + rate limiting para suportar 50 req/s com ≤5% de perda.\n\n" +
                      "**Autenticação:** POST /auth/token na Lançamentos API → copie o token → Authorize."
    });
    c.AddSwaggerJwtSupport();
});

// ── Banco de Dados ────────────────────────────────────────────────────────────
builder.Services.AddDbContext<ConsolidadoDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("ConsolidadoDb")
        ?? "Data Source=consolidado.db"));

// ── Repositórios e UoW ────────────────────────────────────────────────────────
builder.Services.AddScoped<IConsolidadoDiarioRepository, ConsolidadoDiarioRepository>();
builder.Services.AddScoped<ConsolidadoDiario.Domain.Repositories.IConsolidadoUnitOfWork, ConsolidadoUnitOfWork>();
builder.Services.AddScoped<ConsolidadoUnitOfWork>();

// ── MediatR ───────────────────────────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(GetConsolidadoPorDataQueryHandler).Assembly));

// ── Rate Limiting — 50 req/s com tolerância de fila ──────────────────────────
// Configurado para permitir 55 req/s (margem de 10%) + fila de 10 → ~1,8% de perda estimada
builder.Services.AddRateLimiter(opt =>
{
    opt.AddSlidingWindowLimiter("consolidado", limiter =>
    {
        limiter.PermitLimit = 55;
        limiter.Window = TimeSpan.FromSeconds(1);
        limiter.SegmentsPerWindow = 4;
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit = 10;
    });
    opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ── Cache em memória ──────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();

// ── Health Checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ConsolidadoDbContext>("consolidado-db");

var app = builder.Build();

// ── Migrate / seed ────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<ConsolidadoDbContext>().Database.EnsureCreated();

// ── Middleware pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Consolidado API v1"));
}

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ─────────────────────────────────────────────────────────────────
app.MapConsolidadoEndpoints();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

public partial class Program { }
