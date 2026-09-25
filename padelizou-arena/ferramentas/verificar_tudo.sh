#!/usr/bin/env bash
# Tudo o que o CI da pasta (.github/workflows/ci.yml) faz, rodado aqui. Enquanto o Arena mora dentro do repositório
# padelizou, aquele CI NÃO roda (o GitHub só lê .github/ da raiz) — então este script é a verificação de verdade.
# Uso: ferramentas/verificar_tudo.sh GODOT   (o binário do Godot 4.7.2 .NET)
set -uo pipefail
godot="$1"
raiz="$(cd "$(dirname "$0")/.." && pwd)"
cd "$raiz"
falhas=()
passo() {   # passo NOME COMANDO...
    local nome="$1"; shift
    echo "==> $nome"
    if "$@"; then echo "    ok"; else echo "    FALHOU"; falhas+=("$nome"); fi
}

passo "motor (xUnit)" dotnet test Padel.Core.Tests/Padel.Core.Tests.csproj --nologo
passo "compilar o Godot" dotnet build Padel.Godot/Padel.Godot.csproj --nologo
passo "importar" "$godot" --headless --path Padel.Godot --import
passo "partida sem tela, saída limpa" ferramentas/rodar_sem_tela.sh "$godot" --auto --semente 42 --sair-apos 15
passo "conferência da interface" "$godot" --headless --path Padel.Godot res://cenas/TestePlacar.tscn -- --conferir
passo "conferência da rede" "$godot" --headless --path Padel.Godot res://cenas/TesteRede.tscn -- --conferir
passo "salas impossíveis" ferramentas/conferir_salas_impossiveis.sh "$godot"
passo "rodadas de teste: não tocam no save de quem joga e terminam sozinhas" ferramentas/conferir_rodadas_de_teste.sh "$godot"
passo "som: gerador" python3 ferramentas/teste_sintetizar_sons.py
passo "som: TesteSom" "$godot" --headless --audio-driver Dummy --path Padel.Godot res://cenas/TesteSom.tscn
passo "o rodar_sem_tela reprova o que está quebrado" ferramentas/teste_rodar_sem_tela.sh

echo
if [ ${#falhas[@]} -eq 0 ]; then echo "Tudo verde."; else echo "Falharam: ${falhas[*]}"; exit 1; fi
