// PUNHOS DE SHAOLIN — processo principal do Electron: a janela, o arquivo de progresso e a
// ponte pra Steam. O jogo em si (`../index.html` + `../js/`) não sabe que está no Electron: ele
// fala com `window.punhos`, que o `preload.js` expõe, e este arquivo atende do outro lado.
'use strict';
const { app, BrowserWindow, ipcMain } = require('electron');
const path = require('path');
const fs = require('fs');
const steam = require('./steam');

// Instância única: abrir o jogo duas vezes só traz a janela que já existe pra frente.
if (!app.requestSingleInstanceLock()) app.quit();

// Precisa vir ANTES do app ficar pronto — é o que deixa o overlay da Steam desenhar por cima.
steam.prepararOverlay();

function arquivoDeProgresso() { return path.join(app.getPath('userData'), 'progresso.json'); }

ipcMain.on('progresso:carregar', ev => {
    try { ev.returnValue = fs.existsSync(arquivoDeProgresso()) ? fs.readFileSync(arquivoDeProgresso(), 'utf8') : null; }
    catch (erro) { console.warn('progresso: não deu pra ler', erro.message); ev.returnValue = null; }
});
ipcMain.on('progresso:salvar', (ev, texto) => {
    // Escrita atômica: grava ao lado e renomeia. Um desligamento no meio nunca deixa o arquivo pela metade.
    try {
        const destino = arquivoDeProgresso();
        fs.mkdirSync(path.dirname(destino), { recursive: true });
        fs.writeFileSync(destino + '.tmp', texto, 'utf8');
        fs.renameSync(destino + '.tmp', destino);
    } catch (erro) { console.warn('progresso: não deu pra gravar', erro.message); }
});
ipcMain.on('steam:ativo', ev => { ev.returnValue = steam.ativo(); });
ipcMain.on('steam:conquistar', (ev, id) => steam.conquistar(id));
ipcMain.on('steam:estatistica', (ev, nome, valor) => steam.estatistica(nome, valor));
ipcMain.on('steam:presenca', (ev, texto) => steam.presenca(texto));
ipcMain.on('app:sair', () => app.quit());
ipcMain.on('app:telaCheia', (ev, ligar) => {
    const janela = BrowserWindow.fromWebContents(ev.sender);
    if (janela) janela.setFullScreen(ligar == null ? !janela.isFullScreen() : !!ligar);
});

let janela = null;
function criarJanela() {
    janela = new BrowserWindow({
        width: 1280, height: 720, minWidth: 960, minHeight: 540,
        backgroundColor: '#05030a', title: 'Punhos de Shaolin', autoHideMenuBar: true, show: false,
        icon: path.join(__dirname, 'icone.png'),
        webPreferences: { preload: path.join(__dirname, 'preload.js'), contextIsolation: true, nodeIntegration: false, sandbox: false },
    });
    janela.setMenuBarVisibility(false);
    janela.once('ready-to-show', () => janela.show());
    janela.webContents.on('before-input-event', (ev, entrada) => {
        if (entrada.type === 'keyDown' && entrada.key === 'F11') { janela.setFullScreen(!janela.isFullScreen()); ev.preventDefault(); }
    });
    janela.loadFile(path.join(__dirname, '..', 'index.html'));
}

app.on('second-instance', () => { if (janela) { if (janela.isMinimized()) janela.restore(); janela.focus(); } });
app.whenReady().then(() => { steam.iniciar(); criarJanela(); });
app.on('window-all-closed', () => app.quit());
app.on('will-quit', () => steam.encerrar());
