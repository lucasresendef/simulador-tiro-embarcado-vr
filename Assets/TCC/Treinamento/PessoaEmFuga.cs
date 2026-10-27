using Oyedoyin.Rotary.LowFidelity;
using UnityEngine;
using UnityEngine.AI;

// Pessoa que foge do helicóptero. Máquina de estados (padrão State):
// Parada -> Alerta -> Fugindo -> Escondida -> Retornando -> Parada, e Abatida ao ser atingida.
// A fuga segue o comportamento de evasão de Reynolds: foge da posição prevista da aeronave,
// e não da atual, escolhendo destinos na NavMesh dentro da área de treinamento.
[RequireComponent(typeof(NavMeshAgent), typeof(AlvoTreinamento))]
public class PessoaEmFuga : MonoBehaviour
{
    [Tooltip("Distância horizontal em que a pessoa percebe o helicóptero, em metros")]
    public float raioPercepcao = 120f;
    [Tooltip("Raio da área, em torno do ponto inicial, em que a pessoa pode se deslocar")]
    public float raioArea = 45f;
    public float velocidadeCorrida = 5f;
    public float velocidadeRetorno = 2f;
    public float tempoAbatida = 8f;

    // Valores do parâmetro "currentState" do LowPolyHumanAnimator
    const int AnimParado = 1, AnimCorrendo = 2, AnimMorte = 4;

    interface IEstado
    {
        void Entrar(PessoaEmFuga p);
        void Atualizar(PessoaEmFuga p);
    }

    NavMeshAgent agente;
    Animator animator;
    AlvoTreinamento alvo;
    Collider colisor;
    Transform helicoptero;
    Vector3 posicaoInicial, velocidadeHelicoptero, ultimaPosicaoHelicoptero;
    IEstado estado;
    float tempoNoEstado;
    bool atingida;

    public string NomeEstado => estado?.GetType().Name;

    void Start()
    {
        agente = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
        alvo = GetComponent<AlvoTreinamento>();
        colisor = GetComponent<Collider>();
        var controle = FindFirstObjectByType<Controller>();
        if (controle != null)
        {
            helicoptero = controle.transform;
            ultimaPosicaoHelicoptero = helicoptero.position;
        }

        posicaoInicial = transform.position;
        // A reação fica para o próximo quadro: o AutoGun ainda usa o colisor depois de notificar o acerto
        alvo.AoSerAtingido += (_, _) => atingida = true;
        MudarPara(new Parada());
    }

    void Update()
    {
        if (helicoptero != null && Time.deltaTime > 0f)
        {
            var velocidade = (helicoptero.position - ultimaPosicaoHelicoptero) / Time.deltaTime;
            velocidadeHelicoptero = Vector3.Lerp(velocidadeHelicoptero, velocidade, 0.1f);
            ultimaPosicaoHelicoptero = helicoptero.position;
        }

        if (atingida)
        {
            atingida = false;
            MudarPara(new Abatida());
        }

        tempoNoEstado += Time.deltaTime;
        estado.Atualizar(this);

        if (agente.enabled && animator != null && agente.speed > 0f)
            animator.speed = estado is Fugindo || estado is Retornando
                ? Mathf.Clamp(agente.velocity.magnitude / velocidadeCorrida, 0.5f, 1.2f)
                : 1f;
    }

    void MudarPara(IEstado novo)
    {
        estado = novo;
        tempoNoEstado = 0f;
        novo.Entrar(this);
    }

    void Animar(int estadoAnimacao)
    {
        if (animator != null) animator.SetInteger("currentState", estadoAnimacao);
    }

    bool HelicopteroPerto()
    {
        if (helicoptero == null) return false;
        var delta = helicoptero.position - transform.position;
        delta.y = 0f;
        return delta.magnitude < raioPercepcao;
    }

    // Evasão: foge da posição prevista do helicóptero, tentando direções próximas da ideal
    bool EscolherDestinoDeFuga(out Vector3 destino)
    {
        var previsto = helicoptero.position + velocidadeHelicoptero * 2f;
        var fuga = transform.position - previsto;
        fuga.y = 0f;
        fuga = fuga.sqrMagnitude > 0.01f ? fuga.normalized : transform.forward;

        float distancia = Random.Range(15f, 30f);
        foreach (float angulo in new[] { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 135f, -135f })
        {
            var candidato = transform.position + Quaternion.Euler(0f, angulo, 0f) * fuga * distancia;
            if (Vector3.Distance(candidato, posicaoInicial) > raioArea) continue;
            if (NavMesh.SamplePosition(candidato, out var hit, 4f, NavMesh.AllAreas))
            {
                destino = hit.position;
                return true;
            }
        }
        destino = posicaoInicial;
        return false;
    }

