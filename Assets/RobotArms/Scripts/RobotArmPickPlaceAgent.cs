using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

public class RobotArmPickPlaceAgent : Agent
{
    [Header("연결")]
    public RobotArmController arm;
    public Transform targetObject;
    public Transform placeTarget;

    [Header("보상")] 
    public float grabReward = 0.5f;
    public float placeReward = 1.0f;
    public float dropPenalty = -0.3f;
    public float lostPenalty = -0.5f;

    const float GrabDistance = 0.25f;
    const float PlaceRadius = 0.25f;
    const float RestSpeed = 0.2f;
    const float MinSeparation = 0.6f;
    const float MaxStepReward = 0.05f;

    const float WorkMin = 1.00f;
    const float WorkMax = 2.80f;
    const float WorkYaw = 115f;

    bool _holding; bool _grabbed;

    Transform _objectHome;
    int _cycles;

    Rigidbody _objectBody;
    float _lastDistance;
    int _successCount;

    Vector3 CurrentGoal()
    {
        if (!_holding)
        {
            return targetObject.position;
        }
        
        Vector3 local = arm.ToLocal(placeTarget.position);
        local.y = arm.spawnHeight;
        return arm.transform.TransformPoint(local);
    }

    Vector3 Mover => _holding ? targetObject.position : arm.TipPosition;
    
    float DistanceToGoal() => Vector3.Distance(Mover, CurrentGoal());
    
    static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x-b.x,a.z-b.z).magnitude;
    
    bool OverPlaceTarget() => Flat(arm.ToLocal(targetObject.position),arm.ToLocal(placeTarget.position)) < PlaceRadius;

    void MovePlaceTarget(Vector3 objLocal)
    {
        Vector3 spot;
        int guard = 0;

        do
        {
            spot = RandomSpot();
        } while (Flat(objLocal, spot) < MinSeparation && ++guard < 50);

        spot.y = 0.01f;
        placeTarget.position=arm.transform.TransformPoint(spot);
        
      
    }
    Vector3 RandomSpot() => arm.SpawnPointLocal(
        Random.Range(arm.spawnDistanceRange.x, arm.spawnDistanceRange.y),
        Random.Range(arm.spawnYawRange.x, arm.spawnYawRange.y));

    void UpdateGrip(bool wantGrip)
    {
        if (!_holding && wantGrip)
        {
            if (Vector3.Distance(arm.TipPosition, targetObject.position) < GrabDistance)
                Grab();
        }
        else if (_holding && !wantGrip)
        {
            Release(true);
        }
    }
    void Grab()
    {
        _holding = true;

        _objectBody.isKinematic = true;
        targetObject.SetParent(arm.tip);

        if (!_grabbed) AddReward(grabReward);
        _grabbed = true;

        _lastDistance = DistanceToGoal();
    }

    void Release(bool penalty)
    {
        if (!_holding) return;

        _holding = false;

        targetObject.SetParent(_objectHome);
        _objectBody.isKinematic = false;
        _objectBody.linearVelocity = Vector3.zero;
        _objectBody.angularVelocity = Vector3.zero;


        if (penalty && !OverPlaceTarget()) AddReward(dropPenalty);

        _lastDistance = DistanceToGoal();
    }

    bool ObjectLost()
    {
        if (_holding) return false;
        
        Vector3 p = arm.ToLocal(targetObject.position);
        if (p.y < -0.3f) return true;
        
        float d = new Vector2(p.x,p.z).magnitude;
        if (d < WorkMin || d > WorkMax) return true;

        return Mathf.Abs(Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg) > WorkYaw;
    }

    public override void Initialize()
    {
        _objectBody = targetObject.GetComponent<Rigidbody>();
        _objectHome = targetObject.parent;
    }

    public override void OnEpisodeBegin()
    {
        _cycles = 0;
        
        Release(false);
        arm.ResetPose();
        
        Vector3 spot = RandomSpot();
        targetObject.SetPositionAndRotation(arm.transform.TransformPoint(spot),Quaternion.identity);
        _objectBody.linearVelocity = Vector3.zero;
        _objectBody.angularVelocity = Vector3.zero;
        
        MovePlaceTarget(spot);

        Physics.SyncTransforms();
        ResetCycle();
    }
    
    float Distance() => Vector3.Distance(arm.TipPosition, targetObject.position);
    
    void PlaceObject()
    {
        Vector3 spot = RandomSpot();
        
        targetObject.SetPositionAndRotation(arm.transform.TransformPoint(spot),Quaternion.identity);
        
        _objectBody.linearVelocity = Vector3.zero;
        _objectBody.angularVelocity = Vector3.zero;
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        for (int i = 0; i < arm.joints.Length; i++)
        {
            arm.Drive(i, actionBuffers.ContinuousActions[i], Time.fixedDeltaTime);
        }
        UpdateGrip(actionBuffers.DiscreteActions[0]==1);
        GiveReward();
    }

    void GiveReward()
    {
        if (ObjectLost())
        {
            AddReward(lostPenalty); 
            EndEpisode();
            return;
        }
        
        float distance = Distance();

        AddReward((_lastDistance - distance) / arm.reach);

        AddReward(-1f / MaxStep);
        _lastDistance = distance;

        if (_grabbed && !_holding && OverPlaceTarget() && _objectBody.linearVelocity.magnitude < RestSpeed)
        {
            AddReward(placeReward);
            _cycles++;
            
            MovePlaceTarget(arm.ToLocal(targetObject.position));
            ResetCycle();
        }
    }

    void ResetCycle()
    {
        _grabbed = false;
        _lastDistance = DistanceToGoal();
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        for (int i = 0; i < arm.joints.Length; i++)
        {
            sensor.AddObservation(arm.NormalizedAngle(i));
        }

        for (int i = 0; i < arm.joints.Length; i++)
        {
            sensor.AddObservation(arm.NormalizedVelocity(i));
        }
        
        sensor.AddObservation(arm.ToNormalizedLocal(arm.TipPosition));
        sensor.AddObservation(arm.ToNormalizedLocal(targetObject.position));
        
        sensor.AddObservation(_holding ? 1f : 0f);
        sensor.AddObservation(arm.ToNormalizedLocal(CurrentGoal()));
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var continuous=actionsOut.ContinuousActions;
        for (int i = 0; i < arm.joints.Length && i < continuous.Length; i++)
            continuous[i] = RobotArmInput.Joint(i);

        var discrete = actionsOut.DiscreteActions;
        discrete[0] = RobotArmInput.Grip ? 1 : 0;
    }
}
