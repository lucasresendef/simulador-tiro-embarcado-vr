// TESTE TEMPORÁRIO (Claude): simula os controles de VR para medir a mira com as duas mãos.
// Só roda no editor, quando existe Temp/teste_mira_vr.txt. Pode apagar junto com Editor/TesteMiraVRLauncher.cs.
// Move os dois controles como um operador segurando o fuzil (mão esquerda à frente da direita, na
// linha do cano), aponta para o suspeito, varre a mira (baixo, cima, lados) e atira, registrando:
//  - deslocamento fixo entre a direção do controle direito e a do cano (pose de pegada)
//  - erro entre a direção pedida e a do cano, e quanto tempo o cano leva para chegar
//  - acertos no suspeito
#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using Autohand;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-300)]
public class TesteMiraVR : MonoBehaviour
{
    public const string Gatilho = "Temp/teste_mira_vr.txt";
    public static bool Ativo => File.Exists(Gatilho);

    // Posição da mão direita em relação à cabeça (direita, baixo, frente) e distância entre as mãos
    static readonly Vector3 OffsetMaoDireita = new Vector3(0.18f, -0.28f, 0.30f);
    const float DistanciaEntreMaos = 0.38f;

    Camera cabeca;
    Transform controleDireito, controleEsquerdo;
    Hand maoDireita, maoEsquerda;
    AutoGun fuzil;
    PerseguicaoVeicular perseguicao;
    Quaternion pegada = Quaternion.identity;   // rotação do cano em relação ao controle direito
    Vector3 direcaoPedida;
    bool controlando;
    Quaternion desvioFixo = Quaternion.identity;

