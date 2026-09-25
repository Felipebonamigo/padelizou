# Justiça na rede — o online é justo sob latência?

> Tarefa O3E da onda 3, 25/09/2026. CRONOGRAMA, M2: *"Teste com latência simulada de 80, 150 e 250 ms — o jogo tem que
> ser justo a 150."* Aqui só se **mede**: nada do netcode mudou (os arquivos de `Rede/` estão em revisão em paralelo).
> As tabelas saem de `ferramentas/JusticaNaRede` — determinística, as mesmas sementes dão as mesmas tabelas — e a régua
> com que o cliente vê a partida é `Padel.Core/Rede/VisaoParaOHumano.cs`, travada por `Padel.Core.Tests/JusticaNaRedeTests.cs`.
> Tudo foi medido **duas vezes**, com as sementes 1000 e 5000 (outras partidas, outra rede): conclusão aqui é o que as
> duas dizem. Faixas como "59–64 %" juntam as duas sementes (e os dois perfis, quando o texto não separa).

```bash
dotnet run -c Release --project ferramentas/JusticaNaRede                    # sementes 1000, 1001, …: as tabelas daqui
dotnet run -c Release --project ferramentas/JusticaNaRede -- --semente 5000  # a réplica, no fim do documento
dotnet run -c Release --project ferramentas/JusticaNaRede -- --pontos 60     # rascunho: ~30 s, ruído ±12 pp
```

300 pontos por célula: ~2 min em 4 núcleos, cada semente.

## Resposta curta

- **Não é justo hoje — nem a 0 ms.** O golpe do cliente sai atrasado, como se suspeitava, e sai atrasado quase sempre.
  A 150 ms, **86–90 %** dos golpes do cliente (Intermediário) começam com a bola já dentro do alcance — contato no
  próprio tick do aperto, Δt = −120 ms —, contra 12–15 % no local e no host. Golpes bons: **2–3 %** contra **40–44 %**.
  Bolas que passam sem golpe: **15–16 %** contra **5–8 %**. Avançado: 89 %, 1–3 % e 13–14 %, contra 9–12 %, 50–54 % e
  3–8 %.
- **O host não sente a rede.** Com gente do outro lado — frente a frente, o cliente a 150 ms com 2 % de perda —, o
  humano do host joga, bit a bit, como jogaria no local contra as mesmas entradas do cliente: a entrada dele não atrasa
  e a rede só chega à Partida dele pelo que o cliente faz (teste
  `O_humano_no_host_joga_como_no_local_contra_as_mesmas_entradas_do_cliente_a_150_ms`). Nas tabelas, o golpe do host no
  frente a frente fica no nível do local em toda latência (Δt médio −13 a −27 ms; golpes bons 38–45 % no Intermediário e
  47–55 % no Avançado, contra 40–44 % e 50–51 % no local). As linhas "Host" da tabela principal repetem a "Local (vaga 0)"
  **por construção** — o cliente só assiste, a vaga dele é da IA — e só conferem que a entrada do host não atrasa.
- **A maior parte do atraso não é o ping: é a interpolação de 100 ms.** A 0 ms o cliente já aperta ~100 ms tarde
  (Δt médio −84 a −90 ms no Intermediário, −99 ms no Avançado; golpes bons 8–10 % e 4–5 %). O aperto chega ao host
  **ida e volta + atraso de interpolação** (+ 1 a 3 ticks de passo e fila) depois do instante em que a bola estava onde
  o cliente a viu, e o balanço só dá 120 ms de antecedência: a 0 ms sobram ~12 ms; de 80 ms pra cima, nada.
- **Frente a frente, com o mesmo perfil dos dois lados, o host leva 59–64 % dos pontos a 150 ms** (as duas sementes, os
  dois perfis, com e sem perda), 67–71 % a 250 ms e 57–60 % já a 0 ms sem perda. Contra a IA Média o placar do cliente
  cai menos (−3 a −12 pp a 150 ms; −15 a −21 pp a 250), porque golpe ruim ainda devolve a bola e a IA erra sozinha: é o
  frente a frente que mostra o desequilíbrio.
- **Recomendação: saída (ii)** — a bola do lado do cliente simulada localmente até o tick em que a entrada dele chega ao
  host (a D2 original). Emulada, devolve o golpe do cliente ao nível do local em todas as latências, nas duas sementes
  (golpes bons 41–62 %, contra 40–54 % no local), sem mudar protocolo nem host. **O placar do frente a frente ela não
  garante que feche:** a 150 ms o host fica em 52–59 % (≈ 55–56 % juntando as duas sementes, no limite do ruído) e a
  250 ms sobra vantagem nas duas (59–63 %). A saída (iv) — o host ler com o atraso do cliente —, emulada por cima da
  (ii), **não mostrou efeito acima do ruído** (a 150 ms, 51–56 %; a diferença pra (ii) sozinha troca de sinal entre as
  sementes): não é recomendada com estes números.

## Como se mede

**Quem joga.** O `HumanoSimulado` (o mesmo do `--bot` do Godot e de `ferramentas/Calibracao`), perfis Intermediário e
Avançado, com um parceiro IA (perfil Parceiro) contra a dupla de IA Média. As mesmas sementes em todo arranjo (partidas
1000, 1001, …; rede 7 × semente + 2026; a réplica, 5000, 5001, …), partidas inteiras de 1 set até passar de 300 pontos
por célula (dá 300–379).

**A montagem.** A partida é a que o host monta no 1x1 online: humanos nas vagas 0 e 2. A vaga humana que não é medida
vai pra IA Média (como o `ServidorDaPartida` faz com quem cai) e o time do medido recebe o perfil Parceiro. O jogador
entregue à IA fica com o alcance de humano (1,35 m) porque `Jogador.Alcance` só se define no construtor — a mesma
limitação do `ConferirSilencio`, e **igual no local e no online**. Por isso há duas linhas locais, a vaga 0 (referência
do host) e a vaga 2 (referência do cliente).

**Os arranjos.**
- *Local*: o humano na `Partida`, vendo `EstadoVisivel.De(partida)`, como na calibração.
- *Host (vaga 0)*: `ServidorDaPartida` com um `ClienteDaPartida` na vaga 2, que só assiste (a vaga dele vai pra IA); o
  humano do host vê a `Partida` como a `SessaoHost` do Godot. Como nada que vem da rede entra na `Partida` do host
  (o `ServidorDaPartida` só aplica entrada remota em vaga humana), a linha repete a "Local (vaga 0)" **por construção**:
  ela só confere que a entrada do próprio host não atrasa. Se o host sente a rede com gente do outro lado, quem responde
  é o frente a frente e o teste 4.
- *Cliente (vaga 2)*: o humano do cliente vê **só** o quadro do `ClienteDaPartida.ParaDesenhar`, convertido pela
  `VisaoParaOHumano`; a vaga do host vai pra IA Média.
- *Frente a frente*: humano no host (vaga 0) contra humano no cliente (vaga 2), mesmo perfil, os dois com parceiro IA
  Parceiro. Mede os dois.

A cada passo a `RedeEmMemoria` anda 1/120 s, o host dá um tick, o cliente dá um tick e desenha um quadro; o humano
decide pelo quadro do passo anterior, como no Godot.

**A rede.** `RedeEmMemoria`, latência de ida = metade da ida e volta (0, 40, 75 e 125 ms), jitter uniforme de 10 % da
latência de ida, perda de 0 e 2 % no canal não confiável (entradas e instantâneos). "0 ms" ainda tem 1 tick (8,3 ms) de
ida e volta: a entrada do cliente é aplicada no tick seguinte do host — é a granularidade do passo.

**A régua — `VisaoParaOHumano`.** Monta o `EstadoVisivel` a partir do quadro do cliente e de nada mais: a bola do quadro
(interpolada, 100 ms no passado), o próprio jogador predito, os outros interpolados. Quem bateu por último, o tipo do
golpe e se a bola já quicou o instantâneo não carrega — saem dos eventos numerados, como o som. Um evento mais novo que
o instantâneo de onde o quadro saiu corrige estado e sacador (sem isso o humano do cliente passaria até 33 ms achando que
o saque não saiu com a bola já no ar: defeito da régua, não da rede). Os testes provam que ela não entorta:

1. ida e volta 0 e interpolação 0: no tick de cada instantâneo, o `EstadoVisivel` do cliente é o do host — bola, outros
   jogadores e o próprio jogador predito (contra o host no tick seguinte) a menos de 1 cm; velocidade dos outros a
   menos de 1 cm/s e efeito da bola a menos de 0,05 rad/s (é a quantização do instantâneo); velocidade da bola a menos
   de 5 cm/s; estado, sacador, caixa e leitura iguais; time e lado de todos iguais em todo quadro; entre instantâneos, a
   bola em voo também bate, efeito incluído. Velocidade e efeito da bola ficam de fora no tick em que ela toca chão,
   parede ou rede: o cliente desenha ~1e-5 tick fora do inteiro, e um quique nessa fração troca a velocidade de uma vez.
   A velocidade do próprio jogador, predita a partir do instantâneo quantizado, fica a menos de 5 cm/s: o
   `Jogador.Mover` freia (14 m/s²) ou acelera (9 m/s²) conforme |V| passa ou não de |desejado|, e ao inverter a direção
   na mesma rapidez a velocidade arredondada cai do outro lado do limiar — (14 − 9)/120 = 4,2 cm/s num passo, mais a
   quantização. A menos de 1 cm do limite da quadra ela não se compara: o `Jogador.Limitar` zera a componente, e o
   milímetro do arredondamento põe o predito na parede um passo antes ou depois do host (a posição segue conferida);
