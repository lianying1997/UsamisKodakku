using System;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent.Struct;
using KodakkuAssist.Module.Draw;
using System.Collections.Generic;
using System.Threading;
using ECommons;
using System.Numerics;
using Newtonsoft.Json;
using System.Linq;
using System.ComponentModel;
using System.Xml.Linq;
using Dalamud.Utility.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.MathHelpers;
using KodakkuAssist.Module.Draw.Manager;
using KodakkuAssist.Module.GameOperate;

namespace UsamisKodakku.Scripts._06_EndWalker.TOP;

[ScriptType(name: Name, territorys: [1122], guid: "b688fd29-786e-4103-82a3-7ad786af433b", 
    version: Version, author: "Usami", note: NoteStr, updateInfo: UpdateInfo)]

// ^(?!.*\|F).*
// ^\[\w+\|[^|]+\|E\]\s\w+

public class TopPatch
{
    const string NoteStr =
    """
    基于K佬绝欧绘图脚本的个人向补充，
    请先按需求检查并设置“用户设置”栏目。
    
    v0.0.0.0
    集成功能：
    指挥模式  非指挥模式  经本地标点测试
    [o-o] P1 线塔
    [o-o] P1 全能
    [o-o] P2 索尼
    [o-o] P2 分摊换位
    [o-o] P3 TV
    [ooo] P4 分摊换位
    [xxo] P5 一运
    [xxo] P5 一传
    [xoo] P5 二运
    [ooo] P5 二传
    [ooo] P5 三运
    [ooo] P5 三传
    [ooo] P5 四传
    
    补丁功能：
    [o] P1 线塔 线大圈延迟出现
    [o] P2 找男人
    [o] P3 HW 不能贴贴的玩家
    [o] P3 HW 搭档提示
    [o] P3 HW 站位提示
    """;

    private const string Name = "TOP_Patch [欧米茄绝境验证战 补丁]";
    private const string Version = "0.0.0.0";
    private const string DebugVersion = "a";

    private const string UpdateInfo =
        """
        你好。
        这是测试。
        """;
    
    [UserSetting("Debug模式，非开发用请关闭")]
    public static bool DebugMode { get; set; } = false;
    [UserSetting("指挥模式")]
    public static bool CaptainMode { get; set; } = true;
    [UserSetting("站位提示圈绘图-普通颜色")]
    public static ScriptColor PosColorNormal { get; set; } = new() { V4 = new Vector4(1.0f, 1.0f, 1.0f, 1.0f) };
    [UserSetting("站位提示圈绘图-玩家站位颜色")]
    public static ScriptColor PosColorPlayer { get; set; } = new() { V4 = new Vector4(0.0f, 1.0f, 1.0f, 1.0f) };

    private static readonly bool LocalTest = true;
    private static readonly bool LocalStrTest = true;     // 本地不标点，仅用字符串表示。

    private static readonly Vector3 Center = new Vector3(100, 0, 100);
    private static TopPhase _phase = TopPhase.Init;
    private volatile List<bool> _recorded = new bool[20].ToList();      // 被记录flag
    private static DeltaVersion _dv = new();
    private static SigmaVersion _sv = new();
    private static SigmaWorld _sw = new();
    private static DynamicsPass _dyn = new();
    
    private List<ManualResetEvent> _events = Enumerable
        .Range(0, 20)
        .Select(_ => new ManualResetEvent(false))
        .ToList();
    
    public void Init(ScriptAccessory accessory)
    {
        accessory.DebugMsg($"Init {Name} v{Version}{DebugVersion} Success.\n{UpdateInfo}", DebugMode);
        _phase = TopPhase.Init;
        _events = Enumerable
            .Range(0, 20)
            .Select(_ => new ManualResetEvent(false))
            .ToList();
        
        LocalMarkClear(accessory);
        accessory.Method.MarkClear();
        accessory.Method.RemoveDraw(".*");
    }

    [ScriptMethod(name: "随时DEBUG用", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo"], userControl: false)]
    public void EchoDebugActive(Event @event, ScriptAccessory accessory)
    {
        if (!DebugMode) return;
        var msg = @event.Message();
        switch (msg)
        {
            case "=TST":
                
            {
                
                
            }
                
                accessory.DebugMsg($"Debug操作。", DebugMode);
                break;
            
            case "=CLEAR":
                accessory.DebugMsg($"删除绘图与标点 Local。", DebugMode);
                LocalMarkClear(accessory);
                accessory.Method.RemoveDraw(".*");
                break;
            
            case "=dv":
                _dv.PrintDeltaVersion();
                break;
            
            case "=dyn":
                _dyn.PrintDynamicPass();
                break;
        }
    }
    #region P5 全局
    
    [ScriptMethod(name: "潜能量层数记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:3444"], userControl: false)]
    public void P5_DynamicRecord(Event @event, ScriptAccessory accessory)
    {
        if (!IsInPhase5(_phase)) return;
        var tid = @event.TargetId();
        var tidx = accessory.GetPlayerIdIndex(tid);
        _dyn.AddDynamicBuffLevel(tidx);
    }
    
    [ScriptMethod(name: "世界记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(344[23])$"], userControl: false)]
    public void P5_HelloWorldFarNearRecord(Event @event, ScriptAccessory accessory)
    {
        if (!IsInPhase5(_phase)) return;
        lock (_dyn)
        {
            var stid = (StatusId)@event.StatusId();
            var tid = @event.TargetId();
            var tidx = accessory.GetPlayerIdIndex(tid);
            switch (stid)
            {
                case StatusId.FarWorld:
                    _dyn.SetFarSource(tidx);
                    break;
                case StatusId.NearWorld:
                    _dyn.SetNearSource(tidx);
                    break;
            }

            // 如果世界记录完毕，发送Set
            if (_dyn.WorldRecordComplete())
            {
                _events[(int)RecordedIdx.WorldRecord].Set();
                accessory.DebugMsg($"EventSet: 世界记录完毕", DebugMode);
            }
                
        }
    }
    
    #endregion P5 全局
    
    #region P5 一运
    
