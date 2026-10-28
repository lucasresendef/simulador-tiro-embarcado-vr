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

    const string PrefabPessoa = "Assets/3rd Party Plugins/Blink/Art/Characters/LowPoly/FREE_HumanLowPoly/Prefabs_Humans/HumanMale_Character_FREE.prefab";

    static readonly Color[] Camisas =
    {
        new(0.92f, 0.92f, 0.9f), new(0.12f, 0.12f, 0.14f), new(0.7f, 0.12f, 0.12f), new(0.15f, 0.25f, 0.55f),
        new(0.25f, 0.45f, 0.25f), new(0.85f, 0.7f, 0.2f), new(0.45f, 0.45f, 0.48f), new(0.55f, 0.3f, 0.6f)
    };
    static readonly Color[] Calcas = { new(0.2f, 0.3f, 0.5f), new(0.12f, 0.12f, 0.14f), new(0.6f, 0.55f, 0.42f), new(0.35f, 0.35f, 0.38f) };
    static readonly Color[] Peles = { new(0.87f, 0.69f, 0.55f), new(0.68f, 0.48f, 0.34f), new(0.45f, 0.3f, 0.2f), new(0.3f, 0.2f, 0.14f) };
    static readonly Color[] Cabelos = { new(0.08f, 0.07f, 0.06f), new(0.3f, 0.18f, 0.1f), new(0.75f, 0.6f, 0.35f), new(0.5f, 0.5f, 0.5f) };

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
    // Pessoa: personagem low poly do Blink com roupa, pele e cabelo sorteados,
    // NavMeshAgent e máquina de estados de fuga
    public static GameObject CriarPessoa(Transform pai, Vector3 posicao, System.Random aleatorio, RuntimeAnimatorController controlador)
    {
        var pessoa = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPessoa), pai);
        pessoa.transform.SetPositionAndRotation(posicao, Quaternion.Euler(0f, (float)aleatorio.NextDouble() * 360f, 0f));
        pessoa.transform.localScale = Vector3.one * (0.95f + (float)aleatorio.NextDouble() * 0.1f);

        int camisa = aleatorio.Next(Camisas.Length), calca = aleatorio.Next(Calcas.Length);
        int pele = aleatorio.Next(Peles.Length), cabelo = aleatorio.Next(Cabelos.Length);
        bool barba = aleatorio.NextDouble() < 0.4, bigode = aleatorio.NextDouble() < 0.3;
        var materialCamisa = MaterialCor($"Pessoa Camisa {camisa + 1}", Camisas[camisa]);
        var materialCalca = MaterialCor($"Pessoa Calça {calca + 1}", Calcas[calca]);
        var materialPele = MaterialCor($"Pessoa Pele {pele + 1}", Peles[pele]);
        var materialCabelo = MaterialCor($"Pessoa Cabelo {cabelo + 1}", Cabelos[cabelo]);
        var materialBotas = MaterialCor("Pessoa Calçado", new Color(0.1f, 0.08f, 0.07f));

        foreach (var parte in pessoa.GetComponentsInChildren<Renderer>(true))
        {
            switch (parte.name)
            {
                case "Starter_Chest": parte.gameObject.SetActive(true); parte.sharedMaterial = materialCamisa; break;
                case "Starter_Pants": parte.gameObject.SetActive(true); parte.sharedMaterial = materialCalca; break;
                case "Starter_Boots": parte.gameObject.SetActive(true); parte.sharedMaterial = materialBotas; break;
                case "Chest": case "Legs": case "Feet": case "Underwear": parte.gameObject.SetActive(false); break;
                case "Arms": case "Hands": case "Neck": case "Head": parte.sharedMaterial = materialPele; break;
                case "Hair7": case "Eyebrows": case "Eyelashes": parte.sharedMaterial = materialCabelo; break;
                case "Beard6": parte.gameObject.SetActive(barba); parte.sharedMaterial = materialCabelo; break;
                case "Moustache4": parte.gameObject.SetActive(bigode); parte.sharedMaterial = materialCabelo; break;
            }
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
