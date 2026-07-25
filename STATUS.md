# Padelizou — Status e Roadmap

> **Documento vivo.** Atualizar ao fim de cada bloco de trabalho: mover itens de "Próximos" para "Feito" e ajustar prioridades.
> Última atualização: **25/07/2026**

---

## Onde estamos

Sistema no ar em **padelizou.com.br** (+ `dev.` para testes e `admin.` para o painel).
Stack: ASP.NET Core 10 · PostgreSQL no VPS · PWA instalável. Deploy por `deploy.sh` / `deploy-dev.sh`.

**Estado:** funcionalmente rico e tecnicamente protegido (git + 85 testes + monitoramento).
**Falta o principal:** ainda roda em *modo demonstração* — cobrança em sandbox e dados fictícios em produção.
Nenhum torneio real passou pelo sistema com dinheiro de verdade.

---

## ✅ Feito

### 25/07/2026 — Fundação de engenharia
- **Git recuperado**: repo estava escondido na subpasta e parado desde 21/07 (199 arquivos sem commit). Movido para a raiz da solução, `publish/` e segredos fora do versionamento, tudo no GitHub.
- **85 testes automatizados** (`Padelizou.Tests`, xUnit + EF InMemory, roda sem banco): rateio Asaas, ranking, nível comprovado, filtro multi-cidade, CPF e o fluxo completo do torneio.
- **2 bugs críticos** encontrados pelos testes e corrigidos: mata-mata nunca disparava num torneio real (conflito de nomes de fase) e os robôs criavam partida sem código obrigatório.
- **Monitoramento**: endpoint `/healthz` (app + banco), vigia no VPS que reinicia sozinho (cron 5 min) e UptimeRobot externo. Validado derrubando o dev de propósito → voltou em 11s.

### 25/07/2026 — Blindagem (Fase 1 quase toda)
- **CI no GitHub Actions**: os 85 testes rodam a cada push; commit com teste vermelho fica marcado com ❌.
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
- **Bug achado de quebra**: o sorteio definia cabeça de chave por `Jogador.PontuacaoGlobal` — campo que o sistema nunca alimentou, mas que tem valores em produção (120 de 145 jogadores, até 995) vindos de SQL manual antigo. Agora usa os pontos reais; campo marcado `[Obsolete]`.

---

## 🔜 Próximos passos, em ordem

### Fase 1 — Terminar a blindagem `~3-5 dias`
- [x] **CI**: GitHub Actions rodando os 85 testes a cada envio ✅ 25/07
- [x] **Deploy a partir do GitHub**, não do disco local ✅ 25/07
- [x] **Rollback em 1 comando** (guardar versão anterior no VPS) ✅ 25/07
- [x] **Backup também dos uploads** (fotos, logos, capas) ✅ 25/07
- [ ] **Ambiente local**: Postgres na máquina pra rodar o site pelo VS `2h`

### Fase 2 — Sair do modo demonstração `~1 semana` ⭐ *maior impacto*
- [ ] **Asaas para produção** (trocar chave + URL, sem mexer no código) `1h` ← *precisa do Felipe*
- [ ] **Limpar dados fictícios**: desligar seed de demo no startup + remover torneios `TEST*` e jogadores CPF `999*` `2h` ← *combinar o momento*
- [x] **Alerta de limite do MEI** (e-mail aos admins em 70% e 90% do teto) ✅ 25/07 💡
- [x] **Métricas de uso** no admin (`/Admin/Metricas`): cadastros, inscrições, pagamentos, série semanal e medidor do MEI ✅ 25/07
- [x] **Lembrete automático de cobrança** (push + e-mail a 6h do vencimento, 1x só) ✅ 25/07
- [x] **Comprovante imprimível + exportar CSV** pro contador ✅ 25/07

### Fase 3 — O dia do torneio `~1-2 semanas`
- [ ] **Comunicado em massa** aos inscritos (1 clique) `1 dia`
- [x] **Notificações nos momentos-chave**: convite, inscrição confirmada e resultado ✅ 25/07
- [ ] Faltam 2 momentos: **chaves publicadas** e **seu jogo é o próximo** `4h`
- [ ] **Convite pra se cadastrar na tela ao vivo** (maior porta de entrada desperdiçada) `4h` 💡
- [ ] **Check-in de duplas** por QR code `1 dia`
- [ ] **Aviso de quadra atrasada** (o sistema já sabe previsto × real) `1 dia` 💡
- [ ] **Placar que funciona sem internet** (sincroniza depois) `2 dias` 💡
- [ ] **Relatório pós-torneio em PDF** (resultados + público + financeiro) `1 dia`

### Fase 4 — Clube `~1-2 semanas`
- [ ] **Mapa de ocupação semanal** `2 dias`
- [ ] **Horário fixo / mensalista** `2 dias`
- [ ] **Bloquear horário** (manutenção, evento, aula) `4h`
- [ ] **Política de cancelamento e falta** `1 dia`

### Fase 5 — Professor `~1 semana`
- [ ] **Presença e falta do aluno** `1 dia`
- [ ] **Avaliação pelos alunos** `1 dia`
- [ ] **Página pública do professor** (vende aula, traz gente nova) `2 dias` 💡

### Fase 6 — Crescimento `contínuo`
- [ ] **Convidar parceiro sem ele ter conta** (hoje exige CPF na hora — maior atrito) `2 dias` 💡
- [ ] **Resumo do torneio pronto pro Instagram** `2 dias` 💡
- [ ] **Tela inicial conforme o papel** `1 dia`
- [ ] **Primeiros passos guiados** (+ ensinar instalação no iPhone) `1 dia`
- [ ] **Play Store** via empacotamento do PWA `1 dia`

💡 = ideia que não estava no diagnóstico original

---

## 🔧 Metades a fechar (pequenas)
- [ ] Financeiro **por categoria** no torneio (hoje só por torneio)
- [ ] Financeiro **por quadra** no clube (hoje só o total)
- [ ] Resto do **código morto**: entidade `Organizador`, telas órfãs (`RankingPorTorneio`, `RankingCategorias`, CRUD antigo de Jogadores), método `GerarFaseGrupos` sem botão
- [ ] Push de **nova solicitação de aula** pro professor (hoje só e-mail)

## 📋 Backlog consciente (fazer depois)
- Banners/avisos da plataforma
- Fila de denúncias de comentários
- **Exclusão de conta pelo usuário** — ⚠️ vira obrigação legal (LGPD) quando a base crescer
- Quebrar os controllers gigantes (`TorneiosController`)

---

## 🔒 Regras para não regredir
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
