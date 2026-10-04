# Créditos dos modelos 3D

Modelos baixados da comunidade da Meshy em 04/10/2026 (roteiro: `docs/MODELOS-DA-COMUNIDADE.md`). A tela de
créditos do jogo vai ler esta lista quando existir.

## Licença: CC0, não CC BY 4.0

O roteiro pedia CC BY 4.0. Na comunidade da Meshy isso não existe: pelos Termos de Uso da Meshy (versão de
19/09/2026, §3.3, "Community License"), **todo modelo 3D publicado na comunidade é CC0 1.0** (domínio público). Os
cinco modelos abaixo mostram "CC0" na página, e o print de cada página está em `<item>/licenca.png`. O CC0 permite uso
comercial e não exige crédito. O Felipe aceitou CC0 no lugar da regra 1 em 04/10/2026. O crédito fica aqui mesmo
assim.

Os mesmos termos (§2.4) proíbem remover marca d'água ou metadado que identifique conteúdo gerado por IA. Os originais
baixados não têm nenhum: o JSON do glTF não tem `extras`, extensões nem menção à Meshy, e os binários não trazem
C2PA, XMP nem Exif. O `asset.generator` era `pygltflib` e virou `glTF-Transform` na redução.

## Redução de polígonos

Os modelos da comunidade são gerados por IA e vêm com 160 mil a 900 mil triângulos. A Meshy não oferece versão leve
no download, só o remesh por IA, que o roteiro proíbe. Com aprovação do Felipe (04/10/2026), cada original foi
reduzido **localmente**, sem IA, até o teto da regra 6:

- `weld` + `dedup` + `simplify` do glTF-Transform 4.5.1, que usa o meshoptimizer (algoritmo determinístico de
  colapso de arestas).
- Texturas limitadas a 2048×2048 com o sharp.
- Script: `reduzir.mjs` (fora do repositório).

Os triângulos "antes" e "depois" foram medidos no arquivo. O original não entra no git.

A Meshy entrega todo modelo normalizado num cubo de ~1,9 de lado, centrado na origem. A "escala" é o fator que leva
à medida real. A cena `cenas/VitrineDeModelos.tscn` aplica esse fator no nó, e o arquivo não é editado para isso.

No `.glb.import` de cada modelo, `gltf/embedded_image_handling=2` (embutir como Basis Universal). O padrão do
Godot extrai as texturas para `.jpg` ao lado do GLB, o que duplicava 4 MB no histórico. Isso não pode voltar a 1.

## Baixados

| Item | Arquivo | Modelo | Autor (como na página) | URL | Licença | Download | Triângulos (original → arquivo) | MB | Escala original → real | Rigado |
|---|---|---|---|---|---|---|---|---|---|---|
| Raquete | `raquete/raquete-furada.glb` | Padel Racket Charm | jgmunoz00 | https://www.meshy.ai/3d-models/Padel-Racket-Charm-01975f85-063a-7356-a632-e61f6da3e3f8 | CC0 1.0 | 04/10/2026 | 9.998 → 7.896 | 0,64 | 1,99 de comprimento → 45,5 cm (× 0,2284) | não |
| Bola | `bola/bola-feltro.glb` | Tennis Ball | Yoana | https://www.meshy.ai/3d-models/Tennis-Ball-019b9270-fe26-7ef6-9567-cb999a794325 | CC0 1.0 | 04/10/2026 | 382.996 → 1.940 | 0,38 | Ø 1,89 → Ø 6,7 cm (× 0,0354) | não |
| Jogador (feminino) | `jogador/atleta-feminina.glb` | Young Athlete | levass.alex | https://www.meshy.ai/3d-models/Young-Athlete-019e358e-31c0-7a5e-a266-cf5255bca5a5 | CC0 1.0 | 04/10/2026 | 164.384 → 38.799 | 2,01 | 1,90 de altura → 1,70 m (× 0,8952) | não (T-pose) |
| Banco dos jogadores | `banco/banco-de-praca.glb` | park bench | Smuttheir | https://www.meshy.ai/3d-models/park-bench-0199c9cf-7698-796a-a4c2-01e6988b1d65 | CC0 1.0 | 04/10/2026 | 217.687 → 14.548 | 2,25 | 1,90 de comprimento → 2,0 m (× 1,0504) | não |
| Torre de luz | `torre-de-luz/refletor-quadruplo.glb` | Quad-Panel Modular Floodlight Concept | doganayalty | https://www.meshy.ai/3d-models/QuadPanel-Modular-Floodlight-Concept-01a02a58-0f47-7567-ad23-f98ce19394bd | CC0 1.0 | 04/10/2026 | 904.656 → 14.548 | 1,30 | 1,90 de altura → 10 m (× 5,263) | não |

