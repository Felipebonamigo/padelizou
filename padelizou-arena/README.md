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

Argumentos depois de `--`: `--auto` (ninguém humano), `--semente N`, `--sair-apos SEGUNDOS`, `--facil`, `--dificil`.

## Controles (M0)

Setas/WASD ou analógico esquerdo movem. Espaço/Enter/botão A saca e, segurado na hora do golpe,
dá lob. ←/→ no golpe escolhem o canto; ↑ ataca curto; ↓ joga fundo. Esc/P pausa.
O golpe é automático quando a bola entra no alcance — o timing com botão é trabalho do M1.

## Convenções

- Identificadores e comentários em português, como no Padelizou.
- `Padel.Core` não referencia Godot. Nunca. É o que permite testar em segundos e rodar um servidor sem engine.
- Coordenadas do Core: `x` largura (-5..5), `y` comprimento (-10..10, casa em `y > 0`), `z` altura. No Godot: `(x, z, y)`.
- Entrada do humano no referencial dele (`Dy < 0` é rumo à rede); a partida converte pro mundo.
- Toda regra vira teste. Defeito corrigido vira teste antes da correção.
