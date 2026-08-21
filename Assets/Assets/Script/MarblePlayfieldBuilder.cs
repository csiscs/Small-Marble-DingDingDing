using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MarblePlayfieldBuilder : MonoBehaviour
{
    [SerializeField] private PhysicsMaterial2D bounceMaterial;
    [SerializeField] private float pegRadius = 9f;
    [SerializeField] private float pegSpacingX = 50f;
    [SerializeField] private float pegSpacingY = 44f;
    [SerializeField] private int slotCount = 12;
    [SerializeField] private float slotHeight = 72f;
    [SerializeField] private float launchChannelWidth = 50f;
    [SerializeField] private float wowHoleRadius = 76f;
    [SerializeField] private Vector2 wowHoleOffset = new Vector2(0f, 28f);
    [SerializeField] private bool showPegVisuals = true;

    [ContextMenu("Rebuild Colliders")]
    public void Rebuild()
    {
        ClearGenerated();

        RectTransform playfield = (RectTransform)transform;
        float width = playfield.rect.width;
        float height = playfield.rect.height;
        if (width < 10f || height < 10f)
            return;

        float left = -width * 0.5f;
        float right = width * 0.5f;
        float bottom = -height * 0.5f;
        float top = height * 0.5f;
        float dividerX = right - launchChannelWidth;
        float slotTop = bottom + slotHeight;
        float mainLeft = left + 18f;
        float mainRight = dividerX - 16f;

        Transform pegsRoot = CreateRoot("Pegs");
        Transform wallsRoot = CreateRoot("Walls");
        Transform slotsRoot = CreateRoot("Slots");

        BuildWalls(wallsRoot, left, right, bottom, top, dividerX, slotTop);
        BuildSlots(slotsRoot, mainLeft, mainRight, bottom, slotTop);
        BuildPegs(pegsRoot, mainLeft, mainRight, slotTop, top, dividerX);
    }

    private void ClearGenerated()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
    }

    private Transform CreateRoot(string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        return rect;
    }

    private void BuildWalls(Transform root, float left, float right, float bottom, float top, float dividerX, float slotTop)
    {
        var points = new List<Vector2>();
        float curveHeight = 58f;
        int curveCount = 18;

        points.Add(new Vector2(left, slotTop));
        points.Add(new Vector2(left, top - curveHeight));

        for (int i = 0; i <= curveCount; i++)
        {
            float t = i / (float)curveCount;
            float x = Mathf.Lerp(left, right, t);
            float y = top - curveHeight * (1f - Mathf.Sin(Mathf.PI * t));
            points.Add(new Vector2(x, y));
        }

        points.Add(new Vector2(right, top - curveHeight));
        points.Add(new Vector2(right, bottom + 8f));

        CreateEdge("OuterWall", root, points.ToArray());
        CreateEdge("LaunchDivider", root, new[]
        {
            new Vector2(dividerX, slotTop),
            new Vector2(dividerX, top - curveHeight - 36f)
        });
    }

    private void BuildSlots(Transform root, float mainLeft, float mainRight, float bottom, float slotTop)
    {
        float span = mainRight - mainLeft;
        float slotWidth = span / slotCount;
        float dividerWidth = 8f;

        for (int i = 1; i < slotCount; i++)
        {
            float x = mainLeft + slotWidth * i;
            CreateBox($"Divider_{i}", root, new Vector2(x, (slotTop + bottom) * 0.5f), new Vector2(dividerWidth, slotHeight), false);
        }

        CreateBox("SlotFloor", root, new Vector2((mainLeft + mainRight) * 0.5f, bottom + 4f), new Vector2(span + 8f, 8f), false);

        for (int i = 0; i < slotCount; i++)
        {
            float x = mainLeft + slotWidth * (i + 0.5f);
            CreateBox($"Slot_{i}", root, new Vector2(x, bottom + slotHeight * 0.45f), new Vector2(slotWidth - 10f, slotHeight * 0.7f), true);
        }
    }

    private void BuildPegs(Transform root, float mainLeft, float mainRight, float slotTop, float top, float dividerX)
    {
        float topRowY = top - 92f;
        float bottomRowY = slotTop + 28f;
        var positions = new List<Vector2>();

        int topCount = 8;
        for (int i = 0; i < topCount; i++)
        {
            float t = topCount == 1 ? 0.5f : i / (float)(topCount - 1);
            float x = Mathf.Lerp(mainLeft + 24f, mainRight - 24f, t);
            float y = topRowY - 16f * Mathf.Sin(Mathf.PI * t);
            TryAddPeg(positions, new Vector2(x, y), dividerX);
        }

        int rows = 9;
        for (int row = 0; row < rows; row++)
        {
            bool offset = row % 2 == 1;
            int cols = offset ? 8 : 9;
            float y = Mathf.Lerp(topRowY - pegSpacingY, bottomRowY + pegSpacingY, row / (float)(rows - 1));
            float startX = offset ? mainLeft + pegSpacingX * 0.85f : mainLeft + 22f;
            float endX = offset ? mainRight - 22f : mainRight - pegSpacingX * 0.35f;

            for (int col = 0; col < cols; col++)
            {
                float t = cols == 1 ? 0.5f : col / (float)(cols - 1);
                var pos = new Vector2(Mathf.Lerp(startX, endX, t), y);
                if (InsideWowHole(pos))
                    continue;
                TryAddPeg(positions, pos, dividerX);
            }
        }

        for (int i = 1; i < slotCount; i++)
        {
            float span = mainRight - mainLeft;
            float x = mainLeft + span / slotCount * i;
            TryAddPeg(positions, new Vector2(x, bottomRowY), dividerX);
        }

        float[] sideYs = { 40f, 0f, -40f };
        foreach (float y in sideYs)
        {
            TryAddPeg(positions, new Vector2(mainLeft + 18f, y), dividerX);
            TryAddPeg(positions, new Vector2(mainRight - 18f, y), dividerX);
        }

        for (int i = 0; i < positions.Count; i++)
            CreatePeg($"Peg_{i}", root, positions[i]);
    }

    private bool InsideWowHole(Vector2 pos)
    {
        return (pos - wowHoleOffset).sqrMagnitude < wowHoleRadius * wowHoleRadius;
    }

    private void TryAddPeg(List<Vector2> positions, Vector2 pos, float dividerX)
    {
        if (pos.x > dividerX - pegRadius * 2f)
            return;

        for (int i = 0; i < positions.Count; i++)
        {
            if ((positions[i] - pos).sqrMagnitude < (pegSpacingX * 0.55f) * (pegSpacingX * 0.55f))
                return;
        }

        positions.Add(pos);
    }

    private void CreatePeg(string name, Transform parent, Vector2 position)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = Vector2.one * (pegRadius * 2f);

        if (showPegVisuals)
        {
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.color = new Color(0.85f, 0.85f, 0.9f, 0.9f);
            image.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
        }

        var collider = go.AddComponent<CircleCollider2D>();
        collider.radius = pegRadius;
        collider.sharedMaterial = bounceMaterial;
    }

    private void CreateBox(string name, Transform parent, Vector2 position, Vector2 size, bool isTrigger)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        var collider = go.AddComponent<BoxCollider2D>();
        collider.size = size;
        collider.isTrigger = isTrigger;
        if (!isTrigger)
            collider.sharedMaterial = bounceMaterial;
    }

    private void CreateEdge(string name, Transform parent, Vector2[] points)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);

        var collider = go.AddComponent<EdgeCollider2D>();
        collider.points = points;
        collider.sharedMaterial = bounceMaterial;
        collider.edgeRadius = 4f;
    }
}
