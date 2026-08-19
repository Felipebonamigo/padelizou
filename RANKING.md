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
- **E a campanha da categoria também move** — bônus de campeão, pena de quem fica na
  chave, limitados pelas portas da faixa. Régua completa logo abaixo.

### A campanha também move o número (19/08/2026)

Decisão do Felipe, revendo a decisão de 03/08 ("no Elo, sair de chave JÁ é o empurrão"):
*"se for eliminado na chave, perde mais pontos; se for campeão, ganha mais pontos"*. O
motivo é o caminho de VOLTA: quem sobe de categoria não pode mais jogar a anterior — e no
Elo puro quem apanha na categoria nova desce DEVAGAR de propósito (a expectativa desconta
o adversário mais forte, então derrota esperada quase não tira ponto). Subir às vezes leva
anos; quem subia e não parava em pé ficava PRESO no andar de cima, sem número pra voltar.
A campanha passa a falar direto no número:

| Campanha na categoria | Ajuste |
|---|---|
| Campeão | **+10** |
| Caiu na estreia do mata-mata | **−5** |
| Não saiu da chave (ficou nos grupos) | **−10** |

- O tamanho é UM JOGO típico de propósito (K estável entre iguais ≈ ±10): "título vale um
  jogo ganho a mais, ficar na chave custa um jogo perdido a mais" é uma frase que qualquer
  jogador confere no grupo do WhatsApp.
- Vice e fases do meio não levam nada — esses degraus o Elo já pagou jogo a jogo. E quando
  a estreia do mata-mata É a própria final (1 grupo, final direta), quem perdeu é o vice:
  chegou na final, não leva pena de estreia.
- Aplica no FECHAMENTO DA FINAL da categoria (o mesmo gancho que coroa o campeão), uma vez
  por categoria, com linha própria no extrato — sem partida, porque campanha não é jogo:
  não conta pro K de calibração nem pra contagem de jogos.
- **Só pra quem entrou em quadra** em jogo que conta. Campanha inteira de W.O. não mediu
  padel nenhum — mesma razão do W.O. não mover o número.
- Mesmos porteiros das partidas: restrito, Americano, times e cancelado ficam fora. E
  **mista/casal/categoria fora da convenção ficam fora da campanha** (os JOGOS delas
  seguem movendo o número): sem faixa não há porta pra régua mirar.
- Correção de placar que TROCA o vencedor da final não reaplica nada sozinha — o extrato
  é a memória do que já foi aplicado, igual aos jogos. O caminho certo é REABRIR a final
  (desfaz jogo e campanha, e refaz os dois ao finalizar de novo) ou o replay do admin.

⚠️ **AS PORTAS DA FAIXA LIMITAM O AJUSTE — a parte que impede a régua de ser rígida.**
Aviso do Felipe na mesma conversa: *"tem pessoas que passam vários anos (uns 20 torneios)
na mesma categoria — temos que cuidar para não ser muito rígido"*. E um ajuste fixo seria
exatamente isso: em todo torneio METADE do campo não sai da chave e só UM é campeão, então
uma pena solta drenaria o jogador mediano — uns −7 de deriva por torneio, e 20 torneios
depois ele teria caído uma faixa inteira SEM ter piorado. A régua então mira as portas:

- **A pena só age acima da linha de descida da faixa da categoria jogada** (piso − 50, a
  mesma folga da histerese) **e para NELA**: te leva até a porta de voltar pra categoria
  de baixo, nunca através. Dali pra baixo, só derrota de verdade move — a campanha te
  apresenta à porta; quem te empurra por ela é o jogo.
- **O bônus só age abaixo da linha de subida** (teto + 1) **e para NELA**: campeão com
  número já acima da categoria não ganha nada — farmar título de categoria fraca não
  infla, que é o buraco clássico deste tipo de bônus.
- **Quem joga PRA CIMA não leva pena**: nível abaixo da linha de descida da categoria
  jogada significa "essa régua não é a sua" — cair na estreia da 2ª sendo da 4ª é
  aventura, não campanha ruim (o mesmo espírito do "subir é sempre livre").
- O jogador mediano de categoria encontra EQUILÍBRIO: as penas empurram pro fundo da
  faixa, e no fundo da faixa as vitórias pagam caro (Elo contra campo mais forte) e
  empurram de volta. Ele fica os 20 torneios dele na categoria, como sempre ficou. Quem
  NÃO ganha nunca — o caso que o ajuste existe pra soltar — é o único que a régua carrega
  até a linha de descida.

