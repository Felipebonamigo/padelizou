# Decisões técnicas (ADR)

Cada decisão registra o contexto, as alternativas e o que se perde. Mudar uma decisão é abrir
uma seção nova com a data, não apagar a antiga.

## D1 — Engine e linguagem: Godot 4.7 + C#

**Contexto.** Um desenvolvedor (Felipe, que já programa C#/.NET no Padelizou) com apoio de IA,
alvo Steam (Windows, Linux, Steam Deck), jogo 3D multiplayer de escopo médio, orçamento curto.

| Critério | Godot 4.7 + C# | Unity 6 + C# | Unreal 5 + C++/Blueprint |
|---|---|---|---|
| Linguagem que o Felipe já domina | ✅ C# | ✅ C# | ❌ C++ |
| Custo/licença | Grátis, MIT, sem royalty | Grátis até US$ 200 mil/ano de receita; histórico de mudança de regra | 5 % de royalty acima de US$ 1 milhão |
| Roda no container de desenvolvimento por linha de comando (testes, export headless no CI) | ✅ binário único, headless nativo | ⚠️ pesado, licença por máquina | ⚠️ muito pesado |
| Multiplayer embutido | ✅ ENet + replicação de cena; Steam via Facepunch.Steamworks | ✅ Netcode for GameObjects | ✅ o mais maduro |
| Gráfico "bonito" com dev solo | ✅ Forward+ (Vulkan), suficiente pra stylized | ✅ | ✅✅ o melhor, mas exige equipe |
| Steam Deck / Linux | ✅ export nativo | ✅ | ✅ |
| Consoles | ⚠️ via parceiro (W4 Games), pago | ✅ | ✅ |
| Tamanho da comunidade / assets prontos | ⚠️ menor que Unity | ✅✅ | ✅ |
| Código aberto (dá pra ler e consertar a engine) | ✅ | ❌ | ✅ (fonte disponível) |

**Decisão.** Godot 4.7 com C# (SDK `Godot.NET.Sdk/4.7.2`, alvo `net8.0`).
O motor do jogo — regras, física da bola, árbitro, IA, estado da partida — fica em uma
biblioteca C# **sem referência ao Godot** (`Padel.Core`), testada com xUnit. Godot só desenha,
lê entrada e transporta pacotes. Isso é o que permite: testar a lógica em segundos no CI,
rodar um servidor dedicado sem engine no futuro, e trocar de engine sem reescrever o jogo se
um dia fizer sentido.

**O que se perde.** Loja de assets menor que a da Unity (mitigação: personagens e animação
vêm de lojas independentes e do Mixamo, que exportam FBX/glTF pra qualquer engine). Consoles
exigem parceiro (é pós-1.0 de qualquer forma).

**Alternativas descartadas.**
- *JavaScript/Web (o protótipo)*: não é um caminho pra Steam com 3D e online de qualidade; fica como bancada.
- *Unity*: C# também, mas licença instável, editor pesado demais pra automação no container e fechada.
- *Unreal*: melhor gráfico, mas C++ e o custo de aprender enquanto se faz um jogo solo é o risco que mais mata projeto.
- *Motor próprio (MonoGame/Raylib)*: total controle, mas tudo o que a engine dá de graça (editor, física, animação, export, input) viraria trabalho nosso.

## D2 — Netcode: host autoritativo com predição, transporte da Steam

**Contexto.** 2x2 (4 clientes), ritmo rápido, bola compartilhada — o objeto mais sensível a latência.

**Decisão.** Um dos jogadores é o **host** e roda a simulação autoritativa a **120 Hz** (o mesmo
passo do jogo local — *era 60 Hz no texto de 25/09 manhã; o `Padel.Core.Rede` saiu com 120 e ficou*).
Os clientes enviam entradas (com número de sequência, e cada aperto como **contador**, repetidas nas
8 últimas — perda de pacote não some nem duplica aperto); o host devolve instantâneos a 30 Hz
(~165 bytes em média: bola, jogadores, placar, estado e os eventos recentes com id, repetidos por
~0,5 s). O cliente **prediz** o próprio jogador e
**interpola** os outros; a bola é simulada localmente pelo `Padel.Core` (mesmo código do host)
e corrigida suavemente quando o snapshot chega. Transporte: Godot ENet no desenvolvimento;
**Steam Datagram Relay** (via Facepunch.Steamworks) no release, que resolve NAT e esconde IP.

**O que se perde.** Rollback (estilo jogo de luta) daria golpes mais justos a 150 ms, mas exige
simulação 100 % determinística em todos os PCs — com `float` isso é frágil e o custo não cabe
no Early Access. Host com vantagem de latência: mitigado pela predição e por medir o ping na
tela; servidor dedicado (o `Core` roda num console .NET) entra no 1.0 pra partidas ranqueadas.

## D3 — Simulação em passo fixo, `float`, unidades reais

Passo fixo de 1/60 s no `Core`, sub-passos de 1/240 s na bola. Metros, segundos, quilos.
Um `Random` com semente por partida, dono único (a IA pede números dele) — a mesma semente
reproduz o mesmo jogo, o que é como se depuram bugs de rede e de IA.

## D4 — Arte: realismo de transmissão (revisada em 25/09/2026)

**Decisão original (25/09, manhã):** stylized limpo, comprado, animação encomendada — mais barato,
envelhece bem, roda no Deck.

**Revisão (25/09, tarde), a pedido do Felipe:** *"temos que pensar no realismo, visual e de
mecânica"*. A arte passa a mirar **parecer a transmissão do Premier Padel**: proporções reais,
roupa real, quadra panorâmica de vidro com iluminação de estádio, captura de movimento de
jogador de padel. Não é fotorrealismo de close (rosto em close é o que um dev solo não entrega);
é realismo de câmera de TV. O desenho completo, com pipeline e custos, está em `REALISMO.md`.

**O que custa.** Orçamento de R$ 25–72 mil para R$ 70–160 mil; M3 seis semanas maior; Early
Access na semana 46 em vez da 40. Risco novo: vale da estranheza — realismo malfeito é pior que
stylized bem feito; a régua é o frame da TV ao lado do frame do jogo.

## D5 — Steam pelo Facepunch.Steamworks, não pelo GodotSteam

Os dois funcionam. Facepunch é C# nativo (a mesma linguagem do resto), tem lobby, SDR,
conquistas, Rich Presence e Cloud, e o `Padel.Core` continua sem saber que a Steam existe:
tudo passa por uma interface `ITransporte` com implementações `Enet` (dev) e `Steam` (release).

## D7 — Golpe "sim-cade": intenção do jogador + erro do corpo, nunca colisão animada

A bola é simulada de verdade; a entrada do humano é **intenção** (direção, tipo, timing do
balanço) e o erro vem do **corpo** (timing fora do ideal, esticado, bola baixa ou rápida). Não
simulamos a colisão raquete-bola a partir da animação: parece mais real e joga pior — vira
sorteio, o jogador não prevê o que sai, e o netcode sofre. É o modelo de Top Spin e EA FC.
Modo *Automático* (bate sozinho ao alcance) fica como assistência e acessibilidade.

## D8 — Física da bola com os números do esporte, calibrada por teste

Massa e raio da regra FIP, arrasto quadrático (Cd 0,55), Magnus (Cl de Štěpánek), quique com
transferência de spin (rolamento limitado por atrito), vidro (e 0,85) diferente de grade
(e 0,40). Cada coeficiente que saiu da literatura tem um teste que o prende a um fato observável
(a bola solta de 2,54 m sobe 1,35–1,45 m; uma bola a 30 m/s perde 15–30 % em 10 m). Calibrar
com vídeo é mudar o número **e** o teste, nunca só o número.

## D9 — Rating do online: a régua do Padelímetro, com duas regras próprias do jogo (a aprovar)

O online ranqueado usa a mesma matemática do Padelímetro do Padelizou (`Padel.Core/Ranking`,
cada regra com a origem no comentário). Duas situações não existem no site e ganharam regra
aqui — **as duas esperam aprovação do Felipe**:

- **Abandono.** A dupla de quem abandona perde como num 6x0 — inclusive o parceiro, que não
  saiu. A outra dupla não anda. Punir só quem sai deixa aberta a "procuração": uma conta
  descartável abandona de propósito e o parceiro sobe de graça (medido: de 700 a 815 com 50 %
  de vitória). O custo é o parceiro inocente de um estranho que cai; a reconexão de 30 s (M2)
  absorve a queda honesta. Alternativa se o custo pesar: o parceiro não anda na fila solo e
  paga na dupla combinada.
- **Melhor de 3.** O fator de games é o de UM set com a margem média do vencedor por set. Somar
  os sets invertia o fator (7-6 0-6 7-6 valia mais que um 7-6 7-6). Em set único o número é
  idêntico ao do site; em 2+ sets não é, porque o site guarda só os games do set em andamento.
  Se o ranking cruzado precisar bater também aí, uma das duas réguas muda.

Partida contra IA nunca mexe no rating.

## D10 — Equilíbrio depois do contato no ponto ideal (a decidir no playtest)

Desde 25/09 o golpe acontece no instante do balanço e no ponto ideal ao lado do corpo, pra humano e IA (antes saía
na borda do alcance e pagava "esticado" mesmo com timing perfeito). Isso trouxe ralis mais longos (Médio x Médio: 4,6 →
7,8 golpes por ponto; 6,3 → 10,3 s) e o humano simulado mais forte contra a Médio (Avançado: 92 → 100 % das partidas).
Parte dos ralis mais longos é **defeito corrigido** (o saque que passava ao alcance sem ser devolvido, 12 % dos pontos
do Difícil) e está travada por teste. O resto é equilíbrio e **não foi reajustado**: nenhuma alavanca sozinha devolve o
de antes, e o alvo é do Felipe (ex.: "Intermediário x Médio perto de 50 % das partidas"). Números e alavancas no
`REALISMO.md` ("o que falta").

## D6 — O que NÃO entra até o 1.0

Servidor dedicado, editor de quadras, consoles, VR, modo carreira com narrativa, mais de 8
personagens, mais de 3 quadras, cross-play com celular. Cada um é uma funcionalidade boa e
cada um é um mês. A lista existe pra dizer não sem discutir de novo.
