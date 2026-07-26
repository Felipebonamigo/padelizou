# Rodar o Padelizou na sua máquina

Montado em 26/07/2026. Antes disso o projeto **não rodava local**: o `appsettings.json`
ainda apontava pro SQL Server (`.\SQLEXPRESS`) de antes da migração pro PostgreSQL.

## O que está instalado

- **PostgreSQL 17.10** em `C:\Program Files\PostgreSQL\17`
- Serviço `postgresql-x64-17`, sobe sozinho com o Windows
- Banco **`db_padel_local`** (vazio, só o schema)
- Usuário `postgres`, senha `postgres` — é local, não vai pra lugar nenhum

## Como rodar

Pelo Visual Studio: abra `Padelizou.slnx` e aperte F5.

Pelo terminal:

```bash
cd Padelizou && dotnet run
```

O app aplica as migrations sozinho no startup — banco novo vira schema completo sem
comando nenhum.

## Onde fica a configuração

No `Padelizou/appsettings.json`, chave `ConnectionStrings:DefaultConnection`:

```
Host=localhost;Port=5432;Database=db_padel_local;Username=postgres;Password=postgres
```

⚠️ **Esse arquivo é git-ignored de propósito** — ele guarda os segredos de verdade
(Asaas, SMTP, VAPID). Nunca versione. O `appsettings.Development.json`, esse sim, está
no git: não coloque senha nele.

## Comandos úteis

Abrir o banco:

```bash
"C:\Program Files\PostgreSQL\17\bin\psql.exe" -U postgres -h localhost -d db_padel_local
```

Zerar e recomeçar do zero (o app recria tudo no próximo `dotnet run`):

```bash
"C:\Program Files\PostgreSQL\17\bin\psql.exe" -U postgres -h localhost -c "DROP DATABASE db_padel_local" -c "CREATE DATABASE db_padel_local"
```

Trazer os dados do **dev** pra máquina (útil pra reproduzir um bug com dado real):

```bash
ssh root@179.197.233.184 "sudo -u postgres pg_dump db_padel_dev" > dev.sql
```

Depois: `psql -U postgres -h localhost -d db_padel_local -f dev.sql`

> Nunca traga o dump de **produção** pra máquina — são dados pessoais de gente real
> (CPF, telefone, e-mail). Use o dev.

## Se der erro de conexão

1. O serviço está de pé? `Get-Service postgresql*` deve dizer `Running`
2. A porta 5432 está livre? Se você tiver outro Postgres, mude a porta na connection string
3. Lembre da porta 7279: quando o site "não sobe", às vezes é uma instância antiga
   sua ainda rodando pelo VS — feche antes de tentar de novo
