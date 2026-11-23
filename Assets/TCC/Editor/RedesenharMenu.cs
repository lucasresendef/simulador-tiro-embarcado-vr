using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

// Visual do menu inicial: céu HDRI e pós-processamento da cidade, helicóptero ao fundo, painel escuro
// translúcido com cabeçalho, botões com borda em destaque e fontes Roboto dinâmicas (com ç, ã etc.).
// Mantém os botões e a lógica do GameStartMenu; só muda a aparência.
public static class RedesenharMenu
{
    const string Cena = "Assets/Scenes/1 Start Scene.unity";
    const string PastaFontes = "Assets/TCC/Cenario/Fontes";
    const string Helicoptero = "Assets/Eurocopter AS350 Écureuil (LF).prefab";

    static readonly Color CorPainel = new Color(0.03f, 0.055f, 0.10f, 0.80f);
    static readonly Color CorBotao = new Color(0.09f, 0.14f, 0.23f, 0.96f);
    static readonly Color CorDestaque = new Color(0.96f, 0.66f, 0.18f, 1f);
    static readonly Color CorRealce = new Color(0.82f, 0.50f, 0.10f, 1f);
    static readonly Color CorTextoSecundario = new Color(0.78f, 0.83f, 0.90f, 1f);
    const string HexSecundario = "#C7D2E0";

