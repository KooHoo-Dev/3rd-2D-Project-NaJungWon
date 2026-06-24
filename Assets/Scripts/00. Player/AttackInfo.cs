using UnityEngine;

namespace Study_ActionPlatformer
{
    public enum AttackKey
    {
        None = 0,
        Combo1,
        Combo2,
        Combo3,
        JumbAttack,
    }

    [System.Serializable]
    public struct AttackInfo
    {
        public AttackKey Key;
        public int MinDamage;
        public int MaxDamage;
        public AnimationCurve damageCurve;

        // 구조체(Struct)도 메서드를 가질 수 있다.
        // "이 데이터를 어떻게 해석(데미지 계산 공식)하는가?"에 대한 내용은
        // 데이터 곁에 두는것이 응집도가 좋습니다. 
        // 기능이 많아지만 분리(확장함수)하는게 좋지만,
        // 몇개 없으면 struct 내부에다가 구현해놓고 사용하는거 ㄱㅊ

        public int RollDamage()
        {
            // Curve에 t역할을 검색할 랜덤한 값을 추출함
            // ex) 0~99까지의 랜덤한 숫자를 뽑음
            float randomRoll = Random.Range(0f, 1f);

            // 커브를 통해 가중치(t) 평가 = .Evaluate()릴 이용해 Y축 값 추출
            float evaluation = damageCurve.Evaluate(randomRoll);

            float finalDamage = Mathf.Lerp(MinDamage, MaxDamage, evaluation);

            // Mathf.RoundToInt() : float를 반올림 하는 함수
            
            // Round : 반올림
            // Ceil : 올림
            // Floor : 버림(내림)
            // 접미사 ToInt() 붙는 함수의 경우 float로
            // 반환하는게 아니라 int로 반환을 합니다

            return Mathf.RoundToInt(finalDamage);
        }
    }

    
}