---
name: gerar-som
description: 'Gera voz, efeito sonoro e música por IA no Padelizou Arena, com modelos locais gratuitos de licença comercial, testados antes de entrar: escolhe o modelo por teste de ouvido do Felipe, gera em lote, trata o volume e registra licença e gerado_por_ia. Use quando o Felipe pedir "gera a voz do...", "faz um som de...", "música por IA", "testa os modelos de voz" ou quando faltar áudio e não houver biblioteca livre que sirva.'
---
# Gerar som por IA (voz, efeitos, música)

Decisão do Felipe (16/10/2026): **IA para quase tudo, o mais barato possível, sem ator**. Gastar só em último caso. Esta rotina vale
depois da `som-livre`: biblioteca livre primeiro para tiro, batida, passo e ambiente, que costuma soar melhor que IA; IA para o resto.

## Regras deste jogo

- Hoje todo som do Arena é sintetizado por código com semente fixa (`ferramentas/sintetizar_sons.py`, 52 WAVs): os efeitos de golpe, quique e vidro **ficam como estão**, porque o teste regenera e compara. IA entra para narrador, voz de árbitro e música, em PT-BR, EN e ES.
- O repositório não usa LFS e é público: arquivos pequenos, em `Padel.Godot/audio/`.
- Árbitro e narrador são vozes inventadas, nunca de jogador ou locutor real.

Regras do estúdio: nada de modelo de uso não comercial (NC); nada de clonar voz de pessoa real nem de ator sem contrato escrito; todo
som de IA que o jogador ouve é marcado `gerado_por_ia` e vai para a declaração da Steam (skill `declaracao-ia`); serviço pago ou
com conta (ElevenLabs, Suno, Udio e afins), aceitar termo de modelo ou logar: só o Felipe.

## Candidatos (nomes e licenças de memória: CONFIRA a página de cada um antes de usar)

| Para quê | Candidato | Licença que eu lembro |
| --- | --- | --- |
| Voz leve, vozes prontas, tem português do Brasil | Kokoro | Apache 2.0 |
| Voz mais expressiva | Chatterbox (multilíngue) | MIT |
| Voz de reserva, mais robótica | Piper | MIT no código; cada voz tem licença própria |
| Música | ACE-Step | Apache 2.0 |
| Efeito e ambiente curtos | Stable Audio Open | licença da Stability: grátis abaixo de um teto de receita; confira o teto |
| App que reúne vozes (opcional) | VoiceStudio (AGPL-3.0, local, tem API e MCP) | o app é AGPL, mas **o motor padrão dele (OmniVoice) tem pesos CC-BY-NC: não usar** |

Evitar sem exceção: XTTS/Coqui, MusicGen e AudioGen (pesos não comerciais), F5-TTS (pesos CC-BY-NC), qualquer coisa "NC".
Para cada modelo escolhido, copie a licença dos **pesos** (não só do código) para ``padelizou-arena/Padel.Godot/audio/CREDITOS.md` (crie no primeiro; colunas item, arquivo, modelo, autor, URL, licença, data, gerado_por_ia)` com a data e a URL.

## Passos (a nuvem escreve, o PC instala e roda)

1. **Teste de ouvido, antes de tudo.** Gere os dois arquivos de teste de `.claude/skills/gerar-som/`: `frases_teste.csv` (20 falas em
   português do Brasil, de calma a grito) e `efeitos_teste.csv` (10 pedidos de efeito e 5 de música). Rode cada modelo candidato nos
   mesmos textos, com o mesmo nome de arquivo por linha, em `Fontes/audio-teste/<modelo>/` (pasta fora do git).
2. O Felipe ouve e escolhe por modelo: serve, serve só para falas curtas, ou não serve. Eu não ouço: não afirme qualidade.
   Anote o resultado numa tabela em `padelizou-arena/docs/`.
3. **Se nenhum modelo de voz servir em português do Brasil**, pare e diga. As saídas são biblioteca CC0, voz sintetizada simples
   (Piper) só para o que não é personagem, ou o Felipe decidir gastar. Não troque de serviço pago por conta própria.
4. **Gerar em lote** com um script na pasta de ferramentas do jogo (`padelizou-arena/ferramentas/`): um CSV de entrada (`id`, `texto` ou `pedido`,
   `modelo`, `semente`), saída em WAV 48 kHz. Semente fixa e guardada, para repetir a mesma fala depois.
5. **Tratar**: corte o silêncio das pontas, normalize pelo `ffmpeg` (`-af loudnorm=I=-23:TP=-1:LRA=11`, voz em -20 LUFS se o
   jogo mixar voz acima) e converta para o formato do jogo: WAV 44,1 kHz mono para efeito, OGG para música; arquivos pequenos.
6. **Registrar no mesmo commit**: uma linha em `padelizou-arena/Padel.Godot/audio/CREDITOS.md`, em `audio/gerados/` (o teste dos sintetizados não pode tropeçar neles), com `gerado_por_ia = sim`, modelo, versão do modelo, licença dos pesos, semente e
   a data. Guarde o CSV de entrada no git (texto, pequeno): é a "receita" do som.
7. O Felipe ouve dentro do jogo, com a mixagem, antes de aprovar. A skill `declaracao-ia` é atualizada junto.

## Armadilhas

- Voz de IA vai bem em fala curta e neutra; emoção forte (grito, choro, riso) é onde mais falha. Para rugido, grunhido e sons de criatura,
  efeito por IA funciona melhor que voz.
- Português do Brasil não é garantido em modelo "multilíngue": a clonagem herda o sotaque da referência, e a criação de voz por
  descrição pode sair instável. Só o teste de ouvido decide.
- Modelo e licença mudam: anote a versão. Se a licença dos pesos mudar para NC, o som já gerado com a versão antiga continua
  valendo, mas o arquivo da licença daquela data tem que estar no registro.
- Reclamação de jogador sobre "IA": a declaração da Steam tem que ser honesta; esconder conteúdo de IA pode tirar o jogo da loja.
- GPU: um modelo por vez e o editor do Unreal fechado, para não derrubar a medição de fps de outro jogo.
