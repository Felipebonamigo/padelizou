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
| Gravação, codificação, reprodução | `src/game/ghost.ts` (puro) | `recordTick`, `encodeTrace`/`decodeTrace`, `ghostPoseAt`, `liveDelta`, `createGhostRun` |
| Armazenamento e arquivo | `src/game/ghost-store.ts` | chave `nitro-crew.ghosts`, teto e descarte, exportar/importar |
| Ligação com a sessão | `src/game/ghost-session.ts` | `startGhost` → `afterTick` / `frame`; mensagens do HUD |
| Sessão | `src/game/session.ts` | três ganchos: `beginRace`, `stepOnce`, `buildFrame` |
| Render | `src/render/cars.ts` (`poseGhost`), `renderer.ts` | carroceria + cabine com material transparente claro, sem sombra nem rodas |
| HUD | `src/render/ghost-hud.ts` (+ `.css`), gancho em `hud.ts` | "FANTASMA +0,42" no painel do tempo, só no viewport do 1º humano |
| Opções | `src/ui/screens/options.ts` | "Fantasma (contra-relógio)" liga/desliga (`Settings.ghost`, padrão ligado) |
| Recordes | `src/ui/screens/ghost-records.ts` (+ `.css`), gancho em `info.ts` | fantasma na linha da pista, Enter exporta, "Importar fantasma" |
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
JSON `{ format: 'nitro-crew-ghost', v: 1, trackId, ticks, name, carId, date, data }`, nome
`fantasma-<pista>.nitro.json`. Electron: diálogo do sistema pelo `file:save` / `file:open` do preload.
Navegador: download e `<input type=file>` (no navegador o seletor de arquivo exige gesto do usuário:
teclado e mouse funcionam, o botão do gamepad não abre o seletor). Importar substitui o fantasma daquela
pista (a própria volta do jogador continua nos recordes do save); arquivo inválido, grande demais (> 64 KB)
ou de pista desconhecida dá aviso e não muda nada.

### Limite conhecido (onda C)
Nem o fantasma nem os recordes guardam a versão do conteúdo (`CONTENT_FINGERPRINT`, que hoje só o online
usa). Depois de um patch que mude a física ou o traçado de uma pista, o fantasma antigo segue valendo (e
pode ser impossível de bater), e a importação aceita arquivo de outra versão. Nada quebra (o render faz o
módulo do z); a saída, quando houver patch de física, é gravar a impressão no `GhostRecord` e nos recordes
e descartar/arquivar os de outra versão.

## Testes
`tests/ghost.test.ts` (16): ida e volta com erro ≤ meio passo; string só base64url e reta constante quase
vazia; tamanho < 20 KB por volta em todas as pistas (IA e ziguezague); dado corrompido (truncado, sobra,
caractere trocado em várias posições, lixo) ignorado sem lançar; registro/arquivo com tempo que não bate;
interpolação por tempo (antes, entre, depois da última, fim); formatação da diferença; **diferença ao vivo
numa volta simulada** comparada tick a tick com a verdade crua (erro < meio tick) e o fechamento igual à
diferença exata dos tempos; volta mais rápida vira fantasma na hora; fantasma de outra pista/corrompido e
carro com melhorias; descarte por limite; `localStorage` cheio; loja corrompida; ganchos da sessão (só
contra-relógio local, mensagem, opção).

Roteiro Playwright: `scripts/playtest-ghost.mjs` (fluxo de teclado completo com capturas: grava, corre contra, HUD, recordes, exportar/importar, opção; ~3,5 min no swiftshader).
