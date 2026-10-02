# Nitro Crew — build desktop (Electron) para Steam

O jogo é uma aplicação web (TypeScript + Three.js/WebGL, Vite) empacotada com Electron — o mesmo caminho de
Vampire Survivors (versões iniciais) e CrossCode na Steam. Esta pasta só embrulha o `../dist`; nada do jogo
mora aqui.

## Gerar o executável

**Tudo de uma vez** (build do jogo + todos os pacotes que esta máquina consegue gerar + conferência):

```bash
cd desktop
./build-all.sh             # Linux x64 + Windows x64 (+ macOS universal, se a máquina for um Mac)
./build-all.sh --e2e       # idem, e abre o pacote Linux de verdade (e2e.mjs, com xvfb-run se não houver tela)
./build-all.sh --mac-x64   # num Linux: também um .app x64 sem assinatura, só para conferir a estrutura
```

**Passo a passo:**

```bash
# 1) na raiz do projeto, gere a versão de produção do jogo (cria ../dist)
npm install && npm run build

# 2) na pasta desktop, instale o Electron e gere o pacote
cd desktop
npm ci
npm start            # abre a janela do jogo apontando para ../dist (F11 = tela cheia)
npm run dist:win     # release/win-unpacked/Nitro Crew.exe
npm run dist:linux   # release/linux-unpacked/nitro-crew
npm run dist:mac     # release/mac-universal/Nitro Crew.app — só num Mac (ver "macOS" abaixo)
npm run check        # confere o conteúdo de todo pacote em release/ (check-package.mjs)
```

A janela nasce em 1600×900 (mínimo 1024×640), fundo preto, sem barra de menu. Tela cheia é decisão do jogo
(opção salva) — `main.cjs` só obedece.

O pacote leva `main.cjs`, `preload.cjs`, `storage.cjs` e o `../dist` inteiro em `app/` (sem os `.map`) dentro
do `resources/app.asar`; o `steamworks.js` fica fora do asar (`app.asar.unpacked/`, é módulo nativo, com as
bibliotecas de win64/linux64/osx); o `steam_appid.txt` vai **ao lado do executável** (`extraFiles`), que é
onde o `SteamAPI_Init` procura quando o jogo é aberto fora da Steam — dentro do asar ele não servia para nada.

### Matriz: o que se gera onde

Medido em 28/09/2026 com Electron 44.4.5 e electron-builder 26.15.3, jogo `0.1.0` (`app.asar` ≈ 1,1 MB).
Nenhum pacote é versionado: `release/` e os ícones gerados estão no `.gitignore`.

| Pacote | Comando | Gera em Linux | Gera em Windows | Gera em Mac | Tamanho | Aberto de verdade |
|---|---|---|---|---|---|---|
| Linux x64 (`release/linux-unpacked/nitro-crew`) | `dist:linux` | ✅ | não testado | não testado | 292 MB | ✅ aqui, `e2e.mjs` sob Xvfb (userData, saves, log, tela fatal) |
| Windows x64 (`release/win-unpacked/Nitro Crew.exe`) | `dist:win` | ✅ sem wine | não testado (caminho padrão) | não testado | 377 MB | ❌ só conteúdo conferido (`check`): não há Windows aqui |
| macOS universal (`release/mac-universal/Nitro Crew.app`) | `dist:mac` | ❌ `@electron/universal` só roda em macOS | ❌ | esperado ✅ (não testado) | — | ❌ |
| macOS x64 sem assinatura (`release/mac/Nitro Crew.app`) | `build-all.sh --mac-x64` | ✅ (conferência) | — | — | 299 MB | ❌ (e não é para publicar) |

**Windows gerado no Linux, sem wine:** o electron-builder 26 grava ícone, versão e copyright do `.exe` com o
`resedit` (JavaScript), não com o `rcedit.exe` — por isso não precisa de wine, e nada foi pulado. Conferido no
`.exe` gerado aqui: Propriedades → Detalhes com "Nitro Crew", versão 0.1.0 e o copyright; os 7 tamanhos do
`build/icon.ico` embutidos byte a byte; recurso de integridade do asar presente. **Sem assinatura digital**
(não há certificado): baixado por navegador, o SmartScreen avisa "editor desconhecido"; instalado pela Steam,
não (a Steam não marca o arquivo como vindo da internet). Assinar é opcional para a Steam; se um dia quiser,
`CSC_LINK`/`CSC_KEY_PASSWORD` na máquina que gera, sem mudar o `package.json`.

**Não dá para abrir o `.exe` aqui** (não há Windows nem wine nesta máquina): o que roda nele é o mesmo `app.asar`
que o `e2e.mjs` abre no Linux, mas janela, GPU, controles e caminhos do Windows só se provam no checklist
manual abaixo.

