# Padelizou — Status e Roadmap

> **Documento vivo.** Atualizar ao fim de cada bloco de trabalho: mover itens de "Próximos" para "Feito" e ajustar prioridades.
> Última atualização: **27/07/2026** — saiu do modo demonstração (primeiro pagamento real recebido, produção limpa), passou no **ensaio geral** de ponta a ponta (build-58), recebeu os **44 times reais do ranking do "Quanto Tá"** com bandeira e ganhou o **calendário da agenda do professor** (build-65).

---

## Onde estamos

Sistema no ar em **padelizou.com.br** (+ `dev.` para testes e `admin.` para o painel).
Stack: ASP.NET Core 10 · PostgreSQL no VPS · PWA instalável. Deploy por `deploy.sh` / `deploy-dev.sh`.

**Estado:** funcionalmente rico e tecnicamente protegido (git + **380 testes** + CI + monitoramento + rollback em 1 comando + varredura de autorização feita).
As áreas de **professor, clube e organizador estão completas**; a entrada se adapta ao papel de quem entra.

**Saiu do modo demonstração em 27/07:** Asaas de produção ligado, **primeiro pagamento real recebido** (R$ 9,00) e produção limpa dos dados fictícios.
**Falta agora:** o primeiro torneio de verdade rodar de ponta a ponta, e a conta bancária do Asaas sair de `PENDING` (trava Pix e saque).

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
- **Apelido + busca sem caixa** (26/07, build-35): `Jogador.Apelido` opcional, e `Services/BuscaJogador` virou a única autoridade de busca — aceita nome, apelido ou **CPF completo** (parcial não procura), tudo `ToLower` dos dois lados porque `LIKE` no PostgreSQL diferencia maiúscula. Entrada passou a aceitar e-mail **ou** login, também sem caixa.
- **Máscaras de documento** (26/07, build-36): `data-mascara` em `mascaras.js` ganhou CNPJ e o modo `documento` (troca sozinho por tamanho). Auditados os 44 campos de texto — só 2 estavam sem máscara, e um deles tinha `maxlength="11"`, que cortaria o CPF formatado no meio.
- **Prod e dev viraram ambientes de verdade diferentes** (26/07, build-40). Dev é onde se testa: senha própria (`padelizou`/`natapadel`), **sem login automático** — quem entra cria a própria conta — e o seed de demonstração roda só lá (`DadosDemo:Habilitado`, que nasce **desligado**). Antes o seed rodava em qualquer ambiente, o que tornava a produção impossível de limpar: um restart e ela renascia com 23 jogadores inventados. Prod ganhou senha nova e mantém o login automático como Felipe (modo demonstração). Aviso de **Beta** ligado nos dois, com texto próprio por ambiente. Dev semeado com um cenário de cada tipo: torneio finalizado, em andamento (com jogo ao vivo), com inscrições abertas, professor com agenda e clube com 3 quadras precificadas.
- **Primeiros testadores reais no dev** (26/07, build-43). Três coisas quebraram na hora e foram corrigidas: (1) o **cadastro caía com "Ops! Algo deu errado"** — a pasta de uploads do dev era do `root` e o app não criava `fotos-perfil`; permissão corrigida nos dois ambientes e, no código, falha de foto virou não-fatal (a foto é opcional e derrubava o cadastro inteiro); (2) **tema escuro ilegível** — 24 views fixavam `#f8f9fa`/`#fff` em `style` inline, que nenhuma regra de CSS alcança; trocadas pelos tokens do tema, medido no navegador (74 elementos, nada abaixo de 3:1 nos dois temas); (3) **cadastro sem saída** — não havia cidade nenhuma e o botão de adicionar clube chamava um endpoint que passou a exigir login. Agora clube, cidade e time são campos do próprio formulário, criados no servidor junto com a conta (`Services/CatalogoLocais`), sem endpoint aberto. **Cada pessoa cria um time só.**
- **Canal de sugestão/bug/crítica** (26/07): aberto até pra quem não está logado, encaminha pro WhatsApp com a mensagem pronta e guarda cópia por e-mail. Link na faixa de beta, presente em toda tela.
- ⏳ **Limpeza da produção: pronta, não executada.** Script em `/opt/padelizou-deploy/limpar-demo-prod.sh` (faz backup antes). Apaga 144 jogadores fictícios e os 14 torneios de demo, preserva a conta do Felipe e os catálogos.
- **Raquete Livre era outra coisa** (26/07, build-37): estava modelado como evento com hora de início **e fim obrigatórios**, e descrito no material comercial como "entrar de substituto". É rodízio: hora de começar, valor fixo por pessoa, sem dupla marcada, número inexato de gente e **muitas vezes sem hora pra acabar**. `DataHoraFim` virou anulável e as regras de exibição saíram pra `Services/SessaoRaqueteLivre` (sessão sem fim fica em cartaz por 6h após começar). **169 testes.**

