namespace Padelizou.Models;

public class ComentarioPerfil
{
    public int Id { get; set; }

    public int AutorId { get; set; }
    public virtual Jogador Autor { get; set; } = null!;

    public int PerfilId { get; set; }
    public virtual Jogador Perfil { get; set; } = null!;

    public string Texto { get; set; } = null!;

    public DateTime CriadoEm { get; set; } = DateTime.Now;

    // Denúncia: qualquer pessoa logada sinaliza o comentário pra revisão de admin.
    // Um carimbo só — o primeiro; dez denúncias do mesmo texto não dizem mais que uma.
    // Admin decide em /Admin/Denuncias: apagar o comentário ou limpar o carimbo.
    public DateTime? DenunciadoEm { get; set; }

    // Sem FK de propósito: é um marcador de auditoria, e uma FK aqui faria a exclusão
    // da conta do denunciante esbarrar numa denúncia pendente (a ExclusaoDeConta limpa
    // o que o jogador ESCREVEU, não o que ele marcou dos outros).
    public int? DenunciadoPorId { get; set; }
}
