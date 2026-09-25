# Relay do Nitro Crew

Servidor de retransmissão (WebSocket) do online em lockstep. **Não simula a corrida e não entende
o jogo**: guarda salas por código, sabe quem é o anfitrião, repassa as mensagens entre os
computadores da sala e segura o lugar de quem caiu por um tempo. Toda a simulação roda em cada
cliente (`src/core`, determinístico); o relay só leva entradas, hashes e o snapshot de reconexão.
Arquitetura completa em [`docs/ONLINE.md`](../docs/ONLINE.md).

## Rodar

```bash
cd nitro-crew/server && npm ci      # uma vez (dependência única: ws)
cd .. && npm run relay              # = node server/relay.mjs, porta 8787
PORT=9000 npm run relay             # outra porta
PORT=0 node server/relay.mjs        # porta livre (a linha "ouvindo em" diz qual)
```

No jogo: menu principal → **Online** → campo **Servidor** (padrão `ws://localhost:8787`, fica
salvo nas opções). Na mesma rede local, use o IP da máquina do relay: `ws://192.168.0.10:8787`.

## Variáveis de ambiente

| Variável | Padrão | O que faz |
|---|---|---|
| `PORT` | `8787` | Porta TCP (`0` = livre) |
| `HOST` | `0.0.0.0` | Interface; use `127.0.0.1` atrás de um proxy reverso |
| `RELAY_RECONNECT_MS` | `60000` | Janela para quem caiu no meio da corrida voltar com o token |
| `RELAY_HEARTBEAT_MS` | `10000` | Ping de protocolo; quem não responde a um é derrubado no seguinte |
| `RELAY_MAX_ROOMS` | `1000` | Salas simultâneas (acima disso: erro `rooms`) |
| `RELAY_RATE` | `150` | Mensagens por segundo por conexão (sustentado); a corrida usa ~62/s |
| `RELAY_BURST` | `300` | Rajada tolerada acima da taxa |
| `RELAY_QUIET` | — | `1` desliga o log por sala (criação, largada, queda) |

## Limites fixos (em `DEFAULTS`, no topo de `relay.mjs`)

- **4 computadores e 4 humanos por sala**, no máximo 2 jogadores locais por computador.
- **64 KB por mensagem** (o snapshot de reconexão é a maior: ~13 KB com 20 carros). Acima disso o
  `ws` fecha a conexão com o código 1009.
- **4 KB** para os blocos que o relay guarda sem entender (info do cliente, opções da sala,
  configuração da largada) e **64 registros** de entrada por mensagem.
- Acima da taxa: a mensagem é descartada e o cliente recebe `{t:'error', code:'rate'}` (no máximo
  um aviso por segundo); **600 descartes** derrubam a conexão (código 1008). O lockstep tolera o
  descarte: quem fica parado reenvia as entradas recentes.
- Sala sem ninguém conectado fora de corrida é apagada na hora; na corrida, quando o último lugar
  guardado expira.

## Protocolo (resumo)

JSON por mensagem, campo `t` com o tipo. Versão `PROTOCOL_VERSION = 1` (a mesma de
`src/net/protocol.ts`); cliente de outra versão recebe `error: version`.

| Cliente → relay | Relay → clientes |
|---|---|
| `create {v, b, seats, info}` | `welcome {room, id, token, rejoined}` só para quem entrou |
| `join {v, b, room, seats, info}` | `room {room}` — a sala inteira, a cada mudança |
| `rejoin {v, room, token}` | `peer {id, e}` — `join`, `rejoin`, `lost` (caiu), `drop` (não voltou), `leave` |
| `info {seats, info}` · `settings {settings}` (anfitrião) | `start {from, cfg}` — para todos, inclusive o anfitrião |
| `start {cfg}` · `lobby` (anfitrião) | `i {from, d}` · `h {from, k, h}` — repassados aos outros |
| `i {d}` · `h {k, h}` | `snap {from, snap}` — só para o destinatário |
| `snap {to, snap}` (anfitrião) · `ping {n}` · `leave` | `pong {n}` · `error {code}` |

`b` é a impressão do conteúdo do jogo (8 hex: carros, pistas, constantes da simulação — ver
`CONTENT_FINGERPRINT` em `src/game/online-session.ts`). A sala guarda a de quem a criou e recusa
`join` com outra (ou sem nenhuma) com `error: build`: dois builds diferentes não correm juntos.

O anfitrião é quem criou a sala; se ele cai ou sai, o relay passa a sala ao cliente **conectado há
mais tempo** e avisa com um `room` novo. No meio da corrida isso escolhe alguém que não caiu (o
estado dele é o completo), e não quem acabou de voltar e espera um snapshot. O relay confere a **forma** (inteiros, tamanhos, quem é o
anfitrião); o **conteúdo** (pista e carros existentes, dono de cada assento) é validado em cada
cliente por `parseServerMessage` e pela sessão online.

## Saúde

Um `GET` HTTP comum na porta responde `426 Upgrade Required` (é o próprio `ws`): serve de sonda de
vida para um monitor. `SIGINT`/`SIGTERM` fecham as conexões e encerram o processo.

Hospedagem num VPS (systemd, TLS com `wss://`, firewall): ver [`docs/ONLINE.md`](../docs/ONLINE.md#hospedar-o-relay-num-vps).
