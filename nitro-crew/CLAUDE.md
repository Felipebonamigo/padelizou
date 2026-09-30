# Nitro Crew — guia para agentes

Corrida arcade (estilo Top Gear) com visual 3D low-poly estilizado e co-op local de até 4 em tela dividida, em
TypeScript + Three.js (WebGL), empacotável com Electron para a Steam. **O dono pediu gráficos atuais e bonitos** —
referência Horizon Chase Turbo. O pseudo-3D de 16 bits existe só como **modo Retrô opcional** (Opções › Visual,
`src/render-pseudo3d/`, `docs/RETRO.md`); o 3D é o padrão e o foco do trabalho visual. Interface e comentários em português (Brasil); código em inglês.
Este projeto mora numa subpasta do repositório `padelizou` por enquanto; **tudo aqui é independente dele** —
não use nada de fora desta pasta.

## Comandos
- `npm run dev` (porta 5174) · `npm run build` (typecheck + `dist/`) · `npm run preview` (porta 4174)
- `npm test` (vitest) · `npm run typecheck`
- `npm run smoke -- <pista> <humanos>` — corrida sem interface · `npm run balance -- <segundos> <dificuldade> <semente>` — IA×IA em todas as pistas
- `npm run relay` — servidor do online (porta 8787). Para a suíte completa: `(cd server && npm ci)` e `NC_REQUIRE_RELAY=1 npm test`
  (sem o `ws` instalado em `server/`, os testes de integração com o relay são pulados — o CI exige).
- Playtests no Chromium headless (Playwright), com `npm run preview` (porta 4174) no ar e capturas em `scratch/`:
  `node scripts/playtest.mjs` (fluxo geral), `playtest-online.mjs` (dois computadores no mesmo relay), `playtest-controls.mjs`
  (remapeamento e vibração), `pistas-ui.mjs` (telas de copas/pistas), `playtest-tutorial.mjs` ("Como jogar" de ponta a
  ponta), `playtest-assist.mjs` (acessibilidade e direção assistida), `playtest-layout.mjs` (26 telas × 7 resoluções × texto
  normal/grande; rode depois de mexer em CSS de menu), `playtest-memoria.mjs` (sessão longa, vazamentos),
  `playtest-save.mjs` (armazenamento cheio), `playtest-retro.mjs` (modo Retrô: troca, cenários, tela dividida), `playtest-ghost-version.mjs` (fantasma de outra versão; o import pode falhar
  sob carga — ver o cabeçalho).
  `playtest-records.mjs` exige `npm run dev`. `npx tsx scripts/perf-sim.ts` mede o custo por tick da simulação.
  Chromium: `/opt/pw-browsers/chromium-1194/chrome-linux/chrome` (`--use-gl=swiftshader --enable-unsafe-swiftshader`). Sob carga a
  captura de 4 jogadores passa dos 30 s padrão: rode uma cópia com `page.setDefaultTimeout(240000)`.
- `npx tsx scripts/career-balance.ts` — calibragem da carreira (dinheiro × nível dos rivais) com corridas inteiras.
- ⚠️ Para matar um relay órfão, filtre pelo processo `node` (`ps -eo pid,comm,args`); `pkill -f relay.mjs` casa com o próprio shell.

## Regras do núcleo (`src/core`)
- **Determinismo obrigatório**: nada de `Math.random`, `Math.sin/cos/tan/atan2/pow/exp/log/hypot`, `Date.now`,
  `Map`/`Set` no estado. Use `state.rng` (`src/core/rng.ts`). `tests/determinism.test.ts` varre a pasta e falha se violar.
- Toda mutação de estado passa por `stepRace(state, track, inputs)`. O renderizador nunca altera o estado.
- Estado é JSON puro (`serializeRace`/`hashRace`); campo novo precisa de valor padrão em `deserializeRace`.
- Conteúdo é dado: pistas em `track/tracks.ts` (DSL: `straight/curve/hill/s/pit`), carros em `data/cars.ts`,
  copas em `data/cups.ts`. Pista nova entra sozinha nos testes de IA, de pista e de combustível (corrida inteira por pista).
- Desempenho do carro vem de `carStats(car)` (`sim/stats.ts`: CarDef + melhorias da carreira, guardado no estado); `carDef`
  só para nome e cor. A IA da corrida usa só o `AI_CAR_POOL` (os 7 carros livres).
- A tomada de um assento pela IA (online, jogador que caiu) é um comando de entrada (`PlayerInput.takeover`), aplicado
  dentro do `stepRace` — nada muda o estado por fora.
- Constantes de jogabilidade só em `constants.ts`; mudou balanceamento, rode `npm run balance` e compare voltas.
- `tests/sim-golden.test.ts` trava a impressão digital de 8 corridas inteiras. Otimização não pode mexer nela; mudança
  de jogabilidade de propósito muda: rode `npx tsx scripts/perf-sim.ts --fingerprints`, confira que só mudaram as
  corridas que deviam e atualize `EXPECTED`.

## Camadas e contratos
`src/game/contracts.ts` define as interfaces entre camadas (`Renderer`, `InputProvider`, `AudioEngine`, `Menus`,
`Settings`, `SaveData`). `src/game/session.ts` liga tudo. Textos visíveis: `registerStrings('<ns>', {pt, en})` num
`strings.ts` da própria pasta; `tests/i18n.test.ts` exige PT e EN completos.

