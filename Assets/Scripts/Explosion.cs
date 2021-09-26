using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Explosion : MonoBehaviour
{
    [SerializeField] GameObject fragment;
    [SerializeField] int fragmentCount = 10;
    [SerializeField] float force = 100.0f;
    [SerializeField] float repeatTime = 0.0f;

    List<GameObject> fragments = new List<GameObject>();

    public void Explode()
    {
        for (int i = 0; i < fragments.Count; ++i)
        {
            fragments[i].transform.localPosition = Vector3.zero;
            Rigidbody rigidbody = fragments[i].GetComponent<Rigidbody>();
            rigidbody.velocity = Vector3.zero;
            rigidbody.AddForce((Vector3.up * 2.0f + Random.onUnitSphere) * force);
            rigidbody.AddTorque(Random.onUnitSphere * force);
        }
    }

    private void OnEnable()
    {
        for (int i = 0; i < fragmentCount; ++i)
        {
            fragments.Add(Instantiate(fragment, transform));
        }

        Explode();
        if (repeatTime > 0.0f)
        {
            InvokeRepeating("Explode", repeatTime, repeatTime);
        }
    }

    private void OnDisable()
    {
        CancelInvoke("Explode");
    }
}
