using UnityEngine;

public class SmallBallTrigger : MonoBehaviour
{
    public SmallBallSystem BallSystem { private get; set; }

    private void OnCollisionExit2D(Collision2D collision)
    {
        string tag = collision.gameObject.tag;
        switch (tag)
        {
            case "Static":
                BallSystem.AddScore();
                break;
            case "Coin":
                BallSystem.AddScore(10, 2);
                break;
            case "LeftHit":
                BallSystem.TriggerLeftHit = false;
                break;
            case "RightHit":
                BallSystem.TriggerRightHit = false;
                break;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        string tag = collision.gameObject.tag;
        switch (tag)
        {
            case "Static":
                BallSystem.PlayHitSound();
                break;
            case "LeftHit":
                BallSystem.TriggerLeftHit = true;
                break;
            case "RightHit":
                BallSystem.TriggerRightHit = true;
                break;
        }
    }
}
