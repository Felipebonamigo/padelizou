// Ponte segura entre a página do jogo e o processo principal (janela, Steam, arquivos).
// O lado TypeScript disso é src/game/desktop.ts (interface DesktopApi) — os dois têm que andar juntos.
const { contextBridge, ipcRenderer } = require('electron');
contextBridge.exposeInMainWorld('desktop', {
  toggleFullscreen: () => ipcRenderer.invoke('window:toggleFullscreen'),
  setFullscreen: (v) => ipcRenderer.invoke('window:setFullscreen', v),
  isFullscreen: () => ipcRenderer.invoke('window:isFullscreen'),
  quit: () => ipcRenderer.invoke('window:quit'),
  steamName: () => ipcRenderer.invoke('steam:name'),
  achievement: (id) => ipcRenderer.invoke('steam:achievement', id),
  richPresence: (text) => ipcRenderer.invoke('steam:richPresence', text),
  saveFile: (name, content) => ipcRenderer.invoke('file:save', name, content),
  openFile: () => ipcRenderer.invoke('file:open'),
  storeReadAll: () => ipcRenderer.invoke('store:readAll'),
  storeWrite: (key, json) => ipcRenderer.invoke('store:write', key, json),
  logAppend: (text) => ipcRenderer.invoke('log:append', text),
  copyText: (text) => ipcRenderer.invoke('clipboard:write', text),
  onFullscreen: (cb) => { ipcRenderer.on('fullscreen', (_e, v) => cb(v)); },
});
