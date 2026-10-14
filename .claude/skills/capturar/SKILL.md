---
name: capturar
description: Tira captura de tela do Padelizou Arena (partida, menu, placar, vitrine de modelos) com o --screenshot do próprio jogo num X virtual e olha a imagem antes de afirmar qualquer coisa visual. Use ao mudar quadra, jogador, câmera, HUD, menu ou modelo 3D, ou quando pedirem "tira um print", "mostra como ficou" ou "antes e depois".
---
# Capturar o Padelizou Arena

O jogo tira a própria foto com `--screenshot ARQ.png` depois do `--` (`Padel.Godot/scripts/Interface/Captura.cs`: espera
0,8 s e dois quadros desenhados, grava e loga "Screenshot salvo em" ou "Screenshot FALHOU"). Sem tela ele recusa e sai 1,
de propósito. Por isso a foto precisa de uma tela, real ou virtual.

## Comando

Na nuvem e no CI, X virtual com o Mesa (llvmpipe). O `forward_plus` (Vulkan) não abre sem GPU: use o renderer de
compatibilidade (`docs/M0.md`). Dentro de `padelizou-arena/`:

```bash
xvfb-run -a -s "-screen 0 1280x800x24" <godot> --path Padel.Godot --rendering-method gl_compatibility \
  --audio-driver Dummy --resolution 1280x800 -- --auto --semente 42 --sair-apos 12 --screenshot /caminho/partida.png
```

- Partida: a foto sai no instante do "Saindo após" (`PartidaNode`). Troque `--sair-apos` para escolher o momento.
- Menu: `-- --tela principal|online|opcoes --screenshot x.png`.
- Placar: `res://cenas/TestePlacar.tscn -- --estado 1..5 --screenshot x.png` (normal, pausa, fim, ponto de ouro, tie-break).
- Modelos: `res://cenas/VitrineDeModelos.tscn -- --screenshot x.png` (a vitrine não entra no jogo).
- O llvmpipe desenha devagar: não use `--fixed-fps` (o relógio anda) e dê tempo ao `timeout`.
- Rode com `XDG_DATA_HOME=<pasta temporária>` para a rodada não gravar perfil nem carreira do jogador.

## Passos

1. Capture o antes, na mesma semente e com os mesmos argumentos (num `git worktree` do commit anterior, se a mudança já
   está feita).
2. Faça a mudança, rode `dotnet build` e o `--import`, e capture o depois.
3. Olhe as duas imagens com a ferramenta Read antes de dizer qualquer coisa sobre elas. Imagem que você não abriu não conta.
4. A régua do visual é o `REALISMO.md`: um quadro do jogo ao lado de um quadro de transmissão de padel (referência
   interna; nada de logo nem de jogador real no jogo).
5. Captura de trabalho fica fora do repositório (na pasta de rascunho da sessão). Só vai para `docs/imagens/` (1280×800)
   a que documenta uma tela, com o commit que a gerou.

## Armadilhas

- No container a cor e a luz do `gl_compatibility` não são as do `forward_plus` do PC: aprovação de visual final é do
  Felipe, no PC.
- Captura em 640×400 (a do `conferir_rodadas_de_teste.sh`) prova que a foto sai, não que a tela está boa.