    [MenuItem("TCC/Redesenhar menu inicial")]
    public static void Run()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var cena = EditorSceneManager.OpenScene(Cena);
        Aplicar(cena);
        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        Debug.Log("[TCC] Menu inicial redesenhado.");
    }

    // Abre o menu inicial e destaca o arquivo da cena na janela Project (uso: -executeMethod RedesenharMenu.MostrarCena)
    public static void MostrarCena()
    {
        EditorApplication.delayCall += () =>
        {
            EditorSceneManager.OpenScene(Cena);
            var arquivo = AssetDatabase.LoadAssetAtPath<SceneAsset>(Cena);
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = arquivo;
            EditorGUIUtility.PingObject(arquivo);
        };
    }

    // Só abre a cena do menu inicial no editor (uso: -executeMethod RedesenharMenu.AbrirMenu)
    public static void AbrirMenu()
    {
        EditorApplication.delayCall += () => EditorSceneManager.OpenScene(Cena);
    }

    // Abre o menu inicial e entra no modo Play (uso: -executeMethod RedesenharMenu.AbrirEJogar)
    public static void AbrirEJogar()
    {
        EditorApplication.delayCall += () =>
        {
            EditorSceneManager.OpenScene(Cena);
            EditorApplication.EnterPlaymode();
        };
    }

    // Uso em batchmode: aplica sem salvar e grava imagens das páginas do menu em SAIDA_MENU
    public static void Previsualizar()
    {
        string pasta = Environment.GetEnvironmentVariable("SAIDA_MENU") ?? "Temp/menu";
        Directory.CreateDirectory(pasta);
        var cena = EditorSceneManager.OpenScene(Cena);
        Aplicar(cena);
        var menu = UnityEngine.Object.FindFirstObjectByType<GameStartMenu>(FindObjectsInactive.Include);
        Renderizar(Path.Combine(pasta, "menu_principal.png"));
        menu.mainMenu.SetActive(false);
        menu.dificultySelection.SetActive(true);
        Renderizar(Path.Combine(pasta, "menu_cenarios.png"));
        menu.dificultySelection.SetActive(false);
        menu.about.SetActive(true);
        Renderizar(Path.Combine(pasta, "menu_sobre.png"));
        menu.about.SetActive(false);
        var instrucoes = menu.GetComponent<MenuInstrucoes>();
        if (instrucoes != null)
        {
            instrucoes.pagina.SetActive(true);
            Renderizar(Path.Combine(pasta, "menu_instrucoes.png"));
        }
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    public static void Aplicar(Scene cena)
    {
        var negrito = Fonte("Roboto-Bold");
        var regular = Fonte("Roboto-Regular");
        var leve = Fonte("Roboto-Light");

        AmbientacaoRealista.Aplicar(cena, 400f, 4000f);
        RenderSettings.fog = false;
        HelicopteroDeFundo(cena);

        var menu = UnityEngine.Object.FindFirstObjectByType<GameStartMenu>(FindObjectsInactive.Include);
        var canvas = menu.GetComponent<Canvas>();
        var titulo3D = cena.GetRootGameObjects().FirstOrDefault(g => g.name == "Game Title");
        if (titulo3D != null) titulo3D.SetActive(false);

        // Painel de fundo, com cantos arredondados
        var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var painel = canvas.transform.Find("Panel").GetComponent<Image>();
        Centralizar(painel.rectTransform, new Vector2(1560f, 1500f), new Vector2(0f, 180f));
        // Centro do painel na altura dos olhos do operador sentado (câmera do rig a 1,36 m)
        canvas.transform.localEulerAngles = Vector3.zero;
        var posicaoCanvas = canvas.transform.position;
        canvas.transform.position = new Vector3(posicaoCanvas.x, 1.15f, posicaoCanvas.z);
        EditorUtility.SetDirty(canvas.transform);
        painel.sprite = sprite;
        painel.type = Image.Type.Sliced;
        painel.pixelsPerUnitMultiplier = 0.12f;
        painel.color = CorPainel;
        painel.raycastTarget = false;
        EditorUtility.SetDirty(painel);

        // Cabeçalho comum a todas as páginas
        var antigo = canvas.transform.Find("[TCC] Cabeçalho");
        if (antigo != null) UnityEngine.Object.DestroyImmediate(antigo.gameObject);
        var cabecalho = new GameObject("[TCC] Cabeçalho", typeof(RectTransform)).GetComponent<RectTransform>();
        cabecalho.SetParent(canvas.transform, false);
        cabecalho.SetSiblingIndex(painel.transform.GetSiblingIndex() + 1);
        Centralizar(cabecalho, new Vector2(1500f, 1700f), Vector2.zero);

        Texto(cabecalho, "Título", "TIRO EMBARCADO", negrito, 150f, Color.white, new Vector2(0f, 790f), new Vector2(1500f, 190f)).characterSpacing = 6f;
        Texto(cabecalho, "Subtítulo", "Simulador de treinamento em Realidade Virtual  ·  alvos móveis", leve, 52f, CorDestaque, new Vector2(0f, 660f), new Vector2(1500f, 80f));
        var linha = new GameObject("Linha", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        linha.transform.SetParent(cabecalho, false);
        Centralizar(linha.rectTransform, new Vector2(240f, 6f), new Vector2(0f, 600f));
        linha.color = CorDestaque;
        linha.raycastTarget = false;
        Texto(cabecalho, "Rodapé", "TCC  ·  Faculdade de Computação  ·  Universidade Federal de Uberlândia", leve, 36f, CorTextoSecundario, new Vector2(0f, -505f), new Vector2(1500f, 60f));

        Instrucoes(menu, canvas, regular, leve);

        // Botões
        foreach (var botao in canvas.GetComponentsInChildren<Button>(true))
            EstilizarBotao(botao, sprite, regular);

        // Descrição dos cenários e título da página de seleção
        string[] descricoes =
        {
            "Veículos e pessoas em fuga na área urbana",
            "Suspeito em fuga na rodovia, seguido por viaturas",
            "Piloto automático em órbita sobre a área de treino",
        };
        string[] nomes = { "Cidade", "Perseguição", "Cidade + piloto" };
        for (int i = 0; i < menu.dificultyButtons.Length && i < nomes.Length; i++)
        {
            var rotulo = menu.dificultyButtons[i].GetComponentInChildren<TMP_Text>(true);
            rotulo.text = $"{nomes[i]}\n<size=50%><color={HexSecundario}>{descricoes[i]}</color></size>";
            rotulo.fontSize = 80f;
            rotulo.lineSpacing = -12f;
            EditorUtility.SetDirty(rotulo);
        }
        var paginaCenarios = menu.dificultySelection.transform;
        var tituloAntigo = paginaCenarios.Find("[TCC] Título da página");
        if (tituloAntigo != null) UnityEngine.Object.DestroyImmediate(tituloAntigo.gameObject);
        Texto(paginaCenarios, "[TCC] Título da página", "Escolha o cenário de treinamento", regular, 54f, CorTextoSecundario, new Vector2(0f, 480f), new Vector2(1300f, 80f));

        // Textos das páginas de opções e sobre (antes pretos sobre fundo claro)
        foreach (var texto in menu.options.GetComponentsInChildren<TMP_Text>(true).Concat(menu.about.GetComponentsInChildren<TMP_Text>(true)))
        {
            if (texto.GetComponentInParent<TMP_Dropdown>(true) != null || texto.GetComponentInParent<Button>(true) != null) continue;
            texto.font = regular;
            texto.color = Color.white;
            EditorUtility.SetDirty(texto);
        }
        foreach (var texto in menu.options.GetComponentsInChildren<TMP_Dropdown>(true).SelectMany(d => d.GetComponentsInChildren<TMP_Text>(true)))
        {
            texto.font = regular;
            EditorUtility.SetDirty(texto);
        }

        // Página "Sobre": texto centralizado e botão de voltar
        var textoSobre = menu.about.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t => t.GetComponentInParent<Button>(true) == null);
        if (textoSobre != null)
        {
            textoSobre.text = "Simulador de Realidade Virtual para o treinamento de tiro embarcado em helicóptero, desenvolvido por Caetano (2024) e estendido neste trabalho com alvos móveis, cenário de perseguição e piloto automático.";
            textoSobre.fontStyle = FontStyles.Normal;
            textoSobre.alignment = TextAlignmentOptions.Center;
            textoSobre.enableAutoSizing = false;
            textoSobre.fontSize = 54f;
            textoSobre.lineSpacing = 8f;
            textoSobre.textWrappingMode = TextWrappingModes.Normal;
            Centralizar(textoSobre.rectTransform, new Vector2(1250f, 480f), new Vector2(0f, 150f));
            EditorUtility.SetDirty(textoSobre);
        }
        foreach (var rotulo in menu.about.GetComponentsInChildren<Button>(true).SelectMany(b => b.GetComponentsInChildren<TMP_Text>(true)))
        {
            rotulo.text = "Voltar";
            EditorUtility.SetDirty(rotulo);
        }
        EditorUtility.SetDirty(menu);
    }

    const string HexDestaque = "#F5A82E";

    // Botão "Instruções" ao lado de "Opções" e a página com os comandos
    static void Instrucoes(GameStartMenu menu, Canvas canvas, TMP_FontAsset regular, TMP_FontAsset leve)
    {
        foreach (var nome in new[] { "[TCC] Instruções", "[TCC] Página de instruções" })
        {
            var velho = canvas.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == nome);
            if (velho != null) UnityEngine.Object.DestroyImmediate(velho.gameObject);
        }

        // Opções e Instruções dividem a segunda linha, como Sobre e Sair na terceira
        var opcoes = menu.optionButton.GetComponent<RectTransform>();
        opcoes.sizeDelta = new Vector2(444f, opcoes.sizeDelta.y);
        opcoes.anchoredPosition = new Vector2(237f, opcoes.anchoredPosition.y);
        var botaoInstrucoes = UnityEngine.Object.Instantiate(menu.optionButton.gameObject, opcoes.parent).GetComponent<Button>();
        botaoInstrucoes.name = "[TCC] Instruções";
        botaoInstrucoes.onClick = new Button.ButtonClickedEvent();
        var rtInstrucoes = botaoInstrucoes.GetComponent<RectTransform>();
        rtInstrucoes.anchoredPosition = new Vector2(-237f, opcoes.anchoredPosition.y);
        botaoInstrucoes.GetComponentInChildren<TMP_Text>(true).text = "Instruções";

        // Página
        var pagina = new GameObject("[TCC] Página de instruções", typeof(RectTransform)).GetComponent<RectTransform>();
        pagina.SetParent(canvas.transform, false);
        Centralizar(pagina, new Vector2(1500f, 1300f), Vector2.zero);
        Texto(pagina, "Título da página", "Como usar", regular, 54f, CorTextoSecundario, new Vector2(0f, 480f), new Vector2(1300f, 80f));
        var objetivo = Texto(pagina, "Objetivo", "Atinja os veículos e as pessoas em fuga. Acertar viaturas ou civis conta como erro de identificação.",
            leve, 36f, Color.white, new Vector2(0f, 385f), new Vector2(1200f, 100f));
        objetivo.textWrappingMode = TextWrappingModes.Normal;

        string titulo(string t) => $"<b><color={HexDestaque}>{t}</color></b>";
        string esquerda =
            titulo("Com óculos (VR)") + "\n" +
            "Segure o fuzil com as mãos e atire pelo gatilho do controle.\n\n" +
            titulo("Sem óculos (mouse e teclado)") + "\n" +
            "Mouse: mirar\n" +
            "Clique esquerdo: atirar\n" +
            "Botão direito ou Z: zoom\n" +
            "T: recarregar\n" +
            "H: câmera externa do helicóptero\n" +
            "Esc: liberar o cursor";
        string direita =
            titulo("Piloto automático") + "\n" +
            "P: ligar ou desligar\n" +
            "O: trocar o padrão de voo\n(órbita, linear, pairado e acompanhamento)\n\n" +
            titulo("Pilotagem manual") + "\n" +
            "F1 / F2: ligar / desligar o motor\n" +
            "Alt direito / esquerdo: subir / descer\n(Option no Mac)\n" +
            "W A S D ou setas: cíclico\n" +
            "Q / E: pedais\n" +
            "R: reinicia o cenário";
        foreach (var (nome, texto, x) in new[] { ("Coluna esquerda", esquerda, -365f), ("Coluna direita", direita, 365f) })
        {
            var coluna = Texto(pagina, nome, texto, regular, 38f, Color.white, new Vector2(x, 15f), new Vector2(680f, 620f));
            coluna.alignment = TextAlignmentOptions.TopLeft;
            coluna.textWrappingMode = TextWrappingModes.Normal;
            coluna.lineSpacing = 6f;
        }

        var botaoVoltar = UnityEngine.Object.Instantiate(menu.optionButton.gameObject, pagina).GetComponent<Button>();
        botaoVoltar.name = "Voltar";
        botaoVoltar.onClick = new Button.ButtonClickedEvent();
        var rtVoltar = botaoVoltar.GetComponent<RectTransform>();
        Centralizar(rtVoltar, new Vector2(400f, 140f), new Vector2(0f, -395f));
        var rotuloVoltar = botaoVoltar.GetComponentInChildren<TMP_Text>(true);
        rotuloVoltar.text = "Voltar";
        rotuloVoltar.fontSize = 60f;
        pagina.gameObject.SetActive(false);

        // Setas de voltar no canto superior esquerdo de cada página secundária
        var setas = new[] { menu.dificultySelection.transform, menu.options.transform, menu.about.transform, pagina.transform }
            .Select(p => SetaVoltar(p)).ToArray();

        var navegacao = canvas.GetComponent<MenuInstrucoes>() ?? canvas.gameObject.AddComponent<MenuInstrucoes>();
        navegacao.setasVoltar = setas;
        navegacao.menu = menu;
        navegacao.pagina = pagina.gameObject;
        navegacao.abrir = botaoInstrucoes;
        navegacao.voltar = botaoVoltar;
        EditorUtility.SetDirty(navegacao);
        EditorUtility.SetDirty(opcoes);
    }

    // Botão quadrado com um ícone de seta para a esquerda desenhado com três traços
    static Button SetaVoltar(Transform pagina)
    {
        var antiga = pagina.Find("[TCC] Voltar");
        if (antiga != null) UnityEngine.Object.DestroyImmediate(antiga.gameObject);
        var botao = new GameObject("[TCC] Voltar", typeof(RectTransform), typeof(Image), typeof(Button)).GetComponent<Button>();
        botao.transform.SetParent(pagina, false);
        Centralizar(botao.GetComponent<RectTransform>(), new Vector2(110f, 110f), new Vector2(-640f, 500f));
        botao.targetGraphic = botao.GetComponent<Image>();
        void Traco(string nome, Vector2 tamanho, Vector2 posicao, float angulo)
        {
            var traco = new GameObject(nome, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            traco.transform.SetParent(botao.transform, false);
            Centralizar(traco.rectTransform, tamanho, posicao);
            traco.rectTransform.localEulerAngles = new Vector3(0f, 0f, angulo);
            traco.color = Color.white;
            traco.raycastTarget = false;
        }
        Traco("Haste", new Vector2(52f, 9f), new Vector2(6f, 0f), 0f);
        Traco("Ponta de cima", new Vector2(32f, 9f), new Vector2(-9f, 10f), 45f);
        Traco("Ponta de baixo", new Vector2(32f, 9f), new Vector2(-9f, -10f), -45f);
        return botao;
    }

    static void EstilizarBotao(Button botao, Sprite sprite, TMP_FontAsset fonte)
    {
        var imagem = botao.GetComponent<Image>();
        imagem.sprite = sprite;
        imagem.type = Image.Type.Sliced;
        imagem.pixelsPerUnitMultiplier = 0.35f;
        imagem.color = Color.white;
        var cores = botao.colors;
        cores.normalColor = CorBotao;
        cores.highlightedColor = CorRealce;
        cores.pressedColor = new Color(0.62f, 0.38f, 0.08f, 1f);
        cores.selectedColor = CorBotao;
        cores.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);
        cores.colorMultiplier = 1f;
        cores.fadeDuration = 0.08f;
        botao.colors = cores;

        var borda = botao.GetComponent<Outline>() ?? botao.gameObject.AddComponent<Outline>();
        borda.effectColor = new Color(CorDestaque.r, CorDestaque.g, CorDestaque.b, 0.55f);
        borda.effectDistance = new Vector2(3f, -3f);

        foreach (var texto in botao.GetComponentsInChildren<TMP_Text>(true))
        {
            texto.font = fonte;
            texto.color = Color.white;
            texto.enableAutoSizing = false;
            texto.fontSize = Mathf.Min(texto.fontSize, 84f);
            texto.alignment = TextAlignmentOptions.Center;
            EditorUtility.SetDirty(texto);
        }
        EditorUtility.SetDirty(imagem);
        EditorUtility.SetDirty(botao);
    }

    static TextMeshProUGUI Texto(Transform pai, string nome, string conteudo, TMP_FontAsset fonte, float tamanho, Color cor, Vector2 posicao, Vector2 tamanhoCaixa)
    {
        var texto = new GameObject(nome, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        texto.transform.SetParent(pai, false);
        Centralizar(texto.rectTransform, tamanhoCaixa, posicao);
        texto.font = fonte;
        texto.text = conteudo;
        texto.fontSize = tamanho;
        texto.color = cor;
        texto.alignment = TextAlignmentOptions.Center;
        texto.textWrappingMode = TextWrappingModes.NoWrap;
        texto.raycastTarget = false;
        return texto;
    }

    static void Centralizar(RectTransform rt, Vector2 tamanho, Vector2 posicao)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = tamanho;
        rt.anchoredPosition = posicao;
    }

    // Fonte TMP a partir do .ttf do Roboto (licença Apache), com os glifos de Latin-1 já gravados
    // no atlas e modo estático: nada é gerado em tempo de execução (no modo Play, a geração
    // dinâmica falhava em parte das letras, que caíam na fonte reserva itálica)
    static TMP_FontAsset Fonte(string nome)
    {
        string caminho = $"{PastaFontes}/{nome} SDF TCC.asset";
        var fonte = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(caminho);
        if (fonte == null)
        {
            Directory.CreateDirectory(PastaFontes);
            var ttf = AssetDatabase.LoadAssetAtPath<Font>($"Assets/Fonts/roboto/{nome}.ttf");
            fonte = TMP_FontAsset.CreateFontAsset(ttf, 64, 6, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, false);
            fonte.name = $"{nome} SDF TCC";
            AssetDatabase.CreateAsset(fonte, caminho);
            fonte.material.name = fonte.name + " Material";
            AssetDatabase.AddObjectToAsset(fonte.material, fonte);
            fonte.atlasTextures[0].name = fonte.name + " Atlas";
            AssetDatabase.AddObjectToAsset(fonte.atlasTextures[0], fonte);
        }
        if (fonte.atlasPopulationMode != AtlasPopulationMode.Static || fonte.characterTable.Count < 150)
        {
            fonte.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            var caracteres = new System.Text.StringBuilder();
            for (int c = 0x20; c <= 0x7E; c++) caracteres.Append((char)c);
            for (int c = 0xA0; c <= 0xFF; c++) caracteres.Append((char)c);
            caracteres.Append("–—·•“”‘’…");
            fonte.TryAddCharacters(caracteres.ToString(), out string faltando);
            if (!string.IsNullOrEmpty(faltando)) Debug.Log($"[TCC] {nome}: sem glifo para '{faltando}'");
            fonte.atlasPopulationMode = AtlasPopulationMode.Static;
            Debug.Log($"[TCC] {nome}: {fonte.characterTable.Count} caracteres, atlas {fonte.atlasTexture.width}x{fonte.atlasTexture.height}");
        }
        // Reserva sem itálico, caso apareça algum caractere fora do atlas
        var reserva = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/3rd Party Plugins/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (reserva != null) fonte.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { reserva };
        EditorUtility.SetDirty(fonte);
        foreach (var atlas in fonte.atlasTextures) EditorUtility.SetDirty(atlas);
        AssetDatabase.SaveAssets();
        return fonte;
    }

    // Só a parte visual do AS350, sem física, scripts nem câmeras, à direita do painel
    static void HelicopteroDeFundo(Scene cena)
    {
        foreach (var antigo in cena.GetRootGameObjects().Where(g => g.name == "[TCC] Helicóptero do menu"))
            UnityEngine.Object.DestroyImmediate(antigo);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Helicoptero);
        if (prefab == null) return;
        var heli = (GameObject)PrefabUtility.InstantiatePrefab(prefab, cena);
        PrefabUtility.UnpackPrefabInstance(heli, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        heli.name = "[TCC] Helicóptero do menu";
        for (int passada = 0; passada < 6; passada++)
        {
            foreach (var c in heli.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c is Transform || c is MeshFilter || c is MeshRenderer || c is SkinnedMeshRenderer || c is LODGroup) continue;
                try { UnityEngine.Object.DestroyImmediate(c); } catch { }
            }
        }
        heli.transform.position = new Vector3(9.5f, 2.6f, 17f);
        heli.transform.rotation = Quaternion.Euler(6f, -118f, -4f);
    }

    static void Renderizar(string arquivo)
    {
        var camera = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(c => c.name == "Main Camera");
        var canvas = UnityEngine.Object.FindFirstObjectByType<GameStartMenu>(FindObjectsInactive.Include).transform;
        // Mesmo enquadramento do modo sem óculos: só sobe a câmera se o rig não tiver altura,
        // e olha o painel um pouco acima do centro
        var local = camera.transform.localPosition;
        float alturaDoPai = camera.transform.parent != null ? camera.transform.parent.localPosition.y : 0f;
        if (local.y < 0.1f && alturaDoPai < 0.5f) camera.transform.localPosition = new Vector3(local.x, 1.2f, local.z);
        var direcao = canvas.position - camera.transform.position;
        var angulos = Quaternion.LookRotation(direcao).eulerAngles;
        camera.transform.rotation = Quaternion.Euler(Mathf.DeltaAngle(0f, angulos.x) - 8f, angulos.y, 0f);
        Canvas.ForceUpdateCanvases();
        var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        camera.targetTexture = rt;
        camera.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        tex.Apply();
        File.WriteAllBytes(arquivo, tex.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = null;
        UnityEngine.Object.DestroyImmediate(rt);
        UnityEngine.Object.DestroyImmediate(tex);
    }
}
