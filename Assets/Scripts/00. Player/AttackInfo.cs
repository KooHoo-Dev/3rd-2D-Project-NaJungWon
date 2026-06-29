using UnityEngine;

namespace Study_ActionPlatformer
{
    public enum AttackKey
    {
        None = 0,
        Attack
    }

    [System.Serializable]
    public struct AttackInfo
    {
        public AttackKey Key;
        public int Damage;
    }
}