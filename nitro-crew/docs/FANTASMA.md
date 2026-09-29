# Fantasma do contra-relógio (passo 1.7b)

No contra-relógio o jogo grava a volta do jogador e, na corrida seguinte na mesma pista, põe na pista um
carro translúcido fazendo a **melhor volta** já gravada ali. O HUD mostra ao vivo quanto o jogador está
atrás (+0,42, vermelho) ou à frente (−0,15, verde) do fantasma, e o fechamento de cada volta diz a
diferença final. A melhor volta pode ser exportada como arquivo para desafiar um amigo, que importa e
corre contra ela.

**Só local.** O online corre em modo `quick` por lockstep e não tem contra-relógio; `startGhost` devolve
null fora de `mode === 'timetrial'` ou com driver de rede. O fantasma **não entra no estado da corrida**
nem colide: é lido do estado depois de cada tick e desenhado por cima. Nada muda em `src/core`.

## Como funciona

| Parte | Arquivo | O quê |
|---|---|---|
| Gravação, codificação, reprodução | `src/game/ghost.ts` (puro) | `recordTick`, `encodeTrace`/`decodeTrace`, `ghostPoseAt`, `liveDelta`, `createGhostRun`, `checkGhost` |
| Versão do conteúdo | `src/game/content-version.ts` (puro) | `lapFingerprint(pista, carro)`, `PHYSICS_REVISION`; também a `CONTENT_FINGERPRINT` do online |
| Armazenamento e arquivo | `src/game/ghost-store.ts` | chave `nitro-crew.ghosts`, teto e descarte, `ghostFor` (só o que vale), exportar/importar (`judgeGhostFile`) |
| Ligação com a sessão | `src/game/ghost-session.ts` | `startGhost` → `afterTick` / `frame`; mensagens do HUD |
| Sessão | `src/game/session.ts` | três ganchos: `beginRace`, `stepOnce`, `buildFrame` |
| Render | `src/render/cars.ts` (`poseGhost`), `renderer.ts` | carroceria + cabine com material transparente claro, sem sombra nem rodas |
| HUD | `src/render/ghost-hud.ts` (+ `.css`), gancho em `hud.ts` | "FANTASMA +0,42" no painel do tempo, só no viewport do 1º humano |
| Opções | `src/ui/screens/options.ts` | "Fantasma (contra-relógio)" liga/desliga (`Settings.ghost`, padrão ligado) |
| Recordes | `src/ui/screens/ghost-records.ts` (+ `.css`), gancho em `info.ts` | fantasma na linha da pista, Enter exporta, "Importar fantasma" (com o motivo da recusa) |
| Textos | `src/ghost/strings.ts` | namespace `ghost` |

### Tempo da volta
`elapsed = state.tick - car.lapStartTick`, lido depois do `stepRace`. A primeira amostra de uma volta tem
elapsed 1 e a última elapsed = tempo da volta; gravação e reprodução usam a mesma conta, então o fantasma
"recomeça" sozinho a cada volta do jogador (o `lapStartTick` do carro muda na linha). Antes da primeira
passagem pela linha (a arrancada do grid, `lap === 0`), depois da bandeirada e fora da fase `racing` não há
fantasma. Se a volta do fantasma acaba antes da do jogador, ele some; a diferença ao vivo segue crescendo
(extrapolada pela velocidade final dele).

### O que é gravado
Por tick: `z`, `x`, velocidade, pose do volante (−1/0/1) e nitro ativo. Voltas depois da bandeirada (piloto
automático), voltas com tick faltando e carros com melhorias da carreira (a mesma regra dos recordes do save)
não viram fantasma. Volta mais rápida que o fantasma vira o fantasma **na hora** — a volta seguinte já é
contra ela — e é gravada relendo a loja (a tela de recordes pode ter importado outro no meio). A gravação
continua com a opção desligada; a opção só esconde o carro, a diferença e a mensagem de volta.

### Codificação (string compacta)
Uma amostra a cada `GHOST_SAMPLE_TICKS = 3` ticks (20 Hz), quantizada: `z` em passos de 1 unidade, `x` em
1/128, velocidade em 4 u/s. Quatro canais em sequência — `z` em segunda diferença, `x`, velocidade e
bandeiras (volante + nitro, 0..5) em primeira diferença —, cada valor em zigue-zague, dois ou mais zeros
seguidos numa ficha só, tudo em varint de base 64 (5 bits por caractere + continuação, alfabeto base64url).
Cabeçalho: versão, passo, t0 e número de amostras. Erro máximo nas amostras = meio passo; entre amostras,
interpolação linear (volante e nitro pela amostra mais próxima).

