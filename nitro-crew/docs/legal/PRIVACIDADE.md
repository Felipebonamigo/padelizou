# Política de privacidade — Nitro Crew (rascunho)

> **Rascunho técnico, não revisado por advogado.** Escrito a partir do que o código faz em 25/09/2026, para o
> advogado revisar (passo 5.7 do roteiro). Os pontos que precisam de revisão jurídica estão marcados com
> **⚖️ ADVOGADO**. Os marcados com **🔧 CONFERIR** dependem de algo que ainda não existe (servidor, empresa) e
> precisam ser confirmados no dia em que existir. Antes de publicar: apagar este quadro, os marcadores e a
> seção "Para quem mantém este texto".

**Última atualização:** [data da publicação]

## Em poucas palavras

- O jogo guarda **no seu computador** as suas opções, o seu progresso e um registro de erros. Nada disso é
  enviado para nós.
- Se você usa a Steam, a **Steam Cloud** (da Valve) copia as suas opções e o seu progresso entre os seus
  computadores. O registro de erros não vai para a nuvem.
- Para jogar **online**, o servidor que liga os jogadores (o "relay") recebe o nome que você escolheu para a
  partida e os comandos do jogo, só enquanto a sala existe. Ele não grava nada em disco.
- A **telemetria anônima** vem **desligada**. Hoje, mesmo ligada, ela não envia nada: ainda não existe servidor
  para recebê-la. Se isso mudar, esta política muda antes.
- O **relatório de erros** só sai do seu computador se você mesmo copiar e mandar para nós.

## 1. Quem somos

**Controlador dos dados:** [nome da empresa], CNPJ [número], [endereço]. Contato para privacidade:
[e-mail de privacidade].

⚖️ **ADVOGADO:** confirmar o controlador (pessoa física ou jurídica — passo 5.1 do roteiro) e se é preciso
indicar um **encarregado (DPO)**. A Resolução CD/ANPD nº 2/2022 dispensa agentes de tratamento de pequeno porte
de indicar encarregado, mas exige um canal de comunicação com o titular — confirmar se o caso se enquadra.

## 2. O que fica no seu computador (e só nele)

O jogo grava estes dados **localmente**. Nós não temos acesso a eles.

| O quê | Exemplos | Onde |
|---|---|---|
| **Opções** | idioma, volumes, qualidade de imagem, dificuldade, se a telemetria está ligada (e em qual versão destes termos você a ligou) | `nitro-crew.settings` |
| **Progresso** | copas concluídas, recordes de volta e de corrida por pista, conquistas, número de corridas e vitórias | `nitro-crew.save` |
| **Nomes dos jogadores** | os nomes que você digita para cada assento (P1–P4) e o carro de cada um, e o nome gravado junto de cada recorde | dentro do progresso |
| **Registro de erros** | até 50 erros: versão do jogo, data e hora (da primeira e da última vez), quantas vezes aconteceu, tipo do erro, mensagem técnica, trecho do código onde aconteceu, modo de jogo, pista e tela abertas na última vez | `nitro-crew.errors` e, na versão para computador, o arquivo `logs/errors.log` |

**Onde exatamente:** na versão para computador, na pasta de dados do jogo — `%APPDATA%\Nitro Crew` (Windows),
`~/.config/Nitro Crew` (Linux e Steam Deck), `~/Library/Application Support/Nitro Crew` (macOS). No navegador,
no armazenamento local do site.

**Os nomes dos jogadores** são o que você quiser digitar ("P1", um apelido). Recomendamos não usar o nome
completo de ninguém.

**O registro de erros** é limpo antes de ser gravado: caminhos de pasta do seu computador e o seu nome de
usuário do sistema operacional são tirados do texto. Ele não guarda os nomes dos jogadores. O arquivo de log
tem tamanho limitado (512 KB, com um arquivo anterior guardado) e o registro interno guarda só os 50 erros mais
recentes.

**Para apagar tudo:** desinstale o jogo e apague a pasta de dados indicada acima (no navegador, limpe os dados
do site). Apagar a pasta também apaga o progresso.

## 3. Steam Cloud (Valve)

Se você joga pela Steam com a Steam Cloud ativada, a Steam copia os arquivos `saves/*.json` da pasta de dados
(**opções e progresso, incluindo os nomes dos jogadores digitados no jogo**) para os servidores da Valve e para
os seus outros computadores. O registro de erros **não** é copiado.

A Steam também informa ao jogo o seu **nome de perfil da Steam** (usado só na tela do jogo, nunca enviado a
nós), recebe as **conquistas** que você desbloqueia e mostra aos seus amigos o que você está fazendo no jogo
("Rich Presence" — por exemplo, "Correndo em Copacabana").