Total no repositório: **6,58 MB** (limite: 15 MB por arquivo, 120 MB no total).

## Alternativas (não baixadas, ou baixadas e recusadas)

O autor das duas primeiras raquetes saiu da ordem dos cards na busca "padel". A página desses modelos não foi aberta
de novo para conferir.

| Item | Modelo | Autor | URL | Por que não |
|---|---|---|---|---|
| Raquete | Palladium Padel Racket | jadodiguerfi411 | https://www.meshy.ai/3d-models/Palladium-Padel-Racket-019ce9aa-3c7c-748d-bc19-4fdb0ccee3d2 | 267.119 faces e de perfil. A Charm cabia no teto quase sem perda. |
| Raquete | Kona One padel racket (2 versões) | tauanemiranda2014 | https://www.meshy.ai/3d-models/Kona-One-padel-racket-019f476b-5301-77d6-9932-b8550ce03d17 · https://www.meshy.ai/3d-models/Kona-One-padel-racket-019f3cd7-4cbe-7660-82e6-29c957d26713 | Sem textura (cinza) e com 64.894 e 101.948 faces. O nome é de marca (Kona), recusado pela regra 2. |
| Raquete | Padel Racket Slam, Adidas Metalbone, outras da busca "padel" | vários | — | Logo de marca na face (regra 2). |
| Bola | — | — | — | A única outra bola da busca é um pássaro em forma de bola (cartoon). |
| Jogador | T-Pose Athlete | lingshicong | https://www.meshy.ai/3d-models/TPose-Athlete-019eafa4-f425-7c18-bc2e-4cb33d56ec31 | Logo da Mizuno na camiseta e no short (regra 2) e rosto de anime. |
| Jogador | Tennis Pose | 954655103 | https://www.meshy.ai/3d-models/Tennis-Pose-019a4380-df85-790e-84d6-7bebc81263b6 | Rigado, mas segura uma raquete de **tênis** (com cordas) fundida à mão, e tem 462.809 faces. |
| Rede | Tennis net | aledisalva | https://www.meshy.ai/3d-models/Tennis-net-019e335e-2ed4-7134-9a67-1e0347823e2a | **Baixada e recusada.** Os 280.658 triângulos só caíram para 55.970 (teto 15 mil), porque a malha da rede não colapsa. A rede procedural da quadra continua. |
| Cadeira do árbitro | Lifeguard Stand | histel.nathanpro | https://www.meshy.ai/3d-models/Lifeguard-Stand-019f4884-2d10-7690-b4e1-c889c7161516 | Cartoon, com placa "LIFEGUARD" e bandeira. A busca "umpire" só devolve vampiros. |
| Torre de luz | Construction site floodlight | ndals0806 (lido num print pequeno, pode ter erro de grafia) | https://www.meshy.ai/3d-models/30x30x40cmConstruction-site-floodlight-halogen-lamp-yellow-stand-dirty-glass-industrial-lighting-prop-019bd07e-9fe1-7ba2-bf0b-ef973ab5a9d2 | Refletor de obra em tripé (439.116 faces), não torre de estádio. |

## Não encontrados

- **Jogador masculino:** nenhum realista, sem marca e de roupa esportiva. As buscas "athlete" e "tennis" só trazem
  anime ou cartoon, homem sem camisa, ou modelos com logo.
- **Rigado:** nenhum jogador realista rigado. A atleta feminina vem em T-pose, sem esqueleto.
- **Cadeira do árbitro:** não há (ver Alternativas).
- **Placar LED:** a busca "scoreboard" só traz placar cartoon ou de Roblox.
- **Arquibancada:** "bleachers", "bleacher" e "grandstand" voltaram vazias em 4 tentativas, e "stadium" só traz
  estádios inteiros.
- **Miudezas:** não procuradas. O orçamento de MB sobrou (6,58 de 120), mas o tempo foi para os itens 1 a 9.

A busca da comunidade da Meshy é instável: a mesma palavra às vezes volta vazia e na tentativa seguinte traz
resultado. Busca com duas palavras ("tennis ball", "tennis player") nunca trouxe nada, então use uma palavra só.
Abrir a página de um modelo pela URL direta dá 403. É preciso chegar clicando no card dentro do site.
