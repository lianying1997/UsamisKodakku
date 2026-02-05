using System;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent.Struct;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Data;
using KodakkuAssist.Module.Draw.Manager;
using KodakkuAssist.Module.GameOperate;
using KodakkuAssist.Module.GameEvent.Types;
using KodakkuAssist.Extensions;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Numerics;
using Newtonsoft.Json;
using System.Linq;
using System.ComponentModel;
using Dalamud.Utility.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel.Sheets;

namespace UsamisKodakku.Scripts._06_EndWalker.TopReborn;

[ScriptType(name: Name, territorys: [1122], guid: "6272e52b-5500-4ff0-8634-dd15f2e3b25e",
    version: Version, author: "Usami", note: NoteStr, updateInfo: UpdateInfo)]

// ^(?!.*((武僧|机工士|龙骑士|武士|忍者|蝰蛇剑士|钐镰客|舞者|吟游诗人|占星术士|贤者|学者|(朝日|夕月)小仙女|炽天使|白魔法师|战士|骑士|暗黑骑士|绝枪战士|绘灵法师|黑魔法师|青魔法师|召唤师|宝石兽|亚灵神巴哈姆特|亚灵神不死鸟|迦楼罗之灵|泰坦之灵|伊弗利特之灵|后式自走人偶)\] (Used|Cast))).*35501.*$
// ^\[\w+\|[^|]+\|E\]\s\w+

public class TopReborn
{
    const string NoteStr =
        $"""
        v{Version}
        修改自Karlin/Meva的脚本，
        谨慎使用特殊功能。
        """;
    
    const string UpdateInfo =
        $"""
         {Version}
         1. 修复P1线塔与全能之主，涉及对位H2/D4的优先级错误（复制粘贴惨案）。
         """;

    private const string Name = "绝欧精装 Reborn";
    private const string Version = "0.0.0.6";
    private const string DebugVersion = "a";

    private const bool Debugging = false;

    private static readonly List<string> Role = ["MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4"];
    private static readonly Vector3 Center = new Vector3(100, 0, 100);
    
    private static PriorityDict _pd = new PriorityDict();       // 灵活多用字典
    private double _parse = 0;

    private static P1StateParams _p1 = new();
    private static P2StateParams _p2 = new();
    private static P3StateParams _p3 = new();
    private static P4StateParams _p4 = new();
    private static P5AStateParams _p5A = new();
    private static P5BStateParams _p5B = new();
    private static P5CStateParams _p5C = new();
    private static P6StateParams _p6 = new();
    
    [UserSetting("启用方法设置中带*的特殊功能")]
    public bool SpecialMode { get; set; } = false;

    public void Init(ScriptAccessory sa)
    {
        RefreshParams(sa);
        sa.DebugMsg($"脚本 {Name} v{Version}{DebugVersion} 完成初始化.", Debugging);
        sa.Method.RemoveDraw(".*");
        sa.Method.ClearFrameworkUpdateAction(this);
    }
    
    private void RefreshParams(ScriptAccessory sa)
    {
        _pd = new PriorityDict();
        _parse = 0;
        ResetSupportUnitVisibility(sa);

        _p1.Reset(sa);
        _p1.Dispose();
        _p2.Reset(sa);
        _p2.Dispose();
        _p3.Reset(sa);
        _p3.Dispose();
        _p4.Reset(sa);
        _p4.Dispose();
        _p5A.Reset(sa);
        _p5A.Dispose();
        _p5B.Reset(sa);
        _p5B.Dispose();
        _p5C.Reset(sa);
        _p5C.Dispose();
        _p6.Reset(sa);
        _p6.Dispose();
    }

    private void ResetSupportUnitVisibility(ScriptAccessory sa)
    {
        const uint SUPPORTER_DATAID = 9020;
        var objEnums = sa.GetByDataId(SUPPORTER_DATAID);
        unsafe
        {
            foreach (var obj in objEnums)
            {
                sa.WriteVisible(obj, true);
            }
        }
    }
    
