# Supply chain — o que o Padelizou carrega junto

Auditoria de 21/08/2026 sobre a árvore de dependências, o CI e o deploy.
Máquina-legível em `supply-chain-findings.json`. Comandos de reprodução no fim.

**Resumo:** a árvore tem **150 pacotes** (19 diretos, 131 transitivos) e **nenhuma CVE
conhecida** pelo audit do NuGet. O problema não é vulnerabilidade — é **quantidade**:
metade do que ia pro servidor não tinha razão de estar lá. Um achado já está corrigido
e verificado; o resto está descrito com a correção pronta, sem aplicar.

| | |
|---|---|
| Alto | 2 (1 corrigido) |
| Médio | 5 |
| Baixo | 5 |

---

## SC-01 · ALTO · Um pacote de scaffolding despejava 46 DLLs de terceiros no VPS — **CORRIGIDO**

`Microsoft.VisualStudio.Web.CodeGeneration.Design` estava referenciado sem
`<PrivateAssets>all</PrivateAssets>` — ao contrário do `Microsoft.EntityFrameworkCore.Tools`
três linhas acima, que já estava certo. Sem essa marcação, um pacote de *ferramenta de mesa*
vira dependência de runtime e viaja inteiro pro servidor, com a árvore dele junto.

O que ia junto, e não é pouco:

- **o compilador Roslyn completo** (12 DLLs `Microsoft.CodeAnalysis.*`)
- **o MSBuild** (`Microsoft.Build.dll`, `Microsoft.Build.Framework.dll`)
- **o cliente NuGet inteiro** (9 DLLs `NuGet.*`)
- `Microsoft.AspNetCore.Razor.Language` **6.0.24 — .NET 6, fora de suporte desde nov/2024**
- `System.Data.DataSetExtensions` **4.5.0, publicado em 2018**
- `Humanizer` 2.14.1 (jan/2022) **+ 50 pastas de satélite de idioma**
- `Microsoft.CodeAnalysis.Elfie` 1.0.0 — único pacote da Microsoft na árvore sem proveniência de código-fonte no nuspec

Nenhum deles é usado: `grep` por `CodeGeneration`/`Scaffolding` em `.cs`, `.cshtml` e `.razor`
retorna só a própria linha do `.csproj`. Mesma coisa com `NuGet.Packaging` e `NuGet.Protocol`,
que estavam listados como dependência direta do app e não aparecem em uma linha de código.

**Por que isso é supply chain e não faxina.** Cada uma dessas DLLs roda no VPS com os
privilégios do app — credenciais do Postgres, tokens do Google, chaves do Asaas. Um pacote
comprometido ali dentro é o app comprometido. E tem o agravante: Roslyn + MSBuild + cliente
NuGet num servidor web não são só superfície, são *ferramental* — compilar e executar código
novo e baixar pacotes, tudo sem sair do processo. É a diferença entre um invasor que precisa
trazer as ferramentas dele e um que já encontra tudo instalado.

**Correção aplicada** (`Padelizou/Padelizou.csproj`): `PrivateAssets=all` no
`CodeGeneration.Design`, e `NuGet.Packaging`/`NuGet.Protocol` removidos.

| medido no `dotnet publish` | antes | depois |
|---|---|---|
| DLLs no pacote | 66 | **20** |
| pastas (satélites de idioma) | 53 | **3** |
| itens no pacote | — | **96 a menos** |

Build Release limpo, **4781 testes verdes**, `dotnet ef migrations has-pending-model-changes`
não é afetado (o `EntityFrameworkCore.Tools` continua onde estava).

---

## SC-02 · ALTO · 427 MB de binários nativos para 17 plataformas que o VPS nunca executa

Depois do SC-01 o pacote ainda tem 493 MB — e **439 MB são a pasta `runtimes/`**:

```
win-x86  98MB   win-x64  97MB   win-arm64  92MB   osx  16MB
linux-x86, linux-arm, linux-arm64, linux-musl-{x64,arm,arm64,riscv64,loongarch64},
linux-bionic-{x64,arm64} (Android), linux-riscv64, linux-loongarch64
```

O VPS é Linux x64. As outras 17 são `libSkiaSharp` compilado para plataformas que esse
servidor não vai executar nunca — o meta-pacote `SkiaSharp` traz `NativeAssets.Win32` e
`NativeAssets.macOS` por transitividade, mesmo com o `NativeAssets.Linux.NoDependencies`
já declarado explicitamente pro VPS.

