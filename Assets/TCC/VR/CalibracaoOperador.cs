using System;
using Autohand;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

// Calibração do operador no óculos: põe os olhos na altura do assento (a mesma do modo desktop)
// e o olhar voltado para a frente do rig, isto é, para a porta. Roda sozinha logo que o óculos
// começa a rastrear e de novo com a tecla G (instrutor) ou com os dois analógicos pressionados.
// Usa as funções do próprio AutoHandPlayer, que é quem move o espaço de rastreamento.
public class CalibracaoOperador : MonoBehaviour
{
    [Tooltip("Altura dos olhos sobre a base do jogador, em metros (operador sentado)")]
    public float alturaOlhos = 1.2f;
    [Tooltip("Espera depois do início da cena, para o rastreamento estabilizar")]
    public float esperaInicial = 1.5f;

    public static event Action Calibrado;

    const float AjusteMaximo = 1.5f;

    AutoHandPlayer jogador;
    float calibrarEm = -1f;
    bool segurandoAnalogicos;

    void Start()
    {
        jogador = FindFirstObjectByType<AutoHandPlayer>();
        if (jogador == null || ModoDesktop.Ativo || !XRSettings.isDeviceActive) { enabled = false; return; }
        FixarCorpo();
        calibrarEm = Time.time + esperaInicial;
    }

    // Locomoção do AutoHandPlayer ativa (no helicóptero fica desligada: useMovement = false)
    bool LocomocaoAtiva => jogador.enabled && jogador.useMovement;

    // Nas cenas do helicóptero a locomoção do AutoHandPlayer fica desligada: o operador está sentado.
    // O corpo físico dele ficava solto e caía por gravidade sem parar (milhares de metros), e a
    // calibração media a altura a partir desse corpo. Aqui o corpo vira cinemático e acompanha o rig.
    void FixarCorpo()
    {
        if (LocomocaoAtiva) return;
        var corpo = jogador.GetComponent<Rigidbody>();
        if (corpo == null) return;
        corpo.useGravity = false;
        corpo.isKinematic = true;
    }

    void Update()
    {
        if (calibrarEm > 0f && Time.time >= calibrarEm)
        {
            calibrarEm = -1f;
            Calibrar();
        }

        var teclado = Keyboard.current;
        if (teclado != null && teclado.gKey.wasPressedThisFrame) Calibrar();

        // Os dois analógicos pressionados juntos (difícil de acontecer sem querer)
        bool ambos = Pressionado(XRNode.LeftHand) && Pressionado(XRNode.RightHand);
        if (ambos && !segurandoAnalogicos) Calibrar();
        segurandoAnalogicos = ambos;
    }

    static bool Pressionado(XRNode mao)
    {
        var dispositivo = InputDevices.GetDeviceAtXRNode(mao);
        return dispositivo.isValid && dispositivo.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxisClick, out bool apertado) && apertado;
    }

    public void Calibrar()
    {
        if (jogador == null || jogador.headCamera == null) return;
        var cabeca = jogador.headCamera.transform;
        var baseJogador = jogador.transform;

        // Olhar para a frente do rig, no plano horizontal
        var frente = Vector3.ProjectOnPlane(baseJogador.forward, Vector3.up);
        if (frente.sqrMagnitude > 0.001f) jogador.SetRotation(Quaternion.LookRotation(frente, Vector3.up));

        // Cabeça sobre o corpo e olhos na altura do assento
        jogador.Recenter();
        float alturaDesejada = baseJogador.position.y + alturaOlhos;
        float ajuste = alturaDesejada - cabeca.position.y;
        // Um ajuste grande indica referência errada (ex.: corpo fora do lugar): não mexe na altura
        if (Mathf.Abs(ajuste) > AjusteMaximo)
        {
            Debug.LogWarning($"[VR] Calibração: ajuste de altura de {ajuste:0.00} m ignorado (acima de {AjusteMaximo} m).");
            ajuste = 0f;
        }
        if (LocomocaoAtiva)
            jogador.heightOffset += ajuste;
        else if (jogador.trackingContainer != null)
            // Sem a locomoção do AutoHandPlayer o heightOffset não tem efeito: move o espaço de rastreamento
            jogador.trackingContainer.position += Vector3.up * ajuste;

        Debug.Log($"[VR] Operador recentralizado (ajuste de altura {ajuste:+0.00;-0.00} m).");
        Calibrado?.Invoke();
    }
}
