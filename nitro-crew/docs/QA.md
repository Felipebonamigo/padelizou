# QA — matriz, checklist de lançamento e roteiro de teste manual (passo 5.8)

O agente cobre o que dá para cobrir sem hardware (testes, playtest no Chromium, E2E do pacote Electron); este
documento é o que **só gente com máquinas e controles de verdade** consegue fazer, e a ordem em que fazer.
Registre cada rodada numa cópia da tabela de resultado (fim do documento) com a versão testada — a versão está
no relatório de erros (Opções › Copiar relatório de erros, linha `version:`).

## 1. Portões automáticos (antes de qualquer teste manual)

Nenhuma build vai para testador com um destes vermelho.

| Portão | Comando | Onde roda |
|---|---|---|
| Tipos | `npm run typecheck` | raiz do jogo |
| Testes | `npx vitest run` (determinismo, pistas, física, IA, co-op, i18n, save, relatório de erros, ponte do Electron…) | raiz |
| Build | `npm run build` (typecheck + `dist/`) | raiz |
| Fluxo no navegador | `npm run preview` + `node scripts/playtest.mjs http://localhost:4174/` | raiz |
| Pacote Electron | `cd desktop && npm run dist:linux && xvfb-run -a npm run e2e` | `desktop/` |
| Balanceamento | `npm run balance -- 180 profissional 1` — comparar tempos de volta com a versão anterior | raiz |

O `e2e` abre o **executável empacotado** e confere asar, preload, pasta de dados, log de erros, relatório na
área de transferência, save em arquivo vencendo o `localStorage` e a tela de erro fatal. Build Windows e macOS:
o mesmo roteiro ainda não roda nelas (precisa de máquina Windows/Mac) — o item M-1 da matriz cobre à mão.

## 2. Matriz de configurações

### 2.1 Sistemas × GPU

Prioridade **P1** = obrigatório antes do Playtest público (5.9); **P2** = antes do Early Access; **P3** = se der.
"Integrado fraco" é o público mais provável de um jogo arcade de sofá (notebook da família ligado na TV).

| # | Sistema | GPU | Resolução | Prior. | O que olhar em especial |
|---|---|---|---|---|---|
| M-1 | Windows 11 | Integrado Intel (UHD 620 / Iris Xe) | 1080p | P1 | 4 jogadores a 60 fps em Baixa? Tela fatal de WebGL em driver velho |
| M-2 | Windows 10 | Integrado AMD (Vega 8, Ryzen 3/5 U) | 1080p | P1 | Idem; notebook na tomada e na bateria |
| M-3 | Windows 10/11 | Dedicado de entrada (GTX 1050 / RX 560) | 1080p e 1440p | P1 | 4 jogadores em Alta a 60 fps (é o "recomendado" da loja) |
| M-4 | Windows 11 | Dedicado atual (RTX 3060+/RX 6600+) | 1440p e 4K, 120/144 Hz | P2 | Monitor acima de 60 Hz: física igual (passo fixo), sem acelerar o jogo |
| M-5 | Steam Deck (SteamOS 3) | APU do Deck | 1280×800 | P1 | Build Linux nativa; legibilidade da tela dividida em 7"; 2 jogadores a 60 fps (passo 5.5) |
| M-6 | Steam Deck no dock | APU do Deck | 1080p na TV | P2 | 4 controles pelo dock; 30–60 fps com 4 telas |
| M-7 | Ubuntu 22.04/24.04 | Mesa (Intel ou AMD) | 1080p | P2 | Build Linux nativa fora do Deck; Wayland e X11 |
| M-8 | Windows (build Windows) sob Proton | qualquer | 1080p | P3 | Só se a build Linux der problema |
| M-9 | macOS 13+ | Apple Silicon (M1+) | Retina | P3 | Build mac sem assinatura abre com aviso; decidir se vale publicar para mac |
| M-10 | Qualquer | GPU na lista de bloqueio / driver antigo | — | P1 | Tela "O jogo não conseguiu iniciar" aparece, relatório copia (mouse **e** só com controle: direcional + A), `--ignore-gpu-blocklist` resolve |

Em cada linha, anotar: fps médio e mínimo com 1, 2 e 4 jogadores (qualidade Alta e Baixa), tempo de abertura até
a tela de título, uso de memória depois de 30 min, e se houve aviso de erro no canto.

### 2.2 Controles × jogadores