### Metadados

| Campo | Valor | De onde |
|---|---|---|
| Nome | Nitro Crew (provisório, Fase 2.1 — `docs/LOJA.md`) | `productName` |
| Versão | `0.1.0` — tem que ser igual à do `package.json` da raiz (o relatório de erros usa a de lá); `tests/desktop-package.test.ts` confere | `version` |
| Copyright | `Copyright © 2026 Felipe Bonamigo` — trocar pelo nome da empresa quando existir (o EULA e o kit de imprensa ainda têm `[nome da empresa]`) | `build.copyright` |
| Identificador | `br.com.bonamigo.nitrocrew` (Info.plist do macOS; não aparece na Steam) | `build.appId` |
| macOS mínimo | 13 (vem do Electron 44) | — |

### Ícone (PROVISÓRIO até a arte da Fase 2)

`make-icon.cjs` desenha por código (só `zlib` do Node, nenhuma dependência) uma estrada rumo ao pôr do sol com a
faixa na cor de destaque do jogo, e grava em `build/` os três formatos que o electron-builder procura:
`icon.png` 1024×1024 (Linux; ele pede ≥ 512), `icon.ico` 16–256 (Windows; ele exige o 256) e `icon.icns`
32–1024 com as versões @2x (macOS). Os `predist:*` o chamam com `--if-missing`; `npm run icon` regera.
**Quando a arte chegar:** pôr os três arquivos finais em `build/`, tirar as linhas `build/icon.*` do
`.gitignore` desta pasta (para versioná-los) e o `make-icon.cjs --if-missing` dos `predist:*`. O ícone da
biblioteca da Steam e o do Steam Deck **não** vêm daqui: são imagens enviadas no Steamworks (ícone do cliente,
cápsulas — `docs/LOJA.md`). No Linux, o pacote `dir` não usa ícone (a Steam mostra o dela).

### macOS: o que exige um Mac

Gerar o `.app` é possível no Linux (`--mac-x64` acima), mas **publicar** não:

1. **Binário universal** (Intel + Apple Silicon): o `@electron/universal` junta as duas arquiteturas e só roda
   em macOS (aqui falhou com `@electron/universal is only supported on darwin platforms`).
2. **Assinatura** com um certificado *Developer ID Application* (Apple Developer Program, US$ 99/ano): o
   `codesign` só existe no macOS. Em Mac com Apple Silicon, código que não está assinado corretamente nem
   abre. O electron-builder assina sozinho se achar o certificado no Keychain (ou por `CSC_LINK` +
   `CSC_KEY_PASSWORD`), com o *hardened runtime* e as permissões padrão dele (JIT e
   `disable-library-validation`, necessária para o `steamworks.js` carregar a `libsteam_api.dylib`).
3. **Notarização** (a Apple examina o app e devolve um "carimbo"; sem ela o Gatekeeper bloqueia): o
   electron-builder notariza sozinho quando acha estas variáveis — de preferência a chave de API:
   `APPLE_API_KEY` (caminho do `.p8`), `APPLE_API_KEY_ID` e `APPLE_API_ISSUER`; ou `APPLE_ID`,
   `APPLE_APP_SPECIFIC_PASSWORD` e `APPLE_TEAM_ID`.

Comando pronto, num Mac com o certificado instalado:

```bash
cd nitro-crew && npm ci && npm run build
cd desktop && npm ci
export APPLE_API_KEY=~/chaves/AuthKey_XXXXXXXXXX.p8 APPLE_API_KEY_ID=XXXXXXXXXX APPLE_API_ISSUER=<uuid-do-emissor>
npm run dist:mac && npm run check     # release/mac-universal/Nitro Crew.app, assinado e notarizado
spctl -a -vv "release/mac-universal/Nitro Crew.app"   # tem que dizer "accepted" e "Notarized Developer ID"
```

Sem certificado (só para abrir no próprio Mac): `npx electron-builder --mac --universal -c.mac.identity=-`
assina ad hoc; abre com clique direito → Abrir, mas não serve para distribuir. Decidir se vale publicar para
macOS é item da matriz de QA (M-9).

### Conferir o pacote de verdade (`e2e.mjs`)

```bash
npm run dist:linux
xvfb-run -a npm run e2e          # numa máquina com tela: npm run e2e
```

Abre o **executável empacotado** pelo Playwright da raiz do jogo, com o userData numa pasta temporária, e
confere: `app/index.html` carregado de dentro do asar, preload, pasta "Nitro Crew", erro → aviso no canto e
`logs/errors.log`, "Copiar relatório de erros" → área de transferência, telemetria → `saves/*.json`, save
trocado em disco (como a nuvem faria) vencendo um `localStorage` **diferente** na volta (conflito de verdade),
o mesmo erro repetido na 2ª abertura voltando ao log, e a tela de erro fatal sem WebGL — com a opção
`--ignore-gpu-blocklist` num `<code>` e os dois botões operados por um controle simulado.
Capturas em `release/e2e-*.png` (ou no prefixo passado como argumento). Itens de menu são achados pelo texto,
nunca contando setas (em 28/09 as 4 setas de antes paravam em "Festa" e 8 checagens caíam). Leva ≈ 2 min 40 s
com GL por software.

