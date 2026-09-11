using System.Collections;
using System.Text;
using UnityEngine;

public class MarbleForceHoleTest : MonoBehaviour
{
    [Header("力度")]
    [Tooltip("从该力度开始，每个球 +0.5，直到 Max Force")]
    [SerializeField] private float minForce = 100f;
    [SerializeField] private float maxForce = 330f;
    [Tooltip("一轮同时打出多少个球")]
    [SerializeField] private int ballsPerRound = 10;
    [Tooltip("放到发射点后，等多久再施力（球落入通道）")]
    [SerializeField] private float laneWait = 2f;
    [Tooltip("一轮超时（秒），没进洞的也结束")]
    [SerializeField] private float shotTimeout = 8f;

    [Header("运行")]
    [SerializeField] private bool runOnStart;
    [SerializeField] private KeyCode startKey = KeyCode.T;

    private GameObject template;
    private Rigidbody2D[] balls;
    private float[] forces;
    private Vector2 spawnAnchored;
    private Vector2 spawnWorld;
    private int[] holeHits;
    private int[] slotOfForce;
    private bool[] scoredThisRound;
    private int[] slotThisRound;
    private int roundBallCount;
    private int roundForceStart;
    private bool running;

    private void Start()
    {
        if (runOnStart)
            StartCoroutine(Run());
    }

    private void Update()
    {
        if (!running && Input.GetKeyDown(startKey))
            StartCoroutine(Run());
    }

    [ContextMenu("Run Force Hole Test")]
    private void RunFromMenu()
    {
        if (!Application.isPlaying || running)
            return;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        if (running)
            yield break;

        BuildForces();
        if (forces.Length == 0 || ballsPerRound <= 0)
        {
            Debug.LogWarning("MarbleForceHoleTest: 力度范围或球数无效");
            yield break;
        }

        running = true;
        yield return null;
        Prepare();
        MarbleSlotHole.PlayerEntered += OnPlayerEntered;

        int forceCount = forces.Length;
        holeHits = new int[forceCount];
        slotOfForce = new int[forceCount];
        scoredThisRound = new bool[ballsPerRound];
        slotThisRound = new int[ballsPerRound];
        for (int i = 0; i < forceCount; i++)
            slotOfForce[i] = -1;

        int roundCount = (forceCount + ballsPerRound - 1) / ballsPerRound;
        Debug.Log($"开始力度进洞测试：每轮 {ballsPerRound} 球各 +0.5 力度，从 {minForce} 到 {maxForce}，共 {forceCount} 档 / {roundCount} 轮");

        for (int round = 0; round < roundCount; round++)
        {
            roundForceStart = round * ballsPerRound;
            roundBallCount = Mathf.Min(ballsPerRound, forceCount - roundForceStart);

            for (int i = 0; i < roundBallCount; i++)
            {
                scoredThisRound[i] = false;
                slotThisRound[i] = -1;
                PlaceInLane(balls[i]);
            }

            if (laneWait > 0f)
                yield return new WaitForSeconds(laneWait);

            yield return new WaitForFixedUpdate();
            for (int i = 0; i < roundBallCount; i++)
                ApplyForce(balls[i], forces[roundForceStart + i]);

            float t = 0f;
            while (!AllScored() && t < shotTimeout)
            {
                t += Time.deltaTime;
                yield return null;
            }

            for (int i = 0; i < roundBallCount; i++)
                Park(balls[i]);

            LogRound(round);
        }

        roundBallCount = 0;
        MarbleSlotHole.PlayerEntered -= OnPlayerEntered;
        LogResult();
        running = false;
    }

    private void BuildForces()
    {
        float start = minForce;
        float end = maxForce;
        if (end < start)
        {
            float swap = start;
            start = end;
            end = swap;
        }

        const float step = 0.5f;
        int count = Mathf.FloorToInt((end - start) / step) + 1;
        forces = new float[count];
        for (int i = 0; i < count; i++)
            forces[i] = start + i * step;
        forces[count - 1] = end;
    }

