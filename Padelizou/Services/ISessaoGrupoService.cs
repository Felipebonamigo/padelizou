using padelizou.Models;
using Padelizou.Models;

namespace Padelizou.Services;

public interface ISessaoGrupoService
{
    // Obtém a sessão semanal na data pedida (ou a próxima ocorrência do dia/horário fixo do grupo,
    // se nenhuma data foi pedida), criando-a com os mensalistas atuais se ainda não existir.
    // Lazy/sob demanda — não existe job de background disparando isso antecipadamente.
    //
    // ⚠️ PEDIR CRIA — e desde 21/08/2026 o que se cria é uma AFIRMAÇÃO, não uma pergunta: o
    // membro nasce "Presumido" quando o jogo ainda não aconteceu (Services/PresencaNaSessao).
    // Chamar isto sem um humano na frente escala a panelinha inteira pra dentro da lista.
    Task<SessaoGrupo> ObterOuCriarSessaoAsync(GrupoPrivado grupo, DateTime? dataSolicitada);
}