    [ScriptMethod(name: "----《P5 一运》----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloMyWorld"],
        userControl: true)]
    public void SplitLine_DeltaVersion(Event @event, ScriptAccessory accessory)
    {
    }
    
    [ScriptMethod(name: "一运 阶段转换", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(31624)$"], userControl: false)]
    public void P5_RunMi_Delta_PhaseRecord(Event @event, ScriptAccessory accessory)
    {
        _phase = TopPhase.P5_DeltaVersion;
        _dyn = new DynamicsPass();
        _dyn.Init(accessory);
        _dv = new DeltaVersion();
        _dv.Init(accessory);
        MarkClear(accessory);
        
        accessory.DebugMsg($"当前阶段为：{_phase}", DebugMode);
    }
    
    [ScriptMethod(name: "一运 远近线记录", eventType: EventTypeEnum.Tether, eventCondition: ["Id:regex:^(00C[89])$"], userControl: false)]
    public void P5_Delta_LocalRemoteTetherRecord(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_DeltaVersion) return;

        lock (_dv)
        {
            var tetherId = (TetherId)@event.Id();
            var targetId = @event.TargetId();
            var targetIdx = accessory.GetPlayerIdIndex(targetId);
            var sourceId = @event.SourceId();
            var sourceIdx = accessory.GetPlayerIdIndex(sourceId);
            
            switch (tetherId)
            {
                case TetherId.LocalTetherPrep:
                    _dv.LocalTetherTargetAdd(sourceIdx);
                    _dv.LocalTetherTargetAdd(targetIdx);
                    break;
                case TetherId.RemoteTetherPrep:
                    _dv.RemoteTetherTargetAdd(sourceIdx);
                    _dv.RemoteTetherTargetAdd(targetIdx);
                    break;
            }

            // 如果8人远近线记录完毕，发送Set
            if (_dv.TetherRecordComplete())
            {
                _events[(int)RecordedIdx.DeltaTetherRecord].Set();
                accessory.DebugMsg($"EventSet: 8人远近线记录完毕", DebugMode);
            }
                
        }
    }
    
    [ScriptMethod(name: "一运 远近世界记录交换检查", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(31624)$"], userControl: false)]
    public void P5_DeltaVersionSwapCheck(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_DeltaVersion) return;
        if (@event.TargetId() != accessory.Data.Me) return;
        
        // 接线与世界记录完毕后，检查是否交换
        _events[(int)RecordedIdx.DeltaTetherRecord].WaitOne(5000);
        _events[(int)RecordedIdx.WorldRecord].WaitOne(5000);

        _dv.SwapCheck();
        _events[(int)RecordedIdx.DeltaTetherRecord].Reset();
        _events[(int)RecordedIdx.WorldRecord].Reset();
        _events[(int)RecordedIdx.DeltaSwapChecked].Set();
        accessory.DebugMsg($"EventSet: DeltaSwap操作完毕", DebugMode);
    }
    
    [ScriptMethod(name: "一运 指挥模式计算头标", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(31624)$"], userControl: false)]
    public void P5_DeltaVersionMarker(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_DeltaVersion) return;
        if (@event.TargetId() != accessory.Data.Me) return;
        if (!CaptainMode) return;
        _events[(int)RecordedIdx.DeltaSwapChecked].WaitOne(5000);
        
        lock (_dv)
        {
            // 在指挥模式下，建立标点逻辑
            _dv.BuildDeltaMarker();
            MarkAllPlayers(_dv.GetMarkers(), accessory);
                
            // 标志Delta运动会标点完毕
            if (_dv.IsMarkedPlayerCountEqualsTo(8))
            {
                _events[(int)RecordedIdx.DeltaSwapChecked].Reset();
                _events[(int)RecordedIdx.DeltaMarkerComplete].Set();
                accessory.DebugMsg($"EventSet: 指挥模式 DeltaMarker操作完毕", DebugMode);
            }
        }
    }
    
    [ScriptMethod(name: "一运 非指挥模式记录头标", eventType: EventTypeEnum.Marker, eventCondition: ["Operate:Add", "Id:regex:^(0[123467])$"],
        userControl: false)]
    public void P5_DeltaVersionReceiveMarker(Event @event, ScriptAccessory accessory)
    {
        // 只取攻击1234与锁链12
        if (_phase != TopPhase.P5_DeltaVersion) return;
        if (CaptainMode) return;
        
        _events[(int)RecordedIdx.DeltaSwapChecked].WaitOne(5000);
        
        lock (_dv)
        {
            var mark = @event.Id();
            accessory.DebugMsg($"检测到@event.Id() {mark}");
            var tid = @event.TargetId();
            var tidx = accessory.GetPlayerIdIndex(tid);
            _dv.SetMarkerFromOut(tidx, (MarkType)mark);
            if (!_dv.IsMarkedPlayerCountEqualsTo(6)) return;

            // 近线组是否交换检查
            _dv.SwapLocalTetherCheck();
            // 空闲的两个标记由自己标
            _dv.BuildDeltaIdleMarker();
            
            if (!_dv.IsMarkedPlayerCountEqualsTo(8)) return;
            
            _events[(int)RecordedIdx.DeltaSwapChecked].Reset();
            _events[(int)RecordedIdx.DeltaMarkerComplete].Set();
            accessory.DebugMsg($"EventSet: 非指挥模式 DeltaMarker操作完毕", DebugMode);
        }
    }
    
    [ScriptMethod(name: "一运 定位光头", eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["SourceDataId:14669", "Id:7747"],
        userControl: false)]
    public void P5_DeltaVersionFindOmegaBald(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_DeltaVersion) return;
        var spos = @event.SourcePosition();
        var dir = spos.Position2Dirs(Center, 4);
        _dv.OmegaBaldDirection = dir;
        _dv.BeetleDirection = (dir + 2) % 4;
        
        _events[(int)RecordedIdx.OmegaBaldRecorded].Set();
        accessory.DebugMsg($"EventSet: OmegaBald记录完毕", DebugMode);
    }
    
    [ScriptMethod(name: "一运 初始位置指路 *", eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["SourceDataId:14669", "Id:7747"],
        userControl: true)]
    public void P5_DeltaVersionFirstGuidance(Event @event, ScriptAccessory accessory)
    {
        // var myIndex = accessory.GetMyIndex();
        // var marker = MarkType.Stop1;
        // var omegaBaldDirection = 2;
        
        if (_phase != TopPhase.P5_DeltaVersion) return;
        _events[(int)RecordedIdx.OmegaBaldRecorded].WaitOne(5000);
        _events[(int)RecordedIdx.DeltaMarkerComplete].WaitOne(2000);
        
        var myIndex = accessory.GetMyIndex();
        var marker = _dv.GetMarkers()[myIndex];
        var omegaBaldDirection = _dv.OmegaBaldDirection;
        var beetleDirection = _dv.BeetleDirection;

        Vector3 tpos1 = new(90f, 0, 94f);
        Vector3 tpos2 = new(110f, 0, 94f);

        tpos1 = marker switch
        {
            MarkType.Attack1 => tpos1.RotatePoint(Center, 90f.DegToRad() * beetleDirection),
            MarkType.Attack2 => tpos1.RotatePoint(Center, 90f.DegToRad() * beetleDirection),
            MarkType.Attack3 => (tpos1 - new Vector3(0, 0, 8)).RotatePoint(Center, 90f.DegToRad() *
                beetleDirection),
            MarkType.Attack4 => (tpos1 - new Vector3(0, 0, 8)).RotatePoint(Center, 90f.DegToRad() *
                beetleDirection),
            MarkType.Bind1 => tpos1.RotatePoint(Center, 90f.DegToRad() * omegaBaldDirection),
            MarkType.Stop1 => tpos1.RotatePoint(Center, 90f.DegToRad() * omegaBaldDirection),
            MarkType.Bind2 => (tpos1 - new Vector3(0, 0, 8)).RotatePoint(Center,
                90f.DegToRad() * omegaBaldDirection),
            MarkType.Stop2 => (tpos1 - new Vector3(0, 0, 8)).RotatePoint(Center,
                90f.DegToRad() * omegaBaldDirection),
            _ => new Vector3(100f, 0, 100f),
        };

        tpos2 = marker switch
        {
            MarkType.Attack1 => tpos2.RotatePoint(Center, 90f.DegToRad() * beetleDirection),
            MarkType.Attack2 => tpos2.RotatePoint(Center, 90f.DegToRad() * beetleDirection),
            MarkType.Attack3 => (tpos2 - new Vector3(0, 0, 8)).RotatePoint(Center, 90f.DegToRad() *
                beetleDirection),
            MarkType.Attack4 => (tpos2 - new Vector3(0, 0, 8)).RotatePoint(Center, 90f.DegToRad() *
                beetleDirection),
            MarkType.Bind1 => tpos2.RotatePoint(Center, 90f.DegToRad() * omegaBaldDirection),
            MarkType.Stop1 => tpos2.RotatePoint(Center, 90f.DegToRad() * omegaBaldDirection),
            MarkType.Bind2 => (tpos2 - new Vector3(0, 0, 8)).RotatePoint(Center,
                90f.DegToRad() * omegaBaldDirection),
            MarkType.Stop2 => (tpos2 - new Vector3(0, 0, 8)).RotatePoint(Center,
                90f.DegToRad() * omegaBaldDirection),
            _ => new Vector3(100f, 0, 100f),
        };

        var dp = accessory.DrawGuidance(tpos1, 0, 5000, $"一运待命地点1");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        dp = accessory.DrawGuidance(tpos2, 0, 5000, $"一运待命地点2");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

        _events[(int)RecordedIdx.OmegaBaldRecorded].Reset();
        _events[(int)RecordedIdx.DeltaMarkerComplete].Reset();
    }
    
    [ScriptMethod(name: "一运 记录拳头", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(157(09|10))$"], userControl: false)]
    public void P5_DeltaRocketPunchRecord(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_DeltaVersion) return;
        const uint blue = 15709;
        var dataid = @event.DataId();
        var spos = @event.SourcePosition();
        lock (_dv)
        {
            _dv.PunchCount++;
        
            // 找到自己的四分之一半场
            if (_dv.MyQuadrant == -1)
                _dv.MyQuadrant = (IbcHelper.GetById(accessory.Data.Me)?.Position ?? new Vector3(110, 0, 110)).Position2Dirs(Center, 4, false);
            if (spos.Position2Dirs(Center, 4, false) == _dv.MyQuadrant)
            {
                _dv.PunchCountAtMyQuadrant++;
                _dv.PunchColorAtMyQuadrant += dataid == blue ? 1 : -1;
            }

            if (_dv.PunchCount == 8)
            {
                _events[(int)RecordedIdx.PunchCountComplete].Set();
                accessory.Method.RemoveDraw($"一运待命地点.*");
            }
        }
    }
    
    [ScriptMethod(name: "一运 拳头待命指路 *", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(157(09|10))$"],
        userControl: true, suppress: 10000)]
    public void P5_DeltaRocketPunchGuidance(Event @event, ScriptAccessory accessory)
    {
        // var myIndex = accessory.GetMyIndex();
        // var myMarker = MarkType.Attack3;    // 我的头标
        //
        // _dv.MyQuadrant = 3;                 // 我在待命时的象限
        // _dv.PunchCountAtMyQuadrant = 3;     // 我在待命时，象限内拳头数量
        // _dv.PunchColorAtMyQuadrant = 0;     // 0代表象限内拳头异色，2或-2代表象限内拳头同色
        // _dv.OmegaBaldDirection = 2;         // 大光头的方位
        
        if (_phase != TopPhase.P5_DeltaVersion) return;
        _events[(int)RecordedIdx.PunchCountComplete].WaitOne(2000);
        var myIndex = accessory.GetMyIndex();
        var myMarker = _dv.GetMarkers()[myIndex];

        var isOutside = myMarker is MarkType.Attack3 or MarkType.Attack4 or MarkType.Bind2 or MarkType.Stop2;
        var isRemoteTetherOutside = myMarker is MarkType.Bind2 or MarkType.Stop2;
        
        // 找到第0象限内，标点靠内。会随着boss位置的变化而出现偏移。
        var tposOut = new Vector3(108.7f, 0f, 90f).RotatePoint(new Vector3(109.9f, 0f, 90.1f),
            _dv.OmegaBaldDirection % 2 == 1 ? -90f.DegToRad() : 0);
        var tposIn = new Vector3(102.7f, 0f, 90f).RotatePoint(new Vector3(109.9f, 0f, 90.1f),
            _dv.OmegaBaldDirection % 2 == 1 ? -90f.DegToRad() : 0);  // 外锁链

        var tposBase = isRemoteTetherOutside ? tposIn : tposOut;
        
        tposBase = _dv.MyQuadrant switch
        {
            1 => tposBase.FoldPointVertical(Center.Z),
            2 => tposBase.FoldPointVertical(Center.Z).FoldPointHorizon(Center.X),
            3 => tposBase.FoldPointHorizon(Center.X),
            _ => tposBase,
        };
        
        if (!isOutside)
        {
            // 此处的Outside是指标点是否偏大，靠场外。
            // 若玩家靠场内，无脑站象限点。
            
            var dp = accessory.DrawGuidance(tposBase, 0, 5000, $"一运拳头");
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            accessory.DebugMsg($"玩家靠场内，站第{_dv.MyQuadrant}象限点。", DebugMode);
        }
        else
        {
            // 若玩家为外场
            // 玩家所在象限拳头数量不为2，有人没预占位（可能有武士画家），需要自己观察，不作指路
            if (_dv.PunchCountAtMyQuadrant != 2)
            {
                accessory.Method.TextInfo($"观察同组拳头颜色是否交换。", 4000, true);
                accessory.DebugMsg($"第{_dv.MyQuadrant}象限内拳头数量错误，需自己观察。", DebugMode);
            }
            // 玩家所在象限拳头数量为2，且颜色值不为0，则拳头同色，需要换位。
            else if (_dv.PunchColorAtMyQuadrant != 0)
            {
                var tpos = _dv.OmegaBaldDirection % 2 == 1
                    ? tposBase.FoldPointVertical(Center.Z)
                    : tposBase.FoldPointHorizon(Center.X);
                var dp = accessory.DrawGuidance(tpos, 0, 5000, $"一运拳头");
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                accessory.DebugMsg($"玩家{_dv.MyQuadrant}象限内拳头同色，需要换位。", DebugMode);
            }
            else
            {
                // 无需换位，直接根据_dv.myQuadrant指向对应位置
                var dp = accessory.DrawGuidance(tposBase, 0, 5000, $"一运拳头");
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                accessory.DebugMsg($"玩家无需换位，站第{_dv.MyQuadrant}象限点。", DebugMode);
            }
        }
        
        _events[(int)RecordedIdx.PunchCountComplete].Reset();
    }
    
    [ScriptMethod(name: "一运 拳头旋转引导位置", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(009[CD])$"], userControl: true)]
    public void P5_DeltaArmUnitRotate(Event @event, ScriptAccessory accessory)
    {
        // uint id = 0x009C;
        // uint tid = 0x4000C420;
        // var tpos = IbcHelper.GetById(tid)?.Position ?? Center;
        
        if (_phase != TopPhase.P5_DeltaVersion) return;
        var id = @event.Id();
        var tid = @event.TargetId();
        var tpos = IbcHelper.GetById(tid)?.Position ?? Center;
        
        tpos = tpos.PointInOutside(Center, 1f);
        var baitPos = tpos.RotatePoint(Center, id == (uint)IconId.RotateCW ? -5f.DegToRad() : 5f.DegToRad());
        var dp = accessory.DrawStaticCircle(baitPos, accessory.Data.DefaultSafeColor.WithW(3f), 0, 5000, $"手臂单元转转", 0.5f);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
    }
    
    [ScriptMethod(name: "一运 玩家引导拳头指路", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31587"],
        userControl: true)]
    public void P5_DeltaMyArmUnitBiasGuidance(Event @event, ScriptAccessory accessory)
    {
        // var myIndex = accessory.GetMyIndex();
        // var myMarker = MarkType.Attack1;
        // var myPos = new Vector3(90f, 0, 90f);
        // _dv.OmegaBaldDirection = 2;
        
        if (_phase != TopPhase.P5_DeltaVersion) return;
        // 这一条判断会因为suppress被return回去，所以suppress的使用场景需注意，事件确认可被触发。否则就用bool。
        if (@event.TargetId() != accessory.Data.Me) return;
        if (_recorded[(int)RecordedIdx.ArmUnitGuidance]) return;
        _recorded[(int)RecordedIdx.ArmUnitGuidance] = true;
        
        var myIndex = accessory.GetMyIndex();
        var myMarker = _dv.GetMarkers()[myIndex];
        var myPos = @event.TargetPosition();
        
        var omegaBaldDirection12 = _dv.OmegaBaldDirection * 3;
        var omegaBaldPos = new Vector3(100, 0, 80).RotatePoint(Center, omegaBaldDirection12 * 30f.DegToRad());
        
        var isShieldTarget = myMarker is MarkType.Bind1 or MarkType.Stop1;
        var isOutside = myMarker is MarkType.Attack3 or MarkType.Attack4 or MarkType.Bind2 or MarkType.Stop2;
        var isAtRight = myPos.IsAtRight(omegaBaldPos, Center);
        var isBind = myMarker is MarkType.Bind1 or MarkType.Bind2 or MarkType.Stop1 or MarkType.Stop2;
        
        var val = 100 * (isOutside ? 1 : 0) + 10 * (isAtRight ? 1 : 0) + 1 * (isBind ? 1 : 0);
        
        _dv.MyArmUnit = val switch
        {
            111 => (omegaBaldDirection12 + 1) % 12,
            110 => (omegaBaldDirection12 + 5) % 12,
            101 => (omegaBaldDirection12 + 11) % 12,
            100 => (omegaBaldDirection12 + 7) % 12,
            10 => (omegaBaldDirection12 + 3) % 12,
            0 => (omegaBaldDirection12 + 9) % 12,
            
            11 => (omegaBaldDirection12 + 3) % 12,
            1 => (omegaBaldDirection12 + 9) % 12,
            
            _ => -1
        };

        // TODO 目前这一条同组靠内集合会触发，但指路未触发，需要研究一下是有什么bug。
        
        accessory.DebugMsg(!isShieldTarget ? $"玩家所需引导手臂单元位于方位{_dv.MyArmUnit}" : $"玩家需前往场中偏方位{_dv.MyArmUnit}",
            DebugMode);
        accessory.Method.TextInfo($"同组靠内集合，等待黄圈", 2000);

        if (!isShieldTarget)
        {
            accessory.DebugMsg($"向引导拳头位置绘图", DebugMode);
            var armUnitPos = new Vector3(100, 0, 84).RotatePoint(Center, _dv.MyArmUnit * 30f.DegToRad());
            var dp = accessory.DrawGuidance(armUnitPos, 1000, 3000, $"引导拳头指引", isSafe: false);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
        }
        else
        {
            accessory.DebugMsg($"向场中引导位置绘图", DebugMode);
            var armUnitPos = new Vector3(100, 0, 95).RotatePoint(Center, _dv.MyArmUnit * 30f.DegToRad());
            var dp = accessory.DrawGuidance(armUnitPos, 1000, 3000, $"场中引导指引", isSafe: false);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }
    }
    
    [ScriptMethod(name: "一运 玩家引导拳头指路删除", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31482"],
        userControl: false, suppress: 10000)]
    public void P5_DeltaMyArmUnitBiasRemove(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_DeltaVersion) return;
        accessory.Method.RemoveDraw($"引导拳头指引");
        accessory.Method.RemoveDraw($"场中引导指引");
    }
    
    [ScriptMethod(name: "一运 玩家场中盾引导指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31482"],
        userControl: true, suppress: 10000)]
    public void P5_DeltaOmegaCenterShieldBias(Event @event, ScriptAccessory accessory)
    {
        // var myIndex = accessory.GetMyIndex();
        // var myMarker = MarkType.Bind1;
        // _dv.MyArmUnit = 3;  // only 0, 3, 6, 9
        
        if (_phase != TopPhase.P5_DeltaVersion) return;
        var myIndex = accessory.GetMyIndex();
        var myMarker = _dv.GetMarkers()[myIndex];
        
        if (myMarker is not MarkType.Bind1 and not MarkType.Stop1) return;
        
        var centerBiasPos = new Vector3(100, 0, 95).RotatePoint(Center, _dv.MyArmUnit * 30f.DegToRad());
        var dp = accessory.DrawGuidance(centerBiasPos, 0, 3000, $"场中盾连击引导");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }
    
    [ScriptMethod(name: "一运 转转手引导后近线待命指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31600"],
        userControl: true, suppress: 10000)]
    public void P5_DeltaAfterArmUnitBiasGuidance(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_DeltaVersion) return;
        var myIndex = accessory.GetMyIndex();
        var myMarker = _dv.GetMarkers()[myIndex];

        if (myMarker is not MarkType.Attack1 and not MarkType.Attack2 
            and not MarkType.Attack3 and not MarkType.Attack4) return;
        
        var standByPos = new Vector3(100, 0, 86).
            RotatePoint(Center, MathF.Round((float)_dv.MyArmUnit * 2 / 3) * 45f.DegToRad());
        var dp = accessory.DrawGuidance(standByPos, 0, 3000, $"攻击头标标点待命");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }
    
    [ScriptMethod(name: "一运 光头左右扫描记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3163[89])$"],
        userControl: false)]
    public void P5_DeltaOmegaBaldCannonRecord(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_DeltaVersion) return;
        const uint right = 31638;
        _dv.OmegaBaldCannonType = @event.ActionId() == right ? 1 : 2;
    }
    
    [ScriptMethod(name: "一运 玩家小电视Buff记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(345[23])$"],
        userControl: false)]
    public void P5_DeltaPlayerCannonRecord(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_DeltaVersion) return;
        var tidx = accessory.GetPlayerIdIndex(@event.TargetId());
        _dv.PlayerCannonSource = tidx;
        const uint right = 3452;
        _dv.PlayerCannonType = @event.StatusId() == right ? 1 : 2;
    }
    
    [ScriptMethod(name: "一运 盾连击目标记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31528"],
        userControl: false)]
    public void P5_DeltaShieldTargetRecord(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_DeltaVersion) return;
        if (@event.TargetIndex() != 1) return;
        accessory.Method.RemoveDraw($"场中盾连击引导");
        
        var tidx = accessory.GetPlayerIdIndex(@event.TargetId());
        _dv.ShieldTarget = tidx;
        // 盾连击是一运流程的最后一环
        _events[(int)RecordedIdx.ShieldTargetRecorded].Set();
    }
    
    [ScriptMethod(name: "一运 分摊与小电视指路", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31528"],
        userControl: true)]
    public void P5_DeltaStackAndCannonGuidance(Event @event, ScriptAccessory accessory)
    {
        // var myIndex = accessory.GetMyIndex();
        // var myMarker = MarkType.Bind2;
        // _dv.PlayerCannonSource = myIndex;        // 小电视玩家Idx
        // _dv.ShieldTarget = myIndex;              // 盾连击玩家Idx
        // _dv.OmegaBaldDirection = 0;              // 光头位置
        // _dv.OmegaBaldCannonType = 2;             // 光头电视1右2左
        // _dv.PlayerCannonType = 1;                // 玩家电视1右2左
        
        if (_phase != TopPhase.P5_DeltaVersion) return;
        _events[(int)RecordedIdx.ShieldTargetRecorded].WaitOne(1000);
        var myIndex = accessory.GetMyIndex();
        var myMarker = _dv.GetMarkers()[myIndex];
        
        if (myMarker is MarkType.Attack1 or MarkType.Attack2 or MarkType.Attack3 or MarkType.Attack4)
        {
            // 近线组不参与讨论
            _events[(int)RecordedIdx.ShieldTargetRecorded].Reset();
            return;
        }
        
        // 盾连击目标是否等于小电视目标，玩家是否为小电视目标
        // 以光头在A，光头电视打右为基准，以光头为12点做旋转。
        var isSameTarget = _dv.PlayerCannonSource == _dv.ShieldTarget;
        
        // 如果盾连击目标与小电视目标相同，盾连击目标需往外一步，分摊目标需往内一步
        var shieldTargetPos = new Vector3(101, 0, 85) + (isSameTarget ? new Vector3(3.5f, 0, 0) : new Vector3(0, 0, 0));
        var stackPos = new Vector3(101, 0, 100) + (isSameTarget ? new Vector3(0, 0, 0) : new Vector3(3.5f, 0, 0));

        // _dv.OmegaBaldCannonType 1右2左
        if (_dv.OmegaBaldCannonType == 2)
        {
            // 左刀则折叠后再旋转
            shieldTargetPos = shieldTargetPos.FoldPointHorizon(Center.X);
            stackPos = stackPos.FoldPointHorizon(Center.X);
        }
        var rotateRad = _dv.OmegaBaldDirection * 90f.DegToRad();
        shieldTargetPos = shieldTargetPos.RotatePoint(Center, rotateRad);
        stackPos = stackPos.RotatePoint(Center, rotateRad);

        if (myIndex == _dv.ShieldTarget)
        {
            var dp = accessory.DrawGuidance(shieldTargetPos, 0, 5000, $"一运盾连击指路");
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }
        else
        {
            var dp = accessory.DrawGuidance(stackPos, 0, 5000, $"一运分摊指路");
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }

        if (myIndex == _dv.PlayerCannonSource)
        {
            var faceDir = (_dv.OmegaBaldDirection + (_dv.OmegaBaldCannonType != _dv.PlayerCannonType ? 2 : 0)) % 4;
            var dp = accessory.DrawStatic(accessory.Data.Me, null, 0, faceDir * 90f.DegToRad().Logic2Game(),
                1f, 4.5f, PosColorPlayer.V4, 0, 5000, $"小电视面向辅助-正确面向");
            dp.FixRotation = true;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dp);
            
            // 由于DrawStatic一般用于静态方案，所以在rotation中加入了Game2Logic。跟随单位的需要额外加上Game2Logic。
            var dp0 = accessory.DrawStatic(accessory.Data.Me, null, 0, 0f.Logic2Game(),
                1f, 4.5f, accessory.Data.DefaultDangerColor, 0, 5000, $"小电视面向辅助-自身");
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dp0);
            
            accessory.Method.TextInfo($"小电视，站在外侧", 3000, true);
        }
        else
            accessory.Method.TextInfo($"躲避小电视，站在内侧", 3000, true);
        
        _events[(int)RecordedIdx.ShieldTargetRecorded].Reset();
    }
    
    [ScriptMethod(name: "一运 绘图删除，准备一传", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(31529)$"],
        userControl: false)]
    public void P5_DeltaVersionComplete(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_DeltaVersion) return;
        _phase = TopPhase.P5_DeltaWorld;
        accessory.Method.RemoveDraw(".*");
        
        // 初始化_events
        _events = Enumerable
            .Range(0, 20)
            .Select(_ => new ManualResetEvent(false))
            .ToList();
        
        // 初始化_recorded
        _recorded = new bool[20].ToList();
        
        // 根据标点补充DynamicPass
        var markerList = _dv.GetMarkers();
        _dyn.FarTarget.Add(GetMarkedPlayerIndex(accessory, markerList, MarkType.Attack1));
        _dyn.FarTarget.Add(GetMarkedPlayerIndex(accessory, markerList, MarkType.Attack2));
        _dyn.NearTarget.Add(GetMarkedPlayerIndex(accessory, markerList, MarkType.Attack3));
        _dyn.NearTarget.Add(GetMarkedPlayerIndex(accessory, markerList, MarkType.Attack4));
        _dyn.IdleTarget.Add(GetMarkedPlayerIndex(accessory, markerList, MarkType.Stop1));
        _dyn.IdleTarget.Add(GetMarkedPlayerIndex(accessory, markerList, MarkType.Stop2));
    }
    
    #endregion P5 一运
    
    #region P5 一传
    
    [ScriptMethod(name: "----《P5 一传》----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloMyWorld"],
        userControl: true)]
    public void SplitLine_DeltaWorld(Event @event, ScriptAccessory accessory)
    {
    }
    
    [ScriptMethod(name: "一传 蟑螂左右刀记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3163[67])$"],
        userControl: false)]
    public void P5_DeltaBeetleSwipeRecord(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_DeltaWorld) return;
        const uint right = 31636;
        _dv.BeetleSwipe = @event.ActionId() == right ? 1 : 2;
        _events[(int)RecordedIdx.BeetleSwipeRecorded].Set();
    }
    
    [ScriptMethod(name: "一传 指路 *", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3163[67])$"],
        userControl: true)]
    public void P5_DeltaWorldGuidance(Event @event, ScriptAccessory accessory)
    {
        // var myMarker = MarkType.Bind2;
        // _dv.BeetleSwipe = 2;    // 1右2左
        // _dv.BeetleDirection = 1;
        
        if (_phase != TopPhase.P5_DeltaWorld) return;
        _events[(int)RecordedIdx.BeetleSwipeRecorded].WaitOne();
        var myIndex = accessory.GetMyIndex();
        var myMarker = _dv.GetMarkers()[myIndex];
        
        // 以蟑螂右刀，蟑螂在A为基准
        List<Vector3> posList =
        [
            new(102f, 0, 81f),          // Atk1 - FarTarget
            new(110.6f, 0, 116.2f),     // Atk2 - FarTarget
            new(108.9f, 0, 88.9f),      // Atk3 - NearTarget
            new(113.7f, 0, 86.3f),      // Atk4 - NearTarget
            new(119.5f, 0, 100f),       // Bind1 - FarSource
            new(106.5f, 0, 100f),       // Bind2 - NearSource
            new(116.2f, 0, 111f),       // Stop1 - Idle
            new(116.2f, 0, 111f)        // Stop2 - Idle
        ];

        var myPosIdx = myMarker switch
        {
            MarkType.Attack1 => 0,
            MarkType.Attack2 => 1,
            MarkType.Attack3 => 2,
            MarkType.Attack4 => 3,
            MarkType.Bind1 => 4,
            MarkType.Bind2 => 5,
            MarkType.Stop1 => 6,
            MarkType.Stop2 => 7,
            _ => -1,
        };
        
        if (myPosIdx == -1)
        {
            accessory.DebugMsg($"玩家标点{myMarker}读取错误", DebugMode);
            return;
        }

        // 根据蟑螂左右刀与所在方位旋转折叠
        var myPos = posList[myPosIdx];
        var isRightSwipe = _dv.BeetleSwipe == 1;
        if (!isRightSwipe)
            myPos = myPos.FoldPointHorizon(Center.X);
        myPos = myPos.RotatePoint(Center, _dv.BeetleDirection * 90f.DegToRad());

        var dp = accessory.DrawGuidance(myPos, 0, 5000, $"一传指路");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        _events[(int)RecordedIdx.BeetleSwipeRecorded].Reset();
    }
    
    [ScriptMethod(name: "一传 指路移除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(3163[67])$"],
        userControl: false)]
    public void P5_DeltaWorldGuidanceRemove(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_DeltaWorld) return;
        accessory.Method.RemoveDraw("一传指路");
    }
    
    [ScriptMethod(name: "一传 近线拉线提示", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:3529"],
        userControl: true)]
    public void P5_DeltaWorldLocalTetherBreakHint(Event @event, ScriptAccessory accessory)
    {
        // 在DeltaVersion期间建立了近线
        if (_phase != TopPhase.P5_DeltaVersion) return;

        // 在线变为实线前，标点已取好
        var myIndex = accessory.GetMyIndex();
        var myMarker = _dv.GetMarkers()[myIndex];
        if (myMarker is not MarkType.Attack1 and not MarkType.Attack2) return;
        var myPartner = GetMarkedPlayerIndex(accessory, _dv.GetMarkers(),
            myMarker is MarkType.Attack1 ? MarkType.Attack2 : MarkType.Attack1);
        
        var dur = (int)@event.DurationMilliseconds();
        // 在还剩10秒时，DeltaWorld正在执行，需在最后2秒拉断。

        var delay1 = dur - 8000;
        var destroy1 = 6000;
        var delay2 = dur - 2000;
        var destroy2 = 2000;

        // 近线实际距离约为10，取11
        var dp1 = accessory.DrawCircle(accessory.Data.PartyList[myPartner], 11, delay1, destroy1, $"近线别拉断");
        dp1.Color = accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp1);
        
        var dp2 = accessory.DrawCircle(accessory.Data.PartyList[myPartner], 11, delay2, destroy2, $"近线拉断");
        dp2.Color = accessory.Data.DefaultSafeColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp2);
    }
    
    public class DeltaVersion
    {
        public ScriptAccessory accessory { get; set; } = null!;
        // 数大在外，锁2近世界
        public List<int> RemoteTetherInside { get; set; } = [];
        public List<int> RemoteTetherOutside { get; set; } = [];
        public List<int> LocalTetherInside { get; set; } = [];
        public List<int> LocalTetherOutside { get; set; } = [];
        private List<MarkType> Marker { get; set; } = Enumerable.Repeat(MarkType.None, 8).ToList();
        private int MarkedPlayerCount { get; set; } = 0;
        public int OmegaBaldDirection { get; set; } = 0;
        public int BeetleDirection { get; set; } = 0;
        // 拳头计算
        public int PunchCount { get; set; } = 0;
        public int PunchCountAtMyQuadrant { get; set; } = 0;
        public int PunchColorAtMyQuadrant { get; set; } = 0;
        public int MyQuadrant { get; set; } = -1;
        public int MyArmUnit { get; set; } = -1;
        // 一运行动计算
        public int OmegaBaldCannonType { get; set; } = 0;
        public int PlayerCannonType { get; set; } = 0;
        public int PlayerCannonSource { get; set; } = 0;
        public int ShieldTarget { get; set; } = -1;
        public int BeetleSwipe { get; set; } = 0;

        public void Init(ScriptAccessory _accessory)
        {
            accessory = _accessory;
        }
        
        /// <summary>
        /// 添加近线玩家单位
        /// </summary>
        /// <param name="idx">玩家IDX</param>
        public void LocalTetherTargetAdd(int idx)
        {
            if (LocalTetherInside.Count < 2)
            {
                LocalTetherInside.Add(idx);
                LocalTetherInside.Sort();
            }
            else
            {
                LocalTetherOutside.Add(idx);
                LocalTetherOutside.Sort();
            }
        }
    
        /// <summary>
        /// 添加远线玩家单位
        /// </summary>
        /// <param name="idx">玩家IDX</param>
        public void RemoteTetherTargetAdd(int idx)
        {
            if (RemoteTetherInside.Count < 2)
            {
                RemoteTetherInside.Add(idx);
                RemoteTetherInside.Sort();
            }
            else
            {
                RemoteTetherOutside.Add(idx);
                RemoteTetherOutside.Sort();
            }
        }

        public bool TetherRecordComplete()
        {
            return RemoteTetherInside.Count == 2 && LocalTetherInside.Count == 2 &&
                   RemoteTetherOutside.Count == 2 && LocalTetherOutside.Count == 2;
        }

        public void SwapCheck()
        {
            // 检查是否需Swap
            // 远世界被标锁链1，若远世界目标处于RemoteTetherOutside内，需要交换。
            var farSourceIdx = _dyn.FarSource[0];
            var needSwap = RemoteTetherOutside.Contains(farSourceIdx);
            if (needSwap)
            {
                // RemoteTetherInside 与 RemoteTetherOutside 交换。
                (RemoteTetherInside, RemoteTetherOutside) = (RemoteTetherOutside, RemoteTetherInside);
                accessory.DebugMsg($"远世界被设定在外组，程序内交换。", DebugMode);
            }
            else
                accessory.DebugMsg($"远世界被设定在内组，程序内无需交换。", DebugMode);
        }

        public void BuildDeltaMarker()
        {
            // 先获取近远世界idx，标锁链1、2
            var farSourceIdx = _dyn.FarSource[0];
            SetMarkerBySelf(farSourceIdx, MarkType.Bind1);
            var nearSourceIdx = _dyn.NearSource[0];
            SetMarkerBySelf(nearSourceIdx, MarkType.Bind2);
            
            // 获取近线玩家，标攻击1、2、3、4
            SetMarkerBySelf(LocalTetherInside[0], MarkType.Attack1);
            SetMarkerBySelf(LocalTetherInside[1], MarkType.Attack2);
            SetMarkerBySelf(LocalTetherOutside[0], MarkType.Attack3);
            SetMarkerBySelf(LocalTetherOutside[1], MarkType.Attack4);
            
            BuildDeltaIdleMarker();
        }
        public void BuildDeltaIdleMarker()
        {
            var farSourceIdx = _dyn.FarSource[0];
            var nearSourceIdx = _dyn.NearSource[0];
            
            // 获取远世界搭档，标禁止1
            var farSourcePartnerIdx =
                RemoteTetherInside[0] == farSourceIdx ? RemoteTetherInside[1] : RemoteTetherInside[0];
            SetMarkerBySelf(farSourcePartnerIdx, MarkType.Stop1);
            
            // 获取近世界搭档，标禁止2
            var nearSourcePartnerIdx =
                RemoteTetherOutside[0] == nearSourceIdx ? RemoteTetherOutside[1] : RemoteTetherOutside[0];
            SetMarkerBySelf(nearSourcePartnerIdx, MarkType.Stop2);
        }
        
        public void SetMarkerFromOut(int idx, MarkType marker)
        {
            Marker[idx] = marker;
            MarkedPlayerCount++;
            accessory.DebugMsg($"从外部获得{accessory.GetPlayerJobByIndex(idx)}为{marker}, {MarkedPlayerCount}", DebugMode);
        }

        public void SwapLocalTetherCheck()
        {
            // 因为外部可能存在设置不同的情况，需要自己调整。
            
            // 攻击1若处于LocalTetherOutside内，需要交换。
            var localTetherInsidePlayer = GetMarkedPlayerIndex(accessory, Marker, MarkType.Attack1);
            var needSwap = LocalTetherOutside.Contains(localTetherInsidePlayer);
            if (needSwap)
            {
                // LocalTetherInside 与 LocalTetherOutside 交换。
                (LocalTetherInside, LocalTetherOutside) = (LocalTetherOutside, LocalTetherInside);
                accessory.DebugMsg($"近线内被设定在外组，程序内交换。", DebugMode);
            }
            else
                accessory.DebugMsg($"近线内被设定在内组，程序内无需交换。", DebugMode);
        }

        public void SetMarkerBySelf(int idx, MarkType marker)
        {
            Marker[idx] = marker;
            MarkedPlayerCount++;
            accessory.DebugMsg($"于内部设置{accessory.GetPlayerJobByIndex(idx)}为{marker}, {MarkedPlayerCount}", DebugMode);
        }

        public bool IsMarkedPlayerCountEqualsTo(int count)
        {
            return MarkedPlayerCount == count;
        }
        
        public List<MarkType> GetMarkers()
        {
            return Marker;
        }

        public void PrintDeltaVersion()
        {
            var str = "";
            str += "-----标点事件-----\n";
            str += $"远线靠内: {accessory.BuildListStr(RemoteTetherInside, true)}\n";
            str += $"远线靠外: {accessory.BuildListStr(RemoteTetherOutside, true)}\n";
            str += $"近线靠内: {accessory.BuildListStr(LocalTetherInside, true)}\n";
            str += $"近线靠外: {accessory.BuildListStr(LocalTetherOutside, true)}\n";
            str += $"队伍标点: {accessory.BuildListStr(Marker)}\n";
            str += $"光头位置: {OmegaBaldDirection}\n";
            str += $"蟑螂位置: {BeetleDirection}\n";
            str += $"记录已标点玩家数量: {MarkedPlayerCount}\n";
            accessory.DebugMsg(str, DebugMode);
            
            str = "";
            str += "-----预站位时事件-----\n";
            str += $"场上拳头数量: {PunchCount}\n";
            str += $"玩家待命时，所在象限{MyQuadrant}\n";
            str += $"玩家待命时，象限内拳头数量: {PunchCountAtMyQuadrant}\n";
            str += $"玩家待命时，象限内拳头颜色: {PunchColorAtMyQuadrant}\n";
            accessory.DebugMsg(str, DebugMode);
            
            str = "";
            str += "-----线变为实体后事件-----\n";
            str += $"玩家需引导的手臂单元所在方位(12)：{MyArmUnit}\n";
            str += $"被盾连击的攻击目标是：{ShieldTarget}({accessory.GetPlayerJobByIndex(ShieldTarget)})\n";
            str += $"光头的小电视是（1右2左，光头视角）：{OmegaBaldCannonType}\n";
            str += $"玩家小电视目标是：{PlayerCannonSource}({accessory.GetPlayerJobByIndex(PlayerCannonSource)})\n";
            str += $"玩家的小电视类型是（1右2左，玩家视角）：{PlayerCannonType}\n";
            str += $"蟑螂的左右刀是（1右2左，蟑螂视角）：{BeetleSwipe}\n";
            accessory.DebugMsg(str, DebugMode);
        }
    }
    
    #endregion P5 一传

    #region P5 二运
    
    [ScriptMethod(name: "----《P5 二运》----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloMyWorld"],
        userControl: true)]
    public void SplitLine_SigmaVersion(Event @event, ScriptAccessory accessory)
    {
    }
    
    [ScriptMethod(name: "二运 阶段转换", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(32788)$"], userControl: false)]
    public void P5_RunMi_Sigma_PhaseRecord(Event @event, ScriptAccessory accessory)
    {
        _phase = TopPhase.P5_SigmaVersion;
        _sv = new SigmaVersion();
        _sv.Init(accessory);
        _sw = new SigmaWorld();
        _sw.Init(accessory);
        _dyn.ClearWorldList();
        accessory.DebugMsg($"当前阶段为：{_phase}", DebugMode);
    }
    
    [ScriptMethod(name: "二运 中远线记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(342[78])$"], userControl: false)]
    public void P5_GlitchRecord(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_SigmaVersion) return;
        var stid = (StatusId)@event.StatusId();
        var tid = @event.TargetId();
        if (tid != accessory.Data.Me) return;
        _sv.SetGlitchType(stid);
        accessory.DebugMsg($"成功记录下{(_sv.IsRemoteGlitch() ? "远线" : "中线")}", DebugMode);
    }
    
    [ScriptMethod(name: "二运 索尼标记录", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(01A[0123])$"], userControl: false)]
    public void P5_SonyIconRecord(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_SigmaVersion) return;
        var id = (IconId)@event.Id();
        var tid = @event.TargetId();
        var tidx = accessory.GetPlayerIdIndex(tid);
        lock (_sv)
        {
            _sv.AddPlayerToGroup(tidx, id);
            if (_sv.SonyRecordedDone())
                _events[(int)RecordedIdx.SigmaSonyRecord].Set();
        }
    }
    
    [ScriptMethod(name: "二运 八方炮点名标记录", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(00F4)$"], userControl: false)]
    public void P5_SigmaCannonIconRecord(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_SigmaVersion) return;
        var tid = @event.TargetId();
        var tidx = accessory.GetPlayerIdIndex(tid);
        lock (_sv)
        {
            _sv.SetTargetedPlayer(tidx);
            if (_sv.TargetRecordedDone())
                _events[(int)RecordedIdx.SigmaTargetRecord].Set();
        }
    }
    
    [ScriptMethod(name: "二运 男人真北位置记录", eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["Id:7747", "SourceDataId:15720"], userControl: false)]
    public void P5_OmegaTrueNorthRecord(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_SigmaVersion) return;
        var spos = @event.SourcePosition();
        var pos = spos.Position2Dirs(Center, 16);
        _sv.SetSpreadTrueNorth(pos);
    }
    
    [ScriptMethod(name: "二运 索尼头标计算", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31603"], userControl: false)]
    public void P5_SigmaVersionMarker(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_SigmaVersion) return;
        lock (_events)
        {
            // 索尼标记、激光炮标记记录完毕后
            _events[(int)RecordedIdx.SigmaTargetRecord].WaitOne();
            _events[(int)RecordedIdx.SigmaSonyRecord].WaitOne();
        }

        lock (_sv)
        {
            // 找到未被点名的两人
            if (CaptainMode)
            {
                _sv.BuildUntargetedGroup();
                // 在指挥模式下，为两人标点
                // TODO：非指挥模式下，无需标点，可直接进行后续运算。但是算式逻辑待补充。
                _sv.BuildSimgaMarker();
            }
            MarkAllPlayers(_sv.GetMarkers(), accessory);
            _sv.FindSpreadTarget();
            _sv.CalcSpreadPos(Center);
        }

        lock (_events)
        {
            _events[(int)RecordedIdx.SigmaSonyMarker].Set();
        }
    }
    
    [ScriptMethod(name: "二运 八方分散指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31603"])]
    public void P5_SigmaVersionSpreadDir(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_SigmaVersion) return;
        
        _events[(int)RecordedIdx.SigmaSonyMarker].WaitOne();
        DrawSigmaSpreadSolution(accessory);
        _events[(int)RecordedIdx.SigmaSonyMarker].Reset();
        _events[(int)RecordedIdx.SigmaTargetRecord].Reset();
        _events[(int)RecordedIdx.SigmaSonyRecord].Reset();
    }
    
    [ScriptMethod(name: "二运 塔生成记录", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["DataId:regex:^(201324[56])$", "Operate:Add"], userControl: false)]
    public void P5_SigmaTowerRecord(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_SigmaVersion) return;
        var id = (DataId)@event.DataId();
        var spos = @event.SourcePosition();
        _sv.SetTowerType(id, spos, Center);
    }
    
    [ScriptMethod(name: "二运 塔计算与指路", eventType: EventTypeEnum.RemoveCombatant, eventCondition: ["DataId:14669"])]
    public void P5_SigmaTowerCalc(Event @event, ScriptAccessory accessory)
    {
        // 因为计算不对后续机制造成影响，所以可以放在一个事件检测里
        if (_phase != TopPhase.P5_SigmaVersion) return;
        _sv.BuildTargetTowerPos();
        _sv.CalcTargetTowerPos(Center);
        DrawSigmaTowerSolution(accessory);
    }
    
    [ScriptMethod(name: "二运 前半头标消除，计算二传头标", eventType: EventTypeEnum.KnockBack, userControl: false)]
    public void P5_SigmaVersionMarkClear(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_SigmaVersion) return;
        if (!CaptainMode) return;
        MarkClear(accessory);
        accessory.Method.RemoveDraw(".*");
        _sw.CalcMarker();
        // TODO 二传算头标
    }
    
    [ScriptMethod(name: "二传 标头标", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:3149[23]"], userControl: false)]
    public void P5_SigmaWorldMarker(Event @event, ScriptAccessory accessory)
    {
        if (_phase != TopPhase.P5_SigmaVersion) return;
        _phase = TopPhase.P5_SigmaWorld;
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
            MarkPlayerByIdx(accessory, i, marker[i]);
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

    public class SigmaVersion
    {
        public ScriptAccessory accessory { get; set; } = null!;
        private List<int> CircleGroup { get; set; } = [];
        private List<int> CrossGroup { get; set; } = [];
        private List<int> TriangleGroup { get; set; } = [];
        private List<int> SquareGroup { get; set; } = [];
        private List<bool> IsTargeted { get; set; } = new bool[8].ToList();
        private List<int> UntargetedGroup { get; set; } = [];
        private bool GlitchType { get; set; }
        private List<MarkType> Marker { get; set; } = Enumerable.Repeat(MarkType.None, 8).ToList();
        private List<int> TowerType { get; set; } = Enumerable.Repeat(0, 16).ToList();
        private List<int> SpreadTargetPos { get; set; } = Enumerable.Repeat(0, 8).ToList();
        private List<Vector3> SpreadPosV3 { get; set; } = Enumerable.Repeat(new Vector3(0, 0, 0), 8).ToList();
        private List<int> TargetTowerPos { get; set; } = Enumerable.Repeat(0, 8).ToList();
        private List<Vector3> TargetTowerPosV3 { get; set; } = Enumerable.Repeat(new Vector3(0, 0, 0), 8).ToList();
        private int SpreadTrueNorth { get; set; }
        public int SonyGroupCount { get; set; } = 0;
        public void Init(ScriptAccessory _accessory)
        {
            accessory = _accessory;
        }

        public void AddPlayerToGroup(int idx, IconId group)
        {
            switch (group)
            {
                case IconId.IconCircle:
                    CircleGroup.Add(idx);
                    break;
                case IconId.IconCross:
                    CrossGroup.Add(idx);
                    break;
                case IconId.IconTriangle:
                    TriangleGroup.Add(idx);
                    break;
                case IconId.IconSquare:
                    SquareGroup.Add(idx);
                    break;
            }

            SonyGroupCount++;
            accessory.DebugMsg($"添加{accessory.GetPlayerJobByIndex(idx)}到{group}", DebugMode);
        }

        public void BuildTargetTowerPos()
        {
            var str = "";
            for (var i = 0; i < 8; i++)
            {
                var towerIdx = FindTargetTower(i);
                if (towerIdx == -1)
                {
                    TargetTowerPos[i] = 0;
                    str += $"出现错误，{accessory.GetPlayerJobByIndex(i)}的塔未找到\n";
                }
                else
                {
                    TargetTowerPos[i] = towerIdx;
                    str += $"成功找到{accessory.GetPlayerJobByIndex(i)}的塔{towerIdx}\n";
                }
            }
            str += "---------SigmaVersion.BuildTargetTowerPos----------\n";
            accessory.DebugMsg(str, DebugMode);
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

        public void SetSpreadTrueNorth(int pos)
        {
            SpreadTrueNorth = pos;
            accessory.DebugMsg($"设置分散真北方向为{pos}", DebugMode);
        }

        public void SetTargetedPlayer(int idx)
        {
            IsTargeted[idx] = true;
            accessory.DebugMsg($"捕捉到{accessory.GetPlayerJobByIndex(idx)}被选为点名目标", DebugMode);
        }

        public void BuildUntargetedGroup()
        {
            UntargetedGroup = IsTargeted
                .Select((targeted, index) => new { targeted, index })
                .Where(x => !x.targeted)
                .Select(x => x.index)
                .ToList();
            
            UntargetedGroup.Sort();

            var str = "";

            foreach (var playerIdx in UntargetedGroup)
                str += $"{accessory.GetPlayerJobByIndex(playerIdx)}未被选为目标。\n";
            
            str += "---------SigmaVersion.BuildUntargetedGroup----------\n";
            accessory.DebugMsg(str, DebugMode);
        }
        
        public IconId FindIconGroup(int idx)
        {
            if (CircleGroup.Contains(idx))
                return IconId.IconCircle;
            if (CrossGroup.Contains(idx))
                return IconId.IconCross;
            if (TriangleGroup.Contains(idx))
                return IconId.IconTriangle;
            if (SquareGroup.Contains(idx))
                return IconId.IconSquare;
            return IconId.None;
        }

        public int FindPartner(int idx)
        {
            var group = FindIconGroup(idx);
            var chosenGroup = group switch
            {
                IconId.IconCircle => CircleGroup,
                IconId.IconCross => CrossGroup,
                IconId.IconTriangle => TriangleGroup,
                IconId.IconSquare => SquareGroup,
                _ => [],
            };
            return chosenGroup.FirstOrDefault(player => player != idx);
        }

        public void BuildSimgaMarker()
        {
            // 无点名二人
            var playerAttack1 = UntargetedGroup[0];
            var playerBind1 = UntargetedGroup[1];
            SetMarkerBySelf(playerAttack1, MarkType.Attack1);
            SetMarkerBySelf(playerBind1, MarkType.Bind1);

            // 无点名二人搭档
            var playerCircle = FindPartner(playerAttack1);
            var playerAttack4 = FindPartner(playerBind1);
            SetMarkerBySelf(playerCircle, MarkType.Circle);
            SetMarkerBySelf(playerAttack4, MarkType.Attack4);

            var extraMarkIdx = 0;
            List<MarkType> extraMarkType = [MarkType.Attack2, MarkType.Bind3, MarkType.Attack3, MarkType.Bind2];
            // 剩余未被标记的
            for (var i = 0; i < 8; i++)
            {
                if (IsMarkered(i)) continue;
                SetMarkerBySelf(i, extraMarkType[extraMarkIdx]);
                extraMarkIdx++;
                SetMarkerBySelf(FindPartner(i), extraMarkType[extraMarkIdx]);
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

        public void CalcSpreadPos(Vector3 center)
        {
            // TODO 该“中远线距离”值待测试
            var basicDistance = IsRemoteGlitch() ? 19.5f : 11.25f;
            var basicPoint = new Vector3(100, 0, 100 - basicDistance);
            var str = "";
            
            for (var i = 0; i < 8; i++)
            {
                var posV3 = basicPoint.RotatePoint(center, SpreadTargetPos[i] * float.Pi / 8);
                // 第二排往内一步避免引导，第一排往外一步引导
                if (GetMarkerTypeFromIdx(i) is MarkType.Attack2 or MarkType.Bind2)
                    posV3 = posV3.PointInOutside(center, 0.5f);
                if (GetMarkerTypeFromIdx(i) is MarkType.Attack1 or MarkType.Bind1)
                    posV3 = posV3.PointInOutside(center, 0.5f, true);
                SpreadPosV3[i] = posV3;
                str += $"计算出{accessory.GetPlayerJobByIndex(i)}({GetMarkerTypeFromIdx(i)})的分散位置{SpreadPosV3[i]}\n";
            }
            
            str += "---------SigmaVersion.CalcSpreadPos----------\n";
            accessory.DebugMsg(str, DebugMode);
        }

        public void CalcTargetTowerPos(Vector3 center)
        {
            var knockBackDistance = 13f;
            var basicPoint = new Vector3(100, 0, 82.5f + knockBackDistance);
            var str = "";
            for (var i = 0; i < 8; i++)
            {
                var posV3 = basicPoint.RotatePoint(center, TargetTowerPos[i] * float.Pi / 8);
                TargetTowerPosV3[i] = posV3;
                str += $"计算出{accessory.GetPlayerJobByIndex(i)}({GetMarkerTypeFromIdx(i)})的击退塔位置{TargetTowerPosV3[i]}\n";
            }

            str += "---------SigmaVersion.CalcTargetTowerPos----------\n";
            accessory.DebugMsg(str, DebugMode);
        }

        public void SetMarkerFromOut(int idx, MarkType marker)
        {
            Marker[idx] = marker;
            accessory.DebugMsg($"从外部获得{accessory.GetPlayerJobByIndex(idx)}为{marker}", DebugMode);
        }

        public void SetMarkerBySelf(int idx, MarkType marker)
        {
            Marker[idx] = marker;
            accessory.DebugMsg($"于内部设置{accessory.GetPlayerJobByIndex(idx)}为{marker}", DebugMode);
        }

        public void SetTowerType(DataId towerId, Vector3 towerPos, Vector3 center)
        {
            var pos = towerPos.Position2Dirs(center, 16);
            var towerType = towerId switch
            {
                DataId.SingleTower => 1,
                DataId.PartnerTower => 2,
                _ => 0
            };
            TowerType[pos] = towerType;
            accessory.DebugMsg($"检测到方位{pos}的{towerType}人塔", DebugMode);
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
    
    public class SigmaWorld
    {
        public ScriptAccessory accessory { get; set; } = null!;
        private int OmegaFemalePos { get; set; } = 0;
        private bool OmegaFemaleInsideSafe { get; set; } = false;
        private List<int>? _dynamicBuffLevel;
    
        public void Init(ScriptAccessory _accessory)
        {
            accessory = _accessory;
        }
    
        public void CalcMarker()
        {
            // GetDynamicBuffLevel(_dyn);
        }

        private void GetDynamicBuffLevel(DynamicsPass dyn)
        {
            _dynamicBuffLevel = dyn.GetBuffLevelList();
        }
    }

    #endregion P5 二运
    
    [ScriptMethod(name: "P5 三运 阶段转换", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(32789)$"], userControl: false)]
    public void P5_RunMi_Omega_PhaseRecord(Event @event, ScriptAccessory accessory)
    {
        _phase = TopPhase.P5_OmegaVersion;
        accessory.DebugMsg($"当前阶段为：{_phase}", DebugMode);
    }
    
    private static bool IsInPhase5(TopPhase phase)
    {
        return phase is TopPhase.P5_DeltaVersion or TopPhase.P5_DeltaWorld or 
            TopPhase.P5_SigmaVersion or TopPhase.P5_SigmaWorld or 
            TopPhase.P5_OmegaVersion or TopPhase.P5_OmegaWorldA or TopPhase.P5_OmegaWorldB;
    }

    #region General Functions

    private static List<uint> SortByPartyList(List<uint> unsortedList, ScriptAccessory accessory)
    {
        return unsortedList.OrderBy(x => accessory.Data.PartyList.IndexOf(x)).ToList();
    }

    private static void LocalMarkClear(ScriptAccessory accessory)
    {
        accessory.Method.Mark(0xE000000, MarkType.Attack1, true);
        accessory.Method.Mark(0xE000000, MarkType.Attack2, true);
        accessory.Method.Mark(0xE000000, MarkType.Attack3, true);
        accessory.Method.Mark(0xE000000, MarkType.Attack4, true);
        accessory.Method.Mark(0xE000000, MarkType.Attack5, true);
        accessory.Method.Mark(0xE000000, MarkType.Attack6, true);
        accessory.Method.Mark(0xE000000, MarkType.Attack7, true);
        accessory.Method.Mark(0xE000000, MarkType.Attack8, true);
        accessory.Method.Mark(0xE000000, MarkType.Bind1, true);
        accessory.Method.Mark(0xE000000, MarkType.Bind2, true);
        accessory.Method.Mark(0xE000000, MarkType.Bind3, true);
        accessory.Method.Mark(0xE000000, MarkType.Stop1, true);
        accessory.Method.Mark(0xE000000, MarkType.Stop2, true);
        accessory.Method.Mark(0xE000000, MarkType.Square, true);
        accessory.Method.Mark(0xE000000, MarkType.Circle, true);
        accessory.Method.Mark(0xE000000, MarkType.Cross, true);
        accessory.Method.Mark(0xE000000, MarkType.Triangle, true);
    }

    private static void MarkClear(ScriptAccessory accessory)
    {
        if (!CaptainMode) return;
        if (LocalTest)
        {
            accessory.DebugMsg($"本地测试删除标点。");
            if (LocalStrTest) return;
            LocalMarkClear(accessory);
        }
        else
            accessory.Method.MarkClear();
    }

    private static void MarkPlayerByIdx(ScriptAccessory accessory, int idx, MarkType marker)
    {
        if (!CaptainMode) return;
        accessory.DebugMsg($"为{idx}({accessory.GetPlayerJobByIndex(idx)})标上{marker}。", DebugMode && LocalStrTest);
        if (LocalStrTest) return;
        accessory.Method.Mark(accessory.Data.PartyList[idx], marker, LocalTest);
    }
    
    private static void MarkPlayerById(ScriptAccessory accessory, uint id, MarkType marker)
    {
        if (!CaptainMode) return;
        accessory.DebugMsg($"为{accessory.GetPlayerIdIndex(id)}({accessory.GetPlayerJobById(id)})标上{marker}。",
            DebugMode && LocalStrTest);
        if (LocalStrTest) return;
        accessory.Method.Mark(id, marker, LocalTest);
    }

    private static int GetMarkedPlayerIndex(ScriptAccessory accessory, List<MarkType> markerList, MarkType marker)
    {
        return markerList.IndexOf(marker);
    }
    
    public class DynamicsPass
    {
        public ScriptAccessory accessory { get; set; } = null!;
        public int DynamicCount { get; set; } = 0;
        public int WorldCount { get; set; } = 0;
        public List<int> BuffLevel { get; set; } = Enumerable.Repeat(0, 8).ToList();
        public List<int> FarSource { get; set; } = [];
        public List<int> NearSource { get; set; } = [];
        public List<int> FarTarget { get; set; } = [];
        public List<int> NearTarget { get; set; } = [];
        public List<int> IdleTarget { get; set; } = [];
    
        public void Init(ScriptAccessory _accessory)
        {
            accessory = _accessory;
            DynamicCount = 0;
            WorldCount = 0;
            BuffLevel = Enumerable.Repeat(0, 8).ToList();
        }
        public void ClearWorldList()
        {
            FarSource.Clear();
            NearSource.Clear();
            IdleTarget.Clear();
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
            DynamicCount++;
            accessory.DebugMsg($"{accessory.GetPlayerJobByIndex(idx)}的潜能量增加了，目前为{BuffLevel[idx]}",
                DebugMode);
        }
        public void SetFarSource(int idx)
        {
            FarSource.Add(idx);
            WorldCount++;
        }
        public void SetNearSource(int idx)
        {
            NearSource.Add(idx);
            WorldCount++;
        }
        public bool WorldRecordComplete()
        {
            return _phase switch
            {
                TopPhase.P5_DeltaVersion => WorldCount == 2,
                TopPhase.P5_SigmaVersion => WorldCount == 4,
                TopPhase.P5_OmegaVersion => WorldCount == 8,
                _ => true
            };
        }
        
        public void PrintDynamicPass()
        {
            var str = "";
            str += "-----基本事件-----\n";
            str += $"现有潜能量总层数: {DynamicCount}\n";
            str += $"现有你好世界数: {WorldCount}\n";
            str += $"各玩家潜能量: {accessory.BuildListStr(BuffLevel)}\n";
            accessory.DebugMsg(str, DebugMode);
            
            str = "";
            str += "-----世界事件-----\n";
            str += $"远世界：{accessory.BuildListStr(FarSource, true)}\n";
            str += $"远世界目标：{accessory.BuildListStr(FarTarget, true)}\n";
            str += $"近世界：{accessory.BuildListStr(NearSource, true)}\n";
            str += $"近世界目标：{accessory.BuildListStr(NearTarget, true)}\n";
            str += $"闲人：{accessory.BuildListStr(IdleTarget, true)}\n";
            accessory.DebugMsg(str, DebugMode);
        }
        
    }
    
    #endregion General Functions
}

public enum TopPhase : uint
{
    Init,                   // 初始
    P5_DeltaVersion,        // P5 一运
    P5_DeltaWorld,          // P5 二传
    P5_SigmaVersion,        // P5 二运
    P5_SigmaWorld,          // P5 二传
    P5_OmegaVersion,        // P5 三运
    P5_OmegaWorldA,         // P5 三传
    P5_OmegaWorldB,         // P5 四传
    P5_BlindFaith,          // P5 盲信
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
    // _events
    SigmaSonyMarker = 0,
    SigmaSonyRecord = 1,
    SigmaTargetRecord = 2,
    
    DeltaTetherRecord = 3,
    WorldRecord = 4,
    DeltaMarkerComplete = 5,
    DeltaSwapChecked = 6,
    OmegaBaldRecorded = 7,
    PunchCountComplete = 8,
    ShieldTargetRecorded = 9,
    
    BeetleSwipeRecorded = 0,
    
    // _recorded
    ArmUnitGuidance = 0,
}

#region 函数集
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

    public static string Message(this Event @event)
    {
        return @event["Message"];
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
    /// 适用于旋转，FF14游戏基顺时针旋转为负。
    /// </summary>
    /// <param name="radian"></param>
    /// <returns></returns>
    public static float Cw2Ccw(this float radian)
    {
        return -radian;
    }
    
    /// <summary>
    /// 适用于旋转，FF14游戏基顺时针旋转为负。
    /// 与Cw2CCw完全相同，为了代码可读性便于区分。
    /// </summary>
    /// <param name="radian"></param>
    /// <returns></returns>
    public static float Ccw2Cw(this float radian)
    {
        return -radian;
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

    /// <summary>
    /// 获得两点之间距离
    /// </summary>
    /// <param name="point"></param>
    /// <param name="target"></param>
    /// <returns></returns>
    public static float DistanceTo(this Vector3 point, Vector3 target)
    {
        Vector2 v2 = new(point.X - target.X, point.Z - target.Z);
        return v2.Length();
    }
    
    /// <summary>
    /// 寻找两点之间的角度差，范围0~360deg
    /// </summary>
    /// <param name="basePoint">基准位置</param>
    /// <param name="targetPos">比较目标位置</param>
    /// <param name="center">场地中心</param>
    /// <returns></returns>
    public static float FindRadianDifference(this Vector3 targetPos, Vector3 basePoint, Vector3 center)
    {
        var baseRad = basePoint.FindRadian(center);
        var targetRad = targetPos.FindRadian(center);
        var deltaRad = targetRad - baseRad;
        if (deltaRad < 0)
            deltaRad += float.Pi * 2;
        return deltaRad;
    }

    /// <summary>
    /// 从第三人称视角出发观察某目标是否在另一目标的右侧。
    /// </summary>
    /// <param name="basePoint">基准位置</param>
    /// <param name="targetPos">比较目标位置</param>
    /// <param name="center">场地中心</param>
    /// <returns></returns>
    public static bool IsAtRight(this Vector3 targetPos, Vector3 basePoint, Vector3 center)
    {
        // 从场中看向场外，在右侧
        return targetPos.FindRadianDifference(basePoint, center) < float.Pi;
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
    /// <param name="fourPeople">是否为四人迷宫</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static string GetPlayerJobByIndex(this ScriptAccessory accessory, int idx, bool fourPeople = false)
    {
        var str = idx switch
        {
            0 => "MT",
            1 => fourPeople ? "H1" : "ST",
            2 => fourPeople ? "D1" : "H1",
            3 => fourPeople ? "D2" : "H2",
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
    /// <param name="isSafe">使用安全色</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static DrawPropertiesEdit DrawGuidance(this ScriptAccessory accessory, 
        object ownerObj, object targetObj, int delay, int destroy, string name, float rotation = 0, float scale = 1f, bool isSafe = true)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(scale);
        dp.Rotation = rotation;
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.Color = isSafe ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
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
        object targetObj, int delay, int destroy, string name, float rotation = 0, float scale = 1f, bool isSafe = true)
    => accessory.DrawGuidance(accessory.Data.Me, targetObj, delay, destroy, name, rotation, scale, isSafe);
    
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
    public static DrawPropertiesEdit DrawOwnersEnmityOrder(this ScriptAccessory accessory, uint ownerId, uint orderIdx, float width, float length, int delay, int destroy, string name, bool lengthByDistance = false, bool byTime = false)
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
    /// <param name="rotation">绘图旋转角度</param>
    /// <param name="width">绘图宽度</param>
    /// <param name="length">绘图长度</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="lengthByDistance">长度是否随距离改变</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawTarget2Target(this ScriptAccessory accessory, uint ownerId, uint targetId, float width, float length, int delay, int destroy, string name, float rotation = 0, bool lengthByDistance = false, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.Rotation = rotation;
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
    /// 返回画向某目标的扇形绘图
    /// </summary>
    /// <param name="sourceId">起始目标id，通常为自己</param>
    /// <param name="targetId">目标单位id</param>
    /// <param name="radian">扇形角度</param>
    /// <param name="scale">扇形尺寸</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="color">绘图颜色</param>
    /// <param name="rotation">旋转角度</param>
    /// <param name="lengthByDistance">长度是否随距离改变</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawFanToTarget(this ScriptAccessory accessory, uint sourceId, uint targetId, float radian, float scale, int delay, int destroy, string name, Vector4 color, float rotation = 0, bool lengthByDistance = false, bool byTime = false)
    {
        var dp = accessory.DrawTarget2Target(sourceId, targetId, scale, scale, delay, destroy, name, rotation, lengthByDistance, byTime);
        dp.Radian = radian;
        dp.Color = color;
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
    /// <param name="ownerObj">绘图起始，可输入uint或Vector3</param>
    /// <param name="targetObj">绘图目标，可输入uint或Vector3，为0则无目标</param>
    /// <param name="radian">图形角度</param>
    /// <param name="rotation">旋转角度，以北为0度顺时针</param>
    /// <param name="width">绘图宽度</param>
    /// <param name="length">绘图长度</param>
    /// <param name="color">是Vector4则选用该颜色</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawStatic(this ScriptAccessory accessory, object ownerObj, object targetObj,
        float radian, float rotation, float width, float length, object color, int delay, int destroy, string name)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        switch (ownerObj)
        {
            case uint sid:
                dp.Owner = sid;
                break;
            case Vector3 spos:
                dp.Position = spos;
                break;
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
        dp.Radian = radian;
        dp.Rotation = rotation.Logic2Game();
        switch (color)
        {
            case Vector4 clr:
                dp.Color = clr;
                break;
            default:
                dp.Color = accessory.Data.DefaultDangerColor;
                break;
        }
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
    public static DrawPropertiesEdit DrawStaticCircle(this ScriptAccessory accessory, Vector3 center, Vector4 color,
        int delay, int destroy, string name, float scale = 1.5f)
        => accessory.DrawStatic(center, (uint)0, 0, 0, scale, scale, color, delay, destroy, name);
    // {
    //     var dp = accessory.DrawStatic(center, (uint)0, 0, 0, scale, scale, color, delay, destroy, name);
    //     return dp;
    // }

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
    public static DrawPropertiesEdit DrawStaticDonut(this ScriptAccessory accessory, Vector3 center, Vector4 color,
        int delay, int destroy, string name, float scale, float innerscale = 0) 
        => accessory.DrawStatic(center, (uint)0,
        float.Pi * 2, 0, scale, scale, color, delay, destroy, name);
    
    // {
    //     var dp = accessory.DrawStatic(center, (uint)0, float.Pi * 2, 0, scale, scale, color, delay, destroy, name);
    //     dp.InnerScale = innerscale != 0f ? new Vector2(innerscale) : new Vector2(scale - 0.05f);
    //     return dp;
    // }

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

    public static DrawPropertiesEdit DrawKnockBack(this ScriptAccessory accessory, object target, float length,
        int delay, int destroy, string name, float width = 1.5f, bool byTime = false)
        => accessory.DrawKnockBack(accessory.Data.Me, target, length, delay, destroy, name, width, byTime);
    // {
    //     return target switch
    //     {
    //         uint uintTarget => accessory.DrawKnockBack(accessory.Data.Me, uintTarget, length, delay, destroy, name, width, byTime),
    //         Vector3 vectorTarget => accessory.DrawKnockBack(accessory.Data.Me, vectorTarget, length, delay, destroy, name, width, byTime),
    //         _ => throw new ArgumentException("target 的类型必须是 uint 或 Vector3")
    //     };
    // }
    
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

    public static DrawPropertiesEdit DrawSightAvoid(this ScriptAccessory accessory, object target, int delay,
        int destroy, string name)
        => accessory.DrawSightAvoid(accessory.Data.Me, target, delay, destroy, name);
    // {
    //     return target switch
    //     {
    //         uint uintTarget => accessory.DrawSightAvoid(accessory.Data.Me, uintTarget, delay, destroy, name),
    //         Vector3 vectorTarget => accessory.DrawSightAvoid(accessory.Data.Me, vectorTarget, delay, destroy, name),
    //         _ => throw new ArgumentException("target 的类型必须是 uint 或 Vector3")
    //     };
    // }

    /// <summary>
    /// 返回多方向延伸指引
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="owner">分散源</param>
    /// <param name="extendDirs">分散角度</param>
    /// <param name="myDirIdx">玩家对应角度idx</param>
    /// <param name="width">指引箭头宽度</param>
    /// <param name="length">指引箭头长度</param>
    /// <param name="delay">绘图出现延时</param>
    /// <param name="destroy">绘图消失时间</param>
    /// <param name="name">绘图名称</param>
    /// <param name="colorPlayer">玩家对应箭头指引颜色</param>
    /// <param name="colorNormal">其他玩家对应箭头指引颜色</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static List<DrawPropertiesEdit> DrawExtendDirection(this ScriptAccessory accessory, object owner,
        List<float> extendDirs, int myDirIdx, float width, float length, int delay, int destroy, string name,
        Vector4 colorPlayer, Vector4 colorNormal)
    {
        List<DrawPropertiesEdit> dpList = [];
        switch (owner)
        {
            case uint sid:
                for (var i = 0; i < extendDirs.Count; i++)
                {
                    var dp = accessory.DrawGuidance(owner, sid, delay, destroy, $"{name}{i}", extendDirs[i], width);
                    dp.Color = i == myDirIdx ? colorPlayer : colorNormal;
                    dpList.Add(dp);
                }
                break;
            case Vector3 spos:
                for (var i = 0; i < extendDirs.Count; i++)
                {
                    var dp = accessory.DrawGuidance(spos, spos.ExtendPoint(extendDirs[i], length), delay, destroy,
                        $"{name}{i}", 0, width);
                    dp.Color = i == myDirIdx ? colorPlayer : colorNormal;
                    dpList.Add(dp);
                }
                break;
            default:
                throw new ArgumentException("DrawExtendDirection的目标类型输入错误");
        }

        return dpList;
    }

    /// <summary>
    /// 返回多地点指路指引列表
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="positions">地点位置</param>
    /// <param name="delay">绘图出现延时</param>
    /// <param name="destroy">绘图消失时间</param>
    /// <param name="name">绘图名称</param>
    /// <param name="colorPosPlayer">对应位置标记行动颜色</param>
    /// <param name="colorPosNormal">对应位置标记准备颜色</param>
    /// <param name="colorGo">指路出发箭头颜色</param>
    /// <param name="colorPrepare">指路准备箭头颜色</param>
    /// <returns>dpList中的三个List：位置标记，玩家指路箭头，地点至下个地点的指路箭头</returns>
    public static List<List<DrawPropertiesEdit>> DrawMultiGuidance(this ScriptAccessory accessory,
        List<Vector3> positions, List<int> delay, List<int> destroy, string name,
        Vector4 colorGo, Vector4 colorPrepare, Vector4 colorPosNormal, Vector4 colorPosPlayer)
    {
        List<List<DrawPropertiesEdit>> dpList = [[], [], []];
        for (var i = 0; i < positions.Count; i++)
        {
            var dpPos = accessory.DrawStaticCircle(positions[i], colorPosPlayer, delay[i], destroy[i], $"{name}pos{i}");
            dpList[0].Add(dpPos);
            var dpGuide = accessory.DrawGuidance(positions[i], colorGo, delay[i], destroy[i], $"{name}guide{i}");
            dpList[1].Add(dpGuide);
            if (i == positions.Count - 1) break;
            var dpPrep = accessory.DrawGuidance(positions[i], positions[i + 1], delay[i], destroy[i], $"{name}prep{i}");
            dpList[2].Add(dpPrep);
        }
        return dpList;
    }
    
    /// <summary>
    /// 外部用调试模式
    /// </summary>
    /// <param name="str"></param>
    /// <param name="debugMode"></param>
    /// <param name="accessory"></param>
    public static void DebugMsg(this ScriptAccessory accessory, string str, bool debugMode = false)
    {
        if (!debugMode)
            return;
        accessory.Method.SendChat($"/e [DEBUG] {str}");
    }

    /// <summary>
    /// 将List内信息转换为字符串。
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="myList"></param>
    /// <param name="isJob">是职业，在转为字符串前调用转职业函数</param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static string BuildListStr<T>(this ScriptAccessory accessory, List<T> myList, bool isJob = false)
    {
        return string.Join(", ", myList.Select(item =>
        {
            if (isJob && item != null && item is int i)
                return accessory.GetPlayerJobByIndex(i);
            return item?.ToString() ?? "";
        }));
    }
}

#endregion 函数集

