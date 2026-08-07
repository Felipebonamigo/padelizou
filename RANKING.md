# RANKING.md — a espec oficial do ranking do Padelizou

> Decidida com o Felipe em 03/08/2026, nesta ordem: primeiro o desenho das duas trilhas,
> depois a escala única 0–1000, a soma da dupla e o nome **Padelímetro**.
> Este arquivo é a fonte da verdade. Mudou a regra? Muda AQUI primeiro.

## Por que duas trilhas

O concorrente (Quanto Tá) tem dois números e nós também teremos — porque eles medem
coisas opostas e misturá-los cria o "campeão eterno da 5ª":

| | Ranking anual (pontos) | Padelímetro (nível) |
|---|---|---|
| Mede | A campanha do ano | A habilidade agora |
| Movimento | Só soma; expira em 12 meses | Sobe E desce a cada jogo |
| Recorte | Por categoria | Um número por jogador |
| Serve para | Premiar (Melhores do Ano/mês, líder não paga inscrição) | Decidir ONDE a pessoa pode jogar |

Ponto premia presença × resultado — jogar mais nunca diminui. Por isso ponto JAMAIS
decide categoria: quem farmasse torneio fraco subiria sem melhorar. Quem decide
categoria é o Padelímetro.

## Trilha A — o Padelímetro

Um número de **0 a 1000** por jogador. O motor é Elo clássico rodando DIRETO nessa
escala (sem conversão): expectativa com divisor 400, faixas de categoria com 100 de
largura. 100 pontos de diferença ≈ 64% de favoritismo; 200 ≈ 76%.

### O que move o número

- **Cada partida de torneio**, no momento em que a Mesa de Controle finaliza o placar.
- Nível da dupla = **média** dos dois; a expectativa do jogo compara as duas médias.
- Variação de cada jogador = `K_dele × fator_de_games × (resultado − expectativa)`,
  arredondada. O resultado é 1 pra quem venceu, 0 pra quem perdeu — cada um usa o
  próprio K, então parceiro em calibração anda mais rápido que o veterano do lado.
- **K = 40** nos primeiros 10 jogos ("em calibração", selo cinza), **K = 20** depois.
- **Fator de games**: `1 + 0,1 × min(|diferença de games|, 6)` — 6x0 vale 1,6×, 7x6
  vale 1,1×. Ganhar passeando move mais que ganhar no detalhe.
- Na prática: vitória contra dupla igual ≈ ±7 a ±13. Um fim de semana dominante rende
  +60 a +100 — sobe de faixa em 1 ou 2 torneios dominados, nunca por um dia de sorte.

### O que NÃO move o número

- **Torneio Restrito** (mesma razão do `ContaNoRanking`: evento fechado não mede padel
  contra o mundo).
- **Americano**, nos dois formatos — individual e de duplas (decisão do Felipe,
  07/08/2026). Até aqui ele pontuava IGUAL a uma final de 3ª Categoria: três amigos
  criavam um Americano, lançavam os placares que quisessem e fabricavam ranking sem
  enfrentar ninguém de fora. E o estrago não era só o campeão — no Americano **cada
  rodada cria uma dupla nova**, então um rodízio de 12 pessoas despejava dezenas de
  linhas de "participou" de uma vez. O Americano passa a ter **ranking próprio**
  (Trilha C, abaixo). A regra mora em `Services/FormatoDoTorneio.EhAmericano`.
- **Categoria de times** e qualquer dupla-TIME (`Dupla.NomeTime != null` — o
  `Jogador1Id` ali aponta pro organizador que cadastrou, não pra quem jogou).
- **Partida sem placar** (games nulos) ou com dupla incompleta.
- **W.O.** — jogo que não aconteceu não mede nada. (Hoje o sistema nem tem W.O. formal;
  quando tiver a marca, ela entra aqui.)
- Jogo avulso, Raquete Livre, jogo de grupo de amigos: fora do Padelímetro oficial
  (decisão do Felipe — um "nível amistoso" separado pode existir no futuro).

### Onde o número nasce (seed)

