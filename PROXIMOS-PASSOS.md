# Próximos passos — a lista da análise de 13/11/2026

🗣️ Felipe, 13/11/2026: *"Faça uma análise do nosso sistema e hospedagem, veja se está tudo certinho
e pense o que mais podemos adicionar ou melhorar, principalmente em torneios e ranking"* — e depois:
*"deixa a lista que você me mandou anotada para vermos o que iremos seguir depois"*.

Esta é a lista. **Atualize aqui quando um item fechar** (marque `[x]`, com PR e build), e
acrescente o que aparecer. Cada item diz onde está a evidência, pra próxima sessão não refazer a
descoberta. O que já estava certo na hospedagem está no fim, pra ninguém "consertar" o que funciona.

P / M / G = esforço pequeno / médio / grande. ⚖️ = mexe em dinheiro, régua de autorização,
migration ou contrato de API → **architectural** pelo `CLAUDE.md` (desenho aprovado antes do código).

---

## 🏃 Em andamento (Felipe mandou fazer em 13/11)

Ordem combinada: 1 → 2 → 6 → 3 → 4, uma de cada vez, até o fim.

- [x] **1. "Adicionar horário" perto da meia-noite** (defeito do #415). ✅ #417, `build-1641-b0848eb` em dev e prod. Somar a duração ao limite
      passava de 24h: `HorarioDoDia.Escrever` gravava 24h10 como `00:10`, o `Ler` descartava a faixa e
      o dia perdia o horário próprio, com "sucesso" na tela; esticando no meio, o jogo ia pra 00:20
      do dia seguinte. → recusar com explicação; `Escrever` estoura em vez de gravar torto.
- [ ] **2. Backup honesto + cópia do banco no deploy + fuso no `/healthz`.** ✅ #418, `build-1643-bc44326` em dev e prod — o `/healthz` com a conferência de fuso respondeu `ok` nos dois (o dev, que nunca tinha sido conferido, está em São Paulo). ⏳ **Falta o Felipe instalar os dois scripts no VPS** (`infra/vps/README.md`, "Instalar a versão de 13/11/2026").
  - `infra/vps/backup-offsite.sh` grava o carimbo do vigia mesmo sem dump novo (o `rclone copy`
    passa sem nada pra copiar) e o `rclone delete --min-age 30d` vai esvaziando o B2 se o
    `pg_dump` parar. → só carimbar (e só apagar histórico) com dump de < 26h, tamanho mínimo e
    `gunzip -t` ok.
  - `infra/vps/deploy.sh` reinicia sem copiar o banco; a migration roda no startup e o rollback só
    volta o binário. 26 das 168 migrations apagam coluna/tabela ou fazem UPDATE/DELETE no `Up()`.
    → `pg_dump` antes do `systemctl restart`; sem cópia, sem deploy.
  - Nada no código confere o fuso (o app depende do VPS em America/Sao_Paulo). → `/healthz` 503
    fora de São Paulo, e o `deploy.sh` faz rollback sozinho.
  - ⚠️ Os dois scripts moram no VPS: o merge não os instala. O Felipe copia (passo a passo no PR).
- [ ] **6. Previsão do mata-mata igual à chave real** ⚖️ (Felipe aprovou corrigir em 13/11). 🔀 #419 no `main`, publicando.
      `PrevisaoDoTorneio.MataMata` ainda corta pro MAIOR quadro que cabe; a chave real
      (`ChaveamentoMataMata`) leva todo mundo com bye. 3 grupos: prevê 3 jogos, são 5; 6 grupos:
      prevê 7, são 11. Pesa no "não cabe?" antes do sorteio e no preço do pacote de registro de
      resultados (R$ 12 × jogos previstos, `RegistroDeResultados.JogosPrevistos`), que sai a menos.
      `PrevisaoDoTorneioTests` trava o valor errado.
- [ ] **3. Defeitos do ranking + varredura do Padelímetro.** 🔧 Parte 1 (o 500, a âncora e a mista) em PR; falta a varredura e o `RANKING.md`.
  - `/Jogadores/Ranking?clubeId=1` → **500 em produção** (conferido no site): pede a view
    `RankingPorClube`, que nunca existiu (`JogadorsController.cs` ~809).
  - Rótulo do Padelímetro: a categoria de referência (`FaixasDePadelimetro.CategoriaDeReferencia`,
    consultas em `PadelimetroService` ~354, `JogadorsController` ~81, `CartoesController` ~251) só
    exclui mista e data futura — Americano, torneio restrito/time exclusivo, lista de espera,
    inscrição sem parceiro e torneio cancelado contam. Em calibração (quase todo mundo) é ela que
    dá o nome. Mesmo defeito do #397 por outra porta.
  - Quem só jogou mista recebe rótulo da escada MASCULINA no perfil e no card (`FaixaExibida` com
    categoria nula → `EhFeminina(null)` = falso).
  - O Padelímetro roda DEPOIS de o placar ser gravado (`EncerramentoDaPartida`, em try/catch): um
    restart no meio deixa o jogo fora do número pra sempre. → varredura no molde do
    `VarreduraDaChave`.
- [ ] **4. Avisar o jogador quando o horário do jogo muda** (hoje nenhuma troca avisa ninguém —
      `TrocarHorario`, `DefinirHorario`, setas, `InserirHorario`, Aplicar do Planejamento).

---

## 🐞 Defeitos achados que ficaram pra depois

- [ ] **Categoria com 49+ duplas quebra o mata-mata** (P): 17 grupos → 34 classificados → quadro de
      64; `ChaveamentoMataMata.NomeFase(64)` cai em "Final" (2 jogos, 30 byes, sem fase seguinte).
      Falta a trava que a chave direta tem (`ProblemaNaChaveDireta`, 32).
- [ ] **`ObterPlacaresAoVivo`** (`TorneiosController.Placar.cs` ~607): nenhuma tela chama, não exige
      login e não respeita torneio oculto. Apagar (P).
- [ ] **Lista de espera com `LimiteDuplasTotal`**: vaga aberta na categoria A não chama a fila da B
      (lido no código, não testado). A promoção da fila tem 2 cópias e a lotação, 3, que já divergem
      (`DuplasController` ~1984, `PagamentoInscricaoService` ~1734, `InscreverIndividual`).
- [ ] **`RANKING.md` desatualizado em 7 pontos** (correção de placar reaplica o Elo; W.O. formal
      existe; "único lugar que finaliza" são 3; o gráfico de PDZ não existe; o "sem Padelímetro —
      jogue um torneio" não é desenhado; a Trilha C está no ar; exemplos com K antigo). Corrigir
      junto do item 3.

---

## 🖥️ Hospedagem e operação

- [ ] 🟡 **Trava do prod desligada** — o environment `prod` não pede aprovação (anotado no STATUS
      desde agosto). **Decisão do Felipe**: ligar em Settings → Environments → prod → Required
      reviewers, ou assumir que fica assim e corrigir o `infra/vps/README.md` §3.
- [ ] 🟡 **O dev recebe dado pessoal completo**: `copiar-torneio.sh` faz `SELECT *` de `Jogador`
      (CPF, e-mail, celular, endereço, `SenhaHash`, `TokenRecuperacao`, flags de admin). Proteção
      do dev: só o portão de acesso antecipado + noindex; o bloco do dev no Caddy não tem
      `basic_auth`, e a senha de EXEMPLO do portão está no `padelizou.service` (repo público —
      conferir se a do dev é outra). → anonimizar na cópia + `basic_auth` no Caddy do dev (M).
- [ ] 🟡 **Cota de e-mail esgotável por anônimo**: `AuthController.ReportarProblema` é POST sem login
      e sem limite, e cada envio manda e-mail; o Resend grátis dá 100/dia, a mesma cota da
      recuperação de senha. `EsqueciSenha` não tem intervalo por conta. Login sem limite por IP (só a
      trava por conta). → limitar os dois (P).
- [ ] 🟡 **Erros dos serviços de fundo** (23 `BackgroundService`: pagamento expirado, conciliação do
      Pix, lembretes) só vão pro journald; não chegam ao `/Admin/Erros`. → mandar os `catch` pro
      `RegistroDeErros` (P/M).
- [ ] 🟡 **Restauração não ensaiada desde 07–08/08/2026** (~98 migrations depois). → ensaio
      cronometrado e runbook versionado. A config do servidor (`config-servidor_*.tar.gz`: Caddyfile
      dos 5 sites, drop-ins, crons) não sai do VPS no backup de fora; a chave do B2 pode apagar e
      mora no mesmo VPS → Object Lock ou chave sem delete.
- [ ] 🟢 **Releases**: o CI guarda só os 20 últimos (~3 dias); o README diz "todos seguem guardados".
- [ ] 🟢 **Manutenção**: `actions/checkout@v4` e `setup-dotnet@v4` rodam em Node 20 depreciado (subir
      pra v5 fixando por SHA); patches EF Core 10.0.15, SkiaSharp 4.150.7, Pkcs 10.0.15; pendentes da
      auditoria de agosto (`SUPPLY-CHAIN.md`): lockfile (SC-05), actions por SHA (SC-06), sha256 do
      pacote no deploy (SC-07), `permissions` no job de testes (SC-08).
- [ ] 🟢 **Testes aleatórios com teto fixo** (`GradeDoErMedidaTests` caiu no CI em 11/11 com 42 > 40;
      `DescansoNaFaseDeGruposTests` tem o mesmo padrão): o sorteio usa `Guid.NewGuid()` sem semente
      (`TorneiosController.Chaves` ~101/163, `CabecasDeChave` ~23). → `Random` com semente injetável.
- [ ] 🟢 **Chave de deploy = root irrestrito** no VPS dos 5 negócios → `command=` forçado no
      `authorized_keys`.
- [ ] **Com o Felipe**: confirmar se o UptimeRobot ainda está ativo e pra quem alerta; Play Store
      parada no `assetlinks.json` (404, falta a impressão digital); DMARC em `p=none` desde agosto;
      PRs parados (#381 e #330 só de STATUS; #326 deploy com repo privado; #353 campanha dos times;
      #270 desenho da ProSmart).

---

## 🏆 Torneios — o que adicionar

1. [ ] **Lembrete do jogo na véspera** (P/M) — o par do item 4. Reaproveita o padrão do
       `LembreteDaAulaBackgroundService`.
2. [ ] **"Dupla abandonou o torneio"** (P) — W.O. em lote nos jogos que faltam (`EncerramentoPorWo`).
3. [ ] **Ranking do circuito** (P) — a soma das etapas já existe
       (`EstatisticasService.ObterRankingDoTorneioAsync(lista)`, `Torneio.TorneioOrigemId`); falta a tela.
4. [ ] **Modo telão** (M) — quadras ao vivo e próximos jogos numa tela pro clube; alivia o dia de jogo
       (hoje `jogos-ao-vivo-atualiza.js` rebaixa a página inteira a cada 20 s).
5. [ ] **Grupos de 4** (M) — 3 jogos garantidos por dupla. `Torneio.TamanhoGrupo` existe e ninguém lê.
6. [ ] **Chave de consolação "Prata"** (G) ⚖️ — mexe nos pontos do ranking.
7. [ ] **Vaga presa durante o pagamento + regra única de lotação** (M) ⚖️ — hoje quem paga pode cair
       na lista de espera e virar estorno (`PagamentoInscricaoService` ~1558).
8. [ ] **Desempate configurável, com o ranking congelado no sorteio** (M) ⚖️ migration — hoje o
       desempate usa o ranking ao vivo (`ClassificacaoDeGrupos` ~160).
9. [ ] **Disputa de 3º lugar** (P/M).
10. [ ] **Sorteio com semente** (P, técnico) — ver "Testes aleatórios" acima.

## 📈 Ranking — o que adicionar

1. [ ] **Push depois de cada jogo com a variação e o porquê** (P): "Vitória 6x2: +8 (671). Vocês
       tinham 43% de chance." Reaproveita `EncerramentoDaPartida.NotificarResultadoAsync` e o extrato.
2. [ ] **Gráfico do Padelímetro e extrato completo no perfil** (P) — a espec promete; hoje são 8 linhas.
3. [ ] **Melhores do Ano 2026** (P na aba "este ano"; M com foto congelada ⚖️ migration) — **prazo
       31/12**. Decisão do Felipe: "ano" é recorte de tela ou os pontos passam a vencer?
4. [ ] **Ranking da casa (clube)** (P/M) — `ListarRankingAsync(filtroJogadores)` e
       `ObterRankingDoTorneioAsync` já existem; é argumento de venda pro clube.
5. [ ] **Número confiável** (P) — marcar quem está em calibração e quem não joga há 12 meses ("último
       jogo há X"). Hoje um estreante na Open nasce com 900 e lidera com 1 jogo. Sem decaimento.
6. [ ] **Probabilidade antes do jogo** (P), só com os 4 fora da calibração; combina com os palpites.
7. [ ] **Painel de anomalias no admin** (P/M) — categoria com < 3 duplas, torneio oculto ou não
       aprovado mexendo no PDZ (hoje `Conta` ignora `Oculto`/`AprovadoEm`), salto > 60 num fim de
       semana, placar corrigido.
8. [ ] **Avisar o organizador na inscrição** de jogador acima da faixa (M) — `FaixaExibida`,
       `SomaDaDupla` e `LinhaDeSubida` prontos e sem uso.
9. [ ] **Benefício do líder** (M/G) ⚖️ dinheiro (`RANKING.md`, Trilha B).

---

## ✅ O que estava certo (13/11/2026) — não precisa mexer

- Site, `www` (301), `http` (308), `dev` (portão + `noindex`) e `admin.` respondendo; HTTPS Let's
  Encrypt válido até 18/12/2026, renovação automática do Caddy; HTTP/2 e HTTP/3.
- `/healthz` testa banco e migration pendente; deploy por release do CI, com rollback automático;
  `VPS_KNOWN_HOSTS` definido (chave do servidor fixada).
- CI: 28 de 30 builds do `main` verdes de primeira (26/10 → 12/11); 8.134 testes em ~2 min; audit de
  pacotes sem vulnerabilidade conhecida.
- Três backups por dia + `VigiaDoBackupBackgroundService`; e-mail no domínio próprio (SPF/DKIM/DMARC)
  com vigia de cota; limite por IP em recuperação, cadastro, CEP e API; webhook do Asaas validado com
  `FixedTimeEquals`; `GateDeAutorizacaoDosPostsTests` cobrindo todo POST.
- Ranking público: 979 linhas, 54 KB comprimido — leve por enquanto (paginar quando passar de alguns
  milhares).
