// Captura da corrida no jogo (tools/render-harness.html), uma por argumento, em scratch/<nome>.png. Com o vite no ar:
//   PORT=<porta> node tools/capture.mjs "foz::track=foz_do_iguacu&seg=105&hud=0" "rio::track=copacabana&seg=70"
// Parâmetros úteis do harness: track, seg (segmento da câmera), hud=0 (sem HUD), humans=2|4 (tela dividida), car, paints,
// night/… (veja os `q.get(...)` em tools/render-harness.html). O marco de uma pista: `npx tsx tools/landmark-sight.ts <pista>`
// dá o segmento e o lado (x < 0 = esquerda); capture uns 40–80 segmentos ANTES dele para ver a chegada.
import { chromium } from 'playwright';
import { mkdirSync } from 'node:fs';
mkdirSync('scratch', { recursive: true });
const port = process.env.PORT ?? 5601;
const b = await chromium.launch({ executablePath: '/opt/pw-browsers/chromium-1194/chrome-linux/chrome', args: ['--use-gl=swiftshader', '--enable-unsafe-swiftshader'] });
const p = await b.newPage({ viewport: { width: 1280, height: 720 } });
p.setDefaultTimeout(240000);
for (const spec of process.argv.slice(2)) {
  const [name, qs] = spec.split('::');
  await p.goto(`http://localhost:${port}/tools/render-harness.html?${qs}`);
  await p.waitForFunction(() => window.ready, null, { timeout: 240000 }).catch(() => {});
  await p.waitForTimeout(2500);
  await p.screenshot({ path: `scratch/${name}.png` });
  console.log('ok', `scratch/${name}.png`);
}
await b.close();