`check-package.mjs` (`npm run check`) é o complemento para as builds que não abrem aqui: lê o `app.asar` e as
pastas de cada pacote em `release/` e confere o jogo em `app/`, nenhum `.map`, `package.json` com nome e
versão, `steamworks.js` desempacotado com a biblioteca nativa **daquele** sistema, `steam_appid.txt` ao lado
do executável (avisa se ainda é 480), e — no Windows — nome, versão, copyright e ícone gravados no `.exe`; no
macOS, `Info.plist` e `icon.icns`.

### GPU na lista de bloqueio do Chromium

O Chromium recusa WebGL em alguns drivers antigos ou quebrados (visto aqui com GL por software:
`WebGL2 blocklisted`). O jogo então mostra a tela "O jogo não conseguiu iniciar", com o relatório para copiar,
em vez de uma janela preta. A opção de inicialização `--ignore-gpu-blocklist` (Steam → Propriedades → Opções
de inicialização) é repassada ao Chromium e resolve na maioria dos casos — conferido neste pacote. **Decisão em
aberto**: ligar isso por padrão em `main.cjs` (`app.commandLine.appendSwitch('ignore-gpu-blocklist')`), como
fazem muitos jogos HTML5 na Steam; o preço é trocar a tela explicativa por um possível travamento de driver.
Decidir depois do teste em máquinas fracas (docs/QA.md).

## O que a página enxerga (`window.desktop`)

`preload.cjs` expõe `window.desktop` e `src/game/desktop.ts` é a tipagem dele (`DesktopApi`). Fora do
Electron `getDesktop()` devolve `null` e `setFullscreen()` cai na Fullscreen API do navegador.

| Função | Faz |
|---|---|
| `toggleFullscreen()` / `setFullscreen(v)` / `isFullscreen()` | Tela cheia da janela; `onFullscreen(cb)` avisa quando muda (inclusive por F11). |
| `quit()` | Fecha o jogo (o menu mostra "Sair" só no desktop — `MenuContext.isDesktop`). |
| `steamName()` | Nome do jogador na Steam, ou `null` fora dela. |
| `achievement(id)` | Desbloqueia a conquista; `true` se a Steam aceitou. |
| `richPresence(texto)` | Texto na lista de amigos ("Correndo em Copacabana"); texto vazio limpa. |
| `saveFile(nome, conteudo)` / `openFile()` | Diálogos do sistema em Documentos; extensão `.nitro.json`. |
| `storeReadAll()` / `storeWrite(chave, json)` | Saves em `<userData>/saves/<chave>.json` (ver "Steam Cloud" abaixo). |
| `logAppend(texto)` | Acrescenta ao log de erros `<userData>/logs/errors.log`. |
| `copyText(texto)` | Área de transferência do sistema (o preload em sandbox não tem `clipboard`). |

`tests/desktop-storage.test.ts` confere que o preload expõe exatamente as funções de `DesktopApi` e que todo
canal que ele chama tem `ipcMain.handle` no `main.cjs` — mudou um, mude os três.

Links externos (`window.open`, `target=_blank`) abrem no navegador do sistema; a janela nunca navega para fora.
Uma segunda instância só traz a primeira para a frente.

## Integração Steam (opcional)

1. Crie o app no Steamworks e anote o App ID. **Troque o `480` de `steam_appid.txt`** pelo seu — 480 é o
   Spacewar, app de testes da Valve, e serve só para ver o overlay e o nome do jogador funcionando.
2. `npm install` dentro de `desktop/` já tenta instalar `steamworks.js` (está em `optionalDependencies`:
   se a biblioteca nativa falhar na sua máquina, o jogo continua funcionando sem Steam).
3. Com o cliente Steam aberto, `npm start` imprime `Steam: <seu nome>` no terminal. Sem Steam aberto, imprime
   "Steamworks indisponível" e segue — todo o código Steam é `try/catch`.
4. `main.cjs` liga o overlay (Shift+Tab) via `electronEnableSteamOverlay(true)` antes do `ready`. O `true`
   desliga o repintor por quadro do steamworks.js porque o jogo já desenha 60 vezes por segundo.
5. Rich Presence usa a chave `status` — aparece sem configuração extra. Se quiser o texto localizado pela
   Steam (`steam_display`), configure os tokens no painel do Steamworks e troque a chave em `setRichPresence`.
