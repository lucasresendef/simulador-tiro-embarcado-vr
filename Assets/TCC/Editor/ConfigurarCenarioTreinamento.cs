using System.Collections.Generic;
using System.Linq;
using Autohand;
using Oyedoyin.Rotary.LowFidelity;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Splines;

// Monta o cenário de alvos móveis na cena principal: NavMesh da área de treinamento,
// rotas dos veículos sobre as ruas do Fantastic City Generator, veículos, pessoas e o
// piloto automático do helicóptero. Pode ser executado de novo: recria tudo do zero.
// Pela linha de comando: Unity -batchmode -quit -projectPath <projeto> -executeMethod ConfigurarCenarioTreinamento.Run
public static class ConfigurarCenarioTreinamento
{
    const string Cena = "Assets/Scenes/cena principal.unity";
    const string NomeRaiz = "[TCC] Treinamento";
    const string PastaCenario = "Assets/TCC/Cenario";
    const string PastaVeiculos = "Assets/Fantastic City Generator/Traffic System/Vehicles/Prefabs/";
    const float FaixaDireita = 2.5f;

    // Circuitos sobre as ruas ao sul do alvo, com os pontos dos waypoints do FCG (centro da rua).
    // Cantos em ângulo reto são cruzamentos; a curva do canto nordeste segue a rua curva do FCG.
    static readonly Vector3[] QuarteiraoLeste =
    {
        new(50, -2f, 300), new(94, -1.6f, 300), new(136, 0, 300), new(196, 0, 300), new(250, 0, 300),
        new(250, 0, 350), new(250, 0, 381), new(249, 0, 385), new(247, 0, 389), new(245, 0, 393), new(241, 0, 396),
        new(237, 0, 398), new(233, 0, 400), new(228, 0, 400), new(200, 0, 400), new(150, 0, 400), new(100, 0, 400),
        new(50, 0, 400), new(50, 0, 387), new(50, -1.4f, 350), new(50, -2f, 313)
    };
    static readonly Vector3[] QuarteiraoOeste =
    {
        new(-50, -1.3f, 300), new(-38, -1.2f, 300), new(0, -1.6f, 300), new(36, -2f, 300), new(50, -2f, 300),
        new(50, -2f, 313), new(50, -1.4f, 350), new(50, 0, 387), new(50, 0, 400), new(36, 0, 400), new(0, 0, 400),
        new(-38, 0, 400), new(-50, 0, 400), new(-50, 0, 387), new(-50, -1.1f, 353), new(-50, -1.3f, 313)
    };

    [MenuItem("TCC/Configurar cenário de treinamento")]
    public static void Run()
    {
        var cena = EditorSceneManager.OpenScene(Cena);
        Physics.SyncTransforms();
        var antigo = GameObject.Find(NomeRaiz);
        if (antigo != null) Object.DestroyImmediate(antigo);

        var alvoFixo = Object.FindFirstObjectByType<AutoGunTarget>();
        var helicoptero = Object.FindFirstObjectByType<Controller>();
        var centroAlvo = alvoFixo.transform.position;
        var centro = new Vector3(centroAlvo.x, AlturaDoSolo(centroAlvo), centroAlvo.z - 20f);

        // O trânsito aleatório do FCG não enxerga os alvos e atravessa os carros das rotas
        var transito = Object.FindFirstObjectByType<TrafficSystem>(FindObjectsInactive.Include);
        if (transito != null) transito.gameObject.SetActive(false);

        var raiz = new GameObject(NomeRaiz);
        var gerenciador = raiz.AddComponent<GerenciadorTreinamento>();
        gerenciador.efeitoAcerto = RecursosTreinamento.EfeitoAcerto;
        gerenciador.efeitoErro = RecursosTreinamento.EfeitoErro;
        var area = new GameObject("Area de Treinamento");
        area.transform.SetParent(raiz.transform);
        area.transform.position = centro;

        GerarNavMesh(area, centroAlvo);

        var rotas = new GameObject("Rotas");
        rotas.transform.SetParent(raiz.transform);
        var leste = CriarRota("Rota Quarteirão Leste", QuarteiraoLeste, rotas.transform);
        var oeste = CriarRota("Rota Quarteirão Oeste", QuarteiraoOeste, rotas.transform);

        LiberarRotas(new[] { leste, oeste });

        var veiculos = new GameObject("Veículos");
        veiculos.transform.SetParent(raiz.transform);
        CriarVeiculo("Tempra", leste, 40f, 0.0f, veiculos.transform);
        CriarVeiculo("GranFury", leste, 55f, 0.5f, veiculos.transform);
        CriarVeiculo("Fiat 147", oeste, 30f, 0.2f, veiculos.transform);
        CriarVeiculo("Vesta", oeste, 50f, 0.7f, veiculos.transform);

        var pessoas = new GameObject("Pessoas");
        pessoas.transform.SetParent(raiz.transform);
        CriarPessoas(8, centroAlvo, pessoas.transform);

        ConfigurarPilotoAutomatico(helicoptero, area.transform);

        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        Debug.Log("[TCC] Cenário de treinamento configurado.");
    }

