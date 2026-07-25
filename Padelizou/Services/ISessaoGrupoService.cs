using padelizou.Models;
using Padelizou.Models;

namespace Padelizou.Services;

public interface ISessaoGrupoService
{
    // Obtém a sessão semanal na data pedida (ou a próxima ocorrência do dia/horário fixo do grupo,
    // se nenhuma data foi pedida), criando-a com os mensalistas atuais como "Pendente" se ainda não
    // existir. Lazy/sob demanda — não existe job de background disparando isso antecipadamente.
    Task<SessaoGrupo> ObterOuCriarSessaoAsync(GrupoPrivado grupo, DateTime? dataSolicitada);
}
