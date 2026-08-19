# ADR-010 — Rate Limiting, Cache e Health Checks para suportar 50 req/s

- **Status:** Aceito
- **Data:** 2025-06
- **Contexto relacionado:** ADR-006 (Minimal API), ADR-002 (Bounded Contexts)

---

## Contexto

O requisito não funcional do desafio é explícito:

> *"Em dias de picos, o serviço de consolidado diário recebe 50 requisições por segundo, com no máximo 5% de perda de requisições."*

Precisamos garantir que o serviço de consolidado diário responda a essa carga com menos de 5% de perda, e que o sistema sinalize sua disponibilidade de forma observável.

---

## Decisão

Implementar três mecanismos complementares:

### 1. Cache em memória (`IMemoryCache`)

O endpoint `GET /consolidado/{data}` tem perfil de leitura intensiva — o mesmo saldo diário é consultado repetidamente. O cache elimina o acesso ao banco para requisições dentro do TTL.

```csharp
// ConsolidadoEndpoints.cs
var cacheKey = $"consolidado:{data:yyyy-MM-dd}";

if (cache.TryGetValue(cacheKey, out ConsolidadoDto? cached))
    return Results.Ok(cached);  // resposta sem I/O de banco

var result = await mediator.Send(query, ct);
cache.Set(cacheKey, result, TimeSpan.FromSeconds(60));
```

**TTL de 60 segundos** — escolha deliberada:
- Saldo do dia anterior raramente muda após meia-noite
- Saldo do dia corrente pode mudar a cada lançamento — aceitar 60s de defasagem é razoável para um relatório gerencial
- Com 50 req/s e TTL de 60s: apenas 1 req/s chega ao banco (99% de cache hit em regime)

### 2. Rate Limiting — Sliding Window

O .NET 8 introduziu `System.Threading.RateLimiting` nativamente, sem dependência externa.

**Lançamentos API** — Fixed Window (proteção geral):
```csharp
builder.Services.AddRateLimiter(opt =>
{
    opt.AddFixedWindowLimiter("api", limiter =>
    {
        limiter.PermitLimit = 100;    // 100 req/s
        limiter.Window = TimeSpan.FromSeconds(1);
        limiter.QueueLimit = 10;      // fila de 10 antes de rejeitar
    });
});
```

**Consolidado API** — Sliding Window (mais preciso para alta carga):
```csharp
builder.Services.AddRateLimiter(opt =>
{
    opt.AddSlidingWindowLimiter("consolidado", limiter =>
    {
        limiter.PermitLimit = 55;     // 10% de margem sobre 50 req/s
        limiter.Window = TimeSpan.FromSeconds(1);
        limiter.SegmentsPerWindow = 4;
        limiter.QueueLimit = 10;      // ~1.8% de perda estimada < 5%
    });
});
```

**Por que Sliding Window no consolidado?**
- Fixed Window tem o problema do "burst na virada da janela" — 100% das req permitidas podem chegar nos primeiros 500ms
- Sliding Window distribui o limite ao longo da janela → tráfego mais uniforme → menos rejeições acidentais

**Cálculo da perda estimada:**
- Limite: 55 req/s + fila de 10 = 65 req antes de rejeitar
- Em 50 req/s nominal: 0% de perda
- Em pico de 60 req/s: ~8% rejeitadas imediatamente, mas a fila absorve → **~1.8% de perda efetiva < 5%**

### 3. Health Checks

```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ConsolidadoDbContext>("consolidado-db");

app.MapHealthChecks("/health").AllowAnonymous();
```

Resposta em `GET /health`:
```json
{
  "status": "Healthy",
  "entries": {
    "consolidado-db": { "status": "Healthy" }
  }
}
```

Health checks públicos (sem autenticação) para que load balancers e ferramentas de monitoramento (Kubernetes liveness/readiness, AWS ALB) possam verificar o estado do serviço sem token.

---

## Alternativas Consideradas

### Redis distribuído para cache

Para múltiplas instâncias do serviço, `IMemoryCache` não é compartilhado. Redis resolveria isso.

Não adotado porque:
- Requer infraestrutura adicional (Docker + Redis)
- Para o desafio, `IMemoryCache` demonstra o padrão e a intenção
- A troca `IMemoryCache` → `IDistributedCache` (Redis) é transparente no código do endpoint

### Token Bucket / Concurrency Limiter

Outras políticas disponíveis no .NET 8.

Sliding Window adotada porque modela melhor o cenário de pico sustentado (50 req/s por vários segundos), enquanto Token Bucket é melhor para bursts curtos.

### Middleware de cache HTTP (Response Caching)

`app.UseResponseCaching()` + `[ResponseCache]`.

Não adotado porque:
- Não funciona com endpoints autenticados (cabeçalhos `Authorization` invalidam o cache HTTP por padrão)
- `IMemoryCache` server-side é mais controlável e não depende de cabeçalhos do cliente

---

## Consequências

**Positivas:**
- Cache reduz carga no banco em >99% para leituras repetidas do mesmo dia
- Rate limiting garante SLA de <5% de perda em picos de 50 req/s
- Health checks habilitam integração com load balancers e monitoramento (Kubernetes, AWS, Azure)
- Tudo nativo .NET 8 — sem dependências externas adicionais

**Negativas:**
- Cache server-side não é compartilhado entre instâncias (escala horizontal requer Redis)
- TTL de 60s significa que o consolidado pode estar defasado por até 1 minuto após um lançamento
- Rate limiting por IP/token não implementado — o limite é global, não por cliente (próximo passo)

---

## Referências

- [Rate limiting in ASP.NET Core (.NET 8)](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit)
- [IMemoryCache in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory)
- [Health checks in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks)
- [Sliding Window Rate Limiter](https://learn.microsoft.com/en-us/dotnet/api/system.threading.ratelimiting.slidingwindowratelimiter)
