# Realismo — visual e mecânico

> Escrito em 25/09/2026 depois da direção do Felipe: *"temos que pensar no realismo, visual e de mecânica"*.
> Isto revisa a decisão D4 (arte) e cria a D7 (modelo de golpe) e a D8 (física) em `DECISOES.md`.

## O princípio: simulação por baixo, leitura por cima

"Realista" aqui são três camadas, e cada uma tem uma régua diferente:

1. **A bola** obedece à física de verdade — massa, arrasto, efeito, quique, vidro. Régua: número da regra FIP e vídeo de transmissão.
2. **O corpo** tem inércia, alcance, tempo de reação e um balanço que precisa ser dado na hora certa. Régua: quem joga padel reconhece o erro que cometeu.
3. **O que o olho vê** — luz, materiais, animação, câmera, som — parece a transmissão do Premier Padel. Régua: um frame do jogo ao lado de um frame da TV.

O que **não** vamos fazer: simular a colisão raquete-bola a partir da animação. Parece mais "real", mas vira sorteio (o jogador não consegue prever o que sai), é hostil ao netcode e nenhum jogo de esporte grande faz assim. Top Spin e EA FC são *sim-cade*: a bola é simulada, a entrada do jogador é **intenção** (direção, tipo, timing) e o erro vem do **corpo** (timing, posição, esticada) — nunca de dado puro. É o modelo daqui.

## Mecânica — o que já está no `Padel.Core` (com teste)

| Coisa real | Número | No jogo |
|---|---|---|
| Bola (regra FIP) | 56–59,4 g; 6,35–6,77 cm; quica 135–145 cm ao cair de 2,54 m | 57 g, R = 3,35 cm; restituição do chão **0,775** — calibrada **com o arrasto do ar**, porque no vácuo o 0,74 da conta dá 1,28 m e reprova na regra (é um teste) |
| Arrasto | quadrático, Cd ≈ 0,55 pra bola de feltro | K ≈ 0,020 /m: bola a 30 m/s perde ~18 % em 10 m (teste: 15–30 %) |
| Efeito (Magnus) | Cl = 1 / (2 + v/(R·ω)) (Štěpánek) | topspin de 2.500 rpm cai > 0,5 m antes; slice flutua; sidespin desvia > 0,4 m em 8 m (testes) |
| Quique | modelo de rolamento com limite de atrito, casca esférica (I = ⅔ mR²) | topspin sai mais rápido e com mais spin; flat e slice deslizam o contato inteiro (teste) |
| Vidro | temperado, elástico, liso | e = 0,85, μ = 0,25: devolve forte, sidespin escorrega |
| Grade | malha metálica | e = 0,30–0,50 e μ = 0,80, com desvio de até ±15°: mata a bola e devolve torto (teste: < 60 % da velocidade do vidro). A irregularidade sai de um hash do ponto de contato — determinística, igual em qualquer máquina, porque o online depende disso |
| Paredes (regra FIP, a calibrar com a planta de uma quadra panorâmica) | fundo: 3 m de vidro + 1 m de grade; lateral: 2 m junto ao fundo com vidro de 3 m + grade até 4 m, depois 2 m de degrau com vidro de 2 m + grade até 3 m, e o meio de grade até 3 m | exatamente isso em `Quadra.Paineis`, que a física usa e o desenho vai usar; acima da altura de cada trecho a bola sai |
| Portas | 4 aberturas nas laterais, junto à rede | de 0,45 a 1,25 m da rede, até 2 m de altura; a bola que passa por elas sai ("saída pela porta"), e o árbitro dá o ponto pelas regras de sempre |
| Rede | 0,88 m centro / 0,92 postes | 0,90 m; bola que toca cai |
| Jogador | tiro curto 6–7 m/s; arranca em ~1 s; freia mais forte | humano 6,4 m/s, IA 4,6–6,6; aceleração 9 m/s², frenagem 14 (≈ 1,5 m pra parar) (teste) |
| Balanço | ~0,3 s do início ao contato bom | balanço de 0,30 s; a raquete passa pelo ponto de contato **uma vez**, 0,12 s depois do aperto, e só ali toca a bola — se ela estiver ao alcance (1,35 m) nesse instante; senão a bola passa e o balanço acaba no ar, mesmo que ela entre no alcance antes do fim dele. Erro = dificuldade do corpo no instante (esticado, no corpo, baixo, rápido, revés) + 1/m × o quanto a bola já passou do ponto — sem termo de tempo à parte; erro > 0,95 vira bola na rede ou no vidro; **sem apertar, a bola passa** (teste). IA e modo *Automático* batem com a bola no ponto mais perto do ideal, não na borda do alcance |
| Efeito por golpe (rpm) | drive com topspin, bandeja e víbora com slice/sidespin | drive 1.200 · ataque 2.200 · defesa −1.200 · lob −400 · bandeja −1.500 + 500 lateral · víbora −800 + 1.800 lateral · smash 1.500 · saque −600 |
| Velocidades que saem | pró: smash 100–130 km/h, drive 60–90, bandeja 50–70, lob 30–50, saque 50–70 (aproximado) | drive ≈ 65–75, smash ≈ 100–110, bandeja ≈ 55, lob ≈ 35 — o tempo de voo por golpe é o botão de calibração |

