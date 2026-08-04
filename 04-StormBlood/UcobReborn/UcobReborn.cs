using System;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent.Struct;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Data;
using KodakkuAssist.Module.Draw.Manager;
using KodakkuAssist.Module.GameEvent.Types;
using KodakkuAssist.Extensions;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Numerics;
using Newtonsoft.Json;
using System.Linq;
using System.Globalization;
using System.Runtime.CompilerServices;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace UsamisKodakku.Scripts._04_StormBlood.UcobReborn;


[ScriptType(name: Name, territorys: [733], guid: "e2e37136-72a2-46b0-abc7-ade17da161b7",
    version: Version, author: "Usami", note: NoteStr, updateInfo: UpdateInfo)]

public class UcobReborn
{
    const string NoteStr =
        $"""
        {Version}
        基于 UCOB [巴哈姆特绝境战] 脚本的重置版。
        加了很多很多东西。
        原脚本作者：Joshua，Meva，KnightRider，Usami
        """;
    
    const string UpdateInfo =
        $"""
        {Version}
        1. 修复 P3E 连击的三重奏 大地摇动 #1 两根指引线过近的问题。
        2. 修复 P3A 进军的三重奏 不显示属于自己的指路的问题。
        3. 延长 P3B 黑炎的三重奏 拐弯示意的时间
        """;

    private const string Name = "绝巴哈姆特 Reborn";
    private const string Version = "0.0.0.2";
    private const string DebugVersion = "a";
    private int _runId = 0;
    public const bool Debugging = false;
    
    public static readonly Vector3 Center = Vector3.Zero;
    
    private UcobParams _upm = new();
    private PriorityDict _pd = new();
    private readonly object _stateLock = new();

    [UserSetting("特殊模式，含调用游戏原生特效的绘图")]
    public static bool SpecialMode { get; set; } = true;
    
    // [UserSetting("指挥模式")]
    // public static bool CaptainMode { get; set; } = false;

    public void Init(ScriptAccessory sa)
    {
        _runId++;
        DrawTools.ResetLifecycle();
        _upm.Reset();
        _pd.Init("P1黑球");
        sa.Method.RemoveDraw(".*");
        sa.Method.ClearFrameworkUpdateAction(this);
        sa.DebugMsg($"脚本 {Name} v{Version}{DebugVersion} 完成初始化，_runId {_runId}");
    }


    private async Task<bool> WaitUntilConditions(
        Func<bool>[] conditions,
        int timeoutMs = 2000,
        int intervalMs = 50)
    {
        var runId = _runId;
        var count = timeoutMs / intervalMs;
        for (int i = 0; i < count; i++)
        {
            if (_runId != runId) return false;
            lock (_stateLock)
            {
                if (conditions.All(condition => condition()))
                    return _runId == runId;
            }
            await Task.Delay(intervalMs);
        }
        return false;
    }

    #region 测试项

    [ScriptMethod(name: "———————— 《测试项》 ————————",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void 测试项分割线(Event ev, ScriptAccessory sa)
    {
        sa.DebugMsg($"Hello Koda! {Name}");
    }
    
    [ScriptMethod(name: "拉怪位置",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void 拉怪位置(Event ev, ScriptAccessory sa)
    {
        _upm.求解拉怪位置();
        
        for (int i = 0; i < 2; i++)
        {
            if (!Debugging && sa.GetMyIndex() != i) continue;
            sa.DrawGuidance(sa.Data.PartyList[i], _upm.拉怪位置, 0, 5000,
                $"P4_{_upm.当前阶段}_拉怪位置", sa.Data.DefaultSafeColor);
        }

        var color = new Vector4(1f, 0.5f, 0.5f, 0.75f);
        sa.DrawCircle(_upm.拉怪位置, 0, 5000, $"P4_{_upm.当前阶段}_拉怪位置", 1f, color);

        执行分散方向绘图(sa, 0, 5000);
    }
    
    [ScriptMethod(name: "测试模板",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void 测试模板(Event ev, ScriptAccessory sa)
    {
        var myObj = sa.Data.MyObject;
        if (myObj is not { }) return;
        var myPos = myObj.Position;
        
        sa.DrawLaser(myPos + new Vector3(0, 0, 1), 0, 5200, new Vector3(2f, 5f, 2f), new Vector4(1, 0.3f, 0.3f, 1));
        sa.DrawLaser(myPos + new Vector3(0, 0, -1), 0, 5200, new Vector3(2f, 5f, 2f), new Vector4(0.3f, 1, 0.3f, 1));
        sa.DrawLaser(myPos + new Vector3(1, 0, 0), 0, 5200, new Vector3(2f, 5f, 2f), new Vector4(1, 0.3f, 1, 1));
        sa.DrawLaser(myPos + new Vector3(-1, 0, 0), 0, 5200, new Vector3(2f, 5f, 2f), new Vector4(0.3f, 0.3f, 1, 1));
        sa.DrawCountDown(myPos + new Vector3(0, 0, 3), 200, iconScale: 1f, objIdBias: 1);
        sa.DrawCountDown(myPos + new Vector3(0, 0, -3), 200, iconScale: 1f, objIdBias: 2);
        sa.DrawCountDown(myPos + new Vector3(3, 0, 0), 200, iconScale: 1f, objIdBias: 3);
        sa.DrawCountDown(myPos + new Vector3(-3, 0, 0), 200, iconScale: 1f, objIdBias: 4);
    }
    
    #endregion 测试项

    #region 通用 双塔尼亚

    [ScriptMethod(name: "=============《通用 双塔尼亚》=============",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void 双塔尼亚_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "GEN_旋风预警", 
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(9898|9906)$"],
        userControl: true)]
    public void GEN_旋风预警(Event ev, ScriptAccessory sa)
    {
        var destroyMs = ev.ActionId == 9898 ? 2000 : 5500;
        for (var i = 0; i < sa.Data.PartyList.Count; i++)
            sa.DrawCircle(sa.Data.PartyList[i], 0, destroyMs, $"GEN_{_upm.当前阶段}_旋风{i}",
                1.5f, sa.Data.DefaultDangerColor.WithW(2), byTime: true);

        var txt = _upm.当前阶段 < 1000 ? "旋风 -> 分摊" : "旋风旋风";
        var ttstxt = _upm.当前阶段 < 1000 ? "旋风然后分摊" : "旋风旋风";
        
        sa.TextInfo(txt, destroyMs: 2000, isWarning: true);
        sa.TTS(ttstxt);
    }
    
    [ScriptMethod(name: "GEN_旋风危险区", 
        eventType: EventTypeEnum.ObjectChanged, eventCondition: ["DataId:2001168", "Operate:Add"],
        userControl: true)]
    public void GEN_旋风危险区(Event ev, ScriptAccessory sa)
    {
        sa.DrawCircle(ev.SourcePosition, 0, 7000, $"GEN_旋风危险区", 1.25f, new Vector4(1, 0, 0, 4));
    }
        
    [ScriptMethod(name: "GEN_黑球路径", 
        eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:8160"],
        userControl: true)]
    public void GEN_黑球路径(Event ev, ScriptAccessory sa)
    {
        var sid = ev.SourceId;
        var dp = sa.DrawLine(sid, 0, 3500, 10000, $"GEN_黑球路径{sid}",
            0, 2f, 6f, new Vector4(1, 1, 0, 3), draw: false);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        sa.DrawArrow(sid, 0, 3500, 10000, $"GEN_黑球路径{sid}", 0, 1f, 5.5f, new Vector4(0, 0, 1, 1));
    }
    
    [ScriptMethod(name: "GEN_拘束器内黑球爆炸范围",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9902"],
        userControl: Debugging)]
    public void GEN_拘束器内黑球爆炸范围(Event ev, ScriptAccessory sa)
    {
        for (int i = 0; i < _upm.拘束器坐标.Count; i++)
        {
            var destroyMs = _upm.当前阶段.GetDecimalDigit(3) == 3 ? 3500 : 10000;
            sa.DrawCircle(_upm.拘束器坐标[i], 3500, destroyMs, $"GEN_拘束器内黑球爆炸范围{i}", 8f, new Vector4(1, 1, 0, 0.4f));
        }
    }

    [ScriptMethod(name: "GEN_黑球爆炸范围删除",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9903", "TargetIndex:1"],
        userControl: Debugging)]
    public void GEN_黑球爆炸范围删除(Event ev, ScriptAccessory sa)
    {
        var sid = ev.SourceId;
        var tid = ev.TargetId;
        var spos = ev.SourcePosition;

        int minIdx = _upm.获得最近拘束器序列(spos);
        sa.Method.RemoveDraw($"GEN_黑球路径{sid}.*");
        sa.Method.RemoveDraw($".*_{_upm.当前阶段}_黑球搭档连线.*");
        if (_upm.当前阶段 != 3500)
            sa.Method.RemoveDraw($"GEN_拘束器内黑球爆炸范围{minIdx}.*");

        var tidx = sa.GetPlayerIdIndex((uint)tid);
        if (!sa.IsValidPartyIndex(tidx)) return;
        _pd[tidx] = 0;
        sa.Method.RemoveDraw($".*_{_upm.当前阶段}_黑球指路{tidx}.*");
    }
    
    [ScriptMethod(name: "GEN_删除旋风绘图",
        eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(9898)$", "TargetIndex:1"],
        userControl: Debugging)]
    public void GEN_删除旋风绘图(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw(@"GEN_\d{4}_旋风[0-9].*");
    }
    
    [ScriptMethod(name: "GEN_删除垂直下落绘图",
        eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(9896)$", "TargetIndex:1"],
        userControl: Debugging)]
    public void GEN_删除垂直下落绘图(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw(@"GEN_\d{4}_垂直下落.*");
    }
    
    [ScriptMethod(name: "GEN_删除液体地狱绘图",
        eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(9901)$", "TargetIndex:1"],
        userControl: Debugging)]
    public void GEN_删除液体地狱绘图(Event ev, ScriptAccessory sa)
    {
        _upm.液体地狱判定次数++;
        sa.DebugMsg($"液体地狱判定次数 {_upm.液体地狱判定次数}");
        if (_upm.液体地狱判定次数 < 5) return;
        _upm.液体地狱判定次数 = 0;
        sa.Method.RemoveDraw(@"GEN_\d{4}_液体地狱.*");
    }
    
    [ScriptMethod(name: "GEN_黑球点名记录", 
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0076"], 
        userControl: Debugging)]
    public void GEN_黑球点名记录(Event ev, ScriptAccessory sa)
    {
        lock (_stateLock)
        {
            var tidx = sa.GetPlayerIdIndex((uint)ev.TargetId);
            if (!sa.IsValidPartyIndex(tidx)) return;
            _pd.AddPriority(tidx, 100);
        }
    }
    
    [ScriptMethod(name: "GEN_火球分摊范围", 
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0075"],
        userControl: true)]
    public void GEN_火球分摊范围(Event ev, ScriptAccessory sa)
    {
        sa.DrawCircle(ev.TargetId, 0, 20000, $"GEN_双塔火球分摊范围", 4f, new Vector4(0.3f, 1, 0.3f, 1));
        if (_upm.当前阶段 < 3000) return;
        sa.TextInfo("火球分摊");
        sa.TTS("火球分摊");
        if (!SpecialMode) return;
        sa.DrawLockOn(ev.TargetId, 383, 100, 5000, new Vector3(2, 2, 2));
    }

    [ScriptMethod(name: "GEN_火球分摊范围删除", 
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9900", "TargetIndex:1"], 
        userControl: Debugging)]
    public void GEN_火球分摊范围删除(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"GEN_双塔火球分摊范围");
    }
    
    [ScriptMethod(name: "GEN_液体地狱随机火圈范围",
        eventType: EventTypeEnum.ActionEffect, 
        eventCondition: ["ActionId:regex:^(9901)$", "TargetIndex:1"],
        userControl: true)]
    public async void GEN_液体地狱随机火圈范围(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段.GetDecimalDigit(3) == 3) return;
        
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _upm.当前阶段 >= 1999 ||
                      (_upm.P1.技能循环轴[_upm.P1.技能序号] is TwinTaniaSkills.液体地狱 or TwinTaniaSkills.液体地狱随机),
                () => _upm.液体地狱判定次数 == 1
            ])) return;
        
        var color = new Vector4(0.3f, 0.7f, 1, 1.5f);
        sa.DrawCircle(ev.TargetId, 0, 10000, $"GEN_{_upm.当前阶段}_液体地狱随机火圈范围", 5f, color);
    }
    
    private async void 液体地狱引导范围绘图(ScriptAccessory sa, bool phaseKeep)
    {
        if (_upm.当前阶段 < 1999)
        {
            // 加一层当前阶段判断保护
            var lastPhase = _upm.当前阶段;
            if (!await WaitUntilConditions(
                conditions:
                [
                    () => _upm.P1.技能循环轴[_upm.P1.技能序号] == TwinTaniaSkills.液体地狱,
                ])) return;
            if (phaseKeep && _upm.当前阶段 != lastPhase) return;
        }
    
        var color = new Vector4(0.3f, 0.3f, 1, 4f);
        sa.DrawDonut(_upm.P1.双塔尼亚_ObjId, 0, 20000, $"GEN_{_upm.当前阶段}_液体地狱引导范围", 16.5f, 15f, color);

        var biasRole = _upm.当前阶段 == 4000 ? 6 : 3;
        var ttsStr = sa.GetMyIndex() != biasRole ? "环内躲避" : "环外引导";
        sa.TextInfo($"{ttsStr} 液体地狱", destroyMs: 1500);
        sa.TTS(ttsStr);
        
        // 液体地狱预警
        var color2 = new Vector4(0.3f, 0.7f, 1, 1.5f);
        for (int i = 0; i < sa.Data.PartyList.Count; i++)
        {
            if (sa.GetById(sa.Data.PartyList[i]) is not { } obj) continue;
            if (sa.GetById(_upm.P1.双塔尼亚_ObjId) is not { } bossObj) continue;
            
            var draw = sa.DrawCircle(sa.Data.PartyList[i], 0, 20000, 
                $"GEN_{_upm.当前阶段}_液体地狱预警{i}", 6f, color2, draw: false);
            
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, draw, dp =>
            {
                var distance = Vector3.Distance(obj.Position, bossObj.Position);
                dp.Scale = distance > 15 ? new Vector2(5) : new Vector2(0);
            });
        }
    }
    
    #endregion 通用 双塔尼亚

    #region 通用 奈尔

    [ScriptMethod(name: "=============《通用 奈尔》=============",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void 奈尔_分割线(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "GEN_超新星（黑泥）危险范围", 
        eventType: EventTypeEnum.ObjectChanged, eventCondition: ["DataId:2003393", "Operate:Add"],
        userControl: true)]
    public void GEN_超新星危险范围(Event ev, ScriptAccessory sa)
    {
        sa.DrawCircle(ev.SourcePosition, 0, 15000, $"GEN_超新星危险位置", 5f, new Vector4(1, 0, 0, 3));
    }
    
    [ScriptMethod(name: "GEN_台词连续技范围删除", 
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(992[01]|991[5678])$", "TargetIndex:1"], 
        userControl: Debugging)]
    public void GEN_台词连续技范围删除(Event ev, ScriptAccessory sa)
    {
        var aid = ev.ActionId;
        var tidx = -1;
        if (aid == 9920)
        {
            tidx = sa.GetPlayerIdIndex((uint)ev.TargetId);
            if (!sa.IsValidPartyIndex(tidx)) return;
        }
        var skillStr = aid switch
        {
            9915 => "钢铁",
            9916 => "月环",
            9917 => "分摊",
            9918 => "凶鸟冲",
            9920 => $"陨石流{tidx}",
            9921 => "月华冲",
            _ => ""
        };
        if (skillStr == "") return;
        sa.Method.RemoveDraw(@$"GEN_\d{{4}}_台词{skillStr}");
    }
    
    [ScriptMethod(name: "GEN_小龙俯冲范围", 
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(993[12345])$"],
        userControl: true)]
    public void GEN_小龙俯冲范围(Event ev, ScriptAccessory sa)
    {
        var sid = ev.SourceId;
        sa.DrawRect(ev.SourceId, 0, 0, 4000, $"GEN_{_upm.当前阶段}_小龙俯冲范围_{sid}", 
            0, 20, 60, sa.Data.DefaultDangerColor.WithW(1.5f), true);
        
        if (_upm.当前阶段 != 2010) return;
        sa.Method.RemoveDraw($"P2C_{_upm.当前阶段}_小龙俯冲引导时范围_{sid}");
        sa.Method.RemoveDraw($"P2C_{_upm.当前阶段}_小龙俯冲引导位置指路_{sid}");
    }
    
    #endregion 通用 奈尔

    #region P1

    [ScriptMethod(name: "———————— 《P1》 ————————",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P1_分割线(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "P1_记录双塔尼亚ID",
        eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:627"],
        userControl: Debugging)]
    public void P1_记录双塔尼亚ID(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 > 1999) return;
        
        if (sa.GetById(ev.TargetId) is not { } obj) return;
        if (obj.DataId != 8159) return;
        
        _upm.P1.双塔尼亚_ObjId = ev.TargetId;
    }
    
    [ScriptMethod(name: "P1_双塔透明化且显示中心",
        eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:627"],
        userControl: true)]
    public void P1_双塔透明化且显示中心(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 > 1999) return;
        
        if (sa.GetById(ev.TargetId) is not { } obj) return;
        if (obj.DataId != 8159) return;
        
        sa.AlphaModify(obj, 0.5f);
        // 不能被转阶段的删除绘图删掉
        sa.DrawCircle(_upm.P1.双塔尼亚_ObjId, 0, Int32.MaxValue, 
            $"P1_9999_双塔尼亚中心点_内圆", 0.4f, new Vector4(1, 0, 0, 2), useImgui: true);
        sa.DrawDonut(_upm.P1.双塔尼亚_ObjId, 0, Int32.MaxValue, 
            $"P1_9999_双塔尼亚中心点_外环", 0.5f, 0.4f, new Vector4(0, 1, 1, 1), useImgui: true);
    }