O preço aceito: o Padelímetro deixa de ser soma-zero (títulos injetam ponto, chaves
drenam). As portas limitam o vazamento, e o replay recalibra a história inteira se a régua
mudar. Motor em `Services/CampanhaNoPadelimetro.cs`; as linhas de descida/subida moram em
`FaixasDePadelimetro`, que é de onde a trava da fase 3 vai ler as mesmas portas.

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
- **Quem torna as duas portas alcançáveis é o ajuste de campanha** (seção acima): no Elo
  puro, quem só apanhava descia devagar demais pra voltar em tempo humano.
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

### A mão contrária: eles leem os nossos torneios (12/08/2026)

Até aqui a parceria era de mão única — a gente perguntava, eles respondiam, e eles não tinham
como saber onde as perguntas iam nascer. O pedido veio deles, com o escopo escrito por eles
mesmos: *"uma API que libera buscarmos os torneios pontuados no ranking — apenas informações
dos torneios, como nome, clube, data, foto. Não precisa nenhum dado dos atletas."*

`GET /api/ranking/torneios`, com a chave no cabeçalho `x-api-key`. **O documento que vai pra
eles é o [API-TORNEIOS.md](API-TORNEIOS.md)** — este parágrafo é só o ponteiro.

Três decisões que valem registro:

- **Quem entra é `ValidarPeloRankingRs` + inscrição aberta + já público no site**
  (`Services/TorneiosParaOParceiroDoRanking.EntraNaLista`). A primeira condição é a MESMA
  coluna do acerto de R$ 1 por inscrito, então a lista que eles leem e a conta que a gente
  paga enxergam o mesmo conjunto de torneios — que é como uma parceria deve se comportar.
- **A régua de visibilidade é a da vitrine** (`PermissaoDeOrganizador.ApareceParaOPublico`), e
  não uma cópia. Aqui o estrago de uma cópia desatualizada é maior que numa tela nossa: quem
  lê é um site de terceiro, que publica o que a gente mandar e não tem como desconfiar.
- **Nenhum dado de atleta, e isso é teste, não promessa.** Nem a contagem de inscritos — "só um
  totalzinho" é como escopo combinado vira outro sem ninguém decidir nada. O teste serializa a
  resposta inteira e a confere contra os dados de quem está inscrito.

⚠️ **São DUAS chaves agora, e elas não se misturam**: `RankingRs__ApiKey` é a chave DELES, que
a gente manda pra perguntar; `RankingRs__ChaveDoParceiro` é a NOSSA, que eles mandam pra ler.
Conferir a primeira na nossa porta entregaria a chave deles a quem batesse nela.

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

- Pontos por fase alcançada **multiplicados pelo tamanho da categoria** (a régua completa
  está logo abaixo), somados **por categoria**.
- **Validade de 12 meses móveis**: o ponto de um torneio expira 12 meses depois do
  `DataInicio`. O perfil mostra "pontos a defender" (estilo ATP) — urgência boa.
  ⚠️ **ADIADO por decisão do Felipe (10/08/2026): por enquanto os pontos NÃO expiram.**
- **Melhores do Ano**: corte em 31/12 por categoria, selo automático no perfil.
- **Benefício de líder**: o organizador pode configurar "1º do ranking da categoria
  não paga inscrição" (ou desconto) — e como o pagamento passa por nós, o desconto
  aplica SOZINHO na inscrição. Regra no papel vira recurso do produto.
- Torneio Restrito continua fora (já é assim hoje).
- Status: o **peso por tamanho** está feito (10/08/2026, abaixo). A validade de 12 meses,
  os Melhores do Ano e o benefício de líder continuam por fazer.

### O peso por tamanho da categoria (10/08/2026)

Até aqui a tabela era fixa e **cega ao tamanho**: campeão de 4 duplas e campeão de 32
levavam os mesmos 100 pontos, um tendo ganho 2 jogos e o outro 5 ou 6 contra um funil
muito maior. E tudo abaixo de Quartas caía no mesmo "participou 10" — ou seja, quanto
MAIOR o torneio, mais fases o ranking ignorava.

**A régua nova, em uma frase:** todo mundo leva `pontos da fase × peso`, e o peso é
**1,0 com 5 duplas, +0,1 por dupla**.

#### A escada de fases

| Fase alcançada | Base | Multiplica? |
|---|---|---|
| Campeão | 100 | sim |
| Vice (perdeu a Final) | 60 | sim |
| Semifinal | 35 | sim |
| Quartas de Final | 20 | sim |
| Oitavas de Final | 15 | sim |
| 16-avos (`"Primeira Rodada"`, o quadro de 32) | 12 | sim |
| Fase de grupos / participou | 10 | sim |

