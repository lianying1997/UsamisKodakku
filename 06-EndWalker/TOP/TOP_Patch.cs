using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Network.Structures.InfoProxy;
using Newtonsoft.Json;
using Dalamud.Utility.Numerics;
using ECommons;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.Draw.Manager;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using KodakkuAssist.Module.GameOperate;

namespace UsamisKodakku.Scripts._06_EndWalker.TOP;

[ScriptType(name: Name, territorys: [], guid: "b688fd29-786e-4103-82a3-7ad786af433b", 
    version: Version, author: "Usami", note: NoteStr)]

// ^(?!.*((武僧|机工士|龙骑士|武士|忍者|蝰蛇剑士|钐镰客|舞者|吟游诗人|占星术士|贤者|学者|(朝日|夕月)小仙女|炽天使|白魔法师|战士|骑士|暗黑骑士|绝枪战士|绘灵法师|黑魔法师|青魔法师|召唤师|宝石兽|亚灵神巴哈姆特|亚灵神不死鸟|迦楼罗之灵|泰坦之灵|伊弗利特之灵|后式自走人偶)\] (Used|Cast))).*35501.*$

public class TopPatch
{
    const string NoteStr =
    """
    基于K佬绝欧绘图脚本的个人向补充，
    请先按需求检查并设置“用户设置”栏目。
    
    v0.0.0.0
    [ ] P5 一运
    [ ] P5 一传
    [x] P5 二运
    [ ] P5 二传
    [ ] P5 三运
    [ ] P5 三传
    [ ] P5 四传
    """;

    private const string Name = "TOP_Patch [欧米茄绝境验证战 补丁]";
    private const string Version = "0.0.0.0";
    private const string DebugVersion = "a";
    private const string Note = "";
    
    [UserSetting("Debug模式，非开发用请关闭")]
    public static bool DebugMode { get; set; } = false;
    [UserSetting("是否开启柯基模式")]
    public static bool CaptainMode { get; set; } = true;
    [UserSetting("站位提示圈绘图-普通颜色")]
    public static ScriptColor PosColorNormal { get; set; } = new() { V4 = new Vector4(1.0f, 1.0f, 1.0f, 1.0f) };
    [UserSetting("站位提示圈绘图-玩家站位颜色")]
    public static ScriptColor PosColorPlayer { get; set; } = new() { V4 = new Vector4(0.0f, 1.0f, 1.0f, 1.0f) };
    
    private static readonly Vector3 Center = new Vector3(100, 0, 100);
    private TopPhase _phase = TopPhase.Init;
    private List<bool> _drawn = new bool[20].ToList();                  // 绘图记录
    private volatile List<bool> _recorded = new bool[20].ToList();      // 被记录flag
    private static DeltaVersion _dv = new(DebugMode);
    private static SigmaVersion _sv = new(DebugMode);
    private static SigmaWorld _sw = new(DebugMode);
    private static DynamicsPass _dyn = new(DebugMode);
    
    private List<AutoResetEvent> _events = Enumerable.Repeat(new AutoResetEvent(false), 20).ToList();
    
    public void Init(ScriptAccessory accessory)
    {
        accessory.DebugMsg($"Init {Name} v{Version}{DebugVersion} Success.\n{Note}", DebugMode);
        _phase = TopPhase.Init;
        _events = Enumerable.Repeat(new AutoResetEvent(false), 20).ToList();
        accessory.Method.MarkClear();
        accessory.Method.RemoveDraw(".*");
    }

    [ScriptMethod(name: "随时DEBUG用", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:=TST"], userControl: false)]
    public void EchoDebug(Event @event, ScriptAccessory accessory)
    {
        if (!DebugMode) return;
        // ---- DEBUG CODE ----
        // accessory.Method.Mark(accessory.Data.Me, MarkType.Attack1, false);

        // -- DEBUG CODE END --
    }
    
    #region P5 一运
    
    [ScriptMethod(name: "P5 潜能量层数记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:3444"], userControl: false)]
    public void P5_DynamicRecord(Event @event, ScriptAccessory accessory)
    {
        if (!IsInPhase5(_phase)) return;
        var tid = @event.TargetId();
        var tidx = accessory.GetPlayerIdIndex(tid);
        _dyn.AddDynamicBuffLevel(tidx);
        accessory.DebugMsg($"{accessory.GetPlayerJobByIndex(tidx)}的潜能量增加了，目前为{_dyn.GetDynamicBuffLevelCount(tidx)}",
            DebugMode);
    }
    
    [ScriptMethod(name: "P5 一运 阶段转换", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(31624)$"], userControl: false)]
    public void P5_RunMi_Delta_PhaseRecord(Event @event, ScriptAccessory accessory)
    {
        _phase = TopPhase.P5_Delta;
        _dyn = new DynamicsPass(DebugMode);
        _dv = new DeltaVersion(DebugMode);
        accessory.DebugMsg($"当前阶段为：{_phase}", DebugMode);
    }
    
    [ScriptMethod(name: "P5 一运 远近线记录", eventType: EventTypeEnum.Tether, eventCondition: ["Id:regex:^(200|201)$"], userControl: false)]
    public void P5_Delta_LocalRemoteTetherRecord(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_Delta) return;
        var tetherId = (TetherId)@event.Id();
        var targetId = @event.TargetId();
        var sourceId = @event.SourceId();

        switch (tetherId)
        {
            case TetherId.LocalTetherPrep:
                _dv.LocalTetherTargetAdd(sourceId);
                _dv.LocalTetherTargetAdd(targetId);
                break;
            case TetherId.RemoteTetherPrep:
                _dv.RemoteTetherTargetAdd(sourceId);
                _dv.RemoteTetherTargetAdd(targetId);
                break;
        }
    }
    
    [ScriptMethod(name: "P5 一二传世界记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(344[23])$"], userControl: false)]
    public void P5_HelloWorldFarNearRecord(Event @event, ScriptAccessory accessory)
    {
        if (!IsInPhase5(_phase)) return;
        if (_phase == TopPhase.P5_Omega) return;
        var stid = (StatusId)@event.StatusId();
        var targetId = @event.TargetId();
        switch (stid)
        {
            case StatusId.FarWorld:
                _dyn.SetFarSource(targetId);
                break;
            case StatusId.NearWorld:
                _dyn.SetNearSource(targetId);
                break;
        }
    }
    
    #endregion

    #region P5 二运
    
    [ScriptMethod(name: "P5 二运 阶段转换", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(32788)$"], userControl: false)]
    public void P5_RunMi_Sigma_PhaseRecord(Event @event, ScriptAccessory accessory)
    {
        _phase = TopPhase.P5_Sigma;
        _sv = new SigmaVersion(DebugMode);
        _sw = new SigmaWorld(DebugMode);
        _dyn.DynamicIdxAdd();
        _dyn.Reset();
        accessory.DebugMsg($"当前阶段为：{_phase}", DebugMode);
    }
    
    [ScriptMethod(name: "P5 二运中远线记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(342[78])$"], userControl: false)]
    public void P5_GlitchRecord(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_Sigma) return;
        var stid = (StatusId)@event.StatusId();
        var tid = @event.TargetId();
        if (tid != accessory.Data.Me) return;
        _sv.SetGlitchType(stid);
        accessory.DebugMsg($"成功记录下{(_sv.IsRemoteGlitch()?"远线":"中线")}", DebugMode);
    }
    
    [ScriptMethod(name: "P5 二运索尼标记录", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(01A[0123])$"], userControl: false)]
    public void P5_SonyIconRecord(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_Sigma) return;
        var id = (IconId)@event.Id();
        var tid = @event.TargetId();
        _sv.AddPlayerToGroup(tid, id, accessory);
    }
    
    [ScriptMethod(name: "P5 二运八方炮点名标记录", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(00F4)$"], userControl: false)]
    public void P5_SigmaCannonIconRecord(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_Sigma) return;
        var tid = @event.TargetId();
        _sv.SetTargetedPlayer(tid, accessory);
    }
    
    [ScriptMethod(name: "P5 二运男人真北位置记录", eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["Id:7747", "SourceDataId:15720"], userControl: false)]
    public void P5_OmegaTrueNorthRecord(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_Sigma) return;
        var spos = @event.SourcePosition();
        var pos = spos.Position2Dirs(Center, 16);
        _sv.SetSpreadTrueNorth(pos, accessory);
    }
    
    [ScriptMethod(name: "P5 二运索尼头标计算", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31603"], userControl: false)]
    public void P5_SigmaVersionMarker(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_Sigma) return;
        lock (_sv)
        {
            while (!_sv.SonyRecordedDone() && !_sv.TargetRecordedDone()) ;
        }
        _sv.BuildUntargetedGroup(accessory);
        if (CaptainMode)
        {
            _sv.BuildMarker(accessory);
            MarkAllPlayers(_sv.GetMarkers(), accessory);
        }
        _sv.FindSpreadTarget();
        _sv.CalcSpreadPos(Center, accessory);
        _events[(int)RecordedIdx.SigmaSonyMarker].Set();
    }
    
    [ScriptMethod(name: "P5 二运八方分散指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31603"])]
    public void P5_SigmaVersionSpreadDir(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_Sigma) return;
        _events[(int)RecordedIdx.SigmaSonyMarker].WaitOne();
        DrawSigmaSpreadSolution(accessory);
    }
    
    [ScriptMethod(name: "P5 二运塔生成记录", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["DataId:regex:^(201324[56])$", "Operate:Add"], userControl: false)]
    public void P5_SigmaTowerRecord(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_Sigma) return;
        var id = (DataId)@event.DataId();
        var spos = @event.SourcePosition();
        _sv.SetTowerType(id, spos, Center, accessory);
    }
    
