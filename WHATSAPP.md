# WhatsApp do Padelizou — como ligar, e o que fazer quando parar

> Escrito em 30/07/2026, quando o canal saiu da Z-API paga pra uma **Evolution API rodando no
> nosso próprio VPS**. Custo mensal: **R$ 0**. Custo de um erro: um chip pré-pago.

## 🟢 Estado em 24/08/2026 — leia antes de mexer

**O canal está DE PÉ.** Conferido no servidor em 24/08: `"connectionStatus":"open"`, instância
`padelizou`, número `5551 9239-5650`, `_count` de mensagens em **zero** — nada saiu ainda pelo
chip novo.

⚠️ **A saída do `fetchInstances` mostra `"disconnectionReasonCode":401` e `device_removed`
MESMO COM O CANAL ABERTO.** Não é o estado de agora: é o **registro histórico** da última
queda, e a data dentro dele prova (`2026-08-04T16:00:58Z` — a restrição de 04/08). O campo
nunca é limpo ao reconectar. **Quem responde "está conectado?" é o `connectionStatus`, e mais
nada.**

⚠️ **Chip aberto é METADE do circuito.** A outra metade é o `Evolution__BaseUrl` no systemd —
sem ele o app nasce com o canal desligado e nada sai do mesmo jeito. Confira as duas coisas
antes de concluir qualquer coisa (comando no fim da seção *O que já está pronto*).

🔒 **A saída do `fetchInstances` carrega o `token` da instância** — é credencial. Não cole o
print em grupo, issue ou PR.

### Antes disso — o histórico que explica o desenho de hoje

O canal ficou **desligado de 04/08 17:49 a 07/08** (o `Evolution__BaseUrl` foi esvaziado no
systemd depois de a Meta restringir o número por spam). **Religado a pedido do Felipe** —
`Environment=Evolution__BaseUrl=http://127.0.0.1:8081`, backup do arquivo desligado em
`/root/whatsapp.conf.bak-20260807`.

O chip ficou **despareado de 04/08 a 24/08** (`state: close`) — vinte dias em que nada saiu.

> **21/08/2026** — o *alcance* do canal encolheu (a família de torneio saiu; ver *Quem ainda
> fala pelo WhatsApp*). Nesse dia o chip **não foi reconferido**: as duas tentativas morreram
> nas armadilhas de comando (ver *Os comandos daqui são pra colar no PowerShell*), e a leitura
> errada foi "o chip caiu" quando ninguém tinha conseguido perguntar.
>
> **24/08/2026** — chip pareado de novo, `connectionStatus: open`. É o estado no topo.

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
| **Teto** | `TetoDoWhatsApp` | 250/hora (janela deslizante) e 1.200/dia |
| **Alcance** | `AlcanceDoAviso` | Aviso novo nasce **sem** WhatsApp; só vai quem pedir na cara |
| **Saída** | `AvisoPorWhatsApp.Montar` | Toda mensagem diz como parar de receber |

⚠️ **Não existe mais aquecimento.** A versão de 04/08 segurava o canal em 30/dia por uma semana
depois de reconectar; foi **removida em 07/08 por decisão do Felipe** — rampa faz sentido pra
número novo, e não pro nosso, que só levou uma restrição e tem histórico de uso real. O código
dela está no commit `3d4dc8d` se um dia o chip for trocado por um número de verdade novo.

O que o teto barra é **descartado, não adiado** — e contado, no medidor do painel. Adiar
devolveria o excedente em rajada assim que a janela abrisse.

## Quem ainda fala pelo WhatsApp

Cada aviso declara seu alcance em `Services/AlcanceDoAviso.cs`. A régua pra entrar aqui são
**três** coisas ao mesmo tempo — **pessoal** (é sobre a própria pessoa), **urgente** (perde
valor se ela vir amanhã) e **acionável** (ela faz alguma coisa por causa dele). Duas de três
não bastam.

**Aulas**

| Aviso | Onde |
|---|---|
| Aluno pediu aula → pro professor | `AulasController.Aluno.cs` (`AppEWhatsAppSemEmail` — o e-mail bom, com Aceitar/Recusar, já sai à parte) |
| Aluno desmarcou → pro professor | `AulasController.Aluno.cs` |
| Aula apagada pelo professor → pro aluno | `AulasController.Agenda.cs` |
| Reposição marcada → pro aluno | `AulasController.Agenda.cs` |
| Aula mudou de **horário ou local** | `EdicaoDeAula.CanalDoAviso` — preço sozinho **não** vai |

**Desafios**

| Aviso | Onde |
|---|---|
| Você foi desafiado (morre em 48h) | `DesafiosController.cs` |
| Seu parceiro te incluiu num desafio | `DesafiosController.cs` |

**Inscrição**

| Aviso | Onde |
|---|---|
| Pagamento pendente (vence e custa a vaga) | `PagamentoExpiradoBackgroundService.cs` |

### O que SAIU do canal, e quando

