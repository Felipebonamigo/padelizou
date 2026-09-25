# Ferramentas — simulador de balanceamento

O motor (`jogo/js/motor.js`) é determinístico e roda no Node. Aqui mora um **jogador
artificial** (`bot.js`) e um **simulador** (`simular.js`) que põe o bot pra lutar milhares de
vezes, sem desenho, e agrega o resultado. Serve pra responder com número o que o CRONOGRAMA
pede: "ajustar até a curva de morte fazer sentido".

Nada daqui entra no jogo: `index.html` e o Electron não carregam esta pasta.

## Rodar

```bash
node jogo/ferramentas/simular.js --fase 1 --dificuldade normal --habilidade medio --n 100 --semente 1
node jogo/ferramentas/simular.js --fase 2 --personagem shen --json > fase2.json
node jogo/ferramentas/simular.js --matriz                 # fases 1–4 × 3 dificuldades × 3 bots, n=30 por célula
node jogo/ferramentas/simular.js --matriz --n 100         # mais firme, ~1 min
node jogo/ferramentas/simular.js --arena --n 30            # o modo Arena: quantas ondas o bot sobrevive
node jogo/ferramentas/simular.js --arena --matriz          # Arena: 3 dificuldades × 3 bots, n=30 por célula (~8 s)
node jogo/ferramentas/simular.js --ajuda
```

| Opção | O quê | Padrão |
|---|---|---|
| `--fase` | 1 a 4 (a 0, Sala de Treino, não tem onda e nunca conclui) | 1 |
| `--dificuldade` | `facil`, `normal`, `dificil` | `normal` |
| `--habilidade` | `novato`, `medio`, `bom` | `medio` |
| `--personagem` | `long`, `shen` | `long` |
| `--n` | quantas simulações | 100 (matriz: 30 por célula) |
| `--semente` | primeira semente; a simulação *k* usa `semente + k` — de 1 a 4294967295, a faixa inteira (o motor faz `(semente >>> 0) \|\| 1`: 0 seria o mundo da 1, e 2³² + k o da k) | 1 |
| `--teto` | segundos de JOGO antes de contar TRAVA | 900 (15 min) |
| `--json` | resumos de cada luta + agregado, pra planilha ou script | — |
| `--arena` | roda o modo Arena em vez de uma fase (com `--matriz`, dificuldades × bots) | — |

Velocidade: ~300–400 mil passos por segundo (5–7 mil vezes o tempo real). A matriz com n=30
roda em ~25 s. Os passos por segundo da rodada saem no **stderr** (junto dos pontinhos de
progresso da matriz), nunca no stdout nem no `--json`: assim `> antes.txt` grava só números, e
duas rodadas iguais dão arquivos iguais byte a byte — `diff` vazio quer dizer "nada mudou".

**Conferidor:** `node Padelizou.Tests/js/conferir-simulacao-do-shaolin.js` (CI, ~4 s) trava que
o bot fala a língua do motor (borda `apertou` certa), que o bom conclui a fase 1 no fácil,
que nada trava, que a mesma semente dá o mesmo resumo, que o bot nunca chama `Math.random`,
que ele só enxerga o que está na tela, que cada bot bloqueia pelo menos o piso dele, que o
stdout se repete, que semente fora da faixa é recusada, que o bot não entra andando numa zona do
cenário (e contorna quando o alvo está do outro lado), que chuta pra derrubar na zona quando dá, e
que a Arena (`simularArena`, `--arena`) mede ondas, se repete pela semente, trata o teto como teto
e denuncia como trava a Arena em que nada anda (um inimigo congelado fora da tela).

## O que cada simulação faz

Uma luta = uma fase, do começo, com 3 vidas, vida cheia e chi zero. **Não** carrega vida, chi e
vidas da fase anterior como a campanha faz (`principal.js`): assim cada fase se mede sozinha, e
um número ruim na fase 3 é da fase 3, não da 2. Ela termina em um de três resultados:

