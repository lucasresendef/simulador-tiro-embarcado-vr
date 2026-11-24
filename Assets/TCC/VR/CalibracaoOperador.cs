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

    AutoHandPlayer jogador;
    float calibrarEm = -1f;
    bool segurandoAnalogicos;

    void Start()
    {
        jogador = FindFirstObjectByType<AutoHandPlayer>();
        if (jogador == null || ModoDesktop.Ativo || !XRSettings.isDeviceActive) { enabled = false; return; }
        calibrarEm = Time.time + esperaInicial;
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
        jogador.heightOffset += alturaDesejada - cabeca.position.y;

        Debug.Log($"[VR] Operador recentralizado (ajuste de altura {jogador.heightOffset:+0.00;-0.00} m).");
        Calibrado?.Invoke();
    }
}