    [ScriptMethod(name: "P5 二运塔计算与指路", eventType: EventTypeEnum.RemoveCombatant, eventCondition: ["DataId:14669"])]
    public void P5_SigmaTowerCalc(Event @event, ScriptAccessory accessory)
    {
        // 因为计算不对后续机制造成影响，所以可以放在一个事件检测里
        if (_phase != TopPhase.P5_Sigma) return;
        _sv.BuildTargetTowerPos(accessory);
        _sv.CalcTargetTowerPos(Center, accessory);
        DrawSigmaTowerSolution(accessory);
    }
    
    [ScriptMethod(name: "P5 二运前半头标消除，计算二传头标", eventType: EventTypeEnum.KnockBack, userControl: false)]
    public void P5_SigmaVersionMarkClear(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_Sigma) return;
        if (!CaptainMode) return;
        accessory.Method.MarkClear();
        accessory.Method.RemoveDraw(".*");
        _sw.CalcMarker();
        // TODO 二传算头标
    }
    
    [ScriptMethod(name: "P5 二传标头标", eventType: EventTypeEnum.ActionEffect, eventCondition: ["DataId:3149[23]"], userControl: false)]
    public void P5_SigmaWorldMarker(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_Sigma) return;
        if (@event.SourceId() != accessory.Data.Me) return;
        if (!CaptainMode) return;
        // TODO 二传标头标
    }

    /// <summary>
    /// 根据MarkerList中的内容为每位玩家标头标
    /// </summary>
    /// <param name="marker"></param>
    /// <param name="accessory"></param>
    private static void MarkAllPlayers(List<MarkType> marker, ScriptAccessory accessory)
    {
        for (var i = 0; i < 8; i++)
        {
            var player = accessory.Data.PartyList[i];
            accessory.DebugMsg($"为{accessory.GetPlayerJobById(player)}标上{marker[i]}", DebugMode);
            // todo 测试完成后，删除该注释
            accessory.Method.Mark(player, marker[i]);
        }
    }
    
    /// <summary>
    /// 画二运八方炮分散站位
    /// </summary>
    /// <param name="accessory"></param>
    public void DrawSigmaSpreadSolution(ScriptAccessory accessory)
    {
        var posV3 = _sv.GetSpreadPosV3();
        var myIndex = accessory.GetMyIndex();
        for (var i = 0; i < 8; i++)
        {
            var color = i == myIndex ? PosColorPlayer.V4.WithW(2f) : PosColorNormal.V4;
            var dp = accessory.DrawStaticCircle(posV3[i], color, 0, 7700, $"二运八方{i}", 1f);
            accessory.Method.SendDraw(0, DrawTypeEnum.Circle, dp);
            if (i != myIndex) continue;
            var dp0 = accessory.DrawGuidance(posV3[i], 0, 7700, $"二运八方指路{i}");
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);
        }
    }

    /// <summary>
    /// 画二运踩塔站位
    /// </summary>
    /// <param name="accessory"></param>
    public void DrawSigmaTowerSolution(ScriptAccessory accessory)
    {
        // 摧毁时间可以忽略，通过KnockBack摧毁并消除头标
        var posV3 = _sv.GetTargetTowerPosV3();
        var myIndex = accessory.GetMyIndex();
        for (var i = 0; i < 8; i++)
        {
            var color = i == myIndex ? PosColorPlayer.V4.WithW(2f) : PosColorNormal.V4;
            var dp = accessory.DrawStaticCircle(posV3[i], color, 0, 7700, $"二运踩塔{i}", 1f);
            accessory.Method.SendDraw(0, DrawTypeEnum.Circle, dp);
            if (i != myIndex) continue;
            var dp0 = accessory.DrawGuidance(posV3[i], 0, 7700, $"二运踩塔指路{i}");
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);
        }
    }
        
    #endregion
    
    [ScriptMethod(name: "P5 三运 阶段转换", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(32789)$"], userControl: false)]
    public void P5_RunMi_Omega_PhaseRecord(Event @event, ScriptAccessory accessory)
    {
        _phase = TopPhase.P5_Omega;
        accessory.DebugMsg($"当前阶段为：{_phase}", DebugMode);
    }
    
    private static bool IsInPhase5(TopPhase phase)
    {
        return phase is TopPhase.P5_Delta or TopPhase.P5_Sigma or TopPhase.P5_Omega;
    }
}

public class DeltaVersion(bool debugMode)
{
    private readonly bool _debugMode = debugMode;    // 测试完成后置为False
    // 数大在外，锁2近世界
    private List<uint> RemoteTetherInside { get; set; } = [];
    private List<uint> RemoteTetherOutside { get; set; } = [];
    private List<uint> LocalTetherInside { get; set; } = [];
    private List<uint> LocalTetherOutside { get; set; } = [];
    // 以光头为北的左，为场地北，A侧
    public List<uint> TetherUp { get; set; } = [];
    public List<uint> TetherDown { get; set; } = [];

    /// <summary>
    /// 添加近线玩家单位
    /// </summary>
    /// <param name="id">玩家ID</param>
    public void LocalTetherTargetAdd(uint id)
    {
        if (LocalTetherInside.Count < 2)
            LocalTetherInside.Add(id);
        else
            LocalTetherOutside.Add(id);
    }
    
    /// <summary>
    /// 添加远线玩家单位
    /// </summary>
    /// <param name="id">玩家ID</param>
    public void RemoteTetherTargetAdd(uint id)
    {
        if (RemoteTetherInside.Count < 2)
            RemoteTetherInside.Add(id);
        else
            RemoteTetherOutside.Add(id);
    }
}
    
public class SigmaVersion(bool debugMode)
{
    private readonly bool _debugMode = debugMode;
    private List<uint> CircleGroup { get; set; } = [];
    private List<uint> CrossGroup { get; set; } = [];
    private List<uint> TriangleGroup { get; set; } = [];
    private List<uint> SquareGroup { get; set; } = [];
    private List<bool> IsTargeted { get; set; } = new bool[8].ToList();
    private List<uint> UntargetedGroup { get; set; } = [];
    private bool GlitchType { get; set; }
    private List<MarkType> Marker { get; set; } = Enumerable.Repeat(MarkType.None, 8).ToList();
    private List<int> TowerType { get; set; } = Enumerable.Repeat(0, 16).ToList();
    private List<int> SpreadTargetPos { get; set; } = Enumerable.Repeat(0, 8).ToList();
    private List<Vector3> SpreadPosV3 { get; set; } = Enumerable.Repeat(new Vector3(0, 0, 0), 8).ToList();
    private List<int> TargetTowerPos { get; set; } = Enumerable.Repeat(0, 8).ToList();
    private List<Vector3> TargetTowerPosV3 { get; set; } = Enumerable.Repeat(new Vector3(0, 0, 0), 8).ToList();
    private int SpreadTrueNorth { get; set; }
    public int SonyGroupCount { get; set; } = 0;

    public void AddPlayerToGroup(uint id, IconId group, ScriptAccessory accessory)
    {
        switch (group)
        {
            case IconId.IconCircle:
                CircleGroup.Add(id);
                break;
            case IconId.IconCross:
                CrossGroup.Add(id);
                break;
            case IconId.IconTriangle:
                TriangleGroup.Add(id);
                break;
            case IconId.IconSquare:
                SquareGroup.Add(id);
                break;
        }
        SonyGroupCount++;
        // accessory.DebugMsg($"添加{accessory.GetPlayerJobById(id)}到{group}", _debugMode);
    }

    public void BuildTargetTowerPos(ScriptAccessory accessory)
    {
        for (var i = 0; i < 8; i++)
        {
            var towerIdx = FindTargetTower(i);
            if (towerIdx == -1)
            {
                TargetTowerPos[i] = 0;
                accessory.DebugMsg($"出现错误，{accessory.GetPlayerJobByIndex(i)}的塔未找到", _debugMode);
            }
            else
            {
                TargetTowerPos[i] = towerIdx;
                accessory.DebugMsg($"成功找到{accessory.GetPlayerJobByIndex(i)}的塔{towerIdx}", _debugMode);
            }
        }
    }
    
    public int FindTargetTower(int idx)
    {
        var startIdx = SpreadTargetPos[idx];
        var roundNum = TowerType.Count;
        
        // 构建4个观察用index，忽视面前
        var indices = new List<int>
        {
            RoundIndex(startIdx - 2, roundNum),
            RoundIndex(startIdx - 1, roundNum),
            RoundIndex(startIdx + 1, roundNum),
            RoundIndex(startIdx + 2, roundNum)
        };
        
        // 存在双人塔，返回双人塔idx
        foreach (var index in indices.Where(index => TowerType[index] == 2))
            return index;

        // 有且仅有一座塔，返回单人塔idx
        var ones = indices.Where(index => TowerType[index] == 1).ToList();
        if (ones.Count == 1)
            return ones[0];
        
        return -1;
    }
    
    private int RoundIndex(int idx, int roundNum)
    {
        return (idx + roundNum) % roundNum;
    }

    public void SetSpreadTrueNorth(int pos, ScriptAccessory accessory)
    {
        SpreadTrueNorth = pos;
        accessory.DebugMsg($"设置分散真北方向为{pos}", _debugMode);
    }

    public void SetTargetedPlayer(uint id, ScriptAccessory accessory)
    {
        var idx = accessory.GetPlayerIdIndex(id);
        IsTargeted[idx] = true;
        accessory.DebugMsg($"捕捉到{accessory.GetPlayerJobByIndex(idx)}被选为点名目标", _debugMode);
    }

    public void BuildUntargetedGroup(ScriptAccessory accessory)
    {
        UntargetedGroup = IsTargeted
            .Select((value, index) => new { value, index })
            .Where(x => !x.value)
            .Select(x => accessory.Data.PartyList[x.index])
            .ToList();
        
        foreach (var player in UntargetedGroup)
            accessory.DebugMsg($"{accessory.GetPlayerJobById(player)}未被选为目标", _debugMode);
    }

    public IconId FindIconGroup(uint id)
    {
        if (CircleGroup.Contains(id))
            return IconId.IconCircle;
        if (CrossGroup.Contains(id))
            return IconId.IconCross;
        if (TriangleGroup.Contains(id))
            return IconId.IconTriangle;
        if (SquareGroup.Contains(id))
            return IconId.IconSquare;
        return IconId.None;
    }

    public uint FindPartner(uint id)
    {
        var group = FindIconGroup(id);
        var chosenGroup = group switch
        {
            IconId.IconCircle => CircleGroup,
            IconId.IconCross => CrossGroup,
            IconId.IconTriangle => TriangleGroup,
            IconId.IconSquare => SquareGroup,
            _ => [],
        };
        return chosenGroup.FirstOrDefault(player => player != id);
    }

    public void BuildMarker(ScriptAccessory accessory)
    {
        // 无点名二人
        var playerAttack1 = UntargetedGroup[0];
        var playerBind1 = UntargetedGroup[1];
        SetMarkerBySelf(playerAttack1, MarkType.Attack1, accessory);
        SetMarkerBySelf(playerBind1, MarkType.Bind1, accessory);

        // 无点名二人搭档
        var playerCircle = FindPartner(playerAttack1);
        var playerAttack4 = FindPartner(playerBind1);
        SetMarkerBySelf(playerCircle, MarkType.Circle, accessory);
        SetMarkerBySelf(playerAttack4, MarkType.Attack4, accessory);

        var extraMarkIdx = 0;
        List<MarkType> extraMarkType = [MarkType.Attack2, MarkType.Bind3, MarkType.Attack3, MarkType.Bind2];
        // 剩余未被标记的
        for (var i = 0; i < 8; i++)
        {
            if (IsMarkered(i)) continue;
            var markPrepPlayer = accessory.Data.PartyList[i];
            SetMarkerBySelf(markPrepPlayer, extraMarkType[extraMarkIdx], accessory);
            extraMarkIdx++;
            var markPlayerPartner = FindPartner(markPrepPlayer);
            SetMarkerBySelf(markPlayerPartner, extraMarkType[extraMarkIdx], accessory);
            extraMarkIdx++;
        }
    }

    public void FindSpreadTarget()
    {
        for (var i = 0; i < 8; i++)
        {
            var marker = Marker[i];
            var pos = marker switch
            {
                MarkType.Attack1 => 15,
                MarkType.Attack2 => 13,
                MarkType.Attack3 => 11,
                MarkType.Attack4 => 9,
                MarkType.Bind1 => 1,
                MarkType.Bind2 => 3,
                MarkType.Bind3 => 5,
                MarkType.Circle => 7,
                _ => 0
            };
            SpreadTargetPos[i] = (pos + SpreadTrueNorth) % 16;
        }
    }

    public void CalcSpreadPos(Vector3 center, ScriptAccessory accessory)
    {
        // TODO 该“中远线距离”值待测试
        var basicDistance = IsRemoteGlitch() ? 19.5f : 11.25f;
        var basicPoint = new Vector3(100, 0, 100 - basicDistance);
        for (var i = 0; i < 8; i++)
        {
            var posV3 = basicPoint.RotatePoint(center, SpreadTargetPos[i] * float.Pi / 8);
            // 第二排往内一步避免引导，第一排往外一步引导
            if (GetMarkerTypeFromIdx(i) is MarkType.Attack2 or MarkType.Bind2)
                posV3 = posV3.PointInOutside(center, 0.5f);
            if (GetMarkerTypeFromIdx(i) is MarkType.Attack1 or MarkType.Bind1)
                posV3 = posV3.PointInOutside(center, 0.5f, true);
            SpreadPosV3[i] = posV3;
            accessory.DebugMsg($"计算出{accessory.GetPlayerJobByIndex(i)}({GetMarkerTypeFromIdx(i)})的分散位置{SpreadPosV3[i]}",
                _debugMode);
        }
    }

    public void CalcTargetTowerPos(Vector3 center, ScriptAccessory accessory)
    {
        var knockBackDistance = 13f;
        var basicPoint = new Vector3(100, 0, 82.5f + knockBackDistance);
        for (var i = 0; i < 8; i++)
        {
            var posV3 = basicPoint.RotatePoint(center, TargetTowerPos[i] * float.Pi / 8);
            TargetTowerPosV3[i] = posV3;
            accessory.DebugMsg($"计算出{accessory.GetPlayerJobByIndex(i)}({GetMarkerTypeFromIdx(i)})的击退塔位置{TargetTowerPosV3[i]}",
                _debugMode);
        }
    }
   
    public void SetMarkerFromOut(uint id, MarkType marker, ScriptAccessory accessory)
    {
        var idx = accessory.GetPlayerIdIndex(id);
        Marker[idx] = marker;
        accessory.DebugMsg($"从外部获得{accessory.GetPlayerJobById(id)}为{marker}", _debugMode);
    }
    
    public void SetMarkerBySelf(uint id, MarkType marker, ScriptAccessory accessory)
    {
        var idx = accessory.GetPlayerIdIndex(id);
        Marker[idx] = marker;
        accessory.DebugMsg($"于内部设置{accessory.GetPlayerJobById(id)}为{marker}", _debugMode);
    }

    public void SetTowerType(DataId towerId, Vector3 towerPos, Vector3 center, ScriptAccessory accessory)
    {
        var pos = towerPos.Position2Dirs(center, 16);
        var towerType = towerId switch
        {
            DataId.SingleTower => 1,
            DataId.PartnerTower => 2,
            _ => 0
        };
        TowerType[pos] = towerType;
        accessory.DebugMsg($"检测到方位{pos}的{towerType}人塔", _debugMode);
    }
    
    public bool IsMarkered(int idx)
    {
        return Marker[idx] != MarkType.None;
    }

    public bool IsRemoteGlitch()
    {
        return GlitchType;
    }

    public void SetGlitchType(StatusId type)
    {
        GlitchType = type == StatusId.RemoteGlitch;
    }

    public List<MarkType> GetMarkers()
    {
        return Marker;
    }

    public List<int> GetSpreadTargetPos()
    {
        return SpreadTargetPos;
    }

    public MarkType GetMarkerTypeFromIdx(int idx)
    {
        return Marker[idx];
    }

    public List<Vector3> GetSpreadPosV3()
    {
        return SpreadPosV3;
    }
    public List<Vector3> GetTargetTowerPosV3()
    {
        return TargetTowerPosV3;
    }
    public bool SonyRecordedDone()
    {
        return SonyGroupCount == 8;
    }
    public bool TargetRecordedDone()
    {
        return IsTargeted.Count(x => x) == 6;
    }
}

public class SigmaWorld(bool debugMode)
{
    private readonly bool _debugMode = debugMode;
    private int OmegaFemalePos { get; set; } = 0;
    private bool OmegaFemaleInsideSafe { get; set; } = false;
    private List<int>? _dynamicBuffLevel;

    public void CalcMarker()
    {
        // GetDynamicBuffLevel(_dyn);
    }

    private void GetDynamicBuffLevel(DynamicsPass dyn)
    {
        _dynamicBuffLevel = dyn.GetBuffLevelList();
    }
}

public class DynamicsPass(bool debugMode)
{
    private readonly bool _debugMode = debugMode;
    private int DynamicIdx { get; set; } = 0;
    private List<int> BuffLevel { get; set; } = [0, 0, 0, 0, 0, 0, 0, 0];
    private uint FarSource { get; set; } = 0;
    private uint NearSource { get; set; } = 0;
    private List<uint> FarTarget { get; set; } = [];
    private List<uint> NearTarget { get; set; } = [];
    private List<uint> FreeTarget { get; set; } = [];
    
    public void DynamicIdxAdd()
    {
        DynamicIdx++;
    }

    public void Reset()
    {
        FarSource = 0;
        NearSource = 0;
        FreeTarget.Clear();
        FarTarget.Clear();
        NearTarget.Clear();
    }
    public List<int> GetBuffLevelList()
    {
        return BuffLevel;
    }
    public void AddDynamicBuffLevel(int idx)
    {
        BuffLevel[idx]++;
    }
    public int GetDynamicBuffLevelCount(int idx)
    {
        return BuffLevel[idx];
    }
    public void SetFarSource(uint id)
    {
        FarSource = id;
    }
    public void SetNearSource(uint id)
    {
        NearSource = id;
    }
}

public enum TopPhase : uint
{
    Init,                   // 初始
    P5_Delta,               // P5 一运
    P5_Sigma,               // P5 二运
    P5_Omega,               // P5 三运
    P5_BlindFaith,          // P5 盲信
}

public enum ActionId : uint
{
    RunDeltaVersion = 31624,
    RunSigmaVersion = 32788,
    RunOmegaVersion = 32789,
    BlindFaith = 31623,
}

public enum IconId : uint
{
    None = 0,
    IconCircle = 416,
    IconTriangle = 417,
    IconSquare = 418,
    IconCross = 419,
    SigmaCannon = 244,
    
    WaveCannonKyrios = 23, // player
    SolarRay = 343, // player
    Spotlight = 100, // player
    OptimizedMeteor = 346, // player
    RotateCW = 156, // LeftArmUnit/RightArmUnit
    RotateCCW = 157, // LeftArmUnit/RightArmUnit
    
}

public enum TetherId : uint
{
    LocalTetherPrep = 200,
    RemoteTetherPrep = 201,
    LocalTether = 224,
    RemoteTether = 225,
    
    Blaster = 89, // player->Boss
    PartySynergy = 222, // player->player
    OptimizedBladedance = 84, // OmegaFHelper/OmegaMHelper->player
    SigmaHyperPulse = 17, // RightArmUnit->player
}

public enum StatusId : uint
{
    NearWorld = 3442,
    FarWorld = 3443,
    Dynamis = 3444,
    MidGlitch = 3427,
    RemoteGlitch = 3428,
    
    OversampledWaveCannonLoadingR = 3452, // none->player, extra=0x0, cleaves right side
    OversampledWaveCannonLoadingL = 3453, // none->player, extra=0x0, cleaves left side
}

public enum DataId : uint
{
    SingleTower = 2013245,
    PartnerTower = 2013246,
}

public enum RecordedIdx : int
{
    SigmaSonyMarker = 0,
}

#region 函数集

// private ManualResetEvent _event = new(false);
// _event.Set();
// _event.Reset();
// _event.WaitOne();

public static class EventExtensions
{
    private static bool ParseHexId(string? idStr, out uint id)
    {
        id = 0;
        if (string.IsNullOrEmpty(idStr)) return false;
        try
        {
            var idStr2 = idStr.Replace("0x", "");
            id = uint.Parse(idStr2, System.Globalization.NumberStyles.HexNumber);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static uint ActionId(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["ActionId"]);
    }

    public static uint SourceId(this Event @event)
    {
        return ParseHexId(@event["SourceId"], out var id) ? id : 0;
    }

    public static uint SourceDataId(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["SourceDataId"]);
    }

    public static uint TargetId(this Event @event)
    {
        return ParseHexId(@event["TargetId"], out var id) ? id : 0;
    }

    public static uint TargetIndex(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["TargetIndex"]);
    }

    public static Vector3 SourcePosition(this Event @event)
    {
        return JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
    }

    public static Vector3 TargetPosition(this Event @event)
    {
        return JsonConvert.DeserializeObject<Vector3>(@event["TargetPosition"]);
    }

    public static Vector3 EffectPosition(this Event @event)
    {
        return JsonConvert.DeserializeObject<Vector3>(@event["EffectPosition"]);
    }

    public static float SourceRotation(this Event @event)
    {
        return JsonConvert.DeserializeObject<float>(@event["SourceRotation"]);
    }

    public static float TargetRotation(this Event @event)
    {
        return JsonConvert.DeserializeObject<float>(@event["TargetRotation"]);
    }

    public static string SourceName(this Event @event)
    {
        return @event["SourceName"];
    }

    public static string TargetName(this Event @event)
    {
        return @event["TargetName"];
    }

    public static uint DurationMilliseconds(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["DurationMilliseconds"]);
    }

    public static uint Index(this Event @event)
    {
        return ParseHexId(@event["Index"], out var id) ? id : 0;
    }

    public static uint State(this Event @event)
    {
        return ParseHexId(@event["State"], out var id) ? id : 0;
    }

    public static uint DirectorId(this Event @event)
    {
        return ParseHexId(@event["DirectorId"], out var id) ? id : 0;
    }
    public static uint Id(this Event @event)
    {
        return ParseHexId(@event["Id"], out var id) ? id : 0;
    }
    
    public static uint DataId(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["DataId"]);
    }

    public static uint StatusId(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["StatusID"]);
    }

    public static uint StackCount(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["StackCount"]);
    }

    public static uint Param(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["Param"]);
    }
}

