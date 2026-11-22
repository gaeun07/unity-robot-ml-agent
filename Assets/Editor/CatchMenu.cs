using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;

/// <summary>
/// Unity Editor 상단 메뉴에 'Catch' 항목 추가
/// Catch Scene을 자동으로 생성 (두 에이전트 학습용)
/// </summary>
public class CatchMenu
{
    // ==================== 설정 파라미터 ====================
    static float floorSize = 10f;         // 바닥 크기 (조금 더 키움)
    static float wallHeight = 1.5f;       // 벽 높이
    static float halfSize = floorSize / 2f; // 벽 위치 = 바닥 테두리

    [MenuItem("Catch/Create Scene")]
    static void CreateCatchScene()
    {
        // 새 씬 생성
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);

        // 카메라 설정
        SetupCamera();

        // 바닥 생성
        CreateFloor();

        // 벽 생성
        CreateWalls();

        // Runner 생성 (왼쪽)
        GameObject runner = CreateRunner(new Vector3(-4, 0.5f, 0));

        // Chaser 생성 (오른쪽)
        GameObject chaser = CreateChaser(new Vector3(4, 0.5f, 0));

        // 스크립트 변수 연결
        ConnectReferences(runner, chaser);

        // TagArea 생성
        CreateTagArea();

        // ScoreManager에 에이전트 연결
        ConnectScoreManager(runner, chaser);

        // 씬 저장
        SaveScene("Assets/Scenes/CatchScene.unity");

        // Score UI 생성
        ScoreUIHelper.CreateScoreUI();

