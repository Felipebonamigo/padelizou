#!/bin/bash
# Instala o .NET 10 nas sessões do Claude Code na web.
#
# Sem isto o container vem sem SDK nenhum, e o agente não consegue compilar nem
# rodar os testes — descobre que quebrou algo só depois do push, pelo CI. Com
# isto ele valida antes de commitar, que é o que interessa quando você está
# revisando pelo celular.
#
# Só roda na web: na sua máquina o .NET já está instalado e o hook sai na hora.
#
# ⚠️ ESTE HOOK SEMPRE SAI 0, MESMO QUANDO FALHA — de propósito (21/08/2026).
# SessionStart é o pior lugar possível pra derrubar um processo: se o hook morre,
# não sobra sessão de onde investigar por que ele morreu. Antes daqui o script era
# `set -euo pipefail` com `apt-get install` e `dotnet restore` sem guarda: uma
# rede ruim no restore fazia o hook sair 1 na cara de quem só queria abrir o
# projeto. Agora cada etapa que pode falhar avisa e segue — a sessão abre com ou
# sem SDK, e o agente lê no aviso o que não vai funcionar em vez de adivinhar.
#
# Sem `-e` por isso mesmo; `-u` e `pipefail` ficam, que pegam typo e cano quebrado
# sem interromper nada.
set -uo pipefail

# A rede de segurança de verdade: aconteça o que acontecer daqui pra baixo — erro
# não previsto, `set -u` batendo numa variável, um comando faltando no container —
# o hook termina em 0 e a sessão abre.
trap 'exit 0' EXIT

if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

# Instalação por apt, e não pelo script oficial de dot.net: a política de rede
# do ambiente não libera builds.dotnet.microsoft.com (o download volta 403), e
# o pacote do Ubuntu 24.04 entrega o mesmo SDK 10.
SUDO=""
[ "$(id -u)" -ne 0 ] && SUDO="sudo"

# O container é cacheado depois que o hook termina, então na maioria das
# sessões isto aqui só confere e segue.
if ! dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then
  echo "==> Instalando o .NET 10 (a solução é net10.0)..."
  # O || true é por causa de PPAs de terceiros que a política de rede bloqueia:
  # eles fazem o update reclamar, mas o repositório do Ubuntu — que é de onde o
  # dotnet vem — atualiza normal.
  $SUDO apt-get update -qq || true

  if ! $SUDO env DEBIAN_FRONTEND=noninteractive apt-get install -y -qq dotnet-sdk-10.0; then
    echo "⚠️  A instalação do .NET 10 falhou. A sessão abre assim mesmo, mas" >&2
    echo "    'dotnet build' e 'dotnet test' NÃO vão rodar — trate a suíte como" >&2
    echo "    não verificável nesta sessão em vez de assumir que passou." >&2
    exit 0
  fi
fi

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

# O grep evita empilhar linha repetida quando o hook roda de novo (resume, compact).
if [ -n "${CLAUDE_ENV_FILE:-}" ] && ! grep -q 'DOTNET_NOLOGO' "$CLAUDE_ENV_FILE" 2>/dev/null; then
  {
    echo "export DOTNET_CLI_TELEMETRY_OPTOUT=1"
    echo "export DOTNET_NOLOGO=1"
  } >> "$CLAUDE_ENV_FILE" || true
fi

# Baixa os pacotes agora pra que o primeiro build da sessão não espere por isso.
RAIZ="${CLAUDE_PROJECT_DIR:-$(dirname "$0")/../..}"
if ! cd "$RAIZ"; then
  echo "⚠️  Não achei a raiz do projeto ($RAIZ) — pulei o restore." >&2
  exit 0
fi

echo "==> Restaurando os pacotes..."
if ! dotnet restore Padelizou.slnx; then
  # Restore é adiantamento de trabalho, não pré-requisito: o primeiro build da
  # sessão refaz sozinho. Falhar aqui custa alguns segundos depois, não a sessão.
  echo "⚠️  O restore falhou (rede?). A sessão segue — o primeiro build tenta de novo." >&2
  exit 0
fi

echo "==> Pronto: .NET $(dotnet --version)"
