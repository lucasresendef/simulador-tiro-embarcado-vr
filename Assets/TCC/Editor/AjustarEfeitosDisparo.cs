using UnityEditor;
using UnityEngine;

// Conserta os materiais dos efeitos de disparo já existentes (sem refazer as cenas):
// a poeira estava sem a palavra-chave de transparência do URP e não aparecia, e o
// traçante tinha uma cor HDR forte demais (risco laranja-avermelhado com o bloom).
// Pela linha de comando: Unity -batchmode -quit -projectPath <projeto> -executeMethod AjustarEfeitosDisparo.Run
public static class AjustarEfeitosDisparo
{
    // Laranja-amarelado moderado; o brilho final ainda é multiplicado no EfeitosDisparo
    public static readonly Color CorTracante = new Color(3.5f, 2.3f, 1f);

    [MenuItem("TCC/Ajustar efeitos de disparo")]
    public static void Run()
    {
        RecursosTreinamento.MaterialParticula("Efeito Clarão", new Color(1f, 0.75f, 0.4f), true);
        RecursosTreinamento.MaterialParticula("Efeito Traçante", CorTracante, true);
        RecursosTreinamento.MaterialParticula("Efeito Poeira", Color.white, false);
        AssetDatabase.SaveAssets();
        Debug.Log("[TCC] Materiais dos efeitos de disparo ajustados.");
    }
}