        Debug.Log("✅ Catch Scene Created Successfully!");
        Debug.Log($"Floor size: {floorSize}x{floorSize}");
        Debug.Log($"Runner at: {runner.transform.position}");
        Debug.Log($"Chaser at: {chaser.transform.position}");
        Debug.Log("=== 조작 방법 ===");
        Debug.Log("Runner: WASD 또는 방향키");
        Debug.Log("Chaser: 자동 추격 (코드로 구현)");
        Debug.Log("=== 학습 전환 시 ===");
        Debug.Log("Runner/Chaser의 Manual Control 체크 해제 후 Behavior Type을 Default로 변경");
    }

    /// <summary>
    /// ScoreManager에 두 에이전트 연결 (점수 및 라운드 리셋 중앙 관리)
    /// </summary>
    static void ConnectScoreManager(GameObject runner, GameObject chaser)
    {
        ScoreManager manager = ScoreManager.Instance;

        // ScoreManager가 없으면 생성
        if (manager == null)
        {
            GameObject managerObj = new GameObject("ScoreManager");
            manager = managerObj.AddComponent<ScoreManager>();
            // Awake가 즉시 호출되지 않은 경우 대비
            manager.runner = runner.GetComponent<RunnerAgent>();
            manager.chaser = chaser.GetComponent<ChaserAgent>();
        }
        else
        {
            manager.runner = runner.GetComponent<RunnerAgent>();
            manager.chaser = chaser.GetComponent<ChaserAgent>();
        }

        Debug.Log($"ScoreManager connected: runner={manager.runner != null}, chaser={manager.chaser != null}");
    }

    static void SetupCamera()
    {
        float camDistance = floorSize * 1.2f;
        Camera.main.transform.position = new Vector3(0, camDistance * 0.6f, -camDistance * 0.8f);
        Camera.main.transform.rotation = Quaternion.Euler(30, 0, 0);
    }

    /// <summary>
    /// 오브젝트마다 고유한 재질 인스턴스를 생성하고 색상 적용
    /// (sharedMaterial.color 직접 수정 시 모든 오브젝트가 공유 재질이라 색이 섞임)
    /// </summary>
    static void ApplyColor(GameObject obj, Color color)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer == null) return;

        // 기존 재질을 복사하여 고유 인스턴스 생성
        Material mat = new Material(renderer.sharedMaterial);
        mat.color = color;
        renderer.sharedMaterial = mat;
    }

    static void CreateFloor()
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        float scale = floorSize / 10f;
        floor.transform.localScale = new Vector3(scale, 1f, scale);
        ApplyColor(floor, new Color(0.45f, 0.45f, 0.45f));
        Debug.Log($"Floor created: {floorSize}x{floorSize}");
    }

    static void CreateWalls()
    {
        // Front/Back: 가로(X축)
        CreateWall("Wall_Front", new Vector3(0, wallHeight / 2, halfSize), new Vector3(floorSize, wallHeight, 1));
        CreateWall("Wall_Back", new Vector3(0, wallHeight / 2, -halfSize), new Vector3(floorSize, wallHeight, 1));
        // Left/Right: 세로(Z축)
        CreateWall("Wall_Left", new Vector3(-halfSize, wallHeight / 2, 0), new Vector3(1, wallHeight, floorSize));
        CreateWall("Wall_Right", new Vector3(halfSize, wallHeight / 2, 0), new Vector3(1, wallHeight, floorSize));

        Debug.Log($"Walls created at ±{halfSize}");
    }

    static GameObject CreateWall(string name, Vector3 pos, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.position = pos;
        wall.transform.localScale = scale;
        ApplyColor(wall, new Color(0.6f, 0.5f, 0.35f));
        wall.AddComponent<BoxCollider>().isTrigger = false;
        return wall;
    }

    /// <summary>
    /// Behavior Parameters 설정
    /// 주의: Agent에 [RequireComponent(typeof(BehaviorParameters))]가 걸려 있어서
    ///       AddComponent<RunnerAgent>() 시 이미 자동 추가됨 → 추가 생성 금지
    /// </summary>
    static void SetupBehaviorParameters(GameObject obj, string behaviorName, bool heuristicOnly)
    {
        // 중복 제거 (안전장치)
        var duplicates = obj.GetComponents<BehaviorParameters>();
        for (int i = 1; i < duplicates.Length; i++)
        {
            UnityEngine.Object.DestroyImmediate(duplicates[i]);
        }

        // Agent의 RequireComponent로 이미 붙어있거나, 없으면 추가
        var bp = obj.GetComponent<BehaviorParameters>();
        if (bp == null)
        {
            bp = obj.AddComponent<BehaviorParameters>();
        }

        // Behavior 이름 (학습 시 yaml의 behavior 키와 일치해야 함)
        bp.BehaviorName = behaviorName;

        // Behavior 타입
        bp.BehaviorType = heuristicOnly ? BehaviorType.HeuristicOnly : BehaviorType.Default;

        // 관측 크기 = 6
        bp.BrainParameters.VectorObservationSize = 6;

        // 스택 없음
        bp.BrainParameters.NumStackedVectorObservations = 1;

        // 액션 스펙: 연속 2개 (x, z), 이산 없음
        bp.BrainParameters.ActionSpec = new ActionSpec(2, null);

        Debug.Log($"BehaviorParameters set on {obj.name}: " +
                  $"Name={bp.BehaviorName}, Obs={bp.BrainParameters.VectorObservationSize}, " +
                  $"Continuous={bp.BrainParameters.ActionSpec.NumContinuousActions}, " +
                  $"Discrete={bp.BrainParameters.ActionSpec.NumDiscreteActions}, " +
                  $"Type={bp.BehaviorType}, Count={obj.GetComponents<BehaviorParameters>().Length}");
    }

    static GameObject CreateRunner(Vector3 startPosition)
    {
        GameObject runner = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        runner.name = "Runner";
        runner.tag = "Untagged";
        runner.transform.position = startPosition;
        ApplyColor(runner, new Color(0.2f, 0.4f, 1f));

        var rb = runner.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.freezeRotation = true;

        runner.AddComponent<RunnerAgent>();
        // Runner는 학습 시 외부 트레이너와 통신해야 하므로 Default Behavior를 사용한다.
        SetupBehaviorParameters(runner, "Runner", false);

        // MaxStep: 1500 ≈ 30초 (fixedDeltaTime 0.02초 기준)
        // 15초 생존 보너트를 지급한 뒤에도 판이 자동 종료되지 않도록 여유를 둔다.
        runner.GetComponent<RunnerAgent>().MaxStep = 1500;

        var runnerDecisionRequester = runner.AddComponent<DecisionRequester>();
        runnerDecisionRequester.DecisionPeriod = 5;

        Debug.Log($"Runner created at {startPosition}");
        return runner;
    }

    static GameObject CreateChaser(Vector3 startPosition)
    {
        GameObject chaser = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        chaser.name = "Chaser";
        chaser.tag = "Untagged";
        chaser.transform.position = startPosition;
        ApplyColor(chaser, new Color(1f, 0.25f, 0.2f));

        var rb = chaser.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.freezeRotation = true;

        chaser.AddComponent<ChaserAgent>();
        SetupBehaviorParameters(chaser, "Chaser", true);

        // MaxStep: 1500 ≈ 30초 (fixedDeltaTime 0.02초 기준)
        // 15초 생존 보너트를 지급한 뒤에도 판이 자동 종료되지 않도록 여유를 둔다.
        chaser.GetComponent<ChaserAgent>().MaxStep = 1500;

        var chaserDecisionRequester = chaser.AddComponent<DecisionRequester>();
        chaserDecisionRequester.DecisionPeriod = 5;

        Debug.Log($"Chaser created at {startPosition}");
        return chaser;
    }

    static void ConnectReferences(GameObject runner, GameObject chaser)
    {
        var runnerAgent = runner.GetComponent<RunnerAgent>();
        if (runnerAgent != null)
        {
            runnerAgent.chaser = chaser.transform;
            Debug.Log("RunnerAgent.chaser connected");
        }

        var chaserAgent = chaser.GetComponent<ChaserAgent>();
        if (chaserAgent != null)
        {
            chaserAgent.target = runner.transform;
            Debug.Log("ChaserAgent.target connected");
        }
    }

    static void CreateTagArea()
    {
        GameObject tagArea = new GameObject("TagArea");
        tagArea.transform.position = Vector3.zero;
        tagArea.AddComponent<TagArea>();
        Debug.Log("TagArea created");
    }

    static void SaveScene(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), path);
        AssetDatabase.Refresh();
    }
}