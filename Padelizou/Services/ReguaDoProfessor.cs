using Microsoft.EntityFrameworkCore;
using Padelizou.Models;

namespace Padelizou.Services;

// A régua técnica de um professor: semear na primeira vez, ler pra montar a ficha, e mexer
// (acrescentar, renomear, tirar de vista) sem quebrar o histórico de ninguém.
public class ReguaDoProfessor
{
    private readonly DbPadelContext _context;

    public ReguaDoProfessor(DbPadelContext context) => _context = context;

    // Garante que o professor tenha uma régua. Na primeira vez, copia a lista padrão
    // (CatalogoDeFundamentos, tirada da planilha de um professor de verdade); depois disso
    // nunca mais mexe — senão um item que ele apagou de propósito voltaria sozinho a cada
    // visita, que é o pior tipo de bug: o sistema desfazendo o trabalho da pessoa.
    public async Task<List<FundamentoDoProfessor>> GarantirAsync(int professorId)
    {
        var minha = await _context.FundamentosDoProfessor
            .Where(f => f.ProfessorId == professorId)
            .OrderBy(f => f.Modulo).ThenBy(f => f.Ordem)
            .ToListAsync();

        if (minha.Count > 0) return minha;

        int ordem = 0;
        var novos = CatalogoDeFundamentos.Todos.Select(p => new FundamentoDoProfessor
        {
            ProfessorId = professorId,
            Modulo = p.Modulo,
            Familia = p.Familia,
            Nome = p.Nome,
            Ordem = ordem += 10,   // de 10 em 10: dá espaço pra encaixar item novo no meio
            Ativo = true,
        }).ToList();

        _context.FundamentosDoProfessor.AddRange(novos);
        await _context.SaveChangesAsync();

        return novos;
    }

    // O que entra numa ficha NOVA: só o que está ativo. O desativado continua aparecendo nas
    // fichas antigas, que é justamente o motivo de ele não ser apagado.
    public async Task<List<FundamentoDoProfessor>> AtivosDoModuloAsync(int professorId, string modulo)
    {
        await GarantirAsync(professorId);

        return await _context.FundamentosDoProfessor
            .Where(f => f.ProfessorId == professorId && f.Modulo == modulo && f.Ativo)
            .OrderBy(f => f.Ordem)
            .ToListAsync();
    }

    // Os módulos que a régua DELE tem — não a lista fixa nossa: ele pode ter criado um módulo
    // próprio ou esvaziado um dos nossos.
    public async Task<List<string>> ModulosAsync(int professorId)
    {
        var minha = await GarantirAsync(professorId);

        return minha.Where(f => f.Ativo)
            .Select(f => f.Modulo)
            .Distinct()
            // Os nossos primeiro, na ordem pedagógica; os que ele inventou vêm depois, em
            // ordem alfabética. Ordenar tudo por nome jogaria "Avançado" na frente de
            // "Iniciante", que é o contrário de como se ensina.
            .OrderBy(m => CatalogoDeFundamentos.Modulos.Contains(m)
                ? CatalogoDeFundamentos.Modulos.ToList().IndexOf(m)
                : int.MaxValue)
            .ThenBy(m => m)
            .ToList();
    }

    // Tirar da régua = DESATIVAR. Nota já dada continua existindo e legível na ficha antiga;
    // apagar de verdade abriria buraco no histórico do aluno, que é o que a feature existe
    // pra mostrar.
    public async Task<bool> DesativarAsync(int professorId, int fundamentoId)
    {
        var f = await _context.FundamentosDoProfessor
            .FirstOrDefaultAsync(x => x.Id == fundamentoId && x.ProfessorId == professorId);
        if (f == null) return false;

        f.Ativo = false;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ReativarAsync(int professorId, int fundamentoId)
    {
        var f = await _context.FundamentosDoProfessor
            .FirstOrDefaultAsync(x => x.Id == fundamentoId && x.ProfessorId == professorId);
        if (f == null) return false;

        f.Ativo = true;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RenomearAsync(int professorId, int fundamentoId, string nome)
    {
        nome = (nome ?? "").Trim();
        if (nome.Length is 0 or > 150) return false;

        var f = await _context.FundamentosDoProfessor
            .FirstOrDefaultAsync(x => x.Id == fundamentoId && x.ProfessorId == professorId);
        if (f == null) return false;

        f.Nome = nome;
        await _context.SaveChangesAsync();
        return true;
    }

    // Item novo entra no FIM do módulo — é onde o professor espera achar o que acabou de
    // escrever, e não força reordenar o resto.
    public async Task<FundamentoDoProfessor?> AcrescentarAsync(
        int professorId, string modulo, string? familia, string nome)
    {
        modulo = (modulo ?? "").Trim();
        nome = (nome ?? "").Trim();
        if (modulo.Length is 0 or > 60 || nome.Length is 0 or > 150) return null;

        await GarantirAsync(professorId);

        int ultima = await _context.FundamentosDoProfessor
            .Where(f => f.ProfessorId == professorId && f.Modulo == modulo)
            .Select(f => (int?)f.Ordem)
            .MaxAsync() ?? 0;

        var novo = new FundamentoDoProfessor
        {
            ProfessorId = professorId,
            Modulo = modulo,
            Familia = (familia ?? "").Trim(),
            Nome = nome,
            Ordem = ultima + 10,
            Ativo = true,
        };

        _context.FundamentosDoProfessor.Add(novo);
        await _context.SaveChangesAsync();
        return novo;
    }
}
