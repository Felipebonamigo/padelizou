# Padelizou — Status e Roadmap

> **Documento vivo.** Atualizar ao fim de cada bloco de trabalho: mover itens de "Próximos" para "Feito" e ajustar prioridades.
> Última atualização: **26/07/2026** — limpeza do código morto (build-28) + auditoria do plano contra o código.

---

## Onde estamos

Sistema no ar em **padelizou.com.br** (+ `dev.` para testes e `admin.` para o painel).
Stack: ASP.NET Core 10 · PostgreSQL no VPS · PWA instalável. Deploy por `deploy.sh` / `deploy-dev.sh`.

**Estado:** funcionalmente rico e tecnicamente protegido (git + **142 testes** + CI + monitoramento + rollback em 1 comando + varredura de autorização feita).
As áreas de **professor, clube e organizador estão completas**; a entrada se adapta ao papel de quem entra.

**Falta o principal:** ainda roda em *modo demonstração* — cobrança em sandbox e dados fictícios em produção.
Nenhum torneio real passou pelo sistema com dinheiro de verdade. **É o único bloqueio que separa o sistema de virar negócio.**

---

## ✅ Feito

### 25/07/2026 — Fundação de engenharia
- **Git recuperado**: repo estava escondido na subpasta e parado desde 21/07 (199 arquivos sem commit). Movido para a raiz da solução, `publish/` e segredos fora do versionamento, tudo no GitHub.
- **85 testes automatizados** (`Padelizou.Tests`, xUnit + EF InMemory, roda sem banco): rateio Asaas, ranking, nível comprovado, filtro multi-cidade, CPF e o fluxo completo do torneio.
- **2 bugs críticos** encontrados pelos testes e corrigidos: mata-mata nunca disparava num torneio real (conflito de nomes de fase) e os robôs criavam partida sem código obrigatório.
- **Monitoramento**: endpoint `/healthz` (app + banco), vigia no VPS que reinicia sozinho (cron 5 min) e UptimeRobot externo. Validado derrubando o dev de propósito → voltou em 11s.

### 25/07/2026 — Blindagem (Fase 1 quase toda)
- **CI no GitHub Actions**: a suíte inteira roda a cada push; commit com teste vermelho fica marcado com ❌.
- **Deploy via GitHub com versões**: o CI gera o pacote (`build-N-sha`) só se os testes passarem — é *impossível* publicar código reprovado. O VPS baixa, guarda cada versão em `/opt/padelizou-releases/`, troca por symlink e confere o `/healthz`; se não responder, **volta sozinho**. `deploy.sh`/`deploy-dev.sh` locais agora recusam mudanças não commitadas (fim da colisão de sessões).
- **Rollback em 1 comando**: `ssh root@VPS /opt/padelizou-deploy/rollback.sh <prod|dev>`.
- **Dados persistentes fora das versões**: uploads, tokens do Google e `appsettings.json` vivem em `/opt/padelizou-shared/` — trocar de versão nunca apaga foto de ninguém.
- **Backup ampliado**: além do banco, o cron das 4h agora copia uploads + tokens + configs (14 dias de histórico).
- *Limpeza pendente:* apagar `/opt/padelizou-legado` e `/opt/padelizou-dev-legado` (cópias de emergência da migração) depois de alguns dias.