    [ScriptMethod(name: "测试项：展示优先级表格", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void 展示优先级表格(Event ev, ScriptAccessory sa)
    {
        sa.DebugMsg(_pd.ShowPriorities(), Debugging);
    }
    
    #region P1A 循环程序
    
    [ScriptMethod(name: "———————— 《P1A 循环程序》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P1A_循环程序_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P1A_循环程序_分P", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31491"], userControl: Debugging)]
    public void P1A_循环程序_分P(Event ev, ScriptAccessory sa)
    {
        _parse = 1.1;
        sa.DebugMsg($"当前阶段为：{_parse}", Debugging);
        _p1.BossId = ev.TargetId;
        _p1.Register();
        _pd.Init(sa, "P1线塔");
        _pd.AddPriorities([2, 3, 1, 8, 4, 5, 6, 7]);    // 数值越低，代表优先级越高
    }
    
    [ScriptMethod(name: "P1A_循环程序_Buff记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(3004|3005|3006|3451)$"], userControl: Debugging)]
    public void P1A_循环程序_Buff记录(Event ev, ScriptAccessory sa)
    {
        if (_parse != 1.1) return;
        if (ev.SourceId == 0x00000000) return;
        var idx = sa.GetPlayerIdIndex((uint)ev.TargetId);
        var priVal = ev.StatusId switch
        {
            3004 => 10,
            3005 => 20,
            3006 => 30,
            3451 => 40,
            _ => 0
        };
        _pd.AddPriority(idx, priVal);
    }
    
    [ScriptMethod(name: "P1A_循环程序_塔收集", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["Operate:Add", "DataId:2013245"], userControl: Debugging)]
    public void P1A_循环程序_塔收集(Event ev, ScriptAccessory sa)
    {
        if (_parse != 1.1) return;
        lock (_p1.塔字典)
        {
            // sa.DebugMsg($"准备塔收集 R{_p1.线塔轮次} {_p1.前一轮绘图清除完毕.WaitOne(0)}", Debugging);
            
            if (_p1.线塔轮次 != 0)
                _p1.前一轮绘图清除完毕.WaitOne();
            
            var towerPos = ev.SourcePosition;
            var towerPriority = towerPos.GetRadian(Center).RadianToRegion(4, baseRegionIdx: 2, isDiagDiv: true, isCw: true);   // 以北为0，顺时针增加
            _p1.塔字典[towerPriority] = towerPos;
            // sa.DebugMsg($"在第 {_p1.线塔轮次} 轮收集到方位 {towerPriority} 的塔", Debugging);
            if (_p1.塔字典.Count != 2) return;
            _p1.线塔轮次++;
            sa.DebugMsg($"线塔轮次增加 {_p1.线塔轮次} ", Debugging);
            _p1.每轮塔收集完毕.Set();
            _p1.前一轮绘图清除完毕.Reset();
        }
    }
    
    [ScriptMethod(name: "P1A_循环程序_集合提醒", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31491"], userControl: true)]
    public void P1A_循环程序_集合提醒(Event ev, ScriptAccessory sa)
    {
        if (_parse != 1.1) return;
        sa.Method.TextInfo("Boss背后集合", 2000);
        sa.Method.TTS("Boss背后集合");
    }
    
    [ScriptMethod(name: "P1A_循环程序_开始站位提醒", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(3004|3005|3006|3451)$"])]
    public void P1A_循环程序_开始站位提醒(Event ev, ScriptAccessory sa)
    {
        if (_parse != 1.1) return;
        if (ev.TargetId != sa.Data.Me) return;

        var isFirstTether = ev.StatusId == 3006;
        sa.Method.TextInfo(isFirstTether ? "靠前接线" : "靠后", 3000);
        sa.Method.TTS(isFirstTether ? "靠前接线" : "靠后");
    }

    [ScriptMethod(name: "P1A_循环程序_清理绘图", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(3149[67])$"], suppress: 100, userControl: Debugging)]
    public void P1A_循环程序_清理绘图(Event ev, ScriptAccessory sa)
    {
        if (_parse != 1.1) return;
        
        sa.DebugMsg($"清除循环程序绘图 Round {_p1.线塔轮次}", Debugging);
        
        sa.Method.RemoveDraw($"P1_循环程序_R{_p1.线塔轮次}.*");
        _p1.塔字典 = new Dictionary<int, Vector3>();
        _p1.上一次靠近状态 = 0;
        sa.Method.UnregistFrameworkUpdateAction(_p1.闲人指路Framework);
        _p1.前一轮绘图清除完毕.Set();
        
        if (_p1.线塔轮次 < 4) return;
        sa.Method.UnregistFrameworkUpdateAction(_p1.扫描接线Framework);
    }
    
    [ScriptMethod(name: "P1A_循环程序_线塔处理位置", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["Operate:Add", "DataId:2013245"], suppress: 500)]
    public void P1A_循环程序_线塔处理位置(Event ev, ScriptAccessory sa)
    {
        if (_parse != 1.1) return;
        _p1.每轮塔收集完毕.WaitOne();
        int  myIndex      = sa.GetMyIndex();
        int  myPriVal     = _pd.Priorities[myIndex];
        int  myTowerRound = myPriVal / 10;
        int  myLineRound  = (myTowerRound - 1 + 2) % 4 + 1;
        int  myPriority   = _pd.FindPriorityIndexOfKey(myIndex);   // 升序排列，从0开始，偶数则优先级高，奇数则优先级低
        bool isHighPrior  = myPriority % 2 == 0;

        try
        {
            if (_p1.线塔轮次 == myTowerRound)
            {
                var myTower = isHighPrior ? _p1.塔字典.MinBy(kvp => kvp.Key) : _p1.塔字典.MaxBy(kvp => kvp.Key);
                // sa.DebugMsg($"现在是第 {_p1.线塔轮次} 轮线塔，是玩家的塔轮次，玩家需踩方位为 {myTower.Key} 的塔（以北为0，顺时针增加）", Debugging);
                sa.DrawGuidance(myTower.Value, 0, 9000, $"P1_循环程序_R{_p1.线塔轮次}_塔站位");
                sa.DrawCircle(myTower.Value, 0, 9000, $"P1_循环程序_R{_p1.线塔轮次}_塔范围", 3f, isSafe: true);
            }
            else if (_p1.线塔轮次 == myLineRound)
            {
                var myLineRegion = isHighPrior ? 0 : 3;
                while (_p1.塔字典.ContainsKey(myLineRegion))
                {
                    myLineRegion += isHighPrior ? 1 : -1;
                    if (myLineRegion is < 4 and > -1) continue;
                    sa.Log.Error($"没有找到玩家接线安全区");
                    return;
                }
                var myLinePos = new Vector3(100, 0, 85).RotateAndExtend(Center, myLineRegion * -90f.DegToRad());
                // sa.DebugMsg($"现在是第 {_p1.线塔轮次} 轮线塔，是玩家的线轮次，玩家需将线接到方位 {myLineRegion} （以北为0，顺时针增加）", Debugging);
                sa.DrawGuidance(myLinePos, 0, 9000, $"P1_循环程序_R{_p1.线塔轮次}_线站位");
            }
            else
            {
                _p1.闲人指路Framework = sa.Method.RegistFrameworkUpdateAction(Action);
                // sa.DebugMsg($"现在是第 {_p1.线塔轮次} 轮线塔，玩家闲人，在塔附近躲开即可（以北为0，顺时针增加）", Debugging);

                if (_p1.线塔轮次 < myTowerRound)
                {
                    sa.DrawCircle(_p1.塔字典.First().Value, 0, 9000, $"P1_循环程序_R{_p1.线塔轮次}_塔1范围危险", 3f, isSafe: false);
                    sa.DrawCircle(_p1.塔字典.Last().Value, 0, 9000, $"P1_循环程序_R{_p1.线塔轮次}_塔2范围危险", 3f, isSafe: false);
                }
                
                void Action()
                {
                    Vector3  myPos           = sa.Data.MyObject.Position;
                    Vector3  tower1Safe      = new Vector3(100, 0, 86).RotateAndExtend(Center, _p1.塔字典.First().Key * -90f.DegToRad());
                    Vector3  tower2Safe      = new Vector3(100, 0, 86).RotateAndExtend(Center, _p1.塔字典.Last().Key * -90f.DegToRad());
                    float    distanceTower1  = Vector3.Distance(myPos, tower1Safe);
                    float    distanceTower2  = Vector3.Distance(myPos, tower2Safe);
                    int      currentStatus   = distanceTower1 < distanceTower2 ? 1 : 2;
    
                    // 如果状态发生变化，更新绘制
                    if (currentStatus == _p1.上一次靠近状态) return;
            
                    // 移除旧的绘制
                    sa.Method.RemoveDraw($"P1_循环程序_R{_p1.线塔轮次}_闲站位{_p1.上一次靠近状态}");
                    sa.DrawGuidance(currentStatus == 1 ? tower1Safe : tower2Safe, 0, Int32.MaxValue, $"P1_循环程序_R{_p1.线塔轮次}_闲站位{currentStatus}");
                    _p1.上一次靠近状态 = currentStatus;
                }
            }
        }
        finally
        {
            _p1.每轮塔收集完毕.Reset();
        }
    }

    [ScriptMethod(name: "P1A_循环程序_接线标记", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(31496)$", "TargetIndex:1"], suppress: 500)]
    public void P1A_循环程序_接线标记(Event ev, ScriptAccessory sa)
    {
        if (_parse != 1.1) return;
        _p1.每轮塔收集完毕.WaitOne(1000);
        
        int  myIndex      = sa.GetMyIndex();
        int  myPriVal     = _pd.Priorities[myIndex];
        int  myTowerRound = myPriVal / 10;
        int  myLineRound  = (myTowerRound - 1 + 2) % 4 + 1;

        if (_p1.线塔轮次 != myLineRound) return;
        
        int  myPriority     = _pd.FindPriorityIndexOfKey(myIndex);  // 升序排列，从0开始，偶数则优先级高，奇数则优先级低
        int  targetPriority = (myPriority - 2 + 8) % 8;             // 需要接线玩家的优先级Idx
        var  targetPartyIndex = _pd.SelectSpecificPriorityIndex(targetPriority).Key;

        var dp = sa.DrawLine(ev.TargetId, sa.Data.PartyList[targetPartyIndex], 0, 9000,
            $"P1_循环程序_R{_p1.线塔轮次}_接线标记", 0, 5, 10, byY: true, isSafe: true, draw: false);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp);
        
        // sa.DebugMsg($"玩家优先级升序序列为 {myPriority}，需接序列为 {targetPriority} {sa.GetPlayerJobByIndex(targetPartyIndex)} 的线", Debugging);
    }
    
    [ScriptMethod(name: "P1A_循环程序_接线标记移除", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0059"], userControl: Debugging)]
    public void P1A_循环程序_接线标记移除(Event ev, ScriptAccessory sa)
    {
        if (_parse != 1.1) return;
        if (ev.SourceId != sa.Data.Me) return;
        sa.Method.RemoveDraw($"P1_循环程序_R{_p1.线塔轮次}_接线标记");
    }
    
    [ScriptMethod(name: "P1A_循环程序_接线玩家大圈绘图", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0059"], userControl: true)]
    public void P1A_循环程序_接线玩家大圈绘图(Event ev, ScriptAccessory sa)
    {
        if (_parse != 1.1) return;

        if (_p1.扫描接线开启) return;
        _p1.扫描接线开启 = true;
        
        // 当且仅当本轮正确的玩家接线，且其与Boss有一段距离，才会绘制大圈
        _p1.扫描接线Framework = sa.Method.RegistFrameworkUpdateAction(Action);
        
        const uint TETHER_ID = 0x59;
        return;
        
        void Action()
        {
            var bossObj = sa.GetById(_p1.BossId);
            if (bossObj is null) return;
            
            foreach (var member in sa.Data.PartyList)
            {
                // 找到小队成员
                IGameObject? memberObj = sa.GetById(member);
                if (memberObj is null) continue;
                int memberIdx = sa.GetPlayerIdIndex(member);
                
                void CleanUp()
                {
                    _p1.接线绘图字典.Remove(memberIdx, out _);
                    sa.Method.RemoveDraw($"P1_循环程序_R{_p1.线塔轮次}_线源{memberIdx}");
                }

                if (!_pd.Priorities.TryGetValue(memberIdx, out int memberPrival) ||
                    memberPrival < 10) { CleanUp(); continue; }    // 优先级未准备好
                
                // 计算线塔轮次
                int memberTowerRound = memberPrival / 10;
                int memberLineRound  = (memberTowerRound - 1 + 2) % 4 + 1;
                if (memberLineRound != _p1.线塔轮次) { CleanUp(); continue; }

                // 距离判断
                float distance = Vector3.Distance(memberObj.Position, bossObj.Position);
                if (distance < 5f) { CleanUp(); continue; }
                
                // 线源追溯
                var tetherSource = sa.GetTetherSource((IBattleChara?)memberObj, TETHER_ID);
                bool isCorrectTether = tetherSource.Count == 1 && tetherSource[0] == _p1.BossId;
                if (!isCorrectTether) { CleanUp(); continue; }

                // 避免反复绘图
                if (_p1.接线绘图字典.TryAdd(memberIdx, true))
                    sa.DrawCircle(member, 0, Int32.MaxValue, $"P1_循环程序_R{_p1.线塔轮次}_线源{memberIdx}", 15);

            }
        }
    }
    
    #endregion P1A 循环程序

    #region P1B 全能之主

    [ScriptMethod(name: "———————— 《P1B 全能之主》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P1B_全能之主_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P1B_全能之主_分P", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31499"], userControl: Debugging)]
    public void P1B_全能之主_分P(Event ev, ScriptAccessory sa)
    {
        _parse = 1.2;
        _pd.Init(sa, "P1全能之主");
        _pd.AddPriorities([2, 3, 1, 8, 4, 5, 6, 7]);    // 数值越低，代表优先级越高
        _p1.全能之主轮次 = 1;
    }
    
    [ScriptMethod(name: "P1B_全能之主_Buff记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(3004|3005|3006|3451)$"], userControl: Debugging)]
    public void P1B_全能之主_Buff记录(Event ev, ScriptAccessory sa)
    {
        if (_parse != 1.2) return;
        if (ev.SourceId == 0x00000000) return;
        var idx = sa.GetPlayerIdIndex((uint)ev.TargetId);
        var priVal = ev.StatusId switch
        {
            3004 => 10,
            3005 => 20,
            3006 => 30,
            3451 => 40,
            _ => 0
        };
        _pd.AddPriority(idx, priVal); 
    }
    
    [ScriptMethod(name: "P1B_全能之主_顺逆时针记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(31501|32368)$"], userControl: Debugging)]
    public void P1B_全能之主_顺逆时针记录(Event ev, ScriptAccessory sa)
    {
        if (_parse != 1.2) return;
        if (_p1.全能之主顺逆时针判断完毕) return;

        const uint FIRST_FLAME = 31501;
        const uint REST_FLAME = 32368;
        
        lock (_p1)
        {
            if (ev.ActionId == FIRST_FLAME && _p1.全能之主第一次角度寄存 < -8)
            {
                _p1.全能之主第一次角度寄存 = ev.SourceRotation;
                sa.DebugMsg($"得到全能之主第一次角度寄存 {_p1.全能之主第一次角度寄存.RadToDeg()}", Debugging);
            }
                
            if (ev.ActionId == REST_FLAME)
            {
                float diff = ev.SourceRotation.GetDiffRad(_p1.全能之主第一次角度寄存);
                sa.DebugMsg($"计算当前角度{ev.SourceRotation.RadToDeg()} 与 前一次的差为 {diff.RadToDeg()}", Debugging);
                if (MathF.Abs(diff) > float.Pi / 2) return;
                _p1.全能之主为顺时针 = diff < 0;
                _p1.全能之主顺逆时针判断完毕 = true;
                sa.DebugMsg($"全能之主方向判断完毕，{(diff < 0 ? "顺" : "逆")}时针", Debugging);
                _p1.全能之主顺逆时针判断完毕事件.Set();
            }
        }
    }
    
    [ScriptMethod(name: "P1B_全能之主_起跑线", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(31501)$"], userControl: true)]
    public void P1B_全能之主_起跑线(Event ev, ScriptAccessory sa)
    {
        if (_parse != 1.2) return;
        if (_p1.全能之主起跑线绘制完毕) return;
        _p1.全能之主起跑线绘制完毕 = true;
        
        int  myIndex      = sa.GetMyIndex();
        int  myPriority   = _pd.FindPriorityIndexOfKey(myIndex);   // 升序排列，从0开始，偶数则优先级高，奇数则优先级低
        bool isHighPrior  = myPriority % 2 == 0;
        
        var startRegion = _p1.全能之主第一次角度寄存.RadianToRegion(12, 8, isDiagDiv: true);
        startRegion = (startRegion + 3) % 12;     // 某个安全区角度
        sa.DebugMsg($"某个安全区角度：{startRegion}，我是高优先级：{isHighPrior}，我排第 {myPriority}", Debugging);
        var isMyRegion = startRegion < 6 && isHighPrior;
        if (!isMyRegion)
            startRegion = (startRegion + 6) % 12;
        sa.DebugMsg($"最终决定的安全区角度：{startRegion}", Debugging);
        var bossObj = sa.GetById(_p1.BossId);
        if (bossObj is null) return;
        var bossPos = bossObj.Position;
        
        var startRad = ((startRegion + 12 - 8) % 12 * 30f).DegToRad();
        // 绘制一条从boss本体出发到tempRad的线
        var dp = sa.DrawLine(bossPos, 0, 0, 6000, $"P1_全能之主_起跑线", startRad, 1f, 20f, true, draw: false);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dp);
        
        _p1.全能之主顺逆时针判断完毕事件.WaitOne();
        var routeRad = startRad + (_p1.全能之主为顺时针 ? -float.Pi / 2 : float.Pi / 2);
        // 绘制顺逆时针表示
        for (int i = 0; i < 4; i++)
        {
            var basePoint = bossPos + new Vector3(0, 0, 1);
            var startPoint = basePoint.RotateAndExtend(bossPos, startRad, (i + 1) * 4.5f);
            var dp0 = sa.DrawLine(startPoint, 0, 0, 6000, $"P1_全能之主_起跑指针", routeRad, 1f, 2f, true, draw: false);
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dp0);
        }
        
        _p1.全能之主顺逆时针判断完毕事件.Reset();
    }
    
    [ScriptMethod(name: "P1B_全能之主_轮次增加", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31502"], userControl: Debugging, suppress: 500)]
    public void P1B_全能之主_轮次增加(Event ev, ScriptAccessory sa)
    {
        if (_parse != 1.2) return;
        sa.Method.RemoveDraw($"P1_全能之主_R{_p1.全能之主轮次}.*");
        _p1.全能之主轮次++;
        if (_p1.全能之主轮次 > 4) return;
        sa.DebugMsg($"现在是 全能之主 第{_p1.全能之主轮次}轮", Debugging);
    }
    
    [ScriptMethod(name: "P1B_全能之主_出去提示", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(349[567]|3424)$"])]
    public void P1B_全能之主_出去提示(Event ev, ScriptAccessory sa)
    {
        if (_parse != 1.2) return;
        
        if (ev.TargetId != sa.Data.Me) return;
        _p1.全能之主出去时间Framework = sa.Method.RegistFrameworkUpdateAction(Action);
        return;
        
        void Action()
        {
            var statusId = ev.StatusId;
            var dur = sa.GetStatusRemainingTime(sa.Data.MyObject, statusId);
            if (dur > 5f) return;
            sa.Method.TextInfo("出去出去", 2000);
            sa.Method.TTS("出去出去");
            sa.Method.UnregistFrameworkUpdateAction(_p1.全能之主出去时间Framework);
        }
    }
    
    [ScriptMethod(name: "P1B_全能之主_回头提示", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31502", "TargetIndex:1"])]
    public void P1B_全能之主_回头提示(Event ev, ScriptAccessory sa)
    {
        if (_parse != 1.2) return;
        if (ev.TargetId != sa.Data.Me) return;
        sa.Method.TextInfo("回头", 2000);
        sa.Method.TTS("回头");
    }
    
    [ScriptMethod(name: "P1B_全能之主_点名直线", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0017"])]
    public void P1B_全能之主_点名直线(Event ev, ScriptAccessory sa)
    {
        if (_parse != 1.2) return;
        if (sa.Data.MyObject.IsTank()) return;  // 坦克无需关注
        bool isMe = ev.TargetId == sa.Data.Me;
        sa.DrawRect(_p1.BossId, ev.TargetId, 0, 5000, $"P1_全能之主_点名直线", 0, 6, 50, isMe);
    }
    
    [ScriptMethod(name: "P1B_全能之主_后半指路", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0017"], suppress: 15000)]
    public void P1B_全能之主_后半指路(Event ev, ScriptAccessory sa)
    {
        if (_parse != 1.2) return;
        var bossObj = sa.GetById(_p1.BossId);
        if (bossObj is null) return;
        var bossPos = bossObj.Position;

        List<float> rotDeg = [180, 180, -54, 54, -18, 18, -90, 90];
        int myIndex = sa.GetMyIndex();
        var pos = (bossPos + new Vector3(0, 0, 10)).RotateAndExtend(bossPos, rotDeg[myIndex].DegToRad(),
            sa.Data.MyObject.IsTank() ? 2.5f : 0);
        sa.DrawGuidance(pos, 0, 6000, $"P1_全能之主_后半指路");

        for (int i = 0; i < 8; i++)
        {
            var dp = sa.DrawLine(bossPos, 0, 0, 6000, $"P1_全能之主_指引线", rotDeg[i].DegToRad(), 20f, 20f, draw: false);
            dp.Color = i switch
            {
                0 or 1 => new Vector4(0.1f, 0.1f, 1, 1),
                2 or 3 => new Vector4(0.1f, 1f, 0.1f, 1),
                _ => new Vector4(1, 0.1f, 0.1f, 1),
            };
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Line, dp);
        }
    }
    
    [ScriptMethod(name: "P1B_全能之主_最远顺劈范围", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0017"], suppress: 15000)]
    public void P1B_全能之主_最远顺劈范围(Event ev, ScriptAccessory sa)
    {
        if (_parse != 1.2) return;

        var bossObj = sa.GetById(_p1.BossId);
        if (bossObj is null) return;
        _p1.全能之主最远距离Framework = sa.Method.RegistFrameworkUpdateAction(Action);

        void Action()
        {
            // 1. 建立距离字典
            var bossPos = bossObj.Position;
            var distanceDict = new Dictionary<int, float>(sa.Data.PartyList.Count);
            foreach (var memberId in sa.Data.PartyList)
            {
                var member = sa.GetById(memberId);
                if (member == null || !member.IsValid()) continue;
                distanceDict[sa.GetPlayerIdIndex(memberId)] = Vector3.Distance(member.Position, bossPos);
            }
            
            // 2. 只取前两名
            var topTwo = distanceDict.OrderByDescending(kvp => kvp.Value)
                .Take(2)
                .Select(kvp => kvp.Key)
                .ToArray();
            
            // 3. 模式判断
            int myIndex = sa.GetMyIndex();
            bool isInTopTwo = topTwo.Contains(myIndex);
            bool isTank = sa.Data.MyObject.IsTank();

            const int IS_TANK_NOT_TOP2_DANGER = 1;
            const int NOT_TANK_IS_TOP2_DANGER = 2;
            const int NOT_TANK_NOT_TOP2_SAFE = 3;
            const int IS_TANK_IS_TOP2_SAFE = -1;
            
            int currentState = (isTank, isInTopTwo) switch
            {
                (true, false) => IS_TANK_NOT_TOP2_DANGER,
                (false, true) => NOT_TANK_IS_TOP2_DANGER,
                (false, false) => NOT_TANK_NOT_TOP2_SAFE,
                _ => IS_TANK_IS_TOP2_SAFE
            };
            
            // 4. 状态未改变直接返回
            if (currentState == _p1.上一次远距离状态) return;

            // 5. 是坦克且在最远，不绘图，直接返回且清除绘制
            if (currentState == IS_TANK_IS_TOP2_SAFE)
            {
                sa.Method.RemoveDraw("P1_全能之主后半_顺劈状态.*");
                return;
            }
            
            var isDangerous = currentState != NOT_TANK_NOT_TOP2_SAFE;
            var radian = (isDangerous ? 30f : 120f).DegToRad();
            var color = isDangerous ? new Vector4(1f, 0.1f, 0.1f, 2f) : sa.Data.DefaultSafeColor;
            
            // 绘制逻辑
            if (currentState == NOT_TANK_IS_TOP2_DANGER)
            {
                DrawFanCleave(myIndex, radian, color, !isDangerous, currentState);
            }
            else if (currentState == IS_TANK_NOT_TOP2_DANGER)
            {
                if (topTwo[0] > 1) DrawFanCleave(topTwo[0], radian, color, !isDangerous, currentState);
                if (topTwo[1] > 1) DrawFanCleave(topTwo[1], radian, color, !isDangerous, currentState);
            }
            else
            {
                // 最远是谁，与我何干，直接画T
                DrawFanCleave(0, radian, color, !isDangerous, currentState);
                DrawFanCleave(1, radian, color, !isDangerous, currentState);
            }
            
            _p1.上一次远距离状态 = currentState;
            return;
            
            void DrawFanCleave(int playerIdx, float radian, Vector4 color, bool isSafe, int state)
            {
                var dp = sa.DrawFan(_p1.BossId, sa.Data.PartyList[playerIdx], 0, int.MaxValue,
                    $"P1_全能之主后半_顺劈状态{state}", radian, 0, 20, 0, isSafe: isSafe, draw: false);
                dp.Color = color;
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

                switch (state)
                {
                    case IS_TANK_NOT_TOP2_DANGER:
                        sa.Method.RemoveDraw($"P1_全能之主后半_顺劈状态{NOT_TANK_IS_TOP2_DANGER}");
                        sa.Method.RemoveDraw($"P1_全能之主后半_顺劈状态{NOT_TANK_NOT_TOP2_SAFE}");
                        break;
                    case NOT_TANK_IS_TOP2_DANGER:
                        sa.Method.RemoveDraw($"P1_全能之主后半_顺劈状态{IS_TANK_NOT_TOP2_DANGER}");
                        sa.Method.RemoveDraw($"P1_全能之主后半_顺劈状态{NOT_TANK_NOT_TOP2_SAFE}");
                        break;
                    case NOT_TANK_NOT_TOP2_SAFE:
                        sa.Method.RemoveDraw($"P1_全能之主后半_顺劈状态{IS_TANK_NOT_TOP2_DANGER}");
                        sa.Method.RemoveDraw($"P1_全能之主后半_顺劈状态{NOT_TANK_IS_TOP2_DANGER}");
                        break;
                }
            }
        }
    }

    [ScriptMethod(name: "P1B_全能之主_扩散波动炮计数", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31504"], suppress: 200, userControl: Debugging)]
    public void P1B_全能之主_扩散波动炮计数(Event ev, ScriptAccessory sa)
    {
        if (_parse != 1.2) return;
        _p1.扩散波动炮次数++;

        if (_p1.扩散波动炮次数 == 5)
        {
            sa.Method.UnregistFrameworkUpdateAction(_p1.全能之主最远距离Framework);
            sa.Method.RemoveDraw($"P1.*");
        }
    }

    #endregion P1B 全能之主

    #region P2 欧米茄防火墙设置

    [ScriptMethod(name: "———————— 《P2 欧米茄防火墙设置》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P2_欧米茄防火墙设置_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P2A_防火墙_分P", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31552"], userControl: Debugging)]
    public void P2A_防火墙_分P(Event ev, ScriptAccessory sa)
    {
        _parse = 2;
        sa.Method.RemoveDraw($"P1.*");
        _p1.Reset(sa);
        _p1.Dispose();
        _p2.Register();
    }
    
    [ScriptMethod(name: "P2A_防火墙_BossId记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3155[23])$"], userControl: Debugging)]
    public void P2A_防火墙_BossId记录(Event ev, ScriptAccessory sa)
    {
        const uint FIREWALL_MALE = 31552;
        const uint FIREWALL_FEMALE = 31553;

        switch (ev.ActionId)
        {
            case FIREWALL_FEMALE:
                _p2.BossIdFemale = ev.SourceId;
                break;
            case FIREWALL_MALE:
                _p2.BossIdMale = ev.SourceId;
                break;
            default:
                return;
        }
    }
    
    [ScriptMethod(name: "*P2A_防火墙_屏蔽无效目标", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31552"], userControl: true)]
    public void P2A_防火墙_屏蔽无效目标(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2) return;
        
        if (!SpecialMode) return;
        _p2.使能防火墙 = true;
        _p2.判断防火墙目标Framework = sa.Method.RegistFrameworkUpdateAction(Action);
        const uint MALE_DISABLE = 3499;
        const uint FEMALE_DISABLE = 3500;
        return;
    
        void Action()
        {
            var myObject = sa.Data.MyObject;
            if (myObject is null) return;

            if (!_p2.使能防火墙)
            {
                if (_p2.上一次防火墙状态 == 0) return;
                // P2一运期间
                sa.SetTargetable(sa.GetById(_p2.BossIdMale), false);
                sa.SetTargetable(sa.GetById(_p2.BossIdFemale), false);
                _p2.上一次防火墙状态 = 0;
                return;
            }
            
            const int MALE_UNTARGETABLE = 1;
            const int FEMALE_UNTARGETABLE = 2;
            const int FREELY_TARGETABLE = 3;
            const int UNREACHABLE = 4;
            
            int currentState = (myObject.HasStatus(MALE_DISABLE), myObject.HasStatus(FEMALE_DISABLE)) switch
            {
                (true, false) => MALE_UNTARGETABLE,
                (false, true) => FEMALE_UNTARGETABLE,
                (false, false) => FREELY_TARGETABLE,
                _ => UNREACHABLE
            };
            
            if (currentState == _p2.上一次防火墙状态 && _p2.上一次防火墙状态 != 0) return;
            
            _p2.上一次防火墙状态 = currentState;
            sa.SetTargetable(sa.GetById(_p2.BossIdMale), !myObject.HasStatus(MALE_DISABLE));
            sa.SetTargetable(sa.GetById(_p2.BossIdFemale), !myObject.HasStatus(FEMALE_DISABLE));
        }
    }

    [ScriptMethod(name: "P2A_防火墙_一运期间暂时关闭", eventType: EventTypeEnum.Targetable,
        eventCondition: ["Targetable:False", "DataId:regex:^(1571[23])$"], userControl: Debugging, suppress: 1000)]
    public void P2A_防火墙_一运期间暂时关闭(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.1) return;
        sa.DebugMsg($"P2A_防火墙_一运期间暂时关闭", Debugging);
        _p2.使能防火墙 = false;
    }
    
    [ScriptMethod(name: "P2A_防火墙_男女人性别交换", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(3151[78])$"], userControl: Debugging, suppress: 1000)]
    public void P2A_防火墙_男女人性别交换(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.1) return;
        sa.DebugMsg($"P2A_防火墙_男女人性别交换", Debugging);
        (_p2.BossIdFemale, _p2.BossIdMale) = (_p2.BossIdMale, _p2.BossIdFemale);
    }
    
    [ScriptMethod(name: "*P2A_防火墙_一运后再次开启", eventType: EventTypeEnum.Targetable,
        eventCondition: ["Targetable:True", "DataId:regex:^(1571[23])$"], userControl: Debugging, suppress: 1000)]
    public void P2A_防火墙_一运后再次开启(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.15) return;
        if (!SpecialMode) return;
        sa.DebugMsg($"P2A_防火墙_一运后再次开启", Debugging);
        _p2.使能防火墙 = true;
    }

    [ScriptMethod(name: "P2A_防火墙_二运前关闭防火墙判断", eventType: EventTypeEnum.StatusRemove,
        eventCondition: ["StatusID:regex:^(3500|3499)$"], userControl: Debugging, suppress: 1000)]
    public void P2A_防火墙_二运前关闭防火墙判断(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.2) return;
        sa.Method.UnregistFrameworkUpdateAction(_p2.判断防火墙目标Framework);
        _p2.使能防火墙 = false;
        sa.SetTargetable(sa.GetById(_p2.BossIdMale), true);
        sa.SetTargetable(sa.GetById(_p2.BossIdFemale), true);
    }
    
    [ScriptMethod(name: "*P2A_防火墙_无敌设置不可选中", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(671)$"], userControl: true)]
    public void P2_防火墙_无敌设置不可选中(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.2) return;
        if (!SpecialMode) return;
        sa.SetTargetable(sa.GetById(ev.TargetId), false);
    }
    
    [ScriptMethod(name: "P2A_防火墙_无敌移除", eventType: EventTypeEnum.StatusRemove, eventCondition: ["StatusID:regex:^(671)$"], userControl: Debugging)]
    public void P2_防火墙_无敌移除(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.2) return;
        sa.SetTargetable(sa.GetById(ev.TargetId), true);
    }
    
    #endregion P2 欧米茄防火墙设置

    #region P2A 协作程序

    [ScriptMethod(name: "———————— 《P2A 协作程序》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P2A_协作程序_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P2A_协作程序_分P", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31550"], userControl: Debugging)]
    public void P2A_协作程序_分P(Event ev, ScriptAccessory sa)
    {
        _parse = 2.1;
        _pd.Init(sa, "P2索尼");
        _pd.AddPriorities([2, 3, 1, 8, 4, 5, 6, 7]);    // 数值越低，代表优先级越高
    }
    
    [ScriptMethod(name: "P2A_协作程序_远近记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(3427|3428)$"], userControl: Debugging, suppress: 10000)]
    public void P2_协作程序_远近记录(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.1) return;
        const uint MID_GLITCH = 3427, REMOTE_GLITCH = 3428;
        _p2.协作程序是远线 = ev.StatusId == REMOTE_GLITCH;
        sa.DebugMsg($"记录下协作程序是 {(_p2.协作程序是远线 ? "远" : "近")} 线", Debugging);
    }
    
    [ScriptMethod(name: "P2A_协作程序_索尼记录", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(01A[0123])$"], userControl: Debugging)]
    public void P2_协作程序_索尼记录(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.1) return;
        const uint CIRCLE_1 = 416, CROSS_2 = 419, TRIANGLE_3 = 417, SQUARE_4 = 418;
        lock (_pd)
        {
            var idx = sa.GetPlayerIdIndex((uint)ev.TargetId);
            var priVal = ev.Id0() switch
            {
                CIRCLE_1 => 10,
                CROSS_2 => 20,
                TRIANGLE_3 => 30,
                SQUARE_4 => 40,
                _ => 0
            };
            _pd.AddPriority(idx, priVal);
            _pd.AddActionCount();
            if (_pd.ActionCount != 8) return;

            // 为排在右边的四个人加上100优先值
            var (key1, key2, key3, key4) = (_pd.SelectSpecificPriorityIndex(1).Key,
                _pd.SelectSpecificPriorityIndex(3).Key,
                _pd.SelectSpecificPriorityIndex(5).Key, _pd.SelectSpecificPriorityIndex(7).Key);
            
            _pd.AddPriority(key1, 100);
            _pd.AddPriority(key2, 100);
            _pd.AddPriority(key3, 100);
            _pd.AddPriority(key4, 100);
        }

    }
    
    [ScriptMethod(name: "P2A_协作程序_男女人攻击范围", eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["Id:7747", "SourceDataId:regex:^(1571[45])$"])]
    public void P2_协作程序_男女人攻击范围(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.1) return;
        Vector3 pos = ev.SourcePosition;
        if (Vector3.Distance(pos, Center) > 12) return;
        
        var obj = sa.GetById(ev.SourceId);
        if (obj == null) return;
        
        var dataId = obj.DataId;
        var transId = sa.GetTransformationId(obj);
        if (transId == null) return;
        
        const uint MAN = 15714, WOMAN = 15715;
        // const byte MAN_CHARIOT = 0, MAN_DONUT = 4, WOMAN_CROSS = 0, WOMAN_HOTWING = 4;

        switch (dataId == MAN, transId == 0)
        {
            case (true, true):
                sa.DrawCircle(ev.SourceId, 0, 5500, $"P2_协作程序_男女人攻击范围_男钢铁", 10);
                break;
            case (true, false):
                sa.DrawDonut(ev.SourceId, 0, 5500, $"P2_协作程序_男女人攻击范围_男月环", 40, 10);
                break;
            case (false, true):
                var dp1 = sa.DrawRect(ev.SourceId, 0, 5500, $"P2_协作程序_男女人攻击范围_女十字1", 0, 10, 60, draw: false);
                var dp2 = sa.DrawRect(ev.SourceId, 0, 5500, $"P2_协作程序_男女人攻击范围_女十字2", float.Pi / 2, 10, 60, draw: false);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp1);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp2);
                break;
            case (false, false):
                var dp3 = sa.DrawDonut(ev.SourceId, 0, 5500, $"P2_协作程序_男女人攻击范围_女辣翅", 60, 8, draw: false);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.HotWing, dp3);
                break;
        }
    }

    [ScriptMethod(name: "P2A_协作程序_男女人攻击范围删除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(3152[56])$"], userControl: Debugging, suppress: 10000)]
    public void P2_协作程序_男女人攻击范围删除(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.1) return;
        // 采用男人的钢铁月环释放标记删除
        sa.Method.RemoveDraw($"P2_协作程序_男女人攻击范围.*");
        _p2.眼睛激光准备绘图.Set();
    }

    [ScriptMethod(name: "P2A_协作程序_眼睛激光范围", eventType: EventTypeEnum.EnvControl, eventCondition: ["Flag:2"])]
    public void P2_协作程序_眼睛激光范围(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.1) return;
        _p2.眼睛激光准备绘图.WaitOne();
        var basePos = new Vector3(100, 0, 80);
        var eyePos = basePos.RotateAndExtend(Center, -45f.DegToRad() * _p2.大眼睛方位);
        sa.DrawRect(eyePos, Center, 0, 10000, "P2_协作程序_眼睛激光", 0, 16, 40);
        _p2.眼睛激光准备绘图.Reset();
    }
    
    [ScriptMethod(name: "P2A_协作程序_眼睛激光与索尼指路删除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31521"], userControl: Debugging)]
    public void P2_协作程序_眼睛激光与索尼指路删除(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.1) return;
        sa.Method.RemoveDraw($"P2_协作程序_眼睛激光");
        sa.Method.RemoveDraw($"P2_协作程序_索尼站位");
    }

    [ScriptMethod(name: "P2A_协作程序_眼睛激光方位记录", eventType: EventTypeEnum.EnvControl, eventCondition: ["Flag:2"], userControl: Debugging)]
    public void P2_协作程序_眼睛激光方位记录(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.1) return;
        var region = ev.Index() - 1;
        sa.DebugMsg($"记录下Flag2，region：{region}", Debugging);
        // Index从1开始，以A为初始，顺时针增加
        _p2.大眼睛方位 = (int)region;
        _p2.眼睛激光方位记录.Set();
    }
    
    [ScriptMethod(name: "P2A_协作程序_索尼站位", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(3152[56])$"],
        userControl: true, suppress: 10000)]
    public void P2_协作程序_索尼站位(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.1) return;
        _p2.眼睛激光方位记录.WaitOne();
        
        // BasePos的定义均以 圆高-叉高-角高-方高-圆低-...的顺序
        List<Vector3> middleBasePos =
        [
            new(88.5f, 0, 85.5f), new(88.5f, 0, 95.0f), new(88.5f, 0, 105.0f), new(88.5f, 0, 114.5f),
            new(111.5f, 0, 85.5f), new(111.5f, 0, 95.5f), new(111.5f, 0, 105.0f), new(111.5f, 0, 114.5f)
        ];
        List<Vector3> farBasePos =
        [
            new(91.5f, 0, 83.0f), new(82.0f, 0, 93.0f), new(82.0f, 0, 107.0f), new(91.5f, 0, 117.0f),
            new(108.5f, 0, 117.0f), new(118.0f, 0, 107.0f), new(118.0f, 0, 93.0f), new(108.5f, 0, 83.0f)
        ];

        sa.DebugMsg(_pd.ShowPriorities(), Debugging);
        var rank = _pd.FindPriorityIndexOfKey(sa.GetMyIndex());
        var pos = (_p2.协作程序是远线 ? farBasePos : middleBasePos)[rank].RotateAndExtend(Center, _p2.大眼睛方位 * -45f.DegToRad());
        sa.DrawGuidance(pos, 0, 10000, $"P2_协作程序_索尼站位");
        
        _p2.眼睛激光方位记录.Reset();
    }

    [ScriptMethod(name: "P2A_协作程序_男人钢铁位置记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31516"], userControl: Debugging)]
    public void P2_协作程序_男人钢铁位置记录(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.1) return;
        _p2.男人钢铁方位 = ev.SourcePosition.GetRadian(Center).RadianToRegion(8, isDiagDiv: true);
        _p2.男人钢铁方位记录.Set();
    }
    
    [ScriptMethod(name: "P2A_协作程序_分摊记录", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0064"], userControl: Debugging)]
    public void P2_协作程序_分摊站位(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.1) return;
        lock (_pd)
        {
            var index = sa.GetPlayerIdIndex((uint)ev.TargetId);
            _pd.AddPriority(index, 1000);
            
            // 找到连线搭档，增加优先值
            var icon = _pd.Priorities[index].GetDecimalDigit(2);
            foreach (var kvp in _pd.Priorities)
            {
                if (kvp.Key == index) continue;
                if (kvp.Value.GetDecimalDigit(2) != icon) continue;
                _pd.AddPriority(kvp.Key, 500);
            }
            
            _pd.AddActionCount();
            if (_pd.ActionCount < 10) return;
            _p2.分摊记录.Set();
            sa.DebugMsg(_pd.ShowPriorities(), Debugging);
            _parse = 2.15;
            sa.DebugMsg($"阶段转为{_parse}", Debugging);
        }
    }
    
    [ScriptMethod(name: "P2A_协作程序_分摊指路", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31521"], userControl: true)]
    public void P2_协作程序_分摊指路(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.1) return;
        
        _p2.分摊记录.WaitOne();
        _p2.男人钢铁方位记录.WaitOne();
        
        try
        {
            int leftRegion = (_p2.男人钢铁方位 + 2 + 8) % 8;
            int rightRegion = (_p2.男人钢铁方位 - (_p2.协作程序是远线 ? 2 : 4) + 8) % 8;

            var dp1 = sa.DrawLine(Center, 0, 0, 6000, $"P2_协作程序_指引线左", leftRegion * 45f.DegToRad(), 20f, 20f,
                draw: false);
            dp1.Color = new Vector4(0.1f, 1f, 0.1f, 1);
            var dp2 = sa.DrawLine(Center, 0, 0, 6000, $"P2_协作程序_指引线右", rightRegion * 45f.DegToRad(), 20f, 20f,
                draw: false);
            dp2.Color = new Vector4(0.1f, 1f, 0.1f, 1);
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Line, dp1);
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Line, dp2);
        
            // 左：男人钢铁方位记录+2；右：-2（远）或-4（中）
            const int IDLE = 1;
            const int STACK_PARTNER = 2;
            const int STACK_SOURCE = 3;

            int myRank = _pd.FindPriorityIndexOfKey(sa.GetMyIndex());
            (int myState, string myStateStr) = myRank switch
            {
                <= 3 => (IDLE, "闲人"),
                > 3 and <= 5 => (STACK_PARTNER, "分摊搭档"),
                > 5 and <= 7 => (STACK_SOURCE, "分摊源"),
                _ => (0, "未知")
            };
            if (myState == 0) return;
        
            int myPriVal = _pd.Priorities[sa.GetMyIndex()];
            int safePosRegion = 0;
            bool bothStackSourceAtRight = _pd.SelectSpecificPriorityIndex(1, true).Value.GetDecimalDigit(3) == 1;
            bool bothStackSourceAtLeft = _pd.SelectSpecificPriorityIndex(0, true).Value.GetDecimalDigit(3) == 0;
            
            switch (myState)
            {
                case IDLE:
                {
                    safePosRegion = myRank <= 1 ? leftRegion : rightRegion;
                    break;
                }
                case STACK_PARTNER:
                {
                    var needReverse = bothStackSourceAtLeft || (bothStackSourceAtRight && _p2.协作程序是远线);
                    safePosRegion = myRank == (needReverse ? 5 : 4) ? leftRegion : rightRegion;
                    break;
                }
                case STACK_SOURCE:
                {
                    var needReverse = bothStackSourceAtRight && !_p2.协作程序是远线;
                    safePosRegion = myRank == (needReverse ? 7 : 6) ? leftRegion : rightRegion;
                    break;
                }
            }
            sa.DebugMsg($"玩家类型为 {myStateStr}，安全区在 {(safePosRegion == leftRegion ? "左" : "右")}", Debugging);
            var pos = new Vector3(100, 0, 105).RotateAndExtend(Center, safePosRegion * 45f.DegToRad());
            sa.DrawGuidance(pos, 0, 10000, $"P2_协作程序_分摊击退位置");
        
            _p2.女人击退记录.WaitOne();
        
            sa.Method.RemoveDraw($"P2_协作程序_分摊击退位置");
            var pos2 = new Vector3(100, 0, _p2.协作程序是远线 ? 119.5f : 115f).RotateAndExtend(Center, safePosRegion * 45f.DegToRad());
            sa.DrawGuidance(pos2, 0, 10000, $"P2_协作程序_分摊击退后续");
            
            _p2.男人分摊钢铁记录.WaitOne();
            sa.Method.RemoveDraw($"P2_协作程序.*");
        }
        finally
        {
            _p2.分摊记录.Reset();
            _p2.男人钢铁方位记录.Reset();
            _p2.女人击退记录.Reset();
            _p2.男人分摊钢铁记录.Reset();
        }
    }

    [ScriptMethod(name: "P2A_协作程序_女人场中击退记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31534"],
        userControl: Debugging, suppress: 10000)]
    public void P2_协作程序_女人击退记录(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.15) return;
        _p2.女人击退记录.Set();
    }
    
    [ScriptMethod(name: "P2A_协作程序_男人分摊钢铁记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31526"],
        userControl: Debugging, suppress: 10000)]
    public void P2_协作程序_男人分摊钢铁记录(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.15) return;
        _p2.男人分摊钢铁记录.Set();
    }

    #endregion P2A 协作程序

    #region P2B 刀光剑舞

    [ScriptMethod(name: "———————— 《P2B 刀光剑舞》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P2B_刀光剑舞_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P2B_刀光剑舞_分P", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31544"], userControl: Debugging)]
    public void P2_刀光剑舞_分P(Event @event, ScriptAccessory accessory)
    {
        _parse = 2.2;
    }
    
    [ScriptMethod(name: "P2B_刀光剑舞_射手天箭", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31539"], userControl: true)]
    public void P2_刀光剑舞_射手天剑(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.2) return;
        sa.DrawRect(ev.SourceId, 1000, 7000, $"P2_刀光剑舞_射手天剑", 0, 10, 42);
    }
    
    [ScriptMethod(name: "P2B_刀光剑舞_射手天箭删除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31539"], userControl: Debugging)]
    public void P2_刀光剑舞_射手天箭删除(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.2) return;
        sa.Method.RemoveDraw($"P2_刀光剑舞_射手天箭");
    }
    
    [ScriptMethod(name: "P2B_刀光剑舞_接线顺劈", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3154[01])$"], userControl: true)]
    public void P2_刀光剑舞_接线顺劈(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.2) return;
        var dp = sa.DrawFan(ev.SourceId, 0, 5500, 2000, $"P2_刀光剑舞_接线顺劈",
            float.Pi / 2, 0, 40, 0, draw: false);
        dp.TargetResolvePattern = PositionResolvePatternEnum.TetherTarget;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }
    
    [ScriptMethod(name: "P2B_刀光剑舞_接线顺劈删除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(3154[01])$"], userControl: Debugging, suppress: 10000)]
    public void P2_刀光剑舞_接线顺劈删除(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.2) return;
        sa.Method.RemoveDraw($"P2_刀光剑舞_接线顺劈");
    }
    
    [ScriptMethod(name: "P2B_刀光剑舞_盾连击指路方向提示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31527"], userControl: true)]
    public void P2_刀光剑舞_盾连击指路方向提示(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.2) return;
        if (sa.Data.MyObject.IsTank())
        {
            var dp = sa.DrawLine(sa.Data.Me, ev.SourceId, 0, 10000, $"P2_刀光剑舞_盾连击男人连线", 0, 3f, 10f, isSafe: true, byY: true, draw: false);
            dp.Color = new Vector4(0f, 1f, 1f, 1f);
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp);

            var spos = ev.SourcePosition;
            var myPos = new Vector3(100, 0, 108).RotateAndExtend(Center,
                spos.GetRadian(Center) + (sa.GetMyIndex() == 0 ? 20f : -20f).DegToRad());
            sa.DrawGuidance(myPos, 0, 10000, $"P2_刀光剑舞_盾连击指路方向提示");
        }
        else
            sa.DrawGuidance(Center, 0, 10000, $"P2_刀光剑舞_盾连击指路方向提示");
    }
    
    [ScriptMethod(name: "P2B_刀光剑舞_盾连击指路方向删除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31527"], userControl: Debugging)]
    public void P2_刀光剑舞_盾连击指路方向删除(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.2) return;
        sa.Method.RemoveDraw($"P2_刀光剑舞_盾连击.*");
    }
    
    [ScriptMethod(name: "P2B_刀光剑舞_盾连击后分摊", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31528", "TargetIndex:1"], userControl: true)]
    public void P2_刀光剑舞_盾连击后分摊(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.2) return;

        var targetIsMe = ev.TargetId == sa.Data.Me;
        var dp = sa.DrawCircle(ev.SourceId, 0, 10000, $"P2_刀光剑舞_分摊", 6f, isSafe: !targetIsMe, draw: false);
        dp.SetOwnersDistanceOrder(true, 1);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        
        sa.Method.TextInfo(targetIsMe ? "出去出去" : "集合分摊", 3000);
        sa.Method.TTS(targetIsMe ? "出去出去" : "集合分摊");
    }
    
    [ScriptMethod(name: "P2B_刀光剑舞_盾连击后分摊删除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31529", "TargetIndex:1"], userControl: Debugging)]
    public void P2_刀光剑舞_盾连击后分摊删除(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.2) return;
        sa.Method.RemoveDraw($"P2_刀光剑舞_分摊");
    }
    
    #endregion P2B 刀光剑舞

    #region P2C 转场

    [ScriptMethod(name: "P2C_转场_分P", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31507"], userControl: Debugging)]
    public void P2C_转场_分P(Event ev, ScriptAccessory sa)
    {
        _parse = 2.5;
        _pd.Init(sa, "P2.5转场");
    }
    
    [ScriptMethod(name: "P2C_转场_记录头标", eventType: EventTypeEnum.Marker, eventCondition: ["Operate:Add", "Id:regex:^(0[1234679]|10)$"],
        userControl: Debugging)]
    public void P2C_转场_记录头标(Event ev, ScriptAccessory sa)
    {
        // 取攻击1234、锁链12、禁止12
        if (_parse != 2.5) return;
        
        lock (_pd)
        {
            var mark = ev.Id0();
            var tidx = sa.GetPlayerIdIndex((uint)ev.TargetId);
            var targetJob = sa.GetPlayerJobByIndex(tidx);
            sa.DebugMsg($"检测到{targetJob} 被标 ev.Id {mark}", Debugging);
            _pd.AddActionCount();
            var pdVal = mark switch
            {
                0x01 => 10,    // 攻击1
                0x02 => 20,    // 攻击2
                0x03 => 30,    // 攻击3
                0x04 => 40,    // 攻击4
                0x06 => 100,   // 锁链1
                0x07 => 110,   // 锁链2
                0x09 => 200,   // 禁止1
                0x10 => 210,   // 禁止2
                _ => 0
            };
            _pd.AddPriority(tidx, pdVal);
            if (_pd.ActionCount != 8) return;
            sa.DebugMsg($"P2C_转场_记录头标：头标记录完毕", Debugging);
            sa.DebugMsg($"{_pd.ShowPriorities()}", Debugging);
            _p2.转场头标记录.Set();   // 头标记录
        }
    }
    
    [ScriptMethod(name: "P2C_转场_初始位置指路", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(3426)$"],
        userControl: true, suppress: 10000)]
    public void P2C_转场_初始位置(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.5) return;
        
        // Atk1-4, Bind1-2, Stop1-2
        List<float> deg = [-90f, -30f, 30f, 90f, -150f, 150f, -150f, 150f];
        int myIndex = sa.GetMyIndex();
        
        var hasMarker = _p2.转场头标记录.WaitOne(3000);
        
        if (!hasMarker)
        {
            sa.DebugMsg($"未在一定时间内检测到头标，启用THD优先级排列", Debugging);
            _pd.AddPriorities([1, 2, 3, 4, 5, 6, 7, 8]);
            for (int i = 0; i < sa.Data.PartyList.Count; i++)
            {
                var obj = sa.GetById(sa.Data.PartyList[i]);
                if (obj is null) return;
                (bool hasStack, bool hasSpread) = (((IBattleChara)obj).HasStatus(3426), ((IBattleChara)obj).HasStatus(3425));
                var priVal = (hasStack, hasSpread) switch
                {
                    (true, false) => 100,
                    (false, true) => 10,
                    (false, false) => 200,
                    _ => 0
                };
                if (priVal == 0) return;
                _pd.AddPriority(i, priVal);
            }
        }
        
        float myPosDeg = deg[_pd.FindPriorityIndexOfKey(myIndex)];
        var pos = new Vector3(100, 0, 119.5f).RotateAndExtend(Center, myPosDeg.DegToRad());
        sa.DrawGuidance(pos, 0, 5000, $"P2C_转场_初始位置指路");
        _p2.转场头标记录.Reset();
    }
    
    [ScriptMethod(name: "P2C_转场_地震", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(3156[789]|31570)$", "TargetIndex:1"],
        userControl: true)]
    public void P2C_转场_地震(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.5) return;
        const uint CHARIOT = 31567, DONUT_1 = 31568, DONUT_2 = 31569, DONUT_3 = 31570;

        (int outScale, int innerScale, int donutCount) = ev.ActionId switch
        {
            CHARIOT => (12, 6, 1),
            DONUT_1 => (18, 12, 2),
            DONUT_2 => (24, 18, 3),
            _ => (0, 0, 4)
        };

        string prefix = "P2C_转场_地震月环";
        sa.Method.RemoveDraw($"{prefix}{donutCount-1}");
        if (donutCount == 4) return;
        
        var dp = sa.DrawDonut(Center, 0, 10000, $"{prefix}{donutCount}", outScale, innerScale, draw: false);
        dp.Color = sa.Data.DefaultDangerColor.WithW(2f);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
        
        if (donutCount != 3) return;
        sa.Method.RemoveDraw($"P2C_转场_初始位置指路");
    }

    [ScriptMethod(name: "P2C_转场_手臂位置记录", eventType: EventTypeEnum.PlayActionTimeline,
        eventCondition: ["Id:regex:^(774[78])$", "SourceDataId:regex:^(1571[89])$"], userControl: Debugging, suppress: 10000)]
    public void P2C_转场_手臂位置记录(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.5) return;
        var region = ev.SourcePosition.GetRadian(Center).RadianToRegion(6, isDiagDiv: true);
        _p2.转场手臂先正三角 = region % 2 == 1;
    }
    
    [ScriptMethod(name: "P2C_转场_分摊分散指路", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(31566)$"], userControl: true, suppress: 10000)]
    public void P2C_转场_分摊分散指路(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.5) return;
        
        // Atk1-4, Bind1-2, Stop1-2
        List<float> deg = [-90f, -30f, 30f, 90f, -150f, 150f, -150f, 150f];
        int myPriIndex = _pd.FindPriorityIndexOfKey(sa.GetMyIndex());
        
        float myPosDeg = deg[myPriIndex];
        Vector3 regionPos = new Vector3(100, 0, 119.5f).RotateAndExtend(Center, myPosDeg.DegToRad());

        List<bool> ccwPrep     = [false, true, false, true, true, false, true, false];
        List<bool> ccwMovement = [true, false, true, false, false, true, false, true];

        const float PREP_ROTATE_DEG_BASE = 7.5f;
        const float MOVEMENT_ROTATE_DEG_BASE = 25f;
        const float MOVEMENT_EXTEND_DISTANCE_BASE = -3f;

        float prepRotateDeg = PREP_ROTATE_DEG_BASE.DegToRad() * (ccwPrep[myPriIndex] ? 1 : -1) * (_p2.转场手臂先正三角 ? 1 : -1);
        float movementRotateDeg = MOVEMENT_ROTATE_DEG_BASE.DegToRad() * (ccwMovement[myPriIndex] ? 1 : -1) * (_p2.转场手臂先正三角 ? 1 : -1);

        Vector3 prepPos = regionPos.RotateAndExtend(Center, prepRotateDeg);
        Vector3 movementPos = prepPos.RotateAndExtend(Center, movementRotateDeg, MOVEMENT_EXTEND_DISTANCE_BASE);

        sa.DrawGuidance(prepPos, 0, 2000, $"P2C_转场_分摊分散指路1");
        sa.DrawGuidance(prepPos, movementPos, 0, 2000, $"P2C_转场_分摊分散指路2预备", isSafe: false);
        sa.DrawGuidance(movementPos, 2000, 2000, $"P2C_转场_分摊分散指路2");
    }
    
    [ScriptMethod(name: "*P2C_转场_屏蔽场地月环与手臂钢铁释放后特效", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(3156[689]|31570)$", "TargetIndex:1"], userControl: true)]
    public void P2C_转场_屏蔽特效(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.5) return;
        if (!SpecialMode) return;
        sa.WriteVisible(sa.GetById((uint)ev.SourceId), false);
    }

    #endregion P2C 转场

    #region P3A 你好世界

    [ScriptMethod(name: "———————— 《P3A 你好世界》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3A_你好世界_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P3A_你好世界_初始八方", eventType: EventTypeEnum.Targetable, eventCondition: ["DataId:15717"], userControl: true)]
    public void P3A_你好世界_初始八方(Event ev, ScriptAccessory sa)
    {
        if (_parse != 2.5) return;
        List<int> region = [4, 2, 6, 0, 7, 1, 5, 3];
        sa.DrawGuidance(new Vector3(100, 0, 105f).RotateAndExtend(Center, region[sa.GetMyIndex()] * 45f.DegToRad()), 0,
            5000, $"P3A_你好世界_初始八方");
    }
    
    [ScriptMethod(name: "P3A_你好世界_分P", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31573"], userControl: Debugging)]
    public void P3A_你好世界_分P(Event ev, ScriptAccessory sa)
    {
        _parse = 3;
        _p2.Reset(sa);
        _p2.Dispose();
        _p3.Register();
        _pd.Init(sa, "P3HW");
        _p3.BossId = ev.SourceId;
        
        ResetSupportUnitVisibility(sa);
    }
    
    [ScriptMethod(name: "P3A_你好世界_初始八方删除", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31573"], userControl: Debugging)]
    public void P3A_你好世界_初始八方删除(Event ev, ScriptAccessory sa)
    {
        if (_parse != 3) return;
        sa.Method.RemoveDraw($"P3A_你好世界_初始八方");
    }
    
    [ScriptMethod(name: "P3A_你好世界_Buff记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(343[6789]|3527)$"],
        userControl: Debugging)]
    public void P3A_你好世界_Buff记录(Event ev, ScriptAccessory sa)
    {
        if (_parse != 3) return;
        if (ev.SourceId == 0x00000000) return;
        const uint STACK_PREP = 3436;
        const uint DEFAMATION_PREP = 3437;
        const uint RED_ROT_PREP = 3438;
        const uint BLUE_ROT_PREP = 3439;
        const uint NEED_DEFAMATION = 3527;
        
        lock (_pd)
        {
            if (_pd.ActionCount >= 10) return;
            var score = ev.StatusId switch
            {
                BLUE_ROT_PREP   => 2,
                RED_ROT_PREP    => 4,
                STACK_PREP      => 1,
                DEFAMATION_PREP => 16,
                NEED_DEFAMATION => 8,
                _ => 0
            };
            _pd.AddPriority(sa.GetPlayerIdIndex((uint)ev.TargetId), score);
            _pd.AddActionCount();
            if (_pd.SelectSpecificPriorityIndex(0, true).Value != 20) return;
            _p3.大圈是红塔 = true;
            
        }
    }
    
    [ScriptMethod(name: "P3A_你好世界_轮数增加", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(31599)$"],
        userControl: Debugging)]
    public void P3A_你好世界_轮数增加(Event ev, ScriptAccessory sa)
    {
        if (_parse != 3) return;
        _p3.你好世界轮数++;
        _p3.红塔位置 = 0;
        _p3.蓝塔位置 = 0;
        sa.DebugMsg($"现在是 你好世界 第{_p3.你好世界轮数}轮", Debugging);
        sa.DebugMsg(_pd.ShowPriorities(), Debugging);
        _p3.你好世界轮数记录.Set();
    }
    
    [ScriptMethod(name: "P3A_你好世界_红蓝塔方位记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3158[34])$"],
        userControl: Debugging)]
    public void P3A_你好世界_红蓝塔方位记录(Event ev, ScriptAccessory sa)
    {
        if (_parse != 3) return;
        
        _p3.你好世界轮数记录.WaitOne();
        
        const uint RED_TOWER = 31583;
        const uint BLUE_TOWER = 31584;
        
        lock (_pd)
        {
            var region = ev.SourcePosition.GetRadian(Center).RadianToRegion(8, isDiagDiv: true);
            if (ev.ActionId == RED_TOWER)
                AddTowerParam(ref _p3.红塔位置, region);
            else
                AddTowerParam(ref _p3.蓝塔位置, region);
            if (_p3.红塔位置 + _p3.蓝塔位置 < 400) return;
            _p3.红蓝塔方位记录.Set();
        }
        void AddTowerParam(ref int towerParam, int region)
        {
            towerParam += region * (towerParam > 0 ? 1 : 10);
            towerParam += 100;
        }
    }
    
    [ScriptMethod(name: "P3A_你好世界_初始目的地标注", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(31599)$"],
        userControl: true)]
    public void P3A_你好世界_初始目的地标注(Event ev, ScriptAccessory sa)
    {
        if (_parse != 3) return;
        
        //  分摊后拉远 -> 分摊 -> 大圈后靠近 -> 大圈 ->
        const int STATE_REMOTE_BREAK = 0;
        const int STATE_STACK = 1;
        const int STATE_LOCAL_BREAK = 2;
        const int STATE_DEFAMATION = 3;

        _p3.你好世界轮数记录.WaitOne();
        _p3.红蓝塔方位记录.WaitOne();
        
        const float REMOTE_OUT = 11.5f;
        const float STACK_OUT = 12f;
        const float DEFAMATION_OUT = 15f;
        const float LOCAL_OUT = 14f;
        
        const float REMOTE_CLOSE_DEG = 30f;
        const float STACK_CLOSE_DEG = 20f;
        const float DEFAMATION_CLOSE_DEG = 0f;
        const float LOCAL_CLOSE_DEG = -30f;
        
        try
        {
            // 计算玩家当前状态（0:远线, 1:分摊, 2:近线, 3:大圈）
            var myState = (_pd.FindPriorityIndexOfKey(sa.GetMyIndex()) / 2 + _p3.你好世界轮数 - 1 + 4) % 4;
            sa.DebugMsg($"玩家当前状态：{myState}，0分摊远线，1分摊塔，2大圈近线，3大圈塔", Debugging);
            sa.DebugMsg($"红塔位置：{_p3.红塔位置}，蓝塔位置：{_p3.蓝塔位置}，大圈是红塔 {_p3.大圈是红塔}", Debugging);
            
            int defamationTower = _p3.大圈是红塔 ? _p3.红塔位置 : _p3.蓝塔位置;
            int stackTower = _p3.大圈是红塔 ? _p3.蓝塔位置 : _p3.红塔位置;
            var (towerPos, baseAngle, extend, prefix) = GetStateConfig(myState, defamationTower, stackTower);
            DrawDestinationMarks(sa, towerPos, baseAngle, extend, prefix, _p3.你好世界轮数);
            sa.Method.TextInfo(prefix, 5000);
        }
        finally
        {
            _p3.红蓝塔方位记录.Reset();
        }

        return;

        (int towerPos, float baseAngle, float extend, string prefix) GetStateConfig(
            int state, int defamationTower, int stackTower)
        {
            return state switch
            {
                STATE_DEFAMATION   => (defamationTower, DEFAMATION_CLOSE_DEG, DEFAMATION_OUT, $"大圈【{(_p3.大圈是红塔 ? "红" : "蓝")}】塔内"),
                STATE_REMOTE_BREAK => (stackTower,      REMOTE_CLOSE_DEG,     REMOTE_OUT,     $"远线【{(_p3.大圈是红塔 ? "蓝" : "红")}】塔间分摊"),
                STATE_STACK        => (stackTower,      STACK_CLOSE_DEG,      STACK_OUT,      $"分摊【{(_p3.大圈是红塔 ? "蓝" : "红")}】塔内"),
                STATE_LOCAL_BREAK  => _p3.你好世界轮数 == 4 
                                    ? (stackTower,      REMOTE_CLOSE_DEG,     REMOTE_OUT,     $"近线第四轮【{(_p3.大圈是红塔 ? "蓝" : "红")}】塔间分摊") // 第4轮的近线使用远线
                                    : (defamationTower, LOCAL_CLOSE_DEG,      LOCAL_OUT,      $"近线【{(_p3.大圈是红塔 ? "红" : "蓝")}】塔外分散"), // 其他轮次使用大圈塔
                _ => throw new ArgumentException($"未知状态: {state}")
            };
        }
        
        void DrawDestinationMarks(ScriptAccessory sa, int towerPos, float baseAngle, 
            float extend, string prefix, int round)
        {
            // 判断第一个塔是否记录在个位（true:个位, false:十位）
            // bool isFirstAtDigitOne = towerPos.GetDecimalDigit(1) < towerPos.GetDecimalDigit(2);
            // 两者之差为±2或±6，当相减结果为-2或6时，个位数代表的塔方位为第一枚，逆时针转动2后为第二枚
            bool isFirstAtDigitOne = Math.Abs(2 - (towerPos.GetDecimalDigit(1) - towerPos.GetDecimalDigit(2))) == 4;
            float direction = isFirstAtDigitOne ? 1f : -1f;
    
            for (int i = 1; i <= 2; i++)
            {
                float offsetAngle = baseAngle * direction * (i == 1 ? 1f : -1f);
                int region = towerPos.GetDecimalDigit(i);

                Vector3 basePos = new Vector3(100, 0, 100 + extend);
                float rotateRad = (region * 45f + offsetAngle).DegToRad();
                Vector3 pos = basePos.RotateAndExtend(Center, rotateRad);
                
                sa.DebugMsg($"绘制 第{round}轮 {prefix}", Debugging);
                var dp = sa.DrawCircle(pos, 0, 10000, $"P3A_你好世界_初始目的地标注_R{round}_{prefix}{i}", 0.5f, isSafe: true, draw: false);
                dp.Color = sa.Data.DefaultSafeColor.WithW(2f);
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
            }
        }
    }

    [ScriptMethod(name: "P3A_你好世界_大圈与近线传毒标注", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(3158[34])$", "TargetIndex:1"],
        userControl: true, suppress: 500)]
    public void P3A_你好世界_大圈与近线传毒标注(Event ev, ScriptAccessory sa)
    {
        if (_parse != 3) return;
        
        //  分摊后拉远 -> 分摊 -> 大圈后靠近 -> 大圈 ->
        const int STATE_REMOTE_BREAK = 0;
        const int STATE_STACK = 1;
        const int STATE_LOCAL_BREAK = 2;
        const int STATE_DEFAMATION = 3;
        
        // 计算玩家当前状态（0:远线, 1:分摊, 2:近线, 3:大圈）
        var myState = (_pd.FindPriorityIndexOfKey(sa.GetMyIndex()) / 2 + _p3.你好世界轮数 - 1 + 4) % 4;
        var myPos = sa.Data.MyObject.Position;
        
        // 大圈标注八方点
        if (myState == STATE_DEFAMATION)
        {
            var targetPos = GetDefamationPosition();
            sa.DrawGuidance(targetPos, 0, 5000, $"P3A_你好世界_大圈标注_R{_p3.你好世界轮数}_{myState}");
            return;
        }
        
        // 近线标注大圈传毒
        if (myState == STATE_LOCAL_BREAK)
        {
            if (_p3.你好世界轮数 == 4) return;
            var target = GetDefamationPlayer(sa);
            sa.DrawGuidance(target, 0, 5000, $"P3A_你好世界_近线标注_R{_p3.你好世界轮数}_{myState}");
        }
        
        // 本地方法：获取大圈应去的塔位置
        Vector3 GetDefamationPosition()
        {
            var towerRegion = _p3.大圈是红塔 ? _p3.红塔位置 : _p3.蓝塔位置;
            var towerRegion1 = towerRegion.GetDecimalDigit(1);
            var towerRegion2 = towerRegion.GetDecimalDigit(2);
        
            var pos1 = new Vector3(100, 0, 114).RotateAndExtend(Center, towerRegion1 * 45f.DegToRad());
            var pos2 = new Vector3(100, 0, 114).RotateAndExtend(Center, towerRegion2 * 45f.DegToRad());

            return Vector3.Distance(myPos, pos1) < Vector3.Distance(myPos, pos2) ? pos1 : pos2;
        }

        uint GetDefamationPlayer(ScriptAccessory sa)
        {
            var defamationPlayers = 0;
            for (int i = 0; i < 8; i++)
            {
                var playerState = (_pd.FindPriorityIndexOfKey(i) / 2 + _p3.你好世界轮数 - 1 + 4) % 4;
                if (playerState != STATE_DEFAMATION) continue;
                defamationPlayers += 100 + (defamationPlayers > 0 ? i * 10 : i);
                if (defamationPlayers > 200) break;
            }

            var player1 = sa.Data.PartyList[defamationPlayers.GetDecimalDigit(1)];
            var player2 = sa.Data.PartyList[defamationPlayers.GetDecimalDigit(2)];
            
            var pos1 = sa.GetById(player1).Position;
            var pos2 = sa.GetById(player2).Position;
            
            return Vector3.Distance(myPos, pos1) < Vector3.Distance(myPos, pos2) ? player1 : player2;
        }
    }
    
    [ScriptMethod(name: "P3A_你好世界_轮数记录状态重置", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(3158[34])$", "TargetIndex:1"],
        userControl: Debugging, suppress: 500)]
    public void P3A_你好世界_轮数记录状态重置(Event ev, ScriptAccessory sa)
    {
        if (_parse != 3) return;
        sa.Method.RemoveDraw($"P3A_你好世界_初始目的地标注_R{_p3.你好世界轮数}.*");
        _p3.你好世界轮数记录.Reset();
    }
    
    [ScriptMethod(name: "P3A_你好世界_大圈与近线传毒标注删除", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(3526|3429)$"], userControl: Debugging)]
    public void P3A_你好世界_大圈与近线传毒标注删除(Event ev, ScriptAccessory sa)
    {
        if (_parse != 3) return;
        
        // 当且仅当自身周围发身传毒事件才删除
        var myPos = sa.Data.MyObject.Position;
        var targetPos = sa.GetById(ev.TargetId).Position;
        if (Vector3.Distance(myPos, targetPos) > 5f) return;
        
        sa.Method.RemoveDraw($"P3A_你好世界_近线标注_R{_p3.你好世界轮数}.*");
        sa.Method.RemoveDraw($"P3A_你好世界_大圈标注_R{_p3.你好世界轮数}.*");
    }
    
    [ScriptMethod(name: "P3A_你好世界_小钢铁", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(3435|3528)$"], userControl: true)]
    public void P3A_你好世界_小钢铁(Event ev, ScriptAccessory sa)
    {
        if (_parse != 3) return;
        sa.DrawCircle(ev.TargetId, 2500, 10000, $"P3A_你好世界_小钢铁_{ev.TargetId}", 5f);
    }
    
    [ScriptMethod(name: "P3A_你好世界_小钢铁删除", eventType: EventTypeEnum.StatusRemove, eventCondition: ["StatusID:regex:^(3435|3528)$"], userControl: Debugging)]
    public void P3A_你好世界_小钢铁删除(Event ev, ScriptAccessory sa)
    {
        if (_parse != 3) return;
        sa.Method.RemoveDraw($"P3A_你好世界_小钢铁_{ev.TargetId}");
    }
    
    #endregion P3A 你好世界

    #region P3B 小电视

    [ScriptMethod(name: "P3B_小电视_分P", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31588"], userControl: Debugging)]
    public void P3B_转场_分P(Event ev, ScriptAccessory sa)
    {
        // 使用严重错误读条提前进入小电视
        _parse = 3.1;
        _pd.Init(sa, "P3B小电视");
        _pd.AddPriorities([2, 3, 1, 7, 4, 5, 6, 7]);
    }
    
    [ScriptMethod(name: "P3B_小电视_记录头标", eventType: EventTypeEnum.Marker, eventCondition: ["Operate:Add", "Id:regex:^(0[12345678])$"],
        userControl: Debugging)]
    public void P3B_小电视_记录头标(Event ev, ScriptAccessory sa)
    {
        // 取攻击12345、锁链123
        if (_parse != 3.1) return;
        
        lock (_pd)
        {
            var mark = ev.Id0();
            var tidx = sa.GetPlayerIdIndex((uint)ev.TargetId);
            var targetJob = sa.GetPlayerJobByIndex(tidx);
            sa.DebugMsg($"检测到{targetJob} 被标 ev.Id {mark}", Debugging);
            _pd.AddActionCount();
            var pdVal = mark switch
            {
                0x01 => 10,    // 攻击1
                0x02 => 20,    // 攻击2
                0x03 => 30,    // 攻击3
                0x04 => 40,    // 攻击4
                0x05 => 50,    // 攻击5
                0x06 => 110,   // 锁链1
                0x07 => 120,   // 锁链2
                0x08 => 130,   // 锁链3
                _ => 0
            };
            _pd.AddPriority(tidx, pdVal);
            if (_pd.ActionCount != 8) return;
            sa.DebugMsg($"P3B_小电视_记录头标：头标记录完毕", Debugging);
            sa.DebugMsg($"{_pd.ShowPriorities()}", Debugging);
            _p3.小电视头标记录.Set();   // 头标记录
        }
    }

    [ScriptMethod(name: "P3B_小电视_光头扫描方向记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3159[56])$"],
        userControl: Debugging)]
    public void P3B_小电视_光头扫描方向记录(Event ev, ScriptAccessory sa)
    {
        if (_parse != 3.1) return;
        const uint CANNON_RIGHT = 31595;
        _p3.光头小电视方向打右 = ev.ActionId == CANNON_RIGHT;
        _p3.光头扫描方向记录.Set();
    }
    
    [ScriptMethod(name: "P3B_小电视_钢铁范围", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3159[56])$"],
        userControl: true)]
    public void P3B_小电视_钢铁范围(Event ev, ScriptAccessory sa)
    {
        if (_parse != 3.1) return;
        foreach (var member in sa.Data.PartyList)
            sa.DrawCircle(member, 4000, 6000, $"P3B_小电视_钢铁范围", 7f);
    }
    
    [ScriptMethod(name: "P3B_小电视_指路", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(345[23])$"],
        userControl: true, suppress: 1000)]
    public void P3B_小电视_指路(Event ev, ScriptAccessory sa)
    {
        if (_parse != 3.1) return;
        var hasMarker = _p3.小电视头标记录.WaitOne(2500);
        if (!hasMarker)
        {
            sa.DebugMsg($"未在一定时间内检测到头标，启用HTDH优先级排列", Debugging);
            for (int i = 0; i < sa.Data.PartyList.Count; i++)
            {
                var obj = sa.GetById(sa.Data.PartyList[i]);
                if (obj is null) return;
                
                if (!((IBattleChara)obj).HasStatusAny([3452, 3453])) return;
                _pd.AddPriority(i, 100);
            }
        }
        _p3.光头扫描方向记录.WaitOne();

        // 打右左安全，攻1-5，锁1-3
        List<Vector3> staticPos =
        [
            new(99.0f, 0, 91.0f), new(104.0f, 0, 100.0f), new(115.5f, 0, 100.0f), new(99.0f, 0, 109.0f), new(99.0f, 0, 119.0f),
            new(93.0f, 0, 82.0f), new(86.0f, 0, 92.5f), new(86.0f, 0, 107.5f)
        ];

        var myRank = _pd.FindPriorityIndexOfKey(sa.GetMyIndex());
        var myPos = staticPos[myRank];
        if (!_p3.光头小电视方向打右 && myRank is 0 or 3 or 4 or 5)
            myPos = myPos.FoldPointHorizon(Center.X);
        sa.DrawGuidance(myPos, 0, 10000, $"P3B_小电视_指路");
    }

    [ScriptMethod(name: "P3B_小电视_面向计算", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(345[23])$"], userControl: Debugging)]
    public void P3B_小电视_面向计算(Event ev, ScriptAccessory sa)
    {
        if (_parse != 3.1) return;
        if (ev.TargetId != sa.Data.Me) return;
        const uint PLAYER_CANNON_RIGHT = 3452;
        
        _p3.光头扫描方向记录.WaitOne();
        _p3.小电视头标记录.WaitOne(3000);

        int myBindRank = _pd.FindPriorityIndexOfKey(sa.GetMyIndex()) - 5;
        if (myBindRank < 0) return;

        // 基于光头扫右，玩家扫右
        var faceRegion = myBindRank switch
        {
            0 => 0,     // 锁1朝下
            1 => 3,     // 锁2朝左
            2 => 1,     // 锁3朝右
            _ => -1
        };
        if (faceRegion < 0) return;
        faceRegion += (ev.StatusId != PLAYER_CANNON_RIGHT ? 2 : 0) + (myBindRank == 0 && !_p3.光头小电视方向打右 ? 2 : 0);

        _p3.小电视玩家面向 = (faceRegion + 4) % 4;
        _p3.小电视玩家面向记录.Set();
    }
    
    [ScriptMethod(name: "P3B_小电视_面向箭头辅助绘图", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(345[23])$"], userControl: true)]
    public void P3B_小电视_面向箭头辅助(Event ev, ScriptAccessory sa)
    {
        if (_parse != 3.1) return;
        if (ev.TargetId != sa.Data.Me) return;
        
        _p3.小电视玩家面向记录.WaitOne();
        DrawFacingArrow(sa, _p3.小电视玩家面向, false, "P3B_小电视_面向箭头辅助_自身");
        DrawFacingArrow(sa, _p3.小电视玩家面向, true,  "P3B_小电视_面向箭头辅助_正确面向");
        return;

        void DrawFacingArrow(ScriptAccessory sa, int dir, bool isSupport, string name)
        {
            var dp = sa.DrawLine(sa.Data.Me, 0, 0, 10000, name,
                isSupport ? dir * 90f.DegToRad() : 0, 1f, 4.5f,
                isSafe: isSupport, draw: false);
            dp.FixRotation = isSupport;
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dp);
        }
    }
    
    [ScriptMethod(name: "*P3B_小电视_自动面向辅助", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3159[56])$"], userControl: true)]
    public void P3B_小电视_自动面向辅助(Event ev, ScriptAccessory sa)
    {
        if (_parse != 3.1) return;
        
        // 1. 判断进入条件
        _p3.小电视玩家面向记录.WaitOne();
        int myBindRank = _pd.FindPriorityIndexOfKey(sa.GetMyIndex()) - 5;
        if (myBindRank < 0) return;
        
        var myObject = sa.Data.MyObject;
        if (myObject == null) return;
        
        // 2. 设置正确面向
        int correctFaceDir = _p3.小电视玩家面向;
        sa.DebugMsg($"小电视玩家面向应为 {correctFaceDir} ", Debugging);
        float correctFaceRotation = correctFaceDir * 90f.DegToRad();
        
        // 3. 开启触发
        _p3.小电视面向辅助Framework = sa.Method.RegistFrameworkUpdateAction(Action);
        return;
        
        void Action()
        {
            var myRotation = myObject.Rotation;
            var rotationDiff = MathF.Abs(myRotation.GetDiffRad(correctFaceRotation));
            if (!sa.IsMoving() && rotationDiff > 0.1f && (DateTime.Now - _p3.小电视面向辅助触发时间).TotalMilliseconds > 250)
            {
                _p3.小电视面向辅助触发时间 = DateTime.Now;
                sa.SetRotation(myObject, correctFaceRotation);
            }
        }
    }
    
    [ScriptMethod(name: "P3B_小电视_处理结束", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(3159[56])$"],
        userControl: Debugging)]
    public void P3B_小电视_处理结束(Event ev, ScriptAccessory sa)
    {
        if (_parse != 3.1) return;
        _p3.小电视头标记录.Reset();
        _p3.光头扫描方向记录.Reset();
        _p3.小电视玩家面向记录.Reset();
        sa.Method.RemoveDraw($"P3B_小电视.*");
        sa.Method.UnregistFrameworkUpdateAction(_p3.小电视面向辅助Framework);
    }

    #endregion P3B 小电视

    #region P4 蓝屏

    [ScriptMethod(name: "———————— 《P4 蓝屏》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P4_蓝屏_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P4_蓝屏_分P", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(31559)$"],
        userControl: Debugging)]
    public void P4_蓝屏_分P(Event ev, ScriptAccessory sa)
    {
        _parse = 4;
        _p3.Reset(sa);
        _p3.Dispose();
        _p4.Register();
        _p4.BossId = ev.SourceId;
    }
    
    [ScriptMethod(name: "P4_蓝屏_每轮波动炮初始化", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(3161[05])$"], userControl: Debugging, suppress: 1000)]
    public void P4_蓝屏_每轮波动炮初始化(Event ev, ScriptAccessory sa)
    {
        if (_parse != 4) return;
        sa.Method.RemoveDraw($"P4_蓝屏_R{_p4.蓝屏波动炮轮数}.*");
        
        _p4.蓝屏波动炮轮数++;
        _pd.Init(sa, $"P4蓝屏R{_p4.蓝屏波动炮轮数}");
        _pd.AddPriorities([1, 8, 3, 6, 4, 5, 2, 7]);
        sa.DebugMsg($"现在是蓝屏波动炮 第{_p4.蓝屏波动炮轮数}轮", Debugging);
        _p4.波动炮初始化记录.Set();
    }
    
    [ScriptMethod(name: "P4_蓝屏_波动炮八方指路", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(3161[05])$"],
        userControl: true, suppress: 1000)]
    public void P4_蓝屏_波动炮八方指路(Event ev, ScriptAccessory sa)
    {
        if (_parse != 4) return;
        _p4.波动炮初始化记录.WaitOne();
        
        lock (_p4)
        {
            if (_p4.蓝屏波动炮轮数 > 3) return;
            List<int> partySpreadRegion = [11, 5, 13, 3, 14, 2, 12, 4];
            var pos = new Vector3(100, 0, 114f).RotateAndExtend(Center, partySpreadRegion[sa.GetMyIndex()] * 22.5f.DegToRad());
            sa.DrawGuidance(pos, 0, 5000, $"P4_蓝屏_波动炮八方指路");
            for (int i = 0; i < 8; i++)
            {
                var dp = sa.DrawLine(Center, 0, 0, 10000, $"P4_蓝屏_波动炮指引线{i}", partySpreadRegion[i] * 22.5f.DegToRad(), 20f, 20f, draw: false);
                dp.Color = i switch
                {
                    0 or 1 => new Vector4(0.1f, 0.1f, 1, 1),
                    2 or 3 => new Vector4(0.1f, 1f, 0.1f, 1),
                    _ => new Vector4(1, 0.1f, 0.1f, 1),
                };
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Line, dp);
            }
        }
    }
    
    [ScriptMethod(name: "*P4_蓝屏_波动炮八方指路移除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31614"], userControl: Debugging)]
    public void P4_蓝屏_波动炮八方指路移除(Event ev, ScriptAccessory sa)
    {
        if (_parse != 4) return;
        sa.Method.RemoveDraw($"P4_蓝屏_波动炮.*");
        _p4.波动炮初始化记录.Reset();
    }
    
    [ScriptMethod(name: "P4_蓝屏_地震", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(3156[789]|31570)$", "TargetIndex:1"],
        userControl: true)]
    public void P4_蓝屏_地震(Event ev, ScriptAccessory sa)
    {
        if (_parse != 4) return;
        const uint CHARIOT = 31567, DONUT_1 = 31568, DONUT_2 = 31569, DONUT_3 = 31570;

        (int outScale, int innerScale, int donutCount) = ev.ActionId switch
        {
            CHARIOT => (12, 6, 1),
            DONUT_1 => (18, 12, 2),
            DONUT_2 => (24, 18, 3),
            _ => (0, 0, 4)
        };

        string prefix = "P4_蓝屏_地震月环";
        sa.Method.RemoveDraw($"{prefix}{donutCount-1}");
        if (donutCount == 4) return;
        
        var dp = sa.DrawDonut(Center, 0, 10000, $"{prefix}{donutCount}", outScale, innerScale, draw: false);
        dp.Color = sa.Data.DefaultDangerColor.WithW(2.5f);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
    }
    
    [ScriptMethod(name: "P4_蓝屏_波动炮分摊目标记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(22393)$"], userControl: Debugging)]
    public void P4_蓝屏_波动炮分摊目标记录(Event ev, ScriptAccessory sa)
    {
        if (_parse != 4) return;
        lock (_pd)
            _pd.AddPriority(sa.GetPlayerIdIndex((uint)ev.TargetId), 10);
    }
    
    [ScriptMethod(name: "P4_蓝屏_分摊绘图", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31614"], userControl: true, suppress: 1000)]
    public void P4_蓝屏_分摊绘图(Event ev, ScriptAccessory sa)
    {
        if (_parse != 4) return;
        
        var myPriRank = _pd.FindPriorityIndexOfKey(sa.GetMyIndex());
        var mySafeBool = myPriRank is 0 or 1 or 2 or 6 ? 0 : 1;
        for (int i = 0; i < 2; i++)
        {
            var targetIdx = _pd.SelectSpecificPriorityIndex(i+6).Key;
            sa.DrawRect(_p4.BossId, sa.Data.PartyList[targetIdx], 0, 10000,
                $"P4_蓝屏_R{_p4.蓝屏波动炮轮数}_分摊绘图", 0, 6, 50, isSafe: i == mySafeBool);
        }
    }
    
    [ScriptMethod(name: "P4_蓝屏_分摊指路", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31614"], userControl: true, suppress: 1000)]
    public void P4_蓝屏_分摊指路(Event ev, ScriptAccessory sa)
    {
        if (_parse != 4) return;
        sa.DrawLine(Center, 0, 0, 6000, $"P4_蓝屏_R{_p4.蓝屏波动炮轮数}_分摊指引线1", 20f.DegToRad(), 20f, 20f, isSafe: true, draw: true);
        sa.DrawLine(Center, 0, 0, 6000, $"P4_蓝屏_R{_p4.蓝屏波动炮轮数}_分摊指引线2", -20f.DegToRad(), 20f, 20f, isSafe: true, draw: true);

        sa.DebugMsg(_pd.ShowPriorities(), Debugging);
        
        var myPriRank = _pd.FindPriorityIndexOfKey(sa.GetMyIndex());
        var myRegion = myPriRank is 0 or 1 or 2 or 6 ? -1 : 1;
        var extend = 0;     // TODO，不同轮数分摊位置是不同的
        var myPos = new Vector3(100, 0, 114).RotateAndExtend(Center, myRegion * 20f.DegToRad(), extend); 
        sa.DrawGuidance(myPos, 0, 10000, $"P4_蓝屏_R{_p4.蓝屏波动炮轮数}_分摊指路");
    }

    [ScriptMethod(name: "P4_蓝屏_第二段波动炮", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31616"], userControl: true)]
    public void P4_蓝屏_第二段波动炮(Event ev, ScriptAccessory sa)
    {
        if (_parse != 4) return;
        sa.DrawRect(ev.SourceId, 0, 10000, $"P4_蓝屏_第二段波动炮", 0, 6, 50);
    }
    
    [ScriptMethod(name: "P4_蓝屏_第二段波动炮移除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31616"], userControl: Debugging, suppress: 1000)]
    public void P4_蓝屏_第二段波动炮移除(Event ev, ScriptAccessory sa)
    {
        if (_parse != 4) return;
        sa.Method.RemoveDraw($"P4_蓝屏_第二段波动炮");
    }
    
    [ScriptMethod(name: "*P4_蓝屏_屏蔽场地月环特效", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(3156[689]|31570)$", "TargetIndex:1"], userControl: true)]
    public void P4_蓝屏_屏蔽特效(Event ev, ScriptAccessory sa)
    {
        if (_parse != 4) return;
        if (!SpecialMode) return;
        sa.WriteVisible(sa.GetById((uint)ev.SourceId), false);
    }
    
    [ScriptMethod(name: "*P4_蓝屏_屏蔽第一段波动炮特效", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31614"], userControl: true)]
    public void P4_蓝屏_屏蔽第一段波动炮特效(Event ev, ScriptAccessory sa)
    {
        if (_parse != 4) return;
        if (!SpecialMode) return;
        sa.WriteVisible(sa.GetById(ev.SourceId), false);
    }
    
    [ScriptMethod(name: "*P4_蓝屏_屏蔽第二段波动炮特效", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31616"], userControl: true)]
    public void P4_蓝屏_屏蔽第二段波动炮特效(Event ev, ScriptAccessory sa)
    {
        if (_parse != 4) return;
        if (!SpecialMode) return;
        sa.WriteVisible(sa.GetById(ev.SourceId), false);
    }
    
    [ScriptMethod(name: "*P4_蓝屏_屏蔽分摊波动炮特效", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31615"], userControl: true)]
    public void P4_蓝屏_屏蔽分摊波动炮特效(Event ev, ScriptAccessory sa)
    {
        if (_parse != 4) return;
        if (!SpecialMode) return;
        sa.WriteVisible(sa.GetById(ev.SourceId), false);
    }
    
    #endregion P4 蓝屏
    
    #region P5A 一运一传
    
    [ScriptMethod(name: "———————— 《P5A1 一运》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P5_一运_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P5_开场_分P", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31621"],
        userControl: Debugging)]
    public void P5_开场_分P(Event ev, ScriptAccessory sa)
    {
        _parse = 5.0;
    }

    [ScriptMethod(name: "P5A1_一运_分P", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31624"],
        userControl: Debugging)]
    public void P5A1_一运_分P(Event ev, ScriptAccessory sa)
    {
        _parse = 5.1;
        
        _p4.Reset(sa);
        _p4.Dispose();
        _p5A.Register();
        
        ResetSupportUnitVisibility(sa);
        _pd.Init(sa, "P5一运");
        _pd.AddPriorities([0, 1, 2, 3, 4, 5, 6, 7]);    // 依职能顺序添加优先值
        sa.DebugMsg($"当前阶段为：{_parse}", Debugging);
    }
    
    [ScriptMethod(name: "P5A1_一运_眼睛激光", eventType: EventTypeEnum.EnvControl, eventCondition: ["DirectorId:800375AC", "Id:00020001"])]
    public void P5A1_一运_眼睛激光(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.1) return;
        var rot = ev.Index() - 1;
        var basePos = new Vector3(100, 0, 80);
        var eyePos = basePos.RotateAndExtend(Center, -45f.DegToRad() * rot);
        sa.DrawRect(eyePos, Center, 7500, 12500, "P5A1_一运_眼睛激光", 0, 16, 40);
    }
    
    [ScriptMethod(name: "P5A1_一运_眼睛激光删除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31521"], userControl: Debugging)]
    public void P5A1_一运_眼睛激光删除(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.1) return;
        sa.Method.RemoveDraw($"P5A1_一运_眼睛激光");
    }
    
    [ScriptMethod(name: "P5A1_一运_远线记录", eventType: EventTypeEnum.Tether, eventCondition: ["Id:00C9"], userControl: Debugging)]
    public void P5A1_一运_远线记录(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.1) return;

        lock (_pd)
        {
            var targetId = ev.TargetId;
            var targetIdx = sa.GetPlayerIdIndex((uint)targetId);
            var sourceId = ev.SourceId;
            var sourceIdx = sa.GetPlayerIdIndex((uint)sourceId);
            var pdValMax = _pd.SelectSpecificPriorityIndex(0, true).Value;
            
            // 添加优先级值前，先获得优先级字典内的最值（即检查是否添加过），将两组远线搭档分别+1000/+2000
            _pd.AddPriority(targetIdx, pdValMax >= 1000 ? 2000 : 1000);
            _pd.AddPriority(sourceIdx, pdValMax >= 1000 ? 2000 : 1000);
            
            _p5A.远线搭档记录.Set();
        }
    }
    
    [ScriptMethod(name: "P5A1_一运_记录头标", eventType: EventTypeEnum.Marker, eventCondition: ["Operate:Add", "Id:regex:^(0[123467])$"],
        userControl: Debugging)]
    public void P5A1_一运_记录头标(Event ev, ScriptAccessory sa)
    {
        // 只取攻击1234与锁链12
        if (_parse != 5.1) return;
        
        lock (_pd)
        {
            var mark = ev.Id0();
            var tidx = sa.GetPlayerIdIndex((uint)ev.TargetId);
            var targetJob = sa.GetPlayerJobByIndex(tidx);
            sa.DebugMsg($"检测到{targetJob} 被标 ev.Id {mark}", Debugging);
            _pd.AddActionCount();
            var pdVal = mark switch
            {
                1 => 10,    // 攻击1
                2 => 20,    // 攻击2
                3 => 30,    // 攻击3
                4 => 40,    // 攻击4
                6 => 100,   // 锁链1
                7 => 200,   // 锁链2
                _ => 0
            };
            _pd.AddPriority(tidx, pdVal);
            if (_pd.ActionCount != 6) return;
            sa.DebugMsg($"P5_一运_记录头标：头标记录完毕", Debugging);
            sa.DebugMsg($"{_pd.ShowPriorities()}", Debugging);
            _p5A.头标记录.Set();   // 头标记录
        }
    }
    
    [ScriptMethod(name: "P5A1_一运_定位光头", eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["SourceDataId:14669", "Id:7747"],
        userControl: Debugging)]
    public void P5A1_一运_定位光头(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.1) return;
        var spos = ev.SourcePosition;
        var dir = spos.GetRadian(Center).RadianToRegion(4, isDiagDiv: true);
        _p5A.光头位置 = dir;
        _p5A.蟑螂位置 = (dir + 2) % 4;
        sa.DebugMsg($"一运光头位置：{_p5A.光头位置}，一运蟑螂位置：{_p5A.蟑螂位置}", Debugging);
        _p5A.光头蟑螂定位.Set();
    } 
    
    [ScriptMethod(name: "P5A1_一运_初始位置指路", eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["SourceDataId:14669", "Id:7747"],
        userControl: true)]
    public void P5A1_一运_初始位置指路(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.1) return;
        
        _p5A.远线搭档记录.WaitOne(5000);
        _p5A.头标记录.WaitOne(2000);
        _p5A.光头蟑螂定位.WaitOne(2000);
        
        var myIndex = sa.GetMyIndex();
        var myPdVal = _pd.Priorities[myIndex];
        
        // 远线无标玩家自动匹配搭档，并修正优先级
        if (IsFarLineWithoutMarker(myPdVal))
        {
            int partnerIndex = FindFarLinePartnerIndex(myPdVal);
            int partnerChain = _pd.Priorities[partnerIndex].GetDecimalDigit(3); // 1 or 2
            _pd.AddPriority(myIndex, partnerChain * 100 + 10);  // 11 or 21
            sa.DebugMsg($"远线无标玩家找到搭档：{sa.GetPlayerJobByIndex(partnerIndex)}", Debugging);
        }
        
        // 2. 统一优先级 markerVal
        for (int i = 0; i < 8; i++)
            _pd.Priorities[i] = (_pd.Priorities[i] % 1000) / 10;

        int markerVal = _pd.Priorities[myIndex];
        sa.DebugMsg($"经矫正，markerVal = {markerVal}", Debugging);
        
        // 3. 计算待命点
        Vector3 wait1 = CalcWaitPoint(markerVal, isLeft: true );
        Vector3 wait2 = CalcWaitPoint(markerVal, isLeft: false);

        // 4. 画指引
        sa.DrawGuidance(wait1, 0, 5000, "P5A1_一运_待命地点1");
        sa.DrawGuidance(wait2, 0, 5000, "P5A1_一运_待命地点2");

        // 5. 重置信号量
        _p5A.远线搭档记录.Reset();
        _p5A.头标记录.Reset();
        _p5A.光头蟑螂定位.Reset();
        
        return;

        bool IsFarLineWithoutMarker(int pd) => pd >= 1000 && pd.GetDecimalDigit(3) == 0;
        
        int FindFarLinePartnerIndex(int myPd)
        {
            // 千位 1 -> 找降序第3（index 2）；千位 2 -> 找降序第1（index 0）
            int thousand = myPd / 1000;
            return _pd.SelectSpecificPriorityIndex(thousand == 2 ? 0 : 2, true).Key;
        }
        
        Vector3 CalcWaitPoint(int marker, bool isLeft)
        {
            Vector3 raw = isLeft ? new Vector3(90f, 0, 106f) : new Vector3(110f, 0, 106f);

            bool needBack  = marker is 3 or 4 or 20 or 21;      // 靠外组
            bool useOmega  = marker is 10 or 11 or 20 or 21;    // 远线组
            int  dir       = useOmega ? _p5A.光头位置 : _p5A.蟑螂位置;

            if (needBack) raw += new Vector3(0, 0, 8);
            return raw.RotateAndExtend(Center, 90f.DegToRad() * dir);
        }
    }
    
    [ScriptMethod(name: "P5A1_一运_记录拳头", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(157(09|10))$"], userControl: Debugging)]
    public void P5A1_一运_记录拳头(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.1) return;
        
        const uint BLUE_ROCKET  = 15709;
        // const uint YELLOW_ROCKET = 15710;

        var myPos = sa.Data.MyObject.Position;
        
        // 1. 计算玩家四分之一半场
        if (_p5A.玩家四分之一半场 < 0)
            _p5A.玩家四分之一半场 = myPos.GetRadian(Center).RadianToRegion(4, isDiagDiv: false);
        
        // 2. 判断拳头四分之一半场
        int mobRegion = ev.SourcePosition.GetRadian(Center).RadianToRegion(4, isDiagDiv: false);
        if (mobRegion != _p5A.玩家四分之一半场) return;
        
        // 3. 统计
        lock (_pd)
        {
            _pd.AddActionCount();
            _p5A.拳头数量++;
            _p5A.拳头颜色 += ev.DataId() == BLUE_ROCKET ? 1 : -1;

            sa.DebugMsg($"玩家半场刷出第{_p5A.拳头数量}个拳头，" + $"颜色{(ev.DataId() == BLUE_ROCKET ? "蓝" : "黄")}，" + $"颜色累计值={_p5A.拳头颜色}", Debugging);
        }
        
        // 4. 14 只怪（6 头标 + 8 拳头）全部刷完
        if (_pd.ActionCount != 14) return;
        _p5A.拳头记录.Set();
        sa.Method.RemoveDraw($"P5A1_一运_待命地点.*");
    }
    
    [ScriptMethod(name: "P5A1_一运_拳头待命指路", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(157(09|10))$"],
        userControl: true, suppress: 10000)]
    public void P5A1_一运_拳头待命指路(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.1) return;
        _p5A.拳头记录.WaitOne(2000);
        try
        {
            var myIndex = sa.GetMyIndex();
            var markerVal = _pd.Priorities[myIndex];
        
            var isOutside = markerVal is 3 or 4 or 20 or 21;    // 攻3，攻4，锁2，禁2
            var isRemoteTetherOutside = markerVal is 20 or 21;  // 锁2，禁2
        
            // var myQuadrant = _p5A.玩家四分之一半场;
            var punchCountAtMyQuadrant = _p5A.拳头数量;
            var punchColorAtMyQuadrant = _p5A.拳头颜色;
        
            // 1. 内场玩家，不交换，直接站象限点
            if (!isOutside)
            {
                // isRemoteTetherOutside 决定玩家是靠近远线组（欧米茄）还是靠近近线组（蟑螂）
                Vector3 pos = GetQuadrantPoint(isRemoteTetherOutside, _p5A.光头位置, _p5A.玩家四分之一半场);
                sa.DrawGuidance(pos, 0, 5000, $"P5A1_一运_拳头");
                return;
            }
        
            // 2. 拳头数量不对，不绘图
            if (punchCountAtMyQuadrant != 2)
            {
                // 缺人，不给具体坐标
                sa.Method.TextInfo("观察同组拳头颜色是否交换", 4000, true);
                return;
            }
        
            // 3. 检测拳头是否同色，镜像换点
            bool needSwap = punchColorAtMyQuadrant != 0;
            Vector3 final = GetQuadrantPoint(isRemoteTetherOutside, _p5A.光头位置, _p5A.玩家四分之一半场);
            if (needSwap)
                final = MirrorAcrossBoss(final, _p5A.光头位置);
            sa.DrawGuidance(final, 0, 5000, $"P5A1_一运_拳头");
        }
        finally
        {
            _p5A.拳头记录.Reset();
        }
        return;
        
        Vector3 GetQuadrantPoint(bool isRemoteTetherOutside, int omegaDir, int myQuadrant)
        {
            // 所有坐标以第0象限（右下）为原型，后面统一做象限镜像
            Vector3 raw = isRemoteTetherOutside ? new Vector3(102.7f, 0, 110f) : new Vector3(108.7f, 0, 110f);

            // Boss 在奇数方向时整体顺时针旋转 -90°
            if (omegaDir % 2 == 1)
                raw = raw.RotateAndExtend(new Vector3(109.9f, 0, 110f), -90f.DegToRad());
            
            raw = myQuadrant switch
            {
                1 => raw.FoldPointVertical(Center.Z),
                2 => raw.PointCenterSymmetry(Center),
                3 => raw.FoldPointHorizon(Center.X),
                _ => raw,
            };

            return raw;
        }
        
        Vector3 MirrorAcrossBoss(Vector3 pos, int omegaDir)
        {
            return omegaDir % 2 == 0
                ? pos.FoldPointHorizon(Center.X)
                : pos.FoldPointVertical(Center.Z);
        }
    }
    
    [ScriptMethod(name: "P5A1_一运_激光手旋转引导位置", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(009[CD])$"], userControl: true)]
    public void P5A1_一运_激光手旋转引导位置(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.1) return;
        const int ICON_ROTATE_CW  = 156;   // 009C

        lock (_p5A.激光手方向字典)
        {
            bool    isCw    = ev.Id0() == ICON_ROTATE_CW;
            ulong   tid     = ev.TargetId;
            Vector3 tpos    = sa.GetById(tid)?.Position ?? Center;
            Vector3 baitPos = tpos.RotateAndExtend(Center, (isCw ? 5f : -5f).DegToRad(), -1f);
            int     region  = tpos.GetRadian(Center).RadianToRegion(12, isDiagDiv: true);
        
            sa.DebugMsg($"激光手方向字典中添加：Key: {region} / 12, Value: {baitPos}", Debugging);
            _p5A.激光手方向字典.TryAdd(region, baitPos);
        
            var dp = sa.DrawCircle(baitPos, 0, 10000, $"P5A1_一运_激光手旋转引导位置", 0.5f, isSafe: true, draw: false);
            dp.Color = sa.Data.DefaultSafeColor.WithW(3f);
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
        }
    }
    
    [ScriptMethod(name: "P5A1_一运_玩家引导激光手指路", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31587"], userControl: true)]
    public void P5A1_一运_玩家引导激光手指路(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.1) return;
        if (ev.TargetId != sa.Data.Me) return;
        
        // 这一条判断会因为suppress被return回去，所以suppress的使用场景需注意，事件确认可被触发。否则就用bool。
        if (_p5A.第一根远线拉断) return;
        _p5A.第一根远线拉断 = true;
        _p5A.开启玩家引导激光手指路 = true;
        
        // 1. 基础数据
        int     myIndex = sa.GetMyIndex();
        int     marker  = _pd.Priorities[myIndex];
        Vector3 myPos   = ev.TargetPosition;
        
        bool isShieldTarget = marker is 10 or 11;               // 锁1 禁1，需往场中被盾击
        bool isOutside      = marker is 3 or 4 or 20 or 21;     // 攻3，攻4，锁2，禁2，偏场外
        bool isBind         = marker is 10 or 20 or 11 or 21;   // 锁1，锁2，禁1，禁2
        
        // 2. 计算光头12方向与手臂编号
        int     omega12  = _p5A.光头位置 * 3;          // 0~11
        int     armUnit  = CalcArmUnit(omega12, isOutside, isBind, myPos, Center);
        _p5A.玩家引导激光手方位 = armUnit;
        
        Vector3 guidePos = isShieldTarget
            ? new Vector3(100, 0, 105).RotateAndExtend(Center, armUnit * 30f.DegToRad())
            : _p5A.激光手方向字典[armUnit];

        sa.DrawGuidance(guidePos, 0, 4000, isShieldTarget ? "P5A1_一运_玩家引导激光手指路_场中" : "P5A1_一运_玩家引导激光手指路_拳头", isSafe: false);
        return;
        
        int CalcArmUnit(int omega12, bool outside, bool bind, Vector3 myPos, Vector3 center)
        {
            Vector3 omegaPos = new Vector3(100, 0, 120).RotateAndExtend(center, omega12 * 30f.DegToRad());
            bool isRight = IsAtRight(omegaPos, myPos, center);
            // 3-bit 编码
            int code = (outside ? 4 : 0) | (isRight ? 2 : 0) | (bind ? 1 : 0);

            return code switch
            {
                0b111 => (omega12 + 1)  % 12,
                0b110 => (omega12 + 5)  % 12,
                0b101 => (omega12 + 11) % 12,
                0b100 => (omega12 + 7)  % 12,
                0b010 => (omega12 + 3)  % 12,
                0b000 => (omega12 + 9)  % 12,
                
                0b011 => (omega12 + 3)  % 12,
                0b001 => (omega12 + 9)  % 12,
                _     => -1
            };
        }

        bool IsAtRight(Vector3 posReference, Vector3 posTarget, Vector3 posCenter) =>
            posTarget.GetRadian(posCenter).GetDiffRad(posReference.GetRadian(posCenter)) > 0;
    }
    
    [ScriptMethod(name: "P5A1_一运_玩家引导激光手指路刷新", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31482"],
        userControl: Debugging, suppress: 10000)]
    public void P5A1_一运_玩家引导激光手指路刷新(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.1) return;
        sa.Method.RemoveDraw($"P5A1_一运_玩家引导激光手指路.*");
            
        if (!_p5A.开启玩家引导激光手指路) return;
            
        var myIndex = sa.GetMyIndex();
        var markerVal = _pd.Priorities[myIndex];
        var isShieldTarget = markerVal is 10 or 11; 
        if (isShieldTarget) return;
            
        sa.DebugMsg($"玩家引导激光手方位为：{_p5A.玩家引导激光手方位} / 12", Debugging);
        Vector3 armUnitPos = _p5A.激光手方向字典[_p5A.玩家引导激光手方位];
        
        sa.DrawGuidance(armUnitPos, 0, 4000, $"P5A1_一运_引导拳头");
    }
    
    [ScriptMethod(name: "P5A1_一运_转转手引导指路删除", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31600"],
        userControl: Debugging, suppress: 10000)]
    public void P5A1_一运_转转手引导指路删除(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.1) return;
        sa.Method.RemoveDraw($"P5A1_一运_引导拳头");
    }
        
    [ScriptMethod(name: "P5A1_一运_玩家场中盾引导指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31482"],
        userControl: true, suppress: 10000)]
    public void P5A1_一运_玩家场中盾引导指路(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.1) return;
        var myIndex = sa.GetMyIndex();
        var markerVal = _pd.Priorities[myIndex];
            
        if (markerVal is not 10 and not 11) return;
        var myArmUnit = _p5A.玩家引导激光手方位;
            
        var centerBiasPos = new Vector3(100, 0, 105).RotateAndExtend(Center, myArmUnit * 30f.DegToRad());
        sa.DrawGuidance(centerBiasPos, 0, 3000, $"P5A1_一运_场中盾连击引导");
    }
    
    [ScriptMethod(name: "P5A1_一运_转转手引导后近线待命指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31600"],
        userControl: true, suppress: 10000)]
    public void P5A1_一运_转转手引导后近线待命指路(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.1) return;
        var myIndex = sa.GetMyIndex();
        var markerVal = _pd.Priorities[myIndex];

        if (markerVal is not 1 and not 2 and not 3 and not 4) return;
        var myArmUnit = _p5A.玩家引导激光手方位;
        
        var standByPos = new Vector3(100, 0, 114).
            RotateAndExtend(Center, MathF.Round((float)myArmUnit * 2 / 3) * 45f.DegToRad());
        sa.DrawGuidance(standByPos, 0, 6000, $"P5A1_一运_转转手引导后近线待命指路");
    }
    
    [ScriptMethod(name: "P5A1_一运_光头左右扫描记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3163[89])$"],
        userControl: Debugging)]
    public void P5A1_一运_光头左右扫描记录(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.1) return;
        const uint OMEGA_RIGHT_CANNON = 31638;
        _p5A.光头左右扫描 = ev.ActionId == OMEGA_RIGHT_CANNON ? 1 : 2;
    }
    
    [ScriptMethod(name: "P5A1_一运_玩家小电视Buff记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(345[23])$"],
        userControl: Debugging)]
    public void P5A1_一运_玩家小电视Buff记录(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.1) return;
        var tidx = sa.GetPlayerIdIndex((uint)ev.TargetId);
        _pd.AddPriority(tidx, 100);    // 小电视点名+100
        const uint PLAYER_RIGHT_CANNON = 3452;
        _p5A.玩家左右扫描 = ev.StatusId == PLAYER_RIGHT_CANNON ? 1 : 2; // 右刀1，左刀2
    }
        
    [ScriptMethod(name: "P5A1_一运_盾连击目标记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31528", "TargetIndex:1"],
        userControl: Debugging)]
    public void P5A1_一运_盾连击目标记录(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.1) return;
        sa.Method.RemoveDraw($"P5A1_一运_场中盾连击引导");
        var tidx = sa.GetPlayerIdIndex((uint)ev.TargetId);
        _pd.AddPriority(tidx, 1000);    // 盾连击目标+1000
        _p5A.盾连击记录.Set();
    }
    
    [ScriptMethod(name: "P5A1_一运_分摊与小电视指路", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31528"],
        userControl: true)]
    public void P5A1_一运_分摊与小电视指路(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.1) return;
        _p5A.盾连击记录.WaitOne(1000);

        try
        {
            int myIndex   = sa.GetMyIndex();
            int myPriVal  = _pd.Priorities[myIndex];
            int markerVal = myPriVal % 100;          // 仅头标
            
            // 1. 近线组直接退出
            if (markerVal is 1 or 2 or 3 or 4) return;
            
            // 2. 是否同目标，小电视点名+100、盾连击+1000
            bool isSameTarget = _pd.SelectSpecificPriorityIndex(0, true).Value >= 1100;
            
            // 3. 计算原始坐标（以光头在C、右刀为基准）
            Vector3 shieldPos = new(99f + (isSameTarget ? -3.5f : 0), 0, 115f);
            Vector3 stackPos  = new(99f + (isSameTarget ? 0 : -3.5f), 0, 100f);
            sa.DebugMsg($"分摊位置：{stackPos}，盾连击位置：{shieldPos}", Debugging);
            
            // 4. 左右刀镜像
            if (_p5A.光头左右扫描 == 2)   // 1 右 2 左
            {
                shieldPos = shieldPos.FoldPointHorizon(Center.X);
                stackPos  = stackPos.FoldPointHorizon(Center.X);
            }

            // 5. 旋转到光头当前方向
            float rotRad = _p5A.光头位置 * 90f.DegToRad();
            shieldPos = shieldPos.RotateAndExtend(Center, rotRad);
            stackPos  = stackPos.RotateAndExtend(Center, rotRad);
            
            // 6. 画指引
            bool isShield = myPriVal / 1000 == 1;
            sa.DrawGuidance(isShield ? shieldPos : stackPos, 0, 5000, isShield ? "P5A1_一运_盾连击指路" : "P5A1_一运_分摊指路");

            // 7. 画小电视面向辅助
            bool isTv = myPriVal % 1000 >= 100;
            if (isTv)
            {
                int faceDir = (_p5A.光头位置 + (_p5A.光头左右扫描 != _p5A.玩家左右扫描 ? 2 : 0)) % 4;
                DrawFacingArrow(sa, faceDir, true,  "P5A1_一运_小电视面向辅助_正确面向");
                DrawFacingArrow(sa, faceDir, false, "P5A1_一运_小电视面向辅助_自身");
                sa.Method.TextInfo("小电视，站在外侧", 3000, true);
            }
            else
            {
                sa.Method.TextInfo("躲避小电视，站在内侧", 3000, true);
            }
            
        }
        finally
        {
            _p5A.盾连击记录.Reset();
        }
        return;
        
        void DrawFacingArrow(ScriptAccessory sa, int dir, bool isSupport, string name)
        {
            var dp = sa.DrawLine(sa.Data.Me, 0, 0, 5000, name,
                isSupport ? dir * 90f.DegToRad() : 0, 1f, 4.5f,
                isSafe: isSupport, draw: false);
            dp.FixRotation = isSupport;
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dp);
        }
    }

    [ScriptMethod(name: "*P5A1_一运_小电视自动面向辅助", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3163[89])$"], userControl: true)]
    public void P5A1_一运_小电视自动面向辅助(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.1) return;
        
        // 1. 判断进入条件
        int  myIndex  = sa.GetMyIndex();
        int  myPriVal = _pd.Priorities[myIndex];
        bool isTv     = myPriVal % 1000 >= 100;
        if (!isTv) return;
        
        var myObject = sa.Data.MyObject;
        if (myObject == null) return;
        
        // 2. 设置正确面向
        int correctFaceDir = (_p5A.光头位置 + (_p5A.光头左右扫描 != _p5A.玩家左右扫描 ? 2 : 0)) % 4;
        float correctFaceRotation = correctFaceDir * 90f.DegToRad();
        
        // 3. 开启触发
        _p5A.小电视面向辅助Framework = sa.Method.RegistFrameworkUpdateAction(Action);
        return;
        
        void Action()
        {
            var myRotation = myObject.Rotation;
            var rotationDiff = MathF.Abs(myRotation.GetDiffRad(correctFaceRotation));
            if (!sa.IsMoving() && rotationDiff > 0.1f && (DateTime.Now - _p5A.小电视面向辅助触发时间).TotalMilliseconds > 250)
            {
                _p5A.小电视面向辅助触发时间 = DateTime.Now;
                sa.SetRotation(myObject, correctFaceRotation);
            }
        }
    }

    [ScriptMethod(name: "P5A1_一运_小电视自动面向辅助关闭", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(3163[89])$"], userControl: Debugging)]
    public void P5A1_一运_小电视自动面向辅助关闭(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.1) return;
        sa.Method.UnregistFrameworkUpdateAction(_p5A.小电视面向辅助Framework);
    }
    
    [ScriptMethod(name: "P5A1_一运_绘图删除准备一传", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(31529)$"],
        userControl: Debugging)]
    public void P5A1_一运_绘图删除准备一传(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.1) return;
        _parse = 5.15;
        sa.Method.RemoveDraw("P5A1_一运.*");
            
        for (int i = 0; i < 8; i++)
        {
            _pd.Priorities[i] %= 100;    // 保留个位与十位，即删除小电视与盾连击记录
        }
        sa.DebugMsg($"一传：经矫正，{_pd.ShowPriorities()}", Debugging);
    }
    
    [ScriptMethod(name: "———————— 《P5A2 一传》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P5A2_一传_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P5A2_一传_蟑螂左右刀记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3163[67])$"],
        userControl: Debugging)]
    public void P5A2_一传_蟑螂左右刀记录(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.15) return;
        const uint BEETLE_RIGHT_CLEAVE = 31636;
        _p5A.蟑螂左右刀 = ev.ActionId == BEETLE_RIGHT_CLEAVE ? 1 : 2;
        _p5A.蟑螂左右刀记录.Set();
    }
        
    [ScriptMethod(name: "P5A2_一传_蟑螂左右刀", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3163[67])$"],
        userControl: true)]
    public void P5A2_一传_蟑螂左右刀(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.15) return;
        const uint BEETLE_RIGHT_CLEAVE = 31636;
        var rot = ev.ActionId == BEETLE_RIGHT_CLEAVE ? -float.Pi / 2 : float.Pi / 2;
        sa.DrawFan(ev.SourceId, 0, 10000, $"P5A2_一传_蟑螂左右刀", 210f.DegToRad(), rot, 90, 0);
    }
    
    [ScriptMethod(name: "P5A2_一传_指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3163[67])$"],
    userControl: true)]
    public void P5A25_一传_指路(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.15) return;
        _p5A.蟑螂左右刀记录.WaitOne();
        try
        {
            int myIndex = sa.GetMyIndex();
            int marker  = _pd.Priorities[myIndex];
            
            // 1. 配置表：marker -> 坐标（以蟑螂右刀、蟑螂在C为基准）
            // 后续只改这里即可
            Vector3[] basePos =
            {
                new(98f,   0, 119f),    // 1   Atk1   FarTarget
                new(89.4f, 0, 83.8f),   // 2   Atk2   FarTarget
                new(91.1f, 0, 111.1f),  // 3   Atk3   NearTarget
                new(86.3f, 0, 113.7f),  // 4   Atk4   NearTarget
                new(80.5f, 0, 100f),    // 10  Bind1  FarSource
                new(93.5f, 0, 100f),    // 20  Bind2  NearSource
                new(83.8f, 0, 89f),     // 11  Stop1  Idle
                new(83.8f, 0, 89f)      // 21  Stop2  Idle
            };
            
            // 2. 自动映射：marker到索引
            List<int> map = [1, 2, 3, 4, 10, 20, 11, 21]; // 与 basePos 下标一一对应
            int idx = map.IndexOf(marker);
            if (idx < 0)
            {
                sa.DebugMsg($"玩家标点信息{marker}读取错误", Debugging);
                return;
            }
            
            Vector3 pos = basePos[idx];
            
            const int BEETLE_RIGHT_CLEAVE_RECORD = 1;
            // 3. 左右刀镜像
            if (_p5A.蟑螂左右刀 != BEETLE_RIGHT_CLEAVE_RECORD)          // 1 右 2 左
                pos = pos.FoldPointHorizon(Center.X);

            // 4. 方位旋转
            pos = pos.RotateAndExtend(Center, _p5A.蟑螂位置 * 90f.DegToRad());
            sa.DrawGuidance(pos, 0, 5000, "P5A2_一传_指路");
        }
        finally
        {
            _p5A.蟑螂左右刀记录.Reset();
        }
    }
    
    [ScriptMethod(name: "P5A2_一传_指路移除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(3163[67])$"],
        userControl: Debugging)]
    public void P5A2_一传_指路移除(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.15) return;
        sa.Method.RemoveDraw("P5A2_一传.*");
    }
    
    #endregion P5A 一运一传

    #region P5B 二运二传

    [ScriptMethod(name: "———————— 《P5B1 二运》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P5B1_二运_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P5B1_二运_分P", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:32788"],
        userControl: Debugging)]
    public void P5B1_二运_分P(Event ev, ScriptAccessory sa)
    {
        _parse = 5.2;
        
        _p5A.Reset(sa);
        _p5A.Dispose();
        _p5B.Register();
        
        ResetSupportUnitVisibility(sa);
        _pd.Init(sa, "P5二运");
        _pd.AddPriorities([0, 1, 2, 3, 4, 5, 6, 7]);    // 依职能顺序添加优先值
        sa.DebugMsg($"当前阶段为：{_parse}", Debugging);
    }
    
    [ScriptMethod(name: "P5B1_二运_获取男人位置", eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["Id:7747", "SourceDataId:15720"], userControl: Debugging)]
    public void P5B1_二运_获取男人位置(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.2) return;
        var region = ev.SourcePosition.GetRadian(Center).RadianToRegion(16, isDiagDiv: true);
        _p5B.男人方位 = region;
        sa.DebugMsg($"P5B1_二运_获取男人位置：{region} / 16", Debugging);
    }
    
