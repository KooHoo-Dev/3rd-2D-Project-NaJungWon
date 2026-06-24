using UnityEditor;
using UnityEngine;

namespace Study_ActionPlatformer
{
    public class PlayerStat : BaseStat
    {

    }

    // 스탯, 전투 관련 기능을 넣어놓을 겁니다. 그리고 어떤 개체에서든 Player를
    // 찾을 수 있는 기능을 만들겁니다.
    // CombatEntity를 상속 받았기 때문에 "전투에 참여하는 개체"의
    // 공통부(Pivot, TakeDamage 등 순수가상함수 계약)는
    // 물려 받고, 플레이어의 고유의 것만 여기 남습니다.

    public class Player : CombatEntity
    {
        public static Player LocalPlayer { get; set; }

        public override BaseStat BaseStat => Stat;
        
        private PlayerStat Stat { get; set; }

        public AttackInfo attackInfo;

        private void Awake()
        {
            LocalPlayer = this;
        }

        public override void TakeDamage(int damage)
        {
            
        }

        public override void TakeHeal(int heal)
        {
            
        }
    }

}

