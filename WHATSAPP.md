# WhatsApp do Padelizou — como ligar, e o que fazer quando parar

> Escrito em 30/07/2026, quando o canal saiu da Z-API paga pra uma **Evolution API rodando no
> nosso próprio VPS**. Custo mensal: **R$ 0**. Custo de um erro: um chip pré-pago.

## 🔴 Estado em 07/08/2026 — leia antes de mexer

O canal ficou **desligado de 04/08 17:49 a 07/08** (o `Evolution__BaseUrl` foi esvaziado no
systemd depois de a Meta restringir o número por spam). **Religado a pedido do Felipe** —
`Environment=Evolution__BaseUrl=http://127.0.0.1:8081`, backup do arquivo desligado em
`/root/whatsapp.conf.bak-20260807`.

⚠️ **O chip continua despareado** (`state: close`): enquanto isso, **nada sai**. Falta só o
passo do QR, abaixo.

⚠️ **Desligado não acende alarme.** O vigia faz `if (estado == Desligado) return` — ele foi
feito pra pegar canal que CAIU, e não distingue "desligado no dev" de "desligado em produção
por engano". Foi assim que três dias passaram sem ninguém notar. Agora o **painel `/Admin`
mostra o estado sempre**, num medidor discreto; se um dia os avisos sumirem, **confira o
`Evolution__BaseUrl` no systemd antes do log** — canal desligado não deixa rastro (o
`EvolutionApiService` loga em `Debug`, de propósito).

### O que o código faz sozinho pra não repetir 04/08

| Freio | Onde | Vale quanto |
|---|---|---|
| **Consentimento** | Botão do admin raiz no `/Admin` | Tira do canal quem nunca pediu (era 54 de 55 contas antigas) e convida de volta por push e e-mail |
| **Ritmo** | `RitmoDoWhatsApp` | 7–16s sorteados entre mensagens — mata a rajada |
| **Teto** | `TetoDoWhatsApp` | 60/hora (janela deslizante) e 300/dia |
| **Aquecimento** | `VolumeDoWhatsApp` | 30/dia na primeira semana **depois de conectar**; sem data gravada, vale o teto baixo |
| **Saída** | `AvisoPorWhatsApp.Montar` | Toda mensagem diz como parar de receber |

O que o teto barra é **descartado, não adiado** — e contado, no medidor do painel. Adiar
devolveria o excedente em rajada assim que a janela abrisse.

## O que já está pronto

- Container `evolution-api` (v2.3.7) + `evolution-db` rodando em `/opt/evolution`, com
  `restart: always` — sobem sozinhos se o servidor reiniciar.
- Escuta **só em `127.0.0.1:8081`**. Não há porta aberta pra internet: quem fala com ela é o
  app, que roda na mesma máquina.
- Instância `padelizou` criada, status `close` (esperando o chip).
- Produção já sabe o endereço e a chave (drop-in `whatsapp.conf` no systemd). ⚠️ **Conferir de
  verdade**, não confiar nesta linha: foi ela que ficou desatualizada por três dias em 04–07/08.
  `systemctl cat padelizou | grep Evolution__BaseUrl` — vazio quer dizer **canal desligado**.
- Todo aviso do sistema já tenta o WhatsApp — ver `Services/PushNotificationService.cs`.

## O único passo que falta: o chip

**Compre um chip pré-pago novo. Nunca use o seu número pessoal.** Se a Meta identificar
automação, o banimento é **permanente para aquele número** — e você não quer perder o seu.

Coloque o chip num celular qualquer, instale o WhatsApp normal nele, e então:

### 1. Gerar o QR code

```bash
ssh root@179.197.233.184 'set -a; . /opt/evolution/.env; set +a; curl -s "http://127.0.0.1:8081/instance/connect/padelizou" -H "apikey: $EVOLUTION_API_KEY"'
```

Isso devolve um campo `code` (o texto do QR) e um `base64` (a imagem). O jeito mais fácil de
ler: abrir o **painel** da Evolution por um túnel SSH, que mostra o QR na tela:

