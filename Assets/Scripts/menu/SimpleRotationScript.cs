using UnityEngine;

public class SimpleRotationScript : MonoBehaviour
{
    [SerializeField] private float m_Speed;

    void Update()
    {
        transform.Rotate(Vector3.forward, Time.deltaTime * m_Speed);
    }
}
