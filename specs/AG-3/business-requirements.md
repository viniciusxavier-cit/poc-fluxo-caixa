# Business Requirements — AG-3
## Permitir Atualização de um Lançamento Existente

**Issue Key:** AG-3  
**Date:** 2026-10-29  
**Complexity:** M (Medium)  
**Status:** Draft

---

## 1. Executive Summary

O sistema de Fluxo de Caixa atualmente permite registrar e remover lançamentos de débito e crédito, mas não oferece a capacidade de corrigi-los após o registro. Esta feature adiciona a operação de atualização de um lançamento existente, permitindo que o comerciante corrija dados informados erroneamente — tipo, valor, descrição ou data — sem precisar remover e recriar o registro. O consolidado diário deve refletir automaticamente qualquer alteração financeira ou de data resultante da atualização.

---

## 2. Problem Statement

### Estado Atual (Current State)
- O comerciante pode criar e remover lançamentos.
- Erros de digitação (valor errado, tipo invertido, data incorreta, descrição imprecisa) só podem ser corrigidos removendo o lançamento e criando um novo.
- A remoção + recriação gera dois registros de auditoria separados (remoção e nova criação) sem nenhum vínculo entre si, dificultando o rastreamento histórico.
- Não existe operação de atualização parcial ou total de um lançamento.

### Estado Desejado (Desired State)
- O comerciante pode atualizar os dados de um lançamento existente em uma única operação.
- O consolidado diário é automaticamente recalculado para refletir os novos valores.
- Caso a data do lançamento mude, o consolidado da data original é corrigido e o consolidado da nova data é atualizado.
- O histórico do lançamento (identificador único e data/hora de criação) permanece preservado.

### Impacto de Negócio
- **Quem se beneficia:** comerciantes e sistemas de integração (ERP/PDV) que registram lançamentos e ocasionalmente precisam corrigi-los.
- **Custo de não resolver:** erros financeiros persistem no consolidado diário até que o lançamento seja removido e recriado manualmente, aumentando o risco de saldo consolidado incorreto.
- **Urgência:** a ausência desta operação é uma lacuna funcional básica em sistemas de controle financeiro; impacta diretamente a confiabilidade dos relatórios de saldo diário.

---

## 3. Personas & User Stories

### Persona 1 — Comerciante (usuário direto)
O comerciante registra manualmente os lançamentos do dia. Pode cometer erros de digitação ou registrar um lançamento na data errada.

| User Story | Critério de Prioridade |
|---|---|
| Como comerciante, quero corrigir o valor de um lançamento que digitei errado, para que o saldo diário reflita o valor correto. | Must Have |
| Como comerciante, quero alterar o tipo de um lançamento de débito para crédito (ou vice-versa), para corrigir uma classificação incorreta. | Must Have |
| Como comerciante, quero mover um lançamento para a data correta quando o registrei na data errada, para que os consolidados de ambas as datas estejam corretos. | Must Have |
| Como comerciante, quero corrigir a descrição de um lançamento, para que os registros sejam mais informativos. | Must Have |

### Persona 2 — Sistema de Integração (ERP / PDV)
Sistema externo que registra lançamentos automaticamente. Pode enviar retificações após sincronizações ou reconciliações.

| User Story | Critério de Prioridade |
|---|---|
| Como sistema de integração, quero atualizar um lançamento previamente registrado via ID, para sincronizar correções originadas no ERP. | Must Have |

---

## 4. Business Rules

| Código | Regra |
|---|---|
| **BR-001** | Um lançamento só pode ser atualizado se existir no sistema. Tentativa de atualizar um lançamento inexistente resulta em erro de "não encontrado". |
| **BR-002** | O identificador único do lançamento é imutável e não pode ser alterado. |
| **BR-003** | A data e hora de criação original do lançamento (`CriadoEm`) é imutável e deve ser preservada após a atualização. |
| **BR-004** | O tipo do lançamento (Crédito ou Débito) pode ser alterado. Os valores válidos são: `1` = Crédito e `2` = Débito. |
| **BR-005** | O valor do lançamento deve ser maior que zero e não pode exceder R$ 1.000.000.000,00 (um bilhão de reais). |
| **BR-006** | O valor do lançamento aceita no máximo 2 (duas) casas decimais. |
| **BR-007** | A descrição do lançamento é obrigatória e não pode estar vazia ou conter apenas espaços. |
| **BR-008** | A descrição do lançamento deve ter no máximo 255 caracteres. |
| **BR-009** | A data do lançamento não pode ser uma data futura. Datas iguais ao dia atual são permitidas. |
| **BR-010** | Todos os campos atualizáveis (tipo, valor, descrição e data) devem ser fornecidos na requisição de atualização. Atualizações parciais não são suportadas. |
| **BR-011** | Quando tipo, valor ou data forem alterados, o consolidado diário deve ser recalculado automaticamente para refletir os novos dados. |
| **BR-012** | Quando apenas a descrição for alterada (tipo, valor e data permanecem iguais), o consolidado diário não precisa ser recalculado. |
| **BR-013** | Quando a data for alterada, o consolidado da data original deve ter o lançamento estornado (valores revertidos), e o consolidado da nova data deve receber o lançamento com os novos valores. |
| **BR-014** | Quando tipo ou valor forem alterados sem mudar a data, o consolidado da mesma data deve estornar os valores antigos e aplicar os novos valores. |
| **BR-015** | A operação de atualização requer autenticação. Usuários não autenticados não podem atualizar lançamentos. |
| **BR-016** | Uma falha no recálculo do consolidado diário não deve cancelar a atualização do lançamento. O lançamento é persistido com sucesso; o consolidado será corrigido eventualmente. |

