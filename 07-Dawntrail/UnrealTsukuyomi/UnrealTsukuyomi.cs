using System;
using System.Numerics;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using KodakkuAssist.Data;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.Draw.Manager;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.GameOperate;
using KodakkuAssist.Script;
using KodakkuAssist.Extensions;
using Newtonsoft.Json;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using KodakkuAssist.Module.Script.Type;

namespace KodakkuScripts.UsamisKodakku._07_Dawntrail.UnrealTsukuyomi;

[ScriptType(name: Name, territorys: [779], guid: "93036a00-ec46-4897-a97c-66ee03089e66",
    version: Version, author: "Usami", note: NoteStr, updateInfo: UpdateInfo)]

public class UnrealTsukuyomi
{
    const string NoteStr =
        $"""
        {Version}
        初版，目前可用于极神。
        等待幻巧更新以适配。
        """;
    
    const string UpdateInfo =
        $"""
         {Version}
         初版，目前可用于极神。
         等待幻巧更新以适配。
         """;

    private const string Name = "Tsukuyomi-Ur [月读幻巧战]";
    private const string Version = "0.0.0.1";
    private const string DebugVersion = "a";
    private const bool Debugging = false;

    private static readonly Vector3 Center = new Vector3(100, 0, 100);

    // Params
    private float _phase = 0;
    private int _nightFallCounter = 0;  // 黄泉之枪计数器，3次消失
    private int _moonPhaseWhite = 0;    // 盈亏，大于0代表白多 
    private Vector3 _nightFallGuidancePos = Vector3.Zero;
    private List<Vector3> _deadDanceFanPos = [];
    
    // Trigger
    private bool _swapColorRangeTriggered = false;
    private bool _swapColorGuideTriggered = false;
    private bool _zashikiAsobiAntitwilightEnabled = false;
    
    // Framework Guid
    private string _swapColorFirstFw = "";
    private bool _swapColorFirstLastUpsideClose = false;
    private bool _swapColorFirstInit = false;
    
    // AutoResetEvents
    private AutoResetEvent _nightFallAutoEvent = new AutoResetEvent(false);

    [UserSetting("月刀左右斩与钢铁月环画安全区")]
    public static bool LunarBladeDrawSafeRegion { get; set; } = true;
    
    public void Init(ScriptAccessory sa)
    {
        // sa.Log.Debug($"月读幻巧战 {Version}{DebugVersion} 刷新");
        RefreshParams();
        sa.Method.RemoveDraw(".*");
        sa.Method.ClearFrameworkUpdateAction(this);
    }
    
    private void RefreshParams()
    {
        _phase = 0;
        _nightFallCounter = 0;
        _moonPhaseWhite = 0;
        _nightFallGuidancePos = Vector3.Zero;
        _deadDanceFanPos = [];
        
        _swapColorRangeTriggered = false;
        _swapColorGuideTriggered = false;
        _zashikiAsobiAntitwilightEnabled = false;
        
        _swapColorFirstLastUpsideClose = false;
        _swapColorFirstInit = false;
        
        _nightFallAutoEvent = new AutoResetEvent(false);
        
    }

