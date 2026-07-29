# Padelizou — Status e Roadmap

> **Documento vivo.** Atualizar ao fim de cada bloco de trabalho: mover itens de "Próximos" para "Feito" e ajustar prioridades.
> Última atualização: **29/07/2026** — **logo novo no ar** (build-95) e o achado nº 1 da revisão foi fechado: **carimbo antifalsificação (CSRF) global**, mais 2 defeitos reais que estavam escondidos nos avisos do compilador (login de quem não tem e-mail; robô do mata-mata sem checar o torneio), 25 avisos → **0**, e o mascote em **17 telas** de estado vazio. **build-93**, **539 testes**, publicado em dev e prod.
> ⏳ Continua pendente do Felipe: publicar o app no Google (o backup morre em 7 dias sem isso) e conferir o **webhook do meio de pagamento recusado de hora em hora** (13×/24h, aos :28 — provável sobra do sandbox apontando pra produção).

---

## Onde estamos

Sistema no ar em **padelizou.com.br** (+ `dev.` para testes e `admin.` para o painel).
Stack: ASP.NET Core 10 · PostgreSQL no VPS · PWA instalável. Deploy por `deploy.sh` / `deploy-dev.sh`.

**Estado:** funcionalmente rico e tecnicamente protegido (git + **532 testes** + CI + monitoramento + rollback em 1 comando + varredura de autorização feita).
As áreas de **professor, clube e organizador estão completas**; a entrada se adapta ao papel de quem entra.

**Saiu do modo demonstração em 27/07:** Asaas de produção ligado, **primeiro pagamento real recebido** (R$ 9,00) e produção limpa dos dados fictícios.

**Produção zerada em 28/07** — folha em branco, esperando o primeiro torneio de verdade. Ficou só o necessário:
| Fica | Por quê |
|---|---|
| A conta do Felipe (admin) | sem ela ninguém administra |
| 20 categorias padrão | catálogo do sistema |
| 44 times com bandeira | dados reais do ranking "Quanto Tá" |
| 1 pagamento de R$ 9,00 | **dinheiro real que entrou** — o MEI obriga a guardar registro de receita |

Saíram o torneio "teste felipe " (com categoria, dupla, quadras e vínculo de organizador) e o clube "Chakra padel" que nasceu junto dele, sem dono nem contato.
⚠️ **A ordem do DELETE não é arbitrária:** `Categoria.TorneioId` e `Dupla.CategoriaId` são `NO ACTION` (têm que sair antes), e **`Torneio.ClubeId` é `CASCADE`** — apagar o clube arrastaria o torneio de carona. Por isso o torneio saiu explicitamente antes. O pagamento sobreviveu sozinho porque **não tem FK pro torneio**.
Dump completo antes em `/opt/padelizou-shared/backup-prod-antes-limpeza-20260728-1207.sql.gz`. As 7 telas principais conferidas depois: 200, sem erro, 44 bandeiras na vitrine. **Dev ficou como estava**, de propósito — é onde se testa.

**Falta agora:** o primeiro torneio de verdade rodar de ponta a ponta, e a conta bancária do Asaas sair de `PENDING` (trava só o saque; o Pix cai normal).

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
- **399 testes** (+120 nos dias 27–28).

### 28/07/2026 — Serviço de registro de resultados, e imagens que pesavam demais

- **Pacote "nós registramos os resultados pra você"** (build-74/75). O organizador contrata a nossa equipe pra lançar os jogos durante o torneio — marcando na criação do torneio ou depois, na página dele. É **solicitação, não compra**: o botão diz "verificar disponibilidade", porque pode não haver ninguém livre naquela data e naquela cidade. Admin responde em `/Admin/RegistroResultados` (confirma com pessoas e valor, ou devolve "sem disponibilidade"), e o organizador acompanha o status.
  **Preço por JOGO, não por dia** (decisão do Felipe): **R$ 12,00 por jogo, mínimo R$ 500,00**, custo nosso de R$ 10,00 por jogo. Cobrar por dia erraria os dois extremos — um Americano de um dia pode ter mais jogos que um torneio de duplas de três. O número de jogos vem do `PrevisaoDoTorneio`, o mesmo cálculo do sorteio.
  ⚠️ **O mínimo domina até 42 jogos** — abaixo disso todo torneio paga R$ 500, e é onde a margem é maior. Acima, a margem é fixa em R$ 2/jogo (1/6 do preço): torneio grande dá mais trabalho por real. O painel avisa isso na tela pra não parecer conta errada quando dois pedidos diferentes derem o mesmo valor.
  A regra fica **congelada no pedido** (`PrecoPorJogoCotado`, `ValorMinimoCotado`, `JogosPrevistos`): mudar o preço amanhã não muda o que quem pediu ontem leu na tela. O campo de valor já vem preenchido pela regra e é ajustável quando o clube for longe.
