# Padelizou Arena — cronograma até vender na Steam

> Nome provisório. Documento vivo: a cada marco fechado, atualizar as datas reais e o que mudou.
> Escrito em 25/09/2026, semana 0.

## O plano em dez linhas

1. **Produto**: jogo de padel 2x2, terceira pessoa, bonito e rápido de jogar, com **online ranqueado** e **regras oficiais** — o diferencial é jogar padel *de verdade* (parede, bandeja, víbora, chiquita, saída de vidro), não tênis com paredes.
2. **Plataforma**: Steam (Windows + Linux/Steam Deck) em **Early Access**, depois 1.0. Consoles só depois do 1.0, via parceiro de port.
3. **Engine**: **Godot 4.7 + C#** (decisão em `DECISOES.md`). O motor do jogo (regras, física da bola, IA) é uma biblioteca C# pura, `Padel.Core`, testada com `dotnet test` e reusada pelo servidor dedicado.
4. **Multiplayer**: host autoritativo com predição do próprio jogador, transporte pela rede da Steam (Steam Datagram Relay) via Facepunch.Steamworks; ENet no desenvolvimento. Cooperativo local no mesmo PC desde o primeiro marco (e Remote Play Together de graça).
5. **Diferencial de negócio**: a integração com o **Padelizou** — ranking, torneios e jogadores reais; o jogo é a vitrine do circuito e o circuito é o funil do jogo.
6. **Equipe**: Felipe + Claude, com arte, animação e música **compradas ou encomendadas** — é o único jeito de um dev solo entregar "bonito". Direção de arte: **realismo de transmissão** (`REALISMO.md`), com captura de movimento de jogador de padel.
7. **Prazo**: Early Access na **semana 46** (~11 meses a ~20 h/semana; ~6 em dedicação integral). 1.0 seis meses depois. *(Era semana 40 com arte stylized; o realismo custou 6 semanas no M3.)*
8. **Orçamento**: R$ 70–160 mil até o Early Access (mocap, personagens realistas, quadras com luz baked, música, trailer, localização, taxa da Steam), detalhado abaixo.
9. **Métrica que manda**: **wishlists** antes do lançamento. Página na Steam no ar até a semana 24; meta de 7 mil wishlists no dia do Early Access.
10. **Como se ganha**: fazer o jogo divertido no marco 1 antes de gastar um real em arte — se jogar contra a IA no cubo cinza não prender por 20 minutos, o resto não salva.

## Premissas (mudar aqui muda tudo embaixo)

| Premissa | Valor | Se mudar |
|---|---|---|
| Dedicação do Felipe | ~20 h/semana | Integral: divida os prazos por 2 |
| Quem programa | Felipe + Claude | — |
| Arte 3D e animação | **Realismo de transmissão**: personagens realistas comprados/ajustados + captura de movimento de jogador de padel (~60 clipes) | Voltar pra stylized corta o orçamento pela metade e 6 semanas |
| Público-alvo inicial | Brasil, Espanha, Argentina, Itália, Suécia, México | — |
| Idiomas no lançamento | PT-BR, EN, ES; IT e SV no 1.0 | — |
| Preço | US$ 14,99 (R$ 39,99 com preço regional), Early Access a US$ 9,99 | — |

## Mercado: o que já existe e onde entrar

Em setembro de 2026 a Steam já tem padel: **Padel Pro World Tour** (arcade, coop local, lançado em 23/07/2026), **Padel Simulator** (terceira pessoa, foco em física), **Padel Impact Pro** (arcade, online) e **Padel Rivals** (arcade multiplayer com ranking, Early Access prometido pra meados de 2026). Conclusão: a categoria existe e ninguém ainda é "o jogo do padel". A aposta:

- **Fidelidade de regra e tática** como identidade: o jogo em que quem joga padel se reconhece. Os concorrentes vendem "arcade".
- **Online 2x2 ranqueado** com temporadas — é o que faz um jogo de esporte durar.
- **Circuito real**: torneios do Padelizou com chave dentro do jogo, ranking cruzado, o nome do jogador real. Nenhum concorrente tem uma plataforma de torneios por trás.
- **Mercado lusófono/hispânico primeiro**, onde o padel explode e os concorrentes são europeus falando inglês.

Antes de escrever uma linha de arte: **jogar os quatro concorrentes** (custa ~US$ 60) e anotar o que irrita e o que prende. Semana 1.

## Marcos

Cada marco tem uma **definição de pronto** que se testa, não se opina.

### M0 — Fundação (semanas 1–2) ← começa hoje