A IA lê a bola simulando **a mesma física** (inclusive efeito), e escolhe bandeja, víbora ou smash pela altura e pela posição — não existe "IA que sabe onde a bola vai cair" por fora da simulação.

**O contato é no instante do balanço, não na borda do alcance** (25/09, `ContatoDoBalancoTests`). Antes, a raquete batia assim que a bola *entrava* no alcance confortável (0,85 m) com o balanço ativo, e o timing era um termo à parte (\|Δt\|/0,15): todo golpe saía na borda e pagava "esticado" mesmo com timing perfeito. Agora o timing É a posição da bola no instante em que a raquete passa: o ponto ideal fica ao lado do corpo, a 0,6 m, do lado da raquete (drive) ou do outro (revés). **Cedo** = a bola ainda longe (esticada; fora do alcance, raquete no ar e a bola segue). **Tarde** = a bola no corpo ou já passada do ponto. A dificuldade do corpo é simétrica e plana até 0,6 m do corpo, então sozinha não separa 40 cm antes de 40 cm depois; por isso o único termo a mais é o **atraso em metros** (quanto a bola já andou além do ponto mais perto do ideal), a 1/m — o \|Δt\|/0,15 antigo com a bola a ~6,5 m/s, a mediana medida no contato. Cedo fica mais barato que tarde de propósito: a bola à frente do corpo é contato natural; a passada é a que vai na rede. A IA e o modo *Automático* batem no passo em que a bola está mais perto do ponto ideal (ou na última chance, se ela vai sair do alcance). O **primeiro quique** do lado de quem recebe não é saída do alcance — a bola volta a subir, e a espera segue (a partida passa o que o árbitro sabe); só o segundo é. E a altura conta, com o mesmo limite da bola baixa da dificuldade (0,3 m): a bola rente ao chão subindo espera subir; a que já quicou e desce rumo ao segundo quique é batida antes de ficar baixa. Sem isso, esperar o ponto ideal fazia a IA bater a bola a 1–4 cm do chão (revisão de 25/09: no Médio, 5,8 % dos golpes antes do quique a menos de 5 cm do chão, contra 0,4 % antes da mudança). Medido em 25/09 (humano simulado × IA Médio: `ferramentas/Calibracao` com 24 partidas por célula, e o timing perfeito com 6; IA × IA: 12 partidas de 1 set, sementes 42–53):

