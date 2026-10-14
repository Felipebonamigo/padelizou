---
name: som-livre
description: 'Procura, confere a licença, trata e registra efeito sonoro e música do Padelizou Arena, na mesma linha da skill modelo-3d. Use antes de baixar, gravar ou gerar qualquer som, quando o Felipe pedir "põe um som de...", "troca a música" ou "falta áudio", e sempre que um som novo entrar no jogo.'
---
# Som livre: procurar, conferir, registrar

Regra do Padelizou Arena: hoje todo som é sintetizado por código, com semente fixa (`ferramentas/sintetizar_sons.py`: 52 WAVs em
`Padel.Godot/audio/`, testados por `ferramentas/teste_sintetizar_sons.py`). Música ainda não existe; o M3 prevê 6 a 8
faixas e narrador, e o `REALISMO.md` prevê gravar o som num clube. A licença de música tem que cobrir venda
(`docs/LEGAL.md`, A12).

## Onde procurar, nesta ordem

1. O que o jogo já tem: os 52 sons sintetizados (golpes, quique, vidro, grade, rede, passo, torcida, placar, ambiente do clube) e o
   `SomNode` (`Padel.Godot/scripts/Som/SomNode.cs`: 16 vozes, variação sem repetir a última, altura ±5 %, nível por família).
2. **Sonniss GDC Game Audio Bundle**: milhares de efeitos gravados por estúdios, livres para jogo comercial, sem crédito
   obrigatório. A licença proíbe redistribuir o som solto: o repositório `padelizou` é público e não usa LFS: o arquivo da Sonniss fica fora do git e só entra com o Felipe
   de acordo.
3. **Kenney** (pacotes de áudio CC0) e **Freesound** filtrado por licença **CC0** (ou CC BY, com crédito). O download
   do Freesound em qualidade original exige login: quem baixa é o Felipe.
4. **OpenGameArt**: só itens CC0 ou CC BY; confira a licença item por item, não a do pacote.
5. Gerar ou encomendar: pergunte ao Felipe antes; música ou narração por IA só com licença comercial clara, e aí entra na declaração.

Recusa: NC, ND, "uso pessoal", som de outro jogo ou filme, música com letra ou melodia conhecida, a BBC Sound Effects
(licença não comercial), e som de marca (vinheta, buzina registrada).

## Tratar antes de entrar

- Corte o silêncio do começo e do fim e confira se não estala no corte.
- Volume: normalize por categoria com o ffmpeg. Sem alvo escrito no jogo, use -23 LUFS integrado e pico de -1 dBTP:
  ```bash
  ffmpeg -i entrada.wav -af loudnorm=I=-23:TP=-1:LRA=11 -ar 48000 saida.wav
  ```
- Formato: WAV 44,1 kHz, 16 bits, mono, pico em −3 dBFS (o padrão dos sintetizados; ambiente em loop com bloco `smpl`);
  música em OGG. Arquivos pequenos: o repositório não usa LFS.

## Registrar (no mesmo commit)

Uma linha num `padelizou-arena/Padel.Godot/audio/CREDITOS.md` (crie no primeiro som baixado, com as mesmas
colunas do `modelos/CREDITOS.md`: item, arquivo, origem, autor, URL, licença, data). O `teste_sintetizar_sons.py` regenera
e compara os sintetizados: confira se ele aceita um arquivo que não é dele na pasta; se não, use `audio/baixados/`.
Som gerado por IA entra marcado e vai para a declaração (skill `declaracao-ia`).
