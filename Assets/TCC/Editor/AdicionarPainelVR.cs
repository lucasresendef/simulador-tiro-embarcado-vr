using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Coloca nas cenas de treinamento o placar dentro do óculos (PainelVR) e a calibração do operador.
// Pode ser executado de novo: recria os dois objetos. Não mexe no resto das cenas.
// Pela linha de comando: Unity -batchmode -quit -projectPath <projeto> -executeMethod AdicionarPainelVR.Run
public static class AdicionarPainelVR
{
    static readonly string[] Cenas = { "Assets/Scenes/cena principal.unity", "Assets/Scenes/cena perseguicao.unity" };
    const string NomePainel = "[TCC] Painel VR";
    const string NomeCalibracao = "[TCC] Calibração do operador";

    [MenuItem("TCC/Adicionar painel VR e calibração")]
    public static void Run()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        foreach (var caminho in Cenas)
        {
            var cena = EditorSceneManager.OpenScene(caminho);
            foreach (var velho in cena.GetRootGameObjects().Where(g => g.name == NomePainel || g.name == NomeCalibracao))
                UnityEngine.Object.DestroyImmediate(velho);
            CriarPainel(cena);
            var calibracao = new GameObject(NomeCalibracao);
            SceneManager.MoveGameObjectToScene(calibracao, cena);
            calibracao.AddComponent<CalibracaoOperador>();
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena);
            Debug.Log($"[TCC] Painel VR e calibração adicionados em {Path.GetFileNameWithoutExtension(caminho)}.");
        }
    }

    static PainelVR CriarPainel(Scene cena)
    {
        var raiz = new GameObject(NomePainel, typeof(RectTransform), typeof(Canvas));
        SceneManager.MoveGameObjectToScene(raiz, cena);
        raiz.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var rt = raiz.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(560f, 250f);
        rt.localScale = Vector3.one * 0.0004f; // cerca de 22 x 9 cm

        var sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Retangulo(raiz.transform, "Fundo", sprite, new Color(0.05f, 0.08f, 0.14f, 0.62f), 0f);

        var texto = new GameObject("Texto", typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        texto.transform.SetParent(raiz.transform, false);
        Esticar(texto.rectTransform, 22f);
        texto.font = RedesenharMenu.Fonte("Roboto-Regular");
        texto.fontSize = 40f;
        texto.lineSpacing = 4f;
        texto.color = Color.white;
        texto.alignment = TextAlignmentOptions.TopLeft;
        texto.textWrappingMode = TextWrappingModes.Normal;
        texto.raycastTarget = false;
        texto.text = "Acertos 0/0";

        var painel = raiz.AddComponent<PainelVR>();
        painel.texto = texto;
        return painel;
    }

    static void Retangulo(Transform pai, string nome, Sprite sprite, Color cor, float margem)
    {
        var imagem = new GameObject(nome, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        imagem.transform.SetParent(pai, false);
        Esticar(imagem.rectTransform, margem);
        imagem.sprite = sprite;
        imagem.type = Image.Type.Sliced;
        imagem.pixelsPerUnitMultiplier = 0.25f;
        imagem.color = cor;
        imagem.raycastTarget = false;
    }

    static void Esticar(RectTransform rt, float margem)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(margem, margem);
        rt.offsetMax = new Vector2(-margem, -margem);
    }

    // Uso em batchmode: monta o painel numa cena vazia com um texto de exemplo e grava em SAIDA_PAINEL
    public static void Previsualizar()
    {
        string arquivo = Environment.GetEnvironmentVariable("SAIDA_PAINEL") ?? "Temp/painel_vr.png";
        var cena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var painel = CriarPainel(cena);
        painel.texto.text =
            "<color=#F5A82E>Acertos</color> <b>7</b>/12\n<color=#FF6A5C>Não autorizados: 1</color>\n" +
            "Suspeito 92 km/h · 2/6\n<size=75%><alpha=#AA>Munição 288</size>";
        painel.transform.position = Vector3.zero;
        var camera = new GameObject("Câmera").AddComponent<Camera>();
        camera.transform.position = new Vector3(0f, 0f, -0.55f);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.45f, 0.6f, 0.78f);
        camera.fieldOfView = 40f;
        Canvas.ForceUpdateCanvases();
        var rt = new RenderTexture(960, 540, 24) { antiAliasing = 4 };
        camera.targetTexture = rt;
        camera.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
        tex.Apply();
        File.WriteAllBytes(arquivo, tex.EncodeToPNG());
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }
}
