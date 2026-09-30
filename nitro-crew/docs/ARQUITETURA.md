# Nitro Crew — arquitetura técnica (resumo para quem vai escrever um renderizador)

Jogo em **TypeScript + Three.js (WebGL)**, empacotado com Electron. Código em `nitro-crew/src/`. Três camadas:

```
src/core   → simulação pura e determinística (estado JSON, sem DOM, sem Three)
src/game   → sessão: game loop, entrada, menus, som, save; liga as camadas (session.ts)
src/render → desenho (Three.js 3D + HUD em DOM) — só LÊ o estado; implementa a interface Renderer
```

⚠️ O renderizador atual **não é top-down nem de retângulos**: é 3D de verdade (Three.js), câmera de perseguição atrás
do carro, tela dividida 1–4. Mas o **modelo de dados do núcleo é exatamente o do pseudo-3D clássico** (pista 1D de
segmentos com curva e altura; carro em z ao longo da pista + x lateral) — o projeto nasceu pseudo-3D. Um renderizador
pseudo-3D é só outra implementação da interface `Renderer` (seção 3.4); nada no núcleo precisa mudar.

---

## 1. Estado do jogo

### 1.1 Unidades e convenções (`src/core/constants.ts`)
| Grandeza | Unidade | Valores de referência |
|---|---|---|
| `z` (ao longo da pista) | unidades de mundo; volta em `track.length` | `SEGMENT_LENGTH = 200`; `track.length = segments.length × 200` (voltas de ~360.000–420.000) |
| `x` (lateral) | normalizado: 0 = centro, ±1 = bordas do asfalto | grama além de `OFFROAD_X = 1.05`; faixa do box 1,25–1,95 (centro `PIT_X = 1.55`) nos segmentos com `pit`; limitado a ±3,2 |
| `y` (altura) | unidades de mundo, **por segmento** (`y0` no início, `y1` no fim) | o carro não guarda y: interpola-se o do segmento em z |
| velocidade | unidades/s | `REFERENCE_SPEED = 6000` u/s = 300 km/h (`SPEED_TO_KMH = 300/6000`) |
| aceleração/freio | unidades/s² | `accel` 640–900, `brake` 2400–3000 |
| tempo | ticks fixos | `TICK_RATE = 60`, `DT = 1/60`; contagem `COUNTDOWN_TICKS = 210` |
| curva | adimensional por segmento | típico 1,5–6 (sinal: − esquerda, + direita) |

Pegada de colisão hoje: `CAR_LENGTH = 120` u (z), `CAR_HALF_WIDTH = 0.22` (x) — **em revisão** (não bate com o modelo
visual de ~4,4 × 1,9 m; será corrigida para a pegada visual).

### 1.2 Tipos (`src/core/types.ts`)
```ts
RaceState {
  tick: number; phase: 'countdown' | 'racing' | 'finished';
  config: RaceConfig; trackId: string; trackLength: number;
  cars: CarState[];              // ids = índice; humanos no fim do grid
  rng: RngState;                 // único gerador aleatório permitido
  teamNitro: Record<teamId, number>; startTick; firstHumanFinishTick;
  events: SimEvent[];            // eventos do ÚLTIMO tick (lap, nitro, collision, crash, tow, pit_enter, finish…)
  results: RaceResultRow[] | null; party?: PartyState;
}
CarState {
  id; seat /* -1 = IA, 0..3 = jogador */; name; teamId; carId;
  z; x; speed;                   // posição e velocidade escalar (não há vetor de velocidade)
  gear; fuel /* 0..1 */; nitroLeft; nitroTicks /* >0 = nitro ativo */;
  lap; lapTicks[]; lapStartTick; finished; finishTick; position /* 1..N */; progress /* distância total */;
  inPit; collisionCooldown; towCooldown; skidTicks /* >0 = na grama */;
  steerPose /* -1|0|1: pose do volante, para o desenho inclinar */;
  ai: AiBrain | null; stats: CarStats /* topSpeed, accel, brake, handling 0..1, fuelPerUnit, nitro */;
}
PlayerInput { steer: -1..1; throttle; brake; nitro /* borda */; gearUp; gearDown; takeover? }
RaceConfig { trackId; laps; humans: HumanEntry[]; totalCars; difficulty; manualGear; assists; seed; timeTrial?; mode?; … }
CarDef (data/cars.ts) { id; name; color; body /* estilo de carroceria */; accent?; topSpeed; accel; brake; handling; fuelPerUnit; price; blurb }
```
Regras: o estado é JSON puro (serializável, `hashRace` para o online em lockstep); nada de `Math.random`,
`Math.sin/cos/pow…`, `Date`, `Map/Set` no núcleo; **toda mutação passa por `stepRace`**.