- **🔴 Toda imagem enviada passou a ser redimensionada** (build-76). Achado medindo o backup: **uma única capa de torneio de 8 MB em produção era 60% de todo o armazenamento** — e era baixada inteira por quem abrisse aquele torneio no 4G. Os três pontos de upload (foto de perfil, logo de time, capa) gravavam o arquivo cru como veio do celular.
  Agora tudo passa por `Services/ImagemEnviada`: **redimensiona** (perfil e logo 512px, capa 1600px), **recodifica em WebP**, **apaga os metadados** — foto de celular carrega coordenada de GPS embutida, e publicar a foto de perfil de alguém junto com o lugar onde foi tirada é vazar endereço — e **ignora o nome do arquivo enviado**, que antes ia colado no caminho em disco (`"guid_" + FileName`, e um nome com `../` sairia da pasta de uploads).
  Medido de verdade: 4000×3000 / 2,3 MB → **512×384 / 120 KB, 19× menor**, renderizando no navegador.
  Botão **"Otimizar imagens"** no painel admin refaz o que já estava no disco. É **idempotente por formato, não por tamanho**: a conta ingênua ("ficou menor? troca") faria a imagem perder qualidade a cada rodada, porque WebP com perdas recomprimido sempre encolhe mais. Um teste roda a otimização três vezes e exige que o arquivo não mude depois da primeira.
  Recusa também o que não é imagem de verdade (arquivo renomeado pra `.png`), o que é grande demais e o "decompression bomb" (PNG de 2 MB declarando 50000×50000).
  **`SkiaSharp`, não `ImageSharp`:** o ImageSharp 4 passou a exigir chave de licença paga no build. SkiaSharp é MIT, sem limite de faturamento e sem chave. O nativo `runtimes/linux-x64/native/libSkiaSharp.so` foi conferido no `publish` — se faltasse, todo upload falharia **em silêncio**, porque o processamento nunca derruba um cadastro.
  De quebra: `wwwroot/uploads/` entrou no `.gitignore` (tinha **uma foto de perfil versionada** no repositório; em produção a pasta é symlink pro `padelizou-shared`, então nada ali precisa ser versionado).
- **🔴 Backup fora do servidor, no Google Drive do `padelizou@gmail.com`** (4h30, meia hora depois do backup local). O backup de `/var/backups/padelizou` mora **no mesmo disco do banco**: se o VPS morrer, morrem os dois juntos. Enquanto era só dado de teste, tudo bem; com gente de verdade usando, não dá.
  Vai **criptografado** (`rclone crypt`) — o pacote leva `appsettings.json` (chave do meio de pagamento, senha do SMTP) e o banco com CPF/telefone/e-mail de gente real. O Google guarda só o embaralhado; nem os nomes dos arquivos aparecem. **A chave está em `/root/padelizou-chave-backup.txt` e no gerenciador de senhas do Felipe — sem ela o backup é inútil**, essa é a troca que a criptografia impõe.
  **Espelho incremental, não pacote diário**: mandar o `.tar.gz` todo dia daria ~8 GB/ano da mesma foto subindo 365 vezes; assim são 12,7 MB hoje e só o delta depois. O `sync` usa `--backup-dir` datado — no sync puro, apagar uma foto aqui apagaria a cópia lá, e o backup deixaria de proteger justamente contra apagar sem querer.
  **Restauração testada de verdade** (não só "o arquivo subiu"): dump baixado *do Drive* → banco descartável → 49 tabelas, 0 erros, contagens batendo com o `db_padel`; um `.jpg` voltou byte a byte idêntico. O dump das 4h ainda tinha 1 torneio/categoria/clube apagados depois — a prova de que serve pra desfazer engano.
  Um defeito meu apareceu na 1ª execução: a checagem "está autorizado?" usava `rclone lsd`, que **também** falha quando a pasta ainda não existe — o script se declararia não-autorizado pra sempre, um backup que nunca roda e não reclama. Virou `rclone mkdir` (idempotente, só falha quando o Google recusa).
  ⏳ **Pendência com prazo:** o remote usa o `client_id` compartilhado do rclone, que o Google **aposenta durante 2026**. Precisa de um client_id próprio antes disso, senão o backup para sozinho.