    [ScriptMethod(name: "P5B1_二运_获取远近线", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(342[78])$"], userControl: Debugging, suppress: 1000)]
    public void P5B1_二运_获取远近线(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.2) return;
        const uint MID_GLITCH = 3427, REMOTE_GLITCH = 3428;
        _p5B.协作程序是远线 = ev.StatusId == REMOTE_GLITCH;
        sa.DebugMsg($"记录下协作程序是 {(_p5B.协作程序是远线 ? "远" : "近")} 线", Debugging);
    }
    
    [ScriptMethod(name: "P5B1_二运_获取八方头标", eventType: EventTypeEnum.Marker, eventCondition: ["Operate:Add", "Id:regex:^(0[1234678]|12)$"],
        userControl: Debugging)]
    public void P5B1_二运_获取八方头标(Event ev, ScriptAccessory sa)
    {
        // 取攻击1234、锁链123、大饼
        if (_parse != 5.2) return;
        
        lock (_pd)
        {
            var mark = ev.Id0();
            var tidx = sa.GetPlayerIdIndex((uint)ev.TargetId);
            var targetJob = sa.GetPlayerJobByIndex(tidx);
            sa.DebugMsg($"检测到{targetJob} 被标 ev.Id {mark}", Debugging);
            _pd.AddActionCount();
            var pdVal = mark switch
            {
                0x01 => 10,    // 攻击1
                0x02 => 20,    // 攻击2
                0x03 => 30,    // 攻击3
                0x04 => 40,    // 攻击4
                0x06 => 80,   // 锁链1
                0x07 => 70,   // 锁链2
                0x08 => 60,   // 锁链3
                0x12 => 50,   // 大饼
                _ => 0
            };
            _pd.AddPriority(tidx, pdVal);
            if (_pd.ActionCount != 8) return;
            sa.DebugMsg($"P5B1_二运_获取八方头标：头标记录完毕", Debugging);
            sa.DebugMsg($"{_pd.ShowPriorities()}", Debugging);
            _p5B.八方头标记录完毕.Set();   // 头标记录
        }
    }

    [ScriptMethod(name: "P5B1_二运_八方波动炮站位", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(31603)$"],
        userControl: true)]
    public void P5B1_二运_八方波动炮站位(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.2) return;
        _p5B.八方头标记录完毕.WaitOne();
        // 以男人方位为南，逆时针增加
        var myPriValRank = _pd.FindPriorityIndexOfKey(sa.GetMyIndex());
        var myRegion = (_p5B.男人方位 + 1 + myPriValRank * 2) % 16;
        _p5B.玩家八方方位 = myRegion;
        sa.DebugMsg($"P5B1_二运_八方波动炮站位：玩家八方方位为 {myRegion} / 16", Debugging);
        
        var basePos = new Vector3(100f, 0f, _p5B.协作程序是远线 ? 119.75f : 111.75f);
        var myExtend = myPriValRank switch
        {
            0 or 7 when !_p5B.协作程序是远线 => 1,
            1 or 6 when _p5B.协作程序是远线 => -1,
            _ => 0
        };
        var myPos = basePos.RotateAndExtend(Center, myRegion * 22.5f.DegToRad(), myExtend);
        sa.DrawGuidance(myPos, 0, 10000, $"P5B1_二运_八方波动炮站位");
    }

