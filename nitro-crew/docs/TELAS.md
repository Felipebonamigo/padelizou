# Telas de menu e HUD em todas as resoluções (passo 5.5)

Toda tela de menu precisa caber e se ler em qualquer resolução alvo, com texto normal e com
**Opções › Acessibilidade › Texto grande**, e o HUD precisa ser legível na tela dividida do Steam Deck.
Quem garante isso é o roteiro `scripts/playtest-layout.mjs`: ele mede cada tela e falha se alguma
regra abaixo quebrar. Mudou CSS de menu ou do HUD? Rode o roteiro antes de commitar.

## Resoluções alvo

| Resolução | Por quê |
|---|---|
| 1280×720 | 720p, a menor 16:9 comum |
| 1280×800 | **Steam Deck** (16:10, tela de 7") |
| 1366×768 | notebook comum |
| 1600×900 | 900p |
| 1920×1080 | 1080p, o caso mais comum na Steam |
| 2560×1440 | 1440p |
| 1024×640 | o mínimo suportado (janela pequena) |

Cada uma com texto normal e com texto grande (`--text-scale` 1,2): 31 telas × 14 combinações = 434 medições.

## Regras de layout

1. **Nada focável fora da viewport** — nem recortado por uma caixa com `overflow` (o VOLTAR do lobby
   ficava escondido pela borda do painel). Exceção: listas feitas para rolar (abaixo), onde basta a
   caixa inteira estar na tela, porque o foco rola até o item (`scrollIntoView` em `createFocusList`).
2. **Sem rolagem inesperada**: a `.screen` e a página não rolam; caixa interna só rola se estiver em
   `ALLOWED_SCROLL` — `.table-wrap`, `.rec-scroll`, `.pl-list`, `.pl-detail`, `.cup-rows`, `.track-scroll`,
   `.tp-rows`. O `overflow: auto` de `.lobby-panel`, `.garage-panels` e `.online-col` é rede de segurança,
   não layout: se rolar, é defeito.
3. **Nenhum rótulo cortado**: nem com reticências (`text-overflow: ellipsis` que cortou de fato) nem
   recortado seco por `overflow: hidden` ("Tanqu"). Texto que pode não caber quebra em duas linhas
   (`white-space: normal; line-height: 1.1–1.15`) em vez de virar "Código da…".
4. **Irmãos sem sobreposição**: cada elemento conta com o que transborda dele; o conteúdo do cartão do
   carro no lobby também tem de caber dentro da própria borda (`CONTAINED`).

Como escrever CSS que respeite isso:

- **A unidade `--u` cresce com a altura** (`0,15vmin`, ver `styles.css`): em unidades `u` a altura útil é
  ~600 u em qualquer resolução (605 u em 720p, 614 u em 1080p, 602 u em 1024×640). Logo, o que não cabe em
  720p também não cabe em 1440p — **não conserte com `@media (max-height: …)`**: as opções só cabiam
  abaixo de 721 px de altura porque a compactação morava num `max-height: 720px`, e o rodapé saía da tela
  em 1600×900 e 1920×1080. Ajuste a regra em qualquer altura.
- **Texto grande** é a classe `#ui.large-text` (e `--text-scale` nas variáveis `--fs*`). Compactação que
  só o texto grande exige vai com `#ui.large-text .scr-<tela> …`, no CSS da própria tela.
- Tamanhos fixos em `u` (setas, alturas mínimas, miniaturas) não crescem com o texto grande: é neles e
  nos espaçamentos que se ganha altura, não no tamanho da letra.
- CSS da tela no arquivo da tela (`src/ui/screens/<tela>.css`, importado pelo `.ts` dela), com
  `.scr-<tela>` no seletor para vencer `styles.css` qualquer que seja a ordem no build.
- `styles.css` só com regra mínima e geral (ex.: `.screen-title-row` quebra linha quando o chip não cabe).

## HUD na tela dividida (Steam Deck)

O HUD escala por `--s` (`uiScale` em `src/render/layout.ts`: 1 numa célula de 540 px de altura), vezes o
tamanho do HUD das opções (80–150%). Com 2 jogadores a 1280×800 cada viewport tem 400 px de altura →
`--s` 0,74, e a volta, o "/20" e os tempos ficavam com 8,9–11,9 px numa tela de 7". Com 4 jogadores em
1024×640 e HUD em 80%, `--s` 0,47 → 5,7 px.

Regra (fim de `src/render/hud.css`): **números** (posição, "/20", volta, tempo, últimas voltas,
velocidade, marcha) com `max(12px, …)`; **rótulos** (nome, COMBUSTÍVEL, KM/H, companheiros) com
`max(10px, …)`. Acima do piso a escala segue igual; o 3D não muda. O roteiro mede 2P e 4P a 1280×800,
2P a 1280×720 e 4P a 1024×640 com HUD 80%, e confere que os painéis de um viewport não se sobrepõem — com a legenda
dos marcos (docs/VISUAL.md, "Legenda dos marcos") na tela, com o nome e o lugar mais compridos, no pé do viewport.

| HUD, 2P a 1280×800 | antes | depois |
|---|---|---|
| posição / "/20" | 34,1 / 11,9 px | 34,1 / 12 px |
| volta / últimas voltas | 11,9 / 8,9 px | 12 / 12 px |
| tempo / velocidade / marcha | 23,7 / 32,6 / 14,8 px | igual |
| nome / rótulos | 9,6 / 8,2 px | 10 / 10 px |

## Como rodar

```
npx vite build && (npx vite preview --port 4174 --strictPort &)
node scripts/playtest-layout.mjs http://localhost:4174/ scratch/layout
```

Leva ~6 min (uma corrida de copa de verdade para ter resultado, classificação e "Continuar"; depois o laço
de quadros para e só o DOM é medido). Sai com código 1 se algo falhar e grava:

- `scratch/layout-report.json` — todas as medições (problemas e folga até a borda de baixo);
- `scratch/layout-FAIL-<tela>-<res>[-grande].png` — cada combinação que falhou;
- `scratch/layout-tight-…png` — as seis mais apertadas; `scratch/layout-hud-…png` — o HUD.

Variáveis: `NC_LAYOUT_ONLY=options,lobby-4` (só essas telas; sem as que dependem da corrida, ela é pulada),
`NC_LAYOUT_RES=1280x800,1024x640`, `NC_LAYOUT_TEXT=normal|large`, `NC_LAYOUT_SKIP_HUD=1`,
`NC_LAYOUT_ONLY=none` (só o HUD), `NC_LAYOUT_SHOTS=1` (fotografa todas as combinações) e
`NC_LAYOUT_CSS=arquivo.css` (injeta CSS antes de medir: experimente um ajuste sem refazer o build).

Telas medidas: título, menu principal (com "Continuar"), lobby com 1 e com 4 jogadores, copas (aba da fronteira —
o Nordeste, 9 estados —, o Mundial fechado na última copa e o Sul, 3 estados), pistas (começo e fim da grade),
carreira, garagem com 2 pilotos (P1 na vitrine do carro mais caro) e com 4
(P1 na vitrine; os outros com os nomes de carro livre mais largos), festa, inscrição do torneio com 8 pilotos,
"passe o controle", tabela do torneio, opções, acessibilidade, controles, recordes, créditos, online
(conectar), "Como jogar" com 4 assentos, fim do tutorial (concluído e pulado), passaporte (carimbado e um estado
sem carimbo), resultado (normal, com 14 conquistas e com carimbo novo que fecha a região), classificação e pausa.
O save da medição está no meio da Expedição: 12 estados carimbados, o Mundial fechado.

## Telas da Expedição Brasil (onda G, 02/10/2026)

Identidade (docs/PISTAS-TURISMO.md, "Identidade"): **Expedição Brasil, passaporte, carimbo, cartão-postal** — nunca
"turnê" ou "world tour". Nomes de pista e de copa sempre por `t('core.track.<id>')` / `t('core.cup.<id>')`
(`trackName()` em `common.ts`; a definição só como reserva para pista sem string).

- **Copas** (`select.ts`): duas etapas numa barra de abas — *Expedição Brasil* (uma aba por região: Sudeste, Sul,
  Centro-Oeste, Nordeste, Norte, cada uma com "carimbados/estados") e *Mundial* (uma aba "7 países", com cadeado
  enquanto fechado). ←→ trocam de aba (clique também), ↑↓ andam na lista da aba, o detalhe acompanha. A tela abre
  na aba e na copa da fronteira (`startTab`). Copa de estado mostra a sigla num carimbo (`.uf-badge`, tinta da
  região) em vez da bandeira repetida, e "Carimbada no passaporte" quando vencida. Mundial fechado: um aviso em
  cima da lista diz qual copa vencer ("vença a Copa Tocantins"); a linha do aviso não alarga a coluna
  (`width: 0; min-width: 100%`). Com a barra de abas, o detalhe da copa de 4 pistas ficou mais baixo (miniaturas
  de 48 u, 40 u com texto grande) e, com texto grande, a fala do rival sai do detalhe.
- **Pistas** (`select.ts`): uma seção por copa, na ordem das abas, com "EXPEDIÇÃO BRASIL · SUDESTE" etc. em cima da
  primeira copa de cada região; a linha ocupa a largura toda (3 colunas nas copas de estado, 4 no Mundial). A
  navegação é de grade de linhas desiguais (`raggedGridMove`, `FocusListOptions.rows`): ↑↓ mantêm a coluna e
  prendem na última numa copa de 3 (da 4ª coluna do Mundial para o Tocantins cai na 3ª), ←→ param nas pontas.
- **Passaporte** (`passport.ts`, menu principal › Passaporte): 27 carimbos em 5 linhas (uma por região, com
  "n/total"; a linha completa ganha a cor da região). A grade usa a mesma navegação de linhas desiguais e abre no
  último carimbo ganho. Ao lado, o **cartão-postal** do estado em foco: papel claro, cenário em degradê por região
  (céu, sol e morros só de CSS), "Lembranças de / ESTADO", região, as 3 pistas e os marcos
  (`placeOf()` de `src/core/data/places.ts`, nomes em `src/ui/passport/strings.ts`) e o carimbo postal
  ("Carimbado"/"Sem carimbo", desenhado por `::before/::after` para girar sem filhos sobrepostos). Sem carimbo, o
  cartão sai esmaecido com "Vença a Copa X para carimbar o passaporte".
- **Resultado com carimbo novo** (`results.ts`, `stampNews`): a corrida que fecha a copa de um estado pela primeira
  vez (`RaceOutcome.newStamp`, `src/game/raceEnd.ts`) mostra o carimbo e "Bahia carimbado no passaporte!", com a
  etiqueta "Região Nordeste completa!" quando ele fecha a região. Uma linha só.
- **Torneio** (`party.ts`): o teto de rodadas é o da copa (`tournamentMaxRounds`: rodadas + final = pistas da
  copa — 2 nas de estado, 3 no Mundial). Antes, 3 rodadas numa copa de 3 pistas travavam a inscrição em "Esta
  copa não tem pistas suficientes".
- **Carreira**: a faixa das 34 copas mostra a sigla dos 27 estados (eram 27 bandeiras do Brasil iguais) em pips
  mais estreitos, para caber em duas linhas; com texto grande a garagem esconde o "Copa 1 de 34" (o chip já diz a
  copa) e a próxima corrida desce um tamanho. O fim do tutorial ficou mais largo e mais baixo (rolava 28 px em
  todas as resoluções na medição de 02/10, antes destas telas).

## O que a varredura achou (28/09/2026)

Antes das correções: 95 de 350 combinações com defeito (a 26ª tela, copas com a última em foco,
entrou depois: 364 hoje) e o HUD reprovado nos 4 casos medidos, número e rótulo abaixo do piso em cada um
(saída guardada fora do repositório; o roteiro falhava pelo motivo certo em cada um).

| Tela | Defeito | Correção |
|---|---|---|
| Opções | rodapé (relatório, VOLTAR, telemetria) abaixo da tela em toda altura acima de 720 px (VOLTAR 30 px para fora em 1600×900) | linhas compactas em toda altura (`styles.css`); rótulo do rodapé quebra linha (`errors.css`) |
| Lobby | VOLTAR recortado pelo painel (17 px de rolagem em 720p, 65 px com texto grande) | seletores e botões do painel mais baixos (`lobby.css`) |
| Lobby | descrição do carro por cima do nome e da direção a partir de 900p; nome do carro encostando na linha do nome com texto grande em 1024×640 | descrição só na grade de um assento; barras e cartão compactos; PRONTO mais estreito com texto grande |
| Lobby / torneio | "Vácuo de equipe", "Carros na pista", "Rodadas classificatórias" cortados com texto grande | setas e valor mais estreitos no painel |
| Controles | VOLTAR fora da tela em 1280×800 e 1024×640 (as colunas empilhavam abaixo de 1100 px); "Virar à esquer…", "Teclado 1" cortados | colunas pelo conteúdo, células mais baixas, duas colunas também em 1024; sem ícone no título e sem a linha de ajuda com texto grande |
| Garagem, 4 pilotos | nome do piloto virava "P…" em toda resolução; "Próxima: … — corrid…"; painéis rolando e "Tanque"/"Falcão GT" recortados com texto grande | dinheiro numa linha própria; próxima pista quebra linha; aperto das telas 4:3 também no texto grande |
| Garagem, 2 e 4 pilotos (onda F, 30/09) | com 2: "TORNA…", "BEIJA…", "Tanque" e "Piloto 1" cortados em toda resolução; com 2–4, a etiqueta "À VENDA $ 40.000 / faltam…" invadia as setas e fazia o painel rolar; "TORNADO RS" cortado com 4 e texto grande em 1024×640 | 2 pilotos usam a coluna única das 3–4; vitrine compacta (só o preço, vermelho se falta dinheiro); nome do carro um tamanho abaixo com 4 e texto grande (`garage.css`) |
| Lobby (onda F) | "POROROCA V10" cortado na grade 2×2: com o contador "5/14" ao lado do nome em qualquer texto, e mesmo sem ele com texto grande | contador embaixo do desenho; nome um tamanho abaixo com texto grande (`lobby.css`) |
| Online | "Código da…" cortado; VOLTAR fora da tela em 1024×640 (cartões empilhados) | campo do código com largura fixa, rótulo quebra; cartões lado a lado em 1024 |
| Copas | detalhe da copa por baixo da dica com texto grande | corridas mais baixas com texto grande |
| Fim do tutorial | "Menu principal" fora da tela com texto grande | caixa mais larga, menos respiro |
| "Passe o controle" | título partido ao meio, "Teclado (seta…" | `.screen-title-row` quebra linha só quando precisa; controle quebra em duas linhas |
| Classificação | tela rolando com texto grande | tabelas (que rolam por dentro) cedem 4vh |
| HUD | números com 8,9 px no Steam Deck | piso de 12 px (números) e 10 px (rótulos) |

## Limites conhecidos

- O Chromium daqui não tem Inter, Segoe UI nem Roboto: a pilha de `--font` cai em Arial, servida pela
  Liberation Sans (mesma métrica do Arial). No Windows (Segoe UI) as larguras mudam um pouco; num Linux sem
  Arial nem Liberation (o que o Steam Deck tiver instalado) a reserva pode ser mais larga. As larguras
  medidas valem para essa fonte: vale conferir no aparelho de verdade.
- A tela online é medida só no "conectar"; o lobby em rede exige relay e é coberto por
  `scripts/playtest-online.mjs` (sem medição de layout).
- Só em português (os textos em inglês são, em geral, mais curtos).
- Tela mais apertada depois das correções: resultado e recordes em 1024×640 (10 px de folga).
