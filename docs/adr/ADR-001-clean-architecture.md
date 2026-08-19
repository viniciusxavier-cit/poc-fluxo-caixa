# ADR-001 — Adotar Clean Architecture como estrutura da solução

- **Status:** Aceito
- **Data:** 2025-06
- **Contexto relacionado:** ADR-002 (DDD), ADR-003 (CQRS)

---

## Contexto

O sistema precisa ser mantido e evoluído ao longo do tempo. O desafio pede boas práticas, padrões de arquitetura e SOLID. Precisamos de uma estrutura que:

- Separe claramente as responsabilidades de cada camada
- Permita testar a lógica de negócio sem depender de banco de dados ou HTTP
- Facilite a troca de tecnologias de infraestrutura (ex: SQLite → PostgreSQL) sem impacto no domínio
- Possibilite que novos desenvolvedores entendam onde cada tipo de código deve viver

O projeto usa dois bounded contexts com regras de negócio distintas (Lançamentos e Consolidado Diário), o que reforça a necessidade de fronteiras claras.

---

## Decisão

Adotar **Clean Architecture** (Robert C. Martin) como estrutura de organização dos projetos, com as seguintes camadas por bounded context:

```
Domain → Application → Infrastructure
                   ↑
                  API
```

### Regras de dependência

| Camada | Pode depender de | Não pode depender de |
|--------|-----------------|----------------------|
| Domain | Nada externo (apenas SharedKernel) | Application, Infrastructure, API |
| Application | Domain | Infrastructure, API, EF Core, HTTP |
| Infrastructure | Application, Domain | API |
| API | Application, Infrastructure | — |

### Camadas e responsabilidades

**SharedKernel** — contratos e bases compartilhadas entre bounded contexts:
- `Entity<TId>`, `ValueObject`, `AggregateRoot<TId>`
- `IDomainEvent`, `IUnitOfWork`

**Domain** — o coração do sistema. Nenhuma dependência externa:
- Entidades e Aggregate Roots com invariantes
- Value Objects
- Interfaces de repositório (`ILancamentoRepository`)
- Domain Events

**Application** — casos de uso orquestrados:
- Commands e Queries (CQRS)
- Handlers (MediatR)
- DTOs de saída
- Sem lógica de domínio (delega ao Aggregate)

**Infrastructure** — detalhes técnicos:
- `DbContext` e configurações EF Core
- Implementações de repositório
- `UnitOfWork`

**API** — ponto de entrada HTTP:
- Mapeamento de rotas (Minimal API)
- Composition Root (registro de DI)
- Documentação OpenAPI

---

## Alternativas Consideradas

### Arquitetura em camadas tradicional (N-Layer)

Separação em `Data` → `Business` → `Presentation`. Rejeitada porque:
- A camada `Business` frequentemente depende diretamente do ORM
- Testes unitários exigem banco de dados ou mocks complexos
- Mudança de banco de dados afeta várias camadas

### Arquitetura Hexagonal (Ports & Adapters)

Semanticamente equivalente à Clean Architecture. Não adotada como nomenclatura para evitar terminologia que pode ser menos familiar na equipe, mas os princípios são idênticos (DIP, isolamento do domínio).

### Monólito sem separação de camadas

Mais rápido para protótipos. Rejeitado porque o desafio pede explicitamente padrões de arquitetura e SOLID, e a falta de separação tornaria os testes unitários impossíveis sem banco.

---

## Consequências

**Positivas:**
- Lógica de domínio 100% testável sem banco de dados ou HTTP
- Troca de SQLite por PostgreSQL afeta apenas `Infrastructure`
- Troca de MediatR por outro dispatcher afeta apenas `Application`
- Cada camada tem responsabilidade clara — onboarding mais rápido

**Negativas:**
- Mais projetos e arquivos para uma solução pequena
- Indireção adicional (interface → implementação) pode parecer excessiva para CRUD simples
- Desenvolvedores sem experiência em Clean Architecture têm curva de aprendizado

**Mitigação:** para sistemas maiores, o custo inicial é amplamente compensado pela manutenibilidade. Para este desafio, demonstra maturidade arquitetural.

---

## Referências

- Robert C. Martin — *Clean Architecture: A Craftsman's Guide to Software Structure and Design* (2017)
- [The Clean Architecture (blog post)](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)
- [Dependency Rule](https://www.informit.com/articles/article.aspx?p=2832399)
