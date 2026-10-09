# Onda K — Mais uma corrida, e medida

> **Abertura do cartão** (PLAYBOOK §8.3). Escrita em 09/10/2026 sobre o `532d957`, que é a ponta de `claude/top-gear-coop-game-ns9u5l` e coincide com o `origin`. Os cartões de cada frente ficam em arquivos próprios: `docs/ondas/K/K0.md` (orquestrador) e `K1.md` a `K5.md` — o agente de uma frente lê esta abertura e o cartão dele, nada mais.
>
> **Precedência.** Quando esta abertura e um cartão disserem coisas diferentes, **vale a abertura**. Ela foi escrita depois dos seis cartões e confere um contra o outro; as divergências estão no §3.3. Onde a abertura não fala do assunto, vale o cartão.
>
> Todo comando roda de `/home/user/padelizou/nitro-crew`, salvo quando o passo indica outra pasta.

> **Repositório próprio (decisão do dono, 08/10/2026).** O jogo vai para `Felipebonamigo/nitro-crew`, privado — isso
> responde a decisão nº 2 (repositório público). O repositório ainda não existe (o dono cria; a sessão não tem permissão).
> Quando existir: o histórico vai com `git subtree split --prefix=nitro-crew`, e nos cartões troque
> `/home/user/padelizou/nitro-crew` pela raiz do repositório novo e `origin/claude/top-gear-coop-game-ns9u5l` por
> `origin/main` (substituição mecânica; o resto não muda). Enquanto não existir, a onda K **não abre**: tudo o que for
> commitado no `padelizou` sai público.

---

## 1. Estado e bloqueios

### 1.1 Estado em 09/10/2026 (conferido nesta data)

- Ponta: `532d957` ("Nitro Crew: onde paramos…"). Este arquivo e `docs/ondas/K/` foram commitados junto com a abertura (o passo A10 do K0 só confere que estão no remoto).
- Suíte: 1776 testes em 76 arquivos (`CLAUDE.md:96`), cerca de 6 min com a máquina livre.
- `docs/CRONOGRAMA.md:3` ainda diz "**Versão 2 (06/10/2026), aguardando aprovação do dono.**"
- `docs/QA.md` §5 (l. 184–190) tem a tabela vazia: o retorno dos amigos sobre a 0.1.0 não foi registrado.
- O Cristo ainda tem o box torto. `npx tsx scripts/shape-to-track.ts copacabana --quiet` imprime `desvio de fechamento 67.8 · erro médio 2.39 máx. 5.89 (em 100)` e `AVISO: o trecho do box não é reto no desenho (gira 4.2°): mude a largada`.
- `flock` e `xvfb-run` existem na máquina (`/usr/bin`).

### 1.2 Decisões do dono

São colhidas no passo A2 do K0: **uma mensagem só**, com a recomendação marcada em cada pergunta.

| # | Decisão | Bloqueia? | Sem resposta |
|---|---|---|---|
| 1 | Aprovar o cronograma e as horas (3–4 h nas semanas de marco, ~1 h nas outras) | **Sim**, a onda inteira | Pare com **NEEDS_CONTEXT** |
| 2 | Repositório e licença. O `Felipebonamigo/padelizou` é público: tudo o que for commitado sai público, e a 0.1.1 também. Recomendado: (a) `UNLICENSED` já | **Sim**, a onda inteira | **NEEDS_CONTEXT**. A resposta "repositório próprio já" também leva a **NEEDS_CONTEXT**, porque muda o remoto, o PLAYBOOK §4/§5 e todos os cartões |
| 3 | Conta Steamworks e App ID | Não | `desktop/steam_appid.txt` continua `480` |
| 4 | O que os amigos disseram da 0.1.0 | Não | No §5.1: "Sem retorno até dd/mm/2026; perguntar de novo no Marco 1." |
| 16 | VPS do relay | Não | Se aprovada, vira trilha S **depois** da K. A 0.1.1 sai com "online ainda não funciona" |
| K3 | Rampa provisória da IA na carreira, opção A ou B (**pergunta nova**: acrescente à mensagem do A2) | Não | Use a B. A K3 fecha com DONE_WITH_CONCERNS |

Texto da pergunta K3, para colar na mensagem:

> "Rampa provisória do ritmo da IA na carreira:
> - **(B, recomendada)** ritmo 0 do RJ ao ES, subindo a 0,4 do DF ao AC e caindo para 0,2 no Mundial. Dá **29 de 34** copas com média do piloto médio entre 3º e 5º.
> - **(A, a do cronograma)** começa meio degrau abaixo. Do RJ ao RS vira 'volta da vitória' (40 vitórias em 42 corridas), e só **18 de 34** copas ficam na faixa.
>
> O '+0,8 no Mundial' do cronograma foi medido e não serve: EUA com 12,25º de média."

A resposta vai para a instrução da K3 (§2.3).

### 1.3 O que o orquestrador commita antes de lançar qualquer frente

É o passo A11 do K0: **um commit só, já no remoto**. Leva:

- **Contratos:**
  - `RaceConfig.aiPace?: number`, sem padrão gravado;
  - `TrackOp` com `bend`;
  - `tests/contratos-onda-k.test.ts`, com 3 testes.
- **Cristo com o box reto:**
  - o pedestal vai até y = 100,5 e a largada passa a `start: [36, 97]`;
  - `tests/track-art.test.ts`, com 6 testes.
- **A tabela das 7 câmeras** em `docs/DIRECAO-DE-ARTE.md` (A8).
- **Decisões e retorno dos amigos:**
  - as decisões em `docs/CRONOGRAMA.md` (A3);
  - o `docs/QA.md` §5.1 (A4).
- **Desvio do `aiPace`** registrado em `CLAUDE.md:51` e em `CRONOGRAMA:219` (A9).
- **Este `docs/ondas/K.md`**, com a abertura e os seis cartões.
- **Se a nº 2 for (a)**, os arquivos de licença do A3.
- **Se a nº 3 sair**, o App ID.

Acréscimo desta abertura ao A9: em `CLAUDE.md`, troque o parágrafo **Onde paramos** (l. 16–17) por "**Onde paramos (dd/mm/2026):** onda K aberta — cartão em `docs/ondas/K.md`; frentes K1–K5 em andamento; mesclas e fecho pelo orquestrador."

**Antes do push**, a "fase A" do §6 do K0 inteira tem de estar verde. Em particular:
- `npx tsc --noEmit -p .` sem saída;
- `npx vitest run tests/contratos-onda-k.test.ts tests/track-art.test.ts` → `Tests  9 passed (9)`;
- suíte completa → `Test Files  78 passed (78)` e `Tests  1785 passed (1785)`;
- `npm run build` → exit 0.