| # | Combinação | Prior. | O que olhar |
|---|---|---|---|
| C-1 | 1 jogador, teclado (setas; Espaço = nitro; M/N = marchas) | P1 | Menus inteiros só no teclado |
| C-2 | 2 jogadores, dois teclados virtuais (setas + WASD; F = nitro/entrar, E/Q = marchas) | P1 | Os dois no mesmo teclado sem uma tecla travar a outra (limite de teclas simultâneas do teclado — testar um teclado barato) |
| C-3 | 1 controle Xbox (com fio e Bluetooth) | P1 | Entrar, menus, pausa (Start), nitro, marchas |
| C-4 | 1 controle PlayStation (DualShock 4 / DualSense) | P1 | Idem; com Steam Input ligado e desligado |
| C-5 | 2 controles diferentes (Xbox + PlayStation) | P1 | Cada um no seu assento; desligar um no meio da corrida |
| C-6 | 3 jogadores (2 controles + teclado) | P1 | Tela 2×2 com a 4ª célula de classificação e minimapa |
| C-7 | 4 controles ao mesmo tempo | P1 | Os 4 aparecem distintos; a ordem de conexão define P1–P4; ninguém cai no assento de outro |
| C-8 | Controle genérico USB (clone barato) | P2 | Mapeamento não `standard` com Steam Input desligado |
| C-9 | Steam Input ligado com layout "Gamepad" × layout "Teclado e mouse" | P1 | No segundo, o aviso do README (todos no mesmo assento) — confirmar que o layout padrão publicado é "Gamepad" |
| C-10 | Controle que desconecta e reconecta (pilha acabando) | P2 | O jogo pausa ou o carro segue? O jogador consegue voltar ao próprio assento? |

### 2.3 Idiomas

| # | Idioma | Prior. | O que olhar |
|---|---|---|---|
| L-1 | Português (Brasil) | P1 | Acentos em todas as telas; nada cortado em 1024×640 |
| L-2 | Inglês | P1 | Textos mais longos em botões e HUD; nada em português escapando |
| L-3 | Troca de idioma com o jogo aberto | P1 | Todas as telas mudam sem reiniciar; o aviso de erro e a tela fatal também |
| L-4 | Sistema em outro idioma (espanhol, japonês) | P2 | O jogo abre em inglês (ou no último escolhido), não quebra |

`tests/i18n.test.ts` garante que toda chave tem PT e EN; a matriz L-* confere o que teste não vê (corte, quebra de
linha, tamanho).

### 2.4 Cobertura mínima por rodada

Não é preciso testar o produto cartesiano inteiro. Por rodada de P1: **M-1, M-3 e M-5** com **C-1, C-4 e C-7**, nos
dois idiomas (um idioma por máquina, alternando na rodada seguinte), mais M-10 uma vez por versão do Electron.

## 3. Checklist de lançamento

Marque na ordem; cada item tem dono (V = Felipe, A = agente, T = terceiros).

### 3.1 Build

- [ ] (A) Versão nova em `package.json` da raiz **e** de `desktop/` (a do relatório de erros vem da raiz).
- [ ] (A) Portões automáticos da seção 1 verdes, com a saída anotada.
- [ ] (A) `desktop/steam_appid.txt` com o App ID de verdade (não 480).
- [ ] (A) `npm run dist:win`, `dist:linux` (e `dist:mac`, se for publicar para mac) sem erro; ícones em `desktop/build/`.
- [ ] (V) Build Windows aberta numa máquina Windows limpa (sem Node, sem ferramentas de desenvolvedor).
- [ ] (A) Nenhum `.map` no pacote; nenhum arquivo de `scratch/` ou de teste.
- [ ] (V) Decisão sobre `ignore-gpu-blocklist` por padrão (desktop/README.md) tomada com base no M-10.
- [ ] (V) `"license"` do `package.json` da raiz trocado (hoje diz MIT — ver notas do EULA).

### 3.2 Steamworks

- [ ] (V) Depósitos Windows/Linux(/mac) enviados pela SteamPipe; branch de teste instalado **pela biblioteca**, não pelo `npm start`.
- [ ] (V) Opções de inicialização: executável certo por sistema (`Nitro Crew.exe` / `nitro-crew` / `Nitro Crew.app`).
- [ ] (V) Steam Cloud: Auto-Cloud com a raiz e as substituições (Root Overrides) de `desktop/README.md`; teste de
  dois computadores **com sistemas diferentes** (Windows → Steam Deck) feito.
- [ ] (V) Conquistas cadastradas com os IDs exatos de `desktop/README.md`; desbloqueio conferido com a Steam aberta.
- [ ] (V) Suporte a controle: "Suporte completo"; layout padrão do Steam Input = Gamepad.
- [ ] (V) Remote Play Together marcado só depois do teste com 4 pessoas (passo 4.1).
- [ ] (V) Classificação IARC preenchida.
- [ ] (V) Página: textos de `docs/LOJA.md` revisados; screenshots da arte final; requisitos trocados pelos medidos.

### 3.3 Legal

- [ ] (V + advogado) `docs/legal/PRIVACIDADE.md` revisado, marcadores ⚖️ e 🔧 resolvidos, versão EN traduzida, publicada num endereço fixo.
- [ ] (V + advogado) EULA: decidido se usa o próprio ou o padrão da Steam; se próprio, cadastrado no Steamworks.
- [ ] (V) Marca/nome conferidos (INPI, Steam); nada de "Top Gear" em lugar nenhum.
- [ ] (A) Créditos e licenças de terceiros (Three.js, Electron, fontes, arte e música contratadas) no jogo e no pacote.

### 3.4 Qualidade

