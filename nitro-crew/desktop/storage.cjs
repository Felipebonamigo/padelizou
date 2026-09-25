// Arquivos do jogo dentro do userData do Electron: os saves (espelho do localStorage, para o Steam
// Auto-Cloud sincronizar arquivos de verdade em vez do LevelDB do Chromium) e o log de erros.
// Não depende do Electron: main.cjs passa as pastas, e tests/desktop-storage.test.ts usa uma pasta temporária.
// Tudo síncrono de propósito: arquivos pequenos, e uma gravação termina antes de o próximo IPC (ex.: "sair")
// ser atendido — um app.quit() nunca corta um save pela metade.
const fs = require('node:fs');
const path = require('node:path');

/** `nitro-crew.<nome>` (ex.: nitro-crew.save, nitro-crew.settings): só letras, números, `_`, `-` e pontos entre eles. */
const KEY_RE = /^nitro-crew\.[A-Za-z0-9_-]+(?:\.[A-Za-z0-9_-]+)*$/;
const MAX_KEY_LENGTH = 80;
/** Um save maior que isto é lixo ou ataque; o progresso real tem poucos KB. */
const MAX_SAVE_BYTES = 1024 * 1024;
/** Log de erros: passou disto, o atual vira errors.1.log (o anterior é descartado). Máximo em disco ≈ 2×. */
const LOG_MAX_BYTES = 512 * 1024;
/** Uma entrada de log nunca passa disto (pilhas enormes são cortadas). */
const LOG_ENTRY_MAX_CHARS = 16 * 1024;

function isSaveKey(key) {
  return typeof key === 'string' && key.length <= MAX_KEY_LENGTH && KEY_RE.test(key);
}

/**
 * Lê `<dir>/<chave>.json` de todas as chaves válidas: { chave: texto JSON }. Pasta ausente = {}.
 * Arquivo que existe mas não deu para ler (permissão, erro de disco, pasta com nome de save) vem como `null` —
 * "não sei o que tem", e a inicialização não grava por cima dele. Arquivo que sumiu entre a listagem e a
 * leitura fica de fora (não há arquivo); maior que MAX_SAVE_BYTES também (é lixo, pode ser substituído).
 */
function readAllSaves(dir) {
  const out = {};
  let names;
  try { names = fs.readdirSync(dir); } catch { return out; }
  for (const name of names) {
    if (!name.endsWith('.json')) continue;
    const key = name.slice(0, -'.json'.length);
    if (!isSaveKey(key)) continue;
    const file = path.join(dir, name);
    try {
      const st = fs.statSync(file);
      if (!st.isFile()) { out[key] = null; continue; }
      if (st.size > MAX_SAVE_BYTES) continue;
      out[key] = fs.readFileSync(file, 'utf8');
    } catch (e) {
      if (!e || e.code !== 'ENOENT') out[key] = null;
    }
  }
  return out;
}

/**
 * Grava `<dir>/<chave>.json` de forma atômica (arquivo temporário + rename): um corte de energia deixa o
 * save antigo ou o novo, nunca meio arquivo. Recusa chave inválida e texto que não é JSON — nunca grava
 * lixo por cima de um save bom. Devolve falso em qualquer falha.
 */
function writeSave(dir, key, json) {
  if (!isSaveKey(key) || typeof json !== 'string' || Buffer.byteLength(json, 'utf8') > MAX_SAVE_BYTES) return false;
  try { JSON.parse(json); } catch { return false; }
  try {
    fs.mkdirSync(dir, { recursive: true });
    const file = path.join(dir, `${key}.json`);
    const tmp = `${file}.tmp`;
    fs.writeFileSync(tmp, json, 'utf8');
    fs.renameSync(tmp, file);
    return true;
  } catch {
    return false;
  }
}

/** Acrescenta `text` a `<dir>/errors.log`, girando para errors.1.log quando passaria de `maxBytes`. */
function appendLog(dir, text, maxBytes = LOG_MAX_BYTES) {
  if (typeof text !== 'string' || text.length === 0) return false;
  const chunk = text.length > LOG_ENTRY_MAX_CHARS ? `${text.slice(0, LOG_ENTRY_MAX_CHARS)}…\n` : text;
  try {
    fs.mkdirSync(dir, { recursive: true });
    const file = path.join(dir, 'errors.log');
    let size = 0;
    try { size = fs.statSync(file).size; } catch { size = 0; }
    if (size > 0 && size + Buffer.byteLength(chunk, 'utf8') > maxBytes) fs.renameSync(file, path.join(dir, 'errors.1.log'));
    fs.appendFileSync(file, chunk, 'utf8');
    return true;
  } catch {
    return false;
  }
}

module.exports = { isSaveKey, readAllSaves, writeSave, appendLog, LOG_MAX_BYTES, MAX_SAVE_BYTES, LOG_ENTRY_MAX_CHARS };