**Depois do push**, confira que o remoto tem o commit: `git log -1 --format=%s origin/claude/top-gear-coop-game-ns9u5l` tem de imprimir a mensagem do A11. **Nenhuma frente é lançada antes disso**, porque os worktrees das frentes nascem de `origin/…`.

### 1.4 Desvios conscientes do cronograma

1. **`aiPace` sem padrão gravado.** É lido com `?? 0`, como o `aiLevel`. Gravar 0 quebra 5 testes de ida e volta (no `deserializeRace`) ou 9 (no `createRace`). Detalhe: K0 §2, desvio 1.
2. **`scripts/track-art.ts` e `scripts/art-sheet.ts` são do K0.** A K5 só lê `ART.copacabana`.
3. **`perf-sim --fingerprints` muda 2 das 8 corridas:** `solo-sem-assistencias` (copacabana) e `versus-cambio-manual` (sampa_noite, que perde as placas). As 4 pistas das batidas não estão nos cenários (`scripts/sim-scenarios.ts:131–143`).
4. **Relay fora da 0.1.1.** A trilha S é frente própria, depois da K.
5. **Rampa do `aiPace`.** O "meio degrau abaixo no começo, +0,8 no Mundial" foi refutado por medida. O padrão é a opção B (§1.2).
6. **`src/core/constants.ts`** aparece na lista da K3 no cronograma, mas **ninguém o edita** nesta onda: a `lapContent` leva todas as constantes.
7. **As batidas no cenário** se corrigem pelas **placas de curva**, no `builder.ts`, e não pela IA. Os recordes e fantasmas de **24 pistas** passam a "versão anterior", não só os de 5.
8. **Exceção ao princípio 10:** o questionário custa 1 entrada entre corridas, uma vez por sessão e por dia. Vai declarada ao dono no Marco 1.
9. **Do menu à contagem hoje são 8 entradas** (medido pela K1), não "~9".

---

## 2. Ordem de execução

### 2.1 Sequência

```
A  (orquestrador, checkout principal, em sequência) ── A11: commit + push
        │
        ├─► K1  K2  K3  K4  K5           5 agentes em paralelo (o teto do PLAYBOOK §3.2)
        ├─► B   rodada 1, bíblia, contrato, docs   (o próprio orquestrador, worktree nc/k-k0)
        └─► M   mesclas, uma por vez, na ordem em que chegam, com as regras do §4
                 │
                 └─► pós-mescla (cuia, spike, linha de base, cartões L–N) ─► C  fecho (§5)
```

### 2.2 Por que assim

- **A antes de tudo.** Sem o push, os worktrees nascem sem `K.md`, sem `aiPace` e sem `bend`. Faltariam também o Cristo com box reto, base de todos os números da K5, e a tabela das câmeras que a K4 usa.
- **K1 a K5 juntas, por quatro motivos:**
  1. Os arquivos são disjuntos. No §3.1, nenhum arquivo tem duas frentes de agente como dona.
  2. Nenhuma frente usa o código de outra enquanto trabalha. Tudo o que uma entrega para outra é ligado na mescla pelo orquestrador:
     - diário e questionário em `session.ts`, `menus.ts` e `main.ts`;
     - o botão "Abrir pasta";
     - a cuia no `ART_CFG`.
  3. Cada área central tem uma frente só: IA e economia são da K3; traçado é da K5. Ninguém toca física nem `constants.ts`.
  4. Os contratos que mais de uma frente usa já estão no remoto.
- **A parte B fica com o orquestrador, num worktree** (`.claude/worktrees/nc-k-k0`, branch `nc/k-k0`). Não há sexto agente.
  - Enquanto a rodada não estiver completa, o T2 fica vermelho. No checkout principal, isso contaminaria cada mescla.
  - A parte B não usa vite nem WebGL.
  - Se uma frente já tiver sido mesclada antes de o orquestrador começar o B, um agente pode assumir o B com a instrução do §9 do K0.
- **Mescla uma por vez** (PLAYBOOK §3.5), com as regras de ordem do §4.
- **Suíte completa.**
  - Nenhuma frente roda a suíte de **base**: cinco suítes juntas no começo estouram a máquina. A base é a do A11: **1785 testes em 78 arquivos**.
  - Cada frente roda a sua suíte completa só no fim. Estouro de tempo sob carga é aceito, desde que o arquivo passe rodado sozinho (PLAYBOOK §5).
  - O orquestrador **não roda a suíte completa enquanto houver agente rodando**. Nas mesclas, roda só os testes da área.
  - A suíte que vale é a do fecho (C1 e C5).
- **Carga de CPU.** Três frentes pesam: a K2 (linha de base, 3 sementes de 3–7 min), a K3 (sonda com 4 processos) e a K4 (bench e spike). Enquanto elas rodam, é esperado estourar tempo em `cars`, `ghost`, `fuel` e `landmarks`.

### 2.3 Lançamento e o que muda em cada instrução

Lance cada frente com `Agent` (`isolation: "worktree"`, em segundo plano). A instrução é a do §9 do cartão da frente, com as trocas da tabela abaixo.

Toda instrução ganha esta linha: "**Base da suíte: 1785 testes em 78 arquivos, medida pelo K0 no A11 com a máquina livre; não rode a suíte de base. Antes de capturar, leia o §2.4 da abertura do `K.md` (vagas de captura).**"

| Frente | Vaga de captura (§2.4) | Portas (§3.2) | Muda em relação ao cartão | Suíte no fim (na branch dela) |
|---|---|---|---|---|
| K1 | 3 | preview 4181 · vite 5601 | Passo 1: não roda a suíte de base (N = 1785). Os playtests dos passos 3i e 13 rodam sob a vaga 3 | `Test Files 80` · `Tests 1802` |
| K2 | 3 | vite 5612 | Passo 1: não roda a suíte de base. O "total de antes" é 1785, e o `scratch/k2-suite-antes.txt` não existe. O §7 (`k2-shot.mjs`, `playtest-layout`) roda sob a vaga 3. Os passos M são do K0 (§4) | `81` · `1810` |
| K3 | não captura | vite 5603, só se precisar | No §9, troque o 1º trecho `<…>` por "opção B" ou pela resposta do dono, e o 2º por "N = 1785, F = 78" | `80` · `1801` |
| K4 | 2 | vite 5641 e 5642 · preview 5643 | `CAMERAS` e a garagem como no §3.3, item 2. Bench, referências, `playtest-save`, Electron e spike rodam sob a vaga 2 | `79` · `1806` |
| K5 | 1 | vite 5605 | Nenhuma. A trava do cartão (`/tmp/nc-captura.lock`) é a vaga 1 | `79` · `1794` |

