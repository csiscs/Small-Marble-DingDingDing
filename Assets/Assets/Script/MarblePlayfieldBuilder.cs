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
    [SerializeField] private float largePegRadius = 12f;
    [SerializeField] private Sprite smallPegSprite;
    [SerializeField] private Sprite largePegSprite;

    [ContextMenu("Rebuild Colliders")]
    public void Rebuild()
    {
        if (!TryGetPlayfieldBounds(out float left, out float right, out float bottom, out float top,
                out float dividerX, out float slotTop, out float mainLeft, out float mainRight))
            return;

        ClearGenerated();

        Transform pegsRoot = CreateRoot("Pegs");
        Transform wallsRoot = CreateRoot("Walls");
        Transform slotsRoot = CreateRoot("Slots");

        BuildWalls(wallsRoot, left, right, bottom, top, dividerX, slotTop);
        BuildSlots(slotsRoot, mainLeft, mainRight, bottom, slotTop);
        BuildPegs(pegsRoot, mainLeft, mainRight, slotTop, top, dividerX);
    }

    [ContextMenu("Rebuild Pegs")]
    public void RebuildPegs()
    {
        if (!TryGetPlayfieldBounds(out _, out _, out _, out float top,
                out float dividerX, out float slotTop, out float mainLeft, out float mainRight))
            return;

        Transform pegsRoot = transform.Find("Pegs");
        if (pegsRoot == null)
            pegsRoot = CreateRoot("Pegs");
        else
            ClearChildren(pegsRoot);

        BuildPegs(pegsRoot, mainLeft, mainRight, slotTop, top, dividerX);
    }

    private bool TryGetPlayfieldBounds(
        out float left, out float right, out float bottom, out float top,
        out float dividerX, out float slotTop, out float mainLeft, out float mainRight)
    {
        RectTransform playfield = (RectTransform)transform;
        float width = playfield.rect.width;
        float height = playfield.rect.height;
        if (width < 10f || height < 10f)
        {
            left = right = bottom = top = dividerX = slotTop = mainLeft = mainRight = 0f;
            return false;
        }

        left = -width * 0.5f;
        right = width * 0.5f;
        bottom = -height * 0.5f;
        top = height * 0.5f;
        dividerX = right - launchChannelWidth;
        slotTop = bottom + slotHeight;
        mainLeft = left + 18f;
        mainRight = dividerX - 16f;
        return true;
    }

    private void ClearGenerated()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);
    }

    private static void ClearChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
            DestroyImmediate(root.GetChild(i).gameObject);
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
        var positions = new List<Vector2>();
        var radii = new List<float>();
        float cx = (mainLeft + mainRight) * 0.5f;
        float topRowY = top - 108f;
        Vector2 character = new Vector2(cx, slotTop + 78f);

        // 1. Top barrier: large screw-head pegs in a slight downward arc.
        int topCount = 7;
        for (int i = 0; i < topCount; i++)
        {
            float t = i / (float)(topCount - 1);
            float x = Mathf.Lerp(mainLeft + 38f, mainRight - 38f, t);
            float y = topRowY - 18f * Mathf.Sin(Mathf.PI * t);
            TryAddPeg(positions, radii, new Vector2(x, y), dividerX, largePegRadius);
        }

        // 2. Horizontal row under the title / 站 area.
        AddPegRow(positions, radii, 8, topRowY - 66f, mainLeft + 42f, mainRight - 42f, dividerX, pegRadius);

        // 3. Side funnel rails: diagonal lines that guide marbles inward.
        int sideCount = 7;
        for (int i = 0; i < sideCount; i++)
        {
            float t = i / (float)(sideCount - 1);
            float y = Mathf.Lerp(topRowY - 24f, slotTop + 96f, t);
            float inward = Mathf.Lerp(12f, 62f, t);
            TryAddPeg(positions, radii, new Vector2(mainLeft + inward, y), dividerX, pegRadius);
            TryAddPeg(positions, radii, new Vector2(mainRight - inward, y), dividerX, pegRadius);
        }

        // 4. Lower staggered Galton grid (photo's main bounce field).
        float gridTop = topRowY - 128f;
        float gridBottom = slotTop + 118f;
        int rows = 6;
        for (int row = 0; row < rows; row++)
        {
            bool offset = row % 2 == 1;
            int cols = offset ? 8 : 9;
            float y = Mathf.Lerp(gridTop, gridBottom, row / (float)(rows - 1));
            float inset = offset ? 56f : 32f;
            for (int col = 0; col < cols; col++)
            {
                float t = cols == 1 ? 0.5f : col / (float)(cols - 1);
                var pos = new Vector2(Mathf.Lerp(mainLeft + inset, mainRight - inset, t), y);
                if ((pos - character).sqrMagnitude < 58f * 58f)
                    continue;
                TryAddPeg(positions, radii, pos, dividerX, pegRadius);
            }
        }

        // 5. Pegs around the bottom character (ears + crown).
        TryAddPeg(positions, radii, character + new Vector2(-34f, 16f), dividerX, pegRadius);
        TryAddPeg(positions, radii, character + new Vector2(34f, 16f), dividerX, pegRadius);
        TryAddPeg(positions, radii, character + new Vector2(0f, 26f), dividerX, pegRadius);
        TryAddPeg(positions, radii, character + new Vector2(-20f, -10f), dividerX, pegRadius);
        TryAddPeg(positions, radii, character + new Vector2(20f, -10f), dividerX, pegRadius);

        // 6. Feeder row just above the score slots.
        AddPegRow(positions, radii, 9, slotTop + 28f, mainLeft + 26f, mainRight - 26f, dividerX, pegRadius);

        for (int i = 0; i < positions.Count; i++)
            CreatePeg($"Peg_{i}", root, positions[i], radii[i]);
    }

    private void AddPegRow(
        List<Vector2> positions, List<float> radii, int count, float y,
        float startX, float endX, float dividerX, float radius)
    {
        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0.5f : i / (float)(count - 1);
            TryAddPeg(positions, radii, new Vector2(Mathf.Lerp(startX, endX, t), y), dividerX, radius);
        }
    }

    private void TryAddPeg(List<Vector2> positions, List<float> radii, Vector2 pos, float dividerX, float radius)
    {
        if (pos.x > dividerX - radius * 2f)
            return;

        float minDist = pegSpacingX * 0.55f;
        float minDistSq = minDist * minDist;
        for (int i = 0; i < positions.Count; i++)
        {
            if ((positions[i] - pos).sqrMagnitude < minDistSq)
                return;
        }

        positions.Add(pos);
        radii.Add(radius);
    }

    private void CreatePeg(string name, Transform parent, Vector2 position, float radius)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = Vector2.one * (radius * 2f);

        if (showPegVisuals)
        {
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            bool large = radius > pegRadius + 0.5f;
            Sprite sprite = large ? largePegSprite : smallPegSprite;
            image.sprite = sprite != null
                ? sprite
                : Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
            image.color = sprite != null
                ? Color.white
                : (large
                    ? new Color(0.92f, 0.92f, 0.95f, 1f)
                    : new Color(0.78f, 0.80f, 0.84f, 0.95f));
        }

        var collider = go.AddComponent<CircleCollider2D>();
        collider.radius = radius;
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
