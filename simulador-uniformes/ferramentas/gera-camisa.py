# Camiseta fotorrealista por PEÇAS: corpo, mangas e punhos são almofadas
# independentes (como panos costurados), cada uma com seu volume. Luz de
# estúdio + grão de tecido no shader. Sai frente/costas + máscaras RGB.
import bpy, bmesh, math, sys
from mathutils import Vector, noise
from mathutils.geometry import tessellate_polygon, interpolate_bezier

for _o in list(bpy.data.objects):
    bpy.data.objects.remove(_o, do_unlink=True)

S = 0.0015
def P(x, y): return Vector(((x - 220) * S, 0.0, (480 - y) * S))
def bez(a, c1, c2, b, n=34): return interpolate_bezier(P(*a), P(*c1), P(*c2), P(*b), n + 1)[:-1]
def lin(a, b, n=20):
    pa, pb = P(*a), P(*b)
    return [pa.lerp(pb, i / n) for i in range(n)]

CORPO_PTS = (bez((152,84),(176,72),(264,72),(288,84)) + lin((288,84),(312,102)) +
             lin((312,102),(302,186)) + bez((302,186),(298,220),(296,260),(296,310)) +
             bez((296,310),(296,360),(298,410),(300,436)) + bez((300,436),(290,444),(258,446),(220,446)) +
             bez((220,446),(182,446),(150,444),(140,436)) + bez((140,436),(142,410),(144,360),(144,310)) +
             bez((144,310),(144,260),(142,220),(138,186)) + lin((138,186),(128,102), 6) +
             lin((128,102),(152,84)))
MANGA_D_PTS = (lin((306,96),(296,192)) + bez((296,192),(302,208),(312,220),(322,232)) +
               lin((322,232),(374,204)) + bez((374,204),(372,186),(366,166),(356,148)) +
               bez((356,148),(344,128),(326,110),(306,96)))
MANGA_E_PTS = [Vector((-p.x, p.y, p.z)) for p in reversed(MANGA_D_PTS)]
PUNHO_D_PTS = lin((374,204),(322,232)) + lin((322,232),(318,216), 3) + lin((318,216),(370,188), 8) + lin((370,188),(374,204), 3)
PUNHO_E_PTS = [Vector((-p.x, p.y, p.z)) for p in reversed(PUNHO_D_PTS)]