public static class IbcHelper
{
    public static IBattleChara? GetById(uint id)
    {
        return (IBattleChara?)Svc.Objects.SearchByEntityId(id);
    }

    public static IBattleChara? GetMe()
    {
        return Svc.ClientState.LocalPlayer;
    }

    public static IEnumerable<IGameObject?> GetByDataId(uint dataId)
    {
        return Svc.Objects.Where(x => x.DataId == dataId);
    }

    public static uint GetCharHpcur(uint id)
    {
        // 如果null，返回0
        var hp = GetById(id)?.CurrentHp ?? 0;
        return hp;
    }

    public static bool IsTank(uint id)
    {
        var chara = GetById(id);
        if (chara == null) return false;
        return chara.GetRole() == CombatRole.Tank;
    }
    public static bool IsHealer(uint id)
    {
        var chara = GetById(id);
        if (chara == null) return false;
        return chara.GetRole() == CombatRole.Healer;
    }
    public static bool IsDps(uint id)
    {
        var chara = GetById(id);
        if (chara == null) return false;
        return chara.GetRole() == CombatRole.DPS;
    }
    public static bool AtNorth(uint id, float centerZ)
    {
        var chara = GetById(id);
        if (chara == null) return false;
        return chara.Position.Z <= centerZ;
    }
    public static bool AtSouth(uint id, float centerZ)
    {
        var chara = GetById(id);
        if (chara == null) return false;
        return chara.Position.Z > centerZ;
    }
    public static bool AtWest(uint id, float centerX)
    {
        var chara = GetById(id);
        if (chara == null) return false;
        return chara.Position.X <= centerX;
    }
    public static bool AtEast(uint id, float centerX)
    {
        var chara = GetById(id);
        if (chara == null) return false;
        return chara.Position.X > centerX;
    }
}