### 1.3 A pista em memória
```ts
Track   { def: TrackDef; segments: Segment[]; length: number; startIndex: 0 }
Segment { index; z /* = index×200 */; curve; y0; y1; band: 0|1 /* alterna a cada 3 segmentos (zebras) */;
          pit: boolean; sprites: SpriteRef[] }
SpriteRef { kind: 'tree'|'pine'|'palm'|'cactus'|'bush'|'boulder'|'building'|'tower'|'lamp'|'billboard'|
                  'sign_left'|'sign_right'|'grandstand'|'banner_start'|'pit_wall'|'pit_sign'|'cone';
            x /* em meias-larguras de pista; ±1 = borda do asfalto; sprites ficam em |x| ≥ 1,25 */;
            scale; solid /* bater derruba a velocidade */; variant }
TrackDef { id; name; country; scenery /* bioma */; timeOfDay: 'day'|'dusk'|'night'; laps; difficulty; ops: TrackOp[] }
```

---

## 2. Game loop

```
main.ts → createSession(canvas, hudRoot, uiRoot) → requestAnimationFrame(loop) → session.frame(now)

frame(now):
  input.poll()                                   // teclado/gamepad (src/ui/input.ts)
  dt = (now − last), limitado; accumulator += dt
  while accumulator ≥ DT e passos < 4:           // MAX_STEPS_PER_FRAME = 4
      stepOnce(race, readInputs(race)); accumulator −= DT
  renderer.render(RenderFrame)                   // desenha o estado do último tick (sem interpolação)
  (sem corrida: renderer.renderIdle(time, track) — fundo animado dos menus)

readInputs(race): para cada assento local → input.readSeat(seat): PlayerInput
   teclado 1 = setas + Espaço (nitro) + M/N (marcha); teclado 2 = WASD + F + E/Q; gamepad; tudo remapeável
   (src/ui/remap/bindings.ts). Esquerda/direita viram steer −1/+1 (analógico no gamepad).

stepOnce(race, inputs): stepRace(state, track, inputs) → eventos → HUD/som/estatísticas/fantasma
   (online: um "driver" de lockstep chama stepOnce só com os inputs confirmados de todos os computadores)
```

### 2.1 `stepRace(state, track, inputs)` — `src/core/sim/race.ts`, um tick
```
state.events = []
contagem → no fim, phase = 'racing'
para cada carro:
   input = car.ai ? aiInput(state, track, car)                  // IA (sim/ai.ts)
         : car.finished ? cruiseInput(state, track, car)        // humano que já cruzou a chegada: piloto automático
         : assistInput(state, track, car, inputs[car.seat] ?? NEUTRAL_INPUT, nível)  // humano (+ direção assistida, sim/assist.ts)
   mods  = computeModifiers(state, track, car)                  // vácuo, vácuo de equipe, elástico (sim/coop.ts)
   stepCarPhysics(state, track, car, input, mods)               // sim/physics.ts
   resolveSpriteCrash(state, track, car)                        // bater em árvore/placa (sim/collisions.ts)
   updateLaps(state, track, car, prevZ)                         // sim/positions.ts
resolveCarCollisions(state, track); applyTow(state, track)      // carro×carro; empurrão entre companheiros
updatePositions(state, track); updateModes(state, track)
checkRaceOver(state) → phase = 'finished', results = buildResults(state)
state.tick++
```