- **concluiu** — o motor ligou `mundo.concluida` (última onda limpa e o jogador chegou ao fim);
- **fim-de-jogo** — as três vidas acabaram;
- **trava** — passou o teto de 15 min de jogo sem nenhum dos dois. Trava é **defeito**, não
  dificuldade — mas o simulador não sabe de quem: pode ser do **motor** (inimigo que não se
  alcança, onda que nunca fecha, jogador preso), do **bot** (parado, socando o vento) ou só um
  `--teto` curto demais pra luta acabar. Sai no relatório com a semente, a onda, onde estava cada
  um, **há quanto tempo o jogador não acerta ninguém**, o **inimigo vivo mais perto** (`dx`, `dy`)
  e o comando que reproduz. Inimigo longe e ninguém apanhando há minutos aponta pro motor;
  "nenhum vivo" na tela livre, ou inimigo colado e ninguém apanhando, aponta pro bot.

O passo é 1/60 s, igual ao jogo. O congelamento de acerto e a câmera lenta do `principal.js`
são só de tela — o motor não anda durante eles — e por isso o tempo medido é tempo de luta, um
pouco menor que o relógio de quem joga.

## O que cada número quer dizer

**Por lote** (`--fase … --n …`):

| Linha | Significado |
|---|---|
| `concluiu` / `fim de jogo` / `TRAVA` | quantas lutas acabaram de cada jeito |
| `tempo p/ concluir (s)` | só das que concluíram — o ritmo da fase |
| `vidas perdidas` | por luta, 0 a 3 (3 = fim de jogo) |
| `dano recebido` | total de vida perdida na luta, contando o dano de defesa (20% passa) |
| `golpes bloqueados` | dos golpes (e projéteis) que chegaram no jogador, quantos ele bloqueou — a defesa EFETIVA do bot, somando o lote. No resumo de cada luta: `golpesBloqueados` e `golpesLevados` |
| `pontos` | `mundo.pontuacao` no fim |
| `finalizações` | agarrões em atordoado ("FINALIZE!") |
| `maior combo` | o maior contador de combo que o jogador chegou (golpes seguidos sem 1,6 s de pausa) |
| `mortos pelo cenário` | inimigos que morreram numa zona (fogo, espinhos, poço) — no resumo, `inimigosPeloCenario` |
| `dano do cenário` | vida que o jogador perdeu pisando numa zona (`danoDoCenario`). **Não** conta como golpe bloqueado: o cenário também sobe o dano sem subir `golpesLevados`, e o simulador desconta |
| `média · p10 · p50 · p90 · máx` | p50 é a mediana; p10/p90 mostram o espalhamento (linear entre vizinhos) |
| `mortes (vidas)` por onda | quantas vidas se perderam em cada onda, somando o lote; `caminho` = fora de onda; a última é a do chefe |
| `fins de jogo` por onda | onde a ÚLTIMA vida caiu — o muro da fase |

**Na matriz**, cada linha é uma célula `fase × dificuldade × bot`: `concl.`, `fim` e `trava`
em porcentagem/contagem, `t p50`/`t p90` do tempo de conclusão, `vidas` e `dano` médios,
`bloq.` a taxa de golpes bloqueados, `pontos` na mediana, `final.` a média de finalizações, `cen.` a
média de inimigos mortos pelo cenário e a onda que mais tirou vidas.

## A Arena (`--arena`)

