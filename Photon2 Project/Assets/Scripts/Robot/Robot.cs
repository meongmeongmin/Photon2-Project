using UnityEngine;

[DisallowMultipleComponent]
public class Robot : MonoBehaviour
{
    [Header("팔/다리 프리팹")]
    [SerializeField] private GameObject armPrefab;
    [SerializeField] private GameObject legPrefab;

    [Header("커서 프리팹")]
    [SerializeField] private GameObject _cursorPrefab;

    /// <summary>
    /// 몸통 방향 (좌: -1, 우: 1)
    /// </summary>
    public int Direction { get; set; } = 1;

    private Rigidbody2D _rigidbody;
    public Rigidbody2D Rigidbody => _rigidbody;

    private Leg _leftLeg;
    private Leg _rightLeg;
    private Arm _leftArm;
    private Arm _rightArm;

    private readonly Vector3 cameraFollowOffset = new Vector3(0f, 0f, -10f);

    private void Awake()
    {
        Init();
    }

    private void Init()
    {
        _rigidbody = GetComponent<Rigidbody2D>();

        _leftLeg = AttachLimb<Leg>(legPrefab, "PelvisLeft");
        SetMouseControl(_leftLeg);

        _rightLeg = AttachLimb<Leg>(legPrefab, "PelvisRight");
        //SetMouseControl(_rightLeg);

        _leftArm = AttachLimb<Arm>(armPrefab, "ShoulderLeft");
        _leftArm.transform.localScale = new Vector3(-1, 1, 1);   // 좌우 반전
        //SetMouseControl(_leftArm);

        _rightArm = AttachLimb<Arm>(armPrefab, "ShoulderRight");
        _rightArm.transform.localScale = new Vector3(1, 1, 1);
        //SetMouseControl(_rightArm);
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

        T limb = Instantiate(limbPrefab, attachmentPoint, false).GetComponent<T>();
        if (limb == null)
        {
            Debug.LogError($"생성한 프리팹에서 {typeof(T).Name} 스크립트를 찾지 못했습니다.", this);
            return null;
        }

        HingeJoint2D rootJoint = limb.GetComponentInChildren<HingeJoint2D>();
        rootJoint.connectedBody = _rigidbody;
        rootJoint.autoConfigureConnectedAnchor = false;
        rootJoint.anchor = Vector2.zero;
        rootJoint.connectedAnchor = attachmentPoint.localPosition;

        limb.SetInfo(this, rootJoint);
        return limb;
    }

    private void SetMouseControl<T>(T limb) where T : Limb
    {
        Cursor c = Instantiate(_cursorPrefab).GetComponent<Cursor>();
        limb.SetMouseControl(c);
    }
}
