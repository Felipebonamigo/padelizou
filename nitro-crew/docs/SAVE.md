# Save: onde o jogo grava, e o que acontece com o armazenamento cheio

Opções, progresso (copas, recordes, carreira, estatísticas, conquistas) e fantasmas são JSON gravados por
`writeJson` e lidos por `readJson` (`src/game/storage.ts`). A configuração do Steam Cloud e as pastas por sistema
estão em `desktop/README.md` → "Steam Cloud (saves)"; aqui fica o caminho de cada gravação e o caso do
armazenamento cheio (risco "Save no navegador cheio" do roteiro, corrigido na onda E).

## As chaves

| Chave | O quê | Vai para o arquivo (Steam Cloud)? | Descartável para abrir espaço? |
|---|---|---|---|
| `nitro-crew.save` | Progresso: copas, recordes, carreira, estatísticas, conquistas, copa em andamento, lobby | sim | **nunca** |
| `nitro-crew.settings` | Opções | sim | **nunca** |
| `nitro-crew.ghosts` | Fantasmas do contra-relógio (teto de 160 000 caracteres, `docs/FANTASMA.md`) | sim | sim, o gravado há mais tempo primeiro |
| `nitro-crew.errors` | Relatório de erros (últimos 50) | não (o Electron tem `logs/errors.log`) | sim, primeiro |
| `nitro-crew.__pending` | Chaves cuja última gravação não chegou ao arquivo | não | não |

## Os três lugares de uma gravação

1. **O arquivo** (só no Electron): `<userData>/saves/<chave>.json`, gravado pelo espelho que `cloudsave.ts`
   liga (`installSaveMirror`). É a fonte da verdade na abertura (`hydrateFromDisk`).
2. **O `localStorage`**: onde o jogo lê e grava durante a partida (no navegador, o único lugar durável).
3. **A memória da sessão**: o que o `localStorage` recusou. `readJson` lê daqui primeiro, então a sessão
   sempre vê o que gravou por último — a loja de fantasmas, por exemplo, é relida a cada volta nova.

`writeJson(chave, valor)` tenta o `localStorage` e **sempre** chama o espelho, com o `localStorage` tendo aceitado
ou não. Devolve verdadeiro quando a gravação ficou em algum lugar (localStorage, ou arquivo aceitando); a resposta
do disco chega depois e, se for recusa de uma gravação que o `localStorage` também recusou, a chave conta como
perdida.

## Armazenamento cheio

A cota do `localStorage` é da **origem** (≈5 milhões de caracteres no Chromium; medido no playtest: a gravação
seguinte lança `QuotaExceededError`). O save do jogo tem dezenas de KB — quem enche a cota é outra coisa da mesma
origem (outro jogo no mesmo domínio, `localhost` de desenvolvimento, uma página do GitHub Pages do mesmo usuário).

**No Electron** — o arquivo recebe toda gravação, e a sessão segue pela memória:
- nada é descartado (com o arquivo recebendo, o `localStorage` atrasado não custa nada) e não há aviso;
- a gravação que o `localStorage` recusou **não** fica pendente: pendente quer dizer "o `localStorage` é mais novo
  que o arquivo", e ali ficou o valor anterior — marcado, a abertura seguinte o gravaria por cima do arquivo novo;
- na abertura, o arquivo diferente e sem pendência vence como sempre (`chooseSource`); se o `localStorage` ainda
  não tem espaço para ele, a sessão lê o arquivo da memória (`keepInMemory`) em vez do `localStorage` velho;
- a loja de fantasmas vai inteira para o arquivo (não é podada para caber no `localStorage`).

**No navegador** (sem arquivo) — antes de desistir, abre espaço com o que é descartável, nesta ordem
(`setSpaceFreers`, ligado em `main.ts`), tentando de novo depois de cada descarte:
1. o relatório de erros (`dropStoredErrors`) — nesta sessão nada some: o relator guarda o anel na memória e o
   regrava no próximo erro;
2. os fantasmas, um por vez, do gravado há mais tempo (`dropOldestGhost`) — nunca para a própria loja de
   fantasmas, que tem a poda dela (`saveGhostStore`).

