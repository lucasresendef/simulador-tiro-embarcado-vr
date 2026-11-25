// DIAGNÓSTICO TEMPORÁRIO (Claude): registra no Console, com óculos, o desempenho do VR e o estado
// dos controles e do fuzil, para investigar imagem "presa à frente" e gatilho sem disparo. Pode apagar.
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Autohand;
using Autohand.Demo;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

public class DiagnosticoVR : MonoBehaviour
{
    const float Intervalo = 3f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Criar()
    {
        var go = new GameObject("[Diagnóstico VR]");
        DontDestroyOnLoad(go);
        go.AddComponent<DiagnosticoVR>();
        SceneManager.sceneLoaded += (s, m) => { Debug.Log($"[DiagVR] Cena carregada: {s.name}"); OuvirMaos(); };
        OuvirMaos();
    }

    // Registra cada vez que uma mão pega ou solta algo, e por quê
    static void OuvirMaos()
    {
        foreach (var mao in FindObjectsByType<Autohand.Hand>(FindObjectsInactive.Include))
        {
            var m = mao;
            m.OnGrabbed += (h, g) => Debug.Log($"[DiagVR] {h.name} PEGOU '{g.name}'");
            m.OnReleased += (h, g) => Debug.Log($"[DiagVR] {h.name} SOLTOU '{g.name}' (soltura normal: grip solto ou distância) " +
                $"mão→controle {(h.follow != null ? Vector3.Distance(h.transform.position, h.follow.position) : -1f):0.00} m, " +
                $"mão→arma {Vector3.Distance(h.transform.position, g.transform.position):0.00} m | {Posicoes()}");
            m.OnForcedRelease += (h, g) => Debug.Log($"[DiagVR] {h.name} SOLTOU '{g.name}' (forçada)");
            m.OnGrabJointBreak += (h, g) => Debug.Log($"[DiagVR] {h.name} SOLTOU '{g.name}' (junta quebrou por força)");
        }
    }

    float inicio, pior;
    int quadros;
    readonly HashSet<string> gatilhos = new HashSet<string>();

    void Start()
    {
        inicio = Time.realtimeSinceStartup;
        // Mostra os dois olhos na janela Game, para ver o que aparece só num deles
        XRSettings.gameViewRenderMode = GameViewRenderMode.BothEyes;
        var dispositivos = InputSystem.devices.Select(d => $"{d.displayName ?? d.name} [{d.layout}]");
        Debug.Log("[DiagVR] Dispositivos do Input System: " + string.Join(", ", dispositivos));
    }

    void Update()
    {
        quadros++;
        pior = Mathf.Max(pior, Time.unscaledDeltaTime);

        // Registra cada aperto de gatilho/grip visto pelos links de controle do AutoHand
        foreach (var link in FindObjectsByType<OpenXRHandControllerLink>(FindObjectsInactive.Exclude))
        {
            float grip = Ler(link.grabAxis), gatilho = Ler(link.squeezeAxis);
            string chave = link.name + (gatilho > 0.5f ? ":gatilho" : "") + (grip > 0.5f ? ":grip" : "");
            if ((gatilho > 0.5f || grip > 0.5f) && gatilhos.Add(chave))
                Debug.Log($"[DiagVR] {link.name}: grip={grip:0.00} gatilho={gatilho:0.00} segurando='{(link.hand != null && link.hand.holdingObj != null ? link.hand.holdingObj.name : "nada")}'");
        }

        float decorrido = Time.realtimeSinceStartup - inicio;
        if (decorrido < Intervalo) return;

        var sb = new StringBuilder();
        sb.Append($"[DiagVR] FPS médio {quadros / decorrido:0.0}, pior quadro {pior * 1000f:0} ms");
        sb.Append($" | XR ativo={XRSettings.isDeviceActive} disp='{XRSettings.loadedDeviceName}'");
        sb.Append($" olho {XRSettings.eyeTextureWidth}x{XRSettings.eyeTextureHeight} escala {XRSettings.eyeTextureResolutionScale:0.00}");
        var telas = new List<XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(telas);
        foreach (var t in telas)
            if (t.TryGetDisplayRefreshRate(out float hz)) sb.Append($" {hz:0}Hz rodando={t.running}");

        var cam = Camera.main;
        if (cam != null)
        {
            var tpd = cam.GetComponents<Behaviour>().FirstOrDefault(c => c != null && c.GetType().Name == "TrackedPoseDriver");
            sb.Append($" | Camera.main '{cam.name}' olhos={cam.stereoTargetEye} TPD={(tpd == null ? "não tem" : tpd.enabled ? "ligado" : "DESLIGADO")} rot={cam.transform.localEulerAngles}");
        }
        var cams = FindObjectsByType<Camera>(FindObjectsInactive.Exclude).Where(c => c.enabled).Select(c => $"{c.name}(prof {c.depth}, olhos {c.stereoTargetEye}, alvo {(c.targetTexture != null ? "textura" : "tela")})");
        sb.Append(" | câmeras ligadas: " + string.Join(", ", cams));

        foreach (var mao in FindObjectsByType<Autohand.Hand>(FindObjectsInactive.Include))
            sb.Append($" | mão {mao.name} ativa={mao.isActiveAndEnabled} segurando='{(mao.holdingObj != null ? mao.holdingObj.name : "nada")}'");
        var fuzil = FindFirstObjectByType<AutoGun>();
        if (fuzil != null) sb.Append($" | fuzil munição={fuzil.GetAmmo()} ferrolho={fuzil.IsSlideLoaded()}");

        Debug.Log(sb.ToString());
        Debug.Log("[DiagVR] Posições: " + Posicoes());
        inicio = Time.realtimeSinceStartup;
        quadros = 0;
        pior = 0f;
    }