    // Direção em que o cano deve ficar quando os controles apontam para "direcao"
    Vector3 CanoEsperado(Vector3 direcao) => Quaternion.LookRotation(direcao, Vector3.up) * desvioFixo * Vector3.forward;
    // Direção para os controles apontarem para o cano ficar em "cano"
    Vector3 ControlesPara(Vector3 cano) => Quaternion.LookRotation(cano, Vector3.up) * Quaternion.Inverse(desvioFixo) * Vector3.forward;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded += (s, m) => Criar();
        Criar();
    }

    static void Criar()
    {
        if (!Ativo || FindFirstObjectByType<TesteMiraVR>() != null || FindFirstObjectByType<PerseguicaoVeicular>() == null) return;
        // "1mao" no arquivo de gatilho: testa com o fuzil só na mão direita (antes do Start da pegada)
        bool umaMao = File.ReadAllText(Gatilho).Contains("1mao");
        var pegadaInicial = FindFirstObjectByType<AdvancedInitialGrab>();
        if (pegadaInicial != null) pegadaInicial.somenteMaoPrincipal = umaMao;
        Log(umaMao ? "Modo: só mão direita." : "Modo: duas mãos.");
        new GameObject("[Teste mira VR]").AddComponent<TesteMiraVR>();
    }

    IEnumerator Start()
    {
        Log("Iniciando. Esperando a pegada do fuzil...");
        yield return new WaitForSeconds(2.5f);

        var jogador = FindFirstObjectByType<AutoHandPlayer>();
        fuzil = FindFirstObjectByType<AutoGun>();
        perseguicao = FindFirstObjectByType<PerseguicaoVeicular>();
        cabeca = jogador != null && jogador.headCamera != null ? jogador.headCamera : Camera.main;
        var maos = FindObjectsByType<Hand>(FindObjectsInactive.Exclude);
        maoDireita = maos.FirstOrDefault(h => h.name == "RobotHand (R)");
        maoEsquerda = maos.FirstOrDefault(h => h.name == "RobotHand (L)");
        if (fuzil == null || maoDireita == null || maoDireita.follow == null || cabeca == null)
        {
            Log($"ERRO: faltando peça (fuzil={fuzil != null}, mão R={maoDireita != null}, cabeça={cabeca != null}).");
            Encerrar();
            yield break;
        }
        controleDireito = maoDireita.follow;
        controleEsquerdo = maoEsquerda != null ? maoEsquerda.follow : null;
        Log($"controle direito '{Caminho(controleDireito)}', esquerdo '{(controleEsquerdo != null ? Caminho(controleEsquerdo) : "-")}', cabeça '{cabeca.name}'.");

        // Os controles e a cabeça passam a ser comandados por este script
        DesligarRastreamento(controleDireito);
        if (controleEsquerdo != null) DesligarRastreamento(controleEsquerdo);
        DesligarRastreamento(cabeca.transform);
        foreach (var t in new[] { controleEsquerdo, controleEsquerdo != null ? controleEsquerdo.parent : null, controleDireito, controleDireito.parent })
            if (t != null)
                Log($"componentes em '{t.name}': {string.Join(", ", t.GetComponents<Component>().Select(c => c.GetType().Name + (c is Behaviour b && !b.enabled ? "(off)" : "")))}");
        cabeca.transform.localPosition = new Vector3(0f, 1.2f, 0f);

        // Pose neutra: controle direito apontando para a frente da cabeça; mede o desvio do cano
        direcaoPedida = Vector3.ProjectOnPlane(cabeca.transform.parent != null ? cabeca.transform.parent.forward : Vector3.forward, Vector3.up).normalized;
        controlando = true;
        PosicionarControles(direcaoPedida, Quaternion.identity);
        yield return new WaitForSeconds(2f);
        var cano = fuzil.shootForward;
        float desvioNeutro = Vector3.Angle(controleDireito.forward, cano.forward);
        pegada = Quaternion.Inverse(controleDireito.rotation) * cano.rotation;
        // Botão A (realinhar): o cano deve passar a coincidir com a direção do controle direito
        var mira = FindFirstObjectByType<MiraEstabilizada>();
        if (mira != null)
        {
            pegada = Quaternion.identity;
            yield return new WaitForSeconds(0.5f);
            // Aperta A várias vezes seguidas: o cano não pode ir se deslocando (deriva)
            for (int i = 1; i <= 8; i++)
            {
                float antes = Vector3.Angle(controleDireito.forward, cano.forward);
                mira.Realinhar();
                yield return new WaitForSeconds(0.4f);
                yield return new WaitForEndOfFrame();
                Log($"BOTÃO A #{i}: cano vs controle direito {antes:0.0}° antes → {Vector3.Angle(controleDireito.forward, cano.forward):0.0}° depois; " +
                    $"inclinação do cano {Vector3.SignedAngle(Vector3.ProjectOnPlane(cano.forward, Vector3.up), cano.forward, Vector3.Cross(cano.forward, Vector3.up)):0.0}°.");
            }
        }
        // Com a pegada compensada, mede o desvio fixo que sobra entre a direção pedida e a do cano
        // (descontado nas medições, como o operador faz olhando o cano)
        yield return new WaitForSeconds(2f);
        desvioFixo = Quaternion.Inverse(Quaternion.LookRotation(direcaoPedida, Vector3.up)) * Quaternion.LookRotation(cano.forward, Vector3.up);
        Log($"Desvio fixo depois da compensação: {Vector3.Angle(direcaoPedida, cano.forward):0.0}°. Segurando: R='{Nome(maoDireita)}' L='{Nome(maoEsquerda)}'.");
        Log($"Mira estabilizada: {(FindFirstObjectByType<MiraEstabilizada>() != null ? "ligada" : "desligada")}.");
        Log($"POSE: com o controle direito apontado para a frente, o cano aponta {desvioNeutro:0.0}° fora da direção do controle " +
            $"(cano em relação ao controle: {pegada.eulerAngles}). Segurando: R='{Nome(maoDireita)}' L='{Nome(maoEsquerda)}'.");

        var frente = direcaoPedida;
        // Daqui em diante, o controle é girado para que o cano (com a pegada medida) aponte na direção pedida
        yield return Mirar("frente", frente, 2f);
        yield return Mirar("30° para baixo", Quaternion.AngleAxis(30f, Vector3.Cross(Vector3.up, frente)) * frente, 2.5f);
        yield return Mirar("50° para baixo", Quaternion.AngleAxis(50f, Vector3.Cross(Vector3.up, frente)) * frente, 2.5f);
        yield return Mirar("frente", frente, 2f);
        yield return Mirar("30° à esquerda", Quaternion.AngleAxis(-30f, Vector3.up) * frente, 2.5f);
        yield return Mirar("30° à direita", Quaternion.AngleAxis(30f, Vector3.up) * frente, 2.5f);

        // Acompanha o suspeito e atira
        if (perseguicao != null && perseguicao.suspeito != null)
        {
            Log("Acompanhando o suspeito por 12 s e atirando em rajadas...");
            float fim = Time.time + 12f;
            float proximoTiro = Time.time + 1f;
            float somaErro = 0f; int amostras = 0;
            while (Time.time < fim && perseguicao.suspeito != null)
            {
                var alvo = perseguicao.suspeito.transform.position + Vector3.up * 0.8f;
                direcaoPedida = ControlesPara((alvo - fuzil.shootForward.position).normalized);
                float erro = Vector3.Angle(fuzil.shootForward.forward, (alvo - fuzil.shootForward.position).normalized);
                somaErro += erro; amostras++;
                if (Time.time >= proximoTiro)
                {
                    maoDireita.Squeeze();
                    yield return new WaitForSeconds(0.15f);
                    maoDireita.Unsqueeze();
                    proximoTiro = Time.time + 0.8f;
                    Log($"tiro: erro do cano até o suspeito {erro:0.0}° (dist {Vector3.Distance(alvo, fuzil.shootForward.position):0} m) | {perseguicao.Status}");
                }
                yield return null;
            }
            Log($"SUSPEITO: erro médio do cano {somaErro / Mathf.Max(1, amostras):0.0}° | munição {fuzil.GetAmmo()} | {perseguicao.Resumo}");
        }

        Encerrar();
    }

    IEnumerator Mirar(string nome, Vector3 direcao, float duracao)
    {
        direcaoPedida = direcao.normalized;
        float inicio = Time.time;
        float chegouEm = -1f;
        float erro = 180f;
        while (Time.time - inicio < duracao)
        {
            erro = Vector3.Angle(fuzil.shootForward.forward, CanoEsperado(direcaoPedida));
            if (chegouEm < 0f && erro < 3f) chegouEm = Time.time - inicio;
            yield return null;
        }
        // Mede no fim do quadro, depois de tudo posicionado (o que o operador vê)
        yield return new WaitForEndOfFrame();
        erro = Vector3.Angle(fuzil.shootForward.forward, CanoEsperado(direcaoPedida));
        var sb = new StringBuilder($"MIRA {nome}: erro final {erro:0.0}°, ");
        sb.Append(chegouEm >= 0f ? $"chegou a menos de 3° em {chegouEm:0.00} s" : "NÃO chegou a menos de 3°");
        sb.Append($" | mão R→controle {Vector3.Distance(maoDireita.transform.position, controleDireito.position):0.00} m");
        if (maoEsquerda != null && controleEsquerdo != null)
            sb.Append($", mão L→controle {Vector3.Distance(maoEsquerda.transform.position, controleEsquerdo.position):0.00} m");
        var me = FindFirstObjectByType<MiraEstabilizada>();
        if (me != null) sb.Append($" | apoio: dist {me.DiagDistancia:0.00} m, ângulo {me.DiagAngulo:0.0}°, cano vs controle R {Vector3.Angle(controleDireito.forward, fuzil.shootForward.forward):0.0}°, mão L pos local {maoEsquerda.transform.InverseTransformPoint(controleEsquerdo.position)}, controle L no espaço do R {controleDireito.InverseTransformPoint(controleEsquerdo.position)}, pais R='{controleDireito.parent.name}' L='{controleEsquerdo.parent.name}', escala L {controleEsquerdo.lossyScale.x:0.000}");
        sb.Append($" | segurando R='{Nome(maoDireita)}' L='{Nome(maoEsquerda)}' | FPS {1f / Time.smoothDeltaTime:0}");
        Log(sb.ToString());
    }

    void Update() { if (controlando) PosicionarControles(direcaoPedida, pegada); }
    void FixedUpdate() { if (controlando) PosicionarControles(direcaoPedida, pegada); }

    // Controle direito perto do ombro direito; esquerdo à frente, na linha do cano. A rotação do
    // controle desconta a pegada, para o cano apontar na direção pedida
    void PosicionarControles(Vector3 direcao, Quaternion pegadaCano)
    {
        var frenteCabeca = Quaternion.LookRotation(Vector3.ProjectOnPlane(direcao, Vector3.up).sqrMagnitude > 0.001f ? Vector3.ProjectOnPlane(direcao, Vector3.up) : Vector3.forward, Vector3.up);
        cabeca.transform.rotation = Quaternion.LookRotation(direcao, Vector3.up);
        var posDireita = cabeca.transform.position + frenteCabeca * OffsetMaoDireita;
        var rotCano = Quaternion.LookRotation(direcao, Vector3.up);
        controleDireito.SetPositionAndRotation(posDireita, rotCano * Quaternion.Inverse(pegadaCano));
        if (controleEsquerdo != null)
            controleEsquerdo.SetPositionAndRotation(posDireita + rotCano * new Vector3(-0.02f, 0.03f, DistanciaEntreMaos), rotCano * Quaternion.Inverse(pegadaCano));
    }

    static void DesligarRastreamento(Transform t)
    {
        for (var atual = t; atual != null; atual = atual.parent)
        {
            bool achou = false;
            foreach (var c in atual.GetComponents<Behaviour>())
                if (c != null && c.GetType().Name == "TrackedPoseDriver") { c.enabled = false; achou = true; }
            if (achou) return;
        }
    }

    static string Nome(Hand h) => h == null ? "-" : h.holdingObj != null ? h.holdingObj.name : "nada";

    static string Caminho(Transform t) => t.parent != null ? t.parent.name + "/" + t.name : t.name;

    static void Log(string msg) => Debug.Log("[TesteMira] " + msg);

    void Encerrar()
    {
        controlando = false;
        Log("FIM do teste.");
        try { File.Delete(Gatilho); } catch { }
        UnityEditor.EditorApplication.ExitPlaymode();
    }
}
#endif