public static class DirectionCalc
{
    // 以北为0建立list
    // Game         List    Logic
    // 0            - 4     pi
    // 0.25 pi      - 3     0.75pi
    // 0.5 pi       - 2     0.5pi
    // 0.75 pi      - 1     0.25pi
    // pi           - 0     0
    // 1.25 pi      - 7     1.75pi
    // 1.5 pi       - 6     1.5pi
    // 1.75 pi      - 5     1.25pi
    // Logic = Pi - Game (+ 2pi)

    /// <summary>
    /// 将游戏基角度（以南为0，逆时针增加）转为逻辑基角度（以北为0，顺时针增加）
    /// 算法与Logic2Game完全相同，但为了代码可读性，便于区分。
    /// </summary>
    /// <param name="radian">游戏基角度</param>
    /// <returns>逻辑基角度</returns>
    public static float Game2Logic(this float radian)
    {
        // if (r < 0) r = (float)(r + 2 * Math.PI);
        // if (r > 2 * Math.PI) r = (float)(r - 2 * Math.PI);

        var r = float.Pi - radian;
        r = (r + float.Pi * 2) % (float.Pi * 2);
        return r;
    }

    /// <summary>
    /// 将逻辑基角度（以北为0，顺时针增加）转为游戏基角度（以南为0，逆时针增加）
    /// 算法与Game2Logic完全相同，但为了代码可读性，便于区分。
    /// </summary>
    /// <param name="radian">逻辑基角度</param>
    /// <returns>游戏基角度</returns>
    public static float Logic2Game(this float radian)
    {
        // var r = (float)Math.PI - radian;
        // if (r < Math.PI) r = (float)(r + 2 * Math.PI);
        // if (r > Math.PI) r = (float)(r - 2 * Math.PI);

        return radian.Game2Logic();
    }

