# Publicar na Steam (Fase 4 do cronograma)

Pré-requisitos, todos do Felipe: conta Steamworks aprovada, o app criado (AppID), dois depots
(Windows e Linux) e o `steamcmd` instalado na máquina que vai enviar.

1. **AppID no jogo**: crie `jogo/desktop/steam_appid.txt` com o número. Sem ele o jogo usa o 480
   (Spacewar, o app de teste) — bom pra ver a conquista aparecer antes do app existir.
2. **Conquistas no painel** (Steamworks → Stats & Achievements): cadastre uma por linha de
   `jogo/js/conquistas.js`, com o **mesmo id** (`primeiro_sangue`, `finalizador`, …). Estatística
   `pontuacao_maxima` (INT). Publique as mudanças do painel.
3. **Steam Cloud** (Steamworks → Cloud): pasta `progresso.json` em `{userData}` do Electron:
   Windows `%APPDATA%/punhos-de-shaolin/`, Linux `~/.config/punhos-de-shaolin/`. Marque "Auto-Cloud".
4. **Build**: em `jogo/`, `npm ci` e `npm run empacotar`. Sai em `jogo/dist/win-unpacked/` e
   `jogo/dist/linux-unpacked/`.
5. **Enviar**: troque `SEU_APPID`, `SEU_DEPOT_WINDOWS` e `SEU_DEPOT_LINUX` nos três `.vdf` e rode
   `steamcmd +login CONTA +run_app_build "…/jogo/steam/app_build.vdf" +quit`.
6. **Launch options** (Steamworks → Installation → General): Windows `punhos-de-shaolin.exe`,
   Linux `punhos-de-shaolin`. Marque o sistema de cada um.
7. **Branch beta**: o `.vdf` publica em `beta`. Ponha senha no painel, instale pela Steam no
   Windows e confira: abre, salva ao fechar, conquista dispara com o pop-up, overlay (Shift+Tab) abre.
8. **Lançamento**: `"SetLive" "default"`, ou promova o build no painel (Builds → Set build live).

O que NUNCA vai pro repositório: `steam_appid.txt` com o id real (é público na loja de qualquer
jeito, mas o arquivo ao lado do exe muda o comportamento), chaves de conta, `dist/` e `saida/`.
