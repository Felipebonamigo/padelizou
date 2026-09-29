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

## Onde está cada coisa

| Arquivo | O quê |
|---|---|
| `src/game/storage.ts` | `readJson`/`writeJson`, memória da sessão, espelho, liberadores de espaço, `saveHealth` |
| `src/game/cloudsave.ts` | Espelho em arquivo e abertura no Electron (pendência, quem vale, `keepInMemory`) |
| `src/game/errors.ts` · `ghost-store.ts` | `dropStoredErrors` · `dropOldestGhost` |
| `src/game/save-notice.ts` | Quando mostrar o aviso (puro) |
| `src/errors/toast.ts` (+ `errors.css`, `strings.ts`) | O aviso no canto (`createSaveToast`), textos `errors.save.*` |
| `src/main.ts` | Liga os liberadores e o aviso |
| `tests/storage-full.test.ts` | `localStorage` com cota fixa: corrida e copa concluída chegam ao arquivo e a próxima abertura as lê; abertura com o local cheio; sessão relê o que gravou; pendência; disco recusando; descarte e o que nunca se descarta; aviso |
| `scripts/playtest-save.mjs` | O mesmo no Chromium, com a cota real (`npm run preview` no ar): aviso no resultado e no menu, em PT e EN; espaço liberado grava tudo; "Electron" com `window.desktop` falso e o arquivo no Node |
