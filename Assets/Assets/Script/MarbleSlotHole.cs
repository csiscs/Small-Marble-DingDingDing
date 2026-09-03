using UnityEngine;

public class MarbleSlotHole : MonoBehaviour
{
    [Tooltip("洞口编号，Slot_0 为 0，对应 Light1")]
    [SerializeField] private int slotIndex;

    private MarbleGameController controller;

    public void Bind(MarbleGameController gameController, int index)
    {
        controller = gameController;
        slotIndex = index;
    }

    private void Awake()
    {
        if (controller == null)
            controller = FindObjectOfType<MarbleGameController>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (controller == null || !other.CompareTag("Player"))
            return;

        controller.OnBallEnterSlot(slotIndex);
    }
}