Tamanho medido (teste `volta de verdade em todas as pistas`): volta da IA 3–5 KB; o pior caso — volante
batendo de um lado para o outro, indo para a grama, voltas de 2–3 min — até ~13 KB. Teto duro por volta:
`MAX_GHOST_CHARS = 19 000` (a volta que passasse disso não vira fantasma); o teste exige < 20 KB em todas as
32 pistas.

### Armazenamento
Chave própria `nitro-crew.ghosts` (fora do save): `{ ghosts: { [trackId]: GhostRecord & { savedAt } } }`, um
fantasma por pista. Teto total `GHOST_STORE_MAX_CHARS = 160 000` caracteres (~30 voltas típicas): passou,
saem os **gravados há mais tempo** (`savedAt`, que é a hora em que entrou neste computador — um fantasma
importado antigo não sai primeiro por ter data velha). Se a gravação não ficar em lugar nenhum (`localStorage`
cheio no navegador), descarta o mais antigo e tenta de novo até a loja vazia; nunca lança. Grava pela mesma
`writeJson` do jogo, então no Electron também vai para `<userData>/saves/nitro-crew.ghosts.json` (Steam Cloud) —
inteira, mesmo com o `localStorage` cheio. No navegador cheio, os fantasmas mais antigos também abrem espaço
para o save e as opções (`dropOldestGhost`; `docs/SAVE.md`). Leitura saneia tudo:
entrada com dados corrompidos, pista errada ou tempo que não bate com a volta é descartada em silêncio.

### Arquivo (desafiar um amigo)
JSON `{ format: 'nitro-crew-ghost', v: 2, trackId, ticks, name, carId, date, data, fp }`, nome
`fantasma-<pista>.nitro.json`. `fp` é a impressão da volta (ver "Versão do conteúdo"); o arquivo `v: 1`
(de antes dela, sem `fp`) ainda é lido, e cai como versão desconhecida. `GHOST_VERSION` (1) é a versão da
codificação da string `data` e não mudou; `GHOST_FILE_VERSION` (2) é a do arquivo. Electron: diálogo do sistema pelo `file:save` / `file:open` do preload.
Navegador: download e `<input type=file>` (no navegador o seletor de arquivo exige gesto do usuário:
teclado e mouse funcionam, o botão do gamepad não abre o seletor). Importar substitui o fantasma daquela
pista (a própria volta do jogador continua nos recordes do save). Recusa com aviso e sem mudar nada
(`judgeGhostFile`, um texto para cada motivo): arquivo inválido ou grande demais (> 64 KB), pista que o jogo
não tem, volta que não cabe na pista ("o comprimento não bate") e fantasma de outra versão do jogo (inclusive
o arquivo v1).

### Versão do conteúdo (onda E)
Até a onda D o fantasma não guardava a versão do conteúdo: depois de um patch de física ou de traçado o
antigo seguia valendo (e podia ser impossível de bater), e a importação aceitava arquivo de outra versão — um
fantasma de `copacabana` entrava como de `monaco_noite`, pistas de tamanhos diferentes. Agora:

- **Impressão da volta** (`lapFingerprint(pista, carro)`, 8 hex, `content-version.ts`): `PHYSICS_REVISION`,
  **todas** as constantes da simulação, os atributos do carro de fábrica como a física os lê
  (`effectiveStats`) e o traçado como a simulação o lê (comprimento, largada e, por segmento, curva, box e
  os obstáculos sólidos: lado e meia largura). É por pista e por carro, e não a `CONTENT_FINGERPRINT` do
  online (o jogo inteiro), para uma pista nova, um ajuste de outro carro ou um enfeite trocado não apagarem o
  fantasma de ninguém. Fica de fora o que é só de exibição — nome e cor do carro, nome da pista, zebra,
  relevo (a física não lê a altura), enfeites não sólidos —, então a arte da Fase 2 pode trocar o cenário
  sem invalidar nada, desde que não mexa nos obstáculos sólidos.
- **Todas as constantes, de propósito**: separar à mão as "só da IA" erra fácil (o freio da direção
  assistida usa `AI_BRAKE_CURVE`). O custo: um patch que só ajuste a IA também renova os fantasmas e marca os
  recordes. Se isso incomodar depois do lançamento, a saída é uma lista explícita de constantes excluídas,
  com um teste que varra `physics.ts`/`assist.ts`/`collisions.ts` e prove que nenhuma delas é lida ali.