    [ScriptMethod(name: "P5B1_二运_八方波动炮结束", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(31603)$"],
        userControl: Debugging)]
    public void P5B1_二运_八方波动炮结束(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.2) return;
        sa.Method.RemoveDraw("P5B1_二运_八方波动炮站位");
        _p5B.八方头标记录完毕.Reset();
        _parse = 5.21;
    }

    [ScriptMethod(name: "P5B1_二运_塔收集", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["Operate:Add", "DataId:regex:^(201324[56])$"],
        userControl: Debugging)]
    public void P5B1_二运_塔收集(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.21) return;
        const uint DOUBLE_TOWER = 2013246;
        lock (_p5B.塔方位类型字典)
        {
            var isDoubleTower = ev.DataId() == DOUBLE_TOWER;
            var region = ev.SourcePosition.GetRadian(Center).RadianToRegion(16, isDiagDiv: true);
            _p5B.塔方位类型字典.TryAdd(region, isDoubleTower);
            sa.DebugMsg($"P5B1_二运_塔收集：方位 {region} 的 {(isDoubleTower ? "双" : "单")}人塔", Debugging);

            if ((_p5B.塔方位类型字典.Count == 5 && _p5B.协作程序是远线) || (_p5B.塔方位类型字典.Count == 6 && !_p5B.协作程序是远线))
                _p5B.塔方位记录完毕.Set();
        }
    }
    
    [ScriptMethod(name: "P5B1_二运_踩塔击退点", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["DataId:regex:^(201324[56])$"],
        userControl: true, suppress: 1000)]
    public void P5B1_二运_踩塔击退点(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.21) return;
        _p5B.塔方位记录完毕.WaitOne();
        _p5B.玩家踩塔方位 = _p5B.FindPlayerTower(sa);
        var basePos = new Vector3(100, 0, 102.5f);
        var rad = _p5B.玩家踩塔方位 * 22.5f.DegToRad();
        var myKnockBackPos = basePos.RotateAndExtend(Center, rad);
        sa.DrawGuidance(myKnockBackPos, 0, 10000, $"P5B1_二运_踩塔击退点");
        sa.DrawLine(Center, 0, 0, 10000, $"P5B1_二运_踩塔击退指引线", rad, 20f, 20f, isSafe: true);
    }
    
    [ScriptMethod(name: "P5B1_二运_踩塔位置提示", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(31534)$"],
        userControl: true, suppress: 1000)]
    public void P5B1_二运_踩塔位置提示(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.21) return;
        var text = _p5B.协作程序是远线 ? "站在场边将线拉长" : "站在塔中间";
        sa.Method.TextInfo(text, 3000);
    }
    
    [ScriptMethod(name: "P5B1_二运_踩塔击退后删除绘图", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(31534)$"],
        userControl: Debugging)]
    public void P5B1_二运_踩塔击退后删除绘图(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.21) return;
        sa.Method.RemoveDraw("P5B1_二运_踩塔.*");
        _p5B.塔方位记录完毕.Reset();
    }
    
    [ScriptMethod(name: "P5B1_二运_塔消失转阶段", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["Operate:Remove", "DataId:regex:^(201324[56])$"],
        userControl: Debugging, suppress: 1000)]
    public void P5B1_二运_塔消失转阶段(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.21) return;
        _parse = 5.25;
        _pd.Init(sa, "P5二传");
        _pd.AddPriorities([0, 1, 2, 3, 4, 5, 6, 7]);    // 依职能顺序添加优先值
        sa.DebugMsg($"当前阶段为：{_parse}", Debugging);
    }
    
    [ScriptMethod(name: "———————— 《P5B2 二传》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P5B2_二传_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P5B2_二传_获取转圈头标", eventType: EventTypeEnum.Marker, eventCondition: ["Operate:Add", "Id:regex:^(0[1234679]|10)$"],
        userControl: Debugging)]
    public void P5B2_二传_获取转圈头标(Event ev, ScriptAccessory sa)
    {
        // 取攻击1234、锁链12、禁止12
        if (_parse != 5.25) return;
        
        lock (_pd)
        {
            var mark = ev.Id0();
            var tidx = sa.GetPlayerIdIndex((uint)ev.TargetId);
            var targetJob = sa.GetPlayerJobByIndex(tidx);
            sa.DebugMsg($"检测到{targetJob} 被标 ev.Id {mark}", Debugging);
            _pd.AddActionCount();
            var pdVal = mark switch
            {
                0x01 => 10,    // 攻击1
                0x02 => 20,    // 攻击2
                0x03 => 30,    // 攻击3
                0x04 => 40,    // 攻击4
                0x06 => 100,   // 锁链1
                0x07 => 110,   // 锁链2
                0x09 => 200,   // 禁止1
                0x10 => 210,   // 禁止2
                _ => 0
            };
            _pd.AddPriority(tidx, pdVal);
            if (_pd.ActionCount != 8) return;
            sa.DebugMsg($"P5B2_二传_获取转圈头标：头标记录完毕", Debugging);
            sa.DebugMsg($"{_pd.ShowPriorities()}", Debugging);
            _p5B.转圈头标记录完毕.Set();   // 头标记录
        }
    }
    
    [ScriptMethod(name: "P5B2_二传_获取女人位置与技能", eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["Id:7747", "SourceDataId:15720"], userControl: Debugging)]
    public void P5B2_二传_获取女人位置与技能(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.25) return;
        var region = ev.SourcePosition.GetRadian(Center).RadianToRegion(8, isDiagDiv: true);
        _p5B.女人方位 = region;
        _p5B.二传女人Id = ev.SourceId;
        var obj = sa.GetById(ev.SourceId);
        if (obj == null) return;
        var transId = sa.GetTransformationId(obj);
        sa.DebugMsg($"获得女人TransformationId为：{transId}", Debugging);
        if (transId == null) return;
        
        const byte WOMAN_CROSS = 0, WOMAN_HOTWING = 4;
        _p5B.女人是十字外安全 = transId != WOMAN_HOTWING;
        
        sa.DebugMsg($"P5B2_二传_获取女人位置与技能：{region} / 8，{(_p5B.女人是十字外安全 ? "十字外安全" : "辣翅内安全")}", Debugging);
        _p5B.女人技能记录完毕.Set();
    }
    
    [ScriptMethod(name: "P5B2_二传_获取圆环旋转方向", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(009[CD])$"], userControl: Debugging)]
    public void P5B2_二传_获取圆环旋转方向(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.25) return;
        const int ICON_ROTATE_CW  = 156;   // 009C
        _p5B.圆环是顺时针 = ev.Id0() == ICON_ROTATE_CW;
        sa.DebugMsg($"P5B2_二传_获取圆环旋转方向：{(_p5B.圆环是顺时针 ? "顺" : "逆")}时针", Debugging);
        _p5B.圆环方向记录完毕.Set();
    }
    
    [ScriptMethod(name: "P5B2_二传_圆环直线与起跑位置", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(009[CD])$"], userControl: true)]
    public void P5B2_二传_圆环直线与起跑位置(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.25) return;
        _p5B.圆环方向记录完毕.WaitOne();
        _p5B.女人技能记录完毕.WaitOne();
        _p5B.转圈头标记录完毕.WaitOne();
        
        // 女人方位边缘，逆时针22.5deg，为禁1、禁2、攻1起跑点；中心镜像过去，是其余人起跑点
        var startPos = new Vector3(100, 0, 119.5f).RotateAndExtend(Center,
            _p5B.女人方位 * 45f.DegToRad() + (_p5B.圆环是顺时针 ? 22.5f : -22.5f).DegToRad());

        var myPriValRank = _pd.FindPriorityIndexOfKey(sa.GetMyIndex());
        if (myPriValRank is 1 or 2 or 3 or 4 or 5)
            startPos = startPos.PointCenterSymmetry(Center);
        sa.DrawGuidance(startPos, 0, 10000, $"P5B2_二传_圆环直线与起跑位置_起跑位置");
        
        var dp = sa.DrawRect(ev.SourceId, 0, 10000, $"P5B2_二传_圆环直线与起跑位置_圆环直线", 0, 12, 50);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
    }
    
    [ScriptMethod(name: "P5B2_二传_获得圆环Id", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(31631)$"], userControl: Debugging)]
    public void P5B2_二传_获得圆环Id(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.25) return;
        _p5B.圆环Id = ev.SourceId;
    }
    
    [ScriptMethod(name: "P5B2_二传_圆环释放第一条", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(31631)$", "TargetIndex:1"], userControl: Debugging)]
    public void P5B2_二传_圆环释放第一条(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.25) return;
        
        _p5B.圆环方向记录完毕.Reset();
        _p5B.女人技能记录完毕.Reset();
        _p5B.转圈头标记录完毕.Reset();
        
        sa.Method.RemoveDraw($"P5B2_二传_圆环直线与起跑位置_圆环直线");
        sa.Method.RemoveDraw($"P5B2_二传_圆环直线与起跑位置_起跑位置");
    }
    
    [ScriptMethod(name: "P5B2_二传_圆环攻击绘图", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(31631)$", "TargetIndex:1"], userControl: true)]
    public void P5B2_二传_圆环攻击绘图(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.25) return;
        
        for (int i = 0; i < 13; i++)
        {
            var rotation = _p5B.女人方位 * 45f.DegToRad() + (i + 1) * 9f.DegToRad() * (_p5B.圆环是顺时针 ? -1 : 1);
            var dp = sa.DrawRect(Center, 0, 10000, $"P5B2_二传_圆环攻击绘图{i}", rotation, 10, 60, draw: false);
            dp.Color = sa.Data.DefaultDangerColor.WithW(0.25f);
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
        }
    }
    
        
    [ScriptMethod(name: "P5B2_二传_圆环攻击绘图删除", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(31632)$", "TargetIndex:1"], userControl: Debugging)]
    public void P5B2_二传_圆环攻击绘图删除(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.25) return;
        sa.Method.RemoveDraw($"^(P5B2_二传_圆环攻击绘图{_p5B.圆环攻击次数})$");
        _p5B.圆环攻击次数++;
    }
    
    [ScriptMethod(name: "P5B2_二传_女人攻击范围绘图", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(31631)$", "TargetIndex:1"], userControl: true)]
    public void P5B2_二传_女人攻击范围绘图(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.25) return;
        if (_p5B.女人是十字外安全)
        {
            var dp1 = sa.DrawRect(_p5B.二传女人Id, 0, 5500, $"P5B2_二传_女人攻击范围_女十字1", 0, 10, 60, draw: false);
            var dp2 = sa.DrawRect(_p5B.二传女人Id, 0, 5500, $"P5B2_二传_女人攻击范围_女十字2", float.Pi / 2, 10, 60, draw: false);
            dp1.Color = sa.Data.DefaultDangerColor.WithW(2.5f);
            dp2.Color = sa.Data.DefaultDangerColor.WithW(2.5f);
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp1);
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp2);
        }
        else
        {
            var dp3 = sa.DrawDonut(_p5B.二传女人Id, 0, 5500, $"P5B2_二传_女人攻击范围_女辣翅", 60, 8, draw: false);
            dp3.Color = sa.Data.DefaultDangerColor.WithW(2.5f);
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.HotWing, dp3);
        }
    }
    
    [ScriptMethod(name: "P5B2_二传_穿入或停留提示", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(31631)$", "TargetIndex:1"], userControl: true)]
    public void P5B2_二传_穿入或停留提示(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.25) return;
        var text = _p5B.女人是十字外安全 ? "停停停" : "穿穿穿";
        sa.Method.TextInfo(text, 1500, true);
    }
    
    [ScriptMethod(name: "*P5B2_二传_女人十字暂时移除大圆环", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(31631)$", "TargetIndex:1"], userControl: true)]
    public void P5B2_二传_女人十字暂时移除大圆环(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.25) return;
        if (!SpecialMode) return;
        if (!_p5B.女人是十字外安全) return;
        sa.DebugMsg($"女人是十字，暂时移除大圆环", Debugging);
        sa.WriteVisible(sa.GetById(_p5B.圆环Id), false);
    }
    
    [ScriptMethod(name: "P5B2_二传_女人释放技能", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(3153[123])$", "TargetIndex:1"], userControl: Debugging, suppress: 1000)]
    public void P5B2_二传_女人释放技能(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.25) return;
        _p5B.女人十字辣翅释放完毕.Set();
        sa.Method.RemoveDraw($"P5B2_二传_女人攻击范围.*");
        if (!_p5B.女人是十字外安全) return;
        sa.Method.TextInfo("穿穿穿", 1500, true);
        if (!SpecialMode) return;
        sa.WriteVisible(sa.GetById(_p5B.圆环Id), true);
    }
    
    [ScriptMethod(name: "P5B2_二传_指路", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(3153[123])$", "TargetIndex:1"], userControl: true, suppress: 1000)]
    public void P5B2_二传_指路(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.25) return;
        _p5B.女人十字辣翅释放完毕.WaitOne();

        var myPriValRank = _pd.FindPriorityIndexOfKey(sa.GetMyIndex());
        const int ATK1 = 0, ATK2 = 1, ATK3 = 2, ATK4 = 3;
        const int BIND1 = 4, BIND2 = 5, STOP1 = 6, STOP2 = 7;

        var myBasePos = myPriValRank switch
        {
            ATK1 => new Vector3(80.5f, 0, 100f),
            ATK2 => new Vector3(119.5f, 0, 100f),
            ATK3 => new Vector3(98f, 0, 89f),
            ATK4 => new Vector3(94.74f, 0, 81.74f),
            BIND1 => new Vector3(110f, 0, 100f),
            BIND2 => new Vector3(105.26f, 0, 81.74f),
            STOP1 => new Vector3(113.44f, 0, 113.44f),
            STOP2 => new Vector3(86.56f, 0, 113.44f),
        };

        if (!_p5B.圆环是顺时针 && myPriValRank is ATK1 or ATK2 or BIND1)
            myBasePos = myBasePos.FoldPointHorizon(Center.X);

        var rotation = _p5B.女人方位 * 45f.DegToRad();
        var safePos = myBasePos.RotateAndExtend(Center, rotation);
        sa.DrawGuidance(safePos, 0, 10000, $"P5B2_二传_指路");
    }
    
    [ScriptMethod(name: "P5A2_二传_处理结束", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(31625|33040)$"],
        userControl: Debugging)]
    public void P5A2_二传_处理结束(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.25) return;
        sa.Method.RemoveDraw("P5B2_二传.*");
        _p5B.女人十字辣翅释放完毕.Reset();
    }
    
    #endregion 二运二传
    
    #region P5C 三运三传

    [ScriptMethod(name: "———————— 《P5C1 三运》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P5C1_三运_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P5C1_三运_分P", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:32789"],
        userControl: Debugging)]
    public void P5C1_三运_分P(Event ev, ScriptAccessory sa)
    {
        _parse = 5.3;
        
        _p5B.Reset(sa);
        _p5B.Dispose();
        _p5C.Register();
        
        ResetSupportUnitVisibility(sa);
        _pd.Init(sa, "P5三运");
        _pd.AddPriorities([0, 1, 2, 3, 4, 5, 6, 7]);    // 依职能顺序添加优先值
        sa.DebugMsg($"当前阶段为：{_parse}", Debugging);
    }

    [ScriptMethod(name: "P5C1_三运_获取男女组合技", eventType: EventTypeEnum.PlayActionTimeline,
        eventCondition: ["Id:7747", "SourceDataId:regex:^(15721|15722)$"], userControl: Debugging)]
    public void P5C1_三运_获取男女组合技(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.3) return;
        const uint OMEGA_MALE = 15721, OMEGA_FEMALE = 15722, OMEGA_BALD = 14669;
        const byte MAN_CHARIOT = 0, MAN_DONUT = 4, WOMAN_CROSS = 0, WOMAN_HOTWING = 4;
        
        lock (_p5C.组合技记录)
        {
            if (_p5C.组合技记录.Count >= 6) return;
            var region = ev.SourcePosition.GetRadian(Center).RadianToRegion(4);
            IGameObject? obj = sa.GetById(ev.SourceId);
            if (obj == null) return;
            var transId = sa.GetTransformationId(obj);
            var dataId = obj.DataId;
            var isFirstRound = _p5C.组合技记录.Count < 2;
            
            var skillId = (dataId == OMEGA_FEMALE ? 8 : 0) + (transId == 4 ? 4 : 0) + region;
            _p5C.组合技记录.TryAdd(region, (skillId, isFirstRound));
            sa.DebugMsg($"在方位 {region} 有第 {(isFirstRound ? "一" : "二")} 轮技能，技能ID {skillId}", Debugging);
            if (_p5C.组合技记录.Count != 4) return;
            _p5C.男女人组合技记录完毕.Set();
        }
    }
    
    [ScriptMethod(name: "P5C1_三运_获取光头组合技和安全点", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(3164[34])$"], userControl: Debugging)]
    public void P5C1_三运_获取光头组合技和安全点(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.3) return;
        const uint FRONT_FIRST = 31643, SIDE_FIRST = 31644;
        _p5C.男女人组合技记录完毕.WaitOne();
        lock (_p5C.组合技记录)
        {
            if (_p5C.组合技记录.Count >= 6) return;
            // region已无所谓，随便取两个0~3以外的值
            var firstRoundSkillId = ev.ActionId == FRONT_FIRST ? 17 : 16;
            var secondRoundSkillId = ev.ActionId == FRONT_FIRST ? 16 : 17;
            _p5C.组合技记录.TryAdd(10, (firstRoundSkillId, true));
            _p5C.组合技记录.TryAdd(11, (secondRoundSkillId, false));
            sa.DebugMsg($"光头的第一轮技能Id：{firstRoundSkillId}", Debugging);
            
            if (_p5C.组合技记录.Count != 6) return;
            _p5C.FindComboAttackSafePoint(sa);
            sa.DebugMsg($"组合技安全区地点为：{_p5C.组合技安全区[0]}, {_p5C.组合技安全区[1]}", Debugging);
            
            _p5C.男女人组合技记录完毕.Reset();
            _p5C.组合技安全区记录完毕.Set();
        }
    }

    [ScriptMethod(name: "*P5C1_三运_出现的男女人与光头缩小", eventType: EventTypeEnum.PlayActionTimeline,
        eventCondition: ["Id:7747", "SourceDataId:regex:^(15721|15722|14669)$"], userControl: true)]
    public void P5C1_三运_出现的男女人与光头缩小(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.3) return;
        if (!SpecialMode) return;
        var obj = sa.GetById(ev.SourceId);
        sa.ScaleModify(obj, 0.4f);
    }

    [ScriptMethod(name: "P5C1_三运_组合技攻击范围绘图", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(3164[34])$"], userControl: true)]
    public void P5C1_三运_组合技攻击范围绘图(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.3) return;
        _p5C.组合技安全区记录完毕.WaitOne();

        foreach (var (region, (skillId, isFirstRound)) in _p5C.组合技记录)
            DrawComboSkillAttackRange(sa, region, skillId, isFirstRound, "第一轮");

        _p5C.第一段组合技结束.WaitOne();
        sa.Method.RemoveDraw($"P5C1_三运_组合技攻击范围绘图_第一轮.*");
        
        foreach (var (region, (skillId, isFirstRound)) in _p5C.组合技记录)
            DrawComboSkillAttackRange(sa, region, skillId, !isFirstRound, "第二轮");
        
        return;
        
        void DrawComboSkillAttackRange(ScriptAccessory sa, int region, int skillId, bool draw, string prefix)
        {
            if (!draw) return;
            var skillType = skillId / 4;
            
            Vector3 ownerPos;
            float rotation;
            if (region < 4)
            {
                ownerPos = new Vector3(100, 0, 110).RotateAndExtend(Center, (45f + 90f * region).DegToRad());
                rotation = (225f + 90f * region).DegToRad();
            }
            else
            {
                ownerPos = new Vector3(100, 0, 100);
                rotation = skillId == 16 ? 90f.DegToRad() : 0f;
            }
            
            // skillType
            const int CHARIOT = 0, DONUT = 1, CROSS = 2, HOTWING = 3, FAN = 4;
            switch (skillType)
            {
                case DONUT:
                    sa.DrawDonut(ownerPos, 0, 10000, $"P5C1_三运_组合技攻击范围绘图_{prefix}_男月环", 40, 10);
                    break;
                case CHARIOT:
                    sa.DrawCircle(ownerPos, 0, 10000, $"P5C1_三运_组合技攻击范围绘图_{prefix}_男钢铁", 10);
                    break;
                case CROSS:
                    var dp1 = sa.DrawRect(ownerPos, 0, 10000, $"P5C1_三运_组合技攻击范围绘图_{prefix}_女十字1", rotation, 10, 60, draw: false);
                    var dp2 = sa.DrawRect(ownerPos, 0, 10000, $"P5C1_三运_组合技攻击范围绘图_{prefix}_女十字2", rotation + float.Pi / 2, 10, 60, draw: false);
                    sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp1);
                    sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp2);
                    break;
                case HOTWING:
                    var dp3 = sa.DrawDonut(ownerPos, 0, 10000, $"P5C1_三运_组合技攻击范围绘图_{prefix}_女辣翅", 60, 8, draw: false);
                    dp3.Rotation = rotation;
                    sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.HotWing, dp3);
                    break;
                case FAN:
                    sa.DrawFan(ownerPos, 0, 10000, $"P5C1_三运_组合技攻击范围绘图_{prefix}_光头刀1", 120f.DegToRad(), rotation, 20f, 0f);
                    sa.DrawFan(ownerPos, 0, 10000, $"P5C1_三运_组合技攻击范围绘图_{prefix}_光头刀2", 120f.DegToRad(), rotation + 180f.DegToRad(), 20f, 0f);
                    break;
            }
        }
    }

    [ScriptMethod(name: "P5C1_三运_安全点指路", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(3164[34])$"], userControl: true)]
    public void P5C1_三运_安全点指路(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.3) return;
        _p5C.组合技安全区记录完毕.WaitOne();
        var safePos1 = GetSafePosOfComboSkill(_p5C.组合技安全区[0]);
        var safePos2 = GetSafePosOfComboSkill(_p5C.组合技安全区[1]);
        
        sa.DrawGuidance(safePos1, 0, 10000, $"P5C1_三运_安全点指路1");
        sa.DrawGuidance(safePos1, safePos2, 0, 10000, $"P5C1_三运_安全点指路1预备", isSafe: false);

        _p5C.第一段组合技结束.WaitOne();
        sa.Method.RemoveDraw($"P5C1_三运_安全点指路1.*");
        sa.DrawGuidance(safePos2, 0, 10000, $"P5C1_三运_安全点指路2");
        
        Vector3 GetSafePosOfComboSkill(int safePosIdx)
        {
            var rotation = safePosIdx % 4 * 90f.DegToRad();
            var distance = (safePosIdx / 4) switch
            {
                0 => 4.5f,
                1 => 12f,
                2 => 19f,
                _ => 0,
            };
            return new Vector3(100, 0, 100 + distance).RotateAndExtend(Center, rotation);
        }
    }

    [ScriptMethod(name: "P5C1_三运_第一段组合技结束", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(3164[34])$"], userControl: Debugging)]
    public void P5C1_三运_第一段组合技结束(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.3) return;
        _p5C.第一段组合技结束.Set();
    }
    
    [ScriptMethod(name: "P5C1_三运_前半运动会组合技结束", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(3160[78])$"], userControl: Debugging)]
    public void P5C1_三运_前半运动会组合技结束(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.3) return;
        sa.Method.RemoveDraw($"P5C1_三运.*");
        _p5C.第一段组合技结束.Reset();
        _p5C.组合技安全区记录完毕.Reset();
        _parse = 5.35;
    }

    [ScriptMethod(name: "———————— 《P5C2 三传》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P5C2_三传_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P5C2_三传_分P", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void P5B2_三传_分P(Event ev, ScriptAccessory sa)
    {
        _parse = 5.35;
        sa.DebugMsg($"当前阶段为：{_parse}", Debugging);
    }
    
    [ScriptMethod(name: "P5C2_三传_获取三传头标", eventType: EventTypeEnum.Marker, eventCondition: ["Operate:Add", "Id:regex:^(0[1234679]|10)$"],
        userControl: Debugging)]
    public void P5C2_三传_获取三传头标(Event ev, ScriptAccessory sa)
    {
        // 取攻击1234、锁链12、禁止12
        if (_parse != 5.35) return;
        
        lock (_pd)
        {
            var mark = ev.Id0();
            var tidx = sa.GetPlayerIdIndex((uint)ev.TargetId);
            var targetJob = sa.GetPlayerJobByIndex(tidx);
            sa.DebugMsg($"检测到{targetJob} 被标 ev.Id {mark}", Debugging);
            _pd.AddActionCount();
            var pdVal = mark switch
            {
                0x01 => 10,    // 攻击1
                0x02 => 20,    // 攻击2
                0x03 => 30,    // 攻击3
                0x04 => 40,    // 攻击4
                0x06 => 100,   // 锁链1
                0x07 => 110,   // 锁链2
                0x09 => 200,   // 禁止1
                0x10 => 210,   // 禁止2
                _ => 0
            };
            _pd.AddPriority(tidx, pdVal);
            if (_pd.ActionCount != 8) return;
            sa.DebugMsg($"P5C2_三传_获取三传头标：头标记录完毕", Debugging);
            sa.DebugMsg($"{_pd.ShowPriorities()}", Debugging);
            _p5C.三传头标记录完毕.Set();   // 头标记录
        }
    }

    [ScriptMethod(name: "P5C2_三传_光头扫描范围", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3163[89])$"],
        userControl: true)]
    public void P5C2_三传_光头扫描范围(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.35) return;
        _p5C.三传头标记录完毕.WaitOne();
        var myPriValRank = _pd.FindPriorityIndexOfKey(sa.GetMyIndex());
        var isSafe = myPriValRank >= 6;
        const uint CANNON_LEFT = 31639, CANNON_RIGHT = 31638;
        var rotation = (ev.ActionId == CANNON_RIGHT ? 90f : -90f).DegToRad();
        sa.DrawFan(Center, 0, 10000, $"P5C2_三传_光头扫描范围", float.Pi, rotation, 40, 0, isSafe);
    }

    [ScriptMethod(name: "P5C2_三传_指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3163[89])$"],
        userControl: true)]
    public void P5C2_三传_指路(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.35) return;
        _p5C.三传头标记录完毕.WaitOne();

        var myPriValRank = _pd.FindPriorityIndexOfKey(sa.GetMyIndex());
        const int ATK1 = 0, ATK2 = 1, ATK3 = 2, ATK4 = 3;
        const int BIND1 = 4, BIND2 = 5, STOP1 = 6, STOP2 = 7;

        // 以欧米茄打上侧为准
        var safePos = myPriValRank switch
        {
            ATK1 => new Vector3(80.5f, 0, 100.5f),
            ATK2 => new Vector3(119.5f, 0, 100.5f),
            ATK3 => new Vector3(102f, 0, 111f),
            ATK4 => new Vector3(105.26f, 0f, 118.26f),
            BIND1 => new Vector3(90f, 0f, 100.5f),
            BIND2 => new Vector3(94.74f, 0f, 118.26f),
            STOP1 => new Vector3(90.8f, 0f, 90.8f),
            STOP2 => new Vector3(109.2f, 0f, 90.8f),
        };

        const uint CANNON_LEFT = 31639, CANNON_RIGHT = 31638;
        var rotation = (ev.ActionId == CANNON_LEFT ? 90f : -90f).DegToRad();
        safePos = safePos.RotateAndExtend(Center, rotation);
        sa.DrawGuidance(safePos, 0, 10000, $"P5C2_三传_指路");
    }
    
    [ScriptMethod(name: "P5C2_三传_欧米茄扫描结束三传结束", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(3163[89])$"],
        userControl: Debugging)]
    public void P5C3_三传_欧米茄扫描结束三传结束(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.35) return;
        _p5C.三传头标记录完毕.Reset();
        sa.Method.RemoveDraw($"P5C2_三传.*");
        _pd.Init(sa, "P5四传");
        _parse = 5.38;
        sa.DebugMsg($"当前阶段为：{_parse}", Debugging);
    }
    
    [ScriptMethod(name: "———————— 《P5C3 四传》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P5C3_四传_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P5C3_四传_获取四传头标", eventType: EventTypeEnum.Marker, eventCondition: ["Operate:Add", "Id:regex:^(0[1234679]|10)$"],
        userControl: Debugging)]
    public void P5C3_四传_获取四传头标(Event ev, ScriptAccessory sa)
    {
        // 取攻击1234、锁链12、禁止12
        if (_parse != 5.38) return;
        
        lock (_pd)
        {
            var mark = ev.Id0();
            var tidx = sa.GetPlayerIdIndex((uint)ev.TargetId);
            var targetJob = sa.GetPlayerJobByIndex(tidx);
            sa.DebugMsg($"检测到{targetJob} 被标 ev.Id {mark}", Debugging);
            _pd.AddActionCount();
            var pdVal = mark switch
            {
                0x01 => 10,    // 攻击1
                0x02 => 20,    // 攻击2
                0x03 => 30,    // 攻击3
                0x04 => 40,    // 攻击4
                0x06 => 100,   // 锁链1
                0x07 => 110,   // 锁链2
                0x09 => 200,   // 禁止1
                0x10 => 210,   // 禁止2
                _ => 0
            };
            _pd.AddPriority(tidx, pdVal);
            if (_pd.ActionCount != 8) return;
            sa.DebugMsg($"P5C3_四传_获取四传头标：头标记录完毕", Debugging);
            sa.DebugMsg($"{_pd.ShowPriorities()}", Debugging);
            _p5C.四传头标记录完毕.Set();   // 头标记录
        }
    }
    
    [ScriptMethod(name: "P5C3_四传_蟑螂方位记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:32374"], userControl: Debugging)]
    public void P5C3_四传_蟑螂方位记录(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.38) return;

        _p5C.蟑螂Id = ev.SourceId;
        _p5C.蟑螂方位 = ev.SourcePosition.GetRadian(Center).RadianToRegion(4, isDiagDiv: true);
        sa.DebugMsg($"P5C3_四传_蟑螂方位记录：{_p5C.蟑螂方位} / 4", Debugging);
        _p5C.蟑螂方位记录完毕.Set();
    }

    [ScriptMethod(name: "P5C3_四传_指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:32374"], userControl: true)]
    public void P5C3_四传_指路(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.38) return;
        
        _p5C.蟑螂方位记录完毕.WaitOne();
        _p5C.四传头标记录完毕.WaitOne();
        
        var myPriValRank = _pd.FindPriorityIndexOfKey(sa.GetMyIndex());
        const int ATK1 = 0, ATK2 = 1, ATK3 = 2, ATK4 = 3;
        const int BIND1 = 4, BIND2 = 5, STOP1 = 6, STOP2 = 7;

        // 以蟑螂方位在南为准
        var myBasePos = myPriValRank switch
        {
            ATK1 => new Vector3(119.5f, 0, 100f),
            ATK2 => new Vector3(80.5f, 0, 100f),
            ATK3 => new Vector3(98f, 0, 89f),
            ATK4 => new Vector3(94.74f, 0f, 81.74f),
            BIND1 => new Vector3(110f, 0f, 100f),
            BIND2 => new Vector3(105.26f, 0f, 81.74f),
            STOP1 => new Vector3(110.3f, 0f, 116.5f),
            STOP2 => new Vector3(89.7f, 0f, 116.5f),
        };

        var safePos = myBasePos.RotateAndExtend(Center, _p5C.蟑螂方位 * 90f.DegToRad());
        sa.DrawGuidance(safePos, 0, 10000, $"P5C3_四传_指路");
    }

    [ScriptMethod(name: "P5C3_四传_结束", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:32374"],
        userControl: Debugging)]
    public void P5C3_四传_结束(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.38) return;
        _parse = 5.39;
        _p5C.蟑螂方位记录完毕.Reset();
        _p5C.四传头标记录完毕.Reset();
        _p5C.Reset(sa);
        _p5C.Dispose();
        sa.Method.RemoveDraw($"P5.*");
    }

    [ScriptMethod(name: "P5C3_四传_接线范围", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0059"],
        userControl: true)]
    public void P5C3_四传_接线范围(Event ev, ScriptAccessory sa)
    {
        if (_parse != 5.38) return;

        _p5C.蟑螂方位记录完毕.WaitOne();
        _p5C.四传头标记录完毕.WaitOne();
        
        if (_p5C.扫描接线开启) return;
        _p5C.扫描接线开启 = true;
        
        // 当且仅当本轮正确的玩家接线，才会绘制大圈
        _p5C.扫描接线Framework = sa.Method.RegistFrameworkUpdateAction(Action);
        
        const uint TETHER_ID = 0x59;
        return;
        
        void Action()
        {
            var bossObj = sa.GetById(_p5C.蟑螂Id);
            if (bossObj is null) return;
            
            foreach (var member in sa.Data.PartyList)
            {
                // 找到小队成员
                IGameObject? memberObj = sa.GetById(member);
                if (memberObj is null) continue;
                int memberIdx = sa.GetPlayerIdIndex(member);
                
                void CleanUp()
                {
                    _p5C.接线绘图字典.Remove(memberIdx, out _);
                    sa.Method.RemoveDraw($"P5C_四传_接线范围_线源{memberIdx}");
                }

                // 获取优先级，禁止头标优先值 >=200
                if (!_pd.Priorities.TryGetValue(memberIdx, out int memberPrival) || memberPrival < 200) { CleanUp(); continue; }
                
                // 线源追溯
                var tetherSource = sa.GetTetherSource((IBattleChara?)memberObj, TETHER_ID);
                bool isCorrectTether = tetherSource.Count == 1 && tetherSource[0] == _p5C.蟑螂Id;
                if (!isCorrectTether) { CleanUp(); continue; }

                // 避免反复绘图
                if (_p5C.接线绘图字典.TryAdd(memberIdx, true))
                    sa.DrawCircle(member, 0, Int32.MaxValue, $"P5C3_四传_接线范围_线源{memberIdx}", 15);
            }
        }
    }

    #endregion 三运三传

    #region P6 宇宙记忆
    
    [ScriptMethod(name: "———————— 《P6 宇宙记忆》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P6_普通攻击_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P6_宇宙记忆_分P", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31649"],
        userControl: Debugging)]
    public void P6_宇宙记忆_分P(Event ev, ScriptAccessory sa)
    {
        _parse = 6;
        _p6.Reset(sa);
        _p6.Register();
        _p6.BossId = ev.SourceId;
        ResetSupportUnitVisibility(sa);
        sa.DebugMsg($"当前阶段为：{_parse}", Debugging);
    }
    
    [ScriptMethod(name: "P6_普通攻击绘图", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31649", "TargetIndex:1"],
        userControl: true)]
    public void P6_普通攻击绘图(Event ev, ScriptAccessory sa)
    {
        if (_parse != 6) return;
        _p6.使能普通攻击绘图 = true;
        _p6.普通攻击绘图Framework = sa.Method.RegistFrameworkUpdateAction(Action);
        return;
        
        void Action()
        {
            if (!_p6.使能普通攻击绘图)
                return;
            
            var myIndex = sa.GetMyIndex();
            UpdateFarthestPlayer();
            UpdateMainTank();
            return;
            
            void UpdateFarthestPlayer()
            {
                int farthestIdx = FindFarthestPlayerIndex();
                if (farthestIdx == _p6.最远玩家Idx) return;
                
                const string hint = "最远";
                sa.Method.RemoveDraw($"P6_普通攻击绘图_{hint}_{_p6.最远玩家Idx}");
                _p6.最远玩家Idx = farthestIdx;
                
                DrawPlayerCircle(farthestIdx, hint, farthestIdx == 1 && myIndex == 1);
                
                if (farthestIdx != 1 && myIndex == 1 && !_p6.已绘制远离指示线)
                {
                    _p6.已绘制远离指示线 = true;
                    var dp = sa.DrawGuidance(Center, 0, Int32.MaxValue, $"P6_普通攻击绘图_远离指示线", 
                        180f.DegToRad(), draw: false);
                    sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dp);
                    sa.Method.TextInfo("退到最远！", 1500, true);
                }
                else if (farthestIdx == 1 && myIndex == 1 && _p6.已绘制远离指示线)
                {
                    _p6.已绘制远离指示线 = false;
                    sa.Method.RemoveDraw($"P6_普通攻击绘图_远离指示线");
                }
            }
            
            int FindFarthestPlayerIndex()
            {
                float maxDistance = 0f;
                int farthestIdx = 0;

                foreach (uint memberId in sa.Data.PartyList)
                {
                    var memberObj = sa.GetById(memberId);
                    if (memberObj is null) continue;

                    float distance = Vector3.Distance(memberObj.Position, Center);
                    if (distance < maxDistance) continue;
                    maxDistance = distance;
                    farthestIdx = sa.GetPlayerIdIndex(memberId);
                }
                return farthestIdx;
            }
            
            void UpdateMainTank()
            {
                int mainTankIdx = sa.GetPlayerIdIndex((uint)sa.Data.EnmityList[_p6.BossId][0]);
                if (mainTankIdx == _p6.一仇玩家Idx) return;
                const string hint = "一仇";
                sa.Method.RemoveDraw($"P6_普通攻击绘图_{hint}_{_p6.一仇玩家Idx}");
                _p6.一仇玩家Idx = mainTankIdx;

                DrawPlayerCircle(mainTankIdx, hint, mainTankIdx == 0 && myIndex == 0);

                if (mainTankIdx != 0 && myIndex == 0 && !_p6.已提示建立一仇)
                {
                    _p6.已提示建立一仇 = true;
                    sa.Method.TextInfo("建立一仇！", 1500, true);
                }
                else if (mainTankIdx == 0 && myIndex == 0 && _p6.已提示建立一仇)
                {
                    _p6.已提示建立一仇 = false;
                }
            }
            
            void DrawPlayerCircle(int playerIdx, string hint, bool isCorrectChara)
            {
                uint playerId = sa.Data.PartyList[playerIdx];
            
                var dp = sa.DrawCircle(playerId, 0, Int32.MaxValue, $"P6_普通攻击绘图_{hint}_{playerIdx}", 
                    5f, isCorrectChara, draw: false);
            
                dp.Color = dp.Color.WithW(2f);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }
        }
    }
    
    [ScriptMethod(name: "P6_宇宙龙炎_绘图", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31654"],
        userControl: true)]
    public void P6_宇宙龙炎_绘图(Event ev, ScriptAccessory sa)
    {
        if (_parse < 6) return;
        _p6.宇宙龙炎绘图开启.WaitOne();
        
        var sid = ev.SourceId;
        var myIndex = sa.GetMyIndex();
        var isTank = myIndex <= 1;
        var dp1 = sa.DrawCircle(sid, 0, 10000, $"P6_宇宙龙炎_绘图1", 8f, isTank, draw: false);
        dp1.SetOwnersDistanceOrder(true, 1);
        var dp2 = sa.DrawCircle(sid, 0, 10000, $"P6_宇宙龙炎_绘图2", 8f, isTank, draw: false);
        dp2.SetOwnersDistanceOrder(true, 2);
        var dp3 = sa.DrawCircle(sid, 0, 10000, $"P6_宇宙龙炎_绘图3", 6f, !isTank, draw: false);
        dp3.SetOwnersDistanceOrder(true, 3);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp1);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp2);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp3);
    }
    
    [ScriptMethod(name: "P6_宇宙龙炎_绘图删除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31655"],
        userControl: Debugging, suppress: 500)]
    public void P6A_宇宙龙炎_绘图删除(Event ev, ScriptAccessory sa)
    {
        if (_parse < 6) return;
        sa.Method.RemoveDraw($"P6.*");
        
        switch (_parse)
        {
            case 6.1:
                _p6.ResetCosmoArrow(sa);
                break;
            case 6.2:
                _p6.ResetUnlimitedWaveCannon();
                _p6.ResetSpreadWaveCannon();
                break;
        }

        _p6.使能普通攻击绘图 = true;
        _p6.宇宙龙炎绘图开启.Reset();
    }

    [ScriptMethod(name: "P6_波动炮_分摊位置绘图", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31657"],
        userControl: true)]
    public void P6_波动炮_分摊位置绘图(Event ev, ScriptAccessory sa)
    {
        if (_parse < 6) return;
        _p6.波动炮分摊绘图开启.WaitOne();
        var isTank = sa.GetMyIndex() <= 1;
        var stackPos = new Vector3(100, 0, isTank ? 103 : 109);
        sa.DrawGuidance(stackPos, 0, 10000, $"P6_波动炮_分摊位置绘图");
    }
    
    [ScriptMethod(name: "P6_波动炮_八方攻击计数", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31659"],
        userControl: Debugging, suppress: 500)]
    public void P6_波动炮_八方攻击计数(Event ev, ScriptAccessory sa)
    {
        if (_parse < 6) return;
        _p6.波动炮判定次数++;
        if (_p6.波动炮判定次数 < 2) return;
        sa.Method.RemoveDraw($"P6_波动炮_八方指路.*");
        sa.Method.RemoveDraw($"P6_波动炮_指引线.*");
        _p6.波动炮分摊绘图开启.Set();
    }
    
    [ScriptMethod(name: "P6_波动炮_分摊绘图删除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31658"],
        userControl: Debugging, suppress: 500)]
    public void P6_波动炮_分摊绘图删除(Event ev, ScriptAccessory sa)
    {
        if (_parse < 6) return;
        sa.Method.RemoveDraw($".*");
        switch (_parse)
        {
            case 6.1:
                _p6.ResetUnlimitedWaveCannon();
                _p6.ResetSpreadWaveCannon();
                break;
            case 6.2:
                _p6.ResetCosmoArrow(sa);
                break;
        }
        _p6.使能普通攻击绘图 = true;
        _p6.波动炮八方绘图开启.Reset();
        _p6.波动炮分摊绘图开启.Reset();
    }

    #endregion 宇宙记忆
    
    #region P6A 宇宙天箭

    [ScriptMethod(name: "———————— 《P6A 宇宙天箭》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P6A_宇宙天箭_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P6A_宇宙天箭_分P", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31650"],
        userControl: Debugging)]
    public void P6A_宇宙天箭_分P(Event ev, ScriptAccessory sa)
    {
        if (_parse < 6) return;
        _parse = _parse == 6.1 ? 6.2 : 6.1;
        sa.DebugMsg($"当前阶段为：{_parse} 宇宙天箭", Debugging);
        _p6.ResetAutoAttack(sa, false);
        _p6.宇宙天箭读条开始.Set();
    }
    
    [ScriptMethod(name: "P6A_宇宙天箭_类型判断", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31651"],
        userControl: Debugging, suppress: 1000)]
    public void P6A_宇宙天箭_类型判断(Event ev, ScriptAccessory sa)
    {
        if (_parse < 6) return;
        
        _p6.宇宙天箭读条开始.WaitOne();
        if (_p6.宇宙天箭类型判断完毕) return;

        var region = ev.SourcePosition.GetRadian(Center).RadianToRegion(8, isDiagDiv: true);
        _p6.宇宙天箭是内天箭 = region % 2 == 0;
        _p6.宇宙天箭类型判断完毕 = true;
        sa.DebugMsg($"P6A_宇宙天箭_类型判断：阶段 {_parse} 的宇宙天箭，类型为 {(_p6.宇宙天箭是内天箭 ? "内" : "外")} 天箭", Debugging);
    }
    
    [ScriptMethod(name: "P6A_宇宙天箭_读条完毕", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31650"],
        userControl: Debugging)]
    public void P6A_宇宙天箭_读条完毕(Event ev, ScriptAccessory sa)
    {
        if (_parse < 6) return;
        _p6.宇宙天箭读条开始.Reset();
    }
    
    [ScriptMethod(name: "*P6A_宇宙天箭_屏蔽特效", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(3165[12])$", "TargetIndex:1"], userControl: true)]
    public void P6A_宇宙天箭_屏蔽特效(Event ev, ScriptAccessory sa)
    {
        if (_parse < 6) return;
        if (!SpecialMode) return;
        if (_p6.宇宙天箭判定次数 > 4) return;
        sa.WriteVisible(sa.GetById(ev.SourceId), false);
    }
    
    [ScriptMethod(name: "P6A_宇宙天箭_判定次数增加", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(3165[12])$"],
        userControl: Debugging, suppress: 500)]
    public void P6A_宇宙天箭_判定次数增加(Event ev, ScriptAccessory sa)
    {
        if (_parse < 6) return;
        _p6.宇宙天箭判定次数++;
        switch (_parse)
        {
            case 6.1 when _p6.宇宙天箭判定次数 == 7:
                _p6.宇宙龙炎绘图开启.Set();
                if (!SpecialMode) return;
                ResetSupportUnitVisibility(sa);
                break;
            case 6.2 when _p6.宇宙天箭判定次数 == 5:
                _p6.波动炮八方绘图开启.Set();
                if (!SpecialMode) return;
                ResetSupportUnitVisibility(sa);
                break;
        }
    }
    
    [ScriptMethod(name: "P6A_宇宙天箭_绘图", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31650"],
        userControl: true)]
    public void P6A_宇宙天箭_绘图(Event ev, ScriptAccessory sa)
    {
        if (_parse < 6) return;
        sa.DebugMsg($"P6A_宇宙天箭_绘图：启动宇宙天箭绘图Framework", Debugging);
        _p6.宇宙天箭绘图Framework = sa.Method.RegistFrameworkUpdateAction(Action);
        return;

        void Action()
        {
            int[] 内天箭图案 = [0, 12, 60, 195, 3, 12, 48, 192];
            int[] 外天箭图案 = [0, 12, 15, 51, 204, 48, 192];
            if (!_p6.宇宙天箭类型判断完毕) return;
            var cosmoArrowPattern = _p6.宇宙天箭是内天箭 ? 内天箭图案 : 外天箭图案;
            if (_p6.宇宙天箭绘图图案序号 == _p6.宇宙天箭判定次数) return;
            _p6.宇宙天箭绘图图案序号 = _p6.宇宙天箭判定次数;
            
            var pattern = _p6.宇宙天箭绘图图案序号 >= cosmoArrowPattern.Length ? 0 : cosmoArrowPattern[_p6.宇宙天箭绘图图案序号];
            DrawCosmoArrowPattern(pattern);
            
            sa.DebugMsg($"P6A_宇宙天箭_绘图：绘制宇宙天箭绘图图案序号 {_p6.宇宙天箭绘图图案序号} ({pattern})", Debugging);
            return;
            
            void DrawCosmoArrowPattern(int pt)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (((pt >> i) & 1) == 1)
                    {
                        // 计算中心位置
                        float biasZ = (2.5f + (i / 2) * 5) * (i % 2 == 0 ? -1 : 1);
                        sa.DrawRect(new Vector3(80f, 0, 100f + biasZ), 0, 20000,
                            $"P6A_宇宙天箭_绘图_{i}", 90f.DegToRad(), 5f, 60f);
                        sa.DrawRect(new Vector3(100f + biasZ, 0, 80f), 0, 20000,
                            $"P6A_宇宙天箭_绘图_{i}", 0, 5f, 60f);
                    }
                    else
                        sa.Method.RemoveDraw($"P6A_宇宙天箭_绘图_{i}");
                }
            }
        }
    }
    
    [ScriptMethod(name: "P6A_宇宙天箭_指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31650"],
        userControl: true)]
    public void P6A_宇宙天箭_指路(Event ev, ScriptAccessory sa)
    {
        if (_parse < 6) return;
        sa.DebugMsg($"P6A_宇宙天箭_指路：启动宇宙天箭指路Framework", Debugging);
        _p6.宇宙天箭指路Framework = sa.Method.RegistFrameworkUpdateAction(Action);
        return;

        void Action()
        {
            // 以右下为基准
            int[][] 天箭指路图案 =
            [
                [33, 11, 11, 55, 55, 55, 33],   // 内天箭指路图案DPS
                [33, 55, 55, 33, 55, 33, 33],   // 外天箭指路图案DPS
                [33, 11, 11, 33, 53, 88, 38],   // 内天箭指路图案TN
                [33, 55, 55, 33, 88, 38, 38]    // 外天箭指路图案TN
            ];
            
            if (!_p6.宇宙天箭类型判断完毕) return;
            int cosmoArrowGuidanceIdx, myRotation;
            int myIndex = sa.GetMyIndex();
            bool isFirstCosmoArrow = _parse == 6.1;
            if (isFirstCosmoArrow)
            {
                (cosmoArrowGuidanceIdx, myRotation) = myIndex switch
                {
                    0 => (2, 2),
                    1 => (0, 0),
                    _ => (0, 3)
                };
            }
            else
            {
                (cosmoArrowGuidanceIdx, myRotation) = myIndex switch
                {
                    0 => (2, 2),     // MT左上正上
                    1 => (2, 1),     // ST右上正右
                    2 => (2, 3),     // H1左下正左
                    3 => (2, 0),     // H2右下正下
                    4 => (0, 3),     // D1左下
                    5 => (0, 0),     // D2右下
                    6 => (0, 2),     // D3左上
                    7 => (0, 1)      // D4右上
                };
            }
            cosmoArrowGuidanceIdx += _p6.宇宙天箭是内天箭 ? 0 : 1;
            var cosmoArrowGuidance = 天箭指路图案[cosmoArrowGuidanceIdx];
            if (_p6.宇宙天箭指路图案序号 == _p6.宇宙天箭判定次数) return;
            _p6.宇宙天箭指路图案序号 = _p6.宇宙天箭判定次数;
            
            var guidance = _p6.宇宙天箭指路图案序号 >= cosmoArrowGuidance.Length ? 0 : cosmoArrowGuidance[_p6.宇宙天箭指路图案序号];
            var nextGuidance = _p6.宇宙天箭指路图案序号 + 1 >= cosmoArrowGuidance.Length ? 0 : cosmoArrowGuidance[_p6.宇宙天箭指路图案序号 + 1];
            DrawCosmoArrowGuidance(guidance, nextGuidance, myRotation);
            sa.Method.RemoveDraw($"P6A_宇宙天箭_指路{_p6.宇宙天箭指路图案序号 - 1}");
            sa.DebugMsg($"P6A_宇宙天箭_指路：绘制宇宙天箭指路图案序号 {_p6.宇宙天箭指路图案序号} {guidance} {myRotation} {nextGuidance}", Debugging);
            return;
            
            void DrawCosmoArrowGuidance(int gd, int nextGd, int rot)
            {
                if (gd == 0) return;
                var guidancePos = GetGuidancePos(gd, rot);
                if (nextGd != 0)
                {
                    var nextGuidancePos = GetGuidancePos(nextGd, rot);
                    sa.DrawGuidance(guidancePos, nextGuidancePos, 0, 20000, $"P6A_宇宙天箭_指路{_p6.宇宙天箭指路图案序号}", isSafe: false);
                }
                sa.DrawGuidance(guidancePos, 0, 20000, $"P6A_宇宙天箭_指路{_p6.宇宙天箭指路图案序号}", isSafe: true);
            }

            Vector3 GetGuidancePos(int gd, int rot)
            {
                var (digitX, digitZ) = (gd.GetDecimalDigit(1), gd.GetDecimalDigit(2));
                var biasX = digitX == 8 ? 0f : (2.5f + (digitX / 2) * 5) * (digitX % 2 == 0 ? -1 : 1);
                var biasZ = digitZ == 8 ? 13f : (2.5f + (digitZ / 2) * 5) * (digitZ % 2 == 0 ? -1 : 1);
                Vector3 guidancePos = new Vector3(100 + biasX, 0, 100 + biasZ).RotateAndExtend(Center, rot * 90f.DegToRad());
                return guidancePos;
            }
        }
    }

    #endregion 宇宙天箭

    #region P6B 解限波动炮

    [ScriptMethod(name: "———————— 《P6B 解限波动炮》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P6B_解限波动炮_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P6B_解限波动炮_分P", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31660"],
        userControl: Debugging)]
    public void P6B_解限波动炮_分P(Event ev, ScriptAccessory sa)
    {
        if (_parse < 6) return;
        sa.DebugMsg($"当前阶段为：{_parse} 解限波动炮", Debugging);
        _p6.ResetAutoAttack(sa, false);
    }
    
    [ScriptMethod(name: "P6B_解限波动炮_指路场中", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31660"],
        userControl: true)]
    public void P6B_解限波动炮_指路场中(Event ev, ScriptAccessory sa)
    {
        if (_parse < 6) return;
        sa.DrawGuidance(Center, 0, 10000, $"P6A_解限波动炮_指路场中");
    }
    
    [ScriptMethod(name: "P6B_解限波动炮_计算跑动方向", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31661"],
        userControl: Debugging)]
    public void P6B_解限波动炮_计算跑动方向(Event ev, ScriptAccessory sa)
    {
        if (_parse < 6) return;
        var region = ev.SourcePosition.GetRadian(Center).RadianToRegion(8, isDiagDiv: true);
        if (_p6.解限波动炮第一炮方位 == -1)
        {
            _p6.解限波动炮第一炮方位 = region;
        }
        else if (!_p6.解限波动炮方向判断完毕)
        {
            var diff = _p6.解限波动炮第一炮方位 - region;
            _p6.解限波动炮是顺时针 = diff is 1 or -7;
            _p6.解限波动炮方向判断完毕 = true;
            _p6.解限波动炮跑动方向绘图.Set();
        }
    }
    
    [ScriptMethod(name: "P6B_解限波动炮_跑动方向绘图", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31660"],
        userControl: true)]
    public void P6B_解限波动炮_跑动方向绘图(Event ev, ScriptAccessory sa)
    {
        if (_parse < 6) return;
        _p6.解限波动炮跑动方向绘图.WaitOne();
        DrawCannonRoute(false);
        _p6.脚下出现黄圈.WaitOne();
        DrawCannonRoute(true);
        if (_parse == 6.1)  // 跟八方波动炮
        {
            _p6.波动炮八方绘图开启.WaitOne();
            sa.Method.RemoveDraw($"P6B_解限波动炮_跑动方向绘图.*");
            
            List<int> partySpreadRegion = [4, 2, 6, 0, 7, 1, 5, 3];
            var pos = new Vector3(100, 0, 114f).RotateAndExtend(Center, partySpreadRegion[sa.GetMyIndex()] * 45f.DegToRad());
            sa.DrawGuidance(pos, 0, 20000, $"P6_波动炮_八方指路");
            for (int i = 0; i < 8; i++)
            {
                var dp = sa.DrawLine(Center, 0, 0, 20000, $"P6_波动炮_指引线{i}", partySpreadRegion[i] * 45f.DegToRad(), 40f, 20f, draw: false);
                dp.Color = i switch
                {
                    0 or 1 => new Vector4(0.1f, 0.1f, 1, 1),
                    2 or 3 => new Vector4(0.1f, 1f, 0.1f, 1),
                    _ => new Vector4(1, 0.1f, 0.1f, 1),
                };
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Line, dp);
            }
        }
        
        void DrawCannonRoute(bool isSafe)
        {
            var isCw = _p6.解限波动炮是顺时针;
            var startRegion = (_p6.解限波动炮第一炮方位 + (isCw ? 1 : -1) + 8) % 8;
            var startDonutRad = (_p6.解限波动炮第一炮方位 * 45f).DegToRad();
            var startLineRad = (startRegion * 45f).DegToRad() + (isCw ? 22.5f : -22.5f).DegToRad();
            
            sa.DebugMsg($"开始画波动炮路径，isCW {isCw}，startRegion {startRegion}，startDonutRad {startDonutRad.RadToDeg()}，startLineRad {startLineRad.RadToDeg()}", Debugging);
            
            var dp1 = sa.DrawRect(Center, 0, 0, 20000, $"P6B_解限波动炮_跑动方向绘图_直线_{isSafe}",
                startLineRad, 2f, 15f, isSafe, draw: false);
            var dp2 = sa.DrawFan(Center, 0, 0, 20000, $"P6B_解限波动炮_跑动方向绘图_路径_{isSafe}",
                135f.DegToRad(), startDonutRad, 15f, 13f, isSafe, draw: false);
            dp1.Color = dp1.Color.WithW(3f);
            dp2.Color = dp2.Color.WithW(3f);
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp1);
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp2);
            
            sa.Method.RemoveDraw($"P6B_解限波动炮_跑动方向绘图_路径_{!isSafe}");
            sa.Method.RemoveDraw($"P6B_解限波动炮_跑动方向绘图_直线_{!isSafe}");
        }
    }

    [ScriptMethod(name: "P6B_解限波动炮_脚底黄圈", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31663"],
        userControl: Debugging, suppress: 500)]
    public void P6B_解限波动炮_脚底黄圈(Event ev, ScriptAccessory sa)
    {
        if (_parse < 6) return;
        _p6.玩家脚下黄圈轮数++;
        switch (_p6.玩家脚下黄圈轮数)
        {
            case 1:
                _p6.脚下出现黄圈.Set();
                sa.Method.RemoveDraw($"P6A_解限波动炮_指路场中");
                break;
            case 4:
                _p6.脚下出现黄圈.Reset();
                _p6.解限波动炮跑动方向绘图.Reset();
                break;
        }
        
        switch (_parse)
        {
            case 6.1 when _p6.玩家脚下黄圈轮数 == 5:
                _p6.波动炮八方绘图开启.Set();
                break;
            case 6.2 when _p6.玩家脚下黄圈轮数 == 6:
                _p6.宇宙龙炎绘图开启.Set();
                break;
        }
    }

    #endregion 解限波动炮

    #region P6C 宇宙流星

    [ScriptMethod(name: "———————— 《P6C 宇宙流星》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P6C_宇宙流星_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P6C_宇宙流星_分P", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31664"],
        userControl: Debugging)]
    public void P6C_宇宙流星_分P(Event ev, ScriptAccessory sa)
    {
        _parse = 6.3;
        _p6.ResetAutoAttack(sa, true);
        sa.DebugMsg($"当前阶段为：{_parse}", Debugging);
    }
    
    [ScriptMethod(name: "P6C_宇宙流星_指路场中与后续八方", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31664"],
        userControl: true)]
    public void P6C_宇宙流星_指路场中与后续八方(Event ev, ScriptAccessory sa)
    {
        if (_parse < 6) return;
        sa.DrawGuidance(Center, 0, 10000, $"P6C_宇宙流星_指路场中与后续八方");
        List<int> partySpreadRegion = [5, 2, 6, 0, 7, 1, 4, 3];
        var pos = new Vector3(100, 0, 114f).RotateAndExtend(Center, partySpreadRegion[sa.GetMyIndex()] * 45f.DegToRad());
        sa.DrawGuidance(Center, pos, 0, 20000, $"P6C_宇宙流星_指路场中与后续八方", isSafe: false);
    }

    [ScriptMethod(name: "P6C_宇宙流星_删除场中指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31666"],
        userControl: Debugging, suppress: 500)]
    public void P6C_宇宙流星_删除场中指路(Event ev, ScriptAccessory sa)
    {
        if (_parse < 6) return;
        sa.Method.RemoveDraw($"P6C_宇宙流星_指路场中与后续八方");
    }

    [ScriptMethod(name: "P6C_宇宙流星_八方指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31666"],
        userControl: true, suppress: 500)]
    public void P6C_宇宙流星_八方指路(Event ev, ScriptAccessory sa)
    {
        if (_parse < 6) return;
        List<int> partySpreadRegion = [5, 2, 6, 0, 7, 1, 4, 3];
        var pos = new Vector3(100, 0, 114f).RotateAndExtend(Center, partySpreadRegion[sa.GetMyIndex()] * 45f.DegToRad());
        sa.DrawGuidance(pos, 0, 20000, $"P6C_宇宙流星_八方指路", isSafe: true);
        for (int i = 0; i < 8; i++)
        {
            var dp = sa.DrawLine(Center, 0, 0, 20000, $"P6C_宇宙流星_指引线{i}", partySpreadRegion[i] * 45f.DegToRad(), 40f, 20f, draw: false);
            dp.Color = i switch
            {
                0 or 1 => new Vector4(0.1f, 0.1f, 1, 1),
                2 or 3 => new Vector4(0.1f, 1f, 0.1f, 1),
                _ => new Vector4(1, 0.1f, 0.1f, 1),
            };
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Line, dp);
        }
    }
    
    [ScriptMethod(name: "P6C_宇宙流星_删除八方指路与指引线", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:32699"],
        userControl: Debugging, suppress: 500)]
    public void P6C_宇宙流星_删除八方指路与指引线(Event ev, ScriptAccessory sa)
    {
        if (_parse < 6) return;
        sa.Method.RemoveDraw($"P6C_宇宙流星_八方指路");
        sa.Method.RemoveDraw($"P6C_宇宙流星_指引线.*");
    }
    
    [ScriptMethod(name: "P6C_宇宙流星_陨石目标收集", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:015A"],
        userControl: Debugging)]
    public void P6C_宇宙流星_陨石目标收集(Event ev, ScriptAccessory sa)
    {
        if (_parse < 6) return;
        lock (_p6.陨石目标)
        {
            _p6.陨石目标.Add(sa.GetPlayerIdIndex((uint)ev.TargetId));
            if (_p6.陨石目标.Count < 3) return;
            _p6.陨石目标收集完毕.Set();
        }
    }

    [ScriptMethod(name: "P6C_宇宙流星_陨石目标连线", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:015A"],
        userControl: Debugging, suppress: 500)]
    public void P6C_宇宙流星_陨石目标连线(Event ev, ScriptAccessory sa)
    {
        if (_parse < 6) return;
        _p6.陨石目标收集完毕.WaitOne();
        
        var dp1 = sa.DrawGuidance(sa.Data.PartyList[_p6.陨石目标[0]], sa.Data.PartyList[_p6.陨石目标[1]],
            0, 10000, $"P6C_宇宙流星_陨石目标连线与指路01", draw: false);
        var dp2 = sa.DrawGuidance(sa.Data.PartyList[_p6.陨石目标[1]], sa.Data.PartyList[_p6.陨石目标[2]],
            0, 10000, $"P6C_宇宙流星_陨石目标连线与指路12", draw: false);
        var dp3 = sa.DrawGuidance(sa.Data.PartyList[_p6.陨石目标[2]], sa.Data.PartyList[_p6.陨石目标[0]],
            0, 10000, $"P6C_宇宙流星_陨石目标连线与指路20", draw: false);
        dp1.Color = new Vector4(0f, 0f, 0f, 3f);
        dp2.Color = new Vector4(0f, 0f, 0f, 3f);
        dp3.Color = new Vector4(0f, 0f, 0f, 3f);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp1);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp2);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp3);
        dp1.Color = new Vector4(1f, 1f, 0f, 1f);
        dp2.Color = new Vector4(1f, 1f, 0f, 1f);
        dp3.Color = new Vector4(1f, 1f, 0f, 1f);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp1);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp2);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, dp3);
    }

    [ScriptMethod(name: "P6C_宇宙流星_陨石指路", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:015A"],
        userControl: Debugging, suppress: 500)]
    public void P6C_宇宙流星_陨石指路(Event ev, ScriptAccessory sa)
    {
        if (_parse < 6) return;
        _p6.陨石目标收集完毕.WaitOne();

        // 人群安全区
        var crowdSafeRegion = _p6.陨石目标.Contains(6) ? 0 : 2;
        
        if (_p6.陨石目标.Contains(sa.GetMyIndex()))
        {
            // 陨石目标包含自身，则画危险区与指引线

            sa.DrawFan(Center, 0, 20000, $"P6C_宇宙流星_人群安全区示意",
                150f.DegToRad(), crowdSafeRegion * 90f.DegToRad(), 30f, 0f);
            
            for (int i = 0; i < 3; i++)
            {
                sa.DrawLine(Center, 0, 0, 20000, $"P6C_宇宙流星_陨石指引线{i}",
                    (i - 1) * 90f.DegToRad(), 40f, 20f, isSafe: true);
            }
        }
        else
        {
            sa.DrawGuidance(new Vector3(100, 0, 114.5f).RotateAndExtend(Center, crowdSafeRegion * 90f.DegToRad()), 0,
                20000, $"P6C_宇宙流星_陨石指路");
        }
    }

    [ScriptMethod(name: "P6C_宇宙流星_删除宇宙流星绘图", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31668"],
        userControl: Debugging, suppress: 500)]
    public void P6C_宇宙流星_删除宇宙流星绘图(Event ev, ScriptAccessory sa)
    {
        if (_parse < 6) return;
        _p6.陨石目标收集完毕.Reset();
        _p6.Reset(sa);
        _p6.Dispose();
        sa.Method.RemoveDraw(".*");
    }
    
    [ScriptMethod(name: "P6C_宇宙流星_机制结束后", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31668"],
        userControl: true, suppress: 500)]
    public void P6C_宇宙流星_机制结束后(Event ev, ScriptAccessory sa)
    {
        if (_parse < 6) return;
        // 如果全员存活
        foreach (var member in sa.Data.PartyList)
        {
            var obj = sa.GetById(member);
            if (obj == null) return;
            if (obj.IsDead) return;
        }
        sa.Method.TextInfo($"超越极限，迎接胜利吧！", 2000);
    }

    #endregion 宇宙流星
    
    #region 优先级字典 类
    public class PriorityDict
    {
        // ReSharper disable once NullableWarningSuppressionIsUsed
        public ScriptAccessory sa {get; set;} = null!;
        // ReSharper disable once NullableWarningSuppressionIsUsed
        public Dictionary<int, int> Priorities {get; set;} = null!;
        public string Annotation { get; set; } = "";
        public int ActionCount { get; set; } = 0;
        
        public void Init(ScriptAccessory accessory, string annotation, int partyNum = 8, bool refreshActionCount = true)
        {
            sa = accessory;
            Priorities = new Dictionary<int, int>();
            for (var i = 0; i < partyNum; i++)
            {
                Priorities.Add(i, 0);
            }
            Annotation = annotation;
            if (refreshActionCount)
                ActionCount = 0;
        }

        /// <summary>
        /// 为特定Key增加优先级
        /// </summary>
        /// <param name="idx">key</param>
        /// <param name="priority">优先级数值</param>
        public void AddPriority(int idx, int priority)
        {
            Priorities[idx] += priority;
        }
        
        /// <summary>
        /// 从Priorities中找到前num个数值最小的，得到新的Dict返回
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        public List<KeyValuePair<int, int>> SelectSmallPriorityIndices(int num)
        {
            return SelectMiddlePriorityIndices(0, num);
        }

        /// <summary>
        /// 从Priorities中找到前num个数值最大的，得到新的Dict返回
        /// </summary>
        /// <param name="num"></param>
        /// <returns></returns>
        public List<KeyValuePair<int, int>> SelectLargePriorityIndices(int num)
        {
            return SelectMiddlePriorityIndices(0, num, true);
        }
        
        /// <summary>
        /// 从Priorities中找到升序排列中间的数值，得到新的Dict返回
        /// </summary>
        /// <param name="skip">跳过skip个元素。若从第二个开始取，skip=1</param>
        /// <param name="num"></param>
        /// <param name="descending">降序排列，默认为false</param>
        /// <returns></returns>
        public List<KeyValuePair<int, int>> SelectMiddlePriorityIndices(int skip, int num, bool descending = false)
        {
            if (Priorities.Count < skip + num)
                return new List<KeyValuePair<int, int>>();

            IEnumerable<KeyValuePair<int, int>> sortedPriorities;
            if (descending)
            {
                // 根据值从大到小降序排序，并取前num个键
                sortedPriorities = Priorities
                    .OrderByDescending(pair => pair.Value) // 先根据值排列
                    .ThenBy(pair => pair.Key) // 再根据键排列
                    .Skip(skip) // 跳过前skip个元素
                    .Take(num); // 取前num个键值对
            }
            else
            {
                // 根据值从小到大升序排序，并取前num个键
                sortedPriorities = Priorities
                    .OrderBy(pair => pair.Value) // 先根据值排列
                    .ThenBy(pair => pair.Key) // 再根据键排列
                    .Skip(skip) // 跳过前skip个元素
                    .Take(num); // 取前num个键值对
            }
            
            return sortedPriorities.ToList();
        }
        
        /// <summary>
        /// 从Priorities中找到升序排列第idx位的数据，得到新的Dict返回
        /// </summary>
        /// <param name="idx"></param>
        /// <param name="descending">降序排列，默认为false</param>
        /// <returns></returns>
        public KeyValuePair<int, int> SelectSpecificPriorityIndex(int idx, bool descending = false)
        {
            var sortedPriorities = SelectMiddlePriorityIndices(0, Priorities.Count, descending);
            return sortedPriorities[idx];
        }

        /// <summary>
        /// 从Priorities中找到对应key的数据，得到其Value排序后位置返回
        /// </summary>
        /// <param name="key"></param>
        /// <param name="descending">降序排列，默认为false</param>
        /// <returns></returns>
        public int FindPriorityIndexOfKey(int key, bool descending = false)
        {
            var sortedPriorities = SelectMiddlePriorityIndices(0, Priorities.Count, descending);
            var i = 0;
            foreach (var dict in sortedPriorities)
            {
                if (dict.Key == key) return i;
                i++;
            }

            return i;
        }
        
        /// <summary>
        /// 一次性增加优先级数值
        /// 通常适用于特殊优先级（如H-T-D-H）
        /// </summary>
        /// <param name="priorities"></param>
        public void AddPriorities(List<int> priorities)
        {
            if (Priorities.Count != priorities.Count)
                throw new ArgumentException("输入的列表与内部设置长度不同");

            for (var i = 0; i < Priorities.Count; i++)
                AddPriority(i, priorities[i]);
        }

        /// <summary>
        /// 输出优先级字典的Key与优先级
        /// </summary>
        /// <returns></returns>
        public string ShowPriorities(bool showJob = true)
        {
            var str = $"{Annotation} ({ActionCount}-th) 优先级字典：\n";
            if (Priorities.Count == 0)
            {
                str += $"PriorityDict Empty.\n";
                return str;
            }
            foreach (var pair in Priorities)
            {
                str += $"Key {pair.Key} {(showJob ? $"({Role[pair.Key]})" : "")}, Value {pair.Value}\n";
            }

            return str;
        }

        public PriorityDict DeepCopy()
        {
            return JsonConvert.DeserializeObject<PriorityDict>(JsonConvert.SerializeObject(this)) ?? new PriorityDict();
        }

        public void AddActionCount(int count = 1)
        {
            ActionCount += count;
        }

    }

    #endregion 优先级字典 类

    #region 参数容器类
    
    private class P1StateParams
    {
        public ulong BossId = 0;

        public Dictionary<int, Vector3> 塔字典 = new();
        public Dictionary<int, bool> 接线绘图字典 = new();
        public int 线塔轮次 = 0;
        public DateTime 塔触发时间 = DateTime.MinValue;
        public ManualResetEvent 每轮塔收集完毕 = new ManualResetEvent(false);
        public ManualResetEvent 前一轮绘图清除完毕 = new ManualResetEvent(false);
        public string 扫描接线Framework = "";
        public string 闲人指路Framework = "";
        public int 上一次靠近状态 = 0;
        public bool 扫描接线开启 = false;
        
        public int 全能之主轮次 = 0;
        public bool 全能之主为顺时针 = false;
        public bool 全能之主起跑线绘制完毕 = false;
        public bool 全能之主顺逆时针判断完毕 = false;
        public float 全能之主第一次角度寄存 = -10;
        public ManualResetEvent 全能之主顺逆时针判断完毕事件 = new ManualResetEvent(false);
        public int 上一次远距离状态 = 0;
        public string 全能之主出去时间Framework = "";
        public string 全能之主最远距离Framework = "";
        public int 扩散波动炮次数 = 0;
        
        public void Reset(ScriptAccessory sa)
        {
            BossId = 0;
            塔字典 = new Dictionary<int, Vector3>();
            接线绘图字典 = new Dictionary<int, bool>();
            线塔轮次 = 0;
            塔触发时间 = DateTime.MinValue;
            
            上一次靠近状态 = 0;
            扫描接线开启 = false;
            全能之主轮次 = 0;
            全能之主为顺时针 = false;
            全能之主起跑线绘制完毕 = false;
            全能之主顺逆时针判断完毕 = false;
            全能之主第一次角度寄存 = -10;
            
            上一次远距离状态 = 0;
            sa.Method.UnregistFrameworkUpdateAction(闲人指路Framework);
            sa.Method.UnregistFrameworkUpdateAction(扫描接线Framework);
            sa.Method.UnregistFrameworkUpdateAction(全能之主出去时间Framework);
            sa.Method.UnregistFrameworkUpdateAction(全能之主最远距离Framework);
            
            闲人指路Framework = "";
            扫描接线Framework = "";
            全能之主出去时间Framework = "";
            全能之主最远距离Framework = "";
            
            sa.DebugMsg($"P1参数被reset", Debugging);
        }

        public void Dispose()
        {
            每轮塔收集完毕.Dispose();
            前一轮绘图清除完毕.Dispose();
            全能之主顺逆时针判断完毕事件.Dispose();
        }

        public void Register()
        {
            每轮塔收集完毕 = new ManualResetEvent(false);
            前一轮绘图清除完毕 = new ManualResetEvent(false);
            全能之主顺逆时针判断完毕事件 = new ManualResetEvent(false);
            每轮塔收集完毕.Reset();
            前一轮绘图清除完毕.Reset();
            全能之主顺逆时针判断完毕事件.Reset();
        }
    }
    
    private class P2StateParams
    {
        public ulong BossIdMale = 0;
        public ulong BossIdFemale = 0;
        
        public int 上一次防火墙状态 = 0;
        public bool 协作程序是远线 = false;
        public bool 使能防火墙 = false;
        public bool 转场手臂先正三角 = false;
        public int 大眼睛方位 = 0;
        public int 男人钢铁方位 = 0;

        public ManualResetEvent 眼睛激光方位记录 = new(false);
        public ManualResetEvent 分摊记录 = new(false);
        public ManualResetEvent 男人钢铁方位记录 = new(false);
        public ManualResetEvent 女人击退记录 = new(false);
        public ManualResetEvent 男人分摊钢铁记录 = new(false);
        public ManualResetEvent 眼睛激光准备绘图 = new(false);
        public ManualResetEvent 转场头标记录 = new(false);
        
        public string 判断防火墙目标Framework = "";
        
        public void Reset(ScriptAccessory sa)
        {
            BossIdMale = 0;
            BossIdFemale = 0;
            
            上一次防火墙状态 = 0;
            协作程序是远线 = false;
            使能防火墙 = false;
            转场手臂先正三角 = false;
            大眼睛方位 = 0;
            男人钢铁方位 = 0;
            
            sa.Method.UnregistFrameworkUpdateAction(判断防火墙目标Framework);
            判断防火墙目标Framework = "";
        }

        public void Dispose()
        {
            眼睛激光方位记录.Dispose();
            分摊记录.Dispose();
            男人钢铁方位记录.Dispose();
            女人击退记录.Dispose();
            男人分摊钢铁记录.Dispose();
            眼睛激光准备绘图.Dispose();
            转场头标记录.Dispose();
        }
        
        public void Register()
        {
            眼睛激光方位记录 = new ManualResetEvent(false);
            分摊记录 = new ManualResetEvent(false);
            男人钢铁方位记录 = new ManualResetEvent(false);
            女人击退记录 = new ManualResetEvent(false);
            男人分摊钢铁记录 = new ManualResetEvent(false);
            眼睛激光准备绘图 = new ManualResetEvent(false);
            转场头标记录 = new ManualResetEvent(false);
            眼睛激光方位记录.Reset();
            分摊记录.Reset();
            男人钢铁方位记录.Reset();
            女人击退记录.Reset();
            男人分摊钢铁记录.Reset();
            眼睛激光准备绘图.Reset();
            转场头标记录.Reset();
        }
    }
    
    private class P3StateParams
    {
        public ulong BossId = 0;
        public int 你好世界轮数 = 0;

        public int 红塔位置 = 0;
        public int 蓝塔位置 = 0;
        public bool 大圈是红塔 = false;
        public bool 光头小电视方向打右 = false;
        public int 小电视玩家面向 = 0;
        
        public ManualResetEvent 红蓝塔方位记录 = new(false);
        public ManualResetEvent 你好世界轮数记录 = new(false);
        public ManualResetEvent 小电视头标记录 = new(false);
        public ManualResetEvent 光头扫描方向记录 = new(false);
        public ManualResetEvent 小电视玩家面向记录 = new(false);
        
        public string 小电视面向辅助Framework = "";
        public DateTime 小电视面向辅助触发时间 = DateTime.MinValue;
        
        public void Reset(ScriptAccessory sa)
        {
            BossId = 0;
            你好世界轮数 = 0;
            红塔位置 = 0;
            蓝塔位置 = 0;
            大圈是红塔 = false;
            光头小电视方向打右 = false;

            小电视玩家面向 = 0;
            小电视面向辅助触发时间 = DateTime.MinValue;
            sa.Method.UnregistFrameworkUpdateAction(小电视面向辅助Framework);
            小电视面向辅助Framework = "";
        }

        public void Dispose()
        {
            红蓝塔方位记录.Dispose();
            你好世界轮数记录.Dispose();
            小电视头标记录.Dispose();
            光头扫描方向记录.Dispose();
            小电视玩家面向记录.Dispose();
        }
        
        public void Register()
        {
            红蓝塔方位记录 = new ManualResetEvent(false);
            你好世界轮数记录 = new ManualResetEvent(false);
            小电视头标记录 = new ManualResetEvent(false);
            光头扫描方向记录 = new ManualResetEvent(false);
            小电视玩家面向记录 = new ManualResetEvent(false);
            红蓝塔方位记录.Reset();
            你好世界轮数记录.Reset();
            小电视头标记录.Reset();
            光头扫描方向记录.Reset();
            小电视玩家面向记录.Reset();
        }
    }
    
    private class P4StateParams
    {
        public ulong BossId = 0;
        public int 蓝屏波动炮轮数 = 0;
        public ManualResetEvent 波动炮初始化记录 = new(false);
        public void Reset(ScriptAccessory sa)
        {
            BossId = 0;
            蓝屏波动炮轮数 = 0;
        }

        public void Dispose()
        {
            波动炮初始化记录.Dispose();
        }
        
        public void Register()
        {
            波动炮初始化记录 = new ManualResetEvent(false);
        }
    }
    
    private class P5AStateParams
    {
        public int 蟑螂位置 = 0;
        public int 光头位置 = 0;
        public int 玩家四分之一半场 = -1;
        public int 拳头数量 = 0;
        public int 拳头颜色 = 0;
        public int 玩家引导激光手方位 = 0;
        public int 光头左右扫描 = 0;
        public int 玩家左右扫描 = 0;
        public int 蟑螂左右刀 = 0;

        public Dictionary<int, Vector3> 激光手方向字典 = new();
    
        public bool 开启玩家引导激光手指路 = false;
        public bool 第一根远线拉断 = false;

        public ManualResetEvent 远线搭档记录 = new(false);
        public ManualResetEvent 头标记录 = new(false);
        public ManualResetEvent 光头蟑螂定位 = new(false);
        public ManualResetEvent 拳头记录 = new(false);
        public ManualResetEvent 盾连击记录 = new(false);
        public ManualResetEvent 蟑螂左右刀记录 = new(false);

        public string 小电视面向辅助Framework = "";
        public DateTime 小电视面向辅助触发时间 = DateTime.MinValue;

        // 一键重置方法
        public void Reset(ScriptAccessory sa)
        {
            蟑螂位置 = 0;
            光头位置 = 0;
            玩家四分之一半场 = -1;
            拳头数量 = 0;
            拳头颜色 = 0;
            玩家引导激光手方位 = 0;
            光头左右扫描 = 0;
            玩家左右扫描 = 0;
            蟑螂左右刀 = 0;
            
            开启玩家引导激光手指路 = false;
            第一根远线拉断 = false;

            激光手方向字典 = new Dictionary<int, Vector3>();
            小电视面向辅助触发时间 = DateTime.MinValue;
            
            sa.Method.UnregistFrameworkUpdateAction(小电视面向辅助Framework);
            小电视面向辅助Framework = "";
        }
        
        public void Dispose()
        {
            远线搭档记录.Dispose();
            头标记录.Dispose();
            光头蟑螂定位.Dispose();
            拳头记录.Dispose();
            盾连击记录.Dispose();
            蟑螂左右刀记录.Dispose();
        }
        
        public void Register()
        {
            远线搭档记录 = new ManualResetEvent(false);
            头标记录 = new ManualResetEvent(false);
            光头蟑螂定位 = new ManualResetEvent(false);
            拳头记录 = new ManualResetEvent(false);
            盾连击记录 = new ManualResetEvent(false);
            蟑螂左右刀记录 = new ManualResetEvent(false);
            远线搭档记录.Reset();
            头标记录.Reset();
            光头蟑螂定位.Reset();
            拳头记录.Reset();
            盾连击记录.Reset();
            蟑螂左右刀记录.Reset();
        }
    }

    private class P5BStateParams
    {
        public bool 协作程序是远线 = false;
        public int 男人方位 = 0;
        public int 玩家八方方位 = 0;
        public int 玩家踩塔方位 = 0;
        public Dictionary<int, bool> 塔方位类型字典 = new();    // 方位、是否是双人塔
        public ManualResetEvent 八方头标记录完毕 = new(false);
        public ManualResetEvent 塔方位记录完毕 = new(false);
        public ManualResetEvent 踩塔处理完毕 = new(false);
        
        public int 女人方位 = 0;
        public bool 女人是十字外安全 = false;
        public bool 圆环是顺时针 = false;
        public ulong 圆环Id = 0;
        public ulong 二传女人Id = 0;
        public int 圆环攻击次数 = 0;
        public ManualResetEvent 转圈头标记录完毕 = new(false);
        public ManualResetEvent 圆环方向记录完毕 = new(false);
        public ManualResetEvent 女人技能记录完毕 = new(false);
        public ManualResetEvent 女人十字辣翅释放完毕 = new(false);
        
        public void Reset(ScriptAccessory sa)
        {
            协作程序是远线 = false;
            男人方位 = 0;
            玩家八方方位 = 0;
            玩家踩塔方位 = 0;
            塔方位类型字典 = new Dictionary<int, bool>();    // 方位、是否是双人塔
            
            女人方位 = 0;
            女人是十字外安全 = false;
            圆环是顺时针 = false;
            圆环Id = 0;
            二传女人Id = 0;
            圆环攻击次数 = 0;
        }

        public void Dispose()
        {
            八方头标记录完毕.Dispose();
            塔方位记录完毕.Dispose();
            踩塔处理完毕.Dispose();
            
            转圈头标记录完毕.Dispose();
            女人十字辣翅释放完毕.Dispose();
            圆环方向记录完毕.Dispose();
            女人技能记录完毕.Dispose();
        }
        
        public void Register()
        {
            八方头标记录完毕 = new ManualResetEvent(false);
            塔方位记录完毕 = new ManualResetEvent(false);
            踩塔处理完毕 = new ManualResetEvent(false);
            
            转圈头标记录完毕 = new ManualResetEvent(false);
            女人十字辣翅释放完毕 = new ManualResetEvent(false);
            圆环方向记录完毕 = new ManualResetEvent(false);
            女人技能记录完毕 = new ManualResetEvent(false);
            八方头标记录完毕.Reset();
            塔方位记录完毕.Reset();
            踩塔处理完毕.Reset();
            转圈头标记录完毕.Reset();
            女人十字辣翅释放完毕.Reset();
            圆环方向记录完毕.Reset();
            女人技能记录完毕.Reset();
        }

        public int FindPlayerTower(ScriptAccessory sa)
        {
            int[] towerRegionOffsets = [2, 1, -2, -1];
            
            int bestTower = -1;
            int highestScore = -1;
            
            foreach (int offset in towerRegionOffsets)
            {
                int region = (玩家八方方位 + offset + 16) % 16;
                if (!塔方位类型字典.TryGetValue(region, out bool isDoubleTower))
                    continue;
                sa.DebugMsg($"检测到 方位{region} 存在 {(isDoubleTower ? "双" : "单")}人塔", Debugging);
                int score = 1 + (isDoubleTower ? 10 : 0);
                if (score <= highestScore) continue;
                highestScore = score;
                bestTower = region;
                if (isDoubleTower) break;
            }
            
            sa.DebugMsg($"玩家所需踩塔：{bestTower}", Debugging);
            return bestTower;
        }
    }
    
    private class P5CStateParams
    {
        public Dictionary<int, (int, bool)> 组合技记录 = new();   // 方位、(技能类型、是不是第一轮)
        public ManualResetEvent 男女人组合技记录完毕 = new(false);
        public ManualResetEvent 组合技安全区记录完毕 = new(false);
        public ManualResetEvent 第一段组合技结束 = new(false);
        public int[] 组合技安全区 = [];
        
        public int 蟑螂方位 = 0;
        public ulong 蟑螂Id = 0;
        public bool 扫描接线开启 = false;
        public ManualResetEvent 三传头标记录完毕 = new(false);
        public ManualResetEvent 四传头标记录完毕 = new(false);
        public ManualResetEvent 蟑螂方位记录完毕 = new(false);
        public Dictionary<int, bool> 接线绘图字典 = new();
        
        public string 扫描接线Framework = "";
        
        public Dictionary<int, List<bool>> 技能类型安全区字典 = new();    // 方位、技能类型
        public void Reset(ScriptAccessory sa)
        {
            蟑螂方位 = 0;
            蟑螂Id = 0;
            扫描接线开启 = false;
            组合技记录 = new Dictionary<int, (int, bool)>();
            组合技安全区 = [];
            接线绘图字典 = new Dictionary<int, bool>();
            
            sa.Method.UnregistFrameworkUpdateAction(扫描接线Framework);
            扫描接线Framework = "";
            
            // 0~3钢铁，4~7月环，8~11十字，12~15辣尾，16光头打左右，17光头打上下
            技能类型安全区字典 = new Dictionary<int, List<bool>>
            {
                { 4, [true, true, false, false, true, true, false, false, false, false, false, false] },
                { 5, [false, true, true, false, false, true, true, false, false, false, false, false] },
                { 6, [false, false, true, true, false, false, true, true, false, false, false, false] },
                { 7, [true, false, false, true, true, false, false, true, false, false, false, false] },
                { 0, [false, false, true, true, false, false, true, true, true, true, true, true] },
                { 1, [true, false, false, true, true, false, false, true, true, true, true, true] },
                { 2, [true, true, false, false, true, true, false, false, true, true, true, true] },
                { 3, [false, true, true, false, false, true, true, false, true, true, true, true] },
                { 8, [false, false, false, false, false, false, true, true, false, false, true, true] },
                { 9, [false, false, false, false, true, false, false, true, true, false, false, true] },
                { 10, [false, false, false, false, true, true, false, false, true, true, false, false] },
                { 11, [false, false, false, false, false, true, true, false, false, true, true, false] },
                { 12, [true, true, true, true, false, false, false, false, false, false, false, false] },
                { 13, [true, true, true, true, false, false, false, false, false, false, false, false] },
                { 14, [true, true, true, true, false, false, false, false, false, false, false, false] },
                { 15, [true, true, true, true, false, false, false, false, false, false, false, false] },
                { 16, [true, false, true, false, true, false, true, false, true, false, true, false] },
                { 17, [false, true, false, true, false, true, false, true, false, true, false, true] }
            };
        }

        public void Dispose()
        {
            男女人组合技记录完毕.Dispose();
            组合技安全区记录完毕.Dispose();
            第一段组合技结束.Dispose();
            三传头标记录完毕.Dispose();
            四传头标记录完毕.Dispose();
            蟑螂方位记录完毕.Dispose();
        }
        
        public void Register()
        {
            男女人组合技记录完毕 = new ManualResetEvent(false);
            组合技安全区记录完毕 = new ManualResetEvent(false);
            第一段组合技结束 = new ManualResetEvent(false);
            三传头标记录完毕 = new ManualResetEvent(false);
            四传头标记录完毕 = new ManualResetEvent(false);
            蟑螂方位记录完毕 = new ManualResetEvent(false);
            男女人组合技记录完毕.Reset();
            组合技安全区记录完毕.Reset();
            第一段组合技结束.Reset();
            三传头标记录完毕.Reset();
            四传头标记录完毕.Reset();
            蟑螂方位记录完毕.Reset();
        }

        public void FindComboAttackSafePoint(ScriptAccessory sa)
        {
            // 使用局部函数统一处理两轮逻辑，彻底消除重复代码
            // return (FindSafePointForRound(isFirstRound: true, sa), FindSafePointForRound(isFirstRound: false, sa));
            组合技安全区 = [FindSafePointForRound(isFirstRound: true, sa), FindSafePointForRound(isFirstRound: false, sa)];
        }

        private int FindSafePointForRound(bool isFirstRound, ScriptAccessory sa)
        {
            var roundSkills = 组合技记录.Where(kvp => kvp.Value.Item2 == isFirstRound).ToList();
            bool[] safePoints = new bool[12]; Array.Fill(safePoints, true);
            foreach (var (region, (skill, _)) in roundSkills)
            {
                sa.DebugMsg($"在方位{region}发现第{(isFirstRound ? "一" : "二")}轮技能{skill}", Debugging);
                var skillSafeZones = 技能类型安全区字典[skill];
                for (int i = 0; i < 12; i++)
                    safePoints[i] &= skillSafeZones[i];
            }
            int safeIndex = Array.IndexOf(safePoints, true);
            sa.DebugMsg(safeIndex == -1
                    ? $"第{(isFirstRound ? "一" : "二")}轮未找到安全点"
                    : $"第{(isFirstRound ? "一" : "二")}轮安全点索引: {safeIndex}", Debugging);

            return safeIndex;
        }
    }
    
    private class P6StateParams
    {
        public ulong BossId = 0;
        public bool 使能普通攻击绘图 = false;
        public string 普通攻击绘图Framework = "";
        public bool 已绘制远离指示线 = false;
        public bool 已提示建立一仇 = false;
        public int 最远玩家Idx = -1;
        public int 一仇玩家Idx = -1;
        
        public bool 宇宙天箭是内天箭 = false;
        public bool 宇宙天箭类型判断完毕 = false;
        public int 宇宙天箭判定次数 = 0;
        public int 宇宙天箭绘图图案序号 = -1;
        public int 宇宙天箭指路图案序号 = -1;
        public string 宇宙天箭绘图Framework = "";
        public string 宇宙天箭指路Framework = "";

        public int 解限波动炮第一炮方位 = -1;
        public bool 解限波动炮是顺时针 = false;
        public int 玩家脚下黄圈轮数 = 0;
        public bool 解限波动炮方向判断完毕 = false;

        public int 波动炮判定次数 = 0;
        
        public List<int> 陨石目标 = [];
        
        public ManualResetEvent 宇宙天箭读条开始 = new(false);
        public ManualResetEvent 宇宙龙炎绘图开启 = new(false);
        public ManualResetEvent 波动炮八方绘图开启 = new(false);
        public ManualResetEvent 解限波动炮跑动方向绘图 = new(false);
        public ManualResetEvent 脚下出现黄圈 = new(false);
        public ManualResetEvent 波动炮分摊绘图开启 = new(false);
        public ManualResetEvent 陨石目标收集完毕 = new(false);
        
        public void Reset(ScriptAccessory sa)
        {
            BossId = 0;
            陨石目标 = [];
            ResetAutoAttack(sa, true);
            ResetCosmoArrow(sa);
            ResetUnlimitedWaveCannon();
            ResetSpreadWaveCannon();
        }

        public void ResetAutoAttack(ScriptAccessory sa, bool unRegist)
        {
            使能普通攻击绘图 = false;
            已绘制远离指示线 = false;
            已提示建立一仇 = false;
            最远玩家Idx = -1;
            一仇玩家Idx = -1;
            sa.Method.RemoveDraw($"P6_普通攻击绘图.*");
            if (!unRegist) return;
            sa.Method.UnregistFrameworkUpdateAction(普通攻击绘图Framework);
        }
        
        public void ResetCosmoArrow(ScriptAccessory sa)
        {
            宇宙天箭是内天箭 = false;
            宇宙天箭类型判断完毕 = false;
            宇宙天箭判定次数 = 0;
            宇宙天箭绘图图案序号 = -1;
            宇宙天箭指路图案序号 = -1;
            sa.Method.UnregistFrameworkUpdateAction(宇宙天箭绘图Framework);
            sa.Method.UnregistFrameworkUpdateAction(宇宙天箭指路Framework);
        }

        public void ResetUnlimitedWaveCannon()
        {
            解限波动炮第一炮方位 = -1;
            解限波动炮是顺时针 = false;
            玩家脚下黄圈轮数 = 0;
            解限波动炮方向判断完毕 = false;
        }

        public void ResetSpreadWaveCannon()
        {
            波动炮判定次数 = 0;
        }

        public void Dispose()
        {
            宇宙天箭读条开始.Dispose();
            宇宙龙炎绘图开启.Dispose();
            波动炮八方绘图开启.Dispose();
            解限波动炮跑动方向绘图.Dispose();
            脚下出现黄圈.Dispose();
            波动炮分摊绘图开启.Dispose();
            陨石目标收集完毕.Dispose();
        }
        
        public void Register()
        {
            宇宙天箭读条开始 = new ManualResetEvent(false);
            宇宙龙炎绘图开启 = new ManualResetEvent(false);
            波动炮八方绘图开启 = new ManualResetEvent(false);
            解限波动炮跑动方向绘图 = new ManualResetEvent(false);
            脚下出现黄圈 = new ManualResetEvent(false);
            波动炮分摊绘图开启 = new ManualResetEvent(false);
            陨石目标收集完毕 = new ManualResetEvent(false);
            宇宙天箭读条开始.Reset();
            宇宙龙炎绘图开启.Reset();
            波动炮八方绘图开启.Reset();
            解限波动炮跑动方向绘图.Reset();
            脚下出现黄圈.Reset();
            波动炮分摊绘图开启.Reset();
            陨石目标收集完毕.Reset();
        }
    }
    #endregion 参数容器类
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

    public static uint Id0(this Event @event)
    {
        return ParseHexId(@event["Id"], out var id) ? id : 0;
    }
    
    public static uint Index(this Event ev)
    {
        return JsonConvert.DeserializeObject<uint>(ev["Index"]);
    }
    
    public static uint DataId(this Event ev)
    {
        return JsonConvert.DeserializeObject<uint>(ev["DataId"]);
    }
}

