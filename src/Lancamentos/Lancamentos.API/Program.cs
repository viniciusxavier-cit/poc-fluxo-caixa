using System.Threading.RateLimiting;
using ConsolidadoDiario.Application.EventHandlers;
using ConsolidadoDiario.Domain.Repositories;
using ConsolidadoDiario.Infrastructure;
using ConsolidadoDiario.Infrastructure.Persistence;
using ConsolidadoDiario.Infrastructure.Repositories;
using FluentValidation;
using Lancamentos.API.Endpoints;
using Lancamentos.Application.Commands.CriarLancamento;
using Lancamentos.Domain.Repositories;
using Lancamentos.Infrastructure;
using Lancamentos.Infrastructure.Persistence;
using Lancamentos.Infrastructure.Repositories;
using MediatR;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SharedKernel;
using SharedKernel.Auth;
using SharedKernel.Behaviors;
using SharedKernel.Http;

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
        Title = "Fluxo de Caixa — Lançamentos API",
        Version = "v1",
        Description = "Serviço de controle de lançamentos (débitos e créditos). " +
                      "Permanece disponível mesmo que o consolidado diário esteja fora do ar.\n\n" +
                      "**Autenticação:** POST /auth/token → copie o token → clique em Authorize."
    });
    c.AddSwaggerJwtSupport();
});

// ── Banco de Dados ────────────────────────────────────────────────────────────
builder.Services.AddDbContext<LancamentosDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("LancamentosDb")
        ?? "Data Source=lancamentos.db"));

builder.Services.AddDbContext<ConsolidadoDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("ConsolidadoDb")
        ?? "Data Source=consolidado.db"));

// ── Repositórios e UoW ────────────────────────────────────────────────────────
builder.Services.AddScoped<ILancamentoRepository, LancamentoRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IConsolidadoDiarioRepository, ConsolidadoDiarioRepository>();
builder.Services.AddScoped<ConsolidadoDiario.Domain.Repositories.IConsolidadoUnitOfWork, ConsolidadoUnitOfWork>();

// ── Validação ─────────────────────────────────────────────────────────────────
builder.Services.AddValidatorsFromAssembly(typeof(CriarLancamentoCommandValidator).Assembly);
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<NotFoundExceptionHandler>();
builder.Services.AddProblemDetails();

// ── MediatR ───────────────────────────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(CriarLancamentoCommandHandler).Assembly);
    cfg.RegisterServicesFromAssembly(typeof(AtualizarConsolidadoEventHandler).Assembly);
    cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
});

// ── Rate Limiting (proteção dos endpoints) ────────────────────────────────────
builder.Services.AddRateLimiter(opt =>
{
    opt.AddFixedWindowLimiter("api", limiter =>
    {
        limiter.PermitLimit = 100;
        limiter.Window = TimeSpan.FromSeconds(1);
        limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiter.QueueLimit = 10;
    });
    opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ── Cache ─────────────────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();

// ── Health Checks ─────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<LancamentosDbContext>("lancamentos-db")
    .AddDbContextCheck<ConsolidadoDbContext>("consolidado-db");

var app = builder.Build();

// ── Migrate / seed ────────────────────────────────────────────────────────────
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<LancamentosDbContext>().Database.EnsureCreatedAsync();
    await scope.ServiceProvider.GetRequiredService<ConsolidadoDbContext>().Database.EnsureCreatedAsync();
}

// ── Middleware pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Lançamentos API v1"));
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ─────────────────────────────────────────────────────────────────
app.MapAuthEndpoints();
app.MapLancamentosEndpoints();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

public partial class Program { }
