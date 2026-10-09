import { registerStrings } from '../i18n';

registerStrings('bench', {
  pt: {
    running: 'Banco de prova: {scene} · {quality} ({i}/{n})', done: 'Banco de prova concluído: {n} corridas', saved: 'Gravado em {path}',
    openFolder: 'Abrir pasta', download: 'Baixar JSON', quit: 'Sair', problems: '{n} problema(s) no resultado',
    warnings: '{n} aviso(s): veja o JSON',
  },
  en: {
    running: 'Benchmark: {scene} · {quality} ({i}/{n})', done: 'Benchmark done: {n} runs', saved: 'Saved to {path}',
    openFolder: 'Open folder', download: 'Download JSON', quit: 'Quit', problems: '{n} problem(s) in the result',
    warnings: '{n} warning(s): see the JSON',
  },
});
