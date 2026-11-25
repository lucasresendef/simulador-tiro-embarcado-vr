using UnityEditor;
using UnityEngine;

// Menu TCC → Qualidade VR: escolhe entre a versão completa (padrão) e a reduzida, para testar
// o VR em PC sem placa de vídeo dedicada. A opção ativa aparece marcada. Vale a partir do
// próximo Play (ou do próximo executável gerado). Detalhes em TCC/VR/QualidadeVR.cs.
static class MenuQualidadeVR
{
    const string Pasta = "Assets/TCC/Resources";
    const string Caminho = Pasta + "/" + QualidadeVR.NomeNoResources + ".asset";

    const string MenuCompleta = "TCC/Qualidade VR/Completa (padrão)";
    const string MenuReduzida = "TCC/Qualidade VR/Reduzida (teste em PC sem placa de vídeo)";

    [MenuItem(MenuCompleta, false, 0)]
    static void Completa() => Definir(QualidadeVR.Modo.Completa);

    [MenuItem(MenuCompleta, true)]
    static bool ValidarCompleta()
    {
        Menu.SetChecked(MenuCompleta, QualidadeVR.Atual == QualidadeVR.Modo.Completa);
        return true;
    }

    [MenuItem(MenuReduzida, false, 1)]
    static void Reduzida() => Definir(QualidadeVR.Modo.Reduzida);

    [MenuItem(MenuReduzida, true)]
    static bool ValidarReduzida()
    {
        Menu.SetChecked(MenuReduzida, QualidadeVR.Atual == QualidadeVR.Modo.Reduzida);
        return true;
    }

    static void Definir(QualidadeVR.Modo modo)
    {
        var config = AssetDatabase.LoadAssetAtPath<QualidadeVR>(Caminho);
        if (config == null)
        {
            if (!AssetDatabase.IsValidFolder(Pasta)) AssetDatabase.CreateFolder("Assets/TCC", "Resources");
            config = ScriptableObject.CreateInstance<QualidadeVR>();
            AssetDatabase.CreateAsset(config, Caminho);
        }
        config.modo = modo;
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();

        string quando = EditorApplication.isPlaying ? " Vale a partir do próximo Play." : "";
        Debug.Log(modo == QualidadeVR.Modo.Reduzida
            ? "[Qualidade VR] Versão REDUZIDA escolhida (teste em PC sem placa de vídeo)." + quando
            : "[Qualidade VR] Versão COMPLETA escolhida (padrão)." + quando);
    }
}
