using System.Collections.Generic;
using System.Linq;
using Oyedoyin.Rotary.LowFidelity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Splines;

// Cria a cena de perseguição a partir da cena principal: mantém helicóptero, operador,
// iluminação e HUD, troca a cidade por campo aberto com uma rodovia em circuito e coloca
// o suspeito em fuga, as viaturas e o trânsito civil na contramão.
// Pode ser executado de novo: recria a cena do zero (rode antes o cenário da cidade).
public static class ConfigurarPerseguicao
{
    const string Origem = "Assets/Scenes/cena principal.unity";
    const string Destino = "Assets/Scenes/cena perseguicao.unity";
    const string Synty = "Assets/3rd Party Plugins/PolygonStarter/Prefabs/";
    const float LarguraEstrada = 9f, Faixa = 2.2f, SemiEixoX = 700f, SemiEixoZ = 450f;
    static readonly string[] Remover = { "City-Maker", "Traffic System", "TargetCamp", "[TCC] Treinamento" };

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

        CriarSolo(tcc.transform, baseHeli);
        var centroCircuito = baseHeli + new Vector3(SemiEixoX - 80f, 0f, 0f);
        var eixo = AmostrarEixo(centroCircuito, chao + 0.05f);
        CriarEstrada(tcc.transform, eixo);
        var faixaIda = CriarFaixa("Faixa de fuga", eixo, tcc.transform);
        var faixaVolta = CriarFaixa("Faixa contrária", Enumerable.Reverse(eixo).ToList(), tcc.transform);
        CriarPaisagem(tcc.transform, eixo, baseHeli, centroCircuito);

