# Publicar o Padelizou (padelizou.com.br)

Guia de deploy num VPS Ubuntu (22.04/24.04 LTS, x86-64). Não versionar nenhum valor real de
segredo neste arquivo — só a estrutura esperada.

## 1. Variáveis de ambiente necessárias em produção

O ASP.NET Core lê configuração aninhada a partir de variáveis de ambiente usando `__` (duplo
underscore) no lugar de `:`. Estas são as chaves que o app espera (ver `Program.cs`):

```
ConnectionStrings__DefaultConnection = Server=localhost;Database=DB_PADEL;User Id=padelizou_app;Password=SENHA_FORTE_AQUI;TrustServerCertificate=True;

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

Essas variáveis vão dentro do `padelizou.service` (seção `[Service]`, uma linha
`Environment="CHAVE=valor"` por variável) — nunca em um arquivo `appsettings.json` que entra no
git.

**Lembrar**: no Google Cloud Console, adicionar `https://padelizou.com.br/GoogleAuth/Callback`
como Redirect URI autorizado, e adicionar cada professor que for usar o Google Calendar como
"usuário de teste" na tela de consentimento OAuth (o app está em modo Testing).

## 2. Preparar o servidor (rodar uma vez)

```bash
# Atualizar o sistema
sudo apt update && sudo apt upgrade -y

# Instalar o runtime do ASP.NET Core 10
sudo apt install -y dotnet-runtime-10.0 aspnetcore-runtime-10.0

# Instalar o SQL Server (Express) — pacote oficial da Microsoft
curl -fsSL https://packages.microsoft.com/keys/microsoft.asc | sudo gpg --dearmor -o /usr/share/keyrings/microsoft-prod.gpg
curl -fsSL https://packages.microsoft.com/config/ubuntu/22.04/mssql-server-2022.list | sudo tee /etc/apt/sources.list.d/mssql-server-2022.list
sudo apt update
sudo apt install -y mssql-server
sudo /opt/mssql/bin/mssql-conf setup
# ^ escolher a edição "Express" quando perguntado, e definir a senha do usuário 'sa'

# Ferramentas de linha de comando do SQL Server (sqlcmd)
curl -fsSL https://packages.microsoft.com/keys/microsoft.asc | sudo gpg --dearmor -o /usr/share/keyrings/microsoft-prod.gpg
curl -fsSL https://packages.microsoft.com/config/ubuntu/22.04/prod.list | sudo tee /etc/apt/sources.list.d/mssql-release.list
sudo apt update
sudo ACCEPT_EULA=Y apt install -y mssql-tools18 unixodbc-dev

# Instalar o Caddy (proxy reverso + HTTPS automático)
sudo apt install -y debian-keyring debian-archive-keyring apt-transport-https
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | sudo gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | sudo tee /etc/apt/sources.list.d/caddy-stable.list
sudo apt update
sudo apt install -y caddy
```

Criar o usuário de aplicação no SQL Server (não usar o `sa` no connection string do app):

```sql
-- via sqlcmd, logado como sa
CREATE LOGIN padelizou_app WITH PASSWORD = 'SENHA_FORTE_AQUI';
CREATE DATABASE DB_PADEL;
USE DB_PADEL;
CREATE USER padelizou_app FOR LOGIN padelizou_app;
ALTER ROLE db_owner ADD MEMBER padelizou_app;
```

## 3. Primeira publicação

No seu PC, use o `deploy.sh` (raiz do projeto) — ele publica e copia pro servidor. Depois, no
servidor:

```bash
cd /opt/padelizou
sudo cp padelizou.service /etc/systemd/system/
# editar /etc/systemd/system/padelizou.service e preencher as variáveis de ambiente da seção 1
sudo systemctl daemon-reload
sudo systemctl enable --now padelizou

# Aplicar as migrations contra o banco novo (rodar do seu PC, apontando pro servidor,
# ou instalar o dotnet-ef no servidor e rodar lá)
dotnet ef database update --connection "Server=SEU_IP;Database=DB_PADEL;User Id=padelizou_app;Password=SENHA_FORTE_AQUI;TrustServerCertificate=True;"

sudo cp Caddyfile /etc/caddy/Caddyfile
sudo systemctl reload caddy
```

## 4. DNS

No painel do registro.br, criar registros tipo A pra `padelizou.com.br` e `www.padelizou.com.br`
apontando pro IP do VPS.

## 5. Atualizações futuras

Rodar `./deploy.sh` no seu PC sempre que quiser subir uma versão nova (ver script na raiz do
projeto). Se a atualização incluir uma migration nova, rodar o `dotnet ef database update` de novo
apontando pro servidor antes de reiniciar o serviço.

## 6. Backup

Sem o backup automático que um serviço gerenciado (tipo Azure SQL) daria, então configurar um
`cron` simples no servidor pra rodar diariamente:

```bash
sudo /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P 'SENHA' -C -Q \
  "BACKUP DATABASE DB_PADEL TO DISK = '/var/backups/padelizou/DB_PADEL_$(date +\%Y\%m\%d).bak'"
```

Idealmente copiar esse `.bak` periodicamente pra fora do servidor também (um serviço de
armazenamento em nuvem barato, ou até baixar manualmente de vez em quando).
