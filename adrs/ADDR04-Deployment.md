# ADR 004: Estratégia de Deployment

## Status
**Aprovado**

## Contexto
* O módulo de validação antifraude processa transações financeiras e exige alta disponibilidade, resiliência (capacidade de auto-recuperação) e elasticidade para lidar com picos sazonais de tráfego (ex: Black Friday) sem degradação da latência. 
* Como o sistema é dividido em uma API que recebe a solicitação e um Worker para processamento de regras, a estratégia de infraestrutura deve permitir o isolamento e o escalonamento independente de cada componente, equilibrando o custo e a complexidade operacional.

---

## Opções Avaliadas

### Opção 1: Kubernetes (K8s) - A Escolha de Alta Disponibilidade
#### Prós:
* Controle granular sobre os recursos de infraestrutura. 
* Garante *self-healing* (auto-recuperação): se um Pod do Worker falhar, o orquestrador o substitui instantaneamente. 
* Permite escalabilidade elástica via *Horizontal Pod Autoscaler* (HPA) baseada em métricas customizadas, como o tamanho da fila do RabbitMQ (KEDA).
#### Contras:
* *Curva de aprendizado acentuada e altíssima complexidade operacional para configuração, segurança, gerenciamento de rede (Service Mesh/Ingress) e manutenção do cluster. 
* Custo base elevado para ambientes menores.

### Opção 2: Serverless (AWS Lambda / Azure Functions) - Foco em Event-Driven
#### Prós:
* Escalabilidade nativa praticamente infinita e automática por mensagem recebida na fila. Modelo de custo estritamente baseado em consumo (*pay-per-use*). 
* Reduz quase a zero a gestão de sistemas operacionais e servidores.
#### Contras:
* Risco de *Cold Start* (atraso na primeira execução de uma instância fria), o que pode penalizar a latência exigida pelo endpoint que recebe a solicitação. 
* Além disso, dificulta testes locais e aprisiona a arquitetura ao provedor de nuvem (*vendor lock-in*).

### Opção 3: Containers Gerenciados (Docker com AWS ECS / Azure Container Apps) - O Meio-Termo
#### Prós:
* Oferece os benefícios de isolamento, portabilidade e imutabilidade dos containers Docker sem a complexidade de gerenciar um plano de controle do Kubernetes. 
* Facilita pipelines de CI/CD simplificados e garante paridade absoluta entre os ambientes de desenvolvimento e produção ("na minha máquina funciona"). 
* Custos previsíveis e menor sobrecarga operacional (*overhead*).
#### Contras:
Embora possua regras de auto-scale (baseadas em CPU/Memória), o tempo de reação para criar novas instâncias de containers sob picos de tráfego abruptos pode ser ligeiramente mais lento do que o modelo Serverless puro.

---

## Decisão
**Opção Escolhida: Opção 3 - Containers Gerenciados (Docker via AWS ECS Fargate ou Azure Container Apps)**

* Escolhemos empacotar a API e o Worker em **containers Docker** independentes e implantá-los em um serviço de **containers gerenciados *serverless*** (como AWS ECS com Fargate ou Azure Container Apps).

### Justificativa:
1. **Isolamento e Escalabilidade Coerente:** A API e o Worker rodarão em containers separados. Isso nos permite escalar apenas o Worker de forma agressiva quando a fila do RabbitMQ acumular mensagens, mantendo a API leve e estável com recursos fixos e previsíveis para evitar latência no `POST /transactions`.
2. **Resiliência e Auto-Recuperação Sem Complexidade:** A plataforma de container gerenciado monitora a saúde das instâncias de forma nativa (*Health Checks*). Se o container do Worker travar devido a uma falha crítica não capturada, o serviço encerra a instância defeituosa e provisiona um novo container imediatamente (*self-healing*), garantindo a resiliência exigida pelo case.
3. **Equilíbrio Operacional (Foco no Negócio):** Como um projeto sênior, a decisão considera o *Time-to-Market*. Evitamos a complexidade excessiva do Kubernetes (K8s), permitindo que a equipe foque na lógica de detecção de fraudes e na resiliência do código .NET, sem gastar esforço de engenharia gerenciando infraestrutura de clusters complexos.

---

## Consequências

### Positivas:
* Pipelines de deploy (CI/CD) extremamente simples e rápidos usando Dockerfiles padrão.
* Garantia de ambiente idêntico entre o desenvolvimento local do engenheiro e a produção na nuvem.
* Escalabilidade independente para a camada de ingestão (API) e a camada de processamento pesado (Worker).

### Negativas / Desafios:
* O auto-scale baseado em tamanho de fila precisará ser configurado via métricas integradas da nuvem (ex: CloudWatch/Azure Monitor disparando alarmes para o serviço de container), exigindo um pequeno esforço de parametrização inicial se comparado ao KEDA do Kubernetes.
* Dependência de ferramentas de telemetria integradas da plataforma escolhida para garantir a observabilidade de logs e tracing dos containers.