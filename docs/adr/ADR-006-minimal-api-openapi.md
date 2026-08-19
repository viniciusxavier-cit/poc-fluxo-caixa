# ADR-006 — Usar ASP.NET Core Minimal API com OpenAPI/Swagger

- **Status:** Aceito
- **Data:** 2025-06
- **Contexto relacionado:** ADR-001 (Clean Architecture), ADR-003 (CQRS/MediatR)

---

## Contexto

O sistema expõe dois serviços via HTTP REST. A camada de apresentação deve:

1. Ser fina — sem lógica de negócio
2. Ser documentada — o desafio pede documentação da API
3. Ser compatível com .NET 8 — tecnologia escolhida
4. Seguir o princípio de separação de preocupações — apenas mapear HTTP → MediatR

---

## Decisão

Usar **ASP.NET Core Minimal API** (introduzida no .NET 6, amadurecida no .NET 8) com **Swashbuckle** para geração de documentação OpenAPI/Swagger.

### Organização dos endpoints

Endpoints organizados em classes estáticas de extensão por bounded context:

```csharp
public static class LancamentosEndpoints
{
    public static void MapLancamentosEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/lancamentos").WithTags("Lançamentos");
        // ...
    }
}
```

Registrado no `Program.cs`:
```csharp
app.MapLancamentosEndpoints();
```

### Documentação OpenAPI por endpoint

Cada endpoint usa a cadeia de métodos do .NET 8:

```csharp
group.MapPost("/", handler)
    .WithName("CriarLancamento")
    .WithSummary("Registra um lançamento")
    .WithDescription("Cria um débito ou crédito. O consolidado é atualizado via Domain Event.")
    .Produces<LancamentoDto>(StatusCodes.Status201Created)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .WithOpenApi();
```

### Personalização via `.WithOpenApi(op => ...)`

Para customizações finas (descrição de parâmetros):

```csharp
.WithOpenApi(op =>
{
    op.Parameters[0].Description = "Data no formato yyyy-MM-dd (ex: 2025-06-01)";
    return op;
});
```

### Configuração do SwaggerGen

```csharp
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Fluxo de Caixa — Lançamentos API",
        Version = "v1",
        Description = "Serviço de controle de lançamentos (débitos e créditos)."
    });
});
```

### Endpoints expostos

**Lançamentos API (`http://localhost:5092`)**

| Método | Rota | Status de sucesso | Descrição |
|--------|------|-------------------|-----------|
| POST | `/lancamentos` | 201 Created | Registra lançamento |
| GET | `/lancamentos?data=yyyy-MM-dd` | 200 OK | Lista por data |
| GET | `/lancamentos/{id}` | 200 OK | Busca por ID |
| DELETE | `/lancamentos/{id}` | 204 No Content | Remove lançamento |

**Consolidado API (`http://localhost:5093`)**

| Método | Rota | Status de sucesso | Descrição |
|--------|------|-------------------|-----------|
| GET | `/consolidado/{data}` | 200 OK | Saldo consolidado da data |

---

## Alternativas Consideradas

### ASP.NET Core MVC com Controllers

Padrão tradicional com `[ApiController]`, `[HttpGet]`, `[Route]`.

Não adotado porque:
- Minimal API é a abordagem recomendada no .NET 8 para APIs novas
- Controllers adicionam scaffolding que não agrega neste domínio
- Minimal API + MediatR mantém os endpoints como thin wrappers naturalmente

### FastEndpoints

Framework de terceiros que impõe um padrão específico de endpoint.

Não adotado porque:
- Adiciona dependência externa desnecessária
- A Minimal API do .NET 8 já tem recursos suficientes
- FastEndpoints tem curva de aprendizado adicional

### gRPC

Protocolo binário de alta performance.

Não adotado porque:
- O desafio define uma API REST
- A documentação interativa (Swagger UI) é mais acessível para avaliação
- gRPC requer client específico

---

## Consequências

**Positivas:**
- Endpoints são thin wrappers (SRP): apenas mapeiam HTTP → MediatR → resultado HTTP
- A documentação Swagger UI em `/swagger` permite testar a API sem ferramentas externas
- `.WithOpenApi()` e `.Produces<T>()` geram spec OpenAPI preciso
- O `Program.cs` limpo é o Composition Root — toda a DI configurada em um lugar

**Negativas:**
- Sem validação automática de request via Data Annotations (em MVC seria automático)
- Para adicionar validação: integrar `FluentValidation` com `IPipelineBehavior<TRequest, TResponse>` do MediatR
- Minimal API tem menos scaffolding automático que MVC para cenários mais complexos

### Como adicionar validação (evolução futura)

```csharp
// ValidationBehavior.cs na camada Application
public class ValidationBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;
    // valida antes de chegar ao handler
}
```

---

## Referências

- [ASP.NET Core Minimal APIs (.NET 8)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis)
- [OpenAPI support in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/overview)
- [Swashbuckle.AspNetCore](https://github.com/domaindrivendev/Swashbuckle.AspNetCore)
