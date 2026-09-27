using System;
using System.Collections.Generic;
using UnityEngine;

public class RobotArmController : MonoBehaviour
{
    [System.Serializable]
    
    public class Joint
    {
        public string label = "관절";
        public Transform pivot;
        public Vector3 axis = Vector3.left;
        
        public float minAngle = -90f;
        public float maxAngle = 90f;
        public float maxSpeed = 120f;
        public float startAngle = 0f;
        
        public float Angle { get; private set; }
        public float Velocity { get; private set; }

        public void Drive(float normalizedSpeed, float deltaTime)
        {
            // 입력을 "각도"가 아니라 "속도"로 받아서 조금씩 더해 나간다.
            // 그래야 한 프레임에 팔이 순간이동하듯 튀지 않는다.
            float beforeAngle = Angle;
            float deltaAngle = Mathf.Clamp(normalizedSpeed, -1f, 1f) * maxSpeed *  deltaTime;
          
            Angle = Mathf.Clamp(Angle + deltaAngle, minAngle, maxAngle);
            Velocity = deltaTime > 0f ? (Angle - beforeAngle) / deltaTime : 0f;
          
            Apply();
        }


        public void ResetTo(float angle)
        {
            Angle = Mathf.Clamp(angle, minAngle, maxAngle);
            Velocity = 0f;
            Apply();
        }
        
        void Apply()
        {
            if (pivot != null)
            {
                pivot.localRotation = Quaternion.AngleAxis(Angle, axis);
            }
        }
    }

    public Joint[] joints = new Joint[0];

    void Awake()
    {
        ResetPose();
    }

    public void ResetPose()
    {
        foreach (var j in joints)
        {
            j.ResetTo(j.startAngle);
        }
    }

    public void Drive(int index, float normalizedSpeed, float deltaTime)
    {
        float beforeAngle = joints[index].Angle;
        joints[index].Drive(normalizedSpeed, deltaTime);

        if (IsBlocked())
        {
            joints[index].ResetTo(beforeAngle);
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (RobotArmInput.ResetPressed) ResetPose();


        for (int i = 0; i < joints.Length; i++)
        {
            Drive(i, RobotArmInput.Joint(i), Time.fixedDeltaTime);
        }
    }


    public Transform tip;

    public float floorHeight = 0f;

    Transform[] _chain;
    Transform[] Chain
    {
        get
        {
            if (_chain != null && _chain.Length > 0) return _chain;

            var list = new List<Transform>();
            for (var t = tip; t != null && t != transform; t = t.parent)
                list.Add(t);
            list.Reverse();
            return _chain = list.ToArray();
        }
    }

    public float clearance = 0.07f;
    public float tipExemptDistance = 0.28f;
    public float obstacleRadius = 0.16f;
    public Transform[] obstacles = new Transform[0];

    public Vector3 TipPosition =>
        tip != null ? tip.position : transform.position;
    
    public bool IsBlocked()
    {

        var chain = Chain;
        float minY = floorHeight + clearance;
        Vector3 tipPos = TipPosition;

        int samplesPerLink = 6;
        
        for (int seg = 0; seg < chain.Length - 1; seg++)
        {
            Vector3 a = chain[seg].position;
            Vector3 b = chain[seg + 1].position;

            for (int s = 0; s <= samplesPerLink; s++)
            {
                Vector3 p =Vector3.Lerp(a, b, s/(float)samplesPerLink);

                if (ToLocal(p).y < minY)
                {
                    return true;
                }

                if (Vector3.Distance(p, tipPos) <= tipExemptDistance) continue;

                foreach (var o in obstacles)
                {
                    if (o == null) continue;
                    if (Vector3.Distance(p, o.position) <= obstacleRadius) return true;
                }
            }
        }
        
        return false;
    }

    public Vector3 ToLocal(Vector3 worldPosition) =>
        transform.InverseTransformPoint(worldPosition);
}
