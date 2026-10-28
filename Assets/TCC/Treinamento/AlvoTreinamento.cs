using System;
using UnityEngine;

public enum TipoAlvo { Veiculo, Pessoa }

// Alvo móvel do treinamento. Recebe os acertos do AutoGun (via GerenciadorTreinamento)
// e estima a própria velocidade, usada no cálculo da antecipação do disparo.
// Precisa de um Rigidbody (cinemático), pois o AutoGun só notifica acertos em corpos rígidos.
[RequireComponent(typeof(Rigidbody))]
public class AlvoTreinamento : MonoBehaviour
{
    public TipoAlvo tipo;
    [Tooltip("Alvo que não deve ser atingido (viaturas, civis): conta como erro de identificação")]
    public bool proibido;

    public int Acertos { get; private set; }
    // Enquanto neutralizado (parado ou abatido), novos acertos não contam
    public bool Neutralizado { get; set; }
    public Vector3 Velocidade { get; private set; }

    public event Action<AlvoTreinamento, RaycastHit> AoSerAtingido;

    Vector3 ultimaPosicao;
    bool velocidadeInformada;

    // Quem move o alvo informa a velocidade exata; sem isso, ela é estimada pelo deslocamento
    public void InformarVelocidade(Vector3 velocidade)
    {
        Velocidade = velocidade;
        velocidadeInformada = true;
    }

    void Awake()
    {
        GetComponent<Rigidbody>().isKinematic = true;
        ultimaPosicao = transform.position;
    }

    void Update()
    {
        if (!velocidadeInformada && Time.deltaTime > 0f)
            Velocidade = Vector3.Lerp(Velocidade, (transform.position - ultimaPosicao) / Time.deltaTime, 0.3f);
        ultimaPosicao = transform.position;
    }

    public bool RegistrarAcerto(RaycastHit hit)
    {
        if (Neutralizado) return false;
        Acertos++;
        AoSerAtingido?.Invoke(this, hit);
        return true;
    }
}