**Binário nativo é a pior superfície que existe nesse assunto**: executa direto, não é IL,
nenhuma ferramenta do .NET olha dentro, e o audit do NuGet não inspeciona conteúdo. São 17
blobs opacos por build, sem motivo.

**Correção, verificada aqui mas não aplicada:**

```diff
- dotnet publish Padelizou/Padelizou.csproj -c Release -o publish
+ dotnet publish Padelizou/Padelizou.csproj -c Release -r linux-x64 --self-contained false -o publish
```

| | atual | com RID |
|---|---|---|
| pacote publicado | 493 MB | **66 MB** |
| `tar.gz` (o que o CI anexa) | 158 MB | **24 MB** |
| plataformas em `runtimes/` | 17 | **0** (o `libSkiaSharp.so` vai pra raiz) |

Conferi que os três arquivos que o `deploy.sh` exige antes de trocar a versão
(`Padelizou.dll`, `Padelizou.runtimeconfig.json`, `Padelizou.staticwebassets.endpoints.json`)
estão todos lá. **Mas isso muda o layout dos assets nativos — testar no `dev` antes do `prod`.**

De brinde, resolve um problema que você já tinha contornado: o `deploy.sh` guarda 3 releases
por ambiente porque cada build ocupa ~530 MB (comentário de 11/08/2026: "5,2 GB — quase um
terço do disco"). Com 66 MB por build, 10 releases cabem em 660 MB.

---

## SC-03 · MÉDIO · Uma biblioteca de cripto de 2021 que o scanner **nunca** vai flagrar

`Portable.BouncyCastle` **1.9.0**, publicado em **19/10/2021**. É a última versão que vai
existir: o projeto renomeou o pacote para `BouncyCastle.Cryptography` na 2.0. Chega aqui via
`WebPush` 1.0.13 — que não está abandonado (foi publicado em 28/04/2026) mas continua
declarando `Portable.BouncyCastle 1.9.0` no grupo `net10.0` do nuspec. Não há saída pela via
do pacote.

O `dotnet list package --vulnerable` do projeto veio limpo. **Isso não quer dizer o que
parece.** Testei o mecanismo, e dá pra reproduzir em um minuto: um `.csproj` com
`NuGetAudit=true` e `NuGetAuditMode=all`, referenciando os dois:

```
warning NU1902: 'BouncyCastle.Cryptography' 2.2.1 — GHSA-8xfc-gm6g-vgpv
warning NU1902: 'BouncyCastle.Cryptography' 2.2.1 — GHSA-m44j-cfrm-g8qc
warning NU1902: 'BouncyCastle.Cryptography' 2.2.1 — GHSA-v435-xc8x-wvr9
(sobre Portable.BouncyCastle 1.9.0: nada)
```

Mesmo restore, mesma fonte de advisory, mesma biblioteca — e o build **mais velho** passa em
silêncio. A razão é estrutural: advisory é indexado por **ID de pacote**. Quem renomeia o
pacote sai do índice, e nenhum advisory novo vai apontar para o ID antigo. Não é bug do
NuGet; é como o índice funciona. O efeito prático é que esse pacote é invisível para
qualquer relatório de vulnerabilidade que você venha a rodar, hoje ou daqui a cinco anos.

**Exposição real: baixa.** O app usa WebPush em dois lugares
(`PushNotificationService.cs:34,176` e `VarreduraDeFantasmas.cs:62,77`), sempre o mesmo par
`VapidDetails` + `SendNotificationAsync`: ECDSA P-256 e ECDH sobre dados que o próprio app
gera, mandados pros endpoints de push. Não parseia certificado de terceiro, não usa RSA nem
Ed25519 — que é justamente onde ficam os três advisories do ID novo.

**Não é pra corrigir hoje. É pra saber.** Vale como revisão manual periódica. Se um dia
incomodar, o caminho é trocar o WebPush por VAPID nativo — o `ECDsa` do
`System.Security.Cryptography` assina o JWT ES256, e o uso aqui é pequeno o suficiente pra
isso caber.

De quebra: o app carrega **duas** BouncyCastle ao mesmo tempo — `BouncyCastle.Crypto.dll`
(1.9.0, do WebPush) e `BouncyCastle.Cryptography.dll` (2.6.2, do MailKit).

---

## SC-04 · MÉDIO · O CI não reprova por vulnerabilidade

O audit **já está ligado e bem configurado** — `NuGetAudit=true`, `NuGetAuditMode=all`
(cobre as 131 transitivas), `NuGetAuditLevel=low`. Só que ele emite *warning*, o
`TreatWarningsAsErrors` é `false`, e nenhum passo do `ci.yml` checa o resultado. Uma CVE
nova em qualquer um dos 150 pacotes entra em produção com um aviso no meio do log do
restore.

É o mesmo raciocínio do passo `has-pending-model-changes` que você já pôs no `ci.yml`: a
regra morava na memória do projeto até virar falha de build.

```yaml
      - name: Conferir vulnerabilidade nos pacotes
        run: |
          saida=$(dotnet list Padelizou.slnx package --vulnerable --include-transitive 2>&1)
          echo "$saida"
          if echo "$saida" | grep -q "has the following vulnerable packages"; then
            echo "::error::Pacote com vulnerabilidade conhecida na árvore."
            exit 1
          fi
```

---

## SC-05 · MÉDIO · Sem lockfile: 131 dependências não estão fixadas em lugar nenhum

`RestorePackagesWithLockFile` está vazio e não existe `packages.lock.json`. O repositório
fixa as 19 versões diretas; as outras 131 o restore resolve na hora, dentro do runner do CI.
Duas execuções do mesmo commit podem produzir pacotes diferentes, e nada no repositório
registra o hash do que foi baixado.

É o que separa "o CI testou o mesmo código" de "o CI testou o mesmo código **e as mesmas
dependências**".

Correção: `<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>` nos dois
`.csproj`, commitar os `packages.lock.json`, e `dotnet restore --locked-mode` no CI. Gera
dois arquivos novos e faz o restore falhar quando alguém mudar versão sem regenerar o lock —
que é o ponto.

---

## SC-06 · MÉDIO · Actions presas a tag móvel

`actions/checkout@v4` e `actions/setup-dotnet@v4`, quatro usos. Tag no git é ponteiro, não
conteúdo: quem controla o repositório da action reaponta `v4` sem que nada aqui mude. E o
job `publicar` tem `contents: write` e o token que cria os releases que o VPS instala — é o
caminho mais curto entre uma action comprometida e o servidor.

Correção: fixar no sha completo, com a tag em comentário.

```yaml
uses: actions/checkout@08c6903cd8c0fde910a37f88322edcfb5dd907a8  # v4.2.2
```

---

## SC-07 · MÉDIO · O deploy instala o pacote sem conferir integridade

`deploy.sh` baixa o `padelizou.tar.gz` do release e extrai direto. Vem por HTTPS do GitHub,
então não é o caso de qualquer um no meio do caminho — mas **nada amarra esse tar.gz ao build
que o CI produziu**. Quem tiver `contents: write` no repositório (ou o token do job
`publicar`) troca o asset e o VPS instala.

O rollback automático do `/healthz` não pega isso: um pacote adulterado responde 200 igual.

Correção mais barata: o `ci.yml` anexa um `.sha256` junto do tar.gz e o `deploy.sh` roda
`sha256sum -c` antes do `tar -xzf`. Passo seguinte, se quiser ir além:
`actions/attest-build-provenance`.

---

## Baixo

- **SC-08 — job `testes` sem bloco `permissions:`.** O job `publicar` declara
  `contents: write` (escopo mínimo, correto). O `testes` não declara nada e herda o padrão do
  repositório — e ele roda em `pull_request`, ou seja, faz restore e build de código vindo de
  fora. Pôr `permissions: contents: read` no topo do `ci.yml` resolve; o bloco do `publicar`
  continua sobrescrevendo.
- **SC-09 — xunit 2.9.3 marcado `Legacy`** pelo nuget.org (junto com `xunit.assert`,
  `xunit.core` e as duas `xunit.extensibility.*`), alternativa `xunit.v3`. Só testes, não vai
  pro servidor — mas correção de segurança sai na v3, não aqui. Migração mexe em 4781 testes;
  não é urgente.
- **SC-10 — sem `nuget.config`.** Hoje inofensivo (uma fonte só). Vira dependency confusion
  no dia em que um feed privado entrar: sem package source mapping, o restore aceita o pacote
  de qualquer fonte que responda primeiro.
- **SC-11 — `/tmp/padelizou-$TAG.tar.gz` tem nome previsível** no `deploy.sh`. Roda como root
  num VPS de um operador só, então é teórico — mas `/tmp` é gravável por todos e um symlink
  pré-criado nesse caminho faz o `curl -o` escrever no alvo do link. `mktemp` resolve.
- **SC-12 — desatualizações sem CVE.** Produção: QRCoder 1.6.0→1.8.0, Google.Apis.Auth
  1.75.0→1.76.0, SkiaSharp 4.150.1→4.151.1, EF Tools 10.0.10→10.0.11. Testes:
  coverlet.collector 6.0.4→10.0.1 (4 majors), Test.Sdk 17.14.1→18.9.0,
  xunit.runner.visualstudio 3.1.4→4.0.0. O EF está pinado em 10.0.10 de propósito (o
  comentário no `.csproj` de testes explica) — subir os dois juntos ou nenhum.

---

## O que está bem

Vale registrar, porque auditoria que só lista problema mente por omissão:

- **150/150 pacotes com assinatura de repositório** do nuget.org.
- **141/150 declaram proveniência de código-fonte** (`<repository commit=...>` no nuspec).
- **Nenhuma CVE conhecida** na árvore inteira, com o audit cobrindo transitivas em nível
  `low` — com a ressalva do SC-03.
- **Nenhum `curl | bash`**, nenhum script de instalação de terceiro em CI, deploy ou hooks.
  O `session-start.sh` instala o .NET pelo repositório do Ubuntu, e o comentário explica por
  quê.
- **Publicadores concentrados e conhecidos**: Microsoft (75 pacotes), Humanizer (50),
  xunit (7), Google LLC (4), Npgsql, MailKit/MimeKit (Jeffrey Stedfast). Nenhum publicador
  obscuro com pacote único fora dos já citados.
- **O `deploy.sh` é sólido**: valida o input do workflow contra injeção de shell, usa
  `flock`, tem healthcheck com rollback automático, e o TOFU do `known_hosts` está
  documentado como escolha consciente em vez de acontecer por acidente.
- **O padrão certo já existia no arquivo**: o `EntityFrameworkCore.Tools` estava marcado
  `PrivateAssets=all` desde sempre. O SC-01 foi um pacote que ficou de fora da regra, não uma
  regra que não existia.

---

## O que esta auditoria NÃO conseguiu checar

A política de egresso do ambiente bloqueou três hosts (403). Não contornei:

| host | o que ficou de fora |
|---|---|
| `api.osv.dev` | cruzamento com uma segunda base de advisories |
| `api.github.com/advisories` | ler o texto dos 3 GHSA citados no SC-03 |
| `azuresearch-usnc.nuget.org` | owners e prefixo reservado no nuget.org — o publicador saiu do nuspec assinado em cache |

Ou seja: **a checagem de CVE se apoia numa base só**, a que o audit do NuGet consome. O SC-03
mostra que essa base tem um ponto cego demonstrável. Rodar um `osv-scanner` de uma máquina
com saída livre fecharia essa lacuna.

---

## Reproduzir

```bash
dotnet list Padelizou.slnx package --vulnerable  --include-transitive
dotnet list Padelizou.slnx package --deprecated  --include-transitive
dotnet list Padelizou.slnx package --outdated
dotnet list Padelizou.slnx package --include-transitive --format json

# audit ligado? em que nível?
dotnet msbuild Padelizou/Padelizou.csproj \
  -getProperty:NuGetAudit -getProperty:NuGetAuditMode -getProperty:NuGetAuditLevel

# o que de fato vai pro servidor
dotnet publish Padelizou/Padelizou.csproj -c Release -o /tmp/pub
ls /tmp/pub/*.dll | wc -l && du -sh /tmp/pub/runtimes

# SC-03: o ponto cego, em um restore
#   .csproj com NuGetAudit=true, NuGetAuditMode=all e as duas PackageReference:
#   BouncyCastle.Cryptography 2.2.1  +  Portable.BouncyCastle 1.9.0
dotnet restore
```