6. Build de produção: o `asarUnpack` já deixa `steamworks.js` fora do asar (módulo nativo). Copie a
   `steam_api64.dll` / `libsteam_api.so` / `libsteam_api.dylib` do `sdk/redistributable_bin/` da Steamworks
   SDK para a raiz do `release/*-unpacked` se o steamworks.js não trouxer a sua plataforma.

### Steam Cloud (saves)

**Onde fica tudo** — o `main.cjs` fixa o userData em `<appData>/Nitro Crew`, no pacote e no `npm start`:

| Sistema | Pasta de dados |
|---|---|
| Windows | `%APPDATA%\Nitro Crew` (`C:\Users\<você>\AppData\Roaming\Nitro Crew`) |
| Linux / Steam Deck | `~/.config/Nitro Crew` (ou `$XDG_CONFIG_HOME/Nitro Crew`) |
| macOS | `~/Library/Application Support/Nitro Crew` |

Dentro dela:

- `saves/nitro-crew.settings.json`, `saves/nitro-crew.save.json` (e toda chave `nitro-crew.*` que o jogo gravar
  por `writeJson`) — **é isto que vai para a nuvem**. Gravação atômica (temporário + rename), só JSON válido,
  no máximo 1 MB por arquivo.
- `logs/errors.log` (+ `errors.1.log`, rotação em 512 KB) — relatório de erros; **fica fora da nuvem**.
- `Local Storage/`, `Cache/`, `GPUCache/`… — do Chromium. O `localStorage` continua sendo onde o jogo lê e grava
  durante a partida, mas é um LevelDB com arquivos que mudam de nome e ficam travados com o jogo aberto: **não**
  aponte o Auto-Cloud para ele.

**Como o jogo usa** (`src/game/cloudsave.ts`): toda gravação do jogo vai para o `localStorage` e, no Electron,
também para `saves/<chave>.json`. Ao abrir, antes de ler opções e progresso, o arquivo vence o `localStorage`
(pode ter chegado da nuvem vindo de outro computador), exceto quando a última gravação local não foi confirmada
no disco — aí o `localStorage` vence e é regravado. Arquivo corrompido nunca vence um `localStorage` válido. Save
de quem jogou uma versão anterior (só no `localStorage`) é copiado para o disco na primeira abertura. Se a
leitura do disco falhar (IPC sem resposta em 3 s) ou um arquivo existir mas não puder ser lido (`storage.cjs`
devolve `null`), o jogo segue com o `localStorage` e **não** grava por cima do arquivo — ele pode ser o mais novo,
vindo da nuvem; só a chave pendente (local sabidamente mais novo) é regravada. Limite conhecido: a próxima
gravação do jogo naquela sessão (fim de corrida, opção mudada) vai para o arquivo, como o save de qualquer jogo.
Com o `localStorage` cheio (cota), toda gravação vai para o arquivo mesmo assim, a sessão segue pelo que gravou
(memória) e a abertura seguinte lê o arquivo — detalhes, e o aviso ao jogador quando nada grava, em `docs/SAVE.md`.

**Configurar no Steamworks** (App Admin → Cloud → Steam Auto-Cloud):

1. Cota: 1 MB e 20 arquivos por usuário é folga (hoje são 2 arquivos de poucos KB).
2. **Um** caminho raiz, valendo para todos os sistemas, e **substituições de raiz** (Root Overrides) para
   Linux e macOS. Três raízes independentes, uma por sistema, **não** sincronizam entre si: o save do PC não
   chegaria ao Steam Deck. A documentação da Steamworks diz que, para save entre plataformas, se define uma
   raiz e se criam substituições para as outras plataformas, com a raiz em "[All OSes]"; os arquivos da raiz
   com substituições sincronizam em todas elas.

   Caminho raiz (Root Paths):

   | Raiz | Subpasta | Padrão | SO | Recursivo |
   |---|---|---|---|---|
   | `WinAppDataRoaming` | `Nitro Crew/saves` | `*.json` | **[All OSes]** | não |

   Substituições (Root Overrides):

   | Raiz original | SO | Nova raiz | Acrescentar/substituir caminho | Substituir caminho |
   |---|---|---|---|---|
   | `WinAppDataRoaming` | Linux | `LinuxHome` | `.config/Nitro Crew/saves` | marcado |
   | `WinAppDataRoaming` | macOS | `MacAppSupport` | `Nitro Crew/saves` | marcado |

   🔧 **CONFERIR no painel** (a documentação da Steamworks não abre desta rede; o texto acima veio de citações
   dela): os nomes exatos das raízes na lista, e se "Substituir caminho" troca a subpasta inteira
   (`Nitro Crew/saves` → `.config/Nitro Crew/saves`) — a do Linux é diferente das outras por causa do
   `.config`. Se houver uma raiz própria para o `XDG_CONFIG_HOME`, prefira-a no Linux: quem mudou essa variável
   tem os saves fora de `~/.config` (raro; o Steam Deck usa o padrão).

   Rodando a build Windows sob Proton, o `WinAppDataRoaming` cai dentro do prefixo do Proton e sincroniza pela
   raiz original, sem substituição.
