# Playbook — como executar o Nitro Crew com qualquer modelo

Este é o manual para uma sessão (de qualquer modelo, inclusive os mais baratos) fazer o trabalho do jeito que vinha
sendo feito: mesmo processo, mesmas verificações, mesmos cuidados. Ele existe porque o dono pediu para seguir num modo
mais econômico com o mesmo resultado (06/10/2026). **Siga a ordem; não pule verificação.**

O que ler, nesta ordem, no começo de toda sessão:
1. `CLAUDE.md` (deste diretório) — regras do núcleo, comandos, estado do projeto, decisões e pendências do dono.
2. `docs/CRONOGRAMA.md` — as ondas a fazer; ache a primeira não concluída e o cartão dela em `docs/ondas/`.
3. `docs/DIRECAO-DE-ARTE.md` — se a tarefa mexe em qualquer coisa visível.
4. O documento da área (a lista está no `CLAUDE.md`, "Documentos por área").

---

## 1. Ambiente e comandos (copiar e colar)

Tudo roda de `nitro-crew/` (a pasta do jogo dentro do repositório `padelizou`; nada de fora dela é usado).

| Para quê | Comando |
|---|---|
| Instalar (sessão nova) | `npm ci` e `(cd server && npm ci)` (o `ws` do relay é exigido pela suíte completa) |
| Typecheck | `npx tsc --noEmit -p .` (ou `npm run typecheck`) |
| Suíte completa | `NC_REQUIRE_RELAY=1 npx vitest run --testTimeout=180000` (~4–6 min com a máquina livre) |
| Testes de uma área | `npx vitest run tests/<arquivo>.test.ts` (ex.: `tests/landmark*.test.ts tests/scenery*.test.ts`) |
| Build | `npm run build` |
| Impressões digitais (sim-golden) | `npx tsx scripts/perf-sim.ts --fingerprints` → atualize `EXPECTED` em `tests/sim-golden.test.ts` só se a mudança de jogabilidade for de propósito |
| Balanceamento (IA × IA) | `npm run balance -- corrida profissional <semente> [pista]` (sementes 11, 12, 13) |
| Servidor de desenvolvimento | `npx vite --port <porta> --strictPort &` (cada agente com a sua porta: 5601, 5611, 5612…) |

Ferramentas de ver e medir (todas versionadas em `tools/`, usam o Chromium em `/opt/pw-browsers/chromium-1194/chrome-linux/chrome`
com swiftshader; precisam do vite no ar):

| Ferramenta | Uso |
|---|---|
| Captura na corrida | `PORT=<porta> node tools/capture.mjs "nome::track=<pista>&seg=<n>&hud=0"` → `scratch/nome.png` (`humans=2` ou `4` = tela dividida) |
| Folha de marcos | `node tools/landmark-sheet.mjs <porta> scratch/x.png "ids=a,b&cols=2&cw=500&ch=320&zoom=1.6&el=0.04&az=0.3"` |
| Onde fica cada marco e quanto aparece | `npx tsx tools/landmark-sight.ts <pista>` (meta: 2,5 s perto/longe, 4 s horizonte) |
| Vista de frente de um marco (ASCII) | `npx tsx tools/front-view-measure.ts <marco>` |
| Juntar capturas lado a lado | `node tools/tile.mjs scratch/saida.png <colunas> a.png b.png …` |
| SVG → PNG | `node tools/svg2png.mjs entrada.svg saida.png` |
| Contorno de uma pista | `npx tsx scripts/track-outline.ts <pista> [--svg x.svg]` |
| Pistas com desenho | `npx tsx scripts/shape-to-track.ts <pista>` (relatório) · `--apply` (grava) · `--sheet scratch/desenhos.svg` (folha) |
| Desenhos em curva (alvo) | `CELL=420 npx tsx scripts/art-sheet.ts scratch/art.svg [ids…]` e `node tools/svg2png.mjs scratch/art.svg scratch/art.png` |
| Playtests de fluxo | ver `CLAUDE.md`, "Comandos" (`scripts/playtest*.mjs`, com `npm run preview` no ar) |

`scratch/` é ignorado pelo git: é só para saídas (capturas, folhas). Ferramenta útil vai para `tools/` ou `scripts/`,
com o modo de usar no cabeçalho do arquivo.

---

## 2. Regras que não se negociam

1. **Defeito vira teste ANTES da correção, e o teste é visto falhar pelo motivo certo** (não por erro de digitação).
   Se a correção saiu antes, apague-a e recomece. Para ver falhar contra o código antigo, rode o teste antes de editar, ou
   copie o arquivo antigo para `scratch/orig/` e aponte uma cópia do teste para ele.
2. **Nada vai para o remoto com teste vermelho.** O push só sai depois da suíte completa verde, rodada de uma vez, com
   a máquina livre (sem agentes rodando testes ao mesmo tempo).