- **09/08/2026** — lembrete do jogo fixo da panelinha (é o mesmo dia e a mesma hora toda
  semana; quem está no grupo já sabe), resultado de partida, "alguém que você segue se
  inscreveu".
- **21/08/2026 — a família de torneio inteira**, por decisão do Felipe: `"Seu jogo é o
  próximo!"`, `"Chaves do X saíram!"`, `"Torneio cancelado"` e `"Abriu vaga — vocês estão
  dentro!"` (nos dois caminhos: desistência e estorno). Era **o grosso do volume do canal** —
  só um torneio de 100 pessoas dava ~450 mensagens no dia, e as chaves saíam 100 de uma vez,
  todas com texto quase igual. Os quatro continuam indo por app, caixa de avisos e e-mail.

⚠️ **Isso derrubou o volume real pra bem abaixo do teto.** Os tetos ficaram onde estavam de
propósito — eles existem pra um laço infinito não torrar o número numa madrugada, não pra
apertar o uso normal. Ver o comentário no topo de `Services/VolumeDoWhatsApp.cs`.

## O que já está pronto

- Container `evolution-api` (v2.3.7) + `evolution-db` rodando em `/opt/evolution`, com
  `restart: always` — sobem sozinhos se o servidor reiniciar.
- Escuta **só em `127.0.0.1:8081`**. Não há porta aberta pra internet: quem fala com ela é o
  app, que roda na mesma máquina.
- Instância `padelizou` criada e **pareada** — `open` desde 24/08.
- Produção já sabe o endereço e a chave (drop-in `whatsapp.conf` no systemd). ⚠️ **Conferir de
  verdade**, não confiar nesta linha: foi ela que ficou desatualizada por três dias em 04–07/08.
  `ssh root@179.197.233.184 'systemctl cat padelizou | grep Evolution__BaseUrl'` — vazio quer
  dizer **canal desligado**. ⚠️ O `systemctl` roda **dentro das aspas**, no servidor — ver a
  seção dos comandos, abaixo, pra por que isso derruba o diagnóstico com tanta frequência.
- ⚠️ **NEM TODO AVISO VAI PRO WHATSAPP — hoje é a minoria.** Esta linha já disse o contrário, e
  era ela que descrevia o sistema que queimou o número: até 04/08 todo aviso tentava o canal.
  Agora cada aviso declara até onde vai (`Services/AlcanceDoAviso.cs`), e o padrão é **não ir**.
  A lista do que ainda vai está na seção *Quem ainda fala pelo WhatsApp*, acima.

## ⌨️ Os comandos daqui são pra colar no PowerShell do Windows

É de lá que eles são usados, e isso muda como precisam ser escritos. **Dois diagnósticos
falharam em 21 e 24/08 por causa disto**, e nas duas vezes a conclusão errada foi "o chip
caiu" quando o servidor nunca teve problema nenhum:

| Armadilha | O que aparece | Por quê |
|---|---|---|
| **Aspas duplas somem** | `bash: APIKEY: command not found` | O PowerShell come as aspas duplas internas ao passar argumento pra executável nativo. O `bash` do servidor recebeu o `\|` do `grep` como **pipe de verdade** |
| **Nome da variável** | `{"status":401,"error":"Unauthorized"}` | Na Evolution v2 a chave no `.env` é `AUTHENTICATION_API_KEY`; `EVOLUTION_API_KEY` é da v1. Expande vazia e o header sai em branco |
| **Comando rodou na sua máquina** | `systemctl não é reconhecido como cmdlet` | Faltou pôr o comando **dentro das aspas** do `ssh` |

**A régua, que vale pra todo comando novo que entrar neste arquivo:**

1. **Zero aspas duplas** num comando de uma linha. O truque é `-H apikey:$CHAVE` **sem espaço
   depois dos dois-pontos** — sem espaço, não precisa citar.
2. **Aspas simples de fora** (o PowerShell não mexe no que está dentro) e `|` sem aspas quando
   o pipe é pra valer.
3. **Precisa de JSON? Não faça uma linha só.** Entre no servidor (`ssh root@...`) e cole o
   comando lá dentro, no bash — é o caso do envio de teste, abaixo.
4. **Comando de diagnóstico que só funciona no bash do Linux é comando que não funciona**,
   porque quem digita está no PowerShell.

## Parear o chip (feito em 24/08 — isto aqui é pra próxima vez)

O passo abaixo **já foi executado**: o chip está pareado desde 24/08. Ele fica escrito porque a
sessão do WhatsApp cai sozinha — celular muito tempo sem internet, aparelho removido na mão,
chip banido — e nesse dia é por aqui que se recomeça.

**Se for trocar o chip: compre um pré-pago novo, nunca use o seu número pessoal.** Se a Meta
identificar automação, o banimento é **permanente para aquele número**.

Coloque o chip num celular qualquer, instale o WhatsApp normal nele, e então:

