using System.Collections.Generic;
using System.Linq;
using Oyedoyin.Rotary.LowFidelity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

// Cria a cena de perseguição a partir da cena principal: mantém helicóptero, operador,
// iluminação e HUD, troca a cidade por um terreno com morros, bosques e uma rodovia em
// circuito (asfalto, acostamento, faixas e defensas) e coloca o suspeito em fuga,
// as viaturas e o trânsito civil na contramão. Texturas do Poly Haven (CC0).
// Pode ser executado de novo: recria a cena do zero (rode antes o cenário da cidade).
public static class ConfigurarPerseguicao
{
    const string Origem = "Assets/Scenes/cena principal.unity";
    const string Destino = "Assets/Scenes/cena perseguicao.unity";
    const string PolyHaven = "Assets/TCC/Externos/PolyHaven/";
    const string Arvores = "Assets/Fantastic City Generator/Objects/Trees/Prefabs/";
    const float LarguraAsfalto = 7.6f, Acostamento = 1.6f, Faixa = 2.2f, SemiEixoX = 700f, SemiEixoZ = 450f;
    const float TamanhoTerreno = 4000f, AlturaTerreno = 250f, NivelBase = 20f;
    static readonly string[] Remover = { "City-Maker", "Traffic System", "TargetCamp", "[TCC] Treinamento", "_Background 1" };