3. **Mudança visual se prova com captura** (antes × depois, mesma câmera). "Deveria ficar bom" não conta.
4. **Núcleo determinístico** (`src/core`): nada de `Math.random`/`Math.sin…`/`Date.now`/`Map`/`Set` no estado;
   `tests/determinism.test.ts` cobra. Cosmético (cor, legenda, efeitos) fica FORA do estado da corrida.
5. **Dependência de produção é só `three`** (os addons do próprio pacote — `three/examples/jsm/...`, ex.: EffectComposer —
   não são dependência nova). Nada de pacote novo para o que três/DOM/WebAudio já fazem.
6. **Não gerar nada pela API do Meshy** (gasta créditos do dono). Modelos prontos CC0 baixados e convertidos podem.
7. **Textos visíveis em PT e EN** (`registerStrings`; `tests/i18n.test.ts`).
8. **Mudou pista ou física de propósito**: rode o `perf-sim --fingerprints`, confira que só mudaram as corridas que
   deviam e atualize `EXPECTED`; recordes e fantasmas antigos passam a "versão anterior" pela versão do conteúdo — é
   esperado, avise o dono.
9. **Três tentativas seguidas falhando: pare** e volte à causa raiz (reproduza, instrumente cada fronteira), em vez de
   tentar a quarta.
10. **Commits em português** com o rodapé de atribuição que a sessão exigir; um commit por bloco de trabalho;
    `git push -u origin <branch>` no fim (o hook de parada cobra commit e push).

---

## 3. Como executar uma onda (o procedimento que funcionou nas ondas A–J)

1. **Leia o cartão da onda** (`docs/ondas/<onda>.md`): objetivo, frentes, arquivos de cada frente, testes a escrever
   primeiro, critério de pronto, capturas pedidas.
2. **Decida o que paraleliza.** Duas frentes só rodam juntas se nenhuma depende da outra **e** os arquivos são
   disjuntos (o cartão diz). Máximo de **5 agentes ao mesmo tempo** (a máquina tem 4 núcleos: com mais, a suíte estoura
   o tempo e o trabalho fica lento). Regra de jogo central (física, IA, economia) = uma frente só.
3. **Lance cada frente** com `Agent` (`isolation: "worktree"`, em segundo plano) usando o modelo de instrução da seção 4.
4. **Enquanto rodam**, faça algo que não toque os mesmos arquivos (documentação, ferramenta, uma frente pequena).
5. **Mescle uma por vez**, na ordem em que chegam: `git merge --no-ff <branch> -m "…(rodapé)"`. Depois de cada uma:
   `npx tsc --noEmit -p .` e os testes da área dela. Conflito: mantenha as duas mudanças (raro; foi o `CLAUDE.md`).
   Cole no doc da área o parágrafo que o agente mandou no relatório (agentes não editam doc compartilhado).
6. **Depois de todas**: suíte completa de uma vez (em segundo plano, com `run_in_background`, e espere a notificação —
   não fique consultando) e `npm run build`. Falhou? É defeito de integração: siga a regra 1 (o teste que falhou já é
   o teste; ache a causa, corrija, rode de novo).
7. **Atualize** a contagem de testes e o estado no `CLAUDE.md`, marque a onda no `docs/CRONOGRAMA.md`, commit, push.
8. **Mostre ao dono**: capturas antes × depois (`SendUserFile`), resumo curto em português com o resultado primeiro, e
   termine com DONE / DONE_WITH_CONCERNS / NEEDS_CONTEXT / BLOCKED.

---

## 4. Modelo de instrução para um agente de frente

Copie e preencha os `<…>`. Tudo o que está aqui veio de problema real.

```
Você trabalha no jogo Nitro Crew, em `nitro-crew/` dentro do repositório (TypeScript + Three.js; leia
`nitro-crew/CLAUDE.md`, `nitro-crew/docs/PLAYBOOK.md` e <docs da área> antes de tudo). Responda e comente em português;
código em inglês.

## Ambiente (importante)
- O seu worktree nasce do `main` do padelizou, que NÃO tem `nitro-crew/`. Antes de tudo:
  `git fetch origin claude/top-gear-coop-game-ns9u5l && git checkout -b <nc/nome-da-frente> origin/claude/top-gear-coop-game-ns9u5l`.
- `node_modules`: crie links para os do checkout principal (`ln -s /home/user/padelizou/nitro-crew/node_modules nitro-crew/node_modules`
  e o mesmo para `server/node_modules`); não os commite.
- Vite na porta <porta> (`npx vite --port <porta> --strictPort`); encerre-o no fim.

## O pedido do dono
<frase do dono, palavra por palavra>

## Tarefa
<do cartão da onda: o que fazer, arquivos SEUS, critério de pronto>

## Regras
- Teste ANTES de cada mudança e visto falhar (CLAUDE.md, PLAYBOOK §2). Diga no relatório como o viu falhar.
- Só os seus arquivos: <lista>. NÃO edite <arquivos das outras frentes> nem docs compartilhados (docs/VISUAL.md etc.):
  mande no relatório um parágrafo pronto para eu colar.
- `npx tsc --noEmit -p .` e a suíte completa verdes (`NC_REQUIRE_RELAY=1 npx vitest run --testTimeout=180000`). Se
  só estourar tempo-limite com a máquina carregada, rode o arquivo sozinho e diga isso.
- Capturas antes/depois (mesma câmera) em `nitro-crew/scratch/<prefixo>-*.png`.

## Entrega
Commit no SEU branch (português, terminando exatamente com o rodapé de atribuição da sessão). Não faça push. Relatório:
branch e commit; o que mudou e por quê; tabela antes → depois quando houver número; testes novos e como falharam antes;
resultado de typecheck e suíte com números exatos rodados no mesmo turno; caminhos das capturas; ressalvas.
Termine com DONE / DONE_WITH_CONCERNS / BLOCKED.
```

