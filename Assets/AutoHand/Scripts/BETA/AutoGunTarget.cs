using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Autohand
{
    [RequireComponent(typeof(Rigidbody))]
    public class AutoGunTarget : MonoBehaviour
    {
        public GameObject hitDecal;
        public ParticleSystem hitParticle;
        public float hitDecalLifetime;
        public UnityGunHitEvent OnShotEvent;

        // Contador de acertos
        public Text hitCounterText;
        private int hitCounter = 0;
        public int maxHits = 20;  // Define o limite de disparos (20 neste caso)

        // Cronômetro
        public Text timerText; // Referência ao texto do cronômetro no Canvas
        private float timer = 0f;
        private bool isTimerRunning = true; // O cronômetro começa rodando

        Dictionary<GameObject, float> decalLifetimeTracker = new Dictionary<GameObject, float>();
        List<GameObject> decalPool = new List<GameObject>();
        List<ParticleSystem> inactiveParticlePool = new List<ParticleSystem>();
        List<ParticleSystem> activeParticlePool = new List<ParticleSystem>();

        private void Start()
        {
            // Inicia o cronômetro ao começar a cena
            isTimerRunning = true;
            timer = 0f;
        }

        private void Update()
        {
            // Se o cronômetro estiver rodando, incrementa o tempo
            if (isTimerRunning)
            {
                timer += Time.deltaTime;
                UpdateTimerUI(); // Atualiza o texto do cronômetro na UI
            }
        }

        public virtual void OnShot(AutoGun gun, RaycastHit hit)
        {
            OnShotEvent?.Invoke(gun, hit);
            CreateHitParticle(hit);
            CreateHitDecal(hit);

            // Incrementa o contador e verifica se atingiu o limite
            IncrementHitCounter();
        }

        void CreateHitParticle(RaycastHit hit)
        {
            if (hitParticle != null)
            {
                var newHitParticle = GameObject.Instantiate(hitParticle);
                newHitParticle.transform.position = hit.point;
                newHitParticle.transform.forward = hit.normal;
                activeParticlePool.Add(newHitParticle);
            }
        }

        void CreateHitDecal(RaycastHit hit)
        {
            if (hitDecal != null)
            {
                var newHitDecal = GameObject.Instantiate(hitDecal);
                newHitDecal.transform.position = hit.point;
                newHitDecal.transform.forward = hit.normal;
                decalLifetimeTracker.Add(newHitDecal, hitDecalLifetime);
            }
        }

        void CheckDecalLifetime()
        {
            if (decalLifetimeTracker.Count > 0)
            {
                var decalKeys = new GameObject[decalLifetimeTracker.Count];
                decalLifetimeTracker.Keys.CopyTo(decalKeys, 0);
                foreach (var decal in decalKeys)
                {
                    decalLifetimeTracker[decal] -= Time.deltaTime;
                    if (decalLifetimeTracker[decal] <= 0)
                    {
                        decal.SetActive(false);
                        decalPool.Add(decal);
                        decalLifetimeTracker.Remove(decal);
                    }
                }
            }
        }

        void CheckParticlPlaying()
        {
            if (inactiveParticlePool.Count > 0)
            {
                var playingKeys = new ParticleSystem[activeParticlePool.Count];
                activeParticlePool.CopyTo(playingKeys, 0);
                foreach (var particle in playingKeys)
                {
                    if (!particle.isPlaying)
                        particle.gameObject.SetActive(false);
                    inactiveParticlePool.Add(particle);
                    activeParticlePool.Remove(particle);
                }
            }
        }

        // Função para incrementar o contador de acertos e verificar o limite
        void IncrementHitCounter()
        {
            hitCounter++; // Incrementa o contador
            if (hitCounterText != null)
            {
                hitCounterText.text = "Hits: " + hitCounter; // Atualiza o texto na tela
            }

            // Verifica se atingiu o limite de 20 acertos
            if (hitCounter >= maxHits)
            {
                StopTimer(); // Para o cronômetro
            }
        }

        // Função para parar o cronômetro
        void StopTimer()
        {
            isTimerRunning = false;
            Debug.Log("Cronômetro Parado! Tempo final: " + timer);
        }

        // Função para atualizar o texto do cronômetro na UI
        void UpdateTimerUI()
        {
            if (timerText != null)
            {
                // Formata o tempo como minutos:segundos
                timerText.text = "Time: " + Mathf.Floor(timer / 60).ToString("00") + ":" + Mathf.Floor(timer % 60).ToString("00");
            }
        }
    }
}
