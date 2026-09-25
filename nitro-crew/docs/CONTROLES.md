# Controles remapeáveis e vibração (passo 1.5, parte do agente)

Cada ação de pilotagem pode ser trocada por dispositivo; a navegação dos menus continua fixa. Os
gamepads vibram em batidas, nitro, grama e largada, com opção para desligar.

## O que é remapeável

| Ação | Teclado 1 (padrão) | Teclado 2 (padrão) | Controles (padrão, mapeamento "standard") |
|---|---|---|---|
| Acelerar | ↑ | W | A / RT (0, 7) |
| Frear | ↓ | S | X / B / LT (2, 1, 6) |
| Virar à esquerda | ← | A | D-pad ← (14) |
| Virar à direita | → | D | D-pad → (15) |
| Nitro | Espaço | F | RB (5) |
| Marcha acima | M | E | Y (3) |
| Marcha abaixo | N | Q | LB (4) |
| Pausar | Esc | Esc | Start (9) |

- O padrão é o mapeamento que existia antes (o antigo `KEY_LAYOUTS` de `src/ui/input.ts`).
- **Controles** é um mapeamento só para todos os gamepads. O analógico esquerdo sempre vira: o
  remapeamento mexe nos botões, não no eixo.
- **Fixo, fora do remapeamento**, para ninguém se trancar fora: setas, Enter, Espaço, Esc e
  Backspace (teclado 1) e W A S D, F e Esc (teclado 2) nos menus (`KEY_NAV` em `src/ui/menus.ts`,
  `NAV_LAYOUTS` em `src/ui/input.ts`); d-pad, analógico, A, B e Start nos menus do controle.
  **Esc e Start sempre pausam**, além da tecla de pausa escolhida.

## Onde mora

| Arquivo | Papel |
|---|---|
| `src/ui/remap/bindings.ts` | Puro. Tipos, `DEFAULT_BINDINGS` (congelado), `sanitizeBindings`, `assignBinding` (troca), `restoreDefaults`, `keyboardConflicts` |
| `src/ui/remap/capture.ts` | Puro. Captura da tecla/botão novo: aceitar, recusar, cancelar (Esc, Start, 5 s) |
| `src/ui/remap/labels.ts` | Nomes de teclas (com o layout do sistema via `navigator.keyboard.getLayoutMap()`), botões Xbox/PlayStation, ações e colunas |
| `src/ui/remap/strings.ts` | Textos PT/EN (namespace `remap`; mais `ui.options.vibration`) |
| `src/ui/screens/controls.ts` + `controls.css` | Tela de controles: grade ações × dispositivos, captura, avisos, dispositivos ao vivo e teste de entrada |
| `src/ui/input.ts` | `mapKeyboard`/`mapGamepad` recebem os bindings; `peek(device)` e `rumble(seat, strength, ms)` no provedor |
| `src/game/rumble.ts` | Puro. `rumbleCues(state, memory)`: eventos da corrida → pedidos de vibração |
| `src/game/settings.ts` | `controls` e `vibration` no saneamento das opções |
| `src/game/session.ts` | Gancho: `createInput(window, { bindings, vibration })` e `rumbleCues` depois de cada `stepRace` |

## Regras do mapeamento

- **Gravado em `Settings.controls`** (localStorage `nitro-crew.settings`), lido ao vivo pela
  entrada a cada quadro: trocar na tela vale no quadro seguinte, sem reiniciar nada.
- **Saneamento** (`sanitizeBindings`, nunca lança): lixo ou dispositivo ausente → padrão; código
  inválido para a ação → padrão da ação; no máximo 3 códigos por ação, sem repetição.
  Teclas aceitas: letras, números, numérico, setas, Espaço, Enter, Tab, Shift, pontuação,
  Home/End/PgUp/PgDn/Ins/Del. Recusadas: Ctrl, Alt, Meta (a entrada ignora eventos com eles),
  F1–F12 (F5 recarrega, F11 tela cheia, F12 ferramentas) e Caps Lock. Botões aceitos: 0–15
  (16 é Home/Guide, do sistema). **Esc e Start só valem na ação Pausar** (eles cancelam a captura).
- **Conflito no mesmo dispositivo** nunca deixa ação sem tecla: ao ligar um código já usado, a
  outra ação o perde; se ela ficaria vazia, recebe os códigos antigos da ação editada (a troca).
  Se nem isso servir (ex.: Esc não vai para quem não é pausa), recebe o próprio padrão ou o primeiro
  código livre da reserva. No saneamento, quem vem primeiro na ordem das ações fica com o código.
