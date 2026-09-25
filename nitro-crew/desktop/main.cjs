// Processo principal do Electron: janela do jogo, tela cheia, arquivos e integração opcional com Steamworks.
// O jogo em si é a pasta ../dist (Vite). Gamepads: nada a fazer aqui — a Gamepad API funciona no Chromium.
const { app, BrowserWindow, ipcMain, shell, dialog, clipboard } = require('electron');
const path = require('node:path');
const fs = require('node:fs/promises');
const storage = require('./storage.cjs');

// ───────────────────────────── Pasta de dados (Steam Cloud) ─────────────────────────────
// Fixa o userData em "<appData>/Nitro Crew" — %APPDATA%\Nitro Crew no Windows, ~/.config/Nitro Crew no Linux,
// ~/Library/Application Support/Nitro Crew no macOS —, em desenvolvimento (`npm start`) e no pacote. Sem isto
// o nome vinha do package.json ("nitro-crew-desktop"). O Steam Auto-Cloud aponta para `saves/` desta pasta
// (desktop/README.md): mudar o nome aqui perde o save de quem já joga. Tem que vir antes da trava de
// instância única, que também mora no userData.
const USER_DATA_DIR_NAME = 'Nitro Crew';
app.setPath('userData', path.join(app.getPath('appData'), USER_DATA_DIR_NAME));
const savesDir = () => path.join(app.getPath('userData'), 'saves');
const logsDir = () => path.join(app.getPath('userData'), 'logs');

/** Linha do log de erros para o que acontece fora da página (processo do jogo ou da GPU que caiu). */
function logProcessEvent(kind, details) {
  storage.appendLog(logsDir(), `[${new Date().toISOString()}] ${kind} ${JSON.stringify(details)}\n`);
}

// ───────────────────────────── Instância única ─────────────────────────────
// Um segundo clique no atalho só traz a janela existente para a frente (a Steam também abre o jogo assim).
if (!app.requestSingleInstanceLock()) {
  app.quit();
} else {
  app.on('second-instance', () => {
    const w = BrowserWindow.getAllWindows()[0];
    if (!w) return;
    if (w.isMinimized()) w.restore();
    w.focus();
  });
}

// ───────────────────────────── Steam (opcional) ─────────────────────────────
let sw = null;    // módulo steamworks.js
let steam = null; // cliente iniciado (null fora da Steam ou sem o pacote instalado)
try {
  // Precisa do steam_appid.txt ao lado do executável em desenvolvimento e do cliente Steam aberto.
  sw = require('steamworks.js');
  // O overlay (Shift+Tab) exige switches de linha de comando: tem que ser antes do `ready`.
  // `true` desliga o repintor por quadro — o jogo já desenha 60 vezes por segundo sozinho.
  if (typeof sw.electronEnableSteamOverlay === 'function') sw.electronEnableSteamOverlay(true);
} catch (e) {
  console.log('Steamworks indisponível (pacote não instalado):', e.message);
}

function initSteam() {
  if (!sw) return;
  try {
    steam = sw.init();
    console.log('Steam:', steam.localplayer.getName());
  } catch (e) {
    steam = null;
    console.log('Steamworks indisponível (rodando fora da Steam):', e.message);
  }
}

/** Rich Presence: a chave `status` é o texto que aparece na lista de amigos ("Correndo em Copacabana"). */
function setRichPresence(text) {
  if (!steam || typeof steam.localplayer.setRichPresence !== 'function') return;
  try { steam.localplayer.setRichPresence('status', text ? String(text) : null); } catch { /* ignorado */ }
}

// ───────────────────────────── Janela ─────────────────────────────
function createWindow() {
  const win = new BrowserWindow({
    width: 1600, height: 900, minWidth: 1024, minHeight: 640,
    title: 'Nitro Crew', backgroundColor: '#000', autoHideMenuBar: true, show: false,
    webPreferences: { preload: path.join(__dirname, 'preload.cjs'), contextIsolation: true, nodeIntegration: false, sandbox: true },
  });
  win.once('ready-to-show', () => { win.show(); });   // tela cheia é decidida pelo jogo (opções salvas)
  win.loadFile(path.join(__dirname, app.isPackaged ? 'app/index.html' : '../dist/index.html'));

  // Links externos abrem no navegador do sistema; a janela do jogo nunca navega para fora do próprio HTML.
  win.webContents.setWindowOpenHandler(({ url }) => {
    if (/^https?:/i.test(url)) shell.openExternal(url);
    return { action: 'deny' };
  });
  win.webContents.on('will-navigate', (e, url) => {
    if (!url.startsWith('file:')) { e.preventDefault(); if (/^https?:/i.test(url)) shell.openExternal(url); }
  });

  // F11 alterna tela cheia (o menu está oculto, então o atalho é tratado aqui, antes de chegar à página).
  win.webContents.on('before-input-event', (e, input) => {
    if (input.type === 'keyDown' && input.key === 'F11') { e.preventDefault(); win.setFullScreen(!win.isFullScreen()); }
  });
  win.on('enter-full-screen', () => win.webContents.send('fullscreen', true));
  win.on('leave-full-screen', () => win.webContents.send('fullscreen', false));

  // A página caiu (falta de memória, driver de vídeo): registra e recarrega uma vez, para o jogador voltar à
  // tela de título em vez de ficar olhando uma janela preta. Duas quedas em 30 s: não insiste (fica o log).
  let lastReload = 0;
  win.webContents.on('render-process-gone', (_e, details) => {
    logProcessEvent('render-process-gone', details);
    if (details.reason === 'clean-exit' || Date.now() - lastReload < 30_000) return;
    lastReload = Date.now();
    win.webContents.reload();
  });
  return win;
}