public static class IbcHelper
{
    public static IGameObject? GetById(this ScriptAccessory sa, ulong gameObjectId)
    {
        return sa.Data.Objects.SearchById(gameObjectId);
    }
    
    public static IEnumerable<IGameObject?> GetByDataId(this ScriptAccessory sa, uint dataId)
    {
        return sa.Data.Objects.Where(x => x.DataId == dataId);
    }

    public static float GetStatusRemainingTime(this ScriptAccessory sa, IBattleChara? battleChara, uint statusId)
    {
        if (battleChara == null || !battleChara.IsValid()) return 0;
        unsafe
        {
            BattleChara* charaStruct = (BattleChara*)battleChara.Address;
            var statusIdx = charaStruct->GetStatusManager()->GetStatusIndex(statusId);
            return charaStruct->GetStatusManager()->GetRemainingTime(statusIdx);
        }
    }
    
    public static List<ulong> GetTetherSource(this ScriptAccessory sa, IBattleChara? battleChara, uint tetherId)
    {
        List<ulong> tetherSourceId = [];
        if (battleChara == null || !battleChara.IsValid()) return [];
        unsafe
        {
            BattleChara* chara = (BattleChara*)battleChara.Address;
            var tetherList = chara->Vfx.Tethers;

            foreach (var tether in tetherList)
            {
                if (tether.Id != tetherId) continue;
                tetherSourceId.Add(tether.TargetId.ObjectId);
            }
        }
        return tetherSourceId;
    }
    
