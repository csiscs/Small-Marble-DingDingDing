using TMPro;
using UnityEngine;

public class SmallBallSystem : MonoBehaviour
{
    [HideInInspector] public bool TriggerLeftHit = false;
    [HideInInspector] public bool TriggerRightHit = false;
    [SerializeField] private GameObject smallBall;
    [SerializeField] private GameObject gameEnd;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI scoreText2;
    [SerializeField] private TextMeshProUGUI maxScore;
    [SerializeField] private Transform left;
    [SerializeField] private Transform right;
    [SerializeField] private AudioPlaySystem audioSystem;
    [SerializeField] private AudioSource hitClip;
    [SerializeField] private AudioSource coinClip;
    [SerializeField] private float speed = 100f;
    [SerializeField] private float launchMultiplier = 90f;

    private int score;
    private Rigidbody2D rb;
    private bool leftDown = false;
    private bool rightDown = false;

    private const float TargetAngleUp = 130f;
    private const float TargetAngleDown = 65f;

    private void Start()
    {
        Application.targetFrameRate = 120;
        QualitySettings.vSyncCount = 0;

        smallBall ??= GameObject.FindGameObjectWithTag("Player");
        audioSystem ??= GameObject.FindGameObjectWithTag("Audio").GetComponent<AudioPlaySystem>();

        var trigger = smallBall.AddComponent<SmallBallTrigger>();
        trigger.BallSystem = this;

        smallBall.TryGetComponent(out rb);
        GameEnd();
    }

    public void LeftDown()
    {
        leftDown = true;
    }

    public void LeftUp()
    {
        leftDown = false;
    }

    public void RightDown()
    {
        rightDown = true;
    }

    public void RightUp()
    {
        rightDown = false;
    }

    public void AddScore(int points = 1, int velocityScale = 10)
    {
        score += points;
        scoreText2.text = scoreText.text = score.ToString();

        float velocityMultiplier = launchMultiplier / velocityScale;
        float newVelocityY = rb.velocity.y > 0
            ? rb.velocity.y + velocityMultiplier
            : rb.velocity.y - velocityMultiplier;

        rb.velocity = new Vector2(rb.velocity.x, newVelocityY);
    }

    public void PlayHitSound()
    {
        hitClip.Play();
    }

    private void Update()
    {
        if (Input.GetKey(KeyCode.A) || leftDown)
            RotatePaddleUp(left, TriggerLeftHit);
        else
            RotatePaddleDown(left);

        if (Input.GetKey(KeyCode.D) || rightDown)
            RotatePaddleUp(right, TriggerRightHit);
        else
            RotatePaddleDown(right);
    }

    private void RotatePaddleUp(Transform paddle, bool triggerHit)
    {
        if (paddle.eulerAngles.z < 129f)
        {
            Vector3 rot = paddle.eulerAngles;
            rot.z = Mathf.MoveTowardsAngle(rot.z, TargetAngleUp, speed * Time.deltaTime);
            paddle.eulerAngles = rot;

            if (triggerHit)
            {
                rb.velocity = new Vector2(rb.velocity.x, launchMultiplier);
            }
        }
    }

    private void RotatePaddleDown(Transform paddle)
    {
        if (paddle.eulerAngles.z > 66f)
        {
            Vector3 rot = paddle.eulerAngles;
            rot.z = Mathf.MoveTowardsAngle(rot.z, TargetAngleDown, speed * Time.deltaTime);
            paddle.eulerAngles = rot;
        }
    }

    public void GameEnd()
    {
        gameEnd.SetActive(true);
        int maxScoreValue = Mathf.Max(PlayerPrefs.GetInt("Score"), score);
        PlayerPrefs.SetInt("Score", maxScoreValue);
        maxScore.text = maxScoreValue.ToString();
        audioSystem.GameEnd();
    }

    public void GameStart()
    {
        gameEnd.SetActive(false);
        smallBall.transform.localPosition = Vector3.zero;
        rb.velocity = Vector2.zero;
        AddScore(-score);
        audioSystem.GameStart();
    }

    public void RestartBall()
    {
        smallBall.transform.localPosition = Vector3.zero;
        coinClip.Play();
    }

    public void CloseGame()
    {
        Application.Quit();
    }
}