- **Contato do Padelizou no WhatsApp: `(51) 99239-5650`** (build-77, publicado em prod e dev 28/07). Item **"Entre em contato"** no menu (abre o WhatsApp já com a mensagem começada e identificando quem é — do outro lado chega um número desconhecido) e o número **escrito** no rodapé, porque em celular não existe passar o mouse pra ver e tem quem prefira salvar o contato.
  ⚠️ **O número que estava no código era outro** (`51994854884`): tudo que já existia — o botão "Sugestão, bug ou crítica" da faixa de beta — apontava pra lá. O número vive só no default de `SuporteSettings` (não há seção `Suporte` em nenhum `appsettings`), então trocar no código valeu pros três ambientes.
  `WhatsAppLinkHelper.Formatar` separa o número de mostrar do de linkar — quem lê precisa da máscara, o `wa.me` a recusa — e devolve sem máscara o que não reconhece, porque número feio é melhor que número errado.
- **⚠️ Achado de layout (pré-existente, NÃO consertado):** em janela de **1280px** a barra de navegação **estoura 370px** e a página rola na horizontal. Não é o item novo — medido escondendo ele, o estouro continua: são 10 itens + o chip de usuário num container de 1140px. As duas saídas custam algo e a escolha é do Felipe: `navbar-expand-xxl` zera o estouro mas **esconde o menu atrás do hambúrguer até 1400px**, e deixar quebrar em duas linhas também zera mas **dobra a altura da barra (68 → 141px)**, que é fixa no topo. Em celular está tudo certo (hambúrguer, sem rolagem, alvo de 41px).
- **🔴 Dois buracos de permissão no servidor, achados sem querer** (28/07, corrigidos direto no VPS — não é código):
  **1. `uploads/logos-time/` pertencia ao UID `197609`** (um UID do *Windows*, que veio junto na cópia dos 44 escudos) e o app roda como `www-data`. Dava pra **ler** (por isso os logos apareciam) mas não pra **escrever**. Consequências: o botão "Otimizar imagens" falhava nos 44 logos, e — pior — **quem subisse o escudo do time em produção não conseguia, sem ver erro nenhum**, porque processamento de imagem nunca derruba um cadastro. Diagnóstico veio do journal: 76 falhas na linha 105 do `OtimizacaoDeImagens`, que é o `File.WriteAllBytesAsync`. Corrigido nos dois ambientes (`chown www-data:www-data` + 775).
  **2. `appsettings.json` de prod e dev estava `644` (mundo inteiro lê)** — e ele guarda `ApiKey` do meio de pagamento, `Senha` do SMTP, `PrivateKey` do VAPID e `WebhookToken`. Testado antes de mexer: o usuário **`nobody`** lia o arquivo. Agora `640 root:www-data` (só o app lê pelo grupo) e `GoogleTokens/` de `755` para `700`. Verificado depois: `www-data` lê, `nobody` não lê, e os dois serviços reiniciaram em **200**.
  ✅ **Rodado depois do conserto:** *"38 imagem(ns) otimizada(s): 3,5 MB viraram 0,7 MB"*. Os uploads de produção caíram de **13 MB para 9,9 MB**, zero erro. **6 logos foram pulados de propósito** — eram JPEG pequenos e já bem comprimidos, e o WebP em qualidade 95 ficaria *maior*; a rede de segurança do `nova.Length >= original.Length` recusou trocar. Rodado 3×: os arquivos mantiveram o timestamp da primeira passada, então a idempotência vale em produção, não só no teste.
  ⚠️ **Mas a mensagem de retorno é intermitente** — apareceu na 1ª vez e não nas seguintes, com a ação rodando normalmente (confirmado no journal). Não é o Service Worker (ele só intercepta `css/js/imagens`; páginas vão direto pra rede). Causa ainda não achada — é da mesma família do upload que falha calado: **o sistema faz o trabalho e não conta**.
  ⚠️ **A capa de 8 MB não sai pelo botão**: `Torneio.ImagemCapa` não tem nenhuma linha desde a zeragem, então o arquivo é **órfão** — o otimizador varre o banco, e o que ninguém referencia ele nunca vê. Apagar arquivo órfão é outro trabalho.
