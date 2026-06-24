using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace Study_ActionPlatformer
{
    public class IdleState : BossBaseState
    {
        // 다음 공격 패턴을 결정하는 기능을 수행합니다.

        private List<BossPatternState> patterns = new List<BossPatternState>();

        public override void Initialize(BossController controller)
        {
            base.Initialize(controller);

            // 공격 패턴 객체들을 찾아 줍니다
            BossPatternState[] allPatterns =
                controller.GetComponentsInChildren<BossPatternState>(true);

            for(int i =0;  i < allPatterns.Length; i++)
                patterns.Add(allPatterns[i]);

        }

        protected override void OnEnable()
        {
            StartCoroutine(Coroutine());
        }

        protected override void OnDisable()
        {

        }

        private IEnumerator Coroutine()
        {
            Debug.Log("IdleState");
            yield return new WaitForSeconds(2.0f);

            int randNum = Random.Range(0, patterns.Count);
            BossPatternState nextPattern = patterns[randNum];
            BossController.ChangeState(nextPattern.GetType());
        }
    }
}


