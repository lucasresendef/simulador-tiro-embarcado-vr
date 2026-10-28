using UnityEditor;
using UnityEngine;

// Configurações de importação dos recursos gratuitos em Assets/TCC/Externos:
// personagens Rocketbox (MIT) como Humanoid e texturas/céu do Poly Haven (CC0)
public class ImportacaoExternos : AssetPostprocessor
{
    const string Pasta = "Assets/TCC/Externos/";

    void OnPreprocessModel()
    {
        if (!assetPath.StartsWith(Pasta + "Rocketbox/")) return;
        var importador = (ModelImporter)assetImporter;
        importador.animationType = ModelImporterAnimationType.Human;
        importador.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importador.importAnimation = false;
        importador.importCameras = false;
        importador.importLights = false;
        importador.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
    }

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Pasta)) return;
        var importador = (TextureImporter)assetImporter;
        if (assetPath.Contains("_normal") || assetPath.Contains("_nor_gl")) importador.textureType = TextureImporterType.NormalMap;
        if (assetPath.Contains("opacity")) importador.alphaIsTransparency = true;
        if (assetPath.EndsWith(".hdr"))
        {
            importador.maxTextureSize = 4096;
            importador.mipmapEnabled = false;
            importador.wrapModeU = TextureWrapMode.Repeat;
            importador.wrapModeV = TextureWrapMode.Clamp;
            importador.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