- **Menu reorganizado** (28/07). Eram 10 itens no topo disputando espaço; agora são 7:
  **"Times" virou "Buscar"** — quem procura alguém pra jogar não pensa "vou no menu Times". A tela acha **jogador e time**, e as duas que existiam (`Times/Index` e `Jogadores/Buscar`) viraram uma só com abas (`Shared/_AbasBusca`). O botão de ida-e-volta que cada uma tinha no canto sumiu: botão esconde o caminho de volta, aba mostra as duas opções o tempo todo. A aba ativa sai do controller da vez, então não tem como esquecer de passar o parâmetro certo numa das telas.
  **"Buscar jogo", "Marcar jogo" e "Grupos" viraram o menu "Jogos"** — é tudo o mesmo assunto.
  **"Agenda" saiu do topo** e ficou só no perfil, onde o botão "Minha Agenda" já existia. É informação pessoal, não navegação do site.
  ✅ **Isso resolveu de graça o estouro de 370px** que estava esperando decisão: caiu para 23px só com a reorganização, e para **0** ao limitar o chip do usuário a 120px no desktop. Esse limite conserta um defeito à parte: a barra dependia do **tamanho do nome de quem entrou** — com "Felipe" não rolava de lado, com "Felipe Carboni Bonamigo dos Santos" rolava 58px. O nome cortado agora aparece inteiro no `title`.
  Também subiu o alvo de toque do submenu no celular de 30px para 39px (os itens de primeiro nível têm 41px, e o padrão de dedo é 44px).
- **🔴 Ninguém conseguia marcar aula no site — em nenhum ambiente** (build-81). A tela de marcar aula pergunta a cidade primeiro, e a lista sai de quem é professor **e** declarou cidade. `ProfessorCidade` estava **vazia nos três bancos**: 0 de 7 professores. Lista vazia = primeiro seletor não abre = os outros quatro nunca destravam. Virar professor não pede cidade em momento nenhum, então a pessoa se cadastra e some do site sem saber. Já havia um "você ainda não cadastrou cidades" no meio do painel — informava o **fato**, não a **consequência**; agora o aviso está no topo e diz **"nenhum aluno consegue te achar ainda"**, com botão pra cidade e pra local.
- **Contraste no tema escuro** (build-81): fundo claro cravado no `style` inline (`#f3f8ff`, `#fff8e6`…) com texto que **muda** de cor no tema = claro sobre claro. Achados **9 lugares** varrendo o projeto, não só o que apareceu. Viraram classes `.pdz-tinta-*` com variante escura. Ficaram de fora, de propósito, os selos com cor escura cravada (`#8a6d00`, funcionam nos dois) e o relatório de impressão (branco é o certo).
- **Cidades duplicadas** (build-81): "Gravatai", "Gravataí" e "gravatai" conviviam no filtro do ranking. A comparação era `ToLower()` — pegava a caixa e **deixava o acento passar**. `Services/NomeDeCidade` compara sem acento e sem caixa, como uma pessoa compararia, e **preserva** o acento de quem digitou certo (o banco continua guardando "Gravataí"). ⏳ Impede novas; juntar as que já existem é script à parte, mexe em dado real.
- **Grade de dias × períodos**: a célula inteira virou o alvo do clique (44px em vez de 16px) e o quadrado ficou visível no escuro. Era o **único** checkbox da tela sem `<label>` — os outros já tinham texto ao lado pra clicar.
- **⚠️ Apaguei o mascote e o backup salvou.** A capa órfã de 8 MB que eu classifiquei como lixo era o **Pnatinha**, mascote do site. A checagem estava certa (0 referências no dump inteiro) e o julgamento errado: "ninguém referencia" ≠ "não serve pra nada". Restaurado do Drive byte a byte. **A regra que funcionou foi conferir o backup ANTES de apagar** — sem ela, o arquivo tinha ido embora. ⏳ Ele segue solto em `uploads/` (órfão de novo, e 8 MB): o lugar dele é `wwwroot/image/`, versionado e otimizado.
- **O Pnatinha virou parte do produto** (mascote do site). Três coisas:
  **1. Casa própria.** Saiu de `uploads/` — onde estava órfão e por isso foi apagado — e virou arquivo do projeto, versionado: `wwwroot/image/pnatinha.webp` (600×328, cena inteira) e `pnatinha-vazio.webp` (358×400, recortado nele). **8 MB → 59 KB e 39 KB**, mesma reamostragem Mitchell do `ImagemEnviada`. O servidor não tem ImageMagick nem cwebp; o redimensionamento saiu de um projeto SkiaSharp descartável no scratchpad.
  **2. Estados vazios.** O sistema tinha **14** "Nenhum X ainda." em texto cinza solto. Viraram o parcial `Shared/_Vazio` (mascote + mensagem + botão opcional), aplicado por ora nos 4 do painel do professor. O botão é opcional porque nem todo vazio tem saída: "nenhum aluno ainda" não se resolve clicando; "nenhuma cidade cadastrada" sim.
  **Por que agora:** a produção foi zerada hoje, então *quase toda tela* é um estado vazio — é o que mais gente vai ver esta semana, e some sozinho conforme o site enche.
  **3. Página de erro e 404.** `Views/Shared/Error.cshtml` ainda era o template padrão do ASP.NET: **em inglês**, ensinando o usuário final a configurar `ASPNETCORE_ENVIRONMENT`. E **não havia `UseStatusCodePages`** — endereço errado caía na tela crua do navegador, sem menu e sem volta (link velho de torneio no WhatsApp é o caso comum). Agora `/Home/NaoEncontrado` com o mascote, texto por código (404 "Essa bola saiu", 403 "Essa área não é sua"), **devolvendo o status certo** — responder 200 faria buscador e monitoramento tratarem página inexistente como boa. Usa `ReExecute` pra manter na barra a URL que a pessoa digitou.
  **4. Pnatinha feliz** (chegou no mesmo dia): `pnatinha-feliz.webp` (600×328, 69 KB) e `pnatinha-feliz-recorte.webp` (386×400, 50 KB), do mesmo original de 8,2 MB. Entra **no campeão da chave**, e só quando existe campeão de verdade — chave em aberto continua com o troféu, porque comemorar antes da final estraga o momento em que a comemoração vale.
  Deliberadamente **não** foi espalhado: o relatório pós-torneio é feito pra impressão (`@media print`) e mascote grande ali gasta tinta; o onboarding some quando conclui, então não há tela de conclusão pra comemorar. Usar em cinco lugares só pra usar seria pior que usar bem em um.
