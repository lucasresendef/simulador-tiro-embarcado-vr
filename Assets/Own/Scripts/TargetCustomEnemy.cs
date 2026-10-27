using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TargetCustomEnemy : Enemy
{
    public int hits = 1;
    private float amount;

    public int hitCount = 0;
    public Text hitDisplay;

    private void Awake()
    {
        amount = 100 / hits;
    }

    public void Update()
    {
        this.hitDisplay.text = $"hits: {this.hitCount} / {hits}";
        if (hitCount >= hits)
            Object.Destroy(this.gameObject);
    }

    public override void DamageMe(float pAmount)
    {
        hitCount++;
    }
}
