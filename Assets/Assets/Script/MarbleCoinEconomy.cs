using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MarbleCoinEconomy : MonoBehaviour
{
    [Header("资源")]
    [Tooltip("货币预制体")]
    [SerializeField] private GameObject coinPrefab;
    [Tooltip("开局生成数量")]
    [SerializeField] private int startCoinCount = 50;
    [Tooltip("每秒生成多少个（开局与出奖共用）")]
    [SerializeField] private float spawnPerSecond = 30f;
    [Tooltip("IntoPool 里每秒消失多少个")]
    [SerializeField] private float consumePerSecond = 10f;
    [Tooltip("出现在 IntoPoint / OutPoint / StartOutPoint 时，X 轴随机偏离范围（正负）")]
    [SerializeField] private float spawnXJitter = 10f;

    [Header("点位与池")]
    [Tooltip("开局出币点")]
    [SerializeField] private Transform startOutPoint;
    [Tooltip("中奖出币点")]
    [SerializeField] private Transform outPoint;
    [Tooltip("投入出现点")]
    [SerializeField] private Transform intoPoint;
    [Tooltip("场外币父节点（OutPoolTrigger）")]
    [SerializeField] private Transform outPool;
    [Tooltip("投入区父节点（IntoPoolTrigger）")]
    [SerializeField] private Transform intoPool;
    [Tooltip("场外币重叠检测，按数组顺序取币，前一个没有才取后一个")]
    [SerializeField] private MarbleCoinPoolSensor[] outSensors;
    [Tooltip("投入区重叠检测")]
    [SerializeField] private MarbleCoinPoolSensor intoSensor;

    [Header("UI")]
    [Tooltip("投入按钮")]
    [SerializeField] private Button intoButton;
    [Tooltip("长按后每秒连续投入多少个")]
    [SerializeField] private float holdInsertPerSecond = 20f;
    [Tooltip("按住多久后开始连投（秒），短于此时间只走单击")]
    [SerializeField] private float holdInsertDelay = 0.3f;
    [Tooltip("已消耗投入数")]
    [SerializeField] private TextMeshProUGUI numText;

    [Header("中奖音效")]
    [Tooltip("播放中奖出币音效的 AudioSource")]
    [SerializeField] private AudioSource payoutSource;
    [Tooltip("出奖时加快滚动的 UVScroll 材质")]
    [SerializeField] private Material uvScrollMaterial;
    [Tooltip("出奖中 UVScroll 的 Speed X")]
    [SerializeField] private float payoutScrollSpeedX = -1f;
    [Tooltip("平时 UVScroll 的 Speed X")]
    [SerializeField] private float idleScrollSpeedX = -0.05f;

    public int Consumed { get; private set; }

    public event System.Action CoinInserted;
    public event System.Action PayoutStarted;
    public event System.Action PayoutEnded;

    private int pendingSpawn;
    private int pendingPayout;
    private int overflowConsumed;
    private bool roundBetLocked;
    private float spawnAcc;
    private float consumeAcc;
    private bool intoHolding;
    private bool holdRepeating;
    private float holdTime;
    private float holdInsertAcc;
    private readonly Queue<GameObject> pool = new Queue<GameObject>();
    private bool payoutFxActive;
    private static readonly int ScrollSpeedXId = Shader.PropertyToID("_ScrollSpeedX");

    private void Awake()
    {
        BindRefs();
        SetupPayoutAudio();
        RefreshNum();
        AllowCoinSelfCollision();
    }

    private void Start()
    {
        if (intoButton != null)
        {
            intoButton.onClick.AddListener(OnIntoClick);
            WireHold(intoButton.gameObject);
        }

        EnqueueSpawn(startCoinCount);
    }

    private void Update()
    {
        TickSpawn();
        TickConsume();
        TickHoldInsert();
    }

    public void EnqueueSpawn(int count)
    {
        if (count > 0)
            pendingSpawn += count;
    }

    public void EnqueuePayout(int count)
    {
        if (count <= 0)
            return;

        pendingPayout += count;
        BeginPayoutFx();
    }

    public void InsertOne()
    {
        if (intoPoint == null || !TryTakeOutCoin(out GameObject coin))
            return;

        PlaceInPool(coin, intoPool, intoPoint);
        ResetBody(coin);
        CoinInserted?.Invoke();
    }

    private void OnIntoClick()
    {
        if (holdRepeating)
            return;

        InsertOne();
    }

    private void OnIntoPointerDown(BaseEventData _)
    {
        intoHolding = true;
        holdRepeating = false;
        holdTime = 0f;
        holdInsertAcc = 0f;
    }

    private void OnIntoPointerUp(BaseEventData _)
    {
        intoHolding = false;
    }

    private void TickHoldInsert()
    {
        if (!intoHolding)
            return;

        holdTime += Time.deltaTime;
        if (!holdRepeating)
        {
            if (holdTime < holdInsertDelay)
                return;
            holdRepeating = true;
            holdInsertAcc = 0f;
        }

        holdInsertAcc += Time.deltaTime;
        float interval = 1f / Mathf.Max(0.01f, holdInsertPerSecond);
        while (holdInsertAcc >= interval)
        {
            holdInsertAcc -= interval;
            InsertOne();
        }
    }

    private void WireHold(GameObject target)
    {
        EventTrigger trigger = target.GetComponent<EventTrigger>();
        if (trigger == null)
            trigger = target.AddComponent<EventTrigger>();

        AddTrigger(trigger, EventTriggerType.PointerDown, OnIntoPointerDown);
        AddTrigger(trigger, EventTriggerType.PointerUp, OnIntoPointerUp);
        AddTrigger(trigger, EventTriggerType.PointerExit, OnIntoPointerUp);
    }

    private static void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(callback);
        trigger.triggers.Add(entry);
    }

    public void LockRoundBet()
    {
        roundBetLocked = true;
    }

    public void SettleRound(int lockedBet)
    {
        Consumed = Mathf.Max(0, Consumed - lockedBet) + overflowConsumed;
        overflowConsumed = 0;
        roundBetLocked = false;
        RefreshNum();
    }

    private void TickSpawn()
    {
        int remaining = pendingSpawn + pendingPayout;
        if (remaining <= 0 || coinPrefab == null)
            return;
        if (outPoint == null && startOutPoint == null)
            return;

        spawnAcc += Time.deltaTime;
        float interval = 1f / Mathf.Max(0.01f, spawnPerSecond);
        while (remaining > 0 && spawnAcc >= interval)
        {
            spawnAcc -= interval;
            bool payout = pendingPayout > 0;
            Transform point = payout ? outPoint : startOutPoint;
            if (point == null)
                point = outPoint;
            if (point == null)
                break;

            if (payout)
                pendingPayout--;
            else
                pendingSpawn--;
            remaining--;
            SpawnOne(point);
            if (payout && pendingPayout <= 0)
                EndPayoutFx();
        }
    }

    private void TickConsume()
    {
        if (!HasIntoCoin())
        {
            consumeAcc = 0f;
            return;
        }

        consumeAcc += Time.deltaTime;
        float interval = 1f / Mathf.Max(0.01f, consumePerSecond);
        while (HasIntoCoin() && consumeAcc >= interval)
        {
            consumeAcc -= interval;
            if (!TryTakeIntoCoin(out GameObject coin))
                break;

            Recycle(coin);
            if (roundBetLocked)
            {
                overflowConsumed++;
                continue;
            }

            Consumed++;
            RefreshNum();
        }
    }

    private void SpawnOne(Transform point)
    {
        Transform parent = outPool != null ? outPool : point;
        GameObject coin = GetCoin(parent);
        PlaceInPool(coin, parent, point);
        ResetBody(coin);
    }

    private GameObject GetCoin(Transform parent)
    {
        GameObject coin = null;
        while (pool.Count > 0)
        {
            coin = pool.Dequeue();
            if (coin != null)
                break;
        }

        if (coin == null)
            coin = Instantiate(coinPrefab, parent, false);

        coin.SetActive(true);
        return coin;
    }

    private void Recycle(GameObject coin)
    {
        if (coin == null)
            return;

        RemoveFromOutSensors(coin);
        if (intoSensor != null)
            intoSensor.Remove(coin);

        ResetBody(coin);
        coin.SetActive(false);
        coin.transform.SetParent(outPool != null ? outPool : transform, false);
        pool.Enqueue(coin);
    }

    private bool TryTakeOutCoin(out GameObject coin)
    {
        if (outSensors != null)
        {
            for (int i = 0; i < outSensors.Length; i++)
            {
                MarbleCoinPoolSensor sensor = outSensors[i];
                if (sensor == null || sensor.Count <= 0)
                    continue;
                if (!sensor.TryTakeRandom(out coin))
                    continue;

                RemoveFromOutSensors(coin);
                return true;
            }
        }

        coin = null;
        return false;
    }

    private void RemoveFromOutSensors(GameObject coin)
    {
        if (outSensors == null)
            return;

        for (int i = 0; i < outSensors.Length; i++)
        {
            if (outSensors[i] != null)
                outSensors[i].Remove(coin);
        }
    }

    private bool TryTakeIntoCoin(out GameObject coin)
    {
        if (intoSensor != null)
            return intoSensor.TryTakeOne(out coin);

        coin = null;
        return false;
    }

    private bool HasIntoCoin()
    {
        return intoSensor != null && intoSensor.Count > 0;
    }

    private static void ResetBody(GameObject coin)
    {
        if (!coin.TryGetComponent(out Rigidbody2D body))
            return;

        body.velocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.bodyType = RigidbodyType2D.Dynamic;
        body.WakeUp();
    }

    private static void AllowCoinSelfCollision()
    {
        int coinLayer = LayerMask.NameToLayer("Coin");
        if (coinLayer >= 0)
            Physics2D.IgnoreLayerCollision(coinLayer, coinLayer, false);
    }

    private void PlaceInPool(GameObject coin, Transform parent, Transform point)
    {
        if (coin == null || parent == null || point == null)
            return;

        coin.transform.SetParent(parent, false);

        Vector2 local = LocalInParent(parent, point);
        local.x += Random.Range(-spawnXJitter, spawnXJitter);

        RectTransform coinRt = coin.transform as RectTransform;
        if (coinRt != null)
        {
            coinRt.anchoredPosition = local;
            coinRt.localRotation = Quaternion.identity;
            coinRt.localScale = Vector3.one;
        }
        else
        {
            coin.transform.localPosition = local;
            coin.transform.localRotation = Quaternion.identity;
            coin.transform.localScale = Vector3.one;
        }
    }

    private static Vector2 LocalInParent(Transform parent, Transform point)
    {
        RectTransform parentRt = parent as RectTransform;
        RectTransform pointRt = point as RectTransform;
        if (parentRt != null && pointRt != null)
        {
            if (pointRt.parent == parentRt)
                return pointRt.anchoredPosition;
            if (pointRt.parent == parentRt.parent)
                return pointRt.anchoredPosition - parentRt.anchoredPosition;
        }

        return parent.InverseTransformPoint(point.position);
    }

    private void OnDestroy()
    {
        EndPayoutFx();
    }

    private void SetupPayoutAudio()
    {
        if (payoutSource == null)
            payoutSource = GetComponent<AudioSource>();
        if (payoutSource == null)
            return;

        payoutSource.playOnAwake = false;
        payoutSource.loop = true;
    }

    private void BeginPayoutFx()
    {
        PlayPayoutSound();
        SetUvScrollSpeed(payoutScrollSpeedX);
        if (payoutFxActive)
            return;

        payoutFxActive = true;
        PayoutStarted?.Invoke();
    }

    private void EndPayoutFx()
    {
        StopPayoutSound();
        SetUvScrollSpeed(idleScrollSpeedX);
        if (!payoutFxActive)
            return;

        payoutFxActive = false;
        PayoutEnded?.Invoke();
    }

    private void SetUvScrollSpeed(float speedX)
    {
        if (uvScrollMaterial != null)
            uvScrollMaterial.SetFloat(ScrollSpeedXId, speedX);
    }

    private void PlayPayoutSound()
    {
        if (payoutSource == null || payoutSource.clip == null)
            return;

        if (!payoutSource.isPlaying)
            payoutSource.Play();
    }

    private void StopPayoutSound()
    {
        if (payoutSource != null && payoutSource.isPlaying)
            payoutSource.Stop();
    }

    private void RefreshNum()
    {
        if (numText != null)
            numText.text = Consumed.ToString();
    }

    private void BindRefs()
    {
        if (startOutPoint == null)
            startOutPoint = FindNamed("StartOutPoint");
        if (outPoint == null)
            outPoint = FindNamed("OutPoint");
        if (intoPoint == null)
            intoPoint = FindNamed("IntoPoint");
        if (intoButton == null)
        {
            Transform button = FindNamed("IntoBtn");
            if (button != null)
                intoButton = button.GetComponent<Button>();
        }

        if (numText == null)
        {
            Transform num = FindNamed("Num");
            if (num != null)
                numText = num.GetComponent<TextMeshProUGUI>();
        }
    }

    private static Transform FindNamed(string objectName)
    {
        GameObject found = GameObject.Find(objectName);
        return found != null ? found.transform : null;
    }
}
