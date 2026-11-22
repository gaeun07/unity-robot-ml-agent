using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

/// <summary>
/// 술래 (Chaser Agent)
/// - 수동 모드(manualControl=true): 코드로 Runner를 직접 추격
/// - 학습 모드(manualControl=false): ML-Agents가 행동 결정
/// 관측: Runner 상대 위치, 내 속도, Runner 속도 (총 6개)
/// 보상: 잡을 때 +1.0, 스텝당 시간 벌점 -0.001
/// </summary>
public class ChaserAgent : Agent
{
    [Header("참조")]
    public Transform target;  // Runner

    [Header("설정")]
    public bool manualControl = true;    // true: 코드로 직접 추격
    public float moveForce = 12f;         // 힘 (Runner의 1.2배)
    public float maxSpeed = 6f;           // 최대 속도 (Runner의 1.2배)
    public float catchDistance = 1f;       // 두 구의 반지름 합(1m)보다 가까우면 잡힘
    public float catchGraceTime = 0.75f;   // 라운드 시작 직후 잡힘 판정 유예 시간

    private Rigidbody rb;
    private int stepCount = 0;
    private float catchGraceUntil = 0f;

    protected override void Awake()
    {
        base.Awake();

        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.freezeRotation = true;
        rb.linearDamping = 2f;    // 드래그로 무한 가속 방지
        Debug.Log("ChaserAgent: Awake - Ready (manualControl=" + manualControl + ")");
    }

    /// <summary>
    /// 수동 추격 모드
    /// </summary>
    void Update()
    {
        if (!manualControl) return;   // 학습 모드에서는 OnActionReceived가 처리

        // 대상 없으면 이름으로 찾기
        if (target == null)
        {
            GameObject runnerObj = GameObject.Find("Runner");
            if (runnerObj != null)
            {
                target = runnerObj.transform;
                Debug.Log("[Chaser] Found Runner by name!");
            }
            return;
        }

        // 라운드 시작 직후 유예 시간에는 추격을 멈춰 Runner가 반응할 시간을 준다.
        if (IsCatchGraceActive())
        {
            ClampSpeed();
            return;
        }

        // Runner를 향해 이동
        Vector3 direction = (target.position - transform.position).normalized;
        rb.AddForce(direction * moveForce, ForceMode.Acceleration);

        // 최대 속도 제한
        ClampSpeed();

        // 실제 접촉(거리 1m 이하)해야 잡힘으로 인정한다.
        // 라운드 시작 직후에는 유예 시간이 끝날 때까지 잡힘으로 보지 않는다.
        float dist = Vector3.Distance(transform.position, target.position);
        if (dist <= catchDistance && !IsCatchGraceActive())
        {
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.AddChaserScore(1);
                ScoreManager.Instance.StartNewRound();
            }
            Debug.Log($"[Chaser] Caught Runner! Distance={dist:F2}");
        }
    }

    /// <summary>
    /// 라운드 내부 상태 초기화 (ScoreManager에서 호출)
    /// 위치 배장은 ScoreManager가 단독으로 담당한다.
    /// </summary>
    public void ResetState()
    {
        stepCount = 0;
        catchGraceUntil = Time.time + catchGraceTime;

        if (rb != null)
            rb.linearVelocity = Vector3.zero;

        Debug.Log($"[Chaser] Episode state reset (catch grace: {catchGraceTime:F2}s)");
    }

    /// <summary>
    /// 라운드 시작 직후의 유예 시간이 아직 끝나지 않았는지 확인
    /// </summary>
    bool IsCatchGraceActive()
    {
        return Time.time < catchGraceUntil;
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
        if (target == null) return;

        // 1-2: Runner - 나 (x, z) / 10
        Vector3 relPos = target.position - transform.position;
        sensor.AddObservation(relPos.x / 10f);
        sensor.AddObservation(relPos.z / 10f);

        // 3-4: 내 속도 (x, z)
        sensor.AddObservation(rb.linearVelocity.x);
        sensor.AddObservation(rb.linearVelocity.z);

        // 5-6: Runner 속도 (x, z)
        Rigidbody targetRb = target.GetComponent<Rigidbody>();
        if (targetRb != null)
        {
            sensor.AddObservation(targetRb.linearVelocity.x);
            sensor.AddObservation(targetRb.linearVelocity.z);
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

        stepCount++;

        // 라운드 시작 직후 유예 시간에는 행동하지 않고 대기한다.
        if (IsCatchGraceActive())
        {
            AddReward(-0.001f);
            return;
        }

        var continuousActions = actions.ContinuousActions;
        if (continuousActions.Length < 2)
        {
            Debug.LogWarning($"ChaserAgent: ContinuousActions length is {continuousActions.Length}, expected 2.");
            AddReward(-0.001f);
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

        // 시간 벌점
        AddReward(-0.001f);

        // 잡으면 보상 + 점수 (유예 시간 중에는 잡힘 판정하지 않음)
        if (target != null &&
            Vector3.Distance(transform.position, target.position) <= catchDistance &&
            !IsCatchGraceActive())
        {
            AddReward(1.0f);
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.AddChaserScore(1);
                ScoreManager.Instance.StartNewRound();
            }
        }
    }

    public override void OnEpisodeBegin()
    {
        ResetState();
        if (target != null && ScoreManager.Instance != null && ScoreManager.Instance.runner != null)
        {
            ScoreManager.Instance.runner.ResetState();
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuousActions = actionsOut.ContinuousActions;
        if (continuousActions.Length < 2) return;

        continuousActions[0] = Input.GetKey(KeyCode.LeftArrow) ? -1f : Input.GetKey(KeyCode.RightArrow) ? 1f : 0f;
        continuousActions[1] = Input.GetKey(KeyCode.UpArrow) ? 1f : Input.GetKey(KeyCode.DownArrow) ? -1f : 0f;
    }
}