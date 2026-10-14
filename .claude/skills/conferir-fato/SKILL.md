---
name: conferir-fato
description: 'Confere em fonte confiável o texto do Padelizou Arena que fala do mundo real (regras e medidas do padel), anota a fonte e só então libera o texto. Use antes de escrever ou mudar regra do árbitro, medida da quadra ou da bola, pontuação de torneio, nome de golpe ou texto de tutorial, quando houver texto marcado para revisão, ou quando o Felipe perguntar "isso está certo?".'
---
# Conferir um fato

O diferencial do Arena é jogar padel de verdade (`padelizou-arena/CRONOGRAMA.md`): regra, medida e golpe seguem a
fonte oficial. Nada de jogador real, marca ou logo; FIP e Premier Padel são marcas de terceiros, só referência interna
(`docs/LEGAL.md`, A12).

## Onde está o texto

- Já com fonte: a bola da FIP (56 a 59,4 g; 6,35 a 6,77 cm; quique de 135 a 145 cm caindo de 2,54 m) em
  `Padel.Core/Bola.cs` e `Padel.Core.Tests/RealismoTests.cs`.
- A conferir: as paredes, "regra FIP, simplificada" (`Padel.Core/Quadra.cs`); o Magnus pela fórmula de "Štěpánek" sem
  referência completa (`Bola.cs`); os pontos FIP e Premier Padel 2025 em `Padel.Core/Torneio/EscalaDePontos.cs` (marcado
  ⚠️ "a página não abriu"); as regras de `Padel.Core/Arbitro.cs` (só o resumo no comentário); e as velocidades "pró" do
  `REALISMO.md` ("aproximado").
- Rivais da carreira: 40 sobrenomes genéricos (`Padel.Core/Torneio/Carreira.cs`); confira que nenhum par vira o nome de
  um jogador real.

## Fontes que valem

- Regulamento oficial da FIP (padelfip.com) e da confederação brasileira, na versão do ano.
- Planta oficial de quadra panorâmica, para as medidas das paredes.
- Artigo científico com DOI, para a física da bola.
Não valem: busca de IA, resumo de blog, Wikipédia sozinha (serve para achar a fonte primária), página sem autor nem data.

## Passos

1. Liste o que falta conferir: os ⚠️ e os "a conferir" do código (`grep -rn 'conferir\|aproximado\|⚠️' padelizou-arena/Padel.Core`), mais o que
   o pedido muda.
2. Para cada fato (data, medida, autor, regra, nome), abra a página da fonte com o WebFetch. Trecho de resultado de busca
   não é fonte. Para pesquisa longa, use a skill `deep-research`.
3. Se a fonte confirma: anote a fonte (URL, versão do regulamento ou DOI, e data) no comentário do código, ao lado do número, como em `Bola.cs`;
   regra nova vira teste em `Padel.Core.Tests`, com a fonte no comentário.
4. Se a fonte desmente ou não acha: reescreva o texto com o que a fonte diz, ou tire o fato. Não deixe "aproximadamente" no lugar de conferir.
5. Texto que nasceu de IA continua sendo de IA depois de conferido: ele entra na declaração (skill `declaracao-ia`).
6. Commit com a lista do que foi conferido e as fontes.

## Armadilhas

- Mudar regra muda a partida: rode o `verificar_tudo.sh` e confira se a calibração (`ferramentas/Calibracao`) ainda bate.
- Os golpes têm nome espanhol que todo jogador de padel usa (bandeja, víbora, chiquita): a forma por idioma fica no
  glossário (skill `traduzir`).
