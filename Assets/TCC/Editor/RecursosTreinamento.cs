using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Recursos compartilhados pelos cenários de treinamento (criados uma vez em Assets/TCC/Cenario)
public static class RecursosTreinamento
{
    const string Pasta = "Assets/TCC/Cenario";
    const string Animacoes = "Assets/Silantro Simulator/Silantro/Support/Standard Assets/Characters/ThirdPersonCharacter/Animation/";
    const string AnimacaoMorte = "Assets/3rd Party Plugins/Blink/Art/Animations/Animations_Starter_Pack/Combat/Death.fbx";
    const string Personagens = "Assets/3rd Party Plugins/PolygonStarter/Prefabs/Characters/";

    public static GameObject EfeitoAcerto => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3rd Party Plugins/HitFolder/Prefabs/vfx_BasicHit.prefab");
    public static GameObject EfeitoErro => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3rd Party Plugins/HitFolder/Prefabs/vfx_Spark1.prefab");

    const string PastaRocketbox = "Assets/TCC/Externos/Rocketbox";

    static AnimationClip Clipe(string caminho) =>
        AssetDatabase.LoadAllAssetsAtPath(caminho).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));

    // Parado/andando/correndo misturados pela velocidade real (sem deslizar) e a queda ao ser atingida
    public static RuntimeAnimatorController ControladorPessoa()
    {
        var caminho = Pasta + "/PessoaAnimator.controller";
        AssetDatabase.DeleteAsset(caminho);
        var controlador = AnimatorController.CreateAnimatorControllerAtPath(caminho);
        controlador.AddParameter("Velocidade", AnimatorControllerParameterType.Float);
        controlador.AddParameter("Abatida", AnimatorControllerParameterType.Bool);

        var maquina = controlador.layers[0].stateMachine;
        var locomocao = controlador.CreateBlendTreeInController("Locomoção", out var arvore, 0);
        arvore.blendType = BlendTreeType.Simple1D;
        arvore.blendParameter = "Velocidade";
        arvore.useAutomaticThresholds = false;
        arvore.AddChild(Clipe(Animacoes + "HumanoidIdle.fbx"), 0f);
        arvore.AddChild(Clipe(Animacoes + "HumanoidWalk.fbx"), 1.5f);
        arvore.AddChild(Clipe(Animacoes + "HumanoidRun.fbx"), 5f);
        maquina.defaultState = locomocao;

        var morte = maquina.AddState("Queda");
        morte.motion = Clipe(AnimacaoMorte);
        var cair = maquina.AddAnyStateTransition(morte);
        cair.AddCondition(AnimatorConditionMode.If, 0f, "Abatida");
        cair.canTransitionToSelf = false;
        cair.duration = 0.1f;
        var levantar = morte.AddTransition(locomocao);
        levantar.AddCondition(AnimatorConditionMode.IfNot, 0f, "Abatida");
        levantar.hasExitTime = false;
        levantar.duration = 0.2f;
        AssetDatabase.SaveAssets();
        return controlador;
    }

    public static Material MaterialFumaca()
    {
        var caminho = Pasta + "/Fumaça.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        // Partícula redonda e suave embutida no Unity (a textura de fumaça do HitFolder não tem transparência)
        material.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_ZWrite", 0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        AssetDatabase.CreateAsset(material, caminho);
        return material;
    }

    // Material de partícula (Default-Particle redonda): aditivo para clarão e traçante, alfa para poeira
    public static Material MaterialParticula(string nome, Color cor, bool aditivo)
    {
        var caminho = $"{Pasta}/{nome}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        bool novo = material == null;
        if (novo) material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        // Reaplica as configurações também nos materiais que já existem
        material.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
        material.SetColor("_BaseColor", cor);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", aditivo ? 2f : 0f);
        material.SetOverrideTag("RenderType", "Transparent");
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)(aditivo ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
        material.SetInt("_ZWrite", 0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        if (aditivo) material.EnableKeyword("_BLENDMODE_ADD"); else material.DisableKeyword("_BLENDMODE_ADD");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        if (novo) AssetDatabase.CreateAsset(material, caminho);
        else EditorUtility.SetDirty(material);
        return material;
    }

    public static Material MaterialEstrada()
    {
        var caminho = Pasta + "/Estrada.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Pasta + "/Estrada.png"));
        material.SetFloat("_Smoothness", 0.15f);
        AssetDatabase.CreateAsset(material, caminho);
        return material;
    }

    public static Material MaterialCor(string nome, Color cor, bool emissiva = false)
    {
        var caminho = $"{Pasta}/{nome}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(caminho);
        if (material != null) return material;
        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", cor);
        if (emissiva)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", cor * 3f);
        }
        AssetDatabase.CreateAsset(material, caminho);
        return material;
    }

    // Pessoa: modelo Synty (homem ou mulher) com roupa sorteada, NavMeshAgent e máquina de estados de fuga
    // Personagens realistas da Microsoft Rocketbox Avatar Library (MIT), feitos para pesquisa em RV
    public static GameObject[] ModelosPessoa()
    {
        var modelos = AssetDatabase.FindAssets("t:Model", new[] { PastaRocketbox })
            .Select(AssetDatabase.GUIDToAssetPath).Where(c => c.EndsWith(".fbx")).OrderBy(c => c).ToArray();
        foreach (var modelo in modelos) PrepararMateriais(modelo);
        return modelos.Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToArray();
    }

    // Troca os materiais importados do FBX (que apontam para TGAs que não existem) por materiais
    // URP com as texturas convertidas: corpo, cabeça e cabelo/cílios com transparência recortada
    static void PrepararMateriais(string caminhoModelo)
    {
        var importador = (ModelImporter)AssetImporter.GetAtPath(caminhoModelo);
        if (importador.GetExternalObjectMap().Count > 0) return;
        var pasta = Path.GetDirectoryName(caminhoModelo).Replace('\\', '/');
        var nomeModelo = Path.GetFileNameWithoutExtension(caminhoModelo);
        Texture2D Textura(string parte, string mapa) => Directory.GetFiles(pasta, $"*_{parte}_{mapa}.png")
            .Select(c => AssetDatabase.LoadAssetAtPath<Texture2D>(c.Replace('\\', '/'))).FirstOrDefault();

        foreach (var original in AssetDatabase.LoadAllAssetsAtPath(caminhoModelo).OfType<Material>())
        {
            string parte = original.name.Contains("opacity") ? "opacity" : original.name.Contains("head") ? "head" : original.name.Contains("body") ? "body" : null;
            if (parte == null)
            {
                Debug.LogWarning($"[TCC] Material sem correspondência em {nomeModelo}: {original.name}");
                continue;
            }
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = $"{nomeModelo} {parte}" };
            material.SetTexture("_BaseMap", Textura(parte, "color"));
            material.SetFloat("_Smoothness", 0.25f);
            var normal = Textura(parte, "normal");
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            if (parte == "opacity")
            {
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", 0.5f);
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetFloat("_Cull", 0f);
                material.doubleSidedGI = true;
            }
            var caminhoMaterial = $"{pasta}/{material.name}.mat";
            AssetDatabase.DeleteAsset(caminhoMaterial);
            AssetDatabase.CreateAsset(material, caminhoMaterial);
            importador.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), original.name), material);
        }
        importador.SaveAndReimport();
    }

    static GameObject[] modelosPessoa;

    public static GameObject CriarPessoa(Transform pai, Vector3 posicao, System.Random aleatorio, RuntimeAnimatorController controlador)
    {
        modelosPessoa ??= ModelosPessoa();
        var pessoa = (GameObject)PrefabUtility.InstantiatePrefab(modelosPessoa[aleatorio.Next(modelosPessoa.Length)], pai);
        pessoa.transform.SetPositionAndRotation(posicao, Quaternion.Euler(0f, (float)aleatorio.NextDouble() * 360f, 0f));
        // Garante a escala humana (o FBX pode vir em outra unidade)
        var limites = pessoa.GetComponentsInChildren<Renderer>().Select(r => r.bounds).Aggregate((a, b) => { a.Encapsulate(b); return a; });
        if (limites.size.y < 1.4f || limites.size.y > 2.1f)
        {
            Debug.Log($"[TCC] {pessoa.name}: altura {limites.size.y:F2} m, ajustada para 1,75 m.");
            pessoa.transform.localScale *= 1.75f / limites.size.y;
        }

        var animator = pessoa.GetComponentInChildren<Animator>();
        animator.runtimeAnimatorController = controlador;
        animator.applyRootMotion = false;

        var colisor = pessoa.AddComponent<CapsuleCollider>();
        colisor.center = new Vector3(0f, 0.9f, 0f);
        colisor.height = 1.8f;
        colisor.radius = 0.35f;
        pessoa.AddComponent<Rigidbody>().isKinematic = true;
        var agente = pessoa.AddComponent<UnityEngine.AI.NavMeshAgent>();
        agente.radius = 0.35f;
        agente.height = 1.8f;
        agente.speed = 5f;
        agente.angularSpeed = 540f;
        agente.acceleration = 12f;
        pessoa.AddComponent<AlvoTreinamento>().tipo = TipoAlvo.Pessoa;
        pessoa.AddComponent<PessoaEmFuga>();
        return pessoa;
    }
}