    static void GerarNavMesh(GameObject area, Vector3 centroAlvo)
    {
        var superficie = area.AddComponent<NavMeshSurface>();
        superficie.collectObjects = CollectObjects.Volume;
        superficie.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        // Volume rente ao chão da praça do alvo, para que ninguém ande sobre telhados
        superficie.center = area.transform.InverseTransformPoint(centroAlvo);
        superficie.size = new Vector3(140f, 12f, 120f);
        superficie.BuildNavMesh();

        if (!AssetDatabase.IsValidFolder(PastaCenario)) AssetDatabase.CreateFolder("Assets/TCC", "Cenario");
        var caminho = PastaCenario + "/NavMesh Area de Treinamento.asset";
        AssetDatabase.DeleteAsset(caminho);
        AssetDatabase.CreateAsset(superficie.navMeshData, caminho);
        EditorUtility.SetDirty(superficie);
    }

    static SplineContainer CriarRota(string nome, Vector3[] rua, Transform pai)
    {
        var objeto = new GameObject(nome);
        objeto.transform.SetParent(pai);
        var container = objeto.AddComponent<SplineContainer>();
        var spline = container.Spline;
        spline.Clear();

        // Nos cruzamentos (curva fechada), troca o canto por pontos antes e depois dele,
        // para manter as retas sobre a rua e arredondar só a esquina
        var pontos = new List<Vector3>();
        for (int i = 0; i < rua.Length; i++)
        {
            var anterior = rua[(i + rua.Length - 1) % rua.Length];
            var atual = rua[i];
            var proximo = rua[(i + 1) % rua.Length];
            float curva = Vector3.Angle(Plano(atual - anterior), Plano(proximo - atual));
            if (curva > 60f)
            {
                pontos.Add(atual + Plano(anterior - atual).normalized * 8f);
                pontos.Add(atual + Plano(proximo - atual).normalized * 8f);
            }
            else pontos.Add(atual);
        }

        for (int i = 0; i < pontos.Count; i++)
        {
            // Mão direita: desloca para a direita do sentido de percurso
            var direcao = Plano(pontos[(i + 1) % pontos.Count] - pontos[(i + pontos.Count - 1) % pontos.Count]).normalized;
            var p = pontos[i] + Vector3.Cross(Vector3.up, direcao) * FaixaDireita;
            p.y = AlturaDaRua(p, pontos[i].y);
            spline.Add(new BezierKnot(p), TangentMode.AutoSmooth);
        }
        spline.Closed = true;
        return container;
    }

