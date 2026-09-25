# Nitro Crew — Roteiro e cronograma até a Steam

> **Documento vivo.** Ao concluir um passo, marque ✅ aqui. Revisar a cada duas semanas (ver "Rotina" no fim).
> Última revisão: **25/09/2026** — Fase 0 concluída nesta sessão.

Premissas: um desenvolvedor (Felipe) com **8–12 h/semana** para jogar, decidir, testar com amigos e cuidar da
parte comercial, mais o agente (Claude) para código, testes, ferramentas e balanceamento; arte e música
contratadas (ou geradas com ferramentas e retocadas por um artista). Durações são calendário estimado; fases
em paralelo compartilham semanas. O `AgeOfEarth` (o outro jogo do dono) segue a mesma trilha — muita coisa da
Fase 5 (empresa, conta Steamworks) é feita uma vez e serve aos dois.

Legenda de responsável: **V** = você (Felipe) · **A** = agente · **T** = terceiros (artista, compositor, Valve).

---

## Fase 0 — Fundação (✅ concluída em 25/09/2026)
Fatia vertical completa, toda procedural (sem um único asset binário):
- Núcleo determinístico (`src/core`): pista em segmentos com curvas, morros e box; 4 carros com trocas de
  atributos; física arcade (aceleração, freio, força centrífuga, grama, nitro ×3, câmbio automático/manual,
  combustível e pit stop); colisões carro-carro e carro-cenário; 19 pilotos de IA com habilidade, ponto de
  frenagem, desvio, elástico, nitro e box; voltas, posições, tolerância de chegada, resultado e pontos.
- **Cooperativo** (a diferença do jogo): cofre de nitro da equipe, empurrão ao companheiro parado, vácuo de
  equipe, elástico para o último da equipe; pontuação de equipe (dois melhores) e classificação por equipes nas copas.
- 12 pistas em 4 copas (Brasil, EUA, Japão, Europa), 6 cenários × 3 períodos do dia, escritas num DSL de pista.
- Renderizador **3D em Three.js** (low-poly estilizado: iluminação, sombras, bloom, névoa, céu dinâmico, mar,
  partículas, carros procedurais), HUD em DOM, minimapa e **tela dividida para 1–4 jogadores**; menus para sofá
  (teclado, mouse e até 4 gamepads); áudio procedural (motor por jogador, efeitos, jukebox com 4 músicas); opções
  e progresso salvos; empacotamento Electron; PT-BR e EN.
- 100+ testes (determinismo, pista, física, IA, co-op, campeonato, i18n, layout), corrida sem interface
  (`npm run smoke`), balanceamento (`npm run balance`) e playtest automatizado no Chromium (`npm run playtest`).

## Fase 1 — Jogabilidade sólida · semanas 1–5
Objetivo: divertido no sofá com 2–4 amigos, sem travar, sem "sensação de protótipo" na jogabilidade (a arte vem depois).

| # | Passo | Resp. | Semanas |
|---|---|---|---|
| 1.1 | Playtests seus: 3+ sessões com 2–4 pessoas e controles de verdade; lista curta de problemas por sessão (o que travou, o que ficou fácil/difícil, o que ninguém entendeu) | V | 1–5 (contínuo) |
| 1.2 | Sensação de direção: ajuste fino de volante, força centrífuga, freio, grama e colisões com base nos playtests; tremor de tela e "peso" do carro | A | 1–2 |
| 1.3 | Balanceamento por dados: IA × IA em todas as pistas por versão (`npm run balance`); tempos de volta por carro; dificuldade Amador de verdade fácil, Campeão de verdade difícil | A | 1–5 |
| 1.4 | Co-op afinado: quando o empurrão vale, quanto o vácuo rende, se o elástico está "trapaceando"; modo Versus (times por assento) e regra de classificação individual testados | A + V | 2–3 |
| 1.5 | Gamepads reais: Xbox, PlayStation, genérico USB, 4 ao mesmo tempo; Steam Input ligado/desligado; remapeamento na tela de controles | A + V | 2–3 |
| 1.6 | Desempenho: medir 4 viewports a 1080p e 1440p numa máquina fraca (notebook com gráfico integrado) e no Steam Deck; ajustar qualidade baixa/média (sombras, bloom, draw distance); sem estouro de memória em 1 h de jogo | A + V | 3–4 |
| 1.7 | Campeonato salvo no meio (continuar a copa depois de fechar o jogo); fantasma no contra-relógio (grava a melhor volta e mostra o carro-fantasma) | A | 3–4 |
| 1.8 | Caça a bugs por lentes: física, IA, colisões, menus/lobby, entrada, áudio, save; cada defeito vira teste de regressão | A | 4–5 |
| 1.9 | Tutorial de 90 segundos (primeira corrida guiada: acelerar, nitro, box, empurrão) | A | 5 |
| 1.10 | Polimento visual procedural (antes da arte final): chama do nitro mais legível, brilho de lente do sol, reflexos do neon no asfalto molhado (env map da cidade), cabine com colunas e faróis com geometria, poeira com textura, terreno com segunda oitava de ruído e transição de cor por altura, animação de troca de posição no HUD | A | 2–5 |

