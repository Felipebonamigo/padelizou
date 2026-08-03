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
   da 5ª"). Ele decide. Substitui na prática o `RestricaoCategoria` manual.
3. **Travar (opt-in)**: o torneio marca "nivelamento Padelizou" e a faixa + soma valem
   como trava, junto com a regra do bicampeão.

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

## Decisões registradas

- Nome: **Padelímetro** (escolhido pelo Felipe em 03/08/2026, contra Zou/PDZ/Nível
  Padelizou). Slogan de guerra: "o Padelímetro não mente". Garantir @padelimetro no
  Instagram antes de anunciar.
- Grafia: com acento nas telas; `padelimetro` sem acento em slug/código.
- Exibição: "Padelímetro 620 · faixa da 4ª"; em pílula apertada, "620 · 4ª".
- Números NÃO coincidem com o QT Level de propósito (faixas deles: 250 de largura,
  cortes 1050/1300/1550/1750, teto ~2000; soma 1800–3300).
- O "ajuste de categoria" do QT (bump ao sair de chave) não é copiado: no Elo, sair de
  chave JÁ é o empurrão (2–3 vitórias pesadas contra quem foi), e o caso extremo é
  coberto pela regra do bicampeão.
