using System.Collections;
using UnityEngine;

namespace Study_ActionPlatformer
{
    public class DeadState : BossBaseState
    {
        [SerializeField] private float endTime = 5.0f;

        protected override void OnEnable()
        {
            StartCoroutine(Coroutine());
        }

        private IEnumerator Coroutine()
        {
            float waitTime = 0.0f;

            while(waitTime < endTime)
            {
                float currentAlphaValue = Mathf.Lerp(1.0f, 0.0f, waitTime / endTime);
                BossController.ChangeAlpha(currentAlphaValue);

                yield return null;
                waitTime += Time.deltaTime;
            }

            BossController.gameObject.SetActive(false);
        }
            

        protected override void OnDisable()
        {

        }
    }
}


