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

### 25/07/2026 — Produto
- **Mata-mata genérico** (`Services/ChaveamentoMataMata`): funciona com qualquer nº de grupos (antes só 1/2/4/8). Melhores 2ºs completam o quadro; categoria de 1 grupo agora também coroa campeão.
- **Painel financeiro** (`Pagamentos/Meus`): filtro por período, cards de recebido/a receber/taxa/estornado, "de onde veio" por torneio e "quem está devendo" com link de cobrança. **Serve organizador, professor e clube na mesma tela.**
- **Ranking**: categoria prevista movida para o perfil, busca dentro do ranking, dropdown de categorias, ranking por torneio embutido, coluna de vitórias, filtro de período e filtro por estado + **várias cidades**.
- **Fim do "Ranking: 0 pts"**: perfil mostra pontos reais somados dos torneios (3 telas corrigidas).
- **PWA**: ícone de iPhone + maskable e atalhos de app (Agenda, Torneios, Ranking, Marcar jogo).

---

## 🔜 Próximos passos, em ordem

### Fase 1 — Terminar a blindagem `~3-5 dias`
- [ ] **CI**: GitHub Actions rodando os 85 testes a cada envio `2h`
- [ ] **Deploy a partir do GitHub**, não do disco local `3h`
- [ ] **Rollback em 1 comando** (guardar versão anterior no VPS) `1h`
- [ ] **Backup também dos uploads** (fotos, logos, capas) `30min`
- [ ] **Ambiente local**: Postgres na máquina pra rodar o site pelo VS `2h`

### Fase 2 — Sair do modo demonstração `~1 semana` ⭐ *maior impacto*
- [ ] **Asaas para produção** (trocar chave + URL, sem mexer no código) `1h`
- [ ] **Limpar dados fictícios**: desligar seed de demo no startup + remover torneios `TEST*` e jogadores CPF `999*` `2h`
- [ ] **Alerta de limite do MEI** (avisar em 70% e 90% do teto anual) `3h` 💡
- [ ] **Métricas de uso** no admin: cadastros/semana, inscrições, pagamentos `1 dia`
- [ ] **Lembrete automático de cobrança** antes do vencimento `4h`
- [ ] **Comprovante + exportar CSV** pro contador `4h`

### Fase 3 — O dia do torneio `~1-2 semanas`
- [ ] **Comunicado em massa** aos inscritos (1 clique) `1 dia`
- [ ] **Notificações nos momentos-chave**: inscrição confirmada, chaves publicadas, seu jogo é o próximo, resultado `1 dia`
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
- Gráfico de evolução do jogador (pontos por mês)
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