---

## 5. Acceptance Criteria

```gherkin
Feature: Atualização de Lançamento Existente

  Background:
    Given que o comerciante está autenticado no sistema
    And existe um lançamento com id "abc-123", tipo Crédito, valor 500.00, descrição "Venda", data 2026-10-01

  # ─── Happy Path ───

  Scenario: Atualizar todos os campos de um lançamento com dados válidos (mesma data)
    When o comerciante envia uma atualização para o lançamento "abc-123"
      com tipo Débito, valor 750.00, descrição "Compra de estoque", data 2026-10-01
    Then o sistema retorna sucesso com os dados atualizados do lançamento
    And o lançamento "abc-123" passa a ter tipo Débito, valor 750.00, descrição "Compra de estoque", data 2026-10-01
    And o identificador único e a data de criação original permanecem inalterados
    And o consolidado do dia 2026-10-01 estorna o crédito de 500.00 e aplica o débito de 750.00

  Scenario: Atualizar apenas a descrição de um lançamento
    When o comerciante envia uma atualização para o lançamento "abc-123"
      com tipo Crédito, valor 500.00, descrição "Venda balcão - cliente João", data 2026-10-01
    Then o sistema retorna sucesso com os dados atualizados do lançamento
    And a descrição do lançamento "abc-123" é "Venda balcão - cliente João"
    And o consolidado do dia 2026-10-01 não é alterado

  Scenario: Atualizar apenas o valor de um lançamento (mesma data e tipo)
    When o comerciante envia uma atualização para o lançamento "abc-123"
      com tipo Crédito, valor 800.00, descrição "Venda", data 2026-10-01
    Then o sistema retorna sucesso com os dados atualizados do lançamento
    And o consolidado do dia 2026-10-01 estorna o crédito de 500.00 e aplica o crédito de 800.00

  Scenario: Mover um lançamento para uma data diferente
    Given existe um consolidado para 2026-10-01 com total de créditos 500.00
    When o comerciante envia uma atualização para o lançamento "abc-123"
      com tipo Crédito, valor 500.00, descrição "Venda", data 2026-10-02
    Then o sistema retorna sucesso com os dados atualizados do lançamento
    And o consolidado do dia 2026-10-01 tem o crédito de 500.00 estornado
    And o consolidado do dia 2026-10-02 recebe o crédito de 500.00 aplicado

  Scenario: Alterar tipo e data simultaneamente
    When o comerciante envia uma atualização para o lançamento "abc-123"
      com tipo Débito, valor 500.00, descrição "Venda", data 2026-10-03
    Then o sistema retorna sucesso com os dados atualizados do lançamento
    And o consolidado do dia 2026-10-01 tem o crédito de 500.00 estornado
    And o consolidado do dia 2026-10-03 recebe o débito de 500.00 aplicado

  # ─── Erros de Validação ───

  Scenario: Tentar atualizar um lançamento inexistente
    When o comerciante envia uma atualização para o lançamento "id-inexistente"
    Then o sistema retorna erro de "não encontrado"
    And nenhum lançamento ou consolidado é alterado

  Scenario: Tentar atualizar com valor zero
    When o comerciante envia uma atualização para o lançamento "abc-123"
      com valor 0.00
    Then o sistema retorna erro de validação
    And a mensagem de erro indica que o valor deve ser maior que zero
    And o lançamento "abc-123" permanece inalterado

  Scenario: Tentar atualizar com valor negativo
    When o comerciante envia uma atualização para o lançamento "abc-123"
      com valor -100.00
    Then o sistema retorna erro de validação
    And a mensagem de erro indica que o valor deve ser maior que zero
    And o lançamento "abc-123" permanece inalterado

  Scenario: Tentar atualizar com valor acima do limite máximo
    When o comerciante envia uma atualização para o lançamento "abc-123"
      com valor 1000000001.00
    Then o sistema retorna erro de validação
    And a mensagem de erro indica que o valor não pode exceder R$ 1.000.000.000,00
    And o lançamento "abc-123" permanece inalterado

  Scenario: Tentar atualizar com valor com mais de 2 casas decimais
    When o comerciante envia uma atualização para o lançamento "abc-123"
      com valor 100.999
    Then o sistema retorna erro de validação
    And a mensagem de erro indica que o valor deve ter no máximo 2 casas decimais
    And o lançamento "abc-123" permanece inalterado

  Scenario: Tentar atualizar com descrição vazia
    When o comerciante envia uma atualização para o lançamento "abc-123"
      com descrição ""
    Then o sistema retorna erro de validação
    And a mensagem de erro indica que a descrição é obrigatória
    And o lançamento "abc-123" permanece inalterado

  Scenario: Tentar atualizar com descrição acima do limite de caracteres
    When o comerciante envia uma atualização para o lançamento "abc-123"
      com uma descrição de 256 caracteres
    Then o sistema retorna erro de validação
    And a mensagem de erro indica que a descrição não pode exceder 255 caracteres
    And o lançamento "abc-123" permanece inalterado

  Scenario: Tentar atualizar com data futura
    When o comerciante envia uma atualização para o lançamento "abc-123"
      com data 2099-12-31
    Then o sistema retorna erro de validação
    And a mensagem de erro indica que não é permitido registrar lançamentos com data futura
    And o lançamento "abc-123" permanece inalterado

  Scenario: Tentar atualizar com tipo de lançamento inválido
    When o comerciante envia uma atualização para o lançamento "abc-123"
      com tipo 99
    Then o sistema retorna erro de validação
    And a mensagem de erro indica que o tipo de lançamento é inválido
    And o lançamento "abc-123" permanece inalterado

  # ─── Segurança ───

  Scenario: Tentar atualizar sem autenticação
    Given que o usuário não está autenticado
    When o usuário envia uma atualização para o lançamento "abc-123"
    Then o sistema retorna erro de não autorizado
    And o lançamento "abc-123" permanece inalterado

  # ─── Resiliência ───

  Scenario: Falha no recálculo do consolidado após atualização
    When o comerciante envia uma atualização válida para o lançamento "abc-123"
    And o serviço de consolidado diário está indisponível
    Then o sistema atualiza e persiste o lançamento "abc-123" com sucesso
    And o sistema registra o erro no log
    And a resposta ao comerciante indica sucesso na atualização do lançamento

  # ─── Casos de Borda ───

  Scenario: Atualizar um lançamento sem nenhuma alteração efetiva nos dados
    When o comerciante envia uma atualização para o lançamento "abc-123"
      com os mesmos tipo, valor, descrição e data do lançamento existente
    Then o sistema retorna sucesso com os dados do lançamento (sem alterações visíveis)
    And o consolidado diário não é recalculado

  Scenario: Atualizar para a data de hoje (data atual)
    When o comerciante envia uma atualização para o lançamento "abc-123"
      com data igual à data atual
    Then o sistema aceita a atualização e retorna sucesso

  Scenario: Consolidado da data original não existe ao mover lançamento de data
    Given não existe consolidado registrado para o dia 2026-10-01
    When o comerciante move o lançamento "abc-123" para a data 2026-10-05
    Then o sistema não tenta estornar um consolidado inexistente
    And o consolidado do dia 2026-10-05 recebe o lançamento atualizado normalmente
```

