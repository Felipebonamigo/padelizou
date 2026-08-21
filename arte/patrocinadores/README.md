# Arte original dos patrocinadores

Os arquivos que a marca mandou, como ela mandou. **Fora do `wwwroot` de propósito**: ali dentro
tudo é servido publicamente e entra em todo pacote de deploy — 4,4 MB de `.ai` e `.pdf` baixáveis
por qualquer um, em cada versão instalada no servidor. Aqui eles ficam versionados e fora do ar.

O que o site serve são só os `.webp` leves em `Padelizou/wwwroot/image/patrocinadores/`, gerados a
partir daqui. Quem exibe é a faixa do rodapé — ver `Padelizou/Services/PatrocinadoresSettings.cs`.

## ⚠️ A regra que motivou esta pasta

**Arte de patrocinador é a que ELE mandou.** Em 20/08/2026 um logo foi recriado à mão porque a
imagem tinha chegado colada na conversa em vez de arquivo; ficou parecido, e o Felipe reparou na
hora. Logo é identidade registrada. Sem o original em mãos, o patrocinador não entra na lista —
há um teste que trava isso (`So_entra_na_lista_quem_mandou_a_arte`).

Guardar o kit aqui é o que torna a regra praticável: a próxima pessoa que precisar de outra
variante, outro tamanho ou outro formato tem o material oficial no repositório.

## Grand Padel

Kit `Logos Grand Padel Pos-Neg-Mon`, em RGB e CMYK (`.ai`, `.pdf`, `.jpg`, `.png`). Para tela usa-se
o **RGB**; o CMYK é para impressão e não serve para web.

Das seis variantes, o site usa duas — escolha do Felipe:

| Variante | Onde | Vira |
|---|---|---|
| `RGB/PNG/...Pos-Neg-Mon-01.png` | tema claro | `wwwroot/image/patrocinadores/grand-padel.webp` |
| `RGB/PNG/...Pos-Neg-Mon-03.png` | tema escuro | `wwwroot/image/patrocinadores/grand-padel-branco.webp` |

A **-01** é a colorida (azul + verde) e a **-03** é a negativa toda branca. São dois ARQUIVOS, e não
um com filtro: o logo é colorido, e `invert()` em azul dá laranja — ver o comentário de
`Patrocinador.ImagemEscura`.

## Como gerar o `.webp` que o site serve

Altura fixa de **96px** (3,7× os 26px de exibição, o suficiente para tela retina), recortado pelo
alpha para não sobrar folga transparente em volta:

```python
from PIL import Image
im = Image.open(origem).convert("RGBA")
im = im.crop(im.getchannel("A").getbbox())          # tira a folga transparente
im = im.resize((round(im.width * 96 / im.height), 96), Image.LANCZOS)
im.save(destino, "WEBP", quality=92, method=6)
```

O Paralelo (`paralelo.webp`) saiu do `logo_paralelo.pdf` pelo mesmo caminho, rasterizado antes com
PyMuPDF a 4×. O PDF original dele não está aqui: veio como anexo de conversa, não pelo repositório.