### 27/07/2026 — Dinheiro de verdade, e o torneio que se explica sozinho

- **🎉 SAIU DO MODO DEMONSTRAÇÃO.** Asaas de produção configurado (chave + webhook), **primeiro pagamento real recebido** (R$ 9,00 no cartão) e a corrente inteira verificada nos logs: cobrança → webhook → inscrição confirmada → split. Produção limpa dos 144 jogadores fictícios (com backup antes).
  ⚠️ **Pendente do Felipe:** conta bancária no Asaas está `bankAccountInfo: PENDING` — trava Pix e saque. E vale gerar chave e token novos agora que a configuração estabilizou.
- **Como o organizador recebe** (build-46+): três formas na criação do torneio — **só Pix (10%)**, **todas as formas (15%)** ou **por fora (5%, ele cobra e paga a comissão depois)**. O preço é **por pessoa, sempre**. A conta aparece ao vivo enquanto ele digita: quanto o jogador paga, quanto é taxa do Padelizou, quanto sobra. Modal explica prazos (cartão só cai em 32 dias) sem nunca nomear o gateway — pro organizador é só "meio de pagamento", e a única taxa que existe é a do Padelizou.
- **Status Pago por inscrição**: quem paga fica **Pago** na hora; o organizador escolhe se o pagamento é obrigatório na inscrição, define prazo, decide se quem não paga perde a vaga, e pode marcar pago/não pago a qualquer momento. Taxa opcional por **impedimento** (o organizador define se cobra e quanto).
- **Recuperação de senha, e fim da tomada de conta por CPF** (build-53): não existia "esqueci minha senha", e quem esquecia se cadastrava de novo com o mesmo CPF — o cadastro **sobrescrevia a senha**. CPF não é segredo no Brasil: qualquer um que soubesse o do Felipe assumia a conta de admin. Agora tem link por e-mail (token de 32 bytes, 1 hora, uso único, resposta idêntica exista ou não a conta), e o cadastro só deixa reivindicar CPF que **nunca teve senha**.
- **Grade de jogos** (build-52): o agendamento somava um jogo por vez a partir do início — ignorava as quadras, ignorava o expediente e reiniciava a cada categoria (jogo marcado às 3h40 da manhã). Virou `Services/GradeDeJogos`: N quadras em paralelo, para no fim do expediente e retoma no dia seguinte no horário de abertura, uma grade única pro torneio inteiro.
- **Todo jogo do torneio nasce com horário** (build-54): os jogos de **mata-mata** são criados pelos robôs depois da fase de grupos e nasciam **sem hora nenhuma** — "a definir" justo na fase que mais importa. Agora emendam no último jogo já marcado, com as mesmas quadras e expediente, e viram o dia no horário de **abertura**. O **Americano** tinha o defeito antigo (fila indiana) e passou pela mesma grade. A tela de criar torneio agora **mostra a que horas começa o último jogo do dia**, calculado com a mesma conta da grade.
- **Inscrição: o CPF manda nos campos**: nome, celular, cidade e UF nascem travados; CPF cadastrado traz os dados do perfil e mantém travado, CPF novo limpa e destrava avisando que é pré-cadastro. Travado é `readonly`, não `disabled` — `disabled` não vai no POST.
- **Canal de opinião com nota 0–10**: link no rodapé de toda página, só pra quem está logado. Nasce **invisível**; nada aparece em tela até um admin ler e publicar, um a um (`/Admin/Feedbacks`). As notas são lidas como **NPS**, não média. Publicado, vai só o primeiro nome.
- **Bug de produção corrigido na hora**: a página do torneio dava 500 quando alguém se inscrevia **sem parceiro** — `_JogadorChip` recebia `null`. Apareceu com a primeira inscrição real.
- **Ensaio geral no dev** (27/07, build-57/58): torneio completo do zero ao campeão — conta nova, 8 duplas, 7 jogos de grupo, mata-mata automático, campeão com 100 pts — mais o Americano inteiro. **Zero jogos sem horário, zero erro 500 em 20 telas, nada nos logs.** O fluxo do jogo passou; os dois defeitos achados estavam *em volta* dele:
  - **Mesa de Controle sem saída**: ela só mostra jogos *Ao Vivo* e as partidas nascem *Agendada*, então abria vazia no dia do torneio dizendo "nenhuma partida marcada como **Em Andamento**" — status que não existe na interface — e sem dizer onde marcar. Agora nomeia o status certo, explica o passo e leva pros Jogos.
  - **🔴 Todo deploy deslogava TODO MUNDO** (descoberto por acidente, ao publicar a correção acima e cair na tela de login). Faltava `PersistKeysToFileSystem`: o chaveiro de proteção de dados nascia novo a cada start, invalidando o cookie de todos — inclusive nos restarts automáticos do vigia de uptime. No meio de um torneio derrubaria o organizador da Mesa com os jogadores esperando. Chaves agora em disco por ambiente (`/opt/padelizou-shared/{env}/dataprotection-keys`, 700 www-data) + `SetApplicationName` por ambiente. **Verificado ao vivo: serviço reiniciado, sessão de pé.**