| | antes | depois |
|---|---|---|
| Contato, mediana da distância ao corpo (humano com timing perfeito) | 0,83 m | 0,59 m |
| Idem, IA × IA (Fácil / Médio / Difícil) | 0,83 / 0,83 / 0,83 m | 0,56 / 0,56 / 0,59 m |
| "Esticado" médio no golpe da IA (0 a 0,8), mesma ordem | 0,38 / 0,38 / 0,39 | 0,18 / 0,15 / 0,16 |
| Bola no corpo (a menos de 0,3 m na lateral), IA × IA, mesma ordem | 31 / 27 / 23 % | 25 / 19 / 14 % |
| Golpes da IA que saem errados (rede, vidro), IA × IA, mesma ordem | 22,5 / 12,7 / 7,2 % | 16,9 / 9,4 / 5,4 % |
| Golpes por ponto, sem o saque, IA × IA, mesma ordem | 2,9 / 4,6 / 6,9 | 4,1 / 7,8 / 11,2 |
| Segundos por ponto, IA × IA, mesma ordem | 4,4 / 6,3 / 8,8 | 6,2 / 10,3 / 14,5 |
| Pontos só com o saque (ninguém devolve), IA × IA, mesma ordem | 12,1 / 15,5 / 20,8 % | 6,2 / 6,2 / 6,6 % |
| … desses, saques que quicaram duas vezes depois de ~0,3 s ao alcance de quem recebia (12 partidas), mesma ordem | 15 / 47 / 80 | 0 |
| Golpes bons (erro < 0,3) contra a Médio: Iniciante / Intermediário / Avançado / Profissional | 8 / 18 / 23 / 29 % | 43 / 60 / 72 / 78 % |
| Balanços no ar por ponto contra a Médio (mesma ordem) | 0,34 / 0,30 / 0,28 / 0,28 | 0,39 / 0,35 / 0,27 / 0,24 |
| % de pontos contra a Médio (mesma ordem) | 41 / 56 / 58 / 57 | 48 / 58 / 63 / 66 |
| % de pontos contra a Difícil (mesma ordem) | 34 / 35 / 36 / 39 | 23 / 32 / 39 / 41 |

Os ralis mais longos têm duas causas. Uma é defeito corrigido: com a regra antiga, a IA só batia com a bola no alcance confortável ou já indo embora, e o saque que vinha pela faixa de fora do alcance (0,85–1,1 m), ainda chegando, quicava a segunda vez sem a raquete sair — 12 % dos pontos do Difícil (`Nas_partidas_entre_IAs_nenhum_saque_passa_ao_alcance_de_quem_recebe_sem_ser_devolvido`). Esse não se devolve rebalanceando. A outra é equilíbrio: batendo no ponto ideal, a IA erra menos (a chance de erro cresce com a dificuldade do golpe) e o humano também — o humano ganhou mais do que a IA contra a Médio. **Não foi reajustado: é decisão de playtest** (abaixo).

## Mecânica — o que falta (entra no M1)

- ~~Drive x revés, contato ao lado do corpo, chiquita, contrapared, remate por 3 e por 4~~ — feitos em 25/09 (`GolpesEspeciais`, `Jogador.Destro`, `DificuldadeDoGolpe`): bola no corpo e revés alto saem piores; os golpes especiais são achados simulando a própria física, sem altura de parede escrita à mão.
- **Contato na mão**: a bola ainda sai do ponto de contato calculado, não da raquete animada. Depende da animação (M3).
- **Golpes que faltam**: saída de parede dupla (fundo + lateral) como intenção, globo x lob curto, e a IA mirar a porta.
- **Salto no smash**, **posição do corpo** na bandeja (lateral, raquete alta).
- **Fadiga leve**: sprints seguidos reduzem a aceleração por alguns segundos. Decidir no playtest — pode irritar.
- **Equilíbrio depois do contato no ponto ideal** (25/09): decidir no playtest se fica como está (ralis mais longos, humano mais forte contra a Médio) ou volta perto do de antes. As alavancas: `PerfilDeIA.ChanceDeErro` (Jogadores.cs) e `PesoDoCorpoNoErro` / `PesoDoAtraso` e o limiar 0,95 (Partida.cs). Medido em 25/09 (12 partidas por célula): a chance de erro da IA × 1,34 devolve a taxa de golpes errados de antes (21,3 / 13,0 / 7,5 %), mas os golpes por ponto só descem a 3,6 / 5,9 / 9,5, o humano fica ainda mais forte contra a Médio (53 / 62 / 69 / 70 % dos pontos) e a semente 42 do Difícil troca de vencedor (quebra `A_dificuldade_gradua_o_facil_perde_e_o_dificil_vence_o_parceiro`). Do lado do humano, atraso a 2/m ou limiar 0,75 deixam o Avançado e o Profissional em 61–68 % dos pontos contra a Médio: nenhuma alavanca sozinha devolve o de antes — precisa de um alvo (ex.: "Intermediário × Médio perto de 50 % das partidas") e de mais partidas por célula (com 12, a % de partidas tem desvio padrão de 12 a 14 pontos).
- **Calibração com vídeo** (semana 3): gravar 10 pontos de transmissão, marcar quadros, extrair velocidade e altura de 5 golpes; ajustar tempo de voo e coeficientes; **cada ajuste vira teste**, como a restituição virou.