O jogador ganha Padelímetro **na primeira partida contabilizada**, com o valor de
entrada da categoria em que ela foi jogada. Antes disso o perfil mostra "sem
Padelímetro — jogue um torneio". Quem vem do Quanto Tá entra se inscrevendo na
categoria que já joga lá, e o seed faz o resto; o K de calibração conserta exageros
em 1–2 torneios.

### Faixas e valores de entrada

Escala ÚNICA para todo mundo — é o que permite mulher jogar categoria masculina sem
regra especial (o nível dela diz onde cabe). O degrau feminino↔masculino abaixo é
**provisório e por chute educado**; diferente do concorrente (que cravou o offset por
decreto), nós calibramos com dados reais: todo jogo de MISTA cruza as duas populações.

| Masculina | Faixa | Entrada | Soma da dupla |
|---|---|---|---|
| Open / 1ª | 850+ | 900 | — |
| 2ª | 750–849 | 800 | ≤ 1650 |
| 3ª | 650–749 | 700 | ≤ 1450 |
| 4ª | 550–649 | 600 | ≤ 1250 |
| 5ª | 450–549 | 500 | ≤ 1050 |
| 6ª | 350–449 | 400 | ≤ 850 |
| 7ª / Iniciantes | até 349 | 300 | — |

| Feminina (provisório) | Faixa | Entrada | Soma da dupla |
|---|---|---|---|
| Open / 1ª fem | 550+ | 600 | — |
| 2ª fem | 450–549 | 500 | ≤ 1050 |
| 3ª fem | 350–449 | 400 | ≤ 850 |
| 4ª fem | 250–349 | 300 | ≤ 650 |
| 5ª fem | 150–249 | 200 | ≤ 450 |
| 6ª fem | 60–149 | 120 | — |
| 7ª / Iniciantes fem | até 59 | 40 | — |

| Mista (não trava nada; só dá o seed) | Entrada |
|---|---|
| Mista A | 550 |
| Mista B | 450 |
| Mista C | 350 |
| Mista D | 250 |

- A soma segue o padrão `2 × teto − 50`: dois jogadores no topo da mesma faixa não
  jogam juntos NELA — ou um acha parceiro mais leve, ou a dupla sobe. É o segundo
  porteiro, contra o quase-4ª que "carrega" um parceiro fraco de fachada na 5ª.
- Subir de categoria é SEMPRE livre (qualquer dupla joga acima da própria faixa, sem
  teto de soma). A trava — quando existir — é só pra baixo.
- Mistas ficam fora da escada de travas de propósito (mesma filosofia do troféu de
  vidro): jogo de mista MOVE o Padelímetro, mas Mista A/B/C/D não têm faixa de
  bloqueio por enquanto.

### Subir e descer de faixa (histerese)

- **Subir é imediato**: cruzou o teto → a próxima inscrição já é na categoria de cima.
- **Descer tem folga**: só volta pra faixa de baixo com nível 50 pontos ABAIXO do piso
  E pelo menos 10 jogos desde que subiu. Sem pingue-pongue na divisa, sem perder de
  propósito pra descer.
- **Regra do bicampeão** (legível por humanos, independe do número): campeão 2× da
  mesma categoria em 12 meses sobe automaticamente.

### Fases de lançamento (para não queimar o produto)

1. **Mostrar** (fase atual): número no perfil, variação por jogo, "faltam X pra
   subir". NADA trava. Roda 1–2 meses calibrando com torneios reais.