### 2.4 Capturas: no máximo 3 ao mesmo tempo, em 3 vagas

| Vaga | Trava | Quem usa |
|---|---|---|
| 1 | `/tmp/nc-captura.lock` | K5 |
| 2 | `/tmp/nc-captura-2.lock` | K4 |
| 3 | `/tmp/nc-captura-3.lock` | K1, K2 e o orquestrador nas mesclas |

- **Vai sob a trava** todo comando que abre o Chromium com WebGL ou o Electron:
  - `tools/capture.mjs`, `tools/landmark-sheet.mjs`, `tools/render-harness.mjs`;
  - `tools/bench.mjs`, `tools/referencias.mjs`, `tools/spike-webgpu.mjs`;
  - `scripts/playtest*.mjs`, `scripts/pistas-ui.mjs`;
  - `scratch/k2-*.mjs`;
  - `xvfb-run … electron`.
- **Fica fora da trava:** vitest, `scripts/art-sheet.ts`, `tools/svg2png.mjs` (Chromium sem WebGL), `tools/tile.mjs`, `shape-to-track`, `landmark-sight` e `balance`.
- **Formas de usar:**
  - comando simples: `flock /tmp/nc-captura-3.lock node scripts/playtest.mjs http://localhost:4181/ scratch/k1-antes`;
  - com variável de ambiente: `PORT=5605 flock /tmp/nc-captura.lock node tools/capture.mjs "…"`;
  - em laço: a trava vai **em cada comando, dentro do laço**.
- **Rode com `run_in_background`:** o comando pode ficar esperando a vez.
- **Quem não captura:** a K3 nunca; o K0 só nas mesclas. No fecho a máquina fica livre: não há trava, e as capturas rodam uma de cada vez.
- **Por que três travas e não uma.** Com uma trava só, uma captura de ~50 s da K5 esperaria o bench e as referências da K4, que levam 10–25 min cada. Com uma vaga para cada frente que captura muito, e uma dividida entre as que capturam só no fim, o teto de 3 vale sem que uma frente precise ver as outras.

---

## 3. Donos dos arquivos, portas e divergências

### 3.1 Dono de cada arquivo tocado na onda

Regra geral: agente não edita doc compartilhado. Ele manda o parágrafo pronto, e o K0 cola depois da mescla da frente.

