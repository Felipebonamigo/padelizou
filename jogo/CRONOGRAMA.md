# Punhos de Shaolin — cronograma até a Steam

> Plano de 16 semanas (4 meses) pra levar o jogo de "protótipo jogável" a "à venda na Steam".
> Assume sessões de 2–4 h por dia com o Claude implementando e o Felipe decidindo, testando
> num Windows de verdade e cuidando do que só uma pessoa pode fazer (conta, dinheiro, arte,
> trailer). Cada fase tem um **critério de pronto** — sem ele, a fase não fecha.
>
> Datas contam a partir de **25/09/2026**. Semana 1 = 28/09.

## A decisão de tecnologia: JavaScript + Electron, com o motor separado

**Escolha: manter o motor em JavaScript e empacotar com Electron + `steamworks.js`.**

Por quê, em ordem de peso:

1. **O motor já existe, é determinístico e tem 40 checks de regressão.** Reescrever em C#
   (MonoGame), GDScript (Godot) ou C++ jogaria fora o único pedaço testado do projeto pra
   ganhar algo que este jogo não precisa: um beat-em-up 2D em 60 fps roda folgado em Canvas.
2. **A Steam não sabe nem se importa com a linguagem.** *Vampire Survivors* nasceu em Phaser +
   Electron e vendeu milhões antes de ser portado. *CrossCode* é NW.js. O que a Steam exige é
   um executável Windows, integração com a Steamworks API (conquistas, overlay, cloud) e um
   build enviado pelo SteamPipe — o Electron entrega os três.
3. **Uma pessoa só.** JS roda no navegador, no Node (testes sem tela) e no Electron com o
   MESMO arquivo. Godot/Unity exigem um editor que não roda numa sessão web como esta, e o
   Felipe já tem a rotina de PR → CI → conferidor funcionando.
4. **O visual é vetorial**, então o desktop ganha resolução nativa (1080p, 1440p, Steam Deck)
   de graça, sem redesenhar sprite nenhum.

O preço da escolha, dito na cara:

- **Tamanho**: um build Electron pesa ~120–200 MB. Pra um jogo de R$ 20 ninguém reclama, mas
  não é um `.exe` de 5 MB.
- **Consoles nunca**: Switch/PlayStation/Xbox não rodam Electron. Se um dia o jogo chegar lá,
  aí sim se porta o motor (a separação regra/tela é o que deixa isso possível sem reescrever a
  parte difícil). Não é o problema de hoje.
- **macOS custa**: assinar e notarizar exige conta Apple (US$ 99/ano). Lançar em Windows +
  Linux (que também cobre Steam Deck via Proton) e deixar Mac pra depois.

**Alternativa considerada e descartada por agora:** MonoGame em C# (a linguagem do Padelizou,
usada por *Celeste* e *Stardew Valley*). Seria a escolha se o jogo ainda não existisse. Reabrir
essa decisão só se aparecer um motivo que o Electron não resolva — console é o único que conheço.

## As fases

| Fase | Semanas | O que entrega | Quem trava |
|---|---|---|---|
| 0 · Fundação técnica | 1–2 | Build desktop, salvar, opções, dificuldade, conquistas internas | Claude |
| 1 · Conteúdo vendável | 3–6 | Progressão, 3º monge, inimigos e chefes com fases, modo arena, história | Claude + decisões |
| 2 · Arte e som de produção | 5–8 (paralelo) | Polimento visual, capsules, trilha, identidade | **Felipe** (arte e música são compra ou encomenda) |
| 3 · Idiomas e acessibilidade | 7–9 | Inglês obrigatório, espanhol, remapear controles, opções de acessibilidade | Claude |
| 4 · Steam de verdade | 8–11 | Conta Steamworks, AppID, integração real, página "Em breve", build no SteamPipe | **Felipe** (conta, US$ 100, fiscal, banco) |
| 5 · Playtest e polimento | 10–13 | Testadores, bugs, desempenho, Steam Deck, demo | Felipe testa no Windows |
| 6 · Lançamento | 14–16 | Data, wishlists, imprensa, desconto, patch 1 | Felipe |

