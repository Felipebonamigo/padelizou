#!/usr/bin/env bash
# Vigia do Padelizou — roda a cada 5 min pelo cron do VPS.
# Checa o /healthz de produção e de dev; se algum não responder 200,
# reinicia o serviço correspondente e registra no log.
# Log: /var/log/padelizou-monitor.log
set -u

LOG="/var/log/padelizou-monitor.log"

checar() {
  local nome="$1" url="$2" servico="$3"
  local codigo
  codigo=$(curl -s -o /dev/null -w "%{http_code}" --max-time 20 "$url" || echo "000")
  if [ "$codigo" != "200" ]; then
    echo "$(date '+%Y-%m-%d %H:%M:%S') [$nome] healthz=$codigo -> reiniciando $servico" >> "$LOG"
    systemctl restart "$servico"
    sleep 10
    local depois
    depois=$(curl -s -o /dev/null -w "%{http_code}" --max-time 20 "$url" || echo "000")
    echo "$(date '+%Y-%m-%d %H:%M:%S') [$nome] pos-restart healthz=$depois" >> "$LOG"
  fi
}

checar "prod" "https://padelizou.com.br/healthz" "padelizou"
checar "dev"  "https://dev.padelizou.com.br/healthz" "padelizou-dev"
