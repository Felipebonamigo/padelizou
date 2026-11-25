using Padelizou.Models;

namespace Padelizou.Services;

// TROCAR O TIPO DO TORNEIO DEPOIS DE CRIADO (25/11/2026).
//
// 🗣️ Felipe, com o print do "Editar Dados do Torneio" da X Copa Treta: *"permita aqui alterar o
// tipo de torneio, pq o que querem fazer já está criado"*.
//
// ⚠️ TRETA NÃO É UM `Formato` GRAVADO (TRETA.md): ~60 pontos leem `Formato` como texto, e os
// `== "Padrao"` excluiriam um valor novo. Treta é o Oficial com três coisas ligadas: fora do
// ranking, tie-break no 5x5 e DTN nas categorias. Este arquivo é o único que traduz "tipo" ↔
// essas colunas.
//
// As duas regras de quando PODE trocar:
//  • Oficial ↔ Treta: a qualquer hora, até a chave DTN nascer — depois dela, desligar o Treta
//    deixaria uma chave DTN órfã num torneio que diz não ter DTN;
//  • Oficial/Treta ↔ Americano: só sem inscrição nem jogo. A inscrição mora em tabelas
//    diferentes (Dupla × InscricaoAmericana) e o preço é por dupla × por pessoa — trocar com gente
//    dentro perderia gente ou cobraria errado. E virar Oficial exige o perfil que a CRIAÇÃO exige
//    (PermissaoDeOrganizador): a troca não pode ser a porta dos fundos dela.
public static class TrocaDoTipoDoTorneio
{
    public const string Treta = "Treta";

    // Os tipos, na ordem do <select>.
    public static readonly IReadOnlyList<(string Valor, string Texto)> Opcoes = new[]
    {
        (FormatoDoTorneio.Padrao, "Oficial (duplas fixas, grupos + mata-mata)"),
        (Treta, "Treta (Oficial com chave DTN, fora do ranking)"),
        (FormatoDoTorneio.Americano, "Americano (parceiros rotativos)"),
        (FormatoDoTorneio.AmericanoDeDuplas, "Americano de Duplas"),
    };

    public static string TipoAtual(Torneio torneio, IEnumerable<Categoria> categorias)
    {
        var formato = string.IsNullOrEmpty(torneio.Formato) ? FormatoDoTorneio.Padrao : torneio.Formato;
        if (formato == FormatoDoTorneio.Padrao
            && categorias.Any(c => c.PapelDaChave == null && c.DuplasNoDtnPorGrupo > 0))
            return Treta;
        return formato;
    }

    private static string FormatoDe(string tipo) => tipo == Treta ? FormatoDoTorneio.Padrao : tipo;

    // Nulo = pode. `categorias` são TODAS as do torneio (inclusive DTN/Finalíssima já nascidas).
    public static string? Problema(Torneio torneio, IReadOnlyCollection<Categoria> categorias, string para,
        bool temInscricaoOuJogo, Jogador? quem)
    {
        if (para != Treta && !FormatoDoTorneio.Existe(para)) return "Escolha um tipo de torneio da lista.";

        var de = TipoAtual(torneio, categorias);
        if (de == para) return null;

        if (de == Treta && categorias.Any(c => c.PapelDaChave != null))
            return "A chave DTN já foi montada — o torneio não deixa de ser Treta agora.";

        bool mudaOFormato = FormatoDe(de) != FormatoDe(para);
        if (mudaOFormato && temInscricaoOuJogo)
            return "Com inscrição feita, o tipo não troca entre Oficial e Americano — a inscrição é guardada de jeitos diferentes. Dá pra trocar entre Oficial e Treta.";

        if (mudaOFormato && !PermissaoDeOrganizador.PodeCriarTorneio(quem, FormatoDe(para)))
            return "O torneio Oficial é liberado organizador por organizador — peça a liberação antes de trocar.";

        return null;
    }

    // Aplica a troca já conferida por `Problema`.
    public static void Aplicar(Torneio torneio, IEnumerable<Categoria> categorias, string para)
    {
        var lista = categorias.ToList();
        var de = TipoAtual(torneio, lista);
        if (de == para) return;

        torneio.Formato = FormatoDe(para);
        if (!FormatoDoTorneio.EhAmericano(torneio.Formato)) torneio.PontuaNoRankingAmericano = false;

        var modulos = lista.Where(c => c.PapelDaChave == null && !c.DeTimes && !c.ChaveDireta).ToList();
        if (para == Treta)
        {
            torneio.ForaDoRanking = true;
            torneio.TieBreakNaPenultima = true;
            foreach (var c in modulos.Where(c => c.DuplasNoDtnPorGrupo <= 0)) c.DuplasNoDtnPorGrupo = 1;
        }
        else if (de == Treta)
        {
            torneio.ForaDoRanking = false;
            torneio.TieBreakNaPenultima = false;
            foreach (var c in modulos)
            {
                c.DuplasNoDtnPorGrupo = 0;
                c.ParDaFinalissimaId = null;
            }
        }
    }
}
