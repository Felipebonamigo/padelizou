# Padelizou Arena

Jogo de padel 2x2 pra Steam. Godot 4.7 + C#, com o motor do jogo numa biblioteca .NET pura.
Nome provisório. O plano inteiro está em [`CRONOGRAMA.md`](CRONOGRAMA.md); as decisões técnicas
e os porquês em [`DECISOES.md`](DECISOES.md); o diário de cada marco em `docs/`.

## Estrutura

| Pasta | O que é |
|---|---|
| `Padel.Core/` | O jogo sem engine: quadra, placar, física da bola, árbitro, IA, partida. `net8.0`, zero dependência. |
| `Padel.Core.Tests/` | xUnit: regras, física, árbitro e partidas inteiras entre IAs com semente fixa. |
| `Padel.Godot/` | O projeto Godot (C#): desenha, lê entrada e, no M2, transporta pacotes. Abrir com o editor **.NET** do Godot 4.7.2. |
| `.github/workflows/ci.yml` | Testa o Core, compila o projeto Godot e roda 15 s de partida sem tela. |

## Rodar

```bash
dotnet test Padel.Core.Tests/Padel.Core.Tests.csproj     # o motor, em segundos
dotnet build Padel.Godot/Padel.Godot.csproj              # compila os scripts contra o GodotSharp (sem editor)

# Com o Godot 4.7.2 .NET instalado (binário "mono"):
godot --path Padel.Godot                                 # joga
godot --path Padel.Godot -- --auto                       # 4 IAs
godot --headless --path Padel.Godot -- --auto --sair-apos 15   # o que o CI faz
```

Argumentos depois de `--`: `--auto` (ninguém humano), `--auto-golpe` (assistência de golpe), `--semente N`,
`--sair-apos SEGUNDOS`, `--screenshot ARQUIVO.png` (com tela), `--facil`, `--dificil`.
Depois de `dotnet build` com scripts novos, rode `godot --headless --path Padel.Godot --import` antes de executar sem tela.

## Controles

Dois botões — **ação** (Espaço/Enter, botão A) e **lob** (Shift/L, botão B) — mais a direção.
Setas/WASD ou analógico esquerdo movem, com inércia: o jogador acelera e freia.

A ação saca e, no rally, **balança a raquete**: o contato ideal sai 0,12 s depois do aperto — cedo
demais é raquete no ar, tarde é bola em cima do corpo. O golpe sai de drive ou de revés conforme o lado
em que a bola passa; o ponto bom de contato é ao lado do corpo, não na frente dele.

| Situação | Comando | Golpe |
|---|---|---|
| Bola normal | ação | drive/revés com topspin; ←/→ escolhem o canto |
| Bola normal | ação + ↑ | ataque curto e rápido |
| Bola normal | ação + ↓ | defesa funda com slice |
| Qualquer bola | lob | lob por cima da dupla que subiu |
| Qualquer bola | lob + ↑ | **chiquita**: baixa e lenta, nos pés de quem está na rede |
| Bola alta | ação | **bandeja**: segura, funda, com slice |
| Bola alta | ação + ← ou → forte | **víbora**: mais agressiva, com efeito lateral |
| Bola alta | ação + ↑ | smash |
| Bola alta | ação + ↑, segurando a ação até o contato | **remate por 4** (sai por cima do fundo); com ← ou → forte, **por 3** (sai pela lateral). Sem espaço pra isso, vira smash |
| Bola atrás de você, junto ao vidro | ação + ↓ | **contrapared**: bate no próprio vidro e passa a rede |

Remate por 3 e por 4 só saem com contato bom. `--auto-golpe` liga a assistência: a raquete bate sozinha
quando a bola chega ao alcance, e segurar a ação vira lob. No coop local, o segundo jogador usa o controle 1
ou IJKL + U (ação) e O (lob). Esc/P pausa.

O desenho do realismo — física da bola, corpo, e o visual de transmissão — está em [`REALISMO.md`](REALISMO.md).

## Convenções

- Identificadores e comentários em português, como no Padelizou.
- `Padel.Core` não referencia Godot. Nunca. É o que permite testar em segundos e rodar um servidor sem engine.
- Coordenadas do Core: `x` largura (-5..5), `y` comprimento (-10..10, casa em `y > 0`), `z` altura. No Godot: `(x, z, y)`.
- Entrada do humano no referencial dele (`Dy < 0` é rumo à rede); a partida converte pro mundo.
- Toda regra vira teste. Defeito corrigido vira teste antes da correção.
