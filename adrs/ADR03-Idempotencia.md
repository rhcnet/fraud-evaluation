# ADR 003: Estratégia de Idempotência e Deduplicação

## Status
**Aprovado**

## Contexto
* O sistema lida com validação de transações onde o reprocessamento e a duplicidade não são aceitáveis. 
* O contrato da API exige obrigatoriamente uma chave de idempotência. 
* Precisamos garantir que em cenários de retentativas, falhas de rede ou reenvios pelo broker de mensageria, o motor antifraude não execute a mesma validação duas vezes para a mesma transação.

---

## Opções Avaliadas

### Opção 1: Idempotência Apenas na Camada de Persistência (PostgreSQL)
* **Prós:** Garante consistência absoluta através de restrições de unicidade. Mantém o sistema imutável e auditável a longo prazo.
* **Contras:** Sob picos de carga ou ataques de cliques duplos rápidos (*race conditions*), todas as requisições batem diretamente no banco de dados, o que pode degradar a performance geral da API.

### Opção 2: Idempotência Híbrida em Duas Camadas (Redis + PostgreSQL)
* **Prós:** Combina a velocidade de leitura/escrita em memória para rejeição imediata de duplicatas com a segurança relacional e auditabilidade de longo prazo para armazenamento do estado final. Protege o banco de dados principal contra concorrência extrema.
* **Contras:** Introduz uma dependência de infraestrutura adicional (Redis) e adiciona a complexidade de gerenciar a sincronização de estado entre o cache e o banco.
---

## Decisão
**Opção Escolhida: Opção 2 - Idempotência Híbrida em Duas Camadas (Redis + PostgreSQL)**

Adotaremos uma estratégia híbrida que protege o ecossistema em dois níveis distintos de processamento:

### 1. Camada de Cache (Fast Check - API)
Utilização do **Redis** para armazenar a `Idempotency-Key` associada ao payload com um tempo de vida (TTL) de 24 horas. Na entrada da requisição HTTP (via Action Filter ou Middleware customizado no ASP.NET Core):
* Se a chave já existir e o processamento estiver concluído, o sistema intercepta a chamada em milissegundos e retorna o resultado armazenado (`APPROVED`, `REJECTED`, `REVIEW`).
* Se a chave existir mas o processamento inicial ainda estiver em andamento, a API bloqueia a nova tentativa e retorna imediatamente `HTTP 409 Conflict` ou `HTTP 102 Processing`.

### 2. Camada de Persistência (Strong Consistency - Banco de Dados)
Uso de uma restrição de unicidade (`UNIQUE CONSTRAINT`) no campo `IdempotencyKey` da tabela de transações no **PostgreSQL**. Esta é a última linha de defesa para garantir a atomicidade do sistema, mesmo que a camada de cache falhe ou oscile.

---

## Justificativa

1. **Garantia de Auditoria:** Ao persistir a chave de forma definitiva no banco de dados relacional principal, mantemos o rastro histórico imutável exigido pelo requisito de auditoria do sistema.
2. **Performance e Proteção contra Rajadas:** O cache em memória no Redis evita consultas concorrentes e inserções pesadas e desnecessárias no banco de dados relacional em cenários de cliques duplos rápidos do usuário final.
3. **Integridade de Sistemas Financeiros:** A restrição rígida em nível de banco de dados remove qualquer margem de erro ou inconsistência eventual (*race conditions* milissecundárias), atuando como uma trava atômica intransponível.

---

## Consequências

### Positivas:
* Proteção total e em múltiplas camadas contra o processamento duplicado de transações financeiras.
* Conformidade estrita com o contrato de API definido para o cenário de negócio.
* Resiliência a falhas de rede entre a API e o cliente, ou entre o broker de mensageria e o worker.

### Negativas / Desafios:
* Introduz uma dependência arquitetural extra (Redis) que precisa ser provisionada e monitorada na infraestrutura do projeto.
* Adiciona uma pequena latência adicional (sub-milissegundo) para a escrita inicial da chave na camada de cache antes do processamento principal.