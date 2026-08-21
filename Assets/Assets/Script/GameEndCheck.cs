using UnityEngine;

public class GameEndCheck : MonoBehaviour
{
    [SerializeField] private SmallBallSystem system;
    
    private void Start()
    {
        system ??= GameObject.FindGameObjectWithTag("System").GetComponent<SmallBallSystem>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.CompareTag("Player") && system != null) 
        {
            system.GameEnd();
        }
    }
}
