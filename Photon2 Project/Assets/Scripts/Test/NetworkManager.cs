using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.UI;

public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public Text StatusText;
    public InputField roomInput;
    [Header("Session-wide RPC relay (chat, screen change, ...)")]
    public NetworkObject SessionRpcPrefab;

    public static NetworkManager Instance { get; private set; }
    public static NetworkRunner Runner { get; private set; }
    public static List<SessionInfo> LastSessionList { get; private set; } = new List<SessionInfo>();

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        // 네트워크 상태 표시
        if (StatusText != null)
        {
            StatusText.text = Runner != null ? Runner.State.ToString() : "Disconnected";
        }
    }

    public NetworkRunner EnsureRunner()
    {
        if (Runner == null)
        {
            Runner = gameObject.AddComponent<NetworkRunner>();
            Runner.ProvideInput = true;
            Runner.AddCallbacks(this);
        }
        return Runner;
    }

    public void SpawnSessionRpcIfHost(NetworkRunner runner)
    {
        if (runner.IsServer && SessionRpcPrefab != null && SessionRpc.Instance == null)
        {
            runner.Spawn(SessionRpcPrefab);
        }
    }

    public async void JoinLobby()
    {
        var runner = EnsureRunner();
        await runner.JoinSessionLobby(SessionLobby.ClientServer);
        Chatting.Instance?.HandleJoinedLobby();
    }

    public void LeaveLobby()
    {
        if (Runner != null) Runner.Shutdown();
    }

    // 임시 테스트용
    public void DisconnectFromServer()
    {
        if (Runner != null)
        {
            Runner.Shutdown();
            Debug.Log("서버 연결을 끊었습니다.");
        }
        else
        {
            Debug.LogWarning("이미 연결이 끊어진 상태입니다.");
        }
    }

    public async void CreateRoom()
    {
        int roomCount = LastSessionList.Count;
        string defaultRoomName = "Room" + (roomCount + 1);

        var runner = EnsureRunner();
        var result = await runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Host,
            SessionName = defaultRoomName,
            PlayerCount = 5,
        });
        Debug.Log(result.Ok ? $"방생성완료. 방 이름: {defaultRoomName}" : $"방생성실패: {result.ShutdownReason}");
        if (result.Ok)
        {
            SpawnSessionRpcIfHost(runner);
            Chatting.Instance?.HandleJoinedRoom();
        }
    }

    // 친구와 플레이 (방 코드입력)
    public async void JoinRoom()
    {
        if (roomInput != null && !string.IsNullOrEmpty(roomInput.text))
        {
            var runner = EnsureRunner();
            var result = await runner.StartGame(new StartGameArgs
            {
                GameMode = GameMode.Client,
                SessionName = roomInput.text,
            });
            if (!result.Ok) Debug.LogWarning($"방참가실패: {result.ShutdownReason}");
            else Chatting.Instance?.HandleJoinedRoom();
        }
        else
        {
            Debug.LogWarning("Room name is empty. Please enter a room name.");
        }
    }

    public async void JoinRoomByName(string sessionName)
    {
        var runner = EnsureRunner();
        var result = await runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Client,
            SessionName = sessionName,
        });
        if (!result.Ok) Debug.LogWarning($"방참가실패: {result.ShutdownReason}");
        else Chatting.Instance?.HandleJoinedRoom();
    }

    public void ExitGame()
    {
        if (Runner != null)
        {
            Runner.Shutdown();
            Debug.Log("서버 연결을 끊었습니다.");
        }

        Debug.Log("게임을 종료합니다.");
        Application.Quit();
    }

    void OnApplicationQuit()
    {
        if (Runner != null)
        {
            Runner.Shutdown();
            Debug.Log("강제 종료 - 서버 연결을 끊었습니다.");
        }
    }

    public async void JoinRandomRoom()
    {
        var runner = EnsureRunner();
        if (LastSessionList.Count == 0)
        {
            Debug.LogWarning("참가 가능한 방이 없습니다.");
            return;
        }
        var session = LastSessionList[UnityEngine.Random.Range(0, LastSessionList.Count)];
        var result = await runner.StartGame(new StartGameArgs
        {
            GameMode = GameMode.Client,
            SessionName = session.Name,
        });
        if (!result.Ok) Debug.LogWarning($"방참가실패: {result.ShutdownReason}");
        else Chatting.Instance?.HandleJoinedRoom();
    }

    public void LeaveRoom()
    {
        if (Runner != null) Runner.Shutdown();
    }

    [ContextMenu("정보")]
    void Info()
    {
        if (Runner != null && Runner.IsRunning)
        {
            print("현재 방 이름 : " + Runner.SessionInfo.Name);
            print("현재 방 인원수 : " + Runner.SessionInfo.PlayerCount);
            print("현재 방 최대인원수 : " + Runner.SessionInfo.MaxPlayers);
        }
        else
        {
            print("방 개수 : " + LastSessionList.Count);
        }
    }

    #region INetworkRunnerCallbacks

    Vector2 lastMouseWorldPos;

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var data = new NetworkInputData();

        // 창이 활성 상태이고, 마우스가 실제로 이 게임 창 위에 있을 때만 마우스 좌표를 갱신한다.
        Vector3 mousePos = UnityEngine.Input.mousePosition;
        bool cursorInWindow = Application.isFocused
            && mousePos.x >= 0 && mousePos.x <= Screen.width
            && mousePos.y >= 0 && mousePos.y <= Screen.height;

        if (cursorInWindow && Camera.main != null)
            lastMouseWorldPos = Camera.main.ScreenToWorldPoint(mousePos);

        data.MouseWorldPos = lastMouseWorldPos;
        input.Set(data);
    }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        LastSessionList = sessionList;
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log("서버접속완료");
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) => Chatting.Instance?.HandlePlayerJoined(runner, player);
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) => Chatting.Instance?.HandlePlayerLeft(runner, player);
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

    #endregion
}