- **Agora o professor é OBRIGADO a declarar cidade e local** (build seguinte). O aviso no topo do painel não bastava — continuava sendo possível ignorar e seguir invisível. A regra virou `Services/CadastroDeProfessor`, num lugar só e testável:
  **Onde cobra:** (a) ao marcar "sou professor" no perfil, a pessoa vai direto pra Minhas Cidades em vez de voltar achando que terminou; (b) o painel do professor **redireciona** pro que falta antes de abrir.
  **A ordem segue a escada da tela do aluno** (cidade → professor → local → tipo → horário), não a preferência de quem programou: cobrar o local antes deixaria o professor fora da lista do mesmo jeito.
  **Sem risco de laço:** `MinhasCidades` e `MeusLocais` não têm a checagem, e as duas salvam e devolvem. Testado no navegador de ponta a ponta — pedi o painel, fui parar em Minhas Cidades; salvei a cidade, pedi o painel, fui parar em Meus Locais; salvei o local, pedi o painel e **ele abriu**. Depois disso a cidade apareceu no seletor da tela do aluno, que era o objetivo.
  As duas telas mostram **por que** a pessoa foi trazida: redirecionamento sem explicação parece defeito, e ela tenta voltar em vez de resolver. **518 testes.**
- **O terceiro degrau da escada do professor** (achado ao levantar o que faltava): produção tinha 1 professor **com cidade**, 0 locais e **0 horários**. Cobrar só cidade e local deixaria o aluno percorrer quatro degraus — cidade, professor, local, tipo — pra descobrir no quinto que não há horário nenhum. `PendenciaDoProfessor` ganhou `Horario`, e o painel cobra os três em ordem.
- **🔴 Fim do upload de imagem que falhava calado.** `SalvarAsync` devolvia `null` tanto pra "não mandou foto" quanto pra "não deu pra salvar", e o chamador tratava tudo como ausência: a pessoa escolhia o arquivo, salvava e ia embora achando que a foto estava lá. Foi isso que escondeu por um dia inteiro a pasta de logos com dono errado. Agora devolve `ResultadoDaImagem` (**ausência ≠ falha**), com mensagem em português por motivo — grande demais, formato recusado, ilegível, erro ao gravar. O aviso é renderizado **no `_Layout`**, não em cada tela: são 3 telas com upload e cada uma redireciona pra um lugar diferente, então repetir o bloco seria esquecer numa. O cadastro continua seguindo sem a imagem (perder um formulário longo por causa de uma foto é pior) — mas agora **contando**.
- **Numeração da tela de marcar aula** (o "1, 2, 3, 5" do print): "Tipo de aula" só surge depois de escolher o local, e o número estava cravado no HTML. Agora o JS numera **o que está visível**; se o script não rodar, os rótulos continuam legíveis sem número — degradar assim é melhor que quebrar.
- **`client_id` próprio do Google + vigia do backup** (28/07, fim do dia):
  A credencial compartilhada do rclone (que o Google aposenta durante 2026) foi trocada pela do projeto `padelizou`. Dois tropeços no caminho, os dois instrutivos: **(1)** `403 access_denied` — a tela de consentimento estava em modo Teste e `padelizou@gmail.com` não era testador; **(2)** depois de autorizar, o rclone passou a enxergar **0 arquivos** onde havia 59. Não é defeito: `scope=drive.file` dá acesso só ao que o app **criou**, e pro Google a credencial nova é outro app. O backup subiu do zero (60 arquivos, 9 MB) e a cópia antiga ficou no Drive, íntegra mas fora do alcance do script — dá pra apagar pelo navegador.
  ⚠️ **Modo Teste expira o refresh token em 7 dias.** Se a tela de consentimento não for publicada ("Em produção"), o backup morre na oitava noite — e em silêncio. Trocaríamos um prazo de meses por um de uma semana.
  **Daí o `VigiaDoBackup`**: o `backup-drive.sh` grava um carimbo em `/var/lib/padelizou/ultimo-backup-drive` **só quando a cópia termina inteira**, e um `BackgroundService` manda e-mail pros admins se passar de 2 dias sem atualizar. Fica **dentro do app** porque o e-mail já funciona lá, com a senha do SMTP num lugar só — script separado precisaria de uma segunda cópia dela. **"Nunca houve backup" conta como o pior caso**, não o mais inofensivo: servidor onde nunca funcionou parece igual a um onde funciona todo dia. Aviso 1×/semana (alerta repetido vira ruído). Ligado por config, **só em prod** via drop-in do systemd — o dev não faz backup e mandaria alerta todo dia.