### 2.2 `stepCarPhysics(state, track, car, input, mods)` — a física de movimento
```
seg = segmentAt(track, car.z); def = car.stats
marcha (automática pela fração de velocidade, ou manual com gearUp/gearDown)
nitro: borda de botão → nitroTicks = duração; top e aceleração multiplicados enquanto dura
top = effectiveTopSpeed(car, def, state, mods)    // marcha, nitro, vácuo, elástico, tanque vazio, box
se brake:          speed −= def.brake × DT
senão se throttle: speed += def.accel × GEAR_ACCEL[gear] × (nitro? × vácuo?) × DT   (até top)
senão:             speed −= def.accel × 0,45 × DT                                     (freio-motor)
na grama (|x| > 1,05 fora do box): speed cai até um limite; skidTicks
volante:     x += DT × steerRate(def) × max(0,25, speed/top) × steer
centrífuga:  x −= DT × centrifugalRate(def) × (speed/top)² × seg.curve     // a curva "empurra" para fora
combustível: fuel −= fuelPerUnit × speed × DT × (fatores)
avanço:      z += speed × DT (volta a 0 em track.length)
```
Funções puras úteis: `steerRate(def)`, `centrifugalRate(def)`, `holdableSpeedFraction(def, curve)`,
`effectiveTopSpeed(car, def, state, mods)`, `carStats(car)`, `segmentAt(track, z)`, `maxCurveAhead(track, z, n)`,
`wrappedDelta(a, b, length)` (distância em z com volta).

---

## 3. Renderização atual

### 3.1 Contrato (`src/game/contracts.ts`) — é isso que um renderizador novo precisa implementar
```ts
interface Renderer {
  readonly canvas: HTMLCanvasElement;
  resize(width: number, height: number, dpr: number): void;
  render(frame: RenderFrame): void;              // corrida
  renderIdle(time: number, track: Track): void;  // fundo dos menus
  dispose(): void;
}
RenderFrame { state: RaceState; track: Track; viewports: ViewportSpec[] /* 1–4 */; options: RenderOptions;
              time: number; paused: boolean; coop: boolean; showHud: boolean; ghost?: GhostFrame }
ViewportSpec { seat; carIndex /* índice em state.cars */; color; name; messages: HudMessage[] }
RenderOptions { quality: 'low'|'medium'|'high'; showMinimap; screenShake; reduceEffects; palette }
```
Criado em `session.ts` por `createRenderer(canvas, hudRoot)` (`src/render/renderer.ts`). Página: `<canvas id="game">`,
`<div id="hud">` (HUD em DOM por cima), `<div id="ui">` (menus em DOM).

### 3.2 Fluxo de `render(frame)` (`src/render/renderer.ts`)
```
cars.update(frame); effects.update(frame)
rects = viewportRects(n, width, height)                           // layout.ts: 1, 2, 3 ou 4 telas
para cada viewport i:
   car = state.cars[vp.carIndex]
   rf  = buildRoadFrame(track, car.z, BEHIND = 30, AHEAD = 140|200|260 segmentos)   // roadframe.ts
   sky.update(absoluteHeading(track, car.z), time)
   road.update(rf, track)                  // malha da estrada + zebras + faixa do box
   terrain.update(rf, track, time, sunDir, heading)
   scenery.update(rf, track, time)         // sprites da pista viram modelos 3D instanciados
   cars.pose(rf, state, track, carIndex, viewports, time); cars.poseGhost(rf, track, ghost)
   cam.update(rf, car.x, speedFrac, seg.curve, nitro, time, shake)  // ChaseCamera (camera.ts)
   effects.pose(rf, track, escala)          // poeira, faíscas, chama do nitro
   setViewport(rect) (scissor) → composer.render() (bloom, qualidade alta) | renderer.render(scene, camera)
hud.update(frame, width, height)            // hud.ts: posição, volta, tempo, velocímetro, combustível, nitro, minimapa
```
**`buildRoadFrame`** é o coração geométrico: as pistas do DSL não fecham como circuito real (podem se cruzar), então
não existe mundo fixo. A cada quadro, para cada viewport, a linha central é reconstruída no referencial do carro:
origem no ponto da linha central em `car.z`, eixo −Z para a frente, rumo integrado segmento a segmento
(`heading += seg.curve × HEADING_PER_CURVE`, 0,0035 rad), altura `y × Y_SCALE` (0,0025 m/unidade), 1 segmento = 4 m,
meia pista = 7 m (`src/render/units.ts`). Saída: `RoadFrame { count, baseIndex, behind, baseFraction, px, py, pz, heading,
segIndex }` (Float32Arrays). `locateOnFrame(frame, track, z, x, out)` põe qualquer (z, x) — carro, sprite — nesse espaço.