    /// <summary>
    /// 输入逻辑基角度，获取逻辑方位（斜分割以正上为0，正分割以右上为0，顺时针增加）
    /// </summary>
    /// <param name="radian">逻辑基角度</param>
    /// <param name="dirs">方位总数</param>
    /// <param name="diagDivision">斜分割，默认true</param>
    /// <returns>逻辑基角度对应的逻辑方位</returns>
    public static int Rad2Dirs(this float radian, int dirs, bool diagDivision = true)
    {
        var r = diagDivision
            ? Math.Round(radian / (2f * float.Pi / dirs))
            : Math.Floor(radian / (2f * float.Pi / dirs));
        r = (r + dirs) % dirs;
        return (int)r;
    }

    /// <summary>
    /// 输入坐标，获取逻辑方位（斜分割以正上为0，正分割以右上为0，顺时针增加）
    /// </summary>
    /// <param name="point">坐标点</param>
    /// <param name="center">中心点</param>
    /// <param name="dirs">方位总数</param>
    /// <param name="diagDivision">斜分割，默认true</param>
    /// <returns>该坐标点对应的逻辑方位</returns>
    public static int Position2Dirs(this Vector3 point, Vector3 center, int dirs, bool diagDivision = true)
    {
        double dirsDouble = dirs;
        var r = diagDivision
            ? Math.Round(dirsDouble / 2 - dirsDouble / 2 * Math.Atan2(point.X - center.X, point.Z - center.Z) / Math.PI) % dirsDouble
            : Math.Floor(dirsDouble / 2 - dirsDouble / 2 * Math.Atan2(point.X - center.X, point.Z - center.Z) / Math.PI) % dirsDouble;
        return (int)r;
    }

