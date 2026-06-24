using Study.Utilities;
using UnityEngine;

namespace Study_2D_DirectionAndRotation
{
    public class Study_WorldMousePoint : MonoBehaviour
    {
        private void Update()
        {
            // 화면의 중점을 왼쪽 하단에서 화면 중앙 좌표(카메라 좌표계)로 전환 합니다.
            Vector2 mousePosition = SimpleInput.GetMousePosition();

            // 화면 좌표계에서 World(Scene)의 좌표계로 전환합니다.
            // 전환할때는 Camera의 기능을 이용합니다.

            Vector3 worldMousePoint = Camera.main.ScreenToWorldPoint(mousePosition);
            worldMousePoint.z = transform.position.z; //z값 보정해줍니다.

            // 전환된 위치를 대입합니다.
            transform.position = worldMousePoint;
        }
    }

}
