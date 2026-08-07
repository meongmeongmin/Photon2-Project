using UnityEngine;

public class Test : MonoBehaviour
{
    [SerializeField] TMPro.TMP_Text _text;
    float deltaTime = 0f;

    void Update()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        float fps = 1.0f / deltaTime;
        _text.text = $"FPS: {fps}";
    }
}