    /// <summary>
    /// 以逻辑基弧度旋转某点
    /// </summary>
    /// <param name="point">待旋转点坐标</param>
    /// <param name="center">中心</param>
    /// <param name="radian">旋转弧度</param>
    /// <returns>旋转后坐标点</returns>
    public static Vector3 RotatePoint(this Vector3 point, Vector3 center, float radian)
    {
        // 围绕某点顺时针旋转某弧度
        Vector2 v2 = new(point.X - center.X, point.Z - center.Z);
        var rot = MathF.PI - MathF.Atan2(v2.X, v2.Y) + radian;
        var length = v2.Length();
        return new Vector3(center.X + MathF.Sin(rot) * length, center.Y, center.Z - MathF.Cos(rot) * length);
    }

    /// <summary>
    /// 以逻辑基角度从某中心点向外延伸
    /// </summary>
    /// <param name="center">待延伸中心点</param>
    /// <param name="radian">旋转弧度</param>
    /// <param name="length">延伸长度</param>
    /// <returns>延伸后坐标点</returns>
    public static Vector3 ExtendPoint(this Vector3 center, float radian, float length)
    {
        // 令某点以某弧度延伸一定长度
        return new Vector3(center.X + MathF.Sin(radian) * length, center.Y, center.Z - MathF.Cos(radian) * length);
    }

    /// <summary>
    /// 寻找外侧某点到中心的逻辑基弧度
    /// </summary>
    /// <param name="center">中心</param>
    /// <param name="newPoint">外侧点</param>
    /// <returns>外侧点到中心的逻辑基弧度</returns>
    public static float FindRadian(this Vector3 newPoint, Vector3 center)
    {
        var radian = MathF.PI - MathF.Atan2(newPoint.X - center.X, newPoint.Z - center.Z);
        if (radian < 0)
            radian += 2 * MathF.PI;
        return radian;
    }

    /// <summary>
    /// 将输入点左右折叠
    /// </summary>
    /// <param name="point">待折叠点</param>
    /// <param name="centerX">中心折线坐标点</param>
    /// <returns></returns>
    public static Vector3 FoldPointHorizon(this Vector3 point, float centerX)
    {
        return point with { X = 2 * centerX - point.X };
    }

    /// <summary>
    /// 将输入点上下折叠
    /// </summary>
    /// <param name="point">待折叠点</param>
    /// <param name="centerZ">中心折线坐标点</param>
    /// <returns></returns>
    public static Vector3 FoldPointVertical(this Vector3 point, float centerZ)
    {
        return point with { Z = 2 * centerZ - point.Z };
    }
    
    /// <summary>
    /// 将输入点朝某中心点往内/外同角度延伸，默认向内
    /// </summary>
    /// <param name="point">待延伸点</param>
    /// <param name="center">中心点</param>
    /// <param name="length">延伸长度</param>
    /// <param name="isOutside">是否向外延伸</param>>
    /// <returns></returns>
    public static Vector3 PointInOutside(this Vector3 point, Vector3 center, float length, bool isOutside = false)
    {
        Vector2 v2 = new(point.X - center.X, point.Z - center.Z);
        var targetPos = (point - center) / v2.Length() * length * (isOutside ? 1 : -1) + point;
        return targetPos;
    }
}

public static class IndexHelper
{
    /// <summary>
    /// 输入玩家dataId，获得对应的位置index
    /// </summary>
    /// <param name="pid">玩家SourceId</param>
    /// <param name="accessory"></param>
    /// <returns>该玩家对应的位置index</returns>
    public static int GetPlayerIdIndex(this ScriptAccessory accessory, uint pid)
    {
        // 获得玩家 IDX
        return accessory.Data.PartyList.IndexOf(pid);
    }

    /// <summary>
    /// 获得主视角玩家对应的位置index
    /// </summary>
    /// <param name="accessory"></param>
    /// <returns>主视角玩家对应的位置index</returns>
    public static int GetMyIndex(this ScriptAccessory accessory)
    {
        return accessory.Data.PartyList.IndexOf(accessory.Data.Me);
    }

    /// <summary>
    /// 输入玩家dataId，获得对应的位置称呼，输出字符仅作文字输出用
    /// </summary>
    /// <param name="pid">玩家SourceId</param>
    /// <param name="accessory"></param>
    /// <returns>该玩家对应的位置称呼</returns>
    public static string GetPlayerJobById(this ScriptAccessory accessory, uint pid)
    {
        // 获得玩家职能简称，无用处，仅作DEBUG输出
        var idx = accessory.Data.PartyList.IndexOf(pid);
        var str = accessory.GetPlayerJobByIndex(idx);
        return str;
    }

    /// <summary>
    /// 输入位置index，获得对应的位置称呼，输出字符仅作文字输出用
    /// </summary>
    /// <param name="idx">位置index</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static string GetPlayerJobByIndex(this ScriptAccessory accessory, int idx)
    {
        var str = idx switch
        {
            0 => "MT",
            1 => "ST",
            2 => "H1",
            3 => "H2",
            4 => "D1",
            5 => "D2",
            6 => "D3",
            7 => "D4",
            _ => "unknown"
        };
        return str;
    }
}