    [ScriptMethod(name: "---- 测试项 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void SplitLine_Test(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "[初始化] 测试项 初始化参数，清除Framework", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void RefreshParamsTest(Event ev, ScriptAccessory sa)
    {
        RefreshParams();
        sa.Method.ClearFrameworkUpdateAction(this);
    }
    
    [ScriptMethod(name: "测试项 极月读安全点", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void SelenomancySafeSpot(Event ev, ScriptAccessory sa)
    {
        var basePoint = new Vector3(106.5f, 0, 106.5f);
        Vector3[] safePoints =
        [
            basePoint, basePoint.FoldPointHorizon(Center.X), basePoint.FoldPointVertical(Center.Z),
            basePoint.PointCenterSymmetry(Center)
        ];
        foreach (var safePoint in safePoints)
        {
            sa.DrawCircle(safePoint, 0, 2000, "safePoint", 1f);
        }
    }
    
    [ScriptMethod(name: "测试项 找到半场圆心", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void FindHalfMoonCenter(Event ev, ScriptAccessory sa)
    {
        sa.DrawCircle(Center + new Vector3(-15, 0, 0), 0, 2000, "a", 25);
    }
    
    [ScriptMethod(name: "测试项 读取盈亏", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void GetMoonPhase(Event ev, ScriptAccessory sa)
    {
        // sa.Log.Debug($"盈亏值 {_moonPhaseWhite}, {(_moonPhaseWhite > 0 ? "白" : "黑")}多。");
    }
    
    [ScriptMethod(name: "测试项 在地板上画线", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void DrawLineTest(Event ev, ScriptAccessory sa)
    {
        sa.DrawLine(new Vector3(90, 0, 100), new Vector3(100, 0, 115), 0, 2000, "a", 0, 30f, 1, byY: true);
    }
    
    [ScriptMethod(name: "测试项 删除某个扇子", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void RemoveFan(Event ev, ScriptAccessory sa)
    {
        sa.WriteVisible(sa.GetById(0x40001816), false);
    }
    
    [ScriptMethod(name: "测试项 让某个扇子回来", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void CallBackFan(Event ev, ScriptAccessory sa)
    {
        sa.WriteVisible(sa.GetById(0x40001816), true);
    }
    
    [ScriptMethod(name: "---- 深宵换装 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void SplitLine_Start(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "深宵换装指路点计算", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(1119[67])$"],
        userControl: Debugging)]
    public void CalcNightFallGuide(Event ev, ScriptAccessory sa)
    {
        // 11197 黄圈之枪三连 11199
        // 11196 黄圈之弹分摊 11198
        var bossObj = sa.GetById(ev.SourceId);
        var bossPos = bossObj.Position;
        var bossRot = bossObj.Rotation;
        
        var rotate = ev.ActionId == 11196 ? 180f :
            sa.Data.MyObject.IsTank() ? 0f :
            sa.Data.MyObject.IsHealer() ? 90f : 180f;
        _nightFallGuidancePos = (bossPos + new Vector3(0, 0, 3.5f)).RotateAndExtend(bossPos, bossRot + rotate.DegToRad(), 0);
        _nightFallAutoEvent.Set();
    }
    
    [ScriptMethod(name: "开场深宵换装指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(1119[67])$"],
        userControl: true)]
    public void NightFallGuide(Event ev, ScriptAccessory sa)
    {
        if (_phase >= 1f) return;
        _nightFallAutoEvent.WaitOne();
        sa.DrawGuidance(_nightFallGuidancePos, 2000, 12000, $"深宵换装指路");
    }
    
    [ScriptMethod(name: "深宵换装职能刀范围", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11197)$"],
        userControl: true)]
    public void NightFallFanRange(Event ev, ScriptAccessory sa)
    {
        // 11197 黄圈之枪三连 11199
        // 11196 黄圈之弹分摊 11198
        if (sa.Data.PartyList.Count != 8) return;
        
        // 扇形绘制
        var fanData = new[]
        {
            (Index: 0, Role: "T", Condition: sa.Data.MyObject.IsTank()),
            (Index: 2, Role: "H", Condition: sa.Data.MyObject.IsHealer()),
            (Index: 4, Role: "D", Condition: sa.Data.MyObject.IsDps())
        };
    
        foreach (var (index, role, condition) in fanData)
        {
            sa.DrawFan(ev.SourceId, sa.Data.PartyList[index], 2000, 12000,
                $"深宵换装职能刀{role}", 60f.DegToRad(), 0, 40, 0, condition);
        }
    }
    
    [ScriptMethod(name: "深宵换装职能刀连线", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11197)$"],
        userControl: true)]
    public void NightFallFanConnect(Event ev, ScriptAccessory sa)
    {
        // 11197 黄圈之枪三连 11199
        // 11196 黄圈之弹分摊 11198
        if (sa.Data.PartyList.Count != 8) return;
        
        // 连线目标
        var targets = sa.Data.MyObject.IsDps() 
            ? sa.Data.PartyList.Skip(4).Take(4)
            : sa.Data.PartyList.Skip(sa.Data.MyObject.IsTank() ? 0 : 2).Take(2);

        Vector4[] colors = [new(0, 0, 0, 3), new(1, 1, 0, 1)];
        foreach (var target in targets.Where(t => t != sa.Data.Me))
        foreach (var color in colors)
        {
            var dp = sa.DrawConnection(sa.Data.Me, target, 2000, 12000, "深宵换装职能刀连线", draw: false);
            dp.Color = color;
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp);
        }
    }
    
    [ScriptMethod(name: "深宵换装分摊", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11196)$"],
        userControl: true)]
    public void NightFallStack(Event ev, ScriptAccessory sa)
    {
        // 11197 黄圈之枪三连 11199
        // 11196 黄圈之弹分摊 11198
        sa.DrawRect(ev.SourceId, sa.Data.Me, 2000, 10000, $"深宵换装分摊", 0, 8, 40, true);
    }
    
    [ScriptMethod(name: "深宵换装分摊绘图删除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(11198)$"],
        userControl: Debugging)]
    public void NightFallStackRemove(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"深宵换装.*");
        _nightFallAutoEvent.Reset();
    }
    
    [ScriptMethod(name: "深宵换装职能刀计数器初始化", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11197)$"],
        userControl: Debugging)]
    public void NightFallCountInit(Event ev, ScriptAccessory sa)
    {
        _nightFallCounter = 0;
        // sa.Log.Debug($"深宵换装职能刀计数器初始化");
    }
    
    [ScriptMethod(name: "深宵换装职能刀计数与绘图删除", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(11199)$", "TargetIndex:1"],
        userControl: Debugging)]
    public void NightFallFanRemove(Event ev, ScriptAccessory sa)
    {
        _nightFallCounter++;
        if (_nightFallCounter < 3) return;
        sa.Method.RemoveDraw($"深宵换装.*");
        _nightFallAutoEvent.Reset();
    }
    
    [ScriptMethod(name: "---- 极月读 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void SplitLine_Selenomancy(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "[转阶段] 极月读阶段转换", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11952)$"],
        userControl: true)]
    public void PhaseChange_Selenomancy(Event ev, ScriptAccessory sa)
    {
        _phase = 1f;
        // sa.Log.Debug($"阶段转换为 极月读 {_phase}");
    }
    
    [ScriptMethod(name: "初始换色危险区", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(153[89])$", "StackCount:4"],
        userControl: true)]
    public void SwapColorRangeFirst(Event ev, ScriptAccessory sa)
    {
        if (_phase < 1f) return;
        if (ev.TargetId != sa.Data.Me) return;
        if (_swapColorRangeTriggered) return;

        _swapColorRangeTriggered = true;
        var isWhiteBuff = ev.StatusId == 1538;
        // sa.Log.Debug($"获得状态 {(isWhiteBuff ? "白月" : "黑月")} {ev.StatusStackCount} 层");

        var dp = sa.DrawFan(Center, 0, 5000, $"极月读危险区",
            180f.DegToRad(), (isWhiteBuff ? -90f : 90f).DegToRad(), 20, 0, draw: false);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }
    
    [ScriptMethod(name: "初始换色指路", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(153[89])$", "StackCount:4"],
        userControl: true)]
    public void SwapColorGuideFirst(Event ev, ScriptAccessory sa)
    {
        if (_phase < 1f) return;
        if (ev.TargetId != sa.Data.Me) return;
        if (_swapColorGuideTriggered) return;
        
        _swapColorGuideTriggered = true;
        // 必定左白右黑，白buff去右
        var isWhiteBuff = ev.StatusId == 1538;
        var safePosUpside = new Vector3(isWhiteBuff ? 106.5f : 93.5f, 0, 93.5f);
        var safePosDownSide = safePosUpside.FoldPointVertical(Center.Z);
        // sa.Log.Debug($"开启 极月读初始换色指路 FrameWorkUpdateAction");
        
        void Action()
        {
            var myPos = sa.Data.MyObject.Position;
            
            var distanceUpSide = Vector3.Distance(myPos, safePosUpside);
            var distanceDownSide = Vector3.Distance(myPos, safePosDownSide);
            bool isCloseToUpside = distanceUpSide < distanceDownSide;
    
            // 如果状态发生变化，更新绘制
            if (isCloseToUpside == _swapColorFirstLastUpsideClose && _swapColorFirstInit) return;
            
            // 移除旧的绘制
            if (_swapColorFirstInit) sa.Method.RemoveDraw(_swapColorFirstLastUpsideClose ? $"极月读初始换色指路靠上" : $"极月读初始换色指路靠下");
            sa.DrawGuidance(isCloseToUpside ? safePosUpside : safePosDownSide, 0, Int32.MaxValue,
                isCloseToUpside ? $"极月读初始换色指路靠上" : $"极月读初始换色指路靠下");
        
            _swapColorFirstInit = true;
            _swapColorFirstLastUpsideClose = isCloseToUpside;
        }
        _swapColorFirstFw = sa.Method.RegistFrameworkUpdateAction(Action);
    }
    
    [ScriptMethod(name: "[转阶段] 删除初始换色范围与指路，阶段转换，盈亏检索初始化",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(11222)$", "TargetIndex:1"],
        userControl: Debugging, suppress: 2000)]
    public void SwapColorFirstRemove(Event ev, ScriptAccessory sa)
    {
        if (_phase > 1f) return;
        _phase = 1.1f;
        // sa.Log.Debug($"关闭 极月读初始换色指路 FrameWorkUpdateAction");
        // sa.Log.Debug($"阶段转换为 极月读：盈亏 {_phase}，检索值初始化");
        _swapColorFirstInit = false;
        _swapColorFirstLastUpsideClose = false;
        sa.Method.RemoveDraw($"极月读危险区");
        sa.Method.RemoveDraw($"极月读初始换色.*");
        sa.Method.UnregistFrameworkUpdateAction(_swapColorFirstFw);

        _moonPhaseWhite = 0;
    }
    
    [ScriptMethod(name: "检索极月读盈亏", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(153[89])$"],
        userControl: Debugging)]
    public void MoonPhaseCalculation(Event ev, ScriptAccessory sa)
    {
        if (_phase is > 1.1f or < 1f) return;
        lock (this)
        {
            var whiteCenter = new Vector3(85, 0, 100);
            var blackCenter = new Vector3(115, 0, 100);

            var playerPos = sa.GetById(ev.TargetId).Position;
            var distanceToWhite = Vector3.Distance(playerPos, whiteCenter);
            var distanceToBlack = Vector3.Distance(playerPos, blackCenter);
        
            var isWhiteBuff = ev.StatusId == 1538;
            // 如果距离白中心短，但获得了黑buff，说明异常，盈亏值进行增减。
            var isCloseToWhite = distanceToWhite < distanceToBlack;
            
            if (isCloseToWhite && !isWhiteBuff)
            {
                // 异常情况：靠近白，获得黑，盈亏靠近黑
                _moonPhaseWhite -= 3;
                // sa.Log.Debug($"{sa.GetPlayerJobById((uint)ev.TargetId)} 距离白中心近，获得黑Buff，盈亏靠近黑 {_moonPhaseWhite}");
            }
            else if (!isCloseToWhite && isWhiteBuff)
            {
                // 异常情况：靠近黑，获得白，盈亏靠近白
                _moonPhaseWhite += 3;
                // sa.Log.Debug($"{sa.GetPlayerJobById((uint)ev.TargetId)} 距离白中心远，获得白Buff，盈亏靠近白 {_moonPhaseWhite}");
            }
            else if (!isWhiteBuff && distanceToWhite > 25f)
            {
                // 极端情况：非常靠近黑，获得黑，盈亏靠近白
                _moonPhaseWhite += 1;
                // sa.Log.Debug($"{sa.GetPlayerJobById((uint)ev.TargetId)} 距离白中心很远，获得黑Buff，倾向于盈亏靠近白 {_moonPhaseWhite}");
            }
            else if (isWhiteBuff && distanceToBlack > 25f)
            {
                // 极端情况：非常靠近白，获得白，盈亏靠近黑
                _moonPhaseWhite -= 1;
                // sa.Log.Debug($"{sa.GetPlayerJobById((uint)ev.TargetId)} 距离白中心很近，获得白Buff，倾向于盈亏靠近黑 {_moonPhaseWhite}");
            }
        }
    }
    
    [ScriptMethod(name: "极月读深宵换装指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(1119[67])$"],
        userControl: true)]
    public void SelenomancyNightFallGuide(Event ev, ScriptAccessory sa)
    {
        // 11197 黄圈之枪三连 11199
        // 11196 黄圈之弹分摊 11198
        if (_phase is < 1f or >= 2f) return;
        
        // 极月读情况下直接指向方位
        var isMoonPhaseWhite = _moonPhaseWhite > 0;
        // sa.Log.Debug(
            // $"检索到盈亏 {_moonPhaseWhite} 靠近 {(isMoonPhaseWhite ? "白" : "黑")}，" +
            // $"Boss 靠近 {(isMoonPhaseWhite ? "右" : "左")}，侧边需指向 {(isMoonPhaseWhite ? "左" : "右")}");

        var bossPos = sa.GetById(ev.SourceId).Position;
        var guidanceData = new[]
        {
            (MeteorPos: new Vector3(isMoonPhaseWhite ? 103 : 97, 0, 81), Rotate: 180f, Condition: sa.Data.MyObject.IsTank()),
            (MeteorPos: new Vector3(isMoonPhaseWhite ? 81 : 119, 0, 100), Rotate: isMoonPhaseWhite ? -90f : 90f, Condition: sa.Data.MyObject.IsHealer()),
            (MeteorPos: new Vector3(isMoonPhaseWhite ? 103 : 97, 0, 119), Rotate: 0f, Condition: sa.Data.MyObject.IsDps())
        };

        var basePos = bossPos + new Vector3(0, 0, 3.5f);
        foreach (var guide in guidanceData)
        {
            // 若为分摊，不画指路线，指路箭头向背后
            var startPos = basePos.RotateAndExtend(bossPos, ev.ActionId == 11197 ? guide.Rotate.DegToRad() : 0f, 0);
            sa.DrawLine(startPos, guide.MeteorPos,
                0, 12000, $"极月读深宵换装指路线", 0, 30f, 10, guide.Condition, byY: true, draw: ev.ActionId == 11197);
            if (!guide.Condition) continue;
            sa.DrawGuidance(startPos, 0, 12000, $"极月读深宵换装指路");
        }
    }
    
    [ScriptMethod(name: "极月读放陨石指路", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(0083)$"],
        userControl: true)]
    public void PlaceMeteorGuide(Event ev, ScriptAccessory sa)
    {
        if (sa.Data.PartyList.Count != 8) return;
        if (ev.TargetId != sa.Data.Me) return;
        
        var isMoonPhaseWhite = _moonPhaseWhite > 0;
        var targetPos = sa.Data.MyObject.IsTank() ? new Vector3(isMoonPhaseWhite ? 103 : 97, 0, 81) :
            sa.Data.MyObject.IsDps() ? new Vector3(isMoonPhaseWhite ? 103 : 97, 0, 119) :
            new Vector3(isMoonPhaseWhite ? 81 : 119, 0, 100);
        sa.DrawGuidance(targetPos, 0, 10000, $"极月读放陨石");
        // sa.Log.Debug($"玩家要放陨石，盈亏 {_moonPhaseWhite} 靠近 {(isMoonPhaseWhite ? "白" : "黑")}");
    }
    
    [ScriptMethod(name: "陨石点名删除深宵换装指路", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(0083)$"],
        userControl: Debugging)]
    public void SelenomancyNightFallGuideRemove(Event ev, ScriptAccessory sa)
    {
        if (ev.TargetId != sa.Data.Me) return;
        // 如果点了自己，删除深宵换装指路
        sa.Method.RemoveDraw("极月读深宵换装指路");
    }
    
    [ScriptMethod(name: "[转阶段] 陨石点名转阶段", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(0083)$"],
        userControl: Debugging, suppress: 2000)]
    public void PhaseChange_Meteor(Event ev, ScriptAccessory sa)
    {
        _phase = 1.2f;
        // sa.Log.Debug($"阶段转换为 极月读：陨石 {_phase}");
    }
    
    [ScriptMethod(name: "陨石后删除深宵换装与放陨石指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11217)$"],
        userControl: Debugging)]
    public void PlaceMeteorGuideRemove(Event ev, ScriptAccessory sa)
    {
        // 如果点了自己，删除深宵换装指路
        sa.Method.RemoveDraw("极月读放陨石");
        sa.Method.RemoveDraw("极月读深宵换装指路");
        sa.Method.RemoveDraw("极月读深宵换装指路线");
    }
    
    [ScriptMethod(name: "极月读陨石后集合指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11217)$"],
        userControl: true, suppress: 2000)]
    public void AfterPlaceMeteorGuide(Event ev, ScriptAccessory sa)
    {
        var isMoonPhaseWhite = _moonPhaseWhite > 0;
        var targetPos = new Vector3(isMoonPhaseWhite ? 119 : 81, 0, 100);
        sa.DrawGuidance(targetPos, 0, 10000, $"极月读陨石后集合指路");
        // sa.Log.Debug($"放完陨石后集合，盈亏 {_moonPhaseWhite} 靠近 {(isMoonPhaseWhite ? "白" : "黑")}");
    }
    
    [ScriptMethod(name: "[转阶段] 月食转阶段", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(11214)$"],
        userControl: Debugging, suppress: 2000)]
    public void PhaseChange_LunarEclipse(Event ev, ScriptAccessory sa)
    {
        _phase = 1.3f;
        // sa.Log.Debug($"阶段转换为 极月读：月食 {_phase}");
    }
    
    [ScriptMethod(name: "[转阶段] 月下美人转阶段", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11224)$"],
        userControl: Debugging)]
    public void PhaseChange_Antitwilight(Event ev, ScriptAccessory sa)
    {
        _phase = 2f;
        // sa.Log.Debug($"阶段转换为 月下美人 {_phase}");
    }
    
