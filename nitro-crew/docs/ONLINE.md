# Online — lockstep determinístico via relay (passos 4.2 e 4.3)

Até 4 humanos em até 4 computadores, cada computador com 1 ou 2 jogadores locais (tela dividida +
online), mais a IA completando o grid. A simulação inteira roda em **todos** os computadores; pela
rede só passam as entradas de cada tick, um hash do estado a cada segundo e, para quem reconecta,
um snapshot. O servidor é um relay burro (`server/relay.mjs`) que não entende o jogo.

```
 computador A (anfitrião)          relay (VPS)             computador B (2 jogadores)
 ┌──────────────────────┐        ┌────────────┐         ┌──────────────────────┐
 │ teclado → assento 0  │──i────▶│ sala KQXTR │──i─────▶│ assentos 1 e 2       │
 │ Lockstep → stepRace  │◀───i───│ repassa    │◀────i───│ Lockstep → stepRace  │
 │ hash a cada 60 ticks │──h────▶│ guarda o   │──h─────▶│ compara os hashes    │
 └──────────────────────┘        │ lugar de   │         └──────────────────────┘
                                 │ quem caiu  │
                                 └────────────┘
```

## Onde está cada coisa

| Arquivo | Papel |
|---|---|
| `src/net/protocol.ts` | Tipos das mensagens, entrada compacta, **validação de tudo que chega** (`parseServerMessage`) |
| `src/net/lockstep.ts` | Lockstep puro: buffer por tick, atraso de entrada, espera, reenvio, hash, tomada pela IA |
| `src/net/client.ts` | `NetClient`: WebSocket do navegador/Node 22 com validação na chegada |
| `src/net/strings.ts` | Textos PT/EN da tela e dos avisos (e o item "Online" do menu principal) |
| `src/game/online-session.ts` | `OnlineController`: sala, lobby em rede, largada, reconexão, snapshot, ping |
| `src/game/session.ts` | Só o gancho: `RaceDriver` (a fonte das entradas por tick é plugável) |
| `src/ui/screens/online.ts` + `online.css` | Tela Online (conectar, lobby, sair da partida, resultado, erro) e o aviso sobre a corrida |
| `server/relay.mjs` | Relay WebSocket (`ws`), salas, limites, heartbeat, janela de reconexão |

## Como funciona

**Entrada por tick.** Cada registro é `[tick, assento, bits, volante]`: bits de acelerar (1),
frear (2), nitro (4), subir marcha (8), descer marcha (16) e o volante quantizado em int8
(−127..127). Todas as máquinas simulam a entrada **decodificada** — inclusive a que a gerou —, então
a quantização não quebra o determinismo. Nitro e marchas são bordas: se o jogador aperta enquanto a
rede está travada, a borda fica guardada e sai no próximo envio.

**Atraso de entrada.** O que o jogador aperta agora vale para o tick `atual + atraso` (padrão 3
ticks = 50 ms; o anfitrião escolhe de 1 a 12 no lobby). O tick N só roda quando há entrada de
**todos** os assentos humanos para N; até lá o computador espera (a tela continua desenhando). As
entradas chegam fora de ordem, repetidas ou adiantadas: o buffer por tick absorve; repetida é
ignorada, atrasada é descartada, conflitante fica com a primeira, assento alheio é recusado. Quem
ficou atrás dos outros roda até 4 ticks extras por quadro para alcançar.

**Pacote perdido.** O relay descarta o que passa do limite de taxa. Para isso não travar a corrida
para sempre, quem está parado há 500 ms reenvia as próprias entradas recentes (duplicata é
inofensiva). Numa trava todo mundo está parado, então todo mundo reenvia e as lacunas se fecham.

**Dessincronia.** A cada 60 ticks (1 s) cada computador manda o `hashRace` do estado. Hash
diferente → relatório `{tick, local, remoto, de quem}` no console e aviso vermelho na tela
("Dessincronia no tick N"). A corrida continua: o aviso existe para o bug ser reportado, não para
esconder.

**Assentos.** Na largada o anfitrião numera os assentos globais 0..3 pela ordem dos computadores na
sala e dos jogadores de cada um; o número define a cor e a posição no grid. Cada computador liga os
próprios controles aos assentos que recebeu e **só desenha os viewports deles** (1 ou 2). Recordes e
conquistas são só de quem jogou naquele computador.