⚠️ **As duas fases do meio (Oitavas e 16-avos) são NOVAS.** Elas já existiam no
chaveamento (`ChaveamentoMataMata.NomeFase`) e não existiam na pontuação: quem sobrevivia
aos grupos de uma categoria grande e caía nas oitavas pontuava igual a quem perdeu tudo no
grupo. No código a primeira rodada do quadro de 32 se chama `"Primeira Rodada"`; **na tela
ela é "16-avos"** — o nome interno ficou porque renomear constante que decide chaveamento
não vale o risco.

⚠️ **A participação TAMBÉM multiplica** (decisão do Felipe, 10/08/2026 — ele reviu a
primeira versão desta espec, em que os 10 eram fixos). Cair no grupo de uma categoria de 20
duplas vale 25; na de 5, vale 10. **É a lógica do circuito profissional**: perder na
estreia de um Slam paga mais que vencer uma rodada de torneio pequeno, porque entrar
naquele campo já é mais difícil.

O que eu tinha argumentado contra — "premiaria aparecer num torneio grande e perder tudo" —
**deixa de valer junto com a regra do parágrafo seguinte**: agora não se ganha nada por
aparecer. Ganha-se por ter jogado, e o degrau pra quem sobrevive à chave continua existindo
(numa de 20 duplas: 25 no grupo contra 30 nos 16-avos).

#### O ponto só nasce quando a bola rola (10/08/2026)

⚠️ **Pedido do Felipe, e era um buraco de verdade:** *"só dê esses pontos quando o torneio
começar e a pessoa tiver inscrita"*. Até aqui, **a inscrição sozinha já valia ponto** —
`Dupla.UltimaFase` nasce com `"Grupos"`, então quem se inscrevesse num torneio marcado pra
dezembro subia no ranking **hoje**, sem ter tocado numa bola. Com a participação passando a
multiplicar, isso deixaria de ser um detalhe e viraria a maneira mais barata de subir:
inscrever-se na categoria mais cheia que existir.

Duas condições, as duas obrigatórias:

1. **O torneio COMEÇOU** — o sorteio aconteceu e os jogos existem. Em status: **não**
   `"Inscrições Abertas"` (`PortaDaInscricao.Aberta`), **não** `"Chaves em Sorteio"`
   (`PortaDaInscricao.Fechada` — inscrição encerrada, chave ainda não sorteada) e **não**
   `"Cancelado"`. Sobram "Fase de Grupos" e "Finalizado", que só se alcança tendo sorteado.
2. **A pessoa estava NA CHAVE** — a régua é `ForaDoSorteio`, que já existe e já é a dona da
   pergunta "quem não entra no sorteio": fora quem está em **lista de espera** e fora quem
   ficou **sem parceiro**. Os dois se inscreveram; nenhum dos dois jogou.

⚠️ **Torneio cancelado não paga nada, nem a participação.** O torneio pode ser cancelado
depois de sorteado, e nesse caso os pontos que existiam **somem** — é o comportamento certo
(o evento não aconteceu), e é o único caminho em que o total de alguém diminui sem ninguém
ter mexido na regra.

#### O peso

```
peso = 0,5 + (duplas da categoria ÷ 10)
```

**Sem teto** (decisão do Felipe, 10/08/2026): 25 duplas = 3,0 · 26 = 3,1 · 30 = 3,5 ·
40 = 4,5. Um teto criaria uma zona plana onde 25 e 40 duplas valem igual — exatamente a
injustiça que este trabalho existe pra consertar.

- **5 duplas = 1,0 é o ponto de calibração**: no torneio pequeno de sempre os números não
  mudam, e o ranking que já existe quase não se mexe.