- **532 testes** (+133 no dia 28).

### 29/07/2026 — Fechando os achados da revisão

- **🔴 Carimbo antifalsificação (CSRF) agora é global** (build-93). Eram **61 de 114** ações que gravam sem `[ValidateAntiForgeryToken]`, e nenhum filtro global: a única coisa segurando um site externo de fazer o navegador de quem está logado aqui enviar um formulário escondido era o `SameSite=Lax` do navegador — **uma** linha de defesa onde a prática manda ter duas.
  Virou `AutoValidateAntiforgeryTokenAttribute` no `Program.cs`: **protegido passou a ser o padrão, e a exceção é que precisa ser escrita**. Isso importa mais que os 61: quem escrever a ação 115ª não tem como lembrar de algo que não está em lugar nenhum.
  **Única exceção:** o webhook do meio de pagamento, que vem de fora sem cookie e já se defende pelo token próprio.
  As 7 chamadas por `fetch()` (adicionar clube ×3, placar ao vivo, palpitrômetro, ligar/desligar push) passaram a mandar o valor no cabeçalho, por um auxiliar só em `site.js` — o carimbo é renderizado uma vez no `_Layout`.
  **Provado nos dois sentidos, e nos três ambientes:** com carimbo 200, sem carimbo 400. Em dev testei o **portão de acesso** de propósito — se ele quebrasse, o site inteiro ficaria trancado.
  ⚠️ **O 400 aparece como 302 pra quem está fora do portão**: a página de erro é reexecutada e o visitante anônimo é barrado de novo. Confundi os dois por um minuto; quem for testar de novo precisa olhar o `Location`, não o status.
  **3 testes** vigiam a lista de exceções — o risco real não é o filtro sumir, é alguém colar `[IgnoreAntiforgeryToken]` numa ação pra calar um erro que não entendeu.
