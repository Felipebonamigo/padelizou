#!/bin/bash
# Instala o .NET 10 nas sessões do Claude Code na web.
#
# Sem isto o container vem sem SDK nenhum, e o agente não consegue compilar nem
# rodar os testes — descobre que quebrou algo só depois do push, pelo CI. Com
# isto ele valida antes de commitar, que é o que interessa quando você está
# revisando pelo celular.
#
# Só roda na web: na sua máquina o .NET já está instalado e o hook sai na hora.
set -euo pipefail

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
  $SUDO env DEBIAN_FRONTEND=noninteractive apt-get install -y -qq dotnet-sdk-10.0
fi

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

# O grep evita empilhar linha repetida quando o hook roda de novo (resume, compact).
if [ -n "${CLAUDE_ENV_FILE:-}" ] && ! grep -q 'DOTNET_NOLOGO' "$CLAUDE_ENV_FILE" 2>/dev/null; then
  {
    echo "export DOTNET_CLI_TELEMETRY_OPTOUT=1"
    echo "export DOTNET_NOLOGO=1"
  } >> "$CLAUDE_ENV_FILE"
fi

# Baixa os pacotes agora pra que o primeiro build da sessão não espere por isso.
echo "==> Restaurando os pacotes..."
cd "${CLAUDE_PROJECT_DIR:-$(dirname "$0")/../..}"
dotnet restore Padelizou.slnx

echo "==> Pronto: .NET $(dotnet --version)"
