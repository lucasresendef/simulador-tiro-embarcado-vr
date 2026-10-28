using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Completa a ambientação em tempo de execução: ilumina a cena pelo céu HDRI
// e garante o pós-processamento em todas as câmeras (inclusive as criadas depois)
public class AmbienteRealista : MonoBehaviour
{
    void Start()
    {
        DynamicGI.UpdateEnvironment();
        InvokeRepeating(nameof(AtivarPosProcessamento), 0f, 2f);
    }

    void AtivarPosProcessamento()
    {
        foreach (var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            var dados = camera.GetUniversalAdditionalCameraData();
            if (dados != null && !dados.renderPostProcessing) dados.renderPostProcessing = true;
        }
    }
}
