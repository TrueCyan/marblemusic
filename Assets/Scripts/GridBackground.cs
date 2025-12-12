using UnityEngine;

/// <summary>
/// 모눈종이 스타일 배경 생성 - 카메라를 따라가며 무한 반복
/// </summary>
[ExecuteAlways]
public class GridBackground : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private float baseGridSize = 1f;
    [SerializeField] private Color backgroundColor = new Color(0.95f, 0.95f, 0.92f);
    [SerializeField] private Color gridColor = new Color(0.5f, 0.7f, 0.85f, 1f); // 더 진하게
    [SerializeField] private Color subGridColor = new Color(0.7f, 0.85f, 0.95f, 0.5f);
    [SerializeField] private float gridLineWidth = 0.03f; // 더 굵게
    [SerializeField] private float subGridLineWidth = 0.015f;
    [SerializeField] private float gridMultiplier = 4f; // 4칸마다 진한 선

    [Header("Settings")]
    [SerializeField] private int sortingOrder = -100;
    [SerializeField] private float zPosition = 10f; // 카메라 뒤쪽 거리
    [SerializeField] private float sizeMultiplier = 2f; // 화면보다 얼마나 크게

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Material material;
    private Camera mainCamera;

    private void OnEnable()
    {
        mainCamera = Camera.main;
        CreateBackground();
    }

    private void OnValidate()
    {
        if (Application.isPlaying || meshRenderer != null)
        {
            UpdateMaterial();
        }
    }

    private void LateUpdate()
    {
        FollowCamera();
    }

    private void FollowCamera()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        // 카메라 위치 따라가기
        Vector3 camPos = mainCamera.transform.position;
        transform.position = new Vector3(camPos.x, camPos.y, camPos.z + zPosition);

        // 카메라 뷰에 맞게 크기 조절
        float height = mainCamera.orthographicSize * 2f * sizeMultiplier;
        float width = height * mainCamera.aspect;

        // 메시 크기 업데이트
        UpdateMeshSize(width, height);

        // 셰이더에 현재 orthoSize 전달
        if (material != null)
        {
            material.SetFloat("_OrthoSize", mainCamera.orthographicSize);
        }
    }

    private void CreateBackground()
    {
        // Get or create components
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
            meshFilter = gameObject.AddComponent<MeshFilter>();

        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            meshRenderer = gameObject.AddComponent<MeshRenderer>();

        // Create quad mesh with initial size
        Mesh mesh = new Mesh();
        mesh.name = "GridBackground";
        mesh.MarkDynamic(); // 동적 업데이트 최적화

        float initialSize = 100f;
        Vector3[] vertices = new Vector3[4]
        {
            new Vector3(-initialSize / 2, -initialSize / 2, 0),
            new Vector3(initialSize / 2, -initialSize / 2, 0),
            new Vector3(-initialSize / 2, initialSize / 2, 0),
            new Vector3(initialSize / 2, initialSize / 2, 0)
        };

        int[] triangles = new int[6] { 0, 2, 1, 2, 3, 1 };

        Vector2[] uvs = new Vector2[4]
        {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(1, 1)
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();

        meshFilter.sharedMesh = mesh;

        // Create material
        CreateMaterial();

        meshRenderer.sortingOrder = sortingOrder;
    }

    private void UpdateMeshSize(float width, float height)
    {
        if (meshFilter == null || meshFilter.sharedMesh == null) return;

        Mesh mesh = meshFilter.sharedMesh;
        Vector3[] vertices = new Vector3[4]
        {
            new Vector3(-width / 2, -height / 2, 0),
            new Vector3(width / 2, -height / 2, 0),
            new Vector3(-width / 2, height / 2, 0),
            new Vector3(width / 2, height / 2, 0)
        };

        mesh.vertices = vertices;
        mesh.RecalculateBounds();
    }

    private void CreateMaterial()
    {
        Shader shader = Shader.Find("Custom/GridBackground");
        if (shader == null)
        {
            // Fallback to unlit color if custom shader not found
            shader = Shader.Find("Sprites/Default");
        }

        material = new Material(shader);
        UpdateMaterial();
        meshRenderer.material = material;
    }

    private void UpdateMaterial()
    {
        if (material == null) return;

        material.SetColor("_BackgroundColor", backgroundColor);
        material.SetColor("_GridColor", gridColor);
        material.SetColor("_SubGridColor", subGridColor);
        material.SetFloat("_BaseGridSize", baseGridSize);
        material.SetFloat("_GridLineWidth", gridLineWidth);
        material.SetFloat("_SubGridLineWidth", subGridLineWidth);
        material.SetFloat("_GridMultiplier", gridMultiplier);

        // 현재 카메라 orthoSize 전달
        if (mainCamera != null)
        {
            material.SetFloat("_OrthoSize", mainCamera.orthographicSize);
        }
    }

    private void OnDestroy()
    {
        if (material != null)
        {
            if (Application.isPlaying)
                Destroy(material);
            else
                DestroyImmediate(material);
        }
    }
}