    [ScriptMethod(name: "---- 月下美人 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void SplitLine_Antitwilight(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "死刑", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11195|11954)$"],
        userControl: true)]
    public void TankBuster(Event ev, ScriptAccessory sa)
    {
        sa.DrawFan(ev.SourceId, ev.TargetId, 0, 5000,
            $"死刑", 90f.DegToRad(), 0, 15, 0);
    }
    
    [ScriptMethod(name: "月下美人宴会游乐指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11205)$"],
        userControl: true)]
    public void ZashikiAsobiAntitwilight(Event ev, ScriptAccessory sa)
    {
        if (_phase > 2f) return;
        _zashikiAsobiAntitwilightEnabled = true;
        var startPos = sa.Data.MyObject.IsDps() ? new Vector3(100, 0, 111) : new Vector3(92, 0, 107);
        sa.DrawGuidance(startPos, 0, 10000, $"宴会游乐第一步准备", isSafe: false);
        _nightFallAutoEvent.WaitOne();
        sa.DrawGuidance(startPos, _nightFallGuidancePos, 0, 10000, $"宴会游乐第二步准备", isSafe: false);
    }
    
    [ScriptMethod(name: "月下美人宴会游乐指路后续1", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11206)$"],
        userControl: Debugging, suppress: 10000)]
    public void ZashikiAsobiAntitwilightAfter1(Event ev, ScriptAccessory sa)
    {
        if (_phase > 2f) return;
        if (!_zashikiAsobiAntitwilightEnabled) return;
        var startPos = sa.Data.MyObject.IsDps() ? new Vector3(100, 0, 111) : new Vector3(92, 0, 107);
        sa.Method.RemoveDraw($"宴会游乐第一步准备");
        sa.DrawGuidance(startPos, 0, 10000, $"宴会游乐第一步", isSafe: true);
    }
    
    [ScriptMethod(name: "月下美人宴会游乐指路后续2", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(11206)$"],
        userControl: Debugging, suppress: 10000)]
    public void ZashikiAsobiAntitwilightAfter2(Event ev, ScriptAccessory sa)
    {
        if (_phase > 2f) return;
        if (!_zashikiAsobiAntitwilightEnabled) return;
        sa.Method.RemoveDraw($"宴会游乐第一步");
        sa.Method.RemoveDraw($"宴会游乐第二步准备");
        sa.DrawGuidance(_nightFallGuidancePos, 0, 10000, $"深宵换装宴会游乐第二步");
    }
    
    [ScriptMethod(name: "*月下美人宴会游乐删除舞扇判定动画", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(11206)$", "TargetIndex:1"],
        userControl: true)]
    public void ZashikiAsobiAntitwilightRemoveFan(Event ev, ScriptAccessory sa)
    {
        if (_phase > 2f) return;

        var fanObj = sa.GetById(ev.SourceId);
        sa.WriteVisible(fanObj, false);
    }
    
    [ScriptMethod(name: "---- 黄泉之舞 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void SplitLine_DeadDance(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "[转阶段] 黄泉之舞转阶段", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(11472)$", "TargetIndex:1"],
        userControl: Debugging)]
    public void PhaseChange_DeadDance(Event ev, ScriptAccessory sa)
    {
        _phase = 3f;
        // sa.Log.Debug($"阶段转换为 黄泉之舞 {_phase}");
    }
    
    [ScriptMethod(name: "月刀左右斩与钢铁月环", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(1122[67])$"],
        userControl: true)]
    public unsafe void LunarBlade(Event ev, ScriptAccessory sa)
    {
        var bossObj = sa.GetById(ev.SourceId);
        var isLeftBlade = ev.ActionId == 11227;
        var isChariot = ((IBattleChara?)bossObj).HasStatus(1535);

        if (LunarBladeDrawSafeRegion)
        {
            var rotate = (isLeftBlade ? -105f : 105f).DegToRad();
            var (outScale, innerScale) = isChariot ? (40, 10) : (5, 0);

            sa.DrawFan(ev.SourceId, 0, 7000, $"月刀斩{(isLeftBlade ? "右" : "左")}{(isChariot ? "外" : "内")}安全",
                150f.DegToRad(), rotate, outScale, innerScale, true);
        }
        else
        {
            // 画危险区
            sa.DrawFan(ev.SourceId, 0, 6000, $"月刀斩扇形", 210f.DegToRad(),
                (isLeftBlade ? 75f : -75f).DegToRad(), 40, 0);
        
            // 拥有buff满月流/新月流
            if (isChariot) sa.DrawCircle(ev.SourceId, 0, 6000, $"月刀斩钢铁", 10);
            else sa.DrawDonut(ev.SourceId, 0, 6000, $"月刀斩月环", 40, 5);
        }
    }
    
    [ScriptMethod(name: "黄泉之舞扇子记录", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(8755)$"],
        userControl: Debugging)]
    public void DeadDanceFanRecord(Event ev, ScriptAccessory sa)
    {
        if (_phase < 3f) return;
        lock (_deadDanceFanPos)
        {
            _deadDanceFanPos.Add(sa.GetById(ev.SourceId).Position);
        }
    }
    
    [ScriptMethod(name: "黄泉之舞扇子记录初始化", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11195|11954|11194)$"],
        userControl: Debugging)]
    public void DeadDanceFanRecordInit(Event ev, ScriptAccessory sa)
    {
        // 技能分别为魔法死刑、物理死刑、AOE
        if (_phase < 3f) return;
        _deadDanceFanPos = [];
    }
    
    [ScriptMethod(name: "月下缭乱连续分摊范围", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11228)$"],
        userControl: true)]
    public void LunacyStack(Event ev, ScriptAccessory sa)
    {
        sa.DrawCircle(ev.TargetId, 0, 8000, $"月下缭乱", 6, true);
    }
    
    [ScriptMethod(name: "黄泉之舞九连环指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11228)$"],
        userControl: true)]
    public void ShivaRoundGuidance(Event ev, ScriptAccessory sa)
    {
        var fanCount = _deadDanceFanPos.Count;
        // sa.Log.Debug($"扇子List中当前记录了{_deadDanceFanPos.Count}个坐标");
        if (fanCount < 4) return;   // 不够判断
        
        // 九连环，找第2、3个坐标，判断方位
        var region2 = _deadDanceFanPos[1].GetRadian(Center).RadianToRegion(8, 0, true);
        var region3 = _deadDanceFanPos[2].GetRadian(Center).RadianToRegion(8, 0, true);
        
        // 顺逆时针判断
        var safePosAtCw = (region2 - region3 + 8) % 8 == 7; // 扇子逆时针增加，前一个减后一个取余后得7，安全点在顺时针方向
        var safeRegion = (region2 + (safePosAtCw ? -1 : 1) + 8) % 8; 
        // sa.Log.Debug($"第2枚在方位{region2}，第3枚在方位{region3}，安全区是{(safePosAtCw ? "顺" : "逆")}时针方向，在方位{safeRegion}");

        var basePoint = new Vector3(100, 0, 111);
        var startPoint = basePoint.RotateAndExtend(Center, safeRegion * 45f.DegToRad(), 0);
        sa.DrawGuidance(startPoint, 0, 5000, $"月下缭乱第一步");
        sa.DrawGuidance(startPoint, Center, 0, 5000, $"月下缭乱第二步准备", isSafe: false);
        sa.DrawGuidance(Center, 5000, 3000, $"月下缭乱第二步", isSafe: true);
    }
    
    [ScriptMethod(name: "破月点名范围", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(0017)$"],
        userControl: true)]
    public void HagetsuTargetRange(Event ev, ScriptAccessory sa)
    {
        if (_phase < 3f) return;
        
        var targetObj = (IBattleChara)sa.GetById(ev.TargetId);
        var myObj = sa.Data.MyObject;

        var isSameRole = (myObj.IsTank() && targetObj.IsTank()) || (myObj.IsHealer() && targetObj.IsHealer()) ||
                         (myObj.IsDps() && targetObj.IsDps());
        sa.DrawCircle(ev.TargetId, 0, 8000, $"破月", 6, isSameRole);
    }
    
    [ScriptMethod(name: "黄泉之舞破月指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11548)$"],
        userControl: true)]
    public void HagetsuGuidance(Event ev, ScriptAccessory sa)
    {
        var fanCount = _deadDanceFanPos.Count;
        // sa.Log.Debug($"扇子List中当前记录了{_deadDanceFanPos.Count}个坐标");
        if (fanCount < 6) return;   // 不够判断

        // 0 2 4 8 => 1 10 100 1000
        var safeRegionFlag = 1111;
        foreach (var fanPos in _deadDanceFanPos.Take(6))
        {
            if (Vector3.Distance(fanPos, Center) < 5f) continue;
            // 找到四个正点坐标，判断方位
            var region = fanPos.GetRadian(Center).RadianToRegion(8, 0, true);
            if (region % 2 != 0) continue;
            safeRegionFlag -= (int)Math.Pow(10, region / 2);
            // sa.Log.Debug($"{region} {region % 2} {safeRegionFlag}");
        }
        var safeRegion = (int)Math.Log10(safeRegionFlag) * 2;
        // sa.Log.Debug($"基于标志 {safeRegionFlag}，破月安全正点方位为 {safeRegion}");
        
        var guidanceData = new[]
        {
            (SafeRegion: safeRegion - 1, Condition: sa.Data.MyObject.IsTank()),
            (SafeRegion: safeRegion + 1, Condition: sa.Data.MyObject.IsHealer()),
            (SafeRegion: safeRegion, Condition: sa.Data.MyObject.IsDps())
        };

        var basePoint = new Vector3(100, 0, 111);
        foreach (var guide in guidanceData)
        {
            var startPoint = basePoint.RotateAndExtend(Center, guide.SafeRegion * 45f.DegToRad(), 0);
            var endPoint = startPoint.RotateAndExtend(Center, 0, -6);

            sa.DrawLine(Center, startPoint.RotateAndExtend(Center, 0, 9),
                0, 8000, $"指路线", 0, 30f, 1f, guide.Condition, byY: true);
            
            if (!guide.Condition) continue;
            sa.DrawGuidance(startPoint, 0, 5000, $"破月第一步");
            sa.DrawGuidance(startPoint, endPoint, 0, 5000, $"破月第二步准备", isSafe: false);
            sa.DrawGuidance(endPoint, 5000, 3000, $"破月第二步", isSafe: true);
        }
    }

}