Dinheiro obrigatório: **US$ 100** (taxa Steam Direct por jogo; volta quando o jogo passa de
US$ 1.000 de receita). Opcional mas recomendado: capsule art por artista (R$ 500–2.000) e trilha
sonora (R$ 1.000–3.000, ou banco de música licenciada). Tudo o mais é tempo.

---

### Fase 0 · Fundação técnica (semanas 1–2) — **começa agora**

Objetivo: o jogo vira um *programa instalável* que salva, tem opções e sabe o que é uma
conquista, mesmo antes de existir a conta na Steam.

- [x] **Empacotamento Electron** em `jogo/`: janela 16:9, tela cheia (F11), ícone, menu escondido,
      instância única. `npm run start` roda; `npm run empacotar` gera Windows e Linux.
- [x] **Ponte de plataforma** (`plataforma.js`): salvar/carregar em arquivo no desktop, em
      `localStorage` no navegador. A Steam entra pela mesma ponte, com *fallback* — o jogo roda
      igual sem a Steam aberta.
- [x] **Progresso** (`progresso.js`): fase alcançada, recorde, dificuldade, opções, conquistas,
      estatísticas. Com versão e migração — o primeiro salvamento nunca é o último formato.
- [x] **Menu de título**: Novo jogo · Continuar (fase N) · Opções · Sair.
- [x] **Dificuldade**: Fácil / Normal / Difícil (tabela no motor: vida e dano dos inimigos).
- [x] **Opções**: volume da música e dos efeitos separados, tremor de tela, tela cheia, apagar progresso.
- [x] **Conquistas internas** (`conquistas.js`): 15 definidas em código, desbloqueio por evento do
      motor, aviso na tela. Steam só vai *espelhar* isso na Fase 4.
- [x] **Resolução nativa**: o canvas desenha no tamanho real da tela (vetorial fica nítido em 1440p).
- [x] **Conferidores**: progresso/conquistas/dificuldade no Node, como o resto.
- [ ] **Repositório próprio** (`punhos-de-shaolin`): o jogo não é o Padelizou. Até o Felipe criar,
      fica em `jogo/` — mas o `package.json` já mora lá, isolado do app .NET.

**Pronto quando:** `npm run start` abre o jogo numa janela no Windows do Felipe, fecha e reabre
com o progresso guardado, e os conferidores estão verdes no CI.

### Fase 1 · Conteúdo vendável (semanas 3–6)

Objetivo: de "demo de mecânica" pra "jogo de 2–3 horas com motivo pra jogar de novo".

- [ ] **Progressão de golpes** (o coração do *Shaolin Monks*): pontos compram golpes novos entre
      fases — combo de 5, contra-ataque, especial no ar, agarrão por trás.
- [ ] **Terceiro monge** com estilo próprio (rápido e frágil, ou com corrente/chicote — decidir).
- [ ] **Dois inimigos novos** (lanceiro que ataca de longe no chão, monge renegado que defende e
      contra-ataca) e **chefes em duas fases** (padrão muda na metade da vida).
- [ ] **Cenário que mata**: espinhos, fogo, poço — arremessar inimigo neles é morte na hora, como
      no original. Objetos interativos e destrutíveis por fase.
- [ ] **Fases mais longas com pontos de controle** (morrer volta ao último *set*, não ao começo).
- [ ] **Modo Arena** (sobrevivência por ondas, placar) — é o que dá vida longa a beat-em-up.
- [ ] **História mínima**: 3 linhas entre fases, com cena desenhada em canvas. Título e nomes
      originais (nada da marca Mortal Kombat; conferir se "Shaolin" no título tem restrição de marca
      no Brasil e nos EUA — o Templo Shaolin registra o nome em alguns países).
- [ ] **Balanceamento por dados**: o motor é determinístico — simular 1.000 lutas por dificuldade
      no Node e ajustar até a curva de morte fazer sentido.

**Pronto quando:** uma pessoa que nunca viu o jogo termina a campanha no Normal em 2–3 h,
morre, e quer tentar de novo. Testar com 3 pessoas.

