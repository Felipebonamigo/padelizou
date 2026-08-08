namespace Padelizou.Services;

// O nome do status que o ORGANIZADOR lê, que nem sempre é o que está gravado.
//
// "Chaves em Sorteio" descreve o PRÓXIMO PASSO (sortear), não o estado atual — e por isso
// mentia na tela: nada está sendo sorteado, e o torneio pode ficar semanas assim. O que é
// verdade naquele momento é que **as inscrições estão fechadas**.
//
// Pedido do Felipe (08/08/2026) olhando o NATA PADEL TOUR, que aparecia como "CHAVES EM
// SORTEIO" logo acima da faixa dizendo "as inscrições estão fechadas". Duas frases sobre o
// mesmo estado, e a de cima era a errada.
//
// ⚠️ Traduz só a EXIBIÇÃO — o valor gravado continua "Chaves em Sorteio". Renomear a coluna
// exigiria migração e mexer nas ~25 comparações de string espalhadas pelo sistema (consulta,
// vitrine, trava de inscrição, chaveamento), e cada uma delas é um lugar onde esquecer uma
// quebraria o torneio calado. O nome ruim é histórico; o custo de arrastá-lo é uma função.
public static class StatusDoTorneioNaTela
{
    public static string Nome(string? status) => status switch
    {
        PortaDaInscricao.Fechada => "Inscrições Fechadas",
        _ => status ?? "",
    };
}
