using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

// Modo de teste sem óculos VR (ex.: rodando no Mac).
// Se nenhum headset estiver ativo, desliga a câmera da cabeça do jogador VR
// e reativa a câmera externa do helicóptero (desativada na cena),
// para que as câmeras do helicóptero (tecla C) apareçam na tela.
// Também desliga o inventário do corpo (BodySocketInventory), que depende do headset.
// Com óculos conectado, não faz nada. Para remover, basta apagar esta pasta.
public static class DesktopTestMode
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (XRSettings.isDeviceActive) return;

        foreach (var cam in Object.FindObjectsOfType<Camera>())
        {
            if (!IsHeadCamera(cam)) continue;
            cam.enabled = false;
            var listener = cam.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;
            Debug.Log($"[Modo teste sem VR] Nenhum óculos VR detectado: câmera '{cam.name}' desativada.");
        }

        // Os scripts de câmera do helicóptero (Silantro) guardam a câmera externa no campo "normalExterior"
        foreach (var behaviour in Object.FindObjectsOfType<MonoBehaviour>())
        {
            var field = behaviour.GetType().GetField("normalExterior", BindingFlags.Public | BindingFlags.Instance);
            if (field == null || !(field.GetValue(behaviour) is Camera exterior)) continue;
            if (exterior.gameObject.activeSelf) continue;
            exterior.gameObject.SetActive(true);
            Debug.Log($"[Modo teste sem VR] Câmera externa '{exterior.name}' reativada.");
        }

        // O inventário preso ao corpo só segue a cabeça do VR e gera erro a cada quadro sem óculos
        foreach (var inventory in Object.FindObjectsOfType<BodySocketInventory>())
        {
            inventory.enabled = false;
            Debug.Log($"[Modo teste sem VR] Inventário do corpo '{inventory.name}' desativado.");
        }
    }

    static bool IsHeadCamera(Camera cam)
    {
        foreach (var component in cam.GetComponents<MonoBehaviour>())
        {
            if (component != null && component.GetType().Name == "TrackedPoseDriver") return true;
        }
        return false;
    }
}