**Reconexão (4.3).** Quem cai no meio da corrida tem o lugar guardado pelo relay por 60 s. Os outros
param no primeiro tick sem a entrada dele e veem "Aguardando Fulano reconectar…". O jogo de quem
caiu tenta voltar a cada segundo com o mesmo token; quando volta, o anfitrião manda um **snapshot**
(`serializeRace` + tick + entradas já conhecidas + tomadas decididas) e ele segue dali, com o mesmo
hash dos outros. O token vive na memória: recarregar a página perde a vaga.

**Quem não volta vira IA.** Passada a janela (ou se a pessoa escolhe "Sair da partida"), o
anfitrião põe um registro `TAKEOVER` no próprio fluxo de entradas, com o tick em que passa a valer
(o primeiro sem entrada daquele assento). Todas as máquinas aplicam no mesmo tick, antes do
`stepRace`: o carro ganha um cérebro de IA sem sorteio nenhum, então continua determinístico.

**Anfitrião.** É o computador conectado de menor id na sala. Se ele sai ou cai, o relay passa a sala
adiante; o novo anfitrião assume as tarefas (tomada pela IA, snapshot para quem volta, largar a
próxima corrida).

**Sem pausa.** Online, Esc/Start abre "Sair da partida?" e a corrida continua por baixo (as
entradas deste computador ficam neutras enquanto o aviso está aberto). No canto fica o ping até o
relay e o atraso de entrada; no meio, os avisos de espera, reconexão e dessincronia.

**Nada que vem da rede é confiável.** O cliente valida forma e conteúdo de cada mensagem
(`parseServerMessage`: inteiros nas faixas, pista e carros existentes, no máximo 2 assentos por
computador, snapshot com estado em texto) e descarta — contando — o que não vale. A sessão ainda
confere o remetente: entrada de um assento só vale do dono; largada, snapshot e tomada, só do
anfitrião. O relay, por sua vez, confere a forma e os tamanhos antes de repassar.

## Rodar localmente

```bash
cd nitro-crew/server && npm ci && cd ..
npm run relay                       # terminal 1: ws://localhost:8787
npm run dev                         # terminal 2: abra duas janelas (uma anônima) em localhost:5174
```

Menu → Online → **Criar sala** numa janela; na outra, digite o código e **Entrar na sala**. O
convidado escolhe nome e carro e aperta **PRONTO**; o anfitrião escolhe pista, voltas, modo,
dificuldade, carros e atraso e aperta **LARGAR**. Segundo jogador no mesmo computador: no lobby,
aperte F (teclado WASD) ou A num controle.

## Testes

- `tests/net-protocol.test.ts` — entrada compacta, validação de cada mensagem, códigos, endereço.
- `tests/net-client.test.ts` — `NetClient` e sessão contra um socket falso: mensagem fora do formato
  descartada e contada; largada, entrada de assento alheio, tomada pela IA e snapshot de quem não
  pode mandá-los descartados e contados.
- `tests/net-session.test.ts` — numeração dos assentos (nome padrão acompanha o assento), corrida
  montada da largada igual em todas as máquinas, endereço do servidor nas opções.
- `tests/net-lockstep.test.ts` — 2, 3 e 4 clientes em memória com atraso, reordenação e duplicação
  sorteados (semente fixa) chegam ao mesmo `hashRace` após 3000 ticks; entrada faltando não
  avança; pacote perdido não trava; dessincronia apontada no tick certo; reconexão por snapshot com
  o mesmo hash; IA assumindo no mesmo tick em todas as máquinas.
- `tests/net-relay.test.ts` — sobe o `server/relay.mjs` de verdade numa porta livre: regras do relay
  com mensagens cruas (sala cheia, versão, taxa, 64 KB, janela de reconexão) e duas sessões online
  completas pelo WebSocket global do Node 22 (lobby, largada, 600 ticks com hashes iguais, queda e
  volta por snapshot, IA assumindo). Leva ~10 s. **Sem `server/node_modules` o arquivo é pulado**;
  no CI ele é obrigatório (`NC_REQUIRE_RELAY=1`).
- `scripts/playtest-online.mjs` — Playwright com duas páginas no mesmo relay, pelo fluxo real de
  teclado: criar/entrar, pronto, largar, 10 s de corrida com `debugStep`, hashes iguais, Esc sem
  pausa, "aguardando", queda e volta, anfitrião saindo e a IA assumindo. Capturas em
  `scratch/pto-*.png`. Uso: `npm run preview` e `node scripts/playtest-online.mjs http://localhost:4174/`.
  As páginas ficam em 320×180 fora das capturas: com o 3D por software um quadro grande leva
  segundos, a rede só é lida entre quadros e o lockstep anda só "atraso" ticks por ida e volta.

