// PUNHOS DE SHAOLIN — processo principal do Electron: a janela, o arquivo de progresso e a
// ponte pra Steam. O jogo em si (`../index.html` + `../js/`) não sabe que está no Electron: ele
// fala com `window.punhos`, que o `preload.js` expõe, e este arquivo atende do outro lado.
//
// Endurecido pela checklist de segurança do Electron (a página é tratada como não confiável):
// servido por app:// com CSP em vez de file://, renderer em sandbox, navegação/janela/webview/
// permissão negadas, e todo IPC passa pelo porteiro `aceitar` (remetente + argumentos). Toda
// decisão — e toda E/S de disco — mora em `caminho-seguro.js`, que o `conferir-desktop-do-shaolin.js`
// chama de verdade no CI; este arquivo só liga os fios, e os fios são conferidos lá por texto.
'use strict';
const { app, BrowserWindow, ipcMain, protocol, session } = require('electron');
const path = require('path');
const steam = require('./steam');
const {
    servirDoJogo, validar, aceitar, bloquearForaDoJogo, bloquearWebview, salvarProgresso, carregarProgresso,
} = require('./caminho-seguro');

const RAIZ_DO_JOGO = path.join(__dirname, '..');

// Instância única: abrir o jogo duas vezes só traz a janela que já existe pra frente.
if (!app.requestSingleInstanceLock()) app.quit();

// Precisa vir ANTES do app ficar pronto. `standard` dá URL relativa e origem própria (app://jogo);
// `secure` faz da página um contexto seguro — a Gamepad API só existe em contexto seguro.
protocol.registerSchemesAsPrivileged([{ scheme: 'app', privileges: { standard: true, secure: true } }]);

// Também antes do ready — é o que deixa o overlay da Steam desenhar por cima.
steam.prepararOverlay();

// ── IPC ────────────────────────────────────────────────────────────────────────────────────
// A 1ª instrução de cada handler é o porteiro, com o próprio canal e o validador do canal; se ele
// recusa, `return` — nada executa. O progresso mora no userData (progresso.json + .bak).
ipcMain.on('progresso:carregar', ev => {
    if (!aceitar(ev, 'progresso:carregar', true, null)) return;
    ev.returnValue = carregarProgresso(app.getPath('userData'));
});
ipcMain.on('progresso:salvar', (ev, texto) => {
    if (!aceitar(ev, 'progresso:salvar', validar.textoDeProgresso(texto))) return;
    salvarProgresso(app.getPath('userData'), texto);
});
ipcMain.on('steam:ativo', ev => {
    if (!aceitar(ev, 'steam:ativo', true, false)) return;
    ev.returnValue = steam.ativo();
});
ipcMain.on('steam:conquistar', (ev, id) => {
    if (!aceitar(ev, 'steam:conquistar', validar.idDaSteam(id))) return;
    steam.conquistar(id);
});
ipcMain.on('steam:estatistica', (ev, nome, valor) => {
    if (!aceitar(ev, 'steam:estatistica', validar.idDaSteam(nome) && validar.numeroFinito(valor))) return;
    steam.estatistica(nome, valor);
});
ipcMain.on('steam:presenca', (ev, texto) => {
    if (!aceitar(ev, 'steam:presenca', validar.presenca(texto))) return;
    steam.presenca(texto);
});
ipcMain.on('app:sair', ev => {
    if (!aceitar(ev, 'app:sair', true)) return;
    app.quit();
});
ipcMain.on('app:telaCheia', (ev, ligar) => {
    if (!aceitar(ev, 'app:telaCheia', validar.ligarOpcional(ligar))) return;
    const janela = BrowserWindow.fromWebContents(ev.sender);
    if (janela) janela.setFullScreen(ligar == null ? !janela.isFullScreen() : ligar);
});

// ── Navegação, janelas e webview: a página não sai de app://jogo/ ──────────────────────────
// Vale pra TODO webContents que nascer, não só pra janela principal.
app.on('web-contents-created', (_, conteudo) => {
    conteudo.on('will-navigate', ev => bloquearForaDoJogo(ev));
    conteudo.on('will-redirect', ev => bloquearForaDoJogo(ev));
    conteudo.on('will-attach-webview', ev => bloquearWebview(ev));
    conteudo.setWindowOpenHandler(({ url }) => {
        console.warn(`janela nova bloqueada: ${url}`);
        return { action: 'deny' };
    });
});

// Nenhuma permissão web (câmera, microfone, notificação, geolocalização, tela cheia do DOM…):
// o jogo não usa nenhuma. Tela cheia vai pelo IPC (`app:telaCheia`), que o main controla.
function negarPermissoes() {
    session.defaultSession.setPermissionRequestHandler((conteudo, permissao, responder) => {
        console.warn(`permissão negada: ${permissao}`);
        responder(false);
    });
    // Sem log: a checagem roda a toda hora e só consulta — negar é a resposta inteira.
    session.defaultSession.setPermissionCheckHandler(() => false);
}

let janela = null;
function criarJanela() {
    janela = new BrowserWindow({
        width: 1280, height: 720, minWidth: 960, minHeight: 540,
        backgroundColor: '#05030a', title: 'Punhos de Shaolin', autoHideMenuBar: true, show: false,
        icon: path.join(__dirname, 'icone.png'),
        webPreferences: {
            preload: path.join(__dirname, 'preload.js'),
            sandbox: true,
            contextIsolation: true,
            nodeIntegration: false,
            webSecurity: true,
            spellcheck: false,
            devTools: !app.isPackaged,
        },
    });
    janela.setMenuBarVisibility(false);
    janela.once('ready-to-show', () => janela.show());
    janela.webContents.on('before-input-event', (ev, entrada) => {
        if (entrada.type === 'keyDown' && entrada.key === 'F11') { janela.setFullScreen(!janela.isFullScreen()); ev.preventDefault(); }
    });
    janela.loadURL('app://jogo/index.html');
}

app.on('second-instance', () => { if (janela) { if (janela.isMinimized()) janela.restore(); janela.focus(); } });
app.whenReady().then(() => {
    negarPermissoes();
    protocol.handle('app', pedido => servirDoJogo(RAIZ_DO_JOGO, pedido));
    steam.iniciar();
    criarJanela();
});
app.on('window-all-closed', () => app.quit());
app.on('will-quit', () => steam.encerrar());
