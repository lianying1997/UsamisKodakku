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
using ECommons.MathHelpers;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.Draw.Manager;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs;
using KodakkuAssist.Module.Script.Type;

namespace UsamisKodakku.Script._07_DawnTrail.QueenEternal;

[ScriptType(name: Name, territorys: [1243], guid: "45fff289-e23d-41ab-9039-71cd310668e4", 
    version: Version, author: "Usami", note: NoteStr)]

// ^(?!.*((武僧|机工士|龙骑士|武士|忍者|蝰蛇剑士|钐镰客|舞者|吟游诗人|占星术士|贤者|学者|(朝日|夕月)小仙女|炽天使|白魔法师|战士|骑士|暗黑骑士|绝枪战士|绘灵法师|黑魔法师|青魔法师|召唤师|宝石兽|亚灵神巴哈姆特|亚灵神不死鸟|迦楼罗之灵|泰坦之灵|伊弗利特之灵|后式自走人偶)\] (Used|Cast))).*35501.*$

public class QueenEternalEx
{
    const string NoteStr =
    """
    绝对君权为正攻，若逃课请忽略核爆远离指路箭头。
    针对土阶段涉及优先级的踩塔判断，可以开启用户设置中“土阶段四人塔智能优先级判断”。
    若不使用此功能，请确认小队列表排序正确（特别关注D1/D2），避免踩塔指路错误。
    鸭门。
    """;

    private const string Name = "QueenEternalEx [永恒女王忆想歼灭战]";
    private const string Version = "0.0.0.7";
    private const string DebugVersion = "a";
    private const string Note = "初版完成";
    
