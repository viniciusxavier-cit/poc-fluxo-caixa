# Business Requirements — AG-2

**Título:** Adicionar suporte a intervalo de datas no endpoint de consulta de lançamentos  
**Data:** 2026-10-23  
**Status:** Em revisão  
**Complexidade estimada:** S (Small)

---

## 1. Executive Summary

O endpoint de consulta de lançamentos atualmente aceita apenas uma data exata por vez, obrigando o comerciante a realizar múltiplas chamadas para analisar um período (ex: fechamento semanal ou mensal). Esta especificação define os requisitos para que o mesmo endpoint também aceite um intervalo de datas — `dataInicio` e `dataFim` — mantendo plena retrocompatibilidade com o uso atual via parâmetro `data`.

---

## 2. Problem Statement

### Estado atual

O endpoint de consulta de lançamentos aceita um único parâmetro de data (`data=yyyy-MM-dd`), retornando apenas os lançamentos registrados naquele dia específico. Para visualizar um período maior — por exemplo, todos os lançamentos de uma semana ou de um mês —, o comerciante precisa realizar uma chamada separada para cada data de interesse.

### Estado desejado

O endpoint de consulta de lançamentos deve aceitar, alternativamente, um par de parâmetros `dataInicio` e `dataFim`, retornando todos os lançamentos do intervalo definido (extremos inclusivos). O comportamento existente com `data` deve permanecer intacto.

### Impacto no negócio

- **Quem se beneficia:** comerciantes que realizam fechamentos periódicos (semanal, quinzenal, mensal) e sistemas externos (ERP/PDV) que integram automaticamente.
- **Como:** redução de chamadas desnecessárias à API, possibilidade de análise de período sem processamento adicional no lado do cliente.
- **Urgência:** bloqueador para análises de fluxo de caixa por período sem customizações workaround.

---

## 3. Personas & User Stories

### Persona 1 — Comerciante

O comerciante utiliza o sistema para registrar e consultar seus débitos e créditos diários, realizando fechamentos periódicos.

**User Stories:**

> Como comerciante, quero consultar todos os lançamentos de um período (ex: semana ou mês), para que eu possa realizar o fechamento periódico sem precisar fazer uma chamada por dia.

> Como comerciante, quero que a forma atual de consulta por data única continue funcionando, para que eu não precise alterar integrações existentes.

### Persona 2 — Sistema integrado (ERP/PDV)

Sistema externo que chama a API automaticamente para conciliar lançamentos.

**User Stories:**

> Como sistema integrado, quero passar um intervalo de datas na consulta de lançamentos, para que eu possa sincronizar um período completo em uma única chamada.

---

## 4. Business Rules

**BR-001:** O parâmetro `data` existente deve continuar funcionando exatamente como antes, sem qualquer alteração de comportamento ou formato de resposta.

**BR-002:** Os parâmetros `dataInicio` e `dataFim` são opcionais e, quando presentes, devem obedecer ao formato `yyyy-MM-dd`.

**BR-003:** Quando `dataInicio` e `dataFim` forem informados, o sistema deve retornar todos os lançamentos cuja data de registro esteja dentro do intervalo, incluindo os dias de início e fim (extremos inclusivos).

**BR-004:** `dataFim` não pode ser uma data anterior a `dataInicio`. Quando isso ocorrer, o sistema deve rejeitar a requisição com uma mensagem de erro clara e compreensível para o usuário.

**BR-005:** Os parâmetros `dataInicio`/`dataFim` e o parâmetro `data` são mutuamente exclusivos. Fornecer simultaneamente `data` e qualquer um dos novos parâmetros deve resultar em erro de validação com mensagem explicativa.

**BR-006:** Quando nenhum parâmetro de data for informado (nem `data`, nem `dataInicio`, nem `dataFim`), o sistema deve retornar uma lista vazia — não um erro — preservando o comportamento atual para chamadas sem filtro.

**BR-007:** A documentação de API interativa (Swagger) deve ser atualizada para refletir os novos parâmetros, incluindo descrições, formato esperado e exemplos de uso.

**BR-008:** Todos os lançamentos retornados devem manter o mesmo formato de resposta já utilizado na consulta por data única.

---

## 5. Acceptance Criteria

