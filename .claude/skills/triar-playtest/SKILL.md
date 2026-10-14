---
name: triar-playtest
description: Ler, organizar e resolver os problemas de playtest do Padelizou Arena que chegam como Issues "[Playtest] Arena" no GitHub do padelizou. Use quando o Felipe disser que jogou o jogo de padel, mandar uma lista de problemas do jogo ou pedir para ver as Issues do Arena.
---
# Triar o playtest do Padelizou Arena

O jogo ainda mora no repositório `Felipebonamigo/padelizou`, junto da plataforma. Para não misturar com os defeitos do
site, as Issues do jogo têm o título "[Playtest] Arena: ..." e o rótulo `bug`. O formulário fica em
`padelizou-arena/.github/ISSUE_TEMPLATE/playtest.yml`, mas o GitHub só o mostra quando o jogo tiver repositório próprio.
Até lá, abra a Issue pelo conector do GitHub com os mesmos campos no corpo (o que aconteceu, onde, como repetir,
gravidade, versão, onde jogou, anexos). Se o Felipe mandar a lista pelo chat, abra uma Issue por item antes de começar.

## Passos

1. Liste as Issues abertas com "[Playtest] Arena" no título (`search_issues` no repositório `Felipebonamigo/padelizou`).
2. Ordene pela gravidade: trava, quebra a partida ou progresso, atrapalha, incômodo, sugestão. Sugestão não é defeito:
   vira item do `padelizou-arena/CRONOGRAMA.md`, e as que esperam playtest já estão listadas lá e no `DECISOES.md`.
3. Reproduza antes de mexer: a skill `capturar` ou o `ferramentas/rodar_sem_tela.sh` com a mesma semente
   (`--semente N`). Regra de jogo se reproduz sem tela, num teste do `Padel.Core.Tests`. Se não reproduzir, comente na
   Issue o que tentou e pergunte o que falta.
4. Corrija com teste: o defeito vira teste visto falhar antes da correção (regra 1 do CLAUDE.md da raiz). Se a correção
   saiu antes do teste, apague a correção e recomece.
5. Verifique com a skill `verificar`.
6. Commit pequeno, `Padelizou Arena: corrige <o quê> (fecha #N)`, no branch `claude/padel-game-new-9yl0ga`.
7. Comente na Issue o commit e o que mudou, numa frase, terminando com o rodapé do Claude Code. Se a correção depende de o
   Felipe jogar de novo, diga isso e deixe aberta.
8. Ao fim, resuma para o Felipe o que fechou, o que ficou aberto e por quê, e feche com `DONE`, `DONE_WITH_CONCERNS`,
   `NEEDS_CONTEXT` ou `BLOCKED`.

## Armadilhas

- O log do Godot traz o nome da sala e, na conexão por IP, o IP de quem hospeda (`docs/LEGAL.md`, C2): não deixe isso
  numa Issue pública.
- O playtest do M1 pergunta "parece padel?" antes de "é bonito?" (`CRONOGRAMA.md`): problema de regra ou de sensação vem
  antes de visual.