| Arquivo | Dono | Quem mais toca, e como entra |
|---|---|---|
| `src/core/types.ts` | K0, só no A6 | Ninguém edita depois do push. K3, K4 e K5 só leem |
| `src/core/serialize.ts` | K3: 2 linhas de comentário e 1 `delete` | Ninguém grava padrão de `aiPace`. Conferência: `grep -cE "aiPace *(=\|\?\?)" src/core/serialize.ts` → `0` |
| `src/core/sim/ai.ts`, `src/core/career.ts`, `src/game/career-session.ts` | K3 | A K4 não importa `createBrain` |
| `src/core/constants.ts` | **ninguém** | — |
| `src/core/track/builder.ts`, `src/core/track/tracks.ts` | K5 | — |
| `src/game/contracts.ts` | K1, **só a l. 387** | K0, na ligação K1+K2: `\| 'survey'` em `MenuScreen` (l. 339). Ninguém cria `MenuEvent` |
| `src/game/session.ts` | K1 (nenhuma mudança esperada) | K0, na ligação K1+K2: as chamadas da K2 (§4, passo K2-d) |
| `src/game/save.ts` | K1 (`NewRecord`, `recordRaceResults`) | — |
| `src/game/playlog.ts`, `src/game/race-feel.ts` | K2 | K0, no passo K2-d: `startQuick` e `startCup` entram no `SURVEY_GATE_EVENTS` |
| `src/game/errors.ts`, `src/game/ghost-store.ts` | K2 (as guardas) | — |
| `src/game/desktop.ts` | K4 (`benchWrite`, `openFolder`) | Ninguém mais |
| `src/main.ts` | K4 (desvio do bench) | K0, depois da K4: o diário no boot (M3 do cartão K2), fora do desvio |
| `src/bench/*`, `src/render/renderer.ts` (só contadores) | K4 | — |
| `src/ui/menus.ts` | K1 (`keyNav`, `onKeyDown`) | K0, na ligação K1+K2: `survey` em `FACTORIES` e o import; `getActivePlaylog()?.input()` em 3 pontos (§4, K2-b) |
| `src/ui/strings.ts` | K1 | — |
| `src/ui/screens/lobby.ts`, `simple.ts`, `tutorial.ts`, `tutorial.css`, `results.ts`, `records.css` | K1 | `records.css`: o K0 no C2, se corrigir as reticências (T6) |
| `src/ui/screens/survey.ts`, `survey.css`, `src/playlog/strings.ts` | K2 | — |
| `src/errors/options.ts` | K2 | K0, na ligação K2+K4: o item "Abrir pasta" |
| `src/errors/strings.ts` | K2 | K0, no mesmo passo: `options.openFolder` em PT e EN |
| `src/errors/errors.css` | K2 (comentário da l. 63) | K0, no mesmo passo: `.options-footer.has-folder` |
| `desktop/main.cjs`, `desktop/preload.cjs`, `desktop/storage.cjs` | K4 | A trilha S, se abrir, entra depois da mescla da K4 |
| `desktop/package.json`, `desktop/package-lock.json`, `desktop/teste/*`, `desktop/steam_appid.txt` | K0 (C; o App ID no A) | Os rótulos da K2 entram no C4 |
| `desktop/README.md`, `desktop/e2e.mjs` (só os comentários das l. 6 e 68) | K0 cola | Parágrafos da K2 e da K4 |
| `scripts/playtest.mjs` | K1 | K0, na ligação K1+K2: URL padrão da l. 5 → `'http://localhost:4174/?nosurvey=1'` |
| `scripts/playtest-assist.mjs` | K1 (l. 85 e 90) | — |
| `scripts/playtest-save.mjs` | K4 (só o `window.desktop` falso) | — |
| `scripts/playtest-layout.mjs` | K0, nas ligações | Uma entrada `survey` e o `NC_LAYOUT_DESKTOP` (§4, K2-f) |
| `scripts/playtest-memoria.mjs` | K0, na ligação K1+K2 | URL padrão da l. 20 com `?nosurvey=1` |
| `scripts/balance.ts`, `scripts/race-feel-base/*.json` | K2 | A K5 só roda o `balance`. O K0 regrava os JSON depois de K2 + K5 (§4, pós-mescla) |
| `scripts/career-balance-lib.ts`, `scripts/career-balance.ts` | K3 (mantém `PROXY_SKILL = 0.97`) | K2 e K5 só importam |
| `scripts/shape-to-track.ts` | K5 | O K0 só lê (o T2 importa `fitArt`/`selfCrossings`). Depois de K5 + `nc/k-k0`, o K0 põe a `cuia_gaucha` no `ART_CFG` |
| `scripts/track-art.ts` | K0 (A: `cristo()`; B: 9 desenhos) | A K5 só lê `ART.copacabana` |
| `scripts/art-sheet.ts`, `scripts/sim-scenarios.ts`, `scripts/track-shapes.ts` | ninguém edita | — |
| `tools/bench.mjs`, `tools/referencias.mjs`, `tools/render-harness.html` | K4 | — |
| `tools/capture.mjs`, `tile.mjs`, `svg2png.mjs`, `landmark-sight.ts` | ninguém edita | Só usar |
| `tests/contratos-onda-k.test.ts` | K0 (A) | — |
| `tests/track-art.test.ts` | K0 (A: T2a; B: T2b) | — |
| `tests/track.test.ts`, `tests/batidas-cenario.test.ts` | K5 | — |
| `tests/sim-golden.test.ts` (`EXPECTED`) | K5: **duas** linhas, `solo-sem-assistencias` e `versus-cambio-manual` | Ninguém mais |
| `tests/ai-pace.test.ts`, `tests/career-probe.test.ts`, `tests/career-balance.test.ts` | K3 | — |
| `tests/fluxo-sem-atrito.test.ts`, `tests/fluxo-sessao.test.ts`, `tests/ui.test.ts` | K1 | — |
| `tests/race-feel.test.ts`, `tests/playlog.test.ts`, `tests/playlog-fixtures.ts`, `tests/survey.test.ts`, `tests/storage-full.test.ts` | K2 | — |
| `tests/errors.test.ts` | K2 (+1 teste) | K0, na ligação K2+K4 (+1: "Abrir pasta") |
| `tests/bench.test.ts`, `tests/desktop-storage.test.ts` | K4 | — |
| `tests/sessao-diario.test.ts` (novo) | K0, na ligação K1+K2 | — |
| `tests/i18n.test.ts`, `tests/helpers.ts`, `tests/paint-screens.test.ts`, `tests/career-session.test.ts`, `tests/session-controls.test.ts` | ninguém edita | Copiar trechos, nunca editar |
| `docs/ondas/K.md`, `L.md`, `M.md`, `N.md` | K0 | — |
| `docs/DIRECAO-DE-ARTE.md` | K0 | A: câmeras. B: bíblia. Depois de K4 **e** `nc/k-k0`: o parágrafo do spike na §6. Depois de K5 + `nc/k-k0`: o segmento do Cristo junto da câmera 1 |
| `docs/CONTRATO-DO-ASSET.md`, `ARTE.md`, `VISUAL.md`, `DESIGN.md`, `ARQUITETURA.md` | K0 (B) | — |
| `README.md`, `docs/ROADMAP.md`, `docs/legal/EULA.md` | K0 (A: licença; B: alvo novo) | O rótulo da K2 (`EULA.md:72`) é colado depois da mescla |
| `package.json` | K0 (A: licença; B: descrição; C: versão) | — |
| `package-lock.json`, `server/package.json`, `server/package-lock.json` | K0 (A, se a nº 2 for (a); C: versão) | — |
| `docs/QA.md` | K0 (A: §5.1, l. 88 e 93; C: §4) | Rótulos da K2 (l. 6, 120, 175 e um passo novo depois do 22) entram no C4 |
| `docs/CRONOGRAMA.md`, `CLAUDE.md` | K0 (A e C) | Parágrafos: K2 e K4 em "Comandos"; K5 na l. 52 |
| `docs/PISTAS.md` | K0 cola | Parágrafos da K5 e do B9, a seção "Balanceamento" da K2 e os números do encaixe da cuia |
| `docs/TELAS.md`, `TUTORIAL.md`, `ESTATISTICAS.md` (K1) · `CARREIRA.md` (K3) · `SAVE.md`, `legal/PRIVACIDADE.md` (K2) · `DESEMPENHO.md`, `PLAYBOOK.md` §1 (K4) | K0 cola | Parágrafo de cada frente, depois da mescla dela |

### 3.2 Portas

Esta tabela substitui a do K0 (§3, "Portas"): é a que os cartões das frentes de fato usam.

| Quem | vite de desenvolvimento | `vite preview` | Outros |
|---|---|---|---|
| K0 (orquestrador) | 5600 | 4180 | relay 8787, só no fecho |
| K1 | 5601 (só se precisar) | 4181 | — |
| K2 | 5612 | — (os passos M dela são do K0, na 4180) | — |
| K3 | 5603 (não precisa) | — | — |
| K4 | 5641 (o spike também); 5642 (worktree "antes", `/tmp/nc-k4-antes`) | 5643 | — |
| K5 | 5605 | — | — |

- **Passe sempre `PORT=` ao `capture.mjs`.** Sem ele, a ferramenta usa a 5601 (l. 9), que é o vite da K1.
- **Passe sempre a URL aos playtests.** Sem ela, eles usam a 4174.
- **Relay nos testes:** cada teste sobe o próprio relay numa porta livre.

### 3.3 Divergências entre os cartões

Em todos os itens, vale o que está escrito nesta abertura.

1. **Ponte "Abrir pasta".**
   - O K0 (T3, A10, tabela de donos) usa `openDataFolder`/`shell:openDataFolder` e põe o K0 editando `desktop.ts` e `preload.cjs`. A K2 (§2 item 3, M0, M5 e M6) usa `openSavesFolder`.
   - **Vale a K4:** `openFolder(kind: 'saves' | 'logs' | 'bench')` no canal `folder:open`, mais `benchWrite` no canal `bench:write`. A ponte inteira é da K4.
   - O botão é o do M5 do cartão K2, com estas trocas:
     - no código: `void desktop.openFolder('saves').catch(() => false);` no lugar de `openSavesFolder()`;
     - no teste: `const opened: string[] = [];` e `openFolder: (kind: string) => { opened.push(kind); return Promise.resolve(true); }`, afirmando `expect(opened).toEqual(['saves'])`.
   - **O T3 do K0 sai.** Os passos 1–3 e os 2 testes dele são cobertos pelos testes de ponte da K4 (`desktop-storage`, 18) e pelo teste único do M5.
   - O grep do M0 da K2 passa a ser `grep -n "openFolder\|folder:open" src/game/desktop.ts desktop/preload.cjs desktop/main.cjs scripts/playtest-save.mjs`, com uma linha ou mais em cada arquivo.

