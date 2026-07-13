# ADR 002: Escolha do Banco de Dados

## Status
**Aprovado**

## Contexto
* O módulo antifraude precisa armazenar dados de transações recebidas, históricos de análises, decisões tomadas (`APPROVED`, `REJECTED`, `REVIEW`) e metadados para garantir a **idempotência** (evitar reprocessamento) e a **auditabilidade** do sistema. 
* O banco de dados precisa suportar escritas rápidas para não gerar gargalo no fluxo de processamento dos *workers* e permitir consultas eficientes para auditoria e checagem de regras de fraude.

---

## Opções Avaliadas

### Opção 1: SQL Server / PostgreSQL (Relacional)
#### Prós:
* Transações ACID garantem consistência. 
* Excelente suporte para garantir idempotência através de chaves primárias ou índices únicos. 
* Facilidade para criar queries de auditoria e relatórios usando SQL. Integração nativa e de alta performance com .NET através do Entity Framework Core (EF Core).
#### Contras:
* Escalabilidade horizontal mais complexa e cara. 
Esquema rígido (*schema-enforced*) exige migrações de banco (*migrations*) estruturadas caso os dados mudem de formato.

### Opção 2: MongoDB (NoSQL Baseado em Documentos)
#### Prós:
* Esquema flexível, permitindo armazenar o *payload* completo e variável de transações de diferentes origens sem quebras de schema. 
* Altíssima performance de escrita e facilidade de particionamento/escalabilidade horizontal.
#### Contras:
* Menor suporte a joins complexos entre tabelas (ex: cruzar histórico de múltiplos anos de forma relacional). 
* Embora suporte transações ACID em versões recentes, não é o comportamento nativo ideal para cenários de concorrência extrema onde o bloqueio rígido de registros é necessário.

### Opção 3: Redis (NoSQL Chave-Valor In-Memory)
#### Prós:
* Latência extremamente baixa (sub-milissegundo). 
* Perfeito para checagem rápida de chaves de idempotência (ex: travar uma transação duplicada em milissegundos).
#### Contras:
* Não foi projetado para armazenamento persistente de longo prazo e auditoria de grandes volumes de dados complexos devido ao custo de memória RAM e limitações de busca por múltiplos atributos.

---

## Decisão
**Opção Escolhida: Opção 1 - PostgreSQL**

Escolhemos o **PostgreSQL** como o banco de dados principal de persistência e auditoria para o módulo antifraude, utilizando o **EF Core** para persistência na camada de aplicação .NET.

### Justificativa:
1. **Garantia de Idempotência Segura:** O uso de restrições de unicidade (*Unique Constraints*) em nível de banco de dados relacional é a estratégia mais resiliente para garantir a idempotência estrita solicitada pelo escopo. Se duas mensagens idênticas tentarem ser processadas simultaneamente por *workers* diferentes, o banco rejeitará a segunda transação na tentativa de inserção.
2. **Auditabilidade de Ponta a Ponta:** Sistemas antifraude exigem trilhas de auditoria imutáveis e relacionamentos claros entre a transação recebida, as regras que foram disparadas e a decisão final. O modelo relacional garante a integridade referencial dessas informações.
---

## Consequências

### Positivas:
* Garantia de consistência e não-duplicação de registros de transações através de chaves únicas.
* Facilidade para a equipe extrair relatórios e históricos de fraude utilizando consultas SQL padrão.
* Uso de capacidades de auditoria temporal ou tabelas de histórico nativas se necessário no futuro.

### Negativas / Desafios:
* Necessidade de gerenciar e aplicar *migrations* no pipeline de CI/CD para evolução do banco de dados.
* Exige uma estratégia de arquivamento de dados de longo prazo (*data purging/cold storage*) para evitar o crescimento excessivo das tabelas relacionais com o passar dos anos, o que poderia degradar a performance das buscas de histórico recente.