// ───────────────────────────── IPC (mesmos nomes do preload.cjs) ─────────────────────────────
ipcMain.handle('window:toggleFullscreen', (e) => { const w = BrowserWindow.fromWebContents(e.sender); if (w) w.setFullScreen(!w.isFullScreen()); });
ipcMain.handle('window:setFullscreen', (e, v) => { const w = BrowserWindow.fromWebContents(e.sender); if (w) w.setFullScreen(!!v); });
ipcMain.handle('window:isFullscreen', (e) => { const w = BrowserWindow.fromWebContents(e.sender); return w ? w.isFullScreen() : false; });
ipcMain.handle('window:quit', () => app.quit());

ipcMain.handle('steam:name', () => { try { return steam ? steam.localplayer.getName() : null; } catch { return null; } });
ipcMain.handle('steam:achievement', (_e, id) => {
  try { if (steam && typeof id === 'string' && id) return steam.achievement.activate(id) !== false; } catch { /* ignorado */ }
  return false;
});
ipcMain.handle('steam:richPresence', (_e, text) => { setRichPresence(text); });

// Arquivos: o jogo grava/lê progresso ou replays em `.nitro.json` na pasta Documentos (diálogo do sistema).
ipcMain.handle('file:save', async (e, name, content) => {
  const w = BrowserWindow.fromWebContents(e.sender);
  const base = path.basename(typeof name === 'string' && name ? name : 'nitro-crew');
  const file = base.endsWith('.nitro.json') ? base : `${base}.nitro.json`;
  const r = await dialog.showSaveDialog(w, {
    defaultPath: path.join(app.getPath('documents'), file),
    filters: [{ name: 'Nitro Crew', extensions: ['nitro.json', 'json'] }],
  });
  if (r.canceled || !r.filePath) return false;
  await fs.writeFile(r.filePath, String(content ?? ''), 'utf8');
  return true;
});
ipcMain.handle('file:open', async (e) => {
  const w = BrowserWindow.fromWebContents(e.sender);
  const r = await dialog.showOpenDialog(w, { properties: ['openFile'], filters: [{ name: 'Nitro Crew', extensions: ['nitro.json', 'json'] }] });
  if (r.canceled || r.filePaths.length === 0) return null;
  return fs.readFile(r.filePaths[0], 'utf8');
});

// Saves espelhados em arquivo (Steam Auto-Cloud) e log de erros — ver storage.cjs e src/game/cloudsave.ts.
ipcMain.handle('store:readAll', () => storage.readAllSaves(savesDir()));
ipcMain.handle('store:write', (_e, key, json) => storage.writeSave(savesDir(), key, json));
ipcMain.handle('log:append', (_e, text) => storage.appendLog(logsDir(), text));
// O preload roda em sandbox, onde o módulo `clipboard` não existe: a cópia do relatório passa por aqui.
ipcMain.handle('clipboard:write', (_e, text) => {
  if (typeof text !== 'string') return false;
  try { clipboard.writeText(text.slice(0, storage.MAX_SAVE_BYTES)); return true; } catch { return false; }
});

// Processo da GPU caiu (driver): o Chromium tenta de novo sozinho; fica registrado para o relatório.
app.on('child-process-gone', (_e, details) => { if (details.type === 'GPU') logProcessEvent('gpu-process-gone', details); });

// ───────────────────────────── Ciclo de vida ─────────────────────────────
app.whenReady().then(() => {
  initSteam();
  createWindow();
  app.on('activate', () => { if (BrowserWindow.getAllWindows().length === 0) createWindow(); });
});
app.on('window-all-closed', () => { if (process.platform !== 'darwin') app.quit(); });
app.on('before-quit', () => setRichPresence(null));