        var gerenciador = tcc.AddComponent<GerenciadorTreinamento>();
        gerenciador.efeitoAcerto = RecursosTreinamento.EfeitoAcerto;
        gerenciador.efeitoErro = RecursosTreinamento.EfeitoErro;

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

        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        AdicionarAoBuild();
        Debug.Log($"[TCC] Cena de perseguição criada: rodovia de {comprimento / 1000f:F1} km.");
    }

    static void CriarSolo(Transform pai, Vector3 baseHeli)
    {
        var solo = GameObject.CreatePrimitive(PrimitiveType.Plane);
        solo.name = "Solo";
        solo.transform.SetParent(pai);
        solo.transform.position = new Vector3(baseHeli.x + 600f, baseHeli.y - 0.02f, baseHeli.z);
        solo.transform.localScale = new Vector3(600f, 1f, 600f);
        solo.GetComponent<Renderer>().sharedMaterial = MaterialGrama();

        var heliponto = GameObject.CreatePrimitive(PrimitiveType.Cube);
        heliponto.name = "Base do heliponto";
        heliponto.transform.SetParent(pai);
        heliponto.transform.position = baseHeli - Vector3.up * 0.15f;
        heliponto.transform.localScale = new Vector3(24f, 0.3f, 24f);
        heliponto.GetComponent<Renderer>().sharedMaterial = RecursosTreinamento.MaterialCor("Concreto", new Color(0.55f, 0.55f, 0.53f));
    }

    static Material MaterialGrama()
    {
        const string caminho = "Assets/TCC/Cenario/Grama.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Fantastic City Generator/Textures/Grass-01.tga"));
        material.SetTextureScale("_BaseMap", new Vector2(800f, 800f));
        material.SetColor("_BaseColor", new Color(0.75f, 0.85f, 0.7f));
        material.SetFloat("_Smoothness", 0f);
        AssetDatabase.CreateAsset(material, caminho);
        return material;
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

    static void CriarEstrada(Transform pai, List<Vector3> eixo)
    {
        var vertices = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangulos = new List<int>();
        float percorrido = 0f;
        for (int i = 0; i <= eixo.Count; i++)
        {
            var p = eixo[i % eixo.Count];
            var frente = (eixo[(i + 1) % eixo.Count] - eixo[(i + eixo.Count - 1) % eixo.Count]).normalized;
            var direita = Vector3.Cross(Vector3.up, frente);
            if (i > 0) percorrido += Vector3.Distance(eixo[(i - 1) % eixo.Count], p);
            vertices.Add(p - direita * LarguraEstrada / 2f);
            vertices.Add(p + direita * LarguraEstrada / 2f);
            uvs.Add(new Vector2(0f, percorrido / 18f));
            uvs.Add(new Vector2(1f, percorrido / 18f));
            if (i == 0) continue;
            int b = vertices.Count - 4;
            triangulos.AddRange(new[] { b, b + 2, b + 1, b + 1, b + 2, b + 3 });
        }

        var malha = new Mesh { name = "Rodovia", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        malha.SetVertices(vertices);
        malha.SetUVs(0, uvs);
        malha.SetTriangles(triangulos, 0);
        malha.RecalculateNormals();
        malha.RecalculateBounds();
        const string caminho = "Assets/TCC/Cenario/Rodovia.asset";
        AssetDatabase.DeleteAsset(caminho);
        AssetDatabase.CreateAsset(malha, caminho);

        var estrada = new GameObject("Rodovia");
        estrada.transform.SetParent(pai);
        estrada.AddComponent<MeshFilter>().sharedMesh = malha;
        estrada.AddComponent<MeshRenderer>().sharedMaterial = RecursosTreinamento.MaterialEstrada();
        estrada.AddComponent<MeshCollider>().sharedMesh = malha;
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

    static void CriarPaisagem(Transform pai, List<Vector3> eixo, Vector3 baseHeli, Vector3 centro)
    {
        var paisagem = new GameObject("Paisagem").transform;
        paisagem.SetParent(pai);
        var aleatorio = new System.Random(7);
        float Sorteio(float a, float b) => a + (float)aleatorio.NextDouble() * (b - a);

        var arvores = new[] { "SM_Generic_Tree_01", "SM_Generic_Tree_02", "SM_Generic_Tree_03", "SM_Generic_Tree_04" }
            .Select(n => AssetDatabase.LoadAssetAtPath<GameObject>(Synty + n + ".prefab")).ToArray();
        var pedras = Enumerable.Range(1, 5).Select(i => AssetDatabase.LoadAssetAtPath<GameObject>($"{Synty}SM_Generic_Small_Rocks_0{i}.prefab")).ToArray();
        var amostrasEstrada = eixo.Where((_, i) => i % 3 == 0).ToArray();

        bool Livre(Vector3 p, float folga) =>
            Vector3.Distance(p, baseHeli) > 45f && amostrasEstrada.All(e => (e - p).sqrMagnitude > folga * folga);

        int colocadas = 0;
        for (int tentativa = 0; tentativa < 12000 && colocadas < 1100; tentativa++)
        {
            var p = centro + new Vector3(Sorteio(-SemiEixoX - 350f, SemiEixoX + 350f), 0f, Sorteio(-SemiEixoZ - 350f, SemiEixoZ + 350f));
            p.y = baseHeli.y;
            // Mais árvores perto da estrada (bosques), menos no campo aberto
            bool pertoDaEstrada = !Livre(p, 60f);
            if (!pertoDaEstrada && aleatorio.NextDouble() < 0.5) continue;
            if (!Livre(p, 14f)) continue;
            var modelo = arvores[aleatorio.Next(arvores.Length)];
            var arvore = (GameObject)PrefabUtility.InstantiatePrefab(modelo, paisagem);
            arvore.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, Sorteio(0f, 360f), 0f));
            arvore.transform.localScale = Vector3.one * Sorteio(2.5f, 5f);
            colocadas++;
        }
        for (int i = 0; i < 120; i++)
        {
            var p = centro + new Vector3(Sorteio(-SemiEixoX - 200f, SemiEixoX + 200f), 0f, Sorteio(-SemiEixoZ - 200f, SemiEixoZ + 200f));
            p.y = baseHeli.y;
            if (!Livre(p, 10f)) continue;
            var pedra = (GameObject)PrefabUtility.InstantiatePrefab(pedras[aleatorio.Next(pedras.Length)], paisagem);
            pedra.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, Sorteio(0f, 360f), 0f));
            pedra.transform.localScale = Vector3.one * Sorteio(1.5f, 4f);
        }

        // Serras no horizonte
        var serra = AssetDatabase.LoadAssetAtPath<GameObject>(Synty + "SM_Generic_Mountains_Grass_02.prefab");
        for (int i = 0; i < 8; i++)
        {
            float angulo = i * Mathf.PI * 2f / 8f + Sorteio(-0.2f, 0.2f);
            var p = centro + new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo)) * Sorteio(1800f, 2300f);
            p.y = baseHeli.y - 2f;
            var montanha = (GameObject)PrefabUtility.InstantiatePrefab(serra, paisagem);
            montanha.transform.SetPositionAndRotation(p, Quaternion.LookRotation(centro - p));
            var limites = montanha.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
            montanha.transform.localScale = Vector3.one * (Sorteio(900f, 1300f) / Mathf.Max(1f, limites.size.x));
        }
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
