using UnityEngine;

public class StartWallMover : MonoBehaviour
{
    private Animation anim;

    private void Awake()
    {
        anim = GetComponent<Animation>();
    }

    public void Play()
    {
        if (anim == null || anim.isPlaying)
            return;

        anim.Play();
    }
}