    [ScriptMethod(name: "P1_循环技能判定",
        eventType: EventTypeEnum.ActionEffect, 
        eventCondition: ["ActionId:regex:^(989[678]|990[12])$", "TargetIndex:1"],
        userControl: Debugging)]
    public void P1_循环技能判定(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 > 1999) return;
        if (ev.ActionId == 9901)
        {
            // if (!await WaitUntilConditions([() => _upm.P1.液体地狱循环轴判断次数 != _upm.液体地狱判定次数])) return;
            _upm.P1.液体地狱循环轴判断次数++;
            sa.DebugMsg($"液体地狱循环轴判断次数改变 {_upm.P1.液体地狱循环轴判断次数}");
            if (_upm.P1.液体地狱循环轴判断次数 < 5) return;
            _upm.P1.液体地狱循环轴判断次数 = 0;
        }
        _upm.P1.增加循环技能序号();
        sa.DebugMsg($"当前循环技能序号：{_upm.P1.技能序号} 阶段 {_upm.当前阶段}");
    }

    private async void 垂直下落绘图(ScriptAccessory sa)
    {
        if (_upm.当前阶段 < 1999)
        {
            if (!await WaitUntilConditions(
                conditions:
                [
                    () => _upm.P1.技能循环轴[_upm.P1.技能序号] == TwinTaniaSkills.垂直下落,
                ])) return;
        }
        
        if (sa.GetById(_upm.P1.双塔尼亚_ObjId) is not { } obj) return;
        if (obj.DataId != 8159) return;

        sa.TextInfo("即将垂直下落（顺劈）", destroyMs: 1500);
        sa.TTS("即将顺劈");

        var dp = sa.DrawFan(_upm.P1.双塔尼亚_ObjId, 0, 10000, $"GEN_{_upm.当前阶段}_垂直下落范围", 90f.DegToRad(), 0, 11.96f, 0,
            sa.Data.DefaultDangerColor.WithW(1.5f), draw: false);
        dp.SetOwnerTarget(false);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }
    
    [ScriptMethod(name: "P1_垂直下落范围（开场）",
        eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:627"],
        userControl: true)]
    public void P1_垂直下落范围开场(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 > 1999) return;
        
        if (sa.GetById(ev.TargetId) is not { } obj) return;
        if (obj.DataId != 8159) return;
        垂直下落绘图(sa);
    }

    [ScriptMethod(name: "P1_垂直下落范围（中段）",
        eventType: EventTypeEnum.ActionEffect, 
        eventCondition: ["ActionId:regex:^(9898|9897)$", "TargetIndex:1"],
        userControl: true)]
    public void P1_垂直下落范围中段(Event ev, ScriptAccessory sa)
    {
        // 垂直下落只会在死刑后，或旋风后
        if (_upm.当前阶段 > 1999) return;
        垂直下落绘图(sa);
    }
    
    [ScriptMethod(name: "P1_液体地狱引导范围（转阶段）",
        eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["SourceDataId:8159", "Id:148"],
        userControl: true)]
    public void P1_液体地狱引导范围转阶段(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 > 1999) return;
        液体地狱引导范围绘图(sa, phaseKeep: false);
    }
    
    [ScriptMethod(name: "P1_液体地狱引导范围（中段）",
        eventType: EventTypeEnum.ActionEffect, 
        eventCondition: ["ActionId:regex:^(9902|9896)$", "TargetIndex:1"],
        userControl: true)]
    public void P1_液体地狱引导范围中段(Event ev, ScriptAccessory sa)
    {
        // 液体地狱只会在阶段开场，或黑球后，或垂直下落后
        if (_upm.当前阶段 > 1999) return;
        液体地狱引导范围绘图(sa, phaseKeep: true);
    }

    
    [ScriptMethod(name: "P1_拘束器位置记录",
        eventType: EventTypeEnum.ObjectChanged, eventCondition: ["DataId:2001151", "Operate:Add"],
        userControl: Debugging)]
    public void P1_拘束器位置记录(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 > 2001) return;

        var tPos = ev.SourcePosition;
        _upm.拘束器坐标.Add(tPos);
        sa.DebugMsg($"{tPos.ToStr()}");

        // 基于国服打法，收集完三个拘束器后，进行排序，B -> C -> D
        if (_upm.拘束器坐标.Count != 3) return;

        _upm.拘束器坐标 = _upm.拘束器坐标
            .OrderBy(pos => pos.GetRadian(Center).RadianToRegion(3, 1, true, true))
            .ToList();
        sa.DebugMsg($"排序：{string.Join(", ", _upm.拘束器坐标.Select(p => p.ToStr()))}");
        _upm.求解拉怪位置();
    }

    [ScriptMethod(name: "P1_黑球搭档连线",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0076"],
        userControl: true, suppress: 500)]
    public async void P1_黑球搭档连线(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 > 1999) return;
        var cnt = _upm.拘束器坐标.Count;
        if (cnt == 1) return;
        
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _pd.SelectSpecificPriorityIndex(cnt - 1, true).Value >= 100,
            ])) return;

        if (!Debugging && _pd[sa.GetMyIndex()] < 100) return;
        var player1Idx = _pd.SelectSpecificPriorityIndex(0, true).Key;
        var player2Idx = _pd.SelectSpecificPriorityIndex(1, true).Key;
        sa.DrawConnection(sa.Data.PartyList[player1Idx], sa.Data.PartyList[player2Idx], 0, 20000,
            $"P1_{_upm.当前阶段}_黑球搭档连线", new Vector4(1, 1, 0, 1));
    }
    

    [ScriptMethod(name: "P1_黑球指路",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0076"],
        userControl: true, suppress: 500)]
    public async void P1_黑球指路(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 > 1999) return;
        var cnt = _upm.拘束器坐标.Count;
        
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _pd.SelectSpecificPriorityIndex(cnt - 1, true).Value >= 100,
            ])) return;
        
        // 将黑球点名玩家添加进 members
        List<PriorityEntry> members = [];
        for (int i = 0; i < cnt; i++)
            members.Add(_pd.SelectSpecificPriorityIndex(i, true));
        
        // P1 采用就近原则指路
        for (int i = 0; i < members.Count; i++)
        {
            if (!Debugging && members[i].Key != sa.GetMyIndex()) continue;
            var draw = sa.DrawGuidance(sa.Data.PartyList[members[i].Key], _upm.拘束器坐标[0], 0, 20000, 
                $"P1_{_upm.当前阶段}_黑球指路{members[i].Key}", sa.Data.DefaultSafeColor, draw: false);
            
            var i1 = i;
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, draw, dp =>
            {
                if (members.Count == 1)
                    dp.TargetPosition = _upm.拘束器坐标[0];
                else
                {
                    if (sa.GetById(sa.Data.PartyList[members[i1 == 0 ? 1 : 0].Key]) is not { } partnerObj) return;
                    var partnerPos = partnerObj.Position;
                    
                    if (sa.GetById(sa.Data.PartyList[members[i1].Key]) is not { } myObj) return;
                    var myPos = myObj.Position;

                    var myDistanceDelta =
                        Vector3.Distance(myPos, _upm.拘束器坐标[0]) - Vector3.Distance(myPos, _upm.拘束器坐标[1]);
                    var partnerDistanceDelta =
                        Vector3.Distance(partnerPos, _upm.拘束器坐标[0]) - Vector3.Distance(partnerPos, _upm.拘束器坐标[1]);
                    dp.TargetPosition = myDistanceDelta > partnerDistanceDelta ? _upm.拘束器坐标[1] : _upm.拘束器坐标[0];
                }
            });
        }
    }
    
    [ScriptMethod(name: "P1_根据双塔动作转阶段",
        eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["SourceDataId:8159", "Id:148"],
        userControl: Debugging)]
    public void P1_根据双塔动作转阶段(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 > 1999) return;
        sa.Method.RemoveDraw($".*_{_upm.当前阶段}.*");
        
        _upm.当前阶段 = _upm.当前阶段 switch
        {
            1100 => 1200,
            1200 => 2000,
            _ => 1100,
        };

        if (_upm.当前阶段 != 2000)
        {
            _upm.P1.技能序号 = 0;
            _upm.P1.获得阶段技能循环轴(_upm.当前阶段);
        }
        else
        {
            if (sa.GetById(ev.SourceId) is not { } obj) return;
            if (obj.DataId != 8159) return;
            // sa.AlphaModify(obj, 1f, currentAlpha => currentAlpha <= 0.6f);
            sa.Method.RemoveDraw("GEN.*");
            sa.Method.RemoveDraw("P1.*");
        }
        sa.DebugMsg($"{_upm.当前阶段}");
    }
    #endregion P1

    #region P2

    [ScriptMethod(name: "———————— 《P2》 ————————",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P2_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    #region P2A 开场阶段 2000~2002

    [ScriptMethod(name: "=============《P2A 开场阶段》=============",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P2A_开场阶段_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P2A_指向击退位置",
        eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["SourceDataId:8159", "Id:148"],
        userControl: true, suppress: 500)]
    public async void P2A_指向击退位置(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 < 1200) return;
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _upm.当前阶段 == 2000,
            ])) return;

        sa.DrawGuidance(new Vector3(0, 0, -9.37f), 0, 10000, $"P2A_{_upm.当前阶段}_指向击退位置", sa.Data.DefaultSafeColor);
        sa.DrawKnockBack(Center, 0, 10000, $"P2_击退范围", 1f, 10f, sa.Data.DefaultDangerColor.WithW(1.5f));
    }
    
    [ScriptMethod(name: "P2A_诸神黄昏即死区高亮",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9912"],
        userControl: true, suppress: 500)]
    public void P2A_诸神黄昏即死区高亮(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 2000) return;
        var color = new Vector4(1, 0.2f, 0.2f, 3f);
        if (sa.GetById(ev.SourceId) is not { } obj) return;
        var dp = sa.DrawRect(obj.Position, 0, 20000, $"P2A_{_upm.当前阶段}_诸神黄昏即死区", 0, 9, 7, color, draw: false);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
    }
    
    [ScriptMethod(name: "P2A_开场陨石流分散",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9912"],
        userControl: true, suppress: 500)]
    public void P2A_开场陨石流分散(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 2000) return;
        var color = new Vector4(0.4f, 1, 1, 1.5f);
        执行台词连续技绘图(sa, NaelQuoteSkills.陨石流, 0, 20000, color);
    }
    
    [ScriptMethod(name: "P2A_开场陨石流指路",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9912"],
        userControl: true, suppress: 500)]
    public void P2A_开场陨石流指路(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 2000) return;
        List<(int region, bool inside)> tPosList =
        [
            (6, false), (8, true), (8, false), (10, false),
            (4, true), (12, true), (4, false), (12, false)
        ];

        for (int i = 0; i < sa.Data.PartyList.Count; i++)
        {
            if (!Debugging && sa.GetMyIndex() != i) continue;
            var tPos = new Vector3(0, 0, 20).RotateAndExtend(Center, 22.5f.DegToRad() * tPosList[i].region,
                tPosList[i].inside ? -15 : 0);
            sa.DrawGuidance(sa.Data.PartyList[i], tPos, 0, 10000, $"P2A_{_upm.当前阶段}_陨石流指路{i}", sa.Data.DefaultSafeColor);
        }
    }
    
    [ScriptMethod(name: "P2A_删除陨石流指路",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9920"],
        userControl: Debugging, suppress: 500)]
    public void P2A_删除陨石流指路(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 2000) return;
        sa.Method.RemoveDraw($"P2A_{_upm.当前阶段}_陨石流指路.*");
    }
    
    [ScriptMethod(name: "P2A_陨石流转阶段",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9920"],
        userControl: Debugging, suppress: 500)]
    public void P2A_陨石流转阶段(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 > 2002) return;
        _upm.当前阶段 = _upm.当前阶段 switch
        {
            2001 => 2002,
            _ => 2001
        };
        sa.DebugMsg($"{_upm.当前阶段}");
        if (_upm.当前阶段 != 2002) return;
        sa.Method.RemoveDraw($"P2A_2000.*");
    }
    
    [ScriptMethod(name: "P2A_月华冲（初始）",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9920"],
        userControl: true, suppress: 500)]
    public async void P2A_月华冲初始(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 is not (2000 or 2001 or 2002)) return;
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _upm.当前阶段 == 2002,
            ])) return;
        sa.DrawCircle(sa.Data.PartyList[1], 0, 3000, $"GEN_{_upm.当前阶段}_台词月华冲", 5f,
            sa.Data.DefaultDangerColor.WithW(1.5f));
    }

    #endregion P2A 开场阶段 2000~2002
    
    #region P2B 龙神的加护 2010
    
    [ScriptMethod(name: "=============《P2B 龙神的加护》=============",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P2B_龙神的加护_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P2B_龙神的加护_转阶段", 
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9922"],
        userControl: Debugging)]
    public void P2B_龙神的加护_转阶段(Event ev, ScriptAccessory sa)
    {
        _upm.当前阶段 = 2010;
        _upm.P2.奈尔_ObjId = ev.SourceId;
        sa.Method.RemoveDraw(@".*");
        sa.DebugMsg($"{_upm.当前阶段}");
        _pd.Init("P2死宣");
    }
    
    [ScriptMethod(name: "P2B_奈尔透明化", 
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9922"],
        userControl: Debugging)]
    public void P2B_奈尔透明化(Event ev, ScriptAccessory sa)
    {
        if (sa.GetById(ev.SourceId) is not { } obj) return;
        sa.AlphaModify(obj, 0.5f);
    }
    
    [ScriptMethod(name: "P2B_火龙连线分摊范围与指路", 
        eventType: EventTypeEnum.Tether, eventCondition: ["Id:0005"],
        userControl: true)]
    public void P2B_火龙连线分摊范围与指路(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 2010) return;
        sa.DebugMsg($"上一轮受击玩家：{string.Join(", ", _upm.P2.烈火球受击玩家.Select(x => sa.GetPlayerJobByIndex(x)))}");
        
        var myObj = sa.Data.MyObject;
        if (myObj == null) return;
        var tIdx = sa.GetPlayerIdIndex((uint)ev.TargetId);
        if (!sa.IsValidPartyIndex(tIdx)) return;
        
        _upm.P2.烈火球轮数++;
        sa.DebugMsg($"火龙连线 {sa.GetPlayerJobByIndex(tIdx)} 第 {_upm.P2.烈火球轮数} 轮");

        switch (_upm.P2.烈火球轮数)
        {
            case 3:
                sa.TextInfo("先雷，后火");
                sa.TTS("先雷，后火");
                break;
            case 4:
                sa.TextInfo("先火，后雷");
                sa.TTS("先火，后雷");
                break;
        }
        
        var draw = sa.DrawCircle(ev.TargetId, 0, 5200, 
            $"P2B_{_upm.当前阶段}_火龙连线分摊范围", 4, sa.Data.DefaultSafeColor, draw: false);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, draw, dp =>
        {
            var myIndex = sa.GetMyIndex();
            var fireDanger = myObj.HasStatus(464) ||
                             (_upm.P2.烈火球轮数 == 2 && tIdx != myIndex && _upm.P2.烈火球受击玩家.Contains(myIndex)) ||
                             (_upm.P2.烈火球轮数 == 3 && tIdx != myIndex && _upm.P2.烈火球受击玩家.Contains(myIndex));
            dp.Color = (fireDanger ? sa.Data.DefaultDangerColor : sa.Data.DefaultSafeColor).WithW(1.5f);
        });
    }
    
    [ScriptMethod(name: "P2B_火龙连线受击玩家刷新", 
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9925"], 
        userControl: Debugging, suppress: 500)]
    public void P2B_火龙连线受击玩家刷新(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 2010) return;
        _upm.P2.烈火球受击玩家.Clear();
        _upm.P2.烈火球受击玩家记录轮数 = _upm.P2.烈火球轮数;
    }

    [ScriptMethod(name: "P2B_火龙连线受击玩家记录", 
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9925"], 
        userControl: Debugging)]
    public async void P2B_火龙连线受击玩家记录(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 2010) return;
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _upm.P2.烈火球受击玩家记录轮数 == _upm.P2.烈火球轮数,
            ])) return;
        var tIdx = sa.GetPlayerIdIndex((uint)ev.TargetId);
        if (!sa.IsValidPartyIndex(tIdx)) return;
        _upm.P2.烈火球受击玩家.Add(tIdx);
    }
    
    [ScriptMethod(name: "P2B_火龙连线分摊范围与指路删除", 
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9925", "TargetIndex:1"], 
        userControl: Debugging)]
    public void P2B_火龙连线分摊范围删除(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 2010) return;
        sa.Method.RemoveDraw(@"P2B_\d{4}_火龙连线分摊范围");
        sa.Method.RemoveDraw(@"P2B_\d{4}_火龙连线分摊指路");
    }
    
    [ScriptMethod(name: "P2B_雷点名范围", 
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9927"],
        userControl: true)]
    public void P2B_雷点名范围(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 2010) return;
        var color = new Vector4(0.4f, 0.2f, 1f, 2f);
        sa.DrawCircle(ev.TargetId, 0, 6000, $"P2B_雷点名范围", 5f, color);
        // if (!Debugging && sa.GetPlayerIdIndex((uint)ev.TargetId) != sa.GetMyIndex()) return;
        if (!SpecialMode) return;
        sa.DrawLockOn(ev.TargetId, 507, 0, 6000, new(1.5f, 1.5f, 1.5f), 7f / 6f);
    }
    
    [ScriptMethod(name: "P2B_雷点名范围删除", 
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9928"],
        userControl: true)]
    public void P2B_雷点名范围删除(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 2010) return;
        sa.Method.RemoveDraw(@"P2B_雷点名范围");
    }
    
    private void 执行台词连续技绘图(ScriptAccessory sa,
        NaelQuoteSkills nqs, int delayMs, int destroyMs, Vector4 color)
    {
        switch (nqs)
        {
            case NaelQuoteSkills.钢铁:
                sa.DrawCircle(_upm.P2.奈尔_ObjId, delayMs, destroyMs, $"GEN_{_upm.当前阶段}_台词钢铁", 8.55f, color);
                break;
            case NaelQuoteSkills.月环:
                sa.DrawDonut(_upm.P2.奈尔_ObjId, delayMs, destroyMs, $"GEN_{_upm.当前阶段}_台词月环", 22, 6, color);
                break;
            case NaelQuoteSkills.分摊:
                sa.DrawCircle(Center, delayMs, destroyMs, $"GEN_{_upm.当前阶段}_台词分摊", 4, color);
                if (!SpecialMode) break;
                sa.DrawOmen(Center, 453, delayMs, destroyMs, new(4, 8, 4));
                break;
            case NaelQuoteSkills.月华冲:
                var dp = sa.DrawCircle(_upm.P2.奈尔_ObjId, delayMs, destroyMs, 
                    $"GEN_{_upm.当前阶段}_台词月华冲", 5f, color, draw: false);
                dp.SetEnmityOrder(true, 1);
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                break;
            case NaelQuoteSkills.凶鸟冲:
                for (int i = 0; i < sa.Data.PartyList.Count; i++)
                    sa.DrawCircle(sa.Data.PartyList[i], delayMs, destroyMs, 
                        $"GEN_{_upm.当前阶段}_台词凶鸟冲{i}", 3f, color, byTime: true);
                break;
            case NaelQuoteSkills.陨石流:
                for (int i = 0; i < sa.Data.PartyList.Count; i++)
                    sa.DrawCircle(sa.Data.PartyList[i], delayMs, destroyMs, 
                        $"GEN_{_upm.当前阶段}_台词陨石流{i}", 4f, color);
                break;
            default:
                break;
        };
    }

    [ScriptMethod(name: "P2B_台词连续技", 
        eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:regex:^(649[234567]|650[01])$"], 
        userControl: true)]
    public async void P2B_台词连续技(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 2010) return;
        var quoteId = ev.Id0();
        var color = new Vector4(0.4f, 1, 1, 1.5f);
        switch (quoteId)
        {
            case 0x6492:
                // 月光啊！照亮铁血霸道！
                执行台词连续技绘图(sa, NaelQuoteSkills.月环, 0, 5000, color);
                执行台词连续技绘图(sa, NaelQuoteSkills.钢铁, 5000, 3000, color);
                sa.TextInfo("月环 -> 钢铁", isWarning: true);
                sa.TTS("月环，然后钢铁");
                break;
            case 0x6493:
                // 月光啊！用你的炽热烧尽敌人！
                执行台词连续技绘图(sa, NaelQuoteSkills.月环, 0, 5000, color);
                执行台词连续技绘图(sa, NaelQuoteSkills.分摊, 5000, 3000, sa.Data.DefaultSafeColor);
                sa.TextInfo("月环 -> 分摊", isWarning: true);
                sa.TTS("月环，然后分摊");
                break;
            case 0x6494:
                // 被炽热灼烧过的轨迹，乃成铁血霸道！
                执行台词连续技绘图(sa, NaelQuoteSkills.分摊, 0, 5000, sa.Data.DefaultSafeColor);
                执行台词连续技绘图(sa, NaelQuoteSkills.钢铁, 5000, 3000, color);
                sa.TextInfo("分摊 -> 钢铁", isWarning: true);
                sa.TTS("分摊，然后钢铁");
                break;
            case 0x6495:
                // 炽热燃烧！给予我月亮的祝福！
                执行台词连续技绘图(sa, NaelQuoteSkills.分摊, 0, 5000, sa.Data.DefaultSafeColor);
                执行台词连续技绘图(sa, NaelQuoteSkills.月环, 5000, 3000, color);
                sa.TextInfo("分摊 -> 月环", isWarning: true);
                sa.TTS("分摊，然后月环");
                break;
            case 0x6496:
                // 我降临于此，征战铁血霸道！
                执行台词连续技绘图(sa, NaelQuoteSkills.凶鸟冲, 0, 5000, color);
                执行台词连续技绘图(sa, NaelQuoteSkills.钢铁, 5000, 3000, color);
                sa.TextInfo("分散 -> 钢铁", isWarning: true);
                sa.TTS("分散，然后钢铁");
                break;
            case 0x6497:
                // 我降临于此，对月长啸！
                执行台词连续技绘图(sa, NaelQuoteSkills.凶鸟冲, 0, 5000, color);
                执行台词连续技绘图(sa, NaelQuoteSkills.月环, 5000, 3000, color);
                sa.TextInfo("分散 -> 月环", isWarning: true);
                sa.TTS("分散，然后月环");
                break;
            case 0x6500:
                // 超新星啊，更加闪耀吧！在星降之夜，称赞红月！
                执行台词连续技绘图(sa, NaelQuoteSkills.陨石流, 12000, 3000, color);
                执行台词连续技绘图(sa, NaelQuoteSkills.月华冲, 15000, 2000, color);
                var runId1 = _runId;
                var phase1 = _upm.当前阶段;
                await Task.Delay(8000);
                if (_runId != runId1 || _upm.当前阶段 != phase1) return;
                sa.TextInfo("保持分散", isWarning: true);
                sa.TTS("保持分散");
                break;
            case 0x6501:
                // 超新星啊，更加闪耀吧！照亮红月下炽热之地！
                执行台词连续技绘图(sa, NaelQuoteSkills.月华冲, 13000, 3000, color);
                执行台词连续技绘图(sa, NaelQuoteSkills.分摊, 15000, 2000, sa.Data.DefaultSafeColor);
                var runId2 = _runId;
                var phase2 = _upm.当前阶段;
                await Task.Delay(8000);
                if (_runId != runId2 || _upm.当前阶段 != phase2) return;
                sa.TextInfo("奈尔上天后，当前T分散，人群分摊", isWarning: true);
                sa.TTS("当前T分散，人群分摊");
                break;
        }
    }
    
    [ScriptMethod(name: "P2B_死宣记录", 
        eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:210"], 
        userControl: Debugging)]
    public void P2B_死宣记录(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 2010) return;
        if (ev.SourceId != 0xE0000000) return;
        var idx = sa.GetPlayerIdIndex((uint)ev.TargetId);
        if (!sa.IsValidPartyIndex(idx)) return;
        var time = ev.DurationMilliseconds();
        var priVal = time switch
        {
            > 15000 => 10,
            > 9000 => 20,
            _ => 30
        };
        _pd.AddPriority(idx, priVal);
        sa.DebugMsg($"{sa.GetPlayerJobByIndex(idx)} 死宣{4 - priVal / 10}", order: 30 - priVal);
        
        if (priVal != 30) return;
        _upm.P2.死宣一记录完毕 = true;
    }

    [ScriptMethod(name: "P2B_救世之翼预指路",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9930"],
        userControl: true)]
    public async void P2B_救世之翼预指路(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 2010) return;
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _upm.P2.死宣一记录完毕,
            ])) return;

        var castIdx = _upm.P2.救世之翼序号;
        var targetEntry = _pd.SelectSpecificPriorityIndex(castIdx, true);
        var tIdx = targetEntry.Key;
        _upm.P2.救世之翼序号++;
        
        if (!Debugging && tIdx != sa.GetMyIndex()) return;
        var tPos = ev.EffectPosition;
        var dpName = $"P2B_{_upm.当前阶段}_{tIdx}_死宣_准备吃圈";
        sa.DrawGuidance(sa.Data.PartyList[tIdx], tPos, 0, 4500, dpName, sa.Data.DefaultDangerColor);
        
    }

    [ScriptMethod(name: "P2B_救世之翼指路",
        eventType: EventTypeEnum.ObjectChanged, eventCondition: ["Operate:Add", "DataId:2003412"],
        userControl: true)]
    public async void P2B_救世之翼指路(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 2010) return;
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _upm.P2.死宣一记录完毕,
            ])) return;
        
        var foodIdx = _upm.P2.贡品序号;
        _upm.P2.贡品序号++;
        var targetEntry = _pd.SelectSpecificPriorityIndex(foodIdx, true);
        var tIdx = targetEntry.Key;
        sa.Method.RemoveDraw($"P2B_{_upm.当前阶段}_{tIdx}_死宣_准备吃圈");
        
        // 画范围
        var dpRangeName = $"P2B_{_upm.当前阶段}_{tIdx}_{ev.SourceId}_死宣_范围";
        if (tIdx != sa.GetMyIndex())
            sa.DrawCircle(ev.SourceId, 0, 4500, dpRangeName, 1.25f, new Vector4(1, 0, 0, 4));
        
        // 画指路
        if (!Debugging && tIdx != sa.GetMyIndex()) return;
        var tPos = ev.SourcePosition;
        var dpGuideName = $"P2B_{_upm.当前阶段}_{tIdx}_{ev.SourceId}_死宣_去吃圈";
        sa.DrawGuidance(sa.Data.PartyList[tIdx], tPos, 0, 4500, dpGuideName, sa.Data.DefaultSafeColor);
    }
    
    [ScriptMethod(name: "P2B_死宣解除删除相关绘图",
        eventType: EventTypeEnum.StatusRemove, eventCondition: ["StatusID:210"],
        userControl: Debugging)]
    public void P2B_死宣解除删除相关绘图(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 2010) return;
        if (ev.SourceId != 0xE0000000) return;
        var tIdx = sa.GetPlayerIdIndex((uint)ev.TargetId);
        if (!sa.IsValidPartyIndex(tIdx)) return;
        var removeDrawName = @$"P2B_{_upm.当前阶段}_{tIdx}_\d+_死宣.*";
        sa.Method.RemoveDraw(removeDrawName);
        
        // 若此时场上再没有人有死宣 Buff，字典恢复
        foreach (var member in sa.Data.PartyList)
        {
            if (sa.GetById(member) is not { } obj) continue;
            if (((IPlayerCharacter)obj).HasStatus(210)) return;
        }
        _pd.Init("P2死宣");
        _upm.P2.死宣参数重置();
        sa.DebugMsg($"死宣参数重置");
    }
    
    [ScriptMethod(name: "P2B_贡品消失删除相关绘图",
        eventType: EventTypeEnum.ObjectChanged, eventCondition: ["Operate:Remove", "DataId:2003412"],
        userControl: Debugging)]
    public void P2B_贡品消失删除相关绘图(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 2010) return;
        var sid = ev.SourceId;
        var removeDrawName = @$"P2B_{_upm.当前阶段}_\d_{sid}_死宣.*";
        sa.Method.RemoveDraw(removeDrawName);
    }
    
    #endregion P2B 龙神的加护 2010
    
    #region P2C 小龙俯冲 2010
    
    [ScriptMethod(name: "=============《P2C 小龙俯冲》=============",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P2C_小龙俯冲_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P2C_小龙方位记录", 
        eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(816[34567])$"], 
        userControl: Debugging)]
    public void P2C_小龙方位记录(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 is not (2002 or 2010)) return;
        string? debugMsg = null;
        lock (_stateLock)
        {
            var spos = ev.SourcePosition;
            // 以A为0，顺时针增加
            var region = spos.GetRadian(Center).RadianToRegion(8, 4, isDiagDiv: true, isCw: true);
            _upm.P2.小龙列表.Add(new OuterDragon { ObjectId = ev.SourceId, Region = region });
            
            if (_upm.P2.小龙列表.Count >= 5)
            {
                _upm.P2.小龙列表 = _upm.P2.小龙列表.OrderBy(x => x.Region).ToList();
                _upm.P2.获得小龙俯冲引导点();
                debugMsg = $"小龙方位 {string.Join(", ", _upm.P2.小龙列表.Select(x => x.Region))}\n" +
                           $"引导点 {string.Join(", ", _upm.P2.小龙俯冲引导点.Select(x => x))}";
            }
        }
        if (debugMsg != null)
            sa.DebugMsg(debugMsg);
    }

    [ScriptMethod(name: "P2C_小龙俯冲序号增加",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0014"],
        userControl: Debugging)]
    public void P2C_小龙俯冲序号增加(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 2010) return;
        var tIdx = sa.GetPlayerIdIndex((uint)ev.TargetId);
        if (!sa.IsValidPartyIndex(tIdx)) return;
        _upm.P2.小龙点名轮数++;
        _upm.P2.小龙俯冲引导玩家.Add(tIdx);
        sa.DebugMsg($"小龙点名第 {_upm.P2.小龙点名轮数} 轮点 {sa.GetPlayerJobByIndex(tIdx)}", order: 0);
    }

    [ScriptMethod(name: "P2C_小龙俯冲返回提示",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(993[12345])$"],
        userControl: true, suppress: 500)]
    public async void P2C_小龙俯冲返回提示(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 2010) return;
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _upm.P2.小龙点名轮数 > _upm.P2.小龙返回已处理轮数,
            ])) return;
        
        var round = _upm.P2.小龙返回已处理轮数 + 1;
        if (round > _upm.P2.小龙点名轮数) return;
        _upm.P2.小龙返回已处理轮数 = round;

        var tIdx = _upm.P2.小龙俯冲引导玩家[round - 1];
        if (tIdx != sa.GetMyIndex()) return;
        sa.TextInfo("快回去！", destroyMs: 2000, isWarning: true);
        sa.TTS("快回去");
    }

    [ScriptMethod(name: "P2C_小龙俯冲引导时范围",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0014"],
        userControl: true)]
    public async void P2C_小龙俯冲引导时范围(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 2010) return;
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _upm.P2.小龙点名轮数 > _upm.P2.小龙范围已处理轮数,
            ])) return;

        var round = _upm.P2.小龙范围已处理轮数 + 1;
        if (round > _upm.P2.小龙点名轮数) return;
        _upm.P2.小龙范围已处理轮数 = round;
        
        var tid = ev.TargetId;
        if (!Debugging && tid != sa.Data.Me) return;
        
        var color = new Vector4(0.4f, 1, 1, 0.5f);
        switch (round)
        {
            case 1:
                sa.DrawRect(_upm.P2.小龙列表[0].ObjectId, tid, 
                    0, 7300, $"P2C_{_upm.当前阶段}_小龙俯冲引导时范围_{_upm.P2.小龙列表[0].ObjectId}", 0, 20, 45, color);
                sa.DrawRect(_upm.P2.小龙列表[1].ObjectId, tid, 
                    0, 7300, $"P2C_{_upm.当前阶段}_小龙俯冲引导时范围_{_upm.P2.小龙列表[1].ObjectId}", 0, 20, 45, color);
                break;
            case 2:
                sa.DrawRect(_upm.P2.小龙列表[2].ObjectId, tid, 
                    0, 7300, $"P2C_{_upm.当前阶段}_小龙俯冲引导时范围_{_upm.P2.小龙列表[2].ObjectId}", 0, 20, 45, color);
                break;
            case 3:
                sa.DrawRect(_upm.P2.小龙列表[3].ObjectId, tid, 
                    0, 7300, $"P2C_{_upm.当前阶段}_小龙俯冲引导时范围_{_upm.P2.小龙列表[3].ObjectId}", 0, 20, 45, color);
                sa.DrawRect(_upm.P2.小龙列表[4].ObjectId, tid, 
                    0, 7300, $"P2C_{_upm.当前阶段}_小龙俯冲引导时范围_{_upm.P2.小龙列表[4].ObjectId}", 0, 20, 45, color);
                break;
            default:
                return;
        }
    }

    [ScriptMethod(name: "P2C_小龙俯冲引导位置指路",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0014"],
        userControl: true)]
    public async void P2C_小龙俯冲引导位置指路(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 2010) return;
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _upm.P2.小龙点名轮数 > _upm.P2.小龙指路已处理轮数,
            ])) return;

        var round = _upm.P2.小龙指路已处理轮数 + 1;
        if (round > _upm.P2.小龙点名轮数) return;
        _upm.P2.小龙指路已处理轮数 = round;
        
        var tid = ev.TargetId;
        if (!Debugging && tid != sa.Data.Me) return;
        
        var guideIndex = round - 1;
        if (guideIndex < 0 || guideIndex >= _upm.P2.小龙俯冲引导点.Count) return;
        var guideRegion = _upm.P2.小龙俯冲引导点[guideIndex];

        sa.DebugMsg($"{sa.GetPlayerJobById((uint)tid)} 去方位 {guideRegion} 引导第 {round} 轮", order: 1);
        var tPos = new Vector3(0, 0, -20).RotateAndExtend(Center, -30f.DegToRad() * guideRegion);
        // 利用 ObjId 方便删除指路，正好 [1, 2, 3] 是三轮中的其中一条。
        var dragonObjId = _upm.P2.小龙列表[round].ObjectId;
        sa.DrawGuidance(tid, tPos, 0, 6000, $"P2C_{_upm.当前阶段}_小龙俯冲引导位置指路_{dragonObjId}", sa.Data.DefaultSafeColor);
        if (!SpecialMode) return;
        sa.DrawCountDown(tPos, 2300, objIdBias: (uint)round);
    }

    #endregion P2C 小龙俯冲 2010

    #region P2D 第七灵灾 2020

    [ScriptMethod(name: "=============《P2D 第七灵灾》=============",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P2D_第七灵灾_分割线(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "P2D_转阶段",
        eventType: EventTypeEnum.Targetable, eventCondition: ["DataId:8161", "Targetable:False"],
        // eventType: EventTypeEnum.Director, eventCondition: ["Command:80000001", "Instance:80037569"],
        userControl: Debugging)]
    public void P2D_转阶段(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 2010) return;
        _upm.当前阶段 = 2020;
        sa.Method.RemoveDraw($"P2[A-Z]_2010_.*");
        sa.Method.RemoveDraw($"GEN.*");
        sa.DebugMsg($"{_upm.当前阶段}");
        
        if (sa.GetById(_upm.P2.奈尔_ObjId) is not { } obj) return;
        // sa.AlphaModify(obj, 1f, currentAlpha => currentAlpha <= 0.6f);
    }

    [ScriptMethod(name: "P2D_指向击退位置",
        eventType: EventTypeEnum.Director, eventCondition: ["Command:80000001", "Instance:80037569"],
        userControl: true)]
    public async void P2D_指向击退位置(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 is not (2010 or 2020)) return;
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _upm.当前阶段 == 2020,
            ])) return;

        sa.DrawGuidance(new Vector3(0, 0, -9.37f), 0, 10000, $"P2D_{_upm.当前阶段}_指向击退位置", sa.Data.DefaultSafeColor);
        sa.DrawKnockBack(Center, 0, 10000, $"P2D_{_upm.当前阶段}_击退范围", 1f, 10f, sa.Data.DefaultDangerColor.WithW(1.5f));
    }

    [ScriptMethod(name: "P2D_第七灵灾删除绘图与转阶段",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(993[79])$"],
        userControl: Debugging, suppress: 500)]
    public void P2D_第七灵灾删除绘图(Event ev, ScriptAccessory sa)
    {
        var aid = ev.ActionId;
        sa.Method.RemoveDraw($".*");
        
        // 最后一段伤害 灵灾之焰 9939
        if (aid != 9939) return;
        _upm.当前阶段 = 3000;
        sa.DebugMsg($"{_upm.当前阶段}");
    }

    #endregion P2D 第七灵灾 2020
    
    #endregion P2

    #region P3

    [ScriptMethod(name: "———————— 《P3》 ————————",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3_分割线(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "P3_巴哈透明化",
        eventType: EventTypeEnum.Targetable, eventCondition: ["Targetable:True", "DataId:8168"],
        userControl: Debugging, suppress: 500)]
    public void P3_巴哈透明化(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3000) return;
        if (sa.GetById(ev.SourceId) is not { } obj) return;
        sa.AlphaModify(obj, 0.5f);
    }

    [ScriptMethod(name: "P3_记录巴哈姆特ID",
        eventType: EventTypeEnum.Targetable, eventCondition: ["Targetable:True", "DataId:8168"],
        userControl: Debugging, suppress: 500)]
    public void P3A_记录巴哈姆特ID(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3000) return;
        if (_upm.P3.巴哈_ObjId != 0) return;
        _upm.P3.巴哈_ObjId = ev.SourceId;
        _upm.P3.获得阶段技能循环轴(_upm.当前阶段);
    }
    
    [ScriptMethod(name: "P3_记录奈尔ID",
        eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["Id:7747", "SourceDataId:8161"],
        userControl: Debugging)]
    public void P3_记录奈尔ID(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 is not (3000 or 3010)) return;
        if (_upm.P3.奈尔_ObjId != 0) return;
        _upm.P3.奈尔_ObjId = ev.SourceId;
    }
    
    [ScriptMethod(name: "P3_记录双塔尼亚ID",
        eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["Id:7748", "SourceDataId:8159"],
        userControl: Debugging)]
    public void P3_记录双塔尼亚ID(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 is not (3000 or 3010)) return;
        if (_upm.P3.双塔_ObjId != 0) return;
        _upm.P3.双塔_ObjId = ev.SourceId;
    }

    [ScriptMethod(name: "P3_记录Boss位置与方位", 
        eventType: EventTypeEnum.PlayActionTimeline, 
        eventCondition: ["SourceDataId:regex:^(8161|8159|8168)$", "Id:regex:^(774[78])$"], 
        userControl: Debugging)]
    public void P3_记录Boss位置(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段.GetDecimalDigit(3) != 3) return;
        var spos = ev.SourcePosition;
        if (Vector3.Distance(spos, Center) < 18) return;
        var sdid = ev.SourceDataId();
        switch (sdid) {
            case 8161:
                _upm.P3.奈尔_Pos = spos;
                _upm.P3.奈尔方位 = spos.GetRadian(Center).RadianToRegion(8, isDiagDiv: true);
                _upm.P3.奈尔记录阶段 = _upm.当前阶段;
                sa.DebugMsg($"{_upm.当前阶段} 更新奈尔方位 {_upm.P3.奈尔方位} {_upm.P3.奈尔_Pos.ToStr()}");
                break;
            case 8159:
                _upm.P3.双塔_Pos = spos;
                _upm.P3.双塔方位 = spos.GetRadian(Center).RadianToRegion(8, isDiagDiv: true);
                _upm.P3.双塔记录阶段 = _upm.当前阶段;
                sa.DebugMsg($"{_upm.当前阶段} 更新双塔方位 {_upm.P3.双塔方位} {_upm.P3.双塔_Pos.ToStr()}");
                break;
            case 8168:
                _upm.P3.巴哈_Pos = spos;
                _upm.P3.巴哈方位 = spos.GetRadian(Center).RadianToRegion(8, isDiagDiv: true);
                _upm.P3.巴哈记录阶段 = _upm.当前阶段;
                sa.DebugMsg($"{_upm.当前阶段} 更新巴哈方位 {_upm.P3.巴哈方位} {_upm.P3.巴哈_Pos.ToStr()} ");
                break;
        }
    }
    
    [ScriptMethod(name: "P3_巴哈本体技能判定",
        eventType: EventTypeEnum.ActionEffect, 
        eventCondition: ["ActionId:regex:^(994[012])$", "TargetIndex:1"],
        userControl: Debugging, suppress: 500)]
    public void P3_巴哈本体技能判定(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段.GetDecimalDigit(3) != 3) return;
        
        var actionId = ev.ActionId;
        switch (actionId)
        {
            case UcobParamsP3.吐息:
            {
                if (_upm.P3.技能循环轴[_upm.P3.技能序号] == BahamutSkills.三连吐息)
                {
                    _upm.P3.三连吐息判定次数++;
                    sa.DebugMsg($"三连吐息判定次数 {_upm.P3.三连吐息判定次数}");
                    if (_upm.P3.三连吐息判定次数 < 3) return;
                    _upm.P3.三连吐息判定次数 = 0;
                }
                sa.Method.RemoveDraw($"P3_{_upm.当前阶段}_吐息.*");
                break;
            }
            case UcobParamsP3.十亿核爆:
                break;
            case UcobParamsP3.夷为平地:
                break;
        }
        _upm.P3.增加技能序号();
        sa.DebugMsg($"当前技能序号：{_upm.P3.技能序号} 阶段 {_upm.当前阶段}");
    }

    private async void P3_吐息范围绘图(ScriptAccessory sa)
    {
        try
        {
            if (!await WaitUntilConditions(
                timeoutMs: 500,
                conditions:
                [
                    () => _upm.P3.吐息已绘图技能序号 < _upm.P3.技能序号,
                    () => _upm.P3.技能循环轴[_upm.P3.技能序号] is BahamutSkills.吐息 or BahamutSkills.三连吐息,
                    () => _upm.P3.巴哈_ObjId != 0,
                ])) return;

            _upm.P3.吐息已绘图技能序号 = _upm.P3.技能序号;
            var color = new Vector4(0.3f, 1f, 1, 1f);
            var dp = sa.DrawFan(_upm.P3.巴哈_ObjId, 0, 15000, 
                $"P3_{_upm.当前阶段}_吐息范围", 90f.DegToRad(), 0, 30f, 0,
                color, draw: false);
            dp.SetOwnerTarget(false);
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }
        catch (Exception e)
        {
            sa.DebugMsg($"出错 {e}");
        }
    }

    [ScriptMethod(name: "P3_吐息范围（开场）",
        eventType: EventTypeEnum.Targetable, eventCondition: ["Targetable:True", "DataId:8168"],
        userControl: true)]
    public void P3_吐息范围开场(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 >= 3100) return;
        P3_吐息范围绘图(sa);
    }

    [ScriptMethod(name: "P3_吐息范围（中段）",
        eventType: EventTypeEnum.ActionEffect, 
        eventCondition: ["ActionId:regex:^(994[0124])$", "TargetIndex:1"],
        userControl: true, suppress: 500)]
    public void P3_吐息范围中段(Event ev, ScriptAccessory sa)
    {
        // 吐息只会在吐息、风暴之翼、死刑、十亿核爆后
        if (_upm.当前阶段 is > 3999 or < 3000) return;
        P3_吐息范围绘图(sa);
    }

    [ScriptMethod(name: "P3_旋风冲、月流冲、核爆冲", 
        eventType: EventTypeEnum.StartCasting, 
        eventCondition: ["ActionId:regex:^(9906|9923|9953)$"],
        userControl: true)]
    public void P3_旋风冲月流冲核爆冲(Event ev, ScriptAccessory sa)
    {
        const uint 双塔旋风冲 = 9906;
        const uint 奈尔月流冲 = 9923;
        const uint 巴哈核爆冲 = 9953;

        var color = new Vector4(0.3f, 1f, 1, 1f);
        
        var dpName = ev.ActionId switch
        {
            双塔旋风冲 => $"P3_{_upm.当前阶段}_双塔旋风冲",
            奈尔月流冲 => $"P3_{_upm.当前阶段}_奈尔月流冲",
            巴哈核爆冲 => $"P3_{_upm.当前阶段}_巴哈核爆冲",
            _ => ""
        };

        sa.DrawRect(ev.SourceId, 0, 0, 4000, 
            dpName, 0, ev.ActionId == 巴哈核爆冲 ? 12 : 8, 45, color);
    }

    [ScriptMethod(name: "P3_旋风冲、月流冲、核爆冲删除", 
        eventType: EventTypeEnum.ActionEffect, 
        eventCondition: ["ActionId:regex:^(9906|9923|9953)$", "TargetIndex:1"],
        userControl: Debugging)]
    public void P3_旋风冲月流冲核爆冲删除(Event ev, ScriptAccessory sa)
    {
        const uint 双塔旋风冲 = 9906;
        const uint 奈尔月流冲 = 9923;
        const uint 巴哈核爆冲 = 9953;
        
        var dpName = ev.ActionId switch
        {
            双塔旋风冲 => @"P3_\d{4}_双塔旋风冲",
            奈尔月流冲 => @"P3_\d{4}_奈尔月流冲",
            巴哈核爆冲 => @"P3_\d{4}_巴哈核爆冲",
            _ => ""
        };
        sa.Method.RemoveDraw(dpName);
    }
    
    [ScriptMethod(name: "P3_接线加深",
        eventType: EventTypeEnum.Tether, eventCondition: ["Id:0004"],
        userControl: Debugging, suppress: 10000)]
    public void P3_接线加深(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 is not (3100 or 3300)) return;
        var color = new Vector4(1f, 1f, 0.1f, 1f);
        
        // 第一根
        var draw1 = sa.DrawLine(ev.SourceId, ev.TargetId, 0, 6000, 
            $"P3_{_upm.当前阶段}_接线加深1", 0, 1f, 1f, color, byY: true, draw: false);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, draw1, dp =>
        {
            for (int i = 0; i < sa.Data.PartyList.Count; i++)
            {
                var member = sa.Data.PartyList[i];
                if (sa.GetById(member) is not { } obj) continue;
                var tetherSourceList = sa.GetTetherSource((IBattleChara)obj, 0x0004);
                if (tetherSourceList.Count == 0) continue;
                if (tetherSourceList[0] != _upm.P3.巴哈_ObjId) continue;
                dp.Owner = member;
                break;
            }
        });
        
        // 第二根
        var draw2 = sa.DrawLine(ev.SourceId, ev.TargetId, 0, 6000, 
            $"P3_{_upm.当前阶段}_接线加深2", 0, 1f, 1f, color, byY: true, draw: false);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, draw2, dp =>
        {
            for (int i = 0; i < sa.Data.PartyList.Count; i++)
            {
                var memberIndex = sa.Data.PartyList.Count - 1 - i;
                var member = sa.Data.PartyList[memberIndex];
                if (sa.GetById(member) is not { } obj) continue;
                var tetherSourceList = sa.GetTetherSource((IBattleChara)obj, 0x0004);
                if (tetherSourceList.Count == 0) continue;
                if (tetherSourceList[0] != _upm.P3.巴哈_ObjId) continue;
                dp.Owner = member;
                break;
            }
        });
    }
    
    [ScriptMethod(name: "P3_大地摇动范围", 
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0028"],
        userControl: Debugging)]
    public void P3_大地摇动范围(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 is not (3100 or 3500)) return;
        var color = new Vector4(0.7f, 0.7f, 0.3f, _upm.当前阶段 == 3500 ? 1f : 0.7f);
        sa.DrawFan(Center, ev.TargetId, 0, 5000, 
            $"P3_{_upm.当前阶段}_大地摇动范围", 90f.DegToRad(), 0, 50f, 0, color);
    }
    
    [ScriptMethod(name: "P3_删除以太失控屏幕特效", 
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9905"],
        userControl: true, suppress: 500)]
    public void P3_删除以太失控屏幕特效(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段.GetDecimalDigit(3) != 3) return;
        if (sa.GetById(ev.SourceId) is not { } obj) return;
        sa.Redraw(obj);
    }
    
    [ScriptMethod(name: "P3_删除转阶段屏幕特效", 
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(995[456789])$"],
        userControl: true, suppress: 500)]
    public void P3_删除转阶段屏幕特效(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段.GetDecimalDigit(3) != 3) return;
        if (sa.GetById(ev.SourceId) is not { } obj) return;
        sa.Redraw(obj);
    }

    [ScriptMethod(name: "P3_删除十亿核爆屏幕特效", 
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9942"],
        userControl: true, suppress: 500)]
    public void P3_删除十亿核爆屏幕特效(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段.GetDecimalDigit(3) != 3) return;
        if (sa.GetById(ev.SourceId) is not { } obj) return;
        sa.Redraw(obj);
    }
    
    #region P3A 进军的三重奏 3100-3150

    [ScriptMethod(name: "=============《P3A 进军的三重奏》=============",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3A_进军的三重奏_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P3A_进军阶段转换",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9954"],
        userControl: Debugging)]
    public void P3A_进军阶段转换(Event ev, ScriptAccessory sa)
    {
        _upm.当前阶段 = 3100;
        _upm.P3.巴哈_ObjId = ev.SourceId;
        sa.Method.RemoveDraw(".*");
        _pd.Init("P3进军");
        _pd.AddPriorities([1, 2, 3, 4, 5, 6, 7, 8]);
        sa.DebugMsg($"{_upm.当前阶段}");
    }

    [ScriptMethod(name: "P3A_百万核爆分散范围",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9953"],
        userControl: true)]
    public void P3A_百万核爆分散范围(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3100) return;
        var color = new Vector4(0.3f, 1f, 1, 0.5f);
        for (int i = 0; i < sa.Data.PartyList.Count; i++)
            sa.DrawCircle(sa.Data.PartyList[i], 0, 4000, 
                $"P3A_{_upm.当前阶段}_百万核爆分散范围{i}", 5f, color, byTime: true);
        sa.TextInfo("分散分散", destroyMs: 2000, isWarning: true);
        sa.TTS("分散分散");
    }

    [ScriptMethod(name: "P3A_旋风八方指路",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9953"],
        userControl: true)]
    public async void P3A_旋风八方指路(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3100) return;
        
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _upm.P3.巴哈记录阶段 == _upm.当前阶段
            ])) return;
        
        var baseRad = _upm.P3.巴哈方位 * 45f.DegToRad();
        var basePos = new Vector3(0, 0, 19.5f).RotateAndExtend(Center, baseRad);
        List<float> rotDeg = [48, -48, 76, -76, 104, -104, 132, -132];
        for (int i = 0; i < sa.Data.PartyList.Count; i++)
        {
            var color = i switch
            {
                0 or 1 => new Vector4(0.1f, 0.1f, 1, 1),
                2 or 3 => new Vector4(0.1f, 1f, 0.1f, 1),
                _ => new Vector4(1, 0.1f, 0.1f, 1),
            };
            sa.DrawLine(Center, 0, 0, 4000, $"P3A_{_upm.当前阶段}_旋风八方指路_指引线{i}",
                baseRad + rotDeg[i].DegToRad(), 20f, 25f, color);
            
            if (!Debugging && sa.GetMyIndex() != i) continue;
            var member = sa.Data.PartyList[i];
            sa.DrawGuidance(member, basePos.RotateAndExtend(Center, rotDeg[i].DegToRad()), 
                0, 4000, $"P3A_{_upm.当前阶段}_旋风八方指路{i}", sa.Data.DefaultSafeColor);
        }
    }

    [ScriptMethod(name: "P3A_旋风后指引线",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9948"],
        userControl: true, suppress: 500)]
    public void P3A_旋风后指引线(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3100) return;
        var baseRad = _upm.P3.巴哈方位 * 45f.DegToRad();
        List<float> rotDeg = [0, 90f, -90f, 180f, 90f, -90f];
        
        for (int i = 0; i < 6; i++)
        {
            var startPos = i is 1 or 2
                ? new Vector3(0, 0, 10).RotateAndExtend(Center, baseRad + rotDeg[i].DegToRad())
                : Center;
            var length = i is 0 or 3 ? 25 : 10;
            var dp = sa.DrawLine(startPos, 0, 0, 5000, $"P3A_{_upm.当前阶段}_旋风后指引线{i}", 
                baseRad + rotDeg[i].DegToRad(), 20f, length, sa.Data.DefaultSafeColor, draw: false);
            dp.Color = i switch
            {
                0 => new Vector4(1, 0.1f, 0.1f, 1),
                1 or 2 => new Vector4(0.1f, 1f, 0.1f, 1),
                3 => new Vector4(0.1f, 1f, 1f, 1),
                4 or 5 => new Vector4(0.1f, 0.1f, 1f, 1),
            };
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Line, dp);
        }

        var towerPos = new Vector3(0, 0, -6).RotateAndExtend(Center, baseRad);
        if (!SpecialMode) return;
        sa.DrawOmen(towerPos, 453, 0, 5000, new(4, 8, 4));
    }

    [ScriptMethod(name: "P3A_进军机制点名收集",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(002[78])$"],
        userControl: Debugging)]
    public void P3A_进军机制点名收集(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3100) return;
        var iconId = ev.Id0();
        var priVal = iconId switch
        {
            0x0027 => 10, // 分摊
            0x0028 => 20, // 大地摇动
            _ => 0
        };
        var tidx = sa.GetPlayerIdIndex((uint)ev.TargetId);
        if (!sa.IsValidPartyIndex(tidx)) return;
        int actionCount;
        lock (_stateLock)
        {
            _pd.AddPriority(tidx, priVal);
            _pd.AddActionCount();
            actionCount = _pd.ActionCount;
        }
        sa.DebugMsg($"{sa.GetPlayerJobByIndex(tidx)} 被点名 {(priVal == 10 ? "分摊" : "大地摇动")}", priVal + actionCount);
        if (actionCount == 6)
            sa.DebugMsg($"进军机制点名收集完毕", 30);
    }

    [ScriptMethod(name: "P3A_旋风后指路",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(002[78])$"],
        userControl: true, suppress: 500)]
    public async void P3A_旋风后指路(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3100) return;
        if (!await WaitUntilConditions(
            timeoutMs: 4000,
            conditions:
            [
                () => _pd.ActionCount == 6,
            ])) return;
        for (int i = 0; i < sa.Data.PartyList.Count; i++)
        {
            // 降序排列
            // 0 1 2 大地摇动 北、东、西
            // 3 4 5 南分摊
            // 6 7 东、西接线
            var pdEntry = _pd.SelectSpecificPriorityIndex(i, true);
            if (!Debugging && sa.GetMyIndex() != pdEntry.Key) continue;
            var member = sa.Data.PartyList[pdEntry.Key];
            处理进军指路(sa, member, i);
        }
    }

    private void 处理进军指路(ScriptAccessory sa, ulong member, int pdIndex)
    {
        switch (pdIndex)
        {
            case 0 or 1 or 2:
                var rotRad1 = pdIndex switch
                {
                    0 => 0,
                    1 => -90f.DegToRad(),
                    2 => 90f.DegToRad()
                };
                var tPos1 = new Vector3(0, 0, 20f).RotateAndExtend(Center, rotRad1 + _upm.P3.巴哈方位 * 45f.DegToRad());
                sa.DrawGuidance(member, tPos1, 0, 5000, $"P3A_{_upm.当前阶段}_进击指路{pdIndex}_大地摇动", sa.Data.DefaultSafeColor);
                break;
            case 3 or 4 or 5:
                var tPos2 = new Vector3(0, 0, -6f).RotateAndExtend(Center, _upm.P3.巴哈方位 * 45f.DegToRad());
                sa.DrawGuidance(member, tPos2, 0, 5000, $"P3A_{_upm.当前阶段}_进击指路{pdIndex}_分摊", sa.Data.DefaultSafeColor);
                break;
            case 6 or 7:
                var rotRad3 = pdIndex switch
                {
                    6 => -90f.DegToRad(),
                    7 => 90f.DegToRad(),
                };
                var tPos3 = new Vector3(0, 0, 5f).RotateAndExtend(Center, rotRad3 + _upm.P3.巴哈方位 * 45f.DegToRad());
                sa.DrawGuidance(member, tPos3, 0, 5000, $"P3A_{_upm.当前阶段}_进击指路{pdIndex}_接线", sa.Data.DefaultSafeColor);
                break;
        }
    }

    [ScriptMethod(name: "P3A_接线死刑范围",
        eventType: EventTypeEnum.Tether, eventCondition: ["Id:0004"],
        userControl: Debugging, suppress: 10000)]
    public void P3A_接线死刑范围(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3100) return;
        var color = sa.Data.DefaultDangerColor;
        
        // 只有线在坦克身上才显示范围
        var draw1 = sa.DrawCircle(ev.TargetId, 0, 6000, 
            $"P3A_{_upm.当前阶段}_接线死刑范围1", 5f, color, draw: false);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, draw1, dp =>
        {
            for (int i = 0; i < sa.Data.PartyList.Count; i++)
            {
                var member = sa.Data.PartyList[i];
                if (sa.GetById(member) is not { } obj) continue;
                var tetherSourceList = sa.GetTetherSource((IBattleChara)obj, 0x0004);
                if (tetherSourceList.Count == 0) continue;
                if (tetherSourceList[0] != _upm.P3.巴哈_ObjId) continue;
                dp.Owner = member;
                dp.Color = dp.Color.WithW(i <= 1 ? 1 : 0);
                break;
            }
        });
        
        var draw2 = sa.DrawCircle(ev.TargetId, 0, 6000, 
            $"P3A_{_upm.当前阶段}_接线死刑范围2", 5f, color, draw: false);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, draw2, dp =>
        {
            for (int i = 0; i < sa.Data.PartyList.Count; i++)
            {
                var memberIndex = sa.Data.PartyList.Count - 1 - i;
                var member = sa.Data.PartyList[memberIndex];
                if (sa.GetById(member) is not { } obj) continue;
                var tetherSourceList = sa.GetTetherSource((IBattleChara)obj, 0x0004);
                if (tetherSourceList.Count == 0) continue;
                if (tetherSourceList[0] != _upm.P3.巴哈_ObjId) continue;
                dp.Owner = member;
                dp.Color = dp.Color.WithW(memberIndex <= 1 ? 1 : 0);
                break;
            }
        });
    }
    
    [ScriptMethod(name: "P3A_转阶段与刷新技能",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9944"],
        userControl: Debugging, suppress: 500)]
    public void P3A_转阶段与刷新技能(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3100) return;
        sa.Method.RemoveDraw(@".*_3100.*");
        
        _upm.当前阶段 = 3150;
        _upm.P3.获得阶段技能循环轴(_upm.当前阶段);
        sa.DebugMsg($"{_upm.当前阶段}");
    }

    #endregion P3A 进军的三重奏 3100-3150

    #region P3B 黑炎的三重奏 3200-3250
    
    [ScriptMethod(name: "=============《P3B 黑炎的三重奏》=============",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3B_黑炎的三重奏_分割线(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "P3B_黑炎阶段转换",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9955"],
        userControl: Debugging)]
    public void P3B_黑炎阶段转换(Event ev, ScriptAccessory sa)
    {
        _upm.当前阶段 = 3200;
        _pd.Init("P3黑炎");
        _pd.AddPriorities([1, 2, 3, 4, 8, 7, 6, 5]);
        sa.DebugMsg($"{_upm.当前阶段}");
    }
    
    [ScriptMethod(name: "P3B_黑炎指路准备",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9955"],
        userControl: true)]
    public void P3B_黑炎指路准备(Event ev, ScriptAccessory sa)
    {
        sa.DrawGuidance(Center, 0, 4000, $"P3B_{_upm.当前阶段}_黑炎指路场中", sa.Data.DefaultSafeColor);
        if (!SpecialMode) return;
        sa.DrawCountDown(Center, 3500, iconScale: 3f, objIdBias: 0);
        sa.TextInfo("倒计时结束后，向奈尔移动", delayMs: 3500);
    }
    
    [ScriptMethod(name: "P3B_移动方向指引",
        eventType: EventTypeEnum.PlayActionTimeline, 
        eventCondition: ["SourceDataId:regex:^(8161)$", "Id:regex:^(7747)$"], 
        userControl: true)]
    public async void P3B_移动方向指引(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3200) return;
        var spos = ev.SourcePosition;
        if (Vector3.Distance(spos, Center) < 18) return;
        
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _upm.P3.奈尔记录阶段 == _upm.当前阶段,
            ])) return;
        
        var myIndex = sa.GetMyIndex();
        var rad1 = _upm.P3.奈尔方位 * 45f.DegToRad();
        var rad2 = (_upm.P3.奈尔方位 * 2 + (myIndex <= 3 ? 15 : 1)) % 16 * 22.5f.DegToRad();
        
        sa.DrawRect(Center, 0, 1000, $"P3B_{_upm.当前阶段}_移动方向指引1", 
            rad1, 2, 22, sa.Data.DefaultDangerColor.WithW(2f));
        sa.DrawFan(Center, 0, 1000, $"P3B_{_upm.当前阶段}_移动方向指引2", 
            45f.DegToRad(), rad2, 22, 20, sa.Data.DefaultDangerColor.WithW(2f));
        
        sa.DrawRect(Center, 1000, 7500, $"P3B_{_upm.当前阶段}_移动方向指引1", 
            rad1, 2, 22, sa.Data.DefaultSafeColor.WithW(2f));
        sa.DrawFan(Center, 1000, 7500, $"P3B_{_upm.当前阶段}_移动方向指引2", 
            45f.DegToRad(), rad2, 22, 20, sa.Data.DefaultSafeColor.WithW(2f));
    }

    [ScriptMethod(name: "P3B_出发提示",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9901", "TargetIndex:1"],
        userControl: true, suppress: 10000)]
    public void P3B_出发提示(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3200) return;
        sa.TextInfo("走走走", destroyMs: 2000, isWarning: true);
        sa.TTS("走走走");
    }
    
    [ScriptMethod(name: "P3B_黑炎机制点名收集",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(0027)$"],
        userControl: Debugging)]
    public void P3B_黑炎机制点名收集(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3200) return;
        var tidx = sa.GetPlayerIdIndex((uint)ev.TargetId);
        if (!sa.IsValidPartyIndex(tidx)) return;
        int actionCount;
        lock (_stateLock)
        {
            _pd.AddPriority(tidx, 10);
            _pd.AddActionCount();
            actionCount = _pd.ActionCount;
        }
        sa.DebugMsg($"{sa.GetPlayerJobByIndex(tidx)} 被点名 分摊", 10 + actionCount);
        if (actionCount == 4)
            sa.DebugMsg($"黑炎机制点名收集完毕", 30);
    }

    [ScriptMethod(name: "P3B_踩塔分摊指路",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(0027)$"],
        userControl: true, suppress: 2000)]
    public async void P3B_踩塔分摊指路(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3200) return;
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _pd.ActionCount == 4,
            ])) return;
        
        // 塔的方位固定，为奈尔方位 +1 +3 +5 +7，可根据优先级分配塔
        // 分摊位置固定，为奈尔方位 +4

        var naelRegion = _upm.P3.奈尔方位;
        int[] towerRegion = [
            (naelRegion + 7) % 8, (naelRegion + 5) % 8, (naelRegion + 3) % 8, (naelRegion + 1) % 8
        ];
        var stackPos = new Vector3(0, 0, -8.5f).RotateAndExtend(Center, naelRegion * 45f.DegToRad());
        
        for (int i = 0; i < sa.Data.PartyList.Count; i++)
        {
            if (!Debugging && sa.GetMyIndex() != i) continue;
            var member = sa.Data.PartyList[i];
            var priRank = _pd.FindPriorityIndexOfKey(i);
            if (priRank <= 3)
            {
                var targetTowerPos = new Vector3(0, 0, 14).RotateAndExtend(Center, towerRegion[priRank] * 45f.DegToRad());
                sa.DrawGuidance(member, targetTowerPos, 0, 5200, 
                    $"P3B_{_upm.当前阶段}_踩塔分摊指路{i}准备", sa.Data.DefaultDangerColor);
                sa.DrawGuidance(member, targetTowerPos, 5200, 2000, 
                    $"P3B_{_upm.当前阶段}_踩塔分摊指路{i}踩塔", sa.Data.DefaultSafeColor);
                sa.DrawCircle(targetTowerPos, 0, 5200, 
                    $"P3B_{_upm.当前阶段}_踩塔分摊指路{i}塔危险区", 5f, sa.Data.DefaultDangerColor.WithW(2f));

                if (sa.GetMyIndex() == i)
                {
                    sa.TextInfo(SpecialMode ? "倒计时结束后，再进塔" : "等待一次超新星，再进塔", destroyMs: 3200, isWarning: true);
                    sa.TTS("稍后进塔");
                }
                if (SpecialMode)
                    sa.DrawCountDown(targetTowerPos, 200, objIdBias: (uint)i);
            }
            else
            {
                sa.DrawGuidance(member, stackPos, 0, 5000, $"P3B_{_upm.当前阶段}_踩塔分摊指路{i}", sa.Data.DefaultSafeColor);
                if (!SpecialMode || sa.GetMyIndex() != i) continue;
                sa.DrawOmen(stackPos, 453, 0, 5000, new(4, 8, 4));
            }
        }
    }
    
    [ScriptMethod(name: "P3B_转阶段与刷新技能",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9951"],
        userControl: Debugging, suppress: 500)]
    public void P3B_转阶段与刷新技能(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3200) return;
        sa.Method.RemoveDraw(@".*_3200.*");
        
        _upm.当前阶段 = 3250;
        _upm.P3.获得阶段技能循环轴(_upm.当前阶段);
        sa.DebugMsg($"{_upm.当前阶段}");
    }


    #endregion P3B 黑炎的三重奏 3200-3250

    #region P3C 灾厄的三重奏 3300-3350

    [ScriptMethod(name: "=============《P3C 灾厄的三重奏》=============",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3C_灾厄的三重奏_分割线(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "P3C_灾厄阶段转换",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9956"],
        userControl: Debugging)]
    public void P3C_灾厄阶段转换(Event ev, ScriptAccessory sa)
    {
        _upm.当前阶段 = 3300;
        _upm.P3.灾厄台词计数 = 0;
        sa.Method.RemoveDraw(".*");
        sa.DebugMsg($"{_upm.当前阶段}");
    }

    [ScriptMethod(name: "P3C_台词连续技",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:regex:^(650[23])$"],
        userControl: true)]
    public void P3C_台词连续技(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3300) return;
        var quoteId = ev.Id0();
        var color = new Vector4(0.4f, 1, 1, 1.5f);
        switch (quoteId)
        {
            case 0x6502:
                // 我降临于此对月长啸！召唤星降之夜！
                执行台词连续技绘图(sa, NaelQuoteSkills.凶鸟冲, 0, 5000, color);
                执行台词连续技绘图(sa, NaelQuoteSkills.月环, 5000, 3000, color);
                sa.TextInfo("分散 -> 月环", isWarning: true);
                sa.TTS("分散，然后月环");
                break;
            case 0x6503:
                // 我自月而来降临于此，召唤星降之夜！
                执行台词连续技绘图(sa, NaelQuoteSkills.月环, 0, 5000, color);
                执行台词连续技绘图(sa, NaelQuoteSkills.凶鸟冲, 5000, 3000, color);
                sa.TextInfo("月环 -> 分散", isWarning: true);
                sa.TTS("月环，然后分散");
                break;
        }
    }

    [ScriptMethod(name: "P3C_获得拘束器序列",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:regex:^(650[23])$"],
        userControl: Debugging)]
    public void P3C_获得拘束器序列(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3300) return;
        var peopleIdx = _upm.获得最近拘束器序列(ev.SourcePosition);
        _upm.P3.灾厄对应拘束器[2] = peopleIdx;
        _upm.P3.灾厄对应拘束器[0] = (peopleIdx + 1) % 3;
        _upm.P3.灾厄对应拘束器[1] = (peopleIdx + 2) % 3;
        sa.DebugMsg($"MT {_upm.P3.灾厄对应拘束器[0]}, ST {_upm.P3.灾厄对应拘束器[1]}, 人群 {_upm.P3.灾厄对应拘束器[2]}");
    }
    
    [ScriptMethod(name: "P3C_进拘束器指路", 
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(9918|9916)$", "TargetIndex:1"], 
        userControl: true)]
    public void P3C_进拘束器指路(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3300) return;
        _upm.P3.灾厄台词计数++;
        
        if (ev.ActionId == 9916)
        {
            for (int i = 0; i < sa.Data.PartyList.Count; i++)
            {
                if (i >= 2) continue;
                if (!Debugging && sa.GetMyIndex() != i) continue;
                var bhIdx = _upm.P3.灾厄对应拘束器[i];
                sa.DrawGuidance(sa.Data.PartyList[i], _upm.拘束器坐标[bhIdx], 0, 10000, $"P3C_{_upm.当前阶段}_进拘束器指路{i}",
                    sa.Data.DefaultSafeColor);
            }
        }
        if (_upm.P3.灾厄台词计数 == 2)
        {
            var peopleIdx = _upm.P3.灾厄对应拘束器[2];
            for (int i = 0; i < sa.Data.PartyList.Count; i++)
            {
                if (i <= 1) continue;
                if (!Debugging && sa.GetMyIndex() != i) continue;
                sa.DrawGuidance(sa.Data.PartyList[i], _upm.拘束器坐标[peopleIdx], 0, 10000, $"P3C_{_upm.当前阶段}_进拘束器指路{i}",
                    sa.Data.DefaultSafeColor);
            }
        }
    }
    
    [ScriptMethod(name: "P3C_以太失控后陨石流", 
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9905"],
        userControl: true, suppress: 500)]
    public void P3C_以太失控后陨石流(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 is not (3300 or 3350)) return;
        var color = new Vector4(0.4f, 1, 1, 1.5f);
        执行台词连续技绘图(sa, NaelQuoteSkills.陨石流, 0, 4000, color);
    }
        
    [ScriptMethod(name: "P3C_转阶段与刷新技能", 
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9905"],
        userControl: Debugging, suppress: 500)]
    public void P3C_转阶段与刷新技能(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3300) return;
        sa.Method.RemoveDraw(@".*_3300.*");
        
        _upm.当前阶段 = 3350;
        _upm.P3.获得阶段技能循环轴(_upm.当前阶段);
        sa.DebugMsg($"{_upm.当前阶段}");
    }

    #endregion P3C 灾厄的三重奏 3300-3350

    #region P3D 天地的三重奏 3400-3450

    [ScriptMethod(name: "=============《P3D 天地的三重奏》=============",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3D_天地的三重奏_分割线(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "P3D_天地阶段转换",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9957"],
        userControl: Debugging)]
    public void P3D_天地阶段转换(Event ev, ScriptAccessory sa)
    {
        _upm.当前阶段 = 3400;
        sa.DebugMsg($"{_upm.当前阶段}");
    }

    [ScriptMethod(name: "P3D_天地指路准备",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9957"],
        userControl: true)]
    public void P3D_天地指路准备(Event ev, ScriptAccessory sa)
    {
        sa.DrawGuidance(Center, 0, 4000, $"P3D_{_upm.当前阶段}_天地指路场中", sa.Data.DefaultSafeColor);
        sa.TextInfo("场中引导俯冲，然后出发", delayMs: 3500);
    }
    
    [ScriptMethod(name: "P3D_天地旋风指路", 
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9906"],
        userControl: true)]
    public async void P3D_天地旋风指路(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3400) return;
        
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _upm.P3.巴哈记录阶段 == _upm.当前阶段,
                () => _upm.P3.双塔记录阶段 == _upm.当前阶段,
                () => _upm.P3.奈尔记录阶段 == _upm.当前阶段,
            ])) return;
        
        var err = _upm.P3.求解天地旋风方位();
        if (err != 0) return;

        for (int i = 0; i < sa.Data.PartyList.Count; i++)
        {
            if (!Debugging && sa.GetMyIndex() != i) continue;
            var region = _upm.P3.天地旋风方位[i];
            if (region < 0) continue;

            var tPos = new Vector3(0, 0, 20).RotateAndExtend(Center, 45f.DegToRad() * region);
            sa.DrawGuidance(sa.Data.PartyList[i], tPos, 0, 3700, 
                $"P3D_{_upm.当前阶段}_天地旋风指路{i}", sa.Data.DefaultSafeColor);
        }
    }
    
    [ScriptMethod(name: "P3D_天地塔方位收集", 
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9951"], 
        userControl: Debugging)]
    public void P3D_天地塔方位收集(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3400) return;
        lock (_stateLock)
        {
            var spos = ev.SourcePosition;
            var towerRegion = spos.GetRadian(Center).RadianToRegion(16, isDiagDiv: true);
            _upm.P3.天地塔方位.Add(towerRegion);
        }
    }
    
    [ScriptMethod(name: "P3D_天地塔指路", 
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9951"], 
        userControl: true, suppress: 1000)]
    public async void P3D_天地塔指路(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3400) return;
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _upm.P3.天地塔方位.Count == 8
            ])) return;
        
        var err = _upm.P3.求解天地踩塔方位();
        if (err != 0) return;
        
        for (int i = 0; i < sa.Data.PartyList.Count; i++)
        {
            if (!Debugging && sa.GetMyIndex() != i) continue;
            var region = _upm.P3.天地踩塔方位[i];
            if (region < 0) continue;

            var tPos = new Vector3(0, 0, 10).RotateAndExtend(Center, 22.5f.DegToRad() * region);
            sa.DrawGuidance(sa.Data.PartyList[i], tPos, 0, 6500, 
                $"P3D_{_upm.当前阶段}_天地踩塔指路{i}_击退位置", sa.Data.DefaultSafeColor);
            sa.DrawLine(Center, 0, 0, 6500, 
                $"P3D_{_upm.当前阶段}_天地踩塔指路{i}_指引线", tPos.GetRadian(Center), 20f, 25f,
                sa.Data.DefaultSafeColor);
            
            if (!SpecialMode) continue;
            sa.DrawCountDown(sa.Data.PartyList[i], 50);
        }
    }
    
    [ScriptMethod(name: "P3D_中心塔击退", 
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9911"],
        userControl: true)]
    public void P3D_中心塔击退(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3400) return;
        var color = new Vector4(0.4f, 1, 1, 1.5f);
        
        var dp = sa.DrawRect(Center, 0, 5000, 
            $"P3D_{_upm.当前阶段}_诸神黄昏即死区", 0, 9, 7, color, draw: false);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
        
        sa.DrawKnockBack(Center, 0, 5000, 
            $"P3D_{_upm.当前阶段}_天崩地裂击退", 
            1.5f, 12f, sa.Data.DefaultDangerColor.WithW(2f));
    }
    
    [ScriptMethod(name: "P3D_转阶段与刷新技能", 
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0075"],
        userControl: Debugging)]
    public void P3D_转阶段与刷新技能(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3400) return;
        _upm.当前阶段 = 3450;
        _upm.P3.获得阶段技能循环轴(_upm.当前阶段);
        sa.DebugMsg($"{_upm.当前阶段}");
    }

    #endregion P3D 天地的三重奏 3400-3450

    #region P3E 连击的三重奏 3500-3550

    [ScriptMethod(name: "=============《P3E 连击的三重奏》=============",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3E_连击的三重奏_分割线(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "P3E_连击阶段转换",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9958"],
        userControl: Debugging)]
    public void P3E_连击阶段转换(Event ev, ScriptAccessory sa)
    {
        _upm.当前阶段 = 3500;
        _pd.Init("P3连击");
        _pd.AddPriorities([1, 2, 3, 4, 5, 6, 7, 8]);
        sa.DebugMsg($"{_upm.当前阶段}");
    }
    
    [ScriptMethod(name: "P3E_连击指路准备",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9958"],
        userControl: true)]
    public void P3E_连击指路准备(Event ev, ScriptAccessory sa)
    {
        var basePos = new Vector3(0, 0, 15);
        List<float> rotDeg = [-120, 120, -155, 155, -85, 85, -20, 20];
        for (int i = 0; i < sa.Data.PartyList.Count; i++)
        {
            var color = i switch
            {
                0 or 1 => new Vector4(0.1f, 0.1f, 1, 1),
                2 or 3 => new Vector4(0.1f, 1f, 0.1f, 1),
                _ => new Vector4(1, 0.1f, 0.1f, 1),
            };
            sa.DrawLine(Center, 0, 0, 6500, $"P3E_{_upm.当前阶段}_连击指路准备_指引线{i}",
                rotDeg[i].DegToRad(), 20f, 25f, color);
            
            if (!Debugging && sa.GetMyIndex() != i) continue;
            var member = sa.Data.PartyList[i];
            sa.DrawGuidance(member, basePos.RotateAndExtend(Center, rotDeg[i].DegToRad()), 
                0, 6500, $"P3E_{_upm.当前阶段}_连击指路准备{i}", sa.Data.DefaultSafeColor);
        }
    }
    
    [ScriptMethod(name: "P3E_连击阶段陨石流范围", 
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9958"],
        userControl: true)]
    public void P3E_连击阶段陨石流范围(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3500) return;
        var color = new Vector4(0.4f, 1, 1, 1.5f);
        执行台词连续技绘图(sa, NaelQuoteSkills.陨石流, 4000, 15000, color);
    }
    
    [ScriptMethod(name: "P3E_连击阶段三点一线", 
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9958"],
        userControl: true)]
    public void P3E_连击阶段三点一线(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3500) return;
        
        var color = new Vector4(1f, 1f, 0f, 1f);
        for (var i = 0; i < _upm.拘束器坐标.Count; i++)
        {
            var rad = _upm.拘束器坐标[i].GetRadian(Center);
            sa.DrawLine(Center, 0, 4000, 10000, $"P3E_{_upm.当前阶段}_连击阶段三点一线", rad, 20f, 25f, color);
        }
    }

    [ScriptMethod(name: "P3E_黑球搭档连线",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0076"],
        userControl: true, suppress: 500)]
    public async void P3E_黑球搭档连线(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3500) return;
        
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _pd.SelectSpecificPriorityIndex(2, true).Value >= 100,
            ])) return;
        
        if (!Debugging && _pd[sa.GetMyIndex()] < 100) return;
        var color = new Vector4(1, 1, 0, 1);
        
        for (int i = 0; i < 3; i++)
        {
            var idx1 = _pd.SelectSpecificPriorityIndex(i, true).Key;
            var idx2 = _pd.SelectSpecificPriorityIndex((i + 1) % 3, true).Key;
            sa.DrawConnection(sa.Data.PartyList[idx1], sa.Data.PartyList[idx2], 
                0, 20000, $"P3E_{_upm.当前阶段}_黑球搭档连线{i}", color);
        }
    }
    
    [ScriptMethod(name: "P3E_黑球队列计算",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0076"],
        userControl: Debugging, suppress: 500)]
    public async void P3E_黑球队列计算(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3500) return;
        if (!_upm.P3.连击撞球截球玩家.Contains(-1)) return;
        
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _pd.SelectSpecificPriorityIndex(2, true).Value >= 100,
            ])) return;

        _upm.P3.连击点名玩家 = _pd.SelectLargePriorityIndices(3).Select(x => x.Key).ToList();
        _upm.P3.求解连击撞球截球玩家();
        sa.DebugMsg($"撞球截球玩家：{string.Join(", ", 
            _upm.P3.连击撞球截球玩家.Select(x => sa.GetPlayerJobByIndex(x)))}");
    }

    [ScriptMethod(name: "P3E_黑球指路", 
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9902"],
        userControl: true)]
    public async void P3E_黑球指路(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3500) return;
        if (_upm.P3.连击黑球绘图完成) return;

        if (!await WaitUntilConditions(
            conditions:
            [
                () => !_upm.P3.连击撞球截球玩家.Contains(-1),
            ])) return;
        var ls = _upm.P3.连击撞球截球玩家.ToArray();
        var myIndex = sa.GetMyIndex();
        if (!sa.IsValidPartyIndex(myIndex)) return;
        
        for (int i = 0; i < 3; i++)
        {
            var pidx1 = ls[i];
            var pidx2 = ls[i + 3];
            if (!Debugging && myIndex != pidx1 && myIndex != pidx2) continue;
            sa.DrawGuidance(sa.Data.PartyList[pidx1], _upm.拘束器坐标[i], 
                0, 5000, $"P3E_{_upm.当前阶段}_黑球指路{pidx1}", sa.Data.DefaultSafeColor);
            sa.DrawGuidance(sa.Data.PartyList[pidx2], _upm.拘束器坐标[i], 
                0, 5000, $"P3E_{_upm.当前阶段}_黑球指路{pidx2}准备", sa.Data.DefaultDangerColor);
            sa.DrawGuidance(sa.Data.PartyList[pidx2], _upm.拘束器坐标[i], 
                5000, 10000, $"P3E_{_upm.当前阶段}_黑球指路{pidx2}", sa.Data.DefaultSafeColor);
        }
        _upm.P3.连击黑球绘图完成 = true;
    }

    [ScriptMethod(name: "P3E_大地摇动点名收集",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0028"],
        userControl: Debugging)]
    public void P3E_大地摇动点名收集(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3500) return;
        var tidx = sa.GetPlayerIdIndex((uint)ev.TargetId);
        if (!sa.IsValidPartyIndex(tidx)) return;
        lock (_stateLock)
        {
            if (_pd.ActionCount < 4)
            {
                _pd.AddPriority(tidx, 1000);
                _pd.AddActionCount();
            }
        }
        sa.Method.RemoveDraw($"GEN_拘束器内黑球爆炸范围.*");
    }

    [ScriptMethod(name: "P3E_大地摇动搭档连线",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0028"],
        userControl: true, suppress: 500)]
    public async void P3E_大地摇动搭档连线(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3500) return;
        
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _pd.ActionCount == 4,
                () => _upm.P3.大地摇动搭档连线绘图版本 <= _upm.P3.大地摇动判定次数
            ])) return;
        
        _upm.P3.大地摇动搭档连线绘图版本++;
        var color = new Vector4(1, 1, 0, 1);

        List<int> pidx = _upm.P3.大地摇动搭档连线绘图版本 == 1
            ? _pd.SelectLargePriorityIndices(4).Select(x => x.Key).ToList()
            : _pd.SelectSmallPriorityIndices(4).Select(x => x.Key).ToList();
        
        if (!Debugging && !pidx.Contains(sa.GetMyIndex())) return;
        for (int i = 0; i < 4; i++)
        {
            var draw = sa.DrawConnection(sa.Data.PartyList[pidx[i]], sa.Data.PartyList[pidx[(i + 1) % 4]], 0, 5000,
                $"P3E_{_upm.当前阶段}_大地摇动搭档连线{i}", color, draw: false);
            var i1 = i;
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, draw, dp =>
            {
                List<int> tempPidx = pidx
                    .Select(x => new { Index = x, Member = sa.GetById(sa.Data.PartyList[x]) })
                    .Where(x => x.Member != null)
                    .OrderBy(x => x.Member.Position.GetRadian(Center))
                    .Select(x => x.Index)
                    .ToList();
                if (tempPidx.Count != pidx.Count) return;
                dp.Owner = sa.Data.PartyList[tempPidx[i1]];
                dp.TargetObject = sa.Data.PartyList[tempPidx[(i1 + 1) % tempPidx.Count]];
            });
        }
    }

    [ScriptMethod(name: "P3E_大地摇动指引线",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0028"],
        userControl: true, suppress: 500)]
    public async void P3E_大地摇动指引线(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3500) return;
        
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _pd.ActionCount == 4,
                () => _upm.P3.大地摇动指引线绘图版本 <= _upm.P3.大地摇动判定次数
            ])) return;
        
        _upm.P3.大地摇动指引线绘图版本++;
        float[] rotDegs = _upm.P3.大地摇动指引线绘图版本 == 1 ? [-40, 40, -100, 100]: [-80, 80, -140, 140];
        var isFirstRound = _pd.FindPriorityIndexOfKey(sa.GetMyIndex(), true) <= 3;
        if (!Debugging && (isFirstRound ^ (_upm.P3.大地摇动指引线绘图版本 == 1))) return;

        var color = Vector4.One;
        foreach (var deg in rotDegs)
            sa.DrawLine(Center, 0, 0, 5000, $"P3E_{_upm.当前阶段}_大地摇动指引线", deg.DegToRad(), 20f, 25, color);
    }
    
    [ScriptMethod(name: "P3E_大地摇动指路",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0028"],
        userControl: true, suppress: 500)]
    public async void P3E_大地摇动指路(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3500) return;
        
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _pd.ActionCount == 4,
                () => _upm.P3.大地摇动指路绘图版本 <= _upm.P3.大地摇动判定次数
            ])) return;
        
        _upm.P3.大地摇动指路绘图版本++;
        var safePos = _upm.P3.大地摇动指路绘图版本 == 1 ? new Vector3(0, 0, -8.5f) : new Vector3(0, 0, 8.5f);

        List<int> pidx = _upm.P3.大地摇动指路绘图版本 == 1
            ? _pd.SelectLargePriorityIndices(4).Select(x => x.Key).ToList()
            : _pd.SelectSmallPriorityIndices(4).Select(x => x.Key).ToList();
        var myIndex = sa.GetMyIndex();

        // float[] rotDegs = _upm.P3.大地摇动指路绘图版本 == 1
        //     ? [-100, -20, 20, 100]
        //     : [-140, -80, 80, 140];
        
        for (int i = 0; i < pidx.Count; i++)
        {
            var memberIndex = pidx[i];
            if (!Debugging && myIndex != memberIndex) continue;
            var member = sa.Data.PartyList[memberIndex];
            
            // 不太理想，思路保留，功能注释
            // var drawGuidance = sa.DrawGuidance(member, Center,
            //     0, 5000, $"P3E_{_upm.当前阶段}_大地摇动指路{memberIndex}", sa.Data.DefaultSafeColor, draw: false);
            // sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, drawGuidance, dp =>
            // {
            //     List<int> tempPidx = pidx
            //         .Select(x => new { Index = x, Member = sa.GetById(sa.Data.PartyList[x]) })
            //         .Where(x => x.Member != null)
            //         .OrderBy(x => x.Member!.Position.GetRadian(Center))
            //         .Select(x => x.Index)
            //         .ToList();
            //     if (tempPidx.Count != rotDegs.Length) return;
            //     var targetIdx = tempPidx.IndexOf(memberIndex);
            //     if (targetIdx < 0) return;
            //     dp.TargetPosition = new Vector3(0, 0, 15).RotateAndExtend(Center, rotDegs[targetIdx].DegToRad());
            // });

            if (SpecialMode)
                sa.DrawCountDown(member, 50);
            if (myIndex != memberIndex) continue;
            sa.TextInfo("引导大地摇动");
            sa.TTS("引导大地摇动");
        }

        for (int i = 0; i < sa.Data.PartyList.Count; i++)
        {
            if (pidx.Contains(i)) continue;
            if (!Debugging && myIndex != i) continue;
            sa.DrawGuidance(sa.Data.PartyList[i], safePos,
                0, 5000, $"P3E_{_upm.当前阶段}_大地摇动指路{i}", sa.Data.DefaultSafeColor);
            if (myIndex != i) continue;
            sa.TextInfo("前往安全区，四角预占位");
            sa.TTS("前往安全区，四角预占位");
        }
    }

    [ScriptMethod(name: "P3E_转阶段与刷新技能",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9946"],
        userControl: Debugging, suppress: 500)]
    public void P3E_转阶段与刷新技能(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3500) return;
        _upm.P3.大地摇动判定次数++;
        if (_upm.P3.大地摇动判定次数 < 2) return;
        sa.Method.RemoveDraw(@".*_3500.*");
        _upm.当前阶段 = 3550;
        _upm.P3.获得阶段技能循环轴(_upm.当前阶段);
        sa.DebugMsg($"{_upm.当前阶段}");
    }

    #endregion P3E 连击的三重奏 3500-3550

    #region P3F 群龙的八重奏 3600

    [ScriptMethod(name: "=============《P3F 群龙的八重奏》=============",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3F_群龙的八重奏_分割线(Event ev, ScriptAccessory sa)
    {
    }

    
    [ScriptMethod(name: "P3F_群龙阶段转换",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9959"],
        userControl: Debugging)]
    public void P3F_群龙阶段转换(Event ev, ScriptAccessory sa)
    {
        _upm.当前阶段 = 3600;
        _pd.Init($"P3群龙");
        sa.DebugMsg($"{_upm.当前阶段}");
        sa.Method.RemoveDraw(@".*_3550.*");
    }

    [ScriptMethod(name: "P3F_群龙指路准备",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9959"],
        userControl: true)]
    public void P3F_群龙指路准备(Event ev, ScriptAccessory sa)
    {
        sa.DrawGuidance(Center, 0, 4000, $"P3F_{_upm.当前阶段}_群龙指路场中", sa.Data.DefaultSafeColor);
    }
    
    [ScriptMethod(name: "P3F_群龙跑圈指路", 
        eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:8168"],
        userControl: true, suppress: 500)]
    public async void P3F_群龙跑圈指路(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3600) return;
        
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _upm.P3.奈尔记录阶段 == 3600,
                () => _upm.P3.巴哈记录阶段 == 3600,
                () => _upm.P3.双塔记录阶段 == 3600,
            ])) return;
        
        var err = _upm.P3.求解群龙起跑方位与方向();
        if (err != 0) return;

        var rad1 = _upm.P3.群龙起跑方位与方向[0] * 45f.DegToRad();
        var rad2 = rad1 + _upm.P3.群龙起跑方位与方向[1] * 45f.DegToRad();

        var tPos1 = new Vector3(0, 0, 22).RotateAndExtend(Center, rad1);
        var tPos2 = new Vector3(0, 0, 22).RotateAndExtend(Center, rad2);
        
        sa.DrawGuidance(tPos1, 0, 5000, $"P3F_{_upm.当前阶段}_群龙跑圈指路_起跑点准备", sa.Data.DefaultDangerColor);
        sa.DrawGuidance(tPos1, tPos2, 5000, 4000, $"P3F_{_upm.当前阶段}_群龙跑圈指路_起跑点", sa.Data.DefaultDangerColor);
        
        sa.DrawGuidance(tPos1, 5000, 5500, $"P3F_{_upm.当前阶段}_群龙跑圈指路_起跑点", sa.Data.DefaultSafeColor);
        sa.DrawGuidance(tPos1, tPos2, 5000, 5500, $"P3F_{_upm.当前阶段}_群龙跑圈指路_起跑点", sa.Data.DefaultDangerColor);
        
        sa.DrawGuidance(tPos2, 10500, 2000, $"P3F_{_upm.当前阶段}_群龙跑圈指路_开跑", sa.Data.DefaultSafeColor);
    }

    [ScriptMethod(name: "P3F_群龙起跑提示",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0077"],
        userControl: true)]
    public void P3F_群龙起跑提示(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3600) return;
        var dirStr = _upm.P3.群龙起跑方位与方向[1] == 1 ? "左" : "右";
        sa.TextInfo($"等待奈尔冲锋后，面向场外向【{dirStr}】跑", destroyMs: 4000);
        sa.TTS($"即将向{dirStr}跑");
        if (!SpecialMode) return;
        sa.DrawCountDown(sa.Data.Me, 500);
    }
    
    [ScriptMethod(name: "P3F_群龙点名记录", 
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(0077|0029|0014)$"], 
        userControl: Debugging)]
    public void P3F_群龙点名记录(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3600) return;
        lock (_stateLock)
        {
            if (_pd.ActionCount >= 7) return;
            var tidx = sa.GetPlayerIdIndex((uint)ev.TargetId);
            if (!sa.IsValidPartyIndex(tidx)) return;
            _pd.AddPriority(tidx, 10);
            _pd.AddActionCount();
        }
    }
    
    [ScriptMethod(name: "P3F_回中提示与引导双塔指路", 
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(0029)$"], 
        userControl: true)]
    public async void P3F_回中提示与引导双塔指路(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3600) return;
        
        if (!await WaitUntilConditions(
            timeoutMs: 500,
            conditions:
            [
                () => _pd.ActionCount == 7,
            ])) return;
        
        var tidx = _pd.SelectSpecificPriorityIndex(0).Key;
        var myIndex = sa.GetMyIndex();

        if (myIndex != tidx)
            sa.DrawGuidance(Center, 0, 5000, $"P3F_{_upm.当前阶段}_回中提示", sa.Data.DefaultSafeColor);

        if (Debugging || myIndex == tidx)
        {
            var member = sa.Data.PartyList[tidx];
            if (sa.GetById(member) is not { } obj) return;
            var tPos1 = new Vector3(0, 0, 22).RotateAndExtend(Center, (_upm.P3.双塔方位 * 45f + 13f).DegToRad() );
            var tPos2 = new Vector3(0, 0, 22).RotateAndExtend(Center, (_upm.P3.双塔方位 * 45f - 13f).DegToRad() );
            var draw = sa.DrawGuidance(member, 0, 0, 9000, $"P3F_{_upm.当前阶段}_双塔引导指路", sa.Data.DefaultSafeColor,
                draw: false);
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, draw, dp =>
            {
                if (!obj.IsValid()) return;
                var distanceTo1 = Vector3.Distance(obj.Position, tPos1);
                var distanceTo2 = Vector3.Distance(obj.Position, tPos2);
                dp.TargetPosition = distanceTo1 < distanceTo2 ? tPos1 : tPos2;
            });
        }
    }

    [ScriptMethod(name: "P3F_分摊点名记录",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0027"],
        userControl: true)]
    public void P3F_分摊点名记录(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3600) return;
        lock (_stateLock)
        {
            if (_pd.ActionCount >= 11) return;
            var tidx = sa.GetPlayerIdIndex((uint)ev.TargetId);
            if (!sa.IsValidPartyIndex(tidx)) return;
            _pd.AddPriority(tidx, 100);
            _pd.AddActionCount();
        }
    }
    
    [ScriptMethod(name: "P3F_踩塔搭档连线",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0027"],
        userControl: true, suppress: 500)]
    public async void P3F_踩塔搭档连线(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3600) return;
        
        if (!await WaitUntilConditions(
            conditions:
            [
                () => _pd.ActionCount == 11,
            ])) return;
        
        var color = new Vector4(1, 1, 0, 1);

        List<int> pidx = _pd.SelectSmallPriorityIndices(4).Select(x => x.Key).ToList();
        
        if (!Debugging && !pidx.Contains(sa.GetMyIndex())) return;
        for (int i = 0; i < 4; i++)
        {
            var draw = sa.DrawConnection(sa.Data.PartyList[pidx[i]], sa.Data.PartyList[pidx[(i + 1) % 4]], 0, 7000,
                $"P3F_{_upm.当前阶段}_踩塔搭档连线{i}", color, draw: false);
            var i1 = i;
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Line, draw, dp =>
            {
                List<int> tempPidx = pidx
                    .Select(x => new { Index = x, Member = sa.GetById(sa.Data.PartyList[x]) })
                    .Where(x => x.Member != null)
                    .OrderBy(x => x.Member.Position.GetRadian(Center))
                    .Select(x => x.Index)
                    .ToList();
                if (tempPidx.Count != pidx.Count) return;
                dp.Owner = sa.Data.PartyList[tempPidx[i1]];
                dp.TargetObject = sa.Data.PartyList[tempPidx[(i1 + 1) % tempPidx.Count]];
            });
        }
    }

    [ScriptMethod(name: "P3F_踩塔与躲避提示",
        eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0027"],
        userControl: true, suppress: 500)]
    public async void P3F_踩塔与躲避提示(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3600) return;

        if (!await WaitUntilConditions(
            conditions:
            [
                () => _pd.ActionCount == 11,
            ])) return;

        var myIndex = sa.GetMyIndex();
        var priVal = _pd[myIndex];
        if (priVal >= 100 && myIndex <= 1)
        {
            sa.TextInfo("带着分摊踩塔");
            sa.TTS("带着分摊踩塔");
        }
        else if (priVal >= 100)
        {
            sa.TextInfo("避开塔");
            sa.TTS("避开塔");
        }
        else
        {
            sa.TextInfo("踩塔");
            sa.TTS("踩塔");
        }
    }

    [ScriptMethod(name: "P3F_踩塔判定倒计时",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9951"],
        userControl: true)]
    public void P3F_踩塔判定倒计时(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3600) return;
        if (!SpecialMode) return;
        uint objIdBias;
        lock (_stateLock)
        {
            _upm.P3.塔头标偏移++;
            objIdBias = _upm.P3.塔头标偏移;
        }
        sa.DrawCountDown(ev.SourcePosition, 3000, iconScale: 1f, objIdBias: objIdBias);
    }

    [ScriptMethod(name: "P3F_删除绘图与转阶段",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(9906)$", "TargetIndex:1"],
        userControl: Debugging)]
    public void P3F_删除绘图与转阶段(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 3600) return;
        _upm.当前阶段 = 4000;
        sa.Method.RemoveDraw($".*");
        _pd.Init($"P4黑球");
        sa.DebugMsg($"{_upm.当前阶段}");
    }

    #endregion P3F 群龙的八重奏 3600

    #endregion P3

    #region P4

    [ScriptMethod(name: "———————— 《P4》 ————————",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P4_分割线(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "P4_拉怪位置显示",
        eventType: EventTypeEnum.Targetable, eventCondition: ["DataId:8161", "Targetable:True"],
        userControl: Debugging)]
    public void P4_拉怪位置显示(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 4000) return;
        for (int i = 0; i < 2; i++)
        {
            if (!Debugging && sa.GetMyIndex() != i) continue;
            sa.DrawGuidance(sa.Data.PartyList[i], _upm.拉怪位置, 0, 5000,
                $"P4_{_upm.当前阶段}_拉怪位置", sa.Data.DefaultSafeColor);
        }

        var color = new Vector4(1f, 0.5f, 0.5f, 0.75f);
        sa.DrawCircle(_upm.拉怪位置, 0, 140000, $"P4_{_upm.当前阶段}_拉怪位置", 1f, color);
    }
    
    [ScriptMethod(name: "P4_双Boss中心显示",
        eventType: EventTypeEnum.Targetable, eventCondition: ["DataId:8161", "Targetable:True"],
        userControl: Debugging)]
    public void P4_双Boss中心显示(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 4000) return;
        sa.DrawCircle(_upm.P1.双塔尼亚_ObjId, 0, Int32.MaxValue, 
            $"P4_{_upm.当前阶段}_双塔尼亚中心点_内圆", 0.4f, new Vector4(1, 0, 0, 2), useImgui: true);
        sa.DrawDonut(_upm.P1.双塔尼亚_ObjId, 0, Int32.MaxValue, 
            $"P4_{_upm.当前阶段}_双塔尼亚中心点_外环", 0.5f, 0.4f, new Vector4(0, 1, 1, 1), useImgui: true);
        sa.DrawCircle(_upm.P2.奈尔_ObjId, 0, Int32.MaxValue, 
            $"P4_{_upm.当前阶段}_奈尔中心点_内圆", 0.4f, new Vector4(1, 0, 0, 2), useImgui: true);
        sa.DrawDonut(_upm.P2.奈尔_ObjId, 0, Int32.MaxValue, 
            $"P4_{_upm.当前阶段}_奈尔中心点_外环", 0.5f, 0.4f, new Vector4(0, 1, 1, 1), useImgui: true);
    }

    [ScriptMethod(name: "P4_第一次垂直下落",
        eventType: EventTypeEnum.Targetable, eventCondition: ["DataId:8161", "Targetable:True"],
        userControl: Debugging)]
    public void P4_第一次垂直下落(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 4000) return;
        垂直下落绘图(sa);
    }
    
    [ScriptMethod(name: "P4_第二次垂直下落",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9897", "TargetIndex:1"],
        userControl: Debugging)]
    public void P4_第二次垂直下落(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 4000) return;
        垂直下落绘图(sa);
    }
    
    [ScriptMethod(name: "P4_液体地狱引导范围",
        eventType: EventTypeEnum.ActionEffect, 
        eventCondition: ["ActionId:regex:^(9896)$", "TargetIndex:1"],
        userControl: true)]
    public void P4_液体地狱引导范围(Event ev, ScriptAccessory sa)
    {
        // 液体地狱只会在垂直下落后
        if (_upm.当前阶段 != 4000) return;
        液体地狱引导范围绘图(sa, phaseKeep: true);
    }
    
    [ScriptMethod(name: "P4_台词连续技",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:regex:^(650[4567])$"],
        userControl: Debugging)]
    public void P4_台词连续技(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 4000) return;
        var quoteId = ev.Id0();
        var color = new Vector4(0.4f, 1, 1, 1.5f);
        switch (quoteId)
        {
            case 0x6504:
                // 钢铁燃烧吧！成为我降临于此的刀剑吧！
                执行台词连续技绘图(sa, NaelQuoteSkills.钢铁, 0, 5000, color);
                执行台词连续技绘图(sa, NaelQuoteSkills.分摊, 5000, 3000, sa.Data.DefaultSafeColor);
                执行台词连续技绘图(sa, NaelQuoteSkills.凶鸟冲, 8000, 3000, color);
                执行分散方向绘图(sa, 8000, 8000);
                sa.TextInfo("钢铁 -> 分摊 -> 分散", destroyMs: 5000, isWarning: true);
                sa.TTS("钢铁、分摊，然后分散");
                break;
            case 0x6505:
                // 钢铁成为我降临于此的燃烧之剑！
                执行台词连续技绘图(sa, NaelQuoteSkills.钢铁, 0, 5000, color);
                执行台词连续技绘图(sa, NaelQuoteSkills.凶鸟冲, 5000, 3000, color);
                执行台词连续技绘图(sa, NaelQuoteSkills.分摊, 8000, 3000, sa.Data.DefaultSafeColor);
                执行分散方向绘图(sa, 11000, 5000);
                sa.TextInfo("钢铁 -> 分散 -> 分摊", destroyMs: 5000, isWarning: true);
                sa.TTS("钢铁、分散，然后分摊");
                break;
            case 0x6506:
                // 我自月而来降临于此，踏过炽热之地！
                执行台词连续技绘图(sa, NaelQuoteSkills.月环, 0, 5000, color);
                执行台词连续技绘图(sa, NaelQuoteSkills.凶鸟冲, 5000, 3000, color);
                执行台词连续技绘图(sa, NaelQuoteSkills.分摊, 8000, 3000, sa.Data.DefaultSafeColor);
                执行分散方向绘图(sa, 11000, 5000);
                sa.TextInfo("月环 -> 分散 -> 分摊", destroyMs: 5000, isWarning: true);
                sa.TTS("月环、分散，然后分摊");
                break;
            case 0x6507:
                // 我自月而来携钢铁降临于此！
                执行台词连续技绘图(sa, NaelQuoteSkills.月环, 0, 5000, color);
                执行台词连续技绘图(sa, NaelQuoteSkills.钢铁, 5000, 3000, color);
                执行台词连续技绘图(sa, NaelQuoteSkills.凶鸟冲, 8000, 3000, color);
                执行分散方向绘图(sa, 8000, 8000);
                sa.TextInfo("月环 -> 钢铁 -> 分散", destroyMs: 5000, isWarning: true);
                sa.TTS("月环、钢铁，然后分散");
                break;
        }
    }

    private void 执行分散方向绘图(ScriptAccessory sa, int delayMs, int destroyMs)
    {
        List<float> rotDegs = [20, -20, 105, -105, 60, -60, 150, -150];
        var baseRad = _upm.拉怪位置.GetRadian(Center);
        var myIndex = sa.GetMyIndex();

        for (int i = 0; i < 8; i++)
        {
            var width = i == myIndex ? 20f : 10f;
            var color = i == myIndex ? sa.Data.DefaultSafeColor : Vector4.One;
            sa.DrawLine(Center, 0, delayMs, destroyMs, $"P4_{_upm.当前阶段}_分散方向绘图",
                baseRad + rotDegs[i].DegToRad(), width, 25, color);
        }
    }

    [ScriptMethod(name: "P4_黑球指路计算",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9902"],
        userControl: Debugging)]
    public async void P4_黑球指路计算(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 4000) return;

        if (!await WaitUntilConditions(
            conditions:
            [
                () => _pd.SelectSpecificPriorityIndex(2, true).Value >= 100,
            ])) return;

        var keys = _pd.SelectLargePriorityIndices(3).Select(x => x.Key).ToArray();
        _upm.P4.求解黑球撞球序列(keys);
    }

    [ScriptMethod(name: "P4_黑球指路与TTS",
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9902"],
        userControl: true)]
    public async void P4_黑球指路与TTS(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 4000) return;

        if (!await WaitUntilConditions(
            conditions:
            [
                () => !_upm.P4.黑球撞球序列.Contains(-1)
            ])) return;

        for (int i = 0; i < 3; i++)
        {
            var playerIndex = _upm.P4.黑球撞球序列[i];
            if (!Debugging && sa.GetMyIndex() != playerIndex) continue;

            var tPos = _upm.拘束器坐标[i];
            sa.DrawGuidance(sa.Data.PartyList[playerIndex], tPos, 0, 5500, 
                $"P4_{_upm.当前阶段}_黑球指路", sa.Data.DefaultSafeColor);

            var ttsStr = i == 2 ? "旋风后撞球" : "撞球后旋风";
            sa.DebugMsg($"{sa.GetPlayerJobByIndex(playerIndex)} 撞 {i + 1}: {ttsStr}", order: i);
            if (sa.GetMyIndex() != playerIndex) continue;
            sa.TextInfo(ttsStr, isWarning: true);
            sa.TTS(ttsStr);
        }
    }

    [ScriptMethod(name: "P4_黑球计算重置",
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9902"],
        userControl: Debugging)]
    public void P4_黑球计算重置(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 4000) return;
        _pd.Init($"P4黑球");
        _upm.P4.黑球撞球序列 = [-1, -1, -1];
    }

    #endregion P4

    #region P5

    [ScriptMethod(name: "———————— 《P5》 ————————",
        eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P5_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P5_转阶段", 
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9970", "TargetIndex:1"], 
        userControl: Debugging)]
    public void P5_转阶段(Event ev, ScriptAccessory sa)
    {
        _upm.当前阶段 = 5000;
        sa.Method.RemoveDraw(".*");
    }
    
    [ScriptMethod(name: "P5_无尽顿悟分摊", 
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9964"],
        userControl: true)]
    public void P5_无尽顿悟分摊(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 5000) return;
        _upm.P5.分摊轮数++;
        sa.TextInfo($"分摊 #{_upm.P5.分摊轮数}", destroyMs: 4000, isWarning: true);
        sa.TTS($"分摊第{_upm.P5.分摊轮数}轮");
        var color = sa.Data.DefaultSafeColor.WithW(3);
        sa.DrawCircle(ev.TargetId, 0, 6000, $"P5_{_upm.当前阶段}_无尽顿悟分摊", 4f, color, byTime: true);
    }
    
    [ScriptMethod(name: "P5_死亡轮回死刑", 
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9962"],
        userControl: true)]
    public void P5_死亡轮回死刑(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 5000) return;
        _upm.P5.死刑轮数++;
        var destroyMs = 6500 + 1000 * _upm.P5.死刑轮数;
        sa.TextInfo($"死刑 #{_upm.P5.死刑轮数}", destroyMs: 4000, isWarning: true);
        sa.TTS($"死刑第{_upm.P5.死刑轮数}轮");
        var color = sa.GetMyIndex() <= 1 ? sa.Data.DefaultSafeColor.WithW(3) : sa.Data.DefaultDangerColor.WithW(3);
        sa.DrawCircle(ev.TargetId, 0, destroyMs, $"P5_{_upm.当前阶段}_死亡轮回死刑", 4f, color);
    }
    
    [ScriptMethod(name: "P5_百京核爆地火（起爆）", 
        eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9968"],
        userControl: true)]
    public void P5_百京核爆地火起爆(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 5000) return;
        var spos = ev.SourcePosition;
        var srot = ev.SourceRotation;
        var explodeColor = new Vector4(0f, 1f, 1f, 1.5f);
        var warnColor = new Vector4(0f, 0.5f, 1f, 1f);
        
        // 故意让第二枚告警延长一段时间，避免消失 -> 出现过于突兀
        int[] destroyMs = [4000, 4250, 6000];
        
        for (int i = 0; i < 3; i++)
        {
            var pos = spos.RotateAndExtend(spos, srot, i * 8);
            var color = i == 0 ? explodeColor : warnColor.WithW(0.8f / i);
            sa.DrawCircle(pos, 0, destroyMs[i], $"P5_百京核爆_起爆源", 6f, color, byTime: i == 0);
        }
    }
    
    [ScriptMethod(name: "P5_百京核爆地火（后续）", 
        eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(996[89])$", "TargetIndex:1"],
        userControl: true)]
    public void P5_百京核爆地火后续(Event ev, ScriptAccessory sa)
    {
        if (_upm.当前阶段 != 5000) return;
        var srot = ev.SourceRotation;
        var spos = ev.SourcePosition;
        var explodeColor = new Vector4(0f, 1f, 1f, 1.5f);
        var warnColor = new Vector4(0f, 0.5f, 1f, 1f);
        
        // 故意让第二枚告警延长一段时间，避免消失 -> 出现过于突兀
        int[] destroyMs = [1500, 1750, 3000];
        
        for (int i = 0; i < 3; i++)
        {
            var pos = spos.RotateAndExtend(spos, srot, (i + 1) * 8);
            var color = i == 0 ? explodeColor : warnColor.WithW(0.8f / i);
            sa.DrawCircle(pos, 0, destroyMs[i], $"P5_百京核爆_后续", 6f, color, byTime: i == 0);
        }
    }

    #endregion P5

}

