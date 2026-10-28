using UnityEngine;

// Luzes da viatura piscando alternadamente
public class Giroflex : MonoBehaviour
{
    public Renderer vermelho;
    public Renderer azul;
    public float frequencia = 3f;

    void Update()
    {
        bool fase = Mathf.Repeat(Time.time * frequencia, 1f) < 0.5f;
        vermelho.enabled = fase;
        azul.enabled = !fase;
    }
}
