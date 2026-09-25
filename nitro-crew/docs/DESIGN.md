# Nitro Crew — Design e arquitetura

## O jogo em uma frase
Corrida arcade no espírito dos clássicos de 16 bits (Top Gear), com visual 3D atual (low-poly estilizado, à
Horizon Chase Turbo), em que **até 4 pessoas no mesmo sofá correm como uma equipe** contra 16–19 pilotos de IA,
com nitro, box e combustível — e mecânicas que só existem porque há uma equipe.

## Pilares
1. **Sofá primeiro.** Tela dividida para 1–4, entra-se com um botão, tudo navegável por controle. Online vem
   depois (Remote Play Together, depois lockstep).
2. **Arcade honesto.** Física simples e legível: acelera, freia, vira, nitro. Curva forte exige freio; grama
   pune; bater dói. Nada de física de simulador.
3. **Equipe de verdade.** Cooperar tem que valer mais que correr sozinho: cofre de nitro, empurrão, vácuo de
   equipe, elástico para quem ficou para trás, e a pontuação de equipe nas copas.
4. **Bonito de verdade, desde já.** Visual 3D com luz, sombra, névoa, bloom e partículas; modelos, céu, texturas,
   sons e músicas são procedurais até a arte final chegar (em glTF), e o jogo nunca fica esperando asset.

## Modos
- **Campeonato** (co-op ou versus): copas de 3 pistas por país. Solo/versus: seguir exige top 5 na corrida.
  Co-op (2+ humanos no mesmo time): a equipe pontua com os dois melhores resultados e precisa ficar entre as 3
  melhores equipes (a IA corre em duplas). Copas destravam em sequência.
- **Corrida rápida**: qualquer pista, 2–8 voltas, 8–20 carros.
- **Contra-relógio**: 1 jogador, sem combustível, recordes por pista.

## Mecânicas
| Mecânica | Regra | Onde |
|---|---|---|
| Velocidade | 6000 u/s = 300 km/h; 4 carros trocam velocidade × aceleração × curva × consumo | `data/cars.ts`, `sim/physics.ts` |
| Curva | empurrão para fora ∝ velocidade² × curva; o volante compensa até um limite → `holdableSpeedFraction` | `sim/physics.ts` |
| Grama | acima de |x| 1,05: velocidade cai a 35% e sprites sólidos derrubam a 25% | `sim/physics.ts`, `sim/collisions.ts` |
| Nitro | 3 por corrida, 2,5 s a +28% e aceleração ×2,2; no co-op, cofre da equipe (3 × jogadores) | `constants.ts` |
| Câmbio | automático, ou manual com 5 marchas (marcha baixa acelera mais, limita a velocidade) | `constants.ts` (GEAR_*) |
| Combustível | tanque dura ~2,4 voltas de 400.000 u em aceleração total; aliviar o pé economiza; box na reta de largada (x 1,3–1,9) a 25% da velocidade reabastece a 35%/s | `sim/physics.ts` |
| Colisão | por trás: quem bate cai a 90% da velocidade do outro (mínimo 10%), quem é batido ganha um pouco; lado a lado: −3% e empurrão lateral | `sim/collisions.ts` |
| Empurrão | companheiro a menos de 20% da velocidade recebe 80% da velocidade de quem passa a 2 segmentos e 0,7 de lateral | `sim/coop.ts` |
| Vácuo | atrás de qualquer carro (6 segmentos, 0,3 lateral): aceleração ×1,35 e +3% de máxima; de companheiro: +6% | `sim/coop.ts` |
| Elástico | último humano da equipe a 60+ segmentos de todos os companheiros: +5% de máxima | `sim/coop.ts` |
| Chegada | terminou o último humano ou 45 s depois do primeiro; quem não terminou é classificado por progresso | `sim/positions.ts` |
| Pontos | 20-15-12-10-8-6-4-3-2-1 | `constants.ts` |

