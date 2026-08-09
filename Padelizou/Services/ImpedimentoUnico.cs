namespace Padelizou.Services;

// Uma inscrição pode marcar NO MÁXIMO um impedimento de horário.
//
// Antes dava pra marcar os três, e a dupla que faz isso está dizendo "não podemos jogar em
// nenhum dos turnos do torneio" — o chaveamento não tem onde encaixá-la, e o organizador
// descobre isso montando a grade, tarde demais. Um impedimento é uma restrição que se
// contorna; três são uma desistência escrita como pedido.
//
// A tela já deixa marcar só um (os interruptores se desmarcam entre si). Isto aqui é a
// segunda tranca: página velha em cache, dois cliques rápidos ou POST feito à mão continuam
// chegando com mais de um marcado, e a regra não pode morar só no navegador.
//
// Quando vem mais de um, fica o PRIMEIRO na ordem em que aparecem na tela — recusar a
// inscrição inteira por causa disso perderia o cadastro por um detalhe que dá pra resolver.
public static class ImpedimentoUnico
{
    public static (bool Quinta, bool Sexta, bool SabadoManha, bool SabadoTarde) Apenas(
        bool quinta, bool sexta, bool sabadoManha, bool sabadoTarde)
    {
        if (quinta) return (true, false, false, false);
        if (sexta) return (false, true, false, false);
        if (sabadoManha) return (false, false, true, false);
        return (false, false, false, sabadoTarde);
    }
}
