using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    private static readonly Vector3 RobotSpawnPosition = new Vector3(-11f, 4f, 0f);

    public static UIManager Instance;

    public GameObject[] canvases;
    /// <summary>
    /// Map
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

    void Awake()
    {
        Instance = this;
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
                    // 네트워크 자식 Transform이 스폰된 루트를 기준으로 상대적인 위치를 유지하도록 합니다.
                    // 프리팹이 원점에서 떨어진 위치에 저장되어 있다면,
                    // 호스트와 프록시가 서로 다른 Transform 기준에서 시작할 수 있습니다.
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

    // 로봇 골반 로컬 기준 오프셋 위치에, 지정된 순서의 플레이어를 InputAuthority로 스폰한다.
    // 사지는 로봇과 별도의 최상위 NetworkObject로 스폰한다 (Fusion은 NetworkObject 중첩을 지원하지 않음).
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
        //카메라를 로봇(MainPelvis)에 붙이는 작업을 여기서 계속 재시도한다.
        //RPC_ChangeScreen과 로봇 Spawn은 네트워크로 각각 전달되기 때문에, 클라이언트에 따라
        //RPC가 로봇 Spawn보다 먼저 도착할 수 있다 - 그 순간에 한 번만 시도하면 로봇을 못 찾고 영영 못 붙는다.
        //그래서 아직 안 붙어있으면(parent == null) 로봇이 로컬에 나타날 때까지 매 프레임 계속 시도한다.
        if (gameObject4 != null && gameObject4.activeSelf && main_Cam != null && main_Cam.transform.parent == null)
        {
            var body = FindFirstObjectByType<BodyController>();
            if (body != null)
            {
                main_Cam.transform.SetParent(body.transform);
                main_Cam.transform.localPosition = new Vector3(0, 0, -10);
            }
        }
    }
}