#region 优先级字典
internal class PriorityEntry
{
    public int Key { get; set; }
    public string Name { get; set; } = "";
    public int Value { get; set; }
}

internal class PriorityDict
{
    public Dictionary<int, PriorityEntry> Entries { get; set; } = [];
    public string Annotation { get; set; } = "";
    public int ActionCount { get; set; } = 0;

    private static readonly List<string> DefaultName = ["MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4"];

    /// <summary>
    /// 索引器，直接读写对应Key的Value
    /// 若Key不存在则抛出异常
    /// </summary>
    public int this[int key]
    {
        get => Entries[key].Value;
        set => Entries[key].Value = value;
    }

    public void Init(string annotation, int entryCount = 8,
        List<string>? names = null, bool refreshActionCount = true)
    {
        Entries.Clear();

        // names 长度校验
        if (names != null && names.Count != entryCount)
            throw new ArgumentException($"names 长度({names.Count})与 entryCount ({entryCount})不一致");

        // 确定每个条目的Name
        List<string> resolvedNames;
        if (names != null)
        {
            resolvedNames = names;
        }
        else if (entryCount == DefaultName.Count)
        {
            resolvedNames = DefaultName;
        }
        else
        {
            throw new ArgumentException($"entryCount = {entryCount} 无默认 Name，请提供 names 参数");
        }

        for (var i = 0; i < entryCount; i++)
        {
            Entries.Add(i, new PriorityEntry { Key = i, Name = resolvedNames[i], Value = 0 });
        }
        Annotation = annotation;
        if (refreshActionCount)
            ActionCount = 0;
    }