Save, carreira, estatísticas e opções **nunca** são apagados para abrir espaço. Só se descarta quando a recusa é
de cota (`isQuotaError`): `localStorage` bloqueado (`SecurityError`) não apaga nada. Se nem assim coube, a
gravação fica na memória da sessão e a próxima gravação tenta de novo com tudo — liberado o espaço, a corrida
seguinte grava inclusive o progresso que não tinha cabido.

## O aviso ao jogador

Quando uma gravação não ficou em lugar nenhum (`saveHealth().lost`: navegador cheio, ou `localStorage` cheio e o
disco recusando no Electron, ou `localStorage` bloqueado), aparece no canto de cima, à direita (abaixo do aviso de erro, se ele estiver aberto):

> **O progresso não foi salvo** — Não há espaço para gravar (armazenamento do navegador ou disco cheio). O jogo
> tenta de novo na próxima gravação.
> (EN: **Your progress was not saved** — There is no room to save…)

Não bloqueia nada e some em 9 s. Aparece só no menu principal ou no resultado da corrida (nunca no meio dela), uma
vez por gravação perdida desde o último aviso (`save-notice.ts`, conferido a cada 0,5 s em `main.ts`): a corrida
que não gravou avisa no resultado; voltar ao menu não repete; uma opção mudada que também não coube avisa de
novo no menu. Se a gravação seguinte deu certo, não há o que dizer. Com o arquivo do Electron recebendo, nunca
aparece.

## Limites conhecidos

- Com o `localStorage` cheio no Electron, a cópia dele fica velha. Se na abertura a leitura do disco falhar (IPC
  sem resposta em 3 s), o jogo segue com essa cópia velha e a próxima gravação vai para o arquivo — o limite que
  já existia (`desktop/README.md`), só que com a cópia mais atrasada.
- No navegador, se o descartável não bastar, os fantasmas descartados no caminho não voltam (a gravação do save
  falhou do mesmo jeito). Só acontece com quase nada descartável sobrando; o aviso aparece.
- A marcação de pendência também mora no `localStorage`: se ela não couber e o disco recusar a gravação, a abertura
  seguinte fica com o arquivo (anterior). É preciso as duas falhas juntas.

## Save de antes da onda G (as 8 copas antigas)

A onda G trocou as 8 copas (brasil → eua → japao → … → mediterraneo, cada uma exigindo a anterior) por 34: os 27
estados da Expedição e o Mundial, que agora exige a última da Expedição (`br_to`). Não há versão de formato: a
migração acontece na leitura (`sanitizeSave` em `src/game/save.ts`, `sanitizeCareer` em `career-save.ts`), e a
gravação seguinte já sai no formato novo. Conferido em 03/10 com saves montados como eram gravados até 954bb92^
(`tests/migration-brasil.test.ts`, "save de antes da onda G"):

| No save antigo | Depois da onda G |
|---|---|
| Nenhuma copa concluída | A Copa Rio de Janeiro aberta (a Copa Brasil virou ela, `LEGACY_CUP_IDS`). |
| Copa Brasil vencida (com os EUA abertos) | RJ concluída e carimbada, SP aberta, e **os EUA continuam abertos**: `save.cupsUnlocked` herda a abertura (`legacyCupOpens` em `cups.ts`). O Japão continua fechado. |
| Brasil e k copas do Mundial vencidas | As do Mundial concluídas continuam abertas (copa concluída fica aberta), e a seguinte também; a Expedição segue de SP. Testado de 0 a 8 copas, também depois de gravar e reabrir. |
| Campeonato normal no meio da Copa Brasil | Some do save, sem erro (as corridas da RJ são outras). |
| Campeonato normal no meio de uma copa do Mundial | Continua da mesma corrida, com a mesma classificação, semente e pilotos; eliminado, a copa continua aberta para recomeçar. |
| Carreira na Copa Brasil | Segue na RJ com dinheiro, garagem e estatísticas; a copa em andamento e as tentativas recomeçam. |
| Carreira numa copa do Mundial | Continua na mesma copa e corrida, segue pelo Mundial e termina no Mediterrâneo. Nível da IA e prêmio passam a ser os da posição nova (abaixo). Carreira concluída continua concluída. |
| Relatório da garagem de uma corrida da Copa Brasil | Sai: a garagem escreveria a chave do texto (`core.cup.brasil`) no lugar do nome. Relatório de copa que não existe é descartado. |
| Conquista `COPA_BRASIL` | `COPA_BR_RJ`. |
| Recordes, fantasmas, estatísticas | Ficam: são por id de pista, e nenhum id mudou. Nas 18 pistas antigas sem praça nem mirante a impressão da volta é a mesma de antes. Nas 6 de cidade (Sampa, Las Vegas, Osaka, Paris, Sydney, Roma) as praças e, desde a onda H, nas 8 de litoral com marco de perto ou de longe (Baía de Tóquio, Mônaco, Boa Esperança, Great Ocean, Atlântico, Tromsø, Amalfi, Santorini) os mirantes (`core/track/plazas.ts`) tiraram prédios da beira e a colisão mudou: o recorde fica com a marca "versão anterior" e o fantasma fica na loja, mas não corre como rival (`docs/FANTASMA.md`). |

