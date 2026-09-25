# Nitro Crew — guia para agentes

Corrida arcade (estilo Top Gear) com visual 3D low-poly estilizado e co-op local de até 4 em tela dividida, em
TypeScript + Three.js (WebGL), empacotável com Electron para a Steam. **O dono pediu gráficos atuais e bonitos** —
referência Horizon Chase Turbo; pixel art e pseudo-3D de 16 bits estão fora. Interface e comentários em português (Brasil); código em inglês.
Este projeto mora numa subpasta do repositório `padelizou` por enquanto; **tudo aqui é independente dele** —
não use nada de fora desta pasta.

## Comandos
- `npm run dev` (porta 5174) · `npm run build` (typecheck + `dist/`) · `npm run preview` (porta 4174)
- `npm test` (vitest) · `npm run typecheck`
- `npm run smoke -- <pista> <humanos>` — corrida sem interface · `npm run balance -- <segundos> <dificuldade> <semente>` — IA×IA em todas as pistas
- `npm run playtest` — Chromium headless (Playwright) com capturas em `scratch/`; exige `npm run preview` em outro terminal.
  Chromium: `/opt/pw-browsers/chromium-1194/chrome-linux/chrome` neste ambiente (`--use-gl=swiftshader --enable-unsafe-swiftshader`).

## Regras do núcleo (`src/core`)
- **Determinismo obrigatório**: nada de `Math.random`, `Math.sin/cos/tan/atan2/pow/exp/log/hypot`, `Date.now`,
  `Map`/`Set` no estado. Use `state.rng` (`src/core/rng.ts`). `tests/determinism.test.ts` varre a pasta e falha se violar.
- Toda mutação de estado passa por `stepRace(state, track, inputs)`. O renderizador nunca altera o estado.
- Estado é JSON puro (`serializeRace`/`hashRace`); campo novo precisa de valor padrão em `deserializeRace`.
- Conteúdo é dado: pistas em `track/tracks.ts` (DSL: `straight/curve/hill/s/pit`), carros em `data/cars.ts`,
  copas em `data/cups.ts`. Pista nova entra sozinha nos testes de IA e de pista.
- Constantes de jogabilidade só em `constants.ts`; mudou balanceamento, rode `npm run balance` e compare voltas.

## Camadas e contratos
`src/game/contracts.ts` define as interfaces entre camadas (`Renderer`, `InputProvider`, `AudioEngine`, `Menus`,
`Settings`, `SaveData`). `src/game/session.ts` liga tudo. Textos visíveis: `registerStrings('<ns>', {pt, en})` num
`strings.ts` da própria pasta; `tests/i18n.test.ts` exige PT e EN completos.

## Regras de trabalho (as mesmas do padelizou)
- Defeito corrigido vira teste, escrito antes e visto falhar. Nada publicado com teste vermelho.
- Dependência de produção é só `three`. Sem pacote novo para o que já se faz com Three/WebAudio/DOM. Modelos, céu,
  texturas, sons e músicas são procedurais até a arte da Fase 2 (que entra como glTF, sem trocar o renderizador).
- Renderizador: mundo montado no referencial local de cada jogador a partir dos segmentos (as pistas do DSL não
  fecham geometricamente); ver `docs/DESIGN.md`. Mudança visual se prova com captura (`scratch/render-harness.mjs`).
- Commits em português, com o rodapé de atribuição exigido pela sessão.

## Memória do projeto (ler primeiro em toda sessão)
- **Roteiro e cronograma**: `docs/ROADMAP.md` (fases 0–6, passos numerados, V/A/T, marcos, custos, riscos). Documento vivo.
- **Design e arquitetura**: `docs/DESIGN.md` · **Steam**: `docs/STEAM.md` e `desktop/README.md`.
- **Estado atual**: Fase 0 concluída (25/09/2026): núcleo, IA, co-op, 12 pistas, renderizador 3D, menus,
  gamepads, áudio, Electron, 122 testes, playtest com 16 conferências. Próximo: Fase 1 (playtests do Felipe no
  sofá, sensação de direção, balanceamento, gamepads reais, desempenho com 4 viewports em GPU de verdade,
  campeonato salvo, fantasma) e a lista de polimento visual da Fase 1.10 do roteiro.
- **Como ver o jogo sem browser**: `scratch/render-harness.mjs` (Chromium headless, capturas por pista/cenário;
  `?carview=side|rear34|front34` e `?showroom=1` para os carros) e `npm run playtest` (fluxo inteiro).
- **Decisões**: TypeScript + Three.js + Electron (não Unity/Godot) para o agente construir e verificar tudo
  sozinho (o Chromium headless daqui renderiza WebGL com swiftshader); núcleo determinístico separado da
  renderização para lockstep/replays; visual low-poly estilizado procedural como base, arte final em glTF;
  "Nitro Crew" é nome provisório (Fase 2.1 decide).
- **Pendências que dependem do dono**: horas semanais, orçamento de arte e música, nome definitivo, conta Steamworks.
