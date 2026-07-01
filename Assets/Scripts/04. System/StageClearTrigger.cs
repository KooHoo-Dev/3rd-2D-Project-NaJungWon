using UnityEngine;

namespace Study_ActionPlatformer
{
    public class StageClearTrigger : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
            gameObject.tag = "ClearTrigger"; // 태그 설정 확실히
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.gameObject == Player.LocalPlayer.gameObject)
            {
                Debug.Log("[StageClearTrigger] 플레이어 클리어!");
                GameManager.Instance?.ClearStage();
                gameObject.SetActive(false); // 중복 실행 방지
            }
        }
    }
}