```gherkin
Feature: Consulta de lançamentos por intervalo de datas

  Background:
    Given que o comerciante possui um token JWT válido
    And existem lançamentos registrados nos dias 2025-06-01, 2025-06-02 e 2025-06-03

  # --- Caminho atual preservado ---

  Scenario: Consulta por data única (comportamento existente mantido)
    Given que o comerciante informa apenas o parâmetro "data=2025-06-01"
    When o comerciante realiza a consulta de lançamentos
    Then o sistema retorna apenas os lançamentos do dia 2025-06-01
    And o formato da resposta é idêntico ao comportamento atual

  # --- Novos cenários: intervalo de datas ---

  Scenario: Consulta por intervalo de datas válido
    Given que o comerciante informa "dataInicio=2025-06-01" e "dataFim=2025-06-03"
    When o comerciante realiza a consulta de lançamentos
    Then o sistema retorna todos os lançamentos dos dias 2025-06-01, 2025-06-02 e 2025-06-03
    And os extremos do intervalo estão incluídos nos resultados

  Scenario: Consulta por intervalo de um único dia
    Given que o comerciante informa "dataInicio=2025-06-02" e "dataFim=2025-06-02"
    When o comerciante realiza a consulta de lançamentos
    Then o sistema retorna apenas os lançamentos do dia 2025-06-02

  Scenario: Consulta por intervalo sem lançamentos no período
    Given que o comerciante informa "dataInicio=2025-07-01" e "dataFim=2025-07-31"
    And não existem lançamentos nesse período
    When o comerciante realiza a consulta de lançamentos
    Then o sistema retorna uma lista vazia
    And não retorna erro

  # --- Validações ---

  Scenario: dataFim anterior a dataInicio
    Given que o comerciante informa "dataInicio=2025-06-10" e "dataFim=2025-06-01"
    When o comerciante realiza a consulta de lançamentos
    Then o sistema rejeita a requisição com erro de validação
    And a mensagem de erro indica claramente que dataFim não pode ser anterior a dataInicio

  Scenario: Uso simultâneo de "data" e "dataInicio"
    Given que o comerciante informa "data=2025-06-01" e "dataInicio=2025-06-01"
    When o comerciante realiza a consulta de lançamentos
    Then o sistema rejeita a requisição com erro de validação
    And a mensagem de erro indica que os parâmetros são mutuamente exclusivos

  Scenario: Uso simultâneo de "data" e "dataFim"
    Given que o comerciante informa "data=2025-06-01" e "dataFim=2025-06-03"
    When o comerciante realiza a consulta de lançamentos
    Then o sistema rejeita a requisição com erro de validação
    And a mensagem de erro indica que os parâmetros são mutuamente exclusivos

  Scenario: dataInicio informada sem dataFim
    Given que o comerciante informa apenas "dataInicio=2025-06-01" sem "dataFim"
    When o comerciante realiza a consulta de lançamentos
    Then o sistema rejeita a requisição com erro de validação
    And a mensagem de erro indica que dataFim é obrigatória quando dataInicio for informada

  Scenario: dataFim informada sem dataInicio
    Given que o comerciante informa apenas "dataFim=2025-06-30" sem "dataInicio"
    When o comerciante realiza a consulta de lançamentos
    Then o sistema rejeita a requisição com erro de validação
    And a mensagem de erro indica que dataInicio é obrigatória quando dataFim for informada

  Scenario: Nenhum parâmetro de data informado
    Given que o comerciante não informa nenhum parâmetro de data
    When o comerciante realiza a consulta de lançamentos
    Then o sistema retorna uma lista vazia
    And não retorna erro

  # --- Regressão ---

  Scenario: Consulta por data com token inválido (regressão de segurança)
    Given que o comerciante possui um token JWT inválido ou expirado
    When o comerciante realiza a consulta de lançamentos com qualquer parâmetro de data
    Then o sistema rejeita a requisição com erro de autenticação

  # --- Documentação ---

  Scenario: Documentação Swagger atualizada
    Given que o comerciante acessa a documentação interativa da API
    When visualiza o endpoint de consulta de lançamentos
    Then os parâmetros dataInicio e dataFim estão documentados com descrição, formato yyyy-MM-dd e exemplos de uso
```

---

## 6. Non-Functional Requirements

