# Fatia jogável no Unreal — design

> Escrito em 14/10/2026, depois do "ok, vamos fazer" do Felipe para a direção **"Virtua Tennis do padel, com as
> paredes como estrela"**, no Unreal Engine 5. Pela régua do `CLAUDE.md`, isto é trabalho **architectural**:
> motor novo, projeto novo e papel novo para o `Padel.Core`. **Nenhum código antes da aprovação deste documento.**

## A decisão em uma frase

Um jogo de padel 2×2 com **física da bola de verdade e controle generoso**. As paredes são o espetáculo, o visual
é realismo estilizado com cara de transmissão, e as partidas duram de 4 a 6 minutos. A primeira entrega é uma
**fatia jogável de cerca de 12 semanas**, aprovada por duas perguntas:

1. Um jogador de padel olha 30 segundos e diz *"isso é padel"*.
2. Quem joga uma partida pede *"mais uma"*.

Se uma das duas sair "não", o jogo não cresce. Corrige-se a fatia.

## Pilares (o que decide toda dúvida)

| # | Pilar | Na prática | O que **não** fazemos |
|---|---|---|---|
| 1 | **A parede é a estrela** | Batida no vidro com som próprio, câmera que acompanha, rastro-fantasma da saída, botão de saída de parede | Tratar a parede como obstáculo a evitar |
| 2 | **Bola real, corpo assistido** | A bola segue gravidade, arrasto, efeito, vidro e grade. O jogador escolhe intenção, direção e timing; o jogo cuida do passo fino | Colisão raquete-bola "física" a partir da animação. Vira sorteio e não dá para prever |
| 3 | **Partida curta e legível** | Ponto de ouro; set até 4 games com super tie-break em 4–4; placar de TV sempre na tela | Partida de 40 minutos, menus antes de jogar |

## Controle (controle de videogame primeiro; teclado mapeado igual)

| Entrada | Função |
|---|---|
| Analógico esquerdo | Mover. Com assistência ligada, o jogador se aproxima sozinho do ponto de contato quando a bola vem para ele |
| **A** (curto/longo) | Golpe chapado: drive ou revés, escolhido pelo lado da bola. Segurar carrega potência |
| **B** | Lob (por cima). Na rede vira globo de defesa |
| **X** | Efeito cortado: bandeja na bola alta do fundo, slice na baixa |
| **Y** | Ataque: smash na bola alta, víbora com o modificador |
| **RB** (modificador) | Saída de parede: espera a bola voltar do vidro. Com Y, víbora em vez de smash |
| Analógico no instante do golpe | Direção: paralela, cruzada, no corpo, na porta |

**O timing é o coração.** Aperta-se *antes*. O golpe acontece quando a bola passa pelo ponto ideal ao lado do
corpo. Cedo demais estica o braço; tarde demais a bola passa do corpo. O erro do golpe cresce com a distância
entre a bola e o ponto ideal nesse instante. Esse é o modelo do `Padel.Core` (contato no instante do balanço, 25/09):
nunca dado puro. O resultado vem por cor no pé do jogador (verde, amarelo, vermelho) e por som.

**Três níveis de assistência**, escolhidos no menu e trocáveis a qualquer hora:

| Nível | Movimento | Rastro-fantasma da parede | Janela do timing |
|---|---|---|---|
| Casual | Vai sozinho até a bola | Sempre | Larga |
| Padrão | Só o ajuste final | Só na primeira batida no vidro | Média |
| Pro | Nenhum | Desligado | Estreita |

## Golpes da fatia (6)

Os números saem do `Padel.Core`, que já tem teste para cada um.

| Golpe | Quando | Efeito (rpm) | Saída (km/h) | Animações |
|---|---|---|---|---|
| Drive / revés | Bola na altura da cintura | +1.200 topspin | 65–75 | drive e revés, cada um baixo, médio e alto |
| Lob | Qualquer bola, para passar por cima de quem está na rede | −400 | ~35 | 2 (de frente e de costas) |
| Bandeja | Bola alta, do meio para o fundo | −1.500 + 500 lateral | ~55 | 2 |
| Smash | Bola alta e curta | +1.500 | 100–110 | 2 (normal e por 3) |
| Saída de parede | Bola que volta do vidro de fundo | conforme o golpe que sai | conforme o golpe | 4 (fundo e lateral, drive e revés) |
| Saque | Por baixo, depois do quique, abaixo da cintura | −600 | 50–70 | 2 |

Ficam para depois: víbora (sai na fatia só se sobrar tempo), chiquita, contrapared, remate por 4.

## Física