- **Código, não dado**: mudança só de código na física não muda nenhum dado. `PHYSICS_REVISION` é manual, e
  `tests/sim-golden.test.ts` a prende ao hash do contra-relógio de referência (o carro sozinho na pista, sem
  IA): quem regravar esse hash sem subir a revisão vê o teste falhar com a instrução. Ajuste só da IA não mexe
  nesse hash (não há IA no contra-relógio). Não cobre: código que só age com direção assistida ou entre dois
  humanos (a corrida de referência é um carro, sem assistência).
- **Onde vale**: `checkGhost` dá `ok`, `unknownTrack` (pista que o jogo não tem), `otherTrack` (a volta não
  cabe no comprimento da pista: a 1ª amostra tem de cair até 4 segmentos depois da linha e a última até 4
  antes, todas dentro de [0, comprimento]) ou `otherVersion` (impressão diferente ou ausente). Pistas de
  mesmo comprimento (Copacabana, Rota 66, Baía de Tóquio… têm 360 000) só a impressão separa. `ghostFor` só
  entrega o `ok`: o de outra versão fica na loja, invisível — a pista corre sem fantasma e a primeira volta
  (mensagem "FANTASMA GRAVADO") o substitui com a impressão atual; se nunca for substituído, o descarte por
  idade o leva.
- **Fantasma sem impressão (gravado antes da onda E) = versão desconhecida, descartado como rival.** Não dá
  para saber em que física ele foi gravado, e mantê-lo "até ser batido" é justamente o caso que pode ser
  impossível de bater. O que se perde é pouco e volta sozinho: o tempo da volta continua nos recordes do save
  (que não são apagados), e o próximo contra-relógio grava o fantasma de novo. E nenhum jogador tem fantasma
  antigo: o jogo não saiu, só existem os dos testes de desenvolvimento. A loja lê o registro velho sem lançar e
  sem mexer em nenhuma outra pista.
- **Não é assinatura**: quem editar o arquivo e copiar a impressão certa passa. O objetivo é não correr
  contra uma volta de outra física por engano, não impedir trapaça.
- **Recordes** (`docs/ESTATISTICAS.md`): cada recorde novo guarda a mesma impressão; o de outra versão ganha a
  marca discreta "versão anterior" na tela de recordes e **não é apagado** — só sai quando batido.
- Custo: a impressão de cada par pista × carro é calculada uma vez por execução (~1 ms; as 32 pistas, frio,
  ~40 ms na primeira abertura da tela de recordes).

## Testes
`tests/ghost.test.ts` (21): ida e volta com erro ≤ meio passo; string só base64url e reta constante quase
vazia; tamanho < 20 KB por volta em todas as pistas (IA e ziguezague), e cada uma cabe na própria pista com
a impressão desta versão; dado corrompido (truncado, sobra,
caractere trocado em várias posições, lixo) ignorado sem lançar; registro/arquivo com tempo que não bate;
interpolação por tempo (antes, entre, depois da última, fim); formatação da diferença; **diferença ao vivo
numa volta simulada** comparada tick a tick com a verdade crua (erro < meio tick) e o fechamento igual à
diferença exata dos tempos; volta mais rápida vira fantasma na hora; fantasma de outra pista/corrompido e
carro com melhorias; descarte por limite; `localStorage` cheio; loja corrompida; ganchos da sessão (só
contra-relógio local, mensagem, opção). Versão do conteúdo: a volta gravada leva a impressão da pista e do
carro de quem fez e sai no arquivo v2; impressão diferente ou ausente, carro desconhecido, pista de mesmo
comprimento, volta de Copacabana como Mônaco (mais longa) ou como Passo Alpino (mais curta) e pista
inexistente; importação (v1 lido e recusado, outra versão, outra pista, v2 sem impressão, v3); loja antiga
sem lançar; e, na sessão, fantasma de outra impressão sem rival e substituído pela volta nova.
`tests/content-version.test.ts` (7): a impressão muda com constante, atributo do carro, curva, box e
obstáculo sólido, e não muda com nome/cor/texto/preço do carro, nome da pista, enfeite, zebra e relevo; a do
online é a mesma; recordes com a impressão, save antigo, "versão anterior" e recorde de outra versão não
trocado por volta mais lenta. `tests/sim-golden.test.ts`: `PHYSICS_REVISION` presa ao contra-relógio.

Roteiro Playwright: `scripts/playtest-ghost.mjs` (fluxo de teclado completo com capturas: grava, corre contra, HUD, recordes, exportar/importar, opção; ~3,5 min no swiftshader).