    /// <summary>
    /// 为特定Key增加优先级
    /// </summary>
    /// <param name="key">key</param>
    /// <param name="priority">优先级数值</param>
    public void AddPriority(int key, int priority)
    {
        if (!Entries.TryGetValue(key, out var entry))
            throw new KeyNotFoundException($"Key {key} 不存在");
        entry.Value += priority;
    }

    /// <summary>
    /// 从Entries中找到前num个数值最小的，得到新的列表返回
    /// </summary>
    /// <param name="num"></param>
    /// <returns></returns>
    public List<PriorityEntry> SelectSmallPriorityIndices(int num)
    {
        return SelectMiddlePriorityIndices(0, num);
    }

    /// <summary>
    /// 从Entries中找到前num个数值最大的，得到新的列表返回
    /// </summary>
    /// <param name="num"></param>
    /// <returns></returns>
    public List<PriorityEntry> SelectLargePriorityIndices(int num)
    {
        return SelectMiddlePriorityIndices(0, num, true);
    }

    /// <summary>
    /// 从Entries中找到升序排列中间的数值，得到新的列表返回
    /// </summary>
    /// <param name="skip">跳过skip个元素。若从第二个开始取，skip=1</param>
    /// <param name="num">取num个元素</param>
    /// <param name="descending">降序排列，默认为false</param>
    /// <returns></returns>
    public List<PriorityEntry> SelectMiddlePriorityIndices(int skip, int num, bool descending = false)
    {
        if (Entries.Count < skip + num)
            return new List<PriorityEntry>();

        IOrderedEnumerable<PriorityEntry> sortedEntries = descending
            ? Entries.Values.OrderByDescending(e => e.Value).ThenBy(e => e.Key)
            : Entries.Values.OrderBy(e => e.Value).ThenBy(e => e.Key);

        return sortedEntries.Skip(skip).Take(num).ToList();
    }