- [x] Decisão de engine e arquitetura escrita (`DECISOES.md`).
- [x] `Padel.Core` em C#: quadra, placar, física da bola, árbitro, IA, partida — portado do protótipo JS, com os mesmos testes (`dotnet test`).
- [x] Esqueleto do projeto Godot em C# compilando contra o SDK 4.7.2, com quadra 3D em primitivas, bola, jogadores e câmera ligados ao `Padel.Core`.
- [ ] Repositório próprio, CI (build + testes do Core a cada push), template de issue.
- [ ] Conta Steamworks aberta (US$ 100) e nome definitivo escolhido — o nome trava a página, o domínio e a marca; decidir cedo.
- [ ] Jogar os 4 concorrentes; uma página de notas cada.
- **Pronto quando**: `dotnet test` verde no CI, o projeto abre no editor Godot e uma partida IA x IA roda em 3D sem erro.

### M1 — Vertical slice: divertido no cubo cinza (semanas 3–10)

O jogo inteiro, feio. Tudo o que é *sensação de jogo* nasce aqui, e é onde mais se itera.

- [x] Movimento com peso (aceleração 9 m/s², frenagem 14) — no `Core` desde 25/09.
- [x] Golpes com **timing**: balanço de 0,3 s, contato ideal a 0,12 s; cedo é bola no ar, tarde é bola no corpo — no `Core` desde 25/09. Direção pelo analógico; tipos: drive, revés, voleio, **bandeja, víbora, smash, lob** (feitos), **chiquita, saída de parede de fundo e lateral, contra-parede** (faltam). A bandeja e a víbora são o que faz o padeleiro sorrir — prioridade.
- [x] Física da bola real (arrasto quadrático, efeito, quique com spin, vidro x grade) — no `Core` desde 25/09.
- Corpo virado (drive x revés), contato na raquete e não no centro, passo de ajuste, salto no smash — depende da primeira animação.
- Captura por vídeo (Move One / Rokoko Vision) de ~20 golpes de um jogador real, já no M1, pro *feel*.
- Calibrar com vídeo de transmissão (velocidade de smash, altura de lob, quique no vidro): cada ajuste vira teste.
- Controle de gamepad e teclado (Steam Input desde já).
- IA em três níveis com formação de verdade (dupla sobe junto, defende junto).
- Câmera de TV atrás da dupla, com a rede sempre visível; câmera alternativa "lado".
- Partida completa com regras oficiais, placar, replays curtos do ponto.
- **Cooperativo local** (2 gamepads) e 1 x 1 local.
- Som placeholder e música temporária.
- Playtest com **5 pessoas que jogam padel** e 5 que não jogam. Gravar a tela.
- **Pronto quando**: 8 dos 10 testadores jogam 20 minutos sem ser pedidos e dizem uma coisa que querem de volta. Se não passar, M1 continua — não se vai pra arte com jogo chato.

### M2 — Online (semanas 11–18)

- Arquitetura: host autoritativo a 60 Hz, cliente manda entradas, host manda snapshots a 30 Hz; predição do próprio jogador, interpolação dos outros; a bola é simulada pelo `Core` em todo mundo e corrigida pelo snapshot.
- Transporte: ENet (Godot) no dev; **Steam Datagram Relay** via Facepunch.Steamworks no release (NAT traversal e anti-DDoS de graça).
- Lobby: convidar amigo pela Steam, lobby público com lista, 2x2 e 1x1, dupla fixa + dupla aleatória.
- Reconexão em até 30 s; se o host sair, a partida encerra (servidor dedicado fica pro 1.0).
- Teste com latência simulada de 80, 150 e 250 ms — o jogo tem que ser justo a 150.
- Rank inicial (Elo por dupla e por jogador) — pode reusar a régua do Padelímetro (`RANKING.md` do Padelizou).
- **Pronto quando**: 4 pessoas em 4 cidades jogam um set inteiro a 150 ms sem reclamar da bola "pulando".

### M3 — Bonito e com conteúdo (semanas 19–36) — *6 semanas a mais pelo realismo*

