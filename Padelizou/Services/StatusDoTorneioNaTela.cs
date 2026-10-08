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

    // O JOGO AINDA NÃO COMEÇOU? — os dois estados de ANTES da chave ser pública: inscrição
    // fechada esperando o sorteio, e chave sorteada esperando aprovação.
    //
    // ⚠️ Lista EXPLÍCITA, e não "tudo que não é Inscrições Abertas": foi essa negação que, até
    // 07/10/2026, pôs o NATA PADEL TOUR em "Acontecendo agora" com selo vermelho de ao vivo
    // enquanto a chave ainda esperava aprovação. Status novo cai no lado "rolando" por padrão,
    // e é o lado menos grave de errar.
    public static bool AindaNaoComecou(string? status) =>
        status is PortaDaInscricao.Fechada or AprovacaoDeChaves.Pendente;

    // O nome pra quem olha de FORA (a vitrine da Home). Antes do jogo a pessoa só precisa saber
    // que não dá mais pra se inscrever: "Chaves em Aprovação" é passo interno do organizador —
    // a chave só é pública depois de aprovada — e vazava crua pra visitante.
    //
    // ⚠️ É uma função À PARTE, e não a `Nome` mexida: a tela de gestão do torneio também usa a
    // `Nome`, e lá quem aprova a chave PRECISA ler "Chaves em Aprovação".
    public static string NomePublico(string? status) =>
        AindaNaoComecou(status) ? "Inscrições Encerradas" : status ?? "";
}
