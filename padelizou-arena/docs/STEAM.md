# Steam — do build ao botão de comprar

O que já está pronto no repositório e o que só o Felipe pode fazer, em ordem.
Regras e valores da Valve mudam: confira cada passo no painel do Steamworks antes de agir.

## Já pronto (verificado em 25/09/2026)

- `Padel.Godot/export_presets.cfg` — presets **Linux** e **Windows**, x86_64, .NET.
- Build Linux exportado e **rodado** sem editor (`build/linux/PadelizouArena.x86_64 --headless -- --auto`); build Windows exportado daqui mesmo (`PE32+ x86-64`, com `coreclr.dll`), não executado — não há Windows no container.
- CI: o job **Exportar builds** gera os dois e publica como artefato no `main`, em tag, ou por *Run workflow*.
- `steam/app_build.vdf` + `steam/publicar.sh` — sobem os dois depots com o `steamcmd`. Falta o AppID (o script recusa rodar com AppID 0).

Exportar na máquina de desenvolvimento (Godot 4.7.2 .NET + templates instalados):

```bash
godot --headless --path Padel.Godot --import
godot --headless --path Padel.Godot --export-release "Linux"   ../build/linux/PadelizouArena.x86_64
godot --headless --path Padel.Godot --export-release "Windows" ../build/windows/PadelizouArena.exe
```

## O que só o Felipe faz

1. **Conta Steamworks** (partner.steamgames.com): cadastro da empresa ou pessoa física, entrevista fiscal (formulário de não residente nos EUA), conta bancária, verificação de identidade, e a taxa do **Steam Direct** (US$ 100 por jogo, devolvida depois de US$ 1.000 em receita).
2. **Criar o app** → sai o **AppID**. Criar **dois depots** (Windows e Linux) e marcar o sistema de cada um. Preencher os três números em `steam/app_build.vdf`.
3. **Opções de inicialização** no painel: Windows → `PadelizouArena.exe`; Linux → `PadelizouArena.x86_64`.
4. **Conta de build separada** (nunca a pessoal), só com permissão de subir build, e Steam Guard autorizado na máquina que publica.
5. Publicar: `STEAM_USUARIO=conta_de_build steam/publicar.sh`, depois no painel colocar o build no branch **beta** e testar pelo cliente Steam. Branch **default** só na mão, no dia de lançar.
6. **Página "Em breve"** (marco M4): cápsulas, screenshots, trailer, descrição em PT/EN/ES. A Valve exige a página visível por um período mínimo antes do lançamento e um intervalo depois do pagamento da taxa — confira os prazos atuais no Steamworks e conte com eles no cronograma.
7. **Steam Deck**: pedir a revisão de compatibilidade quando o build candidato tiver controle completo e texto legível a 1280×800. O build Linux é nativo, então o Deck não depende do Proton.

## Integração com o SDK (marco M2)

Decisão D5: Facepunch.Steamworks, atrás de uma interface de transporte. No desenvolvimento, usar o AppID de teste **480** (Spacewar) até existir o nosso. Entram: lobby e convite de amigo, Steam Datagram Relay, conquistas, Rich Presence, Cloud (perfil e carreira) e Steam Input.
