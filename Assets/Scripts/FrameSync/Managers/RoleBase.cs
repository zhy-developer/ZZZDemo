using UnityEngine;
using SkillRuntime;

[RequireComponent(typeof(ShapeCircle))]
public class RoleBase : MonoBehaviour
{
    // Bootstrap value copied into BattleWorld once. Changing the prefab/view cannot change an active world.
    public int moveSpeed;
    [SerializeField, Min(.01f)] float positionSmoothTime = .08f;
    [SerializeField, Min(.01f)] float rotationSmoothTime = .1f;
    Vector3 velocity, targetPosition;
    float rotationVelocity;
    Transform modleParent;
    ZZZ.Player player;
    GameVector2 snapshotPosition;
    int snapshotFacing;
    public ZZZ.Player Player => player;
    public GameVector2 LogicalPosition => snapshotPosition;
    public int LogicalFacing => snapshotFacing;
    [HideInInspector] public ShapeBase objShape;
    public void InitData(GameObject ui, GameObject model, int roleID, GameVector2 spawn)
    {
        objShape = GetComponent<ShapeBase>();
        modleParent = transform.Find("Modle");
        model.transform.SetParent(modleParent); model.transform.localPosition = Vector3.zero;
        player = model.GetComponentInChildren<ZZZ.Player>(true);
        objShape.InitSelf(ObjectType.role, roleID); objShape.SetPosition(spawn);
        snapshotPosition = spawn; snapshotFacing = roleID == 1 ? 0 : 180;
        targetPosition = objShape.GetPositionVec3(); transform.position = targetPosition;
        modleParent.rotation = Quaternion.Euler(0, 90 - snapshotFacing, 0);
    }
    // A mirror used for rendering/input aim, never read back by the simulation.
    public void ApplySnapshot(BattleActorSnapshot snapshot)
    {
        snapshotPosition = new GameVector2(snapshot.X, snapshot.Z); snapshotFacing = snapshot.Facing;
        objShape.SetPosition(snapshotPosition);
        targetPosition = objShape.GetPositionVec3();
    }
    void Update()
    {
        if (!modleParent) return;
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, positionSmoothTime);
        float angle = Mathf.SmoothDampAngle(modleParent.eulerAngles.y, 90 - snapshotFacing, ref rotationVelocity, rotationSmoothTime);
        modleParent.eulerAngles = Vector3.up * angle;
    }
}