3. Teste: jogar no computador A, fechar, abrir no B — a copa concluída em A aparece destravada em B. **Faça
   com sistemas diferentes** (Windows → Steam Deck/Linux): é o que prova as substituições de raiz. O `e2e.mjs`
   simula a troca do arquivo em disco entre duas execuções, mas não a Steam.

**Regra para quem mexe no jogo**: chave nova de save tem que ter o prefixo `nitro-crew.` e passar por
`writeJson` (`src/game/storage.ts`) — gravação direta no `localStorage` não vai para a nuvem. O relatório de
erros (`nitro-crew.errors`) e a marcação interna `nitro-crew.__pending` ficam de fora de propósito.

**Mudar o nome da pasta** ("Nitro Crew" em `main.cjs`, `USER_DATA_DIR_NAME`) perde o save de quem já joga e
exige trocar os caminhos acima — o nome definitivo do jogo (Fase 2.1) **não** precisa mudar a pasta.

### Relatório de erros

`src/game/errors.ts` guarda os últimos 50 erros no `localStorage` e, no Electron, em `logs/errors.log`; o
`main.cjs` acrescenta ao mesmo log a queda do processo da página (`render-process-gone`, com um recarregamento
automático) e da GPU. O jogador copia tudo em Opções › "Copiar relatório de erros". Caminhos de arquivo e nomes
de pasta pessoal saem do texto. Política: `docs/legal/PRIVACIDADE.md`.

- Erro repetido (mesma pilha; números da mensagem não contam) é **uma** entrada com contador: guarda a 1ª vez e
  a versão, a data, o modo e a pista da **última** — depois de uma atualização, o relatório mostra que o defeito
  continua na versão nova.
- Vai ao log: o erro novo, a 1ª repetição em cada abertura do jogo (um erro de toda abertura aparece a cada uma)
  e os contadores 10, 100, 1000… O contador vai ao `localStorage` a cada 5 s e ao fechar a página.
- Rajada de erros diferentes: no máximo 10 gravações completas a cada 10 s; o resto fica no relatório, e o log
  ganha uma linha `N more error(s) not logged one by one (burst)`.

### Conquistas

Cadastre estes IDs (exatos) em Steamworks → Stats & Achievements. A lista com nome PT/EN está exportada em
`src/game/desktop.ts` (`ACHIEVEMENTS`); a descrição abaixo é o que digitar no painel em português, e a versão
em inglês (a mesma que o jogo mostra na tela de recordes) está em `src/stats/strings.ts` (`stats.achDesc.<ID>`;
as `COPA_<ID>` usam o modelo `stats.achDescCup` com o nome da copa, então copa nova já nasce com descrição).
As regras exatas, com os limites, estão em `docs/ESTATISTICAS.md`; `tests/desktop.test.ts` confere que toda
conquista da lista tem linha nesta tabela.

