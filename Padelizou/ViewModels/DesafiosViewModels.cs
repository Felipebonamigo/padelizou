using padelizou.Models;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.ViewModels;

// As telas dos Desafios. Espec em DESAFIOS.md.
//
// Os view models existem aqui, e não como ViewBag, porque o cartão do mural precisa de coisas
// que a linha do banco não tem — o retrospecto da dupla, o rótulo da semana, se EU posso
// desafiar — e calcular isso dentro da view espalharia regra por seis arquivos .cshtml que
// nenhum teste alcança.

// Uma dupla anunciada, do jeito que ela aparece no cartão do mural.
public record CartaoDoMural(
    AnuncioDeDesafio Anuncio,
    IReadOnlyList<string> Categorias,
    IReadOnlyList<string> Cidades,
    IReadOnlyList<string> Clubes,
    int DesafiosJogados,
    int Vitorias,
    bool EMeu,
    bool JaDesafiei)
{
    public string PrazoNaTela => SemanaDoDesafio.RotuloDoPrazo(Anuncio.ValeAte);

    // "sem restrição" é informação, não ausência dela: dizer "aceita qualquer categoria" evita
    // que o cartão pareça um cadastro pela metade.
    public string CategoriasNaTela => Categorias.Count > 0 ? string.Join(", ", Categorias) : "Qualquer categoria";
    public string LocaisNaTela => Clubes.Count > 0
        ? string.Join(", ", Clubes)
        : Cidades.Count > 0 ? string.Join(", ", Cidades) : "Qualquer lugar";

    public int Derrotas => Math.Max(0, DesafiosJogados - Vitorias);

    // Sem jogo nenhum não existe aproveitamento — e "0%" numa dupla nova a faria parecer ruim
    // em vez de nova.
    public int? Aproveitamento => DesafiosJogados == 0
        ? null
        : (int)Math.Round(100.0 * Vitorias / DesafiosJogados, MidpointRounding.AwayFromZero);
}

public record MuralVM(
    IReadOnlyList<CartaoDoMural> Cartoes,
    AnuncioDeDesafio? MeuAnuncio,
    string? MeuLinkDeConvite,
    IReadOnlyList<CategoriaPadrao> CatalogoCategorias,
    IReadOnlyList<CidadeNaLista> CatalogoCidades,
    int? CategoriaFiltrada,
    int? CidadeFiltrada,
    bool EmConstrucao);

// O formulário de publicar. Nasce marcado com as preferências que a pessoa já tem no perfil —
// mas o que é gravado é uma CÓPIA no anúncio (ver o comentário de AnuncioDeDesafio).
public record PublicarAnuncioVM(
    IReadOnlyList<CategoriaPadrao> CatalogoCategorias,
    IReadOnlyList<CidadeNaLista> CatalogoCidades,
    IReadOnlyList<Clube> CatalogoClubes,
    IReadOnlyList<int> CategoriasMarcadas,
    IReadOnlyList<int> CidadesMarcadas,
    IReadOnlyList<int> ClubesMarcados,
    DateTime ValeAteSugerido,
    bool SemanaAcabando);

public record ConviteVM(
    AnuncioDeDesafio? Anuncio,
    string Token,
    bool Valido,
    string Motivo,
    IReadOnlyList<string> Categorias,
    IReadOnlyList<string> Locais);

public record DesafiarVM(
    AnuncioDeDesafio Anuncio,
    IReadOnlyList<CategoriaPadrao> CategoriasPossiveis,
    IReadOnlyList<Clube> ClubesPossiveis,
    string DuplaAdversaria,
    DateTime QuandoSugerido);

// Uma linha de "meus desafios". O status vem de EstadoDoDesafio.StatusNaTela — nunca do campo
// cru, senão a proposta vencida apareceria como "Proposto" pra sempre.
public record LinhaDeDesafio(
    Desafio Desafio,
    bool SouDesafiante,
    string Adversarios,
    string StatusNaTela,
    bool PossoResponder,
    bool PossoCancelar,
    bool PossoLancarPlacar,
    bool PossoResponderPlacar,
    TimeSpan? TempoParaResponder,
    TimeSpan? TempoParaContestar)
{
    public string PlacarNaTela => PlacarDoDesafio.TextoParaOLado(Desafio, SouDesafiante);

    public bool TemPlacar => Desafio.GamesDesafiante != null || Desafio.SetsDesafiante != null;

    // O ponto que ESTE lado levou. Nulo enquanto o desafio não fechou.
    public int? MeusPontos => SouDesafiante ? Desafio.PontosDesafiante : Desafio.PontosDesafiado;
}

public record MeusDesafiosVM(
    IReadOnlyList<LinhaDeDesafio> Recebidos,
    IReadOnlyList<LinhaDeDesafio> Enviados,
    IReadOnlyList<LinhaDeDesafio> Agendados,
    IReadOnlyList<LinhaDeDesafio> Historico);