Uma luta = uma Arena, com 3 vidas, até todos morrerem (**fim-de-jogo**) ou o `--teto` de tempo de
jogo — que na Arena **não é trava**: ela não conclui nunca, então chegar ao teto quer dizer
"sobreviveu até lá" (sai como `chegou ao teto vivo`). A **trava** da Arena é outra régua: 180 s de
jogo sem nada andar — nenhuma onda nova, ninguém (jogador ou inimigo) perdendo vida. É o sintoma de
um inimigo que nunca entra na tela: sem ela, a luta ficava parada até o teto e saía como "chegou ao
teto vivo", a melhor coluna. Sai com a semente e o mesmo retrato das travas da campanha (a linha
`reproduzir` traz `--arena`). O número que importa é `ondasSobrevividas`
(ondas limpas inteiras, o placar do jogo), com os pontos, o karma que a partida daria
(`pontos/200`, a tabela da Arena), o tempo, as vidas e o que o cenário fez. Na matriz da Arena,
cada linha é `dificuldade × bot` com as ondas em p10/p50/p90/máx, as que chegaram ao teto e as travas.

## Os três bots

Mesmo cérebro, reflexos diferentes (`HABILIDADES` no topo de `bot.js`):

| | novato | medio | bom |
|---|---|---|---|
| reação a golpe de quem ele vigiava (s) | 0,04–0,14 | 0,04–0,12 | 0,03–0,08 |
| reação a golpe "do nada" (s) | 0,32–0,55 | 0,18–0,32 | 0,08–0,16 |
| chance de tentar defender um golpe que viu | 30% | 60% | 85% |
| **golpes bloqueados, medido** (fase 2 normal, Long, n=30) | **11%** | **29%** | **52%** |
| chance de emendar o próximo soco | 45% | 75% | 95% |
| mira (fração da tolerância de linha que aceita) | 1,05 (erra às vezes) | 0,8 | 0,6 |
| corre (dois toques) e dá investida | não | sim | sim |

A linha "medido" é a que vale: o parâmetro de chance não é a taxa. Golpe que chega enquanto o
bot está no meio do próprio soco, caído ou atordoado não tem como ser bloqueado, e golpe mais
rápido que a reação passa. A taxa de cada célula está na coluna `bloq.` da matriz.

**Vigiar** é a antecipação: inimigo na tela, alinhado, de frente e chegando na distância em que
a IA dele ataca (`ia.alcance × escala + 8`, com 40 px de folga) fica vigiado por 0,3 s. Golpe
que sai de quem estava vigiado usa a reação curta — quem joga não reage a 0,16 s do nada, mas
vê o inimigo encostar e já está com o dedo no botão. Sem isso, a reação era mais lenta que o
arranque dos golpes comuns (garra 0,16 s, sombra 0,18 s, mestre 0,12 s) e o novato e o medio
bloqueavam 0,5% e 7%. Levantar a guarda por tempo, parado, foi tentado e piora o bot (parado
de guarda ele não bate e o grupo cerca): a antecipação que funciona é a do reflexo.

Todos: escolhem alvo (atordoado de pouca vida primeiro, pra finalizar), alinham a
profundidade, chegam no alcance, pulam e chutam às vezes, usam o especial com chi e gente
perto (o Long também atira no chefe e em quem aparece na beirada da tela além do chão onde ele
pode ficar), buscam chá com pouca vida e chutam mais quem tem armadura (o chute derruba; o
soco não interrompe). Sem inimigo na tela com a tela travada (ou na Arena, entre as ondas), vão pro
meio dela — é o que faz quem ainda está lá fora entrar.

**O cenário que mata.** As zonas (`faseDef.perigos`) estão desenhadas na tela, e o bot joga como
quem as vê: **não entra andando** numa zona — olha 0,15 s à frente pelo caminho inteiro (não só a
ponta: na diagonal, a ponta passa da quina e o meio corta a zona) e trata a zona como parede, como
o motor faz com a IA: entraria pela profundidade, segue só em x; entraria pelo x, contorna pela
borda de profundidade que existe e mantém o desvio 0,4 s. Chá caído numa zona não existe pra ele.
Sem isso os números da campanha ficariam piores do que um humano teria. E **empurra** quando é
fácil: inimigo comum de frente, com a zona onde o chute o derruba (`recuo × 0,28 s` de voo), é
chute — sem sorteio. Levar o inimigo até a zona de propósito, ou arremessar nela, ele não faz.
O acaso do bot sai de um `Motor.criarRng` próprio, semeado da semente da luta, e nunca do
`mundo.rng`: o bot pensar diferente não muda o sorteio da IA inimiga.

