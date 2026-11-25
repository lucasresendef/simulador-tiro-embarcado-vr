// TEMPORÁRIO (Claude): abre a cena de perseguição e entra em Play para o TesteMiraVR,
// quando existe Temp/teste_mira_vr_play.txt. Pode apagar junto com TCC/VR/TesteMiraVR.cs.
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
static class TesteMiraVRLauncher
{
    const string GatilhoPlay = "Temp/teste_mira_vr_play.txt";

    static TesteMiraVRLauncher()
    {
        if (!File.Exists(GatilhoPlay) || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var modo = File.ReadAllText(GatilhoPlay);
        File.Delete(GatilhoPlay);
        File.WriteAllText(TesteMiraVR.Gatilho, modo);
        EditorApplication.delayCall += () =>
        {
            Debug.Log("[TesteMira] Abrindo a cena de perseguição e entrando em Play.");
            EditorSceneManager.OpenScene("Assets/Scenes/cena perseguicao.unity", OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        };
    }
}