| ID | Nome (PT) | Nome (EN) | Como desbloquear |
|---|---|---|---|
| `PRIMEIRA_VITORIA` | Primeira vitória | First Win | Vencer qualquer corrida (o contra-relógio não conta). |
| `COPA_EUA` | Copa Estados Unidos | USA Cup | Concluir a Copa Estados Unidos. |
| `COPA_JAPAO` | Copa Japão | Japan Cup | Concluir a Copa Japão. |
| `COPA_EUROPA` | Copa Europa | Europe Cup | Concluir a Copa Europa. |
| `COPA_AFRICA_DO_SUL` | Copa África do Sul | South Africa Cup | Concluir a Copa África do Sul. |
| `COPA_AUSTRALIA` | Copa Austrália | Australia Cup | Concluir a Copa Austrália. |
| `COPA_ESCANDINAVIA` | Copa Escandinávia | Scandinavia Cup | Concluir a Copa Escandinávia. |
| `COPA_MEDITERRANEO` | Copa Mediterrâneo | Mediterranean Cup | Concluir a Copa Mediterrâneo. |
| `EQUIPE_COMPLETA` | Equipe completa | Full Crew | Correr uma corrida com 4 jogadores. |
| `SEM_BOX` | Sem box | No Pit Stop | Vencer sem parar no box. |
| `NITRO_TRIPLO` | Nitro triplo | Triple Nitro | Usar 3 nitros numa mesma volta. |
| `EMPURRAO` | Empurrão | Push | Dar um empurrão a um companheiro. |
| `VOLTA_PERFEITA` | Volta perfeita | Perfect Lap | Completar uma volta sem sair do asfalto. |
| `CAMPEAO` | Campeão | Champion | Vencer uma copa na dificuldade Campeão. |
| `MADRUGADA` | Madrugada | Night Owl | Vencer uma pista noturna. |
| `PODIO_DE_EQUIPE` | Pódio da equipe | Crew Podium | Três jogadores no pódio (1º, 2º e 3º) numa corrida contra a IA. |
| `DO_ULTIMO_AO_PRIMEIRO` | Do último ao primeiro | Last to First | Vencer uma corrida em que você estava em último ao fechar a primeira volta. |
| `SEM_ARRANHAO` | Sem um arranhão | Not a Scratch | Terminar uma corrida contra a IA sem encostar em outro carro nem bater no cenário. |
| `MARATONA` | Maratona | Marathon | Somar 1.000 km de corrida, juntando todos os jogadores. |
| `MESTRE_DO_VACUO` | Mestre do vácuo | Slipstream Master | Passar 60 segundos no vácuo de outros carros numa mesma corrida. |
| `NITRO_NA_BANDEIRA` | Nitro na bandeirada | Nitro Finish | Cruzar a linha de chegada com o nitro ligado. |
| `DEZ_VITORIAS` | Dez vitórias | Ten Wins | Somar 10 vitórias (o contra-relógio não conta). |
| `GIRO_COMPLETO` | Giro completo | Grand Tour | Correr em todas as pistas do jogo (o contra-relógio não conta). |
| `RIVAL_DERROTADO` | Rival derrotado | Rival Defeated | Concluir uma copa (normal ou da carreira) terminando à frente do rival da copa em todas as corridas. |
| `COPA_BR_RJ` | Copa Rio de Janeiro | Rio de Janeiro Cup | Concluir a Copa Rio de Janeiro (carimba RJ no passaporte). |
| `COPA_BR_SP` | Copa São Paulo | São Paulo Cup | Concluir a Copa São Paulo (carimba SP no passaporte). |
| `COPA_BR_MG` | Copa Minas Gerais | Minas Gerais Cup | Concluir a Copa Minas Gerais (carimba MG no passaporte). |
| `COPA_BR_ES` | Copa Espírito Santo | Espírito Santo Cup | Concluir a Copa Espírito Santo (carimba ES no passaporte). |
| `COPA_BR_PR` | Copa Paraná | Paraná Cup | Concluir a Copa Paraná (carimba PR no passaporte). |
| `COPA_BR_SC` | Copa Santa Catarina | Santa Catarina Cup | Concluir a Copa Santa Catarina (carimba SC no passaporte). |
| `COPA_BR_RS` | Copa Rio Grande do Sul | Rio Grande do Sul Cup | Concluir a Copa Rio Grande do Sul (carimba RS no passaporte). |
| `COPA_BR_DF` | Copa Distrito Federal | Federal District Cup | Concluir a Copa Distrito Federal (carimba DF no passaporte). |
| `COPA_BR_GO` | Copa Goiás | Goiás Cup | Concluir a Copa Goiás (carimba GO no passaporte). |
| `COPA_BR_MS` | Copa Mato Grosso do Sul | Mato Grosso do Sul Cup | Concluir a Copa Mato Grosso do Sul (carimba MS no passaporte). |
| `COPA_BR_MT` | Copa Mato Grosso | Mato Grosso Cup | Concluir a Copa Mato Grosso (carimba MT no passaporte). |
| `COPA_BR_BA` | Copa Bahia | Bahia Cup | Concluir a Copa Bahia (carimba BA no passaporte). |
| `COPA_BR_SE` | Copa Sergipe | Sergipe Cup | Concluir a Copa Sergipe (carimba SE no passaporte). |
| `COPA_BR_AL` | Copa Alagoas | Alagoas Cup | Concluir a Copa Alagoas (carimba AL no passaporte). |
| `COPA_BR_PE` | Copa Pernambuco | Pernambuco Cup | Concluir a Copa Pernambuco (carimba PE no passaporte). |
| `COPA_BR_PB` | Copa Paraíba | Paraíba Cup | Concluir a Copa Paraíba (carimba PB no passaporte). |
| `COPA_BR_RN` | Copa Rio Grande do Norte | Rio Grande do Norte Cup | Concluir a Copa Rio Grande do Norte (carimba RN no passaporte). |
| `COPA_BR_CE` | Copa Ceará | Ceará Cup | Concluir a Copa Ceará (carimba CE no passaporte). |
| `COPA_BR_PI` | Copa Piauí | Piauí Cup | Concluir a Copa Piauí (carimba PI no passaporte). |
| `COPA_BR_MA` | Copa Maranhão | Maranhão Cup | Concluir a Copa Maranhão (carimba MA no passaporte). |
| `COPA_BR_PA` | Copa Pará | Pará Cup | Concluir a Copa Pará (carimba PA no passaporte). |
| `COPA_BR_AM` | Copa Amazonas | Amazonas Cup | Concluir a Copa Amazonas (carimba AM no passaporte). |
| `COPA_BR_AP` | Copa Amapá | Amapá Cup | Concluir a Copa Amapá (carimba AP no passaporte). |
| `COPA_BR_RR` | Copa Roraima | Roraima Cup | Concluir a Copa Roraima (carimba RR no passaporte). |
| `COPA_BR_RO` | Copa Rondônia | Rondônia Cup | Concluir a Copa Rondônia (carimba RO no passaporte). |
| `COPA_BR_AC` | Copa Acre | Acre Cup | Concluir a Copa Acre (carimba AC no passaporte). |
| `COPA_BR_TO` | Copa Tocantins | Tocantins Cup | Concluir a Copa Tocantins (carimba TO no passaporte). |
| `REGIAO_SUDESTE` | Sudeste carimbado | Southeast Stamped | Carimbar no passaporte todos os estados da região (RJ, SP, MG e ES). |
| `REGIAO_SUL` | Sul carimbado | South Stamped | Carimbar no passaporte todos os estados da região (PR, SC e RS). |
| `REGIAO_CENTRO_OESTE` | Centro-Oeste carimbado | Center-West Stamped | Carimbar no passaporte todos os estados da região (DF, GO, MS e MT). |
| `REGIAO_NORDESTE` | Nordeste carimbado | Northeast Stamped | Carimbar no passaporte todos os estados da região (BA, SE, AL, PE, PB, RN, CE, PI e MA). |
| `REGIAO_NORTE` | Norte carimbado | North Stamped | Carimbar no passaporte todos os estados da região (PA, AM, AP, RR, RO, AC e TO). |
| `PASSAPORTE_COMPLETO` | Passaporte completo | Full Passport | Carimbar os 27 estados (a Expedição Brasil inteira). |