- **Arte realista de transmissão** (`REALISMO.md`): 3 quadras (clube indoor com luz baked, praia ao pôr do sol, urbana noturna) com vidro panorâmico, público impostor, LED e patrocínio; 8 personagens realistas (2 corpos base × variações) com roupa de padel real; **sessão de captura de movimento** com jogador de padel (~60 clipes: golpes por tipo/altura/lado, locomoção em 8 direções, comemorações, frustrações), `AnimationTree` com root motion e IK.
- Iluminação e pós-processamento (Forward+: GI, SSR nos vidros, SSAO, TAA/FSR2), partículas (areia, suor), câmera lenta no ponto decisivo, replay com ângulo lateral.
- UI/UX final: menu, lobby, placar estilo TV, replay, resultados.
- Trilha sonora (6–8 faixas) e SFX (bola no vidro é o som que tem que estar perfeito), narrador curto.
- Modos: **Torneio/Carreira** (chaves e grupos — a mesma lógica do Padelizou), Amistoso, Americano, Treino com alvo, Tutorial jogável.
- Localização PT-BR/EN/ES; acessibilidade (daltonismo, remapeamento, tamanho de texto).
- **Pronto quando**: um trailer de 60 s gravado só com o jogo, sem mockup, que dá vontade de wishlistar.

### M4 — Steam, página e demo (semanas 28–38, em paralelo com M3)

- Página "Em breve" no ar na **semana 28** (a Steam exige a página no ar antes do lançamento, e wishlist só acumula com página) — com arte realista de verdade nas cápsulas, nunca com o cubo cinza.
- Cápsulas (todas as 6 medidas), 8 screenshots, trailer, descrição em 3 idiomas, tags certas (Esporte, Multiplayer, Coop Local, Tênis…).
- **Demo** pública e inscrição no **Steam Next Fest** (edições em fevereiro, junho e outubro; escolher a que cair 2–4 meses antes do Early Access).
- Conquistas (20), Steam Cloud (perfil), Rich Presence ("Jogando um set, 4-3"), Remote Play Together, controle Steam Input, verificação **Steam Deck**.
- Builds Windows e Linux automatizadas (export headless do Godot no CI), com `depot` de teste.
- Marketing: 1 clipe curto por semana (TikTok/Reels/YouTube Shorts) a partir da semana 20; contato com 20 influenciadores de padel; parceria com clubes via Padelizou (QR code no torneio → wishlist).
- **Pronto quando**: página aprovada, demo jogável, 2.000 wishlists antes do Next Fest.

### M5 — Playtest, beta e polimento (semanas 39–44)

- **Steam Playtest** fechado (200 pessoas), depois beta aberto pela demo.
- Telemetria mínima (duração de partida, abandono, ping, golpe mais usado) — sem dado pessoal.
- Balanceamento da IA e do rank com dados reais; correção dos 20 bugs mais reportados.
- Textos legais: política de privacidade, EULA, LGPD/GDPR (o jogo guarda o mínimo).
- Preço final e preços regionais; página traduzida; comunicado de imprensa.
- **Pronto quando**: taxa de crash < 0,5 % por sessão, 0 bugs críticos abertos, 5.000+ wishlists.

### M6 — Early Access (semana 46)

- Lançar numa **terça ou quarta**, fora de feriado e de lançamento grande; 10–20 % de desconto de lançamento.
- Roadmap público do Early Access na página (o que vem em 3, 6 e 12 meses).
- Primeiras 2 semanas: patch a cada 2–3 dias, responder toda review.
- **Pronto quando**: está à venda. Meta: 10 reviews positivas na primeira semana (a Steam só mostra nota a partir de 10).

### Depois do Early Access (semanas 47–72) → 1.0

- Temporadas de rank, torneios online com chave, servidor dedicado (o `Padel.Core` roda sem Godot — é um console .NET), cosméticos, mais quadras, mais idiomas (IT, SV, FR).
- Integração Padelizou v2: torneio real com fase online, ranking cruzado, perfil compartilhado.
- 1.0 quando: retenção D7 > 15 %, online estável, conteúdo do roadmap entregue.
- Consoles: só com o 1.0 vendendo; Switch/PS/Xbox via parceiro de port (custo 40–80 mil, prazo 6 meses).

## Semana a semana (visão de calendário, 20 h/semana)

