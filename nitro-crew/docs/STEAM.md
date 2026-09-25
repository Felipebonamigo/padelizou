# Guia de publicação na Steam

Resumo do caminho até a loja. Nada aqui exige mudar o jogo: ele roda no navegador durante o desenvolvimento e
vira executável com o Electron (pasta `desktop/`, ver o README de lá para os comandos).

## 1. Conta e app
- Cadastro no [Steamworks](https://partner.steamgames.com/) + Steam Direct (US$ 100 por jogo, devolvidos após
  US$ 1.000 em vendas). Se a empresa e a conta já existirem por causa do AgeOfEarth, só o app novo é criado.
- Anotar o **App ID** e colocar em `desktop/steam_appid.txt` (hoje está 480, o app de testes da Valve).

## 2. Build
- `npm run build` gera `dist/`; `cd desktop && npm install && npm run dist:win` gera `desktop/release/win-unpacked/`.
- Linux (`dist:linux`) para Steam Deck nativo; o Electron também roda bem sob Proton.

## 3. Depósitos e upload (SteamPipe)
- Steamworks SDK → `tools/ContentBuilder`; script `app_build_<appid>.vdf` com `ContentRoot` em
  `desktop/release/win-unpacked` e executável de lançamento `Nitro Crew.exe`.
- `steamcmd +login <usuário> +run_app_build caminho/app_build.vdf +quit`; publicar a build num branch (default/beta).

## 4. Recursos da Steam que o jogo já prevê
- **Remote Play Together**: marcar na página do app. O co-op de sofá vira online sem código de rede — é o
  primeiro "online" do roteiro (Fase 4.1). Testar com 4 pessoas e latência real.
- **Controles**: o jogo lê a Gamepad API (até 4). Na Steam: "Suporte completo a controle"; testar com Steam Input
  ligado e desligado (o Steam Input pode transformar um controle em teclado/mouse — o layout recomendado é
  "Gamepad").
- **Conquistas**: `window.desktop.achievement('ID')` via `steamworks.js`; a lista com PT/EN está em
  `src/game/desktop.ts` (`ACHIEVEMENTS`) e a sessão desbloqueia sozinha (`src/game/achievements.ts`).
- **Rich Presence**: "Correndo em <pista> · 3P" enquanto há corrida.
- **Cloud**: opções e progresso ficam no `localStorage` do Electron (`%APPDATA%/Nitro Crew`); ativar Steam
  Auto-Cloud apontando para essa pasta.
- **Multiplayer próprio (Fase 4.2+)**: núcleo determinístico com `hashRace`; o transporte pode ser Steam
  Networking Sockets via `steamworks.js`.
- **Leaderboards** (Fase 4.4): melhores voltas por pista e fantasmas.
- **Workshop** (pós-lançamento): pistas são dados do DSL (`src/core/track/tracks.ts`), ideais para o Workshop.

## 5. Checklist de loja
- Cápsulas, trailer, 6+ screenshots (a tela dividida com 4 é a foto principal), descrição PT-BR/EN, tags:
  Corrida, Arcade, Retrô, Pixel Art, Cooperativo local, Tela dividida, Multijogador local, Remote Play Together.
- Página "Em breve" o quanto antes depois da arte (Fase 2): wishlists movem o algoritmo.
- Steam Playtest (gratuito) e demo no Next Fest antes do Early Access.
- Marca/nome: verificar antes de publicar a página (Fase 2.1 e 5.7 do roteiro).