    // Posições (m) e velocidades (m/s) do helicóptero, do jogador, da cabeça, da mão direita, do controle e do fuzil
    static string Posicoes()
    {
        var sb = new StringBuilder();
        string V(Vector3 v) => $"({v.x:0.0},{v.y:0.0},{v.z:0.0})";
        var heli = FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude).Where(r => r.name.Contains("Eurocopter")).FirstOrDefault();
        if (heli != null) sb.Append($"heli {V(heli.position)} vel {heli.linearVelocity.magnitude:0.0} kin={heli.isKinematic} interp={heli.interpolation}");
        var jogador = FindFirstObjectByType<AutoHandPlayer>();
        if (jogador != null)
        {
            sb.Append($" | jogador base {V(jogador.transform.position)} pai '{(jogador.transform.parent != null ? jogador.transform.parent.name : "nenhum")}'");
            if (jogador.body != null) sb.Append($" vel {jogador.body.linearVelocity.magnitude:0.0} kin={jogador.body.isKinematic}");
            if (jogador.trackingContainer != null) sb.Append($" | container {V(jogador.trackingContainer.position)} pai '{(jogador.trackingContainer.parent != null ? jogador.trackingContainer.parent.name : "nenhum")}'");
            sb.Append($" heightOffset {jogador.heightOffset:0.00}");
            if (jogador.headCamera != null) sb.Append($" | cabeça {V(jogador.headCamera.transform.position)}");
        }
        var mao = FindObjectsByType<Autohand.Hand>(FindObjectsInactive.Exclude).FirstOrDefault(h => h.name == "RobotHand (R)");
        if (mao != null)
        {
            sb.Append($" | mão R {V(mao.transform.position)} pai '{(mao.transform.parent != null ? mao.transform.parent.name : "nenhum")}'");
            if (mao.follow != null) sb.Append($" controle {V(mao.follow.position)} pai '{(mao.follow.parent != null ? mao.follow.parent.name : "nenhum")}'");
            if (mao.body != null) sb.Append($" vel mão {mao.body.linearVelocity.magnitude:0.0}");
        }
        var fuzil = FindFirstObjectByType<AutoGun>();
        if (fuzil != null)
        {
            var rb = fuzil.GetComponentInParent<Rigidbody>();
            sb.Append($" | fuzil {V(fuzil.transform.position)}");
            if (rb != null) sb.Append($" vel {rb.linearVelocity.magnitude:0.0}");
        }
        return sb.ToString();
    }

    static float Ler(InputActionProperty p)
    {
        try { return p.action != null ? p.action.ReadValue<float>() : -1f; }
        catch { return -1f; }
    }
}
