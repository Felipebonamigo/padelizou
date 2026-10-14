---
name: verificar
description: O que rodar no Padelizou Arena (o jogo de padel em padelizou-arena/) antes de dizer que uma mudança está pronta - os testes do Padel.Core, a compilação do Padel.Godot e, com o Godot 4.7.2 .NET, o verificar_tudo.sh (rodada sem tela, conferências da interface, da rede e do som). Use ao terminar qualquer mudança no jogo, antes do commit, ou quando perguntarem "está pronto?", "os testes passam?" ou "roda a verificação".
---
# Verificar o Padelizou Arena

Vale para o jogo em `padelizou-arena/`, no branch `claude/padel-game-new-9yl0ga`. A plataforma web (o resto do
repositório) tem a verificação dela no CLAUDE.md da raiz. As regras da raiz valem aqui também: nada de "pronto" sem
rodar o comando no mesmo turno e ler a saída, e zero teste vermelho para commitar.

## Na nuvem (sem o Godot)

1. SDK: o hook da raiz (`.claude/hooks/session-start.sh`) instala o .NET 10. O Core mira net8.0 e roda no 10 por
   `RollForward=LatestMajor`. Se faltar o `dotnet`, rode o hook à mão na raiz:
   `CLAUDE_CODE_REMOTE=true CLAUDE_PROJECT_DIR=$PWD .claude/hooks/session-start.sh`.
2. O motor, em segundos (~600 testes):
   ```bash
   cd padelizou-arena
   dotnet test Padel.Core.Tests/Padel.Core.Tests.csproj --nologo
   ```
3. Os scripts do Godot compilam contra o GodotSharp, sem editor:
   ```bash
   dotnet build Padel.Godot/Padel.Godot.csproj --nologo
   ```
   `TreatWarningsAsErrors` vale nos projetos, e o `Directory.Build.props` da raiz faz o CS8602 virar erro aqui também.
4. Mexeu em som: `python3 ferramentas/teste_sintetizar_sons.py`. Mexeu numa ferramenta de shell: o teste dela
   (ex.: `ferramentas/teste_rodar_sem_tela.sh`).
5. Os passos com o Godot (abaixo) ficam para o PC ou para quando houver o binário. Diga no commit quais não rodaram e por quê.

## Com o Godot 4.7.2 .NET (PC, ou nuvem com o binário)

```bash
cd padelizou-arena
ferramentas/verificar_tudo.sh <caminho do godot>
```
Termina em "Tudo verde." ou "Falharam: ..." (sai 1). Os 11 passos: `dotnet test` do Core, `dotnet build` do Godot,
`--import`, uma partida IA × IA sem tela (`rodar_sem_tela.sh --auto --semente 42 --sair-apos 15`), a conferência da
interface (`TestePlacar.tscn -- --conferir`), a da rede (`TesteRede.tscn -- --conferir`, ENet em 127.0.0.1), as salas
impossíveis, as rodadas de teste, os sons (gerador e `TesteSom.tscn`) e o teste do `rodar_sem_tela.sh`.

- O `rodar_sem_tela.sh` reprova por tempo esgotado, código diferente de 0, falta de "Saindo após", `ERROR:`,
  `SCRIPT ERROR:`, `Unhandled exception`, `leaked at exit` ou `still in use at exit`.
- Depois de `dotnet build` com script novo, rode `godot --headless --path Padel.Godot --import` antes de executar sem
  tela; sem isso o Godot trava (`docs/M0.md`).
- Para ter o Godot na nuvem, baixe como o CI do jogo faz (`padelizou-arena/.github/workflows/ci.yml`): o zip
  `Godot_v4.7.2-stable_mono_linux_x86_64.zip` dos releases de `godotengine/godot-builds`. Se a rede da sessão recusar,
  pare e diga que os passos do Godot não rodaram.
- O CI do jogo não roda enquanto o Arena mora dentro do `padelizou` (o GitHub só lê o `.github/` da raiz): o
  `verificar_tudo.sh` é a verificação de verdade.

## Commit

- Mensagem em português, `Padelizou Arena: <o que mudou>`, com as linhas de atribuição da sessão. No corpo, o
  resultado do `verificar_tudo.sh` (ex.: "11 de 11 ok", ou quais não rodaram e por quê).
- Só no branch `claude/padel-game-new-9yl0ga`. Nunca no `main`: ele é a plataforma em produção.
- `ONDAS-PARALELAS.md`: implementador nunca commita; quem orquestra commita.
- Feche o bloco com `DONE`, `DONE_WITH_CONCERNS`, `NEEDS_CONTEXT` ou `BLOCKED`.
