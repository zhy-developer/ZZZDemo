using GameProtocol;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
public class BattleData{

	public int randSeed; //随机种子
	public int battleID;
	public bool isReplay;
	public const int mapRow = 7;//行数
	public const int mapColumn = 13;//列
	public const int gridLenth = 10000;//格子的逻辑大小
	public const int gridHalfLenth = 5000;//格子的逻辑大小
	public int mapTotalGrid;
	public int mapWidth;
	public int mapHeigh;
    public int mapMinX;
    public int mapMaxX;
    public int mapMinY; // 对应 Unity 的 Z
    public int mapMaxY;

    public List<BattleUserInfo> list_battleUser;
	private Dictionary<int,GameVector2> dic_speed;

	private readonly ZZZ.PlayerActionQueue actionQueue = new ZZZ.PlayerActionQueue();
	public PlayerOperation selfOperation;
	public int PendingActionCount => actionQueue.Count;

	private int curFramID;
	private int maxFrameID;
	private int maxSendNum;

	private List<int> lackFrame;

	public static int randSeed_static; //随机种子
	public static List<BattleUserInfo> list_battleUser_Static;
	public static  Dictionary<int, AllPlayerOperation> dic_frameDate_Static;
	public Dictionary<int,AllPlayerOperation> dic_frameDate;
	private Dictionary<int,int> dic_rightOperationID;


	//一些统计数据
	public int fps;
	public int netPack;
	public int sendNum;
	public int recvNum;
	private int lastDir;

	private static BattleData instance;
	public static BattleData Instance
	{
		get	{ 
			// 如果类的实例不存在则创建，否则直接返回
			if (instance == null) {
				instance = new BattleData ();
			}
			return instance;
		}
	}

	private BattleData(){
		actionQueue.Clear();
		selfOperation = new PlayerOperation ();
		selfOperation.move = 121;
		ResetRightOperation ();

		dic_speed = new Dictionary<int, GameVector2> ();
		//初始化速度表
		StreamingAssetService.Instance.GetFileStringFromStreamingAssets ("Desktopspeed.txt", _fileStr => {
			InitSpeedInfo (_fileStr);
		});

		curFramID = 0;
		maxFrameID = 0;
		maxSendNum = 5;

		lackFrame = new List<int> ();
		dic_rightOperationID = new Dictionary<int, int> ();
		dic_frameDate = new Dictionary<int, AllPlayerOperation> ();
	}

	/// <summary>
	/// 更新战场信息
	/// </summary>
	/// <param name="_randseed"></param>
	/// <param name="_userInfo"></param>
	public void UpdateBattleInfo(int _randseed,List<BattleUserInfo> _userInfo){
        Debug.Log("UpdateBattleInfo  更新战场信息 "  + Time.realtimeSinceStartup);
		randSeed = _randseed;
		if (_userInfo==null)
		{
			return;
		}
		list_battleUser = new List<BattleUserInfo> (_userInfo);

		foreach (var item in list_battleUser) {
			if (item.uid == NetGlobal.Instance.userUid) {
				battleID = item.battleID;
				selfOperation.battleID = battleID;
				DeLogger.LogNoticeTrace ("自己的战斗id:" + battleID);
			}
			dic_rightOperationID [item.battleID] = 0;
		}
	}

	/// <summary>
	/// 重置数据状态
	/// </summary>
	public void ClearData ()
	{
		actionQueue.Clear();
		selfOperation.move = 121;
		ResetRightOperation ();

		curFramID = 0;
		maxFrameID = 0;
		maxSendNum = 5;

		lackFrame.Clear();
		dic_rightOperationID.Clear ();
		dic_frameDate.Clear();
	}

	public void Destory ()
	{
		ClearData();
		instance = null;
	}

	/// <summary>
	/// 初始化地图
	/// </summary>
	/// <param name="bounds"></param>
    public void InitMapBounds(Bounds bounds)
    {
        // 向地图内部取整，避免边界超出地面
        int scale = ToolMethod.Render2LogicScale;

        mapMinX = Mathf.CeilToInt(bounds.min.x * scale);
        mapMaxX = Mathf.FloorToInt(bounds.max.x * scale);

        mapMinY = Mathf.CeilToInt(bounds.min.z * scale);
        mapMaxY = Mathf.FloorToInt(bounds.max.z * scale);

        mapWidth = mapMaxX - mapMinX;
        mapHeigh = mapMaxY - mapMinY;

        mapTotalGrid = mapRow * mapColumn;
    }
    /// <summary>
    /// 通过解析Desktopspeed方向查找表获取的字符串信息来初始化速度信息
    /// </summary>
    /// <param name="_fileStr"></param>
    void InitSpeedInfo (string _fileStr)
	{
		string[] lineArray = _fileStr.Split ("\n" [0]); 

		int dir;
		for (int i = 0; i < lineArray.Length; i++) {
			if (lineArray [i] != "") {
				GameVector2 date = new GameVector2 ();
				string[] line = lineArray [i].Split (new char[1]{ ',' }, 3);
				dir = System.Int32.Parse (line [0]);
				date.x = System.Int32.Parse (line [1]);
				date.y = System.Int32.Parse (line [2]);
				dic_speed [dir] = date;
			}
		}
	}

	/// <summary>
	/// 根据角度查表获取对应的速度
	/// </summary>
	/// <param name="_dir"></param>
	/// <returns></returns>
	public GameVector2 GetSpeed (int _dir)
	{
		return dic_speed [_dir];
	}