### 3.3 Módulos de desenho
| Arquivo | Classe/função | O que desenha |
|---|---|---|
| `road.ts` | `class Road { setPalette(p, night, key); update(rf, track); dispose() }` | asfalto, marcações, zebras, box, linha de chegada |
| `terrain.ts` | `class Terrain { setTrack(track, p, key); update(rf, track, time, sunDir, heading) }` | relevo lateral, anéis de horizonte |
| `sky.ts` | `class Sky { setQuality(q); setPalette(p, time, key); update(heading, time) }` | céu, sol/lua, estrelas, nuvens |
| `scenery.ts` | `class Scenery { setNight(n); update(rf, track, time) }` | objetos da beira (a partir de `Segment.sprites`) |
| `cars.ts` | `class Cars { update(frame); pose(rf, state, track, ownIndex, viewports, time); poseGhost(…); hide() }` | carros instanciados, rodas, luzes, chama do nitro, etiquetas |
| `effects.ts` | `class Effects { update(frame); pose(rf, track, scale); clear() }` | partículas |
| `camera.ts` | `class ChaseCamera { update(rf, carX, speedFrac, curve, nitro, time, shake) }`, `class IdleCamera` | câmera de perseguição / menus |
| `palette.ts` | paletas por bioma × período | cores |
| `hud.ts`, `minimap.ts`, `layout.ts` | `Hud.update`, `trackOutline(track, size)`, `viewportRects(n, w, h)` | HUD em DOM, minimapa, divisão de tela |

---

## 4. Geração da pista

- **Definição = dados.** `src/core/track/tracks.ts` exporta `TRACKS: TrackDef[]` (32 pistas); `src/core/data/cups.ts` agrupa
  em 8 copas de 4. Cada pista é uma lista de operações (`TrackOp`):
  `{op:'straight', length}` · `{op:'curve', length, curve, hill?}` · `{op:'hill', length, height}` ·
  `{op:'s', length, curve}` (S) · `{op:'pit', length}` — comprimentos em segmentos.
- **Construção** (`src/core/track/builder.ts`, determinística, sem trigonometria):
  ```
  buildTrack(def): Track
    for op of def.ops: applyOp(builder, op)
       → addRoad(b, enter, hold, leave, curve, dy)   // entrada easeIn, sustentação, saída easeOut; altura easeInOut
          → addSegment(b, curve, y, pit)             // push { index, z = index×200, curve, y0 = lastY, y1 = y, band, pit, sprites: [] }
    box automático nos segmentos 4–39 se a pista não tiver `pit`
    closeElevation(segments)                         // fecha a altura para a volta emendar
    decorate(track, hashString(def.id))              // espalha SpriteRef por bioma (rng com semente), com `solid` e escala
  getTrack(id): Track        // constrói uma vez e guarda em cache (src/core/track/index.ts)
  segmentAt(track, z): Segment
  ```
- **Obstáculos**: só os `SpriteRef` sólidos nos segmentos (colisão por `SPRITE_HALF_WIDTH[kind] × scale`,
  `src/core/track/sprites.ts`) e os outros carros. Não há obstáculo dinâmico na pista.
- A curva é **só um número por segmento** (não há coordenadas XY do traçado); a altura é `y0/y1` por segmento. É o
  formato "Lou's Pseudo 3d Page" / "Javascript Racer": projetar segmento a segmento acumulando `dx += curve`.

---

## 5. Para plugar um renderizador pseudo-3D
1. Implementar `Renderer` (seção 3.1) num módulo novo (ex.: `src/render-pseudo3d/`), lendo `frame.state` e `frame.track`
   **sem alterar nada**.
2. Por viewport: câmera em `(car.x, y do segmento em car.z, car.z)`; iterar `segments` de `segmentAt(car.z)` até N à frente
   com volta (`% segments.length`), acumular o deslocamento lateral pela `curve`, projetar `y0/y1`, desenhar trapézios
   (grama, zebra por `band`, asfalto, faixa, box por `pit`), depois sprites (`x` em meias-larguras) e carros
   (`state.cars[i].z/x`, `steerPose` para a pose) de trás para a frente.
3. Tela dividida: `viewportRects(n, w, h)` de `src/render/layout.ts`. HUD: dá para reusar o `Hud` de `hud.ts` (DOM).
4. Trocar em `session.ts` a chamada `createRenderer(canvas, hudRoot)` (ou escolher por uma opção).
