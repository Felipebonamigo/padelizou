# Nitro Crew — build desktop (Electron) para Steam

O jogo é uma aplicação web (TypeScript + Three.js/WebGL, Vite) empacotada com Electron — o mesmo caminho de
Vampire Survivors (versões iniciais) e CrossCode na Steam. Esta pasta só embrulha o `../dist`; nada do jogo
mora aqui.

## Gerar o executável

```bash
# 1) na raiz do projeto, gere a versão de produção do jogo (cria ../dist)
npm install && npm run build

# 2) na pasta desktop, instale o Electron e gere o pacote
cd desktop
npm install
npm start            # abre a janela do jogo apontando para ../dist (F11 = tela cheia)
npm run dist:win     # release/win-unpacked/Nitro Crew.exe
npm run dist:linux   # release/linux-unpacked/nitro-crew
npm run dist:mac     # release/mac/Nitro Crew.app
```

A janela nasce em 1600×900 (mínimo 1024×640), fundo preto, sem barra de menu. Tela cheia é decisão do jogo
(opção salva) — `main.cjs` só obedece.

O pacote leva `main.cjs`, `preload.cjs`, `storage.cjs`, `steam_appid.txt` e o `../dist` inteiro em `app/`
(sem os `.map`). Conferido em 25/09/2026 com Electron 44.4.5 e electron-builder 26.15.3: `dist:linux` gera
`release/linux-unpacked/` (≈ 290 MB, `resources/app.asar` ≈ 0,8 MB com `app/index.html` e `app/assets/*`, e
`app.asar.unpacked/node_modules/steamworks.js` com as bibliotecas nativas de win64/linux64/osx).

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
Capturas em `release/e2e-*.png` (ou no prefixo passado como argumento).

### GPU na lista de bloqueio do Chromium

O Chromium recusa WebGL em alguns drivers antigos ou quebrados (visto aqui com GL por software:
`WebGL2 blocklisted`). O jogo então mostra a tela "O jogo não conseguiu iniciar", com o relatório para copiar,
em vez de uma janela preta. A opção de inicialização `--ignore-gpu-blocklist` (Steam → Propriedades → Opções
de inicialização) é repassada ao Chromium e resolve na maioria dos casos — conferido neste pacote. **Decisão em
aberto**: ligar isso por padrão em `main.cjs` (`app.commandLine.appendSwitch('ignore-gpu-blocklist')`), como
fazem muitos jogos HTML5 na Steam; o preço é trocar a tela explicativa por um possível travamento de driver.
Decidir depois do teste em máquinas fracas (docs/QA.md).

**Ícone (opcional):** electron-builder procura `build/icon.ico` (Windows), `build/icon.icns` (macOS) e
`build/icon.png` (Linux, ≥ 512×512) dentro desta pasta. Sem eles, usa o ícone padrão do Electron e avisa.

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
`writeJson` (`src/game/settings.ts`) — gravação direta no `localStorage` não vai para a nuvem. O relatório de
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
`src/game/desktop.ts` (`ACHIEVEMENTS`); a descrição abaixo é o que digitar no painel.

| ID | Nome (PT) | Nome (EN) | Como desbloquear |
|---|---|---|---|
| `PRIMEIRA_VITORIA` | Primeira vitória | First Win | Vencer qualquer corrida. |
| `COPA_BRASIL` | Copa Brasil | Brazil Cup | Concluir a Copa Brasil. |
| `COPA_EUA` | Copa Estados Unidos | USA Cup | Concluir a Copa Estados Unidos. |
| `COPA_JAPAO` | Copa Japão | Japan Cup | Concluir a Copa Japão. |
| `COPA_EUROPA` | Copa Europa | Europe Cup | Concluir a Copa Europa. |
| `EQUIPE_COMPLETA` | Equipe completa | Full Crew | Correr uma corrida com 4 jogadores. |
| `SEM_BOX` | Sem box | No Pit Stop | Vencer sem parar no box. |
| `NITRO_TRIPLO` | Nitro triplo | Triple Nitro | Usar 3 nitros numa mesma volta. |
| `EMPURRAO` | Empurrão | Push | Dar um empurrão a um companheiro. |
| `VOLTA_PERFEITA` | Volta perfeita | Perfect Lap | Completar uma volta sem sair do asfalto. |
| `CAMPEAO` | Campeão | Champion | Vencer uma copa na dificuldade Campeão. |
| `MADRUGADA` | Madrugada | Night Owl | Vencer uma pista noturna. |

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
   `release/mac` (macOS). O `.vdf` do depósito aponta para a pasta inteira, com o executável na raiz.
3. No `app_build.vdf`, o executável de lançamento é `Nitro Crew.exe` / `nitro-crew` / `Nitro Crew.app`.
4. `steamcmd +login <conta> +run_app_build <caminho>/app_build.vdf +quit`, depois defina o branch
   (`default` ou um beta) no painel e teste instalando pela biblioteca — não pelo `npm start`, para pegar
   erro de caminho do `app/index.html` empacotado.

## Testar sem Steam

`npm start` sozinho já serve para tudo que não é Steam: janela, F11, diálogos de arquivo, gamepads. Para
testar tela cheia e controles no navegador, `npm run dev` na raiz do projeto — `src/game/desktop.ts` faz a
mesma API cair na Fullscreen API quando `window.desktop` não existe.
