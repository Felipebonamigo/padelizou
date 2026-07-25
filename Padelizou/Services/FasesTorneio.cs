namespace Padelizou.Services;

// Convenções de Partida.Fase para jogos de grupo: os seeds antigos gravavam "Fase de Grupos"
// e o GerarChaves atual grava "Grupo A"/"Grupo B"/... Este helper é o ÚNICO lugar que conhece
// as duas formas — os gatilhos de mata-mata e o carimbo de UltimaFase precisam aceitar ambas.
// Em queries EF (traduzidas pra SQL) use a expressão inline equivalente:
//   p.Fase == "Fase de Grupos" || p.Fase.StartsWith("Grupo ")
public static class FasesTorneio
{
    public const string FaseDeGrupos = "Fase de Grupos";

    public static bool EhFaseDeGrupos(string? fase) =>
        fase == FaseDeGrupos || (fase != null && fase.StartsWith("Grupo "));
}
