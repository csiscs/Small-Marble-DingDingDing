using UnityEngine;

public class StaticAutoMove : MonoBehaviour
{
    [SerializeField] private float speed = 4f;
    private const float Angle = 45f;
    
    private void Update()
    {
        float z = Mathf.Sin(Time.time * speed) * Angle;
        transform.rotation = Quaternion.Euler(0f, 0f, z);
    }
}