def peca(nome, contorno, fundura, escala_d, y_centro, mat):
    bm = bmesh.new()
    verts = [bm.verts.new(p) for p in contorno]
    for t in tessellate_polygon([contorno]):
        try: bm.faces.new((verts[t[0]], verts[t[1]], verts[t[2]]))
        except ValueError: pass
    for _ in range(4):
        # NUNCA subdivide a borda: a costura frente/costas casa os pontos
        # originais do contorno — ponto novo na borda vira buraco na emenda
        arestas = [e for e in bm.edges
                   if e.calc_length() > 0.013 and len(e.link_faces) >= 2]
        if not arestas: break
        bmesh.ops.subdivide_edges(bm, edges=arestas, cuts=1)
        bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bm.verts.ensure_lookup_table(); bm.verts.index_update()
    nb = len(contorno)

    # relaxa o interior no plano: triângulos uniformes = sombreado limpo
    borda_set = set(range(nb))
    for _ in range(40):
        novos = {}
        for v in bm.verts:
            if v.index in borda_set or not v.link_edges: continue
            m = sum((e.other_vert(v).co for e in v.link_edges), Vector()) / len(v.link_edges)
            novos[v] = Vector((m.x, 0.0, m.z))
        for v, c in novos.items(): v.co = c

    def dist_borda(p):
        d = 1e9
        for i in range(nb):
            a, b = contorno[i], contorno[(i + 1) % nb]
            ab = b - a
            if ab.length_squared < 1e-12: continue
            t = max(0.0, min(1.0, (p - a).dot(ab) / ab.length_squared))
            d = min(d, (p - a - ab * t).length)
        return d

    def relevo(p, lado):
        u = min(dist_borda(p) / escala_d, 1.0)
        # a borda já nasce com 52% da espessura: costura com corpo, sem fio de faca
        cupula = fundura * (0.52 + 0.48 * (1 - (1 - u) ** 2))
        dobras = (0.0075 * noise.noise(Vector((p.x * 4, lado * 2.7, p.z * 7))) +
                  0.0060 * noise.noise(Vector((p.x * 1.7, lado * 1.3, p.z * 2.6)))) * u
        return cupula + dobras

    frente_ids = [v.index for v in bm.verts]
    pos = {v.index: v.co.copy() for v in bm.verts}
    for v in bm.verts:
        v.co.y = y_centro - relevo(pos[v.index], 1.0)
    mapa = {}
    for i in frente_ids:
        p = pos[i]
        mapa[i] = bm.verts.new(Vector((p.x, y_centro + relevo(p, -1.0) * 0.92, p.z)))
    bm.faces.ensure_lookup_table()
    for f in list(bm.faces):
        if all(v.index < len(frente_ids) for v in f.verts):
            try: bm.faces.new([mapa[v.index] for v in reversed(f.verts)])
            except ValueError: pass
    bm.verts.ensure_lookup_table()
    for i in range(nb):
        j = (i + 1) % nb
        try: bm.faces.new((bm.verts[i], bm.verts[j], mapa[j], mapa[i]))
        except (ValueError, KeyError): pass
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    malha = bpy.data.meshes.new(nome)
    bm.to_mesh(malha); bm.free()
    o = bpy.data.objects.new(nome, malha)
    bpy.context.collection.objects.link(o)
    for pl in malha.polygons: pl.use_smooth = True
    malha.materials.append(mat)
    sub = o.modifiers.new('Sub', 'SUBSURF'); sub.levels = 2; sub.render_levels = 2
    return o

def novo_mat(nome):
    m = bpy.data.materials.new(nome); m.use_nodes = True
    nt = m.node_tree
    b = nt.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (0.80, 0.80, 0.80, 1)
    b.inputs['Roughness'].default_value = 0.9
    try: b.inputs['Sheen Weight'].default_value = 0.25
    except KeyError: pass
    ruido = nt.nodes.new('ShaderNodeTexNoise')
    ruido.inputs['Scale'].default_value = 260
    ruido.inputs['Detail'].default_value = 3
    bump = nt.nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 0.14
    nt.links.new(ruido.outputs['Fac'], bump.inputs['Height'])
    nt.links.new(bump.outputs['Normal'], b.inputs['Normal'])
    return m

mat_corpo, mat_mangas, mat_gola = novo_mat('corpo'), novo_mat('mangas'), novo_mat('gola')
pecas = [
    peca('corpo',   CORPO_PTS,  0.035, 0.080, 0.000, mat_corpo),
    peca('mangaD',  MANGA_D_PTS,0.028, 0.032, 0.013, mat_mangas),
    peca('mangaE',  MANGA_E_PTS,0.028, 0.032, 0.013, mat_mangas),
    peca('punhoD',  PUNHO_D_PTS,0.022, 0.016, 0.009, mat_gola),
    peca('punhoE',  PUNHO_E_PTS,0.022, 0.016, 0.009, mat_gola),
]

# gola: um tubo que acompanha o decote, abraçando a borda da peça
neck = bez((152,84),(176,72),(264,72),(288,84), 10)
cen = sum(neck, Vector()) / len(neck)
cu = bpy.data.curves.new('golacurva', 'CURVE')
cu.dimensions = '3D'
sp = cu.splines.new('BEZIER')
sp.bezier_points.add(len(neck) - 1)
for bp, p in zip(sp.bezier_points, neck):
    q = cen + (p - cen) * 1.02
    bp.co = Vector((q.x, -0.010, q.z))
    bp.handle_left_type = bp.handle_right_type = 'AUTO'
cu.bevel_depth = 0.012
cu.bevel_resolution = 6
cu.use_fill_caps = True
gola = bpy.data.objects.new('gola', cu)
bpy.context.collection.objects.link(gola)
gola.data.materials.append(mat_gola)

