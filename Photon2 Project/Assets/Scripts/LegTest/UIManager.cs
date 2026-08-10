using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    private static readonly Vector3 RobotSpawnPosition = new Vector3(-11f, 4f, 0f);
    private static readonly Vector3 CameraFollowOffset = new Vector3(0f, 0f, -10f);

    public static UIManager Instance;

    public GameObject[] canvases;
    /// <summary>
    /// 플레이 버튼을 누르면 켜지는 실제 게임 화면(맵) 오브젝트.
    /// 인스펙터에서 반드시 연결해야 하며, 비어 있으면 플레이 버튼이 동작하지 않는다.
    /// </summary>
    public GameObject gameObject4;

    public NetworkObject NetworkRobot;
    public Camera main_Cam;
    public bool isrobot;

    [Header("Network Prefabs")]
    public NetworkObject RobotPrefab;
    public NetworkObject LeftArmPrefab;
    public NetworkObject RightArmPrefab;
    public NetworkObject LeftLegPrefab;
    public NetworkObject RightLegPrefab;

    // 파츠 오브젝트들
    public GameObject leftArm;  // 왼쪽 팔
    public GameObject rightArm; // 오른쪽 팔
    public GameObject leftLeg;  // 왼쪽 다리
    public GameObject rightLeg; // 오른쪽 다리

    private Transform pelvis;
    private Transform cameraTarget;
    private Quaternion fixedCameraRotation;

    void Awake()
    {
        Instance = this;
        fixedCameraRotation = main_Cam != null ? main_Cam.transform.rotation : Quaternion.identity;
    }

    void Start()
    {
        Debug.Log(this.gameObject.name);
    }

    public void ShowCanvas(int index)
    {
        if (canvases == null || canvases.Length == 0) return;
        if (index < 0 || index >= canvases.Length) return;

        CloseAllCanvas();
        canvases[index].SetActive(true);    // 현재 인덱스만 활성화
    }

    void CloseAllCanvas()
    {
        if (canvases == null || canvases.Length == 0) return;

        for (int i = 0; i < canvases.Length; i++)
            canvases[i].SetActive(false);  // 모든 캔버스 비활성화
    }

    // 플레이 버튼 클릭 시 호출되는 메서드 (호스트에서만 동작)
    public void OnPlayButtonPressed()
    {
        var runner = NetworkManager.Runner;
        if (runner == null || !runner.IsServer)
        {
            Debug.LogWarning("방장(호스트)만 이 버튼을 누를 수 있습니다.");
            return;
        }

        if (isrobot == false)
        {
            var robotObj = runner.Spawn(
                RobotPrefab,
                RobotSpawnPosition,
                Quaternion.identity,
                onBeforeSpawned: (_, spawnedObject) =>
                {
                    // 스폰된 로봇 루트를 기준으로, 골반의 상대 위치를 프리팹에 저장된 값 그대로 되돌려 놓는다.
                    // 프리팹 자체가 원점(0,0,0)이 아닌 곳에 저장돼 있으면, 호스트 화면과 다른 플레이어 화면에서
                    // 로봇 부품들이 서로 다른 위치에서 시작해버릴 수 있기 때문이다.
                    Transform spawnedPelvis = spawnedObject.transform.Find("MainPelvis");
                    if (spawnedPelvis != null)
                    {
                        spawnedPelvis.localPosition = Vector3.zero;
                        spawnedPelvis.localRotation = Quaternion.identity;
                    }
                });

            NetworkRobot = robotObj;
            pelvis = robotObj.transform.Find("MainPelvis");
            if (pelvis == null)
            {
                Debug.LogError("Robot 프리팹 루트 밑에 'MainPelvis' 자식을 찾지 못했습니다. 프리팹 구조를 확인해주세요.");
                return;
            }

            var bodyController = pelvis.GetComponent<BodyController>();
            if (bodyController.sholderL == null || bodyController.sholderR == null || bodyController.pelvisL == null || bodyController.pelvisR == null)
            {
                Debug.LogError("BodyController의 sholderL/sholderR/pelvisL/pelvisR 앵커가 Robot 프리팹 Inspector에서 연결되지 않았습니다.");
                return;
            }

            // 4명의 접속 플레이어에게 사지를 1:1로 배정한다 (스폰 시점에 InputAuthority 지정)
            var players = runner.ActivePlayers.ToList();

            leftLeg = SpawnLimb(runner, LeftLegPrefab, new Vector3(-1.1f, 2f, 0), players, 0);
            rightLeg = SpawnLimb(runner, RightLegPrefab, new Vector3(1.1f, 2f, 0), players, 1);
            leftArm = SpawnLimb(runner, LeftArmPrefab, new Vector3(-1.5f, 4.2f, 0), players, 2);
            rightArm = SpawnLimb(runner, RightArmPrefab, new Vector3(1.5f, 4.2f, 0), players, 3);

            // 이름으로 찾지 않고, Robot 프리팹에 미리 연결해둔 앵커를 그대로 사용한다.
            leftArm.GetComponent<ArmManager>().sholder = bodyController.sholderL;
            rightArm.GetComponent<ArmManager>().sholder = bodyController.sholderR;
            leftLeg.GetComponent<LegManager>().pelvis = bodyController.pelvisL;
            rightLeg.GetComponent<LegManager>().pelvis = bodyController.pelvisR;

            bodyController.LeftLeg = leftLeg.GetComponent<LegManager>();
            bodyController.RightLeg = rightLeg.GetComponent<LegManager>();
            bodyController.LeftArm = leftArm.GetComponent<ArmManager>();
            bodyController.RightArm = rightArm.GetComponent<ArmManager>();

            isrobot = true;
        }

        if (gameObject4 != null)
        {
            SessionRpc.Instance?.RPC_ChangeScreen();

            Debug.Log("파츠 스폰 및 플레이어 할당 완료!");
        }
        else
        {
            Debug.LogError("GameObject(4)가 할당되지 않았습니다. 인스펙터에서 할당해 주세요.");
        }
    }

    // 골반을 기준으로 한 상대 위치(localOffset)에, 순서에 맞는 플레이어를 입력 권한(InputAuthority)으로 지정해 팔다리를 스폰한다.
    // 팔다리는 로봇의 자식이 아니라 완전히 별도인 최상위 NetworkObject로 스폰한다 (Fusion은 NetworkObject를 서로 중첩할 수 없다).
    GameObject SpawnLimb(NetworkRunner runner, NetworkObject prefab, Vector3 localOffset, List<PlayerRef> players, int playerIndex)
    {
        Vector3 worldPos = pelvis.TransformPoint(localOffset);
        PlayerRef ? authority = playerIndex < players.Count ? players[playerIndex] : null;
        var obj = runner.Spawn(prefab, worldPos, pelvis.rotation, authority);
        return obj.gameObject;
    }

    public void ApplyChangeScreenAndDisableRoomCanvas()
    {
        gameObject4.SetActive(true);  // 모든 플레이어의 화면을 변경
        main_Cam.gameObject.SetActive(true);
        GameObject roomCanvas = GameObject.Find("RoomCanvas");
        if (roomCanvas != null)
        {
            roomCanvas.SetActive(false);
        }
        else
        {
            Debug.LogError("RoomCanvas를 찾을 수 없습니다.");
        }
    }

    void Update()
    {
        // RPC 화면 전환과 로봇 스폰의 도착 순서가 다를 수 있으므로 몸통을 찾을 때까지 재시도한다.
        if (gameObject4 != null && gameObject4.activeSelf && main_Cam != null && cameraTarget == null)
        {
            var body = FindFirstObjectByType<BodyController>();
            if (body != null)
            {
                cameraTarget = body.transform;

                // 몸통 회전이 카메라로 상속되지 않도록 부모 관계를 사용하지 않는다.
                main_Cam.transform.SetParent(null, true);
            }
        }
    }

    void LateUpdate()
    {
        if (main_Cam == null || cameraTarget == null) return;

        // 몸통과 사지의 이번 프레임 이동이 끝난 뒤 위치만 따라가고 카메라의 월드 회전은 고정한다.
        main_Cam.transform.SetPositionAndRotation(cameraTarget.position + CameraFollowOffset, fixedCameraRotation);
    }
}
