# Padelizou — Status e Roadmap

> **Documento vivo.** Atualizar ao fim de cada bloco de trabalho: mover itens de "Próximos" para "Feito" e ajustar prioridades.
> Última atualização: **08/08/2026 (dia do lançamento)** — 🧮 **A TELA DE CRIAR TORNEIO PAROU DE CALCULAR SOZINHA — eram SEIS cópias da mesma regra, e nenhuma ao alcance dos testes.** Os painéis "Vai caber?" e "último jogo do dia" refaziam **em JavaScript** o que os Services já sabiam: grupos de 3 + mata-mata (`PrevisaoDoTorneio`), o que fecha no Americano individual (`DivisaoDoAmericano`), o todos-contra-todos de duplas (`RodadasAmericanoDeDuplas`) e a grade do dia em dois pedaços (`GradeDeJogos`). ⚠️ **O comentário que morava lá admitia o risco com todas as letras** — *"se estas contas divergirem dos Services, a tela mente pro organizador"* — e a cópia era **invisível pra suíte**: os testes exercitam o C#, nenhum lê JavaScript. A divergência só apareceria no dia do torneio, com a quadra reservada e a gente já chamada. Agora existe **`GET /Torneios/Previsao`** (→ `PrevisaoDoTorneio.ParaATela`) e a tela só **DESENHA**: **−251 linhas de aritmética** em `Create.cshtml`. A régua que fica: **aritmética no servidor, formatação no navegador**. ⚠️ **A SEXTA cópia foi achada no caminho, e era a que já estava ERRADA**: `jogosDoTorneio`, que orça o pacote de registro de resultados, aplicava a régua de grupos-de-3 a **QUALQUER** formato — um Americano de 16 pessoas (**60 jogos**) era orçado como chave de 16 duplas (**26**). Agora acompanha o formato escolhido. ⚠️ **Três guardas que não são enfeite**: debounce de 250 ms (senão cada tecla vira requisição), **número de sequência** (a resposta lenta de "5" chegando depois da de "50" repintaria a tela com o número que a pessoa já apagou — erro que só aparece no 3G do clube) e **teto de 256 inscritos NO SERVIDOR**, porque requisição montada à mão não passa pelo `max` do formulário e um número absurdo penduraria a thread. 🧪 **2.650 testes** (130 novos): os de **delegação** comparam a resposta com os Services de origem em vez de números escritos à mão, então **quebram se alguém reimplementar a conta ali dentro** — que é exatamente o erro que se quer impedir de voltar. Conferido no navegador nos três formatos, no número que não fecha e nos avisos verde e vermelho. ✅ **Commitado em `ab3aab9` — NÃO publicado.**
>
> ⚠️ **Dois achados de FERRAMENTA no mesmo trabalho, e os dois custam tempo de quem não souber.** **1.** O `bin/` estava **quebrando o build de todas as sessões**: ao sobrescrever `BaseOutputPath` (que é justamente como sessões paralelas convivem), o SDK **deixa de excluir `bin/**` do glob de conteúdo**, então cada build **copia as pastas das outras sessões pra dentro da sua** e o aninhamento cresce até estourar o limite de **260 caracteres** do Windows. Removidas 227 MB de `bin` aninhadas (saída de build pura — os binários reais ficam fora delas), e `bin/sessao-abas` já estava **sem `Padelizou.dll`** por causa disso. **2.** **O build incremental do Razor mente**: um *"Compilação com êxito"* em **3,9 s** NÃO recompilou a view editada, e o servidor serviu uma página **meio velha, meio nova** (renderizador novo + listener antigo) até um `touch` no `.cshtml` forçar os **12 s** de recompilação de verdade. ⚠️ **Não é cache do navegador** — o Service Worker usa *network-first* pra navegação, então HTML nunca vem de cache online. Sintoma pra reconhecer: build "com êxito" rápido demais e **sem** o aviso `CS8620` das views.
>
> Antes, no mesmo dia — 🔴 **TORNEIO AO VIVO NA MÃO: três publicações com as gurias em quadra (`build-392`, `395` e `396`), todas nascidas de olhar a tela junto com o organizador.**
>
> 🔢 **O PAINEL DIZIA "30 INSCRITOS" NUM AMERICANO DE 10** (`build-395-a6b767e`). A conta da Home somava **linhas de `Duplas`** com **linhas de `InscricoesAmericanas`** — e no Americano individual a tabela `Dupla` guarda uma linha por **PARCERIA DE RODADA**, não por inscrição: as 10 inscritas do "Americano das Gurias do Padel", em 2 grupos de 5, geram 20 parcerias, e 20 + 10 = os 30 anunciados. ⚠️ **É o MESMO buraco que mordeu o Ranking Americano em 07/08** — era a **terceira cópia** da mesma pergunta espalhada pelo código, e a única sem dono. Quem sabe contar inscrito é `Services/QuantosInscritos`: cada formato inscreve uma **unidade diferente** (pessoa no Americano, time na categoria de times, dupla no resto) e a lista de espera fica de fora. ⚠️ **As entidades são carregadas e a conta é feita em memória de propósito**: repetir a régua numa projeção do EF seria escrever a quarta cópia dela. O painel passou a receber o **rótulo pronto** ("10 inscritos", "8 duplas", "3 times") em vez de um número solto com `inscrito(s)` cravado na view — que obrigava a tela a fingir que os três são a mesma coisa; torneio vazio agora diz "ninguém inscrito ainda" em vez de "0 inscrito(s)". ✅ Conferido no navegador antes de subir: o card do Americano local saiu de **58** para **"10 inscritos"**, com teste novo reproduzindo o caso (10 inscrições + 20 parcerias → tem que dizer 10).
>
> 🆚 **O CARD AO VIVO NÃO SE LIA COMO 2 × 2** (`build-396-0a36b82`) — *"parece que não é 2x2, deixe melhor visualmente separando como duplas"*. Os quatro rostos vinham empilhados com o **mesmo espaço entre todos**: pra saber quem joga com quem, o olho tinha que contar de dois em dois e conferir de que lado estava o número. ⚠️ **Não é só estética: o placar fica na altura do PAR**, então quem lia os quatro nomes como uma lista lia o placar errado junto. Agora cada dupla é um bloco (fundo próprio, canto arredondado) e entre elas entra o **×** com uma régua de cada lado — é a régua que faz ele parecer **fronteira** em vez de mais um caractere solto no meio dos nomes. ⚠️ `site.css` está na lista do service worker → `CACHE_NAME` **v11 → v12**, senão quem já instalou o app ficaria com o CSS antigo. ✅ Medido em **375px**: os blocos ficam 17px dentro da borda do card, o × aparece com régua dos dois lados, o placar não vaza e a página não rola de lado.
>
> 🔴 **A TELA DE JOGOS SE RECARREGAVA INTEIRA DE 20 EM 20 SEGUNDOS — e três queixas do Nata Padel Tour eram essa MESMA linha.** O recarregamento existia pra sincronizar os aparelhos que operam o mesmo torneio (a lição de 05/08, três telas discordando). O preço não tinha sido medido: (1) *"o youtube está parando sozinho aqui do nada"* — todo reload **reinicia o `<iframe>`** da transmissão, e o Americano das Gurias tinha câmera nas duas quadras; (2) *"quando eu salvo aqui, ele volta para tela de aovivo"* — a página **renasce na aba padrão**, que é AO VIVO sempre que há jogo em quadra, então quem estava lendo Agendadas era jogado pra outra tela a cada 20s **e** a cada ação salva; (3) *"ao ficar vendo aqui, ele muda sozinho para o aovivo"*. Agora `jogos-ao-vivo-atualiza.js` **busca o HTML novo e troca só os pedaços**: o cabeçalho de cada card (placar, cronômetro, botões) e as abas sem vídeo. ⚠️ **O iframe não é tocado, e não pode ser nem MOVIDO** — reparentar um iframe é o mesmo que recarregá-lo, então a regra é "nada que contenha vídeo é substituído", e o cartão ganhou `data-partida-id` pra casar o novo com o velho. ⚠️ Reload de verdade **só quando muda o CONJUNTO de jogos ao vivo**: aí o cartão mudou mesmo e não há vídeo a preservar. E a **aba escolhida é lembrada por torneio** (`js/jogos-abas.js`, `sessionStorage` — escolha da sessão de trabalho, não preferência permanente). ✅ Medido no navegador: 26s na tela sem reload (marcador em `window` sobreviveu), iframe intacto (`dataset` preservado), e salvar no Controle de Partida voltou em **Agendadas**, não em Ao Vivo.
>
> 🎥 **O LINK DA CÂMERA É DA QUADRA, NÃO DO JOGO** (`Services/TransmissaoDaQuadra`). Ele era gravado em cada partida, e o único jeito de espalhá-lo era o *"usar este link em todos os próximos jogos desta quadra"* — que é uma **fotografia**: aplica no que existe naquele instante. Todo jogo criado depois (rodada seguinte, desempate, fase que o robô monta) e todo jogo que **muda de quadra** chegava sem transmissão. Agora escolher a quadra no Controle de Partida já traz o link dela (com aviso dizendo de onde veio, senão o organizador apaga um valor que apareceu sozinho), e **trocar de quadra pela lista troca a câmera junto**. ⚠️ **Quadra de destino sem câmera fica SEM link**: manter o link da quadra velha faria o público assistir a OUTRA partida achando que era esta — errado calado é pior que vazio. ⚠️ Link **digitado à mão nunca é sobrescrito**. Ordenação total de propósito (vale o que está em quadra agora, senão o que vem, senão o histórico). ✅ Conferido no navegador: jogo movido da Quadra 1 pra 2 assumiu o link da 2, com o aviso *"A transmissão passou a ser a da nova quadra"*.
>
> 📊 **A CLASSIFICAÇÃO DO AMERICANO EXISTE DESDE O SORTEIO** (*"aqui já deveria aparecer a classificação, mesmo sem nenhum jogo, com tudo zerado e com as participantes"*). Ela saía **só das partidas finalizadas**: a aba abria *"ninguém pontuou ainda"* justamente no começo do torneio, que é quando mais gente entra pra procurar o próprio nome. Agora são **duas perguntas, duas listas**: quem APARECE sai da grade inteira; quem PONTUOU, só do que terminou. ⚠️ Quem ainda não jogou fica no fim, **sem medalha, sem verde e sem "empatado"** — senão a tela anunciaria classificado por ordem alfabética antes da bola rolar, e o alerta de desempate apareceria num torneio que não começou. Vale pro individual e pro de duplas. ✅ Conferido no Americano de 10: as 4 que jogaram no topo, as 6 restantes em 0/0.
>
> ⏱️ **A HORA EM QUE O JOGO ACABOU passa a ser carimbada pelas DUAS telas** (`Services/EncerramentoDaPartida`) — *"a ordem das finalizadas tem que ser por qual terminou por último vem primeiro"*. Só a tela cheia gravava `HorarioFimReal`; encerrando pelo **botão do card AO VIVO**, que é a tela do dia do torneio, ele ficava **nulo** — e a lista de Finalizadas, que ordena por ele, caía no desempate por Id: num torneio "por ordem de liberação" isso é a ordem do **sorteio**, não a de quem terminou. É o defeito de sempre (regra escrita no chamador, e uma das telas não tem). ⚠️ Correção de placar antigo **não** reescreve a hora: um jogo de ontem pularia pro topo. ⚠️ **Os 3 jogos já finalizados do torneio 23 continuam sem esse carimbo** — não dá pra inventar a hora deles.
>
> 🔁 **AMERICANO SORTEADO DAQUI PRA FRENTE INTERCALA AS RODADAS DOS GRUPOS** (`Services/FilaDoAmericano`): *"a próxima rodada é de quem ficou mais tempo parada"*. O sorteio monta um grupo por inteiro, então os jogos nasciam A1..A5 e só depois B1..B5 — o grupo B só entrava em quadra quando o A tivesse **terminado o torneio dele**, e como a grade de horários é posicional as duas primeiras faixas iam pras rodadas **1 e 2 do MESMO grupo**: as mesmas pessoas marcadas em duas quadras ao mesmo tempo. Agora sai rodada 1 de todos os grupos, depois a 2, depois a 3 — e o **número da rodada não é reescrito**, ele já vem do sorteio e passa a ser também a ordem de leitura. ⚠️ **Torneio já sorteado NÃO muda**, e a ordem da lista do torneio em andamento ficou como estava (pedido explícito do Felipe: *"não mexa mais na ordem"* — eu tinha proposto ordenar por rodada e ele preferiu não mexer com o torneio rolando).
>
> 🚀 **Publicado em produção no meio do torneio, e vale saber como**: o `deploy.yml` é **`workflow_dispatch`** — o push **não** publica sozinho, o CI só empacota. Com as jogadoras em quadra, os três deploys foram disparados por SSH com o mesmo comando do workflow (`/opt/padelizou-deploy/deploy.sh prod <build>`). ✅ Conferido no ar a cada um: `/js/jogos-abas.js` 200 (era 404) e a página do torneio 23 servindo os dois scripts com `?v=` novo no `392`; o `sw.js` de produção respondendo `padelizou-static-v12` e o `site.css` já com as regras do `pdz-live-versus` no `396`; `healthz` 200, serviço `active` e **0 reinício** nos três. **2.635 testes.**
>
> 💰 **O NATA PADEL TOUR PASSOU A COBRAR PELO SITE DE VERDADE** (`build-399-1bcb9c2` no ar em dev e prod). O **Lucas "Foka" criou a conta dele no Asaas** e conectou — decisão do Felipe, depois de eu levantar que pôr ele como Criador **redirecionaria o dinheiro**: quem é "Criador" é quem RECEBE (`ObterRecebedorTorneioAsync`), então a troca faria os R$ 120 por inscrito caírem na conta do Felipe e tiraria o Lucas do painel de dinheiro do próprio torneio. Conferido no lado público: sumiram o *"pagamento combinado com o organizador"* e o bloco *"Pix do organizador"* (os dois só existem quando não cobra pelo site), e o preço aparece limpo em R$ 120,00 porque a taxa sai da fatia do organizador (`ModoComissao = "Descontada"`). ⚠️ **A prova de que a carteira do Lucas está conectada é a própria troca ter passado**: o servidor recusa "pelo site" se a conta de quem recebe não estiver ligada. ✅ **CONFERIDO NO SYSTEMD: o Asaas de produção É de produção** — `ApiKey` começa com `$aact_prod_` e `BaseUrl = https://api.asaas.com/v3`; o **dev está em sandbox** (`$aact_hml…` + `sandbox.asaas.com/api/v3`). A separação está certa e a cobrança do torneio nasce de verdade. ⚠️ **O `appsettings.json` traz o sandbox como PADRÃO**, então serviço novo nasce apontando pro lugar errado: a verdade mora no systemd, nunca no repo. 🔐 **Achado de segurança de passagem:** ao ler o `Environment=` eu mascarei só a `ApiKey` e o **`Asaas__WebhookToken` de produção saiu em claro na conversa**. Ele não move dinheiro, mas autentica o Asaas chamando `/Pagamentos/Webhook` — quem o tiver forja "pagamento confirmado" e marca inscrição como paga. **Recomendado trocar**, virando os dois lados juntos (systemd + painel do Asaas).
>
> 🗣️ **"CHAVES EM SORTEIO" PAROU DE MENTIR NA TELA.** O valor gravado descreve o **próximo passo** (sortear), não o estado atual — nada está sendo sorteado, e o torneio pode ficar semanas assim. No NATA PADEL TOUR ele aparecia como "CHAVES EM SORTEIO" **logo acima da faixa dizendo "as inscrições estão fechadas"**: duas frases sobre o mesmo estado, e a de cima era a errada. Agora se lê **"Inscrições Fechadas"** (`Services/StatusDoTorneioNaTela`). ⚠️ **Traduz só a EXIBIÇÃO** — o valor gravado continua `"Chaves em Sorteio"`, porque renomear a coluna exigiria migração e mexer nas **~25 comparações de string** espalhadas (vitrine, trava de inscrição, chaveamento, consulta), e cada uma é um lugar onde esquecer uma quebraria o torneio calado. Tem teste amarrando que a constante gravada **não** muda junto.
>
> 🖼️ **A CAPA DO TORNEIO APARECE INTEIRA NO CARD** (queixa do Felipe: *"o folder sempre fica cortado"*). Era `object-fit: cover`, que preenche a caixa **cortando o que sobra** — e num folder de torneio o que sobra é a informação: no "Americano das Gurias" sumiam a faixa de cima e o troféu de baixo; no NATA PADEL TOUR, as pontas do escudo. Agora a arte cabe inteira (`contain`) **sobre uma cópia dela mesma borrada e ampliada** — o fundo borrado existe pra que a sobra não vire duas tarjas lisas, e a cor sai da própria arte (serve pra folder deitado, quadrado ou vertical). ⚠️ O `scale(1.2)` no fundo esconde a borda transparente que o blur cria nas beiradas, senão aparece um halo claro em volta. **Medido no navegador**: capa de 1600×1200 numa caixa de 355×160 passa a ser pintada em **213×160, inteira** — com `cover` ela sairia 355×266 e perderia **106px de altura**. `CACHE_NAME` → **v13**.
>
> 📋 **E a ordem das seções da vitrine mudou** (decisão do Felipe): **Em Andamento → Inscrições Abertas → Em breve → Finalizados**. O que está acontecendo agora é o que mais gente abre a página pra ver; inscrição é decisão de uma vez só, torneio rolando é tela que se volta a ver várias vezes no mesmo dia.
>
> 🔓 **ENCERRAR INSCRIÇÃO DEIXOU DE SER CAMINHO SÓ DE IDA — e o caso era real, na tela.** O **NATA PADEL TOUR** (id 22 de produção, do Lucas "Foka") estava parado em **"Chaves em Sorteio" com NINGUÉM INSCRITO**: inscrições encerradas antes de a primeira pessoa entrar, e **sem botão pra desfazer**. A única saída era apagar o torneio e criar outro, perdendo o link já compartilhado. Agora existe **"Abrir inscrições"**, e a régua mora em `Services/PortaDaInscricao`. ⚠️ **Sem coluna nova, de propósito**: "Chaves em Sorteio" JÁ significa "inscrição fechada" — é isso que o formulário público consulta. Uma flag `InscricoesAbertas` ao lado do Status criaria duas respostas pra mesma pergunta e um dia elas discordariam. ⚠️ **A trava é o SORTEIO, e ele é PARTIDA existindo** — não o nome da fase: com a chave publicada tem gente sabendo contra quem joga, e aceitar mais uma dupla aí não é reabrir inscrição, é refazer o torneio por baixo de quem já se organizou. ⚠️ **Reabrir NÃO manda aviso pra base**: quem avisa é a aprovação do torneio, e cada clique de fechar/abrir viraria mais um push pelo mesmo evento. 🗣️ **De quebra, a tela parou de mentir**: torneio fechado com zero inscritos dizia *"Inscrições Encerradas! Agora você pode sortear os grupos"* — a frase mais confusa possível pra quem tenta entender por que ninguém consegue entrar; agora diz "as inscrições estão fechadas / ninguém se inscreveu ainda", e o convite pro sorteio some enquanto não há quem sortear.
>
> 🕓 **E DÁ PRA CRIAR O TORNEIO JÁ FECHADO, com a seção "EM BREVE" na vitrine** (decisão do Felipe). Quem monta em duas sentadas era obrigado a deixar o formulário aceitando gente enquanto ainda mexia em categoria, quadra e preço. Caixa **"Já quero receber inscrições"** na criação, **marcada por padrão** — nasce aberto porque criar pra anunciar agora é a maioria, e inverter faria o esquecimento ser **silencioso** (ninguém se inscreve e nada explica por quê). Na vitrine ele ganha seção própria: não é "aberto" nem "em andamento", e sem ela era anunciado como acontecendo. ⚠️ `NuncaAbriu` exige o status FECHADA, e não só "não está aberto" — cancelado e finalizado também não estão abertos e, vazios, apareceriam como "em breve". ⚠️ **A ordem do hidden do checkbox importava e eu errei primeiro**: com o hidden ANTES, marcar mandava `false,true` e o binder de bool fica com o primeiro valor — a caixa fazia o **contrário** do que dizia. Achado **medindo no navegador**, não deduzido. **2.627 testes.**
>
> 💳 **O NATA PADEL VAI COBRAR PELO SITE, COM PIX E BOLETO** — decisão do Felipe. Na prática é a opção **"Todas as formas"**: não existe "Pix + boleto sem cartão", e não precisou existir, porque o boleto **já paga a taxa do Pix** (10%, decisão de 29/07 — pro meio de pagamento os dois custam o mesmo em centavos; o que encarece é o cartão, que fica em 15%). O preço de usar "Todas as formas" é que o cartão aparece junto pro jogador — e cartão só cai em **32 dias**. Formato **Oficial**, então a taxa vale (o Americano é isento nos três modos desde 07/08).
>
> 🔁 **A FORMA DE RECEBIMENTO PASSOU A SER TROCÁVEL — enquanto não há inscrito** (pedido do Felipe). Ela era escolhida na criação e valia pra sempre: aparecia num rádio da tela de criar e **em lugar nenhum da tela de gerenciar**. Quem clicasse errado — ou quem só depois conectasse a conta de recebimento — tinha uma saída só: **apagar o torneio e criar outro**, perdendo o link já compartilhado, a capa, as categorias, as quadras e os horários. ⚠️ **A trava certa é INSCRITO, e não "torneio recém-criado" nem "inscrições abertas"**: a forma é as duas coisas ao mesmo tempo — é o que o jogador LEU antes de entrar e é o que fixa o split no instante em que a cobrança nasce. Trocar com gente dentro mexeria nas duas pontas de quem já pagou; no caminho pior (site → por fora) o torneio passaria a dizer *"acerte o Pix com o organizador"* pra quem está com a vaga **confirmada e paga**. Com zero inscrito não há nada disso: nenhuma cobrança criada, nenhuma vaga vendida, e a taxa do "por fora" com base zero. ⚠️ **A pergunta "tem alguém inscrito?" mora num lugar só** (`TemAlguemInscritoAsync`), lida pela TELA e pelo POST — divergindo, a tela ofereceria o que o servidor nega. ⚠️ **Ela olha as DUAS tabelas de inscrição**, porque cada formato grava na sua (`Duplas` no Oficial e no Americano de Duplas, `InscricoesAmericanas` no Americano individual): contar só a primeira destravaria **todo Americano individual com gente dentro** — o mesmo buraco que zerou o Ranking Americano de duplas em 07/08. ⚠️ E na query é `NomeTime == null`, **não** `!d.EhTime`: `EhTime` é `[NotMapped]` e o EF não traduz propriedade calculada — a consulta estouraria em tempo de execução. Time **não** trava (é cadastrado pelo organizador e não paga pelo sistema, a mesma exceção de `TaxaDoTorneioExterno`). ⚠️ **Trocar PARA "pelo site" sem conta conectada é recusado no servidor**, igual na criação: a tela trava os rádios, mas quem manda o formulário na mão passaria direto pra armadilha cara — torneio rodando, inscrições entrando e **nenhuma cobrança nascendo**, calado. Os três nomes gravados em `FormaPagamento` viraram `Services/FormaDePagamentoDoTorneio`, com `Torneio.CobraPeloSite` delegando pra lá (eram duas escritas da mesma regra). **Verificado no navegador**, no banco local: torneio 27 ("Teste Clube Escrito 07-08", 0 inscritos) saiu de `Externo` e virou `OnlineTodas` clicando e salvando, e o checkout dele passou a oferecer **Pix 10% · Cartão 15% · Boleto 10%**; o torneio 20 (2 duplas) mostra o selo **"travado"** e nenhum rádio. 0 erro no log.
>
> 📅 **E DE PASSAGEM, O ACHADO QUE VALIA A CONFERIDA: O BOLETO VENCIA AMANHÃ.** O vencimento era um `DateTime.Today.AddDays(1)` cravado, **igual pros três meios de pagamento**. ⚠️ Boleto leva **1 dia útil só pra COMPENSAR** depois de pago: emitido numa sexta com vencimento no sábado, ele **já nasce morto** — e vencido o Asaas manda `PAYMENT_OVERDUE`, o webhook grava **"Cancelado"** e a inscrição nunca acontece. Esse é o caminho **NORMAL** do boleto, não o excepcional. ⚠️ **A tela já prometia outra coisa** (*"vence em alguns dias"*, em `CobrancaDoTorneio.ExplicacaoDaEscolha`): era o texto que estava certo e o código que estava errado. Agora **3 dias** — o mesmo prazo que a taxa do "por fora" e a mensalidade do professor já usavam no mesmo arquivo, o boleto era o único que destoava. Pix e cartão seguem em 1 dia **de propósito**. ✅ **O resto do caminho do boleto foi conferido e está certo**: `PAYMENT_RECEIVED` já é tratado junto com `PAYMENT_CONFIRMED`, e **nada cancela pagamento pendente por `ExpiraEm`** (os 60 minutos são só texto de tela) — boleto pago dois dias depois entra normal. **2.602 testes** (11 novos). ⚠️ **Commitado e NÃO publicado**: falta subir pra dev/prod.
>
> ⛔ **O QUE FALTA PRO NATA PADEL, E DEPENDE DO FELIPE:** (1) **conferir se o Asaas de produção está com chave de PRODUÇÃO e não de sandbox** — `appsettings.json` traz `sandbox.asaas.com` como padrão e a chave real vem do systemd; **não consegui checar o VPS nesta sessão** (o acesso foi negado), e é isso que separa "cobrança de verdade" de "cobrança de mentira"; (2) **a conta de recebimento do organizador do Nata Padel** (`ReceberPagamentoOnline` + `AsaasWalletId`) — sem ela o "pelo site" é recusado; instrução padrão desde 07/08 é **criar por fora no provedor e colar o código**, pra não gastar as 10 subcontas/60 dias; (3) **liberar o organizador dele** em `/Admin/Organizadores` **ANTES** de mandar o link (o Oficial exige perfil); (4) **aprovar o torneio** em `/Admin/TorneiosParaAprovar` pra ele aparecer na vitrine.
>
> ⚠️⚠️ **LEIA ISTO PRIMEIRO: o `build-377` levou a CONTAGEM POR SOMA pra PRODUÇÃO, e a entrada abaixo dizia que ela ficaria só em dev até o Felipe testar.** Aconteceu porque o Felipe pediu "sobe tudo pra dev e prod" e o `main` já carregava o commit dela (`5d05e32`) — publicar o `main` publica tudo que está nele. **Risco baixo e medido**: a opção é por torneio e nasce em `"Ate"`, então nenhum torneio existente mudou de comportamento (conferido em prod: os 2 torneios com `ContagemDeGames = 'Ate'`). O que se perdeu foi a ordem de teste, não a segurança. Voltar é `rollback.sh prod`.
>
> 🛡️ **Ainda em 08/08/2026 — O AMERICANO PAROU DE PONTUAR TIME, e a aba "Chaves e Grupos" dele deixou de abrir vazia.** Os três pedidos vieram do **Americano das Gurias do Padel**, o primeiro Americano real de produção, olhando a tela dele. ⚠️ **O placar de times daquele torneio (120 × 120 × 80) media RODADAS, não vitórias:** no Americano cada rodada cria uma dupla nova, cada dupla vale 10 de "participou", e 4 rodadas × 10 = 40 por jogadora — 3 jogadoras = 120. Ninguém tinha ganho nada. **É o MESMO buraco que fechou pro ranking oficial em 07/08 e que sobrou em dois lugares**, porque as duas consultas de time reescreveram a régua à mão em vez de chamar `ContaNoRanking`: a aba do torneio (não filtrava nada) e ⚠️ **o ranking GERAL de times em `/Ranking`, que filtrava só `!Restrito`** — esse é o grave, porque é ranking de verdade e não placar de um evento. Medido no banco local: o Americano respondia por **1110 dos 1960 pontos** do time líder (57%) e **470 de 880** do segundo. Agora a régua tem duas escritas lado a lado — `ContaNoRanking` (entidade) e `DuplaContaNoRanking` (expressão que o EF traduz pra SQL) — **com teste comparando uma com a outra**, que é o que impede a próxima consulta de copiar metade de novo. A aba Times **some** no Americano: tela que só responderia zero não se oferece. 📊 **E a aba "Chaves e Grupos" ganhou a classificação:** o Americano não cria `GrupoTorneio`, então o laço das categorias não achava nada pra desenhar e a aba abria **vazia, sem dizer por quê**, num torneio inteiro em andamento. ⚠️ **Ao ir escrevê-la apareceu a segunda cópia, e ela estava ERRADA: a sub-aba "Classificação" dentro de Jogos somava a CATEGORIA INTEIRA** — num Americano de 10 (2 grupos de 5) ela ordenava as dez por uma soma que nunca aconteceu, enquanto a página avulsa, do mesmo torneio, mostrava as duas tabelas certas. Duas classificações na mesma tela. ⚠️ **E ela calculava em cima da lista JÁ FILTRADA da tela de jogos**: marcar "só meus jogos" reescrevia a classificação do torneio. Agora existe **um** motor (`Services/ClassificacaoDoAmericano`) e **uma** marcação (`_ClassificacaoDoAmericano.cshtml`), lidos pelas três telas — a consulta é do torneio inteiro, e o filtro do que entra na conta mora no motor, não em cada tela. 🟩 **Quem classifica vai de VERDE**, o mesmo `table-success` da classificação dos torneios de chave (pedido do Felipe), com o corte vindo de `Categoria.PassamPorGrupo` e **nunca cravado na view** — e o verde só existe onde há corte de verdade: no grupo único ninguém "passa", todos já estão no que decide. Junto, o Bootstrap pinta essas variantes num pastel de fundo claro que no tema escuro vira bloco berrante com texto preto: `.table-success`/`.table-warning` viraram **tinta com faixa lateral** no escuro (`site.css`, `CACHE_NAME` → **v9**). ⚠️ O empate que oferece "montar o desempate" passou a ser o da tabela que **DECIDE** — empate no topo do grupo A não é empate de título, os dois passam igual. Conferido no navegador (banco local, torneio de 2 grupos de 5 + grupo final): aba Times ausente, Grupo A/B com selo "passam 2" e os dois primeiros em verde, grupo final com "decide o título", as duas telas mostrando a MESMA tabela, `/Ranking` batendo nos 850/410/40 do SQL — e o torneio de chave intacto, com aba Times e chave desenhada. **2.566 testes.**
>
> 🚚 **`deploy.sh`: a versão passa a ser montada AO LADO, e nunca mais se apaga a pasta que está no ar.** O start do dev morreu com *"static resources manifest não encontrado"* e a minha primeira explicação estava ERRADA — eu disse "o symlink virou antes de terminar de desempacotar", mas o `.historico` mostrou o mesmo build **duas vezes no mesmo minuto**: eram **dois deploys simultâneos**, e o `rm -rf "$DESTINO"` da seção 2 apagava a pasta que o outro tinha acabado de pôr em uso, com o processo lendo dela. **Uma espera cega não teria evitado.** Três mudanças: **cadeado** (`flock` por ambiente), **pasta de montagem** (`.montando-<tag>-<pid>` que entra em cena com um `mv`; reinstalar a versão que está no ar cria `<tag>+HHMMSS` em vez de apagar a que roda) e a **espera POR CONDIÇÃO** antes de trocar o symlink — confere que `Padelizou.dll`, o `runtimeconfig`, o **manifesto de estáticos** e o `appsettings` estão lá, e dá `sync`. ⚠️ `sleep 5` seria chute: às vezes curto demais, sempre lento demais. **Testado no VPS reinstalando no dev a mesma versão que estava no ar** (o caso que quebrava): a pasta antiga sobreviveu, a nova entrou como `build-366-9d2651e+201930`, start sem uma exceção. E ganhou **prova de campo no mesmo dia** — a sessão paralela publicou o `build-376` duas vezes e a pasta em uso virou `build-376-5d05e32+033438`, intacta. Nos dois deploys de hoje: **0 reinício do systemd**.
>
> 💸 **"QUANDO EU BOTO PRA EDITAR, SAI O VALOR DA INSCRIÇÃO" — a outra metade do bug de dinheiro, em SETE campos.** ⚠️ `value="150,00"` num `<input type="number">` é valor **inválido**, e o navegador não reclama: mostra o campo **VAZIO**. Com a cultura pt-BR do app, `@Model.PrecoInscricao` escreve exatamente isso. Provado no navegador (`.value` vem `""` com vírgula e `"150.00"` com ponto). A tela de **criação nunca sofreu** porque usa `asp-for`, que já escreve invariante — por isso o defeito só aparecia na edição, e parecia coisa do torneio. Eram sete: preço da inscrição, mensalidade e avulso do grupo, os três preços e o custo do local do professor, e o preço do horário do clube (esse com `ToString("0.00")`, que também segue a cultura). A regra virou **`DinheiroNoCampo`, colado no `DinheiroModelBinder`** — as duas metades de "dinheiro atravessa `type=number`": a entrada já tinha dono desde o `79.90`, a saída não tinha. ⚠️ O teste **fixa a cultura pt-BR dentro dele**: em máquina invariante passaria sem provar nada, que é como isso chegou em produção.
>
> ⏱️ **LIGAR "por ordem de liberação" agora APAGA o horário que já estava marcado** — e isso saiu de um torneio real no ar. A chave só evitava marcar horário no sorteio SEGUINTE; quem sorteou antes e ligou depois ficava com a tela se contradizendo. No **"Americano das Gurias do Padel"** (id 23, da Caroline) eram **10 jogos anunciando 00:54 às 04:14 da MADRUGADA** pras jogadoras. Os 10 foram limpos direto no banco de produção (transação, todos `Agendada`, nenhum começou, nenhuma quadra atribuída → nada de real a preservar). ⚠️ No código, só o que **ainda não começou**: jogo em quadra ou finalizado tem hora REAL, que é registro do que aconteceu. ⚠️ E a limpeza é da **TRANSIÇÃO** desligado→ligado, não de toda gravação — senão um torneio já por ordem perderia horário posto na mão. Junto, o botão **"Recalcular horários" some** nesses torneios: o servidor já recusava o clique, mas só DEPOIS dele, e a recusa voltava como faixa vermelha — era assim que o organizador descobria a regra.
>
> 📱 **As abas de jogos ganharam uma barra em volta, e a Classificação parou de ficar fora da tela.** Duas queixas do Felipe, uma causa: as pills flutuavam soltas ("parecem voando") porque nada reunia o grupo. Medido na página real de produção em 375px: **594px de conteúdo numa caixa de 306px**, com "Finalizadas" e "Classificação" **inteiramente fora do quadro**. ⚠️ Ninguém arrasta o que não sabe que existe — a classificação do Americano, que é o **placar do torneio**, simplesmente não era encontrada no celular. Saiu a rolagem lateral, entrou quebra de linha dentro da barra. Cada aba fica do tamanho do próprio texto (esticar deixava a última sozinha ocupando a largura toda, parecendo botão de ação); no celular estreito o ícone sai e o nome, a contagem e a bolinha do Ao Vivo ficam. `site.css` está na lista do service worker → `CACHE_NAME` subiu.
>
> 🧹 **A gestão parou de OFERECER categoria de times** (pedido do Felipe): o formulário e os avisos ocupavam um cartão inteiro em **todo** torneio comum, por um recurso que quase ninguém usa. ⚠️ Quem **já tem** categoria de times continua vendo o cartão e o botão de gerenciar — sumir com ele deixaria os times cadastrados sem porta de entrada. O endpoint `AdicionarCategoriaDeTimes` segue no servidor, com `[Authorize]` e checagem de dono.
>
> ✅ **Verificação do `build-377` nos dois ambientes**: serviços `active`, healthz 200, **0 reinício**, 0 erro desde o start, as duas migrations novas na história dos dois bancos, `ContagemDeGames` nascendo `Ate` (16 torneios em dev, 2 em prod), **78 jogadores** e o pagamento real intactos, `pg_dump` antes em `/opt/padelizou-shared/backup-prod-antes-build-377-20260808-033653.sql.gz`. ⚠️ **O dev voltou a ficar atrás do portão no meio da tarde**, então lá a conferência é só serviço/healthz/log — as telas eu confiro em produção, que está aberta.
>
> Antes, no mesmo dia — 🔢 **OS GAMES PODEM SER CONTADOS POR SOMA, e não só "até".** Pedido do Felipe: *"nosso atual é 'até' x games, ter a opção de soma de games — soma de 7, serão jogados 7 games no total"*. Vale nos **três formatos** (Oficial e os dois Americanos), porque rodízio de games fixos é justamente o mais comum no Americano — e é o que segura a grade no horário, já que todo jogo dura o mesmo. O mesmo número passa a significar duas coisas, então a coluna nova (`Torneio.ContagemDeGames`) guarda o **significado**, não outro número: `"Ate"` = quem chegar primeiro em X vence (com o vencer-por-dois de sempre); `"Soma"` = jogam-se X games no total e vence quem fizer mais (4x3, 5x2 e 7x0 fecham a mesma soma de 7). ⚠️ **Na soma o teto é POR LADO e depende do adversário** — com o outro em 3 numa soma de 7, o meu máximo é 4. O "até" nunca precisou disso porque lá o teto é simétrico, e era por isso que um número só bastava; agora quem chama pede `TetoDoLado`, e o clamp dos dois placares junto virou `PlacarValido` dentro do serviço — **limitar os dois lados pelo mesmo teto deixaria passar 5x5 numa soma de 7**, porque 5 "cabe" nas duas contas feitas em separado. ⚠️ **A segunda cópia foi procurada ANTES de mexer na primeira**, que é a regra deste repo: `mesa-offline.js` espelha a conta e **recebe a contagem do servidor** em vez de decidir sozinha — foi assim que o `limiteGames: 9` cravado sobreviveu tanto tempo. ⚠️ **Soma PAR alcança o empate** (8 fecha em 4x4) e o sistema recusa finalizar jogo empatado de propósito (`QuemVenceu`: *"no padel alguém tem que fechar o jogo"*); a tela **avisa e recomenda ímpar**, sem proibir — grupo e Americano convivem com empate no meio do caminho, quem não convive é o mata-mata. ⚠️ A migração grava `defaultValue: "Ate"` e **não** o `""` que o EF gera do default de string: todo torneio existente sempre contou no "até", e a coluna tem que dizer isso (mesma armadilha do `defaultValue` de `CategoriaPadrao.Ativa`). **Editável depois de criado** (aba Gerenciar), porque no Americano só se sabe o tamanho certo da rodada quando se sabe quanta gente veio. **2.550 testes** (14 novos). ✅ **No ar em DEV pelo `build-376-5d05e32`** — migração aplicada com `DEFAULT 'Ate'`, 0 erro. 🧪 **Verificado na Mesa DE VERDADE, clicando** (não só nos testes): num Americano de 10 em soma de 7, cinco toques num lado dão 5x0 e cinco no outro **param em 2** (teto 7−5), fechando exatamente 7; trocado pra "até 9", os dois lados chegam a 5x5 — a regressão que importava. ✅ **E EM PRODUÇÃO pelo `build-379-bb109a1`** — migração registrada, os 3 torneios existentes ficaram em `Ate` (a regra de sempre preservada, como a migração prometia), 88 jogadores intactos, 0 erro, `pg_dump` antes em `/opt/padelizou-shared/backup-prod-antes-soma-20260808-034754.sql.gz`. 🎉 **E o torneio da Carol destravou de verdade**: o id 23 saiu de "Chaves em Sorteio" com zero partidas e está em **"Fase de Grupos" com as 10 partidas criadas** — ela sorteou sozinha, sem ninguém mexer em nada, que era o efeito prometido da correção. 📐 **Sobre a ORDEM DOS JOGOS** (pedido do mesmo dia: *"que não seja tão seguido e nem tão espaçado"*): fui mexer no encaixe e **MEDI DUAS TENTATIVAS PIORES** que o que já existia, então o sorteio ficou como estava e o que sobrou foi a medição (`DescansoNaGradeTests`). ⚠️ **Reordenar a fila PIORA**: ela já chega em ordem de rodada (o `RodadasAmericano` monta rodadas em que cada um joga uma vez) e o guloso de primeira vaga preserva essa ordem, que é quase ótima — reordenar por "quem descansou mais" empurra os cansados pro fim, onde só sobram eles (num Americano de 16 em 2 quadras a pior sequência subiu de **2 pra 4** e as emendas de **53 pra 78**; a segunda tentativa, mais conservadora, também deu 78). ⚠️ **E "ninguém joga duas seguidas" é IMPOSSÍVEL na maioria dos tamanhos, por aritmética**: 8 pessoas em 2 quadras enchem a rodada inteira (todos jogam sempre), e 12 pessoas em 2 quadras dão 11 jogos por pessoa em 17 rodadas. 💡 **O que MANDA é o alinhamento rodada × quadras**: 16 pessoas em **2** quadras (rodada de 4 jogos = 2 slots exatos) dá pior sequência **2**; as mesmas 16 em **3** quadras desalinham e pioram. O organizador ganha mais escolhendo o número de quadras do que qualquer reordenação daria — candidato a virar aviso na tela de criação. 🎯 **Cópia de teste esperando por ele em dev**: torneio **id 22, "TESTE Felipe — Americano soma de 7"**, oculto + restrito (chave `somateste`), grátis, 10 inscritas, em "Chaves em Sorteio" com 0 partidas — pronto pra sortear e rodar. **Feito em DEV e não em produção de propósito**: dev já é invisível pra todo mundo atrás do portão, e a alternativa exigiria inventar 10 jogadores falsos no banco real.
>
> Antes, em 07/08 — 💸 **COBRAMOS ERRADO DE UMA USUÁRIA REAL, e ela chegou a mandar o dinheiro.** O Felipe viu no torneio da amiga: *"o americano cobrou 5%, era pra ser 5 reais por pessoa"*. Estava certo, e o defeito era **meu**, de horas antes. A isenção que eu tinha subido valia só pro Americano **SEM** Ranking Americano — então quem **COMPRAVA** o ranking passava a dever as **duas** coisas: os R$ 5 por pessoa (`PontosDoAmericano`, acertados por fora em `/Admin/RankingAmericano`) **E** o percentual sobre cada inscrição. Duas cobranças pela mesma coisa; comprar ponto não é motivo pra passar a dever comissão. ⚠️ **A régua certa é o FORMATO, não a caixinha do ranking**: `IsentoDeTaxa` virou `EhAmericano(Formato)`, ponto — e com isso `TaxaDoTorneioExterno.SeAplica` e o percentual do split zeram pros dois sabores do Americano. 📊 **O estrago, medido no banco de PRODUÇÃO** (torneio id 23, "Americano das Gurias do Padel", da Caroline Souza): 10 inscritas × R$ 13,00 × 5% = **R$ 6,50**, e o `Pagamento id 4` estava em **`AguardandoConfirmacao` por Pix direto** — ou seja, **ela clicou "já fiz o Pix" e o dinheiro caiu na nossa conta** por uma taxa que não existe. O correto era 10 × R$ 5,00 = **R$ 50,00** de ranking, por fora, no fim do torneio. ⚠️ **E não parou no dinheiro: a taxa em aberto TRAVA as chaves** — o torneio dela estava em "Chaves em Sorteio" com **ZERO partidas geradas**, parado. A correção destrava sozinha (`ChavesLiberadas` volta a ser `true`), sem ninguém mexer em nada. 🧪 **Os dois testes que eu tinha escrito afirmavam a regra errada EM VOZ ALTA** (`Americano_que_compra_ranking_paga_a_taxa_normal`) — foram invertidos com o caso da Carol escrito dentro, que é a lição: teste não protege de premissa errada, só congela a que você tinha. **2.523 testes.** Verificado no navegador local reproduzindo o cenário exato dela — Americano + "por fora" + ranking marcado mostra R$ 0,00 e a organizadora recebe os R$ 13 inteiros, enquanto o **Oficial "por fora" segue cobrando os 5%** (regressão conferida). ✅ **NO AR EM DEV (`build-372-d988757`) E EM PRODUÇÃO (`build-373-2a080d9`)** — serviços `active`, 0 erro no start, 78 jogadores intactos, `pg_dump` antes em `/opt/padelizou-shared/backup-prod-antes-build-372-20260808-023355.sql.gz` (conferido: 78 jogadores no dump = 78 ao vivo).
>
> 🩹 **E o rescaldo no torneio dela, decidido pelo Felipe: o primeiro é cortesia.** Três acertos no banco de produção, todos no torneio 23: (1) o `Pagamento id 4` dos R$ 6,50 foi para **`Cancelado`** — e **NÃO** para "Confirmado", porque confirmar registraria como receita nossa uma cobrança que não deveria existir; (2) o torneio **continua valendo ponto** no Ranking Americano; (3) `RankingAmericanoPagoEm` foi carimbado **sem dinheiro nenhum ter entrado** — cortesia consciente, já que este primeiro é teste e não vai ser cobrado. ⚠️ **O passo (3) não é enfeite: sem ele o torneio JAMAIS apareceria no ranking** — a régua são QUATRO condições (contratado · **pago** · ≥8 pessoas · finalizado), e faltando uma ele some calado. ⚠️ **A trava das chaves caiu sozinha pela correção**, sem precisar marcar taxa como paga: `SeAplica` virou `false` pro Americano, então `ChavesLiberadas` devolve `true` — por isso `TaxaExternoPagaEm` segue **em branco**, que é o registro honesto (ninguém pagou nada). Conferido que o número dela fecha: **10 inscritas = 2 grupos de 5, passam 2, grupo final de 4 — 13 jogos**, caso já coberto por teste; e 10 ≥ 8, então pontua. 128 testes do caminho inteiro (divisão, grupos, ranking, cobrança e trava) rodados de novo, verdes.
>
> Antes, no mesmo dia — 🔀 **O RANKING AMERICANO VIROU DOIS (individual × em duplas) — e o pedido era de tela, mas o formato de duplas estava fora do ranking por DOIS caminhos, nenhum deles com erro visível.** ⚠️ **1. Ele nunca era contado**: a contagem de pessoas olhava só `InscricaoAmericana`, que é onde o formato INDIVIDUAL grava — o de duplas grava em `Dupla`, porque herdou o caminho de inscrição do Padrão. Zero pessoas = abaixo do piso de 8 = *"não pontua e não se cobra"*, calado, **e isso valia também pra tela de acerto de R$ 5 do admin** (um Americano de duplas contratado apareceria lá valendo R$ 0,00). ⚠️ **2. Os dois parceiros levavam pontos DIFERENTES pelo mesmo resultado**: a colocação vinha da tabela POR PESSOA, e como a dupla é fixa os dois jogam exatamente as mesmas partidas — as somas empatam, o desempate por Id os separa, e a dupla campeã saía com **um em 1º (100) e outro em 2º (60)**. Agora a colocação é da DUPLA e os dois levam o mesmo, como o título do mata-mata (`TabelaDoAmericanoDeDuplas` no lugar da individual). A contagem virou **`Services/PessoasDoAmericano`, um lugar só**, porque ranking e acerto fazem a MESMA pergunta — separados, cobraríamos por um tamanho e pontuaríamos por outro. ⚠️ **E ela conta POR FORMATO, nunca somando as duas tabelas**: no Americano individual a tabela `Dupla` também tem linhas — **uma por PARCERIA DE RODADA** —, então somar contaria a mesma pessoa uma vez por rodada jogada e o peso do ponto explodiria em silêncio (erro meu, pego antes de virar teste verde). Na tela, duas sub-abas com o significado escrito em cima: no individual o resultado é seu; em duplas metade do mérito é do parceiro que você escolheu — numa lista só, os dois números pareceriam a mesma medida. **2.503 testes.** ✅ **No ar em dev e prod pelo `build-366-9d2651e`** (72 jogadores e o pagamento real intactos, healthz 200, start limpo, as duas sub-abas servindo em produção). ⚠️ **De carona, um tropeço do DEPLOY que vale registrar**: o primeiro start do dev morreu com *"staticwebassets.endpoints.json não encontrado"* — o symlink virou antes de a pasta terminar de ser desempacotada. **O systemd reiniciou sozinho e o segundo start subiu limpo**, então não houve queda; mas o roteiro depende dessa reinicialização pra não deixar o ambiente no chão. Conferir pelo `readlink -f` + `cwd` do processo, nunca só pelo healthz.
>
> Antes, no mesmo dia — 📅 **A DATA DO TORNEIO, A ORDEM DA LISTA E A PERGUNTA DO HORÁRIO** (`build-357` e `build-364`, os dois no ar). O card de torneio dizia local e preço e nunca **QUANDO**; agora diz, com intervalo pra torneio de vários dias. A lista respondia numa ordem só, decrescente, pra todas as seções: o torneio mais **DISTANTE** encabeçava "Inscrições Abertas" e ⚠️ **torneio sem data marcada vinha antes de todo mundo** (no decrescente o nulo vem primeiro). Agora o que vai acontecer se lê do mais próximo, o que passou do mais recente, e quem não marcou data vai pro fim. 🚫 **A seção de CANCELADOS finalmente existe na tela**: o controller já a montava, com comentário dizendo que o organizador continua vendo pra resolver as devoluções — mas **a view nunca renderizou essa lista**, e o teste que "provava" o contrário conferia o ViewBag, que a tela não lê: **passava por motivo errado**. Aparece só pra quem tem cancelado, aberta, com selo vermelho no card. 🔎 **O furo irmão, achado num print de produção**: o seletor *"ver ranking de um torneio…"* listava **tudo** que existe na tabela, sem filtro — um torneio de teste cancelado aparecia numa página pública, e torneio esperando aprovação ou oculto apareceria igual, devolvendo de graça o que a trava de aprovação tirou. Agora obedece a régua da vitrine. ⏱️ **E a criação PERGUNTA se vai ter horário**: o torneio "por ordem de liberação" já existia inteiro, mas a chave morava na tela de gestão, **depois** do torneio criado — então a criação pedia hora do 1º dia, dos demais, do último jogo, duração e "até quando tem a quadra" pra todo mundo, inclusive pra quem ia chamar o jogo conforme a quadra vaga. ⚠️ Nasce em **"Não"** (horário inventado que ninguém cumpre é pior que horário nenhum), e ⚠️ **o `required` sai junto com os campos escondidos**: campo obrigatório VAZIO e escondido faz o navegador recusar o envio do formulário inteiro **sem mostrar erro** — provado no navegador (`checkValidity()` falso sem o conserto, verdadeiro com ele). 🧹 **O torneio de teste saiu de produção**: id 21 "teste" (cancelado) e as 3 categorias dele, dentro de transação, com `pg_dump` antes — 0 duplas, 0 partidas, 0 pagamentos pendurados, e o R$ 9 real sem relação nenhuma com ele. Produção ficou com **0 torneios e 72 jogadores**.
>
> Antes, no mesmo dia — ⬛ **O DEV VIROU PAREDE PRETA: portão religado SÓ no dev, com credencial própria do Felipe.** Com o lançamento, o dev aberto era porta pra gente de verdade se cadastrar no banco de teste. O portão de Acesso Antecipado voltou a `Habilitado=true` **no systemd do dev** (a `ConfiguracaoDoSistema` está vazia nos dois bancos, então o systemd manda), com usuário/senha novos que **moram só no unit dele — nunca no repo**. A tela do portão com `Beta__AmbienteDeTeste=true` agora é OUTRA: **toda preta**, "VOCÊ ESTÁ NO AMBIENTE DE DESENVOLVIMENTO — ABRA PADELIZOU.COM.BR PARA ENTRAR NO SISTEMA", sem o atalho "entrar com meu login" (no dev não há conta de verdade a proteger). Verificado ao vivo (`build-361-dea7c58` nos dois ambientes): dev `/` → 302 pro portão preto, **robots.txt continua legível na frente do portão** (a ordem no pipeline pagou no mesmo dia), healthz 200 nos dois, **prod segue aberto e intocado**. As credenciais antigas `Corneteiros`/`corneta` não valem mais em lugar nenhum.
>
> Antes, no mesmo dia — 🆓 **O AMERICANO LIVRE TAMBÉM É LIVRE DE TAXA.** Decisão do Felipe, revendo a tela de criação ao vivo: quem NÃO compra o Ranking Americano (a caixinha "Quero que este Americano valha ponto") não deve **nenhuma** taxa do Padelizou, em **nenhuma** forma de recebimento — Externo, Pix ou Todas as formas —, nos dois sabores do formato (individual e de Duplas). A única cobrança que sobra pro Americano é a do próprio ranking (R$ 5/pessoa, piso 8), se ele quiser. `CobrancaDoTorneio.IsentoDeTaxa` é a régua única, chamada nos dois lugares onde dinheiro de verdade se decide: `TaxaDoTorneioExterno.SeAplica` (destrava as chaves do "por fora" sozinho, sem precisar de negociação) e `CobrancaDoTorneio.Montar` (zera o percentual do split que vai pro Asaas nas formas online). ⚠️ **Achado no caminho, e o pior tipo — silencioso**: `AsaasService.CalcularRateio` aplicava o **piso mínimo de R$ 4 mesmo com percentual ZERO** — a isenção do Americano teria cobrado R$ 4 escondidos em toda inscrição online, porque `Math.Max(0, piso)` sempre vence. Corrigido pra um percentual **explicitamente** zero (diferente de AUSENTE, que ainda cai na tabela + piso) pular o piso — a mesma distinção que salva qualquer isenção futura de virar cobrança fantasma. Testado no navegador local ponta a ponta (não só nos testes): Americano sem ranking mostra "R$ 0,00" tanto no Pix quanto no Por fora; marcar "valer ponto" devolve a taxa normal na hora, sem reload.
>
> De caminho na mesma tela (queixa direta do Felipe usando o próprio produto): **1.** o simulador "Vai caber?" perguntava **"quantas duplas"** pro Americano individual — formato que não tem dupla na inscrição, é gente sorteada a cada rodada — e por baixo usava a conta de **grupos-de-3-e-mata-mata do Oficial** pros três formatos, mesmo o Americano de Duplas (que é todos-contra-todos comum) e o individual (que é `DivisaoDoAmericano`, com grupo final). Agora o rótulo e a conta trocam com o formato escolhido, espelhando os três Services certos em JavaScript — inclusive o aviso de "não fecha" com os números vizinhos que fecham. **2.** Check-in, Impedimentos de Horário e Conferir no Ranking RS são conceitos do torneio de chave (Oficial) e **sumiram da tela pros dois Americanos** — desmarcados junto, pra não submeter uma flag ligada que a tela escondeu. **3.** Um quarto "botão" ao lado dos três formatos, sem virar um quarto valor de `Formato`: **"Fale conosco que montamos seu tipo de torneio"**, link de WhatsApp pra quando o torneio não é nenhum dos três (liga, temporada, formato misto). **2.494 testes** (8 novos). ⚠️ **Feito e testado local — build limpo, suíte inteira verde, verificado clicando na tela real —, mas AINDA NÃO commitado nem publicado.** De brinde: achada e corrigida uma pane de meses no ambiente local — `.claude/launch.json` tinha um preview apontando pra porta 5061, que o Chrome recusa por padrão (**porta insegura**, faixa do SIP); trocado pra 5062.
>
> Antes, no mesmo dia — 🤖 **o dev saiu da vista do Google.** Quando o portão caiu, o dev abriu junto — público, com dado de teste e **sem `robots.txt`** (404 conferido ao vivo): o Google indexaria o ambiente de teste ao lado do site real na semana do lançamento. Agora `Middleware/RobotsMiddleware` responde **por host**: dev e admin servem `Disallow: /` **e carimbam `X-Robots-Tag: noindex, nofollow` em TODA resposta** — robots.txt só impede rastrear; página já linkada entraria no índice "às cegas", e é o cabeçalho que apaga da busca. O site público serve `Allow: /` e tem **teste explícito de que JAMAIS leva noindex** (o acidente simétrico — sumir da busca no lançamento — é pior que o problema). Fica **na frente do portão** de propósito: religado o Acesso Antecipado, o robots.txt continua legível em vez de virar redirect pra tela de senha. **2.486 testes** (7 novos).
> 💳 **Decisão do Felipe (subcontas):** organizadores serão instruídos a **criar a conta de recebimento por fora do sistema** (direto no provedor), pra não gastar as **10 subcontas/60 dias** do período de avaliação e não travar o 11º no embalo do lançamento.
>
> Antes, no mesmo dia — 📅 **A DATA DO TORNEIO APARECE NO CARD, e a lista deixou de responder na ordem errada.** O card dizia local e preço e nunca **QUANDO** — que é a primeira pergunta de quem bate o olho. ⚠️ **Torneio de mais de um dia mostra o intervalo** (`31/07 a 02/08/2026`): só o primeiro dia esconderia metade do evento de quem está decidindo se consegue ir. E a ordem estava errada em **dois lugares ao mesmo tempo** — era uma só, `OrderByDescending`, pra todas as seções: o torneio mais **DISTANTE** encabeçava "Inscrições Abertas", e ⚠️ **torneio sem data marcada vinha antes de todo mundo**, porque no decrescente o nulo vem primeiro. Agora o que ainda vai acontecer se lê **do mais próximo pro mais distante** e o que já passou **do mais recente pro mais antigo** — sentidos opostos de propósito, porque histórico se lê de trás pra frente; e quem não marcou data cai pro fim das duas. Como o card é o **mesmo da Home**, a data entrou lá junto. **2.479 testes** (4 novos amarram a ordem das três seções). ✅ **No ar em dev e prod pelo `build-357-2e31748`**, que leva junto o Americano livre da entrada abaixo: 72 jogadores e o pagamento real intactos, healthz 200, 0 erro no log, `pg_dump` antes em `/opt/padelizou-shared/backup-prod-antes-build-357-20260807-184505.sql.gz`. ⚠️ **ACHADO DE PASSAGEM E NÃO CORRIGIDO** (tarefa #72): o `Index` monta `ViewBag.Cancelados` com um comentário dizendo que o organizador continua vendo o torneio cancelado — mas **a tela não tem seção que renderize essa lista**. O cancelado some pra todo mundo, **inclusive pra quem precisa devolver dinheiro**, e o teste que "prova" o contrário confere o ViewBag, que a view nunca lê: **passa por motivo errado**. Hoje isso está visível em produção — o **único** torneio que existe lá (id 21, "teste") está cancelado, e a tela de Torneios aparece vazia até pro dono.
>
> Antes, no mesmo dia — 🔓 **O AMERICANO É LIVRE: qualquer pessoa cadastrada cria** (decisão do Felipe, corrigindo a versão que eu tinha subido horas antes exigindo perfil pros dois formatos). Ele é o rodízio de sábado — gente conhecida, parceiro trocando a cada rodada, criado na sexta à noite; exigir liberação pra isso poria o Felipe **no meio do combinado de um grupo de amigos** e mataria a porta de entrada do app. ⚠️ **E isso é seguro POR CAUSA da aprovação, não apesar dela**: criar já não avisa mais ninguém (o "novo torneio aberto" saiu da criação e foi pra aprovação), então um Americano inventado **não alcança a base** — fica no link de quem criou até alguém aprovar. Sem a trava da vitrine essa liberdade seria imprudente; com ela é só conveniência, e tem teste amarrando que o Americano de jogador comum **também nasce esperando o OK**. A pergunta da permissão passou a levar o **FORMATO** junto: a porta estreita ficou só no **Oficial**, que é o que publica chave, cobra inscrição e vale ranking — ali a credibilidade de quem organiza importa. ⚠️ Na tela a opção Oficial aparece **desabilitada com o motivo do lado**, e não escondida: sumindo, a pessoa concluiria que o Padelizou não faz torneio de chave; e o Americano já vem marcado pra ela, que é o que ela pode fazer agora. A tela também **deixou de recusar na porta** — mandar embora quem só queria criar um Americano seria fechar a entrada na cara de quem chegou. Junto: **"Torneio Padrão" virou "Torneio Oficial"** na criação, que é como o Felipe chama e como a separação dos dois foi desenhada. **2.475 testes.**
>
> Antes, no mesmo dia — 🚪 **A PORTA DO TORNEIO FICOU ESTREITA, e antes do problema aparecer.** Do medo certo do Felipe: *"tenho medo que qualquer pessoa chegue, crie torneio e lote de torneios"*. ⚠️ **E o estrago não seria uma lista suja: cada torneio criado dispara push e e-mail pra base inteira** — 71 pessoas com "quero saber de torneio novo" —, então vinte torneios inventados são **milhares de avisos** no celular de quem joga, que é como se perde um jogador de vez. **Duas travas, duas perguntas diferentes**: **PERFIL** (`Jogador.IsOrganizadorTorneio`) = quem pode criar, liberado pessoa a pessoa em `/Admin/Organizadores` (busca pela mesma régua do resto do site, e a pessoa recebe push avisando que foi liberada); **APROVAÇÃO** (`Torneio.AprovadoEm`) = qual torneio APARECE, **todo torneio, sempre**, em `/Admin/TorneiosParaAprovar`. ⚠️ **A aprovação NÃO trava o organizador** — ele monta, recebe inscrição e compartilha o link no mesmo minuto; o que falta até o OK é só a **VITRINE** (listagem, Home e o aviso). Segurar a criação faria ele esperar com a quadra reservada; segurar a vitrine não custa nada a ele e resolve o problema inteiro — e é o que mantém possível o Americano criado na sexta pro sábado. ⚠️ **O aviso "novo torneio aberto" MUDOU DE LUGAR**: saiu da criação e passou pra aprovação — avisar na criação entregaria à base justamente o torneio que ninguém olhou. Na criação quem recebe push são os **ADMINS**, porque o torneio entrou na fila deles. ⚠️ **A migração faz DUAS coisas que o EF não faria sozinho**, e sem elas ela quebra o que já existe: **todo torneio existente nasce APROVADO** (senão a regra nova apagaria da vitrine evento já anunciado e com gente inscrita) e **quem já organiza algum torneio ganha o perfil** (senão o organizador de hoje não abriria a edição do mês que vem, e descobriria isso com a quadra reservada). Conferido em dev: **15 de 15 torneios seguiram na vitrine e os 4 organizadores mantiveram o direito**. ⚠️ **Aprovar duas vezes sai fora ANTES de qualquer coisa** — dois cliques no mesmo botão mandariam o aviso pra base em dobro. ⚠️ **Tirar o perfil NÃO derruba os torneios da pessoa**: têm gente inscrita, e apagar evento por permissão revogada seria punir jogador por algo que ele não fez. 🧪 **18 testes existentes quebraram e cada um estava CERTO em quebrar** (as montagens criavam torneio sem organizador licenciado); o do "publicar não espera e-mail" foi **reescrito** pra afirmar a regra nova. **2.471 testes.** ✅ **No ar em dev e prod pelo `build-352-49fa7be`.** ⚠️ **EM PRODUÇÃO SÓ O FELIPE TEM O PERFIL HOJE** (é o único que organiza um torneio) — **os outros 71 jogadores não criam torneio até serem liberados um a um no painel**. Organizador novo tem que ser liberado ANTES de receber o link, senão bate na porta fechada no primeiro clique.
>
> Antes, no mesmo dia — 🔀 **O AMERICANO GANHOU RANKING PRÓPRIO — e a razão de existir foi um furo achado ao ir conferir, não uma ideia de produto.** Nem o Padelímetro nem as estatísticas olhavam o **formato** do torneio: o rodízio de sábado pontuava igual à final de uma 3ª Categoria. ⚠️ **E o campeão dos 100 pontos era o menor problema — no Americano CADA RODADA cria uma dupla nova**, e o ranking conta uma linha de "participou" por dupla: os ensaios do dev mostram **28 e 68 duplas** num Americano só. Não é brecha que alguém precisaria explorar; ela dispara sozinha em todo Americano que rodar. **Em produção havia ZERO Americanos**, então a correção chegou antes do primeiro real — sem ranking sujo pra limpar nem replay pra rodar. A regra entrou onde já morava (`ContaNoRanking`, a mesma que exclui o Restrito e que o Padelímetro consulta: nível e ranking passam pela mesma porta e não têm como discordar), e os nomes de formato viraram `Services/FormatoDoTorneio` — estavam digitados à mão em uma dúzia de arquivos, e o que antes dava bug visível agora daria **ponto na conta errada, calado**. 🏆 **A Trilha C (RANKING.md): colocação COM PESO** (decisão do Felipe) — tabela 100/60/40/25/10 com a **mesma forma da oficial**, porque as duas aparecem no mesmo perfil e duas escalas fariam comparar números que não se comparam; peso = pessoas ÷ 8, então **ser vice num Americano de 16 (120) vale mais que ganhar um de 4**. Somar GAMES foi recusado de propósito: premiaria volume e o ranking mediria tempo livre. ⚠️ **Piso de 8 inscritos** — 4 ou 5 pessoas é o tamanho em que quatro conhecidos fabricam resultado sem nem combinar nada, e o peso sozinho não resolvia (só fazia o ponto ser menor; ponto menor toda semana continua sendo ponto de graça). **Não pontuou, NÃO PAGA.** ⚠️ Arredondamento **AwayFromZero** e não o `ToEven` padrão do .NET: dois jogadores com a mesma conta receberiam pontos diferentes conforme a paridade. 💰 **Valer ponto é contratado: R$ 5 por pessoa inscrita**, marcado na criação com o preço e a estimativa na própria tela ("com 12 inscritos, dá R$ 60,00"; com 6, *"não vale ponto — começa em 8. Nada a pagar"*). ⚠️ **Contratar NÃO abre o ranking oficial** — a trava de lá é o FORMATO, e tem teste pra isso: sem essa separação, pagar viraria um jeito de comprar ponto em torneio de chave. ⚠️ **QUATRO condições pro Americano contar** (contratado · PAGO · piso de 8 · terminado), todas no mesmo serviço — espalhadas entre consulta e tela, uma ficaria de fora um dia. Pago e não só contratado: se marcar a caixinha bastasse, o preço não existiria. "Terminado" é `Status = Finalizado` e não "tem jogo acabado" — a colocação só existe quando o último jogo saiu, e ponto que aparece e some é pior que ponto que demora. **Aba própria** no hub de Ranking (a primeira linha da tela diz que é separado do oficial) e **acerto em `/Admin/RankingAmericano`**, com o total a receber em cima e "marcar como pago" com desfazer — mesmo desenho do acerto do Ranking RS: o sistema calcula e mostra, o dinheiro entra por fora, confirmação manual pelo extrato. ⚠️ A regra "quais partidas decidem o torneio" saiu do controller pra `TabelaDoAmericano.QueDecidem` **antes** de ganhar o segundo leitor — era a cópia certa de não fazer. **2.459 testes.** ✅ **No ar em dev e prod pelo `build-350-5891c8d`**: as duas migrations na história dos dois bancos, colunas no lugar, aba renderizando (o serviço roda e devolve vazio, que é o certo — zero Americano contratado), **0 erro**, e os 72 jogadores e o pagamento real intactos.
>
> Antes, no mesmo dia — 📐 **OS TETOS DO WHATSAPP SAEM DA CONTA DO PRODUTO — a primeira versão estava ERRADA, e quem viu foi o Felipe:** *"por que 30/dia? num dia de torneio pode ter mais de 100 participantes"*. Estava certo. Os três números (60/hora, 300/dia, 30/dia aquecendo) vieram de **prática genérica de anti-spam**, não da planilha do produto. ⚠️ **A conta, medida no código que dispara** (`AlcanceDoAviso.AppEWhatsApp`), pra um torneio de 100 participantes: **"chaves saíram" é 1 por JOGADOR = 100 de uma vez**, **"seu jogo é o próximo" é 4 por partida × ~88 partidas ≈ 350**, e o lembrete de 24h são mais 100 na véspera — **~450 no dia, contra um teto de 30**. ⚠️ **E o pior nem era o do dia: as 100 mensagens das chaves sozinhas estouravam o teto de 60/hora** — publicar a chave de um torneio de 100 perderia **40 avisos logo na primeira ação do dia**, e o "seu jogo é o próximo" (que o próprio comentário do código chama de *"o aviso mais importante do sistema"*) ia junto. Agora **250/hora** (metade da zona de risco de ~500/h do `WHATSAPP.md`, com folga sobre o pico real de ~150) e **1.200/dia** (quase 3× um torneio cheio) — ⚠️ **o teto não existe pra apertar o uso normal, existe pra um laço infinito não torrar o número numa madrugada**, e o teste que o guarda carrega a conta escrita dentro dele pra não virar chute de novo. ⚠️ **AQUECIMENTO REMOVIDO** (decisão do Felipe): a rampa faz sentido pra número NOVO e não pro nosso, que só levou uma restrição e tem histórico de uso real — com torneio marcado, ela custaria aviso de verdade pra ganhar reputação que o número já tem. **Removido e não desligado** — código morto vira armadilha; se um dia o chip virar um número novo de verdade, a rampa está no commit `3d4dc8d`. A chave `WhatsApp.AquecimentoComecouEm` foi apagada do banco de prod. Em troca, o vigia passou a **avisar por e-mail aos 80% do teto** (tipo próprio `WhatsAppPertoDoTeto`, janela de 6h): descobrir pelo contador de barradas é descobrir **depois** de já ter perdido mensagem. **2.418 testes.** ✅ **No ar em prod pelo `build-345-efc1de6`** — healthz 200, start limpo, canal `open`, medidor em `0/250 na hora · 0/1200 hoje`.
>
> Antes, no mesmo dia — 🛡️ **O PLANO ANTI-SPAM DO WHATSAPP, e o canal religado.** O conserto de 04/08 tratou **rajada**; faltavam duas coisas, e a primeira é a única que a Meta realmente olha. ⚠️ **1. CONSENTIMENTO — a causa raiz:** das 55 contas anteriores a 04/08 em produção, **54 estavam no canal por HERANÇA** (a caixinha nascia marcada e ninguém foi perguntado). Botão do admin **raiz** desmarca essas e convida cada uma a voltar **por push e e-mail** — ⚠️ **nunca pelo WhatsApp**: usar o canal em questão pra avisar que a pessoa saiu dele é a MESMA mensagem não pedida que causou tudo (tem teste só pra isso). O contraste que decidiu fazer: das 17 contas novas, **13 marcaram na mão** — perguntando, 3 em 4 dizem sim, então desfazer não é abrir mão do canal. **2. TETO:** o espaçamento de 7–16s controla CADÊNCIA e não TOTAL — 11,5s de média são **~313 mensagens/hora** ininterruptas, e o `WHATSAPP.md` já chama ~500/h de zona de risco. Agora **60/hora e 300/dia**, ⚠️ com janela **DESLIZANTE** e não balde que vira na hora cheia (com balde, 60 às 13h59 + 60 às 14h01 = 120 em dois minutos, que é a rajada de volta). **3. AQUECIMENTO:** o número volta de uma restrição com reputação zerada, então **30/dia na primeira semana**; ⚠️ o relógio começa quando o canal **CONECTA**, não quando alguém mexe no systemd (entre religar e ler o QR podem passar dias, e nesses dias o número não enviou nada), e **sem data gravada o teto é o BAIXO** — "não sei" e "é veterano" não podem dar no mesmo resultado. **4. MEDIDOR** no `/Admin`, sempre visível, por duas cegueiras: o canal ficou **desligado em produção de 04/08 a 07/08 e ninguém notou** (o vigia faz `if (Desligado) return`, e ⚠️ **prod e dev rodam os dois como `Production`**, então não dá pra separar por ambiente) e **nunca soubemos QUANTO sai** — nem nós nem a Evolution guardam a saída (conferido: tabela vazia). **5. SAÍDA** em toda mensagem: denúncia é o gatilho mais forte da Meta, e o link das preferências vem antes do "responda SAIR" porque é o único que funciona sozinho. ⚠️ O que o teto barra é **descartado, não adiado** (adiar devolveria o excedente em rajada) — e **contado**, nunca calado. De carona, o **número de suporte do site** saiu do 51 99239-5650 ("Bonamigo Systems") pro **pessoal do Felipe** — ⚠️ ele é o número de **ENTRADA**, que a pessoa lê e para o qual escreve; **não é o chip que o robô usa pra enviar**. Conferido no navegador: o card mostrou 7 herdadas, desmarcou as 7, **quem PEDIU continuou marcado**, e o card sumiu sozinho. **2.419 testes** (2.422 com o trabalho da sessão paralela junto). ✅ **NO AR EM PRODUÇÃO pelo `build-342-33bffd3`**: serviço `active`, healthz 200, start sem uma exceção, e o número novo já na página pública (`wa.me/5551994854884`, o antigo zerado). `pg_dump` antes em `/opt/padelizou-shared/backup-prod-antes-antispam-20260807-151940.sql.gz` (72 jogadores conferidos dentro). ✅ **O CANAL VOLTOU AO AR no mesmo dia, e o ciclo fechou com o log provando cada passo.** ⚠️ **O Felipe mandou "desmarca todos do whats, sem avisar", e foi LITERAL: 67 → 0** — não os 55 herdados, mas **todo mundo**, inclusive as **12 pessoas que tinham marcado na mão** depois de 04/08. Feito **direto no banco**, então nenhum convite saiu (o botão do painel dispara push+e-mail; o `UPDATE` não passa pelo app). ⚠️ **Reversível de propósito**: a lista foi guardada antes em `BackupOptInWhatsApp_20260807`, **com a coluna `TinhaPedido`** separando quem escolheu de quem herdou — devolver só os 12 é um comando. Efeito colateral bom: o canal recomeça **vazio**, e daqui pra frente só entra quem pedir. Chip pareado às 13:35 pelo QR — ⚠️ **é o `555192395650` (Bonamigo Systems), o chip de ENVIO e não o pessoal**, que era o risco a evitar depois da troca do número de suporte. Às **13:38:47 o vigia carimbou o início do aquecimento sozinho** (`WhatsApp.AquecimentoComecouEm` em `ConfiguracaoDoSistema`, log: *"aquecimento começa agora, teto de 30 mensagens/dia por 7 dias"*) — a prova de que o relógio novo funciona em produção, e não só no teste. ⚠️ **Sobrou pro Felipe remarcar a própria preferência** (ele saiu junto no "todos"), senão o dono não recebe os próprios avisos.
>
> Antes, no mesmo dia — 🔔 **"MANDEI PUSH PRA MIM E NÃO FUNCIONOU" — eram DUAS coisas, e a mais grave não era o push.** O push está **vivo**: a inscrição do Felipe (iPhone, `web.push.apple.com`) foi exercitada com a **mesma lib e as mesmas chaves VAPID da produção** e a Apple **aceitou** — as chaves do systemd batem com as do `appsettings`, e o journal inteiro não tem **um** erro de envio. O que quebrou foi a **TELA**: as caixinhas "por onde mandar" só existem no **passo 2**, e o passo 1 (Procurar) não manda nenhuma — o `false` do parâmetro era lido como *"o admin desmarcou"*, então a tela de envio nascia com os **dois canais apagados** e o primeiro clique em Enviar batia em *"escolha pelo menos um canal"*. ⚠️ **O padrão `= true` do VM era código morto desde que a tela nasceu**: no GET o passo 2 nem é renderizado, então aquelas caixinhas **nunca** apareceram marcadas pra ninguém. No passo de enviar continua valendo o que ele marcou, **inclusive desmarcar** — senão o teste dirigido perde o que tem de dirigido. **2.398 testes.** ⚠️ **E o achado grande, que ninguém tinha pedido pra procurar: o WhatsApp estava MUDO em produção desde 04/08 17:49** — o drop-in `whatsapp.conf` estava com `Evolution__BaseUrl=` **vazio**, que é a receita documentada de *canal desligado*. São **67 dos 72 jogadores** alcançáveis só por lá (push são **4 aparelhos**, todos iPhone; e-mail alcança 49). ⚠️ **E o vigia não avisou de propósito**: `estado == Desligado` é `return` — ele existe pra detectar canal que CAIU, e não sabe distinguir "desligado no dev" de "desligado por engano em produção". `BaseUrl` religado (`http://127.0.0.1:8081`, backup do arquivo em `/root/whatsapp.conf.bak-20260807`), e o vigia imediatamente fez o trabalho dele: detectou `Desconectado`, tentou religar sozinho, não resolveu e mandou o e-mail. ⚠️ **Falta o gesto humano: o chip está `state: close` — despareado, só QR novo resolve** (roteiro no `WHATSAPP.md`).
>
> Antes, no mesmo dia — 💾 **"preciso sempre que o backup esteja ativo".** O vigia do backup já existia e **funcionou** — mas avisava por **um canal só, e-mail, 1×/semana**, e em 07/08 a **cota diária do Gmail estourou**: o único alarme do backup dependia justamente do que estava fora do ar. Agora o aviso sai pela **`FilaDeAvisos`** (push + e-mail; saiu junto o filtro "só admin com e-mail cadastrado", que excluía exatamente quem seria alcançado por push quando o SMTP não vai), e o estado ficou **permanente no `/Admin/Metricas`** — verde/vermelho com a data da última cópia, e o comando do conserto na própria tela. O painel é o que **não depende de entrega nenhuma**: é só abrir. Ambiente sem backup (dev) não acusa atraso — alarme sempre aceso deixa de ser alarme. ⚠️ **E a causa raiz continua sendo do Felipe, não do código**: a última cópia boa foi **04/08** e a primeira falha **05/08** — **exatamente 7 dias** depois, que é o prazo em que o Google mata o refresh token de app com a tela de consentimento em **Teste**. Com o token de 07/08, a previsão de morte é **~14/08**. Conserto definitivo: Google Cloud Console → projeto `padelizou` → Tela de permissão OAuth → **PUBLICAR APP** (com `scope=drive.file` o Google **não exige verificação**). Enquanto isso não for feito, **nenhuma melhoria no alarme evita o apagão** — só faz saber dele mais cedo. Conferido no cofre depois do religamento do Felipe: **138 objetos, 19,7 MB**, com o dump de hoje e os 3 dias que tinham falhado. **2.401 testes**, no ar em dev e prod pelo `build-341-ade2f18`.
>
> Antes, no mesmo dia — ⏱️ **"está demorando bastante para publicar o torneio" — e a causa era um defeito que eu já tinha corrigido em UM lugar só.** Criar torneio avisava quem tem `NotificarTorneiosAbertos` com um laço de **SMTP DENTRO da requisição**: 71 jogadores em produção, uma conexão com o Gmail por pessoa, tudo antes de a tela voltar. Pior: a `FilaDeAvisos` **já manda e-mail** pra quem tem `NotificarEmail` — o laço inline era e-mail **em dobro**, e foi assim que a cota diária do Gmail estourou no mesmo dia (*"Daily user sending limit exceeded"* no log de produção). ⚠️ **A mesma armadilha derrubou o "finalizar jogo" em 05/08 e foi corrigida SÓ ALI**; sobraram quatro pontos com a cópia velha (criar torneio, inscrição de dupla, inscrição individual e publicar aviso de jogo). Agora saiu dos quatro, e os controllers **nem recebem mais `IEmailService`** — quem quiser mandar e-mail dali passa pela fila, que é a única forma de isso não voltar pela quinta porta. De quebra o aviso de jogo ganhou push (antes só e-mail: quem instalou o app e não marcou e-mail não ficava sabendo de nada). 🖱️ **E o efeito que o usuário viu**: no evento de teste, gente clicou 3× em "me inscrever" e **a mesma dupla entrou 3×**. A demora era o servidor, mas o clique repetido só existiu porque o botão ficava igual depois de tocado — agora ele trava em "Inscrevendo…" nos dois formulários. 🔢 Junto: **campo numérico não nasce mais com `0` escrito dentro** (qtd. de quadras, taxa por impedimento, troco de abertura, desconto, estoque mínimo) — zero pré-digitado vira 10 ou 01 quando se digita em cima, e a qtd. de quadras decide a grade inteira do torneio.
>
> Ainda em **07/08/2026** — 🚫 **cancelar torneio: a saída que faltava.** Finalizar diz que o torneio ACONTECEU (procura campeão, distribui ponto); esconder diz que ele existe mas não aparece. Choveu, o clube perdeu a quadra, não deu gente — e o organizador só tinha as duas erradas. Duas decisões do Felipe, e as duas moram no código: **o sistema NÃO estorna** (mostra quem pagou, quanto e o telefone; a devolução é por fora — mexer em dinheiro real, sem volta, é grande demais pra sair junto de um botão de cancelar) e **o torneio SOME da listagem**, continuando visível só pro organizador, pela mesma porta do "oculto", porque é lá que ele resolve as devoluções. ⚠️ **Preço é POR PESSOA**: a dupla pagou o dobro e é o dobro que tem que voltar. ⚠️ **Torneio finalizado não se cancela** — tem campeão, ponto de ranking distribuído e conquista no perfil de gente; isso não é cancelar, é reescrever o passado. ⚠️ **Cancelar LIBERA o nome pra remarcar**: o caso mais comum de cancelar é justamente remarcar, e com o nome preso o organizador teria que inventar "Amigos do Eder 2". O aviso pede **WhatsApp** (mesmo alcance da promoção da lista de espera): é o mais caro de não chegar, porque sem ele a pessoa sai de casa e vai pra quadra — ⚠️ e por isso este cancelamento depende do **chip despareado** citado acima; enquanto o QR não for refeito, o recado vai só por push e e-mail.
>
> ⚠️⚠️ Ainda em **07/08/2026**, e é o buraco do dia: **DEPLOY VERDE NO CI NÃO SIGNIFICA MIGRATION APLICADA.** Três deploys passaram com o banco travado dentro. `dotnet ef migrations add` lê o **modelo COMPILADO**, e este diretório de trabalho é **compartilhado com outra sessão** — o snapshot que eu commitei levou junto uma entidade (`AcertoRankingRs`) que só existia nos arquivos NÃO commitados dela. Por acidente isso ficou certo (a entidade e a migration dela vieram logo depois), e foi o meu **"conserto"** — tirar a entidade do snapshot — que quebrou de verdade: com ela fora, o EF viu tabela nova e eu gerei uma **migration duplicada** pra criar o que já existia. Com modelo e snapshot em desacordo o EF **recusa o `Migrate()` INTEIRO** no start: nenhuma migration de ninguém roda, e **o app sobe mesmo assim**, com colunas que o banco não tem. **Isso passa pelo CI verde** porque o snapshot declara entidade por STRING (compila sem a classe existir) e a checagem de pendência só acontece no `Migrate()`, no start do servidor — os testes usam banco em memória e não veem. **Regra que fica: gerar migration SEMPRE em worktree limpo, e conferir com `dotnet ef migrations has-pending-model-changes` antes de commitar.** Fechado no `build-339-28538cc`, com `pg_dump` de produção antes: as duas migrations na história, colunas e tabela no lugar, **0 erro** e os 72 jogadores intactos nos dois ambientes.
>
> Antes, no mesmo dia — 🏆 **O QUE DEVEMOS AO RANKING RS, nome por nome (`/Admin/RankingRs`).** R$ 1 por inscrito — e ⚠️ **inscrito é PESSOA, não inscrição**: quem joga a 4ª e a 6ª do mesmo torneio custa **um real**, não dois. ⚠️ **Foi por isso que a regra NÃO pôde reaproveitar `TaxaDoTorneioExterno.PessoasInscritas`, que parece a mesma pergunta e não é**: lá se cobra sobre o dinheiro que entrou, então quem se inscreveu duas vezes PAGOU duas vezes e conta duas — somar inscrições aqui pagaria a mais, calado, em todo torneio com gente repetida. Tem teste comparando as duas contagens lado a lado justamente pra quebrar se alguém "unificar" um dia. De brinde, agrupar por pessoa **dissolve o problema da chave direta** (as duplas dela remontam os mesmos jogadores) sem precisar do filtro que os 5% mantêm na query. Lista de espera fica de fora (não joga) e TIME também (o `Jogador1Id` de um time é o ORGANIZADOR). **Página de detalhe por torneio**: cada jogador com TODAS as categorias que jogou, selo "2 categorias, 1 cobrança", e o contraste inscrições × pessoas × economia — é a tela que resolve conferência com eles. **"Já paguei" grava uma FOTOGRAFIA** (`AcertoRankingRs`: pessoas e valor do dia) e **lança a despesa no caixa** automaticamente. ⚠️ Tabela própria e não coluna no `Torneio`: a lista de inscritos continua se mexendo depois, e o que foi combinado não pode ser recalculado toda vez que a tela abre — a tela de detalhe avisa quando o número de hoje difere do acertado. Preço em `RankingRsSettings.CustoPorInscrito` (é combinado, renegociar não pode exigir deploy). Migração `AcertoComORankingRs`. Conferido no navegador com cenário montado à mão: 9 inscrições → **6 pessoas**, 3 delas em 2 categorias, R$ 6,00, lista de espera corretamente fora, e a despesa caindo no Financeiro. **2.396 testes.**
>
> Antes, no mesmo dia — 🎾 **NASCEU O AMERICANO DE DUPLAS — terceiro formato de torneio — e o seletor de formato deixou de ser invisível no tema escuro.** O pedido veio da tela de criação: o radio estava "meio apagado" — borda azul-marinho a 30% de opacidade sobre fundo escuro, e MARCADO ficava navy sobre navy, então nem dava pra ver qual formato estava escolhido. Regra global nova no `site.css` com a mesma receita que a Mesa já usava (`:root[data-bs-theme="dark"]`), e ⚠️ `site.css` está na lista do service worker: `CACHE_NAME` foi pra **v7**. O formato novo (`Torneio.Formato = "AmericanoDuplas"`): a dupla é **FIXA** — a inscrição é em dupla, igual ao Padrão, e convite de parceiro, pré-cadastro por CPF, pagamento ×2 e lista de espera vieram **DE GRAÇA**, porque tudo que não é `Formato == "Americano"` já caía no caminho de duplas — e cada uma enfrenta todas as outras uma vez; vence a que somar mais games. ⚠️ **Qualquer quantidade de duplas fecha** (a partir de 2): a aritmética do individual (múltiplo de 4 ou 4+1) não se aplica, é um todos-contra-todos comum pelo método do círculo (`Services/RodadasAmericanoDeDuplas`), e com número ímpar uma dupla descansa por rodada, revezando sozinha. ⚠️ **Tabela própria por DUPLA** (`Services/TabelaDoAmericanoDeDuplas`), com a mesma ordem TOTAL (games e, no empate, Id) — e NÃO é cópia da individual: as somas são de unidades diferentes (pessoa × dupla); a régua de verdade compartilhada (quando um empate PODE virar partida) continua num lugar só, `TabelaDoAmericano.ProblemaParaDesempatar`. ⚠️ **A campeã é a DUPLA DE VERDADE**: `UltimaFase = "Campeao"` nela mesma — título dos dois, como no mata-mata, e não a linha solo do individual. ⚠️ **Empate de DUAS na liderança com desempate previsto vira partida criada SOZINHA pelo robô** (`RoboDoChaveamento.FecharAmericanoDeDuplasAsync`) — as duas duplas já existem, não há parceiro a escolher como no individual; com 3+ empatadas ou desempate desligado o torneio encerra e o critério fica com o organizador. A grade do sorteio usa o **encaixe com detector de conflito por PESSOA** (`GradeDeJogos.Encaixar`) — dá, porque aqui as duplas existem antes dos jogos; no individual o posicional é o possível. A fase reusa a grafia `"Americano Rodada N"` (grafia nova é como as telas passam a discordar), e é **grupo único sempre** — divisão em grupos é coisa do individual. Guardas no servidor: categoria de times recusada nos DOIS americanos, `InscreverIndividual` recusa fora do individual (criaria inscrição que nenhum sorteio lê), e formato desconhecido em POST montado à mão é recusado na criação. Os testes novos (`AmericanoDeDuplasTests`) rodam o desfecho **pelas duas telas** que finalizam partida, e a verificação foi ponta a ponta por HTTP contra o servidor local: cadastro → criar torneio no formato novo → 3 duplas por CPF → sortear → 3 jogos finalizados → campeã carimbada e torneio Finalizado. **2.376 testes.** ⚠️ **Commit dividido de propósito**: a sessão paralela estava editando `TorneiosController.cs`, `TorneiosController.Criacao.cs` e `Details.cshtml` no mesmo minuto, então quatro pontos de fiação meus nesses arquivos (previsão da grade só no Padrão, aba Classificação por dupla, whitelist de formato + recusa de times na criação, painel "Sortear Rodadas" no Details) ficaram FORA deste commit pra não arrastar trabalho em andamento alheio — estão na árvore, verdes, e entram no commit seguinte de quem fechar esses arquivos. Antes de deployar este bloco, conferir que esse commit já existe.
>
> Antes, no mesmo dia — 📒 **O CAIXA DO DONO: `/Admin/Financeiro` separa por fora × pelo gateway × despesas, e registra pagamento que chegou por fora.** Três números pra fechar o mês: **recebido por fora** (Pix direto + registro manual), **recebido pelo gateway** (a comissão dos splits, BRUTA) e **pago** (despesas lançadas à mão) — mais o líquido. ⚠️ **O custo exato do gateway NÃO é calculado, e é decisão**: a taxa dele sai por dentro, transação a transação, e nem guardamos a forma que o pagador escolheu — estimar seria inventar contabilidade. O número certo está no extrato deles e entra como DESPESA (`DespesaRegistrada`, tabela própria de propósito: despesa disfarçada de `Pagamento` inflaria as somas de receita de métricas/alerta do MEI/extrato). **Registrar pagamento por fora vale de verdade**: cria o `Pagamento` confirmado (`MetodoPagamento="Manual"`) e chama o MESMO `EfetivarAsync` — mensalidade estende a assinatura, taxa libera as chaves, com as notificações de sempre. Só os dois tipos 100% nossos (mesma trava `AceitaPix`); mensalidade manual exige PROFESSOR, taxa manual exige torneio "por fora" com taxa em aberto (senão duplicaria o split). ⚠️ **Sem desfazer no pagamento registrado** (o efetivar dispara o que não volta sozinho — a tela avisa); despesa PODE apagar, é só caderneta. A régua da soma é a **COMISSÃO**, nunca o valor cheio (no split o repasse nunca foi nosso). ⚠️ **Tudo do Pix/caixa apertado pra admin RAIZ** — inclusive a fila do Pix, que antes aceitava admin nomeado: dar baixa exige o extrato do banco, e o extrato é do dono. Conferido no navegador: registro manual de mensalidade estendeu a assinatura (+1 mês, `ConfirmadoPor` gravado), despesa lançada, e o líquido fechou **nos centavos** (99,80 − 12,34 = 87,46). Migração `FinanceiroDoPadelizou`. **2.376 testes.** ✅ O Pix direto da entrada anterior passou no CI: **`build-331-2d36170`** pronta pro deploy.
>
> Antes, no mesmo dia — 🎨 **O QUADRO PINTA O CAMINHO DO JOGADOR: "seu jogo" e "pode ser seu" na aba Chaves e Grupos.** Continuação natural do "Meus jogos" de 06/08: o filtro da aba Jogos lista os jogos dele, mas o DESENHO da chave não dizia onde ele estava. Agora o jogador logado vê os jogos dele com selo **"SEU JOGO"** (inclusive o que já perdeu — trajeto é trajeto, só a corrente para ali) e as vagas futuras que ainda podem ser dele com **"PODE SER SEU"**, cartão com borda verde e legenda no topo do quadro. ⚠️ **A regra NÃO foi copiada**: quais vagas "podem ser dele" é decisão de `Services/MeusJogos.Filtrar` — a MESMA corrente do filtro da aba Jogos (quem perdeu não entrega nada; bye entra pelo nome). Pra isso a montagem do quadro (numeração global, pareamento primeiro×último, nome de fase futura, bye nomeado na fase em que entra) saiu do Razor solto de `_ChaveDoMataMata.cshtml` e virou serviço, **`Services/QuadroDoMataMata`** — e ganhou os testes de estrutura que o desenho nunca teve. Deslogado ou torcedor: zero pintura, zero legenda (conferido). ⚠️ O selo é o INVERSO do selo de bye (verde no marinho × marinho no verde), senão os dois parecem a mesma coisa lado a lado; e se o jogo pintado está AO VIVO o anel vermelho vence o verde de propósito — jogo em quadra é o que mais importa na tela. ⚠️ `site.css` está na lista do service worker: `CACHE_NAME` foi pra **v6**, senão quem instalou o app nunca veria o verde. Conferido no navegador (banco local, logado como jogador de teste): quartas com "SEU JOGO", semifinal ao vivo com "SEU JOGO", final projetada "PODE SER SEU", e na chave direta com bye o caminho segue do jogo agendado pra semifinal e final. **2.337 testes.**
>
> Antes, no mesmo dia — 💰 **PIX DIRETO NA CONTA DO PADELIZOU: mensalidade do professor e taxa de 5% do externo agora caem sem gateway.** O código "copia e cola" (BR Code do BCB) é montado **por nós** (`Services/PixCopiaECola` — CRC16 ancorado no vetor oficial `29B1`, valor SEMPRE com ponto mesmo em pt-BR, acento vira letra simples porque o EMV é ASCII), o QR é desenhado na hora (QRCoder, rota `/Pagamentos/PixQr/{id}`), e a chave/nome/cidade ficam em `ConfiguracaoDoSistema` editáveis pelo **painel admin** (`/Admin/PixDireto`) — ⚠️ **só o admin RAIZ troca a chave**, porque a chave decide PRA ONDE VAI O DINHEIRO. ⚠️ **A regra que sustenta tudo: só entra no Pix direto o que é 100% receita do Padelizou** (`PixDireto.AceitaPix` trava nos dois tipos; inscrição/aula/quadra tem repasse a terceiro e PRECISA do split do gateway — recebê-las na nossa conta estouraria o teto do MEI e seria movimentar dinheiro alheio). ⚠️ **QR estático NÃO avisa quando é pago — não existe webhook.** O fluxo é: pagador clica **"Já fiz o Pix"** (`Pendente → AguardandoConfirmacao`), admin confere o **extrato do banco** e dá baixa na fila do `/Admin/PixDireto` (`→ Confirmado` + `EfetivarAsync`, o MESMO efetivar do webhook — estende a assinatura / libera as chaves e notifica igual). O identificador `PDZ{id:D8}` vai no campo 62-05 do código e aparece no extrato de boa parte dos bancos. A fila também lista cobrança gerada e **nunca declarada** — o Pix pode ter caído sem clique. Com a chave configurada o Pix vem **primeiro** e o gateway vira reserva (chave vazia = tudo como antes); cartão continua existindo SÓ pelo gateway, porque processar cartão exige credenciadora — não é feature, é empresa. Colunas novas em `Pagamento`: `MetodoPagamento` ("Gateway"/"PixDireto") e `ConfirmadoPorJogadorId` (a baixa manual é uma PESSOA decidindo que o dinheiro entrou, e isso precisa de nome). Migração `RecebimentoPorPixDireto`. Conferido no navegador de ponta a ponta: chave gravada, mensalidade gerou `PDZ00000001` com QR válido, "já paguei" entrou na fila, baixa do admin estendeu a assinatura até 07/09 e gravou quem confirmou. De carona: o comprovante dizia "Identificação (Asaas)" — o nome do gateway saiu da tela (regra antiga). **2.330 testes.** Próximo passo natural quando o volume crescer: Pix com API do banco (txid + webhook de verdade) — o fluxo já está pronto pra receber confirmação automática, só troca quem chama o Efetivar.
>
> Antes, no mesmo dia — 🎛️ **três ajustes pedidos pro primeiro organizador de fora, que abre o sistema hoje.** (1) **O check-in do dia nasce DESMARCADO.** Ligado por padrão, o organizador ganhava uma tela que não pediu — e que os inscritos veem e cobram. Torneio que já está no ar não perde nada: a migração `CheckInOpcional` gravou `true` em todos eles, então muda só o torneio novo. ⚠️ Dois testes de check-in passaram a **ligar o interruptor na montagem** — sem isso o que afirma "quem não organiza não faz check-in" continuaria verde mesmo se a checagem de dono sumisse, porque a recusa viria do interruptor desligado. (2) **As duas categorias "Iniciantes" (masculina e feminina) saíram de circulação.** ⚠️ **Desligadas, não apagadas**: `CategoriaPadrao` ganhou `Ativa`, e a linha fica no banco porque torneio antigo, preferência de jogador e aviso guardam o **Id** dela — apagar deixaria tudo isso órfão. Ligar de volta é um `UPDATE` de uma linha, **sem deploy**. ⚠️ **A migração põe `defaultValue: true`, e não o `false` que o EF gera do default do `bool`** — com `false` toda categoria existente sairia desligada e ninguém conseguiria criar torneio (a mesma armadilha do `GruposAmericano`, no mesmo dia). O filtro mora em **um lugar só** (`Services/CatalogoDeCategorias.Ativas()`) e vale nas 8 telas que oferecem categoria; o servidor recusa junto nas **duas** portas que põem categoria em torneio (criação e "Adicionar categoria" da aba Gerenciar) — corrigir só a criação deixaria o caminho de trás aberto. ⚠️ **Dois jogadores de produção têm "Iniciantes Feminina" declarada no perfil** (Fabiana Stieler Rodrigues e Anderson Virgili): a linha continua no banco, mas some da tela de preferências e cai quando eles salvarem o perfil de novo. **Nenhum torneio usa a categoria** — zero em prod e em dev. (3) **Nome de clube escrito desativa o seletor.** É o que o servidor já fazia calado (`AcharOuCriarClubeAsync` sobrepõe o `ClubeId`); com os dois preenchidos a tela mostrava um clube e o torneio nascia em outro. ⚠️ Desativado e **não zerado**: campo desativado não é enviado, então o valor antigo não vai escondido no POST, o `required` do seletor não trava o envio, e limpar o texto devolve a escolha em vez de obrigar a procurar o clube de novo. Conferido no navegador de ponta a ponta: torneio criado com o clube **digitado**, `UsaCheckIn = f`, 18 categorias na tela e nenhuma "Iniciantes". **2.310 testes.** ✅ **No ar em dev e prod pelo `build-328-d171377`**: nos dois bancos a coluna nasceu com **18 ativas e 2 desligadas** (as duas certas, conferidas pelo nome), migração registrada, serviços `active`, healthz 200, **0 erros** e 0 exceções no log. `pg_dump` de produção antes, em `/opt/padelizou-shared/backup-prod-antes-build-328-20260807-133256.sql.gz` (conferido com os 71 jogadores dentro); os 71 jogadores e o pagamento real de R$ 9 seguem intactos.
>
> Antes, no mesmo dia — 🔓 **O PORTÃO CAIU: prod e dev estão abertos, um dia antes do lançamento.** `AcessoAntecipado__Habilitado` foi pra `false` nos **dois** units do systemd (`padelizou` e `padelizou-dev`), `daemon-reload` + restart, e a `/` que respondia **302 pro `/AcessoAntecipado/Entrar`** agora responde **200 anônimo** nos dois endereços. Backup dos units em `.bak-portao-20260807`. No repo, o **modelo** `padelizou.service` e o `DEPLOY.md` foram pra `false` junto — senão uma reinstalação ressuscitaria o portão sem ninguém ligar uma coisa à outra. ⚠️ **O código do portão FICA de pé, e de propósito**: o botão do admin raiz em `/Admin` continua ligando e desligando, **vence o systemd e sobrevive a deploy** (`Services/PortaoDeAcesso`) — religar de emergência é um toque no celular, não ssh mais restart. As credenciais seguem no drop-in `portao.conf`, sem efeito enquanto isso estiver desligado. ⚠️ **A tabela `ConfiguracaoDoSistema` está VAZIA nos dois bancos** — ninguém tinha mexido pelo app, então quem mandava era mesmo o padrão do systemd; foi por isso que mexer nele era necessário, e é por isso que o painel vai dizer "como está configurado no servidor" e não "você desligou". ⚠️ **O que abriu junto foi o `/Auth/Cadastro`**: ele era o único caminho que o portão segurava de propósito (conta nova era por convite), e agora a única defesa que sobra ali é a trava de força-bruta por IP e por ação (20 tentativas). ⚠️ **E o dev ficou público também** — `dev.padelizou.com.br` já não pede senha e **não existe `robots.txt` em nenhum dos dois** (404): nada impede o Google de indexar o ambiente de teste e mostrá-lo na busca ao lado da produção. Fica pendente, e o conserto é no Caddy (que não está no repo), não no C#. Nada de C# mudou: **2.306 testes**.
>
> Antes, no mesmo dia — 🤝 **O RANKING RS FOI TESTADO EM PRODUÇÃO, com atleta real, e funcionou.** Torneio oculto de teste, inscrição de um atleta que pontua na 2ª Masculina tentando entrar na 6ª: barrado, com a mensagem citando *"2ª Masculina (235 pts), 3ª Masculina (170 pts)"*, o caso apareceu pro organizador e a decisão dele foi aplicada. Torneio de teste apagado depois; produção voltou a 0 torneios, 71 jogadores e o pagamento real intacto. ⚠️ **E o teste achou o furo que importa: o casamento deles é POR NOME, não por CPF.** Provado com três chamadas — nome real com CPF lixo bloqueia igual, sem CPF bloqueia igual, e CPF "válido" com nome inexistente não acha ninguém; o `external_registration_user_id` que mandamos é só ecoado de volta. A consequência é que quem se cadastra como "Zeca" **não é encontrado e a inscrição passa** — a trava não erra barrando quem não devia, ela erra **deixando passar, em silêncio**, que é o pior jeito de falhar. Daí as três mudanças de nome (`build-325`): **sobrenome obrigatório** no cadastro e na inscrição (só 2 dos 140 jogadores tinham nome solto, um deles o famoso "."), **nome muda uma vez só** (a troca livre existe pra quem digitou errado na pressa; depois congela, com a mensagem dizendo que é pra manter o padrão dos rankings e oferecendo o "Reportar problema" pra exceção), e **exibição = primeiro nome + último sobrenome (apelido)** — "José Silva (Zeca)" — em torneio, chave, grupo, ranking e time. Antes quem tinha apelido aparecia **só** pelo apelido, e isso escondia a pessoa: numa chave, "Zeca" pode ser três. ⚠️ A regra de exibição entrou no **`NomeBonito` que já existia** (ele já tratava partícula, sufixo de geração e caixa) — eu tinha começado um serviço novo e apaguei: duas regras de nome seria mais uma cópia pra divergir. Junto, do próprio teste do Felipe: **"Manter" virou "Não liberar"** — o botão precisa dizer o que vai acontecer, e "manter" só faz sentido pra quem já sabe que existe uma recusa em pé, que é justamente quem não precisa do botão. **2.306 testes**, no ar em prod e dev.
>
> Antes, no mesmo dia — 🎾 **O AMERICANO FOI REFEITO, e a origem foi um teste que ninguém tinha pedido.** Rodando o formato inteiro no navegador (8 jogadores, 7 rodadas, 14 jogos) o torneio terminou com **DOIS CAMPEÕES DIFERENTES**: o robô montava sozinho uma "Final" cruzando os 4 primeiros — **sempre**, mesmo sem empate e com a opção de desempate desligada —, e daí cada metade do sistema respondia uma coisa. A tela de Classificação soma só as rodadas e coroava o líder em games; a conquista do perfil lê `Dupla.UltimaFase == "Campeao"` e coroava quem vencesse aquela Final. No ensaio a **Ana fez 56 games, a maior soma do torneio, apareceu em 1º na tabela e ficou SEM TÍTULO**; o Fabio, 2º com 53, levou — e a Gisele levou junto, porque o carimbo é de DUPLA e ela era a parceira dele naquele jogo. Cada lado fazia certo o que fora programado pra fazer: o erro era existir uma final de mata-mata num formato que não tem final. **A regra (Felipe): vence quem somou mais games; só há rodada final se DOIS OU MAIS empatarem na liderança** — com 3+ empatados o sistema avisa e o critério é do organizador, porque três pessoas não cabem em lados opostos de uma quadra. ⚠️ **O campeão do Americano é UMA PESSOA**: o carimbo continua sendo `UltimaFase = "Campeao"` (o que perfil e estatísticas já leem) mas numa linha **SEM parceiro** — coroar uma dupla de rodada daria o título a quem calhou de jogar junto. Vale igual no desempate: quem disputa é o empatado, o parceiro só fecha a quadra. Junto, da mesma família do post-mortem do Interno: **`TabelaDoAmericano` ordenava só por `OrderByDescending(Games)`, sem ordem TOTAL** — quem empatava ficava na ordem em que o dicionário foi preenchido, ou seja na ordem em que as partidas voltaram da consulta, e cada chamador monta a consulta do seu jeito. O robô que escolhe os empatados JÁ tinha o desempate por Id; a tabela que o jogador lê, não. **A correção tinha sido feita numa cópia da regra e não na outra.**
>
> Ainda em **07/08/2026** — 👥 **todo inscrito joga, e o Americano ganhou GRUPOS.** Dois defeitos silenciosos apareceram medindo o formato: (1) o sorteio **cortava pro múltiplo de 4 abaixo** e quem sobrava ficava de fora do TORNEIO INTEIRO — 10 inscritos viravam 8, e as duas pessoas cortadas **tinham pago a inscrição**, avisadas só por uma frase no fim da mensagem do organizador; (2) **5, 9, 13 e 17 fecham a conta perfeitamente e eram recusados à toa**. Agora quem fecha em 4 é a QUADRA: quem não cabe numa rodada descansa, e o descanso reveza. Medido com 10 pessoas de verdade: 12 rodadas, 24 jogos, os 10 em quadra, cada um com 9 ou 10 jogos — **diferença de UM**, o que importa porque a classificação é por soma de games. ⚠️ **"Cada um com cada um" NÃO fecha em todo tamanho, e isso é aritmética**: são n(n−1)/2 duplas a formar e cada jogo forma 2, então com 6 pessoas dariam 7,5 jogos. Fecha só com **4, 5, 8, 9, 12, 13, 16, 17, 20…** (n múltiplo de 4 ou múltiplo de 4 mais 1). A saída é **dividir em grupos**: **10 vira 2 grupos de 5, 15 vira 3 grupos de 5**, os primeiros de cada disputam um **GRUPO FINAL**, e o campeão sai de lá. ⚠️ **Quantos passam NÃO é fixo: é o menor número que faz o grupo final fechar.** Com 2, 4 ou 6 grupos passam 2 de cada; com 3 grupos, 2 de cada dariam 6 finalistas — e 6 é justamente um número que não fecha —, então passam 3 e o grupo final tem 9. Sem essa conta o torneio terminaria na fase decisiva com parceiro repetido. Divisão em que o grupo INTEIRO passaria não é oferecida (20 em 5 grupos de 4 classificaria os 4 de cada: isso não é fase classificatória, é jogar o torneio duas vezes). **O organizador escolhe a divisão na hora do SORTEIO**, não na criação — é agora que se sabe quantos vieram —, e a tela mostra a duração de cada opção; a primeira é a que mais mistura. **Número que não fecha é RECUSADO** e nada é sorteado pela metade: com 14, o sorteio para e diz *"chame mais 1 e feche em 15, ou jogue com 13"* — e a tela de criação já lista os números que fecham, porque descobrir isso no dia do torneio é tarde demais. Dividir também resolve o relógio: 20 pessoas num grupo só são **19 rodadas ≈ 8h de quadra**; em 4 grupos de 5 são 5 de grupo mais 7 de final. ⚠️ **Grupo único mantém a grafia antiga da fase** (`"Americano Rodada N"`) — duas grafias pra mesma coisa é como as telas passam a discordar; as três formas (rodada única, rodada de grupo, rodada do grupo final) moram num lugar só, `Services/FaseDoAmericano`. ⚠️ **A migração põe `defaultValue: 1` em `GruposAmericano`, e não o 0 que o EF geraria do default do `int`** — zero grupos não é torneio, e toda categoria que já existe é de grupo único (conferido: as 21 do banco local ficaram com 1). ⚠️ **Cada grupo tem a SUA tabela**: somar o torneio inteiro compararia gente que nunca se enfrentou, e o corte de quem passa sairia dessa soma errada. ⚠️ **Ninguém é coroado com a fase de grupos apenas** — o robô pergunta se ainda falta a fase decisiva antes de carimbar. A medalha da classificação passou a marcar **quem passa**, e só onde há corte (antes marcava sempre os 4 primeiros, herança da final automática — num grupo de 5 isso dizia a quatro pessoas que tinham se classificado). Conferido no navegador de ponta a ponta com 10 pessoas, e o detalhe que mostra a regra funcionando: **o 1º do grupo A (32 games) terminou em ÚLTIMO no grupo final, e o 2º do grupo A foi campeão**. **2.306 testes** — a varredura nova cobre TODO tamanho de 4 a 40 (324 casos) e exige a repetição de parceria **exatamente no mínimo aritmético**: com 10 pessoas são 48 parcerias pra 45 duplas, ou seja 3 repetições, e uma a mais seria o sorteio jogando fora parceria que cabia.
>
> **07/08/2026 (madrugada)** — 🚨 **`build-323-a6fc8f5` no ar: erro de produção deixou de ser invisível.** Todo 500 agora vira linha em `ErroDoSistema` + push/e-mail pros admins (janela de silêncio de 6h) + a tela `/Admin/Erros` — antes o rastro morria no `journalctl` e o sistema só avisava quando um usuário reclamava. `pg_dump` antes (`/root/pre-deploy-20260807-004331.sql`, conferido com os usuários reais dentro), esteira do CI verde de ponta a ponta, healthz 200, **0 erros registrados**, 141 jogadores e o pagamento real de R$ 9 intactos. Vieram junto duas correções da sessão paralela (Americano com dois campeões; a final que só existe se houver empate). ⚠️ **Precisou de um segundo deploy no mesmo bloco**: eu tinha acrescentado ao app cabeçalhos de segurança que **já existiam no Caddy** — em produção cada um chegava duas vezes, e a RFC 7034 diz que servidor não deve mandar mais de um `X-Frame-Options`. Removidos; hoje chega **um de cada**, conferido.
>
> **07/08/2026 (madrugada)** — 📦 **prod voltou pra um pacote do CI: `build-320-a032746`**, em prod e dev, healthz 200 e zero erro. Enquanto o Actions esteve fora, produção rodou um `build-manual-2017c5c` montado à mão — funcionava, mas **fora do esquema de versões**, ou seja sem rollback em um comando e sem o carimbo de testes verdes. Este deploy devolve as duas garantias e leva junto o logo novo (⚠️ o `CACHE_NAME` do `sw.js` foi pra `v5` no mesmo commit — sem isso quem instalou o app ficaria com o ícone velho pra sempre). `pg_dump` antes, conferido de verdade (65 tabelas, dados dentro), em `/opt/padelizou-shared/backup-prod-antes-build-320-*.sql.gz`. 🧹 **E na mesma madrugada a produção foi ZERADA pro lançamento de 08/08**, por decisão do Felipe: o Interno Los Corneteiros saiu inteiro (**87 partidas · 65 duplas · 6 categorias · 1 torneio**) e com ele as **70 contas SEM SENHA** (cascas de pré-cadastro — conferido antes: zero pagamentos, zero clubes, zero alunos, zero times, zero duplas em outros torneios, zero elogios). Ficaram **71 jogadores**, todos com senha e e-mail, os **44 times**, os **12 clubes** e o **pagamento de R$ 9** (registro de receita que o MEI obriga a guardar). O Padelímetro voltou a zero pelo **replay do admin** — o caminho certo, porque apagar partida NÃO recalcula o nível que já está no `Jogador`. Dump antes em `/opt/padelizou-shared/backup-prod-antes-apagar-interno-*.sql.gz`. Isso encerrou de vez a pendência da final incoerente da categoria de TIMES: o torneio inteiro deixou de existir.
> ✅ **BACKUP PRA FORA DO SERVIDOR CONSERTADO em 07/08 13:35** — estava parado **desde 04/08**: o `rclone` perdeu a autorização do Drive e falhava calado todo dia às 4h30 ("Google Drive ainda não autorizado"). Achado na varredura de véspera do lançamento; o backup local nunca parou, mas por 3 dias **não havia cópia fora do VPS**. Conferido depois do conserto: **138 objetos, 19,7 MB**, com o dump de hoje lá dentro e os dias atrasados recuperados.
> ⚠️ **Reautorizar num servidor SEM NAVEGADOR tem duas pegadinhas**, e as duas mordem: (1) o `rclone` abre a página em `127.0.0.1:53682` **do VPS**, não da sua máquina — a saída é um túnel `ssh -N -L 53682:127.0.0.1:53682 root@padelizou.com.br` numa segunda janela e abrir o link no navegador local; (2) no fim ele estoura **`403 ACCESS_TOKEN_SCOPE_INSUFFICIENT` listando Team Drives** e parece ter falhado — **não falhou**: o escopo do remote é `drive.file` (só o que o app cria, que é tudo o que o backup precisa) e o token JÁ FOI GRAVADO antes desse passo. Ignorar o 403 e testar o que importa: rodar `/usr/local/bin/backup-drive.sh` e conferir se subiu.
> Antes: **06/08/2026 (noite)** — 🤝 **Ranking RS no ar em prod e dev**, publicado **à mão** porque o GitHub Actions caiu (`major_outage`) e a esteira parou de gerar tag. Chave configurada em produção (existia só no dev) e integração provada com HTTP 200 na API deles. Ver a seção do dia; quando o Actions voltar, rodar o `deploy.sh` normal.
> Antes: **06/08/2026** — 🧨 **o post-mortem do Interno: TRÊS defeitos de chaveamento, todos da mesma família — MAIS DE UMA CÓPIA DA MESMA REGRA.** (1) **Dois robôs, um por controller**: o organizador encerra jogo por dois caminhos (Mesa/card → `TorneiosController`; Controle de Placar em tela cheia → `PartidasController`) e cada um tinha a própria cópia do robô que cria a fase seguinte. Os confrontos batiam; o AGENDAMENTO não — a cópia do `PartidasController` marcava `HorarioPrevisto = DateTime.Now.AddHours(2)` pra TODOS os jogos da rodada, sem quadra e sem olhar quem já estava marcado. A rodada inteira nascia no mesmo minuto, "quadra a definir", num dia que nem era o do torneio, **dependendo só de por qual TELA o placar foi lançado**. Agora há motor único (`Services/RoboDoChaveamento`), e a guarda "os grupos acabaram?" saiu do CHAMADOR e foi pro robô — no chamador ela podia existir num caminho e faltar no outro. (2) **Empate triplo sem desempate**: o grupo A dos TIMES fechou com Target.it, Valandro e Argentus em 1 vitória e −2 de saldo; a régua parava no saldo, então o 2º colocado saía da ORDEM EM QUE AS DUPLAS CHEGAVAM na consulta — e cada chamador monta a consulta do seu jeito. **A mesma tabela respondia coisas diferentes conforme quem perguntasse.** Entrou games PRÓ e, por último, o Id (empate que sobrevive a games pró é sorteio de qualquer jeito, e um sorteio ESTÁVEL vale mais que um que muda entre duas telas). Confronto direto ficou de fora: naquele grupo ele é **circular**. (3) **Bye inventado num quadro cheio**: "classificou e não tem jogo de mata-mata" era tratado como bye; com a classificação instável, o 2º do grupo A recalculado no avanço era OUTRO time e virou um terceiro semifinalista — e como o pareamento cruza primeiro com último, **a final saiu entre o vencedor de uma semi e um time eliminado nos grupos, e o vencedor da outra semi sumiu do torneio**. A trava nova é aritmética: a primeira rodada tem `jogos × 2` lugares; se cabe todo mundo, ninguém descansou. ⚠️ **O dado do torneio 18 ainda espera decisão do Felipe**: a final gravada é CredHub × Valandro 0×0 com a Valandro campeã, mas quem venceu a outra semifinal foi o **ST Led** — e o jogo certo nunca aconteceu, então não dá pra corrigir isso no código. Junto na mesma leva: ⚡ **finalizar jogo devolve a tela na hora** (o "muito lento" era o aviso: SMTP + push por jogador DENTRO da requisição; virou fila com entregador de fundo, `Services/FilaDeAvisos` — e era isso que fazia o organizador tocar duas vezes e o jogo subir em duplicidade), 🔓 **o portão de acesso antecipado abre e fecha pelo painel admin** (systemd dá o padrão, o banco pode dizer o contrário e ganha; ⚠️ tabela e não variável em memória, senão o portão voltaria sozinho no primeiro deploy depois do lançamento — só admin RAIZ) e 👤 **"Meus jogos" inclusive os que ainda não existem** (segue a corrente das procedências da projeção; ⚠️ quem PERDEU não entrega nada, senão o eliminado veria a final como próximo jogo dele; ⚠️ a CATEGORIA faz parte da chave — toda categoria tem uma "Semifinal 1"). **1.751 testes.**
>
> Ainda em **06/08/2026**, respondendo *"se tivesse um torneio de novo, estaria pronto?"* — 🔁 **auditei e a resposta era NÃO: as duas telas que finalizam partida ainda divergiam em três pontos**, depois de eu já ter unificado o robô de chaveamento de manhã. (a) A **tela cheia não movia o Padelímetro** — o nível dos 4 jogadores não mudava e o extrato do perfil ficava sem a linha; só a Mesa aplicava, então **o ranking dependia de por qual tela cada placar foi lançado**. (b) A **Mesa não gerava a final do Americano** — encerrada a última rodada pela tela do dia de torneio, o Americano ficava parado esperando uma final que nenhum robô ia criar. (c) A **Mesa não disparava o "seu jogo é o próximo"**, que é o aviso mais importante do sistema e o único que vale WhatsApp. Tudo virou `Services/EncerramentoDaPartida`, chamado pelos dois lados; o robô do Americano foi junto pro motor único e a final dele passou a entrar na **grade** em vez de nascer com `DateTime.Now.AddHours(2)`. ⚠️ O top 4 do Americano ganhou **desempate por Id** pelo mesmo motivo da classificação de grupos: sem ordem TOTAL, quem entra na final dependia da ordem em que o dicionário devolveu. Junto: **"Começar agora" também volta pra página de onde veio** (o `ColocarNoAr` sempre mandava pra `/Torneios/Jogos`, e as abas mãe sumiam — mesma queixa que salvar placar e trocar quadra já tinham resolvido). Os testes novos rodam o **mesmo cenário pelas duas telas** (`[InlineData(Tela.Mesa)]` / `[InlineData(Tela.TelaCheia)]`) — é a única garantia que funciona neste projeto, porque **todo defeito grave veio de uma segunda cópia que ninguém exercitava**. **1.786 testes.**
>
> Antes: **05/08/2026** — ↺ **desfazer o clique errado na Mesa** (`build-295`). De celular, no balcão, com fila esperando, tocar no play do jogo de baixo ou finalizar a partida errada acontece — e não tinha volta. **Ao Vivo → Agendada**: o jogo volta pra fila, hora e quadra ficam, placar zera. **Finalizada → Ao Vivo**: reabrir, e aqui mora o trabalho. ⚠️ **Finalizar dispara uma cascata** — carimba a fase do perdedor, move o Padelímetro dos 4 jogadores e MANDA O ROBÔ CRIAR A FASE SEGUINTE quando era o último jogo da fase. Voltar só o status deixaria a categoria com uma semifinal montada a partir de um resultado apagado. Então o reabrir apaga os jogos das fases posteriores (renascem ao confirmar), tira o carimbo do perdedor, devolve o campeão a finalista, desanda o Padelímetro (linha some do extrato, nível volta ao `NivelAntes`) e destranca o torneio. ⚠️ **RECUSA se a fase seguinte já começou** — apagar uma semifinal com gente em quadra é pior que um placar errado, que se corrige pelo lápis. ⚠️ **O push já foi**: os 4 jogadores receberam o resultado no celular e isso não volta. Fase fora da corrente (Americano, seed antigo) nunca conta como "posterior". **1.704 testes.**
>
> Antes, no mesmo dia — 🔑 **o admin abria a Mesa e levava 403 no placar** (`build-293`). Achado ao conferir se o dono do Padelizou (admin, mas **não** organizador do Interno) conseguiria rodar o torneio da noite: conseguiria abrir a Mesa e **não conseguiria lançar um placar sequer**. As duas telas usavam critérios DIFERENTES — a Mesa chama `EhOrganizadorAsync` (que aceita `IsAdminRaiz`/`IsAdminGeral` desde 31/07), e `PartidasController.PodeControlarPlacarAsync` olhava só a tabela `TorneioOrganizador`. Era o único ponto do sistema sem essa passagem. ⚠️ **Corrigido no CÓDIGO, não na conta**: vale pra qualquer torneio que o admin precise socorrer, sem mexer na lista de organizadores do dono. Antes, no mesmo dia: **folga entre fases virou preferência** (`build-292`) — evitar jogo seguido só faz sentido se houver OUTRO jogo pra pôr no meio; com quadra sobrando a folga vira quadra parada, e o intercalar continua acontecendo sozinho porque o horário cheio empurra a fase seguinte. Junto: **hora e quadra no quadro** da aba Chaves e Grupos (mesma projeção da aba Jogos) e **"Vencedor Primeira Rodada 1" virou LINK** pro jogo citado (`Services/AncoraDoJogo`; ⚠️ o "ª" de "6ª Categoria" é LETRA pra Unicode e passava pro id — filtro virou faixa `a-z0-9`). E o `dev` recebeu o mesmo `TZ=America/Sao_Paulo`. **1.695 testes.**
>
> Antes, no mesmo dia — 🚫 **a regra é PESSOA, não fase** (`build-289`). Cheguei a proibir final e semifinal de dividirem horário em qualquer categoria; **o organizador corrigiu**: entre categorias diferentes são pessoas diferentes, não há nada de impossível nisso, e adiar a final só empurraria o encerramento. A única coisa impossível é **a mesma pessoa em dois jogos ao mesmo tempo** — dentro de UMA categoria isso já é estrutural (a folga entre fases nunca deixa a final encostar na semi que a decide), e entre categorias quem garante é o encaixe, que compara PESSOA. ⚠️ **O risco não estava no sorteio** (isso já era testado) **e sim depois**: cada rodada nova do mata-mata nasce pelo robô com a grade já cheia, e o Mata-Mata Geral usa os MESMOS 48 homens das outras categorias. Teste novo joga o **Interno inteiro** (6 categorias, 5 quadras, do primeiro jogo ao campeão) conferindo todos os horários de todas as fases — **zero conflitos**. ⚠️ **A PREVISÃO não tem essa garantia e não tem como ter**: ela fala em "Vencedor Quartas de Final 1"; quando der choque, o jogo real sai um horário depois da prévia. Junto no mesmo deploy: **mudar a quadra de um jogo sem mexer na hora** (quadra ocupada no mesmo horário TROCA de dono em vez de recusar — ver `Services/TrocaDeQuadra`). **1.684 testes.**
>
> Antes, no mesmo dia — 🕒 **o VPS estava em UTC e o app fala hora de Brasília** (`build-284` + `TZ=America/Sao_Paulo` no systemd). Todo `HorarioPrevisto` no banco é hora de relógio local e o `IcsBuilder` converte Brasília→UTC somando 3h explicitamente — mas o servidor rodava em `Etc/UTC` **sem `TZ` no processo**, então `DateTime.Now` vinha **3h à frente da quadra**. Efeitos que já estavam valendo, sem ninguém ver: todo jogo apareceria **"atrasado" desde o primeiro minuto**, `HorarioInicioReal`/`HorarioFimReal` gravados 3h errados (partida das 20h registrada como 23h), e o recálculo por atraso jogaria a grade pro dia seguinte. Achado ao ligar o "Recalcular horários" em `DateTime.Now`. ⚠️ **Conferir no `/proc/<pid>/environ`, não no `timedatectl`** — o que vale é o ambiente do processo. Falta aplicar o mesmo no `dev`. Junto: **preencher as vagas do mata-mata sempre foi automático** (o robô cria a rodada seguinte ao finalizar a última partida de uma fase — teste novo tranca isso), e o **"Recalcular horários"** deixou de recusar torneio já começado, que é justamente quando ele serve: remarca só o que está "Agendada", não toca em finalizado nem em quem está em quadra (os dois contam como quadra ocupada), parte de AGORA e nunca antes de a quadra do jogo em andamento vagar. **1.653 testes.**
>
> Antes, no mesmo dia — 🏁 **a chave direta ABRE o torneio, e dá pra refazer a grade** (`build-282`). A primeira rodada de uma chave direta não espera resultado de ninguém — as duplas já estão definidas — e é ela que tem MAIS FASES pela frente: 24 duplas são **cinco rodadas** até a final, contra as três de uma categoria de grupos. A regra "grupo primeiro, mata-mata depois" a tratava como mata-mata e a mandava pro fim, empurrando as cinco rodadas junto. Agora ela abre o torneio; a regra antiga continua valendo pro mata-mata que SAI dos grupos (teste separado tranca cada metade). ⚠️ **Medido no Interno replicado** (86 jogos, 5 quadras, 11 min): ordem antiga **209 min**, ordem nova **198 min**, **piso aritmético 189 min** — ou seja, terminar antes disso não é problema de agendamento, é de aritmética: 86 jogos em 5 quadras a 11 min são 3h09 de quadra ocupada sem um minuto de folga. O que move o ponteiro é jogo de 10 min, começar mais cedo, chave geral com 16 duplas (15 jogos em vez de 23) ou uma sexta quadra. Um teste novo tranca o desperdício: a grade tem que caber em `ceil(jogos/quadras)` levas mais duas de folga. Junto: **"Refazer grade de horários"** (botão do organizador, só antes do primeiro jogo) recalcula hora e quadra de TODOS os jogos sem mexer nos confrontos — sorteio e grade são coisas diferentes, e até agora só dava pra trocar dois jogos de lugar. **1.652 testes.**
>
> Antes, no mesmo dia — 📍 **quadra em todo jogo, do primeiro ao último** (`build-277`). Os jogos projetados apareciam sem quadra, e não era campo esquecido na tela: **não havia COMO saber qual**, porque cada categoria calculava o próprio horário por conta própria — o print do Interno mostrava **oito jogos às 22:23 num torneio de cinco quadras**. Quadra é pergunta do **torneio**, não da categoria, então a projeção virou duas passadas: a **estrutura** (quem joga com quem, por categoria, sem hora) e a **grade** (todas as cadeias no mesmo relógio, junto com o que já está marcado de verdade) — horário lotado empurra pro seguinte e cada jogo pega a primeira quadra livre. A grade de verdade veio junto: `Encaixar` recebe os jogos JÁ MARCADOS e semeia a ocupação com eles (pessoas **e** nomes de quadra), a quadra deixou de sair da POSIÇÃO na fila e passou a ser a primeira **livre**, e `AgendarNaGradeAsync` ancora a rodada nova no fim da fase anterior da **própria categoria**. ⚠️ **O âncora antigo enfileirava as categorias** — com 5 quadras e 5 categorias cada semifinal esperava a alheia e quatro quadras ficavam paradas; só dá pra emendar em paralelo porque o encaixe agora confere **pessoa por pessoa** contra o que já está marcado (teste percorre o torneio inteiro com duas categorias dividindo os mesmos 12 jogadores). ⚠️ **Nome de quadra vem dos JOGOS, não do cadastro**: no Interno os jogos dizem "Quadra A".."Quadra E" e o cadastro passou a dizer "Quadra 1".."Quadra 5" — renomear depois do sorteio não reescreve os jogos, e nomear pelo cadastro poria dois nomes pra mesma quadra na mesma tela. **1.647 testes.**
>
> Antes, no mesmo dia — ⏱️ **uma fase nunca abre no horário da fase que a decide** (`build-275`). A tela mostrava "Semifinal 2 — 22:01" e logo abaixo "Final — 22:01": os dois finalistas ainda estariam em quadra jogando a semi. A projeção contava as vagas de quadra de forma **corrida, atravessando a fronteira entre as rodadas** — com 5 quadras, as 2 semifinais ocupavam 2 delas, sobravam 3 vagas naquele horário e a final entrava numa. A regra virou `GradeDeJogos.AberturaDaProximaFase`: a fase seguinte abre **uma rodada inteira depois do fim da anterior**, não no minuto em que ela acaba — só "depois do último jogo" não basta, porque é chamar a mesma dupla de volta no instante em que ela sai da quadra. É esse afastamento que faz as etapas de uma categoria acontecerem adiantadas em relação às outras. ⚠️ **Se a virada do dia já joga a fase pra manhã seguinte, o descanso NÃO é somado** — a noite inteira já é folga, e adiar mais só atrasaria a abertura do dia. Aplicado nos **dois lados**, senão a tela prometeria uma hora e o robô marcaria outra: a projeção da lista de jogos e a grade de verdade (mata-mata emendado no fim dos grupos e cada rodada nova criada pelo robô). Conferido no navegador: `grupos 02:30 → oitavas 04:10 → quartas 05:50/06:40 → semis 08:20 → final 10:00`. **1.635 testes**, incluindo varredura de 1 a 8 quadras — é justamente quando a rodada **não** enche as quadras que a conta antiga furava.
>
> Antes, no mesmo dia — 🎓 **o professor avalia o aluno, e decide se o aluno vê** (`build-274`). Feature nova, feita a partir da **planilha que o Felipe mandou** — o sistema de avaliação técnica de um professor de verdade: 146 fundamentos em 3 módulos e 10 famílias, com duas réguas (execução **A** plástica · **B** técnica · **C** com direção · **D** longa e lenta; e acertos 1–10, de "baixo" a "aplicável em jogo"). Cada professor **recebe essa lista e edita a dele** (acrescenta, renomeia, tira). Ficha por aluno, com **recado pro aluno** e **anotação privada**, e o botão **"mostrar pro aluno" ficha por ficha** — ela nasce fechada. Liberada, o aluno vê as notas traduzidas e a **evolução** entre fichas ("era C, agora é B", "acertos: 9 → 6", "avaliado pela primeira vez"). **As travas são no servidor**: a anotação privada não sai da consulta (o tipo que chega no aluno nem tem o campo — esconder com `@if` na view é publicar), a ficha exige que o aluno seja aluno DELE, e nota só entra em fundamento DELE. ⚠️ **"Tirar da régua" DESATIVA, não apaga** — nota que o aluno já levou não pode sumir porque o professor mudou o método meses depois; e a semeadura roda **uma vez**, senão um item apagado de propósito voltaria a cada visita. ⚠️ **Como cada um edita a própria régua, a nota NÃO é comparável entre professores** — a evolução só compara fichas do mesmo professor, e isso nunca pode virar ranking global. Conferido no navegador com professor e aluna de verdade, incluindo um segredo plantado na anotação privada que **não apareceu nem depois de liberar**. **1.624 testes.**
>
> Antes, no mesmo dia — 🏆 **Semifinal e Final aparecem em Agendadas antes de existirem** (`build-271`). O motor cria cada rodada do mata-mata só quando a anterior fecha (`Dupla1Id`/`Dupla2Id` são obrigatórios — não existe partida "a definir" no banco), e o efeito na tela era que **as fases seguintes não existiam pra quem olhava**: o jogador via a primeira rodada e mais nada, sem saber a que horas voltar nem contra quem pode jogar. Agora entram na lista, **na ordem do horário, junto com os jogos marcados** — o horário sai da duração configurada no torneio e das mesmas regras de grade do resto (`GradeDeJogos.DepoisDe`: número de quadras, virada de dia), então **não é previsão, é slot**; atrasar na prática não muda (decisão do Felipe). O que está "a definir" é QUEM joga: na rodada seguinte é **exato** ("Vencedor de Ana/Bruno × Carla/Diego" — só dois podem chegar), nas mais adiantadas vira "Um destes 4", e quem pegou **bye aparece pelo nome**. Sem palpitrômetro nem botão de organizador na linha: não há partida pra votar nem pra editar. 🐛 **Bug pego escrevendo**: a primeira versão encadeava o NOME da fase (`ProximaFase("Primeira Rodada")` = "Oitavas"), mas os robôs de verdade nomeiam por `NomeFase(quantos avançam)` — numa chave com bye, 2 jogos + 2 descansadas são 4 duplas, ou seja **Semifinal**, e a tela anunciaria uma fase que aquele torneio nunca criaria. **1.583 testes.**
>
> Antes, no mesmo dia — 🔑 **quem já tem conta entra pelo próprio login** (`build-269`): o botão "Entrar" da barra — o único caminho fácil no celular — nunca mandava `returnUrl`, então quem estava vendo um torneio caía no PERFIL e tinha que achar o torneio de novo. Agora, na falta de destino explícito, vale **de onde a pessoa veio** (Referer, só do mesmo host), e sem destino nenhum vai pro **início**, não pro perfil. Resolver na origem cobre os 7 links de hoje e os que alguém escrever amanhã sem lembrar.
>
> Antes, no mesmo dia — 🤝 **quem ajuda a organizar não vê o dinheiro do torneio** (`build-264`). Era tudo ou nada: quem entrava pra lançar placar via quanto o torneio faturou. Agora o ajudante faz tudo — placar, chaves, configurações e **marcar quem já acertou** — e nenhum valor sai da tela dele: some o topo, o "Por categoria", o valor de cada linha (vira selo "pago"/"em aberto") e o botão de cobrar no WhatsApp (a mensagem leva a quantia e a chave Pix do criador). A tela nem se chama "Financeiro" pra ele: vira **"Quem já acertou"**, com uma linha explicando por que não há números — senão lê como página quebrada. O **bloco financeiro do relatório** sai (o pódio fica: foi ele que fez acontecer) e a **área da taxa de 5% recusa no SERVIDOR**, não só escondendo o botão. O aviso mora onde se decide, dentro da caixa "Adicionar novo organizador". ⚠️ **Conferir dado antes de escrever a regra valeu**: existe um nível `"Total"` numa linha de prod ajustada na mão (torneio 19) que o código nunca escreve — só com `"Criador"` o Felipe ficaria trancado fora do próprio caixa; os dois são aceitos, e nenhum torneio em prod ou dev fica sem dono do dinheiro. 🐛 De quebra, um defeito antigo achado ao testar a recusa: `AccessDeniedPath` apontava pra `/Auth/AcessoNegado`, **que nunca existiu** — todo Forbid do site inteiro caía num 404, e a pessoa lia "página não encontrada" e insistia. **1.553 testes.**
>
> Antes, no mesmo dia — 🔓 **quem já tem conta entra pelo próprio login, sem a senha do portão** (`build-260`). O Acesso Antecipado vinha antes de TUDO, inclusive do login: um jogador de verdade que trocasse de celular, limpasse o navegador ou passasse dos 90 dias do cookie ficava trancado do lado de fora do **próprio cadastro**, e a única saída era pedir de novo uma senha compartilhada que ele não tem motivo pra guardar. São **duas peças, e sem a segunda a primeira não serve**: (1) `/Auth/Login` — mais Logout, EsqueciSenha e RedefinirSenha — saem de trás do portão (a recuperação vem junto de propósito: quem esqueceu a senha da conta é exatamente quem não tem a do portão); (2) **estar logado passa a valer como passe**, senão a pessoa fazia login e o clique seguinte a jogava de volta. **`/Auth/Cadastro` continua fora**: conta nova segue por convite, que é o que o portão existe pra controlar — e há teste que quebra se alguém escrever só `/Auth` na lista e abrir o cadastro junto. A tela do portão ganhou **"Já tem conta? Entrar com meu login"**, levando o `returnUrl`, senão o caminho existiria e ninguém acharia. ⚠️ **Consequência**: trocar a senha do portão **não expulsa mais quem tem conta** — era o remédio documentado pra vazamento, e agora vale só pra quem entrou só pelo portão; pra quem tem conta, mexe-se na conta (comentário do `AcessoAntecipadoSettings` corrigido, dizia o contrário). Conferido com o portão **LIGADO** (em Development ele nasce desligado, então testar lá não prova nada) e depois ao vivo em dev e prod. **1.533 testes.**
>
> Antes, na mesma madrugada — 👍 **um elogio e um comentário por pessoa em cada perfil** (`build-258`), trocáveis e editáveis. Dava pra marcar os **18** elogios no mesmo perfil e deixar quantos comentários quisesse: o número do badge deixava de dizer "quantas pessoas acham isso" e passava a dizer "quem clicou mais" — uma pessoa sozinha levava alguém à conquista "Querido da Quadra". **Não era teoria:** medindo o estrago em produção, a tabela cresceu DEBAIXO da consulta (17 → 22 → 23 linhas em minutos), com uma jogadora passando de perfil em perfil dando 3 elogios em cada naquele momento. Agora clicar em outro **troca** e avisa o que saiu e o que entrou; comentar de novo **edita** (o campo volta preenchido, o botão vira "Salvar alteração") e limpa a denúncia da frase antiga — mas reenviar o MESMO texto não limpa, senão bastava reenviar pra sair da fila do admin. Garantia no banco, não só no controller: índice único `(De, Para)` no Elogio e `(Autor, Perfil)` no Comentário. **A migração limpa o que já existe ANTES de criar o índice** — senão ela falha no start e o site não sobe —, mantendo o mais recente de cada par: **26 → 14 elogios em prod, exatamente as 12 linhas previstas**, com backup das duas tabelas em `/opt/padelizou-shared/backups-manuais/`. O `DELETE` foi ensaiado contra o Postgres local com duplicados plantados de propósito antes de encostar em dado real. **1.513 testes.**
>
> Antes, na mesma madrugada — 🎨 **a tela de jogos ficou legível**: no celular o confronto lia como **lista de quatro nomes** (o "×" era escondido no empilhamento) e o placar finalizado saía **partido** — "9" no canto e "5" antes do nome na linha de baixo; no desktop o "×" boiava num buraco porque o nome tem `flex: 1 1 auto` (pro ellipsis) e o `justify-content` não tinha o que empurrar — o CSS do componente tinha **duas definições concorrentes** e a morta escondia o defeito. Junto: **chave em cima e grupos embaixo quando o mata-mata já começou** (order do flex, sem duplicar markup), **prévia da chave empilhada no celular** (cortava em "QUARTAS DE F..." sem pista de rolagem), **chip compacto** na tabela do grupo (nome não quebra mais em três alturas; o `d-block` do Bootstrap exigiu `!important`), palpitrômetro do Ao Vivo **só quando alguém votou**, **"Editar jogo" escondido do torcedor**, pills com a cara do site (o ativo azul do Bootstrap + texto vermelho era ilegível; o vermelho virou bolinha pulsante), **filtro que aplica sozinho** e o select de time **só com times do torneio** (vinha o sistema inteiro). **1.513 testes** verdes.
> Antes: **04/08/2026 (madrugada)** — 🧰 **a véspera do Interno virou uma fila de acertos**: **confirmação bonita** no lugar do `confirm()` do navegador (que ignora o tema, abre longe do botão e não tem espaço pra CONSEQUÊNCIA — agora o de sortear LISTA POR NOME quem vai ficar de fora); **duração do jogo editável** e **torneio sem horário previsto**, por ordem de liberação, que é como roda a maioria dos internos; **Agendadas e Finalizadas viraram LINHA** de 79px (o cartão gigante ficou só no Ao Vivo — com 48 jogos ele era uma parede); e **finalizar pela regra de games da FASE**, com o limite escrito na tela e um atalho quando o placar bate. 🔴 No meio disso o **CI pegou o que a máquina não pegava**: a grade ainda dobrava gente quando nenhum jogo cabia num horário — só aparecia em ALGUNS sorteios, porque a chave direta é aleatória. Agora quadra vazia ganha da pessoa em duas quadras, e o teste roda **25 sorteios por tamanho de chave**. **1.443 testes**, `build-247`.
> Antes, no mesmo dia — 🥊 **chave direta: um mata-mata PARALELO dentro do torneio** (pedido do Virgili, véspera do Interno). 24 duplas remontadas misturando 4ª, 5ª e 6ª — os mesmos 48 jogadores em outros pares. 24 não fecha quadro, então o quadro é 32 e **8 duplas entram direto na segunda rodada**; o bye é a parte perigosa, porque quem passa direto não venceu nada e sem somá-lo ao avanço **8 duplas sumiriam sem nunca ter perdido**. A regra virou `Services/AvancoDaChave` — um lugar só, porque os dois robôs eram cópias e cada uma escolheria um campeão. Junto, dois defeitos que a feature expôs: a **grade comparava DUPLA, não pessoa** (com cada um em duas duplas, o mesmo sujeito seria chamado pra duas quadras no mesmo horário), e o **limite de games que ninguém lia** — o torneio guardava três formatos, a criação perguntava os três e a Mesa tinha `limiteGames: 9` cravado no JavaScript; o Virgili escolheu 4 e a Mesa deixava marcar 9, calada. Agora vale de verdade, é **editável depois de criado**, e semifinal acompanha a final. E a chave direta **não conta dinheiro**: contaria a mesma pessoa duas vezes no Financeiro e na taxa de 5%. **1.436 testes**, `build-242`, no ar.
> Antes, no mesmo dia — 💸 **o Financeiro do torneio "por fora" mostrava R$ 0,00 com R$ 4.080 recebidos logo abaixo**. Os quatro cartões liam só de `Pagamento` (o gateway), e no "por fora" não passa cobrança nenhuma por lá — duas verdades na mesma tela, e a de letra garrafal era a cega. Agora cada número lê da fonte certa, os rótulos mudam junto ("Você já recebeu"/"A receber"), **Estornado some** (quem devolve ali é o organizador, o sistema não sabe) e o líquido só desconta a taxa de 5% que ainda está em aberto. **A tabela "Por categoria" tinha o MESMO defeito uma camada abaixo e sobreviveu à primeira correção** (`build-238`): somava de `Pagamento` e mostrava R$ 0,00 em toda linha enquanto o topo, já corrigido, exibia o total certo — a mesma tela discordando de si mesma outra vez. As linhas saem da caderneta, **casadas por `CategoriaId` e não por nome** (nome se edita, e este projeto já teve torneio com dois nomes iguais), e a coluna "Estornado" vira **"A receber"**. Conferindo no navegador apareceu outro: com ninguém tendo pago, "LÍQUIDO PRA VOCÊ: **−R$ 12,00**" — a taxa é devida sobre TODOS os inscritos, não sobre quem já acertou, e o organizador lê negativo como prejuízo; agora para no zero e o valor devido segue escrito na linha da taxa. Junto no dia: **caixa de juntar inscrições no "Definir por CPF"** (a recusa mandava marcar uma caixa que só existia na outra aba), **time deixou de aceitar parceiro** — na tela e no servidor, porque `Jogador1Id` do time é o ORGANIZADOR e a troca penduraria um jogador na linha do time calada —, **quantos inscritos** no cabeçalho e por categoria, e **nome com a caixa arrumada** na exibição (o banco continua com o que a pessoa escreveu). **1.402 testes**, no ar em prod e dev.
>
> **Padrão do dia, anotado porque se repetiu quatro vezes:** os bugs não eram de conta errada, eram de **tela contando uma história diferente da do dado** — uma recusa apontando pra caixa inexistente, um beco sem saída pro 11º organizador, botão de parceiro numa linha de time, e o Financeiro com R$ 0,00 ao lado de R$ 4.080. Quando a causa é "esta tela lê da fonte errada", a correção certa é varrer **todos** os lugares que leem daquela fonte — não só o que está no print.
> Antes, no mesmo dia — 👨‍🏫 **marcar aula deixou de começar catando o telefone do aluno** (2º pedido do Jonatas). **Telefone virou opcional**; a lista de quem já fez aula com ele vem pronta na página e filtra sem acento, com selo **"aluno recorrente"** pra quem já voltou; e existe **"Procurar no Padelizou"** sob demanda, nunca por tecla (quadra com sinal ruim). O ganho escondido: escolher alguém com conta agora grava o **vínculo de verdade** (`Aula.AlunoId`) — antes a aula nascia como nome solto mesmo pra quem estava cadastrado, e o aluno não via nada no próprio app. Aluno avulso que se cadastrar depois é ligado à conta pelo painel, com o **acordo de preço indo junto** (ele foi combinado com a pessoa, não com o nome). **1.339 testes.**
> Antes: **03/08/2026 (noite)** — ⬅️ **seta de voltar na barra**, em toda tela que não é destino do menu. Entrar no perfil de alguém e não ter saída era o caso apontado, mas valia pra dezenas de telas — e **no app instalado não existe botão do navegador**: em PWA no iPhone a pessoa ficava presa. Não é `history.back()` puro: nasce como link com destino de reserva e só vira "voltar" quando a pessoa veio de dentro do site, senão quem abriu o link do WhatsApp sairia do Padelizou. Junto, **a barra parou de estourar em notebook pequeno**: entre 992 e 1199px os sete itens do menu abriam todos e não cabiam (1221px de conteúdo pra 985px de tela), o que dava rolagem lateral em janela não maximizada e iPad deitado. Era anterior à seta. Corrigido no ponto de quebra (`navbar-expand-lg` → `xl`), com as **três regras de CSS que dependiam de "menu recolhido"** movidas junto pra 1199.98 — senão os alvos de toque encolheriam justo onde o dedo precisa deles. O remendo antigo (`max-width` no nome do usuário) mirava 58px; faltavam 236. Medido em 8 larguras, rolagem zero em todas. **1.309 testes.**
> Antes, no mesmo dia — 🏦 **o organizador abre a conta de recebimento sem sair do Padelizou** (subconta por API). Eram 5 passos, 2 fora do site; virou um formulário aqui dentro. **Endereço e data de nascimento passam e NÃO ficam guardados**, e as duas chaves que o gateway devolve (`apiKey` e `accessToken`, que movem o dinheiro dele) são descartadas — pro split basta o `walletId`. O teste no sandbox achou o que a documentação esconde: **`birthDate` é obrigatório** e não está na lista. ⚠️ **Período de Avaliação**: conta aprovada NÃO isenta — são **10 subcontas em 60 dias** a partir da primeira; o teto de R$ 2.000 conta o que a subconta EMITE, e aqui ela nunca emite (quem emite é a conta-mãe). Roteiro pronto pra WhatsApp em **[MANUAL-CONTA-RECEBIMENTO.md](MANUAL-CONTA-RECEBIMENTO.md)**. **1.281 testes.**
> Antes, no mesmo dia — 💸 **fim da armadilha mais cara do sistema**: "pelo site" vinha marcado por padrão e **nada checava se o organizador tinha conta pra receber**. Sem conta, a cobrança não nascia e o torneio rodava sem cobrar ninguém — sem erro, sem aviso, descoberto só ao procurar o dinheiro. Agora as opções nascem **travadas** pra quem não conectou, "Por fora" já vem marcado, e o servidor recusa quem manda o formulário na mão. Professor e clube **avisam em vez de bloquear** (combinar na quadra é o jeito normal de dar aula). Tela de recebimento reescrita com passo a passo e prazo de cada forma. Roteiro do gerente de conta e ⚠️ **risco do período regulatório** em **[RECEBIMENTO.md](RECEBIMENTO.md)**. **1.255 testes.**
> Antes, no mesmo dia — 🍻 **elogio novo: "Bom de Copo"**. O terceiro tempo faz parte do jogo, e quem é bom de mesa depois também é lembrado na hora de montar dupla. Entra na convivência, junto com Boa Vibe e Look Bonito. Uma linha em `Services/CatalogoElogios.cs`: a tela lê o catálogo inteiro, e `Elogio.Tipo` é texto validado contra ele — sem migração. **1.242 testes.**
> Antes, no mesmo dia — 🧹 **a fila do teste real do "Interno Los Corneteiros"**: **ninguém entra 2× na mesma categoria** (inscrição solo existente vira oferta de juntar; dupla fechada recusa), **nome que pareça nome e CPF com dígito verificador** (fim do jogador "." com CPF inventado), e o **celular parou de quebrar** na Home (título truncado em "Torneio d..."), na Agenda (barra do calendário) e no Gerenciar Inscritos (lista torta). **1.242 testes.**
> Antes, no mesmo dia — 🎯 **nasceu o Padelímetro** (fase 1: só mostrar), o nosso ataque ao maior trunfo do concorrente. Nível 0–1000 por jogador, Elo por jogo de torneio, seed pela categoria da estreia, extrato com o porquê de cada movimento no perfil, replay determinístico no admin. Espec completa em **[RANKING.md](RANKING.md)** (duas trilhas: nível decide ONDE joga, ponto anual premia). **1.195 testes.**
> Antes, no mesmo dia — 📊 **métricas do admin por dia, semana ou mês** (semana só respondia "estamos crescendo?"; dia responde "o que aconteceu hoje?" e mês responde "como está o ano?", que é a conta do teto do MEI). **1.165 testes.**
> Antes, no mesmo dia — 👨‍🏫 **o primeiro professor de verdade usou e trouxe cinco problemas** (o Jonatas): **aula em dupla e trio com preço próprio**, **preço combinado por aluno**, **apagar um local**, **excluir uma aula** e o app que **seguia pedindo pra instalar depois de instalado**. Todos com a mesma raiz — o código supunha um professor mais simples do que o professor real. Junto, o **CI passou a dizer qual teste caiu** sem precisar de login. **1.132 testes**, no ar em prod e dev (`build-204`).
> Antes: **31/07/2026** — 🗓️ **categorias editáveis depois de publicado** (`build-186`: adicionar, mudar limite de vagas e remover — só sai categoria VAZIA, com inscrições abertas, e nunca a última) e **check-in do dia virou opcional** (`build-191`: nasce ligado, desligado some o botão **e** a rota recusa). **1.023 testes.**
> Antes, no mesmo dia — 🔑 **admin manda em qualquer torneio** (`build-184`), pra socorrer organizador travado sem depender dele; **estorno automático** (desfaz a inscrição e chama a fila) e **caderneta de cobrança do "por fora"** no Financeiro. **1.011 testes.**
> Antes, no mesmo dia — 🛠️ **a fila do "o que ainda melhorar"** foi fechada (`build-178`, prod + dev): o jogador **desiste sozinho** (e o parceiro não é arrastado junto), o sorteio **não deixa mais ninguém de fora calado**, existe **vigia de erros 500** por e-mail no VPS, e **todo aviso sai também por e-mail** — push só alcança quem instalou o app.
> Antes, no mesmo dia — 🍺 **o bar do clube nasceu, e ninguém sabe**: comanda, cardápio, caixa do dia e contas a pagar/receber, tudo atrás da chave `Bar__Habilitado` que nasce **desligada** — enquanto isso só admin do Padelizou enxerga, nem o dono do clube. Pedido de um cliente; entregue em duas fases no mesmo dia (`build-176` em dev). **967 testes.**
> Antes, no mesmo dia — 🔐 **inscrever agora exige login de quem inscreve** (o parceiro segue sem precisar de conta, e assume o pré-cadastro pelo CPF quando se cadastrar). Conferido em produção: deslogado, o POST da inscrição responde 302 pro login. **934 testes.**
> Antes, no mesmo dia — 🔎 **varredura antes do uso real**: o achado foi **texto comprido virando erro 500** — login de 31 letras no cadastro (a primeira tela de quem chega), nome colado da agenda na inscrição, descrição colada no nome do torneio. Colunas `varchar(n)` **recusam**, não cortam. Corrigido com aviso + `maxlength`, e **preço negativo** também recusado. Autorização, CSRF, upload, XSS, segredos e cabeçalhos conferidos um a um — sem furo. **921 testes.** Ver a seção do dia.
> Antes, no mesmo dia — 🐛 **achado o bug do dinheiro**: em `pt-BR` o sistema lia **R$ 79,90 como R$ 7.990,00** e nunca tinha estourado porque todo preço em uso era redondo. Corrigido e publicado em prod e dev (`build-165`). Junto: publicar torneio agora **dá sinal de vida** (o "não acontece nada" era recusa nascendo no topo enquanto o botão fica no pé), **chave Pix + recado + datas previstas** no torneio, **"Sou eu"** e **um impedimento só** na inscrição, **troféu com o nome da categoria** (e o diamante virou taça), e o **Pnatinha saiu das telas vazias**. **889 testes.**
> Antes: **30/07/2026 (noite)** — **o app de celular ficou de pé**: instalar virou um toque no Android, o iPhone parou de dizer "não suporta notificações" pra quem só precisava instalar, e sem sinal aparece uma tela nossa no lugar do dinossauro do Chrome. Decisão: **fica só no PWA, sem loja por enquanto** — a loja não muda o app, só ajuda a ser achado. **821 testes.**
> Antes, na mesma noite — 🎉 **o primeiro usuário real entrou** (Lucas "Foka", 15:46) e o uso de verdade achou um defeito em minutos: dava pra criar o **mesmo torneio duas vezes**. Corrigido. Produção limpa dos testes dele, com backup antes. Junto: o **"Painel Admin" do menu**
> deixou de abrir aba nova (e de mandar pra produção quem clicava no localhost), e as
> **conquistas foram de 12 pra 25** (vitórias até 200, títulos até decacampeão), agora abaixo
> dos Elogios. E **todo aviso passou a tentar o WhatsApp da pessoa** além da notificação, por
> uma **Evolution API no nosso próprio VPS (R$ 0/mês)** — falta só o chip pré-pago e o QR code.
> **816 testes.**
> Antes, na tarde — **primeira liberação pra gente de verdade**: organizador dos Corneteiros + primeiro professor. Portão em `Corneteiros`/`corneta`, chave de torneio escolhível (`virgili10`), trava de entrada corrigida (janela por ação) e ensaio do cadastro feito no dev. Ver [PRIMEIROS-USUARIOS.md](PRIMEIROS-USUARIOS.md). **699 testes.**
> Manhã do mesmo dia — varredura completa do sistema e os achados dela fechados: **trava de força-bruta** (login por conta, resto por IP), **cabeçalhos de segurança** no Caddy, **denúncia de comentário** com fila no admin, **convite de parceiro por link** (fim do CPF do outro na mão — o maior atrito da inscrição), **AulasController em 7 partials** e o **roteiro de estorno**. **681 testes.**
> Anterior: **29/07/2026 (madrugada)** — as respostas do Felipe viraram código: **professor assinante existe** (15 dias de teste → R$ 49,90 + 3%/6%, ou avulso 10%), **piso de comissão por tipo** (Aula/Jogo R$ 1), **a condição dos 5% virou trava** (encerrar inscrições → pagar/negociar → chaves liberam via webhook), **boleto herda os 10% do Pix** e o **TorneiosController virou 8 partials** (nenhuma rota mudou). **650 testes**, publicado em dev e prod.
> ✅ **Chave do backup guardada fora do servidor** (29/07): o Felipe copiou pro gerenciador de senhas dele. Conferido antes que o arquivo existe (337 bytes, 9 linhas, chmod 600) e que a chave em uso ABRE o cofre — ele copiou a certa, não uma versão velha. Fecha o furo em que o backup seria inútil justo quando o servidor morresse.
> ✅ **Os dois pendentes com o Google/pagamento fecharam em 29/07:** app do Google **publicado** ("Em produção" — o token do backup não expira mais a cada 7 dias, sem custo e sem verificação) e o **mistério do webhook resolvido**: era mesmo sobra do sandbox apontando pra produção, e o Asaas já o tinha interrompido sozinho. Apagado; produção nunca falhou (recusou um impostor, como devia).

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

### 06/08/2026 (fim da noite) — 🚨 Erro em produção deixou de ser invisível

Até hoje, um erro 500 mostrava a tela amiga e **acabava ali**: o rastro só existia no
`journalctl` do VPS, e ninguém ia lá sem motivo. Na prática o sistema só avisava de erro
quando um usuário reclamava — e com o lançamento público em 08/08 isso significa descobrir
problema pelo WhatsApp, no meio de um torneio.

**Agora todo erro não tratado vira três coisas:** uma linha em `ErroDoSistema`, um **push +
e-mail** pros administradores raiz, e uma tela em `/Admin/Erros` com o detalhe completo (tipo,
caminho, quem estava logado, stack trace) — sem precisar de `ssh`.

**A infraestrutura de avisar já existia**, o que faltava era ligar o erro nela: o aviso sai
pelo `EnviarParaJogadorAsync`, que só **enfileira** (push e e-mail saem no entregador de
fundo). Isso importa mais aqui do que em qualquer outro lugar — o código roda dentro de uma
requisição que **já falhou**, e segurá-la esperando SMTP só pioraria a queda.

**Janela de silêncio de 6h, pela mesma razão do vigia do backup:** alerta que se repete vira
ruído, e ruído ensina a ignorar. O **primeiro** estouro de cada `(tipo, caminho)` avisa na
hora; as repetições ficam só no registro — que é onde se vê "está acontecendo direto". Erro
que continua vivo 6h depois **reavisa**, porque aí é erro que ninguém corrigiu. Retenção de
90 dias, com a limpeza pegando carona no aviso.

🔴 **Defeito achado na verificação em tela, não na revisão do código.** O `UseExceptionHandler`
reescreve o caminho da requisição pra `/Home/Error` **antes** de chamar o handler — então a
primeira versão gravava o destino no lugar da origem. Onze erros foram registrados apontando
todos pro mesmo lugar, ou seja, o registro guardava **a única informação que não serve pra
nada**: ele existe justamente pra dizer ONDE quebrou. O caminho de verdade mora no
`IExceptionHandlerPathFeature`. Dois testes prendem os dois lados (com e sem a feature).

**Botão "Testar o vigia"** no `/Admin/Erros`, mesmo espírito do teste de aviso que já existia:
estoura um erro de verdade e percorre o caminho inteiro. Alarme que só se manifesta no dia do
desastre é alarme que ninguém sabe se está ligado — e no que não se testa, não se confia.

🔴 **E um erro MEU, achado em produção:** acrescentei ao app os cabeçalhos `nosniff`,
`X-Frame-Options` e `Referrer-Policy` "que faltavam" — mas eles **já existiam no Caddy desde
30/07**, nos blocos que cobrem `padelizou.com.br` (com www e admin) e `dev.padelizou.com.br`.
A varredura procurou só no C# e não olhou a configuração do proxy. Em produção cada cabeçalho
passou a chegar **duas vezes**, e a RFC 7034 diz que servidor não deve mandar mais de um
`X-Frame-Options` — duplicar não protegia mais, protegia menos. Removidos do app no
build seguinte, com o porquê escrito no `Program.cs` pra ninguém "consertar" de novo.
**Lição pro próximo diagnóstico: cabeçalho, TLS, cache e redirecionamento moram no Caddy, e o
Caddyfile não está no repositório** — grep no código não enxerga essa camada.

⚠️ **Pra testar comportamento que só existe em Produção**, o `Development` não serve: lá o
`UseExceptionHandler` nem entra no pipeline, e o `/Admin` não exige o subdomínio. O jeito é
rodar o **binário Release** e falar com ele por `curl -H "Host: admin.padelizou.com.br"`
(entrada `padelizou-release` no `.claude/launch.json`, porta 5039 — o `dotnet run` normal não
serve porque a outra sessão trava o binário Debug). Verificado assim de ponta a ponta: erro
disparado, linha gravada com o caminho certo, aviso marcado, duas repetições caladas pela
janela, tela listando os três. **1.866 testes.**

### 06/08/2026 (noite) — 🤝 Ranking RS no ar, publicado à mão com o GitHub fora

O **GitHub Actions entrou em `major_outage`** e travou a esteira: os últimos commits ficaram
sem tag porque o job "Build + testes" esperava runner por 15 min e era cancelado (os testes
nunca falharam — quando o job rodou, passou). Como o `deploy.sh` só instala build do CI, o
Ranking RS ficou 5 horas pronto e não publicado.

Publicado **manualmente** com o mesmo cuidado da esteira automática:
- pacote `dotnet publish -c Release` local, igual ao comando do CI (portável, sem RID);
- `instalar-manual.sh` no VPS repete as etapas 3–6 do `deploy.sh` — dados persistentes por
  symlink, troca de versão, `/healthz` e **rollback automático**; grava no `.historico`;
- `pg_dump` de produção antes (48K, 64 tabelas), em `/opt/padelizou-shared/`;
- dev primeiro, prod depois. As duas migrações aplicaram sozinhas no start, zero erro.

⚠️ **O `publish` local vinha com os segredos junto:** existe um `Padelizou/publish/` antigo
dentro do projeto (git-ignored, nunca vazou pro repo) e o `dotnet publish` o copiava como
conteúdo — o pacote saía com **duas** cópias do `appsettings.json` real. O do CI não tem isso
(checkout limpo). Removidos antes de subir; o pacote foi conferido arquivo a arquivo.

**A chave do Ranking RS existia só no dev.** Produção não tinha drop-in nenhum, e sem chave a
integração nasce desligada por design — publicar não bastaria. Copiada pro
`/etc/systemd/system/padelizou.service.d/rankingrs.conf` (chmod 600), conferida no
`/proc/<pid>/environ` do processo de prod.

**Integração provada de verdade:** `POST /validar-categoria` a partir do VPS respondeu
**HTTP 200** com resposta bem formada ("6ª Masculina · APROVADO · Sem registros"). O que ainda
NÃO foi exercitado em produção é o caminho completo — um inscrito real sendo barrado e o
organizador liberando; esse é o teste do primeiro torneio com a conferência ligada.

Quando o Actions voltar, rodar o `deploy.sh` normal pra esteira reassumir o versionamento.

### 06/08/2026 — 🎨 Logo novo: as raquetes sem a palavra

O Felipe trocou a arte do logo. É o mesmo desenho, mas **os furos das raquetes estão alinhados**
de verdade — e a arte vem com a palavra "Padelizou" embaixo, que **não entra em ícone nenhum**:
o nome já está escrito ao lado do logo na barra, e a 38px uma palavra vira borrão.

**Recorte por cor, não por retângulo.** Cortar a arte por altura deixaria o fundo escuro da
palavra pendurado no ícone. As raquetes são recortadas por **máscara de cor** e recompostas
sobre um disco/placa sintetizado do próprio fundo da arte — assim a palavra some sem rastro e o
enquadramento de cada ícone é escolhido, não herdado.

**A máscara mudou de critério, e isso foi o trabalho.** A versão anterior usava
`G − max(R,B)`, que serviu pra arte antiga. O verde novo é **amarelado** (`#b5d33a`): nesse
critério dá só ~30, e a rampa 10..50 deixava o **miolo** da raquete meio transparente —
a raquete da direita saía lavada de amarelo. `G − média(R,B)` separa de verdade: verde ~90,
fundo escuro ~−4, furos pretos ~0, branco 0. Rampa 15..55.

**Cor da borda vai sem des-misturar do fundo.** A primeira tentativa dividia pelo alfa pra
recuperar a cor pura na borda — matematicamente certo, e desenhava um **halo claro** em volta
do desenho. Todo lugar que usa a versão transparente tem fundo escuro (barra, rodapé, capa,
disco), onde franja escura some e halo claro apareceria. Cor observada, e pronto.

**Os 7 arquivos foram regerados** — `logo-raquetes.webp` (400×326), `logo-icon.webp` (256),
`favicon-32.png` (64), `apple-touch-icon.png` (180, opaco), `icon-192`, `icon-512` (maskable,
raquetes nos 60% centrais) e o `favicon.ico` da raiz (16/32/48), que o navegador pede sozinho
mesmo com o `<link>` apontando pro PNG.

**Nenhum CSS mudou, e isso foi conferido, não presumido.** A proporção nova é 1,227 contra
1,22 da anterior: nos mesmos **38px de largura** da barra o desenho fecha em **31px de altura**,
exatamente como antes — a conta do estouro da navbar em 1280px continua valendo. Verificado no
navegador: a barra serve o arquivo novo (400×326) renderizado em 38×31.

⚠️ **`CACHE_NAME` do Service Worker foi de v4 pra v5.** Sem isso quem já instalou o app ficaria
com o logo antigo pra sempre.

**A ferramenta ficou no repo** (`antigo/gerar-icones/`, fora do `Padelizou.slnx` — CI e deploy
buildam o `.slnx`, então ela não entra em pacote nenhum). Regerar o conjunto inteiro é uma linha,
documentada em `antigo/LEIA-ME.md` junto do porquê de cada tamanho. Da última troca de logo não
sobrou ferramenta, e por isso este trabalho começou do zero.

**Com um terceiro argumento ela grava também o logo em alta** (PNG sem perda, pra camiseta, banner,
Instagram): redondo e quadrado em 1024, e as raquetes transparentes no tamanho **nativo** do
recorte. Nesse único arquivo a borda é **erodida** — a arte tem um brilho claro no contorno que
sobre o azul do site vira contorno discreto e sobre fundo **claro** vira halo de recorte mal feito,
e ele é o único que pode cair em qualquer fundo.

### 05/08/2026 (madrugada) — 🎨 A tela de jogos ficou legível

Pedido do Felipe: *"não me parece muito intuitivo nem muito bonito"*. Varredura das quatro
telas (Ao Vivo, Agendadas, Finalizadas, Chaves e Grupos) em 1385px e 390px, **deslogado** —
que é como o torcedor abre o link do WhatsApp. A estrutura estava certa; o que quebrava era
a execução.

**Os três defeitos que faziam a tela parecer errada:**

- 🔴 **No celular o confronto lia como uma lista de quatro nomes.** As duas duplas empilham
  (lado a lado em 375px truncaria as duas) e o "×" era simplesmente **escondido** — nada
  dizia que era um **contra** o outro. Agora o "×" vira **prefixo da segunda dupla**
  (`::before`), que é o que o empilhamento pedia desde o começo.

- 🔴 **O placar das finalizadas saía partido ao meio.** No HTML os games da 2ª dupla vêm
  **antes** do nome, de propósito — no desktop é o que faz os dois números abraçarem o "×".
  Empilhado, isso virava "9" no canto direito da 1ª linha e "5 Bruno / Lucas" na 2ª: ler o
  placar exigia olhar em diagonal. Resolvido com `order` no breakpoint, sem tocar no HTML
  (que o desktop precisa como está).

- 🔴 **O "×" boiava num buraco no desktop.** O comentário do CSS prometia "os dois nomes
  encostam no ×" e não acontecia: o nome tem `flex: 1 1 auto` (necessário pro ellipsis) e
  engole a metade inteira, então o `justify-content: flex-end` da linha não tem o que
  empurrar. Quem alinha é o **texto**, não a caixa. ⚠️ A causa de raiz era outra: o
  componente tinha **duas definições de `.pdz-jogo-linha*` neste mesmo arquivo**, com
  intenções opostas ("no celular NÃO empilha" × "empilha"); a segunda vencia por ordem e a
  primeira virava armadilha. Consolidadas num bloco só.

**O resto da fila, na mesma leva:**

- **Chave em cima, grupos embaixo — quando o mata-mata já começou.** No celular a aba tinha
  ~8.500px e a Fase Final morava no fim: pra ver o que está em jogo rolava-se **todos** os
  grupos, que a essa altura são história. É `order` num flex-column, sem duplicar markup.
- **A prévia da chave cortava no celular** ("QUARTAS DE F...", "Vence...") — colunas lado a
  lado com rolagem horizontal sem nenhuma pista, e Semifinal e Final ficavam invisíveis.
  Empilhada, igual à chave de verdade: as duas passam a ter a mesma cara.
- **Chip compacto na tabela do grupo**: avatar de 36→26px, clube escondido e nome truncando.
  "Fernanda Lima / Clube dos Feras" ocupava três alturas e deixava a linha com 180px enquanto
  J/V/D/SG sobravam espaço. ⚠️ O `display: none` do clube precisa de `!important` — o `d-block`
  do Bootstrap também é.
- **Palpitrômetro do Ao Vivo só quando alguém votou.** Em jogo no ar não se vota mais, então
  o bloco mostrava barra vazia + "0%" + "0 voto(s)" em cada um dos 5 cartões: parecia defeito.
  É a regra que a versão em linha já seguia.
- **"Editar jogo" sumiu pro torcedor.** Aparecia em todo cartão Ao Vivo sem checar organizador
  (a rota é `[Authorize]`, então não era furo — era convite pra uma porta fechada).
- **Pills com a cara do site**: o ativo era o **azul do Bootstrap**, que não existe na
  identidade, e "Ao Vivo" em vermelho por cima dele ficava ilegível. O vermelho virou a
  **bolinha pulsante**, os emojis viraram ícones `bi-*` (emoji rende diferente em cada
  aparelho) e a fila rola em linha única em vez de quebrar desalinhada.
- **Filtro sem botão**: aplica ao escolher — e as categorias esperam o dropdown **fechar**,
  senão marcar a segunda recarregaria a página no meio da escolha. O select de time só lista
  **times que jogam este torneio** (vinha o cadastro inteiro do sistema), e some quando não
  há nenhum; com uma categoria só, o filtro de categoria também some.
- **Quadra só quando existe** (era "Quadra não definida" em destaque em todo torneio por
  ordem de chegada), **set numa caixinha** (o "1 9" lia como 19), cronômetro sem quebrar no
  meio, título "Chaves e Grupos" (dizia "Fase de Grupos" numa tela que mostra as duas — e
  numa chave direta era mentira) e **finalizadas sem `HorarioFimReal`** caindo pro horário
  previsto em vez de flutuar em ordem arbitrária.

**1.513 testes** verdes. Conferido no navegador nas duas larguras, antes e depois, em quatro
torneios diferentes (com grupos, com mata-mata em andamento, com prévia e com chave direta).

### 04/08/2026 (fim da noite) — 🥊 Chave direta: um mata-mata paralelo dentro do torneio

Pedido do Virgili na véspera do Interno: um mata-mata **separado do chaveamento, como se
fosse outra categoria**, com 24 duplas remontadas misturando 4ª, 5ª e 6ª — na conta, os
**mesmos 48 jogadores** do torneio, cada um aparecendo uma vez, em outro par.

**`Categoria.ChaveDireta`** é a categoria que pula a fase de grupos. A regra que dá trabalho
é que **24 não fecha quadro**: o quadro é a menor potência de 2 que cabe (32), sobram 8
vagas, e essas 8 duplas **entram direto na segunda rodada**. Daí a primeira rodada tem 8
jogos (16 duplas), e o que sai dela são 16 — 8 vencedores + 8 byes — que são as Oitavas.

⚠️ **O bye é a parte perigosa da feature.** Quem passa direto não venceu nada, então não
está entre os vencedores. Sem somá-lo, os 8 vencedores fariam 4 jogos entre si e **8 duplas
sumiriam do torneio sem nunca ter perdido**. A regra de quem avança virou
`Services/AvancoDaChave`, num lugar só — os dois robôs (Mesa de Controle e Controle de
Placar) eram cópias um do outro, e é exatamente o tipo de regra em que duas cópias elegem
dois campeões diferentes. O bye é derivado de "dupla sem partida nenhuma", o que faz a regra
**se esgotar sozinha**: depois da primeira rodada todo mundo vivo já jogou.

No caminho, o robô parou de comparar os vencedores com uma **constante por nome de fase**
(Oitavas = 8 jogos...). Isso só vale na chave cheia: a primeira rodada com bye tem menos
jogos do que o nome promete, e o robô esperaria pra sempre por vencedores que nunca viriam.
Agora conta as partidas que existem — mais geral e uma dependência a menos.

**Dois defeitos que a feature expôs, e que valiam antes dela:**

- 🔴 **A grade evitava conflito por DUPLA, não por pessoa.** Enquanto cada um jogava numa
  categoria só, dava na mesma. Com a chave direta o mesmo jogador está em **duas duplas do
  mesmo torneio**, de Ids diferentes — e a grade marcaria as duas no mesmo horário, em
  quadras diferentes. Ninguém descobriria antes de o nome ser chamado duas vezes.
  `GradeDeJogos.Encaixar` agora recebe quem ocupa a quadra. **Time fica de fora de propósito**
  (cai no Id da dupla, como sempre): lá o `Jogador1Id` é o ORGANIZADOR em todos os times, e
  por pessoa todo time conflitaria com todo time, enfileirando a grade inteira.

- 🔴 **O limite de games não valia.** O torneio guarda três formatos (grupos, mata-mata,
  final) desde sempre, a tela de criação pergunta os três — e **ninguém lia**: a Mesa tinha
  `limiteGames: 9` cravado no JavaScript. O Virgili escolheu 4 games no cadastro e a Mesa
  deixava marcar até 9, calada. `Services/FormatoDaPartida` traduz fase → formato, cada jogo
  carrega o limite da **fase dele** (a mesma Mesa mostra um jogo de grupo até 4 e uma
  semifinal até 6, lado a lado), e os campos viraram **editáveis depois de criado** — ninguém
  acerta o tamanho do jogo antes de saber quanta gente entrou. **Semifinal acompanha a
  final**, não o mata-mata: quem diz "as semis e a final são mais longas" está falando das
  duas.

**Chave direta não conta dinheiro.** O Financeiro soma `PrecoInscricao × 2` por dupla e a
taxa de 5% conta pessoas por dupla — 24 duplas a mais dobrariam os dois, faturando a **mesma
pessoa duas vezes pelo mesmo torneio**. É a regra que já valia pro TIME. A diferença é onde o
filtro mora: a flag é da CATEGORIA e quem calcula a taxa **não carrega a navegação**, então
uma propriedade calculada devolveria `false` calada e cobraria a mais — por isso o filtro é
na query, nos dois pontos.

**1.436 testes** (30 novos), `build-242` no ar. Conferido no navegador: 9 toques param em 4
no jogo de grupo e em 6 na semifinal, na mesma Mesa; a chave direta ganha aba própria com o
quadro da Primeira Rodada. E um teste roda o **torneio inteiro do sorteio ao campeão**
passando pelos byes (23 jogos pra 24 duplas), com a grade sem ninguém em duas quadras.

**Em produção:** categoria `Mata-Mata Geral` criada no torneio 18 com as 24 duplas, e o
formato do Interno ajustado (grupos 4 · mata-mata 4 · semis e final 6). O sorteio da chave
sai junto com o `Gerar Chaves` do torneio, com os 8 byes sorteados na hora.

### 03/08/2026 (fim de tarde) — 🧹 O teste real do "Interno Los Corneteiros" e a fila que ele gerou

O torneio de teste com gente de verdade rodou a tarde inteira e cada esquisitice virou
conserto no mesmo dia:

- **Ninguém entra 2× na mesma categoria** (`Services/InscricaoRepetida`). O Otávio apareceu
  como parceiro de um E sozinho "procurando parceiro" — duas vagas pra uma pessoa. A causa é
  o uso CERTO do sistema (se inscrever sozinho, e depois alguém te inscrever como parceiro),
  então a resposta não é um "não" seco: inscrição existente **sozinha vira oferta de juntar**
  (ela sai, fica só a dupla, no MESMO SaveChanges); **com parceiro, recusa** dizendo com quem
  — confirmar não pode virar jeito de roubar parceiro alheio. O aviso aparece **enquanto o
  CPF é digitado**. Torneio que cobra pelo site não junta (a dupla só nasce no webhook, e
  apagar antes deixaria o outro sem vaga se o checkout fosse abandonado).

- **Nome que pareça nome e CPF que exista** (`Services/NomeDePessoa`,
  `Documentos.CpfEhValido`). Entrou no torneio um jogador chamado **"."** com CPF de 11
  números quaisquer. Agora o CPF fecha dígito verificador (repetidos tipo 111.111.111-11
  barrados à parte) e o nome exige duas letras, sem números. Vale nas 4 portas: dupla,
  americano, troca de parceiro e cadastro. O campo de CPF diz **"esse CPF não existe"** na
  hora — antes caía no mesmo "não cadastrado" do CPF legítimo e a tela *convidava* a
  cadastrar o fantasma. ⚠️ CPF errado prende o histórico num fantasma: é por ele que o
  parceiro sem conta assume o próprio cadastro depois.

- **O celular parou de quebrar** nas três telas flagradas pelo Felipe: **Home** (selo +
  título + botão numa linha — o título, a única coisa que importa, sobrava truncado em
  "Torneio d..."; virou o componente `pdz-cartao-linha` no site.css: empilha no celular com
  botão de largura cheia, linha no desktop), **Agenda** (a barra do FullCalendar com 3 blocos
  não cabia em 375px; empilhada abaixo de 768px) e **Gerenciar Inscritos** (3 filhos num
  justify-between = colunas desalinhadas e botão quebrando no meio; virou 2 filhos com
  nowrap, selos discretos e Remover só com ícone). Sem `overflow-x: hidden` global de
  propósito: a navbar é sticky e overflow em ancestral mata `position: sticky`.

**1.242 testes.** Conferido no navegador em 375px e 1385px, caso a caso.

📌 **Pro lançamento de 08/08:** quando o Interno acabar, **marcar o torneio como Restrito**
(ponto de ranking é calculado na hora — some sozinho, não existe "resetar") e **rodar o
replay do Padelímetro** no admin (esse é guardado). O torneio está com `Restrito=false` em
prod; é por isso que pontua.

### 03/08/2026 (noite) — 🎯 Padelímetro, fase 1

O plano de ataque ao "QT Level" do concorrente, desenhado com o Felipe e escrito em
[RANKING.md](RANKING.md) — que é a fonte da verdade das regras. O resumo do desenho:
**duas trilhas** (o ponto anual premia e expira em 12 meses; o **nível** decide onde a
pessoa PODE jogar), escala única 0–1000 pra todo mundo (mulher jogando masculina sem
regra especial), faixas de 100 por categoria, **soma da dupla** como segundo porteiro,
subida imediata/descida com folga e a regra do bicampeão. Nome escolhido: **Padelímetro**
("o Padelímetro não mente"). Fase 1 é só MOSTRAR — nada trava inscrição.

O que existe em código:
- `Services/Padelimetro.cs` — o motor puro (Elo divisor 400, K 40→20, fator de games
  1,0–1,6) e `Services/FaixasDePadelimetro.cs` — a régua (faixas, entradas, somas).
- `Jogador.Padelimetro` (nulo = nunca jogou) + `HistoricoDePadelimetro` (o extrato).
- Gancho único no `FinalizarPartida` da Mesa (em try/catch: falha não trava torneio).
- **Replay determinístico** no admin ("Recalcular Padelímetro") — reconstrói tudo do
  zero em ordem cronológica; mudou a regra, roda de novo. Testado: replay = ao vivo.
- Perfil público: pílula "Padelímetro 985 PDZ · faixa da Open", selo "em calibração
  (3 de 10 jogos)", "faltam X pra subir" e o extrato com o porquê de cada movimento.
- **Aba "Padelímetro" na página de Ranking** (decisão do Felipe: a unidade se chama
  **PDZ**) — lista todo mundo com nível, do maior pro menor, com faixa, jogos e selo de
  calibração; respeita o filtro regional e a busca por nome do hub.
- Fora da conta (verificado ao vivo com os 3 casos no banco local): torneio restrito,
  categoria/dupla de TIMES, dupla incompleta, partida sem placar.

⚠️ O retroativo NÃO roda sozinho no deploy: é o botão do admin. Em dev tem 24 partidas
finalizadas esperando; em prod ainda não há partida nenhuma (nada a recalcular).

### 03/08/2026 — 📊 Métricas por dia, semana ou mês

A tela do admin só somava por semana. Semana é boa pra tendência e péssima pra duas perguntas
que se faz o tempo todo: *"o que aconteceu **hoje**, depois que mandei o link no grupo?"* e
*"como está o ano?"* — essa segunda é a mesma conta do **teto do MEI**, que aparece logo acima
na mesma página.

Agora são três botões, cada um com o tanto de história que faz sentido pra ele: **14 dias**
(cabe na tela e já mostra o ritmo da semana), **8 semanas** (o que sempre existiu) e
**12 meses** (cobre o ano fiscal do MEI). Título e subtítulo acompanham — a tela dizia
"semana a semana" fixo, e ia mentir assim que mostrasse dias.

A regra mora em `Services/FaixasDeMetricas`, testável sem banco, e guarda duas decisões que
erram fácil:

- **a semana começa na SEGUNDA**, não no domingo. Domingo é o dia 0 do .NET, mas o fim de
  semana é o *evento* — com a conta ingênua, o torneio de sábado-e-domingo aparece partido em
  duas linhas;
- **o fim do mês acompanha o tamanho do mês**, não 30 dias fixos (senão fevereiro sobra dois
  dias dentro de março).

Semana continua sendo o padrão: link antigo, parâmetro digitado errado ou vazio caem nela em
vez de dar erro numa tela que só serve pra ler.

Conferido ao vivo, e **as contas fecham entre as visões**: no diário 1+1+3+2+1+26 = 34
cadastros; no mensal, agosto 1 + julho 33 = 34. Sem buraco e sem contar duas vezes — e é do
mesmo somatório que sai o valor arrecadado. No celular (375px) a página não rola de lado.
**33 testes novos · 1.165 no total.**

### 03/08/2026 — 👨‍🏫 O primeiro professor de verdade usou, e trouxe cinco problemas

O **Jonatas** cadastrou o local dele (Batata Padel), lançou aulas, instalou o app — e cada
coisa que ele tentou fazer encostou num limite. Os cinco pedidos têm a mesma raiz: **o código
supunha um professor mais simples do que o professor real**.

- **Aula em dupla e em trio.** Existia **um** preço por local, e o painel anunciava "aula
  individual" enquanto ele cobrava três valores diferentes conforme quantos alunos dividem a
  quadra. `LocalAula` ganhou `PrecoDupla`/`PrecoTrio` (o **total** da aula, que é o que ele
  fala pro aluno e o que entra no financeiro) e `Aula` ganhou `QuantidadeAlunos`.
  **A regra mora em `Services/PrecoDaAula`** e **a mesma conta roda no JavaScript** das duas
  telas — se as duas divergirem, o professor vê um valor e salva outro.
  Sem preço pro tamanho pedido, cai pro **tamanho menor mais próximo que ele informou**:
  preencheu dupla e deixou trio vazio, o trio sugere o valor da dupla — perto, dá pra ajustar
  pra cima. Cair no individual cobraria três pessoas pelo preço de uma.

- **Apagar um local.** Só existia *Desativar*, então **errar o nome no primeiro cadastro**
  deixava o local errado na lista pra sempre. Agora some de vez quando não tem aula nenhuma
  (horários e pacotes vão junto, e a confirmação diz quais). **Com aula, recusa e explica** —
  apagar levaria junto o histórico do que ele ganhou ali, e `Aula.LocalAulaId` é `Restrict`:
  o banco recusaria de qualquer jeito, entregando um **erro 500 no lugar da explicação**.
  De quebra, a tabela de preços de um local **virou editável** (antes só o custo era).

- **Preço combinado com um aluno.** O aluno antigo que nunca teve reajuste é a **regra, não a
  exceção** — sem lugar pra guardar, ele corrigia o valor na mão em toda aula e esquecia numa.
  Nova tabela `PrecoDeAluno`, editável direto na lista de **Alunos** do painel.
  **Vale na aula individual daquele aluno**; em dupla ou trio manda o preço do tamanho, porque
  o desconto foi dado a **uma pessoa** e não à quadra inteira — valendo lá, o lugar do
  acompanhante sairia de graça sem ele perceber.

- **Excluir a aula.** Só dava pra *Cancelar*, que é um **fato registrado** (conta pra política
  de 24h, aparece no financeiro) e continua na tela — não serve pra desfazer lançamento errado.
  Apagar tira a linha, **remove o evento da Google Agenda** (senão o horário some daqui e
  continua ocupado lá, que é onde ele olha antes de marcar outra coisa) e **avisa o aluno com
  conta** quando ainda havia aula pela frente. A confirmação muda conforme o que está em jogo.

- **Instalou o app e o app seguia pedindo pra instalar.** Os Primeiros Passos mediam
  `PushSubscription` — ou seja, **"aceitou notificação"**, não "instalou". Quem instala sem
  liberar aviso ficava com o passo pendente **pra sempre**. `Jogador` ganhou `InstalouAppEm`,
  carimbado pelo **navegador** (o único que sabe: a requisição que chega no servidor é idêntica
  instalado ou não), e o passo passou a aceitar **as duas provas**.

⚠️ **A migração entra com `defaultValue: 1` em `QuantidadeAlunos`, não o `0` que o EF gera
sozinho** — toda aula anterior era individual, era o único preço que existia. O padrão do EF
escreveria "aula com zero alunos" em cima do histórico inteiro do Jonatas.

**Junto, uma correção de encanamento:** o CI passou a **dizer qual teste caiu**. Em 01/08 a
suíte reprovou três vezes seguidas em commits de código idêntico (um deles mudava *uma linha
de markdown*), o deploy travou e não deu pra saber o que quebrou — o log completo do CI só se
lê autenticado, e a anotação pública dizia só *"exit code 1"*. Agora o `dotnet test` gera um
`.trx` e, **só quando falha**, um passo imprime `::error::TESTE VERMELHO: <nome>` por teste
quebrado; `::error::` vira anotação, e anotação é pública. Sem cano (`| tee`) de propósito:
com cano quem devolve o código de saída é o `tee`, e uma suíte **vermelha passaria por verde**.
A instabilidade em si **segue sem diagnóstico** — passou 5 vezes seguidas em 03/08 e não
reproduz local. Da próxima vez que ficar vermelha, o CI entrega o nome.

**44 testes novos · 1.132 no total**, verdes em Debug **e** Release. Conferido ao vivo no
ambiente local com conta de professor descartável (apagada depois): o preço muda com o tamanho
(120 → 150 → 180), o acordo de R$ 90 vale na individual e **não vaza** pra dupla, local com
aula recusa a exclusão e sem aula some, aula apagada sai da agenda, e o passo do app fecha
sozinho (0 de 5 → 1 de 5, "Instale o app no celular" riscado).

### 31/07/2026 (noite) — ✏️ O que só dava pra decidir na criação virou editável

Três pedidos do Felipe na sequência, todos da mesma família: *"deixa eu mudar isso depois"*.

- **Estrutura da categoria de times editável** (tela Times): nome, quantos times, quantos
  grupos e quantos classificam mudam enquanto as chaves não saem — *"achei que davam 8, vieram
  6"* obrigava a apagar a categoria e recomeçar. A conta é revalidada pelo **mesmo lugar da
  criação** (`CategoriaDeTimes`) e ainda contra os times **já cadastrados**: baixar pra 4 com
  6 dentro deixaria dois de fora sem ninguém saber quais. Categoria vazia também pode ser
  removida. Depois do sorteio, nada disso muda mais.
- **Torneio restrito no Editar** (antes só na criação): ligar/desligar a chave de acesso sem
  refazer o torneio. A regra delicada é o **campo vazio com o torneio já restrito** — significa
  *"não quis mexer na chave"*, e não *"apaga a chave"*: sortear uma nova aí derrubaria todo
  mundo que já recebeu a antiga no grupo do WhatsApp. A tela diz qual é a chave de agora.
  Desligar o restrito apaga a chave (senha que não tranca nada, e que voltaria a valer sozinha
  se ele religasse). "Sumir da listagem" continua morando dentro do restrito.
- **🏅 Torneio restrito NÃO conta pontos pro ranking** (decisão de produto do Felipe). Restrito
  é evento fechado — interno de clube, grupo de amigos. Pontuar evento fechado faria o ranking
  medir **acesso a torneio privado** em vez de padel jogado: quem organiza um interno por mês
  subiria sem enfrentar ninguém de fora. **O que continua:** participação, título e jogos
  seguem no perfil — aconteceram. O que não existe é ponto. Vale nos cinco lugares que contam
  ponto (ranking por categoria, ranking de times, pontos do perfil, gráfico de evolução e os
  pontos que definem **cabeça de chave** no sorteio) — se um só ficasse de fora, o perfil
  mostraria um total e o ranking outro. Os dois avisos de tela (criação e edição) dizem a
  consequência **antes** de o organizador marcar. Medido ao vivo: 250 pts com o torneio aberto,
  **240 com ele restrito** — exatamente os 10 da participação. Em produção **não mexeu em ponto
  de ninguém**: o único torneio de lá não é restrito e está sem inscritos. **1077 testes.**

### 31/07/2026 (noite) — 🏳️ Categoria de TIMES + troca de horários (build-193, prod + dev)

Pedido do Felipe: *"times se enfrentando"* como categoria de torneio, e o organizador podendo
trocar o horário do jogo A com o jogo B depois do sorteio.

- **Categoria de times**: o organizador define a estrutura na criação (ou depois, na aba
  Gerenciar) — quantos times, quantos grupos, quantos classificam por grupo — e cadastra os
  times pelo nome na tela própria (`Torneios/Times`); nome que já existe no cadastro de Times
  entra com o escudo. Jogador NÃO se inscreve nela (tela não oferece e POST à mão é recusado).
  **A conta grupos × classificados precisa fechar quadro (2/4/8/16)** — recusada na criação,
  quando ajustar é de graça, e revalidada no sorteio com os times que existem de verdade
  (`Services/CategoriaDeTimes`, puro).
- **Por dentro, time é uma `Dupla` com `NomeTime`** — o motor inteiro (grupos, partidas,
  classificação, mata-mata, mesa, grade) funciona sem duplicação. `Jogador1Id` leva o
  organizador (coluna NOT NULL com 560 usos), e por isso existem GUARDAS em todo lugar que
  fala com jogador de verdade: não pontua ranking/perfil (`EstatisticasService`, funil
  `LocalizarDuplas`), não recebe push de chaves/próximo/atraso (funil
  `AvisosDoDiaDeJogo.JogadoresDa`), não fica fora do sorteio por "sem parceiro"
  (`ForaDoSorteio`), não conta na taxa do Externo, não desiste pela porta do jogador.
- **Mata-mata generalizado**: `ChaveamentoMataMata.MontarPrimeiraFase` aceita X classificados
  por grupo (X=2 mantém o comportamento histórico, testes antigos intocados); os robôs dos
  dois controllers leem o X da categoria.
- **🔴 Defeito latente achado e corrigido no caminho**: o encaixe da grade era posicional e
  punha **o mesmo inscrito em duas quadras no mesmo horário** (grupo de 3 + 2 quadras — valia
  pra DUPLAS também, desde sempre). Agora `GradeDeJogos.Encaixar` é ciente de conflito:
  pra cada horário entra o primeiro jogo da fila cujos dois lados estão livres. Vale pro
  sorteio e pro mata-mata emendado.
- **Troca de horários** (`Services/TrocaDeHorario` + modal na aba Jogos): organizador marca o
  jogo A no card, escolhe o B no modal, e os dois trocam **horário E quadra** (o slot físico é
  o par). Só jogo AGENDADO — jogo em quadra ou finalizado é história, não agenda.
- **Provado no navegador** (local): torneio misto (1 categoria normal + times 6/2/2), 6 times
  cadastrados, sorteio → 7 jogos numa grade única SEM conflito, dupla e time dividindo as
  mesmas quadras; troca de horário 40↔44 verificada no banco; grupos fechados → robô gerou
  Final direta (dupla) e Semifinal de times com os classificados certos (1ºs + 2ºs, sem
  reedição de grupo). **1049 testes.** Migração `CategoriaDeTimes` (6 colunas novas, nada
  destrutivo).

### 31/07/2026 — 🔑 Admin manda em qualquer torneio (build-184, prod + dev)

Pra socorrer organizador travado. No dia do torneio, com as quadras ocupadas, o problema é
sempre urgente — e "me adiciona como organizador" depende justamente da pessoa que não está
conseguindo mexer no sistema. Antes o único caminho era ir no banco na mão.

Vale pra **toda** ação de gestão de uma vez porque todas passam pela mesma porta
(`EhOrganizadorAsync`): encerrar inscrições, gerar chaves, Mesa de Controle, Financeiro,
remover inscrito, comunicar. O admin também passa a ver os torneios **ocultos** na listagem.

Detalhe que evita um bug sutil: a checagem é sobre o `jogadorId` **recebido**, não sobre o
claim de quem chamou — essa mesma função responde "fulano já manda aqui?" no
`AdicionarOrganizador`, e ler o claim faria a resposta ser sobre outra pessoa.

Provado no dev com conta descartável, antes e depois: como jogador comum, **sem** aba
"Gerenciar Torneio"; com a flag de admin e crachá reemitido, a aba aparece e
`/Torneios/Financeiro/17` e `/Torneios/MesaControle/17` respondem 200 num torneio que não é
dele. A conta foi apagada em seguida. Os testes seguram o outro lado: jogador comum e
visitante deslogado continuam recusados. **1.011 testes.**

Junto (mesma leva): **estorno automático** — o webhook do estorno agora desfaz a inscrição,
avisa quem saiu e chama a próxima da fila (antes mexia só no dinheiro, e a vaga ficava presa a
quem já tinha recebido de volta) — e a **caderneta de cobrança do "por fora"** no Financeiro:
quem já acertou, quem deve, total recebido/a receber, marcar pago e cobrar no WhatsApp com o
valor e a chave Pix na mensagem. Junto veio um defeito que só aparecia no fim: **o botão de
marcar pago sumia depois de encerrar as inscrições**, justo quando o pessoal paga.

### 31/07/2026 — 🛠️ A fila do "o que ainda melhorar" (build-178, prod + dev)

Quatro buracos que o uso real acharia em dias. **962 testes.**

**1. Desistir era mandar mensagem pro organizador**, que mandava mensagem pro suporte. Agora o
próprio inscrito sai, só enquanto as inscrições estão abertas (depois do sorteio a dupla já
está numa chave, com adversários contando com ela). A regra que mais importa: **quem desiste
sai, o parceiro não é arrastado junto** — ele estava inscrito também, muitas vezes já pagou.
Dupla completa fica com uma cadeira vazia e quem ficou é avisado pra achar outro; quem estava
sozinho leva a inscrição embora e a vaga volta pra fila.

Junto: **promover da lista de espera passou a avisar**. Antes era segredo entre o sistema e o
banco — a dupla saía da espera e só descobria olhando a página, e quem entra na espera
justamente não fica olhando. Quem o organizador remove também é avisado, em vez de descobrir
no clube no dia do jogo.

**2. O sorteio deixava gente de fora calado.** Quem estava sem parceiro ou na espera sumia da
chave, e o jogador descobria quando ela saía — sem tempo de resolver. Agora a tela **lista quem
fica de fora e por quê** antes de sortear, o botão pede confirmação, e encerrar inscrições
avisa quem ainda está sem parceiro. A regra virou `Services/ForaDoSorteio` e o `GerarChaves` lê
dela: com duas cópias, a tela prometeria uma coisa e o sorteio faria outra.

**3. Vigia de erros no VPS** (`/opt/padelizou-vigia-erros.sh`, cron de 5 min). O
`padelizou-monitor.sh` pega o site **fora do ar** e reinicia; este pega o outro caso, que
passava batido: site **de pé**, respondendo 200 na home, com uma página quebrando pra todo
mundo. Manda e-mail com as linhas do erro. Só alarma exceção não tratada e Kestrel —
**antiforgery fica de fora de propósito** (17 em 7 dias, nenhum era defeito; alarme que toca à
toa vira alarme ignorado). O cursor do `journalctl` garante que cada erro é avisado uma vez só.
Testado ponta a ponta com um padrão inofensivo antes de valer.

**4. Todo aviso sai também por e-mail.** O push só alcança quem instalou o app; o WhatsApp
depende do chip, que ainda não está ligado. Quem não fez nenhum dos dois **não ficava sabendo
de nada**. Entra no mesmo lugar onde o WhatsApp já entrava (`PushNotificationService`), não nos
~30 pontos que mandam aviso — os avisos que já existiam ganharam e-mail de graça. Respeita a
preferência da pessoa e falha calado.

### 31/07/2026 — 🍺 O bar do clube, 3 fases (build-169, 176 e 180 — só em dev, INVISÍVEL)

Um cliente pediu "gerenciamento completo de bar e financeiro". A estratégia escolhida, antes de
qualquer código: **não é sistema separado nem subdomínio** (`caixa.padelizou.com.br` daria mais
um login, mais um cookie e dois sistemas que não se enxergam) — é aba dentro do painel do clube
que já existe, no mesmo banco. O que dá valor à comanda é justamente o que já está aqui: o
jogador, a reserva, o horário.

**Nasce escondido.** A chave `Bar__Habilitado` nasce desligada e, enquanto isso, só admin do
Padelizou entra — o dono do clube não vê nem o atalho. Testado dos dois lados: o dono leva
`Forbid` mesmo sendo dono. Chave de configuração em vez de branch porque o módulo mexe em tabela
nova **e** no painel do clube: segurar isso numa branch por semanas é garantir conflito e um dia
de merge no fim. Ligar depois é uma linha no systemd, sem republicar.

**Fase 1 — comanda, cardápio e caixa** (`build-169`):
- Comanda aberta pelo **nome** (é o que se procura no balcão; o número é do dia e reinicia).
  Quem está na quadra agora abre comanda **num toque**, com nome e celular vindos da reserva.
- O item guarda o preço de **quando foi vendido**. Sem isso, reajustar a cerveja às 20h mudaria
  o valor do que saiu às 15h e das comandas fechadas do mês inteiro. Conferido ao vivo: comanda
  de R$ 30,00 continuou R$ 30,00 depois de o produto ir a R$ 15,90.
- Item e comanda cancelados **não somem**, ficam com autor e motivo — item que desaparece da
  comanda é a forma mais comum de furto num bar.
- Caixa do dia: abriu com quanto, vendeu quanto **em dinheiro** (cartão e Pix não passam pela
  gaveta), contou quanto. O contado é digitado, nunca calculado — se o sistema preenchesse, a
  conferência não conferiria nada. E não fecha com comanda aberta.
- 🐛 **Achado testando de ponta a ponta:** `FecharCaixa` recebia `decimal` não-nulável, então
  campo em branco virava **zero** e o caixa fechava acusando um rombo do tamanho do dia — e
  fechar caixa não tem volta. Virou `decimal?` que recusa vazio.

**Fase 2 — contas a pagar e a receber** (dentro de `build-176`):
- É **manual de propósito**. O que já passou pelo Padelizou (comanda fechada, reserva paga)
  aparece em **coluna separada** como "já recebido", nunca somado com o "a receber" — somar
  contaria a mesma receita duas vezes. Aqui entra o que o sistema não tem como saber: luz,
  boleto da distribuidora, cachê do DJ, cliente que levou fiado.
- A situação (vencida / vence hoje / a vencer / quitada) é **calculada na hora**, não gravada:
  gravada, obrigaria alguém a varrer a tabela toda meia-noite pra manter a verdade em dia.
- Conta que se repete nasce com **todas as parcelas gravadas**, não como regra "repete todo dia
  10" — porque conta que se repete muda (reajuste, contrato que acaba, mês pago adiantado), e
  com linhas gravadas mexer numa parcela é mexer numa linha. Dia 31 cai no último dia do mês que
  não tem 31 e **volta pro 31** no mês seguinte, como todo boleto.
- Apagar a série poupa as parcelas já quitadas — apagar parcela paga apagaria o registro de um
  dinheiro que de fato saiu.
- Conferido no navegador: aluguel de R$ 3.500,90 × 4 gerou 31/08, 30/09, 31/10 e 30/11 somando
  R$ 14.003,60, e o recebível atrasado apareceu primeiro na lista.

**O que este módulo NÃO faz, e é decisão:** nota fiscal. NFC-e é homologação por estado,
impressora fiscal e um produto inteiro — existem empresas que só fazem isso. O Padelizou cuida
do controle interno; o clube segue emitindo nota pelo que já usa. Dinheiro e maquininha são
**registro** aqui, não cobrança.

**Onde está no histórico:** a Fase 1 é o commit `71d809c`. A Fase 2 ficou **dentro de
`23f9b39`** ("Jogador desiste sozinho…"), porque duas sessões trabalhavam na mesma árvore e os
arquivos de uma foram varridos pelo commit da outra. Nada se perdeu, mas quem procurar "contas
do clube" na mensagem de commit não acha — está aqui.

**Fase 3 — estoque, e o relatório que fechou a fase 1** (`build-180`):

A premissa está escrita no código pra não se perder: **estoque de bar NÃO é exato**, e prometer
exatidão é o jeito mais rápido de o dono parar de confiar e voltar pro caderno. Garrafa quebra,
funcionário consome, cerveja sai sem passar pela comanda no sábado lotado. O que segura o saldo
colado na prateleira não é precisão — é a **contagem semanal**. Por isso perda e contagem são
botões de primeira classe, e não correções escondidas num canto.

- Ligado **produto a produto**: lata entra e sai inteira, porção e caipirinha não — a saída
  delas depende da mão de quem serve, e saldo que mente é pior que saldo nenhum.
- **Não existe coluna "quantidade em estoque"**: o saldo é a soma dos movimentos. Um número
  guardado responde "tem 8" mas não responde "por que tem 8", que é a pergunta que aparece
  quando a prateleira discorda.
- A baixa **pega carona** no lançamento da comanda — o operador não faz nada a mais, única forma
  de estoque sobreviver a um sábado cheio. Cancelar item devolve a unidade sozinho.
- **Saldo zerado NÃO impede a venda.** A prateleira manda, não o sistema: recusar uma cerveja
  que já está na mão do cliente porque o número não bate seria o jeito mais rápido de o bar
  desligar o estoque pra sempre. Negativo aparece como "falta registrar entrada".
- Compra-se caixa com 24, vende-se lata: a conversão evita o erro mais comum de todo controle de
  estoque de bar (digitar 24 e ficar com 24 caixas).
- Entrada com custo **vira conta a pagar num clique** — digitar a mesma compra duas vezes é o
  que faz o dono desistir de uma das duas telas. Margem pelo **último** custo, não pela média:
  é o preço da próxima compra, e é com ele que a decisão de reajustar é tomada.
- 🐛 **Bug evitado:** `SalvarProduto` com `bool controlaEstoque = false` faria editar o preço
  pelo cardápio **desligar o estoque em silêncio**, e o dono só descobriria na contagem
  seguinte. Os campos viraram nuláveis — ausente preserva.

**Relatório:** o caixa do dia responde "bateu?" e nada mais. O relatório responde o que decide
compra e preço — mais vendidos, como pagaram, dia a dia, ticket médio. Cancelamentos e perdas
aparecem juntos e visíveis: são os números que o dono precisa ver crescer pra desconfiar.

Conferido no navegador: 2 caixas de 24 viraram 48 un., custo 7,90 digitado com vírgula, margem
R$ 4,60 (58,2%), a compra virou conta a pagar de R$ 379,20, vender+cancelar deixou 47, e contar
5 gerou o ajuste de −42 com o aviso de comprar. **997 testes.**

**DECISÃO DO FELIPE (31/07): o bar só REGISTRA pagamento, nunca cobra.** As quatro formas
(Dinheiro, Cartão, Pix, Cortesia) são anotação do que aconteceu no balcão — o dinheiro corre
fora do sistema, como no torneio "por fora". O Pix com split, que seria o único a virar receita
nossa, **não vai ser feito**. Isso deixa de ser pendência e vira desenho do produto.

Por que isso é bom: a fase 1 fecha sem depender de conta no meio de pagamento, sem prazo de
repasse, sem estorno de bar pra tratar, e sem o Padelizou responder por dinheiro que não viu.
O clube vende do jeito que já vende e o sistema conta a história. Confirmado no código: **não
há uma única chamada de cobrança em todo o módulo** — fechar comanda grava forma, data e status.

Ficha técnica de drink, validade/lote e múltiplos depósitos ficam **de fora com convicção**:
quem precisa disso precisa de um ERP de restaurante, não do Padelizou.

**Falta pra usar de verdade:** nada de código. Ligar é `Bar__Habilitado=true` no systemd do
ambiente, quando o cliente estiver pronto.

### 31/07/2026 — 🔐 Inscrever exige login de quem inscreve (build-174, prod + dev)

Fecha o ponto nº 2 da varredura. A inscrição era aberta a qualquer visitante que soubesse a
senha do portão — e **o portão não identifica ninguém**: dava pra criar cadastro com CPF de
terceiro sem deixar rastro de quem fez. Agora existe **autor**: é dele o aviso "Fulano
inscreveu você", e é ele quem responde pelo que digitou.

**O parceiro continua sem precisar de conta** — e isso já funcionava: ele entra como
**pré-cadastro** (`Jogador` sem senha, achado por CPF) e, quando se cadastrar depois, o próprio
CPF reencontra a linha dele e ele **assume a conta com o histórico junto**. Tem trava: CPF que
**já tem senha** não pode ser reivindicado (o cadastro manda pra recuperação de senha) — senão
quem soubesse o seu CPF tomava a sua conta, e CPF não é segredo no Brasil.

Pra não virar armadilha, a aba de inscrição mostra o **convite pra entrar** no lugar do
formulário quando ninguém está logado: preencher a dupla inteira e só então ser jogado pro
login perderia tudo. E o login passou a respeitar **de onde a pessoa veio** (`returnUrl`),
inclusive quando ela erra a senha na primeira tentativa — destino de fora do site é ignorado,
senão o login viraria trampolim pra outro site.

Conferido **em produção**, deslogado e já dentro do portão: `POST /Duplas/Create` e
`POST /Torneios/InscreverIndividual` respondem **302 pro login**. **934 testes.**

### 31/07/2026 — 🔎 Varredura antes do uso real (build-171, prod + dev)

**O achado:** algumas colunas são `character varying(n)` e o Postgres **não corta** o que passa
— ele **recusa** (`value too long`). Sem checagem, a página caía em **erro 500** e a pessoa
perdia tudo o que digitou, sem entender por quê. Os casos que dava pra provocar sem forçar nada:

| Onde | Coluna | Exemplo que quebrava |
|---|---|---|
| **Cadastro** (a 1ª tela de quem chega) | `Login` varchar(30) | `joaovictordossantosoliveirajunior` (33) |
| Inscrição no torneio | `Nome` varchar(100) | nome colado da agenda do celular |
| Criar torneio | `Nome` varchar(150) | descrição colada no campo do nome |
| Aula manual | `NomeAlunoAvulso` (100), `Telefone` (20) | idem |

Agora cada um **recusa com aviso** dizendo quantos caracteres vieram e quantos cabem, e a tela
ganhou `maxlength` (o navegador nem deixa digitar e corta o que for colado). O servidor continua
conferindo — formulário em cache e POST feito à mão não passam pela tela. Junto: **preço
negativo recusado** (não estourava nada, e era por isso que passava batido — a cobrança sumia e
o torneio anunciava "−R$ 50,00").

**O que a varredura conferiu e está OK** (não é suposição, foi verificado arquivo por arquivo):
- **Autorização**: toda ação de gestão de torneio tem `[Authorize]` + `EhOrganizadorAsync` +
  `Forbid()`. Conferidos os 8 partials do TorneiosController, mais Partidas, Duplas, Times,
  Jogadores, Professores e Feedback.
- **CSRF global**; a única exceção é o webhook de pagamento, que **valida token e devolve 401**.
- **Upload de imagem**: limite de tamanho, lista de extensões e — o que importa — **reencoda** a
  imagem. SVG ou HTML disfarçado de `.png` não decodifica e é recusado; o nome do arquivo é
  gerado pelo servidor e a saída é sempre `.webp`.
- **XSS**: os nomes de jogador que vão pra HTML via `Html.Raw` passam por `HtmlEncode`.
- **Sem segredos versionados**; produção roda `ASPNETCORE_ENVIRONMENT=Production` (nada de
  stack trace na tela); cabeçalhos HSTS, `nosniff`, `X-Frame-Options` e `Referrer-Policy` no ar.
- **Perfil privado** realmente esconde o telefone (o bloco de contato está dentro do `else`).
- **Divisão por zero** na grade de jogos: protegida (`duracao > 0 ? duracao : 50`).
- **Pagamento em produção aponta pro Asaas de produção**, não sandbox — dinheiro de verdade.

**921 testes.**

### 31/07/2026 — 🐛 O bug do dinheiro (build-165, prod + dev)

**O sistema lia R$ 79,90 como R$ 7.990,00.** A cultura do app é `pt-BR`, onde `.` é separador
de **milhar**. Todo campo de dinheiro da tela é `<input type="number">`, e o navegador manda o
valor sempre no formato da máquina (`79.90`) mesmo exibindo `79,90`. O binder padrão do
ASP.NET Core lê campo de formulário na cultura da requisição → `decimal.Parse("79.90", pt-BR)`
= **7990**. Sem erro, sem aviso, sem log.

Por que ninguém viu: **todos os preços em uso eram redondos** (120, 150, 20). Sem centavos não
há separador, e o bug não aparece. O primeiro torneio com inscrição de R$ 89,90 teria cobrado
R$ 8.990,00 do jogador. Confirmado com teste **antes** de mexer, não por dedução.

Correção em `Services/DinheiroModelBinder.cs`, registrado global pra todo `decimal`: com
vírgula **e** ponto, o último manda (`1.234,56` e `1,234.56` caem os dois em 1234.56); com um
só, ele é o decimal — porque o `input type=number` **nunca** manda separador de milhar. 15
casos cobertos, incluindo `R$ `, espaço fino de teclado de celular e lixo digitado.

Junto, na criação do torneio:
- **Publicar não dava sinal de vida.** A recusa (nome repetido, sem categoria) nascia no topo e
  o botão fica no pé — publicar *parecia* não fazer nada. Agora a tela rola até o erro e o botão
  trava em "Publicando o torneio…". O segundo clique impaciente era o que criava torneio dobrado.
- Inscrição nasce em **150** (zero é preço válido, então campo vazio não avisava nada), mostra
  centavos e se seleciona ao receber o foco. Simulador "Vai caber?" começa em **50 duplas**.
- **Chave Pix do organizador** e **recado aos inscritos** no "por fora": o jogador não sabia pra
  onde mandar o dinheiro, e o organizador respondia isso no zap trinta vezes.
- **Duas datas previstas** (encerramento das inscrições e chaveamento). São promessa publicada:
  nada encerra nem sorteia sozinho.

E na inscrição:
- Botão **"Sou eu"** preenche o CPF de quem está logado. Digitar o próprio CPF com máscara no
  celular era o degrau mais alto do formulário — e é o dado que o sistema já tem.
- **Um impedimento só** (`Services/ImpedimentoUnico`). Marcar os três é dizer "não podemos jogar
  em turno nenhum", e isso só aparecia na hora de montar a grade. Trava na tela **e** no servidor.
- **Aviso de categoria mais fraca** que a declarada nas Preferências: "No cadastro, Fulano
  colocou que é 4ª Categoria. Por que quer se inscrever na 5ª?". É pergunta, não trava — pode
  ter caído de nível ou o parceiro ser mais fraco. A trava dura por histórico continua sendo outra.
- Confirmando com CPF que não é o seu, **pede confirmação**, e quem foi inscrito recebe
  "Fulano inscreveu você" em vez de um "Inscrição confirmada" que parece ter partido dele.

Antes disso, no mesmo dia:
- **Troféu leva o nome da CATEGORIA**, não do material. Ninguém ganha "um diamante": ganha a 1ª
  Categoria Masculina. Dois títulos na mesma categoria somam; a 2ª masculina e a 2ª feminina
  viram duas taças de ouro separadas — é isso que a pessoa quer mostrar.
- **O diamante virou taça.** Era uma pedra lapidada e destoava dos outros sete; agora o material
  aparece nas facetas e no brilho, nunca num formato diferente.
- **Pnatinha fora das telas vazias.** Tela sem dado não é erro: é o começo normal de quem acabou
  de entrar, e no painel do professor o mesmo desenho aparecia **quatro vezes** na mesma rolagem.
  Ele fica em Error, NaoEncontrado e convite inválido.
- **Toda tela do menu do professor volta pro painel.** Meus Locais, Meus Horários e Relatório
  voltavam pra *Agenda* (um desvio) e Adicionar Aula não tinha saída nenhuma. A página pública
  só mostra o botão pro próprio professor.
- **"Receber por fora sempre pode"** em destaque na tela de planos, antes dos dois cards: a
  porcentagem vale só pra aula paga **dentro do app**. Conferido no código — sem cobrança no
  app, não nasce taxa.

**889 testes.** Backup de produção conferido antes de publicar (2 jogadores, o pagamento real
de R$ 9, marcador de fim presente).

**Portão com credenciais extras** (`build-167`): além da principal, dá pra abrir entrada
separada pra outra pessoa sem reemitir a senha de todo mundo — e cortar essa entrada depois
sem expulsar quem entrou pela principal. O cookie continua saindo da credencial **principal**
(ele diz "passou pelo portão", não quem passou), então trocar a senha principal segue
derrubando todo mundo de uma vez, que é o que se quer quando ela vaza. Extra sem senha não
abre nada — um `Extras` meio preenchido no systemd viraria porta escancarada pra quem
deixasse o campo em branco. Configuração em `/etc/systemd/system/padelizou*.service.d/
portao-extra.conf` (chmod 600, arquivo separado de propósito: apagar + `daemon-reload` +
restart corta a entrada). **As senhas do portão não ficam em arquivo versionado.**
**894 testes.**

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
- **Cidades duplicadas** (build-81): "Gravatai", "Gravataí" e "gravatai" conviviam no filtro do ranking. A comparação era `ToLower()` — pegava a caixa e **deixava o acento passar**. `Services/NomeDeCidade` compara sem acento e sem caixa, como uma pessoa compararia, e **preserva** o acento de quem digitou certo (o banco continua guardando "Gravataí"). ✅ **Duplicatas antigas fechadas em 29/07**: produção já estava limpa (conferido: só "Gravataí", 1 jogador), dev normalizado por UPDATE (2 linhas viraram "Gravataí"; nada apagado).
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
  ~~⏳ Sobraram em `wwwroot/image/` os dois JPEG de origem~~ ✅ movidos pra `antigo/` na raiz do repo (fora do `wwwroot`), junto com o logo anterior recuperado do histórico e um `LEIA-ME.md`.
- **Segunda rodada do logo, por decisão do Felipe** (build-98): na barra o disco escuro sumia (1,7:1 contra o navy) e dependia do aro pra existir. Agora **onde o fundo é o azul do site vão as raquetes SOLTAS** (`logo-raquetes.webp`, recorte por máscara de cor `G−max(R,B)` com rampa 10..50) — barra (38px), rodapé (26px, disco nesse tamanho vira borrão) e capa de torneio sem imagem (gradiente navy fixo). **Logo completo fica onde o fundo é claro**: login, portão, relatório impresso, favicon e ícones de app. Sem o disco de moldura o desenho fica **65% maior nos mesmos 38px**. Barra mantida em 38px de largura **de propósito** (a conta do estouro em 1280px fechou com esse número — conferido: estouro 0). `border`/`box-shadow` saíram do `.pdz-brand-logo`: desenhariam um **retângulo** em volta da área transparente; `drop-shadow` segue o contorno. **`CACHE_NAME` v2 → v3.** Conferido em tela nos dois temas, e em produção.

### 29/07/2026 (noite) — Perfil mais vivo, aula com caderno e nota de escola

- **17 elogios de padel** (eram 8) e **12 conquistas** (eram 6). Elogios novos pensando no que só existe no padel: Boa Víbora, Boa Chiquita, **Saída de Parede** (a parede é metade do jogo), Mão Macia, Leitura de Jogo, Rápido na Quadra, Garra, Fair Play e **Look Bonito** (padel tem moda — entrou depois, a pedido). Conquistas novas todas calculadas do que já existe no banco: Veterano (5 torneios), 10 Vitórias, Finalista, Bicampeão, **Querido da Quadra** (5 elogios recebidos — cruza as duas features) e Aluno Aplicado (3 aulas). Regra saiu do `EstatisticasService` pro `CatalogoConquistas` (puro): lá se coleta, aqui se decide. **Campeão implica Finalista.** Conquista bloqueada virou META: ganhou `Descricao` ("Dispute 5 torneios") no lugar do "bloqueado" mudo. Total fecha em 12 = 3 fileiras exatas de 4, **com teste prendendo o número**.
- **Avaliação do professor: as estrelas 1–5 FICARAM.** A escala 0–10 foi pedida, implementada e desfeita **no mesmo dia** — o Felipe lembrou que as estrelas já existiam e preferiu mantê-las. Como o deploy estava segurado, a conversão ×2 saiu da migração antes de valer em qualquer ambiente. Saldo do vai-e-vem: `Estrelas()` agora mora no serviço (duas telas desenham estrelas; divergir seria pior) com `MidpointRounding.AwayFromZero` — **o `Math.Round` pelado arredonda pra PAR e 4,5 viraria 4 estrelas**; o teste pegou na primeira rodada. Elegibilidade não mudou: só avalia quem teve aula Realizada, uma avaliação por aluno, editável.
- **Interruptor de comentários do professor**: ele liga/desliga os depoimentos da própria página. **A NOTA não se desliga** — nota é o dado que protege o próximo aluno; o texto é vitrine, e vitrine é do dono. Vale também pra POST direto (regra na gravação, não só na tela); nada é apagado, religou voltam; a página diz "optou por não exibir comentários" pra não parecer que ninguém escreveu. ⚠️ O `defaultValue` da migração foi corrigido de `false` pra `TRUE`: o default do banco vale pras **linhas existentes**, e com false todo professor já cadastrado acordaria com comentários desligados sem ter escolhido.
- **Caderno da aula** (`AnotacaoAula`): professor e aluno anotam sobre CADA aula no mesmo fio, cada linha assinada com papel + nome ("trabalhamos bandeja e saída de parede; na próxima, víbora"). Só os dois da aula participam; aluno avulso (sem conta) fica fora por construção; anotar avisa o OUTRO lado por push. Links no card de Minhas Aulas (aluno) e no modal da agenda (professor). Verificado em tela com dado real (média 5,0 ★★★★★ no perfil e na vitrine). **605 testes** (as sessões somaram).
- **Horários do professor em vários dias de uma vez** (build-108). O dia único do formulário virou **7 botões de marcar**: quem dá aula seg/qua/sex no mesmo horário cadastra a semana num clique. Regra em `Services/NovoHorarioDoProfessor` (pura, 9 testes): horário idêntico **ativo** fica como está, **pausado é religado** (recadastrar a semana depois das férias faz o que a pessoa quis, sem linha duplicada), e duas armadilhas que a tela engolia agora são recusadas com mensagem — **fim antes do início** e **aula que não cabe na janela** (os dois só apareciam pro ALUNO, como agenda vazia). Resumo em português: *"Horário criado pra segunda, quarta e sexta."* Provado ponta a ponta no navegador, inclusive a repetição idempotente.
- **Busca sem filtro lista TODO MUNDO, paginada** (build-108, pedido do Felipe). O "escolha um filtro pra começar" saiu: quem abre a busca querendo "ver quem tem por aqui" não precisa adivinhar um nome. O que torna isso barato é a **paginação** (30 por página, janela de páginas com reticências; página fora do intervalo vai pra mais próxima que existe). A página corta pela ordem **alfabética** — que o banco pagina de verdade — e o selo "combina"/pontos reordenam só dentro da página, porque pontos vêm de cálculo em memória e ordenar o total por eles desfaria a paginação. ⚠️ **O teste antigo protegia a regra oposta** ("sem filtro não lista ninguém") e foi trocado com a decisão nova escrita nele. Verificado no dev com dado real: *"73 jogadores — página 1 de 3"*. **605 testes.**
- ⚠️ **Colisão de sessões, capítulo 2** (registrado pra não repetir): o commit `4aa281f` varreu, via `git add -A`, trabalho **não commitado de outra sessão** que rodava em paralelo (`NovoHorarioDoProfessor` — horários em vários dias de uma vez — e busca de jogadores paginada/sem filtro). O código varrido compilava e os testes passavam, mas o certo é **stage explícito** (`git add <arquivos>`) quando há suspeita de sessão paralela. **Desfecho:** o deploy ficou segurado até a outra sessão commitar o trabalho dela de propósito (`21b3719`, "provado ponta a ponta") — e ela varreu 5 arquivos MEUS em estado final no caminho, o acidente inverso. Tudo consolidado e publicado junto no **build-108**; regra pros dois lados anotada na memória.
- **🔴 Local DESATIVADO abria um buraco na escada do professor** (build-120). Achado numa varredura da escada de ponta a ponta, em sessão separada. A checagem do painel perguntava a coisa errada ao banco: *"existe local cadastrado?"* em vez de *"o **aluno** consegue chegar até aqui?"*. A tela do aluno só enxerga local e horário **ativos**, e o filtro faltava — então um clique em **"Desativar"** (que não pede confirmação nenhuma) deixava o painel do professor abrindo em 200, sem aviso, enquanto o aluno escolhia a cidade, escolhia o professor e batia em *"nenhum local cadastrado para este professor"*. Exatamente o silêncio que a escada foi escrita pra impedir. Mesma família um degrau adiante: **horário ativo pendurado em local desativado** também não aparece pro aluno, e agora não conta. A consulta **saiu do controller e foi morar junto da regra** (`CadastroDeProfessor.PendenciaAsync`) — a regra pura estava certa e passava nos testes *enquanto o sistema errava*, porque quem a alimentava perguntava errado; só dava pra testar movendo. Sem risco de laço: `MeusLocais` lista **todos** os locais (inativos primeiro, com "Reativar"), conferido. **624 testes.**
- **Escada do professor validada ponta a ponta** (sessão de teste separada, ambiente local, produção intocada): 13 pontos conferidos — os 3 redirecionamentos com as mensagens certas, os 5 seletores do aluno destravando em cadeia, aula marcada/aceita, série fixa de 4 semanas com aceite em lote, **pacote de 3 aulas por R$ 310 fechando exato (103,33 + 103,33 + 103,34)** e o caderno de anotações. Os 4 ramos da marcação multi-dia também: criar, dia repetido, dia pausado religado, e as 3 recusas. **Conclusão que importa: o "Marcar Aula" NÃO está quebrado no código.** De quebra, o preço na lista de locais saía "R$ 120.00" com ponto (JS com `toFixed`) — corrigido pra vírgula.
- ✅ **Em produção, o dado que faltava foi criado** (30/07, autorizado pelo Felipe "a fim de teste"): 3 horários **Seg/Qua/Sex 18h–21h, aulas de 60 min, no Chakra** — inseridos via SQL idempotente (não duplica se o dia já existir) e verificados com a mesma condição da tela do aluno: **cidade 1 · local ativo 1 · horários válidos 3**. ⚠️ São horários de TESTE: quando o Felipe definir a agenda real, é só editar/desativar em Meus Horários.

### 29/07/2026 (fim da noite) — As 5 decisões de monetização e produto do Felipe

- **Torneio, "por fora" (5%): a condição agora está escrita ANTES da escolha** (build-121), dentro da própria opção: *"as chaves são liberadas mediante o pagamento da taxa — ou mediante negociação combinada com o Padelizou (prazo, parcelamento ou isenção)"*, com o contraste de que nas formas pelo site a taxa sai automática. Nesta forma o dinheiro não passa pelo sistema, então a taxa depende de o organizador pagar; quem só descobre isso ao gerar as chaves sente que a regra mudou no meio do jogo. ~~⚠️ É texto, não trava~~ ✅ **virou trava em 29/07 (madrugada), por decisão do Felipe** — ver a seção seguinte.
- **🔴 Torneio, "todas as formas": a taxa passou a ser a da forma que o JOGADOR escolheu** (build-123). Antes, aceitar cartão encarecia tudo — quem pagava por Pix pagava 15%, a taxa do cartão parcelado. Agora Pix custa 10% mesmo nesse torneio.
  **O obstáculo era arquitetural:** o rateio é fixado quando a cobrança NASCE, e ela nascia com a forma aberta (o jogador escolhia depois, no meio de pagamento) — na hora de definir a taxa não se sabia o que ele usaria. A escolha veio **pra dentro do nosso checkout**: o jogador declara Pix/cartão/boleto e a cobrança nasce travada naquela forma, com a taxa correspondente.
  `Services/CobrancaDoTorneio` responde as **duas** coisas no mesmo lugar de propósito — travar em Pix e ficar com a taxa de cartão seria cobrar uma coisa e entregar outra; **um teste percorre todas as combinações e exige que forma travada e taxa cobrada nunca se contradigam**. Escolha ausente ou desconhecida (formulário em cache, requisição à mão) cai no comportamento antigo: forma aberta + taxa cheia — errar pra esse lado nunca cobra do organizador **menos** do que ele combinou. O parcial `_EscolhaFormaPagamento` serve os dois formulários (dupla e americano); a tela de criar torneio anuncia **"10% a 15%"** com a explicação, em vez de prometer 15% fixo.
- **Aula: o custo real medido, e por que 10% incomoda** (resposta ao Felipe). Comissão de 10% numa aula de R$ 70/100/130 rende R$ 7/10/13 e **custa R$ 0,99 no Pix** (R$ 1,88–3,08 no cartão): margem de **77% a 92%**. O custo não justifica 10% — o que justifica o desconforto é o outro lado: **um professor com 40 aulas/mês a R$ 100 paga R$ 400/mês, e o valor cresce quando ele trabalha mais.** Isso reforça o modelo já decidido (assinatura R$ 49,90 + 3% Pix / 6% cartão ≈ R$ 170/mês no mesmo cenário). ~~⚠️ Pré-requisito não óbvio: o piso global de R$ 4~~ ✅ **piso virou por tipo em 29/07 (madrugada)** — ver a seção seguinte.
- **Painel do clube** (build-125): a área do clube **já tinha tudo** — mapa da semana, bloqueio, mensalista, no-show, política, financeiro por quadra e o liga/desliga de horário publicado. O que não existia era **um lugar**. `ClubeGestao/Painel` entrega números do dia, **próximas reservas com nome, quadra, valor e WhatsApp** (o contato é o que o clube usa quando chove), atalhos com o estado real de cada área, o **aviso de invisibilidade** (sem quadra / sem horário / marcação desligada o clube não aparece pra ninguém marcar — mesma armadilha da escada do professor) e o cartão do plano, que abre conversa em vez de inventar número. Bloqueio entra na lista mas **não nos números**: bloqueio é o clube fechando a própria agenda. Usa o **mesmo `PrecoDe` do Financeiro** — o preço não fica na reserva, sai da regra do horário, e dois cálculos divergiriam.
- **"Quero meu clube aqui" na tela Marcar Jogo** (build-121): a marcação é assinatura negociada caso a caso, então a tela ganhou porta pra quem quer o clube dele lá. Fica **depois** da lista (quem veio marcar jogo resolve primeiro o que veio fazer) e abre o WhatsApp com a mensagem pronta, já identificando quem é. Visual próprio em navy fixo com brilho verde — convite comercial que troca de cor com o tema vira mais um cartão da lista.
- **Marcar aula: nome completo + quem mais vem** (build-127). O professor aceitava sem saber duas coisas que mudam o treino: com que nome a pessoa se apresenta (o cadastro pode dizer um nome e ele conhecer outro) e **quantos vêm** — aula de padel é muitas vezes em dupla ou trio. Nome é obrigatório e **já vem pré-preenchido** (é confirmação, não pergunta nova); acompanhantes é **opcional e texto livre de propósito** — exigir conta no site pra cada um travaria a marcação por causa de quem nem usa o app. Aparece no **e-mail da solicitação** (onde ele decide), no modal da agenda (a linha some quando o aluno vem só) e no painel; nas três telas o nome dado na solicitação vem antes do nome do cadastro. **634 testes.**
- **Cadastro: a recusa parou de apagar o formulário** (build-112). As três travas de unicidade (CPF, login, e-mail — cada uma contra os **dois** campos de identificação) já existiam desde o build-63; o que faltava era a **experiência** da recusa: o formulário voltava **vazio** com o erro no topo, e a pessoa achava que o cadastro tinha sumido — foi exatamente o "Dev Padelizou" do teste de hoje. Agora a recusa devolve o formulário **preenchido** (senha nunca, de propósito), com mensagens na língua do usuário: *"Já foi criado um login com esse nome — escolha outro"*, *"Já tem alguém cadastrado com esse e-mail"*, e a do CPF já mandava pro "Esqueci minha senha". **Login passou a exigir mínimo de 4 caracteres** (`IdentidadeJogador.ValidarLogin` + `minlength` no HTML): login curto vira sigla ambígua num espaço de nomes dividido com os e-mails. Provado em tela: as 5 recusas e os campos sobrevivendo a cada uma. **613 testes.**

### 29/07/2026 (madrugada) — As respostas do Felipe viram código

O Felipe respondeu às 4 decisões pendentes e liberou o refactor. Tudo implementado, testado e publicado:

- **🔴 Professor assinante existe** (a maior pendência de produto do pipeline): **15 dias de teste** com condições de assinante, depois a escolha — **Assinante R$ 49,90/mês + 3% Pix/boleto / 6% cartão** ou **Avulso 10%**. O plano decide a taxa de CADA aula em `Services/PlanoDoProfessor` (regra pura, um lugar só): mensalidade atrasada (7 dias de carência) **volta sozinha pra 10%**, e pagar reativa na hora — ninguém precisa lembrar de desligar nada. O relógio do teste só começa quando o professor **vê** o painel, não no cadastro. Tela `/PlanoProfessor` com os dois pacotes e a conta pronta (20 aulas de R$ 100: R$ 200 avulso vs R$ 109,90 assinante); aluno de professor assinante declara a forma no checkout do Jogo Aula (mesma régua do torneio) e vê o total de cada opção. Mensalidade é cobrança nossa no gateway, sem split; webhook estende a vigência (pagar adiantado soma no fim; atrasado conta de hoje). Valores todos em `PlanoProfessorSettings` — renegociar não exige republicar. **Jonatas e Índio podem entrar como fundadores amanhã.**
- **Piso de comissão virou por tipo** (pré-requisito do 3%): Torneio R$ 4, **Aula e Jogo R$ 1** (só cobre o custo fixo do Pix). Sem isso, 3% de uma aula de R$ 100 viraria 4% disfarçado. Teste de regressão específico: R$ 3 de comissão numa aula de R$ 100 valem R$ 3 de verdade.
- **A condição dos 5% virou trava de verdade** (fluxo que o Felipe desenhou): organizador **encerra as inscrições** → área nova `Torneios/TaxaPlataforma` mostra a conta inteira (**pessoas × preço × 5%**; dupla completa = 2, sem parceiro = 1, lista de espera e impedimentos fora — errar pra menos é a cortesia certa) → paga pelo gateway → **webhook libera o sorteio sozinho**. Alternativa: **admin registra negociação** (com observação de como foi — "quem liberou isso?" tem resposta). A trava vale nos dois formatos (chaves E rodadas do Americano) e **no POST montado à mão**, não só no botão; torneio sem inscrito não trava. Provado em tela de ponta a ponta no ambiente local, incluindo a negociação liberando e o botão de sortear voltando.
- **Boleto herda os 10% do Pix** — pro gateway os dois custam o mesmo valor fixo em centavos; quem encarece é o cartão. O invariante do `CobrancaDoTorneio` mudou junto: *forma barata (Pix/boleto) ⟺ taxa menor*, testado em todas as combinações.
- **"Quero que meu clube esteja aqui"** — título do convite ajustado pro texto exato do Felipe; o destino já era o WhatsApp dele.
- **TorneiosController quebrado em 8 partials por área** (núcleo, Criação, Inscrições, TaxaExterno, Chaves, Placar, DiaDoJogo, Americano): 2.400 linhas viraram arquivos de ≤398, **nenhuma rota mudou** (partial class de propósito). Partição contígua por linha com conferência de soma, e smoke de runtime nas rotas depois do corte.
- Decisões do Felipe registradas de quebra: **chave do Asaas fica a mesma** (risco baixo; trocar continua recomendável um dia, sem pressa), **Acesso Antecipado continua nos dois ambientes** enquanto o sistema está em desenvolvimento, e os "horários de aula do Felipe em produção" **saem da lista de pendências** — ele não é professor, era dado de teste (a outra sessão já criou horários de teste no Chakra).
- 2 migrações novas (colunas anuláveis: taxa do externo no Torneio, plano do professor no Jogador). **650 testes.**

### 30/07/2026 — A varredura do sistema, e os achados dela fechados

Análise completa do sistema (33 controllers, ~73 serviços, CI, cabeçalhos que a produção
responde de verdade) e execução do que não dependia do Felipe:

- **🔴 Ninguém trancava a porta: força-bruta era ilimitada.** Dava pra tentar senha sem
  limite nenhum no login, no portão, na recuperação e no cadastro. Agora são **duas travas
  diferentes, de propósito** (`Services/TravaDeEntrada`): o **login** conta por **CONTA**
  (10 falhas / 5 min) e o resto conta por **IP** no rate limiter do próprio ASP.NET.
  **Por que não tudo por IP:** no dia de torneio o clube inteiro sai pelo mesmo Wi-Fi, e uma
  janela por IP no login trancaria gente legítima na pior hora possível. Por conta também
  cobre o ataque distribuído, que uma trava por IP deixa passar. Conta trancada recusa **até
  a senha certa** (senão a trava não trava nada) e acertar a senha zera a janela.
  O preço aceito: quem sabe o e-mail de outro consegue incomodá-lo por 5 minutos — troca
  barata por não deixar adivinharem a senha dele. **Provado ao vivo:** 10 tentativas passam,
  da 11ª em diante **429** com aviso em português, e a home segue 200 o tempo todo.
- **Cabeçalhos de segurança em prod e dev** (Caddy): `nosniff` (impede o navegador de
  "adivinhar" que um upload é script), `SAMEORIGIN` (o site não pode ser embutido em iframe
  alheio) e `Referrer-Policy`. Testado antes: só vinha o HSTS. O Caddyfile foi **validado
  antes de entrar** (`caddy validate` num arquivo de estágio) e os outros sites do VPS ficaram
  intocados. ⚠️ Backup do Caddyfile anterior em `/etc/caddy/Caddyfile.bak-20260730`.
- **Denunciar comentário, com fila no admin** (`/Admin/Denuncias`). Antes, texto ofensivo só
  saía do ar se o autor, o dono do perfil ou um admin **passassem por ali** — com o portão
  aberto ao público isso não se sustenta. Qualquer pessoa logada sinaliza; o admin **apaga ou
  mantém**. **Um carimbo só, o primeiro:** a fila ordena pela denúncia mais antiga, e
  re-carimbar empurraria justamente o pior texto pro fim da fila. Autor não denuncia o próprio
  comentário (pode apagar direto). Não existe "banir autor" aqui de propósito — punição de
  conta é decisão pra tomar com calma, não num clique de fila.
- **🔴 Convite de parceiro por link — o maior atrito da inscrição caiu.** Pra fechar a dupla
  era obrigatório digitar os **11 dígitos do CPF do parceiro**, que ninguém sabe de cabeça:
  inscrever dependia de uma conversa por fora antes de o site conseguir ajudar. Agora quem se
  inscreveu gera um link ("Convidar por link", com copiar e mandar no WhatsApp) e quem recebe
  **entra com a própria conta** e aceita. **De quebra fecha um furo de privacidade:** o
  formulário de CPF aceitava qualquer número, então dava pra inscrever alguém que nunca pediu
  isso — e criar conta no nome dele se o CPF não tivesse cadastro.
  Token de 32 bytes comparado em **tempo fixo**; **sem prazo em dias de propósito** (o fim das
  inscrições já é o prazo natural — prazo menor mataria o link com o torneio ainda aberto);
  aceitar **queima** o token; e a validade é conferida **de novo no POST**, porque entre abrir
  e clicar outra pessoa pode ter aceitado o mesmo link. As recusas do `TrocarParceiro`
  (categoria única, já inscrito, anti-sandbagging) saíram pra **um método compartilhado** — o
  caminho aberto por link não pode ser mais frouxo que o outro — e agora aparecem **já na tela
  do convite**, não só no clique. **Provado em runtime com duas sessões de verdade.**
- **`AulasController` quebrado em 7 partials** (era o maior arquivo do sistema, 1.383 linhas —
  o que o TorneiosController era anteontem): núcleo, Aluno, Decisão, Cadastro, Agenda,
  Financeiro e Caderno. Partição contígua com conferência de soma (1.339 + 42 de cabeçalho =
  1.381 linhas de conteúdo) e as **36 assinaturas de método idênticas** às de antes; as 10
  rotas conferidas em 200 com sessão de professor. Nenhuma rota mudou.
- **[ESTORNO.md](ESTORNO.md)**: o roteiro pra quando quem já pagou desiste. O estorno **já
  existia** em código; o que faltava era o roteiro. ⚠️ **Achado ao escrever:** estornar mexe
  **só no dinheiro** — a dupla continua inscrita e marcada como paga, e **a lista de espera não
  anda** até alguém remover à mão. Documentado como passo obrigatório e listado como decisão
  do Felipe (automatizar × avisar na tela).
- **O original de 8 MB do Pnatinha saiu da pasta pública** — mas **não foi apagado**: é a arte
  original, e o repositório só tem os derivados de 40–70 KB. Foi pra
  `/opt/padelizou-shared/prod/arte-original/`, com checksum conferido antes e depois.
  ⚠️ **A primeira tentativa o teria desprotegido:** movi pra fora de `prod/`, e o backup do
  Drive sincroniza **só `prod/`** — teria ficado só no disco do servidor, exatamente o risco
  que o backup off-site existe pra remover. Corrigido. Uploads de produção: **9,9 MB → 1,9 MB**.
- **Nome do job do CI** deixou de dizer "85 testes" (nasceu envelhecendo; hoje são 681).
- ⚠️ **Defeito meu, corrigido:** os cabeçalhos das 12 partials que gerei por script saíram como
  `InscriÃ§Ãµes` — o **PowerShell 5.1 lê `.ps1` como ANSI**, então os acentos chegaram
  corrompidos ao arquivo. Gerar código com acento por script exige `.ps1` com BOM.
- **681 testes** (+31 hoje).

### 30/07/2026 (tarde) — Preparando a primeira noite com gente de verdade

O Felipe decidiu liberar hoje à noite pro **primeiro organizador (torneio dos Corneteiros)** e
pro **primeiro professor**. Ensaio e ajustes:

- ✅ **O maior risco não existia:** o "modo demonstração" que fazia todo visitante entrar como
  o Felipe **já estava desligado em produção** (`LoginAutomaticoCpf` vazio no systemd). Se
  estivesse ligado, o organizador entraria na conta de administrador do Felipe. Conferido no
  ambiente de verdade, não na memória — que estava desatualizada nesse ponto.
- **Ensaio completo no dev** (configuração idêntica à prod), pelo caminho exato de hoje:
  portão → chega **deslogado** → cadastro → conta criada e já logada → **criar torneio 200** e
  **configurar recebimento 200**; e, no caminho do professor, cadastro com "sou professor" →
  painel **redireciona pra Minhas Cidades** (a escada cobrando) → tela do plano com os 15 dias
  de teste e os R$ 49,90.
- **🔴 Um defeito que eu mesmo criei hoje de manhã, achado no ensaio:** a trava de força-bruta
  partia só por IP, então **portão + cadastro + "esqueci minha senha" dividiam as mesmas 10
  tentativas**. Quem chega pela primeira vez faz as três coisas em sequência — e seria barrado
  no meio do próprio cadastro, com dois convidados no mesmo Wi-Fi somando no mesmo IP. Agora a
  janela é **por IP e por ação**, e o cadastro tem **20** (formulário longo: cada recusa gasta
  uma tentativa). A página do 429 deixou de ser beco sem saída.
- **Credenciais do portão trocadas** pra `Corneteiros` / `corneta` (drop-in do systemd, prod).
  ⚠️ **Testado antes de mudar:** `corneteiros` em minúscula era **recusado** — o teclado do
  celular decide sozinho se capitaliza, e isso viraria chamado de suporte na primeira noite.
  O **usuário** agora compara sem caixa e os dois campos levam `Trim`; **a senha continua
  exata**, porque ela é o segredo.
- **A chave do torneio restrito passou a ser escolhível** (pedido: `virgili10`). Era sorteada
  com 6 caracteres e não dava pra escolher — chave que a pessoa não consegue repetir no
  telefone vira ligação pro organizador. Campo opcional na criação; vazio continua sorteando;
  recusa com motivo antes de gravar (menos de 4, mais de 20, espaço no meio).
- **[PRIMEIROS-USUARIOS.md](PRIMEIROS-USUARIOS.md)**: as mensagens prontas pra mandar nos dois
  casos, o que eles vão encontrar (site vazio, escada do professor) e o que fazer se travar.
- **Decisão do 1º torneio:** vai ser **"por fora"**, e no fim o Felipe **registra a negociação**
  como admin pra liberar as chaves — exercita a corrente inteira sem dinheiro trocando de mão.
- ✅ **A corrente inteira ensaiada no ambiente publicado** (dev, mesmo build da prod), com conta
  criada na hora: conta nova → torneio restrito com chave `virgili10` → inscrição com chave
  **errada recusada** e com chave **CERTA em maiúscula aceita** → encerrar inscrições →
  **sorteio barrado até no POST feito à mão** → tela da taxa com a conta certa (**4 pessoas ×
  R$ 50 × 5% = R$ 10,00**) → admin registra a negociação (observação guardada) → **sorteio
  liberado, jogos criados, nenhum sem horário**, torneio em Fase de Grupos.
- **🔴 Um defeito achado nesse ensaio: torneio nascia sem categoria nenhuma, em silêncio.** Sem
  categoria o formulário de inscrição não tem o que escolher — **ninguém consegue se inscrever**,
  e o organizador só descobriria pelo primeiro jogador que tentasse. A caixa das categorias fica
  no fim de um formulário longo, então passar batido é o caso comum. Agora recusa com o motivo,
  antes de gravar.
- Conferido também: e-mail de produção configurado (Gmail SMTP) e **sem falha de envio em 7
  dias**; a conta do Felipe em prod é **admin raiz** (com o campo `Login` vazio — ele entra pelo
  **e-mail**); e o botão "Criar seu Torneio" aparece pra qualquer pessoa logada.
- **703 testes.**

### 30/07/2026 (noite) — 🎉 O PRIMEIRO USUÁRIO REAL ENTROU

**Lucas Almeida Coelho** (login `Foka`, professor) criou conta em produção às **15:46** — o
primeiro usuário de verdade do Padelizou, fora o Felipe. Em minutos ele já tinha criado
torneio, clube e inscrito uma dupla. O que o uso real mostrou em menos de uma hora:

- **🔴 Criou o mesmo torneio DUAS vezes** ("Amigos do Eder"). Formulário longo, botão apertado
  de novo por não ter certeza se salvou — e o sistema aceitou os dois em silêncio. Dois torneios
  iguais dividem as inscrições e ninguém sabe em qual entrar.
  **Agora é barrado**, por organizador e só enquanto o torneio está de pé (`Services/NomeDoTorneio`):
  quem faz o mesmo torneio todo mês precisa criar a próxima edição depois que a anterior termina,
  e "Copa de Verão" não pertence a ninguém — bloquear no sistema inteiro recusaria o torneio de
  um clube por causa do nome escolhido por outro. Compara **como uma pessoa compara** (sem caixa,
  sem acento, espaços colapsados).
  De quebra: as recusas da criação passaram a acontecer **todas antes** de achar-ou-criar o clube
  — antes, cada tentativa recusada deixava um clube novo no catálogo.
- **A inscrição criou um pré-cadastro** para o parceiro ("Giacomello"): é o comportamento
  desenhado (inscrever quem ainda não tem conta), e vale lembrar que **o convite por link**,
  publicado hoje de manhã, é o caminho que evita digitar o CPF do outro.
- **Limpeza pedida pelo Felipe**, feita com backup antes
  (`/opt/padelizou-shared/backup-prod-antes-limpeza-20260730-1716.sql.gz`): os 2 torneios, suas
  4 categorias, 2 quadras, a 1 dupla e o pré-cadastro Giacomello. Tudo em **uma transação** e na
  ordem das chaves estrangeiras, conferindo antes quem apontava pro jogador (só a dupla).
  **Preservados de propósito:** a conta do Lucas, os clubes que ele cadastrou e o **pagamento
  real de R$ 9** (o MEI obriga a guardar registro de receita).
  A pontuação do Lucas zerou junto — ela é **calculada** dos torneios, não guardada.
- **"Prefiro combinar com o Padelizou"** (pedido do Felipe ao olhar a tela de criação): a porta pra
  negociar a taxa dos 5% existia **só no fim** — na tela da taxa, depois de encerrar as inscrições.
  Mas quem precisa de prazo, parcelamento ou isenção quer saber disso **antes** de escolher como vai
  receber. O botão agora fica dentro do bloco "Por fora", na hora da escolha, e abre o WhatsApp já
  identificando quem fala (e o nome do torneio, se já tiver sido digitado — melhoria progressiva:
  sem JS o link continua valendo). Fica **fora do `<label>` de propósito**: dentro dele, clicar
  marcaria o rádio junto, e quem só queria perguntar acabaria escolhendo a forma.
  ⚠️ **Não virou uma quarta "forma de recebimento"**: as três respondem *como o jogador paga a
  inscrição*; a negociação é sobre *como a taxa é acertada com o Padelizou*. Como bolinha ali, o
  organizador leria "posso escolher não pagar a taxa".
- **O "Painel Admin" do menu abria aba nova e nunca acendia como selecionado.** O endereço era
  fixo (`https://admin.padelizou.com.br`), então clicar nele **no localhost ou no dev jogava
  quem estava testando dentro da produção**. Agora o destino é decidido por
  `AdminHostMiddleware.LinkDoPainel`: pula de host só no site público de produção, que é o único
  lugar onde `/Admin` dá 404 — no resto é relativo, na mesma aba, e acende igual aos outros itens.
  Dentro do painel o menu do site sumia em 404 item por item (ali só existem `/Admin` e `/Auth`):
  a barra agora vira **"← Voltar ao site" + "Painel Admin"**, e os dois links do rodapé apontam
  pra fora do host. Conferido nos quatro cenários (público, dev, localhost e dentro do painel).
- **Conquistas: duas escadas longas, e o bloco desceu pra baixo dos Elogios.** Vitórias em
  torneio agora têm degraus em **10, 25, 50, 100, 150 e 200**; os títulos vão de **bi a
  decacampeão** (bi, tri, tetra, penta, hexa, hepta, octa, nona, deca). De 12 conquistas pra
  **25** — a grade de 4 por fileira ganhou `justify-content-center` porque a última fileira
  passou a sobrar com 1. Os códigos antigos (`DezVitorias`, `Bicampeao`) não mudaram: conquista
  que alguém já viu no perfil não pode sumir porque a lista cresceu. Vale saber que a maioria
  desses degraus é meta de longo prazo — num perfil novo o bloco é quase todo cinza, e é por
  isso que ele desceu.
- **Todo aviso agora tenta também o WhatsApp da pessoa.** Antes, notificação só chegava pra
  quem instalou o app — que é a minoria. O disparo entrou **dentro do `PushNotificationService`**,
  num lugar só, então os ~30 pontos que mandam aviso ganharam o canal sem tocar em nenhum
  deles (e nenhum aviso novo nasce esquecendo). Sai **antes** do `return` de quem não tem
  push, porque é justamente essa pessoa que só é alcançável por lá. Respeita a preferência
  `NotificarWhatsApp` e exige número com 10/11 dígitos; falha do provedor **não derruba o
  aviso nem a ação que o gerou**. O botão de *notificação de teste* do painel ficou de fora
  de propósito — teste no celular de todo mundo faz a pessoa desligar o canal.
  ⚠️ **Não sai nada ainda:** a Z-API não está contratada (`ZApi.InstanceId`/`Token` em branco).
  Com as credenciais preenchidas, começa a enviar sozinho — nada mais a programar.
- **Pnatinha fora dos Comentários:** o mascote no meio da sequência Elogios → Conquistas →
  Comentários empurrava o perfil inteiro pra baixo por causa de uma linha de texto. Nos vazios
  de página inteira ele continua.
- **O canal de WhatsApp saiu do papel: Evolution API no nosso próprio VPS, custo R$ 0.**
  A Z-API (~R$ 100–150/mês) foi trocada por um container open-source rodando ao lado do app
  (`/opt/evolution/docker-compose.yml`), com **banco Postgres próprio** — se essa coisa
  corromper dado ou encher disco, o banco dos jogadores nem fica sabendo. Escuta **só em
  `127.0.0.1:8081`** (conferido de fora: sem resposta), imagem **fixada em v2.3.7**, e
  **não grava mensagem, contato, conversa nem histórico**: o Padelizou só envia, e guardar o
  WhatsApp dos jogadores seria dado de terceiro sem motivo.
  No código, `EvolutionApiService` substituiu o `WhatsAppApiService`; a interface
  `IWhatsAppService` não mudou, então o resto do sistema nem soube da troca. **Ligado só em
  produção** (drop-in `whatsapp.conf` no systemd) — no localhost e no dev nasce desligado,
  senão um teste manda mensagem pro celular de gente de verdade.
  ⏳ **Falta um passo, e é físico:** comprar um **chip pré-pago** (nunca o número pessoal — se
  a Meta banir, o número morre) e ler o QR code. O comando está no STATUS abaixo.
- **Painel: "Teste de aviso" dirigido a UMA pessoa.** Digita o **login (ou e-mail)** de quem
  vai receber, marca **WhatsApp e/ou notificação do app**, manda. O botão antigo continua lá,
  mas ele é outra coisa: dispara pra todo mundo com o app, e **só push**.
  O que essa tela tem de diferente é o **diagnóstico**: "não chegou" tinha meia dúzia de
  causas que na tela viravam a mesma coisa. Agora ela diz **qual** — jogador sem o app,
  número em branco no cadastro, preferência desmarcada pelo próprio jogador, canal desligado
  no ambiente, ou provedor recusando. Cada motivo tem frase própria (tem teste garantindo que
  não se repetem), e o envio bem-sucedido mostra **o número de volta** — digitar login na mão
  erra fácil, e é assim que se percebe antes de sair contando que o canal funciona.
  **Acha a pessoa por login, e-mail, nome, apelido ou CPF** (o CPF só inteiro — parcial
  deixaria varrer os documentos da base aos poucos). Login e e-mail acham direto; **nome pode
  achar mais de uma, e aí a tela pede pra escolher** em vez de chutar: teste que foi pro
  homônimo errado é pior que teste nenhum, porque o admin conclui que testou. Na lista de
  escolha o **CPF aparece mascarado** — o miolo basta pra desempatar.
- ⚠️ **O chip desconectou sozinho na primeira noite.** O log do container é claro:
  `conflict: device_removed` — o aparelho pareado foi **removido do lado do celular**. Não é
  bug do sistema nem ban: é o WhatsApp derrubando o dispositivo vinculado. **Precisa ler o QR
  de novo.** O diagnóstico da tela nova apontou certo na primeira tentativa ("o provedor
  recusou o envio, veja se a instância está conectada"), que era exatamente pra isso.
- **A tela de teste virou dois tempos, e o envio agora grita.** Achar e mandar eram a mesma
  ação, então o admin só descobria pra quem tinha mandado **depois** de já ter mandado — num
  teste, tarde. Agora: procura → **mostra quem achou** (com login, e-mail, CPF mascarado e o
  número de WhatsApp que vai receber) → só então o botão de enviar existe, com o nome da
  pessoa escrito nele. Achou mais de um? Escolhe. Achou um só? Já fica escolhido, mas na tela.
  E o resultado virou faixa: **verde "Enviado para Fulano!"** quando algum canal entregou de
  verdade, **amarela "Não chegou em nenhum canal"** quando não. Junto veio um defeito de
  honestidade: o push contava como "enviado" só por existir aparelho cadastrado, mesmo quando
  a entrega falhava — agora conta **entregas**, e inscrição já revogada pelo navegador aparece
  como falha em vez de sucesso.
- **816 testes.**

### 30/07/2026 (noite) — O app de celular, e o iPhone que dizia "não suporta"

Pergunta do Felipe: *precisa ir pra loja, ou dá pra baixar direto da página?* **Dá direto da
página, e já dava** — o Padelizou é PWA desde 25/07. A decisão foi **ficar só no PWA por agora**;
a loja fica pra quando houver base de usuários, e nada do trabalho de hoje se perde nesse dia,
porque o app da Play Store seria este mesmo site embrulhado (TWA).

⚠️ **APK solto no site nunca**: aviso vermelho de "fonte desconhecida", Play Protect assustando,
e **sem atualização automática** — cada correção exigiria todo mundo baixar de novo. E no iPhone
é impossível. O que a loja daria de verdade é **uma coisa só: a pessoa buscar "padel" e achar**.
Custos, pra quando for a hora: Play US$ 25 uma vez (conta pessoal precisa de **12 testadores por
14 dias**; conta de empresa escapa disso mas exige D-U-N-S, que demora semanas — e o CNPJ MEI já
existe), Apple US$ 99/**ano** + Mac pra publicar, com risco de recusa por "site embrulhado".

- **🔴 O iPhone dizia "seu navegador não suporta notificações push".** Suporta: no iOS o
  `PushManager` **só passa a existir depois** de a pessoa adicionar o Padelizou à tela de início.
  Quem tocasse no botão lia que o aparelho dele não servia — e desistia pra sempre, do canal que
  a gente mais quer que ele use. Agora o botão diz **"Instale o app pra receber avisos"**,
  **continua clicável**, e o clique abre o passo a passo que já estava na mesma tela.
  `motivoSemPush()` separa **"precisa instalar"** de **"não suporta"** — eram a mesma coisa no código.
- **Botão de instalar de verdade no Android.** Mandávamos a pessoa caçar nos três pontinhos.
  O `beforeinstallprompt` agora é capturado **no `<head>`** (no fim do `<body>` ele às vezes já
  passou, e aí o botão nunca apareceria) e o modal abre a **caixa nativa** com um toque. Recusou?
  Volta pro passo a passo escrito, em vez de virar um botão que não faz nada. No iPhone continua
  só o texto: **a Apple não deixa nenhum site abrir essa caixa** — não é limitação nossa.
- **"Instalar o app" fixo no menu.** O convite aparecia **uma vez por aparelho**; quem fechasse
  sem querer não tinha mais volta. Some sozinho pra quem já instalou.
- **Tela de "sem internet"** (`wwwroot/offline.html`). Instalado e sem sinal, o Chrome desenhava
  o **dinossauro dentro do app** — e app que mostra erro de navegador parece app quebrado, não
  celular sem sinal. Agora navegação que falha cai numa tela nossa, que **recarrega sozinha
  quando o sinal volta**. Ela não pode depender de nada da rede: tudo inline, e o logo vem do
  cache. Testado **derrubando o servidor** com o app aberto: pedi `/Torneios` e veio a tela offline.
- **Prints no manifest** (3 telas em 504×1000). Sem eles o Android mostra uma barrinha sem graça;
  com eles, uma janela com imagem e descrição, cara de loja. Capturados com Edge headless, que
  **trava a largura mínima em 504px** — como o Bootstrap só muda de layout em 576, o print sai
  com o layout de celular de verdade. Junto: **ícone 192** e o **`id`** do manifest, que faltavam.
- **Faixa segura do iPhone**: `viewport-fit=cover` + `env(safe-area-*)` na barra e no rodapé. O
  app ocupa a tela toda sem o menu ficar embaixo do relógio. Em Android e no navegador normal
  esses valores são **zero** — nada muda.
- **Achado pelo teste, não por mim:** o `favicon-32.png` tem **64×64** de verdade e o manifest
  jurava 32×32. **Tamanho mentido faz o navegador descartar o ícone**, calado. Corrigido no
  manifest e no `_Layout` (o nome do arquivo ficou: ele também é o selo das notificações no
  Android, onde 32px borraria).
- **5 testes novos** (`PwaArquivosTests`) amarrando `manifest.json` e `sw.js` ao que existe no
  disco. São **dois arquivos que ninguém compila** e que citam imagens pelo caminho: um nome
  errado no `addAll` derruba a **instalação inteira** do service worker — sem cache, sem tela
  offline, sem push — e **sem um único erro na tela**. **821 testes.**
- **Publicado em produção e no dev (`build-154-abf6895`)**, os dois com `/healthz` 200. Conferido
  no ar: manifest com `id` e os 3 prints, `offline.html`, `icon-192` e `instalar-app.js` todos
  200, e o `sw.js` servindo `padelizou-static-v4`.
  ⚠️ **Falta o teste que importa:** nada disso foi visto num **celular de verdade** — a caixa
  nativa do Android e o "Adicionar à Tela de Início" do iPhone foram simulados e conferidos
  estado por estado, mas quem confirma é o Felipe instalando no aparelho dele.

### 04/08/2026 (tarde) — A Meta restringiu o número, e o canal foi redesenhado

Depois de religado, o número apareceu com **"Sua conta está restringida"** — 4h de bloqueio,
motivo *spam*. Isso reexplicou tudo: as falhas de ontem às 18h36 não eram socket morrendo, era
a Meta **já cortando os envios**.

**A causa, com os dados na mão:** entre 18h e 19h do dia 03/08 **24 pessoas se cadastraram**
(53 no dia), e cada uma disparou avisos automáticos. Um número com **4 dias de vida** saiu do
zero e mandou dezenas de mensagens em uma hora, pra gente que nunca tinha escrito pra ele. A
primeira falha foi **18h36** — no meio dessa primeira hora.

Não foi o chip recauchutado (ligação de quem procurava o dono antigo é tráfego de ENTRADA, e
entrada não restringe ninguém). Mas ser chip novo importou: **reputação zero deixa a barreira
baixa**. O mesmo disparo num número com meses de conversa normal provavelmente teria passado.

- **Envio desligado na hora** (`Evolution__BaseUrl` vazio no systemd, backup do drop-in em
  `/root/whatsapp.conf.desligado-04-08`). O vigia entende como "desligado" e fica quieto.
- **O padrão virou o silêncio.** `AlcanceDoAviso.SoApp` é o valor por omissão de
  `EnviarParaJogadorAsync`: **aviso novo nasce sem WhatsApp**. Antes era o contrário, e é por
  isso que cada aviso novo entrava no canal sem ninguém decidir.
- **De 26 tipos de aviso, só 9 pedem WhatsApp** — os que são pessoais, urgentes e acionáveis:
  seu jogo é o próximo · jogo em 24h · chaves saíram · inscrição confirmada · abriu vaga (os
  **dois** caminhos, estorno e desistência) · pagamento pendente · nova solicitação de aula ·
  aula desmarcada · aula apagada. **Os 8 disparos em massa saíram todos**, incluindo o "Novo
  torneio aberto", que sozinho mandava 63 mensagens de uma vez.
- **Fila com respiro** (`FilaDeWhatsApp` + `EntregadorDeWhatsAppBackgroundService`): uma
  mensagem por vez, **7 a 16s entre elas, sorteado** — cadência exata de relógio é assinatura
  de robô. O respiro vem DEPOIS de entregar, então aviso solitário e urgente sai na hora. Fila
  em memória (aviso é perecível) e limitada a 500: cheia, **devolve false** em vez de segurar a
  ação do jogador. ⚠️ `BoundedChannelFullMode.Drop*` seria a escolha óbvia pelo nome e está
  errada — com ela `TryWrite` devolve `true` mesmo jogando a mensagem fora.
- **"Receber por WhatsApp" nasce DESMARCADO** no cadastro, único canal assim. Marcado por
  omissão, a pessoa "aceitava" sem nunca ter decidido — e é exatamente essa mensagem que vira
  denúncia.
- **Defeito achado no caminho:** o lembrete de 24h mandava **duas vezes** — WhatsApp direto e
  de novo pelo push (que passou a mandar WhatsApp quando o fan-out entrou). Mensagem repetida é
  o que faz alguém apertar "bloquear".
- **1.398 testes.** ⚠️ **As 63 contas que já existem seguem com o WhatsApp marcado** — elas
  nunca escolheram isso, foi o padrão antigo. Decisão do Felipe se desmarca todas.
- **O cadastro agora termina convidando a instalar o app.** A primeira tela depois de criar a
  conta abre o modal de instalação em modo boas-vindas ("Conta criada — falta 1 toque pra
  virar app"), vendendo o que a pessoa ganha: os avisos do jogo dela no celular. Ignora o "já
  dispensei" do aparelho (conta nova ainda não disse não) e aparece até no computador. O sinal
  vem por TempData — morre sozinho no primeiro carregamento, sem tocar banco. É o ataque à
  raiz do problema do WhatsApp: **cada instalação é alguém alcançável por push**, o único
  canal 100% nosso (hoje: 2 de 67). Ensaiado com cadastro real no localhost: 1º carregamento
  convida, 2º não. Direção decidida com o Felipe: **TWA na Play Store** (conta como empresa
  pelo CNPJ do MEI, US$ 25 única vez) e **iPhone fica no PWA** — App Store (US$ 99/ano) só se
  usuário de iPhone sentir falta. **1.402 testes.**

### 04/08/2026 — O WhatsApp ficou 17 horas fora e ninguém soube

O Felipe perguntou se as notificações estavam certas. **Não estavam.** O canal caiu em
**03/08 às 18h36** e seguia fora — **~200 avisos falharam** (163 erro interno + 38 recusados),
cada um escrevendo no log, e nada nem ninguém foi avisado.

- **Consertado com um restart da instância** — o pareamento estava vivo, só o socket tinha
  morrido, então **não precisou de QR**. A instância estava presa em `connecting`, que parece
  esperança e não envia nada. Confirmado com mensagem real + zero falhas novas depois.
- **O número que muda a leitura de tudo:** das **67 contas ativas**, **63 só são alcançáveis
  por WhatsApp** e **2 têm o app instalado**. O WhatsApp não é um canal a mais — é *o* canal.
  Com ele fora, o aviso não chega em 94% das pessoas.
- **Segunda queda em cinco dias** (30/07 foi `device_removed`, que exigiu QR; 03/08 foi socket,
  que o restart resolve). Vai acontecer de novo.
- **O problema nunca foi a queda — foi o silêncio.** Daí o `VigiaDoWhatsAppBackgroundService`:
  confere de **5 em 5 minutos**, **religa sozinho** quando o restart tem chance, e só manda
  e-mail (no máximo de 6h em 6h) quando o conserto exige gente — QR novo ou container caído.
  A decisão mora em `VigiaDoWhatsApp.Decidir`, pura e testada; o serviço é só o relógio.
  Espera 20s depois do restart antes de reavaliar, senão o e-mail sairia por um problema que
  se resolveu dez segundos depois.
- **Selo vermelho no `/Admin`** quando o canal está fora, lido da última passada do vigia (não
  de uma consulta na hora — o painel não pode depender de rede pra abrir). No dev não aparece:
  lá o canal é desligado de propósito, e alarme sempre aceso é alarme que ninguém olha.
- ⚠️ **Os ~200 avisos perdidos não voltam.** Não existe fila nem reenvio: cada aviso é tentado
  uma vez. Se algum jogo ou torneio dependia de um deles ontem à noite, ninguém foi avisado.

### 30/07/2026 (noite) — O clube vira balcão

O Felipe pediu a melhor proposta pra área do clube ("pode ser a parte de maior lucro depois
dos torneios"). O diagnóstico: as telas de gestão já existiam, mas faltava **a cena mais comum
da vida real** — toca o WhatsApp, "tem quadra amanhã às 19h?", e o dono não tinha onde
registrar. Bloquear escondia nome e receita; deixar livre arriscava venda dupla pelo site.
Resultado: o clube mantinha o caderninho JUNTO com o sistema, e ninguém paga mensalidade pra
ter dois controles. Proposta aprovada (mockup antes de codar) e implementada inteira:

- **Reserva de balcão**: horário livre (no mapa E no Hoje) abre "Marcar pra alguém" — nome e
  celular **texto livre, sem exigir conta** (decisão do Felipe). Celular que bate com uma
  conta liga a reserva a ela por baixo (aparece no "minhas marcações" do cliente), mas **o
  nome digitado continua mandando na tela** — é o nome que o dono conhece. Conflito recusado
  com a mesma régua do site; cancelar só de balcão (a do site envolve conta e dinheiro do
  jogador).
- **Pago / paga lá**: balcão nasce "paga lá" (a vida real do telefone); "recebi" carimba e
  **não desmarca** — sumir com receita registrada não pode ser um clique. Site via checkout
  nasce "pago" (webhook); sem cobrança online nasce "paga lá". **Reserva antiga fica sem
  selo**: não dá pra saber como foi acertada, e chutar mentiria pra um lado ou pro outro.
- **O painel abre no HOJE**: o dia em ordem de hora — reservas com selo e WhatsApp, livres
  clicáveis com o botão de marcar. O dono opera o dia, não o mês. "Próximos dias" abaixo.
- **Financeiro**: receita dividida **site × balcão** e o card **"A receber no balcão"** — o
  número que o dono compara com o bolso no fim do dia.
- Migração de 4 colunas anuláveis em `MarcacaoJogo`; regra pura em `Services/ReservaDeBalcao`.
  Provado em tela de ponta a ponta no local (marcar → selo no mapa → a receber → recebi).
  **845 testes.**
- **Grade num clique** (pedido do Felipe, na sequência): card "Montar a grade de uma vez" na
  tela de horários — quadra (ou todas), dias, abre/fecha, preço, e os **três botões (30 min /
  1h / 1h30) já são o envio**. Idêntica pausada religa (mesma regra do professor); sobreposição
  com ativa é pulada e contada — gerar por cima venderia o mesmo horário duas vezes; rodar de
  novo não duplica. E o **"Excluir todos"** (respeitando o seletor de quadra): DELETE de
  verdade pro "montei errado, quero recomeçar", com a consequência dita na confirmação (preço
  de reserva antiga sai da regra — apagar regra com histórico tira o valor do financeiro).
  Provado em tela: 36 excluídos, 28 gerados (4 quadras × 7 dias), 2ª rodada sem duplicar.
- ✅ **Copa/bar: decidido em 30/07 — o clube vai SEM essa parte por enquanto.** PDV fiscal e
  estoque são outro produto (não entrar). O candidato certo pra quando um clube real pedir é
  a **comanda pendurada na reserva** ("põe na minha conta": os itens da copa presos à reserva
  do jogador, fechando quadra + copa juntos no "recebi") — o lançamento genérico de
  receitas/despesas não mata o caderninho da copa, porque a venda é por pessoa, não por dia.
  Estimativa quando chegar a hora: caixa simples ~1 dia, comanda ~2 dias.

---

## 🎯 O que realmente falta (auditado em 26/07)

Das 6 fases originais, **4 estão fechadas**. Sobrou pouco, e o que sobrou está em 3 grupos:

| | O quê | Quem faz |
|---|---|---|
| 🔴 **Bloqueia o negócio** | Asaas para produção · limpar dados fictícios | **Felipe decide** |
| 🟡 **Fecha pendências** | ~~Código morto~~ ✅ · ~~184 MB no VPS~~ ✅ · ~~Postgres local~~ ✅ · ~~varredura de autorização~~ ✅ | **fechado** |
| 🟢 **Cresce depois** | ~~2 pushes do dia de jogo~~ ✅ · ~~quadra atrasada~~ ✅ · ~~placar offline~~ ✅ · ~~convite sem CPF~~ ✅ 30/07 · arte pro Instagram · Play Store | sem pressa |

**Nada do que sobrou impede um torneio real de acontecer amanhã.** O único impedimento é a chave do Asaas.

---

## 🤝 Pipeline de clientes (informado pelo Felipe em 29/07/2026)

Quase confirmados, em três frentes:

| Frente | Quem | Modelo de cobrança | Dá pra vender hoje? |
|---|---|---|---|
| **Torneios** | Loberos, Corneteiros, Golden Point, Nata Padel, Chakra, Er Padel | régua 5/10/15% já no ar | ✅ produto completo, pagamento real já testado |
| **Professores** | Jonatas Portal, Gabriel Reis "Índio" | assinante R$ 49,90/mês + 3% Pix / 6% cartão | ⚠️ modelo decidido mas **não implementado** — entram como fundadores (1º mês grátis) enquanto se constrói |
| **Clubes** | Golden Point, Er Padel, Chakra Padel | mensalidade caso a caso (âncora R$ 59–99/quadra) | ⚠️ preço não fechado; a porta de entrada é o torneio deles |

- **Golden Point, Chakra e Er Padel aparecem em DUAS frentes** (clube + torneio): entrar pelo torneio (pronto, sem mensalidade) e a conversa de clube vem depois, com o sistema já em uso na casa.
- Antes do primeiro cliente externo entrar: decidir o **portão de Acesso Antecipado** (dar a senha ou abrir), gerar **chave+token novos do Asaas**, e decidir o **"externo 5%"** (hoje na prática é grátis — pode virar argumento de venda em vez de furo).

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
- [x] ✅ **Google: app publicado ("Em produção") 29/07** — o token do backup fora do servidor não expira mais a cada 7 dias. Sem custo (API gratuita; 22,7 MB de 15 GB no Drive) e sem verificação do Google (escopo `drive.file` não é restrito).
- [ ] ⏳ **Conta bancária no Asaas** — **em andamento, sem pressa**: o Felipe está abrindo a conta PJ (o MEI é recém-criado, leva alguns dias). Auditado pela API em 29/07: comercial/documentação/geral **APROVADOS**, só `bankAccountInfo: PENDING`, **0 contas cadastradas**, saldo **R$ 0,00**. Nada está travado: o pagamento de R$ 9 está `CONFIRMED` mas com `paymentDate` vazio — no cartão o dinheiro só é liberado em ~32 dias (≈28/08), e o líquido é R$ 8,34. Enquanto não houver saldo, a pendência não impede nada; dinheiro que entrar antes fica acumulado na conta do gateway, não se perde. ⚠️ Ao cadastrar, a **titularidade tem que bater com o CNPJ do MEI** — conta de pessoa física costuma ser recusada
- [x] ✅ **Webhook auditado pela API em 29/07**: produção tem **1 só** webhook, ativo, não interrompido, 0 requisições penalizadas. As recusas de hora em hora vinham de um webhook do **sandbox** apontando pra URL de produção (o Asaas já o havia marcado `interrupted`) — apagado com autorização. O "Atomatiza" no sandbox é de outro projeto do Felipe e não foi tocado.
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
- [x] **Aviso de quadra atrasada** ✅ 29/07 (build-102). O "seu jogo é o próximo" é disparado pelo fim do jogo anterior de propósito; este é o complemento: **atraso é um fato de relógio**. Push pros 4 jogadores quando a partida agendada passa de **15 min** sem começar (tolerância: grade escorrega minutos o tempo todo, avisar no 1º minuto ensinaria a ignorar), com **teto de 3h** (além disso é torneio com problema, não "fique por perto" — e protege o jogo de ontem nunca lançado). **Só dispara se alguma bola rolou HOJE** no torneio — sem isso, torneio de portão fechado pushparia "atrasado" em massa, e a final de sábado não pode fazer o domingo "já ter começado". Quem já ouviu "é o próximo" fica fora (os dois avisos se contradizem). Um aviso por partida (`AvisoAtrasoEnviadoEm`, migração de 1 coluna). Mensagem diz o que a pessoa decide com ela: quanto esperar e que **não perdeu a vez**. Regra pura em `Services/QuadraAtrasada` (12 testes); tick de 5 min com filtro barato no banco. Primeiro tick verificado limpo em produção. ⚠️ *Ainda não exercitado com torneio real atrasado — o primeiro dia de jogo de verdade é o teste de fogo.* **563 testes.**
- [x] **Placar que funciona sem internet** ✅ 29/07 (build-117) — **FECHA A FASE 3 INTEIRA.** A regra: *o toque do organizador nunca se perde*. Cada toque atualiza a tela na hora, entra numa fila no aparelho (`localStorage`) e é entregue quando a rede deixar. **A decisão central: mandar o placar INTEIRO, não o "+1"** — incremento reentregue pela fila dobraria o game; placar absoluto reenviado dá sempre no mesmo lugar, e de vinte toques presos só o último estado viaja (medido: 2 toques offline = 1 item na fila). Vence o placar marcado por último **na quadra** (relógio do aparelho, coluna `PlacarMarcadoEm`): fila atrasada ou segundo aparelho esquecido não atropelam o de agora; partida finalizada não aceita placar da fila. `FinalizarPartida` entrou na fila e ganhou guarda idempotente (reentrega não redispara robô de mata-mata). **Selo de rede sempre visível** ("Sem internet — N mudanças guardadas. **Pode continuar marcando.**"): organizador que não sabe disso para de marcar achando que quebrou — e aí sim se perde placar. A Mesa virou a **única página com cache no service worker** (rede primeiro, cópia só quando a rede falha): celular trava a tela, navegador descarta a página, organizador recarrega — e a Mesa volta, com o placar corrigido pela fila local. O endpoint incremental `AtualizarPlacarAoVivo` foi removido (a Mesa era a única usuária); entra `SincronizarPlacar` com checagem de organizador. Regra em `Services/PlacarDaMesa` (pura, 6 testes). **Provado ao vivo**: rede derrubada, toques offline, fila entregou ao voltar, banco carimbado, placar 10 min mais velho recusado, página no cache. **619 testes.**

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
- [x] **Adversários do Americano** ✅ 29/07 (build-100) — **cada jogador enfrenta cada rival EXATAMENTE 2 vezes** (torneio de whist), de 4 a 32 jogadores. As tabelas foram **encontradas por busca fora do sistema** e embutidas como dado: 12–32 usam base cíclica com starter livre (uma rodada-base boa gira e vira o torneio inteiro); 8 não tem base cíclica possível (provado por exaustão nos 3 agrupamentos) e leva a tabela completa. O sorteio segue real — o desenho é fixo, quem veste qual número é sorteado (n! variações). 36+ cai no método antigo, mantido como fallback com teste próprio. **O teste de integridade pegou na primeira rodada um erro de transcrição** (base de 32 com 7 mesas em vez de 8) — exatamente o tipo de defeito que tabela embutida sofre. De quebra a suíte caiu de 13s pra 3s: tabela pronta não otimiza nada. **551 testes.**

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
Gerado em 07/08/2026, no repo:
- **Apresentação de vendas completa** — `APRESENTACAO.html` (fonte) + `Padelizou-Apresentacao.pdf`
  (12 páginas A4). O sistema inteiro por papel — jogador, organizador, professor, clube —
  com mockups de celular desenhados em CSS e a mensagem central "tudo pelo celular, instala
  sem loja". Taxas conferidas no código do dia: torneio 5%/10%/15%, professor R$ 49,90+3%/6%
  (15 dias de teste, avulso 10%), Americano valendo ponto R$ 5/pessoa (piso 8). Sem citar o
  gateway, sem bar (atrás de flag). Regerar o PDF: Edge headless `--print-to-pdf` sobre o HTML.

Gerados em 25/07/2026, salvos também em PDF na Área de Trabalho:
- **Análise do sistema** — diagnóstico completo por área
- **Plano de evolução** — as 6 fases detalhadas com justificativa
- **Inventário de melhorias** — as 41 melhorias com status individual

Gerados em 26/07/2026 (PDFs na Área de Trabalho + artifacts no claude.ai):
- **Apresentação comercial** — página de venda com o posicionamento "o padel cresceu,
  chegou a plataforma à altura"; falta trocar o WhatsApp de exemplo do botão final.
  Artifact: `claude.ai/code/artifact/05bdbad2-ff8c-411f-99a2-675455a21756`
- **Análise de monetização — 2ª edição 29/07/2026, alinhada ao código:**
  · Torneio: MANTÉM a régua já construída — organizador escolhe: externo 5% / só Pix 10% /
    todas as formas 15%, taxa descontada. ⚠️ o 5% do "externo" NÃO tem mecanismo de
    cobrança (na prática é grátis) — decidir: zerar oficialmente ou construir faturamento
  · Professor (decidido, AINDA NÃO IMPLEMENTADO): **100% assinante** — R$ 49,90/mês ou
    R$ 499,90/ano + **3% Pix / 6% cartão**; 1º mês grátis; fundadores; carência na saída.
    Implementação sugerida: régua por forma de recebimento como no torneio (só Pix 3% /
    todas 6%), modo descontado, mínimo próprio (~R$ 2 — o piso global de R$ 4 atropela
    o 3% até aula de R$ 133) + assinatura recorrente via Asaas
  · Clube: mensalidade negociada caso a caso (âncora interna R$ 59–99/quadra; falta
    decidir o cruzamento com a comissão de reserva)
  · Jogador: nunca paga
  Taxas reais do Asaas (conferidas 27/07, promocionais até 27/10/2026): Pix R$ 0,99
  (100 primeiros do mês grátis), crédito à vista 1,99%+0,49, 21× 3,29%+0,49; depois
  Pix R$ 1,99, à vista 2,99% — **27/10/2026 é data de recálculo dos pisos**.
  Vigia do teto MEI já existe (AlertaMeiBackgroundService, e-mail aos 70% e 90%).
  Artifact: `claude.ai/code/artifact/128ee0e3-a783-4bfa-9ff8-d7b24f8f6c43`
- **Concorrente mapeado: Gripo (gripo.com.br)** — preços conferidos 05/08/2026: clube
  R$ 149/219 por clube/mês + módulos (NF, TEF, WhatsApp); torneio R$ 4,80/dupla, mínimo
  R$ 200 (cobra por inscrição mesmo com dinheiro por fora); pagamento online 0,89%.
  Forte em balcão (nota fiscal, maquininha, comanda, facial); NÃO tem professor nem rede
  de jogador. Torneio de 60 duplas × R$ 150 (R$ 9.000): Gripo ≈ R$ 368 · nosso externo
  R$ 450 (briga de perto) · só Pix R$ 900 (**~2,5×** — a conta de 05/08 dizia 6× por erro:
  comparava 2 torneios nossos com 1 deles; corrigido no mesmo dia) · todas R$ 1.350.
  Recomendação registrada (decisão em aberto): NÃO baixar % agora — pro Gripo torneio é
  isca do SaaS de clube, pra nós é o motor até o MRR de professor/clube existir; na gaveta,
  contra-oferta fixa por dupla pra quando o Gripo for citado. Comparação completa na
  seção 3 do artifact de monetização.

> ⚠️ Os 3 documentos refletem o diagnóstico de **25/07 de manhã** e envelheceram: a maior
> parte do que eles listam como "falta" já foi entregue. Este STATUS.md é a fonte da verdade.
