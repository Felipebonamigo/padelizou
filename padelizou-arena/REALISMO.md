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
| Balanço | ~0,3 s do início ao contato bom | balanço de 0,30 s; contato ideal 0,12 s depois do aperto; erro = \|Δt\|/0,15 + 0,6 × dificuldade (esticado, baixo, rápido); erro > 0,95 vira bola na rede ou no vidro; **sem apertar, a bola passa** (teste). Modo *Automático* fica como assistência |
| Efeito por golpe (rpm) | drive com topspin, bandeja e víbora com slice/sidespin | drive 1.200 · ataque 2.200 · defesa −1.200 · lob −400 · bandeja −1.500 + 500 lateral · víbora −800 + 1.800 lateral · smash 1.500 · saque −600 |
| Velocidades que saem | pró: smash 100–130 km/h, drive 60–90, bandeja 50–70, lob 30–50, saque 50–70 (aproximado) | drive ≈ 65–75, smash ≈ 100–110, bandeja ≈ 55, lob ≈ 35 — o tempo de voo por golpe é o botão de calibração |

A IA lê a bola simulando **a mesma física** (inclusive efeito), e escolhe bandeja, víbora ou smash pela altura e pela posição — não existe "IA que sabe onde a bola vai cair" por fora da simulação.

## Mecânica — o que falta (entra no M1)

- ~~Drive x revés, contato ao lado do corpo, chiquita, contrapared, remate por 3 e por 4~~ — feitos em 25/09 (`GolpesEspeciais`, `Jogador.Destro`, `DificuldadeDoGolpe`): bola no corpo e revés alto saem piores; os golpes especiais são achados simulando a própria física, sem altura de parede escrita à mão.
- **Contato na mão**: a bola ainda sai do ponto de contato calculado, não da raquete animada. Depende da animação (M3).
- **Golpes que faltam**: saída de parede dupla (fundo + lateral) como intenção, globo x lob curto, e a IA mirar a porta.
- **Salto no smash**, **posição do corpo** na bandeja (lateral, raquete alta).
- **Fadiga leve**: sprints seguidos reduzem a aceleração por alguns segundos. Decidir no playtest — pode irritar.
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
