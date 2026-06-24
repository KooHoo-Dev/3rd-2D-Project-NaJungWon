using UnityEngine;
using UnityEngine.InputSystem;

namespace Study.Utilities
{
    public class SimpleFollowCameraTester : MonoBehaviour
    {
        private SimpleFollowCamera followCam;

        private void Awake()
        {
            followCam = GetComponent<SimpleFollowCamera>();
        }

        private void Update()
        {
            if(SimpleInput.GetKeyDown(Key.F1))
            {
                followCam.ChangeState(SimpleFollowCamera.States.Stopped);
            }
            if (SimpleInput.GetKeyDown(Key.F2))
            {
                followCam.ChangeState(SimpleFollowCamera.States.Holding);
            }
            if (SimpleInput.GetKeyDown(Key.F3))
            {
                followCam.ChangeState(SimpleFollowCamera.States.Following);
            }
        }
    }
}