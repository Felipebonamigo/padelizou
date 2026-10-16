# Padelizou Arena — instruções para o Claude

Jogo de padel 2x2 para a Steam, em Godot 4.7.2 + C#, com o motor numa biblioteca .NET pura (`Padel.Core`). Feito pelo
Felipe com o Claude. Nome provisório. Comece pelo `README.md` (estrutura, comandos, controles), pelo `CRONOGRAMA.md` (os
marcos e o que falta) e pelo `DECISOES.md` (os porquês). O realismo está no `REALISMO.md`.

## Onde o jogo mora

- Na pasta `padelizou-arena/` do repositório `Felipebonamigo/padelizou`, no branch **`claude/padel-game-new-9yl0ga`**.
  Nunca commite o jogo no `main`: ele é a plataforma Padelizou em produção. O plano é o jogo ganhar repositório próprio.
- As regras do CLAUDE.md da raiz valem aqui: defeito corrigido vira teste visto falhar antes, nada com teste vermelho,
  uma coisa de cada vez, três correções falhando é hora de parar, e fechar com `DONE`, `DONE_WITH_CONCERNS`,
  `NEEDS_CONTEXT` ou `BLOCKED`.
- Commits em português, `Padelizou Arena: <o que mudou>`, com as linhas de atribuição da sessão.
- **Direção nova, aguardando aprovação (14/10/2026):** a fatia jogável no Unreal Engine 5, num repositório novo, com a
  física reescrita em C++ e o `Padel.Core` como gabarito (`docs/FATIA-UNREAL.md`). Quando o repositório existir, as
  rotinas do estúdio vão para lá, mais as do Unreal (`revisar-sem-compilar`, `ligar-unreal-mcp`), como nos outros
  jogos em Unreal.

## Estúdio

- O Arena é um dos jogos do Felipe. Ferramentas, pré-configurações e pendências em comum estão no doc do estúdio:
  https://claude.ai/artifact/72KXW2Tve5gB65BHD1CDp6 (documento do Claude Docs, lido pelo conector).
- **Modelos 3D em comum aos jogos:** antes de baixar, procure na aba "Modelos 3D" do doc do estúdio e no repositório
  `Felipebonamigo/acervo-3d`. Modelo novo entra nos dois no mesmo dia, além do `Padel.Godot/modelos/CREDITOS.md`.

## Rotinas do Claude

Ficam em `.claude/skills/` na raiz do repositório (assim valem desde o início da sessão, também na nuvem):

- `verificar` (o que rodar antes de dizer "pronto"), `capturar` (print com o `--screenshot` num X virtual),
  `modelo-3d`, `triar-playtest`;
- `achar-gargalo`, `ler-crash`, `publicar-na-steam`, `declaracao-ia`, `conferir-fato`, `som-livre` e `traduzir`.
- Também servem as skills do Claude que já vêm instaladas: `skill-creator` (testa se uma rotina dispara com o jeito de o Felipe pedir), `statistical-analysis` (quantas partidas ou rodadas uma comparação precisa para valer), `computer-use` (no PC, pelo app do Claude, abre o executável, navega pelos menus e tira print; o Felipe liga em Settings > This computer > Computer use) e `fewer-permission-prompts` (no PC, libera de vez os comandos só de leitura que mais pedem permissão).

Problemas de playtest viram Issues "[Playtest] Arena: ..." no `padelizou`, com os campos de
`.github/ISSUE_TEMPLATE/playtest.yml` (o formulário só aparece no GitHub quando o jogo tiver repositório próprio). O
commit que corrige diz "fecha #N".

## Princípio do estúdio: divertido e bonito

Decisão do Felipe (16/10/2026): **todo jogo nosso tem que ser divertido e bonito, sempre**. Vale para toda tarefa, não só para as de arte.

- **Divertido primeiro.** Se não prende no protótipo feio, arte e polimento não salvam. Antes de chamar um sistema de pronto, pergunte
  se jogar ele dá vontade de "mais uma".
- **Bonito é requisito, aprovado por captura** (nunca só por teste verde), e a captura é olhada antes de se afirmar qualquer coisa visual.
- **Desempenho protege os dois:** quadro travado não é divertido nem bonito.
- Ao fechar um bloco, diga em uma linha o que ele mudou na diversão e no visual. Se não mudou nada, diga isso.
- Na dúvida entre "correto" e "gostoso de jogar", o Felipe decide jogando: leve a versão jogável para ele ver, não só o relatório.

## Regras do jogo

- `Padel.Core` não referencia o Godot. Nunca. Toda regra vira teste no `Padel.Core.Tests`.
- O motor da partida (a raiz de `Padel.Core`) não chama a libm: seno, cosseno, arco-tangente, exponencial, logaritmo e
  potência saem de `MatematicaDeterministica` (a libm muda de bits entre máquinas, e o PadelSim do `padelizou-game`
  reproduz o Core bit a bit). O teste `Nenhum_arquivo_do_motor_chama_funcao_da_libm` barra a volta.
- Identificadores e comentários em português. Coordenadas do Core: `x` largura, `y` comprimento, `z` altura; no Godot,
  `(x, z, y)`.
- Nada de marca nem de jogador real (FIP e Premier Padel são marcas de terceiros, só referência interna).
- Modelo 3D só CC0 da comunidade e nada gerado (`docs/MODELOS-DA-COMUNIDADE.md`). Aceitar termo, logar ou pagar: só o Felipe.