## IA (`sim/ai.ts`)
Cada piloto tem habilidade (por dificuldade), faixa preferida, visão à frente e agressividade. A cada tick:
ponto de frenagem para cada curva à frente (velocidade permitida = √(limite² + 2·freio·distância)), faixa por
dentro da curva, desvio de quem está na frente (troca de faixa a cada 45 ticks), carro quase parado é obstáculo
(mantém 35% para conseguir esterçar), nitro em reta livre, box abaixo de 22% de tanque, elástico em relação ao
melhor humano. Dificuldade muda a velocidade de reta (86/94/100%) e a faixa de habilidade.

## Arquitetura
```
src/core      simulação determinística, sem DOM, sem Math.random/sin/cos/Date (teste garante)
  track/      DSL de pista → segmentos + cenário (builder.ts, tracks.ts)
  data/       carros, pilotos/equipes da IA, copas
  sim/        physics, ai, collisions, coop, positions, race (createRace/stepRace)
  championship.ts, serialize.ts (estado é JSON puro; hashRace para lockstep)
src/render    Three.js: roadframe (referencial local), road, terrain, scenery, cars, sky, effects, camera, hud (DOM), palette, minimap, layout
src/ui        menus (DOM), input (teclado + Gamepad API), styles.css
src/audio     WebAudio: synth, engine (motor por jogador), sfx, music (jukebox por sequenciador)
src/game      contracts.ts (interfaces), session.ts (laço), settings/save (localStorage), desktop.ts (Electron), achievements.ts
src/i18n      registerStrings por namespace, t(), PT-BR/EN
desktop/      Electron + steamworks.js
scripts/      smoke.ts (corrida sem interface), balance.ts (IA×IA por pista), playtest.mjs (Chromium), diag.ts
tests/        vitest
```
Regras que mantêm o jogo pronto para multiplayer: toda mutação passa por `stepRace(state, track, inputs)`; o
renderizador nunca altera o estado; o estado é serializável e `hashRace` detecta dessincronia; sementes controlam
o elenco (`rosterSeed`) e o acaso da IA (`seed`).

## Renderização 3D (`src/render`)
A simulação é "1D + lateral" (z ao longo da pista, x entre as bordas), como no pseudo-3D — e as pistas do DSL não
fecham geometricamente. Por isso o mundo 3D é montado **no referencial local de cada jogador**, a cada quadro:
origem na linha central em `z = car.z`, integrando a curvatura para ~30 segmentos atrás e 140–260 à frente
(`roadframe.ts`, puro e testado). Com uma câmera de perseguição alinhada à pista, o resultado é indistinguível de
um mundo fixo; o que fica ao longe (céu, sol, cordilheira, skyline) gira pelo heading absoluto do carro.
Escalas: x = ±1 → ±7 m; segmento = 4 m (300 km/h do velocímetro ≈ 430 km/h visuais, exagero arcade); elevação × 0,0025
(com 0,006 as rampas passavam de 40% e a câmera empinava).
Malha da pista, terreno por bioma, cenário instanciado a partir dos `sprites` dos segmentos, 20 carros low-poly
re-posicionados por viewport, céu procedural com PMREM para reflexos, sombras direcionais, bloom só nos
emissivos, partículas. Tela dividida por scissor: 1 = cheia, 2 = em cima/embaixo, 3–4 = 2×2 (com 3, a 4ª célula é
classificação + minimapa). HUD em DOM por cima do canvas.

## Decisões
- **TypeScript + Three.js + Electron**, não Unity/Godot: o agente constrói e verifica tudo sozinho (testes,
  corrida sem interface, capturas no Chromium headless com WebGL), e o mesmo caminho do AgeOfEarth leva à Steam.
- **Three.js, não Canvas 2D**: o primeiro renderizador era pseudo-3D em Canvas 2D (estilo 16 bits); foi trocado em
  25/09/2026 a pedido do dono ("gráficos atuais, bonitos"). A simulação não mudou uma linha.
- **Sem trigonometria no núcleo**: suavizações polinomiais; o minimapa (que precisa de seno/cosseno) vive no
  renderizador, fora do estado.
- **Humanos largam por último** (como no Top Gear) e a IA corre em duplas com nome de equipe, para a classificação
  por equipes fazer sentido.
- **Nome provisório** "Nitro Crew"; a decisão de nome/marca é da Fase 2.1 do roteiro.
