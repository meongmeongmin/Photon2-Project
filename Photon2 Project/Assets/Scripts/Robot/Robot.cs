using UnityEngine;

[DisallowMultipleComponent]
public class Robot : MonoBehaviour
{
    [Header("팔/다리 프리팹")]
    [SerializeField] private GameObject armPrefab;
    [SerializeField] private GameObject legPrefab;

    private Rigidbody2D _rigidbody;
    public Rigidbody2D Rigidbody => _rigidbody;

    [Header("커서 프리팹")]
    [SerializeField] private GameObject _cursorPrefab;

    private readonly Vector3 cameraFollowOffset = new Vector3(0f, 0f, -10f);

    private void Awake()
    {
        Init();
    }

    private void Init()
    {
        _rigidbody = GetComponent<Rigidbody2D>();

        Leg leftLeg = AttachLimb<Leg>(legPrefab, "PelvisLeft");
        SetMouseControl(leftLeg);

        Leg rightLeg = AttachLimb<Leg>(legPrefab, "PelvisRight");
        //SetMouseControl(rightLeg);

        Arm leftArm = AttachLimb<Arm>(armPrefab, "ShoulderLeft");
        leftArm.transform.localScale = new Vector3(-1, 1, 1);   // 좌우 반전
        Arm rightArm = AttachLimb<Arm>(armPrefab, "ShoulderRight");
        rightArm.transform.localScale = new Vector3(1, 1, 1);
    }

    private void Update()
    {
        // 테스트용 초기 위치 복귀
        if (Input.GetKeyDown(KeyCode.R))
        {
            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
        }
    }

    private void LateUpdate()
    {
        // 로봇의 회전이 카메라에 영향을 주지 않도록 고정
        Camera.main.transform.position = transform.position + cameraFollowOffset;
        // TODO: 시네머신 카메라를 이용해서 카메라 영역 설정
    }

    private T AttachLimb<T>(GameObject limbPrefab, string attachmentPointName) where T : Limb
    {
        if (limbPrefab == null)
        {
            Debug.LogError($"{attachmentPointName}에 연결할 팔다리 프리팹이 없습니다.", this);
            return null;
        }

        Transform attachmentPoint = transform.Find(attachmentPointName);
        if (attachmentPoint == null)
        {
            Debug.LogError($"Robot 아래에서 {attachmentPointName} 연결 위치를 찾지 못했습니다.", this);
            return null;
        }

        HingeJoint2D joint = limbPrefab.GetComponentInChildren<HingeJoint2D>();
        joint.connectedBody = _rigidbody;
        joint.autoConfigureConnectedAnchor = false;
        joint.anchor = Vector2.zero;
        joint.connectedAnchor = attachmentPoint.localPosition;

        T limb = Instantiate(limbPrefab, attachmentPoint, false).GetComponent<T>();
        limb.SetInfo(this);
        return limb;
    }

    private void SetMouseControl<T>(T limb) where T : Limb
    {
        if (limb == null)
        {
            Debug.LogError($"생성한 프리팹에서 {typeof(T).Name} 스크립트를 찾지 못했습니다.", this);
            return;
        }

        Cursor c = Instantiate(_cursorPrefab).GetComponent<Cursor>();
        limb.SetMouseControl(c);
    }
}