	/// <summary>
	/// 获取在地图中的逻辑位置
	/// </summary>
	/// <param name="_pos"></param>
	/// <returns></returns>
	//坐标不超出地图
	public GameVector2 GetMapLogicPosition(GameVector2 _pos){
		return new GameVector2(Mathf.Clamp(_pos.x, mapMinX, mapMaxX), Mathf.Clamp(_pos.y, mapMinY, mapMaxY));
	}

    // Two-player PvP: fixed offsets from the map center, assigned by server battleID.
    public GameVector2 GetPvpSpawnPosition(int playerBattleId)
    {
        if (playerBattleId != 1 && playerBattleId != 2)
            throw new System.ArgumentOutOfRangeException(nameof(playerBattleId), "PvP spawn requires battleID 1 or 2.");
        int centerX = mapMinX + (mapMaxX - mapMinX) / 2;
        int centerZ = mapMinY + (mapMaxY - mapMinY) / 2;
        int offset = 3 * ToolMethod.Render2LogicScale; // Each player starts 3 world units from center.
        return GetMapLogicPosition(new GameVector2(centerX + (playerBattleId == 1 ? -offset : offset), centerZ));
    }

	public GameVector2 GetMapGridCenterPosition(int _row, int _column)
	{
		return new GameVector2(mapMinX + _column * gridLenth + gridHalfLenth, mapMinY + _row * gridLenth + gridHalfLenth);
	}

	public GameVector2 GetMapGridFromRand(int _randNum)
	{
		int _num1 = _randNum % mapTotalGrid;
		int _row = _num1 / mapColumn;
		int _column = _num1 % mapColumn;
		return new GameVector2(_row, _column);
	}

	public GameVector2 GetMapGridCenterPositionFromRand(int _randNum)
	{
		GameVector2 grid = GetMapGridFromRand(_randNum);
		return GetMapGridCenterPosition(grid.x, grid.y);
	}

	/// <summary>
	/// 更新玩家移动方向
	/// </summary>
	/// <param name="_dir"></param>
	public void UpdateMoveDir (int _dir)
	{
		DeLogger.LogTrace("更新玩家移动方向 dir:"+ _dir);
		selfOperation.move = _dir;
		lastDir = _dir;
	}

	public void StopMove() {
		selfOperation.move = 121;
		lastDir = 121;
	}

	public void UpdateMoveDirUp(int _dir)
	{
		// Debug.Log("_dir  ************   "  + _dir);
		selfOperation.move = _dir;
	}


	public void UpdateRightOperation(RightOpType _type,int _value1,int _value2){
		if (_type == RightOpType.noop) return;
		actionQueue.Enqueue((int)_type, _value1, _value2);
		RefreshPendingAction();
	}

	private void RefreshPendingAction()
	{
		if (!actionQueue.TryPeek(out var action)) { ResetRightOperation(); return; }
		selfOperation.rightOperation = (RightOpType)action.Type;
		selfOperation.operationID = action.Id;
		selfOperation.operationValue1 = action.Value1;
		selfOperation.operationValue2 = action.Value2;
	}

	public void skill1()
	{
		UpdateRightOperation(RightOpType.rop2, lastDir, 0);
	}
	public void skill2()
	{
		UpdateRightOperation(RightOpType.rop3, 0, 0);
	}

	public bool IsValidRightOp(int _battleID,int _rightOpID){
		return _rightOpID > 0 && dic_rightOperationID.TryGetValue(_battleID, out var lastID) && _rightOpID > lastID;
	}

	public void UpdateRightOperationID(int _battleID,int _opID,RightOpType _type){
		dic_rightOperationID [_battleID] = _opID;
		if (battleID == _battleID) {
			// 按序号确认，避免同类型的旧回包误删下一次输入。
			if (actionQueue.Acknowledge(_opID)) RefreshPendingAction();
		}
	}

	public void ResetRightOperation(){
		selfOperation.rightOperation = RightOpType.noop;
		selfOperation.operationValue1 = 0;
		selfOperation.operationValue2 = 0;
		selfOperation.operationID = 0;
	}

	public int GetFrameDataNum(){
		if (dic_frameDate == null) {
			return 0;
		} else {
			return dic_frameDate.Count;
		}
	}

	public void AddNewFrameData(int _frameID,AllPlayerOperation _op){
		dic_frameDate [_frameID] = _op;
		//for (int i = maxFrameID + 1; i < _frameID; i++) { // 
		//	lackFrame.Add (i);
		//	Debug.LogError ("缺失 :" + i);
		//}
		maxFrameID = _frameID;

		//发送缺失帧数据
		//if (lackFrame.Count > 0) {
		//	if (lackFrame.Count > maxSendNum) {
		//		List<int> sendList = lackFrame.GetRange (0, maxSendNum);
		//		UdpPB.Instance ().SendDeltaFrames (selfOperation.battleID,sendList);
		//	} else {
		//		UdpPB.Instance ().SendDeltaFrames (selfOperation.battleID,lackFrame);
		//	}
		//}
	}

	public void AddLackFrameData (int _frameID, AllPlayerOperation _newOp)
	{
		//删除缺失的帧记录
		if (lackFrame.Contains(_frameID)) {
			dic_frameDate [_frameID] = _newOp;
			lackFrame.Remove (_frameID);
			Debug.Log ("补上 :" + _frameID);
		}
	}

	public bool TryGetNextPlayerOp (out AllPlayerOperation _op)
	{
		int _frameID = curFramID + 1;	
		return dic_frameDate.TryGetValue (_frameID,out _op);
	}
	public bool TryGetNextPlayerOpReplay(out AllPlayerOperation _op)
	{
		int _frameID = curFramID + 1;
		return dic_frameDate_Static.TryGetValue(_frameID, out _op);
	}
	public void RunOpSucces ()
	{
		curFramID++;
	}
}
