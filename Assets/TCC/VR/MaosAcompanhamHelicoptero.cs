using System.Collections.Generic;
using Autohand;
using UnityEngine;
using UnityEngine.SceneManagement;

// Faz as mãos do AutoHand (e o que elas seguram) andarem junto com o helicóptero.
// O AutoHand move cada mão até o controle por velocidade, limitada a maxHeldVelocity (10 m/s quando
// segura algo). Com o helicóptero a 12–25 m/s (piloto automático, perseguição), a mão não alcançava o
// controle, ficava metros para trás e soltava o fuzil. Aqui, a cada passo de física, as mãos são
// transportadas pelo mesmo deslocamento e rotação do espaço de rastreamento (que segue o helicóptero),
// como numa plataforma móvel; a física da mão só cuida do movimento do braço do operador.
// Criado sozinho nas cenas com AutoHandPlayer; não faz nada no modo desktop.
[DefaultExecutionOrder(-200)]
public class MaosAcompanhamHelicoptero : MonoBehaviour
{
    const float IntervaloBuscaMaos = 1f;

    Transform referencia;
    Vector3 ultimaPosicao;
    Quaternion ultimaRotacao;
    bool temReferencia;
    float proximaBusca;
    Hand[] maos = new Hand[0];
    readonly List<Rigidbody> corpos = new List<Rigidbody>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= AoCarregarCena;
        SceneManager.sceneLoaded += AoCarregarCena;
        Criar();
    }

    static void AoCarregarCena(Scene cena, LoadSceneMode modo) => Criar();

    static void Criar()
    {
        if (FindFirstObjectByType<MaosAcompanhamHelicoptero>() != null) return;
        if (FindFirstObjectByType<AutoHandPlayer>() == null) return;
        new GameObject("[TCC] Mãos acompanham o helicóptero").AddComponent<MaosAcompanhamHelicoptero>();
    }

    void Start()
    {
        var jogador = FindFirstObjectByType<AutoHandPlayer>();
        if (ModoDesktop.Ativo || jogador == null || jogador.trackingContainer == null)
        {
            Destroy(gameObject);
            return;
        }
        referencia = jogador.trackingContainer;
    }

    void FixedUpdate()
    {
        if (referencia == null) return;

        var posicao = referencia.position;
        var rotacao = referencia.rotation;
        if (!temReferencia)
        {
            Guardar(posicao, rotacao);
            return;
        }

        var deltaRotacao = rotacao * Quaternion.Inverse(ultimaRotacao);
        bool moveu = (posicao - ultimaPosicao).sqrMagnitude > 1e-8f || Quaternion.Angle(rotacao, ultimaRotacao) > 0.001f;
        if (moveu) Transportar(posicao, deltaRotacao);
        Guardar(posicao, rotacao);
    }

    void Guardar(Vector3 posicao, Quaternion rotacao)
    {
        ultimaPosicao = posicao;
        ultimaRotacao = rotacao;
        temReferencia = true;
    }

    void Transportar(Vector3 posicao, Quaternion deltaRotacao)
    {
        if (Time.time >= proximaBusca)
        {
            maos = FindObjectsByType<Hand>(FindObjectsInactive.Exclude);
            proximaBusca = Time.time + IntervaloBuscaMaos;
        }

        corpos.Clear();
        foreach (var mao in maos)
        {
            if (mao == null || !mao.isActiveAndEnabled) continue;
            Adicionar(mao.body);
            if (mao.holdingObj != null) Adicionar(mao.holdingObj.body);
        }

        // Calcula todos os destinos antes de mover, porque a arma pode ser filha da mão
        var destinos = new (Rigidbody corpo, Vector3 pos, Quaternion rot)[corpos.Count];
        for (int i = 0; i < corpos.Count; i++)
        {
            var corpo = corpos[i];
            destinos[i] = (corpo, posicao + deltaRotacao * (corpo.position - ultimaPosicao), deltaRotacao * corpo.rotation);
        }
        foreach (var (corpo, pos, rot) in destinos)
        {
            corpo.position = pos;
            corpo.rotation = rot;
            corpo.linearVelocity = deltaRotacao * corpo.linearVelocity;
        }
    }

    void Adicionar(Rigidbody corpo)
    {
        if (corpo != null && !corpo.isKinematic && !corpos.Contains(corpo)) corpos.Add(corpo);
    }
}
