# ADR-011 — Limitações do SharedKernel e acoplamento entre bounded contexts (POC)

- **Status:** Aceito (com ressalvas)
- **Data:** 2026-08
- **Contexto relacionado:** ADR-001 (Clean Architecture), ADR-002 (DDD Bounded Contexts), ADR-004 (Domain Events)

---

## Contexto

Durante o desenvolvimento deste projeto, foram identificadas duas inconsistências arquiteturais que, em um sistema de produção, deveriam ser corrigidas. Por se tratar de uma **prova de conceito (POC)**, a decisão foi mantê-las como estão, documentando o problema e a solução ideal para referência futura.

---

## Problema 1 — Acoplamento direto entre bounded contexts via ProjectReference

`ConsolidadoDiario.Application` referencia diretamente `Lancamentos.Domain` para consumir os eventos `LancamentoCriadoEvent` e `LancamentoRemovidoEvent`:

```xml
<!-- ConsolidadoDiario.Application.csproj -->
<ProjectReference Include="..\..\Lancamentos\Lancamentos.Domain\Lancamentos.Domain.csproj" />
```

Isso cria um acoplamento em tempo de compilação entre dois bounded contexts que deveriam ser independentes. Uma mudança interna no domínio de `Lancamentos` pode quebrar o `ConsolidadoDiario` sem que haja intenção de fazê-lo.

### Solução ideal

Criar um projeto `Contracts` (ou `IntegrationEvents`) neutro, que nenhum dos dois bounded contexts "possui":

```
src/
  Contracts/
    LancamentoCriadoIntegrationEvent.cs
    LancamentoRemovidoIntegrationEvent.cs
    TipoMovimento.cs
```

- `Lancamentos.Domain` publica para `Contracts` após persistir um lançamento
- `ConsolidadoDiario.Application` assina eventos de `Contracts`
- Nenhum dos dois conhece os internos do outro

```
Antes:  ConsolidadoDiario.Application ──→ Lancamentos.Domain
Depois: Lancamentos.Domain ──→ Contracts ←── ConsolidadoDiario.Application
```

---

## Problema 2 — SharedKernel com responsabilidades além de abstrações de domínio

O `SharedKernel` contém, além dos blocos de construção DDD esperados (`Entity`, `AggregateRoot`, `ValueObject`, `IDomainEvent`, `IUnitOfWork`), dois elementos que fogem do seu papel:

| Item | Problema |
|---|---|
| `Auth/` (`JwtExtensions`, `TokenService`, `JwtSettings`) | Infraestrutura de autenticação misturada com abstrações de DDD no mesmo projeto. |
| `TipoLancamento` (enum) | Conceito de negócio do domínio de `Lancamentos` exposto como contrato compartilhado, criando acoplamento semântico. |

### Solução ideal

**Auth** — extrair para um projeto dedicado `SharedKernel.Auth`, separado do `SharedKernel`:

```
src/
  SharedKernel/       ← apenas blocos DDD (Entity, AggregateRoot, ValueObject, IDomainEvent, IUnitOfWork)
  SharedKernel.Auth/  ← apenas infraestrutura de autenticação
    JwtExtensions.cs
    JwtSettings.cs
    TokenService.cs
```

Essa separação é a correta porque autenticação é uma preocupação transversal legítima — qualquer novo serviço que surgir precisará do mesmo mecanismo de auth ou, no mínimo, de uma base comum. Mover para dentro de `Lancamentos.API` resolveria o problema hoje, mas fecharia a porta para o reúso amanhã. Um projeto `SharedKernel.Auth` independente mantém a auth compartilhável sem contaminar as abstrações de domínio.

**TipoLancamento** — mover para `Lancamentos.Domain`. No projeto `Contracts`, um enum `TipoMovimento` independente seria usado para comunicação entre contextos.

O `SharedKernel` ficaria restrito a abstrações agnósticas de negócio e infraestrutura.

---

## Decisão para esta POC

Ambos os problemas foram identificados e são conhecidos pela equipe. A correção **não foi implementada** por:

1. Tratar-se de uma POC com escopo e prazo delimitados
2. Os dois serviços são co-deployados e co-evoluídos no mesmo repositório, o que reduz o risco prático do acoplamento em compilação
3. O esforço de criar o projeto `Contracts` e refatorar os eventos de integração excederia o benefício demonstrável no contexto de uma POC

Em um projeto com intenção de ir para produção ou de separar os serviços em repositórios distintos, **ambas as correções seriam obrigatórias** antes do primeiro deploy.

---

## Consequências

- O código atual funciona corretamente dentro do escopo da POC
- Qualquer evolução para microserviços reais exigirá o projeto `Contracts` e a remoção do `ProjectReference` cruzado
- A infra de `Auth` precisará ser movida caso `ConsolidadoDiario` ganhe seu próprio mecanismo de autenticação