    /// <summary>
    /// 从Entries中找到升序排列第idx位的数据，返回
    /// </summary>
    /// <param name="idx"></param>
    /// <param name="descending">降序排列，默认为false</param>
    /// <returns></returns>
    public PriorityEntry SelectSpecificPriorityIndex(int idx, bool descending = false)
    {
        if (idx < 0 || idx >= Entries.Count)
            throw new ArgumentOutOfRangeException(nameof(idx));

        IOrderedEnumerable<PriorityEntry> sortedEntries = descending
            ? Entries.Values.OrderByDescending(e => e.Value).ThenBy(e => e.Key)
            : Entries.Values.OrderBy(e => e.Value).ThenBy(e => e.Key);

        return sortedEntries.ElementAt(idx);
    }

    /// <summary>
    /// 从Entries中找到对应key的数据，得到其Value排序后位置返回
    /// </summary>
    /// <param name="key"></param>
    /// <param name="descending">降序排列，默认为false</param>
    /// <returns></returns>
    public int FindPriorityIndexOfKey(int key, bool descending = false)
    {
        if (!Entries.TryGetValue(key, out var targetEntry))
            throw new ArgumentOutOfRangeException(nameof(key));

        int count = 0;
        foreach (var pair in Entries)
        {
            if (pair.Key == key) continue;

            bool isBetter = descending
                ? pair.Value.Value > targetEntry.Value
                : pair.Value.Value < targetEntry.Value;

            if (isBetter) count++;
            else if (pair.Value.Value == targetEntry.Value && pair.Key < key)
                count++;
        }
        return count;
    }

