# Física do carro: colisões carro-carro (onda F)

O dono jogou e reclamou: "eles batem um no outro sem estarem muito perto". Estava certo: a caixa de colisão
não tinha o tamanho do carro que a tela mostra. Este documento diz qual é a pegada agora, as regras do
contato e os números medidos antes e depois. Código: `src/core/sim/collisions.ts`; constantes em
`src/core/constants.ts`; testes em `tests/collisions.test.ts`.

## Pegada: a caixa de colisão é o carro da tela

| | Tela (`src/render/cars.ts`, todos os estilos) | Colisão antes | Colisão agora |
|---|---|---|---|
| Comprimento (centro a centro, em fila) | 4,4 m | 120 u = **2,4 m** | 200 u = **4,0 m** (91%) |
| Largura (centro a centro, lado a lado) | 1,9 m | 0,44 = **3,08 m** | 0,26 = **1,82 m** (96%) |

Escala da tela (`src/render/units.ts`): x normalizado ±1 = ±7 m; um segmento de 200 u = 4 m (1 m = 50 u).

- **Antes**, lado a lado os carros batiam com **1,2 m de ar** entre as latarias (a reclamação), e em fila um
  entrava **2 m** dentro do outro antes de bater — e, como a batida só mexia na velocidade, às vezes
  atravessava de vez (o teste "batida por trás não atravessa" pegou um carro saindo do outro lado).
- **Agora** a caixa é ~o carro. Um pouco menor, de propósito, para perdoar o raspão:
  - as pontas afinam (o bico tem 1,24 m de largura contra 1,8 m no meio): uma caixa de 4,4 m bateria quina
    com quina onde a tela mostra ar; tirar 0,2 m de cada ponta aproxima o chanfro. Em fila, de frente, sobra
    no máximo 0,4 m de sobreposição visual no contato — a câmera de perseguição olha ao longo desse eixo;
  - o carro gira até 0,08 rad ao esterçar (`Cars.update`), o que a caixa alinhada à pista não acompanha;
  - na largura o perdão é menor (4 cm de cada lado) porque o vão lateral é o que a câmera mais mostra: no
    contato as latarias se tocam nos para-lamas (1,84 m) e os pneus (1,98 m) encostam.
- `CarBody` (`types.ts`) garante que todo estilo de carroceria ocupa essa pegada (~4,4 × 1,9 m). Quem mudar o
  tamanho dos modelos muda `CAR_LENGTH`/`CAR_HALF_WIDTH` junto — `tests/collisions.test.ts` confere que a
  caixa fica entre 85% e 100% da pegada da tela.

## Regras do contato (`resolveCarCollisions`, depois da física de todos os carros)

1. **Detecção**: par a par, `|dz| < CAR_LENGTH` e `|dx| < 2·CAR_HALF_WIDTH`.
2. **Qual eixo**: o que acabou de se fechar. Se no começo do tick os dois ainda estavam separados em z (a
   distância de antes, pela velocidade de cada um, passava de `CAR_LENGTH`), é **batida em fila**; senão é
   **raspão de lado**. Carros postos já sobrepostos (testes, ou sobra de um engavetamento) vão pelo eixo de
   menor sobreposição proporcional. (Antes: `|dz| < 0,35·CAR_LENGTH` era "lado a lado", o resto "em fila".)
3. **Em fila**: quem vem atrás **recua até encostar** (`CAR_LENGTH` + 1 u de folga) — não atravessa nem fica
   enfiado no outro na tela. Se vinha mais rápido, **a velocidade passa como sempre**: quem bate cai a 90% da
   do outro (mínimo 10% da própria máxima), quem é batido ganha `(diferença / máxima) × 400`, e o evento
   `collision` sai com os dois fora do cooldown (20 ticks). Sem empurrão para o lado (antes: 0,03 para cada
   lado a cada tick de contato, mesmo numa batida reta). O recuo nunca passa de z = 0 para trás: cruzar a
   linha de chegada ao contrário contaria a volta de novo no tick seguinte (`positions.ts`).
