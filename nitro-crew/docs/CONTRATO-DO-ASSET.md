# Contrato do asset — como um carro e um marco-herói chegam ao jogo

Este contrato é o que o `npm run check-car` (carro) e o `tools/convert-landmark.mjs` (marco e peça) conferem, mais o que
a bíblia de estilo (`docs/DIRECAO-DE-ARTE.md`) pede de acabamento. **O dono aprova no Marco 1, e a L3 o leva ao
`src/render/cars/check.ts`.** Tudo o que **não vem do código de hoje** está marcado **proposta**: ainda não é conferido
por nenhum validador e pode mudar na aprovação.

**Independente do motor.** O contrato descreve um arquivo glTF (metros, eixos, nomes de nós e de materiais, tetos), não
uma API. O motor — Unity 6 URP ou Three.js Ultra — será decidido pelo duelo antes da L (decisão nº 27 de
`docs/CRONOGRAMA.md`), e o mesmo arquivo serve aos dois.

**Quem faz a arte.** Não há artista (dono, 09/10/2026; `docs/CRONOGRAMA.md`, "Revisão visual", R4 e decisão nº 28). O
arquivo sai de **modelo pronto CC0**, ou do **Meshy só para o que não existir pronto** (lista em `docs/MESHY-LISTA.md`, nada
gerado sem o dono ver), e **sempre passa por limpeza no Blender** antes de entrar. O Meshy entrega uma malha só; por isso
o carro, em especial, precisa de rodas separadas, camadas de pintura por material, nós `kit_*` e o ponto `seat` (abaixo), e
é o `npm run check-car` que diz se ficou certo.

**Licença.** Registrada **arquivo por arquivo** (página de origem, autor, licença, data: tabela em `docs/ARTE.md`). Nada de
marca, logotipo ou cópia de carro real; o CC0 cobre o arquivo, não marca nem direito de imagem.

---

## 1. Carro

### Convenção de hoje (conferida)

Vem de `docs/ARTE.md`, "Carros — pronto", e do `src/render/cars/check.ts`.

- **Unidades e eixos:** metros; +Y para cima; frente em +Z no glTF (o jogo usa −Z; a conversão é do `gltf.ts`).
- **Nós:** `body`, `wheel_fl`, `wheel_fr`, `wheel_rl`, `wheel_rr`.
- **Vazios:** `exhaust_l` e `exhaust_r`.
- **Pegada** (`CAR_LIMITS`, `check.ts`):
  - |x| ≤ 1,0 e |z| ≤ 2,35 do centro; altura < 1,95;
  - vão do chão: o ponto mais baixo acima de 0,04;
  - comprimento > 4,2 (micro: > 3,2) e largura > 1,8;
  - raio da roda ≥ 0,26.
- **Casco:** ≤ 3.500 triângulos (`CAR_LIMITS.maxShellTriangles`), até a M4 subir de propósito, com teste.
- **Validação:** `npm run check-car -- x.glb` (o mesmo validador do jogo, procedural ou glTF).
- A **proporção** do carro (a roda ≥ meta da bíblia; mesma pegada × curto e gordo) sai da decisão nº 7, no Marco 2, pela
  folha da L3. Até lá, a roda é **provisória** (bíblia, seção 4).

### Materiais (pelo nome)

| Nome do material | O que faz | Origem |
|---|---|---|
| `paint*` | recebe a cor de pintura do carro (a cor do material é o tom: branco = cor pura, cinza = um tom abaixo) | código |
| `accent*` | recebe a cor de detalhe | código |
| `stripe_a*`, `stripe_b*`, `stripe_ab*` | faixas que recebem a cor de faixa A, B ou as duas | código |
| `headlight*`, `taillight*` | emissivos: acendem; força = emissivo ÷ cor (sem emissivo = 1) | código |
| `popup` no nome | sobe com o farol escamoteável | código |
| qualquer outro nome | peça fixa, com a cor, a rugosidade e o metal do material | código |
| `rim*` | o aro da roda; a **cor do aro** é separada da pintura (a N3 separa) | **proposta** (o modelo-base de hoje já grava `rim`; a regra de cor é nova) |
| `glass*` | o vidro; vira o bit de vidro em `aMat` do material da L (reflexo e transparência) | **proposta** (o modelo-base já grava `glass`; o bit é novo) |