    /// <summary>
    /// 一次性增加优先级数值
    /// 通常适用于特殊优先级（如H-T-D-H）
    /// </summary>
    /// <param name="priorities"></param>
    public void AddPriorities(List<int> priorities)
    {
        if (Entries.Count != priorities.Count)
            throw new ArgumentException("输入的列表与内部设置长度不同");

        for (var i = 0; i < Entries.Count; i++)
            AddPriority(i, priorities[i]);
    }

    /// <summary>
    /// 输出优先级字典的Key与优先级
    /// </summary>
    /// <returns></returns>
    public string ShowPriorities(bool showName = true)
    {
        var str = $"{Annotation} ({ActionCount}-th) 优先级字典：\n";
        if (Entries.Count == 0)
        {
            str += $"PriorityDict Empty.\n";
            return str;
        }
        foreach (var entry in Entries.Values)
        {
            str += $"Key {entry.Key} {(showName ? $"({entry.Name})" : "")}, Value {entry.Value}\n";
        }

        return str;
    }

    public void AddActionCount(int count = 1)
    {
        ActionCount += count;
    }

    public string ShowGroup(string name, List<PriorityEntry> entryList)
    {
        return $"{name}：{string.Join(" ", entryList.Select(x => $"({x.Name}, {x.Value})"))}";
    }
}

