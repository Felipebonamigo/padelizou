using Microsoft.AspNetCore.Routing;
using Padelizou.Models;
using Padelizou.Services;

namespace Padelizou.ViewModels;

// A tela "Compartilhar esta lista" (Views/Torneios/CompartilharJogos.cshtml).
//
// `Filtros` são os valores de rota da aba Jogos que geraram esta lista (time, categorias, meus
// jogos, clube, quadra, fase, comPrevias): a tela os repete em cada link — a imagem de cada
// parte, o alternador de prévias, a volta pra lista — porque o que se compartilha é ESTE
// recorte, e um link sem eles compartilharia outro.
public sealed record CompartilharJogosVM(
    Torneio Torneio,
    GradeDesenhavel Grade,
    IReadOnlyList<JogoDaLista> Jogos,
    string Texto,
    string LinkDaLista,
    bool ComPrevias,
    bool TemPrevias,
    bool FonteDisponivel,
    bool CabeNumaImagemSo,
    bool UmaImagem,
    RouteValueDictionary Filtros);