Marco **M1 (semana 5)**: "fatia vertical jogável por terceiros" — enviar build a 5 amigos com controles.

## Fase 2 — Identidade visual e áudio · semanas 3–14 (paralela)
Objetivo: do "bonito procedural" para o "bonito de loja". O visual é 3D low-poly estilizado (decisão de 25/09,
a pedido do dono: gráficos atuais); a arte final entra como modelos glTF e texturas no mesmo renderizador. O jogo
é lembrado pela música tanto quanto pela pista (o Top Gear é o exemplo), então a trilha é tão importante quanto os modelos.

| # | Passo | Resp. | Semanas |
|---|---|---|---|
| 2.1 | Direção de arte fechada em documento de 1 página (paleta por bioma, proporção dos carros, o que é "premium" nas referências Horizon Chase Turbo / Art of Rally); **nome definitivo** (verificar marca no INPI e nomes na Steam — "Nitro Crew" é provisório) e logo | V + T | 3–4 |
| 2.2 | Contratar artista 3D low-poly (ou pipeline com IA + retoque no Blender) com o briefing gerado da lista de `SpriteKind`, carros e biomas já no código; formato glTF, orçamento de triângulos por modelo | V + T | 4–5 |
| 2.3 | Assets: 8 carros (com variações de cor por material), ~60 modelos de cenário (6 biomas), skyboxes/céus e anéis de horizonte por bioma × período, arco de largada, box, arquibancadas; retratos de 20 pilotos; capsule art da Steam | T | 5–13 |
| 2.4 | Integração: carregador glTF no renderizador (substitui os modelos procedurais um a um), materiais e LODs, animações (rodas, suspensão, chama), efeitos de clima; "visual procedural" pode ficar como opção de baixo custo | A | 8–14 |
| 2.5 | Interface final: HUD e menus com design de produto (tipografia, ícones, transições), tela de vitória da copa, cinemática curta de pódio | T + A | 9–13 |
| 2.6 | Áudio: trilha com 10–12 músicas para o jukebox (compositor synthwave/rock), ~40 efeitos gravados ou desenhados, locutor de contagem (opcional) | T + A | 8–14 |
| 2.7 | Trailer de anúncio (30 s) com arte final e tela dividida | V + T | 13–14 |

Marco **M2 (semana 14)**: "arte e som finais no jogo" — página "Em breve" na Steam pode ir ao ar.

## Fase 3 — Conteúdo e profundidade · semanas 6–20
| # | Passo | Resp. | Semanas |
|---|---|---|---|
| 3.1 | 32 pistas em 8 países (como o original): +4 países (África do Sul, Austrália, Escandinávia, França/Itália), 4 pistas cada, escritas no DSL; teste de IA por pista já cobre as novas sozinho | A | 6–10 |
| 3.2 | Clima e período: chuva e neve (aderência, visual, spray), noite com faróis; pistas com túnel e ponte | A | 8–11 |
| 3.3 | Modo Carreira: prêmio em dinheiro por corrida; entre corridas, upgrades (motor, pneus, tanque, nitro extra) e compra de carros (8 no total); senha/continuar | A | 10–15 |
| 3.4 | Rivais com personalidade (agressivo, limpo, "bloqueador") e um rival principal por copa que provoca no resultado | A | 12–14 |
| 3.5 | Modos co-op extras: **Revezamento** (cada volta um jogador, com troca no box) e **Escolta** (a equipe protege um carro lento contra a IA) · Torneio local de sofá (chaveamento de até 8 pessoas alternando controles) | A | 14–18 |
| 3.6 | Conquistas (as 12 previstas em `src/game/desktop.ts` + 8), estatísticas e recordes por pista com nome | A | 16–18 |
| 3.7 | Acessibilidade: daltonismo (cores dos jogadores), tamanho do HUD, direção assistida (freio automático em curva) para crianças | A | 18–20 |

Marco **M3 (semana 20)**: conteúdo completo da 1.0.

## Fase 4 — Online · semanas 10–24
A ordem importa: o online barato primeiro.

| # | Passo | Resp. | Semanas |
|---|---|---|---|
| 4.1 | **Steam Remote Play Together**: o co-op de sofá vira online sem uma linha de netcode (a Steam transmite a tela e recebe os controles). Ativar, testar com 4 pessoas, ajustar latência do volante | A + V | 10–11 |
| 4.2 | Lockstep determinístico: o núcleo já é determinístico com hash por tick; relay WebSocket (copiar `server/relay.mjs` do AgeOfEarth), lobby por código, 4 jogadores, atraso de entrada de 2–3 ticks | A | 14–19 |
| 4.3 | Reconexão por snapshot (estado é JSON) e detecção de dessincronia com relatório | A | 19–21 |
| 4.4 | Steam Networking Sockets + convites da Steam via `steamworks.js`; Steam Leaderboards para contra-relógio e fantasmas de amigos | A | 20–24 |

Marco **M4 (semana 24)**: co-op online estável pela Steam (Remote Play desde a semana 11).

