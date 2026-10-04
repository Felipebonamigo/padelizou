// Textos da legenda de um marco: o nome (o do passaporte para os do Brasil, os do Mundial em ./strings.ts) e onde fica
// a pista ("Foz do Iguaçu · PR", "Paris · França"). Montados só quando a legenda troca de marco (hud.ts), nunca por
// quadro.
import { placeOf } from '../../core/data/places';
import { t } from '../../i18n';
import '../../ui/passport/strings';
import './strings';

/** Nome do marco no idioma atual. */
export function captionName(landmarkId: string): string {
  const key = `passport.landmark.${landmarkId}`;
  const name = t(key);
  return name !== key ? name : t(`caption.landmark.${landmarkId}`);
}

/**
 * Onde fica a pista: no Brasil, cidade (ou região) · UF do places.ts; sem a cidade na tabela, o nome do estado. No
 * Mundial, lugar · país. Pista sem lugar: vazio (a legenda mostra só o nome).
 */
export function captionPlace(trackId: string): string {
  const uf = placeOf(trackId)?.state;
  if (uf) {
    const key = `caption.town.${trackId}`;
    const town = t(key);
    return town === key ? t(`core.state.${uf}`) : t('caption.where.br', { town, uf });
  }
  const key = `caption.place.${trackId}`;
  const where = t(key);
  return where === key ? '' : where;
}
