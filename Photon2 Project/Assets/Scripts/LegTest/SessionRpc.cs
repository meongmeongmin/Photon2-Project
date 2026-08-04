using Fusion;

// 방 전체에 브로드캐스트되는 RPC(채팅, 플레이어 입퇴장 알림 등)를 위한 세션 전역 오브젝트.
// 호스트가 세션 시작 직후 스폰한다.
public class SessionRpc : NetworkBehaviour
{
    public static SessionRpc Instance;

    public override void Spawned()
    {
        Instance = this;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (Instance == this) Instance = null;
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_Chat(string msg)
    {
        Chatting.Instance?.OnChatMessage(msg);
    }

    [Rpc(RpcSources.All, RpcTargets.All)]
    public void RPC_ChangeScreen()
    {
        UIManager.Instance?.ApplyChangeScreenAndDisableRoomCanvas();
    }
}
