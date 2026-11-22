using UnityEngine;

/// <summary>
/// 점수 관리자 + 라운드 리셋 중앙 관리
/// - 두 에이전트의 점수와 상태를 여기서 연결
/// - 리셋 시 두 에이전트 간 최소 거리를 보장 (즉시 잡힘 방지)
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    public int runnerScore = 0;
    public int chaserScore = 0;

    // 에이전트 참조 (CatchMenu에서 연결)
    public RunnerAgent runner;
    public ChaserAgent chaser;

    [Header("라운드 설정")]
    public float arenaHalfSize = 4f;    // 에이전트 배치 범위 (±4)
    public float minSeparation = 4f;    // 두 에이전트 사이 최소 거리

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void AddRunnerScore(int amount)
    {
        runnerScore += amount;
        Debug.Log($"[Score] Runner Score: {runnerScore}");
    }

    public void AddChaserScore(int amount)
    {
        chaserScore += amount;
        Debug.Log($"[Score] Chaser Score: {chaserScore}");
    }

    /// <summary>
    /// 라운드 종료 후 두 에이전트 모두 초기화
    /// - 두 에이전트 사이에 최소 간격을 보장하여 즉시 잡힘 방지
    /// </summary>
    public void ResetEpisode()
    {
        // Runner 위치 먼저 결정
        Vector3 runnerPos = RandomPoint();

        // Chaser는 Runner와 최소 간격 이상 떨어진 곳
        Vector3 chaserPos = FindSeparatedPosition(runnerPos);

        if (runner != null)
        {
            Rigidbody rb = runner.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = runnerPos;
                rb.linearVelocity = Vector3.zero;
            }
            else
            {
                runner.transform.position = runnerPos;
            }
            runner.ResetState();
        }

        if (chaser != null)
        {
            Rigidbody rb = chaser.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.position = chaserPos;
                rb.linearVelocity = Vector3.zero;
            }
            else
            {
                chaser.transform.position = chaserPos;
            }
            chaser.ResetState();
        }

        Debug.Log($"[Round] Reset - Runner {runnerPos} / Chaser {chaserPos} (gap={Vector3.Distance(runnerPos, chaserPos):F2})");
    }

    /// <summary>
    /// Runner와 최소 간격을 유지하는 Chaser 위치 찾기.
    /// 무작위 탐색이 실패하면 Runner에서 가장 먼 모서리를 사용한다.
    /// </summary>
    Vector3 FindSeparatedPosition(Vector3 runnerPos)
    {
        const int maxAttempts = 100;
        Vector3 candidate = RandomPoint();

        for (int i = 0; i < maxAttempts; i++)
        {
            candidate = RandomPoint();
            if (Vector3.Distance(runnerPos, candidate) >= minSeparation)
                return candidate;
        }

        Vector3[] corners =
        {
            new Vector3(-arenaHalfSize, 0.5f, -arenaHalfSize),
            new Vector3(-arenaHalfSize, 0.5f,  arenaHalfSize),
            new Vector3( arenaHalfSize, 0.5f, -arenaHalfSize),
            new Vector3( arenaHalfSize, 0.5f,  arenaHalfSize)
        };

        Vector3 farthest = corners[0];
        float farthestDistance = -1f;
        foreach (Vector3 corner in corners)
        {
            float distance = Vector3.Distance(runnerPos, corner);
            if (distance > farthestDistance)
            {
                farthest = corner;
                farthestDistance = distance;
            }
        }

        return farthest;
    }

    /// <summary>
    /// 점수를 받은 뒤 새 판을 시작한다.
    /// 위치/내부 상태를 초기화하고 두 에이전트의 에피소드를 함께 종료한다.
    /// </summary>
    public void StartNewRound()
    {
        ResetEpisode();
        EndEpisodeForBoth();
    }

    /// <summary>
    /// 두 에이전트가 같은 판을 동시에 종료하도록 한다.
    /// </summary>
    public void EndEpisodeForBoth()
    {
        if (runner != null) runner.EndEpisode();
        if (chaser != null) chaser.EndEpisode();
    }

    /// <summary>
    /// 무작위 위치 (바닥 위)
    /// </summary>
    Vector3 RandomPoint()
    {
        return new Vector3(
            Random.Range(-arenaHalfSize, arenaHalfSize),
            0.5f,
            Random.Range(-arenaHalfSize, arenaHalfSize)
        );
    }

    public void ResetScores()
    {
        runnerScore = 0;
        chaserScore = 0;
        Debug.Log("[Score] Scores reset");
    }
}