- **🔴 Quem não tem e-mail não conseguia entrar.** `new Claim(tipo, null)` lança exceção, e o bloco de claims estava copiado em **4 lugares** — três no `AuthController` e um no middleware, mantidos iguais por um comentário pedindo que ficassem iguais. Não ficaram: **só a cópia do middleware tratava e-mail nulo**. Pré-cadastro (jogador inscrito pelo organizador) nasce exatamente sem e-mail, então a tela de login caía inteira. Centralizado em `IdentidadeJogador.ClaimsDe`, com 4 testes.
- **🔴 O robô do mata-mata usava o torneio sem checar se existe** — e estourava *dentro* do salvamento do placar, ou seja, a Mesa de Controle daria erro no meio de um torneio por causa de outro torneio.
- **Mais dois que só apareceriam com o usuário na frente:** a tela de confirmar aula pelo e-mail caía com **aluno avulso** (sem conta no sistema, `Aluno` nulo) — justo o link que o professor abre fora do site, sem caminho de volta; e `DuplaContagemVM` prometia um parceiro que pode não existir (inscrição sozinho).
- **25 avisos do compilador → 0.** Não por `!` espalhado: cada um foi lido, e o `!` só ficou onde é verdade (o EF lê a expressão do `Include`, não executa). O único **suprimido de propósito** é a API obsoleta do Google Agenda: trocar pro `DateTimeOffset` usaria o fuso da máquina — que em produção é **UTC** — e as 14h do professor virariam 11h na agenda do aluno. Fica com o comentário explicando, pra ser trocado junto com um teste que prove o horário.
- **EF dos testes fixado em 10.0.10.** O projeto de testes resolvia **Relational 10.0.4** enquanto produção roda **10.0.10** — os testes estavam aprovando um motor de banco diferente do que está no ar.
- **Mascote em 17 telas de estado vazio** (eram 4). A produção foi zerada, então quase toda tela é um estado vazio esta semana. **Onde NÃO foi aplicado, de propósito:** busca sem resultado — "nenhum time encontrado para *xyz*" não é "nenhum time existe", e quem digitou precisa corrigir o que digitou, não ser consolado. `Times/Index` e `Professores/Index` tinham os dois casos na mesma linha e agora estão separados. Também ficaram de fora relatórios financeiros (tom errado), o painel `/Admin` (ferramenta interna) e avisos inline dentro de formulário.
- **539 testes.**
- **Logo novo em todo o sistema** (build-95). O arquivo entregue é um **círculo escuro com raquetes verdes, em JPEG sobre fundo branco** — e as duas medidas da imagem mandaram no resultado:
  **(1) O fundo branco tinha que sair.** São 21,4% da área (os cantos fora do disco) e virariam uma moldura branca em volta do logo na barra escura. Recorte circular com transparência.
  **(2) As raquetes ocupam só 44% da largura do círculo.** A 38px na barra isso as deixa com **17px**, e sobra disco vazio. Comparei 1,00 / 1,20 / 1,35 / 1,50 lado a lado nos dois extremos de uso (38px da barra e 64px do card): **1,35** lê bem no pequeno sem encostar na borda no grande. É o mesmo logo, só opticamente ajustado — o que todo conjunto de ícone faz.
  Cada ícone derivado respeita a exigência da sua plataforma: `logo-icon.webp` transparente (**WebP porque carrega em TODA página: 257 KB → 18 KB**), favicon ainda mais aproximado (a 32px o enquadramento original vira uma mancha verde), `apple-touch-icon` **opaco** (o iOS pinta preto atrás de transparência) e `icon-512` com fundo cheio e raquetes nos 60% centrais (o Android recorta a *maskable* num círculo).
  No CSS o `border-radius` foi de 10px pra **50%** (o logo virou redondo) e a sombra escura — que não separa nada numa barra escura — ganhou um **aro fininho na cor de destaque**.
  ⚠️ **`CACHE_NAME` do Service Worker foi de v1 pra v2.** Ele pré-guarda os ícones **pelo caminho**, e o `activate` só descarta cache de nome diferente: sem virar a versão, quem já instalou o app ficaria com o logo antigo **pra sempre**. Todo `<img>`/`<link>` ganhou `asp-append-version`.
  ⏳ Sobraram em `wwwroot/image/` os dois JPEG de origem (`logo-novo.jpeg` e `logo-novo-padel.jpeg`, 216 KB) — são a arte original, mas estão numa pasta pública e ninguém os usa. Valia moverem pra fora do `wwwroot`.

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
- [x] **Push de chaves publicadas e "seu jogo é o próximo"** ✅ 28/07 (build-71).
      *Chaves:* cada inscrito recebe o horário do **próprio** primeiro jogo — "as chaves saíram" sozinho obriga a pessoa a ir procurar.
      *Próximo:* disparado pelo **fim do jogo anterior na mesma quadra**, não por relógio. Torneio atrasa, e um aviso preso ao `HorarioPrevisto` chegaria com o jogador ainda almoçando (ou depois de ele já ter jogado). Quadra sem nome casa com quadra sem nome, senão torneio pequeno nunca receberia o aviso.
      ⚠️ **Defeito achado ao testar em tela:** o aviso saía sempre que o status era "Finalizada", e **corrigir placar de jogo já encerrado é rotina**. Cada correção chamaria a partida seguinte de novo — e, como a primeira já fica marcada, chamaria a *seguinte da seguinte*. Agora só dispara na **transição**.
      ⚠️ **E um teste que passava sem testar nada:** `Url.Action` estoura em controller de teste sem `UrlHelper`, e como as chamadas de push vivem em `try/catch` (push é acessório, não pode derrubar o placar), o teste ficava verde sem executar o trecho. `TestInfra` agora injeta um `IUrlHelper`.
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
- [x] **Apagar `/opt/padelizou-legado` e `/opt/padelizou-dev-legado` no VPS** ✅ 28/07 — conferido, as duas pastas já não existem. Disco do VPS em 7% (6,3 GB de 97 GB).