    public static unsafe byte? GetTransformationId(this ScriptAccessory sa, IGameObject? obj)
    {
        if (obj == null) return null;
        Character* objStruct = (Character*)obj.Address;
        return objStruct->Timeline.ModelState;
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
    public static Vector3 RotateAndExtend(this Vector3 point, Vector3 center, float radian, float length = 0)
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
    
    /// <summary>
    /// 获取给定数的指定位数
    /// </summary>
    /// <param name="val">给定数值</param>
    /// <param name="x">对应位数，个位为1</param>
    /// <returns></returns>
    public static int GetDecimalDigit(this int val, int x)
    {
        var valStr = val.ToString();
        var length = valStr.Length;
        if (x < 1 || x > length) return 0;
        var digitChar = valStr[length - x]; // 从右往左取第x位
        return int.Parse(digitChar.ToString());
    }

    /// <summary>
    /// 获得两个弧度（rad到radReference）的差值，逆时针增加大于0
    /// </summary>
    /// <param name="rad">取值角度</param>
    /// <param name="radReference">参考角度</param>
    /// <returns></returns>
    public static float GetDiffRad(this float rad, float radReference)
    {
        var diff = (rad - radReference + 4 * float.Pi) % (2 * float.Pi);
        if (diff > float.Pi) diff -= 2 * float.Pi;
        return diff;
    }
}

#endregion 计算函数

#region 位置序列函数
public static class IndexHelper
{
    /// <summary>
    /// 输入玩家dataId，获得对应的位置index
    /// </summary>
    /// <param name="pid">玩家SourceId</param>
    /// <param name="sa"></param>
    /// <returns>该玩家对应的位置index</returns>
    public static int GetPlayerIdIndex(this ScriptAccessory sa, uint pid)
    {
        // 获得玩家 IDX
        return sa.Data.PartyList.IndexOf(pid);
    }