#endregion 优先级字典 类

#region 参数容器类
internal class UcobParams
{
    public int 当前阶段 = 0;
    public List<Vector3> 拘束器坐标 = [];
    public Vector3 拉怪位置 = Vector3.Zero;
    public int 液体地狱判定次数 = 0;

    public UcobParamsP1 P1 = new();
    public UcobParamsP2 P2 = new();
    public UcobParamsP3 P3 = new();
    public UcobParamsP4 P4 = new();
    public UcobParamsP5 P5 = new();

    public void Reset()
    {
        当前阶段 = 0;
        拘束器坐标.Clear();
        拉怪位置 = Vector3.Zero;
        液体地狱判定次数 = 0;
        P1.Reset();
        P2.Reset();
        P3.Reset();
        P4.Reset();
        P5.Reset();
    }
}

internal static class UcobExtension
{
    public static int 获得最近拘束器序列(this UcobParams upm, Vector3 spos)
    {
        int minIdx = 0;
        float minLength = 999f;
        for (int i = 0; i < upm.拘束器坐标.Count; i++)
        {
            float length = upm.拘束器坐标[i].GetLength(spos);
            if (length < minLength)
            {
                minLength = length;
                minIdx = i;
            }
        }
        return minIdx;
    }

    public static int 求解拉怪位置(this UcobParams upm)
    {
        if (upm.拘束器坐标.Count != 3) return -1;
        var rad0 = upm.拘束器坐标[0].GetRadian(UcobReborn.Center);
        var rad1 = upm.拘束器坐标[1].GetRadian(UcobReborn.Center);
        var rad = MathF.Atan2(MathF.Sin(rad0) + MathF.Sin(rad1), MathF.Cos(rad0) + MathF.Cos(rad1));
        upm.拉怪位置 = new Vector3(0, 0, 15f).RotateAndExtend(UcobReborn.Center, rad);
        return 0;
    } 
}

#region P1 Params

internal class UcobParamsP1
{
    public int 技能序号 = 0;
    public List<TwinTaniaSkills> 技能循环轴 = [TwinTaniaSkills.垂直下落, TwinTaniaSkills.旋风, TwinTaniaSkills.死刑];
    public ulong 双塔尼亚_ObjId = 0;
    public int 液体地狱循环轴判断次数 = 0;
    
    internal const uint 垂直下落 = 9896;
    internal const uint 旋风 = 9898;
    internal const uint 死刑 = 9897;
    internal const uint 液体地狱 = 9901;
    internal const uint 黑球 = 9902;
    
    public void Reset()
    {
        技能序号 = 0;
        液体地狱循环轴判断次数 = 0;
        this.获得阶段技能循环轴(0);
        双塔尼亚_ObjId = 0;
    }
}

internal enum TwinTaniaSkills
{
    垂直下落,
    旋风,
    死刑,
    液体地狱,
    液体地狱随机,
    黑球,
}

internal static class UcobP1Extension
{
    public static void 获得阶段技能循环轴(this UcobParamsP1 p1, int currentPhase)
    {
        // 根据输入的阶段返回技能循环轴
        p1.技能循环轴 = currentPhase switch
        {
            1100 =>
            [
                TwinTaniaSkills.液体地狱, TwinTaniaSkills.黑球, TwinTaniaSkills.液体地狱, TwinTaniaSkills.死刑,
                TwinTaniaSkills.黑球, TwinTaniaSkills.旋风, TwinTaniaSkills.垂直下落
            ],
            1200 =>
            [
                TwinTaniaSkills.液体地狱, TwinTaniaSkills.黑球, TwinTaniaSkills.液体地狱随机, TwinTaniaSkills.死刑,
                TwinTaniaSkills.垂直下落, TwinTaniaSkills.黑球, TwinTaniaSkills.旋风, TwinTaniaSkills.垂直下落
            ],
            _ =>
            [
                TwinTaniaSkills.垂直下落, TwinTaniaSkills.旋风, TwinTaniaSkills.死刑
            ],
        };
    }
    public static void 增加循环技能序号(this UcobParamsP1 p1)
    {
        if (p1.技能循环轴.Count == 0) return;
        p1.技能序号 = (p1.技能序号 + 1) % p1.技能循环轴.Count;
    }
}

#endregion P1 Params

#region P2 Params
internal class OuterDragon
{
    public ulong ObjectId { get; set; }
    public int Region { get; set; }
}

internal enum NaelQuoteSkills
{
    钢铁,
    月环,
    分摊,
    月华冲,
    凶鸟冲,
    陨石流,
}
internal class UcobParamsP2
{
    public ulong 奈尔_ObjId = 0;
    public List<OuterDragon> 小龙列表 = [];
    public bool 死宣一记录完毕 = false;
    public int 救世之翼序号 = 0;
    public int 贡品序号 = 0;
    public int 烈火球轮数 = 0;
    public int 烈火球受击玩家记录轮数 = 0;
    public List<int> 烈火球受击玩家 = [];
    public int 小龙点名轮数 = 0;
    public int 小龙范围已处理轮数 = 0;
    public int 小龙指路已处理轮数 = 0;
    public int 小龙返回已处理轮数 = 0;
    public List<int> 小龙俯冲引导点 = [];
    public List<int> 小龙俯冲引导玩家 = [];
    public void Reset()
    {
        奈尔_ObjId = 0;
        小龙列表.Clear();
        烈火球轮数 = 0;
        烈火球受击玩家记录轮数 = 0;
        烈火球受击玩家 = [];
        死宣参数重置();
        小龙俯冲参数重置();
    }

    public void 死宣参数重置()
    {
        死宣一记录完毕 = false;
        救世之翼序号 = 0;
        贡品序号 = 0;
    }

    public void 小龙俯冲参数重置()
    {
        小龙点名轮数 = 0;
        小龙范围已处理轮数 = 0;
        小龙指路已处理轮数 = 0;
        小龙返回已处理轮数 = 0;
        小龙俯冲引导点.Clear();
        小龙俯冲引导玩家.Clear();
    }
}

internal static class UcobP2Extension
{
    public static void 获得小龙俯冲引导点(this UcobParamsP2 p2)
    {
        if (p2.小龙列表.Count == 0) return;
        List<int> dragonRegionList = p2.小龙列表.Select(t => t.Region).ToList();
        p2.小龙俯冲引导点 = dragonRegionList switch
        {
            [0, 1, 2, 3, 4] => [11, 5, 7],
            [0, 1, 2, 3, 5] => [11, 5, 7],
            [0, 1, 2, 3, 6] => [11, 5, 7],
            [0, 1, 2, 3, 7] => [11, 5, 8],
            [0, 1, 2, 4, 5] => [2, 5, 8],
            [0, 1, 2, 4, 6] => [11, 5, 8],
            [0, 1, 2, 4, 7] => [2, 5, 8],
            [0, 1, 2, 5, 6] => [2, 5, 10],
            [0, 1, 2, 5, 7] => [11, 5, 8],
            [0, 1, 2, 6, 7] => [2, 5, 8],
            [0, 1, 3, 4, 5] => [2, 6, 9],
            [0, 1, 3, 4, 6] => [11, 3, 8],
            [0, 1, 3, 4, 7] => [2, 6, 8],
            [0, 1, 3, 5, 6] => [2, 6, 10],
            [0, 1, 3, 5, 7] => [2, 6, 9],
            [0, 1, 3, 6, 7] => [11, 3, 8],
            [0, 1, 4, 5, 6] => [2, 5, 10],
            [0, 1, 4, 5, 7] => [2, 5, 10],
            [0, 1, 4, 6, 7] => [11, 5, 8],
            [0, 1, 5, 6, 7] => [2, 6, 11],
            [0, 2, 3, 4, 5] => [2, 6, 8],
            [0, 2, 3, 4, 6] => [2, 6, 8],
            [0, 2, 3, 4, 7] => [2, 6, 8],
            [0, 2, 3, 5, 6] => [2, 6, 10],
            [0, 2, 3, 5, 7] => [2, 6, 9],
            [0, 2, 3, 6, 7] => [2, 6, 8],
            [0, 2, 4, 5, 6] => [2, 5, 10],
            [0, 2, 4, 5, 7] => [2, 5, 9],
            [0, 2, 4, 6, 7] => [2, 5, 8],
            [0, 2, 5, 6, 7] => [2, 6, 11],
            [0, 3, 4, 5, 6] => [2, 8, 10],
            [0, 3, 4, 5, 7] => [2, 8, 10],
            [0, 3, 4, 6, 7] => [2, 8, 11],
            [0, 3, 5, 6, 7] => [2, 6, 11],
            [0, 4, 5, 6, 7] => [3, 9, 11],
            [1, 2, 3, 4, 5] => [1, 6, 8],
            [1, 2, 3, 4, 6] => [1, 6, 8],
            [1, 2, 3, 4, 7] => [1, 6, 9],
            [1, 2, 3, 5, 6] => [4, 7, 10],
            [1, 2, 3, 5, 7] => [1, 6, 9],
            [1, 2, 3, 6, 7] => [4, 7, 11],
            [1, 2, 4, 5, 6] => [4, 8, 10],
            [1, 2, 4, 5, 7] => [1, 5, 9],
            [1, 2, 4, 6, 7] => [4, 8, 11],
            [1, 2, 5, 6, 7] => [11, 6, 1],
            [1, 3, 4, 5, 6] => [3, 8, 10],
            [1, 3, 4, 5, 7] => [3, 8, 10],
            [1, 3, 4, 6, 7] => [3, 8, 11],
            [1, 3, 5, 6, 7] => [3, 9, 11],
            [1, 4, 5, 6, 7] => [4, 9, 11],
            [2, 3, 4, 5, 6] => [2, 5, 10],
            [2, 3, 4, 5, 7] => [2, 7, 10],
            [2, 3, 4, 6, 7] => [2, 8, 11],
            [2, 3, 5, 6, 7] => [5, 9, 11],
            [2, 4, 5, 6, 7] => [5, 9, 11],
            [3, 4, 5, 6, 7] => [4, 9, 11],
            _ => []
        };
    }
}

#endregion P2 Params

#region P3 Params

internal enum BahamutSkills
{
    吐息,
    三连吐息,
    夷为平地,
    十亿核爆,
    无
}

internal class UcobParamsP3
{
    public ulong 巴哈_ObjId = 0;
    public ulong 奈尔_ObjId = 0;
    public ulong 双塔_ObjId = 0;
    public int 技能序号 = 0;
    public int 吐息已绘图技能序号 = -1;
    public List<BahamutSkills> 技能循环轴 = [BahamutSkills.吐息, BahamutSkills.夷为平地, BahamutSkills.无];
    public int 三连吐息判定次数 = 0;

    public Vector3 奈尔_Pos = Vector3.Zero;
    public Vector3 双塔_Pos = Vector3.Zero;
    public Vector3 巴哈_Pos = Vector3.Zero;
    
    internal const uint 风暴之翼 = 9943;
    internal const uint 吐息 = 9940;
    internal const uint 十亿核爆 = 9942;
    internal const uint 夷为平地 = 9941;

    public int 奈尔方位 = -1;
    public int 双塔方位 = -1;
    public int 巴哈方位 = -1;
    public int 奈尔记录阶段 = 0;
    public int 双塔记录阶段 = 0;
    public int 巴哈记录阶段 = 0;

    public int 灾厄台词计数 = 0;
    public int[] 灾厄对应拘束器 = [-1, -1, -1];
    
    public int[] 天地旋风方位 = [-1, -1, -1, -1, -1, -1, -1, -1];
    public List<int> 天地塔方位 = [];
    public int[] 天地踩塔方位 = [-1, -1, -1, -1, -1, -1, -1, -1];

    public List<int> 连击点名玩家 = [];
    public int[] 连击撞球截球玩家 = [-1, -1, -1, -1, -1, -1];
    public bool 连击黑球绘图完成 = false;
    public int 大地摇动搭档连线绘图版本 = 0;
    public int 大地摇动指引线绘图版本 = 0;
    public int 大地摇动指路绘图版本 = 0;
    public int 大地摇动判定次数 = 0;

    public int[] 群龙起跑方位与方向 = [-1, -1];
    public uint 塔头标偏移 = 0;
    
    public void Reset()
    {
        巴哈_ObjId = 0;
        奈尔_ObjId = 0;
        双塔_ObjId = 0;
        技能序号 = 0;
        吐息已绘图技能序号 = -1;
        三连吐息判定次数 = 0;
        this.获得阶段技能循环轴(0);

        奈尔记录阶段 = 0;
        双塔记录阶段 = 0;
        巴哈记录阶段 = 0;
        奈尔_Pos = Vector3.Zero;
        双塔_Pos = Vector3.Zero;
        巴哈_Pos = Vector3.Zero;
        this.重置Boss方位();

        灾厄台词计数 = 0;
        灾厄对应拘束器 = [-1, -1, -1];

        天地参数重置();
        连击参数重置();

        群龙起跑方位与方向 = [-1, -1];
        塔头标偏移 = 0;
    }

    public void 天地参数重置()
    {
        天地旋风方位 = [-1, -1, -1, -1, -1, -1, -1, -1];
        天地塔方位 = [];
        天地踩塔方位 = [-1, -1, -1, -1, -1, -1, -1, -1];
    }
    
    public void 连击参数重置()
    {
        连击点名玩家 = [];
        连击撞球截球玩家 = [-1, -1, -1, -1, -1, -1];
        连击黑球绘图完成 = false;
        大地摇动搭档连线绘图版本 = 0;
        大地摇动指引线绘图版本 = 0;
        大地摇动指路绘图版本 = 0;
        大地摇动判定次数 = 0;
    }
}

internal static class UcobP3Extension
{
    public static void 获得阶段技能循环轴(this UcobParamsP3 p3, int currentPhase)
    {
        // 根据输入的阶段返回技能循环轴
        p3.技能循环轴 = currentPhase switch
        {
            3000 =>
            [
                BahamutSkills.吐息, BahamutSkills.夷为平地, BahamutSkills.无
            ],
            3150 =>
            [
                BahamutSkills.吐息, BahamutSkills.夷为平地, BahamutSkills.无
            ],
            3250 =>
            [
                BahamutSkills.十亿核爆, BahamutSkills.三连吐息, BahamutSkills.无
            ],
            3350 =>
            [
                BahamutSkills.十亿核爆, BahamutSkills.吐息, BahamutSkills.夷为平地, BahamutSkills.吐息, BahamutSkills.无
            ],
            3450 =>
            [
                BahamutSkills.十亿核爆, BahamutSkills.三连吐息, BahamutSkills.无
            ],
            3550 =>
            [
                BahamutSkills.十亿核爆, BahamutSkills.夷为平地, BahamutSkills.吐息, BahamutSkills.无
            ],
            _ => 
            [
                BahamutSkills.吐息, BahamutSkills.夷为平地, BahamutSkills.无
            ],
        };
        p3.技能序号 = 0;
        p3.吐息已绘图技能序号 = -1;
    }
    public static void 增加技能序号(this UcobParamsP3 p3)
    {
        if (p3.技能循环轴.Count == 0) return;
        p3.技能序号 += 1;
    }
    
    public static int 求解天地旋风方位(this UcobParamsP3 p3)
    {
        // MT, H1 固定去基准方位逆时针 90 度（即，基准方位+2）
        // D2, D4 固定去基准方位顺时针 90 度（即，基准方位-2）
        // ST, H2 固定去奈尔方位
        // D1, D3 固定去奈尔方位对面（即，奈尔方位+4）

        p3.天地旋风方位 = [-1, -1, -1, -1, -1, -1, -1, -1];
        int[] bossDirections = [p3.巴哈方位, p3.奈尔方位, p3.双塔方位];

        if (bossDirections.Any(direction => direction is < 0 or > 7) ||
            bossDirections.Distinct().Count() != bossDirections.Length)
            return -1;

        var bossDirectionSet = new HashSet<int>(bossDirections);
        var baseDirection = -1;
        for (var idx = 0; idx < bossDirections.Length; idx++)
        {
            var candidate = bossDirections[idx];
            if (!bossDirectionSet.Contains((candidate + 7) % 8) ||
                !bossDirectionSet.Contains((candidate + 1) % 8))
                continue;

            baseDirection = candidate;
            break;
        }
        if (baseDirection == -1) return -1;

        var 基准逆 = (baseDirection + 2) % 8;
        var 基准顺 = (baseDirection + 6) % 8;
        var 奈尔对面 = (p3.奈尔方位 + 4) % 8;
        p3.天地旋风方位 =
        [
            基准逆, p3.奈尔方位, 基准逆, p3.奈尔方位,
            奈尔对面, 基准顺, 奈尔对面, 基准顺
        ];
        return 0;
    }

    public static int 求解天地踩塔方位(this UcobParamsP3 p3)
    {
        // 以奈尔方位为基准方位，
        // 从基准方位开始逆时针遇到的塔，踩塔的分别是：ST MT H1 D1 D3 D4 D2 H2
        p3.天地踩塔方位 = [-1, -1, -1, -1, -1, -1, -1, -1];

        if (p3.奈尔方位 is < 0 or > 7 ||
            p3.天地塔方位.Count != 8 ||
            p3.天地塔方位.Any(direction => direction is < 0 or > 15))
            return -1;

        var towerDirectionSet = new HashSet<int>(p3.天地塔方位);
        if (towerDirectionSet.Count != p3.天地塔方位.Count) return -1;

        var baseDirection = p3.奈尔方位 * 2 + 1;
        int[] playerOrder = [0, 2, 4, 6, 7, 5, 3, 1];
        var towerIdx = 0;
        for (var offset = 0; offset < 16 && towerIdx < playerOrder.Length; offset++)
        {
            var towerDirection = (baseDirection + offset) % 16;
            if (!towerDirectionSet.Contains(towerDirection)) continue;

            p3.天地踩塔方位[playerOrder[towerIdx]] = towerDirection;
            towerIdx++;
        }
        return 0;
    }

    public static void 求解连击撞球截球玩家(this UcobParamsP3 p3)
    {
        // 三组撞球与截球的优先级
        // H2 ST D2 D4 D3 D1 MT H1
        int[] pdGroup1 = [3, 1, 5, 7, 6, 4, 0, 2];
        // D3 D4 D1 D2 MT ST H1 H2
        int[] pdGroup2 = [6, 7, 4, 5, 0, 1, 2, 3];
        // H1 MT D1 D4 D3 D2 ST H2
        int[] pdGroup3 = [2, 0, 4, 7, 6, 5, 1, 3];

        p3.连击撞球截球玩家 = [-1, -1, -1, -1, -1, -1];
        int[][] pdGroups = [pdGroup1, pdGroup2, pdGroup3];
        var 未分配撞球玩家 = new HashSet<int>(p3.连击点名玩家);
        var 撞球玩家 = new HashSet<int>();
        var 截球玩家 = new HashSet<int>();

        for (var pIdx = 0;
            pIdx < pdGroups.Min(group => group.Length) && 撞球玩家.Count < 3;
            pIdx++)
        {
            for (var gIdx = 0; gIdx < pdGroups.Length; gIdx++)
            {
                if (p3.连击撞球截球玩家[gIdx] != -1) continue;

                var player = pdGroups[gIdx][pIdx];
                if (!未分配撞球玩家.Remove(player)) continue;

                p3.连击撞球截球玩家[gIdx] = player;
                撞球玩家.Add(player);
            }
        }

        for (var gIdx = 0; gIdx < pdGroups.Length; gIdx++)
        {
            for (var pIdx = 0; pIdx < pdGroups[gIdx].Length; pIdx++)
            {
                var player = pdGroups[gIdx][pIdx];
                if (撞球玩家.Contains(player) || !截球玩家.Add(player)) continue;

                p3.连击撞球截球玩家[gIdx + 3] = player;
                break;
            }
        }
    }