---

## 6. Non-Functional Requirements

### Performance
- A operação de atualização de um lançamento deve responder dentro de tempo aceitável para uso interativo.
- A atualização do consolidado decorrente da mudança do lançamento ocorre de forma assíncrona (eventual consistency), sem bloquear a resposta ao solicitante.

### Segurança
- A operação de atualização exige autenticação válida (o mesmo mecanismo de autenticação já utilizado para as demais operações do serviço de lançamentos).
- Não há requisito de autorização diferenciada entre perfis para esta operação neste momento (qualquer usuário autenticado pode atualizar qualquer lançamento).

### Confiabilidade
- Uma falha no recálculo do consolidado diário não deve resultar em falha visível para o solicitante nem impedir a persistência do lançamento atualizado.
- O sistema deve manter a atomicidade na persistência do lançamento: ou o lançamento é atualizado por completo, ou permanece no estado original — não deve haver estado parcialmente atualizado.

### Rastreabilidade
- Toda tentativa de atualização inválida deve ser rastreável via os mecanismos de logging já existentes no sistema.
- Erros no recálculo do consolidado decorrentes de uma atualização devem ser registrados no log com detalhes suficientes para diagnóstico (identificador do lançamento, data afetada, motivo do erro).

### Taxa de Requisições
- O endpoint de atualização está sujeito ao mesmo controle de taxa de requisições já aplicado aos demais endpoints de lançamentos.