## Hospedar o relay num VPS

O relay é um processo Node sem estado em disco (salas só na memória). Um VPS de 1 vCPU/1 GB aguenta
centenas de salas: cada corrida manda ~62 mensagens pequenas por segundo por computador.

1. **Node 22** e o código: `git clone …` e `cd nitro-crew/server && npm ci --omit=dev`.
2. **Serviço** (`/etc/systemd/system/nitro-relay.service`):

   ```ini
   [Unit]
   Description=Nitro Crew relay
   After=network.target

   [Service]
   WorkingDirectory=/opt/nitro-crew
   ExecStart=/usr/bin/node server/relay.mjs
   Environment=PORT=8787 HOST=127.0.0.1
   Restart=always
   User=nitro
   NoNewPrivileges=true

   [Install]
   WantedBy=multi-user.target
   ```

   `systemctl enable --now nitro-relay` · log: `journalctl -u nitro-relay -f`.
3. **TLS (`wss://`)**: o jogo empacotado roda em `file://`/Electron e aceita `ws://`, mas na
   internet use TLS. Com Caddy (certificado automático):

   ```
   relay.exemplo.com.br {
       reverse_proxy 127.0.0.1:8787
   }
   ```

   Com nginx, `proxy_pass http://127.0.0.1:8787;` mais `proxy_http_version 1.1;`,
   `proxy_set_header Upgrade $http_upgrade;`, `proxy_set_header Connection "upgrade";` e
   `proxy_read_timeout 120s;`. No jogo: `wss://relay.exemplo.com.br`.
4. **Firewall**: abrir só 443 (e 80 para o certificado). Sem proxy, abrir a porta do relay
   (`ufw allow 8787/tcp`) e usar `ws://IP:8787` — aceitável para testes entre amigos.
5. **Monitor**: `curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:8787/` deve dar `426`.

**Portas**: 8787 (padrão, `PORT`), atrás do proxy 443. **Limites**: 4 computadores e 4 humanos por
sala, 64 KB por mensagem, 150 mensagens/s por conexão (rajada de 300), 1000 salas, janela de
reconexão de 60 s — todos em `server/README.md`, a maioria por variável de ambiente.

**Privacidade (LGPD)**: o relay não grava nada em disco; o log por sala tem só código, horário e ids
numéricos (sem nome nem IP). Os nomes dos jogadores passam pela memória do relay enquanto a sala
existe. Entra na política de privacidade do passo 5.7 do roteiro.

## Limitações conhecidas

- **Latência sentida = atraso de entrada** (50 ms no padrão) mais a espera pelo computador mais
  lento. Não há previsão/rollback: com ping alto, suba o atraso no lobby em vez de ver travadas.
- **Um relay só**, sem escolha de região nem lista de salas públicas (entra-se pelo código).
- **A tomada pela IA usa o que o anfitrião sabe**: ela começa no primeiro tick sem entrada do
  assento que o anfitrião conhece. Como o relay entrega as mensagens na mesma ordem para todos e a
  decisão só acontece depois da janela de 60 s, os outros já receberam tudo o que o anfitrião
  recebeu.
- **Snapshot só do anfitrião**: se o anfitrião e quem volta caírem juntos, o novo anfitrião é quem
  manda.
- **Recarregar a página perde a vaga** (o token fica só na memória).
- **Queda no lobby não tem volta**: o relay só guarda o lugar durante a corrida; no lobby quem cai
  sai da sala (e vê "A conexão com o servidor caiu"). Quem fica 10–20 s sem responder ao ping de
  protocolo do relay é derrubado (`RELAY_HEARTBEAT_MS`).
- **Sem pausa online** por desenho; "Sair da partida" põe a IA no carro.

## Próximos passos

- **Steam Networking Sockets** (passo 4.4) via `steamworks.js`: troca o transporte (`NetClient`)
  pelo P2P da Steam com relay da Valve (SDR), sem servidor próprio e sem IP exposto; convites e
  lobby da Steam no lugar do código de 5 letras. O `Lockstep` e o protocolo ficam iguais — só o
  transporte muda.
- **Remote Play Together** (passo 4.1) continua sendo o caminho de co-op online mais simples: um
  computador roda o jogo local e a Steam transmite o vídeo; não usa nada deste módulo.
- Previsão de entrada/rollback para esconder o atraso com ping alto; escolha de região; relatório
  de dessincronia salvo em arquivo (estado serializado dos dois lados) para depuração.