**Decisão:** a física da bola é **reescrita em C++**, como módulo puro (`PadelSim`, sem `UObject`). Passo fixo de
240 Hz e mesma ordem de operações sempre. O `Padel.Core` (C#) vira o **gabarito**, e o C++ precisa reproduzir
as trajetórias dele.

- **Por que não usar a DLL do `Padel.Core` por NativeAOT, como o TDFende faz?** O multijogador do Unreal replica
  estado de ator e roda em C++. Uma simulação em DLL de outra linguagem tem de atravessar a ponte a cada passo,
  em host e cliente, e depurar por cima dela no editor custa caro. Bola e quadra somam cerca de 550 linhas
  (`Bola.cs` e `Quadra.cs`). Os solucionadores dos golpes especiais (`GolpesEspeciais.cs`, 1.000 linhas) só entram
  junto com a víbora e a chiquita. Reescrever sai mais barato que manter a ponte.
- **Como provar que a reescrita está certa:** um gerador em C#, rodado na nuvem, grava trajetórias do
  `Padel.Core` em CSV: posição, velocidade e spin a cada 10 ms, para cerca de 200 casos (quique, vidro, grade,
  rede, efeito, golpes especiais). O teste do Unreal (Automation Spec) roda os mesmos casos em C++ e exige
  diferença menor que 1 mm na posição em 3 s de voo. É teste escrito antes do código, e dá para ver falhar
  (regra 1).
- **Números que vêm junto**, todos já calibrados e com teste: bola de 57 g com R = 3,35 cm; Cd = 0,55; restituição
  do chão 0,775 com atrito 0,6; vidro 0,85 com 0,25; grade 0,30–0,50 com 0,80 e desvio de até ±15° (hash do
  ponto de contato, determinístico); rede 0,20 a 0,90 m; Magnus pela fórmula de Štěpánek; decaimento do spin de
  12 %/s.
- **O corpo** vem do Core: corrida de 6,4 m/s, aceleração de 9 m/s², frenagem de 14 m/s², balanço de 0,30 s com
  contato aos 0,12 s, alcance de 1,35 m e ponto ideal a 0,6 m ao lado do corpo.
- **A animação nunca move a bola.** O contato acontece no instante que a física decide. A animação é alinhada a
  ele com o **Motion Warping** do Unreal: a raquete chega ao ponto de contato no quadro certo, e o corpo
  escorrega até 30 cm para isso. Passou de 30 cm, sai o golpe esticado e a animação de "não alcancei".

## Câmera e apresentação

- **Câmera de TV** atrás da dupla da casa: lente equivalente a 35 mm, 7,5 m de altura, 17,5 m de recuo, segue a
  bola com atraso. Leve profundidade de campo, tremida pequena no smash.
- **Na batida no vidro**, a câmera dá um passo de lado de 0,3 s e há um som de vidro grave. É o pilar 1 em ação.
- **Replay automático** do ponto bonito, em câmera lenta e ângulo lateral, pelo Sequencer, a partir do estado
  gravado da bola e dos jogadores. O DemoNetDriver fica para quando houver online.
- **Placar de TV** com CommonUI, navegável por controle e pronto para o Steam Deck.

## Personagens e animação

- **Corpos:** dois MetaHumans, um masculino e um feminino, com duas variações de rosto, cabelo e roupa cada:
  quatro jogadores visíveis. A roupa é técnica de padel, sem marca. A câmera de TV deixa o rosto pequeno; o
  MetaHuman segura os closes da comemoração.
- **Locomoção:** Game Animation Sample (Motion Matching), de graça no Fab. Não precisa de captura.
- **Golpes: captura por vídeo** (Move AI ou Rokoko Vision) com um jogador de padel de verdade, num dia de quadra.
  A estimativa do `REALISMO.md` é de R$ 1–3 mil. A lista mínima tem 40 clipes:

| Grupo | Clipes |
|---|---|
| Drive e revés (baixo, médio, alto) | 6 |
| Drive e revés esticados e "não alcancei" | 4 |
| Lob e globo | 3 |
| Bandeja e smash (normal e por 3) | 5 |
| Saída de parede (fundo e lateral, drive e revés) | 4 |
| Saque (dos dois lados) e recepção | 4 |
| Espera na rede e no fundo, *split step* | 4 |
| Comemoração, lamento e toque de raquete com o parceiro | 6 |
| Pegar a bola e voltar para a posição | 4 |

Até a captura, a fatia usa animações cinza: Mixamo e Game Animation Sample. Os termos da Adobe precisam ser
conferidos antes de qualquer coisa do Mixamo ir para a build pública.

## Quadra e visual

- **Uma quadra panorâmica à noite**, com medidas da FIP: 20 × 10 m, vidro de 3 m no fundo, grade até 4 m e portas
  nas laterais. A geometria de colisão sai da mesma tabela de painéis que o `PadelSim` usa, para a bola nunca
  atravessar uma parede desenhada.
- **Lumen** para luz e reflexo. Vidro de 12 mm com a borda verde e reflexo dos refletores. Piso de grama
  sintética com areia, linhas gastas. Rede com tecido do Chaos. Seis torres de luz e público ao fundo, em
  impostores animados.
- **Arte:** primeiro o acervo do estúdio (`acervo-3d`, galeria da Meshy em CC0 ou CC BY 4.0, Poly Haven CC0). O
  que faltar sai do Fab, com a licença conferida item a item. Nada com marca nem pessoa real.
- **Desempenho:** 60 fps em 1440p na RTX 4070 Ti do PC, com DLSS ou FSR. A meta do Steam Deck fica para depois
  da fatia.
- **Som:** MetaSounds. Batidas de bola em raquete, vidro, grade e chão gravadas num clube, no mesmo dia da captura.

## IA

- **Lê a mesma física:** a IA simula a trajetória no `PadelSim` e decide o golpe. Ela nunca "sabe" onde a bola
  cai por fora da simulação.
- **A dupla joga em formação:** os dois na rede, os dois no fundo, ou um em cada. O parceiro da IA se move junto
  com o humano, como no padel de verdade. Esse é o item que mais dá cara de padel depois da animação.
- **Três níveis** variam reação, precisão do timing e escolha de golpe. O nível nunca muda a física.

## Código e projeto

```
PadelUnreal/                       repositório novo (nome a decidir)
├─ Source/PadelSim/                C++ puro: bola, quadra, jogador, árbitro, IA. Sem UObject.
├─ Source/PadelGame/               atores, câmera, entrada, animação, UI
├─ Source/PadelSimTests/           Automation Spec, com as trajetórias-gabarito do Padel.Core
├─ Content/                        no Git LFS ou no Perforce (decisão abaixo)
└─ docs/estado-atual.md            o que está feito, pelo padrão dos outros jogos
```

O `PadelSim` também compila fora do Unreal, num executável de teste. A nuvem roda os testes da física sem o
editor, que nem cabe no container. O que precisa do editor (atores, animação, visual) roda na **sessão local
do PC**, com a skill `unreal-mcp`. Os outros jogos do estúdio já trabalham assim: `verificar`, `capturar`,
`triar-playtest`.

## Plano em etapas (cerca de 12 semanas)

| Semanas | Etapa | Onde | Pronto quando |
|---|---|---|---|
| 1–2 | Gabarito do `Padel.Core` em CSV e o `PadelSim` em C++ passando nele | Nuvem | Os ~200 casos com diferença < 1 mm |
| 3–4 | Quadra cinza jogável: 4 cápsulas, câmera de TV, drive, lob e saque, placar | PC | Uma partida inteira de ponta a ponta, contra a IA |
| 5–6 | Os 6 golpes, saída de parede com rastro-fantasma, IA em formação, 2×2 local | PC | Teste interno: o Felipe e 3 amigos jogam 2×2 |
| 7–8 | Dia de captura e animação: Motion Matching, Motion Warping, os 40 clipes | PC + quadra | Golpe alinhado ao contato em 95 % dos casos, medido |
| 9–10 | Visual: MetaHumans, quadra noturna, Lumen, som gravado | PC | Print do jogo ao lado de um frame da TV |
| 11–12 | Polimento, replay, menus mínimos e playtest com 10 jogadores de padel | PC | As duas perguntas do topo respondidas |

## O que depende do Felipe

| Item | Por quê |
|---|---|
| **Aprovar este documento** | Regra do projeto para trabalho architectural |
| Criar o repositório novo e dizer o nome | É ação na conta do GitHub. O jogo não deve morar dentro do `padelizou`, que é o site |
| Git LFS ou Perforce para o `Content/` | O doc do estúdio aponta que o `Content/` do Age of Earth e do TDFende está sem histórico. Aqui ele nasce versionado |
| Espaço no PC | O doc do estúdio registra 128 GB livres para três projetos Unreal. Este seria o quarto: o SSD externo vira pré-requisito |
| Um dia de quadra com um jogador para captura e som | Semanas 7–8. Custo estimado de R$ 1–3 mil |
| Onde entra na fila | O Detonauto começa em tempo integral em 02/11/2026 |

## Riscos

| Risco | Mitigação |
|---|---|
| Animação ruim derruba o visual inteiro | Captura com jogador de verdade; Motion Warping; a semana 8 só fecha com a medição dos 95 % |
| A reescrita da física diverge do Core | Gabarito de 200 casos antes de qualquer gameplay |
| A IA parceira irrita | Formação de padel como regra fixa, não comportamento emergente; playtest da semana 6 |
| Escopo: quinto jogo de uma pessoa só | A fatia tem fim e duas perguntas. Online, carreira e torneio só depois de um "sim" |
| Vale da estranheza nos closes | Câmera de TV longe; closes só na comemoração e com MetaHuman |

## Fora da fatia

Online 2×2, carreira, torneios e ranking, Americano e Mexicano, mais quadras, Steam e conquistas, Steam Deck,
celular e navegador.
