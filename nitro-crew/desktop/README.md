# Nitro Crew — build desktop (Electron) para Steam

O jogo é uma aplicação web (TypeScript + Canvas 2D, Vite) empacotada com Electron — o mesmo caminho de
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

### Conquistas

Cadastre estes IDs (exatos) em Steamworks → Stats & Achievements. A lista com nome PT/EN está exportada em
`src/game/desktop.ts` (`ACHIEVEMENTS`); a descrição abaixo é o que digitar no painel em português, e a versão
em inglês (a mesma que o jogo mostra na tela de recordes) está em `src/stats/strings.ts` (`stats.achDesc.<ID>`).
As regras exatas, com os limites, estão em `docs/ESTATISTICAS.md`; `tests/desktop.test.ts` confere que toda
conquista da lista tem linha nesta tabela.

| ID | Nome (PT) | Nome (EN) | Como desbloquear |
|---|---|---|---|
| `PRIMEIRA_VITORIA` | Primeira vitória | First Win | Vencer qualquer corrida (o contra-relógio não conta). |
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
| `PODIO_DE_EQUIPE` | Pódio da equipe | Crew Podium | Três jogadores no pódio (1º, 2º e 3º) numa corrida contra a IA. |
| `DO_ULTIMO_AO_PRIMEIRO` | Do último ao primeiro | Last to First | Vencer uma corrida em que você estava em último ao fechar a primeira volta. |
| `SEM_ARRANHAO` | Sem um arranhão | Not a Scratch | Terminar uma corrida contra a IA sem bater em carro nem no cenário. |
| `MARATONA` | Maratona | Marathon | Somar 1.000 km de corrida, juntando todos os jogadores. |
| `MESTRE_DO_VACUO` | Mestre do vácuo | Slipstream Master | Passar 60 segundos no vácuo de outros carros numa mesma corrida. |
| `NITRO_NA_BANDEIRA` | Nitro na bandeirada | Nitro Finish | Cruzar a linha de chegada com o nitro ligado. |
| `DEZ_VITORIAS` | Dez vitórias | Ten Wins | Somar 10 vitórias (o contra-relógio não conta). |
| `GIRO_COMPLETO` | Giro completo | Grand Tour | Correr em todas as pistas do jogo (o contra-relógio não conta). |

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