2. **Câmeras de referência.**
   - A K4 (passo 14b) usa Sampa no seg. 70, Rota 66 e Lapônia, e a garagem com 1 piloto.
   - **Vale a bíblia (K0, A8).** Os segmentos foram conferidos com `landmark-sight`: MASP @1029, cânion @158, Matterhorn @234. No topo de `tools/referencias.mjs`:
     ```js
     const CAMERAS = [
       { id: 'copa', track: 'copacabana', seg: 70 },       // a entrada da fatia da L; não segue o Cristo
       { id: 'foz', track: 'foz_do_iguacu', seg: 105 },    // Cataratas @182
       { id: 'sampa', track: 'sampa_noite', seg: 970 },    // MASP @1029 (na largada a captura sai preta)
       { id: 'deserto', track: 'xingo', seg: 110 },        // cânion @158
       { id: 'neve', track: 'passo_alpino', seg: 180 },    // Matterhorn @234
     ];
     ```
   - A captura `garagem` usa a receita da tela `garage-2` (`scripts/playtest-layout.mjs:116–120`): 2 pilotos (`kb1`/`kb2`, `falcao`/`tornado`) e o `ArrowLeft`. Depois, espera `s.menus.current() === 'garage'` e mais 1500 ms. A `escolha` continua a `lobby-1`.
   - O M4 do K0 confere com `grep -cE "track: '(copacabana|foz_do_iguacu|sampa_noite|xingo|passo_alpino)', seg: (70|105|970|110|180)\b" tools/referencias.mjs` → `5`.

3. **`EXPECTED` do sim-golden.** O K0 (desvio 3, M2, tabela) espera só `solo-sem-assistencias`. **Vale a K5:** mudam também `versus-cambio-manual`. As outras 6 ficam iguais.

4. **Conferência do `serialize.ts` depois da K3.** O K0 (M1, §6) espera `grep -n "aiPace"` → nada. **Vale a K3:** `grep -cE "aiPace *(=|\?\?)" src/core/serialize.ts` → `0`. O arquivo passa a ter 2 linhas com `aiPace`: o comentário e o `delete`.

5. **Textos do questionário.** O K0 diz `registerStrings('survey', …)` em `src/errors/strings.ts`. **Vale a K2:** `src/playlog/strings.ts`, namespace `playlog`.

6. **`contracts.ts` e `simple.ts`.**
   - A K1 edita só a l. 387 do `contracts.ts` e **não** cria `MenuEvent`.
   - O `src/ui/screens/simple.ts` também é da K1; a tabela do K0 não o lista.

7. **T4 e T4b do K0 × M1–M4 da K2.**
   - São a mesma ligação: faça uma vez só, pelos passos K2-b a K2-d do §4.
   - A entrada `survey` do `playtest-layout.mjs` vai uma vez só, logo depois de `['options', …]` (l. 144), e não antes do `].filter(`.
   - O T4b continua sendo o teste escrito antes do M4, com o **gatilho corrigido**: o questionário não abre no fim da 3ª corrida. Ele abre no próximo evento-portão depois dela (`toMain`, `retryRace`, `nextRace`…).

8. **Contador de entradas no `menus.ts`.**
   - O M2 da K2 manda pôr `getActivePlaylog()?.input();` "logo depois de `if (!m) return;`", mas essa linha some com a K1. **Vale:** logo depois de `if (!nav) return;` no `onKeyDown` da K1.
   - O `noteMenuInput(device)` citado no cartão K1 não existe. A API é `getActivePlaylog()?.input()`.

9. **`?nosurvey=1`.** A K2 esperava que a K1 mudasse a URL padrão de `scripts/playtest.mjs:5`, e o cartão K1 não muda. **Vale:** o K0 muda na ligação K1+K2. No fecho, todo playtest recebe a URL com `?nosurvey=1`; todos usam `page.goto(url)` direto, conferido.

10. **Portão do questionário.** Depois da K1, o `results.ts` emite também `startQuick` ("MAIS UMA?") e `startCup` ("RECOMEÇAR A COPA", "PRÓXIMA COPA"). Os dois entram no `SURVEY_GATE_EVENTS`. Avise o dono: o questionário pode aparecer também ao largar pelo lobby, uma vez por sessão e por dia.

11. **Portas nos passos M da K2.** Troque `4174` por `4180` em M5–M8, inclusive no `p.goto` do `scratch/k2-m8.mjs`. Passe a URL ao `playtest-save.mjs`.

12. **Linha de base do termômetro depois da K5.**
    - O M9 da K2 espera mudança em 5 pistas. A K5 muda traçado ou placas de 24; pode mudar qualquer uma delas e nenhuma outra (comando no §4).
    - Dentro de `nitro-crew/`, `git show HEAD:<caminho>` precisa de `./` (`HEAD:./scripts/…`).

13. **Pedido do bench ao dono.** O M4 do K0 pede o bench na mescla da K4. Mas o jogo do dono só ganha `--bench` no pacote 0.1.1. O pedido vai com a 0.1.1 (C10).

14. **Grep dos textos no C4.** Precisa de `-i`, porque os botões da K1 estão em CAIXA ALTA.

15. **Trava de captura.** O cartão K5 diz que K1, K2 e K4 usam a trava dela. **Vale o §2.4.**

16. **Base da suíte.** K1 e K2 mandam rodar a suíte de base no passo 1. **Vale o §2.3:** não rodam.

17. **Total do fecho.** O K0 diz "pelo menos 1842". **Vale o §5:** 1929 + os 2 testes do T4b + os do T6.

18. **Verificações da fase M do K0.** "Os 2 testes novos do T3" passa a ser **1**, o do M5: `tests/errors.test.ts` vai a 43. "`openDataFolder` no cartão da K4" passa a ser `folder:open`.

---

## 4. Ordem de mescla

### 4.1 Regras

1. **Uma mescla por vez.** Siga o M0 do K0: `git status` limpo em `nitro-crew/`, `git merge --no-ff`, `npx tsc --noEmit -p .` sem saída e os testes da área.
2. **A ordem é a de chegada**, com estas restrições:
   - **K2 só depois de K1 e K4.** As ligações dela usam o `keyNav` da K1 e a ponte da K4. Se a K2 chegar antes, espere.
   - **`nc/k-k0` só com `npx vitest run tests/track-art.test.ts` → `61 passed (61)` no worktree.**
   - **Prefira K5 antes de K3.** Assim o `career-balance` já sai conferido sobre o traçado novo. Se a K3 chegar antes, repita o teste depois da K5 (pós-mescla).
