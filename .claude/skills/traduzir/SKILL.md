---
name: traduzir
description: 'Escreve e confere o texto do Padelizou Arena nos idiomas do jogo (PT-BR, EN e ES), com um glossário único dos termos, e acha texto que ficou fora da tradução. Use ao criar ou mudar qualquer texto que o jogador lê (menu, HUD, tutorial, loja), quando o Felipe pedir "traduz", "põe em inglês/espanhol" ou "tem texto sem tradução".'
---
# Traduzir

Idiomas: PT-BR, EN e ES no lançamento; IT e SV na 1.0; FR depois (`padelizou-arena/CRONOGRAMA.md`, `docs/PAGINA-STEAM.md`).

## Como o texto funciona neste jogo

- O sistema de tradução da interface ainda não existe: os textos estão fixos em português nos scripts C# de
  `Padel.Godot/scripts/` (ex.: `CarreiraNode.cs`, `PainelDeOpcoes.cs`), e não há opção de idioma.
- O que já é traduzido: as 20 conquistas e o Rich Presence, pelo `TextoLocalizado(...).Em(idioma)` de
  `Padel.Core/Perfil/Conquistas.cs` (`enum Idioma { Portugues, Ingles, Espanhol }`), testado em `PerfilTests.cs` e
  `ConquistasTests.cs`; a tabela do `docs/CONQUISTAS.md` é conferida por teste.
- Para a interface, o caminho nativo do Godot é um CSV de tradução (`keys,pt_BR,en,es`, importado como `.translation`
  e ligado em Projeto › Localização) com `Tr("CHAVE")` no C#. É decisão de arquitetura: proponha ao Felipe antes de criar.

## Passos

1. Escreva o texto já em todos os idiomas do jogo, no mesmo commit. Texto só em PT não entra.
2. Use os termos do glossário: `padelizou-arena/docs/GLOSSARIO.csv` (colunas `termo_pt`, `en`, `es`, `nota`; crie na primeira vez). Termo novo de jogo (golpe, modo, peça, recurso) entra no glossário
   antes de aparecer na tela.
3. Confira: `dotnet test padelizou-arena/Padel.Core.Tests/Padel.Core.Tests.csproj` (conquistas e presença nos 3 idiomas);
   na interface, enquanto não houver o CSV, todo literal em português nos `*.cs` de `Padel.Godot/scripts/` é texto sem tradução.
4. Texto longo (tutorial, loja): releia no idioma de destino como alguém daquele país leria. Tradução literal de
   gíria gaúcha ou de futebol não funciona; ache o equivalente.
5. Veja na tela: o inglês e o espanhol costumam ser 20 a 30 % mais longos que o português. Capture as telas nos 3 idiomas (skill `capturar`) e confira a 1280×800, a tela do Steam Deck.

## Armadilhas

- Na Steam, "Espanhol - Espanha" e "Espanhol - América Latina" são idiomas separados; o mesmo vale para "Português -
  Brasil" e "Português - Portugal". Declare só os que o jogo tem de verdade.
- Número, data e moeda mudam com o idioma (1.000,5 em PT, 1,000.5 em EN).
- Nome próprio de lugar real, marca e nome de pessoa não se traduzem; e o jogo não tem marca real (CLAUDE.md).
- Golpe tem nome espanhol que o jogador usa em todo idioma (bandeja, víbora, chiquita): não traduza; o glossário dá a forma.
- "Ponto de ouro" é "punto de oro" em espanhol e "golden point" em inglês.
