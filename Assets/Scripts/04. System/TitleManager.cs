using UnityEngine;
using UnityEngine.SceneManagement;
using Study.Utilities;

namespace Study_ActionPlatformer
{
    public class TitleManager : MonoBehaviour
    {
        [Header("이동할 첫 스테이지")]
        [SerializeField] private string firstStageName = "Stage_1";

        public void OnClickStart()
        {
            SceneManager.LoadScene(firstStageName);
        }

        public void OnClickQuit()
        {
            #if UNITY_EDITOR
            // Application.Quit()은 유니티 에디터에서는 아무 동작도 하지 않습니다.
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }
    }
}