### 25/07/2026 — Produto
- **Mata-mata genérico** (`Services/ChaveamentoMataMata`): funciona com qualquer nº de grupos (antes só 1/2/4/8). Melhores 2ºs completam o quadro; categoria de 1 grupo agora também coroa campeão.
- **Painel financeiro** (`Pagamentos/Meus`): filtro por período, cards de recebido/a receber/taxa/estornado, "de onde veio" por torneio e "quem está devendo" com link de cobrança. **Serve organizador, professor e clube na mesma tela.**
- **Ranking**: categoria prevista movida para o perfil, busca dentro do ranking, dropdown de categorias, ranking por torneio embutido, coluna de vitórias, filtro de período e filtro por estado + **várias cidades**.
- **Fim do "Ranking: 0 pts"**: perfil mostra pontos reais somados dos torneios (3 telas corrigidas).
- **PWA**: ícone de iPhone + maskable e atalhos de app (Agenda, Torneios, Ranking, Marcar jogo).
- **Fase 2 (parte de código)**: métricas de uso no admin com medidor do MEI, alerta de 70/90% por e-mail, lembrete automático de cobrança e comprovante + CSV. Colunas `CriadoEm` novas (registro antigo = sem data). Testado no dev (lembrete disparou de verdade) e **publicado em produção** (build-5).
- **Área do jogador**: gráfico de evolução (pontos por mês + acumulado, SVG sem biblioteca), push nos momentos-chave (convite de grupo, inscrição confirmada, resultado — seguidor só no mata-mata pra não virar spam) e onboarding de 5 passos que some quando concluído. **98 testes**. Publicado em produção (build-9).
- **Nova página inicial**: deixou de ser vitrine de torneio e virou o mapa da plataforma — acontecendo agora, 6 portas de entrada (jogo, torneios, aulas, ranking, grupos, quadra), inscrições abertas, números da comunidade e faixa organizador/professor/clube. Aba mostra só **"Padelizou"**. Publicado em produção (build-13).
- **Home logada personalizada** ("hoje no seu padel"): visitante vê o mapa; logado vê onboarding, **seu próximo jogo em destaque (hora, quadra, adversários — usa `HorarioPrevisto`)**, próximos compromissos (aula/quadra), seus torneios com badge de lista de espera, e torneio próprio não repete na vitrine. 102 testes. Publicado em produção (build-15).
- **2 fixes de quebra na home**: torneio `Oculto` aparecia na vitrine; e 53 views duplicavam o título ("Entrar - Padelizou - Padelizou") — resolvido no `_Layout`.
- **Aba Times**: vitrine dos times (logo, membros, pontos) + página com quem veste cada camisa, dono destacado. O time já existia como entidade, faltava a tela. Publicado (build-17).
- **Busca de jogadores com filtros** (`/Jogadores/Buscar`): nome + categoria + estado/cidade + clube, combináveis, com chips removíveis um a um. Quem **declarou** a preferência sobe com selo "combina"; quem não declarou entra igual (nessas tabelas "sem linha" = "aceito qualquer um"). Ligada na página de Times e na home. **116 testes.** Publicado (build-20).
- **Uma categoria por jogador (opcional)**: `Torneio.PermiteMultiplasCategorias`, escolhido na criação. Vale pra dupla E americano. Migração sobe com **default TRUE** — antes não havia trava, e `false` mudaria a regra dos 14 torneios que já existem.
- **Inscrição sem parceiro**: `Dupla.Jogador2Id` virou anulável. O jogador garante a vaga sozinho e define o parceiro depois; qualquer integrante (ou o organizador) troca enquanto as inscrições estão abertas, com push pra quem sai e pra quem entra. **109 testes.**
- **2º fix de quebra**: `GerarChaves` não filtrava `EmListaDeEspera` — o modelo dizia que lista de espera fica fora das chaves, mas o sorteio incluía todo mundo. Agora só entra dupla completa e confirmada.
- **Limpeza do código morto** (26/07, build-28): ~800 linhas removidas — CRUD scaffolded de Jogadores, `RankingCategorias`, `RankingPorTorneio`, `GerarFaseGrupos` e a entidade `Organizador`. **Fechou de quebra uma porta aberta:** as ações do CRUD não tinham `[Authorize]` e `/Jogadores/Delete/5` apagava jogador.
- **Varredura de autorização nos 21 controllers** (26/07, build-31). Duas camadas: ações sem `[Authorize]` (20 achados, 19 páginas públicas legítimas) e IDOR — ação que recebe id e grava sem checar dono (34 candidatos, **todos seguros**, usam filtro de dono na própria consulta). Um achado real: `Clubes/Criar` era `[AllowAnonymous]` e criava clube sem validação nem limite. Corrigido, com deduplicação por nome de quebra.
- **Ambiente local + limpeza do VPS** (26/07): PostgreSQL 17 na máquina e 184 MB de legado apagados.
- **Segurança: só o organizador mexe no placar** (26/07, build-29). Auditando autorização depois da limpeza, achei que `ControlePlacar` (GET e POST) não exigia login nem checava organizador — qualquer um que alcançasse a rota mudava o placar de qualquer jogo, inclusive ao vivo. Corrigido nos dois verbos, com **4 testes de regressão** (139 no total).
- **Bug achado de quebra**: o sorteio definia cabeça de chave por `Jogador.PontuacaoGlobal` — campo que o sistema nunca alimentou, mas que tem valores em produção (120 de 145 jogadores, até 995) vindos de SQL manual antigo. Agora usa os pontos reais; campo marcado `[Obsolete]`.

