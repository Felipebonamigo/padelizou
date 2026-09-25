// Textos do relatório de erros: aviso no canto, os dois itens das Opções e a tela de erro fatal.
import { registerStrings } from '../i18n';

registerStrings('errors', {
  pt: {
    'toast.title': 'Algo deu errado — o jogo continua.',
    'toast.hint': 'Opções › Copiar relatório de erros',
    'toast.count': '{n} erros registrados',

    'options.report': 'Copiar relatório de erros',
    'options.none': 'Nenhum erro',
    'options.one': '1 erro',
    'options.many': '{n} erros',
    'options.copied': 'Copiado ✓',
    'options.copyFailed': 'Não deu para copiar',
    'options.telemetry': 'Telemetria anônima',

    'fatal.title': 'O jogo não conseguiu iniciar',
    'fatal.webgl': 'O computador não conseguiu criar a imagem 3D (WebGL). Atualize o driver da placa de vídeo e tente de novo. Na Steam, dá para tentar também a opção de inicialização --ignore-gpu-blocklist.',
    'fatal.generic': 'Um erro impediu o jogo de começar. Copie o relatório e mande para nós — é o que ajuda a corrigir.',
    'fatal.copy': 'Copiar relatório de erros',
    'fatal.retry': 'Tentar de novo',
  },
  en: {
    'toast.title': 'Something went wrong — the game keeps going.',
    'toast.hint': 'Options › Copy error report',
    'toast.count': '{n} errors recorded',

    'options.report': 'Copy error report',
    'options.none': 'No errors',
    'options.one': '1 error',
    'options.many': '{n} errors',
    'options.copied': 'Copied ✓',
    'options.copyFailed': 'Could not copy',
    'options.telemetry': 'Anonymous telemetry',

    'fatal.title': 'The game could not start',
    'fatal.webgl': 'Your computer could not create the 3D view (WebGL). Update your graphics driver and try again. On Steam you can also try the launch option --ignore-gpu-blocklist.',
    'fatal.generic': 'An error stopped the game from starting. Copy the report and send it to us — that is what helps us fix it.',
    'fatal.copy': 'Copy error report',
    'fatal.retry': 'Try again',
  },
});