### Pontos e nós novos

- **Vazio `seat`** — onde o piloto senta (a cabeça do piloto e o ponto de câmera interna) — **proposta**.
- **Nós `kit_<peça>_<nível>`** — peças que aparecem no carro conforme a melhoria comprada (`docs/CARROS.md`).
  - `peça` ∈ `engine | turbo | tires | brakes | tank | nitro` (`UpgradePart`, `src/core/types.ts`);
  - `nível` de 1 a 3 (`UPGRADE_MAX_LEVEL`, `src/core/constants.ts`);
  - o jogo mostra o nó do nível comprado e esconde os outros — **proposta** (o código só tem as peças e os níveis; os
    nós ainda não são lidos).
- **`decal_<nome>`** — malha plana, com UV, para número, placa e adesivos (N3, U4) — **proposta**.

### Textura, UV e LOD

- **Textura:** atlas de paleta **256 × 256** (`map`), com normal map opcional — **proposta**. **Hoje (até a L3)** o carro
  não tem textura: cor chapada por material.
- **UV:** a malha leva UV no atlas de paleta; a 2ª UV é reservada para a luz assada (bíblia, seção 3) — **proposta**.
- **LOD:** nós de topo `lod0` e `lod1`; o `lod1` com no máximo **metade** dos triângulos do `lod0` — **proposta**.
- **Faceta:** nenhuma faceta visível acima de 15° na distância de jogo (bíblia, seção 4).

---

## 2. Marco-herói

### Convenção de hoje (conferida)

Vem de `src/render/scenery/landmarks/types.ts` e de `docs/ARTE.md`, "Marcos baixados".

- **Origem** no centro da base; y = 0 no chão (o que fica abaixo de 0 cobre o declive).
- **Frente** (o lado que o jogador vê) para **+X**; o **comprimento ao longo da pista** em Z.
- **Metros reais.** **Sem sombra projetada** (o marco é longe demais para o mapa de sombra) e sem `blob`.
- **Lugar**, medido na **borda de dentro da pegada** (`LANDMARK_LAT`, `src/render/scenery/layout.ts`; `docs/VISUAL.md`,
  "Marcos turísticos"):
  - **perto:** 30–80 m;
  - **longe:** 120–330 m;
  - **horizonte:** 220–300 m (o pico fica a ~400 m ou mais).
- **Distância mínima:** nenhuma parte a menos de **26 m** do centro da pista à vista (`LANDMARK_CLEAR_M`, `layout.ts`).
- **`perLap`, `place` e `side` não mudam** (`LANDMARK_PLAZAS`, `src/core/track/plazas.ts`): as praças abrem a colisão das
  pistas de cidade e de litoral.
- **Tetos de triângulos** (`docs/ARTE.md`): perto 2.500 (teto 3.500), longe 4.000 (5.000), horizonte 2.000 (2.500).
  O **teto do marco-herói** é diferente e fica **"a confirmar no bench da L"**.
- **Régua de leitura:** todo marco passa por `tests/front-view.ts` (`docs/VISUAL.md`, "Leitura").
- **Validação:** `node tools/convert-landmark.mjs art/raw/x.glb --id <marco>` (saída 0 = aceito).

### Propostas

- **Nós animáveis `anim_<nome>`**, com o pivô na origem do nó: a Q5 e a U2 animam por código (bondinho, hélice, bandeira) —
  **proposta**.
- **LOD** `lod0`/`lod1`, como no carro — **proposta**.
- **Luz assada** em 2ª UV (`lightMap`/`aoMap`), por período — **proposta** (bíblia, seção 3; R2).

---

## 3. O que este contrato ainda não decide

- A proporção do carro (decisão nº 7) e o contorno (nº 8): Marco 2.
- O teto de triângulos do herói e os tetos por qualidade: dependem do bench da K4 e do bench do dono (`docs/DESEMPENHO.md`
  §4); até lá são **provisórios**.
- Se o motor mudar (nº 27), os **nomes e os eixos** deste contrato ficam; só muda quem lê o arquivo.
