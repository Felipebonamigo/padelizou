# Fatia jogável no Unreal — mudou de casa

O design aprovado em 14/10/2026 está em [`Felipebonamigo/Padelizou-Game`](https://github.com/Felipebonamigo/Padelizou-Game),
`docs/DESIGN.md`. Este repositório guarda o **gabarito**: `ferramentas/Gabarito` grava trajetórias do `Padel.Core`
que o `PadelSim` (C++) daquele repositório precisa reproduzir bit a bit. Mudou a física aqui, regere o gabarito lá:

```bash
dotnet run --project padelizou-arena/ferramentas/Gabarito -c Release -- ../padelizou-game/Testes/PadelSim/Gabarito $(git rev-parse --short HEAD)
```
