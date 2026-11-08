using UnityEngine;

namespace Humanoid
{
    // 휴머노이드를 위한 3인칭 카메라 컴포넌트
    // 히어로무오이드 캐릭터가 움직일 때 부드럽게 따라가며, 플레이어 입장에서 편안한 시점을 제공합니다.
    public class HumanoidCamera : MonoBehaviour
    {
        [Header("타겟 설정")]
        [Tooltip("추적할 휴머노이드 트랜스폼 (없으면 이 컴포넌트가 부착된 오브젝트의 부모)")]
        public Transform target;

        [Tooltip("휴머노이드 pelvis 트랜스폼. 타겟이 없으면 자동으로 자식에서 탐색합니다.")]
        public Transform pelvis;

        [Tooltip("카메라 피벗 오브젝트 (자동 생성됨). 카메라가 휴머노이드 주변을 도는 기준점이 됩니다.")]
        public Transform pivot;

        [Tooltip("카메라와 휴머노이드 사이의 거리 (양의 값)")]
        public float distance = 3f;

        [Tooltip("카메라가 휴머노이드 위쪽에서 보는 각도 (90=직시, 0=평면, -90=아래 looking)")]
        public float pitch = 15f;

        [Tooltip("카메라가 휴머노이드 주변을 도는 각도 (0=정면, 180=뒤)")]
        public float yaw = 180f;

        [Tooltip("위치 보간 속도 (초당) - 값이 클수록 즉시 따라옴)")]
        public float positionDampSpeed = 5f;

        [Tooltip("회전 보간 속도 (초당) - 값이 클수록 즉시 회전)")]
        public float rotationDampSpeed = 5f;

        [Tooltip("상하 회전 각도 제한 (피치)")]
        public float minPitch = -20f;

        [Tooltip("상하 회전 각도 제한 (피치)")]
        public float maxPitch = 80f;

        [Tooltip("좌우 회전 각도 제한 (요aw)")]
        public float minYaw = 0f;

        [Tooltip("좌우 회전 각도 제한 (요aw)")]
        public float maxYaw = 360f;

        [Tooltip("휴머노이드 눈높이 높이 (캐릭터 중심을 보정할 y축 높이)")]
        public float heightOffset = 1.5f;

        [Tooltip("최소 카메라 거리 (너무 가까이 접근하지 않음)")]
        public float minDistance = 2f;

        [Tooltip("최대 카메라 거리 (너무 멀어지지 않음)")]
        public float maxDistance = 15f;

        // 내부 상태
        private Transform _humanoidTransform;
        private Transform _cameraPivot;
        private Camera _camera;

        // 피벗이 생성되지 않으면 자동 생성
        void OnEnable()
        {
            // 타겟 설정: pelvis가 지정되어 있으면 pelvis 사용, 없으면 기존 로직
            if (pelvis != null)
            {
                target = pelvis;
            }

            _humanoidTransform = target ?? (transform.parent != null ? transform.parent : transform);
            _camera = GetComponent<Camera>();

            // 피벗 트랜스폼 설정 또는 생성
            if (pivot == null)
            {
                // 기존 피벗 찾거나 생성
                _cameraPivot = transform.Find("CameraPivot");
                if (_cameraPivot == null)
                {
                    var go = new GameObject("CameraPivot");
                    go.transform.parent = transform;
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localRotation = Quaternion.identity;
                    _cameraPivot = go.transform;
                }
            }
            else
            {
                _cameraPivot = pivot;
                // 피벗의 부모가 카메라가 아니면 설정
                if (_cameraPivot.parent != transform)
                {
                    _cameraPivot.parent = transform;
                }
            }

            // 초기 각도 보정
            ResetAngles();
        }

        void ResetAngles()
        {
            // 기본값: 휴머노이드 뒤에서 slightly 위를 바라보는 각도
            if (pivot != null)
            {
                pivot.localEulerAngles = new Vector3(pitch, yaw, 0f);
            }
        }

