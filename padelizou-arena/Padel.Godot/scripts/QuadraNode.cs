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

        // Vidros: fundo (3 m + 1 m de grade) e laterais (3 m, simplificado — na quadra real a lateral escalona).
        float vidroAlto = Quadra.AlturaDoVidro, grade = Quadra.AlturaDaParede - Quadra.AlturaDoVidro;
        foreach (int s in new[] { -1, 1 })
        {
            Caixa($"VidroFundo{s}", new Vector3(Quadra.Largura + 0.2f, vidroAlto, 0.05f), new Vector3(0, vidroAlto / 2, s * (Quadra.MeioComprimento + 0.025f)), Vidro, transparente: true);
            Caixa($"GradeFundo{s}", new Vector3(Quadra.Largura + 0.2f, grade, 0.03f), new Vector3(0, vidroAlto + grade / 2, s * (Quadra.MeioComprimento + 0.025f)), new Color(0.5f, 0.55f, 0.65f, 0.35f), transparente: true);
            Caixa($"VidroLateral{s}", new Vector3(0.05f, vidroAlto, Quadra.Comprimento), new Vector3(s * (Quadra.MeiaLargura + 0.025f), vidroAlto / 2, 0), Vidro, transparente: true);
            Caixa($"GradeLateral{s}", new Vector3(0.03f, grade, Quadra.Comprimento), new Vector3(s * (Quadra.MeiaLargura + 0.025f), vidroAlto + grade / 2, 0), new Color(0.5f, 0.55f, 0.65f, 0.35f), transparente: true);
        }
        // Colunas da estrutura nos cantos e no meio das laterais.
        foreach (int sx in new[] { -1, 1 })
            foreach (float z in new[] { -Quadra.MeioComprimento, -Quadra.LinhaDeSaque, 0, Quadra.LinhaDeSaque, Quadra.MeioComprimento })
                Caixa($"Coluna{sx}_{z}", new Vector3(0.12f, Quadra.AlturaDaParede, 0.12f), new Vector3(sx * (Quadra.MeiaLargura + 0.06f), Quadra.AlturaDaParede / 2, z), Estrutura);

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
