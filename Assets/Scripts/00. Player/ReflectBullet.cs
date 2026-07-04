using Study_ActionPlatformer;
using UnityEngine;

public class ReflectBullet : MonoBehaviour
{
    private CombatEntity Owner { get; set; }
    public Animator reflectEffect;
    
    private void Awake()
    {
        Owner = GetComponentInParent<CombatEntity>();

        if (Owner == null)
        {
            Debug.LogError($"{name} : 부모 계층에서 CombatEntity를 찾지 못했습니다." +
                $"오브젝트를 삭제합니다");
            Destroy(gameObject);
        }
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if(other.GetComponent<Bullet>())
        {
            Bullet bullet = other.GetComponent<Bullet>();
            reflectEffect.transform.position = other.transform.position;
            reflectEffect.Play("Effect");
            bullet.ReflectBullet(Owner);
        }
    }
}