Esses dados são tratados pela Valve conforme a política de privacidade da Steam
(<https://store.steampowered.com/privacy_agreement/>). Você pode desligar a Steam Cloud nas propriedades do jogo
na Steam, e controlar quem vê a sua atividade nas configurações de privacidade do seu perfil Steam.

⚖️ **ADVOGADO:** confirmar o enquadramento da Valve (controladora independente dos dados da conta Steam) e se a
Steam Cloud caracteriza transferência internacional por nossa conta (art. 33 da LGPD) ou se é tratamento
próprio da Valve sob o contrato do usuário com ela.

## 4. Jogo online (relay)

Quando você cria ou entra numa sala online, o seu jogo se conecta ao nosso **servidor de retransmissão
("relay")**, que só repassa mensagens entre os computadores da sala. O relay **não entende o jogo** e **não grava
nada em disco**.

| O relay recebe | Para quê | Por quanto tempo |
|---|---|---|
| **Endereço IP** do seu computador | É assim que a internet funciona: sem ele não há conexão | Só enquanto a conexão existe; **não é registrado** pelo relay |
| **Nome que você escolheu para a partida** e o carro | Mostrar aos outros jogadores da sala | Na memória, enquanto a sala existe |
| **Comandos do jogo** (acelerar, frear, nitro, marcha, volante) a cada instante, e verificações do estado da corrida | Manter a corrida igual em todos os computadores | Na memória, só o tempo de repassar |
| **Retrato do estado da corrida** (posições, voltas, nomes) quando alguém reconecta | Devolver quem caiu à mesma corrida | Na memória, só o tempo de repassar |
| **Código da sala** e um número de identificação da conexão | Organizar as salas | Na memória, enquanto a sala existe (até 60 s depois de uma queda, para a reconexão) |

O registro de funcionamento do relay (log) guarda **só** o código da sala, horários e números de conexão — **sem
nomes e sem IP**.

**Os outros jogadores da sala** recebem o nome e o carro que você escolheu, os seus comandos e o seu carro na
corrida — é o que faz o jogo online funcionar.

🔧 **CONFERIR** quando o servidor existir: onde fica o relay (empresa de hospedagem e país) e o que **o
provedor** registra por conta própria (logs de rede, firewall, proteção contra ataques). Se o provedor for de
fora do Brasil, há transferência internacional (art. 33) — dizer para onde.

⚖️ **ADVOGADO:** base legal do relay. Proposta: execução de contrato / procedimentos a pedido do titular
(art. 7º, V — o jogador pede para jogar online) e legítimo interesse para o log mínimo de funcionamento (art.
7º, IX). Confirmar.

## 5. Telemetria anônima (opcional, desligada por padrão)

Em **Opções › Telemetria anônima** você pode autorizar o envio automático de um resumo de cada erro novo, para
nos ajudar a corrigir problemas que ninguém reporta.

- **Vem desligada.** Nada é enviado enquanto você não ligar.
- **Hoje não envia nada, mesmo ligada:** ainda não existe servidor para receber. A opção só guarda a sua
  preferência.
- **Quando houver servidor**, o resumo terá apenas: versão do jogo, tipo do erro, a mensagem técnica, as 8
  primeiras linhas do trecho do código onde aconteceu, o modo de jogo, a pista e quantas vezes o erro se repetiu.
  **Sem** nomes de jogadores, **sem** nome de usuário ou pastas do computador, **sem** identificador do
  computador ou da conta Steam. O servidor que receber verá o endereço IP da conexão, como qualquer servidor.
- Você pode desligar a qualquer momento, na mesma opção.
- **Ligar agora não vale para depois:** o jogo guarda em qual versão destes termos você ligou a opção. A versão
  que passar a enviar de verdade usa termos novos, e quem ligou antes volta a ver a opção **desligada** e decide
  de novo, já sabendo do envio.

🔧 **CONFERIR** antes de ligar o envio: onde fica o servidor da telemetria, por quanto tempo guarda os
resumos (proposta: 90 dias), se descarta o IP na chegada (proposta: sim) — e atualizar esta seção **antes** de a
versão com envio ser publicada.

⚖️ **ADVOGADO:** base legal proposta: consentimento (art. 7º, I), dado pela opção ligada pelo próprio jogador,
revogável a qualquer momento (art. 8º, § 5º). Confirmar se o resumo, sem IP guardado, é dado anonimizado (art.
12) e se o IP visto na conexão muda isso.

## 6. Relatório de erros que você nos manda

Em **Opções › Copiar relatório de erros** (e na tela "O jogo não conseguiu iniciar"), o jogo copia para a área
de transferência um texto com: a versão do jogo, a data, o sistema e o navegador/versão do Electron
(identificação técnica, sem pastas pessoais), idioma, qualidade de imagem, se a telemetria está ligada e os
erros registrados (seção 2). **Você vê o texto antes de mandar**, e só nos manda se quiser, pelo canal que
escolher (e-mail, fórum da Steam, Discord).

Usamos o relatório apenas para corrigir o erro. [Prazo de guarda: proposta de 1 ano a partir do recebimento.]

⚖️ **ADVOGADO:** prazo de guarda e base legal (proposta: consentimento — o próprio envio — ou legítimo
interesse para correção de defeitos).

## 7. O que não fazemos

- Não vendemos nem compartilhamos dados com anunciantes. O jogo não tem anúncios.
- Não pedimos cadastro, e-mail, telefone, localização, câmera ou microfone.
- Não usamos cookies de rastreamento nem ferramentas de análise de terceiros.
- Não fazemos perfil de comportamento dos jogadores.

## 8. Crianças e adolescentes

O jogo é para toda a família e não pede dados pessoais. O único texto livre é o **nome do jogador**, que no jogo
online é visto pelos outros jogadores da sala. Pais e responsáveis: orientem as crianças a usar apelidos.

⚖️ **ADVOGADO:** art. 14 da LGPD (melhor interesse; consentimento de um dos pais para dados de crianças). O
jogo online com nome livre visto por estranhos precisa de consentimento parental? Considerar nomes gerados
("Piloto 1") no online como padrão. Classificação indicativa (ClassInd) — ver EULA.

## 9. Seus direitos (LGPD, art. 18)

Você pode pedir confirmação de tratamento, acesso, correção, anonimização, eliminação, portabilidade, informação
sobre compartilhamento e revogação do consentimento. Na prática:

- **Dados no seu computador** (seção 2): você mesmo acessa, copia e apaga — estão na pasta de dados do jogo.
- **Relay** (seção 4): nada fica guardado depois que a sala acaba; não há o que acessar ou apagar.
- **Telemetria** (seção 5): desligue a opção; e, quando houver servidor, peça pelo contato abaixo.
- **Relatórios que você mandou** (seção 6): peça a exclusão pelo contato abaixo.
- **Steam / Valve** (seção 3): pelos canais da própria Steam.

Contato: [e-mail de privacidade]. Você também pode reclamar à Autoridade Nacional de Proteção de Dados (ANPD).

⚖️ **ADVOGADO:** prazo de resposta (art. 19: 15 dias para a declaração completa) e como confirmar a identidade
de quem pede sem coletar dado novo.

## 10. Segurança

- O jogo para computador roda com as proteções do Electron ligadas (página isolada do sistema, sem acesso direto
  a arquivos); só grava arquivos na própria pasta de dados, com nomes validados.
- Tudo o que chega pela rede no jogo online é validado antes de ser usado.
- 🔧 **CONFERIR:** conexão com o relay por **WSS (criptografada)** em produção — o relay de testes aceita `ws://`
  sem criptografia.

## 11. Mudanças nesta política

Se mudarmos o que o jogo coleta, atualizamos esta página antes da versão que muda, e avisamos nas notas da
atualização na Steam. Mudança que dependa de consentimento (como ligar o envio da telemetria) volta a pedir a sua
autorização.

---

## Para quem mantém este texto (apagar antes de publicar)

A política descreve o código; mudou o código, muda a política **no mesmo commit**:

| Seção | Código que ela descreve |
|---|---|
| 2 | `src/game/settings.ts`, `src/game/save.ts` (`SaveData`), `src/game/errors.ts` (`ErrorEntry`, `scrubPaths`, `RING_SIZE`), `desktop/storage.cjs` (limites do log) |
| 3 | `src/game/cloudsave.ts` (`isCloudKey`), `desktop/main.cjs` (Steam, Rich Presence), `desktop/README.md` (Auto-Cloud) |
| 4 | `server/relay.mjs` e `docs/ONLINE.md` (branch do online) |
| 5 | `src/game/errors.ts` (`telemetryPayload`, `telemetryEndpoint`, `TELEMETRY_TERMS` — subir junto com o envio), `Settings.telemetryConsent` |
| 6 | `src/game/errors.ts` (`formatReport`), `src/errors/options.ts`, `src/errors/fatal.ts` |

Uma versão em inglês (Privacy Policy) é necessária para a página da Steam: traduzir **depois** da revisão do
advogado, para não traduzir duas vezes.
