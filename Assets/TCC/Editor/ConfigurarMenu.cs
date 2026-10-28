using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Traduz o menu inicial (herdado de um modelo de VR em inglês) e transforma a seleção de
// dificuldade em seleção de cenário: cidade, perseguição e cidade com piloto automático.
public static class ConfigurarMenu
{
    const string Cena = "Assets/Scenes/1 Start Scene.unity";
    static readonly string[] Cenarios = { "Cidade", "Perseguição", "Cidade + piloto" };

    static readonly Dictionary<string, string> Traducoes = new Dictionary<string, string>
    {
        { "FPS VR", "Tiro Embarcado" },
        { "Start", "Iniciar" },
        { "Options", "Opções" },
        { "Quit", "Sair" },
        { "About", "Sobre" },
        { "Validate", "Confirmar" },
        { "Turn", "Giro" },
        { "Snap", "Por passos" },
    };

    [MenuItem("TCC/Configurar menu inicial")]
    public static void Run()
    {
        var cena = EditorSceneManager.OpenScene(Cena);
        foreach (var texto in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (Traducoes.TryGetValue(texto.text.Trim(), out var traducao)) texto.text = traducao;
            else if (texto.text.StartsWith("This game was made"))
                texto.text = "Simulador de Realidade Virtual para treinamento de tiro embarcado (Caetano, 2024), com alvos móveis e piloto automático.\n\nTCC · FACOM/UFU";
            EditorUtility.SetDirty(texto);
        }

        var menu = Object.FindFirstObjectByType<GameStartMenu>(FindObjectsInactive.Include);
        for (int i = 0; i < menu.dificultyButtons.Length && i < Cenarios.Length; i++)
        {
            var rotulo = menu.dificultyButtons[i].GetComponentInChildren<TMP_Text>(true);
            rotulo.text = Cenarios[i];
            rotulo.enableAutoSizing = true;
            rotulo.fontSizeMin = 8f;
            rotulo.fontSizeMax = rotulo.fontSize;
            EditorUtility.SetDirty(rotulo);
        }
        EditorUtility.SetDirty(menu);

        EditorSceneManager.MarkSceneDirty(cena);
        EditorSceneManager.SaveScene(cena);
        Debug.Log("[TCC] Menu inicial configurado.");
    }
}