    /// <summary>
    /// 获得主视角玩家对应的位置index
    /// </summary>
    /// <param name="sa"></param>
    /// <returns>主视角玩家对应的位置index</returns>
    public static int GetMyIndex(this ScriptAccessory sa)
    {
        return sa.Data.PartyList.IndexOf(sa.Data.Me);
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
    /// 赋予输入的dp以ownerId为源的远近目标绘图
    /// </summary>
    /// <param name="self"></param>
    /// <param name="isNearOrder">从owner计算，近顺序或远顺序</param>
    /// <param name="orderIdx">从1开始</param>
    /// <returns></returns>
    public static DrawPropertiesEdit SetOwnersDistanceOrder(this DrawPropertiesEdit self, bool isNearOrder,
        uint orderIdx)
    {
        self.CentreResolvePattern = isNearOrder
            ? PositionResolvePatternEnum.PlayerNearestOrder
            : PositionResolvePatternEnum.PlayerFarestOrder;
        self.CentreOrderIndex = orderIdx;
        return self;
    }
}

#endregion 绘图函数

#region 调试函数

public static class DebugFunction
{
    public static void DebugMsg(this ScriptAccessory sa, string msg, bool enable = true, bool showInChatBox = false)
    {
        if (!enable) return;
        sa.Log.Debug(msg);
        if (!showInChatBox) return;
        sa.Method.SendChat($"/e {msg}");
    }
}

#endregion 调试函数

#region 特殊函数

public static class SpecialFunction
{
    public static void SetTargetable(this ScriptAccessory sa, IGameObject? obj, bool targetable)
    {
        if (obj == null || !obj.IsValid())
        {
            sa.Log.Error($"传入的IGameObject不合法。");
            return;
        }
        unsafe
        {
            GameObject* charaStruct = (GameObject*)obj.Address;
            if (targetable)
            {
                if (obj.IsDead || obj.IsTargetable) return;
                charaStruct->TargetableStatus |= ObjectTargetableFlags.IsTargetable;
            }
            else
            {
                if (!obj.IsTargetable) return;
                charaStruct->TargetableStatus &= ~ObjectTargetableFlags.IsTargetable;
            }
        }
        sa.Log.Debug($"SetTargetable {targetable} => {obj.Name} {obj}");
    }