        void LateUpdate()
        {
            if (_humanoidTransform == null) return;

            // 1. 피벗 위치 업데이트 (휴머노이드 위치 + 높이 보정)
            // 피봉은 휴머노이드 중심 약간 위에 위치
            if (_cameraPivot != null)
            {
                _cameraPivot.position = _humanoidTransform.position + Vector3.up * heightOffset;
            }

            // 2. 피벗 회전 적용 (yaw: 좌우, pitch: 상하)
            // yaw는 휴머노이드 기준 직전 뒤(-z)를 0으로 간주하고 회전
            if (_cameraPivot != null)
            {
                // 현재 피벗 로테이션 유지하면서 목표 각도 적용
                Quaternion targetPivotRot = Quaternion.Euler(pitch, yaw, 0f);
                _cameraPivot.localRotation = Quaternion.Slerp(
                    _cameraPivot.localRotation,
                    targetPivotRot,
                    Time.deltaTime * rotationDampSpeed
                );
            }

            // 3. 카메라 위치 계산 (피벗을 중심으로 거리만큼 뒤로)
            // 카메라는 피벗 뒤에서 distance만큼 떨어져 있어야 함
            Vector3 lookDir = new Vector3(0f, 0f, -1f);
            // 피벗의 회전에 따라 방향 변환
            Quaternion pivotRot = _cameraPivot.localRotation;
            Vector3 cameraOffset = pivotRot * (Vector3.back * distance);

            // 거리 클램프
            distance = Mathf.Clamp(distance, minDistance, maxDistance);
            cameraOffset = pivotRot * (Vector3.back * distance);

            // 카메라 위치 계산: 피벗 위치 - offset
            Vector3 desiredPos = _cameraPivot.position + cameraOffset;

            // 위치 보간 (스무스하게 따라오기)
            transform.position = Vector3.Lerp(
                transform.position,
                desiredPos,
                Time.deltaTime * positionDampSpeed
            );

            // 4. 휴머노이드를 바라보도록 카메라 회전
            // LookAt 대신 Slerp로 부드럽게
            Vector3 lookAtPoint = _humanoidTransform.position;
            lookAtPoint.y = transform.position.y; // 카메라 높이에 맞춰 y축 보정

            Vector3 dirToTarget = (lookAtPoint - transform.position).normalized;
            if (dirToTarget.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dirToTarget);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRot,
                    Time.deltaTime * rotationDampSpeed
                );
            }

            // 5. 롤 유지 (카메라가 옆으로 쓰러지지 않음)
            // x축 회전 강제 제한 (0도 유지)
            Vector3 euler = transform.eulerAngles;
            euler.z = 0f;
            transform.eulerAngles = euler;
        }

        // 편의 함수: 카메라 위치를 현재 휴머노이드 포지션에 맞게 재설정
        public void ResetCameraPosition()
        {
            if (_humanoidTransform != null)
            {
                // 거리와 각도 초기화
                distance = 3f;
                pitch = 15f;
                yaw = 180f;
                heightOffset = 1.5f;

                // 피벗 위치 재설정
                if (_cameraPivot != null)
                {
                    _cameraPivot.position = _humanoidTransform.position + Vector3.up * heightOffset;
                    _cameraPivot.localEulerAngles = new Vector3(pitch, yaw, 0f);
                }

                // 카메라 위치 즉시 업데이트
                transform.position = _humanoidTransform.position - Vector3.back * distance + Vector3.up * heightOffset;
                transform.LookAt(_humanoidTransform.position + Vector3.up * 0.9f);
            }
        }

        // 인스펙터에서 즉시 카메라 위치 업데이트
        public void UpdateCameraPosition()
        {
            if (_humanoidTransform == null) return;

            // 피벗 위치 업데이트
            if (_cameraPivot != null)
            {
                _cameraPivot.position = _humanoidTransform.position + Vector3.up * heightOffset;
                Quaternion targetRot = Quaternion.Euler(pitch, yaw, 0f);
                _cameraPivot.localRotation = targetRot;
            }

            // 카메라 위치 계산
            Vector3 cameraOffset = Quaternion.Euler(pitch, yaw, 0f) * (Vector3.back * distance);
            transform.position = _humanoidTransform.position + cameraOffset;

            // Look at humanoid
            transform.LookAt(_humanoidTransform.position + Vector3.up * 0.9f);
        }
    }
}