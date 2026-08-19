namespace Padelizou.Services;

// O módulo fiscal tem interruptor PRÓPRIO, separado do `Bar__Habilitado` — e essa separação
// não é zelo técnico, é o desenho comercial (ver FISCAL.md).
//
// São dois planos diferentes: o clube que assina só a GESTÃO ganha comanda, estoque e caixa
// (o bar inteiro, que já existe); quem assina o FISCAL ganha por cima disso a emissão de
// nota. Um interruptor só obrigaria a ligar os dois juntos pra todo mundo, e aí não haveria
// dois planos pra vender — haveria um.
//
// Vale também pro caminho contrário, que é o mais provável nos primeiros meses: o cadastro
// fiscal vai pra produção (as colunas são todas nulas, nada muda pra ninguém) e fica
// invisível até existir clube pagante e certificado configurado.
//
// Como ligar (systemd, uma linha no Environment= do unit):
//   Fiscal__Habilitado=true
public class FiscalSettings
{
    // false = em construção: só admin do Padelizou enxerga a aba fiscal.
    public bool Habilitado { get; set; }
}