    [MenuItem("TCC/Criar cena de perseguição")]
    public static void Run()
    {
        // Altura do chão sob o helicóptero na cena original, antes de remover a cidade
        EditorSceneManager.OpenScene(Origem);
        Physics.SyncTransforms();
        var heliOriginal = Object.FindFirstObjectByType<Controller>();
        float chao = Physics.RaycastAll(heliOriginal.transform.position + Vector3.up * 5f, Vector3.down, 50f)
            .Where(h => !h.transform.IsChildOf(heliOriginal.transform)).Select(h => h.point.y).DefaultIfEmpty(0f).Max();

        AssetDatabase.DeleteAsset(Destino);
        AssetDatabase.CopyAsset(Origem, Destino);
        var cena = EditorSceneManager.OpenScene(Destino);
        foreach (var raiz in cena.GetRootGameObjects().Where(r => Remover.Contains(r.name))) Object.DestroyImmediate(raiz);
        // Placar do tambor fixo (que não existe neste cenário)
        foreach (var texto in Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (texto.text.StartsWith("Acertos") && texto.canvas != null) texto.canvas.gameObject.SetActive(false);

        var helicoptero = Object.FindFirstObjectByType<Controller>();
        var baseHeli = helicoptero.transform.position;
        baseHeli.y = chao;
        var tcc = new GameObject("[TCC] Perseguição");

        var centroCircuito = baseHeli + new Vector3(SemiEixoX - 80f, 0f, 0f);
        var eixo = AmostrarEixo(centroCircuito, chao + 0.05f);
        var terreno = CriarTerreno(tcc.transform, baseHeli, centroCircuito, eixo);
        CriarEstrada(tcc.transform, eixo);
        CriarDefensas(tcc.transform, eixo);
        CriarHeliponto(tcc.transform, baseHeli);
        PlantarArvores(terreno, eixo, baseHeli);
        var faixaIda = CriarFaixa("Faixa de fuga", eixo, tcc.transform);
        var faixaVolta = CriarFaixa("Faixa contrária", Enumerable.Reverse(eixo).ToList(), tcc.transform);

        var gerenciador = tcc.AddComponent<GerenciadorTreinamento>();
        ConfigurarCenarioTreinamento.AdicionarEfeitos(tcc);

        // Suspeito sai 60 m à frente do ponto da rodovia mais próximo do heliponto
        var veiculos = new GameObject("Veículos").transform;
        veiculos.SetParent(tcc.transform);
        float comprimento = faixaIda.CalculateLength();
        float inicio = DistanciaMaisProxima(faixaIda, baseHeli, comprimento) + 60f;
        float Posicao(float d) => Mathf.Repeat(d, comprimento) / comprimento;

        var suspeito = ConfigurarCenarioTreinamento.CriarVeiculo("GranFury", faixaIda, 95f, Posicao(inicio), veiculos).GetComponent<VeiculoAlvo>();
        suspeito.name = "Suspeito";
        suspeito.acertosParaParar = 6;
        suspeito.tempoParado = 0f;
        suspeito.zigueZague = 1.2f;

        var viaturas = new[] { 25f, 50f }.Select((atras, i) =>
        {
            var viatura = ConfigurarCenarioTreinamento.CriarVeiculo("Soul", faixaIda, 110f, Posicao(inicio - atras), veiculos);
            viatura.name = $"Viatura {i + 1}";
            viatura.GetComponent<AlvoTreinamento>().proibido = true;
            ColocarGiroflex(viatura);
            return viatura.GetComponent<VeiculoAlvo>();
        }).ToArray();

        var civis = new[] { "Tempra", "Fiat 147", "Vesta", "Furgao", "GranFury-2", "Tempra-2", "Fiat 147-b" };
        for (int i = 0; i < civis.Length; i++)
        {
            var civil = ConfigurarCenarioTreinamento.CriarVeiculo(civis[i], faixaVolta, 60f + 4f * i, (i + 0.5f) / civis.Length, veiculos);
            civil.name = $"Civil {i + 1} ({civis[i]})";
            civil.GetComponent<AlvoTreinamento>().proibido = true;
        }

        var piloto = helicoptero.GetComponent<PilotoAutomatico>();
        if (piloto == null) piloto = helicoptero.gameObject.AddComponent<PilotoAutomatico>();
        var heliponto = new GameObject("Heliponto").transform;
        heliponto.SetParent(tcc.transform);
        heliponto.position = baseHeli;
        piloto.centro = heliponto;
        piloto.alvoAcompanhado = suspeito.transform;
        piloto.padrao = PilotoAutomatico.Padrao.Acompanhamento;
        piloto.alturaPes = 200f;
        piloto.raio = 50f;
        piloto.ligarAoIniciar = true;
        EditorUtility.SetDirty(piloto);

        var perseguicao = tcc.AddComponent<PerseguicaoVeicular>();
        perseguicao.suspeito = suspeito;
        perseguicao.viaturas = viaturas;
        perseguicao.piloto = piloto;

        AmbientacaoRealista.Aplicar(cena, 600f, 3600f);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        AdicionarAoBuild();
        Debug.Log($"[TCC] Cena de perseguição criada: rodovia de {comprimento / 1000f:F1} km.");
    }

    // Eixo da rodovia: um oval irregular (curvas variadas), amostrado a cada ~4 m
    static List<Vector3> AmostrarEixo(Vector3 centro, float altura)
    {
        var controle = new Spline();
        const int Pontos = 28;
        for (int i = 0; i < Pontos; i++)
        {
            float t = Mathf.PI + i * 2f * Mathf.PI / Pontos;
            float r = 1f + 0.12f * Mathf.Sin(3f * t) + 0.07f * Mathf.Sin(5f * t + 1f);
            var p = centro + new Vector3(Mathf.Cos(t) * SemiEixoX * r, 0f, Mathf.Sin(t) * SemiEixoZ * r);
            p.y = altura;
            controle.Add(new BezierKnot(p), TangentMode.AutoSmooth);
        }
        controle.Closed = true;

        float comprimento = controle.GetLength();
        int amostras = Mathf.CeilToInt(comprimento / 4f);
        return Enumerable.Range(0, amostras).Select(i => (Vector3)controle.EvaluatePosition(i / (float)amostras)).ToList();
    }

    // Faixa ao longo do eixo entre os deslocamentos laterais "de" e "ate", com UV em metros
    static void FaixaDeMalha(List<Vector3> eixo, float de, float ate, float altura, List<Vector3> vertices, List<Vector2> uvs, List<int> triangulos, float escalaUv)
    {
        float percorrido = 0f;
        for (int i = 0; i <= eixo.Count; i++)
        {
            var p = eixo[i % eixo.Count];
            var direita = Vector3.Cross(Vector3.up, (eixo[(i + 1) % eixo.Count] - eixo[(i + eixo.Count - 1) % eixo.Count]).normalized);
            if (i > 0) percorrido += Vector3.Distance(eixo[(i - 1) % eixo.Count], p);
            vertices.Add(p + direita * de + Vector3.up * altura);
            vertices.Add(p + direita * ate + Vector3.up * altura);
            uvs.Add(new Vector2(de / escalaUv, percorrido / escalaUv));
            uvs.Add(new Vector2(ate / escalaUv, percorrido / escalaUv));
            if (i == 0) continue;
            int b = vertices.Count - 4;
            triangulos.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
        }
    }

    static Mesh SalvarMalha(string nome, List<Vector3> vertices, List<Vector2> uvs, params List<int>[] submalhas)
    {
        var malha = new Mesh { name = nome, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        malha.SetVertices(vertices);
        malha.SetUVs(0, uvs);
        malha.subMeshCount = submalhas.Length;
        for (int i = 0; i < submalhas.Length; i++) malha.SetTriangles(submalhas[i], i);
        malha.RecalculateNormals();
        malha.RecalculateTangents();
        malha.RecalculateBounds();
        var caminho = $"Assets/TCC/Cenario/{nome}.asset";
        AssetDatabase.DeleteAsset(caminho);
        AssetDatabase.CreateAsset(malha, caminho);
        return malha;
    }

    static GameObject Objeto(string nome, Transform pai, Mesh malha, bool colisor, params Material[] materiais)
    {
        var objeto = new GameObject(nome);
        objeto.transform.SetParent(pai);
        objeto.AddComponent<MeshFilter>().sharedMesh = malha;
        objeto.AddComponent<MeshRenderer>().sharedMaterials = materiais;
        if (colisor) objeto.AddComponent<MeshCollider>().sharedMesh = malha;
        return objeto;
    }

    // Asfalto e acostamento de cascalho (texturas PBR do Poly Haven) e faixas pintadas por cima
    static void CriarEstrada(Transform pai, List<Vector3> eixo)
    {
        var vertices = new List<Vector3>(); var uvs = new List<Vector2>();
        var asfalto = new List<int>(); var acostamento = new List<int>();
        float meia = LarguraAsfalto / 2f;
        FaixaDeMalha(eixo, -meia, meia, 0f, vertices, uvs, asfalto, 4f);
        FaixaDeMalha(eixo, -meia - Acostamento, -meia, -0.03f, vertices, uvs, acostamento, 3f);
        FaixaDeMalha(eixo, meia, meia + Acostamento, -0.03f, vertices, uvs, acostamento, 3f);
        var malha = SalvarMalha("Rodovia", vertices, uvs, asfalto, acostamento);
        Objeto("Rodovia", pai, malha, true,
            MaterialPbr("Asfalto", "asphalt_track", new Vector2(1f, 1f), 0.25f),
            MaterialPbr("Acostamento", "gravel_road", new Vector2(1f, 1f), 0.1f));

        // Linhas de bordo brancas contínuas e linha central amarela tracejada
        var vb = new List<Vector3>(); var ub = new List<Vector2>(); var tb = new List<int>();
        FaixaDeMalha(eixo, -meia + 0.15f, -meia + 0.3f, 0.015f, vb, ub, tb, 1f);
        FaixaDeMalha(eixo, meia - 0.3f, meia - 0.15f, 0.015f, vb, ub, tb, 1f);
        Objeto("Faixas brancas", pai, SalvarMalha("Rodovia faixas brancas", vb, ub, tb), false,
            RecursosTreinamento.MaterialCor("Tinta branca", new Color(0.85f, 0.85f, 0.82f)));
        var tracos = new List<Vector3>(); var ut = new List<Vector2>(); var tt = new List<int>();
        for (int i = 0; i + 1 < eixo.Count; i += 4)
        {
            var frente = (eixo[i + 1] - eixo[i]).normalized;
            var direita = Vector3.Cross(Vector3.up, frente);
            int b = tracos.Count;
            foreach (var lado in new[] { -0.06f, 0.06f })
            {
                tracos.Add(eixo[i] + direita * lado + Vector3.up * 0.015f);
            }
            foreach (var lado in new[] { -0.06f, 0.06f })
            {
                tracos.Add(eixo[i + 1] + direita * lado + Vector3.up * 0.015f);
            }
            ut.AddRange(new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one });
            tt.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
        }
        Objeto("Faixa central", pai, SalvarMalha("Rodovia faixa central", tracos, ut, tt), false,
            RecursosTreinamento.MaterialCor("Tinta amarela", new Color(0.9f, 0.68f, 0.12f)));
    }

    // Defensas metálicas dos dois lados: lâmina contínua e postes a cada 4 m
    static void CriarDefensas(Transform pai, List<Vector3> eixo)
    {
        var vertices = new List<Vector3>(); var uvs = new List<Vector2>(); var triangulos = new List<int>();
        float afastamento = LarguraAsfalto / 2f + Acostamento + 0.4f;
        foreach (var lado in new[] { -1f, 1f })
        {
            // Lâmina: duas faces verticais
            for (int face = 0; face < 2; face++)
            {
                int inicio = vertices.Count;
                for (int i = 0; i <= eixo.Count; i++)
                {
                    var p = eixo[i % eixo.Count];
                    var direita = Vector3.Cross(Vector3.up, (eixo[(i + 1) % eixo.Count] - eixo[(i + eixo.Count - 1) % eixo.Count]).normalized);
                    var base_ = p + direita * (afastamento * lado + (face == 0 ? -0.02f : 0.02f) * lado);
                    vertices.Add(base_ + Vector3.up * 0.5f);
                    vertices.Add(base_ + Vector3.up * 0.85f);
                    uvs.Add(new Vector2(i, 0f)); uvs.Add(new Vector2(i, 1f));
                    if (i == 0) continue;
                    int b = vertices.Count - 4;
                    bool voltadaParaFora = (face == 1) == (lado > 0);
                    if (voltadaParaFora) triangulos.AddRange(new[] { b, b + 1, b + 2, b + 1, b + 3, b + 2 });
                    else triangulos.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
                }
            }
            // Postes
            for (int i = 0; i < eixo.Count; i++)
            {
                var direita = Vector3.Cross(Vector3.up, (eixo[(i + 1) % eixo.Count] - eixo[(i + eixo.Count - 1) % eixo.Count]).normalized);
                var centro = eixo[i] + direita * (afastamento + 0.12f) * lado;
                Caixa(centro + Vector3.up * 0.4f, new Vector3(0.12f, 0.8f, 0.12f), Quaternion.LookRotation(direita), vertices, uvs, triangulos);
            }
        }
        var malha = SalvarMalha("Defensas", vertices, uvs, triangulos);
        var material = RecursosTreinamento.MaterialCor("Aço galvanizado", new Color(0.62f, 0.64f, 0.66f));
        material.SetFloat("_Metallic", 0.7f);
        material.SetFloat("_Smoothness", 0.45f);
        // Visível dos dois lados (lâmina fina e postes gerados por código)
        material.SetFloat("_Cull", 0f);
        material.doubleSidedGI = true;
        EditorUtility.SetDirty(material);
        var objeto = Objeto("Defensas", pai, malha, false, material);
        objeto.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
    }

    static void Caixa(Vector3 centro, Vector3 tamanho, Quaternion rotacao, List<Vector3> vertices, List<Vector2> uvs, List<int> triangulos)
    {
        var faces = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        foreach (var normal in faces)
        {
            var u = Vector3.Cross(normal, normal == Vector3.up || normal == Vector3.down ? Vector3.forward : Vector3.up);
            var v = Vector3.Cross(normal, u);
            int b = vertices.Count;
            foreach (var (a, c) in new[] { (-1f, -1f), (1f, -1f), (-1f, 1f), (1f, 1f) })
            {
                var local = Vector3.Scale(normal * 0.5f + u * 0.5f * a + v * 0.5f * c, tamanho);
                vertices.Add(centro + rotacao * local);
                uvs.Add(new Vector2((a + 1f) / 2f, (c + 1f) / 2f));
            }
            triangulos.AddRange(new[] { b, b + 1, b + 2, b + 2, b + 1, b + 3 });
        }
    }

    static void CriarHeliponto(Transform pai, Vector3 baseHeli)
    {
        var heliponto = GameObject.CreatePrimitive(PrimitiveType.Cube);
        heliponto.name = "Heliponto";
        heliponto.transform.SetParent(pai);
        heliponto.transform.position = baseHeli - Vector3.up * 0.15f;
        heliponto.transform.localScale = new Vector3(26f, 0.3f, 26f);
        const string caminho = "Assets/TCC/Cenario/Heliponto.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/TCC/Cenario/Heliponto.png"));
            material.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(material, caminho);
        }
        heliponto.GetComponent<Renderer>().sharedMaterial = material;
    }

    static Material MaterialPbr(string nome, string textura, Vector2 escala, float lisura)
    {
        var caminho = $"Assets/TCC/Cenario/{nome}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{PolyHaven}{textura}_diff_2k.jpg"));
        material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{PolyHaven}{textura}_nor_gl_2k.jpg"));
        material.EnableKeyword("_NORMALMAP");
        material.SetTextureScale("_BaseMap", escala);
        material.SetFloat("_Smoothness", lisura);
        AssetDatabase.CreateAsset(material, caminho);
        return material;
    }

    static TerrainLayer Camada(string textura, float tamanho, Vector4 tom)
    {
        var caminho = $"Assets/TCC/Cenario/Camada {textura}.terrainlayer";
        var camada = AssetDatabase.LoadAssetAtPath<TerrainLayer>(caminho);
        if (camada != null) return camada;
        camada = new TerrainLayer
        {
            diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PolyHaven}{textura}_diff_2k.jpg"),
            normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{PolyHaven}{textura}_nor_gl_2k.jpg"),
            tileSize = new Vector2(tamanho, tamanho),
            smoothness = 0.05f,
            // As gramas fotografadas tendem ao marrom; o tom puxa para o verde de pasto
            diffuseRemapMax = tom
        };
        AssetDatabase.CreateAsset(camada, caminho);
        return camada;
    }

    // Distância de cada ponto da grade até a rodovia (carimbando a vizinhança de cada amostra do eixo)
    static float[,] DistanciaAteEstrada(List<Vector3> eixo, Vector3 origem, int resolucao, float alcance)
    {
        float celula = TamanhoTerreno / (resolucao - 1);
        var distancia = new float[resolucao, resolucao];
        for (int z = 0; z < resolucao; z++) for (int x = 0; x < resolucao; x++) distancia[z, x] = alcance;
        int raio = Mathf.CeilToInt(alcance / celula);
        for (int i = 0; i < eixo.Count; i += 2)
        {
            int cx = Mathf.RoundToInt((eixo[i].x - origem.x) / celula), cz = Mathf.RoundToInt((eixo[i].z - origem.z) / celula);
            for (int z = Mathf.Max(0, cz - raio); z <= Mathf.Min(resolucao - 1, cz + raio); z++)
            for (int x = Mathf.Max(0, cx - raio); x <= Mathf.Min(resolucao - 1, cx + raio); x++)
            {
                float d = new Vector2(origem.x + x * celula - eixo[i].x, origem.z + z * celula - eixo[i].z).magnitude;
                if (d < distancia[z, x]) distancia[z, x] = d;
            }
        }
        return distancia;
    }

    // Terreno com morros (planos junto à rodovia e ao heliponto) que viram serra no horizonte
    static Terrain CriarTerreno(Transform pai, Vector3 baseHeli, Vector3 centro, List<Vector3> eixo)
    {
        const int Resolucao = 513;
        var origem = new Vector3(centro.x - TamanhoTerreno / 2f, baseHeli.y - NivelBase, centro.z - TamanhoTerreno / 2f);
        var dados = new TerrainData { heightmapResolution = Resolucao, alphamapResolution = 512 };
        dados.size = new Vector3(TamanhoTerreno, AlturaTerreno, TamanhoTerreno);

        var distancia = DistanciaAteEstrada(eixo, origem, Resolucao, 80f);
        float celula = TamanhoTerreno / (Resolucao - 1);
        var alturas = new float[Resolucao, Resolucao];
        for (int z = 0; z < Resolucao; z++)
        for (int x = 0; x < Resolucao; x++)
        {
            float wx = origem.x + x * celula, wz = origem.z + z * celula;
            float relevo = Mathf.PerlinNoise(wx * 0.0011f + 3f, wz * 0.0011f + 7f) * 50f + Mathf.PerlinNoise(wx * 0.004f, wz * 0.004f) * 10f - 15f;
            float doCentro = new Vector2(wx - centro.x, wz - centro.z).magnitude;
            relevo += Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1300f, 2000f, doCentro)) * (90f + Mathf.PerlinNoise(wx * 0.003f, wz * 0.003f) * 80f);
            relevo = Mathf.Max(relevo, -8f);
            float doHeliponto = new Vector2(wx - baseHeli.x, wz - baseHeli.z).magnitude;
            float plano = Mathf.Min(distancia[z, x] - 8f, doHeliponto - 25f);
            float mascara = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 55f, plano));
            alturas[z, x] = (NivelBase - 0.05f + relevo * mascara) / AlturaTerreno;
        }
        dados.SetHeights(0, 0, alturas);

        dados.terrainLayers = new[]
        {
            Camada("grass_ground", 12f, new Vector4(0.78f, 1.05f, 0.6f, 1f)),
            Camada("aerial_grass_rock", 45f, new Vector4(0.85f, 1.05f, 0.75f, 1f)),
            Camada("park_dirt", 8f, Vector4.one)
        };
        var distanciaMapa = DistanciaAteEstrada(eixo, origem, dados.alphamapResolution, 30f);
        float celulaMapa = TamanhoTerreno / (dados.alphamapResolution - 1);
        var mistura = new float[dados.alphamapResolution, dados.alphamapResolution, 3];
        for (int z = 0; z < dados.alphamapResolution; z++)
        for (int x = 0; x < dados.alphamapResolution; x++)
        {
            float wx = origem.x + x * celulaMapa, wz = origem.z + z * celulaMapa;
            float seca = 0.8f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 0.75f, Mathf.PerlinNoise(wx * 0.003f + 11f, wz * 0.003f + 5f)));
            float terra = 0.7f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.7f, 0.88f, Mathf.PerlinNoise(wx * 0.009f + 31f, wz * 0.009f + 17f)));
            terra = Mathf.Max(terra, 0.6f * (1f - Mathf.InverseLerp(6f, 16f, distanciaMapa[z, x])));
            float grama = Mathf.Max(0.05f, 1f - seca - terra);
            float soma = grama + seca + terra;
            mistura[z, x, 0] = grama / soma; mistura[z, x, 1] = seca / soma; mistura[z, x, 2] = terra / soma;
        }
        dados.SetAlphamaps(0, 0, mistura);

        const string caminho = "Assets/TCC/Cenario/Terreno Perseguição.asset";
        AssetDatabase.DeleteAsset(caminho);
        AssetDatabase.CreateAsset(dados, caminho);
        var objeto = Terrain.CreateTerrainGameObject(dados);
        objeto.name = "Terreno";
        objeto.transform.SetParent(pai);
        objeto.transform.position = origem;
        var terreno = objeto.GetComponent<Terrain>();
        if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null)
            terreno.materialTemplate = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.defaultTerrainMaterial;
        terreno.heightmapPixelError = 4f;
        terreno.basemapDistance = 1200f;
        terreno.drawInstanced = true;
        terreno.treeDistance = 1800f;
        terreno.treeBillboardDistance = 400f;
        return terreno;
    }

    // Bosques e árvores esparsas (árvores do FCG como árvores do Terrain, desenhadas em lote)
    static void PlantarArvores(Terrain terreno, List<Vector3> eixo, Vector3 baseHeli)
    {
        var dados = terreno.terrainData;
        var modelos = new[] { "Tree-A-01", "Tree-A-02", "Tree-A-03", "Tree-B-01", "Tree-B-02", "Tree-B-03", "Tree-B-04", "Tree-B-05" }
            .Select(n => AssetDatabase.LoadAssetAtPath<GameObject>(Arvores + n + ".prefab")).Where(m => m != null).ToArray();
        dados.treePrototypes = modelos.Select(m => new TreePrototype { prefab = m }).ToArray();

        var origem = terreno.transform.position;
        var distancia = DistanciaAteEstrada(eixo, origem, 513, 40f);
        float celula = TamanhoTerreno / 512f;
        var aleatorio = new System.Random(11);
        var arvores = new List<TreeInstance>();
        for (int tentativa = 0; tentativa < 120000 && arvores.Count < 4500; tentativa++)
        {
            float nx = (float)aleatorio.NextDouble(), nz = (float)aleatorio.NextDouble();
            float wx = origem.x + nx * TamanhoTerreno, wz = origem.z + nz * TamanhoTerreno;
            if (distancia[Mathf.RoundToInt(nz * 512f), Mathf.RoundToInt(nx * 512f)] < 16f) continue;
            if (new Vector2(wx - baseHeli.x, wz - baseHeli.z).magnitude < 60f) continue;
            // Bosques onde o ruído é alto; fora deles, poucas árvores isoladas
            float bosque = Mathf.PerlinNoise(wx * 0.004f + 71f, wz * 0.004f + 13f);
            if (bosque < 0.5f && aleatorio.NextDouble() > 0.03) continue;
            float escala = 1.3f + (float)aleatorio.NextDouble() * 1.1f;
            arvores.Add(new TreeInstance
            {
                position = new Vector3(nx, dados.GetInterpolatedHeight(nx, nz) / AlturaTerreno, nz),
                prototypeIndex = aleatorio.Next(modelos.Length),
                widthScale = escala,
                heightScale = escala * (0.9f + (float)aleatorio.NextDouble() * 0.3f),
                rotation = (float)aleatorio.NextDouble() * Mathf.PI * 2f,
                color = Color.white,
                lightmapColor = Color.white
            });
        }
        dados.SetTreeInstances(arvores.ToArray(), true);
    }

    // Faixa de rolamento: eixo deslocado para a direita do sentido de percurso (mão direita)
    static SplineContainer CriarFaixa(string nome, List<Vector3> eixo, Transform pai)
    {
        var objeto = new GameObject(nome);
        objeto.transform.SetParent(pai);
        var container = objeto.AddComponent<SplineContainer>();
        var spline = container.Spline;
        spline.Clear();
        for (int i = 0; i < eixo.Count; i += 5)
        {
            var frente = (eixo[(i + 1) % eixo.Count] - eixo[(i + eixo.Count - 1) % eixo.Count]).normalized;
            spline.Add(new BezierKnot(eixo[i] + Vector3.Cross(Vector3.up, frente) * Faixa), TangentMode.AutoSmooth);
        }
        spline.Closed = true;
        return container;
    }

    static float DistanciaMaisProxima(SplineContainer faixa, Vector3 ponto, float comprimento)
    {
        float melhor = 0f, menor = float.MaxValue;
        for (int i = 0; i < 2000; i++)
        {
            float t = i / 2000f;
            float d = Vector3.Distance(faixa.EvaluatePosition(t), ponto);
            if (d < menor) { menor = d; melhor = t; }
        }
        return melhor * comprimento;
    }

    static void ColocarGiroflex(GameObject viatura)
    {
        var limites = viatura.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
        var giroflex = new GameObject("Giroflex");
        giroflex.transform.SetParent(viatura.transform, false);
        giroflex.transform.position = new Vector3(limites.center.x, limites.max.y + 0.08f, limites.center.z);
        Renderer Luz(string nome, float lado, Color cor)
        {
            var luz = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(luz.GetComponent<Collider>());
            luz.name = nome;
            luz.transform.SetParent(giroflex.transform, false);
            luz.transform.localPosition = new Vector3(lado * 0.35f, 0f, 0f);
            luz.transform.localScale = new Vector3(0.6f, 0.15f, 0.25f);
            var renderer = luz.GetComponent<Renderer>();
            renderer.sharedMaterial = RecursosTreinamento.MaterialCor(nome, cor, true);
            return renderer;
        }
        var componente = giroflex.AddComponent<Giroflex>();
        componente.vermelho = Luz("Giroflex vermelho", -1f, new Color(1f, 0.05f, 0.05f));
        componente.azul = Luz("Giroflex azul", 1f, new Color(0.1f, 0.3f, 1f));
    }

    static void AdicionarAoBuild()
    {
        var cenas = EditorBuildSettings.scenes.ToList();
        if (cenas.Any(c => c.path == Destino)) return;
        cenas.Add(new EditorBuildSettingsScene(Destino, true));
        EditorBuildSettings.scenes = cenas.ToArray();
    }
}
