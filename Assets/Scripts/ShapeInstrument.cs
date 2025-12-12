using UnityEngine;

/// <summary>
/// 도형 타입 정의
/// </summary>
public enum ShapeType
{
    Circle,
    Triangle,
    Square,
    Rectangle,
    Pentagon,
    Hexagon,
    Star,
    Custom
}

/// <summary>
/// 도형 기반 악기 오브젝트 - 런타임에 도형 생성 및 콜라이더 자동 생성
/// </summary>
[RequireComponent(typeof(PolygonCollider2D))]
public class ShapeInstrument : InstrumentObject
{
    [Header("Shape Settings")]
    [SerializeField] private ShapeType shapeType = ShapeType.Circle;
    [SerializeField] private float size = 1f;
    [SerializeField] private Color shapeColor = Color.white;
    [SerializeField] private int circleSegments = 32; // 원의 세그먼트 수

    private PolygonCollider2D polygonCollider;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    protected override void Awake()
    {
        base.Awake();
        polygonCollider = GetComponent<PolygonCollider2D>();

        // SpriteRenderer가 없으면 Mesh 기반 렌더링 사용
        if (spriteRenderer == null)
        {
            SetupMeshRendering();
        }
    }

    private void Start()
    {
        GenerateShape();
    }

    private void SetupMeshRendering()
    {
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }

        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            // 기본 2D 스프라이트 머티리얼 사용
            meshRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }
    }

    /// <summary>
    /// 선택된 도형 타입에 따라 형태 생성
    /// </summary>
    public void GenerateShape()
    {
        Vector2[] points = GetShapePoints(shapeType);

        // 콜라이더 설정
        if (polygonCollider != null)
        {
            polygonCollider.SetPath(0, points);
        }

        // 메시 생성 (SpriteRenderer가 없는 경우)
        if (spriteRenderer == null && meshFilter != null)
        {
            Mesh mesh = CreateMeshFromPoints(points);
            meshFilter.mesh = mesh;

            if (meshRenderer != null)
            {
                meshRenderer.material.color = shapeColor;
            }
        }
    }

    private Vector2[] GetShapePoints(ShapeType type)
    {
        switch (type)
        {
            case ShapeType.Circle:
                return CreateCirclePoints(circleSegments);
            case ShapeType.Triangle:
                return CreatePolygonPoints(3);
            case ShapeType.Square:
                return CreateSquarePoints();
            case ShapeType.Rectangle:
                return CreateRectanglePoints(2f, 1f);
            case ShapeType.Pentagon:
                return CreatePolygonPoints(5);
            case ShapeType.Hexagon:
                return CreatePolygonPoints(6);
            case ShapeType.Star:
                return CreateStarPoints(5);
            default:
                return CreatePolygonPoints(4);
        }
    }

    private Vector2[] CreateCirclePoints(int segments)
    {
        Vector2[] points = new Vector2[segments];
        float angleStep = 360f / segments;

        for (int i = 0; i < segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            points[i] = new Vector2(
                Mathf.Cos(angle) * size * 0.5f,
                Mathf.Sin(angle) * size * 0.5f
            );
        }

        return points;
    }

    private Vector2[] CreatePolygonPoints(int sides)
    {
        Vector2[] points = new Vector2[sides];
        float angleStep = 360f / sides;
        float startAngle = 90f; // 위쪽부터 시작

        for (int i = 0; i < sides; i++)
        {
            float angle = (startAngle + i * angleStep) * Mathf.Deg2Rad;
            points[i] = new Vector2(
                Mathf.Cos(angle) * size * 0.5f,
                Mathf.Sin(angle) * size * 0.5f
            );
        }

        return points;
    }

    private Vector2[] CreateSquarePoints()
    {
        float half = size * 0.5f;
        return new Vector2[]
        {
            new Vector2(-half, -half),
            new Vector2(-half, half),
            new Vector2(half, half),
            new Vector2(half, -half)
        };
    }

    private Vector2[] CreateRectanglePoints(float widthRatio, float heightRatio)
    {
        float halfW = size * 0.5f * widthRatio / Mathf.Max(widthRatio, heightRatio);
        float halfH = size * 0.5f * heightRatio / Mathf.Max(widthRatio, heightRatio);

        return new Vector2[]
        {
            new Vector2(-halfW, -halfH),
            new Vector2(-halfW, halfH),
            new Vector2(halfW, halfH),
            new Vector2(halfW, -halfH)
        };
    }

    private Vector2[] CreateStarPoints(int points)
    {
        Vector2[] starPoints = new Vector2[points * 2];
        float outerRadius = size * 0.5f;
        float innerRadius = size * 0.2f;
        float angleStep = 360f / (points * 2);
        float startAngle = 90f;

        for (int i = 0; i < points * 2; i++)
        {
            float angle = (startAngle + i * angleStep) * Mathf.Deg2Rad;
            float radius = (i % 2 == 0) ? outerRadius : innerRadius;

            starPoints[i] = new Vector2(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius
            );
        }

        return starPoints;
    }

    private Mesh CreateMeshFromPoints(Vector2[] points)
    {
        Mesh mesh = new Mesh();

        // 버텍스 (중심점 + 외곽 포인트)
        Vector3[] vertices = new Vector3[points.Length + 1];
        vertices[0] = Vector3.zero; // 중심점

        for (int i = 0; i < points.Length; i++)
        {
            vertices[i + 1] = new Vector3(points[i].x, points[i].y, 0);
        }

        // 삼각형 인덱스 (팬 방식)
        int[] triangles = new int[points.Length * 3];
        for (int i = 0; i < points.Length; i++)
        {
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = (i + 1) % points.Length + 1;
        }

        // UV
        Vector2[] uvs = new Vector2[vertices.Length];
        uvs[0] = new Vector2(0.5f, 0.5f);
        for (int i = 0; i < points.Length; i++)
        {
            uvs[i + 1] = new Vector2(
                points[i].x / size + 0.5f,
                points[i].y / size + 0.5f
            );
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();

        return mesh;
    }

    /// <summary>
    /// 도형 타입 변경
    /// </summary>
    public void SetShapeType(ShapeType type)
    {
        shapeType = type;
        GenerateShape();
    }

    /// <summary>
    /// 도형 크기 변경
    /// </summary>
    public void SetSize(float newSize)
    {
        size = newSize;
        GenerateShape();
    }

    /// <summary>
    /// 도형 색상 변경
    /// </summary>
    public void SetShapeColor(Color color)
    {
        shapeColor = color;
        originalColor = color;

        if (meshRenderer != null)
        {
            meshRenderer.material.color = color;
        }
        else if (spriteRenderer != null)
        {
            spriteRenderer.color = color;
        }
    }

    /// <summary>
    /// 현재 도형 타입 반환
    /// </summary>
    public ShapeType GetShapeType()
    {
        return shapeType;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 에디터에서 값 변경 시 도형 재생성
        if (Application.isPlaying && polygonCollider != null)
        {
            GenerateShape();
        }
    }
#endif
}
