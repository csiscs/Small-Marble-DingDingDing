using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MarbleGameController : MonoBehaviour
{
    private static readonly int[] Multipliers = { 2, 4, 6, 8, 10 };
    private static readonly int[] LampCounts = { 4, 3, 2, 1, 1 };
    private static readonly int[] MultiplierWeights = { 50, 25, 12, 8, 5 };

    [Header("引用")]
    [Tooltip("灯根节点 Lights")]
    [SerializeField] private Transform lightsRoot;
    [Tooltip("洞口根节点，子物体为 Slot_0 到 Slot_11")]
    [SerializeField] private Transform slotsRoot;
    [Tooltip("开闸动画")]
    [SerializeField] private StartWallMover startWall;
    [Tooltip("货币系统")]
    [SerializeField] private MarbleCoinEconomy economy;
    [Tooltip("开始按钮")]
    [SerializeField] private Button startButton;
    [Tooltip("首次打开应用时显示的提示")]
    [SerializeField] private GameObject firstTip;
    [Tooltip("用积分买币的按钮")]
    [SerializeField] private Button buyButton;

    [Header("文本")]
    [Tooltip("本局倍率")]
    [SerializeField] private TextMeshProUGUI scaleText;
    [Tooltip("投入 × 倍率的结果")]
    [SerializeField] private TextMeshProUGUI resultText;
    [Tooltip("积分数")]
    [SerializeField] private TextMeshProUGUI scoreText;

    private static readonly string ScorePrefKey = "Marble_Score";
    private static readonly string FirstTipKey = "Marble_FirstTip";
    private const int FirstTipScore = 5;
    private const int CoinsPerPoint = 100;
    private const int BuyCoinCount = 100;

    private readonly bool[] lampsOn = new bool[12];
    private readonly Transform[] lamps = new Transform[12];
    private readonly Animation[] lampAnims = new Animation[12];
    private bool roundActive;
    private bool keepWinUntilInsert;
    private int roundBet;
    private int multiplier;
    private int score;

    private void Awake()
    {
        BindRefs();
        BindSlots();
        CollectLamps();
        SetAllLamps(false);
        if (economy != null)
        {
            economy.CoinInserted += OnCoinInserted;
            economy.PayoutStarted += PlayLampAnims;
            economy.PayoutEnded += StopLampAnims;
        }
        if (scaleText != null)
            scaleText.text = "";
        if (resultText != null)
            resultText.text = "0";
        LoadScore();
        RefreshScore();
    }

    private void Start()
    {
        ShowFirstTipIfNeeded();
        if (buyButton != null)
            buyButton.onClick.AddListener(TryBuyCoins);
    }

    public void TryStart()
    {
        if (roundActive || keepWinUntilInsert || economy == null || economy.Consumed <= 0)
            return;

        StopLampAnims();
        int pick = PickWeightedIndex(MultiplierWeights);
        multiplier = Multipliers[pick];
        LightRandomLamps(LampCounts[pick]);
        keepWinUntilInsert = false;

        if (scaleText != null)
            scaleText.text = multiplier.ToString();
        RefreshRoundResult();

        roundActive = true;
        if (startButton != null)
            startButton.interactable = false;
        if (startWall != null)
            startWall.Play();
    }

    private void Update()
    {
        if (roundActive)
            RefreshRoundResult();
    }

    public void OnBallEnterSlot(int slotIndex)
    {
        if (!roundActive)
            return;

        roundActive = false;
        roundBet = economy != null ? economy.Consumed : 0;
        if (economy != null)
            economy.LockRoundBet();

        bool win = slotIndex >= 0 && slotIndex < lampsOn.Length && lampsOn[slotIndex];
        int payout = win ? roundBet * multiplier : 0;
        if (resultText != null)
            resultText.text = win ? payout.ToString() : "";
        if (payout > 0)
        {
            economy.EnqueuePayout(payout);
            AddPayoutScore(payout);
        }

        if (!win)
        {
            if (scaleText != null)
                scaleText.text = "";
            economy.SettleRound(roundBet);
            keepWinUntilInsert = false;
        }
        else
        {
            keepWinUntilInsert = true;
        }

        SetAllLamps(false);
        roundBet = 0;
        multiplier = 0;
        if (startButton != null)
            startButton.interactable = true;
    }

    private void OnCoinInserted()
    {
        if (roundActive || !keepWinUntilInsert)
            return;

        keepWinUntilInsert = false;
        if (scaleText != null)
            scaleText.text = "";
        if (resultText != null)
            resultText.text = "";
        if (economy != null)
            economy.SettleRound(economy.Consumed);
    }

    private void ShowFirstTipIfNeeded()
    {
        bool firstTrigger = PlayerPrefs.GetInt(FirstTipKey, 0) == 0;
        if (firstTip != null)
            firstTip.SetActive(firstTrigger);
        if (!firstTrigger)
            return;

        PlayerPrefs.SetInt(FirstTipKey, 1);
        score += FirstTipScore;
        SaveScore();
        RefreshScore();
    }

    public void TryBuyCoins()
    {
        if (score < 1 || economy == null)
            return;

        score--;
        SaveScore();
        RefreshScore();
        economy.EnqueueSpawn(BuyCoinCount);
    }

    private void AddPayoutScore(int payout)
    {
        if (payout <= 0)
            return;

        int gained = payout / CoinsPerPoint;
        if (gained <= 0)
            return;

        score += gained;
        SaveScore();
        RefreshScore();
    }

    private void LoadScore()
    {
        score = Mathf.Max(0, PlayerPrefs.GetInt(ScorePrefKey, 0));
    }

    private void SaveScore()
    {
        PlayerPrefs.SetInt(ScorePrefKey, score);
        PlayerPrefs.Save();
    }

    private void RefreshScore()
    {
        if (scoreText != null)
            scoreText.text = score.ToString();
    }

    private static int PickWeightedIndex(int[] weights)
    {
        int total = 0;
        for (int i = 0; i < weights.Length; i++)
            total += weights[i];

        int roll = Random.Range(0, total);
        int acc = 0;
        for (int i = 0; i < weights.Length; i++)
        {
            acc += weights[i];
            if (roll < acc)
                return i;
        }

        return weights.Length - 1;
    }

    private void LightRandomLamps(int count)
    {
        for (int i = 0; i < lampsOn.Length; i++)
            lampsOn[i] = false;

        int[] order = new int[12];
        for (int i = 0; i < 12; i++)
            order[i] = i;
        for (int i = 11; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = order[i];
            order[i] = order[j];
            order[j] = tmp;
        }

        count = Mathf.Clamp(count, 0, 12);
        for (int i = 0; i < count; i++)
            lampsOn[order[i]] = true;

        ApplyLampVisuals();
    }

    private void SetAllLamps(bool on)
    {
        for (int i = 0; i < lampsOn.Length; i++)
            lampsOn[i] = on;
        ApplyLampVisuals();
    }

    private void RefreshRoundResult()
    {
        if (economy == null)
            return;

        roundBet = economy.Consumed;
        if (resultText != null)
            resultText.text = (roundBet * multiplier).ToString();
    }

    private void PlayLampAnims()
    {
        SetLampAnimsPlaying(true);
    }

    private void StopLampAnims()
    {
        SetLampAnimsPlaying(false);
        ApplyLampVisuals();
    }

    private void SetLampAnimsPlaying(bool play)
    {
        for (int i = 0; i < lampAnims.Length; i++)
        {
            Animation anim = lampAnims[i];
            if (anim == null)
                continue;

            if (play)
                anim.Play();
            else
                anim.Stop();
        }
    }

    private void ApplyLampVisuals()
    {
        for (int i = 0; i < lamps.Length; i++)
        {
            if (lamps[i] == null)
                continue;

            Transform glow = lamps[i].Find("Light (1)");
            if (glow != null)
                glow.gameObject.SetActive(lampsOn[i]);
        }
    }

    private void BindRefs()
    {
        if (economy == null)
            economy = GetComponent<MarbleCoinEconomy>();
        if (lightsRoot == null)
            lightsRoot = FindNamed("Lights");
        if (slotsRoot == null)
        {
            Transform slot0 = FindNamed("Slot_0");
            if (slot0 != null)
                slotsRoot = slot0.parent;
        }

        if (startWall == null)
        {
            Transform wall = FindNamed("StartWall");
            if (wall != null)
                startWall = wall.GetComponent<StartWallMover>();
        }

        if (startButton == null)
        {
            Transform button = FindNamed("StartButton");
            if (button != null)
                startButton = button.GetComponent<Button>();
        }

        if (firstTip == null)
        {
            Transform tip = FindNamed("FirstTip");
            if (tip != null)
                firstTip = tip.gameObject;
        }

        if (scaleText == null)
        {
            Transform scale = FindNamed("Scale");
            if (scale != null)
                scaleText = scale.GetComponent<TextMeshProUGUI>();
        }

        if (resultText == null)
        {
            Transform result = FindNamed("Result");
            if (result != null)
                resultText = result.GetComponent<TextMeshProUGUI>();
        }

        if (scoreText == null)
        {
            Transform scoreLabel = FindNamed("ScoreText");
            if (scoreLabel != null)
                scoreText = scoreLabel.GetComponent<TextMeshProUGUI>();
        }

        if (buyButton == null)
        {
            Transform buy = FindNamed("BuyBtn");
            if (buy != null)
                buyButton = buy.GetComponent<Button>();
        }
    }

    private void BindSlots()
    {
        if (slotsRoot == null)
            return;

        for (int i = 0; i < slotsRoot.childCount; i++)
        {
            Transform child = slotsRoot.GetChild(i);
            if (!child.name.StartsWith("Slot_"))
                continue;
            if (!int.TryParse(child.name.Substring(5), out int index))
                continue;

            MarbleSlotHole hole = child.GetComponent<MarbleSlotHole>();
            if (hole == null)
                hole = child.gameObject.AddComponent<MarbleSlotHole>();
            hole.Bind(this, index);
        }
    }

    private void CollectLamps()
    {
        if (lightsRoot == null)
            return;

        for (int i = 0; i < lightsRoot.childCount; i++)
        {
            Transform child = lightsRoot.GetChild(i);
            if (!child.name.StartsWith("Light"))
                continue;
            if (!int.TryParse(child.name.Substring(5), out int number))
                continue;
            if (number < 1 || number > 12)
                continue;
            lamps[number - 1] = child;
            lampAnims[number - 1] = child.GetComponent<Animation>();
        }
    }

    private static Transform FindNamed(string objectName)
    {
        GameObject found = GameObject.Find(objectName);
        return found != null ? found.transform : null;
    }
}