### Fase 2 · Arte e som de produção (semanas 5–8, em paralelo com a 1)

Objetivo: o que aparece na página da Steam. A página vende antes do jogo.

- [ ] **Decisão de identidade**: manter o visual vetorial "à mão" como marca (barato, coerente,
      escala em qualquer resolução) e POLIR — rosto, cabelo, roupa, animação de andar e parado
      mais expressiva, iluminação de cenário, sombras, partículas melhores.
- [ ] **Capsules da Steam** (tamanhos exigidos pela loja: header 920×430, capsule pequena
      462×174, principal 1232×706, vertical 748×896, biblioteca 600×900, herói 3840×1240, logo
      1280×720). O jogo pode renderizar as poses; a composição final é trabalho de artista ou
      de ferramenta de imagem — **decisão e gasto do Felipe**.
- [ ] **Trilha sonora**: 5 faixas (4 fases + título). O sequenciador sintetizado serve de
      *placeholder*; trilha real vende. Opções: músico, ou banco licenciado (ver Fase 4, licença
      precisa cobrir venda comercial).
- [ ] **Efeitos e vozes**: grunhidos e gritos sintetizados hoje; avaliar pacote licenciado.
- [ ] **Trailer de 30–60 s**: gravado com OBS do próprio jogo, cortes rápidos, sem narração.

**Pronto quando:** capsules e 5 screenshots aprovados pelo Felipe, trailer exportado em 1080p.

### Fase 3 · Idiomas e acessibilidade (semanas 7–9)

Objetivo: a Steam é global — sem inglês, metade das vendas não existe.

- [ ] **Tabela de textos** (`textos.js`): todo texto do HUD, menus e história por chave.
      pt-BR + **en** obrigatórios; es depois. Detecção pelo idioma da Steam/navegador, troca nas opções.
- [ ] **Remapear teclado e controle** na tela de opções, com glifos do controle (Steam Input).
- [ ] **Acessibilidade**: desligar tremor e flash, tamanho do HUD, modo daltônico nas barras,
      pausa automática ao perder o foco.
- [ ] **Steam Deck**: layout de controle padrão, 16:10 com barras, 60 fps no perfil de 15 W.

**Pronto quando:** o jogo inteiro em inglês sem nenhuma string solta no código, e jogável de
ponta a ponta só com controle.

### Fase 4 · Steam de verdade (semanas 8–11)

Objetivo: página no ar e build enviado. **Aqui o Felipe é o gargalo, e é normal.**

- [ ] **Conta Steamworks** (partner.steamgames.com): pessoa física ou empresa (o Padelizou já é
      empresa — usar a empresa evita refazer depois). Taxa de US$ 100. Formulário fiscal (W-8BEN-E
      pra empresa brasileira), dados bancários pra receber em dólar. **Leva de 3 a 10 dias entre
      cadastro, verificação e liberação.**
- [ ] **Criar o app**: AppID, depots (Windows, Linux), `steam_appid.txt` no desenvolvimento
      (o app de teste 480 serve antes do AppID existir).
- [ ] **Integração real** pela ponte já pronta: `steamworks.js` inicializa, conquistas cadastradas
      no painel com os mesmos ids do `conquistas.js`, estatísticas, overlay funcionando, **Steam
      Cloud** apontando pra pasta do `progresso.json`, Rich Presence ("Lutando no Poço das Almas").
- [ ] **Build pelo SteamPipe**: `steamcmd` + os `.vdf` de `jogo/steam/`. Branch `beta` privado
      primeiro; `default` só no lançamento.
- [ ] **Página "Em breve"**: descrição curta e longa, tags, capsules, 5+ screenshots, trailer,
      gênero, requisitos mínimos, preço. **Regra da Steam: a página precisa ficar no ar pelo menos
      2 semanas antes do lançamento**, e a revisão da página leva 2–5 dias úteis. O build também
      passa por revisão.