- **279 testes.**

### 27/07/2026 (noite) — Identificador único e os times reais

- **🔴 E-mail, CPF e login: um identificador, uma pessoa** (build-63). A entrada casa **e-mail OU login** na mesma consulta, com `FirstOrDefault` — mas só o cadastro checava unicidade, e só de *login contra login*. A **edição de perfil gravava e-mail sem checar nada**. Dava pra pegar o e-mail de outra pessoa e **trancá-la fora da conta dela**: ela não entra (a senha confere contra a outra linha) e não recupera (o link vai pro e-mail de quem ocupou). Mesma família do buraco de CPF fechado de manhã: identificador que deveria ser único e não era.
  Regra centralizada em `Services/IdentidadeJogador` — toda checagem compara contra os **dois** campos, porque e-mail e login vivem no mesmo espaço de nomes.
  No banco: CPF já era único; **Login era único mas sensível a maiúscula** ("Bona" e "bona" cabiam os dois); **Email não tinha índice nenhum**. Migração cria índices únicos por `LOWER()`, parciais pra não esbarrar em pré-cadastro sem e-mail/login. Os três bancos foram conferidos sem duplicado **antes** — a migração roda no start do app, e falhar ali deixaria o app fora do ar.
  De quebra: a foto só é salva **depois** das validações, então cadastro recusado não deixa mais arquivo órfão no disco.
- **44 times reais, com bandeira.** Os times de teste saíram e entraram os 44 do ranking do "Quanto Tá" (a lista do Felipe tinha 36; a página tem 44). Bandeiras baixadas e servidas em `/uploads/logos-time/` nos dois ambientes. `DELETE`, nunca `TRUNCATE CASCADE` — e a única FK que aponta pra `Times` é `Jogador.TimeId` com `ON DELETE SET NULL`, então ninguém sumiu. Backup em `/tmp/backup-times-{prod,dev}.csv`; de prod só saiu o "Nata Padel".
- **Time com vários administradores** (build-63): `Time.DonoId` (um só) virou a tabela `TimeAdministradores`. O primeiro administrador de cada time só entra pela mão de um **admin do Padelizou**; daí em diante um administrador do time inclui o próximo. Regra em `Services/AdministracaoTime`, fora dos controllers.
  ⚠️ **A migração precisou ser corrigida à mão:** o EF gerou o `DropColumn` do `DonoId` **antes** de criar a tabela nova, o que jogaria os donos fora. Ficou: cria, copia, e só então derruba. A cópia faz `JOIN` com `Jogador` porque `DonoId` era coluna solta, sem FK — podia apontar pra quem não existe mais.
  **Trava que importa:** entrar num time pelo nome no cadastro **não dá cargo nenhum**. É isso que impede alguém de digitar "SINDAQUA" e sair comandando um dos times importados.
