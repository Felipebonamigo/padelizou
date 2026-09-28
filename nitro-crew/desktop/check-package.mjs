// Confere o CONTEÚDO de um pacote gerado pelo electron-builder, sem abri-lo — serve para as builds que não dá
// para executar aqui (Windows, macOS) e complementa o e2e.mjs (que abre a do Linux).
// Uso, dentro de desktop/ e depois de um `npm run dist:*`:
//   node check-package.mjs [release/win-unpacked | release/linux-unpacked | release/mac | release/mac-arm64 ...]
// Sem argumento, confere toda pasta de pacote que existir em release/. Sai com 1 se algo falhar.
// Usa só o que o electron-builder já instala (@electron/asar, resedit, pe-library): nenhuma dependência nova.
import { createRequire } from 'node:module';
import { existsSync, lstatSync, readFileSync, readdirSync, statSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const require = createRequire(import.meta.url);
const asar = require('@electron/asar');
const pkg = JSON.parse(readFileSync(join(here, 'package.json'), 'utf8'));
const PRODUCT = pkg.productName;

const fails = [];
const warns = [];
const check = (cond, msg) => { console.log(`  ${cond ? '✓' : '✗'} ${msg}`); if (!cond) fails.push(msg); };
const warn = (msg) => { console.log(`  ! ${msg}`); warns.push(msg); };

/** Tamanho em bytes de uma pasta (sem seguir links). */
function du(p) {
  const st = lstatSync(p, { throwIfNoEntry: false });
  if (!st) return 0;
  if (!st.isDirectory()) return st.size;
  return readdirSync(p).reduce((s, n) => s + du(join(p, n)), 0);
}
const mb = (b) => `${(b / 1024 / 1024).toFixed(0)} MB`;

/** Plataforma pelo nome da pasta que o electron-builder cria. */
function layout(dir) {
  const name = dir.split(/[\\/]/).filter(Boolean).pop();
  if (name.startsWith('win')) return { os: 'win', exe: join(dir, `${PRODUCT}.exe`), resources: join(dir, 'resources'), exeDir: dir };
  if (name.startsWith('linux')) return { os: 'linux', exe: join(dir, pkg.build.linux.executableName), resources: join(dir, 'resources'), exeDir: dir };
  if (name.startsWith('mac')) {
    const app = join(dir, `${PRODUCT}.app`, 'Contents');
    return { os: 'mac', exe: join(app, 'MacOS', PRODUCT), resources: join(app, 'Resources'), exeDir: join(app, 'MacOS'), contents: app };
  }
  return null;
}

/** Arquivos nativos do steamworks.js que cada sistema carrega. */
const STEAM_NATIVE = {
  win: ['dist/win64/steam_api64.dll', 'dist/win64/steamworksjs.win32-x64-msvc.node'],
  linux: ['dist/linux64/libsteam_api.so', 'dist/linux64/steamworksjs.linux-x64-gnu.node'],
  mac: ['dist/osx/libsteam_api.dylib', 'dist/osx/steamworksjs.darwin-x64.node', 'dist/osx/steamworksjs.darwin-arm64.node'],
};

function checkWinResources(exe) {
  const PE = require('pe-library');
  const ResEdit = require('resedit');
  const bin = PE.NtExecutable.from(readFileSync(exe), { ignoreCert: true });
  const res = PE.NtExecutableResource.from(bin);
  const [vi] = ResEdit.Resource.VersionInfo.fromEntries(res.entries);
  const lang = vi?.getAllLanguagesForStringValues()[0];
  const s = lang ? vi.getStringValues(lang) : {};
  check(s.ProductName === PRODUCT && s.FileDescription === PRODUCT, `Propriedades do .exe: nome "${s.ProductName}"`);
  check(s.FileVersion === pkg.version, `Propriedades do .exe: versão ${s.FileVersion}`);
  check(s.LegalCopyright === pkg.build.copyright, `Propriedades do .exe: ${s.LegalCopyright}`);
  // O .ico do build/ entra byte a byte no grupo de ícones; o ícone padrão do Electron não bate.
  const icoFile = join(here, 'build', 'icon.ico');
  if (!existsSync(icoFile)) { check(false, 'build/icon.ico existe (npm run icon)'); return; }
  const ico = ResEdit.Data.IconFile.from(readFileSync(icoFile));
  const [group] = ResEdit.Resource.IconGroupEntry.fromEntries(res.entries);
  const bytes = (item) => Buffer.from(item.isRaw() ? item.bin : item.generate());
  const embedded = group ? group.getIconItemsFromEntries(res.entries).map(bytes) : [];
  const wanted = ico.icons.map((i) => bytes(i.data));
  const same = embedded.length === wanted.length && wanted.every((w) => embedded.some((e) => e.equals(w)));
  check(same, `ícone do .exe é o de build/icon.ico (${embedded.length} tamanhos embutidos, ${wanted.length} no .ico)`);
}

function checkMacBundle(l) {
  const plist = readFileSync(join(l.contents, 'Info.plist'), 'utf8');
  const val = (key) => plist.match(new RegExp(`<key>${key}</key>\\s*<string>([^<]*)</string>`))?.[1];
  check(val('CFBundleShortVersionString') === pkg.version, `Info.plist: versão ${val('CFBundleShortVersionString')}`);
  check(val('CFBundleIdentifier') === pkg.build.appId, `Info.plist: ${val('CFBundleIdentifier')}`);
  check(val('NSHumanReadableCopyright') === pkg.build.copyright, `Info.plist: ${val('NSHumanReadableCopyright')}`);
  console.log(`  · macOS mínimo (do Electron): ${val('LSMinimumSystemVersion')}`);
  const icns = join(here, 'build', 'icon.icns');
  const inApp = join(l.resources, 'icon.icns');
  check(existsSync(icns) && existsSync(inApp) && readFileSync(icns).equals(readFileSync(inApp)), 'ícone do .app é o de build/icon.icns');
  warn('pacote macOS sem assinatura nem notarização (só num Mac: desktop/README.md → macOS)');
}

function checkDir(dir) {
  const l = layout(dir);
  console.log(`\n${dir}`);
  if (!l) { check(false, `pasta desconhecida (esperado win-*, linux-*, mac*): ${dir}`); return; }
  check(existsSync(l.exe), `executável ${l.exe.slice(dir.length + 1)}`);
  const archive = join(l.resources, 'app.asar');
  check(existsSync(archive), 'resources/app.asar');
  if (!existsSync(archive)) return;
  const files = asar.listPackage(archive, { isPack: false }).map((f) => f.replace(/\\/g, '/'));
  const has = (f) => files.includes(f);
  check(has('/app/index.html'), 'jogo em app/index.html (dentro do asar)');
  check(files.some((f) => /^\/app\/assets\/index-[^/]+\.js$/.test(f)), 'app/assets/index-*.js');
  check(!files.some((f) => f.endsWith('.map')), 'nenhum .map no pacote');
  for (const f of ['main.cjs', 'preload.cjs', 'storage.cjs', 'package.json']) check(has(`/${f}`), f);
  const inner = JSON.parse(asar.extractFile(archive, 'package.json').toString('utf8'));
  check(inner.version === pkg.version && inner.productName === PRODUCT, `package.json do pacote: ${inner.productName} ${inner.version}`);
  const sw = join(l.resources, 'app.asar.unpacked', 'node_modules', 'steamworks.js');
  check(existsSync(join(sw, 'index.js')), 'steamworks.js desempacotado (app.asar.unpacked)');
  for (const f of STEAM_NATIVE[l.os]) check(existsSync(join(sw, f)), `  nativo ${f}`);
  if (l.os !== 'mac') {
    // O SteamAPI_Init procura o steam_appid.txt na pasta de trabalho — a do executável —, não dentro do asar.
    const appid = join(l.exeDir, 'steam_appid.txt');
    const id = existsSync(appid) ? readFileSync(appid, 'utf8').trim() : null;
    check(id !== null && /^\d+$/.test(id), `steam_appid.txt ao lado do executável (${id})`);
    if (id === '480') warn('steam_appid.txt ainda é 480 (Spacewar, app de testes da Valve)');
  }
  if (l.os === 'win') checkWinResources(l.exe);
  if (l.os === 'mac') checkMacBundle(l);
  console.log(`  · tamanho: pasta ${mb(du(dir))}, app.asar ${(statSync(archive).size / 1024).toFixed(0)} KB`);
}

const release = join(here, 'release');
const dirs = process.argv.slice(2).length
  ? process.argv.slice(2)
  : (existsSync(release) ? readdirSync(release) : [])
    .filter((n) => /^(win|linux|mac)/.test(n) && !n.includes('-temp') && statSync(join(release, n)).isDirectory())
    .map((n) => join('release', n));
if (dirs.length === 0) { console.error('Nenhum pacote em release/: rode npm run dist:linux (ou dist:win/dist:mac) antes.'); process.exit(1); }
for (const d of dirs) checkDir(d);
console.log(`\n${fails.length ? `FALHOU: ${fails.length}` : 'pacotes OK'}${warns.length ? ` · ${warns.length} aviso(s)` : ''}`);
process.exit(fails.length ? 1 : 0);
