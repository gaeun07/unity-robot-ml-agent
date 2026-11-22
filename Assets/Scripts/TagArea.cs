using UnityEngine;

/// <summary>
/// 충돌 감지 영역 (디버그용)
/// 태그 사용 안 함 - 이름 기반으로 확인
/// </summary>
public class TagArea : MonoBehaviour
{
    void OnCollisionEnter(Collision collision)
    {
        GameObject obj = collision.gameObject;

        if (obj.name == "Runner" || obj.name == "Chaser")
        {
            Debug.Log($"TagArea: Collision detected! Object: {obj.name}");
        }
    }
}