---

## 🎯 O que realmente falta (auditado em 26/07)

Das 6 fases originais, **4 estão fechadas**. Sobrou pouco, e o que sobrou está em 3 grupos:

| | O quê | Quem faz |
|---|---|---|
| 🔴 **Bloqueia o negócio** | Asaas para produção · limpar dados fictícios | **Felipe decide** |
| 🟡 **Fecha pendências** | ~~Código morto~~ ✅ · ~~184 MB no VPS~~ ✅ · ~~Postgres local~~ ✅ · ~~varredura de autorização~~ ✅ | **fechado** |
| 🟢 **Cresce depois** | 2 pushes do dia de jogo · quadra atrasada · placar offline · convite sem CPF · arte pro Instagram · Play Store | sem pressa |

**Nada do que sobrou impede um torneio real de acontecer amanhã.** O único impedimento é a chave do Asaas.

---

## 🔜 Próximos passos, em ordem

### Fase 1 — Terminar a blindagem `~3-5 dias`
- [x] **CI**: GitHub Actions rodando os 85 testes a cada envio ✅ 25/07
- [x] **Deploy a partir do GitHub**, não do disco local ✅ 25/07
- [x] **Rollback em 1 comando** (guardar versão anterior no VPS) ✅ 25/07
- [x] **Backup também dos uploads** (fotos, logos, capas) ✅ 25/07
- [x] **Ambiente local**: PostgreSQL 17 na máquina, `db_padel_local`, app em `localhost:5199` ✅ 26/07 — ver [AMBIENTE-LOCAL.md](AMBIENTE-LOCAL.md). Nunca rodava porque o `appsettings.json` ainda apontava pro SQL Server de antes da migração.

### Fase 2 — Sair do modo demonstração `~1 semana` ⭐ *maior impacto*
- [ ] **Asaas para produção** (trocar chave + URL, sem mexer no código) `1h` ← *precisa do Felipe*
- [ ] **Limpar dados fictícios**: desligar seed de demo no startup + remover torneios `TEST*` e jogadores CPF `999*` `2h` ← *combinar o momento*
- [x] **Alerta de limite do MEI** (e-mail aos admins em 70% e 90% do teto) ✅ 25/07 💡
- [x] **Métricas de uso** no admin (`/Admin/Metricas`): cadastros, inscrições, pagamentos, série semanal e medidor do MEI ✅ 25/07
- [x] **Lembrete automático de cobrança** (push + e-mail a 6h do vencimento, 1x só) ✅ 25/07
- [x] **Comprovante imprimível + exportar CSV** pro contador ✅ 25/07

### Fase 3 — O dia do torneio `quase toda feita`
- [x] **Comunicado em massa** aos inscritos (1 clique, por categoria ou geral) ✅ 25/07 (build-25)
- [x] **Notificações nos momentos-chave**: convite, inscrição confirmada e resultado ✅ 25/07
- [x] **Convite pra se cadastrar na tela ao vivo** ✅ 25/07 (build-25)
- [x] **Check-in de duplas** (lista de presença por categoria) ✅ 25/07 (build-25)
- [x] **Relatório pós-torneio** (pódio + público + financeiro, imprime em PDF) ✅ 25/07 (build-25)
- [x] **Financeiro do torneio por categoria** ✅ 25/07 (build-25)
- [ ] Faltam 2 momentos de push: **chaves publicadas** e **seu jogo é o próximo** `4h`
- [ ] **Aviso de quadra atrasada** (o sistema já sabe previsto × real) `1 dia` 💡
- [ ] **Placar que funciona sem internet** (sincroniza depois) `2 dias` 💡

### Fase 4 — Clube `feita`
- [x] **Mapa de ocupação semanal** (grade quadra × dia × hora, % e receita) ✅ 25/07 (build-25)
- [x] **Horário fixo / mensalista** (gera N semanas, pula conflito) ✅ 25/07 (build-25)
- [x] **Bloquear horário** (manutenção, evento, aula) ✅ 25/07 (build-25)
- [x] **Política de cancelamento e no-show** ✅ 25/07 (build-25)
- [x] **Financeiro do clube** por quadra e por dia da semana ✅ 25/07 (build-25)

