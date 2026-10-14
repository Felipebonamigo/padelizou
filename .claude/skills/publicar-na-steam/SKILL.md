---
name: publicar-na-steam
description: 'Empacota o Padelizou Arena (export do Godot para Windows e Linux), confere o build e sobe para a Steam pelo SteamPipe, parando onde só o Felipe pode seguir (login, Steam Guard, branch padrão). Use quando o Felipe pedir "sobe para a Steam", "gera o build da loja", "publica no beta" ou "prepara o lançamento".'
---
# Publicar na Steam

Estado hoje: o repositório já tem os presets de exportação (`padelizou-arena/Padel.Godot/export_presets.cfg`, Linux e Windows,
.NET), o `padelizou-arena/steam/app_build.vdf` (dois depots, `*.pdb` fora, `SetLive "beta"` comentado) e o
`padelizou-arena/steam/publicar.sh` (recusa rodar com App ID 0). Falta a conta Steamworks e o App ID; no desenvolvimento,
o App ID de teste é o 480 (`padelizou-arena/docs/STEAM.md`).

## O que só o Felipe faz

- Conta Steamworks, taxa do Steam Direct, dados fiscais e bancários, criar o app (sai o **App ID**) e os depots.
- O login no `steamcmd`, com a **conta de build** (nunca a pessoal) e o Steam Guard. O Claude nunca digita senha nem código.
- Pôr o build no branch **padrão** (o que todo mundo baixa) e apertar o botão de lançar. O `steamcmd` só põe no ar
  branches beta; o padrão é sempre no painel, pela mão do Felipe.
- Aceitar termos, pagar ou mudar preço.

## Passos

1. Verifique o jogo (skill `verificar`). Nada sobe com teste vermelho.
2. Empacote: com o Godot 4.7.2 .NET e os templates de exportação em `~/.local/share/godot/export_templates/4.7.2.stable.mono/`:
   ```bash
   cd padelizou-arena
   godot --headless --path Padel.Godot --import
   godot --headless --path Padel.Godot --export-release "Linux"   ../build/linux/PadelizouArena.x86_64
   godot --headless --path Padel.Godot --export-release "Windows" ../build/windows/PadelizouArena.exe
   build/linux/PadelizouArena.x86_64 --headless -- --auto --sair-apos 15   # o exportado roda sem editor
   ```
3. Confira o build antes de subir:
   - roda sem o editor e sem a pasta do código (abra o executável empacotado);
   - tamanho total e o que entrou: nada de `.pdb`, log, save de teste, arquivo-fonte de arte ou pasta `Saved/`;
   - versão do jogo visível no menu ou no log, igual à da tag;
   - licenças e créditos no build (skill `declaracao-ia` e o registro de licenças do jogo).
4. Prepare o SteamPipe: já pronto em `padelizou-arena/steam/app_build.vdf`: preencha `AppID` e os dois `DepotID` com os números do
   Felipe (o `ContentRoot` é `../build/`). No `app_build`, `SetLive` aponta para um branch beta (ex.: `"SetLive" "beta"`).
   Guarde fora do depot os símbolos (`.pdb`) da mesma versão: sem eles, a pilha de um crash vem só com endereços.
5. Peça ao Felipe para rodar o envio, com a conta de build: `STEAM_USUARIO=<conta_de_build> padelizou-arena/steam/publicar.sh`.
6. O Felipe instala pelo cliente Steam, no branch beta, e joga 5 minutos. Só então ele promove ao padrão no painel.
7. Anote a versão, a data, o branch e o tamanho em `padelizou-arena/docs/STEAM.md` e o `STATUS.md`.

## Armadilhas

- A Valve revisa a página e o build antes do lançamento e exige a página "Em breve" no ar por um tempo mínimo. Prazos
  mudam: confira no Steamworks antes de marcar data.
- Conteúdo de IA: a declaração da página tem que bater com o build (skill `declaracao-ia`).
- A página só promete o que o build mostra (`docs/PAGINA-STEAM.md`): os requisitos ainda estão "a medir".
- O Facepunch.Steamworks (decisão D5) ainda não está no projeto: conquistas, lobby e Cloud chegam no M2.
- Steam Deck: o build Linux é nativo; peça a revisão só com controle completo e texto legível a 1280×800.