3. **Depois de cada mescla:**
   - cumpra o "Pronto quando" da frente;
   - cole os parágrafos dela nos docs;
   - commite.
   - **Não faça push até o C8.**
4. **Capturas e playtests nas mesclas** vão sob a vaga 3 enquanto houver agente rodando.

Ordem ideal: **K5 → K3 → K1 → K4 → K2 (com as ligações) → `nc/k-k0` → pós-mescla.**

### 4.2 O que conferir depois de cada mescla

**`nc/k-k5`**
- `npx vitest run tests/track.test.ts tests/batidas-cenario.test.ts tests/sim-golden.test.ts tests/track-art.test.ts` → 0 failed. Dá `157 passed` sem o `nc/k-k0`, e `212 passed` com ele.
- Impressões digitais contra o `532d957`:
  ```bash
  npx tsx scripts/perf-sim.ts --fingerprints | grep -o "fingerprint: '[0-9a-f]*'" > scratch/k0-fp.txt
  git show 532d957:nitro-crew/tests/sim-golden.test.ts | grep -o "fingerprint: '[0-9a-f]*'" | diff - scratch/k0-fp.txt
  ```
  A saída tem de ser exatamente os blocos `1c1` (`5553cf13` → `368c98f8`) e `3c3` (`1fbd037d` → `56beefb8`).
- `grep -rln "Object.keys(ART)" tests` → só `tests/track-art.test.ts`.
- `grep -n "export const ART_CFG" scripts/shape-to-track.ts` → a linha ainda termina em `= {};`.
- Cole os parágrafos em `docs/PISTAS.md` (L15, L148, L306, L316) e em `CLAUDE.md:52`.

**`nc/k-k3`**
- `npx vitest run tests/ai-pace.test.ts tests/career-probe.test.ts tests/career-balance.test.ts tests/determinism.test.ts tests/sim-golden.test.ts tests/contratos-onda-k.test.ts` → `Tests  38 passed (38)`.
- `grep -cE "aiPace *(=|\?\?)" src/core/serialize.ts` → `0`.
- `grep -n "aiPace" src/core/sim/race.ts` → nada.
- `git diff HEAD^1 HEAD -- tests/sim-golden.test.ts` → vazio.
- Se a K5 já entrou, o `career-balance` acima já vale sobre o traçado novo. Se `br_pe` ou `br_pb` sair de 2,5–5,5, recalibre só `CAREER_AI_PACE_POINTS` (K3, armadilha 17).
- Cole o parágrafo em `docs/CARREIRA.md`.

**`nc/k-k1`**
- `npx vitest run tests/fluxo-sem-atrito.test.ts tests/fluxo-sessao.test.ts tests/ui.test.ts tests/paint-screens.test.ts tests/tutorial.test.ts tests/i18n.test.ts` → 0 failed (15, 1 e 30 nos três primeiros).
- Se a K3 já entrou: `grep -rn "careerAiLevel(career)" src/game` → só `career-session.ts`, com a linha do `aiPace` logo abaixo.
- Cole os parágrafos em `docs/TELAS.md`, `TUTORIAL.md` e `ESTATISTICAS.md`.
- Anote a pendência das reticências (`records.css:280`) para o C2.

**`nc/k-k4`**
- `npx vitest run tests/bench.test.ts tests/desktop-storage.test.ts tests/i18n.test.ts tests/assist.test.ts` → 0 failed (16 e 18 nos dois primeiros).
- O grep de `CAMERAS` (§3.3, item 2) → `5`.
- `git branch --list nc/k-k4-spike-webgpu` existe e **não** é mesclada.
- Guarde o parágrafo do spike. Ele só entra depois do `nc/k-k0`.
- Cole os parágrafos em `docs/DESEMPENHO.md` §4 (roteiro do bench), `desktop/README.md`, `docs/PLAYBOOK.md` §1 e `CLAUDE.md` ("Comandos").

**`nc/k-k2` e as ligações** (no mesmo turno, nesta ordem)
- **K2-a.** Mescla. Depois: `npx vitest run tests/race-feel.test.ts tests/playlog.test.ts tests/survey.test.ts tests/errors.test.ts tests/storage-full.test.ts tests/i18n.test.ts` → 0 failed (23 + 42 + 12 nos cinco primeiros).
- **K2-b** (= T4 do K0 = M1 e M2 da K2):
  1. Ponha `| 'survey'` em `MenuScreen`. Rode o `tsc` e veja o erro único `src/ui/menus.ts(40,7): error TS2741`.
  2. Registre `survey: surveyScreen` em `FACTORIES` e importe a tela. O `tsc` fica limpo.
  3. Ponha `getActivePlaylog()?.input();` em três pontos do `menus.ts`:
     - logo depois de `if (!nav) return;` no `onKeyDown`;
     - como 1ª linha de `onPointerDown`;
     - logo depois da guarda do `navigate`.
- **K2-c.** O M3 da K2 (`main.ts`).
- **K2-d** (T4b e depois M4):
  1. Escreva `tests/sessao-diario.test.ts`, com 2 testes, no arnês de `tests/fluxo-sessao.test.ts` (K1).
     - No `beforeEach`: `log = createPlaylog(fakeDeps().deps); log.sessionStart(); setActivePlaylog(log);`. O `fakeDeps` vem de `tests/playlog-fixtures.ts`.
     - No `afterEach`: `setActivePlaylog(null)`.
     - **Teste 1.** Uma corrida rápida terminada, do jeito do T17: carros na reta final, `debugStep`, `frame()` até `results`. Depois, `log.data().races` tem 1 corrida com `abandoned: false`, e `sessions[0].funnel?.mode` é `'quick'`.
     - **Teste 2.** Duas corridas, emendadas por `handleMenuEvent({ type: 'retryRace' })`. Um 3º `retryRace` **não** abre `'survey'` e larga a 3ª corrida. Depois que ela termina, `handleMenuEvent({ type: 'toMain' })` deixa `menus.current()` em `'survey'`.
  2. Veja os dois falharem (diário vazio; `'main'` no lugar de `'survey'`).
  3. Faça o M4 da K2 em `session.ts`.
  4. Rode `grep -o "type: '[a-zA-Z]*'" src/ui/screens/results.ts | sort -u` e acrescente ao `SURVEY_GATE_EVENTS` o que faltar. Hoje são `startQuick` e `startCup`.
  5. `npx vitest run tests/sessao-diario.test.ts tests/fluxo-sessao.test.ts tests/session-controls.test.ts tests/career-session.test.ts tests/raceEnd.test.ts tests/paint-session.test.ts tests/playlog.test.ts` → 0 failed.