#region 函数集

public static class IbcHelper
{
    public static IGameObject? GetById(this ScriptAccessory sa, ulong gameObjectId)
    {
        return sa.Data.Objects.SearchById(gameObjectId);
    }
    
}
#region 计算函数

public static class MathTools
{
    public static float DegToRad(this float deg) => (deg + 360f) % 360f / 180f * float.Pi;
    public static float RadToDeg(this float rad) => (rad + 2 * float.Pi) % (2 * float.Pi) / float.Pi * 180f;
    
    /// <summary>
    /// 获得任意点与中心点的弧度值，以(0, 0, 1)方向为0，以(1, 0, 0)方向为pi/2。
    /// 即，逆时针方向增加。
    /// </summary>
    /// <param name="point">任意点</param>
    /// <param name="center">中心点</param>
    /// <returns></returns>
    public static float GetRadian(this Vector3 point, Vector3 center)
        => MathF.Atan2(point.X - center.X, point.Z - center.Z);

    /// <summary>
    /// 获得任意点与中心点的长度。
    /// </summary>
    /// <param name="point">任意点</param>
    /// <param name="center">中心点</param>
    /// <returns></returns>
    public static float GetLength(this Vector3 point, Vector3 center)
        => new Vector2(point.X - center.X, point.Z - center.Z).Length();
    
