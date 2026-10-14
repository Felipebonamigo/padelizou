---
name: achar-gargalo
description: 'Acha o que está pesando no quadro do Padelizou Arena: qual linha de execução manda, qual função ou passo de GPU, quanto passa da meta (60 fps; passo de 8,3 ms) e onde anotar. Use quando o fps cair, antes de aprovar sistema novo de dano, tráfego, efeito ou multidão, ou quando o Felipe disser "está travando", "caiu o fps", "mede isso" ou "por que pesa".'
---
# Achar o gargalo

A meta: 60 fps numa GTX 1060 no preset alto e 40 fps no Steam Deck no médio (`padelizou-arena/REALISMO.md`,
`docs/PAGINA-STEAM.md`). A física roda a 120 Hz, então cada passo do `Padel.Core` cabe em 8,3 ms, e nenhuma chamada de
solucionador passa de 1 ms. Os três presets gráficos ainda não existem no código.

Medir o fps diz *se* pesa; esta rotina diz *o quê*. Sem saber o quê, otimização é chute.

## Passos

1. Reproduza com números: a mesma cena, a mesma câmera, o mesmo preset.
   Na nuvem: `dotnet run -c Release --project padelizou-arena/ferramentas/Desempenho` e os testes de trabalho. No PC:
   o jogo com `--print-fps` na cena que pesa e, para separar lógica e desenho, os monitores do Godot (abaixo).
2. Ache a linha de execução ou a etapa que manda (seções abaixo).
3. Ache a função ou o passo culpado e meça de novo sem ele (desligue o sistema, troque o efeito por nada) para provar.
4. Corrija o culpado e meça de novo nas mesmas condições. Sem antes e depois, não está provado.
5. Anote data, cena, preset, máquina, antes e depois, e o que mudou em o diário do marco em `padelizou-arena/docs/` (ou o bloco do `STATUS.md`), com a máquina, o preset e o commit.

## No Godot

1. **Quanto custa.** `dotnet run -c Release --project padelizou-arena/ferramentas/Desempenho [-- --execucoes 3 --sementes 1,2,3,4]`
   mede cada passo de 1/120 s em 12 partidas IA × IA: mediana, p99, p99,9, passos acima de 2, 4 e 8,3 ms, quem rodava nos
   lentos, os solucionadores `Chiquita`, `Contrapared`, `SmashPor3` e `SmashPor4` isolados, e o determinismo. O
   `GolpesEspeciaisDesempenhoTests` trava o trabalho (até 25.000 sub-passos por chamada, cerca de 0,75 ms), não o tempo.
2. **Onde está o tempo.** `--print-fps` na linha de comando imprime o fps a cada segundo. Para separar lógica de
   desenho, leia os monitores de `Performance` num script de teste (`TIME_PROCESS`, `TIME_PHYSICS_PROCESS`,
   `RENDER_TOTAL_DRAW_CALLS_IN_FRAME`, `RENDER_TOTAL_PRIMITIVES_IN_FRAME`, `OBJECT_NODE_COUNT`) e grave em CSV.
   No editor, o Profiler e o Visual Profiler (aba Depurador) mostram função por função e passo de GPU.
3. O `Padel.Core` não depende do Godot: custo de regra, física e IA se mede em `dotnet` puro, sem tela, na nuvem.
   Desenho e GPU só no PC.

## Armadilhas

- Média esconde engasgo: olhe também o p95 e o pior quadro.
- Primeira execução compila shader e carrega asset: descarte os primeiros segundos.
- Nenhum monitor de desempenho do Godot é lido hoje no código. Quem for o primeiro deixa o leitor num script de teste
  em `Padel.Godot/scripts/`, ligado por um argumento depois de `--`.
- No container o Godot roda no `gl_compatibility` com llvmpipe: fps de lá não vale nada.