## Fase 5 — Steam, produção e legal · semanas 8–28
| # | Passo | Resp. | Semanas |
|---|---|---|---|
| 5.1 | Empresa/CNPJ, banco, W-8BEN (se já feito para o AgeOfEarth, reaproveitar) | V | 8–10 |
| 5.2 | Conta Steamworks + Steam Direct (US$ 100) + App ID; trocar `desktop/steam_appid.txt` | V | 10 |
| 5.3 | Página "Em breve": cápsulas, 6+ screenshots (tela dividida com 4 jogadores é a foto principal), descrição PT/EN, tags (Corrida, Arcade, Retrô, Co-op local, Tela dividida, Remote Play Together), trailer | V + T + A | 14–16 |
| 5.4 | Build Electron completo: SteamPipe, Steam Cloud (save em `localStorage` → pasta do app), conquistas, Rich Presence, Steam Input, tela cheia/resoluções | A | 16–20 |
| 5.5 | Steam Deck: verificar legibilidade da tela dividida em 7", 60 fps com 2 viewports, 4 controles via dock; build Linux nativa | A + V | 20–22 |
| 5.6 | Telemetria opt-in e relatório de erros | A | 18–20 |
| 5.7 | Legal: EULA, política de privacidade (relay → LGPD), licenças de fontes/áudio, créditos; **checagem de marca/nome** e distância visual dos jogos originais (nada de nome "Top Gear", logos ou traçados copiados) | V + A | 20–22 |
| 5.8 | QA: matriz (Windows 10/11, Linux, Mac; integrado × dedicado; 1–4 controles), checklist de lançamento | A + V | 22–26 |
| 5.9 | Steam Playtest público + demo (2 copas, co-op) no **Steam Next Fest** | V | 24–28 |

## Fase 6 — Lançamento · semanas 28–32
| # | Passo | Resp. | Semanas |
|---|---|---|---|
| 6.1 | Decidir Early Access (recomendado: com M1–M3 + Remote Play, e o online próprio na 1.0) ou 1.0 direto | V | 26 |
| 6.2 | Marketing: comunidade brasileira de retrô (YouTubers de SNES, grupos de Top Gear, Steam Brasil), streamers de co-op, kit de imprensa, Discord, devlogs | V + T | 24–32 |
| 6.3 | Preço (referência: R$ 30–40 / US$ 9,99), regiões, desconto de lançamento, pacote com o AgeOfEarth | V | 28 |
| 6.4 | Lançamento e janela de hotfix (2 semanas com correções diárias) | A + V | 30–32 |

Marco **M5 (semana ~30)**: Early Access na Steam. Marco **M6 (semana ~44)**: 1.0 com online próprio.

## Pós-lançamento (contínuo)
Editor de pistas + Workshop (as pistas são dados do DSL, ideais para isso), DLC de países, temporadas com ranking,
localização ES/DE/FR/JA, modo espectador para torneios. **Consoles**: o Electron não roda em Switch/Xbox/PS; um port
exigiria reescrever renderização e áudio noutro motor mantendo `src/core` como referência — decisão só depois de
ver as vendas na Steam.

---

## Custos previstos
| Item | Estimativa |
|---|---|
| Steam Direct | US$ 100 (devolvidos após US$ 1.000 em vendas) |
| Arte 3D low-poly (8 carros, cenários de 6 biomas, UI, cápsulas) | R$ 12–40 mil conforme escopo; menos com pipeline assistido por IA + Blender |
| Trilha (10–12 músicas) e efeitos | R$ 3–12 mil (compositor chiptune) ou bancos licenciados |
| Marca no INPI (opcional, recomendado) | ~R$ 355 + honorários |
| Servidor relay (só para o online próprio, Fase 4.2) | R$ 30–100/mês |
| Contabilidade/empresa | R$ 100–300/mês (dividido com o AgeOfEarth) |

## Riscos e mitigação
- **Arte é o caminho crítico**: fechar direção de arte na semana 4; os sprites procedurais nunca bloqueiam o código.
- **Nome e semelhança**: "inspirado em" é permitido, cópia não. Nome próprio, logo próprio, traçados próprios; sem música ou nomes de carros dos originais.
- **Tela dividida em máquina fraca**: 4 viewports quadruplicam o desenho 3D (sombras e pós-processamento por viewport). Já existe qualidade baixa/média; medir cedo (1.6).
- **Online de corrida é sensível a latência**: por isso Remote Play Together primeiro (4.1) e lockstep com atraso de entrada depois; nunca prometer online próprio antes de M4.
- **Escopo**: Carreira (3.3) e modos extras (3.5) só entram se M1 e M2 estiverem no prazo; senão, pós-lançamento.
- **Motivação/ritmo**: marco a cada 5–6 semanas com algo jogável no sofá.

## Rotina sugerida
- **Semanal**: você joga 1–2 sessões (idealmente com mais gente) e manda uma lista curta de problemas; o agente
  entrega correções + uma feature com testes, roda `npm test`, `npm run balance` e `npm run playtest`.
- **Quinzenal**: revisão deste arquivo (marcar concluído, mover o que atrasou, subir/descer prioridade).
- **Toda sessão do agente começa por `CLAUDE.md` e por este roteiro** — o estado atual mora aqui.