O jogo chama `getDesktop()?.achievement(id)` e também guarda o id em `SaveData.achievements`, para o
desbloqueio contar fora da Steam e ser reenviado se a Steam estiver fechada na hora.

## Steam Input / controles

O jogo lê os controles pela **Gamepad API** do Chromium — nada de Steamworks para entrada. Até **4
controles** (um por assento, `gp0`..`gp3`) mais dois teclados virtuais (`kb1` setas, `kb2` WASD).

Na página do app em Steamworks:

- Marque **suporte completo a controle** (Full Controller Support) para Xbox e PlayStation; Steam Deck é
  compatível pelo mesmo caminho.
- Teste com **Steam Input ligado e desligado** (Propriedades do jogo → Controle). Ligado, a Steam apresenta
  qualquer controle como um Xbox padrão à Gamepad API (mapeamento `standard`); desligado, o Chromium fala
  direto com o driver e alguns controles chegam com mapeamento próprio — o jogo tem que funcionar nos dois.
- Teste **quatro controles ligados ao mesmo tempo**: com Steam Input, cada um precisa aparecer como um
  `navigator.getGamepads()` distinto, e a ordem de conexão define `gp0`..`gp3`.
- Deixe o layout de Steam Input padrão como "Gamepad" (não "Teclado e mouse") — senão o controle vira
  setas do teclado e todos os jogadores caem no mesmo assento.

## Enviar pela SteamPipe

1. Baixe a Steamworks SDK e abra `tools/ContentBuilder`.
2. Um depósito por plataforma: `release/win-unpacked` (Windows), `release/linux-unpacked` (Linux),
   `release/mac-universal` (macOS, assinado e notarizado num Mac). O `.vdf` do depósito aponta para a pasta
   inteira, com o executável na raiz.
3. No `app_build.vdf`, o executável de lançamento é `Nitro Crew.exe` / `nitro-crew` / `Nitro Crew.app`.
   O `steam_appid.txt` que o pacote leva ao lado do executável é para **abrir fora da Steam** (teste do
   overlay e das conquistas com o `.exe` direto); a Valve orienta não publicá-lo — no `.vdf` do depósito,
   `"FileExclusion" "steam_appid.txt"`. Aberto pela Steam, o App ID vem dela.
4. `steamcmd +login <conta> +run_app_build <caminho>/app_build.vdf +quit`, depois defina o branch
   (`default` ou um beta) no painel e teste instalando pela biblioteca — não pelo `npm start`, para pegar
   erro de caminho do `app/index.html` empacotado.

## Testar sem Steam