    public static int 求解群龙起跑方位与方向(this UcobParamsP3 p3)
    {
        // 根据巴哈位置、奈尔位置寻找该机制的起跑方位与跑动方向。
        // 最终赋值给 群龙起跑方位与方向。其中，index 0 代表起跑方位，index 1 的值代表方向
        // 若 index 1 为 -1，则顺时针跑动；为 1，则逆时针跑动

        // 规则如下：
        // 1. 巴哈方位若为正点（0,2,4,6，即正东南西北），逆时针跑；若为斜点，顺时针跑
        // 2. 若巴哈方位正对面（+4）不是奈尔，则起跑点在巴哈方位正对面
        // 3. 若巴哈方位正对面是奈尔，则起跑点基于奈尔方位，往跑动方向顺延 1。
        p3.群龙起跑方位与方向 = [-1, -1];

        if (p3.巴哈方位 is < 0 or > 7 || p3.奈尔方位 is < 0 or > 7)
            return -1;

        var runDirection = p3.巴哈方位 % 2 == 0 ? 1 : -1;
        var oppositeBahamut = (p3.巴哈方位 + 4) % 8;
        var startDirection = oppositeBahamut != p3.奈尔方位
            ? oppositeBahamut
            : (p3.奈尔方位 + runDirection + 8) % 8;

        p3.群龙起跑方位与方向 = [startDirection, runDirection];
        return 0;
    }

    public static void 重置Boss方位(this UcobParamsP3 p3)
    {
        p3.奈尔方位 = -1;
        p3.双塔方位 = -1;
        p3.巴哈方位 = -1;
    }
}

#endregion P3 Params

#region P4 Params

internal class UcobParamsP4
{
    public int[] 黑球撞球序列 = [-1, -1, -1];
    
    public void Reset()
    {
        黑球撞球序列 = [-1, -1, -1];
    }
}

internal static class UcobP4Extension
{
    public static int 求解黑球撞球序列(this UcobParamsP4 p4, int[] targetPlayerKeys)
    {
        p4.黑球撞球序列 = [-1, -1, -1];
        if (targetPlayerKeys is not { Length: 3 } ||
            targetPlayerKeys.Any(player => player is < 4 or > 7) ||
            targetPlayerKeys.Distinct().Count() != targetPlayerKeys.Length)
            return -1;

        foreach (var player in targetPlayerKeys.Where(player => player != 7))
            p4.黑球撞球序列[player - 4] = player;

        if (targetPlayerKeys.Contains(7))
        {
            var unassignedBall = -1;
            for (var i = 0; i < p4.黑球撞球序列.Length; i++)
            {
                if (p4.黑球撞球序列[i] != -1) continue;
                unassignedBall = i;
                break;
            }
            if (unassignedBall == -1) return -1;
            p4.黑球撞球序列[unassignedBall] = 7;
        }

        return 0;
    }
}

#endregion P4 Params

#region P5 Params

internal class UcobParamsP5
{
    public int 分摊轮数 = 0;
    public int 死刑轮数 = 0;
    
    public void Reset()
    {
        分摊轮数 = 0;
        死刑轮数 = 0;
    }
}

internal static class UcobP5Extension
{
}

#endregion P5 Params

#endregion 参数容器类

#region 函数集
internal static class EventExtensions
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

    public static uint Id0(this Event ev)
    {
        return ParseHexId(ev["Id"], out var id) ? id : 0;
    }

    public static uint DurationMilliseconds(this Event ev)
    {
        return JsonConvert.DeserializeObject<uint>(ev["DurationMilliseconds"]);
    }

    public static uint DataId(this Event ev)
    {
        return JsonConvert.DeserializeObject<uint>(ev["DataId"]);
    }
    
    public static uint SourceDataId(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["SourceDataId"]);
    }
}

internal static class IbcHelper
{
    public static IGameObject? GetById(this ScriptAccessory sa, ulong gameObjectId)
    {
        return sa.Data.Objects.SearchById(gameObjectId);
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
}
#region 计算函数

internal static class MathTools
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

    public static string ToStr(this Vector3 point, int digits = 2)
        => $"({point.X.ToString($"F{digits}")}, {point.Y.ToString($"F{digits}")}, {point.Z.ToString($"F{digits}")})";

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
    /// 获取给定整数的指定位数
    /// </summary>
    /// <param name="val">给定数值</param>
    /// <param name="x">对应位数，个位为0</param>
    /// <returns>返回指定位的数字，如果x超出范围返回0</returns>
    public static int GetDecimalDigit(this int val, int x)
        => (int)(Math.Abs(val) / Math.Pow(10, x) % 10);
    
}

#endregion 计算函数

#region 位置序列函数
internal static class IndexHelper
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

    public static bool IsValidPartyIndex(this ScriptAccessory sa, int index)
    {
        return index >= 0 && index < sa.Data.PartyList.Count;
    }

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

internal static class DrawTools
{
    private static CancellationTokenSource _lifecycleCts = new();

    public static void ResetLifecycle()
    {
        _lifecycleCts.Cancel();
        _lifecycleCts.Dispose();
        _lifecycleCts = new CancellationTokenSource();
    }

    public static async void TextInfo(this ScriptAccessory sa, string msg,
        int delayMs = 0, int destroyMs = 3000, bool isWarning = false)
    {
        var token = _lifecycleCts.Token;
        try
        {
            await Task.Delay(delayMs, token);
            sa.Method.TextInfo(msg, destroyMs, isWarning);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public static async void TTS(this ScriptAccessory sa, string msg,
        int delayMs = 0, int voiceSpeed = 3)
    {
        var token = _lifecycleCts.Token;
        try
        {
            await Task.Delay(delayMs, token);
            sa.Method.TTS(msg, voiceSpeed);
        }
        catch (OperationCanceledException)
        {
        }
    }

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
    /// <param name="color">使用颜色</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="byY">动画效果随距离变更</param>
    /// <param name="draw">是否直接绘图</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawOwnerBase(this ScriptAccessory sa, 
        object ownerObj, object targetObj, int delay, int destroy, string name, 
        float radian, float rotation, float width, float length, float innerWidth, float innerLength,
        DrawModeEnum drawModeEnum, DrawTypeEnum drawTypeEnum, Vector4 color,
        bool byTime = false, bool byY = false, bool draw = true)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.InnerScale = new Vector2(innerWidth, innerLength);
        dp.Radian = radian;
        dp.Rotation = rotation;
        dp.Color = color;
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
    /// <param name="color">使用颜色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawGuidance(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name,
        Vector4 color, float rotation = 0, float width = 1f, bool draw = true, bool useImgui = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 0, rotation, width,
            width, 0, 0, useImgui ? DrawModeEnum.Imgui : DrawModeEnum.Default, 
            DrawTypeEnum.Displacement, color, false, true, draw);
    
    public static DrawPropertiesEdit DrawGuidance(this ScriptAccessory sa,
        object targetObj, int delay, int destroy, string name, Vector4 color, float rotation = 0, float width = 1f,
        bool draw = true, bool useImgui = true)
        => sa.DrawGuidance((ulong)sa.Data.Me, targetObj, delay, destroy, name, color, rotation, width, draw, useImgui);

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
    /// <param name="color">使用颜色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawCircle(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name,
        float scale, Vector4 color, bool byTime = false, bool draw = true, bool useImgui = false)
        => sa.DrawOwnerBase(ownerObj, 0, delay, destroy, name, 2 * float.Pi, 0, scale, scale,
            0, 0, useImgui ? DrawModeEnum.Imgui : DrawModeEnum.Default, DrawTypeEnum.Circle, color, byTime,false, draw);

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
    /// <param name="color">使用颜色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawDonut(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name,
        float outScale, float innerScale, Vector4 color, bool byTime = false, bool draw = true, bool useImgui = false)
        => sa.DrawOwnerBase(ownerObj, 0, delay, destroy, name, 2 * float.Pi, 0, outScale, outScale, innerScale,
            innerScale, useImgui ? DrawModeEnum.Imgui : DrawModeEnum.Default, 
            DrawTypeEnum.Donut, color, byTime, false, draw);
    
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
    /// <param name="color">使用颜色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawFan(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name, float radian, float rotation,
        float outScale, float innerScale, Vector4 color, bool byTime = false, bool draw = true, bool useImgui = false)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, radian, rotation, outScale, outScale, innerScale,
            innerScale, useImgui ? DrawModeEnum.Imgui : DrawModeEnum.Default, 
            innerScale == 0 ? DrawTypeEnum.Fan : DrawTypeEnum.Donut, color, byTime, false, draw);

    public static DrawPropertiesEdit DrawFan(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name, float radian, float rotation,
        float outScale, float innerScale, Vector4 color, bool byTime = false, bool draw = true, bool useImgui = false)
        => sa.DrawFan(ownerObj, 0, delay, destroy, name, radian, rotation, outScale, innerScale, 
            color, byTime, draw, useImgui);

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
    /// <param name="color">使用颜色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawRect(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name, float rotation,
        float width, float length, Vector4 color, bool byTime = false, bool byY = false, 
        bool draw = true, bool useImgui = false)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 0, rotation, width, length, 0, 0,
            useImgui ? DrawModeEnum.Imgui : DrawModeEnum.Default, DrawTypeEnum.Rect, color, byTime, byY, draw);
    
    public static DrawPropertiesEdit DrawRect(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name, float rotation,
        float width, float length, Vector4 color, bool byTime = false, bool byY = false, 
        bool draw = true, bool useImgui = false)
        => sa.DrawRect(ownerObj, 0, delay, destroy, name, rotation, width, length, color, byTime, byY, draw, useImgui);
    
    /// <summary>
    /// 返回击退绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="targetObj">击退源</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="width">箭头宽</param>
    /// <param name="length">箭头长</param>
    /// <param name="color">使用颜色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawKnockBack(this ScriptAccessory sa,
        object targetObj, int delay, int destroy, string name, float width, float length,
        Vector4 color, bool draw = true, bool useImgui = false)
        => sa.DrawOwnerBase(sa.Data.Me, targetObj, delay, destroy, name, 0, float.Pi, width, length, 0, 0,
            useImgui ? DrawModeEnum.Imgui : DrawModeEnum.Default, DrawTypeEnum.Displacement, color, false, false, draw);

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
    /// <param name="color">使用颜色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawLine(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name, float rotation,
        float width, float length, Vector4 color, bool byTime = false, bool byY = false, 
        bool draw = true, bool useImgui = false)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 1, rotation, width, length, 0, 0,
            useImgui ? DrawModeEnum.Imgui : DrawModeEnum.Default, DrawTypeEnum.Line, color, byTime, byY, draw);

    /// <summary>
    /// 返回箭头绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">箭头起始</param>
    /// <param name="targetObj">箭头目标</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="rotation">旋转角度</param>
    /// <param name="width">箭头宽度</param>
    /// <param name="length">箭头长度</param>
    /// <param name="byTime">是否随时间扩充</param>
    /// <param name="byY">是否随距离扩充</param>
    /// <param name="color">使用颜色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawArrow(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name, float rotation,
        float width, float length, Vector4 color, 
        bool byTime = false, bool byY = false, bool draw = true, bool useImgui = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 1, rotation, width, length, 0, 0,
            useImgui ? DrawModeEnum.Imgui : DrawModeEnum.Default, DrawTypeEnum.Arrow, color, byTime, byY, draw);

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
    /// <param name="color">使用颜色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawConnection(this ScriptAccessory sa, object ownerObj, object targetObj,
        int delay, int destroy, string name, Vector4 color, float width = 1f, bool draw = true, bool useImgui = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 0, 0, width, width,
            0, 0, useImgui ? DrawModeEnum.Imgui : DrawModeEnum.Default, DrawTypeEnum.Line, color, false, true, draw);

    /// <summary>
    /// 赋予输入的dp以仇恨顺序绘图
    /// </summary>
    /// <param name="self"></param>
    /// <param name="setOwner">获得目标赋值给owner</param>
    /// <param name="orderIdx">仇恨顺序，从1开始</param>
    /// <returns></returns>
    public static DrawPropertiesEdit SetEnmityOrder(this DrawPropertiesEdit self, bool setOwner, uint orderIdx)
    {
        if (setOwner)
        {
            self.CentreResolvePattern = PositionResolvePatternEnum.OwnerEnmityOrder;
            self.CentreOrderIndex = orderIdx;
        }
        else
        {
            self.TargetResolvePattern = PositionResolvePatternEnum.OwnerEnmityOrder;
            self.TargetOrderIndex = orderIdx;
        }

        return self;
    }
    
    /// <summary>
    /// 赋予输入的dp以owner目标为源的绘图
    /// </summary>
    /// <param name="self"></param>
    /// <param name="setOwner">获得目标赋值给owner</param>
    /// <returns></returns>
    public static DrawPropertiesEdit SetOwnerTarget(this DrawPropertiesEdit self, bool setOwner)
    {
        if (setOwner)
            self.CentreResolvePattern = PositionResolvePatternEnum.OwnerTarget;
        else
            self.TargetResolvePattern = PositionResolvePatternEnum.OwnerTarget;
        return self;
    }
    
    /// <summary>
    /// 在指定位置添加Omen
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="position">生成位置</param>
    /// <param name="omenId">omenID</param>
    /// <param name="delayMs">延时</param>
    /// <param name="destroyMs">消失时间</param>
    /// <param name="omenScale">omen缩放倍数</param>
    /// <param name="color">omen颜色</param>
    /// <param name="rotation">omen旋转弧度</param>
    /// <param name="speed">播放速度</param>
    /// <returns></returns>
    public static void DrawOmen(this ScriptAccessory sa, Vector3 position, uint omenId,
        int delayMs, int destroyMs, Vector3 omenScale, Vector4? color = null, float rotation = 0f, float speed = 1f)
    {
        Task.Delay(Math.Max(50, delayMs)).ContinueWith(t =>
        {
            var handle = sa.Method.VfxMethod.CreateOmen(omenId,
                omenScale, position, rotation,
                color ?? Vector4.One, destroyAt: destroyMs);
            sa.Method.RunOnMainThreadAsync(() =>
            {
                sa.Method.VfxMethod.SetVfxSpeed(handle, speed);
            });
        });
    }
    
    /// <summary>
    /// 在实体上添加Lockon
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="objectId">实体</param>
    /// <param name="lockonId">头标ID</param>
    /// <param name="delayMs">延时</param>
    /// <param name="destroyMs">消失时间</param>
    /// <param name="iconScale">头标缩放倍数</param>
    /// <param name="speed">播放速度</param>
    /// <returns></returns>
    public static void DrawLockOn(this ScriptAccessory sa, ulong objectId, uint lockonId,
        int delayMs, int destroyMs, Vector3 iconScale, float speed = 1)
    {
        Task.Delay(Math.Max(50, delayMs)).ContinueWith(t =>
        {
            var handle = sa.Method.VfxMethod.CreateLockOn(lockonId, objectId, Vector4.One, destroyAt: destroyMs,
                (ref Vector4 color, ref Vector4 pos, ref Vector3 scale) =>
                {
                    scale = iconScale;
                });
            sa.Method.RunOnMainThreadAsync(() =>
            {
                sa.Method.VfxMethod.SetVfxSpeed(handle, speed);
            });
        });
    }

    /// <summary>
    /// 在指定位置添加Lockon
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="position">生成位置</param>
    /// <param name="lockonId">头标ID</param>
    /// <param name="delayMs">延时</param>
    /// <param name="destroyMs">消失时间</param>
    /// <param name="objIdBias">生成虚拟实体ID偏置</param>
    /// <param name="iconScale">头标缩放倍数</param>
    /// <param name="speed">播放速度</param>
    /// <returns></returns>
    public static void DrawLockOn(this ScriptAccessory sa, Vector3 position, uint lockonId,
        int delayMs, int destroyMs, Vector3 iconScale, uint objIdBias = 0, float speed = 1)
    {
        var virtualObjectId = 0x40001234u + objIdBias;
        var objHandle = sa.Method.ObjectMethod.CreateEmptyChara(virtualObjectId);

        Task.Delay(Math.Max(50, delayMs)).ContinueWith(t =>
        {
            var handle = sa.Method.VfxMethod.CreateLockOn(lockonId, virtualObjectId, Vector4.One, destroyAt: destroyMs,
                (ref Vector4 color, ref Vector4 pos, ref Vector3 scale) =>
                {
                    scale = iconScale;
                    unsafe
                    {
                        var obj = (BattleChara*)objHandle;
                        obj->Position = position;
                    }
                });
            sa.Method.RunOnMainThreadAsync(() =>
            {
                sa.Method.VfxMethod.SetVfxSpeed(handle, speed);
            });
            Task.Delay(destroyMs).ContinueWith(t => sa.Method.ObjectMethod.DestoryChara(objHandle));
        });
    }

    public static void DrawCountDown(this ScriptAccessory sa, ulong objectId,
        int delayMs, float iconScale = 2f, float speed = 1)
        => sa.DrawLockOn(objectId, 184, delayMs, (int)(6000f / speed), new Vector3(iconScale, iconScale, iconScale),
            speed);

    public static void DrawCountDown(this ScriptAccessory sa, Vector3 position, 
        int delayMs, float iconScale = 2f, uint objIdBias = 0, float speed = 1)
        => sa.DrawLockOn(position, 184, delayMs, (int)(6000f / speed), new Vector3(iconScale, iconScale, iconScale), objIdBias, speed);

    public static void DrawLaser(this ScriptAccessory sa, Vector3 position, 
        int delayMs, int destroyMs, Vector3 omenScale, Vector4? color = null)
        => sa.DrawOmen(position, 358, delayMs, destroyMs, omenScale, color ?? Vector4.One);
}

#endregion 绘图函数

#region 调试函数

/// <summary>
/// 调试输出函数集，所有输出经由有序缓冲区，按 order 优先级统一排列后输出。
/// </summary>
internal static class DebugFunction
{
    private static readonly List<string> PrefixWhiteList = [""];
    private static readonly List<string> PrefixBlackList = [""];
    
    /// <summary>
    /// 有序输出缓冲区的单条记录。
    /// </summary>
    private record Entry(int Order, long Seq, string Content, bool ShowInChatBox);

    private static readonly List<Entry> _buffer = new();
    private static long _seq;
    private static CancellationTokenSource? _cts;
    private static readonly object _lock = new();
    
    public static void DebugMsg(this ScriptAccessory sa, string msg,
        int order = 0,
        [CallerMemberName] string prefix = "",
        bool showInChatBox = true, bool enableWhiteList = false, bool enableBlackList = true,
        int flushDelayMs = 200, bool immediate = false)
    {
        if (!UcobReborn.Debugging) return;
        
        if (enableWhiteList)
            if (!PrefixWhiteList.Contains(prefix)) return;
        if (enableBlackList)
            if (PrefixBlackList.Contains(prefix)) return;

        var content = $"[{prefix}] {msg}";

        if (immediate)
        {
            sa.Log.Debug(content);
            if (showInChatBox)
                sa.Method.SendChat($"/e {content}");
            return;
        }
        
        lock (_lock)
        {
            _buffer.Add(new Entry(order, _seq++, content, showInChatBox));

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            Task.Delay(flushDelayMs, token).ContinueWith(t =>
            {
                if (t.IsCanceled) return;
                FlushInternal(sa);
            });
        }
    }

    public static void FlushDebugMsg(this ScriptAccessory sa)
    {
        FlushInternal(sa);
    }

    public static void ClearDebugMsg(this ScriptAccessory sa)
    {
        lock (_lock)
        {
            _cts?.Cancel();
            _buffer.Clear();
        }
    }

    private async static void FlushInternal(ScriptAccessory sa)
    {
        List<Entry> sorted;
        lock (_lock)
        {
            if (_buffer.Count == 0) return;
            sorted = _buffer.OrderBy(x => x.Order).ThenBy(x => x.Seq).ToList();
            _buffer.Clear();
        }

        foreach (var entry in sorted)
        {
            sa.Log.Debug(entry.Content);
            if (entry.ShowInChatBox)
                sa.Method.SendChat($"/e {entry.Content}");
            await Task.Delay(20);
        }
    }
}

#endregion 调试函数

#region 特殊函数

internal static class SpecialFunction
{
    public static unsafe void AlphaModify(this ScriptAccessory sa, IGameObject? obj, float alpha,
        Func<float, bool>? shouldModify = null)
    {
        alpha = Math.Clamp(alpha, 0f, 1f);
        sa.Method.RunOnMainThreadAsync(Action);
        void Action()
        {
            if (obj == null) return;
            
            Character* charaStruct = (Character*)obj.Address;
            if (!obj.IsValid() || !charaStruct->IsReadyToDraw())
            {
                sa.Log.Error($"传入的IGameObject不合法。");
                return;
            }
            
            if (!charaStruct->IsCharacter())
            {
                sa.Log.Error($"传入的IGameObject不是Character，无法修改透明度。");
                return;
            }
            
            if (shouldModify != null && !shouldModify(charaStruct -> Alpha))
                return;

            charaStruct->Alpha = alpha;
            sa.DebugMsg($"AlphaModify => {obj.Name.TextValue} | {obj} => {alpha}");
        }
    }

    public static unsafe void Redraw(this ScriptAccessory sa, IGameObject? obj)
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
            
            charaStruct->DisableDraw();
            charaStruct->EnableDraw();
        }
    }
}

#endregion 特殊函数

#endregion 函数集

