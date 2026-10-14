---
name: modelo-3d
description: Procurar, registrar e reaproveitar modelos 3D entre os jogos do estúdio, com as regras do Padelizou Arena (só CC0 da comunidade, nada gerado, tetos de tamanho e de triângulos). Use antes de baixar ou trocar qualquer modelo, textura ou animação do Padelizou Arena, e sempre que um modelo novo entrar no jogo.
---
# Modelo 3D: procurar antes, registrar depois

Os jogos do Felipe dividem um acervo de modelos. Esta rotina evita baixar duas vezes, gastar crédito à toa e perder a
licença de um arquivo. O roteiro completo da busca na comunidade do Meshy está em
`padelizou-arena/docs/MODELOS-DA-COMUNIDADE.md`.

## Antes de baixar

1. Procure na aba "Modelos 3D" do doc do estúdio (https://claude.ai/artifact/72KXW2Tve5gB65BHD1CDp6), pelo conector do
   Claude Docs; nunca por WebFetch.
2. Procure no repositório `Felipebonamigo/acervo-3d`, no `catalogo.csv` (colunas `arquivo`, `categoria`, `titulo`,
   `fonte`, `autor`, `licenca`, `url`, `usado_em`). Se ele não estiver na sessão, anexe com `add_repo`.
3. Confira se serve ao Arena: realismo de transmissão (`padelizou-arena/REALISMO.md`). A quadra (vidro, grade, portas) é
   procedural e sai de `Quadra.Paineis`, a mesma lista que a física usa: não baixe quadra inteira, só o que fica em volta
   e o que está na mão do jogador.
4. Se nada servir, procure pronto: a comunidade do Meshy (pela sessão local com navegador, porque a nuvem não alcança o
   site), Sketchfab (a chave vem pelo proxy em `api.sketchfab.com`), Poly Haven, Kenney, Quaternius.
5. **Não gere nada** no Meshy (nem Text to 3D, Image to 3D, retexturização ou remesh): pedido do Felipe para o Arena.
   Login, crédito, assinatura ou termo novo: pare e pergunte.

## Regras do Arena

- Licença: só **CC0** (os termos do Meshy põem a comunidade em CC0; o §2.4 proíbe remover o metadado de IA).
- Nada de marca nem de pessoa real: raquete com logo, roupa com marca, logo da FIP ou do Premier Padel, rosto ou corpo de
  atleta reconhecível. Na dúvida, pula.
- GLB com textura 2K, até 15 MB por arquivo e 120 MB no total (o repositório não usa LFS).
- Tetos de triângulos: raquete 8k, bola 2k, jogador 40k, cenário 15k, arquibancada 60k. A redução é feita na sessão
  local, com glTF-Transform e meshoptimizer.
- Unidades em metro, Y para cima. Escala errada se corrige no nó da vitrine, nunca editando o arquivo; anote o fator.

## Depois que o modelo entrou (no mesmo dia)

1. No jogo: o arquivo em `padelizou-arena/Padel.Godot/modelos/<item>/<nome>.glb`, o print da licença em
   `modelos/<item>/licenca.png` e a linha em `padelizou-arena/Padel.Godot/modelos/CREDITOS.md` (Item, Arquivo, Modelo,
   Autor como na página, URL, Licença, Download, Triângulos original → arquivo, MB, Escala original → real, Rigado). O
   `.glb.import` fica com `gltf/embedded_image_handling=2`.
2. Antes do commit: `npx @gltf-transform/cli inspect` no arquivo, `--import` sem `ERROR:`, print da vitrine
   (skill `capturar`) e a skill `verificar`.
3. No acervo (`Felipebonamigo/acervo-3d`, público): o arquivo em `<categoria>/<fonte>/`, a linha no `catalogo.csv` com
   `usado_em` = Padelizou Arena e, se for o caso, a linha em `CREDITOS.md`. Nada acima de 50 MB.
4. No doc do estúdio: a linha na tabela da categoria certa da aba "Modelos 3D", com o menu "Licença" preenchido.
5. Conteúdo de IA que o jogador vê entra na declaração da Steam (skill `declaracao-ia`).