- **K2-e.** O M5 da K2 com `openFolder('saves')` (§3.3, item 1). Primeiro o teste, visto falhar com `3 !== 4`; depois o código, os textos e o CSS. `tests/errors.test.ts` → `43 passed`.
- **K2-f.**
  - Em `scripts/playtest-layout.mjs`: a entrada `survey` depois de `['options', …]`, e o `NC_LAYOUT_DESKTOP` do M6 com `benchWrite: () => Promise.resolve(null), openFolder: ok` no lugar de `openSavesFolder: ok` (16 funções).
  - `scripts/playtest-memoria.mjs:20` e `scripts/playtest.mjs:5` → `'http://localhost:4174/?nosurvey=1'`.
- **K2-g.** `npm run build` e `(npx vite preview --port 4180 --strictPort > scratch/k0-preview.log 2>&1 &)`. Depois, sob a vaga 3:
  - `NC_LAYOUT_ONLY=survey NC_LAYOUT_SKIP_HUD=1 node scripts/playtest-layout.mjs http://localhost:4180/ scratch/k0-survey` → `14/14 combinações limpas` e exit 0;
  - o M7 e o M8 da K2 na porta 4180 → `tudo verde`, `TUDO OK`, `playtest OK`, memória sem `✗`;
  - `NC_LAYOUT_DESKTOP=1 NC_LAYOUT_ONLY=options …` → `tudo verde`. Se der vermelho, só o M5 fica BLOCKED (cartão K2).
- **K2-h.** Cole os parágrafos da K2: `docs/SAVE.md`, `legal/PRIVACIDADE.md`, `legal/EULA.md:72`, `docs/PISTAS.md` ("Balanceamento"), `desktop/README.md`, os comentários de `desktop/e2e.mjs` e `CLAUDE.md` ("Comandos"). Os rótulos de `docs/QA.md`, `LEIA-ME.txt` e `release.md` ficam para o C4.

**`nc/k-k0`**
- `npx vitest run tests/track-art.test.ts` → `61 passed (61)`, no worktree antes e no checkout principal depois.
- Se a K5 já entrou: `npx vitest run tests/track-art.test.ts tests/track.test.ts` → `195 passed`.

### 4.3 Pós-mescla (com todas as frentes dentro)

1. **Cuia** (K5 e `nc/k-k0`): o M7 do K0.
2. **Spike** (K4 e `nc/k-k0`): troque "a preencher na mescla da K4" na §6 da bíblia pelo parágrafo. Confira com `grep -c "a preencher na mescla da K4" docs/DIRECAO-DE-ARTE.md` → `0`.
3. **Linha de base do termômetro** (K2 e K5): regrave as 3 sementes com os comandos do passo 13 da K2 e confira:
   ```bash
   node -e "const fs=require('fs'),cp=require('child_process');const ok=new Set('copacabana sampa_noite pampulha convento_penha curitiba floripa brasilia bonito transpantaneira porto_seguro aracaju maceio olinda campina_grande cajueiro_pirangi serra_capivara sao_luis belem manaus macapa monte_roraima porto_velho rio_branco palmas'.split(' '));for(const s of [11,12,13]){const f='scripts/race-feel-base/profissional-'+s+'.json';const a=JSON.parse(cp.execSync('git show HEAD:./'+f)).tracks,b=JSON.parse(fs.readFileSync(f)).tracks;const ch=Object.keys(b).filter((k)=>JSON.stringify(a[k])!==JSON.stringify(b[k]));console.log(s,'mudaram',ch.length,'| fora das 24:',ch.filter((k)=>!ok.has(k)).join(' ')||'nenhuma','| copacabana e maceio:',['copacabana','maceio'].every((k)=>ch.includes(k)))}"
   ```
   As 3 linhas têm de mostrar `fora das 24: nenhuma` e `copacabana e maceio: true`. Commite.
4. **Autobahn** (depois da K3): `npm run balance -- corrida profissional 11 autobahn` tem de conter `emoção ult/min 7.33 disputa 52% sozinho 17% fundo 85% nitro 3.3% raspões 20 traseiras 15`. Se mudou, regrave as 3 sementes e avise no fecho.
5. **Carreira** (K3 e K5): `npx vitest run tests/career-balance.test.ts` → `Tests  3 passed (3)`.
6. **Bench com tudo mesclado.** Suba o vite: `(npx vite --port 5600 --strictPort > scratch/k0-vite.log 2>&1 &)` e `until curl -s -o /dev/null http://localhost:5600/; do sleep 1; done`. Depois:
   `node tools/bench.mjs 5600 "cenas=copa-1p,copa-4p&q=low,high&frames=2&warm=1" scratch/k-bench-mescla.json` → `ok scratch/k-bench-mescla.json (4 corridas, 0 problemas)`.
7. **Cristo:** `npx tsx tools/landmark-sight.ts copacabana`. Anote na bíblia, junto da câmera 1, o segmento do Cristo (a câmera fica no seg. 70).
8. **Cartões L, M e N:** o M8 do K0.

---

## 5. Fecho da onda

Faça na ordem, com a máquina livre, e marque cada item.

- [ ] **0. Tudo mesclado.**
  - `git branch --no-merged HEAD | grep "nc/k-"` → só `nc/k-k4-spike-webgpu`.
  - O §4.3 está feito.
  - `ls docs/ondas` → `K.md  L.md  M.md  N.md`.
- [ ] **1. Máquina livre:** nenhum agente rodando, e `ps -eo pid,args | grep -E "[v]itest|[v]ite |[c]hrome|[e]lectron|[r]elay.mjs"` → nada.
- [ ] **2. Suíte e build (C1).** Em segundo plano: `NC_REQUIRE_RELAY=1 npx vitest run --testTimeout=180000 2>&1 | tail -6`. Esperado:
  - `Test Files  88 passed (88)`;
  - `Tests  1931 passed (1931)`, mais os testes do T6, se já houver.
  - A conta é 1785 + K1 17 + K2 25 + K3 16 + K4 21 + K5 9 + B 55 + M5 1 + T4b 2.

  Depois, `npm run build` → exit 0. Qualquer vermelho é defeito de integração: o teste que falhou já é o teste (T6).