2. **Avisar**: alerta na inscrição pro organizador ("este jogador está acima da faixa
   da 5ª"). Ele decide.
3. **Travar (opt-in)**: o torneio marca "nivelamento Padelizou" e a faixa + soma valem
   como trava, junto com a regra do bicampeão.

## Quem trava a inscrição hoje (06/08/2026)

**Só o Ranking RS.** Decisão do Felipe: quem nivela é o ranking gaúcho, que enxerga o
estado inteiro — a nossa régua interna só conhece quem já jogou aqui, e no começo isso
é pouca gente pra barrar alguém com credibilidade.

| Medidor | Trava? |
|---|---|
| Pontos por categoria (trilha B) | Não — exibe |
| Nível comprovado (`RestricaoCategoria`) | **Dormente** desde 06/08 |
| **Ranking RS** (`ValidarPeloRankingRs`) | **Sim** — e o organizador dá a palavra final |
| Padelímetro (PDZ) | Não — fase 1 |

A trava do Ranking RS é MACIA de propósito: a recusa vira `BloqueioDoRanking` pendente e
o organizador decide (Liberar/Manter); API fora do ar nunca vira recusa; e categoria sem
de-para (`Categoria.RankingRsCategoriaId`) não é validada.

**Onde se liga:** na criação do torneio (caixa "Conferir as inscrições no Ranking RS",
nasce desmarcada) e depois em Editar torneio. Ao criar, o de-para de cada categoria é
preenchido pelo palpite do nome (`CategoriaDoRankingRs.Adivinhar`) — seguro porque o
catálogo padrão sempre escreve o sexo, que é o que o palpite exige. Não é calado: a
mensagem de sucesso diz quantas serão conferidas e **nomeia as que ficaram de fora** (7ª,
Iniciantes e Mista D não existem no Ranking RS). A revisão fina segue em Editar torneio.

O `RestricaoCategoria` continua no código, testado e funcionando — só saiu das telas, e a
migração `TravaDeCategoriaSoPeloRankingRs` zerou as linhas ligadas pra ninguém ficar
barrado por uma regra sem tela onde ser desligada. Voltar a oferecer é devolver o
`<select>` em `Views/Torneios/Create.cshtml` e `Details.cshtml`.

**O que isso deixa descoberto, de propósito:** torneio fora do RS, categoria sem de-para
e integração desligada ficam sem nivelamento nenhum. É o preço aceito enquanto o
Padelímetro não chega na fase 3.

### O Americano não tem trava, e é decisão (06/08/2026)

`TorneiosController.InscreverIndividual` — a inscrição do formato Americano — não chama
nenhuma das duas regras. **Não é esquecimento: o Felipe decidiu que segue assim.**

Faz sentido pelo próprio formato: no Americano o parceiro TROCA a cada rodada e todo
mundo joga com todo mundo, então misturar nível é o objetivo, não o defeito. A inscrição
é individual, quase sempre resolvida no dia, e barrar alguém ali quebraria o rodízio —
que precisa de número fechado de gente pra fechar as rodadas.

Quem for mexer nesse método: a ausência das duas checagens é para ficar como está.

### Implementação

- Motor puro em `Services/Padelimetro.cs` (estático, testável, sem banco).
- Estado no `Jogador`: `Padelimetro` (int?, nulo = nunca jogou) e
  `JogosDePadelimetro` (conta pro K de calibração).
- Extrato em `HistoricoDePadelimetro` (JogadorId, PartidaId?, NivelAntes, Delta,
  Motivo, CriadoEm) — é o que desenha o gráfico e explica cada movimento.
- Gancho único: `FinalizarPartida` (Mesa de Controle) — é o único lugar do sistema
  que carimba `Status = "Finalizada"`.
- **Replay determinístico**: recalcular tudo do zero a partir das partidas
  finalizadas, em ordem cronológica. É o que dá o retroativo (nascemos com histórico,
  não com folha em branco) e o que permite mudar regra sem sujeira — mudou a fórmula,
  roda o replay de novo. Botão no admin.

## Trilha B — o ranking anual (pontos)

- Pontos por fase alcançada (tabela atual: Campeão 100, Vice 60, Semi 35, Quartas 20,
  participou 10), somados **por categoria**.
- **Validade de 12 meses móveis**: o ponto de um torneio expira 12 meses depois do
  `DataInicio`. O perfil mostra "pontos a defender" (estilo ATP) — urgência boa.
- **Melhores do Ano**: corte em 31/12 por categoria, selo automático no perfil.
- **Benefício de líder**: o organizador pode configurar "1º do ranking da categoria
  não paga inscrição" (ou desconto) — e como o pagamento passa por nós, o desconto
  aplica SOZINHO na inscrição. Regra no papel vira recurso do produto.
- Torneio Restrito continua fora (já é assim hoje).
- Status: ainda não implementada — hoje os pontos somam a vida toda e sem recorte por
  categoria. Esta trilha é o próximo passo depois da fase 1 do Padelímetro.

## Trilha C — o Ranking Americano (07/08/2026)

Ranking **separado** do oficial, porque o Americano é outro esporte social: rodízio,
parceiro trocando a cada rodada, gente conhecida, criado na véspera. Misturar os dois
estraga os dois — o oficial deixa de medir torneio, e o Americano fica sem lugar.

- **Sai do oficial e do Padelímetro** (ver "O que NÃO move o número"). Continua no
  histórico da pessoa: ela jogou, e isso aparece no perfil.
- **Só pontua se o organizador contratar**: **R$ 5 por pessoa inscrita**, decidido na
  criação do Americano — a tela avisa o preço ali, antes de ele publicar.
  Não contratou, o Americano roda normal e não gera ponto nenhum.
- **Piso de 8 inscritos** (decisão do Felipe, 07/08/2026). Abaixo disso não vale ponto
  e não se cobra nada. 4 ou 5 pessoas é o tamanho em que quatro conhecidos fabricam
  resultado sem esforço — e o peso sozinho não resolvia, só fazia o ponto ser menor;
  ponto menor toda semana continua sendo ponto de graça. O piso é o mesmo 8 da
  referência de peso, de propósito: "vale a partir de 8, e 8 é o peso 1" é uma frase só.
- **Pontos = colocação × peso** (`Services/PontosDoAmericano`). Tabela 100/60/40/25/10
  por colocação — a mesma FORMA da oficial, porque as duas aparecem no mesmo perfil e
  duas escalas fariam a pessoa comparar números que não se comparam. Peso = pessoas ÷ 8,
  linear: com o teto de 16 o peso vai no máximo a 2×, então não há a distorção de uma
  tabela sem teto. ⚠️ Arredondamento **AwayFromZero**, não o `ToEven` padrão do .NET —
  com ToEven, 12,5 vira 12 e 15,5 vira 16, e dois jogadores com a mesma conta receberiam
  pontos diferentes conforme a paridade.
- **Grátis até 16 pessoas** vale pro Americano em si (o evento não tem taxa). O R$ 5
  é outra coisa: é o preço de VALER PONTO, e independe do tamanho.
- Acima de 16 pessoas o Americano não é criado sozinho — a tela manda falar com o
  Felipe (por enquanto, decisão de 07/08/2026).

Status: a exclusão do oficial está no ar. A trilha própria (tabela de pontos, tela e
cobrança dos R$ 5) ainda não — é o próximo passo.

## Decisões registradas

- Nome: **Padelímetro** (escolhido pelo Felipe em 03/08/2026, contra Zou/PDZ/Nível
  Padelizou). Slogan de guerra: "o Padelímetro não mente". Garantir @padelimetro no
  Instagram antes de anunciar.
- Grafia: com acento nas telas; `padelimetro` sem acento em slug/código.
- **Unidade: PDZ** (decisão do Felipe, 03/08/2026) — o número se lê "620 PDZ".
- Exibição: "Padelímetro 620 PDZ · faixa da 4ª"; em pílula apertada, "620 · 4ª".
- Onde mora no site: aba **Padelímetro** dentro da página de Ranking (ao lado do
  ranking por categoria), respeitando o mesmo filtro regional do hub.
- Números NÃO coincidem com o QT Level de propósito (faixas deles: 250 de largura,
  cortes 1050/1300/1550/1750, teto ~2000; soma 1800–3300).
- O "ajuste de categoria" do QT (bump ao sair de chave) não é copiado: no Elo, sair de
  chave JÁ é o empurrão (2–3 vitórias pesadas contra quem foi), e o caso extremo é
  coberto pela regra do bicampeão.