**Defeitos achados nesta conferência (03/10), com teste escrito antes e visto falhar:** (1) quem só tinha vencido a
Copa Brasil perdia os EUA — passavam a exigir os 27 estados; quem estava no meio dos EUA continuava a copa, mas se
fosse eliminado não podia recomeçá-la; (2) a carreira guardava o relatório de uma corrida da Copa Brasil, e a
garagem mostrava `core.cup.brasil` na faixa de "copa concluída"/"eliminado".

**Limite que ficou: carreira antiga parada no Mundial.** O nível da IA da carreira segue a posição da copa na lista
(`careerAiLevel`): nos EUA ele era 0,18 (2ª de 8 copas) e passa a 2,89 (28ª de 34). Medido com o piloto médio
(0,97) e o Falcão 111111 — o que a economia antiga lhe dava ao fim da Copa Brasil —, 2 sementes × 4 pistas dos EUA:
antes 1,75 de média e 8/8 no top 5; agora **9,38 e 2/8** — quase toda tentativa termina eliminada (solo exige
top 5). O prêmio sobe na mesma proporção (×1,61 em vez de ×1,11) e cada tentativa paga a ajuda de custo, então a
carreira anda, devagar. Não mexi: o jogo não foi publicado (os saves antigos são do dono e de quem testou), e a
curva da IA é decisão aberta (`docs/CARREIRA.md`). Para quem cair nisso: "Nova carreira".

## Onde está cada coisa

| Arquivo | O quê |
|---|---|
| `src/game/storage.ts` | `readJson`/`writeJson`, memória da sessão, espelho, liberadores de espaço, `saveHealth` |
| `src/game/cloudsave.ts` | Espelho em arquivo e abertura no Electron (pendência, quem vale, `keepInMemory`) |
| `src/game/errors.ts` · `ghost-store.ts` | `dropStoredErrors` · `dropOldestGhost` |
| `src/game/save-notice.ts` | Quando mostrar o aviso (puro) |
| `src/errors/toast.ts` (+ `errors.css`, `strings.ts`) | O aviso no canto (`createSaveToast`), textos `errors.save.*` |
| `src/main.ts` | Liga os liberadores e o aviso |
| `src/game/save.ts` · `career-save.ts` · `src/core/data/cups.ts` | Migração do save de antes da onda G: `currentCupId`/`LEGACY_CUP_IDS` (brasil → br_rj), `legacyCupOpens` → `save.cupsUnlocked`, relatório de copa que não existe sai |
| `tests/migration-brasil.test.ts` | Saves no formato de antes da onda G: 0 a 8 copas concluídas, campeonato e carreira no meio do Mundial, ids de copa que sumiram, recordes e fantasmas das 32 pistas antigas |
| `tests/storage-full.test.ts` | `localStorage` com cota fixa: corrida e copa concluída chegam ao arquivo e a próxima abertura as lê; abertura com o local cheio; sessão relê o que gravou; pendência; disco recusando; descarte e o que nunca se descarta; aviso |
| `scripts/playtest-save.mjs` | O mesmo no Chromium, com a cota real (`npm run preview` no ar): aviso no resultado e no menu, em PT e EN; espaço liberado grava tudo; "Electron" com `window.desktop` falso e o arquivo no Node |