## Visual — realismo de transmissão

Referência: a transmissão do Premier Padel — câmera alta atrás da dupla, quadra panorâmica de vidro, luz de estádio. Não é fotorrealismo de close, é **parecer TV**. Isso é atingível no Godot 4.7 por um dev solo com arte comprada; fotorrealismo de rosto não é.

**Cena e luz (Godot Forward+).** PBR; iluminação global baked (lightmaps) nas quadras indoor e SDFGI nas abertas; reflexo de tela (SSR) nos vidros; SSAO; TAA ou FSR 2; névoa volumétrica leve na quadra noturna; sombras em cascata do sol/torres.

**Quadra.** Piso de grama sintética com areia (normal + roughness, linhas gastas), vidro temperado 12 mm com reflexo e o verde da borda, estrutura preta, rede com malha real, postes, cadeira do árbitro, bancos, placar LED, painéis de patrocínio (Padelizou e clubes reais), arquibancada com público impostor animado, seis torres de luz.

**Personagens.** Proporção real (1,70–1,90 m), roupa técnica de padel de verdade, suor progressivo, cabelo em cards. Pipeline: Character Creator 4 (exporta glTF/FBX, roupa e morphs) ou modelagem encomendada; 8 personagens = 2 corpos base × variações de rosto, cabelo e roupa. MetaHuman não serve (é Unreal).

**Animação — o item que decide se parece padel.** Captura de movimento é obrigatória. Três caminhos:
1. Estúdio com jogador de padel de verdade: R$ 15–40 mil, 2 dias, ~60 clipes. O melhor resultado.
2. Captura por vídeo (Move One / Rokoko Vision) com celulares: R$ 1–3 mil, qualidade menor, exige limpeza à mão.
3. Animação à mão: cara e inferior.

Plano: **(2) no M1** — o *feel* precisa de animação de padel desde cedo, mesmo suja — e **(1) antes do M3**. No Godot: `AnimationTree` com locomoção em 8 direções + golpes por tipo/altura/lado, root motion, IK da mão na raquete e dos pés no chão. Godot não tem *motion matching* nativo; máquina de estados com blend spaces resolve.

**Bola.** Feltro (shader de fur leve), rastro só na câmera lenta, sombra real, amassado de um quadro no impacto.

**Câmera.** TV atrás (equivalente a 35 mm, 7,5 m de altura, 17,5 m de recuo), leve profundidade de campo, tremida sutil no smash; replay em câmera lenta com ângulo lateral; câmera "ombro" opcional.

**Som.** Gravar num clube: bola na raquete (5 tipos), no vidro, na grade, no chão, passos na areia, respiração; ambiente de clube; público reage ao ponto.

**Desempenho.** 60 fps numa GTX 1060 no preset "alto"; Steam Deck a 40 fps no "médio"; três presets.

## O que muda no plano

- **D4 (arte) revisada → realismo de transmissão.** Orçamento até o Early Access sobe de R$ 25–72 mil para **R$ 70–160 mil** (mocap, personagens realistas, três quadras com iluminação baked). Prazo: M3 cresce 6 semanas → **Early Access na semana 46**; 1.0 seis meses depois.
- **M1 ganha**: balanço com timing (feito hoje), corpo virado e contato na raquete, captura por vídeo de ~20 golpes, calibração com vídeo.
- **Risco novo: vale da estranheza.** Realismo malfeito é pior que stylized bem feito. Mitigação: a régua é o frame da TV lado a lado, e o playtest do M1 pergunta "parece padel?" antes de "é bonito?".
- **O que não muda**: o `Core` continua sem engine; a IA continua lendo a mesma física; o netcode continua host-autoritativo (a simulação realista roda igual no host e no cliente).

## Referências pra calibrar (semana 3)

- Regra FIP da bola e da quadra (já no código); velocidades publicadas de smash profissional; tempo de reação humano (~0,2–0,3 s).
- Vídeo de transmissão em câmera lenta: smash, bandeja, víbora, saída de parede.
- Método: 10 pontos gravados, quadros marcados, velocidade e altura por golpe; ajustar e testar.