---

## 7. Edge Cases & Special Scenarios

| Cenário | Comportamento Esperado |
|---|---|
| Lançamento não encontrado | Retorna erro de "não encontrado"; nenhuma alteração é realizada. |
| Nenhum campo financeiro alterado (apenas descrição) | Atualiza descrição; consolidado não é recalculado. |
| Data movida para um dia sem consolidado registrado | Não tenta estornar o consolidado da data original (inexistente); apenas aplica o lançamento na nova data. |
| Data movida de um dia sem consolidado para outro sem consolidado | Nenhum estorno é necessário; cria ou atualiza o consolidado da nova data. |
| Todos os valores idênticos ao original | Persiste sem erros; consolidado não é recalculado. |
| Tipo alterado na mesma data | Estorna o tipo antigo, aplica o novo tipo no mesmo dia. |
| Valor e tipo alterados ao mesmo tempo na mesma data | Estorna tipo e valor antigos; aplica novos tipo e valor no mesmo dia. |
| Falha no serviço de consolidado | Lançamento é persistido com sucesso; erro registrado em log; resposta de sucesso ao solicitante. |
| Tipo de lançamento inválido | Validação falha antes de qualquer persistência; retorna erro de validação. |
| Requisição sem token de autenticação | Retorna erro de não autorizado; nenhuma alteração é realizada. |

---

## 8. Out of Scope

Os itens a seguir **não fazem parte** desta feature e não devem ser implementados:

- **Atualização parcial (patch):** todos os campos atualizáveis devem ser fornecidos; atualização de campo único sem fornecer os demais não é suportada.
- **Histórico de versões do lançamento:** o sistema não registrará um log de alterações com os valores anteriores versus novos do lançamento.
- **Controle de concorrência otimista:** não há requisito de detecção de conflito quando dois solicitantes atualizam o mesmo lançamento simultaneamente.
- **Notificação ao usuário sobre recálculo do consolidado:** o sistema não enviará alertas ou mensagens assíncronas ao comerciante sobre o status do recálculo do consolidado.
- **Restrição de atualização por perfil:** a autorização diferenciada (ex: apenas admin pode atualizar) não é escopo desta feature.
- **Atualização em lote:** atualizar múltiplos lançamentos em uma única requisição não é suportado.
- **Soft delete com restauração:** a operação de atualização não restaura lançamentos previamente removidos.
- **Validação de saldo negativo no consolidado:** o sistema não rejeita uma atualização que resulte em saldo consolidado negativo para um dia.

---

## 9. Success Metrics

| Métrica | Critério de Sucesso |
|---|---|
| Operação disponível | O sistema aceita requisições de atualização de lançamentos. |
| Integridade dos dados do lançamento | Após atualização, o lançamento reflete exatamente os dados enviados na requisição; campos imutáveis (id, data de criação) permanecem inalterados. |
| Consistência do consolidado diário | O consolidado de cada data afetada reflete corretamente o efeito da atualização (créditos e débitos ajustados). |
| Resiliência | Uma falha no recálculo do consolidado não resulta em perda da atualização do lançamento. |
| Validação | Requisições com dados inválidos são rejeitadas antes de qualquer alteração, com mensagem de erro clara. |
| Segurança | Requisições sem autenticação válida são rejeitadas. |

---

## 10. References

| Referência | Descrição |
|---|---|
| README.md | Visão geral do sistema, requisitos de negócio existentes (RN01–RN06) e arquitetura |
| RN01 | Requisito existente: Registrar lançamentos de débito e crédito |
| RN03 | Requisito existente: Remover um lançamento |
| BR de criação (CriarLancamentoCommandValidator) | Regras de validação de Tipo, Valor, Descrição e Data — reaproveitadas em BR-004 a BR-009 |
| LancamentoCriadoEvent / LancamentoRemovidoEvent | Padrão de eventos de domínio que propaga mudanças ao consolidado — mesmo padrão a ser adotado para atualização |
| RecalcularConsolidadoEventHandler | Handler que estorna lançamentos no consolidado ao remover — lógica de estorno relevante para a atualização |
| AtualizarConsolidadoEventHandler | Handler que aplica novos lançamentos no consolidado ao criar — lógica de aplicação relevante para a atualização |
| ConsolidadoDiario.EstornarLancamento() | Operação de domínio que reverte o efeito de um lançamento no consolidado |
| ConsolidadoDiario.AplicarLancamento() | Operação de domínio que aplica um lançamento no consolidado |
