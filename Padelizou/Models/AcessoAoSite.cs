using System.ComponentModel.DataAnnotations.Schema;

namespace Padelizou.Models;

// UMA VISITA REAL AO SITE — não um asset, não um polling de fundo.
//
// Nasceu do pedido do Felipe por "acessos hoje" e "máximo de acessos simultâneos" nas
// Métricas. Até 18/08/2026 o sistema não guardava rastro nenhum de tráfego — nem sequer um
// "último acesso" no Jogador — então esta tabela é o alicerce novo, não só a tela.
//
// ⚠️ De propósito, SEM identidade nenhuma: nem JogadorId, nem IP, nem sessão. As duas
// perguntas que motivaram isto são sobre VOLUME — quanto e quão concentrado — não sobre QUEM.
// Guardar identidade seria coletar dado que a régua de privacidade deste projeto não pede
// (nunca colocar dado pessoal em URL, nunca compilar informação sem necessidade), e as duas
// contas que a tela precisa saem inteiras de um timestamp sozinho.
[Table("AcessoAoSite")]
public class AcessoAoSite
{
    public long Id { get; set; }
    public DateTime Quando { get; set; }
}