| Semanas | Marco | Entrega visível |
|---|---|---|
| 1–2 | M0 | Core testado, Godot rodando IA x IA em 3D, conta Steam |
| 3–4 | M1 | Movimento e primeiro golpe com timing; gamepad |
| 5–6 | M1 | Todos os golpes; câmera; IA nova |
| 7–8 | M1 | Partida completa; coop local; replay |
| 9–10 | M1 | Playtest de sensação; iteração |
| 11–12 | M2 | Netcode local (ENet) 1x1 |
| 13–14 | M2 | 2x2, predição, snapshots |
| 15–16 | M2 | Steam lobby e SDR; convite de amigo |
| 17–18 | M2 | Latência simulada; rank; playtest online |
| 19–22 | M3 | Arte das quadras; personagens; pipeline de animação |
| 23–26 | M3 + M4 | UI final; trilha; página Steam no ar (sem. 24) |
| 27–30 | M3 + M4 | Modos; localização; demo; trailer |
| 31–32 | M4 | Next Fest; conquistas; Deck; builds automáticas |
| 33–36 | M5 | Playtest fechado; telemetria; balanceamento |
| 37–38 | M5 | Beta aberto; preço; jurídico |
| 39 | M6 | Build de lançamento, review da Steam |
| 40 | M6 | **Early Access no ar** |

## Orçamento estimado até o Early Access

| Item | Faixa (R$) | Nota |
|---|---|---|
| Taxa Steamworks | ~600 (US$ 100) | Devolvida após US$ 1.000 em vendas |
| Personagens realistas + roupas (8, de 2 corpos base) | 20.000–45.000 | Character Creator 4 / encomenda; rosto realista é o item mais caro |
| Captura de movimento (~60 clipes) + limpeza | 15.000–40.000 | Estúdio com jogador de padel; captura por vídeo (R$ 1–3 mil) só no M1 |
| 3 quadras realistas com luz baked, público, LED | 12.000–30.000 | Modelagem + materiais PBR + iluminação |
| Trilha + SFX + narrador | 4.000–10.000 | SFX de vidro e bola: gravar de verdade num clube |
| Trailer e cápsulas | 2.000–6.000 | Cápsula ruim mata wishlist |
| Localização (EN, ES) | 1.500–3.000 | ~3.000 palavras |
| Jurídico (EULA, privacidade) | 1.000–3.000 | Modelo + revisão |
| Marketing (influenciadores, anúncios) | 2.000–10.000 | O melhor canal é o Padelizou, que é grátis |
| **Total** | **70.000–160.000** | Faixa baixa é "comprar pronto e ajustar"; alta é "encomendar". *(Era R$ 25–72 mil com arte stylized.)* |

Receita pra pagar isso: a 30 % da Steam e US$ 9,99, cada venda líquida fica em ~US$ 6 (menos impostos e preço regional). **5 mil cópias** paga a faixa baixa; a alta pede **12 mil** — é o preço do realismo, e é por isso que o preço de venda sobe pra US$ 14,99 no 1.0. A régua de bolso do mercado: de 10 a 20 % das wishlists viram compra na primeira semana — 7 mil wishlists ≈ 700–1.400 vendas de largada.

## Riscos e o que fazer com eles

| Risco | Probabilidade | Mitigação |
|---|---|---|
| O jogo não é divertido | Média | M1 termina só com playtest aprovado; nada de arte antes |
| Animação de padel de qualidade | Alta | Captura por vídeo no M1; sessão de mocap orçada na semana 10 e feita até a 22 |
| Vale da estranheza (realismo malfeito) | Média | Régua: frame da TV ao lado do frame do jogo; playtest pergunta "parece padel?" antes de "é bonito?" |
| Netcode injusto/instável | Média | Host autoritativo + `Core` determinístico o bastante; teste de latência semanal a partir da semana 13 |
| Dev solo, escopo grande | Alta | Este cronograma corta: sem servidor dedicado, sem console, sem editor de quadras até o 1.0 |
| Concorrentes lançam antes | Certa | Já lançaram. A diferença é a fidelidade e o circuito; não competir em "arcade" |
| Nome/marca | Baixa | "Padelizou" já é marca; o sufixo se decide na semana 2 e registra o domínio |
| Página tarde demais | Média | Semana 24 é o limite; wishlist não se recupera |
| Burnout | Alta | Marcos curtos com entrega visível; semana 10 e 18 têm folga de uma semana embutida |

## O que começa hoje (25/09/2026, semana 0)

Está nesta pasta:

- `DECISOES.md` — engine, linguagem, netcode, arte, com os porquês e o que perde cada alternativa.
- `Padel.Core/` — o motor do jogo em C# puro, sem Godot, com os testes que provam as regras.
- `Padel.Godot/` — projeto Godot 4.7 em C#, quadra 3D em primitivas, bola, jogadores e câmera lendo o `Core`; abre no editor e roda IA x IA.
- `docs/` — notas por marco, começando por `M0.md`.

O protótipo em JavaScript (`../padelizou-arcade`) continua útil como **bancada de regras e de IA**: itera em segundos, roda no browser, e serve de demo pra mostrar a ideia sem instalar nada.
