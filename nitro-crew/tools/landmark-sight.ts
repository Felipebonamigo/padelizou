// Onde cada marco de uma pista ficou e quanto tempo fica à vista (a conta de src/render/scenery/sight.ts, a mesma do
// teste de enquadramento: meta 2,5 s perto/longe, 4 s horizonte):
//   npx tsx tools/landmark-sight.ts foz_do_iguacu copacabana
// Saída por instância: marco @segmento · lateral x (m; negativo = esquerda) · lugar · 1ª da volta? · praça? · segundos.
import { getTrack } from '../src/core/track';
import { landmarkSight, sceneryLayout } from '../src/render/scenery/layout';
for (const id of process.argv.slice(2)) {
  const t = getTrack(id);
  const rows = landmarkSight(t, sceneryLayout(t));
  console.log(`${id} (${t.def.scenery}, ${t.segments.length} segmentos)`);
  for (const r of rows) console.log(`  ${r.id} @${r.seg} x=${r.x.toFixed(0)} ${r.place}${r.first ? ' 1ª' : ''}${r.plaza ? ' praça' : ''} → ${r.seen.toFixed(1)} s`);
}