2. interpolação de 100 ms: a bola vista é a do quadro, a do host 12 ticks atrás (a menos de 1 cm; o efeito dentro da
   quantização + 0,5 % — depois de um quique o efeito sai da velocidade no impacto, e o erro de quantização dela vira
   até 0,1 % do efeito), a mais de 0,5 m em média da do host agora — fora do trecho, desde o instantâneo anterior, em
   que a bola tocou chão, parede ou rede: simulada a partir do marco quantizado, a `Bola` acha o contato um subpasso
   (1/240 s) antes ou depois do host e anda meio tick com a velocidade trocada (até 6,4 cm em 40 sementes); estado,
   time, lado e leitura são os do tick desenhado em todo tick; os outros jogadores, interpolados, a menos de 1 cm entre
   dois instantâneos do rally — menos no intervalo em que o `Jogador.Limitar` parou um deles no limite da quadra (a
   reta não segue a quina: até v/120, 2,45 cm em 40 sementes; menos de 1 % das comparações); o próprio jogador —
   posição e velocidade, como no 1 — é o de agora;
3. a 0 ms e sem interpolação, o humano do cliente joga como o local: Δt médio, golpes bons e pontos dentro de 3 erros
   padrão da diferença (a conta está no teste: 29 ms, 15 pp e 15 pp com pelo menos 200 golpes e 200 pontos de cada
   lado). Cada lado joga partidas até ter os 200 golpes e os 200 pontos (3 a 5 partidas), em vez de um número fixo
   delas; conferido com sete faixas de sementes (1000 a 7000). Com a interpolação de 100 ms o mesmo teste reprova —
   `Δt médio: local -19.0 ms, cliente -80.8 ms` —, o que o faz régua, e não formalidade;
4. frente a frente a 150 ms com 2 % de perda, a vaga do cliente humana: o teste grava a entrada que o host aplicou à
   vaga do cliente em cada tick e joga de novo, numa `Partida` local, o mesmo humano do host contra essa gravação — dá a
   mesma partida, bit a bit. A rede muda o jogo (em 120 s, a mesma mesa sem latência está 1-1 30-15; a 150 ms, 2-1), mas
   só pelo que o cliente faz.

Cada um foi visto falhar com o defeito que devia pegar: sem a correção de estado pelos eventos, o 2 reprova
(`Expected: Rally`); sem o quique na leitura, o 1 e o 2 reprovam (`host leu (0, Saque, quicou True), cliente (0, Saque,
quicou False)`); com a velocidade dos jogadores zerada, o 1 e o 2 (`a velocidade dos outros jogadores errou 5.800 m/s`);
com os times trocados, o 1 e o 2 (`tick 4, jogador 0: host (time 0, lado 1), cliente (time 1, lado -1)`); com o efeito
da bola zerado, o 1 e o 2 (`o efeito da bola vista errou 378.539 rad/s`); o 3 com 4 partidas fixas reprovava em outras
faixas de sementes pelo tamanho da amostra (`golpes: local 207, cliente 167`), e passa nelas com o piso; o 4 reprova
com a entrada do host um tick atrasada, com o host adiantando um tick o balanço do cliente ao receber o aperto (um
arremedo da saída i) e, de controle, com a gravação do cliente trocada por entrada vazia. Com as tolerâncias e os
recortes do limite da quadra e do contato da bola (que a IA da T3, com outras trajetórias, expôs: `a velocidade do
jogador predito errou 0.039 m/s`, `os outros jogadores ficaram a 1.60 cm`), o 1 e o 2 seguem reprovando com a predição
um passo curta (`ficou a 5.28 cm`), sem a conversão de referencial (1,32 cm), com a velocidade de partida zerada, com a
aceleração do predito trocada pela frenagem (`errou 0.172 m/s`); o 2 com a interpolação dos outros um tick adiantada
(`4.89 cm`) e com a bola do quadro um tick atrasada (`28.29 cm`); e o 2 reprova se um recorte engolir amostras demais. Em
40 sementes (1 a 40), nenhuma asserção de acerto reprova; o que cai é o piso de amostra do 1 (menos de 8 pontos em 90 s
em 6 delas). A correção do *sacador* pelos
eventos não tem teste que a pegue — tirá-la passa nos quatro: nas partidas medidas o placar do instantâneo já traz o
sacador do ponto seguinte quando o saque é preparado. Fica como defesa.

**As métricas**, tiradas da `Partida` do host — a verdade — pro jogador medido:
- **Δt** = tempo no balanço no contato − 0,12 s. Negativo: apertou tarde (bola em cima do corpo); positivo: cedo. Por
  construção Δt ≥ −120 ms — apertar com a bola já no alcance dá contato no próprio tick do aperto. Por isso o **p90 de
  |Δt|** satura em 120 ms em quase toda célula (até no local mais de 10 % dos golpes caem nesse chão) e a coluna que
  discrimina é **"bola já no alcance ao apertar"**, a fração dos golpes nesse chão.
- **Golpe bom**: erro do golpe (`Partida.UltimoErroDoHumano` = 0,6 × dificuldade do corpo + |Δt| / 0,15) menor que 0,5.
- **Bolas que passaram**: de cada bola do adversário (do golpe dele até o golpe seguinte ou o fim do ponto) que em algum
  tick ficou batível pro medido — no alcance confortável, ou no esticado indo embora, que é o critério da `Partida` pra
  quem está balançando, com o árbitro deixando bater —, tirando as que o parceiro bateu: a fração que virou ponto contra
  sem golpe do medido.
- **Pontos ganhos** pelo time do medido; **golpes por ponto** do medido (sem o saque).

**Ruído.** Com ~320 pontos por célula, "pontos ganhos" tem ±5,5 pp de margem (95 %, p ≈ 0,5: 1,96 × √(0,25 / 320)) e a
diferença entre duas células, ±7,7 pp; golpes bons (~300 golpes), ±5,6 pp; Δt médio (σ ≈ 60 ms), ±7 ms. Pontos da
mesma partida não são de todo independentes (saque, placar), então a margem de verdade é um pouco maior. E estas tabelas
têm dezenas de comparações: a 95 %, uma em cada vinte passa da margem por acaso. Exemplo: frente a frente a 0 ms,
Intermediário, semente 1000 — 57 % sem perda, 49 % com 2 % de perda: 8 pp, acima da margem. Com a semente 5000, 59 % e
55 %; no Avançado, 60 % e 59 %, 57 % e 58 %. E vai contra o mecanismo (perda atrapalha o cliente, e aqui é o host que
perde pontos) e não aparece no Avançado: fica tratado como sorteio. Por isso a regra deste documento — **conclusão é o que se repete nas duas
sementes**; diferença que muda de sinal entre elas, ou que fica dentro da margem nas duas, não é conclusão.
"Juntando as duas sementes" é a média ponderada pelos pontos de cada célula (≈ 650–700 pontos: margem de ±3,8 pp).

## Tabelas

