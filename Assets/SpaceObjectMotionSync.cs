using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class SpaceObjectMotionSync : MonoBehaviour, IOnEventCallback
{
    const byte SnapshotEventCode = 71;
    const byte ImpulseRequestEventCode = 72;
    const byte SpaceMineDetonationEventCode = 73;

    static SpaceObjectMotionSync instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        EnsureInstance();
    }

    static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject root = new GameObject("SpaceObjectMotionSync");
        instance = root.AddComponent<SpaceObjectMotionSync>();
        DontDestroyOnLoad(root);
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        PhotonNetwork.AddCallbackTarget(this);
    }

    void OnDisable()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    public static void BroadcastState(string stableId, Vector2 position, Vector2 velocity, float rotation, float angularVelocity)
    {
        if (string.IsNullOrWhiteSpace(stableId) || !PhotonNetwork.IsConnected || !PhotonNetwork.IsMasterClient)
            return;

        object[] payload = { stableId, position.x, position.y, velocity.x, velocity.y, rotation, angularVelocity };
        RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(SnapshotEventCode, payload, options, SendOptions.SendUnreliable);
    }

    public static void RequestImpulse(string stableId, Vector2 impulse)
    {
        if (string.IsNullOrWhiteSpace(stableId))
            return;

        if (!PhotonNetwork.IsConnected || PhotonNetwork.IsMasterClient)
        {
            MovingSpaceObject localTarget = MovingSpaceObject.Find(stableId);
            if (localTarget != null)
            {
                localTarget.ApplyImpulse(impulse);
            }

            return;
        }

        Player masterClient = PhotonNetwork.MasterClient;
        if (masterClient == null)
            return;

        object[] payload = { stableId, impulse.x, impulse.y };
        RaiseEventOptions options = new RaiseEventOptions { TargetActors = new[] { masterClient.ActorNumber } };
        PhotonNetwork.RaiseEvent(ImpulseRequestEventCode, payload, options, SendOptions.SendReliable);
    }

    public static void BroadcastSpaceMineDetonation(int sourceViewId, Vector3 worldPosition)
    {
        if (!PhotonNetwork.IsConnected)
        {
            EnemyBot.SpawnSpaceMineDetonationEffects(worldPosition);
            return;
        }

        object[] payload = { sourceViewId, worldPosition.x, worldPosition.y, worldPosition.z };
        RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        PhotonNetwork.RaiseEvent(SpaceMineDetonationEventCode, payload, options, SendOptions.SendReliable);
    }

    public void OnEvent(EventData photonEvent)
    {
        switch (photonEvent.Code)
        {
            case SnapshotEventCode:
                ApplySnapshot(photonEvent.CustomData as object[]);
                break;
            case ImpulseRequestEventCode:
                ApplyImpulseRequest(photonEvent.CustomData as object[]);
                break;
            case SpaceMineDetonationEventCode:
                ApplySpaceMineDetonation(photonEvent.CustomData as object[]);
                break;
        }
    }

    void ApplySnapshot(object[] payload)
    {
        if (PhotonNetwork.IsMasterClient || payload == null || payload.Length < 7)
            return;

        string stableId = payload[0] as string;
        if (string.IsNullOrWhiteSpace(stableId))
            return;

        MovingSpaceObject target = MovingSpaceObject.Find(stableId);
        if (target == null)
            return;

        Vector2 position = new Vector2(ConvertToFloat(payload[1]), ConvertToFloat(payload[2]));
        Vector2 velocity = new Vector2(ConvertToFloat(payload[3]), ConvertToFloat(payload[4]));
        float rotation = ConvertToFloat(payload[5]);
        float angularVelocity = ConvertToFloat(payload[6]);
        target.ApplyNetworkState(position, velocity, rotation, angularVelocity);
    }

    void ApplyImpulseRequest(object[] payload)
    {
        if (!PhotonNetwork.IsMasterClient || payload == null || payload.Length < 3)
            return;

        string stableId = payload[0] as string;
        if (string.IsNullOrWhiteSpace(stableId))
            return;

        MovingSpaceObject target = MovingSpaceObject.Find(stableId);
        if (target == null)
            return;

        Vector2 impulse = new Vector2(ConvertToFloat(payload[1]), ConvertToFloat(payload[2]));
        target.ApplyImpulse(impulse);
    }

    void ApplySpaceMineDetonation(object[] payload)
    {
        if (payload == null || payload.Length < 4)
            return;

        int sourceViewId = ConvertToInt(payload[0]);
        Vector3 fallbackWorldPosition = new Vector3(
            ConvertToFloat(payload[1]),
            ConvertToFloat(payload[2]),
            ConvertToFloat(payload[3]));

        Vector3 resolvedWorldPosition = EnemyBot.ResolveSpaceMineDetonationPosition(sourceViewId, fallbackWorldPosition);
        EnemyBot.SpawnSpaceMineDetonationEffects(resolvedWorldPosition);
    }

    static float ConvertToFloat(object value)
    {
        if (value is float floatValue)
            return floatValue;

        if (value is double doubleValue)
            return (float)doubleValue;

        if (value is int intValue)
            return intValue;

        return 0f;
    }

    static int ConvertToInt(object value)
    {
        if (value is int intValue)
            return intValue;

        if (value is short shortValue)
            return shortValue;

        if (value is byte byteValue)
            return byteValue;

        if (value is float floatValue)
            return Mathf.RoundToInt(floatValue);

        if (value is double doubleValue)
            return Mathf.RoundToInt((float)doubleValue);

        return 0;
    }
}
