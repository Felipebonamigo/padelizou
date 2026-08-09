namespace Padelizou.ViewModels;

// O que os OUTROS disseram sobre você: elogios e comentários, com o nome de quem fez.
//
// O perfil público mostra elogio agregado por TIPO ("3× Boa Bandeja") — que é a leitura certa
// pra quem chega de fora e quer saber como a pessoa joga. Mas o dono do perfil quer a outra
// pergunta respondida: QUEM. E essa não estava em lugar nenhum do sistema.
public class RecebidosNoPerfilVM
{
    public List<ElogioRecebidoVM> Elogios { get; set; } = new();
    public List<ComentarioRecebidoVM> Comentarios { get; set; } = new();

    // Os totais são contados no banco, não pelo tamanho das listas acima: elas trazem só as
    // últimas linhas, e "ver todos os 12" precisa saber que são 12.
    public int TotalElogios { get; set; }
    public int TotalComentarios { get; set; }

    public bool TemAlgo => TotalElogios > 0 || TotalComentarios > 0;
}

public record ElogioRecebidoVM(
    int DeJogadorId,
    string DeQuem,
    string? Foto,
    string Titulo,
    string Icone,
    DateTime Quando);

public record ComentarioRecebidoVM(
    int AutorId,
    string DeQuem,
    string? Foto,
    string Texto,
    DateTime Quando);