    public static unsafe void ScaleModify(this ScriptAccessory sa, IGameObject? obj, float scale, bool vfxScaled = true)
    {
        sa.Method.RunOnMainThreadAsync(Action);
        void Action()
        {
            if (obj == null) return;
            GameObject* charaStruct = (GameObject*)obj.Address;
            if (!obj.IsValid() || !charaStruct->IsReadyToDraw())
            {
                sa.Log.Error($"传入的IGameObject不合法。");
                return;
            }
            charaStruct->Scale = scale;
            if (vfxScaled)
                charaStruct->VfxScale = scale;

            if (charaStruct->IsCharacter())
                ((BattleChara*)charaStruct)->Character.CharacterData.ModelScale = scale;
        
            charaStruct->DisableDraw();
            charaStruct->EnableDraw();
        
            sa.Log.Debug($"ScaleModify => {obj.Name.TextValue} | {obj} => {scale}");
        }
    }

    public static void SetRotation(this ScriptAccessory sa, IGameObject? obj, float radian, bool show = false)
    {
        if (obj == null || !obj.IsValid())
        {
            sa.Log.Error($"传入的IGameObject不合法。");
            return;
        }
        unsafe
        {
            GameObject* charaStruct = (GameObject*)obj.Address;
            charaStruct->SetRotation(radian);
        }
        sa.Log.Debug($"改变面向 {obj.Name.TextValue} | {obj.EntityId} => {radian.RadToDeg()}");
        
        if (!show) return;
        var ownerObj = sa.GetById(obj.EntityId);
        if (ownerObj == null) return;
        var dp = sa.DrawGuidance(ownerObj, 0, 0, 2000, $"改变面向 {obj.Name.TextValue}", radian, draw: false);
        dp.FixRotation = true;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dp);
    }
    
    public static unsafe bool IsMoving(this ScriptAccessory sa)
    {
        FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentMap* ptr = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentMap.Instance();
        return ptr is not null && ptr->IsPlayerMoving;
    }
    
    public static unsafe void WriteVisible(this ScriptAccessory sa, IGameObject? actor, bool visible)
    {
        const VisibilityFlags VISIBLE_FLAG = VisibilityFlags.None;
        const VisibilityFlags INVISIBILITY_FLAG = VisibilityFlags.Model;
        try
        {
            var flagsPtr = &((GameObject*)actor?.Address)->RenderFlags;
            *flagsPtr = visible ? VISIBLE_FLAG : INVISIBILITY_FLAG;
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

