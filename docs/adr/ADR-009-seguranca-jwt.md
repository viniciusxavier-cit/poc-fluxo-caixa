# ADR-009 — Autenticação com JWT Bearer

- **Status:** Aceito
- **Data:** 2025-06
- **Contexto relacionado:** ADR-006 (Minimal API)

---

## Contexto

O desafio pede que a arquitetura implemente mecanismos de segurança: autenticação, autorização e proteção contra ataques. Sem autenticação, qualquer cliente pode criar ou remover lançamentos financeiros, o que representa risco direto ao negócio.

---

## Decisão

Usar **JWT Bearer Authentication** com `Microsoft.AspNetCore.Authentication.JwtBearer` (nativo do .NET 8).

### Fluxo de autenticação

```
1. POST /auth/token { usuario, senha }
      └─► TokenService.GerarToken()
            └─► JWT assinado com HMAC-SHA256
                  └─► { token, expiraEm }

2. GET /lancamentos?data=... (Header: Authorization: Bearer <token>)
      └─► JwtBearerMiddleware valida: assinatura, issuer, audience, expiração
            └─► ClaimsPrincipal disponível no HttpContext
```

### Configuração centralizada no SharedKernel

```csharp
// JwtExtensions.cs (SharedKernel.Auth)
services.AddJwtAuthentication(configuration);  // usado nas duas APIs
```

A configuração está no `SharedKernel` para não duplicar entre `Lancamentos.API` e `ConsolidadoDiario.API`.

### Parâmetros de validação

| Parâmetro | Valor |
|---|---|
| Algoritmo | HMAC-SHA256 |
| Issuer | `fluxo-caixa` |
| Audience | `fluxo-caixa-clients` |
| Expiração | 60 minutos |
| ClockSkew | 0 (sem tolerância) |

### Proteção dos endpoints

```csharp
// Todos os endpoints de negócio requerem autenticação
var group = app.MapGroup("/lancamentos")
    .RequireAuthorization();  // retorna 401 se não autenticado

// Endpoint de token é público
app.MapPost("/auth/token", ...).AllowAnonymous();

// Health check é público (monitoramento)
app.MapHealthChecks("/health").AllowAnonymous();
```

### Swagger UI com suporte a Bearer

O botão **Authorize** aparece no Swagger UI para informar o token diretamente na interface.

### Secret no appsettings (dev) — configuração para produção

```json
"Jwt": {
  "Secret": "TROQUE-ESTA-CHAVE-EM-PRODUCAO-MIN-32-CHARS!"
}
```

**Em produção:** injetar via variável de ambiente ou secrets manager:
```bash
export Jwt__Secret="chave-segura-gerada-aleatoriamente-256-bits"
```

---

## Alternativas Consideradas

### API Key simples (header `X-Api-Key`)

Mais simples de implementar. Rejeitada porque:
- Sem expiração nativa
- Sem claims (não permite autorização por papel)
- Não é o padrão de mercado para APIs REST modernas

### OAuth 2.0 / OpenID Connect (Keycloak, Azure AD)

Mais robusto para produção com múltiplos clientes e SSO. Não adotado porque:
- Requer infraestrutura externa (servidor de identidade)
- Adiciona complexidade desnecessária para o desafio
- JWT Bearer é o bloco de construção que um sistema OAuth usa internamente

### Sem autenticação com documentação

Documentar conscientemente a ausência. Rejeitado porque o desafio lista segurança como critério e a ausência total não demonstra conhecimento da área.

---

## Consequências

**Positivas:**
- Endpoints protegidos contra acesso não autorizado
- Claims extensíveis para autorização por papel (`comerciante`, `admin`)
- `TokenService` testável isoladamente
- Swagger UI com suporte nativo ao fluxo de autenticação

**Negativas:**
- Secret em `appsettings.json` no repositório é um risco se não tratado — mitigado pelo alerta explícito no arquivo e instrução de uso de variável de ambiente
- JWT não tem revogação nativa — token roubado é válido até expirar (mitigado pelo TTL curto de 60 min)
- Endpoint `/auth/token` com usuários hard-coded é apenas para demo — em produção: banco + bcrypt

---

## Referências

- [JWT Bearer Authentication in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/jwt-authn)
- [RFC 7519 — JSON Web Token](https://datatracker.ietf.org/doc/html/rfc7519)
- [OWASP — Authentication Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)