### 1. Gerar o QR code

```bash
ssh root@179.197.233.184 'set -a; . /opt/evolution/.env; set +a; curl -s http://127.0.0.1:8081/instance/connect/padelizou -H apikey:${AUTHENTICATION_API_KEY:-$EVOLUTION_API_KEY}'
```

Isso devolve um campo `code` (o texto do QR) e um `base64` (a imagem). O jeito mais fácil de
ler: abrir o **painel** da Evolution por um túnel SSH, que mostra o QR na tela:

```bash
ssh -L 8081:127.0.0.1:8081 root@179.197.233.184
```

Com o túnel aberto, acesse `http://127.0.0.1:8081/manager` no navegador. A chave de acesso
está em `/opt/evolution/.env`.

⚠️ **`{"status":401,"error":"Unauthorized"}` quase nunca é chave errada — é a variável que
expandiu VAZIA**, porque o nome dela no `.env` não é o que o comando pediu (na Evolution v2 ela
se chama `AUTHENTICATION_API_KEY`; a v1 usava `EVOLUTION_API_KEY`). Os comandos deste arquivo
aceitam as duas (`${AUTHENTICATION_API_KEY:-$EVOLUTION_API_KEY}`). Se ainda der 401, confira o
nome de verdade antes de suspeitar do chip:

```bash
ssh root@179.197.233.184 'grep -i api /opt/evolution/.env'
```

### 2. Ler o QR no celular do chip

WhatsApp → **Dispositivos conectados** → **Conectar dispositivo** → aponta pro QR.

### 3. Conferir que conectou

```bash
ssh root@179.197.233.184 'set -a; . /opt/evolution/.env; set +a; curl -s http://127.0.0.1:8081/instance/fetchInstances -H apikey:${AUTHENTICATION_API_KEY:-$EVOLUTION_API_KEY}'
```

Tem que aparecer `"connectionStatus":"open"`. Enquanto estiver `close`, nada é enviado (o app
não quebra — só não manda).

### 4. Testar com o SEU número antes de soltar

⚠️ **Este é o único que NÃO vai como uma linha só do PowerShell** — ele manda JSON, e JSON
exige aspas duplas (ver a régua abaixo). **Entre no servidor primeiro**, e só então cole:

```powershell
ssh root@179.197.233.184
```

Já dentro do servidor, no bash dele:

```bash
set -a; . /opt/evolution/.env; set +a
curl -s -X POST "http://127.0.0.1:8081/message/sendText/padelizou" \
  -H "apikey: ${AUTHENTICATION_API_KEY:-$EVOLUTION_API_KEY}" \
  -H "Content-Type: application/json" \
  -d '{"number":"5551999999999","text":"teste do Padelizou"}'
```

Troque `5551999999999` pelo seu número com o 55 na frente.

## Regras pra não perder o chip

O risco de banimento é real e não some — dá pra deixar pequeno:

1. **Nunca mandar pra quem não pediu.** O sistema já respeita isso: só vai pra quem tem conta,
   marcou "aceito WhatsApp" nas preferências e tem número válido.
2. **Ritmo.** Acima de ~500 mensagens/hora o risco sobe muito. Hoje o volume é de dezenas.
3. **Conteúdo variado.** Mensagem idêntica pra muita gente é o sinal clássico de spam. As
   nossas já variam (nome de quem chamou, horário da aula, clube do desafio) — e desde 21/08
   o que sobrou no canal é tudo de **um pra um**, disparado por gesto humano. O disparo em
   lote (chaves, cancelamento) era o formato de risco, e ele saiu.
4. **Deixe o chip "vivo".** Um número que só dispara e nunca recebe parece robô. Mandar e
   responder alguma conversa de vez em quando ajuda.

## Quando parar de funcionar

**Sintoma: ninguém recebe mais nada.**

```bash
ssh root@179.197.233.184 'docker ps'
ssh root@179.197.233.184 'docker logs --tail 50 evolution-api'
```

- **Container caiu** → `cd /opt/evolution && docker compose up -d`
- **`connectionStatus` voltou pra `close`** → o celular ficou muito tempo sem internet ou a
  sessão caiu. Refaça o passo 1 (o QR code).
- **Chip banido** → compre outro chip, apague a instância e crie de novo:
  ```bash
  ssh root@179.197.233.184 'set -a; . /opt/evolution/.env; set +a; curl -s -X DELETE http://127.0.0.1:8081/instance/delete/padelizou -H apikey:${AUTHENTICATION_API_KEY:-$EVOLUTION_API_KEY}'
  ```
  Depois recrie com `POST /instance/create` (`instanceName: padelizou`) e leia o QR de novo.
  **Nada no Padelizou precisa mudar** — o nome da instância continua o mesmo.

**Sintoma: o log do Padelizou reclama.** Procure por `Evolution API devolveu`:

```bash
ssh root@179.197.233.184 'journalctl -u padelizou --since -1h | grep -i evolution'
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