public static class ColorHelper
{
    public static ScriptColor ColorRed = new ScriptColor { V4 = new Vector4(1.0f, 0f, 0f, 1.0f) };
    public static ScriptColor ColorPink = new ScriptColor { V4 = new Vector4(1f, 0f, 1f, 1.0f) };
    public static ScriptColor ColorCyan = new ScriptColor { V4 = new Vector4(0f, 1f, 0.8f, 1.0f) };
    public static ScriptColor ColorDark = new ScriptColor { V4 = new Vector4(0f, 0f, 0f, 1.0f) };
    public static ScriptColor ColorLightBlue = new ScriptColor { V4 = new Vector4(0.48f, 0.40f, 0.93f, 1.0f) };
    public static ScriptColor ColorWhite = new ScriptColor { V4 = new Vector4(1f, 1f, 1f, 2f) };
    public static ScriptColor ColorYellow = new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 0f, 1.0f) };

}

public static class AssignDp
{
    /// <summary>
    /// 返回箭头指引相关dp
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="ownerObj">箭头起始，可输入uint或Vector3</param>
    /// <param name="targetObj">箭头指向目标，可输入uint或Vector3，为0则无目标</param>
    /// <param name="delay">绘图出现延时</param>
    /// <param name="destroy">绘图消失时间</param>
    /// <param name="name">绘图名称</param>
    /// <param name="rotation">箭头旋转角度</param>
    /// <param name="scale">箭头宽度</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static DrawPropertiesEdit DrawGuidance(this ScriptAccessory accessory, 
        object ownerObj, object targetObj, int delay, int destroy, string name, float rotation = 0, float scale = 1f)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(scale);
        dp.Rotation = rotation;
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        
        switch (ownerObj)
        {
            case uint sid:
                dp.Owner = sid;
                break;
            case Vector3 spos:
                dp.Position = spos;
                break;
            default:
                throw new ArgumentException("ownerObj的目标类型输入错误");
        }

        switch (targetObj)
        {
            case uint tid:
                if (tid != 0) dp.TargetObject = tid;
                break;
            case Vector3 tpos:
                dp.TargetPosition = tpos;
                break;
        }

        return dp;
    }
    
    public static DrawPropertiesEdit DrawGuidance(this ScriptAccessory accessory, 
        object targetObj, int delay, int destroy, string name, float rotation = 0, float scale = 1f)
    {
        return accessory.DrawGuidance(accessory.Data.Me, targetObj, delay, destroy, name, rotation, scale);
    }
    
    /// <summary>
    /// 返回扇形左右刀
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="ownerId">起始目标id，通常为boss</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="isLeftCleave">是左刀</param>
    /// <param name="radian">扇形角度</param>
    /// <param name="scale">扇形尺寸</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawLeftRightCleave(this ScriptAccessory accessory, uint ownerId, bool isLeftCleave, int delay, int destroy, string name, float radian = float.Pi, float scale = 60f, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(scale);
        dp.Radian = radian;
        dp.Rotation = isLeftCleave ? float.Pi / 2 : -float.Pi / 2;
        dp.Owner = ownerId;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }

    /// <summary>
    /// 返回扇形前后刀
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="ownerId">起始目标id，通常为boss</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="isFrontCleave">是前刀</param>
    /// <param name="radian">扇形角度</param>
    /// <param name="scale">扇形尺寸</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawFrontBackCleave(this ScriptAccessory accessory, uint ownerId, bool isFrontCleave, int delay, int destroy, string name, float radian = float.Pi, float scale = 60f, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(scale);
        dp.Radian = radian;
        dp.Rotation = isFrontCleave ? 0 : -float.Pi;
        dp.Owner = ownerId;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }

    /// <summary>
    /// 返回距离某对象目标最近/最远的dp
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="ownerId">起始目标id，通常为boss</param>
    /// <param name="orderIdx">顺序，从1开始</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="width">绘图宽度</param>
    /// <param name="length">绘图长度</param>
    /// <param name="isNear">true为最近，false为最远</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="lengthByDistance">长度是否随距离改变</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawTargetNearFarOrder(this ScriptAccessory accessory, uint ownerId, uint orderIdx,
        bool isNear, float width, float length, int delay, int destroy, string name, bool lengthByDistance = false, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.Owner = ownerId;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.CentreResolvePattern =
            isNear ? PositionResolvePatternEnum.PlayerNearestOrder : PositionResolvePatternEnum.PlayerFarestOrder;
        dp.CentreOrderIndex = orderIdx;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.ScaleMode |= lengthByDistance ? ScaleMode.YByDistance : ScaleMode.None;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }

    /// <summary>
    /// 返回距离某坐标位置最近/最远的dp
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="position">特定坐标点</param>
    /// <param name="orderIdx">顺序，从1开始</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="width">绘图宽度</param>
    /// <param name="length">绘图长度</param>
    /// <param name="isNear">true为最近，false为最远</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="lengthByDistance">长度是否随距离改变</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawPositionNearFarOrder(this ScriptAccessory accessory, Vector3 position, uint orderIdx,
        bool isNear, float width, float length, int delay, int destroy, string name, bool lengthByDistance = false, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.Position = position;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.TargetResolvePattern =
            isNear ? PositionResolvePatternEnum.PlayerNearestOrder : PositionResolvePatternEnum.PlayerFarestOrder;
        dp.TargetOrderIndex = orderIdx;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.ScaleMode |= lengthByDistance ? ScaleMode.YByDistance : ScaleMode.None;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }

    /// <summary>
    /// 返回ownerId施法目标的dp
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="ownerId">起始目标id，通常为boss</param>
    /// <param name="width">绘图宽度</param>
    /// <param name="length">绘图长度</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="lengthByDistance">长度是否随距离改变</param>
    /// <param name="name">绘图名称</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawOwnersTarget(this ScriptAccessory accessory, uint ownerId, float width, float length, int delay,
        int destroy, string name, bool lengthByDistance = false, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.Owner = ownerId;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.TargetResolvePattern = PositionResolvePatternEnum.OwnerTarget;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.ScaleMode |= lengthByDistance ? ScaleMode.YByDistance : ScaleMode.None;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }

    /// <summary>
    /// 返回ownerId仇恨相关的dp
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="ownerId">起始目标id，通常为boss</param>
    /// <param name="orderIdx">仇恨顺序，从1开始</param>
    /// <param name="width">绘图宽度</param>
    /// <param name="length">绘图长度</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="lengthByDistance">长度是否随距离改变</param>
    /// <param name="name">绘图名称</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawOwnersEntityOrder(this ScriptAccessory accessory, uint ownerId, uint orderIdx, float width, float length, int delay, int destroy, string name, bool lengthByDistance = false, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.Owner = ownerId;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.CentreResolvePattern = PositionResolvePatternEnum.OwnerEnmityOrder;
        dp.CentreOrderIndex = orderIdx;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.ScaleMode |= lengthByDistance ? ScaleMode.YByDistance : ScaleMode.None;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }

    /// <summary>
    /// 返回owner与target的dp，可修改 dp.Owner, dp.TargetObject, dp.Scale
    /// </summary>
    /// <param name="ownerId">起始目标id，通常为自己</param>
    /// <param name="targetId">目标单位id</param>
    /// <param name="width">绘图宽度</param>
    /// <param name="length">绘图长度</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="lengthByDistance">长度是否随距离改变</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawTarget2Target(this ScriptAccessory accessory, uint ownerId, uint targetId, float width, float length, int delay, int destroy, string name, bool lengthByDistance = false, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.Owner = ownerId;
        dp.TargetObject = targetId;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.ScaleMode |= lengthByDistance ? ScaleMode.YByDistance : ScaleMode.None;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }

    /// <summary>
    /// 返回owner与target之间的连线dp，使用Line绘制
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="ownerId">起始目标id，通常为自己</param>
    /// <param name="targetId">目标单位id</param>
    /// <param name="scale">线条宽度</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawConnectionBetweenTargets(this ScriptAccessory accessory, uint ownerId,
        uint targetId, int delay, int destroy, string name, float scale = 1f)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(scale);
        dp.Owner = ownerId;
        dp.TargetObject = targetId;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.ScaleMode |= ScaleMode.YByDistance;
        return dp;
    }

    /// <summary>
    /// 返回圆形dp，跟随owner，可修改 dp.Owner, dp.Scale
    /// </summary>
    /// <param name="ownerId">起始目标id，通常为自己或Boss</param>
    /// <param name="scale">圆圈尺寸</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawCircle(this ScriptAccessory accessory, uint ownerId, float scale, int delay, int destroy, string name, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(scale);
        dp.Owner = ownerId;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }

    /// <summary>
    /// 返回环形dp，跟随owner，可修改 dp.Owner, dp.Scale
    /// </summary>
    /// <param name="ownerId">起始目标id，通常为自己或Boss</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="scale">外环实心尺寸</param>
    /// <param name="innerScale">内环空心尺寸</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawDonut(this ScriptAccessory accessory, uint ownerId, float scale, float innerScale, int delay, int destroy, string name, bool byTime = false)
    {
        var dp = accessory.DrawFan(ownerId, float.Pi * 2, 0, scale, innerScale, delay, destroy, name, byTime);
        return dp;
    }

    /// <summary>
    /// 返回静态dp，通常用于指引固定位置。可修改 dp.Position, dp.Rotation, dp.Scale
    /// </summary>
    /// <param name="center">绘图中心位置</param>
    /// <param name="radian">图形角度</param>
    /// <param name="rotation">旋转角度，以北为0度顺时针</param>
    /// <param name="width">绘图宽度</param>
    /// <param name="length">绘图长度</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawStatic(this ScriptAccessory accessory, Vector3 center, float radian, float rotation, float width, float length, int delay, int destroy, string name)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.Position = center;
        dp.Radian = radian;
        dp.Rotation = rotation.Logic2Game();
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        return dp;
    }

    /// <summary>
    /// 返回静态圆圈dp，通常用于指引固定位置。
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="center">圆圈中心位置</param>
    /// <param name="color">圆圈颜色</param>
    /// <param name="scale">圆圈尺寸，默认1.5f</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawStaticCircle(this ScriptAccessory accessory, Vector3 center, Vector4 color, int delay, int destroy, string name, float scale = 1.5f)
    {
        var dp = accessory.DrawStatic(center, 0, 0, scale, scale, delay, destroy, name);
        dp.Color = color;
        return dp;
    }

    /// <summary>
    /// 返回静态月环dp，通常用于指引固定位置。
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="center">月环中心位置</param>
    /// <param name="color">月环颜色</param>
    /// <param name="scale">月环外径，默认1.5f</param>
    /// <param name="innerscale">月环内径，默认scale-0.05f</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawStaticDonut(this ScriptAccessory accessory, Vector3 center, Vector4 color, int delay, int destroy, string name, float scale, float innerscale = 0)
    {
        var dp = accessory.DrawStatic(center, float.Pi * 2, 0, scale, scale, delay, destroy, name);
        dp.Color = color;
        dp.InnerScale = innerscale != 0f ? new Vector2(innerscale) : new Vector2(scale - 0.05f);
        return dp;
    }

    /// <summary>
    /// 返回矩形
    /// </summary>
    /// <param name="ownerId">起始目标id，通常为自己或Boss</param>
    /// <param name="width">矩形宽度</param>
    /// <param name="length">矩形长度</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawRect(this ScriptAccessory accessory, uint ownerId, float width, float length, int delay, int destroy, string name, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.Owner = ownerId;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }

    /// <summary>
    /// 返回扇形
    /// </summary>
    /// <param name="ownerId">起始目标id，通常为自己或Boss</param>
    /// <param name="radian">扇形弧度</param>
    /// <param name="rotation">图形旋转角度</param>
    /// <param name="scale">扇形尺寸</param>
    /// <param name="innerScale">扇形内环空心尺寸</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawFan(this ScriptAccessory accessory, uint ownerId, float radian, float rotation, float scale, float innerScale, int delay, int destroy, string name, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(scale);
        dp.InnerScale = new Vector2(innerScale);
        dp.Radian = radian;
        dp.Rotation = rotation;
        dp.Owner = ownerId;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }
    
    /// <summary>
    /// 返回击退
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="target">击退源，可输入uint或Vector3</param>
    /// <param name="width">击退绘图宽度</param>
    /// <param name="length">击退绘图长度/距离</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="ownerId">起始目标ID，通常为自己或其他玩家</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static DrawPropertiesEdit DrawKnockBack(this ScriptAccessory accessory, uint ownerId, object target, float length, int delay, int destroy, string name, float width = 1.5f, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.Owner = ownerId;
        switch (target)
        {
            // 根据传入的 tid 类型来决定是使用 TargetObject 还是 TargetPosition
            case uint tid:
                dp.TargetObject = tid; // 如果 tid 是 uint 类型
                break;
            case Vector3 tpos:
                dp.TargetPosition = tpos; // 如果 tid 是 Vector3 类型
                break;
            default:
                throw new ArgumentException("DrawKnockBack的目标类型输入错误");
        }
        dp.Rotation = float.Pi;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }
    
    public static DrawPropertiesEdit DrawKnockBack(this ScriptAccessory accessory, object target, float length, int delay, int destroy, string name, float width = 1.5f, bool byTime = false)
    {
        return accessory.DrawKnockBack(accessory.Data.Me, target, length, delay, destroy, name, width, byTime);
    }

    
    /// <summary>
    /// 返回背对
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="target">背对源，可输入uint或Vector3</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="ownerId">起始目标ID，通常为自己或其他玩家</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static DrawPropertiesEdit DrawSightAvoid(this ScriptAccessory accessory, uint ownerId, object target, int delay, int destroy, string name)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = ownerId;
        switch (target)
        {
            // 根据传入的 tid 类型来决定是使用 TargetObject 还是 TargetPosition
            case uint tid:
                dp.TargetObject = tid; // 如果 tid 是 uint 类型
                break;
            case Vector3 tpos:
                dp.TargetPosition = tpos; // 如果 tid 是 Vector3 类型
                break;
            default:
                throw new ArgumentException("DrawSightAvoid的目标类型输入错误");
        }
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        return dp;
    }
    
    public static DrawPropertiesEdit DrawSightAvoid(this ScriptAccessory accessory, object target, int delay, int destroy, string name)
    {
        return accessory.DrawSightAvoid(accessory.Data.Me, target, delay, destroy, name);
    }
    
    /// <summary>
    /// 外部用调试模式
    /// </summary>
    /// <param name="str"></param>
    /// <param name="debugMode"></param>
    /// <param name="accessory"></param>
    public static void DebugMsg(this ScriptAccessory accessory, string str, bool debugMode)
    {
        if (!debugMode)
            return;
        accessory.Method.SendChat($"/e [DEBUG] {str}");
    }
}

#endregion