    private void Prepare()
    {
        template = GameObject.FindGameObjectWithTag("Player");
        if (template == null)
            throw new System.Exception("找不到 Player");

        if (!template.TryGetComponent(out Rigidbody2D templateBody))
            throw new System.Exception("Player 没有 Rigidbody2D");

        spawnWorld = templateBody.position;
        RectTransform templateRt = template.transform as RectTransform;
        spawnAnchored = templateRt != null ? templateRt.anchoredPosition : (Vector2)template.transform.localPosition;
        template.SetActive(false);

        Transform parent = template.transform.parent;
        balls = new Rigidbody2D[ballsPerRound];
        for (int i = 0; i < ballsPerRound; i++)
        {
            GameObject clone = Instantiate(template, parent);
            clone.name = $"Player_Test_{i}";
            clone.tag = "Player";
            clone.SetActive(true);
            if (clone.TryGetComponent(out SmallBallTrigger trigger))
                DestroyImmediate(trigger);
            balls[i] = clone.GetComponent<Rigidbody2D>();
            Park(balls[i]);
        }

        MarbleGameController controller = FindObjectOfType<MarbleGameController>();
        if (controller != null)
            controller.enabled = false;

        GameEndCheck[] ends = FindObjectsOfType<GameEndCheck>();
        for (int i = 0; i < ends.Length; i++)
            ends[i].enabled = false;

        GameRestartCheck[] restarts = FindObjectsOfType<GameRestartCheck>();
        for (int i = 0; i < restarts.Length; i++)
            restarts[i].enabled = false;

        GameObject wall = GameObject.Find("StartWall");
        if (wall != null && wall.TryGetComponent(out Collider2D wallCol))
            wallCol.enabled = false;

        PlungerDrag plunger = FindObjectOfType<PlungerDrag>();
        if (plunger != null)
            plunger.enabled = false;
    }

    private bool AllScored()
    {
        for (int i = 0; i < roundBallCount; i++)
        {
            if (!scoredThisRound[i])
                return false;
        }

        return true;
    }

    private void PlaceInLane(Rigidbody2D body)
    {
        body.gameObject.SetActive(true);
        RectTransform rt = body.transform as RectTransform;
        if (rt != null)
            rt.anchoredPosition = spawnAnchored;
        else
            body.position = spawnWorld;

        body.rotation = 0f;
        body.velocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.WakeUp();
    }

    private static void ApplyForce(Rigidbody2D body, float force)
    {
        body.WakeUp();
        body.velocity = Vector2.up * force;
        body.angularVelocity = 0f;
    }

    private static void Park(Rigidbody2D body)
    {
        body.velocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.gameObject.SetActive(false);
    }

    private int IndexOfBall(Rigidbody2D body)
    {
        if (body == null || balls == null)
            return -1;

        for (int i = 0; i < roundBallCount; i++)
        {
            if (balls[i] == body)
                return i;
        }

        return -1;
    }

    private void OnPlayerEntered(Collider2D other, int slotIndex)
    {
        if (other == null || roundBallCount <= 0)
            return;

        int index = IndexOfBall(other.attachedRigidbody);
        if (index < 0 || scoredThisRound[index])
            return;

        scoredThisRound[index] = true;
        slotThisRound[index] = slotIndex;
        int forceIndex = roundForceStart + index;
        holeHits[forceIndex]++;
        slotOfForce[forceIndex] = slotIndex;
    }

    private void LogRound(int round)
    {
        int entered = 0;
        StringBuilder sb = new StringBuilder();
        float from = forces[roundForceStart];
        float to = forces[roundForceStart + roundBallCount - 1];
        sb.Append("第").Append(round + 1).Append("轮 ");
        sb.Append(from.ToString("0.##")).Append('-').Append(to.ToString("0.##"));
        sb.Append(':');
        for (int i = 0; i < roundBallCount; i++)
        {
            sb.Append(' ').Append(forces[roundForceStart + i].ToString("0.##")).Append('=');
            if (slotThisRound[i] >= 0)
            {
                entered++;
                sb.Append("洞").Append(slotThisRound[i]);
            }
            else
            {
                sb.Append("未进");
            }
        }

        sb.Append("  进洞 ").Append(entered).Append('/').Append(roundBallCount);
        Debug.Log(sb.ToString());
    }

    private void LogResult()
    {
        int total = forces.Length;
        int[] counts = new int[12];
        for (int i = 0; i < total; i++)
        {
            int slot = slotOfForce[i];
            if (slot >= 0 && slot < 12)
                counts[slot]++;
        }

        StringBuilder sb = new StringBuilder();
        sb.Append("洞-占比\n");
        for (int i = 0; i < 12; i++)
        {
            float pct = total > 0 ? counts[i] * 100f / total : 0f;
            sb.Append(i).Append('-').Append(pct.ToString("0.0")).Append("%\n");
        }
        Debug.Log(sb.ToString());
    }

    private void OnDestroy()
    {
        MarbleSlotHole.PlayerEntered -= OnPlayerEntered;
    }
}