    /// <summary>
    /// 将任意点以中心点为圆心，逆时针旋转并延长。
    /// </summary>
    /// <param name="point">任意点</param>
    /// <param name="center">中心点</param>
    /// <param name="radian">旋转弧度</param>
    /// <param name="length">基于该点延伸长度</param>
    /// <returns></returns>
    public static Vector3 RotateAndExtend(this Vector3 point, Vector3 center, float radian, float length)
    {
        var baseRad = point.GetRadian(center);
        var baseLength = point.GetLength(center);
        var rotRad = baseRad + radian;
        return new Vector3(
            center.X + MathF.Sin(rotRad) * (length + baseLength),
            center.Y,
            center.Z + MathF.Cos(rotRad) * (length + baseLength)
        );
    }
    
    /// <summary>
    /// 获得某角度所在划分区域
    /// </summary>
    /// <param name="radian">输入弧度</param>
    /// <param name="regionNum">区域划分数量</param>
    /// <param name="baseRegionIdx">0度所在区域的初始Idx</param>>
    /// <param name="isDiagDiv">是否为斜分割，默认为false</param>
    /// <param name="isCw">是否顺时针增加，默认为false</param>
    /// <returns></returns>
    public static int RadianToRegion(this float radian, int regionNum, int baseRegionIdx = 0, bool isDiagDiv = false, bool isCw = false)
    {
        var sepRad = float.Pi * 2 / regionNum;
        var inputAngle = radian * (isCw ? -1 : 1) + (isDiagDiv ? sepRad / 2 : 0);
        var rad = (inputAngle + 4 * float.Pi) % (2 * float.Pi);
        return ((int)Math.Floor(rad / sepRad) + baseRegionIdx + regionNum) % regionNum;
    }
    
