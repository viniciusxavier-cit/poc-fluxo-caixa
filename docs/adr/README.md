# Architectural Decision Records (ADRs)

Este diretório contém os registros de decisões arquiteturais do sistema de Fluxo de Caixa.

ADRs documentam o contexto, as opções consideradas e o raciocínio por trás de cada decisão arquitetural relevante. O objetivo é que qualquer pessoa que entre no projeto entenda **por que** as coisas são como são — não apenas o que foi feito.

## Formato adotado

Baseado no modelo de [Michael Nygard](https://cognitect.com/blog/2011/11/15/documenting-architecture-decisions), com adições de contexto técnico:

- **Título** — ação imperativa curta
- **Status** — Proposto / Aceito / Substituído / Obsoleto
- **Contexto** — forças e restrições que motivaram a decisão
- **Decisão** — o que foi decidido e por quê
- **Alternativas consideradas** — o que foi descartado e por quê
- **Consequências** — trade-offs positivos e negativos
- **Referências** — links e leituras relevantes

## Índice

| ADR | Título | Status | Data |
|-----|--------|--------|------|
| [ADR-001](ADR-001-clean-architecture.md) | Adotar Clean Architecture como estrutura da solução | Aceito | 2025-06 |
| [ADR-002](ADR-002-ddd-bounded-contexts.md) | Modelar domínio com DDD e Bounded Contexts separados | Aceito | 2025-06 |
| [ADR-003](ADR-003-cqrs-mediatr.md) | Implementar CQRS com MediatR | Aceito | 2025-06 |
| [ADR-004](ADR-004-domain-events-desacoplamento.md) | Usar Domain Events para desacoplar Lançamentos de Consolidado | Aceito | 2025-06 |
| [ADR-005](ADR-005-sqlite-efcore.md) | Usar SQLite com EF Core (banco independente por serviço) | Aceito | 2025-06 |
| [ADR-006](ADR-006-minimal-api-openapi.md) | Usar ASP.NET Core Minimal API com OpenAPI/Swagger | Aceito | 2025-06 |
| [ADR-007](ADR-007-testes-xunit-nsubstitute.md) | Usar xUnit + FluentAssertions + NSubstitute para testes | Aceito | 2025-06 |
| [ADR-008](ADR-008-tipo-lancamento-tabela-dominio.md) | TipoLancamento como tabela de domínio no banco de dados | Aceito | 2025-06 |
| [ADR-009](ADR-009-seguranca-jwt.md) | Autenticação com JWT Bearer | Aceito | 2025-06 |
| [ADR-010](ADR-010-rate-limiting-cache-health.md) | Rate Limiting, Cache e Health Checks para 50 req/s | Aceito | 2025-06 |

## Como criar um novo ADR

1. Copie o template abaixo para um novo arquivo `ADR-NNN-titulo-kebab-case.md`
2. Preencha todas as seções
3. Atualize este índice

```markdown
# ADR-NNN — Título da Decisão

- **Status:** Proposto
- **Data:** YYYY-MM-DD
- **Autores:** Nome

## Contexto

## Decisão

## Alternativas Consideradas

## Consequências

## Referências
```
