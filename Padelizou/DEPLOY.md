# Publicar o Padelizou (padelizou.com.br)

Arquitetura: **app + banco no mesmo VPS Oracle Cloud "Always Free"** (Ubuntu ARM, sempre ligado,
sem custo). O banco é **PostgreSQL rodando localmente no próprio VPS** — sem cota, sem pausa, sem
mensalidade. Não versionar nenhum valor real de segredo neste arquivo — só a estrutura esperada.

> **Histórico:** até 2026-07-23 o banco era Azure SQL Database (free offer). A cota grátis mensal
> (~100 mil vCore-segundos) esgotava em ~2 dias porque o app mantém o banco sempre ativo, então
> migramos para PostgreSQL self-hosted no VPS. O provider do EF passou de SQL Server para Npgsql.

## 0. Contas necessárias (você cria, eu não tenho como)

1. **Oracle Cloud** ([cloud.oracle.com](https://cloud.oracle.com)) — cria a conta, confirma
   e-mail, cadastra cartão (só verificação, o tier "Always Free" nunca cobra).

(Não precisa mais de conta Azure — o banco é local ao VPS.)

## 1. Criar a VPS na Oracle Cloud

No console da Oracle (**Compute → Instances → Create Instance**):

1. **Name**: `padelizou-prod`.
2. **Image**: Ubuntu 22.04/24.04.
3. **Shape**: **Ampere (ARM), VM.Standard.A1.Flex** — marcar "Always Free eligible". Configurar
   2-4 OCPUs e 12-24GB de RAM (dentro do limite grátis de 4 OCPU/24GB da conta). Espaço de sobra
   pra rodar o app e o PostgreSQL juntos.
4. **SSH Key**: cola sua chave pública (gerar com `ssh-keygen -t ed25519 -C "padelizou"`, conteúdo
   em `C:\Users\Felip\.ssh\id_ed25519.pub`).
5. **Create**. Anota o **IP público** (hoje: `179.197.233.184`). Conecta com `ssh root@SEU_IP`.

**Abrir as portas** (a Oracle bloqueia tudo por padrão, em duas camadas):
- No console: **Networking → Virtual Cloud Networks → (sua VCN) → Security Lists** → regras de
  entrada (Ingress) liberando `80` e `443` (TCP, origem `0.0.0.0/0`).
- No Ubuntu, via SSH:
  ```bash
  sudo iptables -I INPUT -p tcp --dport 80 -j ACCEPT
  sudo iptables -I INPUT -p tcp --dport 443 -j ACCEPT
  sudo netfilter-persistent save
  ```
  > Obs: NÃO abrir a porta `5432` (PostgreSQL) — o banco só escuta em `localhost` e é acessado
  > pelo próprio app na mesma máquina. Deixar o Postgres fechado pra internet é intencional.

## 2. Preparar o servidor (rodar uma vez)

```bash
sudo apt update && sudo apt upgrade -y

# Runtime do ASP.NET Core 10
sudo apt install -y dotnet-runtime-10.0 aspnetcore-runtime-10.0

# PostgreSQL
sudo apt install -y postgresql
sudo systemctl enable --now postgresql

# Caddy (proxy reverso + HTTPS automático)
sudo apt install -y debian-keyring debian-archive-keyring apt-transport-https
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | sudo gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | sudo tee /etc/apt/sources.list.d/caddy-stable.list
sudo apt update
sudo apt install -y caddy
```

> Se `apt update` reclamar do repo `packages.microsoft.com` (chave GPG), ignore — não afeta os
> pacotes do Ubuntu (postgres, caddy). Se precisar, rode com `|| true`.

## 3. Criar o banco PostgreSQL no VPS (rodar uma vez)

```bash
# gera uma senha e cria o role + banco (guarde a senha pra usar na connection string)
PGPASS=$(openssl rand -hex 24); echo "senha do banco: $PGPASS"
sudo -u postgres psql -c "CREATE ROLE padelizou LOGIN PASSWORD '$PGPASS';"
sudo -u postgres createdb -O padelizou db_padel
```

O app **cria e atualiza o schema sozinho no startup** (`db.Database.Migrate()` no `Program.cs`) —
não precisa rodar migração à mão. Um banco novo é populado com dados de demonstração no primeiro
boot (só enquanto não houver jogadores — ver `Data/DadosDemo.cs`).

## 4. Variáveis de ambiente necessárias em produção

O ASP.NET Core lê configuração aninhada a partir de variáveis de ambiente usando `__` (duplo
underscore) no lugar de `:`. Vão dentro do `padelizou.service` (seção `[Service]`, uma linha
`Environment="CHAVE=valor"` por variável) — **nunca** num `appsettings.json` versionado.

```
ConnectionStrings__DefaultConnection = Host=localhost;Port=5432;Database=db_padel;Username=padelizou;Password=SENHA_DO_PASSO_3

EmailSettings__SmtpHost        = smtp.gmail.com
EmailSettings__SmtpPort        = 587
EmailSettings__RemetenteEmail  = seuemail@gmail.com
EmailSettings__RemetenteSenhaApp = senha-de-app-do-gmail
EmailSettings__RemetenteNome   = Padelizou

GoogleCalendar__ClientId       = (mesmo valor já usado localmente)
GoogleCalendar__ClientSecret   = (mesmo valor já usado localmente)
GoogleCalendar__RedirectUri    = https://padelizou.com.br/GoogleAuth/Callback

AcessoAntecipado__Habilitado   = true
AcessoAntecipado__Usuario      = padelizou
AcessoAntecipado__Senha        = TROCAR_ANTES_DE_PUBLICAR

ASPNETCORE_ENVIRONMENT         = Production
```

Para trocar qualquer segredo em produção: editar a linha `Environment=...` correspondente em
`/etc/systemd/system/padelizou.service`, depois `systemctl daemon-reload && systemctl restart padelizou`.
Editar só o `appsettings.json` do servidor **não** adianta — a variável de ambiente sempre vence.

## 5. Primeira publicação

No seu PC, use o `deploy.sh` (raiz do projeto) — ele faz `dotnet publish`, copia pro servidor e
reinicia o serviço. Depois, no servidor (uma vez):

```bash
cd /opt/padelizou
sudo cp padelizou.service /etc/systemd/system/
# editar /etc/systemd/system/padelizou.service e preencher as variáveis da seção 4
sudo systemctl daemon-reload
sudo systemctl enable --now padelizou

sudo cp Caddyfile /etc/caddy/Caddyfile
sudo systemctl reload caddy
```

Como o schema é criado no startup, **não há passo manual de migração** — o app monta o banco no
primeiro boot.

## 6. DNS

No painel do registro.br, criar registros tipo A pra `padelizou.com.br` e `www.padelizou.com.br`
apontando pro IP público da VPS.

## 7. Atualizações futuras

Rodar `./deploy.sh` no seu PC sempre que quiser subir uma versão nova. Alterações de schema entram
como migrations do EF (`dotnet ef migrations add ...` no projeto) e são **aplicadas automaticamente
no próximo start do app** — não precisa rodar nada contra o banco de produção à mão.

## 8. Backup

Backup automático diário do PostgreSQL, configurado no VPS:
- Script: `/usr/local/bin/backup-padelizou.sh` (`pg_dump db_padel | gzip` → `/var/backups/padelizou/`,
  mantém os últimos 14 dias).
- Agenda: `/etc/cron.d/padelizou-backup` (todo dia às 04:00 UTC).

Restaurar um backup:
```bash
gunzip -c /var/backups/padelizou/db_padel_AAAAMMDD_HHMMSS.sql.gz | sudo -u postgres psql db_padel
```

Vale copiar os dumps pra fora do VPS de vez em quando (ex: baixar via `scp`) pra ter cópia off-site.

## 9. Custo

- **Oracle Always Free**: não cobra nada dentro dos limites (4 OCPU/24GB, ~10TB de saída/mês) — folga
  enorme pra essa escala.
- **PostgreSQL**: é software livre rodando no próprio VPS — sem cota, sem mensalidade, sem pausa.