### Fase 5 — Professor `feita`
- [x] **"Meu dia" na entrada + push de nova solicitação** ✅ 25/07 (build-22)
- [x] **Visão financeira**: entrou no mês, quem deve, previsão, resultado por local ✅ 25/07 (build-24)
- [x] **Presença e falta do aluno** + política de cancelamento ✅ 25/07 (build-24)
- [x] **Avaliação pelos alunos** (só quem teve aula) ✅ 25/07 (build-24)
- [x] **Página pública do professor** + vitrine `/Professores` ✅ 25/07 (build-24)

### 🎭 Entrada por papel `feito 25/07`
- [x] Home reconhece professor / organizador / dono de clube e empilha os painéis de quem acumula papéis (build-22)

### Fase 6 — Crescimento `contínuo`
- [x] **Tela inicial conforme o papel** ✅ 25/07 (build-22)
- [x] **Primeiros passos guiados** (onboarding de 5 passos, inclui instalação no iPhone) ✅ 25/07 (build-9)
- [ ] **Convidar parceiro sem ele ter conta** `2 dias` 💡
      *Meio caminho já feito:* dá pra se inscrever sem parceiro e definir depois (build-17).
      Falta o convite por link/WhatsApp que dispensa digitar o CPF do outro.
- [ ] **Resumo do torneio pronto pro Instagram** `2 dias` 💡 — o relatório pós-torneio (build-25) já reúne os dados; falta a arte
- [ ] **Play Store** via empacotamento do PWA `1 dia`

💡 = ideia que não estava no diagnóstico original

---

## 🔧 Metades a fechar (pequenas)
- [x] Financeiro **por categoria** no torneio ✅ 25/07 (build-25)
- [x] Financeiro **por quadra** no clube ✅ 25/07 (build-25)
- [x] Push de **nova solicitação de aula** pro professor ✅ 25/07
- [x] **Código morto removido** ✅ 26/07 (build-28) — CRUD scaffolded de Jogadores (9 ações + 5 views), `RankingCategorias`, `RankingPorTorneio`, `GerarFaseGrupos` (77 linhas) e a entidade `Organizador` (tabela vazia, dropada por migração). **~800 linhas a menos.**
      ⚠️ **Achado de segurança no caminho:** nenhuma ação do CRUD tinha `[Authorize]` — `/Jogadores/Delete/5` apagava jogador. O gate de Acesso Antecipado barrava anônimo, mas qualquer usuário logado alcançava, e ficaria aberto ao mundo no dia em que o gate saísse. Fechado.
- [ ] **Apagar `/opt/padelizou-legado` e `/opt/padelizou-dev-legado` no VPS** — cópias de emergência da migração de deploy, ocupando **184 MB**. Já passou tempo suficiente; podem ir `5min`

## 📋 Backlog consciente (fazer depois)
- Banners/avisos da plataforma
- Fila de denúncias de comentários
- **Exclusão de conta pelo usuário** — ⚠️ vira obrigação legal (LGPD) quando a base crescer
- Quebrar os controllers gigantes (`TorneiosController`)

---

## 🔒 Regras para não regredir
0. **Ação que grava dado precisa de `[Authorize]` E de checagem de dono/organizador.**
   Dois buracos em 26/07 vieram da falta disso. O gate de Acesso Antecipado *não* é
   autorização — ele some no dia em que o sistema abrir pro público.
1. **Todo defeito corrigido vira teste.**
2. **Nada é publicado com teste vermelho.**
3. **Testar em `dev` antes de produção.**
4. **Fechou um trabalho, commit + push.**
5. **Uma coisa de cada vez, até o fim.**

---

## 📎 Documentos de apoio
Gerados em 25/07/2026, salvos também em PDF na Área de Trabalho:
- **Análise do sistema** — diagnóstico completo por área
- **Plano de evolução** — as 6 fases detalhadas com justificativa
- **Inventário de melhorias** — as 41 melhorias com status individual

> ⚠️ Os 3 documentos refletem o diagnóstico de **25/07 de manhã** e envelheceram: a maior
> parte do que eles listam como "falta" já foi entregue. Este STATUS.md é a fonte da verdade.
