namespace Padelizou.Services;

// Os Desafios estão em construção. Enquanto não estiverem prontos, o módulo existe no código,
// roda em produção e NÃO aparece pra ninguém — só pra quem é admin do Padelizou.
//
// Por que uma chave de configuração e não uma branch: é o mesmo raciocínio do Bar. O módulo
// mexe em tabelas novas e no menu do site inteiro; segurar isso numa branch por semanas é
// garantir conflito com o resto do trabalho e um dia de merge no fim. Ligado por ambiente, dá
// pra testar no dev com dado real e virar a chave em produção quando a cidade estiver pronta —
// sem republicar nada.
//
// Como ligar (systemd, uma linha no Environment= do unit):
//   Desafios__Habilitado=true
//
// ⚠️ E aqui a chave tem uma segunda função, que o Bar não tem: DENSIDADE. Mural vazio é pior
// do que mural nenhum — a pessoa entra uma vez, não vê ninguém e não volta. A chave é o que
// permite abrir cidade por cidade, quando cada uma tiver dupla suficiente pra tela não nascer
// morta (ver DESAFIOS.md, "Buracos conhecidos").
public class DesafiosSettings
{
    // false = em construção: só admin do Padelizou enxerga o menu e as telas.
    // true  = liberado pros jogadores.
    public bool Habilitado { get; set; }
}