- **Linear, +0,1 por dupla, sem degraus.** Degrau cria fronteira ("com 11 vale menos que
  com 12"), e fronteira em régua de ponto vira briga e vira manipulação de inscrição.
  Foi o pedido do Felipe: *"como muitas vezes não são múltiplos de 4"*.
- **É o tamanho da CATEGORIA, não do torneio** — é contra o funil da SUA chave que se
  jogou. Um torneio de 60 duplas com 6 categorias não é um torneio de 60 duplas pra
  ninguém.
- **Conta quem entrou na chave**: fora `NomeTime != null` (dupla-TIME, cujo `Jogador1Id` é o
  organizador) e fora quem `ForaDoSorteio` deixa de fora (lista de espera, sem parceiro).
  **É a MESMA régua que decide quem pontua** — se o peso contasse gente que a soma não
  conta, a categoria teria dois tamanhos ao mesmo tempo.
- **Piso de 3 duplas pra valer campanha** (proposto por mim e **confirmado pelo Felipe em
  10/08/2026**, quando ele perguntou o que era o piso — antes disso era escolha minha dentro
  de um "pode fazer", que é coisa diferente de regra decidida): com 1 dupla o "campeão" não
  jogou nada e com 2 ganhou um jogo só — é fabricável em cinco minutos, a mesma porta que o
  piso de 8 fecha no Americano. Abaixo de 3, a campanha desaba pra participação e **todo mundo da categoria sai
  com a mesma pontuação** (com 2 duplas: 10 × 0,7 = 7 pra campeão e vice). O piso não apaga
  o ponto de quem jogou; ele só se recusa a pagar título fabricado.
- **Arredondamento `AwayFromZero`**, nunca o `ToEven` padrão do .NET — com ToEven, dois
  jogadores com a mesma conta receberiam pontos diferentes conforme a paridade (é a mesma
  armadilha já documentada na Trilha C).

#### A tabela que sai disso

| Duplas | Peso | Campeão | Vice | Semi | Quartas | Oitavas | 16-avos | Grupos |
|---|---|---|---|---|---|---|---|---|
| 2 | 0,7 | 7 | 7 | — | — | — | — | 7 |
| 3 | 0,8 | 80 | 48 | 28 | — | — | — | 8 |
| **5** | **1,0** | **100** | **60** | **35** | **20** | — | — | **10** |
| 8 | 1,3 | 130 | 78 | 46 | 26 | — | — | 13 |
| 12 | 1,7 | 170 | 102 | 60 | 34 | — | — | 17 |
| 16 | 2,1 | 210 | 126 | 74 | 42 | 32 | — | 21 |
| 20 | 2,5 | 250 | 150 | 88 | 50 | 38 | 30 | 25 |
| 26 | 3,1 | 310 | 186 | 109 | 62 | 47 | 37 | 31 |

Leitura de justiça — chegar menos longe num funil maior pode valer mais, e é o ponto
inteiro da mudança: **semifinal numa categoria de 20 duplas (88) vale mais que o TÍTULO de
uma de 3 (80)**. E **cair no grupo de uma categoria de 26 duplas (31) vale mais que ser
semifinalista numa de 3 (28)** — o campo de 26 duplas era mais difícil de atravessar do que
o de 3 inteiro.

#### É retroativo, e é de propósito

Ponto de ranking **não é guardado**: é calculado na hora a partir de `Dupla.UltimaFase`.
Mudar a fórmula reordena o ranking inteiro no mesmo segundo — inclusive a história. É a
mesma filosofia do replay do Padelímetro: *mudou a régua, ela vale pra história toda*. A
alternativa (versionar a regra por torneio) colocaria duas moedas no mesmo ranking, e aí
ninguém entende o próprio número. Com a referência em 5 duplas = 1,0, o torneio típico de
hoje quase não se move.

#### O que NÃO entrou: bônus por adversário forte

O Felipe perguntou se enfrentar dupla mais bem ranqueada não deveria pagar mais. **Não
entra na trilha de pontos — porque a trilha A já é exatamente isso.** No Elo do
Padelímetro, bater dupla forte paga muito e bater dupla fraca paga quase nada, e ele acerta
essa conta porque parte da EXPECTATIVA do confronto. Repetir a ideia aqui traria três
defeitos:

1. **O passado se reescreveria sozinho.** O PDZ do adversário de março muda todo fim de
   semana; o ponto de março flutuaria pra sempre junto. Congelar exigiria fotografar o
   adversário partida a partida — coluna, migração e replay, pra duplicar o que a trilha A
   faz de graça.
2. **Premiaria o SORTEIO, não o mérito.** Duas duplas na mesma fase: a que pegou o futuro
   campeão levaria mais que a que pegou chave leve. No Elo isso é justo (a expectativa
   desconta); numa soma de pontos vira loteria de chaveamento.
3. **O número deixaria de ser explicável.** "Quartas em 16 duplas = 42" qualquer um
   confere; "43,7 porque o adversário tinha 612 PDZ" é discussão no grupo do WhatsApp.

**Fica como fase 2**, com gatilho: quando ~70% dos jogadores de uma categoria tiverem PDZ
fora da calibração, entra um multiplicador pequeno (até ~1,25×) pela **força do campo
inteiro** — o PDZ médio da categoria, **fotografado na geração das chaves**. Isso premia
"o torneio era forte" sem premiar sorteio, e uma fotografia por categoria é barata.

#### Implementação

- Motor puro em **`Services/PontosDoTorneio.cs`** (estático, sem banco, espelho do
  `PontosDoAmericano`): `Peso(duplas)`, `ValeCampanha(fase)`, `TorneioJaComecou(status)` e
  `Pontos(fase, duplas, status)`.
- ⚠️ **O status do torneio é PARÂMETRO da conta, não um filtro de quem chama.** Toda soma
  de ponto passa por `Pontos(...)`, e um torneio que não começou devolve **0** ali dentro.
  Deixar isso como `.Where(...)` na consulta exigiria oito lugares lembrarem — e o nono,
  escrito daqui a três meses, não lembraria.
- ⚠️ **Quem entra na soma é `ForaDoSorteio`**, com um gêmeo `EstaNaChave` escrito como
  `Expression` pro EF traduzir (o método recebe a entidade e o `Completa` é propriedade
  calculada, que o provedor não sabe ler). Mesmo padrão do par
  `ContaNoRanking`/`DuplaContaNoRanking`, **com teste comparando os dois lado a lado** —
  foi exatamente assim que o Americano escapou por uma consulta escrita à mão.
- ⚠️ **`EstatisticasService.PontosPorFase(fase)` foi REMOVIDO de propósito.** Eram 8 lugares
  somando ponto (perfil, busca, ranking por categoria, times, por torneio, evolução) e um
  método que ainda aceitasse só a fase deixaria qualquer um deles com a regra velha, sem
  erro nenhum aparecer. Apagar o método quebra a compilação nos 8 — é a lição da regra
  duplicada, que é A causa dos bugs graves deste projeto.
- A contagem de duplas por categoria sai numa passada só
  (`ContarDuplasPorCategoriaAsync`), nunca uma consulta por linha.

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

### Duas listas dentro da Trilha C: individual × em duplas (07/08/2026)

Decisão do Felipe: a aba do Ranking Americano tem **subdivisão por formato**, e não uma
lista só com uma coluna a mais.

- **Individual**: o parceiro troca a cada rodada, então o que sobra no fim é o seu jogo.
- **Em duplas**: a dupla é fixa do começo ao fim, e **metade do mérito é do parceiro que
  você escolheu**. Numa lista só, os dois números pareceriam a mesma medida, e o de
  duplas premiaria quem escolheu bem, não quem jogou melhor. É a mesma razão pela qual
  o Americano já não soma com o oficial.

⚠️ **A colocação do Americano de Duplas é da DUPLA, e os dois levam os mesmos pontos** —
como o título do mata-mata. Isso exigiu a tabela certa (`TabelaDoAmericanoDeDuplas`, por
par) no lugar da individual: os dois parceiros jogam exatamente as mesmas partidas, então
a tabela por PESSOA lhes dá somas idênticas, empata os dois e desempata pelo Id — a dupla
campeã saía com um jogador em 1º (100) e o outro em 2º (60), pelo mesmo resultado.

⚠️ **Contar pessoas depende do formato** (`Services/PessoasDoAmericano`, um lugar só, lido
pelo ranking E pelo acerto de R$ 5 do admin). O individual grava uma linha por PESSOA em
`InscricaoAmericana`; o de duplas grava uma linha por PAR em `Dupla`, porque herdou o
caminho de inscrição do Padrão. Duas armadilhas nisso:

1. Contar só `InscricaoAmericana` devolvia **zero** pro Americano de Duplas — abaixo do
   piso, ou seja "não pontua e não se cobra", sem erro nenhum aparecer.
2. **Somar as duas tabelas é pior ainda**: no Americano individual a tabela `Dupla` também
   tem linhas — uma por PARCERIA DE RODADA —, e a mesma pessoa seria contada uma vez por
   rodada jogada. O peso do ponto explodiria em silêncio.

O piso de 8 é por PESSOA nos dois formatos: senão bastaria jogar em duplas pra driblar a
regra que existe justamente contra resultado combinado.

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
- O "ajuste de categoria" do QT (bump ao sair de chave) não foi copiado no desenho
  original de 03/08 — "no Elo, sair de chave JÁ é o empurrão". **Revisto em 19/08/2026**:
  a régua ganhou o próprio ajuste de campanha (campeão/estreia/chave, com as portas da
  faixa como limite — ver "A campanha também move o número"), porque o Elo puro não
  devolvia ninguém pra categoria de baixo em tempo humano.