- **Mesma tecla nos dois teclados** não é bloqueada (quem joga sozinho não se importa com o
  teclado 2): a tela marca as células com "!" e explica que, com os dois em uso, a tecla comanda os dois.
- **Restaurar padrão** é por dispositivo (botão "Padrão" embaixo de cada coluna).

## Tela de controles

- Grade 8 ações × 3 colunas. Setas/d-pad andam; Enter, A ou clique abrem a captura da célula.
- Captura: a próxima tecla (colunas de teclado) ou botão (coluna Controles) vira o comando.
  Esc ou Start cancelam; sem nada em **5 s** ela desiste. O relógio anda com o `dt` da sessão,
  limitado a 0,25 s por quadro: a 60 Hz são 5 s de verdade, e um travamento não come a janela. Tecla proibida é recusada com aviso e a captura continua. Clique fora cancela.
  O botão que abriu a captura (o A segurado) só conta depois de solto; a tecla capturada e a
  repetição automática dela não viram navegação.
- Aviso de troca ("R era de Nitro, que ficou com ↑") e de conflito entre teclados.
- Nomes: botões no estilo do primeiro controle conectado (✕ ○ □ △ num PlayStation); letras pelo
  layout do sistema quando o navegador informa (AZERTY, Ç do ABNT2).
- Lateral: dispositivos ao vivo (conectado, livre, assento) e **teste de entrada** — barra de
  volante, acelerar, frear e pílulas de nitro, marchas e pausa, lidas por `input.peek()` com o
  mapeamento atual.

## Vibração

- `InputProvider.rumble(seat, strength, ms)`: `gamepad.vibrationActuator.playEffect('dual-rumble', …)`
  quando existe (Chromium/Electron). No-op em assento de teclado, assento vazio, controle sem motor,
  navegador sem Gamepad API e com a opção **Vibração** desligada (Opções → Geral). Promessa
  recusada ou exceção do `playEffect` são engolidas. Um tremor mais fraco não corta um mais forte
  que ainda está tocando.
- O que vibra (`src/game/rumble.ts`, constantes no topo):

| Evento | Força | Duração |
|---|---|---|
| Largada (`go`), todos os humanos | 0,55 | 220 ms |
| Batida entre carros (`collision`), os dois humanos | 0,3 + 0,6 × intensidade | 110 + 170 × intensidade ms |
| Batida no cenário (`crash`) | 1 | 320 ms |
| Nitro | 0,35 | 110 ms |
| Grama (fora do asfalto, andando, só na corrida) | 0,16 | 160 ms, no máximo a cada 9 ticks |

## Testes

`tests/input-remap.test.ts` (Node, sem DOM): padrão e saneamento, troca, restaurar, 600 trocas
aleatórias sem ação vazia nem código repetido, `mapKeyboard`/`mapGamepad` com bindings, navegação
de menu ignorando bindings, `preventDefault` só nas teclas em uso, provedor com janela falsa
(bindings ao vivo, `dual-rumble`, opção desligada, no-op sem gamepad, `playEffect` que falha),
`rumbleCues`, captura e nomes. Roteiro Playwright do fluxo real: `scratch/controles.mjs`
(fora do git, como o resto de `scratch/`), com 40 conferências: grade, captura por teclado,
mouse e controle falso (com motor de vibração que registra as chamadas), troca, conflito entre
teclados, recusa, Esc/Start/clique fora cancelam, tempo limite, teste de entrada, restaurar,
menus ainda nas setas, opção Vibração, corrida com o teclado remapeado, vibração na largada,
nitro e grama, vibração desligada e remapeamento que sobrevive ao recarregar.

Dica para roteiros: no swiftshader o fundo 3D dos menus custa ~1 s por quadro; o roteiro troca
`window.nc.session.renderer.renderIdle` por uma função vazia enquanto testa os menus (o último
quadro fica na tela). O renderizador já expõe `__idle` (a câmera de depuração) — não use esse nome
para guardar a função original.

## Fora deste passo

- Remapear o eixo analógico, zona morta e sensibilidade por jogador.
- Mapeamento diferente para cada gamepad (hoje é um só para todos).
- Mais de uma tecla por ação pela tela (a captura grava uma; o padrão do gamepad tem várias e o
  saneamento aceita até 3).
- Teste com gamepads reais (Xbox, DualSense, genérico) e Steam Input — a parte "V" do passo 1.5.