4. **De lado**: cada um se afasta **metade da sobreposição** (+ 0,002 de folga), só até encostar — era 0,03
   fixo (0,21 m) por carro por tick, um tranco mesmo num raspão de 1 cm. Os dois perdem velocidade na
   proporção do **quadrado** da sobreposição: `perda = 3% × min(1, sobreposição / 0,06)²`. Uma guinada forte
   contra o outro custa os 3% de antes; dois carros apenas encostados, um esterçando contra o outro, quase não
   perdem. Com perda fixa de 3% por tick e separação exata, dois carros encostados se seguravam a ~15% da
   máxima para sempre (o "travamento lado a lado" do `docs/RIVAIS.md`, visto no teste do revezamento).
5. **Engavetamento**: até 3 passadas por tick (`COLLISION_PASSES`). Quem recua depois de bater pode cair
   dentro de quem vinha atrás, num par já conferido; a segunda passada resolve (teste "engavetamento").
6. **Folga** (`COLLISION_SLOP_X/Z`): a separação deixa 0,002 de lado e 1 u em fila além do encosto; sem ela
   o arredondamento pode deixar uma sobreposição residual (~1e-17) que contaria como contato de novo.

Batida no cenário (`resolveSpriteCrash`) usa a mesma meia largura: com o carro do tamanho certo, a IA que
abre para a grama bate bem menos em árvore e placa (ver números).

## O que dependia do tamanho errado

| Onde | Antes | Agora |
|---|---|---|
| Grid (`GRID_ROW_GAP`, `sim/race.ts`) | filas a 260 u (5,2 m): 0,8 m de vão na tela | 400 u (8 m, um grid de verdade): 3,6 m de vão. Com 260, quem arrancava melhor batia no da frente nos primeiros metros |
| `BLOCK_ALONGSIDE` (bloqueador respeita o desvio) | 1,5 × 120 = 180 u (3,6 m) | 1,1 × 200 = 220 u (4,4 m): o bico do humano na traseira do bloqueador, na tela. 1,5 × a caixa nova (6 m) desistia cedo demais e o bloqueador parava de segurar o humano |
| Agressivo "passa raspando" (`data/drivers.ts`) | a 0,42 de lado, dentro da caixa de 0,44: batia a cada ultrapassagem, com 1,1 m de ar | a 0,36: 0,7 m de ar |
| Contatos das estatísticas (`achievements.ts`, `touching`) | folga lateral 0,06 (o tranco antigo) | folga de 5× a do núcleo: 0,01 de lado, 5 u em fila |
| Tutorial, passo do empurrão (`tests/tutorial.test.ts`, `scripts/playtest-tutorial.mjs`) | o piloto de roteiro mirava no carro parado e passava por dentro dele | passa colado ao lado (0,31) |
| Batida no cenário (`resolveSpriteCrash`) | meia largura 0,22 | 0,13: a IA que abre para a grama bate bem menos em árvore e placa |
| Vácuo, empurrão, escolta | não usam a pegada (6/2/2 segmentos, janelas laterais próprias) | iguais |

### A IA com carros sólidos (`sim/ai.ts`)

Com a batida atravessando, a IA se virava mesmo errando; com carros sólidos, dois defeitos apareceram (os dois com
teste em `tests/ai.test.ts`, escrito antes e visto falhar):

- **Lado da ultrapassagem sem espaço.** A IA que vinha por fora de um carro parado perto da borda (x ≥ 0,45)
  escolhia passar por fora, a faixa batia no limite de 0,7 e sobrava 0,25 de lado — menos que a largura do carro.
  Antes passava por dentro dele; agora ficava batendo na traseira dele para sempre (no `npm run balance`, um carro
  da IA bateu 212 vezes no humano parado no grid). Agora, se o lado escolhido não deixa `AI_PASS_CLEARANCE`
  (largura + 0,04), passa pelo outro.
