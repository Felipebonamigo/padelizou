using Godot;
using Padel.Core;

namespace Padel.Godot;

/// <summary>Quadra em primitivas (M0/M1): piso, linhas, vidros, grade, rede. Arte de verdade entra no M3.</summary>
public partial class QuadraNode : Node3D
{
    private static readonly Color Piso = new(0.16f, 0.39f, 0.77f);
    private static readonly Color PisoFaixa = new(0.18f, 0.42f, 0.82f);
    private static readonly Color Linha = new(0.96f, 0.96f, 0.96f);
    private static readonly Color Vidro = new(0.6f, 0.8f, 1f, 0.18f);
    private static readonly Color Estrutura = new(0.12f, 0.15f, 0.25f);
    private static readonly Color Rede = new(0.05f, 0.07f, 0.14f, 0.85f);

    public override void _Ready()
    {
        // Piso (a quadra inteira) e a faixa entre as linhas de saque, um pouco mais clara.
        Caixa("Piso", new Vector3(Quadra.Largura, 0.1f, Quadra.Comprimento), new Vector3(0, -0.05f, 0), Piso);
        Caixa("PisoFaixa", new Vector3(Quadra.Largura, 0.1f, Quadra.LinhaDeSaque * 2), new Vector3(0, -0.045f, 0), PisoFaixa);

        // Linhas: as duas de saque e a central de saque, de cada linha até o fundo.
        const float largura = 0.05f, altura = 0.004f;
        foreach (int s in new[] { -1, 1 })
        {
            Caixa($"LinhaDeSaque{s}", new Vector3(Quadra.Largura, altura, largura), new Vector3(0, altura, s * Quadra.LinhaDeSaque), Linha);
            float meio = (Quadra.LinhaDeSaque + Quadra.MeioComprimento) / 2;
            Caixa($"LinhaCentral{s}", new Vector3(largura, altura, Quadra.MeioComprimento - Quadra.LinhaDeSaque), new Vector3(0, altura, s * meio), Linha);
        }

        // Paredes: exatamente os painéis que a física usa (Quadra.Paineis) — vidro escalonado, grade e as portas abertas.
        // Desenhar medida à mão aqui deixaria a bola sair "através" de uma parede desenhada.
        foreach (var painel in Quadra.Paineis) Painel(painel);
        // Colunas da estrutura nas emendas dos painéis de cada lateral e nos cantos.
        var colunas = new HashSet<(int, float)>();
        foreach (var painel in Quadra.Paineis)
        {
            if (painel.Parede != QualParede.Lateral) continue;
            colunas.Add((painel.Sinal, painel.Inicio));
            colunas.Add((painel.Sinal, painel.Fim));
        }
        foreach (var (sinal, y) in colunas)
            Caixa($"Coluna{sinal}_{y:F2}", new Vector3(0.1f, Quadra.AlturaDaParede, 0.1f), new Vector3(sinal * (Quadra.MeiaLargura + 0.05f), Quadra.AlturaDaParede / 2, y), Estrutura);

        // Rede e postes.
        Caixa("Rede", new Vector3(Quadra.Largura, Quadra.AlturaDaRede, 0.02f), new Vector3(0, Quadra.AlturaDaRede / 2, 0), Rede, transparente: true);
        Caixa("FitaDaRede", new Vector3(Quadra.Largura, 0.06f, 0.03f), new Vector3(0, Quadra.AlturaDaRede - 0.03f, 0), Linha);
        foreach (int s in new[] { -1, 1 })
        {
            var poste = new MeshInstance3D { Name = $"Poste{s}", Mesh = new CylinderMesh { TopRadius = 0.04f, BottomRadius = 0.04f, Height = 1.05f }, Position = new Vector3(s * (Quadra.MeiaLargura + 0.05f), 0.52f, 0) };
            poste.MaterialOverride = new StandardMaterial3D { AlbedoColor = Estrutura };
            AddChild(poste);
        }
    }

    private static readonly Color Grade = new(0.55f, 0.6f, 0.7f, 0.3f);

    private void Painel(Painel p)
    {
        float comprimento = p.Fim - p.Inicio, altura = p.ZMax - p.ZMin, meio = (p.Inicio + p.Fim) / 2, zMeio = (p.ZMin + p.ZMax) / 2;
        bool vidro = p.Superficie == Superficie.Vidro;
        float espessura = vidro ? 0.05f : 0.03f;
        var cor = vidro ? Vidro : Grade;
        // Core: lateral fica em x = ±5 e corre em y; fundo fica em y = ±10 e corre em x. Godot: (x, altura, y).
        if (p.Parede == QualParede.Lateral)
            Caixa($"{p.Superficie}Lateral{p.Sinal}_{meio:F2}_{zMeio:F1}", new Vector3(espessura, altura, comprimento),
                new Vector3(p.Sinal * (Quadra.MeiaLargura + espessura / 2), zMeio, meio), cor, transparente: true);
        else
            Caixa($"{p.Superficie}Fundo{p.Sinal}_{meio:F2}_{zMeio:F1}", new Vector3(comprimento, altura, espessura),
                new Vector3(meio, zMeio, p.Sinal * (Quadra.MeioComprimento + espessura / 2)), cor, transparente: true);
    }

    private void Caixa(string nome, Vector3 tamanho, Vector3 posicao, Color cor, bool transparente = false)
    {
        var material = new StandardMaterial3D { AlbedoColor = cor, Roughness = transparente ? 0.05f : 0.9f };
        if (transparente) material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
        AddChild(new MeshInstance3D { Name = nome, Mesh = new BoxMesh { Size = tamanho }, Position = posicao, MaterialOverride = material });
    }

    /// <summary>Contorno tracejado da caixa de saque, mostrado durante o saque.</summary>
    public MeshInstance3D CriarMarcaDaCaixa(Caixa caixa)
    {
        var marca = new MeshInstance3D
        {
            Name = "MarcaDaCaixa",
            Mesh = new BoxMesh { Size = new Vector3(caixa.XMax - caixa.XMin - 0.1f, 0.003f, caixa.YMax - caixa.YMin - 0.1f) },
            Position = new Vector3(caixa.CentroX, 0.006f, caixa.CentroY),
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.64f, 0.85f, 0.15f, 0.25f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha },
        };
        AddChild(marca);
        return marca;
    }
}