```bash
ssh -L 8081:127.0.0.1:8081 root@179.197.233.184
```

Com o túnel aberto, acesse `http://127.0.0.1:8081/manager` no navegador. A chave de acesso
está em `/opt/evolution/.env` (`EVOLUTION_API_KEY`).

### 2. Ler o QR no celular do chip

WhatsApp → **Dispositivos conectados** → **Conectar dispositivo** → aponta pro QR.

### 3. Conferir que conectou

```bash
ssh root@179.197.233.184 'set -a; . /opt/evolution/.env; set +a; curl -s "http://127.0.0.1:8081/instance/fetchInstances" -H "apikey: $EVOLUTION_API_KEY"'
```

Tem que aparecer `"connectionStatus":"open"`. Enquanto estiver `close`, nada é enviado (o app
não quebra — só não manda).

### 4. Testar com o SEU número antes de soltar

```bash
ssh root@179.197.233.184 'set -a; . /opt/evolution/.env; set +a; curl -s -X POST "http://127.0.0.1:8081/message/sendText/padelizou" -H "apikey: $EVOLUTION_API_KEY" -H "Content-Type: application/json" -d "{\"number\":\"5551999999999\",\"text\":\"teste do Padelizou\"}"'
```

Troque `5551999999999` pelo seu número com o 55 na frente.

## Regras pra não perder o chip

O risco de banimento é real e não some — dá pra deixar pequeno:

1. **Nunca mandar pra quem não pediu.** O sistema já respeita isso: só vai pra quem tem conta,
   marcou "aceito WhatsApp" nas preferências e tem número válido.
2. **Ritmo.** Acima de ~500 mensagens/hora o risco sobe muito. Hoje o volume é de dezenas.
3. **Conteúdo variado.** Mensagem idêntica pra muita gente é o sinal clássico de spam. As
   nossas já variam (nome do torneio, horário, nome da pessoa).
4. **Deixe o chip "vivo".** Um número que só dispara e nunca recebe parece robô. Mandar e
   responder alguma conversa de vez em quando ajuda.

## Quando parar de funcionar

**Sintoma: ninguém recebe mais nada.**

```bash
ssh root@179.197.233.184 'docker ps --format "{{.Names}}\t{{.Status}}"'
ssh root@179.197.233.184 'docker logs --tail 50 evolution-api'
```

- **Container caiu** → `cd /opt/evolution && docker compose up -d`
- **`connectionStatus` voltou pra `close`** → o celular ficou muito tempo sem internet ou a
  sessão caiu. Refaça o passo 1 (o QR code).
- **Chip banido** → compre outro chip, apague a instância e crie de novo:
  ```bash
  ssh root@179.197.233.184 'set -a; . /opt/evolution/.env; set +a; curl -s -X DELETE "http://127.0.0.1:8081/instance/delete/padelizou" -H "apikey: $EVOLUTION_API_KEY"'
  ```
  Depois recrie com `POST /instance/create` (`instanceName: padelizou`) e leia o QR de novo.
  **Nada no Padelizou precisa mudar** — o nome da instância continua o mesmo.

**Sintoma: o log do Padelizou reclama.** Procure por `Evolution API devolveu`:

```bash
ssh root@179.197.233.184 "journalctl -u padelizou --since '1 hour ago' | grep -i evolution"
```

## Se um dia o volume crescer

Aí vale migrar pra **API oficial da Meta** (Cloud API): sem risco de ban, mas cobra por
mensagem (~R$ 0,03–0,09 a utilitária) e exige templates aprovados. A troca é barata no código:
basta uma nova classe implementando `IWhatsAppService` — o resto do sistema não fica sabendo,
igual foi a saída da Z-API.

## Desligar o canal (sem mexer em código)

```bash
ssh root@179.197.233.184 'rm /etc/systemd/system/padelizou.service.d/whatsapp.conf && systemctl daemon-reload && systemctl restart padelizou'
```

Sem `Evolution__BaseUrl`, o canal nasce desligado e o sistema volta a mandar só notificação.