---

## 5. Armadilhas que já custaram retrabalho (e o que fazer)

| Armadilha | O que acontece | O que fazer |
|---|---|---|
| Worktree nasce do `main` | `nitro-crew/` não existe nele | criar o branch da ponta de `claude/top-gear-coop-game-ns9u5l` (modelo §4) |
| Suíte com vários agentes rodando | estoura tempo: `cars` (beforeAll 600 s), `ghost`/`fuel` (30 s), `landmarks` "perLap" (120 s) | não é defeito: rode o arquivo sozinho; a suíte final só com a máquina livre |
| Pista mudou de traçado + marco mudou de tamanho | um marco cai abaixo de 2,5 s à vista (`landmarks-enquadramento`) — foi a Pampulha | suíte completa depois de mesclar TUDO; corrigir pela pegada do marco (a lagoa de 70×104 m segurava a praça longe) ou pelo traçado |
| Pista no `sim-golden` (copacabana, sampa_noite…) | `EXPECTED` muda | `perf-sim --fingerprints`, conferir que só elas mudaram, atualizar |
| Pista de mata com quinas curtas | passa de 900 triângulos por segmento (`scenery-forma`) | arredondar as quinas / curvas longas (a mata não nasce do lado de dentro de curva longa) |
| Superfície de luz (`k.light`, material `glow`) | tem UMA face: virada para dentro, nunca aparece (o lago da gruta) | `bothSides` para vidro, lago, cortina d'água |
| Marco "à vista" mas ilegível | lê como prédio (as Cataratas) | régua de leitura: `tests/front-view.ts` + teste em `tests/landmarks-leitura*.test.ts` (docs/VISUAL.md, "Leitura") |
| `cv` usado para imitar traço contínuo | cada `cv` entra e sai do zero: o desenho desvia e a curva pulsa | trecho de curva constante sem rampa (`bend`), ver docs/PISTAS.md, "Desenhos em cartum (em andamento)" |
| Captura no jogo saiu preta | pista noturna logo na largada, ou o harness não terminou | outro segmento; `page.setDefaultTimeout(240000)` sob carga |
| `scratch/` | é ignorado pelo git; a próxima sessão não tem o que ficou lá | ferramenta útil vai para `tools/`/`scripts/` |
| Hook de parada pede push com a suíte rodando | tentação de subir sem verificar | não suba: diga que espera a suíte e suba quando ficar verde |
| Arquivo grande para o dono | `SendUserFile` falha acima de ~150 MB; o proxy corta downloads em 150 MiB | release no GitHub; retomar download com `curl -C -` |
| Sessão local do dono | `claude remote-control` exige login com a conta do claude.ai (`/login`) | ensinar o `/login`; sem ele, as sessões não conversam |

---

## 6. Padrões de qualidade (o que é "fazer igual")

- **Resultado primeiro**, em português, curto. Número, unidade, código e texto de erro nunca são parafraseados.
- **Toda afirmação de "pronto", "corrigido" ou "testes passam" vem de um comando rodado naquele turno**, com o número.
- **Antes × depois** em toda mudança visível, mesma câmera, enviado ao dono.
- **Decisão que é do dono** (gosto, dinheiro, ordem das copas, trocar um símbolo): pergunte com a recomendação; o resto
  decida e diga em uma linha.
- **Escada do mínimo** (`/home/user/padelizou/CLAUDE.md`, "Quanto código o pedido merece"): reaproveite o que existe
  antes de criar; mas a escada encurta a solução, nunca a leitura.
- **Feche cada bloco** com DONE / DONE_WITH_CONCERNS / NEEDS_CONTEXT / BLOCKED.

---

## 7. Dicas para o modo econômico

- Use os cartões de `docs/ondas/`: eles já dizem os arquivos, a ordem e o critério de pronto — não re-derive.
- Uma frente de cada vez se estiver em dúvida; paralelizar é ganho de tempo, não de qualidade.
- Leia o arquivo inteiro antes de editar algo nele (diff pequeno no lugar errado é o segundo defeito).
- Visual: compare sempre com as capturas de referência do cartão e com `docs/DIRECAO-DE-ARTE.md`; se não parecer
  "jogo cartunesco de última geração", não está pronto.
- Ao ficar em dúvida entre duas leituras do pedido, pergunte ao dono com as duas opções e a recomendação.