`npm start` sozinho já serve para tudo que não é Steam: janela, F11, diálogos de arquivo, gamepads. Para
testar tela cheia e controles no navegador, `npm run dev` na raiz do projeto — `src/game/desktop.ts` faz a
mesma API cair na Fullscreen API quando `window.desktop` não existe.

## Teste manual do Felipe (Windows e Steam Deck)

O que o agente não consegue provar daqui. Anote a versão (Opções › Copiar relatório de erros, linha `version:`)
e o resultado de cada item na tabela de `docs/QA.md`.

### Windows (máquina sem Node nem ferramentas de desenvolvedor — itens M-1 a M-3 da matriz)

Levar o pacote: gerar lá mesmo (`npm ci && npm run build`, `cd desktop && npm ci && npm run dist:win`) ou
compactar a pasta gerada no Linux (`cd release && zip -qr nitro-crew-win.zip win-unpacked`) e copiar.

- [ ] `Nitro Crew.exe` abre com dois cliques. O SmartScreen avisa "editor desconhecido" (sem assinatura):
      "Mais informações → Executar assim mesmo". Anotar se o antivírus reclamou.
- [ ] Ícone da estrada (provisório) no Explorer e na barra de tarefas; botão direito → Propriedades →
      Detalhes: "Nitro Crew", versão 0.1.0, `Copyright © 2026 Felipe Bonamigo`.
- [ ] Janela 1600×900 sem menu; F11 entra e sai da tela cheia; a opção de tela cheia sobrevive a fechar e abrir.
- [ ] Menu inteiro no teclado **e** num controle Xbox/PlayStation; dois controles + teclado entrando na corrida.
- [ ] Corrida rápida com 1 jogador até a bandeirada; depois 2 e 4 jogadores em tela dividida. Anotar a
      qualidade usada e se fica a 60 fps (Opções › Qualidade).
- [ ] `%APPDATA%\Nitro Crew\saves` tem `nitro-crew.settings.json` e, depois da corrida, `nitro-crew.save.json`.
      Fechar e abrir: progresso e opções mantidos.
- [ ] Opções › "Copiar relatório de erros" e colar no Bloco de Notas: `desktop: yes`, versão 0.1.0, nenhum
      `C:\Users\<seu nome>`.
- [ ] "Sair" no menu fecha tudo (Gerenciador de Tarefas sem "Nitro Crew"); um segundo clique no atalho com o
      jogo aberto só traz a janela para a frente.
- [ ] Com o cliente Steam aberto (e o 480 em `steam_appid.txt`): Shift+Tab abre o overlay; a Steam mostra
      "jogando Spacewar". Sem a Steam aberta, o jogo abre normalmente.
- [ ] Notebook com vídeo integrado: se aparecer "O jogo não conseguiu iniciar", copiar o relatório e testar a
      opção `--ignore-gpu-blocklist` (atalho → Propriedades → Destino, no fim). Guardar o relatório.

### Steam Deck (passo 5.5 — itens M-5 e M-6)

O teste que vale é pela Steam (depósito Linux num branch beta, depois da conta Steamworks). Antes disso: no
Modo Desktop, copiar `release/linux-unpacked` para o Deck (pendrive ou `scp`), "Adicionar jogo não Steam"
apontando para `nitro-crew` e voltar ao Modo Jogo.

- [ ] Abre em tela cheia 1280×800. ⚠️ **Risco a conferir primeiro:** a pasta Linux traz o `chrome-sandbox`
      sem SUID (a Steam não preserva essa permissão). Se o jogo não abrir, ou o log falar em "SUID sandbox",
      testar com `--no-sandbox` nas opções de inicialização e avisar o agente: a decisão é ligar isso no
      `main.cjs` só no Linux (o `e2e.mjs` daqui sempre passa `--no-sandbox` porque roda como root, então não
      prova este caso). O mesmo vale para Ubuntu 24.04 fora do Deck (M-7).
- [ ] Controles do próprio Deck entram como jogador 1 com o layout "Gamepad" do Steam Input; um segundo
      controle por Bluetooth entra como jogador 2.
- [ ] 2 jogadores em tela dividida na tela de 7": HUD, posição, volta e nitro legíveis a um braço de distância.
- [ ] 60 fps com 2 telas (sobreposição de desempenho do Deck, nível 1), anotando a qualidade; bateria por hora.
- [ ] No dock, na TV em 1080p: 4 controles, 4 telas; anotar fps.
- [ ] Botão de energia no meio da corrida (suspender) e voltar: o jogo continua sem travar nem perder o áudio.
- [ ] Save em `~/.config/Nitro Crew/saves` (Modo Desktop, Dolphin com arquivos ocultos visíveis).
- [ ] "Sair" no menu volta para a biblioteca.
- [ ] Comparação: a build Windows sob Proton (forçar compatibilidade) — se rodar melhor que a nativa, anotar.