Saída de `dotnet run -c Release --project ferramentas/JusticaNaRede` (semente 1000, 300 pontos por célula), só com os
títulos rebaixados; a réplica com a semente 5000 está no fim, em [Réplica](#réplica-semente-5000). As linhas "Host"
repetem a "Local (vaga 0)" por construção (o cliente só assiste). As linhas "(≈ saída i)", "(≈ saída ii)" e "(iv)" são
**emulações** feitas na ferramenta, sem mexer no netcode — ver a proposta.

### Intermediário com parceiro IA contra a IA Média

| Arranjo | Ida e volta | Perda | Δt médio | Δt mediano | σ(Δt) | p90 de \|Δt\| | Bola já no alcance ao apertar | Golpes bons | Bolas que passaram | Pontos ganhos | Golpes/ponto | Pontos |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Local (vaga 0) | — | — | -17 ms | -12 ms | 69 ms | 120 ms | 15 % | 40 % | 6 % | 55 % | 0,92 | 319 |
| Local (vaga 2) | — | — | -16 ms | -20 ms | 63 ms | 120 ms | 12 % | 42 % | 5 % | 57 % | 1,00 | 339 |
| Host (vaga 0) | 0 ms | 0 % | -17 ms | -12 ms | 69 ms | 120 ms | 15 % | 40 % | 6 % | 55 % | 0,92 | 319 |
| Host (vaga 0) | 0 ms | 2 % | -17 ms | -12 ms | 69 ms | 120 ms | 15 % | 40 % | 6 % | 55 % | 0,92 | 319 |
| Host (vaga 0) | 80 ms | 0 % | -17 ms | -12 ms | 69 ms | 120 ms | 15 % | 40 % | 6 % | 55 % | 0,92 | 319 |
| Host (vaga 0) | 80 ms | 2 % | -17 ms | -12 ms | 69 ms | 120 ms | 15 % | 40 % | 6 % | 55 % | 0,92 | 319 |
| Host (vaga 0) | 150 ms | 0 % | -17 ms | -12 ms | 69 ms | 120 ms | 15 % | 40 % | 6 % | 55 % | 0,92 | 319 |
| Host (vaga 0) | 150 ms | 2 % | -17 ms | -12 ms | 69 ms | 120 ms | 15 % | 40 % | 6 % | 55 % | 0,92 | 319 |
| Host (vaga 0) | 250 ms | 0 % | -17 ms | -12 ms | 69 ms | 120 ms | 15 % | 40 % | 6 % | 55 % | 0,92 | 319 |
| Host (vaga 0) | 250 ms | 2 % | -17 ms | -12 ms | 69 ms | 120 ms | 15 % | 40 % | 6 % | 55 % | 0,92 | 319 |
| Cliente (vaga 2) | 0 ms | 0 % | -84 ms | -112 ms | 55 ms | 120 ms | 44 % | 8 % | 9 % | 50 % | 0,83 | 344 |
| Cliente (vaga 2) | 0 ms | 2 % | -88 ms | -120 ms | 55 ms | 120 ms | 51 % | 7 % | 8 % | 46 % | 0,91 | 309 |
| Cliente (vaga 2) | 80 ms | 0 % | -104 ms | -120 ms | 47 ms | 120 ms | 81 % | 4 % | 10 % | 49 % | 0,84 | 328 |
| Cliente (vaga 2) | 80 ms | 2 % | -103 ms | -120 ms | 52 ms | 120 ms | 82 % | 3 % | 9 % | 50 % | 0,81 | 317 |
| Cliente (vaga 2) | 150 ms | 0 % | -109 ms | -120 ms | 44 ms | 120 ms | 90 % | 2 % | 16 % | 49 % | 0,67 | 323 |
| Cliente (vaga 2) | 150 ms | 2 % | -98 ms | -120 ms | 60 ms | 120 ms | 84 % | 3 % | 16 % | 44 % | 0,82 | 350 |
| Cliente (vaga 2) | 250 ms | 0 % | -98 ms | -120 ms | 64 ms | 120 ms | 87 % | 2 % | 24 % | 42 % | 0,57 | 316 |
| Cliente (vaga 2) | 250 ms | 2 % | -88 ms | -120 ms | 72 ms | 120 ms | 80 % | 6 % | 22 % | 38 % | 0,62 | 318 |

#### Intermediário: frente a frente (humano no host x humano no cliente, parceiros IA Parceiro)

| Ida e volta | Perda | Cliente com | Pontos do host | Δt médio host / cliente | Golpes bons host / cliente | Passaram host / cliente | Golpes/ponto host / cliente | Pontos |
|---|---|---|---|---|---|---|---|---|
| 0 ms | 0 % | hoje | 57 % | -13 ms / -93 ms | 42 % / 7 % | 5 % / 7 % | 1,03 / 0,99 | 319 |
| 0 ms | 2 % | hoje | 49 % | -16 ms / -91 ms | 40 % / 8 % | 5 % / 9 % | 0,94 / 0,83 | 345 |
| 0 ms | 0 % | saída (ii) emulada | 47 % | -18 ms / -28 ms | 43 % / 39 % | 4 % / 8 % | 1,04 / 0,93 | 303 |
| 0 ms | 0 % | (ii) + host lendo com o atraso do cliente (iv) | 47 % | -18 ms / -28 ms | 43 % / 39 % | 4 % / 8 % | 1,04 / 0,93 | 303 |
| 80 ms | 0 % | hoje | 59 % | -16 ms / -99 ms | 43 % / 2 % | 4 % / 12 % | 0,91 / 0,86 | 358 |
| 80 ms | 2 % | hoje | 56 % | -19 ms / -105 ms | 44 % / 4 % | 6 % / 14 % | 0,93 / 0,82 | 351 |
| 80 ms | 0 % | saída (ii) emulada | 45 % | -16 ms / -28 ms | 40 % / 40 % | 6 % / 7 % | 1,03 / 1,03 | 313 |
| 80 ms | 0 % | (ii) + host lendo com o atraso do cliente (iv) | 50 % | -21 ms / -28 ms | 38 % / 42 % | 6 % / 6 % | 1,03 / 1,03 | 319 |
| 150 ms | 0 % | hoje | 60 % | -16 ms / -92 ms | 41 % / 1 % | 5 % / 18 % | 0,89 / 0,83 | 338 |
| 150 ms | 2 % | hoje | 64 % | -19 ms / -90 ms | 40 % / 2 % | 6 % / 23 % | 0,76 / 0,72 | 314 |
| 150 ms | 0 % | saída (ii) emulada | 58 % | -17 ms / -31 ms | 42 % / 41 % | 7 % / 10 % | 0,91 / 0,86 | 365 |
| 150 ms | 0 % | (ii) + host lendo com o atraso do cliente (iv) | 51 % | -25 ms / -23 ms | 41 % / 50 % | 8 % / 12 % | 0,84 / 0,84 | 340 |
| 250 ms | 0 % | hoje | 67 % | -18 ms / -73 ms | 38 % / 7 % | 6 % / 32 % | 0,69 / 0,55 | 310 |
| 250 ms | 2 % | hoje | 71 % | -21 ms / -71 ms | 41 % / 5 % | 4 % / 29 % | 0,71 / 0,65 | 325 |
| 250 ms | 0 % | saída (ii) emulada | 59 % | -15 ms / -25 ms | 42 % / 48 % | 5 % / 16 % | 0,84 / 0,76 | 313 |
| 250 ms | 0 % | (ii) + host lendo com o atraso do cliente (iv) | 56 % | -24 ms / -24 ms | 44 % / 42 % | 7 % / 16 % | 0,85 / 0,85 | 332 |

#### Intermediário: o que cada saída compraria pro cliente (sem perda)

| Ida e volta | Cliente com | Δt médio | Δt mediano | p90 de \|Δt\| | Bola já no alcance ao apertar | Golpes bons | Bolas que passaram | Pontos ganhos | Golpes/ponto |
|---|---|---|---|---|---|---|---|---|---|
| 0 ms | interpolação de 100 ms (hoje) | -84 ms | -112 ms | 120 ms | 44 % | 8 % | 9 % | 50 % | 0,83 |
| 0 ms | interpolação de 50 ms | -58 ms | -70 ms | 120 ms | 23 % | 22 % | 7 % | 51 % | 0,84 |
| 0 ms | interpolação de 33 ms | -44 ms | -53 ms | 120 ms | 18 % | 28 % | 8 % | 56 % | 0,99 |
| 0 ms | interpolação de 0 ms | -19 ms | -20 ms | 120 ms | 11 % | 43 % | 7 % | 57 % | 0,90 |
| 0 ms | bola adiantada, leitura a 100 ms (≈ saída i) | -22 ms | -20 ms | 120 ms | 15 % | 44 % | 8 % | 56 % | 0,84 |
| 0 ms | bola adiantada, leitura sem atraso (≈ saída ii) | -12 ms | -3 ms | 120 ms | 10 % | 49 % | 10 % | 52 % | 0,92 |
| 80 ms | interpolação de 100 ms (hoje) | -104 ms | -120 ms | 120 ms | 81 % | 4 % | 10 % | 49 % | 0,84 |
| 80 ms | interpolação de 50 ms | -101 ms | -120 ms | 120 ms | 65 % | 4 % | 10 % | 51 % | 0,73 |
| 80 ms | interpolação de 33 ms | -97 ms | -120 ms | 120 ms | 58 % | 6 % | 11 % | 53 % | 0,84 |
| 80 ms | interpolação de 0 ms | -73 ms | -87 ms | 120 ms | 28 % | 17 % | 9 % | 48 % | 0,99 |
| 80 ms | bola adiantada, leitura a 100 ms (≈ saída i) | -19 ms | -12 ms | 120 ms | 17 % | 44 % | 9 % | 54 % | 0,80 |
| 80 ms | bola adiantada, leitura sem atraso (≈ saída ii) | -17 ms | -12 ms | 120 ms | 14 % | 41 % | 8 % | 60 % | 0,86 |
| 150 ms | interpolação de 100 ms (hoje) | -109 ms | -120 ms | 120 ms | 90 % | 2 % | 16 % | 49 % | 0,67 |
| 150 ms | interpolação de 50 ms | -102 ms | -120 ms | 120 ms | 84 % | 1 % | 10 % | 46 % | 0,82 |
| 150 ms | interpolação de 33 ms | -104 ms | -120 ms | 120 ms | 83 % | 2 % | 10 % | 49 % | 0,86 |
| 150 ms | interpolação de 0 ms | -103 ms | -120 ms | 120 ms | 75 % | 2 % | 10 % | 53 % | 0,73 |
| 150 ms | bola adiantada, leitura a 100 ms (≈ saída i) | -21 ms | -12 ms | 120 ms | 19 % | 41 % | 10 % | 47 % | 0,78 |
| 150 ms | bola adiantada, leitura sem atraso (≈ saída ii) | -28 ms | -20 ms | 120 ms | 21 % | 41 % | 10 % | 51 % | 0,87 |
| 250 ms | interpolação de 100 ms (hoje) | -98 ms | -120 ms | 120 ms | 87 % | 2 % | 24 % | 42 % | 0,57 |
| 250 ms | interpolação de 50 ms | -102 ms | -120 ms | 120 ms | 87 % | 3 % | 23 % | 44 % | 0,57 |
| 250 ms | interpolação de 33 ms | -101 ms | -120 ms | 120 ms | 87 % | 3 % | 19 % | 44 % | 0,66 |
| 250 ms | interpolação de 0 ms | -104 ms | -120 ms | 120 ms | 88 % | 1 % | 18 % | 46 % | 0,66 |
| 250 ms | bola adiantada, leitura a 100 ms (≈ saída i) | -13 ms | -3 ms | 120 ms | 20 % | 45 % | 18 % | 40 % | 0,64 |
| 250 ms | bola adiantada, leitura sem atraso (≈ saída ii) | -24 ms | -12 ms | 120 ms | 25 % | 44 % | 13 % | 48 % | 0,71 |

### Avançado com parceiro IA contra a IA Média

| Arranjo | Ida e volta | Perda | Δt médio | Δt mediano | σ(Δt) | p90 de \|Δt\| | Bola já no alcance ao apertar | Golpes bons | Bolas que passaram | Pontos ganhos | Golpes/ponto | Pontos |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Local (vaga 0) | — | — | -19 ms | -20 ms | 59 ms | 120 ms | 12 % | 50 % | 5 % | 55 % | 1,10 | 330 |
| Local (vaga 2) | — | — | -20 ms | -20 ms | 56 ms | 120 ms | 9 % | 51 % | 3 % | 61 % | 1,07 | 306 |
| Host (vaga 0) | 0 ms | 0 % | -19 ms | -20 ms | 59 ms | 120 ms | 12 % | 50 % | 5 % | 55 % | 1,10 | 330 |
| Host (vaga 0) | 0 ms | 2 % | -19 ms | -20 ms | 59 ms | 120 ms | 12 % | 50 % | 5 % | 55 % | 1,10 | 330 |
| Host (vaga 0) | 80 ms | 0 % | -19 ms | -20 ms | 59 ms | 120 ms | 12 % | 50 % | 5 % | 55 % | 1,10 | 330 |
| Host (vaga 0) | 80 ms | 2 % | -19 ms | -20 ms | 59 ms | 120 ms | 12 % | 50 % | 5 % | 55 % | 1,10 | 330 |
| Host (vaga 0) | 150 ms | 0 % | -19 ms | -20 ms | 59 ms | 120 ms | 12 % | 50 % | 5 % | 55 % | 1,10 | 330 |
| Host (vaga 0) | 150 ms | 2 % | -19 ms | -20 ms | 59 ms | 120 ms | 12 % | 50 % | 5 % | 55 % | 1,10 | 330 |
| Host (vaga 0) | 250 ms | 0 % | -19 ms | -20 ms | 59 ms | 120 ms | 12 % | 50 % | 5 % | 55 % | 1,10 | 330 |
| Host (vaga 0) | 250 ms | 2 % | -19 ms | -20 ms | 59 ms | 120 ms | 12 % | 50 % | 5 % | 55 % | 1,10 | 330 |
| Cliente (vaga 2) | 0 ms | 0 % | -99 ms | -120 ms | 34 ms | 120 ms | 51 % | 4 % | 10 % | 52 % | 0,84 | 317 |
| Cliente (vaga 2) | 0 ms | 2 % | -103 ms | -120 ms | 33 ms | 120 ms | 60 % | 4 % | 10 % | 50 % | 0,87 | 310 |
| Cliente (vaga 2) | 80 ms | 0 % | -108 ms | -120 ms | 46 ms | 120 ms | 89 % | 2 % | 11 % | 48 % | 0,92 | 334 |
| Cliente (vaga 2) | 80 ms | 2 % | -114 ms | -120 ms | 34 ms | 120 ms | 94 % | 1 % | 10 % | 57 % | 0,88 | 330 |
| Cliente (vaga 2) | 150 ms | 0 % | -101 ms | -120 ms | 61 ms | 120 ms | 89 % | 3 % | 14 % | 49 % | 0,78 | 349 |
| Cliente (vaga 2) | 150 ms | 2 % | -106 ms | -120 ms | 52 ms | 120 ms | 92 % | 1 % | 15 % | 47 % | 0,72 | 306 |
| Cliente (vaga 2) | 250 ms | 0 % | -96 ms | -120 ms | 62 ms | 120 ms | 84 % | 4 % | 23 % | 40 % | 0,61 | 328 |
| Cliente (vaga 2) | 250 ms | 2 % | -99 ms | -120 ms | 60 ms | 120 ms | 87 % | 3 % | 27 % | 39 % | 0,50 | 316 |

#### Avançado: frente a frente (humano no host x humano no cliente, parceiros IA Parceiro)

| Ida e volta | Perda | Cliente com | Pontos do host | Δt médio host / cliente | Golpes bons host / cliente | Passaram host / cliente | Golpes/ponto host / cliente | Pontos |
|---|---|---|---|---|---|---|---|---|
| 0 ms | 0 % | hoje | 60 % | -25 ms / -103 ms | 51 % / 2 % | 2 % / 6 % | 0,99 / 1,04 | 358 |
| 0 ms | 2 % | hoje | 59 % | -20 ms / -104 ms | 54 % / 4 % | 3 % / 6 % | 0,93 / 0,90 | 344 |
| 0 ms | 0 % | saída (ii) emulada | 46 % | -24 ms / -25 ms | 45 % / 51 % | 3 % / 4 % | 1,18 / 1,21 | 351 |
| 0 ms | 0 % | (ii) + host lendo com o atraso do cliente (iv) | 46 % | -24 ms / -25 ms | 45 % / 51 % | 3 % / 4 % | 1,18 / 1,21 | 351 |
| 80 ms | 0 % | hoje | 55 % | -19 ms / -97 ms | 55 % / 2 % | 4 % / 8 % | 1,06 / 1,03 | 351 |
| 80 ms | 2 % | hoje | 55 % | -21 ms / -98 ms | 55 % / 1 % | 4 % / 12 % | 0,98 / 0,90 | 334 |
| 80 ms | 0 % | saída (ii) emulada | 56 % | -21 ms / -27 ms | 51 % / 49 % | 4 % / 4 % | 1,12 / 1,15 | 330 |
| 80 ms | 0 % | (ii) + host lendo com o atraso do cliente (iv) | 53 % | -23 ms / -28 ms | 49 % / 48 % | 4 % / 3 % | 1,24 / 1,20 | 322 |
| 150 ms | 0 % | hoje | 59 % | -27 ms / -88 ms | 47 % / 5 % | 4 % / 14 % | 1,00 / 0,97 | 318 |
| 150 ms | 2 % | hoje | 61 % | -20 ms / -81 ms | 49 % / 5 % | 5 % / 14 % | 0,95 / 0,93 | 333 |
| 150 ms | 0 % | saída (ii) emulada | 59 % | -12 ms / -36 ms | 49 % / 47 % | 3 % / 8 % | 1,06 / 1,08 | 331 |
| 150 ms | 0 % | (ii) + host lendo com o atraso do cliente (iv) | 53 % | -25 ms / -34 ms | 47 % / 46 % | 4 % / 7 % | 1,10 / 1,11 | 318 |
| 250 ms | 0 % | hoje | 67 % | -21 ms / -76 ms | 50 % / 7 % | 3 % / 27 % | 0,77 / 0,66 | 315 |
| 250 ms | 2 % | hoje | 70 % | -25 ms / -81 ms | 54 % / 5 % | 3 % / 28 % | 0,59 / 0,61 | 323 |
| 250 ms | 0 % | saída (ii) emulada | 63 % | -17 ms / -31 ms | 54 % / 49 % | 3 % / 9 % | 1,16 / 1,17 | 308 |
| 250 ms | 0 % | (ii) + host lendo com o atraso do cliente (iv) | 58 % | -26 ms / -28 ms | 52 % / 49 % | 7 % / 8 % | 1,02 / 1,02 | 351 |

#### Avançado: o que cada saída compraria pro cliente (sem perda)

| Ida e volta | Cliente com | Δt médio | Δt mediano | p90 de \|Δt\| | Bola já no alcance ao apertar | Golpes bons | Bolas que passaram | Pontos ganhos | Golpes/ponto |
|---|---|---|---|---|---|---|---|---|---|
| 0 ms | interpolação de 100 ms (hoje) | -99 ms | -120 ms | 120 ms | 51 % | 4 % | 10 % | 52 % | 0,84 |
| 0 ms | interpolação de 50 ms | -66 ms | -70 ms | 120 ms | 21 % | 21 % | 5 % | 60 % | 1,00 |
| 0 ms | interpolação de 33 ms | -55 ms | -62 ms | 120 ms | 14 % | 28 % | 7 % | 56 % | 1,06 |
| 0 ms | interpolação de 0 ms | -28 ms | -28 ms | 120 ms | 13 % | 42 % | 7 % | 56 % | 1,04 |
| 0 ms | bola adiantada, leitura a 100 ms (≈ saída i) | -23 ms | -28 ms | 112 ms | 10 % | 52 % | 9 % | 54 % | 0,87 |
| 0 ms | bola adiantada, leitura sem atraso (≈ saída ii) | -16 ms | -12 ms | 120 ms | 10 % | 45 % | 6 % | 53 % | 1,11 |
| 80 ms | interpolação de 100 ms (hoje) | -108 ms | -120 ms | 120 ms | 89 % | 2 % | 11 % | 48 % | 0,92 |
| 80 ms | interpolação de 50 ms | -112 ms | -120 ms | 120 ms | 80 % | 1 % | 8 % | 47 % | 0,98 |
| 80 ms | interpolação de 33 ms | -103 ms | -120 ms | 120 ms | 61 % | 2 % | 7 % | 53 % | 0,84 |
| 80 ms | interpolação de 0 ms | -92 ms | -103 ms | 120 ms | 38 % | 6 % | 6 % | 48 % | 1,02 |
| 80 ms | bola adiantada, leitura a 100 ms (≈ saída i) | -18 ms | -20 ms | 120 ms | 10 % | 52 % | 8 % | 50 % | 0,75 |
| 80 ms | bola adiantada, leitura sem atraso (≈ saída ii) | -19 ms | -20 ms | 120 ms | 10 % | 53 % | 6 % | 54 % | 0,93 |
| 150 ms | interpolação de 100 ms (hoje) | -101 ms | -120 ms | 120 ms | 89 % | 3 % | 14 % | 49 % | 0,78 |
| 150 ms | interpolação de 50 ms | -103 ms | -120 ms | 120 ms | 89 % | 1 % | 10 % | 50 % | 0,81 |
| 150 ms | interpolação de 33 ms | -111 ms | -120 ms | 120 ms | 93 % | 1 % | 11 % | 54 % | 0,83 |
| 150 ms | interpolação de 0 ms | -111 ms | -120 ms | 120 ms | 89 % | 1 % | 9 % | 50 % | 0,83 |
| 150 ms | bola adiantada, leitura a 100 ms (≈ saída i) | -22 ms | -12 ms | 120 ms | 17 % | 56 % | 7 % | 53 % | 0,82 |
| 150 ms | bola adiantada, leitura sem atraso (≈ saída ii) | -35 ms | -28 ms | 120 ms | 17 % | 48 % | 7 % | 53 % | 0,89 |
| 250 ms | interpolação de 100 ms (hoje) | -96 ms | -120 ms | 120 ms | 84 % | 4 % | 23 % | 40 % | 0,61 |
| 250 ms | interpolação de 50 ms | -104 ms | -120 ms | 120 ms | 90 % | 2 % | 17 % | 44 % | 0,66 |
| 250 ms | interpolação de 33 ms | -107 ms | -120 ms | 120 ms | 89 % | 3 % | 16 % | 43 % | 0,65 |
| 250 ms | interpolação de 0 ms | -99 ms | -120 ms | 120 ms | 87 % | 1 % | 11 % | 45 % | 0,79 |
| 250 ms | bola adiantada, leitura a 100 ms (≈ saída i) | -17 ms | -3 ms | 120 ms | 18 % | 56 % | 15 % | 46 % | 0,66 |
| 250 ms | bola adiantada, leitura sem atraso (≈ saída ii) | -29 ms | -20 ms | 120 ms | 21 % | 54 % | 10 % | 49 % | 0,79 |

## Leitura

### O cliente a 150 ms, contra o local e o host

Sem perda, recortado das tabelas — semente 1000 · semente 5000:

| | Local (vaga 2) | Host (vaga 0) | Cliente a 150 ms |
|---|---|---|---|
| **Intermediário** — Δt médio / mediano | −16 / −20 · −18 / −20 ms | −17 / −12 · −19 / −20 ms | −109 / −120 · −101 / −120 ms |
| bola já no alcance ao apertar | 12 % · 12 % | 15 % · 13 % | 90 % · 86 % |
| golpes bons | 42 % · 43 % | 40 % · 44 % | 2 % · 3 % |
| bolas que passaram | 5 % · 8 % | 6 % · 7 % | 16 % · 15 % |
| pontos ganhos contra a IA Média | 57 % · 56 % | 55 % · 57 % | 49 % · 53 % |
| golpes por ponto | 1,00 · 0,93 | 0,92 · 0,93 | 0,67 · 0,67 |
| **Avançado** — Δt médio / mediano | −20 / −20 · −19 / −20 ms | −19 / −20 · −20 / −20 ms | −101 / −120 · −102 / −120 ms |
| bola já no alcance ao apertar | 9 % · 10 % | 12 % · 10 % | 89 % · 89 % |
| golpes bons | 51 % · 54 % | 50 % · 51 % | 3 % · 1 % |
| bolas que passaram | 3 % · 8 % | 5 % · 6 % | 14 % · 13 % |
| pontos ganhos contra a IA Média | 61 % · 56 % | 55 % · 58 % | 49 % · 44 % |
| golpes por ponto | 1,07 · 0,96 | 1,10 · 1,08 | 0,78 · 0,89 |

O cliente a 150 ms acerta **1–3 % de golpes bons, contra 42–54 % no local**, com a bola já no alcance em 86–90 % dos
apertos; deixa passar **1,6 a 4,7 vezes mais bolas** (13–16 % contra 3–8 %); dá menos golpes por ponto (−7 a −33 %); e
perde **3 a 12 pp** de pontos contra a IA Média — os −12 do Avançado se repetem nas duas sementes; no Intermediário,
−8 com a 1000 (no limite da margem de ±7,7 pp) e −3 com a 5000 (dentro dela). Nesta tabela o host é o local por construção; com gente do outro lado, o golpe do
host continua o do local (frente a frente e teste 4, acima). Quanto melhor o jogador, mais ele perde — o atraso é
absoluto (~250 ms a 150 de ping) e engole qualquer precisão de perfil (desvio de 20 a 30 ms).

### Por que: a conta do atraso

O cliente desenha o tick do host `estimado − 12` (100 ms de interpolação), e o estimado já está a latência de ida atrás
do host. O aperto que ele dá agora chega ao host a latência de volta depois, mais 1 tick de passo e 0–2 de fila. Em
relação à bola que ele viu, o aperto chega **ida e volta + 100 ms + 8 a 25 ms** tarde. O balanço dá 120 ms entre o aperto
e o contato ideal:

- a 0 ms, sobram ~12 ms — Δt mediano −112 ms (Intermediário) e −120 ms (Avançado), 44–51 % dos golpes já no chão;
- a 80 ms ou mais, o aperto chega com a bola **já dentro do alcance** (80–94 % dos golpes: contato no tick do aperto, erro
  de timing 0,8, golpe ruim) **ou já fora dele** — a bola passou;
- a 250 ms o Δt médio "melhora" (−94 a −98 ms no Intermediário, sem perda) por sobrevivência: as bolas mais atrasadas
  nem viram golpe (23–26 % passaram, sem perda).

2 % de perda não muda o quadro de 80 ms pra cima (já está saturado). A 0 ms empurra mais golpes pro chão — +6 a +17 pp
de "bola já no alcance", nas quatro combinações de perfil e semente —: o pacote perdido leva o aperto no pacote
seguinte, 1 tick depois, e a 0 ms sobravam só ~12 ms. Golpes bons e placar não se separam do ruído. A redundância das
entradas (8 por pacote) e a repetição dos eventos por 0,5 s funcionam, como o `RedePartidaOnlineTests` já mostrava. Os
números batem com a medição do Godot em `docs/REDE.md` ("com +100 ms e 5 % de perda, o jogador do cliente deu 7 golpes
aplicados no host").

### O placar contra a IA esconde; o frente a frente mostra

Contra a IA Média o cliente perde a qualidade do golpe quase inteira e só 3 a 12 pp de pontos a 150 ms (15 a 21 pp a
250 ms): golpe no chão (erro ≥ 0,8) ainda devolve a bola na maioria das vezes — só erro acima de 0,95 tem chance de ir na
rede ou no vidro — e a IA Média erra sozinha. Frente a frente, com o mesmo perfil dos dois lados, o host leva **57–60 %
dos pontos a 0 ms sem perda, 54–59 % a 80, 59–64 % a 150 e 67–71 % a 250** — cada célula de 150 ms pra cima fora da
margem de ±5,5 pp, nas duas sementes. A 150 ms, o jogo **não** é justo.

### O que cada saída compraria (emulado)

- **(iii) menos interpolação** só resolve rede local: a 0 ms, interpolação 0 devolve o nível do local (golpes bons
  40–43 % no Intermediário, 42–49 % no Avançado); a 80 ms, 15–17 % / 6–7 %; a 150 ms, 2–3 % / 1 %.
- **Bola adiantada** (a bola que o humano do cliente vê levada pela física até o tick em que a entrada dele chega — o que
  (i) e (ii) entregam ao timing): golpes bons **37–62 %** em toda latência, Δt médio −4 a −35 ms, 9–25 % no chão, nas
  duas sementes. Ler o golpe adversário assim que o instantâneo chega (≈ ii) em vez de 100 ms depois (≈ i), a 250 ms:
  passam 10–13 % contra 14–19 %, e o placar contra a IA fica em 44–53 % contra 40–46 % — na mesma direção nas quatro
  combinações de perfil e semente, mas cada diferença dentro do ruído.
- **Frente a frente**, host a 150 ms, sem perda:

  | | Intermediário (1000 · 5000) | Avançado (1000 · 5000) | Juntando as sementes |
  |---|---|---|---|
  | hoje | 60 % · 61 % | 59 % · 59 % | ≈ 60 % · ≈ 59 % |
  | saída (ii) emulada | 58 % · 52 % | 59 % · 53 % | ≈ 55 % · ≈ 56 % |
  | (ii) + (iv) | 51 % · 56 % | 53 % · 53 % | ≈ 53 % · ≈ 53 % |

  Com a (ii) o golpe do cliente fica igual ao do host (golpes bons 41–48 % contra 40–53 %), e a vantagem do host cai de
  ≈ 60 % pra ≈ 55 % — mas com a semente 1000 quase não cai (60 → 58, 59 → 59) e com a 5000 cai quase toda (61 → 52,
  59 → 53). Juntando, 55–56 % fica no limite da margem de ±3,8 pp: **a 150 ms, se sobra vantagem do host depois da (ii),
  é pequena, e 300 pontos por célula não resolvem**. A (iv) por cima: no Intermediário, 58 → 51 com a 1000 e 52 → 56
  com a 5000 (troca de sinal); no Avançado, 59 → 53 e 53 → 53. Juntando, ≈ 55–56 % → ≈ 53 %: −2 a −3 pp, dentro da
  margem de ±5,4 pp pra diferença entre duas células juntadas — **sem efeito medível**. A 250 ms: hoje 67–68 %, (ii)
  59–63 %, (ii) + (iv) 56–60 % — ali sobra vantagem do host com a (ii) nas duas sementes (cada célula fora da margem), e
  a (iv) não a tira. A explicação provável é a leitura: com a (ii), o cliente ainda sabe do golpe do host ~meia ida e
  volta + até 33 ms (um instantâneo) depois de ele acontecer, e o host sabe do dele na hora — mas a emulação da (iv), que
  iguala exatamente isso, não mostrou ganho, então a causa não está provada.

  (Juntar = média ponderada pelos pontos das células: Intermediário com (ii), (58 × 365 + 52 × 341) / 706 ≈ 55 %.)

## Proposta de compensação

### (i) O host julga o timing no tick que o cliente via

**Como.** Cada pacote de entradas leva o tick de desenho do quadro em que o aperto aconteceu. O host guarda ~0,5 s de
estados da bola; ao aplicar um aperto remoto, calcula o atraso visto (tick atual − tick de desenho, com teto no ping
medido + interpolação + folga) e julga o balanço como se tivesse começado lá atrás: se a bola passou pelo alcance dentro
dessa janela, o contato é refeito no tick em que teria acontecido e a trajetória nova é levada pela física até agora.
"Favorece quem bate" — o padrão de jogo de tiro e de esporte online.

**Prós.** O cliente continua vendo a bola interpolada, sem correção visual. O timing passa a ser o que ele viu (emulado:
golpes bons 37–56 % em toda latência, nas duas sementes).

**Contras.** Mexe no coração do jogo: golpe no passado e balanço com início retroativo (`Partida`, `Jogadores`), ponto
que o árbitro já decidiu dentro da janela (dois quiques, bola fora — segurar a decisão ou desfazê-la: `Arbitro`),
histórico e rebobinada (`ServidorDaPartida`), protocolo versão 2 (+4 B por pacote). O desequilíbrio **muda de lado** em
vez de sumir: o adversário recebe a bola já adiantada pelo atraso do cliente e perde esse tempo de reação. O cliente
declara o próprio atraso (trapaça possível: precisa de teto). E a leitura do golpe adversário continua 100 ms atrasada.
Quebra de propósito o teste 4 (`O_humano_no_host_joga_como_no_local_contra_as_mesmas_entradas_do_cliente_a_150_ms`): a
Partida do host passa a mudar por outro caminho que não a entrada aplicada.

**Arquivos.** `Rede/Protocolo.cs`, `Rede/ClienteDaPartida.cs` (`EnviarEntrada`), `Rede/FilaDeEntradas.cs` (o tick vai
com o aperto), `Rede/ServidorDaPartida.cs`, `Partida.cs`, `Jogadores.cs`, `Arbitro.cs`, e testes de rede e de golpe.

### (ii) O cliente simula a bola do lado dele até o tick em que a entrada chega — a D2 original

**Como.** O `ClienteDaPartida` já simula a bola com a mesma `Bola` do host a partir do último marco; hoje para no tick
de desenho (100 ms + ida no passado). A bola **do jogador** vai até o tick em que a entrada mandada agora será aplicada
no host — o mesmo instante em que o próprio jogador já é predito —, então corpo e bola ficam no mesmo tempo. O golpe do
adversário entra na leitura assim que o instantâneo chega, sem esperar a interpolação (o som pode continuar no tick
desenhado). Na tela, a bola é desenhada no passado perto do adversário (interpolada: o golpe dele não salta) e no
futuro perto do jogador, misturando pela posição ao longo do voo — o "time warp" dos jogos de esporte.

**Prós.** Nada muda no host nem no protocolo — os arquivos em revisão não mudam de contrato, e o teste 4 continua de pé.
Nada de confiança: o cliente só vê melhor, não declara nada. É a física do host, que já roda no cliente. Emulada: golpes
bons 41–62 % e bolas que passaram 5–13 % em toda latência, nas duas sementes. É o que a D2 decidiu ("a bola é simulada
localmente pelo `Padel.Core` e corrigida suavemente"); o código atual interpolou a bola em vez de simulá-la adiante.

**Contras.** A bola que vem do adversário parece um pouco mais rápida na chegada (a 150 ms + 100 ms de interpolação, num
voo de 1 s, ~25 %) — é a "bola pulando" que o M2 manda evitar, e decide no playtest. A estimativa do tick de chegada erra
1–2 ticks (8–17 ms de Δt, dentro do desvio do próprio jogador). Golpe inesperado do adversário só corrige quando o
instantâneo chega. **Talvez não feche o placar**: frente a frente, o host fica em 52–59 % a 150 ms (≈ 55–56 % juntando as
sementes, no limite do ruído) e em 59–63 % a 250 ms (fora do ruído, nas duas).

**Arquivos.** `Rede/ClienteDaPartida.cs` (tick de chegada da entrada — pelas confirmações de seq, melhor que pelo `Ping`;
`BolaNo` até ele; eventos de jogo entregues na chegada), `Rede/VisaoDaPartida.cs` (a bola do jogador e o tick dela; os
eventos chegados), `Rede/VisaoParaOHumano.cs` (lê a bola do jogador e os eventos chegados),
`Padel.Godot/scripts/Sessao/RetratoDaVisao.cs` (mistura as duas bolas pela posição), `docs/REDE.md`. Testes:
`JusticaNaRedeTests` ganha "a 150 ms o humano do cliente joga como o local" (o número daqui vira teste);
`RedePartidaOnlineTests` fica como está (a bola interpolada continua existindo).

### (iii) Diminuir o atraso de interpolação

**Como.** Trocar o padrão de 0,1 s no construtor do `ClienteDaPartida`.

**Prós.** Uma linha. A 0 ms, interpolação 0 devolve o nível do local.

**Contras.** Não resolve nada fora da rede local (80 ms: 15–17 % / 6–7 % de golpes bons; 150 ms: 2–3 % / 1 %) e cobra
na suavidade: com perda, o quadro sai mais vezes extrapolado — o `RedePartidaOnlineTests` prende "menos de 2 % de
quadros extrapolados" a 10 % de perda com os 100 ms.

**Arquivos.** `Rede/ClienteDaPartida.cs`, `Padel.Godot/scripts/Sessao/SessaoCliente.cs`, `RedePartidaOnlineTests.cs`.

### (iv) Igualar a leitura: o host lê com o atraso do cliente

**Como.** O host desenha — e o humano simulado dele lê — a partida atrasada pela latência de ida do cliente, com o
próprio jogador no presente e a bola adiantada até agora: o que o cliente vê na saída (ii), do outro lado da rede. O
golpe do cliente aparece pro host com o mesmo atraso com que o do host aparece pro cliente.

**Prós.** Não mexe no protocolo; o host só desenha de outro jeito. No papel, iguala a última assimetria que a (ii) deixa.

**Contras.** **Emulada por cima da (ii), não mostrou efeito acima do ruído**: a 150 ms, 51–56 % contra 52–59 % da (ii)
sozinha, com a diferença trocando de sinal entre as sementes (Intermediário: 58 → 51 com a 1000, 52 → 56 com a 5000);
a 250 ms, 56–60 % contra 59–63 %. Piora de propósito o jogo de quem hospeda. No 2x2 com clientes em pings diferentes,
iguala a quem (o pior? a média?). Faria sentido só na ranqueada, não entre amigos. Emulada de forma idealizada: o atraso
sai direto do quadro do cliente, que o host de verdade teria que estimar pelo ping.

**Arquivos.** `Rede/ServidorDaPartida.cs` (guardar os instantâneos que ele já captura e um `ParaDesenhar(atraso)`),
`Rede/VisaoParaOHumano.cs`, `Padel.Godot/scripts/Sessao/SessaoHost.cs`, opção na sala.

### Recomendação

**(ii) agora; (iii) não sozinha; (i) não; (iv) não, por enquanto.** (ii) conserta o que é regra do jogo — o timing do
golpe, que a D7 diz ser do corpo e não do ping — sem tocar no host nem no protocolo, é a decisão D2 como foi escrita, e o
ganho no golpe se repete nas duas sementes. (i) entrega o mesmo timing mexendo em `Partida`, `Arbitro` e protocolo, com
trapaça possível e o desequilíbrio só trocando de lado. (iii) só ajuda em rede local. (iv) cobra do host e, emulada, não
mostrou efeito medível: não entra sem uma medição que mostre ganho.

Ordem: (1) implementar (ii) quando a revisão de `Rede/` fechar; (2) rodar esta ferramenta de novo, com a (ii) real no
lugar da emulação, com as duas sementes e, no frente a frente, com 1.200 pontos por célula ou mais (margem de ±2,8 pp
por célula, ±4 pp pra diferença entre duas) — é o que decide se sobra vantagem do host a 150 ms; (3) virar teste o que
se repetir nas duas sementes: "a 150 ms, golpes bons do cliente dentro de 15 pp do local" (a emulação da (ii) já fica
dentro nas duas: 41–52 % contra 42–54 % no local); (4) só se sobrar vantagem do host fora do ruído, atacar a leitura —
com a (iv) ou outra saída —, medindo antes de decidir; (5) playtest da "bola pulando".

## Limites

- As linhas emuladas são teto: o tick de chegada sai do `Ping` (que inclui até 33 ms de espera pelo instantâneo); não
  mostram o custo visual da correção; (iv) usa o atraso lido do quadro do cliente.
- Duas sementes de ~320 pontos por célula: diferença de placar abaixo de ~8 pp entre duas células não se separa do
  ruído, e o documento a diz como incerta. A pergunta que ficou aberta — sobra vantagem do host a 150 ms depois da
  (ii)? — pede a amostra maior da ordem acima.
- O humano simulado não se adapta. Uma pessoa aprende a apertar mais cedo com atraso constante — em parte; o jitter
  atrapalha —, então "hoje" é o pior caso de quem não compensa. Mas o jogo não deve exigir essa compensação.
- Só o 1x1 com o cliente na vaga 2, só a IA Média. O 2x2 com três clientes não foi medido (a conta vale por cliente).
- O jogador entregue à IA fica com alcance de humano (1,35 m): igual dos dois lados de cada comparação, mas deixa a IA
  Média daqui um pouco mais forte que a da partida local comum.

## Réplica: semente 5000

Saída de `dotnet run -c Release --project ferramentas/JusticaNaRede -- --semente 5000`, títulos rebaixados. É dela que
saem os segundos números das faixas e das tabelas da Leitura.

<details>
<summary>Tabelas completas da semente 5000</summary>

### Intermediário com parceiro IA contra a IA Média

| Arranjo | Ida e volta | Perda | Δt médio | Δt mediano | σ(Δt) | p90 de \|Δt\| | Bola já no alcance ao apertar | Golpes bons | Bolas que passaram | Pontos ganhos | Golpes/ponto | Pontos |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Local (vaga 0) | — | — | -19 ms | -20 ms | 65 ms | 120 ms | 13 % | 44 % | 7 % | 57 % | 0,93 | 325 |
| Local (vaga 2) | — | — | -18 ms | -20 ms | 65 ms | 120 ms | 12 % | 43 % | 8 % | 56 % | 0,93 | 333 |
| Host (vaga 0) | 0 ms | 0 % | -19 ms | -20 ms | 65 ms | 120 ms | 13 % | 44 % | 7 % | 57 % | 0,93 | 325 |
| Host (vaga 0) | 0 ms | 2 % | -19 ms | -20 ms | 65 ms | 120 ms | 13 % | 44 % | 7 % | 57 % | 0,93 | 325 |
| Host (vaga 0) | 80 ms | 0 % | -19 ms | -20 ms | 65 ms | 120 ms | 13 % | 44 % | 7 % | 57 % | 0,93 | 325 |
| Host (vaga 0) | 80 ms | 2 % | -19 ms | -20 ms | 65 ms | 120 ms | 13 % | 44 % | 7 % | 57 % | 0,93 | 325 |
| Host (vaga 0) | 150 ms | 0 % | -19 ms | -20 ms | 65 ms | 120 ms | 13 % | 44 % | 7 % | 57 % | 0,93 | 325 |
| Host (vaga 0) | 150 ms | 2 % | -19 ms | -20 ms | 65 ms | 120 ms | 13 % | 44 % | 7 % | 57 % | 0,93 | 325 |
| Host (vaga 0) | 250 ms | 0 % | -19 ms | -20 ms | 65 ms | 120 ms | 13 % | 44 % | 7 % | 57 % | 0,93 | 325 |
| Host (vaga 0) | 250 ms | 2 % | -19 ms | -20 ms | 65 ms | 120 ms | 13 % | 44 % | 7 % | 57 % | 0,93 | 325 |
| Cliente (vaga 2) | 0 ms | 0 % | -90 ms | -112 ms | 48 ms | 120 ms | 47 % | 10 % | 11 % | 48 % | 0,89 | 323 |
| Cliente (vaga 2) | 0 ms | 2 % | -90 ms | -120 ms | 54 ms | 120 ms | 53 % | 6 % | 9 % | 52 % | 0,83 | 300 |
| Cliente (vaga 2) | 80 ms | 0 % | -104 ms | -120 ms | 50 ms | 120 ms | 85 % | 2 % | 11 % | 50 % | 0,76 | 316 |
| Cliente (vaga 2) | 80 ms | 2 % | -104 ms | -120 ms | 49 ms | 120 ms | 85 % | 3 % | 11 % | 50 % | 0,78 | 332 |
| Cliente (vaga 2) | 150 ms | 0 % | -101 ms | -120 ms | 56 ms | 120 ms | 86 % | 3 % | 15 % | 53 % | 0,67 | 337 |
| Cliente (vaga 2) | 150 ms | 2 % | -100 ms | -120 ms | 62 ms | 120 ms | 88 % | 3 % | 15 % | 47 % | 0,68 | 314 |
| Cliente (vaga 2) | 250 ms | 0 % | -94 ms | -120 ms | 65 ms | 120 ms | 83 % | 4 % | 26 % | 39 % | 0,54 | 300 |
| Cliente (vaga 2) | 250 ms | 2 % | -100 ms | -120 ms | 57 ms | 120 ms | 86 % | 6 % | 31 % | 37 % | 0,54 | 327 |

#### Intermediário: frente a frente (humano no host x humano no cliente, parceiros IA Parceiro)

| Ida e volta | Perda | Cliente com | Pontos do host | Δt médio host / cliente | Golpes bons host / cliente | Passaram host / cliente | Golpes/ponto host / cliente | Pontos |
|---|---|---|---|---|---|---|---|---|
| 0 ms | 0 % | hoje | 59 % | -21 ms / -94 ms | 43 % / 5 % | 5 % / 8 % | 0,96 / 1,00 | 374 |
| 0 ms | 2 % | hoje | 55 % | -20 ms / -97 ms | 41 % / 4 % | 2 % / 8 % | 0,91 / 0,90 | 348 |
| 0 ms | 0 % | saída (ii) emulada | 50 % | -21 ms / -28 ms | 45 % / 37 % | 6 % / 6 % | 1,04 / 0,99 | 301 |
| 0 ms | 0 % | (ii) + host lendo com o atraso do cliente (iv) | 50 % | -21 ms / -28 ms | 45 % / 37 % | 6 % / 6 % | 1,04 / 0,99 | 301 |
| 80 ms | 0 % | hoje | 54 % | -22 ms / -102 ms | 38 % / 2 % | 8 % / 11 % | 0,80 / 0,79 | 374 |
| 80 ms | 2 % | hoje | 54 % | -18 ms / -99 ms | 45 % / 2 % | 6 % / 8 % | 0,85 / 0,88 | 324 |
| 80 ms | 0 % | saída (ii) emulada | 56 % | -22 ms / -27 ms | 41 % / 39 % | 4 % / 8 % | 0,97 / 0,96 | 336 |
| 80 ms | 0 % | (ii) + host lendo com o atraso do cliente (iv) | 53 % | -25 ms / -22 ms | 42 % / 42 % | 5 % / 8 % | 1,02 / 1,07 | 320 |
| 150 ms | 0 % | hoje | 61 % | -24 ms / -88 ms | 39 % / 4 % | 5 % / 20 % | 0,85 / 0,75 | 338 |
| 150 ms | 2 % | hoje | 63 % | -24 ms / -91 ms | 42 % / 5 % | 6 % / 20 % | 0,87 / 0,83 | 350 |
| 150 ms | 0 % | saída (ii) emulada | 52 % | -24 ms / -30 ms | 40 % / 47 % | 7 % / 8 % | 1,01 / 0,99 | 341 |
| 150 ms | 0 % | (ii) + host lendo com o atraso do cliente (iv) | 56 % | -21 ms / -29 ms | 44 % / 42 % | 8 % / 7 % | 1,03 / 1,09 | 320 |
| 250 ms | 0 % | hoje | 67 % | -27 ms / -69 ms | 39 % / 6 % | 2 % / 23 % | 0,78 / 0,72 | 321 |
| 250 ms | 2 % | hoje | 67 % | -26 ms / -86 ms | 38 % / 5 % | 3 % / 34 % | 0,74 / 0,59 | 300 |
| 250 ms | 0 % | saída (ii) emulada | 60 % | -16 ms / -25 ms | 40 % / 45 % | 7 % / 14 % | 0,83 / 0,82 | 321 |
| 250 ms | 0 % | (ii) + host lendo com o atraso do cliente (iv) | 58 % | -25 ms / -33 ms | 38 % / 46 % | 4 % / 12 % | 0,91 / 0,88 | 307 |

#### Intermediário: o que cada saída compraria pro cliente (sem perda)

| Ida e volta | Cliente com | Δt médio | Δt mediano | p90 de \|Δt\| | Bola já no alcance ao apertar | Golpes bons | Bolas que passaram | Pontos ganhos | Golpes/ponto |
|---|---|---|---|---|---|---|---|---|---|
| 0 ms | interpolação de 100 ms (hoje) | -90 ms | -112 ms | 120 ms | 47 % | 10 % | 11 % | 48 % | 0,89 |
| 0 ms | interpolação de 50 ms | -56 ms | -70 ms | 120 ms | 24 % | 23 % | 10 % | 51 % | 0,85 |
| 0 ms | interpolação de 33 ms | -49 ms | -53 ms | 120 ms | 19 % | 31 % | 5 % | 53 % | 1,01 |
| 0 ms | interpolação de 0 ms | -26 ms | -28 ms | 120 ms | 15 % | 40 % | 7 % | 55 % | 0,99 |
| 0 ms | bola adiantada, leitura a 100 ms (≈ saída i) | -22 ms | -20 ms | 120 ms | 17 % | 42 % | 10 % | 48 % | 0,90 |
| 0 ms | bola adiantada, leitura sem atraso (≈ saída ii) | -19 ms | -20 ms | 120 ms | 9 % | 48 % | 10 % | 56 % | 0,88 |
| 80 ms | interpolação de 100 ms (hoje) | -104 ms | -120 ms | 120 ms | 85 % | 2 % | 11 % | 50 % | 0,76 |
| 80 ms | interpolação de 50 ms | -99 ms | -120 ms | 120 ms | 64 % | 6 % | 10 % | 54 % | 0,86 |
| 80 ms | interpolação de 33 ms | -96 ms | -120 ms | 120 ms | 58 % | 6 % | 8 % | 48 % | 0,87 |
| 80 ms | interpolação de 0 ms | -79 ms | -103 ms | 120 ms | 41 % | 15 % | 10 % | 55 % | 0,80 |
| 80 ms | bola adiantada, leitura a 100 ms (≈ saída i) | -23 ms | -12 ms | 120 ms | 21 % | 37 % | 9 % | 48 % | 0,86 |
| 80 ms | bola adiantada, leitura sem atraso (≈ saída ii) | -19 ms | -20 ms | 120 ms | 13 % | 46 % | 10 % | 55 % | 0,78 |
| 150 ms | interpolação de 100 ms (hoje) | -101 ms | -120 ms | 120 ms | 86 % | 3 % | 15 % | 53 % | 0,67 |
| 150 ms | interpolação de 50 ms | -104 ms | -120 ms | 120 ms | 88 % | 2 % | 14 % | 48 % | 0,76 |
| 150 ms | interpolação de 33 ms | -105 ms | -120 ms | 120 ms | 82 % | 3 % | 13 % | 48 % | 0,81 |
| 150 ms | interpolação de 0 ms | -104 ms | -120 ms | 120 ms | 77 % | 3 % | 12 % | 51 % | 0,89 |
| 150 ms | bola adiantada, leitura a 100 ms (≈ saída i) | -16 ms | -12 ms | 120 ms | 23 % | 38 % | 13 % | 50 % | 0,71 |
| 150 ms | bola adiantada, leitura sem atraso (≈ saída ii) | -25 ms | -12 ms | 120 ms | 21 % | 43 % | 10 % | 52 % | 0,84 |
| 250 ms | interpolação de 100 ms (hoje) | -94 ms | -120 ms | 120 ms | 83 % | 4 % | 26 % | 39 % | 0,54 |
| 250 ms | interpolação de 50 ms | -94 ms | -120 ms | 120 ms | 82 % | 3 % | 14 % | 43 % | 0,77 |
| 250 ms | interpolação de 33 ms | -108 ms | -120 ms | 120 ms | 90 % | 2 % | 19 % | 45 % | 0,73 |
| 250 ms | interpolação de 0 ms | -104 ms | -120 ms | 120 ms | 90 % | 2 % | 15 % | 47 % | 0,67 |
| 250 ms | bola adiantada, leitura a 100 ms (≈ saída i) | -4 ms | 5 ms | 120 ms | 13 % | 49 % | 19 % | 43 % | 0,63 |
| 250 ms | bola adiantada, leitura sem atraso (≈ saída ii) | -25 ms | -20 ms | 120 ms | 22 % | 45 % | 12 % | 44 % | 0,77 |

### Avançado com parceiro IA contra a IA Média

| Arranjo | Ida e volta | Perda | Δt médio | Δt mediano | σ(Δt) | p90 de \|Δt\| | Bola já no alcance ao apertar | Golpes bons | Bolas que passaram | Pontos ganhos | Golpes/ponto | Pontos |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Local (vaga 0) | — | — | -20 ms | -20 ms | 55 ms | 112 ms | 10 % | 51 % | 6 % | 58 % | 1,08 | 317 |
| Local (vaga 2) | — | — | -19 ms | -20 ms | 54 ms | 120 ms | 10 % | 54 % | 8 % | 56 % | 0,96 | 326 |
| Host (vaga 0) | 0 ms | 0 % | -20 ms | -20 ms | 55 ms | 112 ms | 10 % | 51 % | 6 % | 58 % | 1,08 | 317 |
| Host (vaga 0) | 0 ms | 2 % | -20 ms | -20 ms | 55 ms | 112 ms | 10 % | 51 % | 6 % | 58 % | 1,08 | 317 |
| Host (vaga 0) | 80 ms | 0 % | -20 ms | -20 ms | 55 ms | 112 ms | 10 % | 51 % | 6 % | 58 % | 1,08 | 317 |
| Host (vaga 0) | 80 ms | 2 % | -20 ms | -20 ms | 55 ms | 112 ms | 10 % | 51 % | 6 % | 58 % | 1,08 | 317 |
| Host (vaga 0) | 150 ms | 0 % | -20 ms | -20 ms | 55 ms | 112 ms | 10 % | 51 % | 6 % | 58 % | 1,08 | 317 |
| Host (vaga 0) | 150 ms | 2 % | -20 ms | -20 ms | 55 ms | 112 ms | 10 % | 51 % | 6 % | 58 % | 1,08 | 317 |
| Host (vaga 0) | 250 ms | 0 % | -20 ms | -20 ms | 55 ms | 112 ms | 10 % | 51 % | 6 % | 58 % | 1,08 | 317 |
| Host (vaga 0) | 250 ms | 2 % | -20 ms | -20 ms | 55 ms | 112 ms | 10 % | 51 % | 6 % | 58 % | 1,08 | 317 |
| Cliente (vaga 2) | 0 ms | 0 % | -99 ms | -120 ms | 39 ms | 120 ms | 51 % | 5 % | 6 % | 52 % | 0,95 | 335 |
| Cliente (vaga 2) | 0 ms | 2 % | -104 ms | -120 ms | 37 ms | 120 ms | 68 % | 3 % | 6 % | 51 % | 0,93 | 340 |
| Cliente (vaga 2) | 80 ms | 0 % | -111 ms | -120 ms | 43 ms | 120 ms | 91 % | 0 % | 11 % | 48 % | 0,87 | 358 |
| Cliente (vaga 2) | 80 ms | 2 % | -105 ms | -120 ms | 53 ms | 120 ms | 88 % | 2 % | 11 % | 46 % | 0,91 | 317 |
| Cliente (vaga 2) | 150 ms | 0 % | -102 ms | -120 ms | 58 ms | 120 ms | 89 % | 1 % | 13 % | 44 % | 0,89 | 316 |
| Cliente (vaga 2) | 150 ms | 2 % | -109 ms | -120 ms | 44 ms | 120 ms | 92 % | 2 % | 15 % | 47 % | 0,82 | 379 |
| Cliente (vaga 2) | 250 ms | 0 % | -97 ms | -120 ms | 58 ms | 120 ms | 83 % | 6 % | 23 % | 37 % | 0,59 | 304 |
| Cliente (vaga 2) | 250 ms | 2 % | -94 ms | -120 ms | 60 ms | 120 ms | 80 % | 6 % | 25 % | 39 % | 0,56 | 307 |

#### Avançado: frente a frente (humano no host x humano no cliente, parceiros IA Parceiro)

| Ida e volta | Perda | Cliente com | Pontos do host | Δt médio host / cliente | Golpes bons host / cliente | Passaram host / cliente | Golpes/ponto host / cliente | Pontos |
|---|---|---|---|---|---|---|---|---|
| 0 ms | 0 % | hoje | 57 % | -22 ms / -105 ms | 53 % / 1 % | 4 % / 5 % | 1,03 / 1,01 | 346 |
| 0 ms | 2 % | hoje | 58 % | -24 ms / -108 ms | 51 % / 1 % | 4 % / 6 % | 1,01 / 0,94 | 323 |
| 0 ms | 0 % | saída (ii) emulada | 53 % | -23 ms / -23 ms | 49 % / 45 % | 3 % / 6 % | 1,25 / 1,21 | 316 |
| 0 ms | 0 % | (ii) + host lendo com o atraso do cliente (iv) | 53 % | -23 ms / -23 ms | 49 % / 45 % | 3 % / 6 % | 1,25 / 1,21 | 316 |
| 80 ms | 0 % | hoje | 55 % | -22 ms / -105 ms | 53 % / 2 % | 6 % / 9 % | 1,03 / 1,05 | 352 |
| 80 ms | 2 % | hoje | 58 % | -22 ms / -99 ms | 49 % / 2 % | 4 % / 10 % | 0,97 / 0,98 | 333 |
| 80 ms | 0 % | saída (ii) emulada | 51 % | -21 ms / -33 ms | 52 % / 42 % | 5 % / 5 % | 1,25 / 1,19 | 310 |
| 80 ms | 0 % | (ii) + host lendo com o atraso do cliente (iv) | 48 % | -27 ms / -25 ms | 50 % / 44 % | 5 % / 5 % | 1,13 / 1,17 | 318 |
| 150 ms | 0 % | hoje | 59 % | -21 ms / -81 ms | 55 % / 2 % | 6 % / 12 % | 1,02 / 0,95 | 325 |
| 150 ms | 2 % | hoje | 63 % | -23 ms / -83 ms | 51 % / 2 % | 4 % / 16 % | 0,91 / 0,91 | 341 |
| 150 ms | 0 % | saída (ii) emulada | 53 % | -20 ms / -33 ms | 53 % / 48 % | 5 % / 5 % | 1,14 / 1,19 | 305 |
| 150 ms | 0 % | (ii) + host lendo com o atraso do cliente (iv) | 53 % | -26 ms / -40 ms | 46 % / 41 % | 3 % / 5 % | 1,10 / 1,05 | 313 |
| 250 ms | 0 % | hoje | 68 % | -18 ms / -74 ms | 54 % / 7 % | 3 % / 29 % | 0,72 / 0,64 | 314 |
| 250 ms | 2 % | hoje | 70 % | -21 ms / -80 ms | 49 % / 6 % | 2 % / 29 % | 0,69 / 0,65 | 301 |
| 250 ms | 0 % | saída (ii) emulada | 60 % | -21 ms / -32 ms | 55 % / 48 % | 4 % / 9 % | 1,06 / 1,15 | 325 |
| 250 ms | 0 % | (ii) + host lendo com o atraso do cliente (iv) | 60 % | -26 ms / -29 ms | 51 % / 49 % | 5 % / 12 % | 0,94 / 0,95 | 320 |

#### Avançado: o que cada saída compraria pro cliente (sem perda)

| Ida e volta | Cliente com | Δt médio | Δt mediano | p90 de \|Δt\| | Bola já no alcance ao apertar | Golpes bons | Bolas que passaram | Pontos ganhos | Golpes/ponto |
|---|---|---|---|---|---|---|---|---|---|
| 0 ms | interpolação de 100 ms (hoje) | -99 ms | -120 ms | 120 ms | 51 % | 5 % | 6 % | 52 % | 0,95 |
| 0 ms | interpolação de 50 ms | -61 ms | -62 ms | 120 ms | 14 % | 20 % | 10 % | 54 % | 0,94 |
| 0 ms | interpolação de 33 ms | -51 ms | -53 ms | 120 ms | 12 % | 31 % | 8 % | 58 % | 0,96 |
| 0 ms | interpolação de 0 ms | -31 ms | -28 ms | 120 ms | 14 % | 49 % | 7 % | 53 % | 1,01 |
| 0 ms | bola adiantada, leitura a 100 ms (≈ saída i) | -28 ms | -20 ms | 120 ms | 16 % | 49 % | 9 % | 51 % | 0,95 |
| 0 ms | bola adiantada, leitura sem atraso (≈ saída ii) | -25 ms | -20 ms | 120 ms | 10 % | 49 % | 5 % | 57 % | 1,01 |
| 80 ms | interpolação de 100 ms (hoje) | -111 ms | -120 ms | 120 ms | 91 % | 0 % | 11 % | 48 % | 0,87 |
| 80 ms | interpolação de 50 ms | -110 ms | -120 ms | 120 ms | 76 % | 2 % | 9 % | 45 % | 0,80 |
| 80 ms | interpolação de 33 ms | -103 ms | -120 ms | 120 ms | 66 % | 2 % | 11 % | 53 % | 0,90 |
| 80 ms | interpolação de 0 ms | -90 ms | -103 ms | 120 ms | 38 % | 7 % | 6 % | 51 % | 0,92 |
| 80 ms | bola adiantada, leitura a 100 ms (≈ saída i) | -23 ms | -20 ms | 120 ms | 16 % | 47 % | 11 % | 48 % | 0,93 |
| 80 ms | bola adiantada, leitura sem atraso (≈ saída ii) | -21 ms | -12 ms | 120 ms | 13 % | 54 % | 5 % | 54 % | 0,86 |
| 150 ms | interpolação de 100 ms (hoje) | -102 ms | -120 ms | 120 ms | 89 % | 1 % | 13 % | 44 % | 0,89 |
| 150 ms | interpolação de 50 ms | -105 ms | -120 ms | 120 ms | 89 % | 1 % | 10 % | 49 % | 0,89 |
| 150 ms | interpolação de 33 ms | -109 ms | -120 ms | 120 ms | 91 % | 1 % | 9 % | 50 % | 0,89 |
| 150 ms | interpolação de 0 ms | -108 ms | -120 ms | 120 ms | 84 % | 1 % | 6 % | 50 % | 0,94 |
| 150 ms | bola adiantada, leitura a 100 ms (≈ saída i) | -29 ms | -20 ms | 120 ms | 20 % | 55 % | 11 % | 53 % | 0,89 |
| 150 ms | bola adiantada, leitura sem atraso (≈ saída ii) | -28 ms | -20 ms | 120 ms | 17 % | 52 % | 8 % | 54 % | 0,87 |
| 250 ms | interpolação de 100 ms (hoje) | -97 ms | -120 ms | 120 ms | 83 % | 6 % | 23 % | 37 % | 0,59 |
| 250 ms | interpolação de 50 ms | -103 ms | -120 ms | 120 ms | 88 % | 4 % | 17 % | 46 % | 0,66 |
| 250 ms | interpolação de 33 ms | -108 ms | -120 ms | 120 ms | 92 % | 1 % | 12 % | 46 % | 0,71 |
| 250 ms | interpolação de 0 ms | -101 ms | -120 ms | 120 ms | 89 % | 2 % | 15 % | 42 % | 0,76 |
| 250 ms | bola adiantada, leitura a 100 ms (≈ saída i) | -19 ms | -12 ms | 120 ms | 15 % | 50 % | 14 % | 43 % | 0,68 |
| 250 ms | bola adiantada, leitura sem atraso (≈ saída ii) | -18 ms | -12 ms | 120 ms | 14 % | 62 % | 13 % | 53 % | 0,69 |

</details>