**NFR-001 — Desempenho:** A consulta por intervalo de datas deve retornar resultados em tempo compatível com a experiência atual de consulta por data única. Intervalos extensos (ex: 1 ano inteiro) com volume elevado de registros devem responder dentro de um tempo aceitável para uso interativo.

**NFR-002 — Segurança:** A consulta por intervalo deve exigir autenticação, seguindo as mesmas regras de autorização já aplicadas ao endpoint existente. Não deve ser acessível sem token válido.

**NFR-003 — Retrocompatibilidade:** Nenhuma integração existente que usa `data=yyyy-MM-dd` pode ser impactada. O contrato de resposta deve permanecer idêntico quando `data` for utilizado.

**NFR-004 — Observabilidade:** Erros de validação dos novos parâmetros devem ser registrados nos logs com nível e contexto suficientes para diagnóstico.

**NFR-005 — Testabilidade:** A funcionalidade deve ser coberta por testes unitários que validem: intervalo válido, intervalo de data única, período sem lançamentos, `dataFim` anterior a `dataInicio`, combinação inválida com parâmetro `data`, e nenhum parâmetro informado.

---

## 7. Edge Cases & Special Scenarios

| Cenário | Comportamento esperado |
|---|---|
| `dataInicio` igual a `dataFim` | Retorna lançamentos do único dia informado (equivalente à consulta por `data`) |
| Intervalo sem nenhum lançamento | Retorna lista vazia, sem erro |
| Nenhum parâmetro informado | Retorna lista vazia, sem erro |
| `dataFim` anterior a `dataInicio` | Rejeita com erro de validação e mensagem clara |
| `data` informado junto com `dataInicio` ou `dataFim` | Rejeita com erro de validação indicando exclusividade |
| Apenas `dataInicio` sem `dataFim` | Rejeita com erro de validação solicitando `dataFim` |
| Apenas `dataFim` sem `dataInicio` | Rejeita com erro de validação solicitando `dataInicio` |
| Data com formato inválido (ex: `2025-6-1`, `01/06/2025`) | Rejeita com erro de validação indicando formato esperado `yyyy-MM-dd` |
| Requisição sem autenticação | Rejeita com erro de autenticação (comportamento já existente, não deve regredir) |

---

## 8. Out of Scope

- **Paginação dos resultados:** não incluída nesta especificação; deve ser tratada em task separada caso o volume de registros justifique.
- **Endpoint de consolidado diário:** nenhuma alteração no endpoint `/consolidado/{data}` faz parte desta entrega.
- **Filtros adicionais:** tipo de lançamento (crédito/débito), valor mínimo/máximo, descrição — fora do escopo desta feature.
- **Exportação de resultados:** geração de arquivos (CSV, Excel, PDF) para o período consultado — fora do escopo.
- **Alterações de autenticação:** o mecanismo de autenticação existente permanece inalterado.
- **Alteração no formato de resposta:** o formato dos objetos de lançamento retornados deve permanecer idêntico ao atual.

---

## 9. Success Metrics

| Métrica | Critério de sucesso |
|---|---|
| Retrocompatibilidade | Zero quebras em integrações que usam `data=yyyy-MM-dd` |
| Cobertura de testes | Testes unitários cobrindo todos os cenários definidos na seção 5 |
| Validação de erros | Todas as combinações inválidas de parâmetros retornam mensagem compreensível |
| Documentação | Swagger atualizado com novos parâmetros, descrições e exemplos antes do merge |
| Correção funcional | Consulta por intervalo retorna exatamente os lançamentos cujas datas estão dentro do intervalo inclusivo definido |

---

## 10. References

- **RN02** — Requisito de negócio existente: "Consultar lançamentos por data" (README.md)
- **Endpoint atual:** `GET /lancamentos?data=yyyy-MM-dd` (documentado em README.md — seção Endpoints da API)
- **ADR-006** — ASP.NET Core Minimal API com OpenAPI/Swagger (docs/adr/ADR-006-minimal-api-openapi.md)
- **ADR-007** — xUnit + FluentAssertions + NSubstitute para testes (docs/adr/ADR-007-testes-xunit-nsubstitute.md)
- **Fora de escopo relacionado:** paginação de resultados mencionada como task futura no próprio requisito (AG-2)
