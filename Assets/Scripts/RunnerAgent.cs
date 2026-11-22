using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

/// <summary>
/// 도주자 (Runner Agent)
/// - 수동 모드(manualControl=true): WASD 키로 직접 조작
/// - 학습 모드(manualControl=false): ML-Agents가 행동 결정
/// 관측: 술래 상대 위치, 내 속도, 술래 속도 (총 6개)
/// 보상: 생존 +0.001/스텝, 15초 버틸 때 +1.0, 잡히면 -1.0
/// </summary>
public class RunnerAgent : Agent
{
    [Header("참조")]
    public Transform chaser;

    [Header("설정")]
    public bool manualControl = true;    // true: WASD 직접 조작
    public float moveForce = 10f;         // 힘 (가속도)
    public float maxSpeed = 5f;           // 최대 속도 (터미널 속도)
    public float survivalTimeTarget = 15f;

    private Rigidbody rb;
    private float episodeStartedAt = 0f;
    private float survivalTimer = 0f;
    private bool hasEarnedSurvivalBonus = false;
    private bool hasReceivedCatchPenalty = false;

    protected override void Awake()
    {
        base.Awake();

        // Editor 창에 포커스가 없어도 학습이 멈추지 않도록 한다.
        Application.runInBackground = true;

        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.freezeRotation = true;
        rb.linearDamping = 2f;    // 드래그로 무한 가속 방지
        episodeStartedAt = Time.time;
        Debug.Log("RunnerAgent: Awake - Ready (manualControl=" + manualControl + ")");
    }

    /// <summary>
    /// 수동 조작 모드에서는 Update에서 직접 입력 처리
    /// </summary>
    void Update()
    {
        if (!manualControl) return;   // 학습 모드에서는 OnActionReceived가 처리

        // WASD 키 입력 (레거시 Input - ProjectSettings에서 Both로 설정됨)
        float h = 0f;
        float v = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) h -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) h += 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) v += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) v -= 1f;

        if (h != 0f || v != 0f)
        {
            Vector3 force = new Vector3(h * moveForce, 0f, v * moveForce);
            rb.AddForce(force, ForceMode.Acceleration);
        }

        // 최대 속도 제한
        ClampSpeed();

        // 생존 시간은 실제 경과 시간으로 계산한다.
        // Update와 FixedUpdate의 실행 순서 때문에 15초 보너트가 빗나가지 않게 한다.
        survivalTimer = Mathf.Max(0f, Time.time - episodeStartedAt);
        if (survivalTimer >= survivalTimeTarget)
        {
            CompleteSurvivalRound();
        }
    }

    /// <summary>
    /// 라운드 내부 상태 초기화 (ScoreManager에서 호출)
    /// 위치 배장은 ScoreManager가 단독으로 담당한다.
    /// </summary>
    public void ResetState()
    {
        episodeStartedAt = Time.time;
        survivalTimer = 0f;
        hasEarnedSurvivalBonus = false;
        hasReceivedCatchPenalty = false;

        if (rb != null)
            rb.linearVelocity = Vector3.zero;

        Debug.Log("[Runner] Episode state reset");
    }

    /// <summary>
    /// Runner가 15초를 버텼을 때 점수를 주고 새 판을 시작한다.
    /// Chaser가 잡았을 때와 동일한 리셋 경로를 사용한다.
    /// </summary>
    void CompleteSurvivalRound()
    {
        if (hasEarnedSurvivalBonus) return;
        hasEarnedSurvivalBonus = true;

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.AddRunnerScore(1);
            ScoreManager.Instance.StartNewRound();
        }

        Debug.Log($"[Runner] Survived {survivalTimeTarget}s! Score +1 - New round started");
    }

    /// <summary>
    /// Chaser가 Runner를 잡았을 때 종단 페널티를 한 번만 적용한다.
    /// ScoreManager가 에피소드를 종료하기 전에 호출해야 최종 보상이 기록된다.
    /// </summary>
    public void OnCaught()
    {
        if (hasReceivedCatchPenalty) return;
        hasReceivedCatchPenalty = true;

        AddReward(-1.0f);
        Debug.Log("[Runner] Caught! Reward -1");
    }

    /// <summary>
    /// 속도를 maxSpeed 이하로 제한 (무한 가속 방지)
    /// </summary>
    void ClampSpeed()
    {
        if (rb == null) return;
        Vector3 v = rb.linearVelocity;
        float speed = v.magnitude;
        if (speed > maxSpeed)
        {
            rb.linearVelocity = v.normalized * maxSpeed;
        }
    }

    // ==================== ML-Agents 학습용 ====================

    public override void Initialize()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.freezeRotation = true;
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        if (chaser == null) return;

        // 1-2: 술래 - 나 (x, z) / 10
        Vector3 relPos = chaser.position - transform.position;
        sensor.AddObservation(relPos.x / 10f);
        sensor.AddObservation(relPos.z / 10f);

        // 3-4: 내 속도 (x, z)
        sensor.AddObservation(rb.linearVelocity.x);
        sensor.AddObservation(rb.linearVelocity.z);

        // 5-6: 술래 속도 (x, z)
        Rigidbody chaserRb = chaser.GetComponent<Rigidbody>();
        if (chaserRb != null)
        {
            sensor.AddObservation(chaserRb.linearVelocity.x);
            sensor.AddObservation(chaserRb.linearVelocity.z);
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (manualControl) return;   // 수동 모드에서는 무시

        var continuousActions = actions.ContinuousActions;
        if (continuousActions.Length < 2)
        {
            Debug.LogWarning($"RunnerAgent: ContinuousActions length is {continuousActions.Length}, expected 2.");
            AddReward(0.001f);
            return;
        }

        Vector3 force = new Vector3(
            continuousActions[0] * moveForce,
            0f,
            continuousActions[1] * moveForce
        );
        rb.AddForce(force, ForceMode.Acceleration);

        // 최대 속도 제한
        ClampSpeed();

        // 생존 보상
        AddReward(0.001f);
        survivalTimer = Mathf.Max(0f, Time.time - episodeStartedAt);

        // 15초 버티면 보너스 + 새 판
        if (survivalTimer >= survivalTimeTarget)
        {
            AddReward(1.0f);
            CompleteSurvivalRound();
        }
    }

    public override void OnEpisodeBegin()
    {
        ResetState();
        if (chaser != null && ScoreManager.Instance != null && ScoreManager.Instance.chaser != null)
        {
            ScoreManager.Instance.chaser.ResetState();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        if (continuousActions.Length < 2) return;

        continuousActions[0] = Input.GetKey(KeyCode.A) ? -1f : Input.GetKey(KeyCode.D) ? 1f : 0f;
        continuousActions[1] = Input.GetKey(KeyCode.W) ? 1f : Input.GetKey(KeyCode.S) ? -1f : 0f;
    }
}