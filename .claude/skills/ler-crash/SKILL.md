---
name: ler-crash
description: 'Lê o que o Padelizou Arena deixa quando trava ou fecha sozinho (o log do Godot), acha a linha de código e transforma em Issue "[Playtest]" com o passo a passo. Use quando o jogo travar, fechar sozinho, um testador mandar log ou pasta de crash, ou o Felipe disser "crashou", "fechou do nada" ou "deu erro".'
---
# Ler um crash

## Onde fica

- Log do Godot (padrão do motor: os 5 mais recentes na pasta `logs/` da pasta do usuário). Windows:
  `%APPDATA%\Godot\app_userdata\Padelizou Arena\logs\`; Linux: `~/.local/share/godot/app_userdata/Padelizou Arena/logs/`
  (`padelizou-arena/docs/LEGAL.md`, 1.2 e 1.9).
- Os erros saem por `GD.PushError` e `GD.PrintErr`; não há tratador global de exceção. Exceção de C# não tratada aparece
  no log com a pilha.
- Os saves têm gravação atômica: perfil ilegível é guardado ao lado, nunca apagado; carreira de versão mais nova mostra aviso.
- Telemetria e marcador de queda (`docs/TELEMETRIA.md`) são desenho do M5: nada implementado.

## Passos

1. Junte os arquivos. Do PC do Felipe, leia direto. De um testador, peça a pasta zipada anexada à Issue "[Playtest]"
   (o modelo da Issue tem o campo de anexo). Anote a versão do jogo e a data.
2. Ache a primeira linha que é do jogo. Procure `ERROR:`, `SCRIPT ERROR:` e `Unhandled exception` no log (os mesmos padrões que o
   `ferramentas/rodar_sem_tela.sh` reprova). Na pilha de C#, a primeira linha de `Padel.Core` ou de `Padel.Godot/scripts`
   traz arquivo e linha. Erro do `Padel.Core` se reproduz sem tela: monte o teste xUnit com a mesma semente (`--semente N`).
3. Leia as 50 linhas de log antes do erro: o que o jogo estava fazendo (cena, modo, último comando).
4. Ligue ao código: abra o arquivo e a linha, e o `git log` daquele trecho perto da data da versão.
5. Reproduza com a skill `capturar` ou o `ferramentas/rodar_sem_tela.sh` com a mesma semente. Se reproduziu, o defeito vira teste visto falhar antes da correção.
6. Registre: Issue "[Playtest]" com gravidade "trava" ou "quebra a partida", o trecho da pilha, o arquivo e a linha, e
   como reproduzir. A correção segue a skill `triar-playtest`.

## Armadilhas

- O log traz o nome da sala e, na conexão por IP, o IP de quem hospeda (`docs/LEGAL.md`, C2): apague antes de colar.
- `leaked at exit` ou `still in use at exit` no fim do log é vazamento, não crash, mas o `rodar_sem_tela.sh` reprova igual.
- Log de testador pode ter nome de usuário e caminho do computador dele: não copie isso para a Issue pública.
