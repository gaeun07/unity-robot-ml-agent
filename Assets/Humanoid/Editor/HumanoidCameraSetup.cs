using Humanoid;
using UnityEditor;
using UnityEngine;

namespace HumanoidEditor
{
    // 휴머노이드 카메라 설정 에디터 툴
    // 씬에서 카메라가 휴머노이드를 따라가도록 설정하는 기능을 제공합니다.
    public static class HumanoidCameraSetup
    {
        [MenuItem("Humanoid/카메라 설정 - 휴머노이드 팔로우 추가")]
        public static void AddHumanoidCameraComponent()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[Humanoid] 메인 카메라가 없어 카메라 설정을 건너뜁니다.");
                return;
            }

            // HumanoidCamera 컴포넌트가 이미 있으면 건너뜀
            var humanoidCam = cam.GetComponent<HumanoidCamera>();
            if (humanoidCam != null)
            {
                Debug.Log("[Humanoid] 카메라에 HumanoidCamera 컴포넌트가 이미 있습니다.");
                return;
            }

            // 컴포넌트 추가
            humanoidCam = cam.gameObject.AddComponent<HumanoidCamera>();

            // 타겟 설정: Main Camera의 부모 오브젝트 (보통 Humanoid)
            humanoidCam.target = cam.transform.parent;
            if (humanoidCam.target == null)
            {
                // 부모가 없으면 자체 오브젝트 사용
                humanoidCam.target = cam.transform;
            }

            // pelvis 타겟 자동 설정: Humanoid 오브젝트에서 Pelvis 찾기
            if (humanoidCam.target != null)
            {
                var pelvisObj = humanoidCam.target.transform.Find("Pelvis");
                if (pelvisObj != null)
                {
                    humanoidCam.pelvis = pelvisObj.transform;
                    humanoidCam.target = pelvisObj.transform;
                }
            }

            // 3인칭 카메라 기본 설정
            // 거리: 카메라가 휴머노이드에서 어느 정도 떨어져 있는지
            humanoidCam.distance = 3f;
            // 상하 각도: 휴머노이드를 약간 아래로 내려다보는 각도
            humanoidCam.pitch = 15f;
            // 좌우 각도: 휴머노이드의 뒤쪽을 바라보도록 (180도)
            humanoidCam.yaw = 180f;
            // 높이 오프셋: 휴머노이드 가슴/허리 높이 수준
            humanoidCam.heightOffset = 1.5f;

            // 보간 속도 기본값
            humanoidCam.positionDampSpeed = 5f;
            humanoidCam.rotationDampSpeed = 5f;

            // 각도 제한
            humanoidCam.minPitch = -20f;
            humanoidCam.maxPitch = 80f;
            humanoidCam.minYaw = 0f;
            humanoidCam.maxYaw = 360f;

            // 기존 카메라 위치 초기화 (PlaceCamera과 호환)
            cam.transform.position = new Vector3(2.4f, 1.5f, 3.2f);
            cam.transform.rotation = Quaternion.Euler(8.5f, 217f, 0f);

            Debug.Log("[Humanoid] 카메라에 HumanoidCamera 팔로우 컴포넌트가 추가되었습니다.");
            Debug.Log(" - 타겟: " + (humanoidCam.target ? humanoidCam.target.name : "없음"));
            Debug.Log(" - 거리: " + humanoidCam.distance);
            Debug.Log(" - 각도 (pitch, yaw): " + humanoidCam.pitch + ", " + humanoidCam.yaw);
            Debug.Log(" - 높이 오프셋: " + humanoidCam.heightOffset);
            Debug.Log(" - 플레이 모드에서 휴머노이드를 따라갑니다.");
        }

        [MenuItem("Humanoid/카메라 설정 - 현재 카메라 위치 재설정")]
        public static void ResetCameraPosition()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var humanoidCam = cam.GetComponent<HumanoidCamera>();
            if (humanoidCam != null)
            {
                humanoidCam.ResetCameraPosition();
                Debug.Log("[Humanoid] 카메라 위치가 현재 휴머노이드 포지션으로 재설정되었습니다.");
            }
            else
            {
                Debug.LogWarning("[Humanoid] HumanoidCamera 컴포넌트가 없습니다. Add Humanoid Camera Component 메뉴를 먼저 실행하세요.");
            }
        }

        [MenuItem("Humanoid/카메라 설정 - 타겟 오브젝트 지정")]
        public static void SetCameraTarget()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var humanoidCam = cam.GetComponent<HumanoidCamera>();
            if (humanoidCam == null)
            {
                Debug.LogWarning("[Humanoid] HumanoidCamera 컴포넌트가 없습니다.");
                return;
            }

            // 팝업을 통한 타겟 선택 (간단하게 Hierarchy에서 Humanoid 선택)
            Object[] targets = Selection.objects;
            string targetName = "Humanoid"; // 기본값

            if (targets.Length > 0)
            {
                targetName = targets[0].name;
            }

            humanoidCam.target = Selection.activeTransform;
            if (humanoidCam.target == null)
            {
                // Hierarchy에서 Humanoid 오브젝트를 찾으려 시도
                humanoidCam.target = GameObject.Find("Humanoid")?.transform;
            }

            Debug.Log($"[Humanoid] 카메라 타겟이 '{targetName}'으로 설정되었습니다.");
        }

        [MenuItem("Humanoid/카메라 설정 - 기본값 복원")]
        public static void RestoreDefaultSettings()
        {
            var cam = Camera.main;
            if (cam == null) return;

            var humanoidCam = cam.GetComponent<HumanoidCamera>();
            if (humanoidCam == null)
            {
                AddHumanoidCameraComponent();
                humanoidCam = cam.GetComponent<HumanoidCamera>();
            }

            // 기본값으로 복원
            humanoidCam.distance = 3f;
            humanoidCam.pitch = 15f;
            humanoidCam.yaw = 180f;
            humanoidCam.heightOffset = 1.5f;

            humanoidCam.positionDampSpeed = 5f;
            humanoidCam.rotationDampSpeed = 5f;

            humanoidCam.minPitch = -20f;
            humanoidCam.maxPitch = 80f;
            humanoidCam.minYaw = 0f;
            humanoidCam.maxYaw = 360f;

            // 카메라 위치 초기화
            cam.transform.position = new Vector3(2.4f, 1.5f, 3.2f);
            cam.transform.rotation = Quaternion.Euler(8.5f, 217f, 0f);

            // 타겟 재설정 (부모)
            humanoidCam.target = cam.transform.parent;

            Debug.Log("[Humanoid] 카메라 설정이 기본값으로 복원되었습니다.");
        }
    }
}