- **Esterçar para dentro de quem está do lado.** A faixa-alvo da IA ignorava carros do lado. Com o tranco fixo
  antigo o outro era jogado longe; com a separação proporcional, os dois ficavam se empurrando de lado e perdendo
  velocidade (a maior sequência de contato lateral numa corrida foi de 70 ticks para 936), e a IA que ia para o box
  com alguém do lado perdia a entrada e secava o tanque (o teste de combustível de Rochosas pegou). Agora, com um
  carro a menos de `AI_SIDE_LOOK` (4,4 m entre centros) no caminho da faixa que ela quer, a IA segura a faixa ao lado
  dele (`sideBlocker`); indo para o box, tira o pé e passa por trás. Três detalhes, cada um visto num teste ou na medição:
  - indo para o box, quem vem atrás só conta se já estiver encostado (`CAR_LENGTH`): um carro na faixa do box logo
    atrás, na mesma velocidade, prendia o outro do lado de fora até o fim dela (Autobahn, semente 42);
  - só tira o pé para quem está andando (≥ 10% da máxima; o limitador do box segura a 25%): atrás de um carro parado
    ela ficava esperando ao lado dele para sempre;
  - o erro de frenagem do errático fica de fora: ele abre para fora porque não segura a curva, com ou sem alguém do
    lado (e continua indo à grama como a personalidade pede).

As janelas da IA que já existiam ficaram: "carro à frente" a 0,5 de lado e ultrapassagem do neutro e do limpo a
0,55 e 0,65 (passam com 2–2,7 m de ar). São mais largas que o carro agora, o que é seguro; apertá-las deixaria a IA
mais rápida no tráfego e mexeria no balanceamento sem pedido.

## Números

**Corridas com um humano "médio" simulado** (freio automático, volante com atraso e lapsos: o piloto de rascunho
da recalibragem da onda C, fora do repositório), profissional, 20 carros, 3 voltas, as 32 pistas, semente 1. Uma
sonda embrulha `resolveCarCollisions` numa cópia do código (o repositório não muda) e conta, por corrida, o começo
de cada contato (um par encostado conta uma vez a cada 20 ticks) e o ar que havia entre as latarias na tela
(pegada visual 4,4 × 1,9 m) no instante do contato:

| Por corrida (média) | Antes | Depois |
|---|---|---|
| Contatos carro-carro | 259 | **107** (−59%) |
| … IA × IA | 215 | 82 |
| … com o humano | 44 | 25 |
| **Contatos com mais de 0,25 m de ar na tela** ("batem sem estar perto") | **158** (61% de todos) | **0** |
| … com o humano | 18,6 | 0 |
| Maior ar entre as latarias num contato | 1,18 m | 0 |
| Batidas por trás (evento `collision`) IA × IA / com o humano | 164 / 37 | 48 / 17 |
| Contatos de lado | 87 | 16 |
| Pares × tick com os carros enfiados um no outro mais de 0,45 m na tela | 4775 | **0** |
| Maior sobreposição na tela | 1,90 m | 0,40 m (o perdão da ponta, em fila) |
| Batidas no cenário da IA | 12,3 | 3,4 |
| Posição do humano / vitórias em 32 | 10,4 / 2 | 9,4 / 8 |
| Tempo do humano / do vencedor | 246,1 s / 236,0 s | 240,0 s / 232,0 s |
| Maior tempo do humano preso atrás do mesmo carro | 9,5 s | 6,1 s |

Sem o ajuste da IA que não esterça para dentro de quem está do lado, a mesma medição dava 4270 pares × tick de
contato lateral por corrida (antes 886) e uma sequência de 936 ticks (15 s) de dois carros se empurrando de lado;
com ele, 260 e 141.

