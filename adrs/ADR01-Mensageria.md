# ADR 001: Escolha da Mensageria

## Status
**Aprovado**

## Contexto
* O módulo antifraude precisa processar transações financeiras de forma assíncrona, garantindo alta resiliência, auditoria e rastreabilidade. 
* O sistema receberá picos de carga (transações em lote ou eventos de alta demanda) e não pode perder nenhuma mensagem. 
* Além disso, o fluxo exige mecanismos robustos de tratamento de falhas, como *Retries* (tentativas de reprocessamento), *Exponential Backoff* e direcionamento para uma *Dead Letter Queue* (DLQ) para análise posterior de transações problemáticas.

---

## Opções Avaliadas

### Opção 1: RabbitMQ (Message Broker Tradicional)
#### Prós: 
* Excelente suporte nativo para roteamento complexo, gerenciamento de filas flexível, suporte maduro a DLQ e controle granular de *Acknowledge* (ACK/NACK). Perfeito para o ecossistema .NET com bibliotecas como MassTransit.
#### Contras: 
* Escalabilidade horizontal e alta disponibilidade (HA) exigem mais esforço de configuração de cluster e infraestrutura em comparação a soluções gerenciadas. 
* Não retém o histórico de mensagens por padrão após o consumo.

### Opção 2: Apache Kafka
#### Prós: 
* Altíssima vazão e escalabilidade horizontal massiva através de partições. 
* Retenção de logs durável (armazenamento persistente de eventos), o que facilita nativamente os requisitos de auditoria e *replay* (reprocessamento) de transações passadas.
#### Contras:
* Curva de aprendizado e complexidade operacional muito elevadas para gerenciar o cluster.
* O ecossistema .NET, embora bem atendido pela biblioteca Confluent, exige mais código manual para implementar padrões como DLQ e retries.

### Opção 3: AWS SQS + SNS (Serviço Gerenciado em Nuvem)
#### Prós: 
* Totalmente gerenciado (*Serverless*), escalabilidade praticamente infinita sem esforço operacional, alta disponibilidade nativa e integração simples com DLQ. 
* Excelente se a infraestrutura do projeto já estiver na AWS.
#### Contras:
* Modelo de custos baseado em requisições pode se tornar elevado sob tráfego massivo constante. 
* Vinculação ao provedor de nuvem (*Vendor Lock-in*). 
* Latência ligeiramente superior se comparado a brokers dedicados locais.

---

## Decisão
**Opção Escolhida: Opção 1 - RabbitMQ**

Escolhemos o **RabbitMQ** como o broker de mensageria principal para o módulo antifraude.

### Justificativa:
1. **Padrões de Resiliência Nativos:** O ecossistema .NET com RabbitMQ/MassTransit reduz drasticamente a complexidade para implementar exatamente o que o escopo pede: *Retry*, *Exponential Backoff* e *Dead Letter Queue (DLQ)* de forma declarativa e segura.
2. **Garantia de Entrega (At-least-once):** O modelo de *Acknowledge* do RabbitMQ garante que se um worker falhar ou cair no meio de uma análise antifraude, a transação retorna para a fila e é processada por outra instância, evitando perda de dados financeiros.
3. **Casamento Perfeito com Arquitetura de Microsserviços .NET:** O RabbitMQ atende perfeitamente à topologia de comandos/eventos necessária para isolar a API de recepção de transações dos *workers* que executam as regras de negócio pesadas do antifraude.

---

## Consequências

### Positivas:
* Implementação acelerada dos pontos de resiliência exigidos através de configurações simples no `Program.cs` usando MassTransit.
* Isolamento completo: em caso de pico de transações, a API que recebe os dados continua operando normalmente, enquanto o RabbitMQ amortece a carga para os workers processarem no próprio ritmo (*Load Leveling*).

### Negativas / Desafios:
* **Auditoria de Eventos:** Como o RabbitMQ apaga a mensagem da fila após o sucesso (ACK), a persistência e auditoria histórica das transações processadas deverão ser garantidas salvando o estado e o histórico de decisões diretamente no banco de dados do microsserviço (ou publicando em uma fila secundária de *logging/auditoria*).
* Exige atenção na configuração de concorrência dos *workers* (Prefetch Count) para não sobrecarregar as integrações externas ou o banco de dados durante rajadas de mensagens.