- **Agenda do professor virou calendário** (build-65). "Minha Agenda" era um monte de cartão solto, sem noção de tempo — não dava pra ver a semana nem saber se terça está cheia. Agora é **calendário no estilo Google** (grade de horas no dia/semana, quadro do mês) **ou lista de eventos em ordem** agrupada por dia, nos dois casos filtrando por **dia, semana ou mês**, com setas e botão "Hoje". Clicar num evento abre um modal único, preenchido por JS a partir de dados que já vieram do servidor (no ginásio com 3G ruim, clicar e não abrir nada seria pior que não ter o modal). Cada ação aparece só onde faz sentido, e a política de 24h continua igual.
  **Pendências ficam fora da janela de propósito:** solicitação pro mês que vem sumiria da tela de quem está olhando esta semana, e o professor perderia o prazo sem nunca ver.
  Conta de datas em `Services/PeriodoAgenda`, com 18 testes — é o tipo de coisa que erra em silêncio (semana começando no dia errado, dia 31 fora da grade, 31/01 + 1 mês pulando fevereiro). Nomes de mês escritos à mão em vez de `CultureInfo`: o servidor não tem cultura pt-BR garantida.
  **Verificado rodando local** com 16 aulas de teste: semana (domingo a sábado, faixa de horas esticando pra 05:00), mês (5 semanas, dia 31 presente, hoje destacado), dia, lista agrupada, modal por status, e `Confirmada → Realizada` gravando de verdade. Sem erro de console; no celular a página não rola na horizontal.
- **Nomes dos times em caixa normal** ("Joel Padel Trainer", não "JOEL PADEL TRAINER"), por `UPDATE` que preserva Ids e administradores. Siglas seguem maiúsculas (ER, SL, MMC, TNT, POA) e os acentos voltaram — o site de origem tira acento de tudo ("CAMPEAO"), então a falta deles era artefato da fonte.
- **380 testes** (+101 no dia).

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

### Fase 2 — Sair do modo demonstração ✅ *feita 27/07*
- [x] **Asaas para produção** (chave + webhook) ✅ 27/07 — **primeiro pagamento real recebido**, corrente verificada nos logs
- [x] **Limpar dados fictícios** ✅ 27/07 — 144 jogadores e os torneios de demo apagados de produção, com backup antes
- [ ] ⏳ **Conta bancária no Asaas** (`bankAccountInfo: PENDING`) — trava **só o saque**; as chaves Pix estão ATIVAS e o dinheiro cai normal ← *precisa do Felipe*
- [ ] ⏳ **Gerar chave e token novos** do Asaas, agora que a configuração estabilizou ← *precisa do Felipe*
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

## 🔎 Achados da varredura de 27/07 (noite) — ainda abertos
- **`Ranking.cshtml` desreferencia `Jogador2` sem checar nulo** (linhas 387, 421, 487). É a forma exata do 500 que apareceu em produção dia 27. **Hoje não quebra**: o parceiro nunca volta a ser nulo depois de definido e o sorteio só aceita dupla completa, então dupla sem parceiro não acumula vitória pra chegar nessa tabela. Mas todo o resto do sistema já se protege disso — essas 3 linhas ficaram de fora. `3 linhas`
- **Exportação de calendário** (`AgendaController:359`) monta `"Ana" + "/" + null` → vira "Ana/" no evento. Feio, não quebra. `5min`
- **Adversários do Americano**: com 8 jogadores alguém pega o mesmo rival 4 dos 7 jogos. Parceiros está perfeito (cada um com cada um, exatamente uma vez); adversário exigiria outro desenho matemático (whist design).
- **Botão "colocar no ar" na lista de Jogos**: hoje é uma tela por partida; numa rodada de 4 quadras são 4 aberturas. Único atrito real no dia do torneio.

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

Gerados em 26/07/2026 (PDFs na Área de Trabalho + artifacts no claude.ai):
- **Apresentação comercial** — página de venda com o posicionamento "o padel cresceu,
  chegou a plataforma à altura"; falta trocar o WhatsApp de exemplo do botão final.
  Artifact: `claude.ai/code/artifact/05bdbad2-ff8c-411f-99a2-675455a21756`
- **Análise de monetização** — recomendação: híbrido em fases (comissão 15/10/10 como
  base → assinatura que compra taxa menor, estilo Shopify → audiência); jogador nunca
  paga; o concorrente da cobrança é o Pix por fora; teto do MEI vira tarefa quando a
  receita chegar perto de R$ 6,7 mil/mês. Preços de planos são hipóteses a validar com
  3 meses de dado real do Asaas em produção.
  Artifact: `claude.ai/code/artifact/128ee0e3-a783-4bfa-9ff8-d7b24f8f6c43`

> ⚠️ Os 3 documentos refletem o diagnóstico de **25/07 de manhã** e envelheceram: a maior
> parte do que eles listam como "falta" já foi entregue. Este STATUS.md é a fonte da verdade.