    [UserSetting("Debug模式，非开发用请关闭")]
    public static bool DebugMode { get; set; } = false;
    [UserSetting("土阶段四人塔智能优先级判断")]
    public static bool IntelligentPriorTowerMode { get; set; } = true;
    [UserSetting("站位提示圈绘图-普通颜色")]
    public static ScriptColor PosColorNormal { get; set; } = new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 1.0f, 1.0f) };
    [UserSetting("站位提示圈绘图-玩家站位颜色")]
    public static ScriptColor PosColorPlayer { get; set; } = new ScriptColor { V4 = new Vector4(0.0f, 1.0f, 1.0f, 1.0f) };
    [UserSetting("地火爆炸区颜色")]
    public ScriptColor exflareColor { get; set; } = new ScriptColor { V4 = new Vector4(0f, 1.0f, 1.0f, 1.0f) };
    [UserSetting("地火预警区颜色")]
    public ScriptColor exflareWarnColor { get; set; } = new ScriptColor { V4 = new Vector4(0f, 0.5f, 1.0f, 1.0f) };
    
    private List<bool> _drawn = new bool[20].ToList();                   // 绘图记录
    private QueenEternalPhase _phase = QueenEternalPhase.Init;
    private static uint _bossId = 0;
    
    private static readonly Vector3 Center = new Vector3(100, 0, 100);
    private static readonly Vector3 CenterWind = new Vector3(100, 0, 92.5f);
    private static readonly Vector3 CenterEarth = new Vector3(100, 0, 94f);
    private static readonly Vector3 CenterIce = new Vector3(100, 0, 100);
    private static readonly Vector3 GravityFieldLeft = new Vector3(92f, 0, 94f);
    private List<ManualResetEvent> _manualResetEvents = Enumerable.Repeat(new ManualResetEvent(false), 20).ToList();
    private List<AutoResetEvent> _autoResetEvents = Enumerable.Repeat(new AutoResetEvent(false), 20).ToList();
    private List<bool> _earthPhaseTarget = new bool[8].ToList(); // 绘图记录
    private List<int> _intelligentPriority = Enumerable.Repeat(0, 8).ToList();
    private bool _intelligentPriorityValid = true;
    
    private bool _isFlareTarget = false;    // 绝对君权核爆目标
    private Vector3 _sourceIceDartPos = new Vector3(0, 0, 0);     // 与玩家连线的目标冰柱位置
    private int _myIceBridgeIdx = 0;       // 冰阶段走的冰桥
    private List<bool> _iceRushRangeDrawn = new bool[8].ToList();   // 冰柱突刺是否被绘图
    private int _raisedTributeNum = 0;      // 接线分摊，分摊次数

    
    public void Init(ScriptAccessory accessory)
    {
        // DebugMsg($"Init {Name} v{Version}{DebugVersion} Success.\n{Note}", accessory);
        _phase = QueenEternalPhase.Init;
        _drawn = new bool[20].ToList();
        _bossId = 0;
        _manualResetEvents = Enumerable.Repeat(new ManualResetEvent(false), 20).ToList();
        _autoResetEvents = Enumerable.Repeat(new AutoResetEvent(false), 20).ToList();
        
        _earthPhaseTarget = new bool[8].ToList(); // 绘图记录
        _isFlareTarget = false;     // 绝对君权核爆目标
        _sourceIceDartPos = new Vector3(0, 0, 0);     // 与玩家连线的目标冰柱位置
        _myIceBridgeIdx = 0;       // 冰阶段走的冰桥
        _iceRushRangeDrawn = new bool[8].ToList();   // 冰柱突刺是否被绘图
        _raisedTributeNum = 0;      // 接线分摊，分摊次数
        _exaflareShown = false;     // 是否显示地火
        
        _intelligentPriority = Enumerable.Repeat(0, 8).ToList();    // 四人塔智能优先级模式权重
        _intelligentPriorityValid = true;   // 四人塔智能优先级模式是否合理
        
        accessory.Method.MarkClear();
        accessory.Method.RemoveDraw(".*");
    }

    public static void DebugMsg(string str, ScriptAccessory accessory)
    {
        if (!DebugMode) return;
        accessory.Method.SendChat($"/e [DEBUG] {str}");
    }

    [ScriptMethod(name: "随时DEBUG用", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:=TST"], userControl: false)]
    public void EchoDebug(Event @event, ScriptAccessory accessory)
    {
        if (!DebugMode) return;
        // ---- DEBUG CODE ----
        
        // -- DEBUG CODE END --
    }
    
    #region 以太税
    
    [ScriptMethod(name: "记录bossId", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40972"], userControl: false)]
    public void BossIdRecord(Event @event, ScriptAccessory accessory)
    {
        if (_bossId != 0) return;
        _bossId = @event.SourceId();
    }
    
    [ScriptMethod(name: "---- 《以太税》 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld"],
        userControl: true)]
    public void SplitLine_Aethertithe(Event @event, ScriptAccessory accessory)
    {
    }
    
    [ScriptMethod(name: "以太税分摊区提示", eventType: EventTypeEnum.EnvControl, eventCondition: ["Id:regex:^((04|08|10)000100)$", "Index:00000000"], userControl: true)]
    public void AethertitheStackPositionHint(Event @event, ScriptAccessory accessory)
    {
        var id = @event.Id();
        List<bool> safePlace = [true, true, true];
        const uint left = 0x04000100;
        const uint middle = 0x08000100;
        const uint right = 0x10000100;
        var idx = id switch
        {
            left => 0,
            middle => 1,
            _ => 2
        };
        
        safePlace[idx] = false;
        var myIndex = accessory.GetMyIndex();
        var playerPos = myIndex % 2 == 0 ? safePlace.IndexOf(true) : safePlace.LastIndexOf(true);
        
        List<Vector3> dirPos = [new Vector3(80, 0, 100), new Vector3(100, 0, 120), new Vector3(120, 0, 100)];
        List<float> rot = [-60f.DegToRad(), 0, 60f.DegToRad()];
        for (var i = 0; i < 3; i++)
        {
            var startPos = new Vector3(100, 0, 80);
            var dp = accessory.DrawGuidance(startPos, dirPos[i], 0, 6000, $"分摊指示{i}", 0, 2f);
            dp.Color = i == playerPos ? PosColorPlayer.V4.WithW(3f) : PosColorNormal.V4;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
        }

        var dp0 = accessory.DrawFan(_bossId, 70f.DegToRad(), rot[idx], 100, 0, 0, 20000, $"以太税{idx}");
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp0);
        
        // var stackPosStr = playerPos switch
        // {
        //     0 => "左侧",
        //     1 => "中间",
        //     _ => "右侧"
        // };
        // accessory.Method.TextInfo($"{stackPosStr}分摊", 6000);
    }
    
    [ScriptMethod(name: "以太税删除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(4097[456])"], userControl: false)]
    public void AethertitheStackRemove(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw(".*");
    }
    
    #endregion 以太税

    #region 左右刀

    [ScriptMethod(name: "---- 《左右刀》 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld"],
        userControl: true)]
    public void SplitLine_LegitimateForce(Event @event, ScriptAccessory accessory)
    {
    }
    
    private bool _legitimateForce;
    [ScriptMethod(name: "左右刀提示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4099[02])"], userControl: true)]
    public void LegitimateForce(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var aid = @event.ActionId();
        _legitimateForce = true;
        const uint attackLeftHand = 40992;
        
        var dp = accessory.DrawLeftRightCleave(sid, aid == attackLeftHand, 3000, 5000, $"左右刀1");
        dp.Scale = new Vector2(150f);
        if (_phase is QueenEternalPhase.Earth)
        {
            dp.Offset = new(0, -3.5f, 0);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Fan, dp);
        }
        else
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "左右刀第二段", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(4099[02])"], userControl: false)]
    public void LegitimateForceSecond(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var aid = @event.ActionId();
        if (!_legitimateForce) return;
        _legitimateForce = false;
        accessory.Method.RemoveDraw($"左右刀1");
        const uint attackLeftHand = 40992;
        var dp = accessory.DrawLeftRightCleave(sid, aid != attackLeftHand, 0, 8000, $"左右刀2");
        dp.Scale = new Vector2(150f);
        if (_phase == QueenEternalPhase.Earth)
        {
            dp.Offset = new(0, -3.5f, 0);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Fan, dp);
        }
        else
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }
    
    [ScriptMethod(name: "左右刀第二段消失", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(4099[34])"], userControl: false)]
    public void LegitimateForceRemove(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw($"左右刀2");
    }
    
    #endregion
    
    #region 风阶段
    
    [ScriptMethod(name: "---- 《风阶段》 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld"],
        userControl: true)]
    public void SplitLine_LawsofWind(Event @event, ScriptAccessory accessory)
    {
    }
    
    [ScriptMethod(name: "风阶段转换", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40995"], userControl: false)]
    public void LawsofWindPhaseChange(Event @event, ScriptAccessory accessory)
    {
        _phase = QueenEternalPhase.Wind;
        accessory.DebugMsg($"当前阶段为：{_phase}", DebugMode);
        _autoResetEvents[0].Set();
    }
    
    [ScriptMethod(name: "风分摊提示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40995"], userControl: true)]
    public void AeroquellStackPositionHint(Event @event, ScriptAccessory accessory)
    {
        _autoResetEvents[0].WaitOne();
        if (_phase != QueenEternalPhase.Wind) return;
        var myIndex = accessory.GetMyIndex();
        
        var stackPos = new Vector3(94f, 0, 89.5f);
        if (myIndex % 2 == 1)
            stackPos = stackPos.FoldPointHorizon(CenterWind.X).FoldPointVertical(CenterWind.Z);
        var dp = accessory.DrawGuidance(stackPos, 0, 9000, $"风分摊");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        
        // var stackPosStr = myIndex % 2 == 0 ? "左上靠中" : "右下靠中";
        // accessory.Method.TextInfo($"{stackPosStr}分摊", 4000);
        // accessory.Method.TTS($"{stackPosStr}分摊");
    }
    
    [ScriptMethod(name: "拉线集合提示", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0146"], userControl: true)]
    public void MissingLinkStackFirst(Event @event, ScriptAccessory accessory)
    {
        if (_phase != QueenEternalPhase.Wind) return;
        var tid = @event.TargetId();
        if (tid != accessory.Data.Me) return;
        
        var dp = accessory.DrawGuidance(CenterWind, 0, 4000, $"拉线集合");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        
        accessory.Method.TextInfo($"场中集合", 4000);
        // accessory.Method.TTS($"场中集合";
    }
    
    [ScriptMethod(name: "拉线集合提示删除", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(3587)$"], userControl: false)]
    public void MissingLinkStackRemove(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw($"拉线集合");
    }
    
    [ScriptMethod(name: "拉线方向提示", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(3587)$"], userControl: true)]
    public void MissingLineTether(Event @event, ScriptAccessory accessory)
    {
        if (_phase != QueenEternalPhase.Wind) return;
        var tid = @event.TargetId();
        if (tid != accessory.Data.Me) return;
        var myIndex = accessory.GetMyIndex();
        
        var tetherDes = new Vector3(88f, 0, 104f);
        if (myIndex >= 4)
            tetherDes = tetherDes.FoldPointHorizon(CenterWind.X).FoldPointVertical(CenterWind.Z);
        var dp = accessory.DrawGuidance(tetherDes, 0, 6000, $"拉线位置");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        
        // var tetherDesStr = myIndex <= 3 ? $"左下" : "右上";
        // accessory.Method.TextInfo($"{tetherDesStr}拉线", 3000);
        // accessory.Method.TTS($"{tetherDesStr}拉线");
    }
    
    [ScriptMethod(name: "拉线方向提示删除", eventType: EventTypeEnum.StatusRemove, eventCondition: ["StatusID:regex:^(3587)$"], userControl: false)]
    public void MissingLineTetherRemove(Event @event, ScriptAccessory accessory)
    {
        var tid = @event.TargetId();
        if (tid != accessory.Data.Me) return;
        accessory.Method.RemoveDraw($"拉线位置");
        _autoResetEvents[0].Set();
    }

    private bool _windChargeToLeft;
    [ScriptMethod(name: "风击退方向记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(4189|4190)$"], userControl: false)]
    public void WindOfChangeRecord(Event @event, ScriptAccessory accessory)
    {
        if (_phase != QueenEternalPhase.Wind) return;
        var tid = @event.TargetId();
        var stid = @event.StatusId();
        if (tid != accessory.Data.Me) return;
        const uint eastWindOfCharge = 4189;
        _windChargeToLeft = stid == eastWindOfCharge;   // 向左击退
        accessory.DebugMsg($"记录到{stid}, {(_windChargeToLeft ? "向左击退" : "向右击退")}", DebugMode);
    }

    private List<Vector3> _windChargeGuidancePosition = [new(0, 0, 0), new(0, 0, 0), new(0, 0, 0)];
    private bool _windGuidance;
    [ScriptMethod(name: "风击退指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4099[02])$"], userControl: true)]
    public void WindOfChangeGuidancePart1(Event @event, ScriptAccessory accessory)
    {
        if (_phase != QueenEternalPhase.Wind) return;
        _autoResetEvents[0].WaitOne();
        var aid = @event.ActionId();
        var myIndex = accessory.GetMyIndex();
        _windGuidance = true;
        
        const float deltaZ = 10.5f;   // 上半场与下半场Z轴差
        const float centerDeltaX = 1f;  // 躲左右刀X轴差
        const float edgeX = 12f;    // 场中X与风场地边缘X差
        
        // 是TN在下半场，所以增加dz。此处以上半场为基准
        var dz = myIndex <= 3 ? new Vector3(0, 0, deltaZ) : new Vector3(0, 0, -deltaZ);

        // 根据左右刀顺序决定dx偏置
        const uint attackRightFirst = 40992;
        var dx = aid == attackRightFirst ? new Vector3(-centerDeltaX, 0, 0) : new Vector3(centerDeltaX, 0, 0);

        // 根据击退方向决定dxWind偏置
        var dxWind = _windChargeToLeft ? new Vector3(edgeX, 0, 0) : new Vector3(-edgeX, 0, 0);

        var targetPos1 = CenterWind + dz + dx;
        var targetPos2 = CenterWind + dz - dx;
        var targetPos3 = CenterWind + dz + dxWind;
        _windChargeGuidancePosition = [targetPos1, targetPos2, targetPos3];
        
        var dp1 = accessory.DrawGuidance(_windChargeGuidancePosition[0], 0, 8000, $"风击退1");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp1);
        var dp12 = accessory.DrawGuidance(_windChargeGuidancePosition[0], _windChargeGuidancePosition[1], 0, 8000, $"风击退12");
        dp12.Color = accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp12);
    }
    
    [ScriptMethod(name: "风击退指路2", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(4099[02])$"], userControl: false)]
    public void WindOfChangeGuidancePart2(Event @event, ScriptAccessory accessory)
    {
        if (_phase != QueenEternalPhase.Wind) return;
        accessory.Method.RemoveDraw($"风击退1");
        accessory.Method.RemoveDraw($"风击退12");
        
        if (!_windGuidance) return;
        var dp2 = accessory.DrawGuidance(_windChargeGuidancePosition[1], 0, 6000, $"风击退2");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);
        var dp23 = accessory.DrawGuidance(_windChargeGuidancePosition[1], _windChargeGuidancePosition[2], 0, 8000, $"风击退23");
        dp23.Color = accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp23);
    }
    
    [ScriptMethod(name: "风击退指路3", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(4099[34])$"], userControl: false)]
    public void WindOfChangeGuidancePart3(Event @event, ScriptAccessory accessory)
    {
        if (_phase != QueenEternalPhase.Wind) return;
        accessory.Method.RemoveDraw($"风击退2");
        accessory.Method.RemoveDraw($"风击退23");
        
        if (!_windGuidance) return;
        var dp3 = accessory.DrawGuidance(_windChargeGuidancePosition[2], 0, 6000, $"风击退3");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp3);
    }
    
    [ScriptMethod(name: "风击退指路删除", eventType: EventTypeEnum.StatusRemove, eventCondition: ["StatusID:regex:^(4189|4190)$"], userControl: false)]
    public void WindOfChangeGuidanceRemove(Event @event, ScriptAccessory accessory)
    {
        if (_phase != QueenEternalPhase.Wind) return;
        if (@event.TargetId() != accessory.Data.Me) return;
        accessory.Method.RemoveDraw($".*");
        _windGuidance = false;
    }
    
    #endregion 风阶段

    #region 分治法
    [ScriptMethod(name: "---- 《分治法》 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld"],
        userControl: true)]
    public void SplitLine_DivideAndConquer(Event @event, ScriptAccessory accessory)
    {
    }
    
    [ScriptMethod(name: "站位提示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40983)$"], userControl: true)]
    public void DivideAndConquerPosition(Event @event, ScriptAccessory accessory)
    {
        var myIndex = accessory.GetMyIndex();
        var sid = @event.SourceId();
        var spos = @event.SourcePosition();

        List<float> rot = [-85f.DegToRad(), 85f.DegToRad(), -10f.DegToRad(), 10f.DegToRad(), -60f.DegToRad(), 60f.DegToRad(), -35f.DegToRad(), 35f.DegToRad()];
        for (var i = 0; i < 8; i++)
        {
            var dp = accessory.DrawRect(sid, 2f, 60f, 0, 10000, $"分治法{i}");
            dp.Rotation = rot[i];
            dp.Color = i == myIndex ? PosColorPlayer.V4.WithW(3f) : PosColorNormal.V4;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
        }
        
        // var standPosStr = myIndex switch
        // {
        //     0 => "左上",
        //     1 => "右上",
        //     2 => "场中偏左",
        //     3 => "场中偏右",
        //     4 => "左2",
        //     5 => "右2",
        //     6 => "左3",
        //     7 => "右3",
        //     _ => "???"
        // };
        // accessory.Method.TextInfo($"{standPosStr}引导后躲开", 4000);
        // accessory.Method.TTS($"{standPosStr}引导后躲开");
    }
    
    #endregion 分治法

    #region 土阶段

    [ScriptMethod(name: "---- 《土阶段》 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld"],
        userControl: true)]
    public void SplitLine_LawsofEarth(Event @event, ScriptAccessory accessory)
    {
    }
    
    [ScriptMethod(name: "土阶段转换", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:41000"], userControl: false)]
    public void LawsofEarthPhaseChange(Event @event, ScriptAccessory accessory)
    {
        _phase = QueenEternalPhase.Earth;
        accessory.DebugMsg($"当前阶段为：{_phase}", DebugMode);
    }
    
    [ScriptMethod(name: "第一轮踩塔提示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4099[02])$"], userControl: true)]
    public void LawsofEarthTower8(Event @event, ScriptAccessory accessory)
    {
        if (_phase != QueenEternalPhase.Earth) return;

        // 左右浮空阵
        var targetPos1 = Enumerable.Repeat(new Vector3(0, 0, 0), 2).ToList();
        targetPos1[0] = GravityFieldLeft;
        targetPos1[1] = GravityFieldLeft.FoldPointHorizon(CenterEarth.X);
        
        // 8人对应踩塔位置
        List<Vector3> targetPos2 = Enumerable.Repeat(new Vector3(0, 0, 0), 8).ToList();
        targetPos2[0] = new Vector3(94f, 0, 88f);
        targetPos2[2] = targetPos2[0].FoldPointVertical(CenterEarth.Z);
        targetPos2[4] = targetPos2[0].FoldPointHorizon(GravityFieldLeft.X);
        targetPos2[6] = targetPos2[4].FoldPointVertical(CenterEarth.Z);
        targetPos2[1] = targetPos2[0].FoldPointHorizon(CenterEarth.X);
        targetPos2[3] = targetPos2[2].FoldPointHorizon(CenterEarth.X);
        targetPos2[5] = targetPos2[4].FoldPointHorizon(CenterEarth.X);
        targetPos2[7] = targetPos2[6].FoldPointHorizon(CenterEarth.X);

        var myIndex = accessory.GetMyIndex();
        var idx1 = myIndex % 2 == 0 ? 0 : 1;

        var dp1 = accessory.DrawGuidance(CenterEarth, targetPos1[idx1], 0, 11000, $"踩浮空阵{idx1}");
        var dp2 = accessory.DrawGuidance(targetPos1[idx1], targetPos2[myIndex], 0, 11000, $"踩塔{myIndex}");
        dp1.Color = accessory.Data.DefaultDangerColor;
        dp2.Color = accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp1);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);
        
        dp1 = accessory.DrawGuidance(CenterEarth, targetPos1[idx1], 11000, 5000, $"踩浮空阵{idx1}");
        dp2 = accessory.DrawGuidance(targetPos1[idx1], targetPos2[myIndex], 11000, 5000, $"踩塔{myIndex}");
        dp1.Color = accessory.Data.DefaultSafeColor;
        dp2.Color = accessory.Data.DefaultSafeColor;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp1);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);
        
        // var idxStr = myIndex switch
        // {
        //     0 => "左上内",
        //     1 => "左下内",
        //     2 => "右上内",
        //     3 => "右下内",
        //     4 => "左上外",
        //     5 => "左下外",
        //     6 => "右上外",
        //     7 => "右下外",
        //     _ => "???"
        // };
        //
        // accessory.Method.TextInfo($"准备踩{idxStr}塔", 4000);
        // accessory.Method.TTS($"准备踩{idxStr}塔");
    }

    [ScriptMethod(name: "踩塔提示删除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(4100[12])$"], userControl: false)]
    public void LawsofEarthTower8Remove(Event @event, ScriptAccessory accessory)
    {
        if (_phase != QueenEternalPhase.Earth) return;
        accessory.Method.RemoveDraw($".*");
    }
    
    [ScriptMethod(name: "踩塔优先级判断", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(4100[12])$"], userControl: false)]
    public void Tower4PriorityJudge(Event @event, ScriptAccessory accessory)
    {
        // 根据八人塔相对位置判断
        if (_phase != QueenEternalPhase.Earth) return;
        if (_drawn[3]) return;
        _drawn[3] = true;
        if (!IntelligentPriorTowerMode) return;

        for (var i = 0; i < accessory.Data.PartyList.Count; i++)
        {
            var str = $"玩家设定{accessory.GetPlayerJobByIndex(i)}的信息：";
            var id = accessory.Data.PartyList[i];
            var chara = IbcHelper.GetById(id);

            if (chara == null || chara.IsDead)
            {
                _intelligentPriorityValid = false;
                accessory.DebugMsg($"发现存在角色阵亡或不存在，智能判断失效。", DebugMode);
                return;
            }

            if (!IbcHelper.IsDps(id))
            {
                _intelligentPriority[i] += 1;
                str += $"为TN(+1)";
            }
            else
            {
                str += $"为DPS(+0)";
            }

            if (chara.Position.X > CenterEarth.X)
            {
                _intelligentPriority[i] += 100;
                str += $"，在右半场(+100)";
            }
            else
            {
                str += $"，在左半场(+0)";
            }
            
            if (chara.Position.Z > CenterEarth.Z)
            {
                _intelligentPriority[i] += 10;
                str += $"，在下半场(+10)";
            }
            else
            {
                str += $"，在上半场(+0)";
            }
            accessory.DebugMsg($"{str} = {_intelligentPriority[i]}分。", DebugMode);
        }
        
        // 判断是否合法
        // 1. 右半场4人 (>= 100)
        // 2. 下半场4人 (% 100 >= 10)
        if (_intelligentPriority.Count(x => x >= 100) != 4)
        {
            _intelligentPriorityValid = false;
            accessory.DebugMsg($"右半场人数不为4人，智能判断失效", DebugMode);
            return;
        }

        if (_intelligentPriority.Count(x => x % 100 >= 10) != 4)
        {
            _intelligentPriorityValid = false;
            accessory.DebugMsg($"下半场人数不为4人，智能判断失效", DebugMode);
            return;
        }
    }
    
    [ScriptMethod(name: "大圈提示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(41004)$"], userControl: true)]
    public void LawsofEarthDefamation(Event @event, ScriptAccessory accessory)
    {
        if (_phase != QueenEternalPhase.Earth) return;
        var tid = @event.TargetId();
        var tidx = accessory.GetPlayerIdIndex(tid);
        HandleEarthPhaseTarget(tidx, accessory, $"大圈目标");
        
        if (tid != accessory.Data.Me) return;
        // 建立在分组正常的情况下，一左一右点名
        
        // 左右浮空阵
        var targetPos1 = Enumerable.Repeat(new Vector3(0, 0, 0), 2).ToList();
        targetPos1[0] = GravityFieldLeft;
        targetPos1[1] = GravityFieldLeft.FoldPointHorizon(CenterEarth.X);
        
        var targetPos2 = Enumerable.Repeat(new Vector3(0, 0, 0), 2).ToList();
        targetPos2[0] = new Vector3(82f, 0, 95f);
        targetPos2[1] = targetPos2[0].FoldPointHorizon(CenterEarth.X);
    
        var myIndex = accessory.GetMyIndex();
        var idx = myIndex % 2 == 0 ? 0 : 1;
        var bias = myIndex % 4 <= 1 ? new Vector3(0, 0, -5) : new Vector3(0, 0, 5);
        
        // 偷个懒，不从自己身上指路，做一个bias意为先踩塔后
        var dp1 = accessory.DrawGuidance(targetPos1[idx] + bias, targetPos1[idx], 0, 8000,
            $"踩浮空阵{idx}");
        var dp2 = accessory.DrawGuidance(targetPos1[idx], targetPos2[idx], 0, 8000, $"放圈{idx}");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp1);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);
    }
    
    [ScriptMethod(name: "扇形引导提示", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0011"], userControl: true)]
    public void LawsofEarthFan(Event @event, ScriptAccessory accessory)
    {
        if (_phase != QueenEternalPhase.Earth) return;
        var sid = @event.SourceId();
        var sidx = accessory.GetPlayerIdIndex(sid);
        HandleEarthPhaseTarget(sidx, accessory, $"扇形目标");

        var dp = accessory.DrawFan(_bossId, 60f.DegToRad(), 0, 60f, 0, 4000, 4000, $"扇形引导");
        dp.TargetObject = sid;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Fan, dp);
        
        if (sid != accessory.Data.Me) return;
        // 建立在分组正常的情况下，一左一右点名
        
        // 左右浮空阵
        var targetPos1 = Enumerable.Repeat(new Vector3(0, 0, 0), 2).ToList();
        targetPos1[0] = GravityFieldLeft;
        targetPos1[1] = GravityFieldLeft.FoldPointHorizon(CenterEarth.X);
        
        var targetPos2 = Enumerable.Repeat(new Vector3(0, 0, 0), 2).ToList();
        targetPos2[0] = new Vector3(90f, 0, 80.5f);
        targetPos2[1] = targetPos2[0].FoldPointHorizon(CenterEarth.X);

        var myIndex = accessory.GetMyIndex();
        var idx = myIndex % 2 == 0 ? 0 : 1;
        var bias = myIndex % 4 <= 1 ? new Vector3(0, 0, -5) : new Vector3(0, 0, 5);
        
        // 偷个懒，不从自己身上指路，做一个bias意为先踩塔后
        var dp1 = accessory.DrawGuidance(targetPos1[idx] + bias, targetPos1[idx], 0, 8000,
            $"踩浮空阵{idx}");
        var dp2 = accessory.DrawGuidance(targetPos1[idx], targetPos2[idx], 0, 8000, $"放圈{idx}");
        dp2.Offset = new Vector3(0, -3.5f, 0);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp1);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);
    }
    
    private void HandleEarthPhaseTarget(int tidx, ScriptAccessory accessory, string targetType)
    {
        lock (_earthPhaseTarget)
        {
            _earthPhaseTarget[tidx] = true;
            var count = _earthPhaseTarget.Count(x => x == true);
            if (count == 4) _manualResetEvents[0].Set();

            if (DebugMode)
            {
                accessory.DebugMsg($"检测到{targetType}，现在有{count}个目标", DebugMode);
            }
        }
    }
    
    [ScriptMethod(name: "第二轮踩塔提示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(41003)$"], userControl: true)]
    public void LawsofEarthGravityEmpireTower(Event @event, ScriptAccessory accessory)
    {
        if (_phase != QueenEternalPhase.Earth) return;
        
        // var count = _earthPhaseTarget.Count(x => x == true);
        // accessory.DebugMsg($"waitone前，现在有{count}个目标", DebugMode);
        _manualResetEvents[0].WaitOne();
        // var count1 = _earthPhaseTarget.Count(x => x == true);
        // accessory.DebugMsg($"waitone后，现在有{count1}个目标", DebugMode);
        
        var myIndex = accessory.GetMyIndex();
        List<int> sortedTowerTargetIdxs;

        if (_intelligentPriorityValid)
        {
            var str = "";
            // 找到_earthPhaseTarget为false的四个index
            var towerTargetIdxs = _earthPhaseTarget
                .Select((value, index) => new { value, index })
                .Where(x => x.value == false)
                .Select(x => x.index)
                .ToList();
            str = $"要去踩塔的是：{accessory.BuildListStr(towerTargetIdxs)}\n";
            
            // 找到_intelligentPriority中对应index的值
            var priorityValues = towerTargetIdxs
                .Select(index => _intelligentPriority[index])
                .ToList();
            str = $"对应的优先级权重是：{accessory.BuildListStr(priorityValues)}\n";
            
            // 将他们从小到大排序，并记下对应index
            sortedTowerTargetIdxs = priorityValues
                .Select((value, index) => new { OriginalIndex = towerTargetIdxs[index], Value = value })
                .OrderBy(x => x.Value)
                .Select(x => x.OriginalIndex)
                .ToList();
            str = $"踩塔智能优先级为：{accessory.BuildListStr(sortedTowerTargetIdxs)}\n";
            
            accessory.DebugMsg($"{str}");
        }
        else
        {
            // 根据D1-MT-D3-H1, D2-ST-D4-H2的优先级排序
            List<int> priorityIdx = [4, 0, 6, 2, 5, 1, 7, 3];

            var towerTargetIdxs = _earthPhaseTarget
                .Select((value, index) => new { value, index })
                .Where(x => x.value == false)
                .Select(x => x.index)
                .ToList();

            var priorityMap = priorityIdx
                .Select((value, index) => new { value, index })
                .ToDictionary(x => x.value, x => x.index);

            sortedTowerTargetIdxs = towerTargetIdxs
                .OrderBy(index => priorityMap[index])
                .ToList();

            var str = accessory.BuildListStr(sortedTowerTargetIdxs);
            accessory.DebugMsg($"踩塔优先级为: {str}");
        }
        
        if (_earthPhaseTarget[myIndex]) return;

        var myTowerIdx = sortedTowerTargetIdxs.IndexOf(myIndex);
        List<Vector3> targetTowerPositions = Enumerable.Repeat(new Vector3(0, 0, 0), 4).ToList();
        targetTowerPositions[0] = new Vector3(GravityFieldLeft.X, 0, 89f);
        targetTowerPositions[1] = targetTowerPositions[0].FoldPointVertical(GravityFieldLeft.Z);
        targetTowerPositions[2] = targetTowerPositions[0].FoldPointHorizon(CenterEarth.X);
        targetTowerPositions[3] = targetTowerPositions[1].FoldPointHorizon(CenterEarth.X);

        var dp = accessory.DrawGuidance(targetTowerPositions[myTowerIdx], 0, 8000, $"踩塔{myTowerIdx}");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }
    
    [ScriptMethod(name: "土阶段重力帝国机制删除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(41003)$"], userControl: false)]
    public void LawsofEarthGravityEmpireRemove(Event @event, ScriptAccessory accessory)
    {
        if (_phase != QueenEternalPhase.Earth) return;
        accessory.Method.RemoveDraw($".*");
        _manualResetEvents[0].Reset();
    }
    
    #endregion 土阶段

    #region 放陨石

    [ScriptMethod(name: "放陨石", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(4100[678])$"], userControl: true)]
    public void MeteorImpactPos(Event @event, ScriptAccessory accessory)
    {
        if (_phase != QueenEternalPhase.Earth) return;
        var aid = @event.ActionId();
        var myIndex = accessory.GetMyIndex();
        var tid = @event.TargetId();

        const uint meteorStart = 41006;
        if (aid != meteorStart && tid != accessory.Data.Me) return;
        
        List<Vector3[]> targetPos = Enumerable.Range(0, 8)
            .Select(_ => new Vector3[] { new(0, 0, 0), new(0, 0, 0) })
            .ToList();
        targetPos[0] = [new Vector3(88.5f, 0, GravityFieldLeft.Z), new Vector3(95.5f, 0, GravityFieldLeft.Z)];
        targetPos[1] = [new Vector3(104.5f, 0, GravityFieldLeft.Z), new Vector3(111.5f, 0, GravityFieldLeft.Z)];
        targetPos[4] = [new Vector3(88.5f, 0, 86.5f), new Vector3(95.5f, 0, 86.5f)];
        targetPos[5] = [new Vector3(104.5f, 0, 86.5f), new Vector3(111.5f, 0, 86.5f)];
        targetPos[2] = [CenterEarth, CenterEarth];
        targetPos[3] = [CenterEarth, CenterEarth];
        targetPos[6] = [CenterEarth, CenterEarth];
        targetPos[7] = [CenterEarth, CenterEarth];
        
        if (aid == 41006)
        {
            var dp = accessory.DrawGuidance(targetPos[myIndex][0], 0, 8000, $"放陨石1");
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }
        else if (!_drawn[0])
        {
            _drawn[0] = true;
            accessory.Method.RemoveDraw($"放陨石1");
            var dp = accessory.DrawGuidance(targetPos[myIndex][1], 0, 8000, $"放陨石2");
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }
        else
        {
            accessory.Method.RemoveDraw(".*");
        }
    }

    #endregion 放陨石

    #region 绝对君权
    
    [ScriptMethod(name: "---- 《绝对君权》 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld"],
        userControl: true)]
    public void SplitLine_AbsoluteAuthority(Event @event, ScriptAccessory accessory)
    {
    }

    [ScriptMethod(name: "引导炮台", eventType: EventTypeEnum.Tether, eventCondition: ["Id:regex:^(010[EF])$"], userControl: true)]
    public void CoronationGuidance(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        if (sid != accessory.Data.Me) return;
        var tpos = @event.TargetPosition();
        var dir = tpos.Position2Dirs(Center, 4);
        var id = @event.Id();
        const uint relativeRightCorner = 0x010F;
        
        List<Vector3> targetPos = Enumerable.Repeat(new Vector3(0, 0, 0), 8).ToList();
        for (var i = 0; i < 4; i++)
        {
            targetPos[i] = Center.ExtendPoint((45f + 90f * i).DegToRad(), 27.5f);
            targetPos[i + 4] = Center.ExtendPoint((90f * i).DegToRad(), 19.5f);
        }
        var myTargetPos = id == relativeRightCorner ? targetPos[dir] : targetPos[dir + 4];
        var dp = accessory.DrawGuidance(myTargetPos, 0, 10000, $"引导炮台");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }
    
    [ScriptMethod(name: "引导炮台消失", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(40982)$"], userControl: false)]
    public void CoronationGuidanceRemove(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw(".*");
    }

    [ScriptMethod(name: "角落待命提示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(41025)$"], userControl: true)]
    public void AbsoluteAuthorityHint(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.DrawGuidance(new Vector3(119, 0, 81), 0, 10000, $"绝对君权右上待命");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }
    
    [ScriptMethod(name: "回中走位", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(41025)$"], userControl: true)]
    public void AbsoluteAuthorityGuide(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw($"绝对君权右上待命");
        var dp1 = accessory.DrawGuidance(new Vector3(119, 0, 81), new Vector3(100, 0, 81), 0, 10000, $"绝对君权走位1");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp1);
        var dp2 = accessory.DrawGuidance(new Vector3(100, 0, 81), Center, 0, 10000, $"绝对君权走位2");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);
    }

    [ScriptMethod(name: "核爆走位", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(4186)$"],
        userControl: true)]
    public void AbsoluteAuthorityFlareGuide(Event @event, ScriptAccessory accessory)
    {
        var tid = @event.TargetId();
        if (tid != accessory.Data.Me) return;
        _isFlareTarget = true;
        var myIndex = accessory.GetMyIndex();
        
        List<Vector3> flarePos = Enumerable.Repeat(new Vector3(0, 0, 0), 4).ToList();
        flarePos[0] = new(81, 0, 81);
        flarePos[1] = flarePos[0].FoldPointHorizon(Center.X);
        flarePos[2] = flarePos[0].FoldPointVertical(90);
        flarePos[3] = flarePos[2].FoldPointHorizon(Center.X);
        var myPosIdx = myIndex % 4;
        
        var dp = accessory.DrawGuidance(Center, flarePos[myPosIdx], 0, 10000, $"绝对君权核爆");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }
    
    [ScriptMethod(name: "绝对君权走位删除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(4103[01])$"],
        userControl: false)]
    public void AbsoluteAuthorityGuideRemove(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw($"绝对君权.*");
    }
    
    [ScriptMethod(name: "孤独感提示", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(4103[01])$"],
        userControl: true)]
    public void AbsoluteAuthorityHeelGuidance(Event @event, ScriptAccessory accessory)
    {
        if (_drawn[1]) return;
        _drawn[1] = true;
        
        if (!_isFlareTarget) return;
        var myIndex = accessory.GetMyIndex();
        List<int> partnerIdx = [2, 3, 0, 1, 6, 7, 4, 5];
        var myPartnerIdx = partnerIdx[myIndex];
        var dp = accessory.DrawConnectionBetweenTargets(accessory.Data.Me, accessory.Data.PartyList[myPartnerIdx], 0,
            10000, $"孤独感", 2f);
        dp.Color = accessory.Data.DefaultSafeColor;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        accessory.Method.TextInfo($"与队友集合", 3000, true);
    }
    
    [ScriptMethod(name: "孤独感提示删除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(4103[23])$"],
        userControl: true)]
    public void AbsoluteAuthorityHeelGuideRemove(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw($".*");
    }

    #endregion 绝对君权

    #region 冰阶段

    [ScriptMethod(name: "---- 《冰阶段》 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld"],
        userControl: true)]
    public void SplitLine_LawsofIce(Event @event, ScriptAccessory accessory)
    {
    }
    
    [ScriptMethod(name: "冰阶段转换", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:41013"], userControl: false)]
    public void LawsofIcePhaseChange(Event @event, ScriptAccessory accessory)
    {
        if (_phase == QueenEternalPhase.Ice) return;
        _phase = QueenEternalPhase.Ice;
        accessory.DebugMsg($"当前阶段为：{_phase}", DebugMode);
        accessory.Method.TextInfo($"保持移动！", 4000, true);
    }
    
    [ScriptMethod(name: "行动提示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:41014"], userControl: true)]
    public void LawsofIcePhaseMoveHint(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.TextInfo($"保持移动！", 4000, true);
    }
    
    [ScriptMethod(name: "冰柱范围", eventType: EventTypeEnum.Tether, eventCondition: ["Id:regex:^(0039|0001)$"], userControl: true)]
    public void IceRushRange(Event @event, ScriptAccessory accessory)
    {
        var tid = @event.TargetId();
        var tidx = accessory.GetPlayerIdIndex(tid);
        if (_iceRushRangeDrawn[tidx]) return;
        
        var sid = @event.SourceId();
        var myIndex = accessory.GetMyIndex();

        lock (_earthPhaseTarget)
        {
            _iceRushRangeDrawn[tidx] = true;
            var count = _earthPhaseTarget.Count(x => x == true);
            var delay = count <= 4 ? 8000 : 4000;
            var destroy = 4000;
            var dp = accessory.DrawTarget2Target(sid, tid, 4, 80, delay, destroy, $"冰柱突刺范围{tidx}");
            dp.Color = tidx == myIndex ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }

    }
    
    private List<Vector3> _upIceBridgeRoute = [new(100, 0, 96), new(90, 0, 96), new(110, 0, 96)];
    private List<Vector3> _downIceBridgeRoute = [new(100, 0, 104), new(90, 0, 104), new(110, 0, 104)];
    [ScriptMethod(name: "冰柱连线路径", eventType: EventTypeEnum.Tether, eventCondition: ["Id:regex:^(0039|0001)$"], userControl: true)]
    public void IceRushRoute(Event @event, ScriptAccessory accessory)
    {
        if (_phase != QueenEternalPhase.Ice) return;
        var tid = @event.TargetId();
        var sid = @event.SourceId();
        if (tid != accessory.Data.Me) return;
        if (_sourceIceDartPos != new Vector3(0, 0, 0)) return;
        var spos = @event.SourcePosition();
        _sourceIceDartPos = spos;
        
        List<Vector3> targetPosList = Enumerable.Repeat(new Vector3(0, 0, 0), 4).ToList();
        
        // 判断是否为第一轮
        var isFirstRound = _sourceIceDartPos.Z > 108;
        // 91 97 103 109 ==-90==> 1 7 13 19 == /6 ==> 0 1 2 3
        if (isFirstRound)
        {
            var iceIdx = Math.Floor((_sourceIceDartPos.X - 90) / 6);
            accessory.DebugMsg($"被第一轮下方冰柱，第{iceIdx+1}个所连", DebugMode);
            List<Vector3> iceBaitPos = Enumerable.Repeat(new Vector3(0, 0, 0), 4).ToList();
            iceBaitPos[0] = new(108.5f, 0, 80.5f);
            iceBaitPos[1] = new(115.5f, 0, 80.5f);
            iceBaitPos[2] = iceBaitPos[1].FoldPointHorizon(CenterIce.X);
            iceBaitPos[3] = iceBaitPos[0].FoldPointHorizon(CenterIce.X);

            targetPosList = iceIdx switch
            {
                0 => [CenterIce, _upIceBridgeRoute[0], _upIceBridgeRoute[2], iceBaitPos[0]],
                1 => [CenterIce, _downIceBridgeRoute[0], _downIceBridgeRoute[2], iceBaitPos[1]],
                2 => [CenterIce, _downIceBridgeRoute[0], _downIceBridgeRoute[1], iceBaitPos[2]],
                _ => [CenterIce, _upIceBridgeRoute[0], _upIceBridgeRoute[1], iceBaitPos[3]],
            };
            
            _myIceBridgeIdx = (int)iceIdx;
        }
        else
        {
            var iceIdx = _sourceIceDartPos.Position2Dirs(CenterIce, 4, false);
            accessory.DebugMsg($"被第二轮左右冰柱，北顺时针起第{iceIdx+1}个所连", DebugMode);
            List<Vector3> iceBaitPos = Enumerable.Repeat(new Vector3(0, 0, 0), 4).ToList();
            iceBaitPos[0] = new(84.5f, 0, 109.5f);
            iceBaitPos[1] = iceBaitPos[0].FoldPointVertical(CenterIce.Z);
            iceBaitPos[2] = iceBaitPos[1].FoldPointHorizon(CenterIce.X);
            iceBaitPos[3] = iceBaitPos[0].FoldPointHorizon(CenterIce.X);

            targetPosList = iceIdx switch
            {
                0 => [CenterIce, _downIceBridgeRoute[0], _downIceBridgeRoute[1], iceBaitPos[0]],
                1 => [CenterIce, _upIceBridgeRoute[0], _upIceBridgeRoute[1], iceBaitPos[1]],
                2 => [CenterIce, _upIceBridgeRoute[0], _upIceBridgeRoute[2], iceBaitPos[2]],
                _ => [CenterIce, _downIceBridgeRoute[0], _downIceBridgeRoute[2], iceBaitPos[3]],
            };

            var bridgeStr = iceIdx switch
            {
                0 => "左下",
                1 => "左上",
                2 => "右上",
                _ => "右下",
            };
            
            _myIceBridgeIdx = 10 + iceIdx;
            accessory.Method.TextInfo($"观察【{bridgeStr}】桥梁，等第一轮先过", 4000, true);
        }
        
        var dp01 = accessory.DrawGuidance(targetPosList[0], targetPosList[1], 0, 15000, $"冰柱路径01");
        var dp12 = accessory.DrawGuidance(targetPosList[1], targetPosList[2], 0, 15000, $"冰柱路径12");
        var dp23 = accessory.DrawGuidance(targetPosList[2], targetPosList[3], 0, 15000, $"冰柱路径23");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp01);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp12);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp23);
    }

    [ScriptMethod(name: "回中躲左右刀路径", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(4101[56])"], userControl: true)]
    public void AfterIceRushRoute(Event @event, ScriptAccessory accessory)
    {
        if (_phase != QueenEternalPhase.Ice) return;
        if (_drawn[2]) return;
        _drawn[2] = true;
        accessory.Method.RemoveDraw($"冰柱路径.*");
        accessory.Method.RemoveDraw($"冰柱突刺范围.*");
        
        List<Vector3> targetPosList = _myIceBridgeIdx switch
        {
            0 => [CenterIce, _upIceBridgeRoute[0], _upIceBridgeRoute[2]],
            1 => [CenterIce, _downIceBridgeRoute[0], _downIceBridgeRoute[2]],
            2 => [CenterIce, _downIceBridgeRoute[0], _downIceBridgeRoute[1]],
            3 => [CenterIce, _upIceBridgeRoute[0], _upIceBridgeRoute[1]],
            
            10 => [CenterIce, _downIceBridgeRoute[0], _downIceBridgeRoute[1]],
            11 => [CenterIce, _upIceBridgeRoute[0], _upIceBridgeRoute[1]],
            12 => [CenterIce, _upIceBridgeRoute[0], _upIceBridgeRoute[2]],
            13 => [CenterIce, _downIceBridgeRoute[0], _downIceBridgeRoute[2]],
        };
        
        var dp21 = accessory.DrawGuidance(targetPosList[2], targetPosList[1], 0, 15000, $"回中路径01");
        var dp10 = accessory.DrawGuidance(targetPosList[1], targetPosList[0], 0, 15000, $"回中路径12");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp21);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp10);

        if (_myIceBridgeIdx >= 10)
            accessory.Method.TextInfo($"【先】过桥回中", 4000, false);
        else
            accessory.Method.TextInfo($"【后】过桥回中", 4000, true);
    }
    
    [ScriptMethod(name: "回中路径移除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(4099[02])"], userControl: false)]
    public void AfterIceRushRouteRemove(Event @event, ScriptAccessory accessory)
    {
        if (_phase != QueenEternalPhase.Ice) return;
        accessory.Method.RemoveDraw($"回中路径.*");
    }
    
    private bool _raisedTribute;
    [ScriptMethod(name: "接线分摊", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(020C)"], userControl: true)]
    public void RaisedTributeRoute(Event @event, ScriptAccessory accessory)
    {
        if (_phase != QueenEternalPhase.Ice) return;
        if (_raisedTributeNum != 0) return;
        
        _raisedTribute = true;
        
        var myIndex = accessory.GetMyIndex();
        if (myIndex != 4 && myIndex != 5) return;
        var topRightCorner = new Vector3(115.5f, 0, 80.5f);
        var topLeftCorner = topRightCorner.FoldPointHorizon(CenterIce.X);
        List<Vector3> targetPosList = myIndex switch
        {
            4 => [CenterIce, _upIceBridgeRoute[0], _upIceBridgeRoute[1], topLeftCorner],
            5 => [CenterIce, _upIceBridgeRoute[0], _upIceBridgeRoute[2], topRightCorner],
        };
        
        var dp01 = accessory.DrawGuidance(targetPosList[0], targetPosList[1], 0, 15000, $"D接线路径01");
        var dp12 = accessory.DrawGuidance(targetPosList[1], targetPosList[2], 0, 15000, $"D接线路径12");
        var dp23 = accessory.DrawGuidance(targetPosList[2], targetPosList[3], 0, 15000, $"D接线路径23");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp01);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp12);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp23);
    }
    
    [ScriptMethod(name: "接线分摊增加", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(41024)", "TargetIndex:1"], userControl: false)]
    public void RaisedTributeNumPlus(Event @event, ScriptAccessory accessory)
    {
        if (_phase != QueenEternalPhase.Ice) return;
        _raisedTributeNum++;
    }
    
    [ScriptMethod(name: "接线分摊第二轮", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(020C)"], userControl: false)]
    public void RaisedTributeRouteSecond(Event @event, ScriptAccessory accessory)
    {
        if (_phase != QueenEternalPhase.Ice) return;
        if (_raisedTributeNum != 1) return;
        var myIndex = accessory.GetMyIndex();
        
        _raisedTribute = false;
        accessory.Method.RemoveDraw($"D接线.*");
        
        var topRightCorner = new Vector3(115.5f, 0, 80.5f);
        var topLeftCorner = topRightCorner.FoldPointHorizon(CenterIce.X);
        
        List<int> idxList = [0, 1, 4, 5];
        if (!idxList.Contains(myIndex)) return;
        
        List<Vector3> targetPosList = myIndex switch
        {
            0 => [CenterIce, _upIceBridgeRoute[0], _upIceBridgeRoute[1], topLeftCorner],
            1 => [CenterIce, _upIceBridgeRoute[0], _upIceBridgeRoute[2], topRightCorner],
            
            4 => [CenterIce, _downIceBridgeRoute[0], _downIceBridgeRoute[1], topLeftCorner],
            5 => [CenterIce, _downIceBridgeRoute[0], _downIceBridgeRoute[2], topRightCorner],
        };

        if (myIndex <= 1)
        {
            var dp01 = accessory.DrawGuidance(targetPosList[0], targetPosList[1], 0, 15000, $"T接线路径01");
            var dp12 = accessory.DrawGuidance(targetPosList[1], targetPosList[2], 0, 15000, $"T接线路径12");
            var dp23 = accessory.DrawGuidance(targetPosList[2], targetPosList[3], 0, 15000, $"T接线路径23");
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp01);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp12);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp23);
        }
        else
        {
            var dp32 = accessory.DrawGuidance(targetPosList[3], targetPosList[2], 0, 15000, $"D返回路径32");
            var dp21 = accessory.DrawGuidance(targetPosList[2], targetPosList[1], 0, 15000, $"D返回路径21");
            var dp10 = accessory.DrawGuidance(targetPosList[1], targetPosList[0], 0, 15000, $"D返回路径10");
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp32);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp21);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp10);
        }
    }
    
    [ScriptMethod(name: "接线分摊移除", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(020C)"], userControl: false)]
    public void RaisedTributeRouteRemove(Event @event, ScriptAccessory accessory)
    {
        if (_phase != QueenEternalPhase.Ice) return;
        if (_raisedTributeNum <= 2) return;
        accessory.Method.RemoveDraw($".*");
    }
    
    #endregion 冰阶段

    #region 激进切换

    [ScriptMethod(name: "---- 《软狂暴》 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld"],
        userControl: true)]
    public void SplitLine_Enrage(Event @event, ScriptAccessory accessory)
    {
    }
    
    [ScriptMethod(name: "固定安全点", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(41039)"], userControl: true)]
    public void RadicalShift(Event @event, ScriptAccessory accessory)
    {
        if (_phase == QueenEternalPhase.Ice)
            _phase = QueenEternalPhase.Enrage;
        var myIndex = accessory.GetMyIndex();
        List<Vector3> safePos = Enumerable.Repeat(new Vector3(0, 0, 0), 8).ToList();
        safePos[0] = new Vector3(91.3f, 0f, 86.6f);
        safePos[1] = safePos[0].FoldPointHorizon(Center.X);
        safePos[2] = new Vector3(95.5f, 0f, 94.6f);
        safePos[3] = safePos[2].FoldPointHorizon(Center.X);
        safePos[4] = new Vector3(91.45f, 0f, 98f);
        safePos[5] = safePos[4].FoldPointHorizon(Center.X);
        safePos[6] = new Vector3(88.35f, 0f, 101.65f);
        safePos[7] = safePos[6].FoldPointHorizon(Center.X);
        
        var dp = accessory.DrawGuidance(safePos[myIndex], 0, 15000, $"固定安全点{myIndex}");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }
    
    [ScriptMethod(name: "固定安全点移除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(41039)"], userControl: false)]
    public void RadicalShiftRemove(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw(".*");
    }
    
    private bool _exaflareShown = false;
    [ScriptMethod(name: "地火范围", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(41043)"], userControl: true)]
    public void Exaflare(Event @event, ScriptAccessory accessory)
    {
        _exaflareShown = true;
        var sid = @event.SourceId();
        var dp = accessory.DrawCircle(sid, 6, 0, 5000, $"地火{sid}", true);
        dp.Color = exflareColor.V4.WithW(2f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        var dpWarn = accessory.DrawCircle(sid, 6, 0, 6100, $"地火警告{sid}");
        dpWarn.Offset = new Vector3(0, 0, -8.5f);
        dpWarn.Color = exflareWarnColor.V4;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpWarn);
    }
    
    
    [ScriptMethod(name: "地火范围后续", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(4104[34])"], userControl: false)]
    public void ExaflareRest(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var spos = @event.SourcePosition();
        if (!_exaflareShown) return;
        
        var dp = accessory.DrawCircle(sid, 6, 0, 1100, $"地火警告{sid}");
        dp.Offset = new Vector3(0, 0, -8.5f);
        dp.Color = exflareColor.V4.WithW(2f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        var dpWarn = accessory.DrawCircle(sid, 6, 0, 2200, $"地火警告{sid}");
        dpWarn.Offset = new Vector3(0, 0, -17f);
        dpWarn.Color = exflareWarnColor.V4;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpWarn);
    }

    #endregion 激进切换
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
        return targetObj switch
        {
            uint uintTarget => accessory.DrawGuidance(accessory.Data.Me, uintTarget, delay, destroy, name, rotation, scale),
            Vector3 vectorTarget => accessory.DrawGuidance(accessory.Data.Me, vectorTarget, delay, destroy, name, rotation, scale),
            _ => throw new ArgumentException("targetObj 的类型必须是 uint 或 Vector3")
        };
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
    public static DrawPropertiesEdit DrawStaticCircle(this ScriptAccessory accessory, Vector3 center, Vector4 color, int delay, int destroy, string name, float scale = 1.5f)
    {
        var dp = accessory.DrawStatic(center, (uint)0, 0, 0, scale, scale, color, delay, destroy, name);
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
        var dp = accessory.DrawStatic(center, (uint)0, float.Pi * 2, 0, scale, scale, color, delay, destroy, name);
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
        return target switch
        {
            uint uintTarget => accessory.DrawKnockBack(accessory.Data.Me, uintTarget, length, delay, destroy, name, width, byTime),
            Vector3 vectorTarget => accessory.DrawKnockBack(accessory.Data.Me, vectorTarget, length, delay, destroy, name, width, byTime),
            _ => throw new ArgumentException("target 的类型必须是 uint 或 Vector3")
        };
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
        return target switch
        {
            uint uintTarget => accessory.DrawSightAvoid(accessory.Data.Me, uintTarget, delay, destroy, name),
            Vector3 vectorTarget => accessory.DrawSightAvoid(accessory.Data.Me, vectorTarget, delay, destroy, name),
            _ => throw new ArgumentException("target 的类型必须是 uint 或 Vector3")
        };
    }

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
                    var dp = accessory.DrawRect(sid, width, length, delay, destroy, $"{name}{i}");
                    dp.Rotation = extendDirs[i];
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
    public static void DebugMsg(this ScriptAccessory accessory, string str, bool debugMode = true)
    {
        if (!debugMode)
            return;
        accessory.Method.SendChat($"/e [DEBUG] {str}");
    }
    
    public static string BuildListStr<T>(this ScriptAccessory accessory, List<T> myList)
    {
        return string.Join(", ", myList.Select(item => item?.ToString() ?? ""));
    }
}

public enum QueenEternalPhase : uint
{
    Init,
    Wind,
    Earth,
    Ice,
    Enrage
}

#endregion
