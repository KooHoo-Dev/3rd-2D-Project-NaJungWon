using Study.Utilities;
using Study_ActionPlatformer;
using UnityEngine;

public class DamagePopupFeedback : MonoBehaviour, ICombatObserver
{
    [field: SerializeField] public FloatingText PopupText { get; private set; }
    [field: SerializeField] public Color DamageColor { get; private set; }
    [field: SerializeField] public Color CriticalColor { get; private set; }

    public int CriticalPoint = 260;

    private ComponentPool<FloatingText> Pool { get; set; }

    private void Awake()
    {
        Pool = new ComponentPool<FloatingText>(PopupText, transform);
    }

    private void OnEnable()
    {
        CombatSystem.Instance.Subscribe(this);
    }

    private void OnDisable()
    {
        CombatSystem.Instance.UnSubscribe(this);
    }

    public void OnDamageTaken(CombatEntity sender, CombatEntity receiver, CombatEvent @event)
    {
        FloatingText textItem = Pool.Get();

        Color color = DamageColor;

        if (@event.Amount > CriticalPoint) color = CriticalColor;
        textItem.Show($"{@event.Amount}", color, receiver.HeadUpPivot.position);
    }

    public void OnHealTaken(CombatEntity sender, CombatEntity receiver, CombatEvent @event)
    {
        
    }
}
