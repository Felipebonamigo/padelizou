# Pedido pro PC — a saída de parede (06/11/2026)

O que a sessão na nuvem não pôde fazer depois de a IA passar a esperar a saída de parede (`SaidaDeParedeTests`,
`REALISMO.md` → "A saída de parede se espera"). Nada aqui bloqueia o branch: o Core está em 710 testes verdes e o
`Padel.Godot` compila.

## 1. Jogar e decidir o rali (Felipe, 10 min)

A versão com a espera está no branch. O que mudou na mão: a IA Média e a Difícil não batem mais a bola colada no vidro —
deixam passar, esperam a saída a ~2 m do vidro e devolvem. Os ralis ficaram mais longos (IA × IA: Médio 10 → 17 s por
ponto, Difícil 16 → 29 s). A pergunta que só se responde jogando: **dá vontade de "mais uma", ou cansa?** Se cansar, as
alavancas são as de sempre (D10: `ChanceDeErro`, e a IA fechar mais o ponto com remate/víbora) — a sessão não mexeu nelas.

```bash
godot --path Padel.Godot -- --dificil --semente 7          # jogue um set contra a Difícil
godot --path Padel.Godot -- --auto --dificil --semente 7   # ou só assista: IA × IA
```

Com `--auto-golpe` (a raquete bate sozinha) a sua raquete também espera a saída em vez de bater na bola que passa. O
movimento assistido (Casual/Padrão) existe só no Core por enquanto — o `Padel.Godot` ainda não liga o nível de
assistência (`Configuracao.cs` não tem a opção) —, então o jogador sendo levado ao ponto da saída só se vê na fatia do
Unreal ou num teste (`O_humano_assistido_e_levado_a_saida_de_parede`).

## 2. Captura (skill `capturar`)

Uma foto do momento da espera — o jogador parado a ~2 m do vidro com a bola entre ele e o vidro, o rastro desenhado —, pra
ficar em `docs/imagens/saida-de-parede.png`. A sessão na nuvem não tem o Godot nem tela; o princípio do estúdio pede a
captura antes de se dizer que ficou bonito.

```bash
ferramentas/verificar_tudo.sh GODOT   # os 11 passos; na nuvem só rodaram o dotnet test e o dotnet build
```

## 3. Gabarito do PadelSim (`padelizou-game`)

A regra muda as partidas do gabarito (quem espera a parede bate em outro passo), então o CSV das partidas e o dos rastros
precisam ser regerados **e o PadelSim em C++ precisa da mesma regra** antes de voltar a bater bit a bit:

```bash
dotnet run --project padelizou-arena/ferramentas/Gabarito -c Release -- ../padelizou-game/Testes/PadelSim/Gabarito $(git rev-parse --short HEAD)
```

O que portar (tudo em `Padel.Core/Jogadores.cs`, classe `IA`, e uma linha em `Partida.Rally`):

- `Prever`: a contagem de quiques começa em 1 quando o árbitro já viu o quique (`QuicouNoReceptor`); cada candidato
  carrega `DepoisDaParede` (parede do nosso lado depois de um quique).
- `Planejar`: depois de escolher o primeiro candidato alcançável, se o perfil espera (`EsperaAParede`: todos menos o
  Fácil), o candidato não é voleio e está a ≥ 8 m ou já é depois da parede → `EsperaDaSaida`: candidato depois da parede
  com z ≥ 0,5, |y| ≤ 9, |x| ≤ 4, alguém chegando com folga ≥ 0,25 s além da reação; o primeiro com |y| ≤ 8, senão o de
  menor |y|. O plano guarda `EsperaAParede`.
- `Partida.Rally`: quem é o responsável do plano e está esperando não bate (`IA.EsperandoAParede`), nem no modo Automático.
- O `docs/ASSISTENCIA.md` de lá cita números do movimento assistido (15/10) que mudam com a espera: remedir com a
  `ferramentas/Calibracao` quando a parte 2 (o humano simulado esperando) entrar.
