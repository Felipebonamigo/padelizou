namespace Padelizou.Services;

// O bar do clube está em construção. Enquanto não estiver pronto, ele existe no código, roda
// em produção e NÃO aparece pra ninguém — só pra quem é admin do Padelizou.
//
// Por que uma chave de configuração e não uma branch: o módulo mexe em tabelas novas e em
// telas existentes (o painel do clube). Segurar isso numa branch por semanas é garantir
// conflito com o resto do trabalho e um dia de merge no fim. Ligado por ambiente, dá pra
// testar no dev com dado real e virar a chave em produção quando o cliente estiver pronto —
// sem republicar nada.
//
// Como ligar (systemd, uma linha no Environment= do unit):
//   Bar__Habilitado=true
public class BarSettings
{
    // false = em construção: só admin do Padelizou enxerga e mexe.
    // true  = liberado pros donos e administradores de clube.
    public bool Habilitado { get; set; }
}
