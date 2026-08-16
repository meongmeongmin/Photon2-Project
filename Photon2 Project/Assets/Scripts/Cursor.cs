using UnityEngine;
using UnityEngine.InputSystem;

public class Cursor : MonoBehaviour
{
    private float _maxRange;
    private Transform _rangeCenter;
    private Transform _mouseFollower;
    private float _speed = 0.01f;

    private Vector2 _offset;

    /// <summary>
    /// 마우스가 게임 화면 안에 있는지 확인합니다.
    /// </summary>
    public bool CursorInWindow => Application.isFocused && Input.mousePosition.x >= 0 && Input.mousePosition.x <= Screen.width
        && Input.mousePosition.y >= 0 && Input.mousePosition.y <= Screen.height;

    private void Awake()
    {
        Init();
    }

    private void Init()
    {
        UnityEngine.Cursor.visible = false;
        UnityEngine.Cursor.lockState = CursorLockMode.Locked;
    }

    public void SetInfo(float maxRange, Transform rangeCenter, Transform mouseFollower)
    {
        _maxRange = maxRange;
        _rangeCenter = rangeCenter;
        _mouseFollower = mouseFollower;

        // 손/발의 초기 위치를 범위 중심으로부터 떨어진 거리로 저장합니다.
        _offset = (Vector2)_mouseFollower.position - (Vector2)_rangeCenter.position;
    }

    /// <summary>
    /// 마우스 이동량을 이용해 가상 커서를 이동합니다.
    /// _rangeCenter를 중심으로 _maxRange 반경 내에서만 이동합니다.
    /// 반드시 Update()에서 호출해야 합니다.
    /// </summary>
    public Vector3 UpdatePosition()
    {
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        // 손/발 위치가 아니라 저장된 가상 커서 위치에 이동량을 더합니다.
        _offset += mouseDelta * _speed;
        _offset = Vector2.ClampMagnitude(_offset, _maxRange);

        Vector2 position = (Vector2)_rangeCenter.position + _offset;
        transform.position = position;
        return transform.position;
    }
}
