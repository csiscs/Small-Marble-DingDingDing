using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class PlungerDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerUpHandler
{
    [Header("手柄")]
    [Tooltip("手柄最多能向下拉多少像素")]
    [SerializeField] private float maxDownPixels = 100f;
    [Tooltip("手柄松开后回到的锚点 Y")]
    [SerializeField] private float restY = 100f;
    [Tooltip("松开后手柄弹回静止位置的时长（秒）")]
    [SerializeField] private float returnDuration = 0.1f;
    [Tooltip("拉动时弹簧刚度，拉得越深越不跟手")]
    [SerializeField] private float pullStiffness = 1.2f;
    [Tooltip("弹簧阻力随拉力递增的上限，到顶后不再更难拉")]
    [SerializeField] private float maxSpringIncrement = 2f;

    [Header("跟随杆")]
    [Tooltip("场内没有挂本脚本的弹射杆，会跟随手柄移动")]
    [SerializeField] private RectTransform follower;
    [Tooltip("跟随杆默认（静止）锚点 Y")]
    [SerializeField] private float followerRestY = -210f;
    [Tooltip("跟随杆最低能到的锚点 Y，不会再往下")]
    [SerializeField] private float followerMinY = -220f;

    [Header("弹簧弹射")]
    [Tooltip("拉满时给球的力度")]
    [SerializeField] private float launchForce = 300f;
    private float launchRange = 15f;
    [Tooltip("拉力到力度的弹簧指数。1 为线性；越大则轻拉越弱、拉满越猛")]
    [SerializeField] private float springPower = 2f;
    [Tooltip("球相对拉杆贴着通道边缘时的偏转角（度）")]
    [SerializeField] private float maxTiltDegrees = 18f;
    [Tooltip("力度随机幅度，0.1 表示在 90%～110% 之间取值")]
    [SerializeField] private float forceJitter = 0.1f;

    private const float LaneHalfWidth = 0.8f;

    private RectTransform rect;
    private Canvas canvas;
    private Rigidbody2D playerBody;
    private AudioSource hitSource;
    private float startScreenY;
    private float startAnchoredY;
    private bool dragging;

    private void Awake()
    {
        rect = (RectTransform)transform;
        canvas = GetComponentInParent<Canvas>();

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            player.TryGetComponent(out playerBody);

        TryGetComponent(out hitSource);

        SyncFollower();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        dragging = true;
        rect.DOKill();
        startScreenY = eventData.position.y;
        startAnchoredY = rect.anchoredPosition.y;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging)
            return;

        float scale = canvas != null ? canvas.scaleFactor : 1f;
        float deltaY = (eventData.position.y - startScreenY) / scale;
        float targetY = startAnchoredY + deltaY;

        Vector2 position = rect.anchoredPosition;
        position.y = SpringPullY(targetY);
        rect.anchoredPosition = position;
        SyncFollower();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        Release();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Release();
    }

    private void Release()
    {
        if (!dragging)
            return;

        dragging = false;
        TryLaunch();
        rect.DOKill();
        rect.DOAnchorPosY(restY, returnDuration)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true)
            .OnUpdate(SyncFollower);
    }

    private void TryLaunch()
    {
        if (playerBody == null || follower == null || maxDownPixels <= 0f)
            return;

        float pull = Mathf.Clamp01((restY - rect.anchoredPosition.y) / maxDownPixels);
        if (pull < 0.05f || !PlayerInLaunchLane())
            return;

        float force = launchForce * Mathf.Pow(pull, springPower);
        force *= Random.Range(1f - forceJitter, 1f + forceJitter);

        float offsetX = playerBody.position.x - follower.position.x;
        float tilt = Mathf.Clamp(offsetX / LaneHalfWidth, -1f, 1f);
        Vector2 dir = Quaternion.Euler(0f, 0f, -tilt * maxTiltDegrees) * Vector2.up;
        playerBody.velocity = dir.normalized * force;
        PlayHitSound();
    }

    private void PlayHitSound()
    {
        if (hitSource == null || hitSource.clip == null)
            return;

        hitSource.PlayOneShot(hitSource.clip);
    }

    private float SpringPullY(float targetY)
    {
        float desiredDown = restY - targetY;
        if (desiredDown <= 0f)
            return restY;

        float t = maxDownPixels > 0f ? desiredDown / maxDownPixels : 0f;
        float increment = Mathf.Min(t * pullStiffness, maxSpringIncrement);
        float actualDown = Mathf.Min(desiredDown / (1f + increment), maxDownPixels);
        return restY - actualDown;
    }

    private bool PlayerInLaunchLane()
    {
        Vector2 followerPos = follower.position;
        Vector2 playerPos = playerBody.position;
        if (Mathf.Abs(playerPos.x - followerPos.x) > LaneHalfWidth)
            return false;

        float dy = playerPos.y - followerPos.y;
        return dy >= 0f && dy <= launchRange;
    }

    private void SyncFollower()
    {
        if (follower == null)
            return;

        float delta = rect.anchoredPosition.y - restY;
        float followerY = Mathf.Clamp(followerRestY + delta, followerMinY, followerRestY);

        Vector2 position = follower.anchoredPosition;
        position.y = followerY;
        follower.anchoredPosition = position;
    }

    private void OnDestroy()
    {
        rect.DOKill();
    }
}
