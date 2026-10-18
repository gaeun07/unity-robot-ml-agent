using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(Rigidbody))]

public class Gripper : MonoBehaviour
{
    [Header("연결")] 
    public Transform follow;
    public Transform yawReference;
    public Transform target;
    public HingeJoint fingerL;
    public HingeJoint fingerR;

    [Header("쥐는 힘")] 
    public float gripSpeed = 90f;
    public float gripForce = 4f;
    public float gripRange = 65f;
    public float gripDepth = 0.22f;

    [Header("쉬는 자세")] 
    public float fingerOffset = 0.17f;
    public float openAngle = 50f;

    const float ContactMargin = 0.01f;

    Rigidbody _body, _bodyL, _bodyR;
    BoxCollider[] _boxL, _boxR;
    Collider _targetCollider;
    readonly Collider[] _hits = new Collider[8];

    public Vector3 GripCenter => transform.position + transform.forward * gripDepth;
    
    public float GripValue => (Angle(fingerL, 1f) + Angle(fingerR, -1f)) * 0.5f;
    
    public bool TouchingL { get; private set; }
    public bool TouchingR { get; private set; }
    public bool BothTouching => TouchingL && TouchingR;

    float Angle(HingeJoint h, float sign)
    {
        if (h==null) return 0f;
        float t = h.angle * sign / Mathf.Max(1f, gripRange);
        return float.IsNaN(t) ? 0f : Mathf.Clamp01(t);
    }

    void Awake()
    {
        _body = GetComponent<Rigidbody>();
        _body.isKinematic = true;
        _body.useGravity = false;

        _bodyL = fingerL.GetComponent<Rigidbody>();
        _bodyR = fingerR.GetComponent<Rigidbody>();
        _boxL = fingerL.GetComponentsInChildren<BoxCollider>();
        _boxR = fingerR.GetComponentsInChildren<BoxCollider>();
        
        _targetCollider = target.GetComponent<Collider>();
    }

    void FixedUpdate()
    {
        if (follow == null) return;
        _body.MovePosition(follow.position);
        _body.MoveRotation(AimRotation(follow.position,yawReference.position));
    }

    public static Quaternion AimRotation(Vector3 tip, Vector3 basePos)
    {
        Vector3 flat = tip - basePos;
        flat.y = 0f;
        if (flat.sqrMagnitude < 1e-6f) flat = Vector3.forward;
        return Quaternion.LookRotation(Vector3.down,flat.normalized);
    }

    public void Drive(bool close)
    {
        Motor(fingerL, close ? 1f : -1f);
        Motor(fingerR, close ? -1f : 1f);
    }

    void Motor(HingeJoint h, float direction)
    {
        h.motor = new JointMotor()
        {
            targetVelocity = direction * gripSpeed,
            force = gripForce
        };
        h.useMotor = true;
    }

    public void UpdateContacts()
    {
        TouchingL = Overlaps(_boxL);
        TouchingR = Overlaps(_boxR);
    }

    bool Overlaps(BoxCollider[] boxes)
    {
        foreach (var box in boxes)
        {
            var t = box.transform;
            Vector3 half = Vector3.Scale(box.size, t.lossyScale) * 0.5f
                           + Vector3.one * ContactMargin;


            int n =
                Physics.OverlapBoxNonAlloc(t.TransformPoint(box.center), half,
                    _hits, t.rotation, ~0, QueryTriggerInteraction.Collide);


            for (int i = 0; i < n; i++)
                if (_hits[i] == _targetCollider) return true;
        }
        return false;

    }

    public void ResetPose()
    {
        transform.SetPositionAndRotation(follow.position,AimRotation(follow.position,yawReference.position));

        Restore(_bodyL, -fingerOffset, -openAngle);
        Restore(_bodyR, +fingerOffset, +openAngle);

        Drive(false);
        TouchingL = TouchingR = false;
        Physics.SyncTransforms();
    }

    void Restore(Rigidbody rb, float sideOffset, float angle)
    {
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.transform.SetPositionAndRotation(transform.TransformPoint(new Vector3(sideOffset,0f,0f)),
            transform.rotation*Quaternion.AngleAxis(angle,Vector3.up));
    }
}