# cordões de costura: tubos finos cobrindo a emenda corpo-manga
def cordao(nome, pts, ybias, bevel, mat):
    cu = bpy.data.curves.new(nome, 'CURVE')
    cu.dimensions = '3D'
    sp = cu.splines.new('BEZIER')
    sp.bezier_points.add(len(pts) - 1)
    for bp, p in zip(sp.bezier_points, pts):
        bp.co = Vector((p.x, ybias, p.z))
        bp.handle_left_type = bp.handle_right_type = 'AUTO'
    cu.bevel_depth = bevel
    cu.bevel_resolution = 5
    cu.use_fill_caps = True
    o = bpy.data.objects.new(nome, cu)
    bpy.context.collection.objects.link(o)
    o.data.materials.append(mat)
    return o

cava_d = bez((314,100),(312,130),(308,160),(303,190), 8)
cava_e = [Vector((-p.x, p.y, p.z)) for p in cava_d]
costuras = [
    cordao('cavaD', cava_d, -0.005, 0.0048, mat_mangas),
    cordao('cavaE', cava_e, -0.005, 0.0048, mat_mangas),
]

def luz(nome, loc, rot, energia, tam):
    d = bpy.data.lights.new(nome, 'AREA'); d.energy = energia; d.size = tam
    o = bpy.data.objects.new(nome, d); o.location = loc; o.rotation_euler = rot
    bpy.context.collection.objects.link(o)
luz('chave',    (-1.0, -1.5, 1.1), (math.radians(50), 0, math.radians(-28)), 520, 0.7)
luz('preenche', (1.3, -1.3, 0.35), (math.radians(72), 0, math.radians(40)), 45, 1.9)
luz('topo',     (0, -0.6, 1.6),    (math.radians(12), 0, 0), 40, 1.6)

cam_d = bpy.data.cameras.new('cam'); cam_d.lens = 85
cam = bpy.data.objects.new('cam', cam_d)
cam.location = (0, -2.35, (480 - 268) * S)
cam.rotation_euler = (math.radians(90), 0, 0)
bpy.context.collection.objects.link(cam)
cena = bpy.context.scene
cena.camera = cam
cena.render.engine = 'CYCLES'
cena.cycles.samples = int(sys.argv[-2]) if sys.argv[-2].isdigit() else 64
cena.cycles.use_denoising = True
cena.render.film_transparent = True
cena.render.resolution_x = 880
cena.render.resolution_y = 1060
mundo = bpy.data.worlds.new('w'); mundo.use_nodes = True
mundo.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.10
cena.world = mundo

saida = sys.argv[-1]
todos = pecas + [gola] + costuras
cena.render.filepath = saida + 'base-frente.png'
bpy.ops.render.render(write_still=True)
for o in todos:
    o.rotation_euler.z = math.pi
cena.render.filepath = saida + 'base-costas.png'
bpy.ops.render.render(write_still=True)

def emissivo(mat, rgb):
    mat.node_tree.nodes.clear()
    out = mat.node_tree.nodes.new('ShaderNodeOutputMaterial')
    em = mat.node_tree.nodes.new('ShaderNodeEmission')
    em.inputs['Color'].default_value = (*rgb, 1); em.inputs['Strength'].default_value = 1
    mat.node_tree.links.new(em.outputs[0], out.inputs[0])
for m, rgb in ((mat_corpo,(1,0,0)), (mat_mangas,(0,1,0)), (mat_gola,(0,0,1))):
    emissivo(m, rgb)
for o in bpy.data.objects:
    if o.type == 'LIGHT': o.hide_render = True
mundo.node_tree.nodes['Background'].inputs['Strength'].default_value = 0
cena.cycles.samples = 16; cena.cycles.use_denoising = False
cena.render.filepath = saida + 'mask-costas.png'
bpy.ops.render.render(write_still=True)
for o in todos:
    o.rotation_euler.z = 0
cena.render.filepath = saida + 'mask-frente.png'
bpy.ops.render.render(write_still=True)
print('RENDERS OK')
