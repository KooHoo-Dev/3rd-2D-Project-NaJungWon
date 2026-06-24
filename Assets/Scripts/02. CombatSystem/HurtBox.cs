using UnityEngine;

namespace Study_ActionPlatformer
{
    [RequireComponent(typeof(Collider2D))]
    public class HurtBox : MonoBehaviour
    {
        [field: SerializeField] public CombatEntity Owner { get; private set; }
        private Collider2D col2D;

        private void Awake()
        {
            Owner = GetComponent<CombatEntity>();
            if(Owner == null) Owner = GetComponentInParent<CombatEntity>();

            if (Owner == null)
            {
                Debug.LogError($"{name} : CombatEntity를 찾지 못했습니다.");
            }

            col2D = GetComponent<Collider2D>();
        }

        // 아래의 Start와 OnDestroy보다 OnEnable이랑 OnDisable

        private void Start()
        {
            CombatSystem.Instance.AddHurtBox(col2D, this);
        }

        private void OnDestroy()
        {
            CombatSystem.Instance.RemoveHurtBox(col2D);
        }
    }
}