    public static GameObject CriarVeiculo(string modelo, SplineContainer rota, float velocidadeKmh, float posicaoInicial, Transform pai)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PastaVeiculos + modelo + ".prefab");
        var veiculo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, pai);
        PrefabUtility.UnpackPrefabInstance(veiculo, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        veiculo.name = $"{modelo} ({rota.name.Replace("Rota ", "")}, {velocidadeKmh:F0} km/h)";

        // Remove o tráfego do FCG: o movimento passa a ser pela rota
        foreach (var script in veiculo.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(script);
        foreach (var roda in veiculo.GetComponentsInChildren<WheelCollider>(true)) Object.DestroyImmediate(roda);
        foreach (var corpo in veiculo.GetComponentsInChildren<Rigidbody>(true).Where(c => c.gameObject != veiculo)) Object.DestroyImmediate(corpo);
        if (!veiculo.TryGetComponent<Rigidbody>(out var rb)) rb = veiculo.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        if (veiculo.GetComponentsInChildren<Collider>(true).Length == 0)
        {
            var limites = new Bounds(veiculo.transform.position, Vector3.zero);
            foreach (var r in veiculo.GetComponentsInChildren<Renderer>()) limites.Encapsulate(r.bounds);
            var caixa = veiculo.AddComponent<BoxCollider>();
            caixa.center = veiculo.transform.InverseTransformPoint(limites.center);
            caixa.size = limites.size;
        }

        veiculo.AddComponent<AlvoTreinamento>().tipo = TipoAlvo.Veiculo;
        var movimento = veiculo.AddComponent<VeiculoAlvo>();
        movimento.rota = rota;
        movimento.velocidadeKmh = velocidadeKmh;
        movimento.posicaoInicial = posicaoInicial;
        movimento.materialFumaca = RecursosTreinamento.MaterialFumaca();
        veiculo.transform.position = rota.EvaluatePosition(posicaoInicial);
        return veiculo;
    }

    // Veículos estáticos do FCG parados nas faixas seriam atravessados pelos alvos: desativa
    static void LiberarRotas(SplineContainer[] rotas)
    {
        var modelos = System.IO.Directory.GetFiles(PastaVeiculos, "*.prefab").Select(System.IO.Path.GetFileNameWithoutExtension).ToArray();
        var amostras = rotas.SelectMany(r => Enumerable.Range(0, 400).Select(i => (Vector3)r.EvaluatePosition(i / 400f))).ToArray();
        foreach (var raiz in EditorSceneManager.GetActiveScene().GetRootGameObjects())
        foreach (var objeto in raiz.GetComponentsInChildren<Transform>(true))
        {
            if (!objeto.gameObject.activeInHierarchy || !modelos.Any(m => objeto.name.StartsWith(m))) continue;
            var p = objeto.position;
            bool naRota = amostras.Any(a => Mathf.Abs(a.y - p.y) < 4f && new Vector2(a.x - p.x, a.z - p.z).magnitude < 4f);
            if (!naRota) continue;
            objeto.gameObject.SetActive(false);
            Debug.Log($"[TCC] Veículo estático desativado por estar na rota: {objeto.name} em {p}.");
        }
    }

    static void CriarPessoas(int quantidade, Vector3 centroAlvo, Transform pai)
    {
        var controlador = RecursosTreinamento.ControladorPessoa();
        var aleatorio = new System.Random(42);
        var posicoes = new List<Vector3>();
        for (int tentativa = 0; tentativa < 400 && posicoes.Count < quantidade; tentativa++)
        {
            float angulo = (float)aleatorio.NextDouble() * Mathf.PI * 2f;
            float distancia = 6f + (float)aleatorio.NextDouble() * 22f;
            var candidato = centroAlvo + new Vector3(Mathf.Cos(angulo), 0f, Mathf.Sin(angulo)) * distancia;
            if (!NavMesh.SamplePosition(candidato, out var hit, 3f, NavMesh.AllAreas)) continue;
            // Espalhadas pela praça, não em grupo
            if (posicoes.Any(p => Vector3.Distance(p, hit.position) < 5f)) continue;
            posicoes.Add(hit.position);
            RecursosTreinamento.CriarPessoa(pai, hit.position, aleatorio, controlador).name = $"Pessoa {posicoes.Count}";
        }
        if (posicoes.Count < quantidade) Debug.LogWarning($"[TCC] Só foi possível posicionar {posicoes.Count} de {quantidade} pessoas na NavMesh.");
    }

    static void ConfigurarPilotoAutomatico(Controller helicoptero, Transform centro)
    {
        var piloto = helicoptero.GetComponent<PilotoAutomatico>();
        if (piloto == null) piloto = helicoptero.gameObject.AddComponent<PilotoAutomatico>();
        piloto.centro = centro;
        EditorUtility.SetDirty(piloto);
    }

    static Vector3 Plano(Vector3 v) => new Vector3(v.x, 0f, v.z);

    // Superfície mais próxima da altura esperada da rua (acima pode haver postes; abaixo, a base do quarteirão)
    static float AlturaDaRua(Vector3 ponto, float alturaEsperada)
    {
        var hits = Physics.RaycastAll(new Vector3(ponto.x, 300f, ponto.z), Vector3.down, 600f, ~0, QueryTriggerInteraction.Ignore);
        return hits.Length == 0 ? alturaEsperada : hits.OrderBy(h => Mathf.Abs(h.point.y - alturaEsperada)).First().point.y;
    }

    static float AlturaDoSolo(Vector3 ponto)
    {
        var hits = Physics.RaycastAll(new Vector3(ponto.x, 300f, ponto.z), Vector3.down, 600f, ~0, QueryTriggerInteraction.Ignore);
        return hits.Length == 0 ? 0f : hits.OrderBy(h => h.distance).First().point.y;
    }
}
