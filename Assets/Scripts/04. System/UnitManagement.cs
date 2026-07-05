using System.Collections.Generic;
using UnityEngine;

namespace Study_ActionPlatformer
{
    public class UnitManagement : MonoBehaviour
    {
        public static UnitManagement Instance { get; private set; }

        [SerializeField] private int totalEnemyCount;
        [SerializeField] private int deadEnemyCount;

        [field: SerializeField] private Player player;
        [field:SerializeField] private PlayerController playerController;

        private Enemy[] enemies;

        public Queue<Bullet> EnemyBullets = new Queue<Bullet>();
        public bool stageClear => totalEnemyCount > 0 && deadEnemyCount >= totalEnemyCount;

        private void Awake()
        {
            Instance = this;
            enemies = GetComponentsInChildren<Enemy>(true);
            playerController = player.GetComponent<PlayerController>();
            totalEnemyCount = enemies.Length;
            deadEnemyCount = 0;

            Debug.Log($"[UnitManagement] 초기화 - 총 적 수: {totalEnemyCount}");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void ReportEnemyDeath()
        {
            deadEnemyCount++;
            Debug.Log($"[UnitManagement] 적 사망: {deadEnemyCount}/{totalEnemyCount}");

            // 이벤트 대신 GameManager 직접 호출
            if (stageClear) GameManager.Instance?.OnAllEnemiesDead();
        }

        public void ResetUnit()
        {
            // [수정] 플레이어 HP와 컨트롤 상태를 함께 초기화합니다.
            Player.LocalPlayer.ResetState();
            playerController.ResetPlayer();

            /*
            foreach(Bullet bullet in EnemyBullets)
            {
                Destroy(bullet.gameObject);
            }
            EnemyBullets.Clear();
            */

            deadEnemyCount = 0;
            foreach (var enemy in enemies)
            {
                if (enemy != null)
                    enemy.ResetState();
            }
            Debug.Log("[UnitManagement] 적 상태 초기화 완료");
        }
    }
}