**Co-op** (2 humanos "médios" no mesmo time, todas as assistências, mesmas 32 pistas): contatos 276 → 117, sem ar
158 → 0 (34 → 0 com humanos), batidas por trás com humanos 62 → 31; posição do P1 9,4 → 8,6 e do P2 12,1 → 12,5.
O empurrão, o vácuo de equipe e o elástico seguem com os testes de sempre (`tests/coop.test.ts`, `tests/career.test.ts`).

**Escolta** (2 humanos "médios": um empurra o VIP, o outro faz guarda ao lado; 8 pistas × 3 dificuldades × 3
sementes = 72 corridas de cada lado). VIP no top 3:

| Dificuldade | Antes | Depois |
|---|---|---|
| Amador | 24/24 (100%) | 24/24 (100%) |
| Profissional | 15/24 (62%) | 12/24 (50%) |
| Campeão | 11/24 (46%) | **5/24 (21%)** |

⚠️ A escolta ficou mais difícil, bem mais no campeão: sem a caixa larga, a guarda ao lado do VIP segura menos gente e
a IA passa mais limpo. O empurrão funciona igual (`tests/modes.test.ts`). Recalibrar `ESCORT_VIP_*` (`docs/MODOS.md`)
fica para uma rodada própria, com as 32 corridas por célula da onda C — aqui são 24.

**`npm run balance -- 150 profissional 11`** (IA × IA, o humano parado no fundo do grid): média geral das voltas
**86,19 → 84,62 s (−1,8%)**, toda pista mais rápida (−0,5% a −4,9%; Baía de Tóquio e Serra do Mar as que mais
ganharam); batidas por trás 4790 → 1662 (−65%); batidas no cenário 173 → 38; grama 0,32% → 0,40%. A IA anda mais
porque para de perder tempo em batidas que não existiam e em carro se esfregando de lado — e o humano ganha mais que
ela (acima: −2,5% no tempo dele, −1,7% no do vencedor). A calibragem da carreira (`tests/career-balance.test.ts`)
segue passando (piloto-proxy 3,4º na primeira copa; 1º em todas as corridas da última, como antes).

**Personalidades** (`tests/rivals.test.ts`): ver `docs/RIVAIS.md` (onda F).

**Custo**: +2 a +5 µs por tick na simulação (~+7%; pior quadro de 4 ticks 399 → 397 µs de 16.667) — `docs/DESEMPENHO.md`.

**Largada**: com o grid a 5,2 m e a caixa nova, as batidas da largada eram metade das batidas da IA no Passo Alpino
(`tests/rivals.test.ts`: 17 das 27 do limpo nos primeiros 12 s); com o grid a 8 m, 7 de 11.

## Testes

Todos escritos antes da correção e vistos falhar pelo motivo certo.

- `tests/collisions.test.ts`: a caixa entre 85% e 100% da tela; lado a lado com 0,5 m de ar não colidem; lado a
  lado a 0,3 m por 3 s ninguém perde velocidade (antes perdiam 58%); encostados colidem, separação proporcional,
  sem atravessar nem arremessar; em fila o contato vem a ~4 m e passa velocidade como antes; batida por trás não
  atravessa e não empurra para o lado; engavetamento sem sobra.
- `tests/ai.test.ts`: carro parado perto da borda (a IA passa pelo lado com espaço); a IA não esterça para dentro
  de quem está do lado; indo para o box com um carro parado no caminho, não fica esperando ao lado dele. Os testes de
  combustível de sempre (corrida inteira por pista; Rochosas e Autobahn com o elástico) pegaram a IA perdendo o box.
- `tests/race.test.ts`: o grid deixa pelo menos 1,5 m de vão na tela entre as filas.
- Ajustados à pegada nova (a intenção é a mesma): os contatos das estatísticas (`tests/stats.test.ts`, com
  `CAR_HALF_WIDTH` em vez de 0,43/0,44) e o piloto do tutorial (`tests/tutorial.test.ts`).
