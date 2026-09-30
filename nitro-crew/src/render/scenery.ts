// Cenário de beira de pista (docs/VISUAL.md, seção "Cenário"). O código mora em src/render/scenery/:
// geom (kit de modelagem), vegetation/structures/props (modelos procedurais por bioma e país),
// catalog (sprite → modelo, receitas de decoração), ground (altura do chão = a do terreno), layout
// (onde cada objeto fica, uma vez por pista, determinístico) e runtime (lotes instanciados por material).
// O contrato com o renderizador não mudou: new Scenery(), .group, setNight(), update(frame, track, time), dispose().
export { Scenery } from './scenery/runtime';