## Regras de trabalho (as mesmas do padelizou)
- Defeito corrigido vira teste, escrito antes e visto falhar. Nada publicado com teste vermelho.
- Dependência de produção é só `three`. Sem pacote novo para o que já se faz com Three/WebAudio/DOM. Modelos, céu,
  texturas, sons e músicas são procedurais até a arte da Fase 2 (que entra como glTF, sem trocar o renderizador).
- Renderizador: mundo montado no referencial local de cada jogador a partir dos segmentos (as pistas do DSL não
  fecham geometricamente); ver `docs/DESIGN.md`. Mudança visual se prova com captura (`tools/render-harness.mjs`, com `npm run dev` no ar).
- Commits em português, com o rodapé de atribuição exigido pela sessão.

## Memória do projeto (ler primeiro em toda sessão)
- **Roteiro e cronograma**: `docs/ROADMAP.md` (fases 0–6, passos numerados, V/A/T, marcos, custos, riscos). Documento vivo.
- **Design e arquitetura**: `docs/DESIGN.md` · **Steam**: `docs/STEAM.md` e `desktop/README.md`.
- **Estado atual** (30/09/2026): Fase 0 e as ondas A–F das Fases 1/3/4/5 concluídas e mescladas — 32 pistas em 8 copas,
  Carreira (14 carros — 7 livres e 7 à venda —, melhorias, rivais que evoluem, campeonato salvo), controles remapeáveis e vibração, online por
  lockstep com relay (reconexão, queda do anfitrião, janela escondida), estatísticas e 25 conquistas, build Electron
  (Linux conferido), save em arquivo para o Steam Cloud, relatório de erros, textos de loja/legal/QA/imprensa.
  Onda B: rivais com personalidade e rival por copa, direção assistida e acessibilidade, modos de festa (torneio,
  escolta, revezamento), tutorial "Como jogar" e fantasma do contra-relógio. Onda C: caça a bugs em 4 lentes
  (8 defeitos corrigidos) e balanceamento por dados (escolta, agressivo, erro de frenagem). Onda D: co-op afinado
  por dados, telas em 7 resoluções + Steam Deck, simulação 40% mais barata e sessão longa sem vazamento, direção
  assistida no online, pacote Windows gerado no Linux. Onda E: ajustes do anfitrião online que o eco da sala
  desfazia, save com o armazenamento cheio (vai ao arquivo e avisa), fantasma e recordes com a versão do conteúdo.
  Onda F (visual, a pedido do dono): 6 carros novos (14), um modelo 3D por estilo de carroceria (13), pista/céu/luz
  por bioma × período, cenário refeito por bioma e país com pegada = colisão, colisão carro-carro do tamanho do
  carro na tela; e o modo Retrô (pseudo-3D) opcional em Opções › Visual. 847 testes. Documentos por área:
  `docs/PISTAS.md`, `CARREIRA.md`, `CONTROLES.md`, `ONLINE.md`, `ESTATISTICAS.md`, `RIVAIS.md`, `ASSISTENCIAS.md`,
  `MODOS.md`, `TUTORIAL.md`, `FANTASMA.md`, `TELAS.md`, `RETRO.md`, `CARROS.md`, `FISICA.md`, `VISUAL.md`, `DESEMPENHO.md`, `SAVE.md`, `LOJA.md`, `QA.md`, `IMPRENSA.md`, `legal/`. Próximo: jogar com
  gente de verdade e trazer a lista de problemas; na Fase 2, a direção de arte (2.1) e a arte final em glTF (2.2–2.4).
- **Como ver o jogo sem browser**: `tools/render-harness.mjs` (Chromium headless, capturas por pista/cenário;
  `?carview=side|rear34|front34` e `?showroom=1` para os carros) e `npm run playtest` (fluxo inteiro).
- **Decisões**: TypeScript + Three.js + Electron (não Unity/Godot) para o agente construir e verificar tudo
  sozinho (o Chromium headless daqui renderiza WebGL com swiftshader); núcleo determinístico separado da
  renderização para lockstep/replays; visual low-poly estilizado procedural como base, arte final em glTF;
  "Nitro Crew" é nome provisório (Fase 2.1 decide).
- **Pendências que dependem do dono**: horas semanais, orçamento de arte e música, nome definitivo, conta Steamworks,
  direção assistida completa (hoje vence a IA profissional em 70% das corridas e conta para recordes: teto de
  velocidade medido em `docs/ASSISTENCIAS.md`, ou tirar dos recordes), ordem das pistas nas copas (Great Ocean e
  Amalfi jogam como nível 5; `docs/PISTAS.md`),
  licença do código (o `package.json` diz MIT e o repositório é público, mas a venda usa a EULA comercial), nomes de carro
  que lembram modelos reais (Falcão GT, Tornado), revisão jurídica dos textos em `docs/legal/`, se o executável Linux
  (Deck) passa a usar `--no-sandbox` (o `chrome-sandbox` sai sem SUID pela Steam; `desktop/README.md`).