    /// <summary>
    /// 将输入点左右折叠
    /// </summary>
    /// <param name="point">待折叠点</param>
    /// <param name="centerX">中心折线坐标点</param>
    /// <returns></returns>
    public static Vector3 FoldPointHorizon(this Vector3 point, float centerX)
        => point with { X = 2 * centerX - point.X };

    /// <summary>
    /// 将输入点上下折叠
    /// </summary>
    /// <param name="point">待折叠点</param>
    /// <param name="centerZ">中心折线坐标点</param>
    /// <returns></returns>
    public static Vector3 FoldPointVertical(this Vector3 point, float centerZ)
        => point with { Z = 2 * centerZ - point.Z };

    /// <summary>
    /// 将输入点中心对称
    /// </summary>
    /// <param name="point">输入点</param>
    /// <param name="center">中心点</param>
    /// <returns></returns>
    public static Vector3 PointCenterSymmetry(this Vector3 point, Vector3 center) 
        => point.RotateAndExtend(center, float.Pi, 0);
}

#endregion 计算函数

#region 位置序列函数
public static class IndexHelper
{
    /// <summary>
    /// 输入玩家dataId，获得对应的位置称呼，输出字符仅作文字输出用
    /// </summary>
    /// <param name="pid">玩家SourceId</param>
    /// <param name="sa"></param>
    /// <returns>该玩家对应的位置称呼</returns>
    public static string GetPlayerJobById(this ScriptAccessory sa, uint pid)
    {
        // 获得玩家职能简称，无用处，仅作DEBUG输出
        var idx = sa.Data.PartyList.IndexOf(pid);
        var str = sa.GetPlayerJobByIndex(idx);
        return str;
    }

