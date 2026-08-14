using UnityEngine;

[DisallowMultipleComponent]
public class Robot : MonoBehaviour
{
    [Header("팔/다리 프리팹")]
    [SerializeField] private GameObject armPrefab;
    [SerializeField] private GameObject legPrefab;

    private Rigidbody2D bodyRigidbody;

    private readonly Vector3 cameraFollowOffset = new Vector3(0f, 0f, -10f);

    private void Awake()
    {
        Init();
    }

    private void Init()
    {
        bodyRigidbody = GetComponent<Rigidbody2D>();

        GameObject leftLeg = AttachLimb(legPrefab, "PelvisLeft");
        SetMouseControl<Leg>(leftLeg);

        GameObject rightLeg = AttachLimb(legPrefab, "PelvisRight");
        //SetMouseControl<Leg>(rightLeg);

        GameObject leftArm = AttachLimb(armPrefab, "ShoulderLeft");
        leftArm.transform.localScale = new Vector3(-1, 1, 1);   // 좌우 반전
        GameObject rightArm = AttachLimb(armPrefab, "ShoulderRight");
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

    private GameObject AttachLimb(GameObject limbPrefab, string attachmentPointName)
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
        joint.connectedBody = bodyRigidbody;
        joint.autoConfigureConnectedAnchor = false;
        joint.anchor = Vector2.zero;
        joint.connectedAnchor = attachmentPoint.localPosition;

        return Instantiate(limbPrefab, attachmentPoint, false);
    }

    private void SetMouseControl<T>(GameObject limb) where T : Leg
    {
        if (limb == null)
            return;

        T limbComponent = limb.GetComponent<T>();
        if (limbComponent == null)
        {
            Debug.LogError($"{limb.name}에서 {typeof(T).Name} 스크립트를 찾지 못했습니다.", limb);
            return;
        }

        limbComponent.SetMouseControl();
    }
}