## 🔎 Achados da varredura de 27/07 (noite)
- [x] **`Ranking.cshtml` desreferenciava `Jogador2` sem checar nulo** (3 lugares) ✅ 28/07 (build-67) — viraram um helper só, `NomesDaDupla`. Verificado forçando o caso no banco local: a dupla sem parceiro agora sai como "(sem parceiro)" em vez de derrubar a página.
- [x] **Exportação de calendário** montava `"Ana" + "/" + null` ✅ 28/07 (build-67) — nomes separados na consulta, juntados na memória.
- [x] **Botão "colocar no ar" na lista de Jogos** ✅ 28/07 (build-67) — um toque começa a partida sem sair da tela; idempotente, dois toques não reiniciam o cronômetro.
      ⚠️ **Bug achado ao testar em tela:** `ViewBag.EhOrganizador` só era definido DENTRO do `if` do Americano. Em torneio de duplas — a maioria — a flag nem existia, então o botão novo nunca apareceria e o "Editar Jogo" aparecia pra todo mundo. Movido pra fora do `if`. Os 386 testes não pegariam isso; só rodar a tela pegou.
- [ ] **Adversários do Americano**: com 8 jogadores alguém pega o mesmo rival 4 dos 7 jogos. Parceiros está perfeito (cada um com cada um, exatamente uma vez); adversário exigiria outro desenho matemático (whist design).

## 🔒 LGPD — exclusão de conta ✅ 28/07 (build-69)
A pessoa exclui a própria conta em `/Auth/ExcluirConta` (link discreto no perfil). A conta é
**anonimizada, não apagada** — e isso não é atalho: das 45 FKs que apontam pra `Jogador`,
`Pagamento.JogadorId` é `ON DELETE CASCADE` (apagar levaria junto o registro fiscal que o MEI
obriga a guardar) e `Dupla.Jogador1Id` é `NO ACTION` (o banco **recusa** apagar quem já jogou).
Além disso o placar de uma partida é dado de quatro pessoas.

Somem: nome, CPF, e-mail, login, telefone, cidade, Instagram, foto (o arquivo sai do disco),
senha e token de recuperação, comentários que escreveu, feedback do site, preferências, avisos
abertos, quem seguia, aparelhos com push e a administração de times. Fica: resultado dos jogos
(como "Jogador removido") e os pagamentos.

Duas travas, ambas sobre **não deixar outras pessoas na mão**: último administrador do sistema,
e organizador único de torneio não finalizado. Verificado em tela ponta a ponta, inclusive
postando direto no servidor com o formulário desabilitado — a recusa aguentou.

## 📋 Backlog consciente (fazer depois)
- Banners/avisos da plataforma
- Fila de denúncias de comentários
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