- [ ] (V) Rodada P1 da matriz completa, sem bloqueador aberto.
- [ ] (V) Roteiro de 30 minutos (seção 4) feito por alguém que **não** é o Felipe.
- [ ] (V) Sessão de 1 hora com 4 jogadores sem travar, sem vazar memória (anotar a memória no início e no fim).
- [ ] (A) Todo defeito achado virou teste de regressão antes da correção.
- [ ] (V) Relatório de erros de cada testador recolhido (Opções › Copiar relatório de erros) — mesmo sem erro, confirma que ficou limpo.

### 3.5 Dia do lançamento

- [ ] (V) Build publicada no branch `default`; branch de teste guardado para voltar atrás.
- [ ] (V) Notas da versão em PT e EN.
- [ ] (V) Canal de suporte aberto (e-mail/fórum da Steam/Discord) e citado no texto do relatório de erros.
- [ ] (V) Kit de imprensa (`docs/IMPRENSA.md`) no ar, com o contato preenchido.
- [ ] (V) Primeiras 48 h: ler os fóruns e as análises; relatórios de erro recebidos viram testes.

## 4. Roteiro de teste manual — 30 minutos

Para um testador que nunca viu o jogo, com 1 controle e o teclado (e um segundo controle para o bloco C).
Anote **tudo o que estranhar**, mesmo sem certeza de que é defeito. Tempo entre parênteses.

### A. Primeira abertura (3 min)

1. Abra o jogo pela primeira vez. Anote quanto tempo leva até a tela de título e se a janela abre no tamanho
   certo. ✅ tela de título com o carro ao fundo, sem aviso de erro no canto.
2. Aperte qualquer botão do controle. ✅ vai para o menu principal; o controle navega.
3. F11 (ou Opções › Tela cheia). ✅ entra e sai de tela cheia; a imagem não fica esticada nem cortada.

### B. Uma corrida sozinho (7 min)

4. Corrida rápida → entre com o controle → escolha a pista "Orla de Copacabana", 2 voltas.
5. Na largada, segure o acelerador durante a contagem. ✅ nada de largar antes do "JÁ!".
6. Corra a primeira volta **sem frear**; na segunda, freie antes das curvas. ✅ dá para sentir a diferença; sair
   na grama tira velocidade; bater em carro por trás dói.
7. Use os 3 nitros. ✅ o HUD conta; o nitro acaba no terceiro.
8. Aperte Start/Esc no meio da corrida. ✅ pausa; "Continuar" volta de onde parou; "Sair para o menu" volta ao menu.
9. Termine a corrida. ✅ tela de resultado com posições e tempos; recorde novo, se foi o caso.

### C. Tela dividida (8 min)

10. Ligue o segundo controle (ou use o teclado como P2: setas, ou WASD com F para entrar). Corrida rápida com os
    dois. ✅ cada um entra no próprio assento; a tela divide em cima/embaixo.
11. Um jogador para no meio da pista; o outro passa bem perto. ✅ o parado recebe o **empurrão**.
12. Andem colados (um atrás do outro). ✅ quem está atrás ganha **vácuo** e se aproxima (não há aviso no HUD — anote se deu para perceber).
13. Um usa nitro. ✅ o cofre da equipe desconta para os dois (com a assistência "Cofre de nitro" ligada).
14. Corra até o combustível acabar ou entre no box na reta de largada. ✅ o carro reabastece devagar no box; o
    HUD avisa "COMBUSTÍVEL BAIXO — ENTRE NO BOX" antes de acabar.
15. Desligue o segundo controle no meio da corrida e ligue de novo. Anote o que acontece.

### D. Campeonato (5 min)

16. Campeonato → Copa Brasil com 1 ou 2 jogadores. Termine a primeira corrida (pode ser mal).
17. ✅ a classificação da copa aparece; a próxima corrida começa na pista seguinte.
18. Feche o jogo no meio da copa e abra de novo. Anote se o progresso da copa ficou (passo 1.7 do roteiro: ainda
    pode não existir).

### E. Opções, idioma e relatório (5 min)

19. Opções → troque o idioma para English. ✅ todas as telas mudam na hora; nada fica em português.
20. Mude volume, qualidade e minimapa; saia e volte. ✅ tudo ficou salvo.
21. Feche o jogo e abra de novo. ✅ as opções continuam (e o idioma também).
22. Opções → "Copiar relatório de erros" → cole num bloco de notas. ✅ texto começa com
    "Nitro Crew — error report", tem a versão, e **não** tem o seu nome de usuário nem pastas do computador.
23. Confira "Telemetria anônima": ✅ veio **Desligado**.

### F. Fechamento (2 min)

24. Saia pelo menu ("Sair", só na versão para computador). ✅ fecha sem travar.
25. Anote: o que foi mais divertido, o que ninguém entendeu, e cole o relatório de erros no fim das anotações.

## 5. Registro de uma rodada

Copie para cada rodada (uma linha por máquina × combinação):

| Data | Versão | Máquina (M-*) | Controles (C-*) | Idioma | fps 1P / 2P / 4P | Abertura (s) | Memória 30 min | Defeitos (link) | Relatório de erros limpo? | Testador |
|---|---|---|---|---|---|---|---|---|---|---|
| | | | | | | | | | | |
