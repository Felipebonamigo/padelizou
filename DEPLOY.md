# Publicar o Padelizou (padelizou.com.br)

Arquitetura: **app no Oracle Cloud "Always Free"** (VPS Linux, sempre ligado, sem custo) +
**banco no Azure SQL Database "Free Offer"** (SQL Server gerenciado, sem custo, sem precisar
migrar nada do código). Não versionar nenhum valor real de segredo neste arquivo — só a estrutura
esperada.

## 0. Contas necessárias (você cria, eu não tenho como)

1. **Oracle Cloud** ([cloud.oracle.com](https://cloud.oracle.com)) — cria a conta, confirma
   e-mail, cadastra cartão (só verificação, o tier "Always Free" nunca cobra). Pode pedir
   confirmação por telefone.
2. **Azure** ([azure.microsoft.com](https://azure.microsoft.com)) — cria a conta (ou usa uma
   existente), confirma que tem acesso ao "Azure SQL Database free offer" (1 banco grátis por
   assinatura, 32GB, 100.000 segundos-vCore/mês).

## 1. Criar a VPS na Oracle Cloud

No console da Oracle (**Compute → Instances → Create Instance**):

1. **Name**: `padelizou-prod`.
2. **Image**: Ubuntu 24.04 (como o banco não roda mais localmente, não tem mais a restrição de
   versão do SQL Server — pode usar a LTS mais nova).
3. **Shape**: trocar para **Ampere (ARM), VM.Standard.A1.Flex** — marcar "Always Free eligible".
   Configurar 2-4 OCPUs e 12-24GB de RAM (dentro do limite grátis de 4 OCPU/24GB no total da
   conta).
4. **SSH Key**: cola sua chave pública (gerar com `ssh-keygen -t ed25519 -C "padelizou"` no
   PowerShell, se ainda não tiver — o conteúdo fica em
   `C:\Users\Felip\.ssh\id_ed25519.pub`).
5. **Create**.

Depois de criada, anota o **IP público**. Testar conexão: `ssh ubuntu@SEU_IP` (usuário padrão da
imagem Ubuntu na Oracle é `ubuntu`, não `root`).

**Abrir as portas** (a Oracle bloqueia tudo por padrão, em duas camadas):
- No console: **Networking → Virtual Cloud Networks → (sua VCN) → Security Lists** → adicionar
  regras de entrada (Ingress) liberando as portas `80` e `443` (TCP, origem `0.0.0.0/0`).
- No próprio Ubuntu, depois de conectado via SSH:
  ```bash
  sudo iptables -I INPUT -p tcp --dport 80 -j ACCEPT
  sudo iptables -I INPUT -p tcp --dport 443 -j ACCEPT
  sudo netfilter-persistent save
  ```

## 2. Criar o banco no Azure SQL (Free Offer)

No portal Azure (**SQL databases → Create**):

1. Cria um **SQL Server (logical server)** novo — anota o nome (vira
   `SEUSERVIDOR.database.windows.net`), define um login de administrador e senha forte.
2. Na criação do banco, em **Compute + storage**, escolhe **Serverless** e marca a opção de usar
   o **free offer** (aparece como opção quando disponível na assinatura).
3. Em **Networking**, marca "Allow Azure services to access this server" e adiciona uma regra de
   firewall liberando o **IP público da VPS da Oracle** (passo 1) especificamente — evita deixar
   o banco aberto pra qualquer IP.
4. Nome do banco: `DB_PADEL`.

## 3. Variáveis de ambiente necessárias em produção

O ASP.NET Core lê configuração aninhada a partir de variáveis de ambiente usando `__` (duplo
underscore) no lugar de `:`. Estas são as chaves que o app espera (ver `Program.cs`):

```
ConnectionStrings__DefaultConnection = Server=tcp:SEUSERVIDOR.database.windows.net,1433;Database=DB_PADEL;User Id=SEU_LOGIN_ADMIN;Password=SENHA_FORTE_AQUI;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;

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

## 4. Preparar o servidor (rodar uma vez)

Bem mais simples que instalar banco local — só o runtime do .NET e o Caddy:

```bash
sudo apt update && sudo apt upgrade -y

# Runtime do ASP.NET Core 10
sudo apt install -y dotnet-runtime-10.0 aspnetcore-runtime-10.0

# Caddy (proxy reverso + HTTPS automático)
sudo apt install -y debian-keyring debian-archive-keyring apt-transport-https
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | sudo gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | sudo tee /etc/apt/sources.list.d/caddy-stable.list
sudo apt update
sudo apt install -y caddy
```

## 5. Primeira publicação

No seu PC, use o `deploy.sh` (raiz do projeto) — ele publica e copia pro servidor. Depois, no
servidor:

```bash
cd /opt/padelizou
sudo cp padelizou.service /etc/systemd/system/
# editar /etc/systemd/system/padelizou.service e preencher as variáveis de ambiente da seção 3
sudo systemctl daemon-reload
sudo systemctl enable --now padelizou

sudo cp Caddyfile /etc/caddy/Caddyfile
sudo systemctl reload caddy
```

Aplicar as migrations contra o banco do Azure (pode rodar direto do seu PC, já que o Azure SQL é
acessível pela internet — só garantir que seu IP também está liberado no firewall do banco
enquanto for rodar isso do PC, ou rodar de dentro da própria VPS):

```bash
dotnet ef database update --connection "Server=tcp:SEUSERVIDOR.database.windows.net,1433;Database=DB_PADEL;User Id=SEU_LOGIN_ADMIN;Password=SENHA_FORTE_AQUI;Encrypt=True;"
```

## 6. DNS

No painel do registro.br, criar registros tipo A pra `padelizou.com.br` e `www.padelizou.com.br`
apontando pro IP público da VPS da Oracle.

## 7. Atualizações futuras

Rodar `./deploy.sh` no seu PC sempre que quiser subir uma versão nova (ver script na raiz do
projeto). Se a atualização incluir uma migration nova, rodar o `dotnet ef database update` de novo
apontando pro Azure antes de reiniciar o serviço.

## 8. Backup

O Azure SQL Database já faz backup automático (restauração a um ponto no tempo) como parte do
próprio serviço — diferente do banco autogerenciado, aqui não precisa configurar nada extra.
Só vale conferir no portal, na página do banco, se o período de retenção mostrado atende (padrão
costuma ser 7 dias).

## 9. Atenção ao uso (ficar dentro do grátis)

- **Azure SQL**: 100.000 segundos-vCore + 32GB grátis por mês, por banco. Dá pra acompanhar o
  consumo no portal (métricas do banco). Se algum dia estourar num mês de muito uso, ou escolhe
  pausar até o mês seguinte, ou paga só o excedente — configurável na página do banco.
- **Oracle**: o tier Always Free não cobra nada dentro dos limites (4 OCPU/24GB, ~10TB de saída
  de dados/mês) — folga enorme pra essa escala de app.