    /// <summary>
    /// 输入位置index，获得对应的位置称呼，输出字符仅作文字输出用
    /// </summary>
    /// <param name="idx">位置index</param>
    /// <param name="fourPeople">是否为四人迷宫</param>
    /// <param name="sa"></param>
    /// <returns></returns>
    public static string GetPlayerJobByIndex(this ScriptAccessory sa, int idx, bool fourPeople = false)
    {
        List<string> role8 = ["MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4"];
        List<string> role4 = ["T", "H", "D1", "D2"];
        if (idx < 0 || idx >= 8 || (fourPeople && idx >= 4))
            return "Unknown";
        return fourPeople ? role4[idx] : role8[idx];
    }
}
#endregion 位置序列函数

#region 绘图函数

public static class DrawTools
{
    /// <summary>
    /// 返回绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">绘图基准，可为UID或位置</param>
    /// <param name="targetObj">绘图指向目标，可为UID或位置</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="radian">绘制图形弧度范围</param>
    /// <param name="rotation">绘制图形旋转弧度，以owner面前为基准，逆时针增加</param>
    /// <param name="width">绘制图形宽度，部分图形可保持与长度一致</param>
    /// <param name="length">绘制图形长度，部分图形可保持与宽度一致</param>
    /// <param name="innerWidth">绘制图形内宽，部分图形可保持与长度一致</param>
    /// <param name="innerLength">绘制图形内长，部分图形可保持与宽度一致</param>
    /// <param name="drawModeEnum">绘图方式</param>
    /// <param name="drawTypeEnum">绘图类型</param>
    /// <param name="isSafe">是否使用安全色</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="byY">动画效果随距离变更</param>
    /// <param name="draw">是否直接绘图</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawOwnerBase(this ScriptAccessory sa, 
        object ownerObj, object targetObj, int delay, int destroy, string name, 
        float radian, float rotation, float width, float length, float innerWidth, float innerLength,
        DrawModeEnum drawModeEnum, DrawTypeEnum drawTypeEnum, bool isSafe = false,
        bool byTime = false, bool byY = false, bool draw = true)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.InnerScale = new Vector2(innerWidth, innerLength);
        dp.Radian = radian;
        dp.Rotation = rotation;
        dp.Color = isSafe ? sa.Data.DefaultSafeColor: sa.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        dp.ScaleMode |= byY ? ScaleMode.YByDistance : ScaleMode.None;
        
        switch (ownerObj)
        {
            case uint u:
                dp.Owner = u;
                break;
            case ulong ul:
                dp.Owner = ul;
                break;
            case Vector3 spos:
                dp.Position = spos;
                break;
            default:
                throw new ArgumentException($"ownerObj {ownerObj} 的目标类型 {ownerObj.GetType()} 输入错误");
        }

        switch (targetObj)
        {
            case 0:
            case 0u:
                break;
            case uint u:
                dp.TargetObject = u;
                break;
            case ulong ul:
                dp.TargetObject = ul;
                break;
            case Vector3 tpos:
                dp.TargetPosition = tpos;
                break;
            default:
                throw new ArgumentException($"targetObj {targetObj} 的目标类型 {targetObj.GetType()} 输入错误");
        }
        
        if (draw)
            sa.Method.SendDraw(drawModeEnum, drawTypeEnum, dp);
        return dp;
    }

    /// <summary>
    /// 返回指路绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">出发点</param>
    /// <param name="targetObj">结束点</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="rotation">箭头旋转角度</param>
    /// <param name="width">箭头宽度</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawGuidance(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name,
        float rotation = 0, float width = 1f, bool isSafe = true, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 0, rotation, width,
            width, 0, 0, DrawModeEnum.Imgui, DrawTypeEnum.Displacement, isSafe, false, true, draw);
    
    public static DrawPropertiesEdit DrawGuidance(this ScriptAccessory sa,
        object targetObj, int delay, int destroy, string name, float rotation = 0, float width = 1f, bool isSafe = true,
        bool draw = true)
        => sa.DrawGuidance((ulong)sa.Data.Me, targetObj, delay, destroy, name, rotation, width, isSafe, draw);

    /// <summary>
    /// 返回圆形绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">圆心</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="scale">圆形径长</param>
    /// <param name="byTime">是否随时间扩充</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawCircle(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name,
        float scale, bool isSafe = false, bool byTime = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, 0, delay, destroy, name, 2 * float.Pi, 0, scale, scale,
            0, 0, DrawModeEnum.Default, DrawTypeEnum.Circle, isSafe, byTime,false, draw);

    /// <summary>
    /// 返回环形绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">圆心</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="outScale">外径</param>
    /// <param name="innerScale">内径</param>
    /// <param name="byTime">是否随时间扩充</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawDonut(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name,
        float outScale, float innerScale, bool isSafe = false, bool byTime = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, 0, delay, destroy, name, 2 * float.Pi, 0, outScale, outScale, innerScale,
            innerScale, DrawModeEnum.Default, DrawTypeEnum.Donut, isSafe, byTime, false, draw);

    /// <summary>
    /// 返回扇形绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">圆心</param>
    /// <param name="targetObj">目标</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="radian">弧度</param>
    /// <param name="rotation">旋转角度</param>
    /// <param name="outScale">外径</param>
    /// <param name="innerScale">内径</param>
    /// <param name="byTime">是否随时间扩充</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawFan(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name, float radian, float rotation,
        float outScale, float innerScale, bool isSafe = false, bool byTime = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, radian, rotation, outScale, outScale, innerScale,
            innerScale, DrawModeEnum.Default, innerScale == 0 ? DrawTypeEnum.Fan : DrawTypeEnum.Donut, isSafe, byTime, false, draw);

    public static DrawPropertiesEdit DrawFan(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name, float radian, float rotation,
        float outScale, float innerScale, bool isSafe = false, bool byTime = false, bool draw = true)
        => sa.DrawFan(ownerObj, 0, delay, destroy, name, radian, rotation, outScale, innerScale, isSafe, byTime, draw);

    /// <summary>
    /// 返回矩形绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">矩形起始</param>
    /// <param name="targetObj">目标</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="rotation">旋转角度</param>
    /// <param name="width">矩形宽度</param>
    /// <param name="length">矩形长度</param>
    /// <param name="byTime">是否随时间扩充</param>
    /// <param name="byY">是否随距离扩充</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawRect(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name, float rotation,
        float width, float length, bool isSafe = false, bool byTime = false, bool byY = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 0, rotation, width, length, 0, 0,
            DrawModeEnum.Default, DrawTypeEnum.Rect, isSafe, byTime, byY, draw);
    
    public static DrawPropertiesEdit DrawRect(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name, float rotation,
        float width, float length, bool isSafe = false, bool byTime = false, bool byY = false, bool draw = true)
        => sa.DrawRect(ownerObj, 0, delay, destroy, name, rotation, width, length, isSafe, byTime, byY, draw);

    /// <summary>
    /// 返回线型绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">线条起始</param>
    /// <param name="targetObj">线条目标</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="rotation">旋转角度</param>
    /// <param name="width">线条宽度</param>
    /// <param name="length">线条长度</param>
    /// <param name="byTime">是否随时间扩充</param>
    /// <param name="byY">是否随距离扩充</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawLine(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name, float rotation,
        float width, float length, bool isSafe = false, bool byTime = false, bool byY = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 1, rotation, width, length, 0, 0,
            DrawModeEnum.Default, DrawTypeEnum.Line, isSafe, byTime, byY, draw);
    
    /// <summary>
    /// 返回两对象间连线绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">起始源</param>
    /// <param name="targetObj">目标源</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="width">线宽</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawConnection(this ScriptAccessory sa, object ownerObj, object targetObj,
        int delay, int destroy, string name, float width = 1f, bool isSafe = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 0, 0, width, width,
            0, 0, DrawModeEnum.Imgui, DrawTypeEnum.Line, isSafe, false, true, draw);
}

#endregion 绘图函数

#region 特殊函数

public static class SpecialFunction
{
    [Flags]
    public enum DrawState : uint
    {
        Invisibility      = 0x00_00_00_02,
        IsLoading         = 0x00_00_08_00,
        SomeNpcFlag       = 0x00_00_01_00,
        MaybeCulled       = 0x00_00_04_00,
        MaybeHiddenMinion = 0x00_00_80_00,
        MaybeHiddenSummon = 0x00_80_00_00,
    }
    
    public static unsafe DrawState* ActorDrawState(IGameObject actor)
        => (DrawState*)(&((GameObject*)actor.Address)->RenderFlags);
    
    public static unsafe void WriteVisible(this ScriptAccessory sa, IGameObject? actor, bool visible)
    {
        try
        {
            *ActorDrawState(actor!) |= visible ? ~DrawState.Invisibility : DrawState.Invisibility;
        }
        catch (Exception e)
        {
            sa.Log.Error(e.ToString());
            throw;
        }
    }
}

#endregion 特殊函数

#endregion 函数集

