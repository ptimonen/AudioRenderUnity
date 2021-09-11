using UnityEngine;

public class Rotate : MonoBehaviour
{
    public Vector3 axis;
    public float degreesPerSecond;
    void Update()
    {
        transform.Rotate(axis, degreesPerSecond * Time.deltaTime);
    }
}