- [ ] **Preço**: comparáveis do gênero ficam entre US$ 4,99 e 9,99. Sugestão de partida
      **US$ 5,99**, com o preço regional que a Steam sugere pro Brasil (fica perto de R$ 20).
      Desconto de lançamento de 10%.
- [ ] **Classificação indicativa**: sem sangue explícito o jogo fica em "violência de fantasia";
      preencher o questionário IARC no painel (dá a classificação pra várias regiões de uma vez).

**Pronto quando:** página "Em breve" aprovada e visível, build `beta` instalável pela Steam no
Windows do Felipe, conquista desbloqueando com o pop-up da Steam.

### Fase 5 · Playtest e polimento (semanas 10–13)

- [ ] **Steam Playtest** ou chaves pra 10–20 pessoas. Formulário curto de feedback. Bug que
      alguém achou vira conferidor (Regra 1 do `CLAUDE.md` vale pro jogo).
- [ ] **Desempenho**: 60 fps estáveis num notebook fraco com gráfico integrado; medir com o
      próprio contador de quadros; flags do Electron pra GPU.
- [ ] **QA no Windows** (obrigatório — o navegador headless daqui não substitui): controle
      Xbox/PS, teclado, alt-tab, dois monitores, DPI 125/150%, fechar durante a luta.
- [ ] **Demo pro Steam Next Fest** (edições em fevereiro, junho e outubro; a inscrição fecha
      semanas antes): a primeira fase como demo dá wishlist de graça. Decidir se cabe na data.
- [ ] **Telemetria opcional** (opt-in): onde as pessoas morrem, quanto tempo jogam. Anônima, com
      botão de desligar nas opções.

**Pronto quando:** duas semanas sem bug bloqueante reportado, e 3 testadores externos terminam
a campanha.

### Fase 6 · Lançamento (semanas 14–16)

- [ ] **Data marcada** no painel (evitar a semana de lançamentos grandes; terça a quinta).
- [ ] **Wishlists**: meta de 1.000 antes do dia (a Steam mostra "Populares em breve" a partir de
      ~7.000, mas 1.000 já dá as primeiras vendas). Post no Reddit/r/IndieGaming, TikTok do
      combate, creators brasileiros de beat-em-up, imprensa indie BR.
- [ ] **Dia D**: build no branch `default`, desconto de 10%, anúncio de comunidade, responder as
      primeiras avaliações e reembolsos com calma.
- [ ] **Patch 1 na semana seguinte**: os bugs que só 1.000 pessoas acham.
- [ ] **Depois**: conquistas globais, atualizações de conteúdo (modo arena, monge novo), Mac se
      valer o custo, e aí sim a conversa de console — que começaria com o porte do motor.

**Pronto quando:** o jogo está à venda, o dinheiro entra na conta da empresa 30 dias depois do
fim do mês, e o Felipe decidiu o que vem depois.

---

## O que só o Felipe pode fazer (lista pra não travar o resto)

1. Criar o repositório `punhos-de-shaolin` no GitHub (Fase 0).
2. Abrir a conta Steamworks com a empresa, pagar os US$ 100, preencher fiscal e banco (Fase 4 —
   **mas vale começar na semana 3**: a verificação demora e não depende do jogo).
3. Decidir e pagar arte de capsule e trilha (Fase 2).
4. Testar cada build num Windows de verdade, com controle (todas as fases).
5. Gravar o trailer (ou aprovar o que sair do OBS).
6. Nome final, preço, data.

## Riscos, sem maquiagem

- **Vender pouco é o resultado mais provável** de um beat-em-up indie sem marca: a mediana de
  um jogo pequeno na Steam fica na casa das centenas de cópias. O plano existe pra dar ao jogo a
  melhor chance com o menor gasto — não pra prometer receita.
- **Arte é o teto.** O visual vetorial é honesto e coerente, mas capsule fraca mata página.
- **Tempo do Felipe** é o recurso mais curto — daí as fases em paralelo e a lista de bloqueios.
- **Escopo cresce.** Cada item da Fase 1 que virar "e se tivesse também…" empurra a data. A
  regra do `CLAUDE.md` vale: uma coisa de cada vez, até o fim.
