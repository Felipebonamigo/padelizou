// Textos do fantasma do contra-relógio: HUD (curtos, caixa alta), opção e tela de recordes.
import { registerStrings } from '../i18n';

registerStrings('ghost', {
  pt: {
    'hud.label': 'FANTASMA',
    'hud.lapDelta': 'FANTASMA {delta}',
    'hud.newBestDelta': 'NOVO FANTASMA {delta}',
    'hud.recorded': 'FANTASMA GRAVADO',
    'options.ghost': 'Fantasma (contra-relógio)',
    'records.ghost': 'Fantasma',
    'records.hint': 'Enter numa pista com fantasma exporta a volta para desafiar um amigo; o amigo importa e corre contra ela no contra-relógio.',
    'records.import': 'Importar fantasma',
    'records.exported': 'Fantasma de {track} exportado.',
    'records.exportFailed': 'Não deu para exportar o fantasma.',
    'records.imported': 'Fantasma de {name} em {track} importado: corra contra ele no contra-relógio.',
    'records.invalid': 'Esse arquivo não é um fantasma válido do Nitro Crew.',
    'records.unknownTrack': 'Esse fantasma é de uma pista que este jogo não tem.',
    'records.saveFailed': 'Não deu para guardar o fantasma (armazenamento cheio ou indisponível).',
  },
  en: {
    'hud.label': 'GHOST',
    'hud.lapDelta': 'GHOST {delta}',
    'hud.newBestDelta': 'NEW GHOST {delta}',
    'hud.recorded': 'GHOST SAVED',
    'options.ghost': 'Ghost (time trial)',
    'records.ghost': 'Ghost',
    'records.hint': 'Press Enter on a track with a ghost to export the lap and challenge a friend; they import it and race against it in time trial.',
    'records.import': 'Import ghost',
    'records.exported': 'Ghost for {track} exported.',
    'records.exportFailed': 'Could not export the ghost.',
    'records.imported': 'Imported {name}\'s ghost on {track}: race against it in time trial.',
    'records.invalid': 'This file is not a valid Nitro Crew ghost.',
    'records.unknownTrack': 'This ghost is for a track this game does not have.',
    'records.saveFailed': 'Could not store the ghost (storage full or unavailable).',
  },
});