    Vector3 PontoAleatorioPertoDoInicio()
    {
        var candidato = posicaoInicial + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(0f, 8f);
        return NavMesh.SamplePosition(candidato, out var hit, 4f, NavMesh.AllAreas) ? hit.position : posicaoInicial;
    }

    class Parada : IEstado
    {
        public void Entrar(PessoaEmFuga p)
        {
            p.agente.ResetPath();
            p.Animar(AnimParado);
        }

        public void Atualizar(PessoaEmFuga p)
        {
            if (p.HelicopteroPerto()) p.MudarPara(new Alerta());
        }
    }

    // Tempo de reação: olha para a aeronave antes de correr
    class Alerta : IEstado
    {
        float reacao;

        public void Entrar(PessoaEmFuga p)
        {
            reacao = Random.Range(0.5f, 1.5f);
            p.agente.ResetPath();
            p.Animar(AnimParado);
        }

        public void Atualizar(PessoaEmFuga p)
        {
            var paraHelicoptero = p.helicoptero.position - p.transform.position;
            paraHelicoptero.y = 0f;
            if (paraHelicoptero.sqrMagnitude > 0.01f)
                p.transform.rotation = Quaternion.Slerp(p.transform.rotation, Quaternion.LookRotation(paraHelicoptero), 5f * Time.deltaTime);
            if (p.tempoNoEstado >= reacao) p.MudarPara(new Fugindo());
        }
    }

    class Fugindo : IEstado
    {
        float duracao, proximaDecisao;

        public void Entrar(PessoaEmFuga p)
        {
            duracao = Random.Range(6f, 10f);
            p.agente.speed = p.velocidadeCorrida;
            p.Animar(AnimCorrendo);
            Decidir(p);
        }

        public void Atualizar(PessoaEmFuga p)
        {
            if (p.tempoNoEstado >= proximaDecisao) Decidir(p);
            if (p.tempoNoEstado >= duracao) p.MudarPara(new Escondida());
        }

        void Decidir(PessoaEmFuga p)
        {
            p.EscolherDestinoDeFuga(out var destino);
            p.agente.SetDestination(destino);
            proximaDecisao = p.tempoNoEstado + 1.5f;
        }
    }

    class Escondida : IEstado
    {
        float duracao;

        public void Entrar(PessoaEmFuga p)
        {
            duracao = Random.Range(5f, 12f);
            p.agente.ResetPath();
            p.Animar(AnimParado);
        }

        public void Atualizar(PessoaEmFuga p)
        {
            if (p.tempoNoEstado >= duracao) p.MudarPara(new Retornando());
        }
    }

    class Retornando : IEstado
    {
        public void Entrar(PessoaEmFuga p)
        {
            p.agente.speed = p.velocidadeRetorno;
            p.agente.SetDestination(p.PontoAleatorioPertoDoInicio());
            p.Animar(AnimCorrendo);
        }

        public void Atualizar(PessoaEmFuga p)
        {
            bool chegou = !p.agente.pathPending && p.agente.remainingDistance <= p.agente.stoppingDistance + 0.3f;
            if (chegou) p.MudarPara(new Parada());
            else if (p.tempoNoEstado > 3f && p.HelicopteroPerto()) p.MudarPara(new Alerta());
        }
    }

    class Abatida : IEstado
    {
        public void Entrar(PessoaEmFuga p)
        {
            p.alvo.Neutralizado = true;
            p.agente.ResetPath();
            p.agente.isStopped = true;
            if (p.colisor != null) p.colisor.enabled = false;
            p.Animar(AnimMorte);
        }

        public void Atualizar(PessoaEmFuga p)
        {
            if (p.tempoNoEstado < p.tempoAbatida) return;
            p.agente.Warp(p.PontoAleatorioPertoDoInicio());
            p.agente.isStopped = false;
            if (p.colisor != null) p.colisor.enabled = true;
            p.alvo.Neutralizado = false;
            p.MudarPara(new Parada());
        }
    }
}