O bot lê **só o que a tela mostra**: o recorte `[camera.x, camera.x + LARGURA]` que o jogo
desenha. Inimigo, projétil, chá ou vaso com o corpo inteiro fora dele não existe pra ele — nem
pra escolher alvo, nem pra contar gente perto do especial, nem pra defender. (Antes ele mirava
no chefe esperando além da borda, atirava em quem ninguém via e se afastava pra "atrair" um
inimigo invisível; isso mexia nos números nos dois sentidos, e o conferidor agora prova que um
"fantasma" fora da tela não muda nenhuma tecla.) Da tela ele lê posição, estado, golpe em
curso e vida de cada um, mas não a `pausa` da IA nem o próximo número do sorteio. Ele vê o
golpe inimigo começar no quadro exato; o "olho humano" é o atraso de reação sorteado, não uma
visão borrada.

## Como usar pra ajustar as tabelas do motor

1. **Fotografe antes**: `--matriz --n 100 > antes.txt`. Mesmas sementes, então a comparação
   é pareada — a diferença é da mudança, não do acaso. O tempo de relógio vai pro stderr, então
   `diff antes.txt depois.txt` vazio quer dizer que a mudança não mexeu em nada.
2. **Mude UMA coisa** em `motor.js`: `DIFICULDADES` (vida e dano dos inimigos), `INIMIGOS`
   (`vida`, `golpes[x].dano`, `ia.agressividade`, `ia.pausa`, `ia.alcance`), `FASES` (quem vem
   em cada onda), o reforço por fase em `criarInimigo` (`0.15` por fase), ou o chá (`+35`).
3. **Fotografe depois** e compare. Leitura rápida:
   - a curva que se quer: **bom** conclui quase tudo no normal; **medio** conclui o fácil e
     boa parte do normal; **novato** conclui a fase 1 no fácil. O difícil é pra quem já zerou.
   - `fins de jogo` concentrado numa onda que não é a do chefe = aquela onda está fora da curva
     (é ali que se mexe: quem vem nela, ou a vida/dano de quem vem);
   - `tempo p/ concluir` que cresce muito entre `bom` e `medio` = luta arrastada (muito
     bloqueio, inimigo fugindo), não luta difícil;
   - `dano recebido` alto com poucas vidas perdidas = chá demais ou dano de defesa (chip) caro.
4. Se aparecer **TRAVA**, pare o balanceamento: rode o comando `reproduzir` da trava com
   `--json` e olhe `trava.semAcertar`, `trava.maisPerto` e `trava.inimigos` (quem sobrou, onde,
   em que estado). Primeiro descubra de quem é: com o teto padrão, inimigo fora de alcance e
   ninguém apanhando é do **motor** — vira teste no conferidor do motor antes da correção;
   bot parado, andando pro lado errado ou socando o vento é do **bot** — vira teste no conferidor
   da simulação (Regra 1 do `CLAUDE.md`, nos dois casos).
5. Se o **bot** fizer algo que nenhuma pessoa faria (e isso mudar o número), o ajuste é no bot,
   não no motor — e o conferidor da simulação tem que continuar verde.

## Limites conhecidos

- Um jogador só (sem P2) e sem as melhorias do Templo.
- O bot não usa arremesso pra atropelar grupo de propósito, nem arremessa no cenário; do cenário,
  só o chute que derruba quem já está de frente pra zona.
- Não simula o ponto de controle: cada fase começa do começo, e o fim de jogo é o fim da luta.
- Os números são de um bot: servem pra COMPARAR versões do motor e achar outlier, não pra
  prometer "uma pessoa leva 2 h". Isso continua sendo o teste com 3 pessoas do CRONOGRAMA.
