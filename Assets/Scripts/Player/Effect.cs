using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Effect : MonoBehaviour
{
    public int effectsState;
    public Animator effectsAnimator;

    public void DestroyEffect()
    {
        Destroy(gameObject);
    }
}
