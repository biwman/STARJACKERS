using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class SpaceObjectMotionSync : MonoBehaviour, IOnEventCallback
{
    const byte SnapshotEventCode = 71;
    const byte ImpulseRequestEventCode = 72;
    const byte SpaceMineDetonationEventCode = 73;
    const byte ObstacleDamageRequestEventCode = 74;
    const byte ObstacleStateSyncEventCode = 75;
    const byte ObstacleSplitEventCode = 76;
    const float MaxAcceptedImpulseMagnitude = 8f;

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

    static bool CanRaiseRoomEvent()
    {
        return PhotonNetwork.IsConnected && PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null;
    }

    public static void BroadcastState(string stableId, Vector2 position, Vector2 velocity, float rotation, float angularVelocity)
    {
        if (string.IsNullOrWhiteSpace(stableId) || !CanRaiseRoomEvent() || !PhotonNetwork.IsMasterClient)
            return;

        object[] payload = { stableId, position.x, position.y, velocity.x, velocity.y, rotation, angularVelocity };
        RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.Others };
        PhotonNetwork.RaiseEvent(SnapshotEventCode, payload, options, SendOptions.SendUnreliable);
    }

    public static void RequestImpulse(string stableId, Vector2 impulse)
    {
        if (string.IsNullOrWhiteSpace(stableId))
            return;

        if (!CanRaiseRoomEvent() || PhotonNetwork.IsMasterClient)
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

        object[] payload = { stableId, impulse.x, impulse.y, PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 0 };
        RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient };
        PhotonNetwork.RaiseEvent(ImpulseRequestEventCode, payload, options, SendOptions.SendUnreliable);
    }

    public static void BroadcastSpaceMineDetonation(Vector3 worldPosition, float radius)
    {
        float resolvedRadius = Mathf.Max(0.1f, radius);

        if (!CanRaiseRoomEvent())
        {
            EnemyBot.SpawnSpaceMineDetonationEffects(worldPosition, resolvedRadius);
            return;
        }

        object[] payload = { worldPosition.x, worldPosition.y, worldPosition.z, resolvedRadius };
        RaiseEventOptions options = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        bool raised = PhotonNetwork.RaiseEvent(SpaceMineDetonationEventCode, payload, options, SendOptions.SendReliable);
        if (!raised)
            EnemyBot.SpawnSpaceMineDetonationEffects(worldPosition, resolvedRadius);
    }

    public static void RequestObstacleDamage(string stableId, int damage)
    {
        if (string.IsNullOrWhiteSpace(stableId) || damage <= 0)
            return;

        if (!CanRaiseRoomEvent() || PhotonNetwork.IsMasterClient)
        {
            ObstacleSpawner.ApplyObstacleDamage(stableId, damage);
            return;
        }

        Player masterClient = PhotonNetwork.MasterClient;
        if (masterClient == null)
            return;

        object[] payload = { stableId, damage };
        RaiseEventOptions options = new RaiseEventOptions { TargetActors = new[] { masterClient.ActorNumber } };
        PhotonNetwork.RaiseEvent(ObstacleDamageRequestEventCode, payload, options, SendOptions.SendReliable);
    }

    public static void BroadcastObstacleState(string serializedState, int[] targetActors = null)
    {
        if (!CanRaiseRoomEvent())
        {
            ObstacleSpawner.ApplyRuntimeStateSnapshot(serializedState);
            return;
        }

        RaiseEventOptions options = targetActors != null && targetActors.Length > 0
            ? new RaiseEventOptions { TargetActors = targetActors }
            : new RaiseEventOptions { Receivers = ReceiverGroup.Others };

        PhotonNetwork.RaiseEvent(ObstacleStateSyncEventCode, serializedState, options, SendOptions.SendReliable);
    }

    public static void BroadcastObstacleSplit(string sourceStableId, string childAState, string childBState)
    {
        if (string.IsNullOrWhiteSpace(sourceStableId))
            return;

        if (!CanRaiseRoomEvent())
        {
            ObstacleSpawner.ApplyObstacleSplitDelta(sourceStableId, childAState, childBState);
            return;
        }

        if (!PhotonNetwork.IsMasterClient)
            return;

        object[] payload = { sourceStableId, childAState ?? string.Empty, childBState ?? string.Empty };
        PhotonNetwork.RaiseEvent(
            ObstacleSplitEventCode,
            payload,
            new RaiseEventOptions { Receivers = ReceiverGroup.Others },
            SendOptions.SendReliable);
    }

    public void OnEvent(EventData photonEvent)
    {
        switch (photonEvent.Code)
        {
            case SnapshotEventCode:
                ApplySnapshot(photonEvent.CustomData as object[]);
                break;
            case ImpulseRequestEventCode:
                ApplyImpulseRequest(photonEvent);
                break;
            case SpaceMineDetonationEventCode:
                ApplySpaceMineDetonation(photonEvent.CustomData as object[]);
                break;
            case ObstacleDamageRequestEventCode:
                ApplyObstacleDamageRequest(photonEvent.CustomData as object[]);
                break;
            case ObstacleStateSyncEventCode:
                ApplyObstacleStateSync(photonEvent.CustomData);
                break;
            case ObstacleSplitEventCode:
                ApplyObstacleSplit(photonEvent.CustomData as object[]);
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

    void ApplyImpulseRequest(EventData photonEvent)
    {
        object[] payload = photonEvent.CustomData as object[];
        if (!PhotonNetwork.IsMasterClient || payload == null || payload.Length < 3)
            return;

        string stableId = payload[0] as string;
        if (string.IsNullOrWhiteSpace(stableId))
            return;

        MovingSpaceObject target = MovingSpaceObject.Find(stableId);
        if (target == null)
            return;

        Vector2 impulse = Vector2.ClampMagnitude(
            new Vector2(ConvertToFloat(payload[1]), ConvertToFloat(payload[2])),
            MaxAcceptedImpulseMagnitude);
        target.ApplyImpulse(impulse);
        target.ForceBroadcastSnapshot();
    }

    void ApplySpaceMineDetonation(object[] payload)
    {
        if (payload == null || payload.Length < 4)
            return;

        Vector3 worldPosition = new Vector3(
            ConvertToFloat(payload[0]),
            ConvertToFloat(payload[1]),
            ConvertToFloat(payload[2]));
        float radius = ConvertToFloat(payload[3]);

        EnemyBot.SpawnSpaceMineDetonationEffects(worldPosition, radius);
    }

    void ApplyObstacleDamageRequest(object[] payload)
    {
        if (!PhotonNetwork.IsMasterClient || payload == null || payload.Length < 2)
            return;

        string stableId = payload[0] as string;
        if (string.IsNullOrWhiteSpace(stableId))
            return;

        int damage = ConvertToInt(payload[1]);
        if (damage <= 0)
            return;

        ObstacleSpawner.ApplyObstacleDamage(stableId, damage);
    }

    void ApplyObstacleStateSync(object payload)
    {
        if (payload is string serializedState)
            ObstacleSpawner.ApplyRuntimeStateSnapshot(serializedState);
    }

    void ApplyObstacleSplit(object[] payload)
    {
        if (PhotonNetwork.IsMasterClient || payload == null || payload.Length < 3)
            return;

        string sourceStableId = payload[0] as string;
        string childAState = payload[1] as string;
        string childBState = payload[2] as string;
        ObstacleSpawner.ApplyObstacleSplitDelta(sourceStableId, childAState, childBState);
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
