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

**Decisão.** Um dos jogadores é o **host** e roda a simulação autoritativa a 60 Hz. Os
clientes enviam entradas (com número de tick); o host devolve snapshots a 30 Hz (posição e
velocidade de bola e jogadores, placar, estado). O cliente **prediz** o próprio jogador e
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

## D4 — Arte stylized, comprada, com animação encomendada

Realismo é o que os concorrentes com mais dinheiro fazem pior do que um jogo AAA; stylized
limpo (proporções levemente exageradas, cores chapadas com iluminação boa) envelhece bem, é
mais barato e roda no Steam Deck. Animações genéricas (corrida, idle, comemoração) vêm do
Mixamo; as **padel-específicas** (bandeja, víbora, saída de parede, chiquita) não existem
prontas em lugar nenhum e são encomendadas — orçar na semana 10, entregar na 22.

## D5 — Steam pelo Facepunch.Steamworks, não pelo GodotSteam

Os dois funcionam. Facepunch é C# nativo (a mesma linguagem do resto), tem lobby, SDR,
conquistas, Rich Presence e Cloud, e o `Padel.Core` continua sem saber que a Steam existe:
tudo passa por uma interface `ITransporte` com implementações `Enet` (dev) e `Steam` (release).

## D6 — O que NÃO entra até o 1.0

Servidor dedicado, editor de quadras, consoles, VR, modo carreira com narrativa, mais de 8
personagens, mais de 3 quadras, cross-play com celular. Cada um é uma funcionalidade boa e
cada um é um mês. A lista existe pra dizer não sem discutir de novo.