- [ ] **3. Impressões digitais.** O bloco do §4.2 (K5) dá exatamente os blocos `1c1` e `3c3`, e `npx vitest run tests/sim-golden.test.ts` → `13 passed`.
- [ ] **4. Estabilização (C2).** Siga o C2 do K0, com duas trocas:
  - **URLs:** em todo playtest, a URL é `"http://localhost:4180/?nosurvey=1"`, entre aspas e escrita em cada comando.
  - **Questionário:** confira com `node scratch/k2-m8.mjs` → `TUDO OK`, se o arquivo ainda existir.
  - **Lente render:** o `playtest-layout` sai vermelho nas 14 combinações de `results`, com a regra `[reticências]` (`records.css:280`, defeito anterior à K). O playtest vermelho já é o teste (T6): corrija em `src/ui/screens/records.css` até `results` sair limpo. Se não couber no meio dia, registre e feche com DONE_WITH_CONCERNS.
  - Cada playtest tem de sair com `echo $?` → `0`.
- [ ] **5. Versão (C3, T5).**
  - `npm version 0.1.1 --no-git-tag-version`.
  - `npx vitest run tests/desktop-package.test.ts` → vermelho.
  - `(cd desktop && npm version 0.1.1 --no-git-tag-version)` → verde.
  - `node -e "console.log(require('./package.json').version, require('./desktop/package.json').version)"` → `0.1.1 0.1.1`.
- [ ] **6. Textos (C4).**
  - Pegue os nomes exatos com `grep -in "recomeçar\|mais uma\|primeira marca" src/ui/strings.ts` e `grep -in "relatório\|abrir pasta" src/errors/strings.ts`.
  - Incorpore os rótulos da K2 em `desktop/teste/LEIA-ME.txt:30`, `desktop/teste/release.md:12` e `docs/QA.md` (l. 6, 120, 175, mais o passo novo depois do 22).
- [ ] **7. Suíte final (C5).** Repita o item 2 depois das correções e da versão: 0 failed. **Anote o total**; ele vai para o `CLAUDE.md`.
- [ ] **8. Pacote (C6).**
  - `./build-all.sh --e2e` termina em `==> pronto`.
  - Abra `desktop/release/e2e-02-toast-options.png` com o Read: tem de mostrar "Abrir pasta" sem cortar o "Voltar".
- [ ] **9. Linha de base (C7).** O C7 do K0, mais o quadro completo da K4 com o código da 0.1.1, para a L montar o antes × depois:
  - `node tools/referencias.mjs captura 5600 scratch/ref k011` → 20 PNG e `scratch/ref/k011-medidas.json`;
  - inclua no zip: `zip -qj scratch/referencias-0.1.1.zip scratch/ref/k011-*`.

  Abra a folha com o Read: nenhuma captura pode sair em branco ou preta.
- [ ] **10. Commit, push e release (C8).**
  - `gh api repos/Felipebonamigo/padelizou/releases/tags/nitro-crew-teste-0.1.1 --jq '.assets[] | "\(.name) \(.size)"'` → 4 anexos com tamanho > 0: os dois pacotes, o LEIA-ME e o `referencias-0.1.1.zip`.
  - Upload recusado depois do `/root/.ccr/README.md` → **BLOCKED**, com o texto exato do erro.
- [ ] **11. Registros (C9), num commit só de docs, com push.** Na mensagem do commit, diga que a suíte do C5 continua valendo.
  - `CLAUDE.md`:
    - **Onde paramos** → "onda K concluída em dd/mm/2026; próximo: abrir a L (`docs/ondas/L.md`) quando os números do bench chegarem";
    - "Estado atual" (l. 79–99) com a onda K;
    - a contagem da l. 96 ("1776 testes") passa ao total do C5.
  - `docs/CRONOGRAMA.md:209`: "### Onda K…" ganha "concluída em dd/mm/2026 (commit `<sha7 do C8>`)".
  - `docs/DESEMPENHO.md`: os números do bench do dono, se já vieram.
- [ ] **12. Pronto da onda** (CRONOGRAMA, "Onda K", "Pronto quando"). Cada linha tem a sua prova:

  | Pronto | Prova |
  |---|---|
  | Menu → contagem em ≤ 4 entradas; resultado → largada em ≤ 2 | `scratch/k1-depois.txt` (relatório da K1): `entradas menu→contagem … : 4` e as linhas "em N entradas" |
  | Três testes vistos falhar | T1, T9 e T14 + T16, com as mensagens no relatório da K1 |
  | `br_pe` e `br_pb` entre 2,5 e 5,5 | `scratch/k3-falha-3.txt` (antes) e o item 5 do §4.3 (depois) |
  | `sim-golden` igual com `aiPace` ausente | O `git diff` da mescla da K3, vazio |
  | Cristo com erro ≤ 1,0 e fechamento ≤ 3 | `npx tsx scripts/shape-to-track.ts copacabana --current --quiet` → `desvio de fechamento 2.7`, e o teste de fidelidade (K5, T3) verde |
  | `perf-sim` | Item 3: 2 corridas (desvio 3 do §1.4) |
  | Bench com JSON válido | `scratch/k4-bench-swiftshader.json` e o item 6 do §4.3 |
  | Números do bench do dono | Mensagem do item 13 |
  | Decisão do spike | `docs/DIRECAO-DE-ARTE.md` §6 |
  | 0.1.1 entregue | Item 10 |

- [ ] **13. Mensagem ao dono (C10).** Uma folha só, com `SendUserFile` para as imagens. Leva:
  - **Capturas:**
    - Cristo antes × depois;
    - folha da rodada 1;
    - contorno × alvo;
    - folhas da K1 e da K5, rodapé da K2, quadro da K4;
    - as 7 câmeras.
  - **Números:**
    - entradas (8 → 4);
    - médias de PE e PB (1,67 e 1,17 → 3,67 e 3,33);
    - termômetro médio das 3 sementes.
  - **O link da release.**
  - **O pedido do bench**, no PC e no Deck, com o roteiro da K4 (§7, item 5).
  - **As decisões do Marco 1**, com a recomendação marcada:
    - aprovar a bíblia, o quadro de referências, a rodada 1 e o contrato do asset;
    - encomendar os style frames, o carro-herói de teste e o Cristo-régua.
  - **As ressalvas:**
    - recordes e fantasmas de 24 pistas passam a "versão anterior", e a `onlineFingerprint` muda;
    - o questionário custa 1 entrada entre corridas (exceção ao princípio 10) e pode aparecer também ao largar pelo lobby;
    - o diário conta o dia em UTC e vai para a Steam Cloud quando houver Steam;
    - o online ainda não funciona;
    - a rampa A × B, se o dono não respondeu;
    - o spike, se a decisão ficou "provisória até o bench do dono".

  Feche com **NEEDS_CONTEXT** se os números do bench ainda não vieram: sem eles a L não abre, e a mensagem diz isso. Com os números em mãos, feche com **DONE**, ou **DONE_WITH_CONCERNS** se sobrou ressalva do item 4 ou da lista acima.

---

Os cartões K0 a K5 seguem abaixo, nesta ordem.