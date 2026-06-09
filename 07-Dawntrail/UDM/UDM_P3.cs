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
using System.Reflection;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using System.Linq;
using System.ComponentModel;
using System.Globalization;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace UsamisKodakku.Scripts._07_DawnTrail.UDM_P3;

[ScriptType(name: Name, territorys: [1363], guid: "c902dd56-629e-4033-a77f-fb23b8d94745",
    version: Version, author: "Usami", note: NoteStr, updateInfo: UpdateInfo)]

public class UDM_P3
{
    const string NoteStr =
        $"""
        {Version}
        一运
        - 正攻：相同水晶，卡奥斯场中，真空波按Buff->职能站位
        - TLB_相反水晶_盗火文档：相反水晶，卡奥斯风水晶，真空波按小队身份站位，TLB吃两层风分摊
        - TLB_纯固定_NoCCHH：无视水晶，近战贴 Boss，远程远离于两水晶分组
        - TLB_拉火水晶_夜音：拉火水晶6人集合，只有火 Buff 在外

        二运未完成，只有黑洞指挥模式
        - 麻将1：攻123
        - 麻将2：锁123
        - 麻将3：禁12
        - 标点优先级 H > D > T

        特殊方法为屏蔽艾克斯迪司释放钢铁暴雷时的连线
        """;
    
    const string UpdateInfo =
        $"""
        {Version}
        1. 修复 TLB_拉火水晶_夜音 策略下 火Buff 指路以风水晶为圆心向内分散的问题，应为火水晶
        """;

    private const string Name = "绝妖星乱舞_P3";
    private const string Version = "0.0.0.12";
    private const string DebugVersion = "a";
    private int _runId = 0;

    private const bool Debugging = false;

    private static readonly List<string> Role = ["MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4"];
    private static readonly Vector3 Center = new Vector3(100, 0, 100);
    private static readonly Vector3 CrystalBasePos = new Vector3(109.9f, 0, 109.9f);
    private UDMP3Params _udmP3Param = new UDMP3Params();
    private PriorityDict _pd = new PriorityDict();

    [UserSetting("启用方法设置中带*的特殊功能")]
    public bool SpecialMode { get; set; } = false;
    
    [UserSetting("P3A1 - 一运深层痛楚策略")]
    public static BoAStgEnum BoAStg { get; set; } = BoAStgEnum.TLB_纯固定_NoCCHH;
    public enum BoAStgEnum
    {
        // TLB_火炸人群_OverClock,
        TLB_拉火水晶_夜音,
        TLB_纯固定_NoCCHH,
        TLB_相反水晶_盗火文档,
        正攻,
    }

    [UserSetting("P3A1 - 拉艾克斯迪司的是 MT")]
    public bool ExDeathMT { get; set; } = false;
    
    [UserSetting("P3B1 - 二运黑洞指挥模式")]
    public bool P3B1CaptainMode { get; set; } = false;
    
    public void Init(ScriptAccessory sa)
    {
        _runId++;
        sa.Log.Debug($"脚本 {Name} v{Version}{DebugVersion} 完成初始化，_runId {_runId}");
        _udmP3Param.Reset(sa);
        sa.Method.RemoveDraw(".*");
        sa.Method.MarkClear();
        sa.Method.ClearFrameworkUpdateAction(this);
    }
    
    [ScriptMethod(name: "———————— 《测试项》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void 测试项分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "测试项：打印当前阶段", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void 打印当前阶段(Event ev, ScriptAccessory sa)
    {
        sa.DebugMsg($"{_udmP3Param.当前阶段}", Debugging);
    }
    
    [ScriptMethod(name: "测试项：展示优先级表格", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void 展示优先级表格(Event ev, ScriptAccessory sa) =>
        sa.DebugMsg(_pd.ShowPriorities(), Debugging);

    [ScriptMethod(name: "测试项：展示一运状态", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void 展示一运状态(Event ev, ScriptAccessory sa)
    {
        _udmP3Param.打印水晶与分组(sa, _pd);
        sa.DebugMsg($"{_udmP3Param.当前轮为火()}", Debugging);
    }

    [ScriptMethod(name: "测试项：初始化参数与本地标点", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void 初始化参数与本地标点(Event ev, ScriptAccessory sa)
    {
        _udmP3Param.Reset(sa);
        sa.MarkClear(local: true);
    }

    [ScriptMethod(name: "测试项：一运赋值", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void 一运赋值(Event ev, ScriptAccessory sa)
    {
        _pd.Init(sa, "P3一运");
        _udmP3Param.当前阶段 = 3100;
        
        _pd.AddPriorities([21, 112, 13, 214, 15, 26, 127, 228]);
        _udmP3Param.是长火 = true;
        _udmP3Param.无水晶方位 = 0;
        _udmP3Param.水水晶方位 = 3;
        _udmP3Param.火水晶方位 = 1;
        _udmP3Param.风水晶方位 = 2;
        _udmP3Param.成员分组(sa, _pd);

        _udmP3Param.ObjectId_卡奥斯 = sa.GetByDataId(19508u).First().GameObjectId;
        _udmP3Param.ObjectId_艾克斯迪司 = sa.GetByDataId(19509u).First().GameObjectId;
        
        sa.DebugMsg($"[测试项：一运赋值] 赋值完毕", Debugging);
    }
    
    [ScriptMethod(name: "测试项：究极冲击波赋值", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void 究极冲击波赋值(Event ev, ScriptAccessory sa)
    {
        _udmP3Param.当前阶段 = 3111;
        _udmP3Param.究极冲击波起始方位 = 6;
        _udmP3Param.究极冲击波为顺时针 = false;
        _udmP3Param.究极冲击波记录完毕 = true;
        _pd.AddPriorities([7000, 8000, 2000, 1000, 4000, 3000, 5000, 6000]);
        sa.DebugMsg($"[测试项：究极冲击波赋值] 赋值完毕", Debugging);
    }
    
    [ScriptMethod(name: "测试项：测试TTS", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void 测试TTS(Event ev, ScriptAccessory sa)
    {
        var needBack = "背对";
        sa.Method.TextInfo($"{needBack} 吃火月环", 3000);
        sa.Method.TTS($"{needBack}吃火月环", 2);
    }
    
    [ScriptMethod(name: "测试项：画近远钢铁月环", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void 画近远钢铁月环(Event ev, ScriptAccessory sa)
    {
        DrawNearCircle(sa, sa.Data.DefaultDangerColor, 2000);
        DrawNearDonut(sa, sa.Data.DefaultDangerColor, 2000);
    }
    
    [ScriptMethod(name: "测试项：二运赋值", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void 二运赋值(Event ev, ScriptAccessory sa)
    {
        _pd.Init(sa, "P3二运");
        _udmP3Param.当前阶段 = 3200;

        _pd.AddPriorities([37, 28, 21, 12, 33, 24, 15, 16]);
        _udmP3Param.ObjectId_卡奥斯 = sa.GetByDataId(19508u).First().GameObjectId;
        _udmP3Param.ObjectId_艾克斯迪司 = sa.GetByDataId(19509u).First().GameObjectId;
        
        sa.DebugMsg($"[测试项：二运赋值] 赋值完毕", Debugging);
    }
        
    
    [ScriptMethod(name: "———————— 《P3》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void P3_分割线(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "P3_分P_重构", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47842"],
        userControl: Debugging)]
    public void P3_分P_重构(Event ev, ScriptAccessory sa)
    {
        _udmP3Param.当前阶段 = 3000;
        sa.Log.Debug($"当前阶段为：P3 重构 {_udmP3Param.当前阶段}");
    }

    #region P3A 深层痛楚 通用部分

    [ScriptMethod(name: "=============《P3A 深层痛楚》=============", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3A_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P3A_一运分P_深层痛楚", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:47858"],
        userControl: Debugging)]
    public void P3A_一运分P_深层痛楚(Event ev, ScriptAccessory sa)
    {
        _pd.Init(sa, "P3一运");
        _pd.AddPriorities([1, 2, 3, 4, 5, 6, 7, 8]);
        _udmP3Param.当前阶段 = 3100;
        sa.DebugMsg($"当前阶段为：P3 一运 深层痛楚 {_udmP3Param.当前阶段}", Debugging);
    }

    
    [ScriptMethod(name: "P3A_状态添加记录", eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:regex:^(160[0123])$", "SourceId:E0000000"],
        userControl: Debugging)]
    public void P3A_状态添加(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3100) return;
        const uint 混沌之炎 = 1600;
        const uint 混沌之水 = 1601;
        const uint 混沌之风 = 1602;
        const uint 混沌之逆风 = 1603;
        
        var statusId = ev.StatusId;
        var playerIdx = sa.GetPlayerIdIndex((uint)ev.TargetId);
        var priVal = ev.StatusId switch
        {
            混沌之炎 => 100,
            混沌之水 => 200,
            混沌之风 => 10,
            混沌之逆风 => 20,
            _ => 0
        };
        
        _pd.AddPriority(playerIdx, priVal);
        _udmP3Param.一运状态记录次数++;
        sa.DebugMsg($"[P3A_状态添加记录] 记录到状态 {statusId} / {priVal} 于角色 {Role[playerIdx]}", Debugging);

        if (_udmP3Param.一运状态记录完毕())
            _udmP3Param.一运状态记录.Set();
        
        if (statusId != 混沌之炎) return;
        var statusTime = ev.DurationMilliseconds();
        _udmP3Param.是长火 = statusTime > 30000;
    }

    [ScriptMethod(name: "P3A_水晶生成记录", eventType: EventTypeEnum.ObjectChanged,
        eventCondition: ["Operate:Add", "DataId:regex:^(201529[012])$"],
        userControl: Debugging)]
    public void P3A_水晶生成记录(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3100) return;
        const uint 火水晶 = 2015290;
        const uint 水水晶 = 2015291;
        const uint 风水晶 = 2015292;
        
        var region = ev.SourcePosition.GetRadian(Center).RadianToRegion(4);
        var dataId = ev.DataId();

        switch (dataId)
        {
            case 火水晶:
                _udmP3Param.火水晶方位 = region;
                break;
            case 水水晶:
                _udmP3Param.水水晶方位 = region;
                break;
            case 风水晶:
                _udmP3Param.风水晶方位 = region;
                break;
        }
        sa.DebugMsg($"[P3A_水晶生成记录] 记录到水晶 {dataId} 于方位 {region}", Debugging);

        if (!_udmP3Param.水晶状态记录完毕()) return;
        _udmP3Param.无水晶方位 = 6 - _udmP3Param.火水晶方位 - _udmP3Param.水水晶方位 - _udmP3Param.风水晶方位;
        _udmP3Param.一运水晶记录.Set();
    }
    
    [ScriptMethod(name: "P3A_暴雷死刑范围", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47881"], userControl: true)]
    public void P3A_暴雷死刑范围(Event ev, ScriptAccessory sa)
    {
        var dp = sa.DrawCircle(ev.SourceId, 0, 5000, $"靠近死刑", 5f, sa.Data.DefaultDangerColor.WithW(2f), draw: false);
        dp.SetOwnersDistanceOrder(true, 1);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }
    
    [ScriptMethod(name: "P3A_暴雷死刑通用提示", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47881"], userControl: true)]
    public void P3A_暴雷死刑通用提示(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 <= 3101) return;
        sa.Method.TextInfo($"艾克斯迪司 靠近死刑", 3000);
        sa.Method.TTS($"艾克斯迪司 靠近死刑", 3);
    }
    
    [ScriptMethod(name: "P3A_经纬聚爆", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47869|47870)$"], userControl: true)]
    public void P3A_经纬聚爆(Event ev, ScriptAccessory sa)
    {
        const uint 纬度聚爆 = 47870;    // 先打左右
        const uint 经度聚爆 = 47869;    // 先打上下
        bool 先打左右 = ev.ActionId == 纬度聚爆;
        _udmP3Param.ObjectId_卡奥斯 = ev.SourceId;
        
        // 第一轮
        sa.DrawFan(ev.SourceId, 0, 5000, $"第一轮前左", 90f.DegToRad(), (先打左右 ? 90f : 0f).DegToRad(),
            60, 0, sa.Data.DefaultDangerColor.WithW(2f));
        sa.DrawFan(ev.SourceId, 0, 5000, $"第一轮后右", 90f.DegToRad(), (先打左右 ? -90f : 180f).DegToRad(),
            60, 0, sa.Data.DefaultDangerColor.WithW(2f));
        
        // 第二轮
        sa.DrawFan(ev.SourceId, 5000, 2500, $"第二轮前左", 90f.DegToRad(), (!先打左右 ? 90f : 0f).DegToRad(),
            60, 0, sa.Data.DefaultDangerColor.WithW(2f));
        sa.DrawFan(ev.SourceId, 5000, 2500, $"第二轮后右", 90f.DegToRad(), (!先打左右 ? -90f : 180f).DegToRad(),
            60, 0, sa.Data.DefaultDangerColor.WithW(2f));
    }

    #endregion P3A 深层痛楚 通用部分
    
    #region P3A1 一水火 3100
    
    [ScriptMethod(name: "———————— 《P3A1 一水火》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3A1_一水火分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P3A_暴雷钢铁", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47890"], userControl: true)]
    public void P3A_暴雷钢铁(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3100) return;
        _udmP3Param.ObjectId_艾克斯迪司 = ev.SourceId;
        sa.DrawCircle(ev.SourceId, 0, 7000, $"P3A_暴雷钢铁 暴雷钢铁", 14.8f, sa.Data.DefaultDangerColor.WithW(2f), byTime: true);
    }
    
    [ScriptMethod(name: "*P3A_移除暴雷连线", eventType: EventTypeEnum.VfxEvent,
        eventCondition: ["Type:Channeling", "Id:64"], userControl: true)]
    public void P3A_移除暴雷连线(Event ev, ScriptAccessory sa)
    {
        if (!SpecialMode) return;
        if (!ev.TryParseVfxHandle(out var handleId)) return;
        sa.Method.VfxMethod.RemoveVfx(handleId, VfxType.Channeling);
    }

    [ScriptMethod(name: "P3A_深层痛楚判定", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:47858"], suppress: 10000,
        userControl: Debugging)]
    public void P3A_深层痛楚判定(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3100) return;
        if (!_udmP3Param.一运状态记录.WaitOne(2000)) return;
        sa.DebugMsg($"[P3A_深层痛楚判定] 一运状态记录完毕", Debugging);
        if (!_udmP3Param.一运水晶记录.WaitOne(2000)) return;
        sa.DebugMsg($"[P3A_深层痛楚判定] 一运水晶记录完毕", Debugging);

        _udmP3Param.一运状态记录.Reset();
        _udmP3Param.一运水晶记录.Reset();

        _udmP3Param.成员分组(sa, _pd);
        sa.DebugMsg($"[P3A_深层痛楚判定] 成员分组完毕", Debugging);

        _udmP3Param.一水火准备.Set();
    }

    [ScriptMethod(name: "P3A_第一轮水火范围", eventType: EventTypeEnum.ActionEffect,
    eventCondition: ["ActionId:47858"], suppress: 10000,
    userControl: true)]
    public async void P3A_第一轮水火范围(Event ev, ScriptAccessory sa)
    {
        var runId = _runId;

        if (_udmP3Param.当前阶段 != 3100) return;
        await Task.Delay(8000);
        if (runId != _runId) return;
        if (_udmP3Param.当前阶段 != 3100) return;

        DrawWaterFireRange(sa, _udmP3Param.一水火指路与绘图时间);
    }

    [ScriptMethod(name: "P3A_一水火指路", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:47858"], suppress: 10000,
        userControl: true)]
    public async void P3A_一水火指路(Event ev, ScriptAccessory sa)
    {
        var runId = _runId;
        if (_udmP3Param.当前阶段 != 3100) return;
        if (!_udmP3Param.一水火准备.WaitOne(2000)) return;

        sa.DebugMsg($"[P3A_一水火指路] 开始", Debugging);
        var myIndex = sa.GetMyIndex();

        // 艾克斯迪司 ST 无水晶
        if (myIndex == (ExDeathMT ? 0 : 1))
        {
            var aimRegion = BoAStg switch
            {
                BoAStgEnum.TLB_拉火水晶_夜音 => _udmP3Param.水水晶方位,
                _ => _udmP3Param.无水晶方位
            };
            执行P3拉怪指路逻辑(sa, "艾克斯迪司", aimRegion, 6500, false);
        }
            
        // 卡奥斯 MT 正攻场中，逃课风水晶
        else if (myIndex == (ExDeathMT ? 1 : 0))
        {
            var toCenter = BoAStg == BoAStgEnum.正攻;
            var aimRegion = BoAStg switch
            {
                BoAStgEnum.TLB_拉火水晶_夜音 => _udmP3Param.火水晶方位,
                _ => _udmP3Param.风水晶方位
            };
            执行P3拉怪指路逻辑(sa, "卡奥斯", aimRegion, 6500, toCenter);
        }

        await Task.Delay(8000);
        if (runId != _runId) return;
        
        执行水火指路逻辑(sa, _udmP3Param.一水火指路与绘图时间);
        _udmP3Param.一水火准备.Reset();
    }



    [ScriptMethod(name: "P3A_第二轮水火范围", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47869|47870)$"], suppress: 10000, userControl: true)]
    public void P3A_第二轮水火范围(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3102 && _udmP3Param.当前阶段 != 3110) return;
        DrawWaterFireRange(sa, _udmP3Param.二水火指路与绘图时间);
    }

    private void DrawWaterFireRange(ScriptAccessory sa, int destroyTime)
    {
        // 通用绘图范围
        if (_udmP3Param.当前轮为火())
        {
            DrawGroupCircle(sa, _udmP3Param.火组, "DrawWaterFireRange 火组", sa.Data.DefaultDangerColor, 5, destroyTime);
            DrawNearDonut(sa, sa.Data.DefaultDangerColor, destroyTime + 3000);
        }
        else
        {
            DrawGroupDonut(sa, _udmP3Param.水组, "DrawWaterFireRange 水组", sa.Data.DefaultDangerColor, destroyTime);
            DrawNearCircle(sa, sa.Data.DefaultDangerColor, destroyTime + 3000);
        }
    }

    private void 执行P3拉怪指路逻辑(ScriptAccessory sa, string bossStr, int region, int destroyTime, bool toCenter = false)
    {
        string regionStr;
        Vector3 guidePos;

        if (toCenter)
        {
            guidePos = Center;
            regionStr = "场中";
        }
        else
        {
            guidePos = _udmP3Param.基于水晶旋转(CrystalBasePos, region);
            regionStr = region switch
            {
                0 => "右下",
                1 => "右上",
                2 => "左上",
                _ => "左下"
            };
        }

        sa.DrawGuidance(guidePos, 0, destroyTime, $"执行P3拉怪指路逻辑 {bossStr}", sa.Data.DefaultSafeColor);
        sa.Method.TextInfo($"将 {bossStr} 拉到{regionStr}", 3000);
        sa.Method.TTS($"将{bossStr}拉到{regionStr}", 3);
    }

    private void 执行水火指路逻辑(ScriptAccessory sa, int destroyTime)
    {
        var myIndex = sa.GetMyIndex();
        var myPriVal = _pd.Priorities[myIndex];

        var windIndex = _udmP3Param.查询组序(_udmP3Param.风组, myIndex);
        var waterIndex = _udmP3Param.查询组序(_udmP3Param.水组, myIndex);
        var fireIndex = _udmP3Param.查询组序(_udmP3Param.火组, myIndex);

        switch (BoAStg)
        {
            case BoAStgEnum.正攻:
                if (windIndex >= 0)
                    DrawWindGuideAroundCrystal_Wf(sa, windIndex, destroyTime);
                if (waterIndex >= 0)
                    DrawWaterGuideAlongCrystal_Wf(sa, waterIndex, myPriVal, destroyTime);
                if (fireIndex >= 0)
                    DrawFireGuideAlongCrystal_Wf(sa, fireIndex, myPriVal, destroyTime);
                break;

            case BoAStgEnum.TLB_相反水晶_盗火文档:
                if (windIndex >= 0)
                    DrawWindGuideAroundCrystal_Wf(sa, windIndex, destroyTime);
                if (waterIndex >= 0)
                {
                    DrawToCrystal(sa, _udmP3Param.火水晶方位, destroyTime, $"水组去火");
                    sa.Method.TextInfo($"靠近火水晶，集合放月环", 6000);
                    sa.Method.TTS($"靠近火水晶 集合放月环", 3);
                }
                    
                if (fireIndex >= 0)
                {
                    DrawToCrystal(sa, _udmP3Param.水水晶方位, destroyTime, $"火组去水");
                    sa.Method.TextInfo($"靠近水水晶，分散放钢铁", 6000);
                    sa.Method.TTS($"靠近水水晶 分散放钢铁", 3);
                }
                break;

            case BoAStgEnum.TLB_纯固定_NoCCHH:
                DrawGuide_CCHH_Wf(sa, destroyTime);
                DrawGuideLine_CCHH_Wf(sa, destroyTime);
                break;

            case BoAStgEnum.TLB_拉火水晶_夜音:
                if (fireIndex >= 0)
                    DrawFireGuide_YY_Wf(sa, fireIndex, destroyTime);
                else
                {
                    DrawToCrystal(sa, _udmP3Param.火水晶方位, destroyTime, $"风水去水");
                    sa.Method.TextInfo($"原地处理月环", 6000);
                    sa.Method.TTS($"原地处理月环", 3);
                }
                break;

            // case BoAStgEnum.TLB_火炸人群_OverClock:
            //     break;
        }
    }

    private void DrawFireGuide_YY_Wf(ScriptAccessory sa, int fireIndex, int destroyTime)
    {
        float angleBias = fireIndex switch
        {
            0 => -45f,
            1 => 45f,
        };
        var basePos = CrystalBasePos.RotateAndExtend(Center, 0, -10f);
        var guidePos = basePos.RotateAndExtend(CrystalBasePos, angleBias.DegToRad());

        if (_udmP3Param.当前轮为火())
        {
            sa.Method.TextInfo($"远离 放火钢铁", 6000);
            sa.Method.TTS($"远离放火钢铁", 3);
        }
        else
        {
            sa.Method.TextInfo($"远离 吃水钢铁", 6000);
            sa.Method.TTS($"远离吃水钢铁", 3);
        }

        guidePos = _udmP3Param.基于水晶旋转(guidePos, _udmP3Param.火水晶方位);
        sa.DrawGuidance(guidePos, 0, destroyTime, $"DrawFireGuide_YY_Wf 指路", sa.Data.DefaultSafeColor);

    }

    private void DrawGuide_CCHH_Wf(ScriptAccessory sa, int destroyTime)
    {
        var myIndex = sa.GetMyIndex();
        float angleBias = myIndex switch
        {
            0 => -135f,
            1 => 135f,
            4 => -45f,
            5 => 45f,
            2 => -30f,
            3 => 30f,
            6 => -75f,
            7 => 75f
        };
        bool isMelee = (myIndex % 4) <= 1;
        var basePos = CrystalBasePos.RotateAndExtend(Center, 0, isMelee ? -4f : -14.5f);
        var guidePos = basePos.RotateAndExtend(CrystalBasePos, angleBias.DegToRad());

        // 检查近战组是否有水 Buff，且该轮为水，则站脚下等月环
        if (!_udmP3Param.当前轮为火() && _udmP3Param.水组有近战(sa) && isMelee)
        {
            guidePos = CrystalBasePos;
            sa.Method.TextInfo($"集合放月环", 6000);
            sa.Method.TTS($"集合放月环", 3);
        }
        else
        {
            sa.Method.TextInfo($"避开钢铁月环", 6000);
            sa.Method.TTS($"避开钢铁月环", 3);
        }

        guidePos = _udmP3Param.基于水晶旋转(guidePos, _udmP3Param.风水晶方位);
        sa.DrawGuidance(guidePos, 0, destroyTime, $"DrawGuide_CCHH_Wf 指路", sa.Data.DefaultSafeColor);
    }

    private void DrawGuideLine_CCHH_Wf(ScriptAccessory sa, int destroyTime)
    {
        var myIndex = sa.GetMyIndex();
        bool isMelee = (myIndex % 4) <= 1;
        var windCrystalPos = _udmP3Param.基于水晶旋转(CrystalBasePos, _udmP3Param.风水晶方位);
        var baseRadian = 180f.DegToRad() + windCrystalPos.GetRadian(Center);
        if (isMelee)
        {
            List<float> biasDeg = [-135, 135, -45, 45];
            for (int i = 0; i < 4; i++)
            {
                var dp = sa.DrawLine(windCrystalPos, 0, 0, destroyTime, $"DrawStaticGuideLine 指引线{i}",
                    baseRadian + biasDeg[i].DegToRad(), 20f, 50f, sa.Data.DefaultSafeColor, draw: false);
                dp.Color = i switch
                {
                    0 or 1 => new Vector4(0.1f, 0.1f, 1f, 1),   // Tank
                    _ => new Vector4(1, 0.1f, 0.1f, 1),         // DPS
                };
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Line, dp);
            }
        }
        else
        {
            List<float> biasDeg = [-30, 30, -75, 75];
            for (int i = 0; i < 4; i++)
            {
                var dp = sa.DrawLine(windCrystalPos, 0, 0, destroyTime, $"DrawStaticGuideLine 指引线{i}",
                    baseRadian + biasDeg[i].DegToRad(), 20f, 50f, sa.Data.DefaultSafeColor, draw: false);
                dp.Color = i switch
                {
                    0 or 1 => new Vector4(0.1f, 1f, 0.1f, 1),   // Healer
                    _ => new Vector4(1, 0.1f, 0.1f, 1),         // DPS
                };
                sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Line, dp);
            }
        }
    }

    private void DrawWindGuideAroundCrystal_Wf(ScriptAccessory sa, int windIndex, int destroyTime)
    {
        List<float> angleBias = [-30, -15, 15, 30];
        var guidePos = CrystalBasePos.RotateAndExtend(Center, angleBias[windIndex].DegToRad());
        guidePos = _udmP3Param.基于水晶旋转(guidePos, _udmP3Param.风水晶方位);
        sa.DrawGuidance(guidePos, 0, destroyTime, $"DrawWindGuideByBuffAndIndex_Wf 风组指路", sa.Data.DefaultSafeColor);
        var dir = windIndex >= 2 ? "右" : "左";
        sa.Method.TextInfo(BoAStg == BoAStgEnum.正攻 ? $"面向场中，前往 风水晶{dir} 吃分摊" : $"前往 风水晶", 6000);
        sa.Method.TTS(BoAStg == BoAStgEnum.正攻 ? $"前往 风水晶{dir} 吃分摊" : $"前往风水晶", 3);
    }

    private void DrawWaterGuideAlongCrystal_Wf(ScriptAccessory sa, int waterIndex, int myPriVal, int destroyTime)
    {
        Vector3 guidePos;
        if (_udmP3Param.当前轮为火())
        {
            guidePos = Center;
            DrawCrystalSightAvoid(sa, _udmP3Param.火水晶方位, myPriVal, destroyTime);
            // DrawGroupDonut(sa, _udmP3Param.火组, "DrawWaterGuideAlongCrystal_Wf 火组", sa.Data.DefaultSafeColor, destroyTime + 3000);
            guidePos = _udmP3Param.基于水晶旋转(guidePos, _udmP3Param.水水晶方位);

            var needBackStr = _udmP3Param.需要背对(myPriVal) ? "背对" : "面向";
            sa.Method.TextInfo($"前往场中 {needBackStr}火水晶 吃火", 6000);
            sa.Method.TTS($"前往场中{needBackStr}火水晶吃火", 3);
        }
        else
        {
            List<float> distanceBias = [-10, 4];
            guidePos = CrystalBasePos.RotateAndExtend(Center, 0, distanceBias[waterIndex]);
            // DrawGroupDonut(sa, _udmP3Param.水组, "DrawWaterGuideAlongCrystal_Wf 水组", sa.Data.DefaultDangerColor, destroyTime);
            // DrawGroupCircle(sa, _udmP3Param.水组, "DrawWaterGuideAlongCrystal_Wf 水组", sa.Data.DefaultSafeColor, 5, destroyTime + 3000);
            guidePos = _udmP3Param.基于水晶旋转(guidePos, _udmP3Param.水水晶方位);

            var dir = waterIndex >= 1 ? "外" : "内";
            sa.Method.TextInfo($"前往 水水晶{dir} 准备判定", 6000);
            sa.Method.TTS($"前往 水水晶{dir} 准备判定", 3);
        }

        sa.DrawGuidance(guidePos, 0, destroyTime, $"DrawWaterGuideAlongCrystal_Wf 水组指路", sa.Data.DefaultSafeColor);
    }

    private void DrawFireGuideAlongCrystal_Wf(ScriptAccessory sa, int fireIndex, int myPriVal, int destroyTime)
    {
        Vector3 guidePos;
        if (_udmP3Param.当前轮为火())
        {
            List<float> distanceBias = [-10, 4];
            guidePos = CrystalBasePos.RotateAndExtend(Center, 0, distanceBias[fireIndex]);
            // DrawGroupDonut(sa, _udmP3Param.火组, "DrawFireGuideAlongCrystal_Wf 火组", sa.Data.DefaultSafeColor, destroyTime + 3000);
            // DrawGroupCircle(sa, _udmP3Param.火组, "DrawFireGuideAlongCrystal_Wf 火组", sa.Data.DefaultDangerColor, 5, destroyTime);
            guidePos = _udmP3Param.基于水晶旋转(guidePos, _udmP3Param.火水晶方位);

            var dir = fireIndex >= 1 ? "外" : "内";
            sa.Method.TextInfo($"前往 火水晶{dir} 准备判定", 6000);
            sa.Method.TTS($"前往 火水晶{dir} 准备判定", 3);

        }
        else
        {
            guidePos = Center;
            DrawCrystalSightAvoid(sa, _udmP3Param.水水晶方位, myPriVal, destroyTime);
            // DrawGroupCircle(sa, _udmP3Param.水组, "DrawFireGuideAlongCrystal_Wf 水组", sa.Data.DefaultSafeColor, 5, destroyTime + 3000);
            guidePos = _udmP3Param.基于水晶旋转(guidePos, _udmP3Param.火水晶方位);

            var needBackStr = _udmP3Param.需要背对(myPriVal) ? "背对" : "面向";
            sa.Method.TextInfo($"前往场中 {needBackStr}水水晶 吃火", 6000);
            sa.Method.TTS($"前往场中{needBackStr}水水晶吃火", 3);
        }
        
        sa.DrawGuidance(guidePos, 0, destroyTime, $"DrawFireGuideWf 火组指路", sa.Data.DefaultSafeColor);
    }

    private void DrawToCrystal(ScriptAccessory sa, int crystalRegion, int destroyTime, string name)
    {
        var guidePos = _udmP3Param.基于水晶旋转(CrystalBasePos, crystalRegion);
        sa.DrawGuidance(guidePos, 0, destroyTime, $"DrawToCrystal {name}", sa.Data.DefaultSafeColor);
    }

    private void DrawCrystalSightAvoid(ScriptAccessory sa, int crystalRegion, int myPriVal, int destroyTime)
    {
        var needBack = _udmP3Param.需要背对(myPriVal);
        var sightRegion = crystalRegion + (needBack ? 0 : 2);
        DrawFacingArrow(sa, sightRegion, false, $"DrawCrystalSightAvoid 面向辅助自身", destroyTime);
        DrawFacingArrow(sa, sightRegion, true, $"DrawCrystalSightAvoid 面向辅助正确面向", destroyTime);
    }
    
    void DrawFacingArrow(ScriptAccessory sa, int dir, bool isSupport, string name, int destroyTime)
    {
        var dp = sa.DrawLine(sa.Data.Me, 0, 0, destroyTime, name,
            isSupport ? (45f + dir * 90f).DegToRad() : 0, 1f, 4.5f,
            isSupport ? sa.Data.DefaultSafeColor : sa.Data.DefaultDangerColor, draw: false);
        dp.FixRotation = isSupport;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dp);
    }

    private void DrawGroupCircle(
        ScriptAccessory sa, List<KeyValuePair<int, int>> group, string name, Vector4 color, float scale, int destroyTime)
    {
        for (var i = 0; i < group.Count; i++)
            sa.DrawCircle(sa.Data.PartyList[group[i].Key], 0, destroyTime, $"{name}钢铁{i + 1}", scale, color);
    }

    private void DrawGroupDonut(
        ScriptAccessory sa, List<KeyValuePair<int, int>> group, string name, Vector4 color, int destroyTime)
    {
        for (var i = 0; i < group.Count; i++)
            sa.DrawDonut(sa.Data.PartyList[group[i].Key], 0, destroyTime, $"{name}月环{i + 1}", 10, 5, color);
    }

    private void DrawNearCircle(ScriptAccessory sa, Vector4 color, int destroyTime)
    {
        var crystalPos = new Vector3(109.9f, 0, 109.9f);
        crystalPos = _udmP3Param.基于水晶旋转(crystalPos, _udmP3Param.水水晶方位);

        for (int i = 0; i < 2; i++)
        {
            var dp = sa.DrawCircle(crystalPos, 0, destroyTime, $"DrawNearCircle 水水晶钢铁{i + 1}", 5f, color, draw: false);
            dp.SetOwnersDistanceOrder(true, (uint)(i + 1));
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    private void DrawNearDonut(ScriptAccessory sa, Vector4 color, int destroyTime)
    {
        var crystalPos = new Vector3(109.9f, 0, 109.9f);
        crystalPos = _udmP3Param.基于水晶旋转(crystalPos, _udmP3Param.火水晶方位);
        
        for (int i = 0; i < 2; i++)
        {
            var dp = sa.DrawDonut(crystalPos, 0, destroyTime, $"DrawNearCircle 火水晶月环{i + 1}", 10f, 5f, color, draw: false);
            dp.SetOwnersDistanceOrder(true, (uint)(i + 1));
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
        }
    }

    
    #endregion P3A1 一水火 3100
    
    #region P3A1 暴雷死刑 3101 3102
    
    [ScriptMethod(name: "P3A_暴雷一死刑提示", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:47881"], userControl: true)]
    public void P3A_暴雷一死刑提示(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3100) return;
        _udmP3Param.当前阶段 = 3101;
        sa.DebugMsg($"[P3A_暴雷死刑] 暴雷死刑，一水火结束");
        
        if (sa.GetMyIndex() != (ExDeathMT ? 0 : 1)) return;
        sa.DrawGuidance(ev.TargetId, 0, 5000, $"P3A_暴雷_死刑", sa.Data.DefaultSafeColor);
        sa.Method.TextInfo($"前往 艾克斯迪司 脚下吃死刑", 3000);
        sa.Method.TTS($"前往艾克斯迪司脚下吃死刑", 3);
    }
    
    [ScriptMethod(name: "P3A_暴雷死刑后_拉Boss提示", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:47884"], suppress: 10000, userControl: true)]
    public void P3A_暴雷死刑后_拉Boss提示(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3101) return;
        _udmP3Param.当前阶段 = 3102;


        // 非正攻情况下，死刑后，艾克斯迪司回人群
        if (sa.GetMyIndex() == (ExDeathMT ? 0 : 1))
        {
            if (BoAStg == BoAStgEnum.正攻) return;
            var aimRegion = BoAStg switch
            {
                BoAStgEnum.TLB_拉火水晶_夜音 => _udmP3Param.火水晶方位,
                _ => _udmP3Param.风水晶方位
            };
            执行P3拉怪指路逻辑(sa, "艾克斯迪司", aimRegion, 4000, false);
            sa.DebugMsg($"[P3A_暴雷死刑后_拉Boss提示] 暴雷死刑结束，ST 拉艾克斯迪司");
        }

        // 正攻情况下，死刑后，卡奥斯去场中
        if (sa.GetMyIndex() == (ExDeathMT ? 1 : 0))
        {
            if (BoAStg != BoAStgEnum.正攻) return;
            执行P3拉怪指路逻辑(sa, "卡奥斯", 0, 4000, true);
            sa.DebugMsg($"[P3A_暴雷死刑后_拉Boss提示] 暴雷死刑结束，MT 拉卡奥斯去场中");
        }
    }
    
    #endregion P3A1 暴雷死刑 3101 3102

    #region P3A1 二水火 3110
    
    [ScriptMethod(name: "———————— 《P3A1 二水火》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3A1_二水火分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P3A_二水火指路", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47869|47870)$"], suppress: 10000, userControl: true)]
    public void P3A_二水火指路(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3102) return;
        _udmP3Param.当前阶段 = 3110;
        sa.DebugMsg($"[P3A_二水火指路] 开始", Debugging);
        执行水火指路逻辑(sa, _udmP3Param.二水火指路与绘图时间);
    }

    [ScriptMethod(name: "P3A_二水火结束拉怪提示", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(4786[01])$"], suppress: 10000, userControl: true)]
    public void P3A_二水火结束(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3110) return;
        _udmP3Param.当前阶段 = 3111;
        
        /*
         * 混沌之水月环 混沌之水 47862
         * 混沌之水引发的钢铁 海啸 47861
         * 混沌之火钢铁 混沌之炎 47859
         * 混沌之火引发的月环 烈焰 47860
         * 混沌之风引发的分摊 龙卷风 47864
         */
        
        // 根据海啸/烈焰与当前阶段判断二水火的结束
        sa.DebugMsg($"[P3A_二水火结束拉怪提示] 二水火结束，转阶段 {_udmP3Param.当前阶段}", Debugging);

        var myIndex = sa.GetMyIndex();
        if (myIndex <= 1)
            执行二水火后拉怪指路逻辑(sa, true);
        else if (myIndex == 6)
            执行二水火后引导指路逻辑(sa);
        else
            执行二水火后拉怪指路逻辑(sa, false);
    }
    
    private void 执行二水火后拉怪指路逻辑(ScriptAccessory sa, bool isTank)
    {
        var aimRegion = BoAStg switch
        {
            BoAStgEnum.TLB_拉火水晶_夜音 => _udmP3Param.火水晶方位,
            _ => _udmP3Param.风水晶方位
        };
        var guidePos = _udmP3Param.基于水晶旋转(CrystalBasePos, aimRegion);

        sa.DrawGuidance(guidePos, 0, 6500, $"执行二水火后拉怪指路逻辑 拉怪", sa.Data.DefaultSafeColor);
        var dirStr = aimRegion switch
        {
            0 => "右下",
            1 => "右上",
            2 => "左上",
            _ => "左下"
        };
        sa.Method.TextInfo(isTank ? $"将 双BOSS 拉到{dirStr}" : $"于 {dirStr} 集合", 3000);
        sa.Method.TTS(isTank ? $"将双BOSS拉到{dirStr}" : $"于 {dirStr} 集合", 3);
    }
    
    private void 执行二水火后引导指路逻辑(ScriptAccessory sa)
    {
        var aimRegion = BoAStg switch
        {
            // 火对面是水
            BoAStgEnum.TLB_拉火水晶_夜音 => _udmP3Param.水水晶方位,
            // 风对面是无
            _ => _udmP3Param.无水晶方位
        };
        
        var guidePos = _udmP3Param.基于水晶旋转(CrystalBasePos, aimRegion);
        sa.DrawGuidance(guidePos, 0, 6500, $"执行二水火后引导指路逻辑 引导", sa.Data.DefaultSafeColor);
        var dirStr = aimRegion switch
        {
            0 => "右下",
            1 => "右上",
            2 => "左上",
            _ => "左下"
        };
        sa.Method.TextInfo($"去{dirStr} 引导超级跳", 3000);
        sa.Method.TTS($"去{dirStr}引导超级跳", 3);
    }
    
    #endregion P3A1 二水火 3110
    
    #region P3A1 真空波 3111
    
    [ScriptMethod(name: "———————— 《P3A1 真空波》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3A1_真空波分割线(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "P3A_真空波", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47891)$"], userControl: true)]
    public void P3A_真空波(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3111) return;
        sa.DebugMsg($"[P3A_真空波] 开始", Debugging);

        if (BoAStg == BoAStgEnum.正攻)
            执行真空波正攻指路逻辑(sa);
        else
            // 其余逃课都是执行固定站位逻辑
            执行真空波逃课指路逻辑(sa);
    }

    [ScriptMethod(name: "P3A_真空波指引线", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(47891)$"], userControl: true)]
    public void P3A_真空波指引线(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3111) return;
        DrawStaticGuideLineVw(sa, _udmP3Param.真空波指路时间);
    }
    
    [ScriptMethod(name: "P3A_真空波后分摊", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(47891)$"], suppress: 10000, userControl: true)]
    public void P3A_真空波后分摊(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3111) return;
        sa.DebugMsg($"[P3A_真空波] 击退后分摊", Debugging);
        
        if (BoAStg == BoAStgEnum.正攻)
            DrawGroupCircle(sa, _udmP3Param.风组, "P3A_真空波后分摊", sa.Data.DefaultSafeColor, 6, 4000);
        else
        {
            var myIndex = sa.GetMyIndex();
            List<int> partner = [2, 3, 0, 1];
            for (var i = 0; i < 4; i++)
                sa.DrawCircle(sa.Data.PartyList[i + (myIndex < 4 ? 4 : 0)], 0, 4000, $"真空波分摊 {i}", 6,
                    i == partner[myIndex % 4] ? sa.Data.DefaultSafeColor : sa.Data.DefaultDangerColor);
        }
    }
    
    private void 执行真空波正攻指路逻辑(ScriptAccessory sa)
    {
        var myIndex = sa.GetMyIndex();
        var myPriVal = _pd.Priorities[myIndex];

        // 水组火组站定，风组靠近等待击退
        var windIndex = _udmP3Param.查询组序(_udmP3Param.风组, myIndex);
        if (windIndex >= 0)
        {
            DrawWindGuideVw(sa, windIndex, myPriVal, _udmP3Param.真空波指路时间);
            return;
        }
        
        var waterIndex = _udmP3Param.查询组序(_udmP3Param.水组, myIndex);
        if (waterIndex >= 0)
        {
            DrawWaterFireGuideVw(sa, waterIndex, _udmP3Param.真空波指路时间, true);
            return;
        }
        
        var fireIndex = _udmP3Param.查询组序(_udmP3Param.火组, myIndex);
        if (fireIndex >= 0)
        {
            DrawWaterFireGuideVw(sa, fireIndex, _udmP3Param.真空波指路时间, false);
            return;
        }
    }

    private void DrawWindGuideVw(ScriptAccessory sa, int windIndex, int myPriVal, int destroyTime)
    {
        List<float> angleBias = [60, 20, -20, -60];
        var basePos = CrystalBasePos.RotateAndExtend(Center, 0, -1f);
        var guidePos = basePos.RotateAndExtend(CrystalBasePos, angleBias[windIndex].DegToRad(), 4f);
        
        guidePos = _udmP3Param.基于水晶旋转(guidePos, _udmP3Param.风水晶方位);
        sa.DrawGuidance(guidePos, 0, destroyTime, $"DrawWindGuideVw 风组指路", sa.Data.DefaultSafeColor);

        var needBack = _udmP3Param.需要背对(myPriVal);
        var needBackStr = needBack ? "背对" : "面向";
        
        DrawFacingArrow(sa, 0, false, $"DrawWindGuideVw 面向辅助自身", destroyTime);
        var dp = sa.DrawLine(sa.Data.Me, _udmP3Param.ObjectId_艾克斯迪司, 0, destroyTime, $"DrawWindGuideVw 与艾克斯迪司连线",
            (needBack ? 0 : 180f).DegToRad(), 1, 4.5f, sa.Data.DefaultSafeColor, draw: false);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Arrow, dp);
        
        sa.Method.TextInfo($"目标圈内 {needBackStr} 艾克斯迪司 击退", 4000);
        sa.Method.TTS($"{needBackStr}Boss击退", 3);
    }
    
    private void DrawWaterFireGuideVw(ScriptAccessory sa, int groupIndex, int destroyTime, bool isWater)
    {
        List<float> angleBias = [20, 60];
        var basePos = CrystalBasePos.RotateAndExtend(Center, 0, -1f);
        var guidePos = basePos.RotateAndExtend(CrystalBasePos, (isWater ? -1 : 1) * angleBias[groupIndex].DegToRad(), 10f);
        guidePos = _udmP3Param.基于水晶旋转(guidePos, _udmP3Param.风水晶方位);
        sa.DrawGuidance(guidePos, 0, destroyTime, $"DrawWaterFireGuideVw 水组指路", sa.Data.DefaultSafeColor);
        
        sa.Method.TextInfo($"防击退，然后与搭档分摊", 4000);
        sa.Method.TTS($"防击退，然后与搭档分摊", 3);
    }

    private void 执行真空波逃课指路逻辑(ScriptAccessory sa)
    {
        var myIndex = sa.GetMyIndex();

        // 根据 role 固定站位
        List<float> angleBias = [-60, 60, -20, 20, -20, 20, -60, 60];
        Vector3 bossPos = sa.GetById(_udmP3Param.ObjectId_艾克斯迪司).Position;
        var basePos = bossPos.RotateAndExtend(Center, 0, -3.8f);
        var guidePos = basePos.RotateAndExtend(bossPos, angleBias[myIndex].DegToRad());
        sa.DrawGuidance(guidePos, 0, _udmP3Param.真空波指路时间, $"执行真空波相反水晶指路逻辑 指路", sa.Data.DefaultSafeColor);

        // 面向指示
        var myPriVal = _pd.Priorities[myIndex];
        var needBack = _udmP3Param.需要背对(myPriVal);
        var needBackStr = needBack ? "背对" : "面向";
        DrawFacingArrow(sa, 0, false, $"执行真空波相反水晶指路逻辑 面向辅助自身", _udmP3Param.真空波指路时间);

        var dp = sa.DrawLine(sa.Data.Me, _udmP3Param.ObjectId_艾克斯迪司, 0, _udmP3Param.真空波指路时间, $"执行真空波相反水晶指路逻辑 面向辅助正确面向",
            needBack ? 180f.DegToRad() : 0, 1f, 4.5f, sa.Data.DefaultSafeColor, draw: false);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dp);

        sa.Method.TextInfo($"目标圈内 {needBackStr} 艾克斯迪司 击退", 4000);
        sa.Method.TTS($"{needBackStr}Boss击退", 3);
    }
    
    private void DrawStaticGuideLineVw(ScriptAccessory sa, int destroyTime)
    {
        var myIndex = sa.GetMyIndex();
        Vector3 bossPos = sa.GetById(_udmP3Param.ObjectId_艾克斯迪司).Position;
        var baseRadian = 180f.DegToRad() + bossPos.GetRadian(Center);
        
        List<float> biasDeg = [-20, 20, -60, 60];
        List<int> partner = [2, 3, 0, 1];
        for (int i = 0; i < 4; i++)
        {
            var dp = sa.DrawLine(bossPos, 0, 0, destroyTime, $"DrawStaticGuideLineVw 指引线{i}",
                baseRadian + biasDeg[i].DegToRad(), 20f, 50f, sa.Data.DefaultSafeColor, draw: false);
            dp.Color = i == partner[myIndex % 4] ? sa.Data.DefaultSafeColor : sa.Data.DefaultDangerColor;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Line, dp);
        }
    }

    #endregion P3A1 真空波 3111

    #region P3A1 究极冲击波 3111
    
    [ScriptMethod(name: "———————— 《P3A1 究极冲击波》 ————————", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3A1_究极冲击波分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P3A_究极冲击波方位记录", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(47843)$"], suppress: 500, userControl: Debugging)]
    public void P3A_究极冲击波方位记录(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3111) return;
        if (_udmP3Param.究极冲击波记录完毕) return;
        var region = ev.SourceRotation.RadianToRegion(8, 0, true);
        if (_udmP3Param.究极冲击波起始方位 < 0)
        {
            _udmP3Param.究极冲击波起始方位 = region;
            sa.DebugMsg($"[P3A_究极冲击波方位记录] 起始方位 {region}", Debugging);
        }
        else
        {
            sa.DebugMsg($"[P3A_究极冲击波方位记录] 第二轮方位 {region}", Debugging);
            
            int delta = (region - _udmP3Param.究极冲击波起始方位 + 8) % 8;
            // 7 为顺时针，1 为逆时针
            _udmP3Param.究极冲击波为顺时针 = delta == 7;
            _udmP3Param.究极冲击波记录完毕 = true;
            
            // 用面向计算的起始方位可直接作为终点，顺逆时针需要转换
            sa.DebugMsg($"[P3A_究极冲击波方位记录] 为 {(_udmP3Param.究极冲击波为顺时针 ? "顺" : "逆")} 时针", Debugging);
        }
    }
    
    [ScriptMethod(name: "P3A_究极冲击波麻将记录", eventType: EventTypeEnum.TargetIcon,
        eventCondition: ["Id:regex:^(015[0123]|01B[5678])$"], userControl: Debugging)]
    public void P3A_究极冲击波麻将记录(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3111) return;
        var priVal = ev.Id0() switch
        {
            0x0150 => 1000,
            0x0151 => 2000,
            0x0152 => 3000,
            0x0153 => 4000,
            0x01B5 => 5000,
            0x01B6 => 6000,
            0x01B7 => 7000,
            _ => 8000,
        };
        var playerIdx = sa.GetPlayerIdIndex((uint)ev.TargetId);
        _pd.AddPriority(playerIdx, priVal);
        sa.DebugMsg($"[P3A_究极冲击波麻将记录] {Role[playerIdx]} 被点 {priVal / 1000}", Debugging);
    }
    
    [ScriptMethod(name: "P3A_究极冲击波指路", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:47864"], suppress: 1000, userControl: true)]
    public void P3A_究极冲击波指路(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3111) return;
        sa.DebugMsg($"[P3A_究极冲击波指路] 风分摊结束，从方位 {_udmP3Param.究极冲击波起始方位} {(_udmP3Param.究极冲击波为顺时针 ? "逆" : "顺")} 开始", Debugging);
        var priVal = _pd.Priorities[sa.GetMyIndex()];
        var num = priVal.GetDecimalDigit(3);
        sa.DebugMsg($"[P3A_究极冲击波指路] 玩家被点麻将 {priVal.GetDecimalDigit(3)}", Debugging);
        if (num == 0) return;

        // 顺则逆转
        var baseRotDeg = _udmP3Param.究极冲击波起始方位 * 45f + 22.5f * (_udmP3Param.究极冲击波为顺时针 ? 1 : -1);
        var basePos = new Vector3(100, 0, 119.5f).RotateAndExtend(Center,
            baseRotDeg.DegToRad());
        var guidePos = basePos.RotateAndExtend(Center, 45f.DegToRad() * (num - 1) * (_udmP3Param.究极冲击波为顺时针 ? 1 : -1));
        sa.DrawGuidance(guidePos, 0, 12000, $"P3A_究极冲击波指路", sa.Data.DefaultSafeColor);
    }
    
    #endregion P3A1 究极冲击波 3111

    #region P3B 地震 通用部分

    [ScriptMethod(name: "=============《P3B 地震》=============", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3B_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P3B_二运分P_地震", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:50545"],
        userControl: Debugging)]
    public void P3A_二运分P_地震(Event ev, ScriptAccessory sa)
    {
        _pd.Init(sa, "P3二运");
        // 尽可能让坦克不参与第一轮黑洞
        _pd.AddPriorities([7, 8, 3, 4, 5, 6, 7, 8]);
        _udmP3Param.当前阶段 = 3200;
        sa.Method.MarkClear();
        sa.DebugMsg($"当前阶段为：P3 二运 地震 {_udmP3Param.当前阶段}", Debugging);
    }

    #endregion P3B 地震 通用部分
    
    #region P3B1 黑洞 3200
    
    [ScriptMethod(name: "=============《P3B1 黑洞》=============", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void P3B1_分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P3B1_状态添加记录", eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:regex:^(300[456])$", "SourceId:E0000000"],
        userControl: Debugging)]
    public void P3B_状态添加(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3200) return;
        const uint 第一目标 = 3004;
        const uint 第二目标 = 3005;
        const uint 第三目标 = 3006;
        
        var statusId = ev.StatusId;
        var playerIdx = sa.GetPlayerIdIndex((uint)ev.TargetId);
        var priVal = ev.StatusId switch
        {
            第一目标 => 10,
            第二目标 => 20,
            第三目标 => 30,
            _ => 0
        };
        
        _pd.AddPriority(playerIdx, priVal);
        _udmP3Param.二运状态记录次数++;
        sa.DebugMsg($"[P3A_状态添加记录] 记录到状态 {statusId} / {priVal} 于角色 {Role[playerIdx]}", Debugging);

        if (_udmP3Param.二运状态记录完毕())
            _udmP3Param.二运状态记录.Set();
    }

    [ScriptMethod(name: "P3B1_二运黑洞指挥标点", eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:regex:^(300[456])$", "SourceId:E0000000"],
        suppress: 10000, userControl: Debugging)]
    public void P3B1_二运黑洞指挥标点(Event ev, ScriptAccessory sa)
    {
        if (_udmP3Param.当前阶段 != 3200) return;
        if (!P3B1CaptainMode) return;
        if (!_udmP3Param.二运状态记录.WaitOne(2000)) return;
        sa.DebugMsg($"[P3B1_二运黑洞指挥标点] 进入指挥标点", Debugging);
        for (int i = 0; i < 8; i++)
        {
            var kvp = _pd.SelectSpecificPriorityIndex(i);
            var marker = GetMarkTypeByRankBh(i);
            sa.MarkPlayerByIdx(kvp.Key, marker);
            sa.DebugMsg($"[P3B1_二运黑洞指挥标点] 给 {sa.GetPlayerJobByIndex(kvp.Key)} 标 {marker}", Debugging);
        }
        _udmP3Param.二运状态记录.Reset();
    }

    private MarkType GetMarkTypeByRankBh(int rank)
    {
        return rank switch
        {
            0 => MarkType.Attack1,
            1 => MarkType.Attack2,
            2 => MarkType.Attack3,
            3 => MarkType.Bind1,
            4 => MarkType.Bind2,
            5 => MarkType.Bind3,
            6 => MarkType.Stop1,
            _ => MarkType.Stop2
        };
    }
    

    #endregion P3B1 黑洞 3200
    
}


#region 优先级字典
internal class PriorityDict
{
    // ReSharper disable once NullableWarningSuppressionIsUsed
    public Dictionary<int, int> Priorities {get; set;} = null!;
    public string Annotation { get; set; } = "";
    public int ActionCount { get; set; } = 0;
    
    private static readonly List<string> Role = ["MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4"];
    
    public void Init(ScriptAccessory sa, string annotation, int partyNum = 8, bool refreshActionCount = true)
    {
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
    
    public string ShowGroup(string name, List<KeyValuePair<int, int>> kvpList)
    { 
        return $"{name}：{string.Join(" ", kvpList.Select(x => $"({Role[x.Key]}, {x.Value})"))}";
    }
}

#endregion 优先级字典 类
    

#region 参数容器类
internal class UDMP3Params
{
    public bool Debugging = false;
    public int 当前阶段 = 0;
    public ulong ObjectId_卡奥斯 = 0;
    public ulong ObjectId_艾克斯迪司 = 0;
    
    // ---- 一运 ----
    public bool 是长火 = false;
    public int 一运状态记录次数 = 0;
    public int 火水晶方位 = -1;
    public int 水水晶方位 = -1;
    public int 风水晶方位 = -1;
    public int 无水晶方位 = -1;

    public int 究极冲击波起始方位 = -1;
    public bool 究极冲击波为顺时针 = false;
    public bool 究极冲击波记录完毕 = false;
    
    public List<KeyValuePair<int, int>> 火组 = new();
    public List<KeyValuePair<int, int>> 水组 = new();
    public List<KeyValuePair<int, int>> 风组 = new();
    public ManualResetEvent 一运状态记录 = new(false);
    public ManualResetEvent 一运水晶记录 = new(false);
    public ManualResetEvent 一水火准备 = new(false);

    public int 一水火指路与绘图时间 = 10000;
    public int 二水火指路与绘图时间 = 9000;
    public int 真空波指路时间 = 7000;
    
    // ---- 二运 ----
    public int 二运状态记录次数 = 0;
    public ManualResetEvent 二运状态记录 = new(false);
    
    public void Reset(ScriptAccessory sa)
    {
        当前阶段 = 0;
        
        // 一运
        是长火 = false;
        一运状态记录次数 = 0;
        火水晶方位 = -1;
        水水晶方位 = -1;
        风水晶方位 = -1;
        无水晶方位 = -1;
        
        究极冲击波起始方位 = -1;
        究极冲击波为顺时针 = false;
        究极冲击波记录完毕 = false;
        
        ObjectId_卡奥斯 = 0;
        ObjectId_艾克斯迪司 = 0;
        
        一运状态记录.Reset();
        一运水晶记录.Reset();
        一水火准备.Reset();
        一运状态记录 = new(false);
        一运水晶记录 = new(false);
        一水火准备 = new(false);

        
        火组 = new();
        水组 = new();
        风组 = new();
            
        一水火指路与绘图时间 = 10000;
        二水火指路与绘图时间 = 9000;
        真空波指路时间 = 7000;
        
        // 二运
        二运状态记录次数 = 0;
        二运状态记录 = new(false);
        
        Dbg(sa, $"绝妖星乱舞 P3 参数重置");
    }
    
    public void Dbg(ScriptAccessory sa, string msg) =>
        sa.DebugMsg(msg, Debugging);
}
    
internal static class P3AExtension
{
    public static void 打印水晶与分组(this UDMP3Params prm, ScriptAccessory sa, PriorityDict pd)
    { 
        prm.Dbg(sa, $"是长 {(prm.是长火 ? "火" : "水")}");
        prm.Dbg(sa, $"火 {prm.火水晶方位} 水 {prm.水水晶方位} 风 {prm.风水晶方位} 无 {prm.无水晶方位}");
        prm.Dbg(sa, pd.ShowGroup("风组", prm.风组));
        prm.Dbg(sa, pd.ShowGroup("水组", prm.水组));
        prm.Dbg(sa, pd.ShowGroup("火组", prm.火组));
    }
    
    public static bool 一运状态记录完毕(this UDMP3Params prm) => 
        prm.一运状态记录次数 == 12;

    public static bool 水晶状态记录完毕(this UDMP3Params prm) => 
        prm is { 火水晶方位: >= 0, 水水晶方位: >= 0, 风水晶方位: >= 0 };

    public static void 成员分组(this UDMP3Params prm, ScriptAccessory sa, PriorityDict pd)
    {
        prm.火组.Clear();
        prm.水组.Clear();
        prm.风组.Clear();
        
        prm.风组.Add(pd.SelectSpecificPriorityIndex(0));   // 顺风
        prm.风组.Add(pd.SelectSpecificPriorityIndex(1));   // 逆风
        prm.风组.Add(pd.SelectSpecificPriorityIndex(2));   // 顺风
        prm.风组.Add(pd.SelectSpecificPriorityIndex(3));   // 逆风
        
        // 风组再按 KVP.value 的个位数大小进行 sort，小的在前
        prm.风组.Sort((a, b) => a.Value.GetDecimalDigit(0).CompareTo(b.Value.GetDecimalDigit(0)));
        
        prm.火组.Add(pd.SelectSpecificPriorityIndex(4));
        prm.火组.Add(pd.SelectSpecificPriorityIndex(5));
        prm.水组.Add(pd.SelectSpecificPriorityIndex(6));
        prm.水组.Add(pd.SelectSpecificPriorityIndex(7));
    }

    public static bool 当前轮为火(this UDMP3Params prm)
        // 第一轮 XOR 是长火
        => prm.当前阶段 == 3100 ^ prm.是长火;

    public static bool 需要背对(this UDMP3Params prm, int pdValue)
        // 混沌之风，背对击退距离减半
        => pdValue.GetDecimalDigit(1) == 1;

    public static Vector3 基于水晶旋转(this UDMP3Params prm, Vector3 basePos, int crystalRegion)
    {
        var center = new Vector3(100, 0, 100);
        return basePos.RotateAndExtend(center, (crystalRegion * 90f).DegToRad());
    }

    public static int 查询组序(this UDMP3Params prm, List<KeyValuePair<int, int>> group, int myIndex)
        => group.FindIndex(kvp => kvp.Key == myIndex);

    public static bool 水组有近战(this UDMP3Params prm, ScriptAccessory sa)
    {
        bool result = false;
        foreach (var kvp in prm.水组)
            result |= kvp.Key % 4 <= 1;
        prm.Dbg(sa, $"水组 {(result ? "有" : "无")} 近战");
        return result;
    }
}

internal static class P3BExtension
{
    public static bool 二运状态记录完毕(this UDMP3Params prm) =>
        prm.二运状态记录次数 == 8;
}

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
    
    public static bool TryParseVfxHandle(this Event ev, out nint handleId)
    {
        handleId = 0;
        var rawHandle = ev["Handle"];
        if (string.IsNullOrWhiteSpace(rawHandle))
            return false;

        var handleText = rawHandle.Trim();
        if (handleText.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            handleText = handleText[2..];

        return nint.TryParse(handleText, System.Globalization.NumberStyles.HexNumber, null, out handleId);
    }

    public static uint Id0(this Event ev)
    {
        return ParseHexId(ev["Id"], out var id) ? id : 0;
    }
    
    public static uint Index(this Event ev)
    {
        return JsonConvert.DeserializeObject<uint>(ev["Index"]);
    }
    
    public static uint DurationMilliseconds(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["DurationMilliseconds"]);
    }
    
    public static uint DataId(this Event ev)
    {
        return JsonConvert.DeserializeObject<uint>(ev["DataId"]);
    }
}

internal static class IbcHelper
{
    public static IGameObject? GetById(this ScriptAccessory sa, ulong gameObjectId)
    {
        return sa.Data.Objects.SearchById(gameObjectId);
    }
    
    public static IGameObject? GetMe(this ScriptAccessory sa)
    {
        return sa.Data.Objects.LocalPlayer;
    }

    public static IEnumerable<IGameObject?> GetByDataId(this ScriptAccessory sa, uint dataId)
    {
        return sa.Data.Objects.Where(x => x.DataId == dataId);
    }

    public static string GetPlayerJob(this ScriptAccessory sa, IPlayerCharacter? playerObject, bool fullName = false)
    {
        if (playerObject == null) return "None";
        return fullName ? playerObject.ClassJob.Value.Name.ToString() : playerObject.ClassJob.Value.Abbreviation.ToString();
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
    /// 获取给定整数的指定位数
    /// </summary>
    /// <param name="val">给定数值</param>
    /// <param name="x">对应位数，个位为0</param>
    /// <returns>返回指定位的数字，如果x超出范围返回0</returns>
    public static int GetDecimalDigit(this int val, int x)
        => (int)(Math.Abs(val) / Math.Pow(10, x) % 10);
    
    /// <summary>
    /// 获取整数的指定二进制位值
    /// </summary>
    /// <param name="val">给定数值</param>
    /// <param name="bitPosition">二进制位位置，从最低位开始，最低位为0</param>
    /// <returns>返回指定位的值：0 或 1</returns>
    public static int GetBinaryBit(this int val, int bitPosition)
        => (val >> bitPosition) & 1;
    
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
    
    /// <summary>
    /// 将List内信息转换为字符串。
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="myList"></param>
    /// <param name="isJob">是职业，在转为字符串前调用转职业函数</param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static string BuildListStr<T>(this ScriptAccessory sa, List<T> myList, bool isJob = false)
    {
        return string.Join(", ", myList.Select(item =>
        {
            if (isJob && item != null && item is int i)
                return sa.GetPlayerJobByIndex(i);
            return item?.ToString() ?? "";
        }));
    }
}
#endregion 位置序列函数

#region 绘图函数

internal static class DrawTools
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
        Vector4 color, float rotation = 0, float width = 1f, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 0, rotation, width,
            width, 0, 0, DrawModeEnum.Imgui, DrawTypeEnum.Displacement, color, false, true, draw);
    
    public static DrawPropertiesEdit DrawGuidance(this ScriptAccessory sa,
        object targetObj, int delay, int destroy, string name, Vector4 color, float rotation = 0, float width = 1f,
        bool draw = true)
        => sa.DrawGuidance((ulong)sa.Data.Me, targetObj, delay, destroy, name, color, rotation, width, draw);

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
        float scale, Vector4 color, bool byTime = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, 0, delay, destroy, name, 2 * float.Pi, 0, scale, scale,
            0, 0, DrawModeEnum.Default, DrawTypeEnum.Circle, color, byTime,false, draw);

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
        float outScale, float innerScale, Vector4 color, bool byTime = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, 0, delay, destroy, name, 2 * float.Pi, 0, outScale, outScale, innerScale,
            innerScale, DrawModeEnum.Default, DrawTypeEnum.Donut, color, byTime, false, draw);
    
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
        float outScale, float innerScale, Vector4 color, bool byTime = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, radian, rotation, outScale, outScale, innerScale,
            innerScale, DrawModeEnum.Default, innerScale == 0 ? DrawTypeEnum.Fan : DrawTypeEnum.Donut, color, byTime, false, draw);

    public static DrawPropertiesEdit DrawFan(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name, float radian, float rotation,
        float outScale, float innerScale, Vector4 color, bool byTime = false, bool draw = true)
        => sa.DrawFan(ownerObj, 0, delay, destroy, name, radian, rotation, outScale, innerScale, color, byTime, draw);

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
        float width, float length, Vector4 color, bool byTime = false, bool byY = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 0, rotation, width, length, 0, 0,
            DrawModeEnum.Default, DrawTypeEnum.Rect, color, byTime, byY, draw);
    
    public static DrawPropertiesEdit DrawRect(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name, float rotation,
        float width, float length, Vector4 color, bool byTime = false, bool byY = false, bool draw = true)
        => sa.DrawRect(ownerObj, 0, delay, destroy, name, rotation, width, length, color, byTime, byY, draw);
    
    /// <summary>
    /// 返回背对绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="targetObj">目标</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="color">使用颜色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawSightAvoid(this ScriptAccessory sa,
        object targetObj, int delay, int destroy, string name, Vector4 color, bool draw = true)
        => sa.DrawOwnerBase(sa.Data.Me, targetObj, delay, destroy, name, 0, 0, 0, 0, 0, 0,
            DrawModeEnum.Default, DrawTypeEnum.SightAvoid, color, false, false, draw);

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
        Vector4 color, bool draw = true)
        => sa.DrawOwnerBase(sa.Data.Me, targetObj, delay, destroy, name, 0, float.Pi, width, length, 0, 0,
            DrawModeEnum.Default, DrawTypeEnum.Displacement, color, false, false, draw);

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
        float width, float length, Vector4 color, bool byTime = false, bool byY = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 1, rotation, width, length, 0, 0,
            DrawModeEnum.Default, DrawTypeEnum.Line, color, byTime, byY, draw);
    
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
        int delay, int destroy, string name, Vector4 color, float width = 1f, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 0, 0, width, width,
            0, 0, DrawModeEnum.Imgui, DrawTypeEnum.Line, color, false, true, draw);

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
    
    /// <summary>
    /// 赋予输入的dp以ownerId为源的仇恨顺序绘图
    /// </summary>
    /// <param name="self"></param>
    /// <param name="orderIdx">仇恨顺序，从1开始</param>
    /// <returns></returns>
    public static DrawPropertiesEdit SetOwnersEnmityOrder(this DrawPropertiesEdit self, uint orderIdx)
    {
        self.CentreResolvePattern = PositionResolvePatternEnum.OwnerEnmityOrder;
        self.CentreOrderIndex = orderIdx;
        return self;
    }
    
    /// <summary>
    /// 赋予输入的dp以position为源的远近目标绘图
    /// </summary>
    /// <param name="self"></param>
    /// <param name="isNearOrder">从owner计算，近顺序或远顺序</param>
    /// <param name="orderIdx">从1开始</param>
    /// <returns></returns>
    public static DrawPropertiesEdit SetPositionDistanceOrder(this DrawPropertiesEdit self, bool isNearOrder,
        uint orderIdx)
    {
        self.TargetResolvePattern = isNearOrder
            ? PositionResolvePatternEnum.PlayerNearestOrder
            : PositionResolvePatternEnum.PlayerFarestOrder;
        self.TargetOrderIndex = orderIdx;
        return self;
    }
    
    /// <summary>
    /// 赋予输入的dp以ownerId施法目标为源的绘图
    /// </summary>
    /// <param name="self"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit SetOwnersTarget(this DrawPropertiesEdit self)
    {
        self.TargetResolvePattern = PositionResolvePatternEnum.OwnerTarget;
        return self;
    }
}

#endregion 绘图函数

#region 标点函数

internal static class MarkerHelper
{
    public static void LocalMarkClear(this ScriptAccessory sa)
    {
        sa.Log.Debug($"删除本地标点。");
        sa.Method.Mark(0xE000000, MarkType.Attack1, true);
        sa.Method.Mark(0xE000000, MarkType.Attack2, true);
        sa.Method.Mark(0xE000000, MarkType.Attack3, true);
        sa.Method.Mark(0xE000000, MarkType.Attack4, true);
        sa.Method.Mark(0xE000000, MarkType.Attack5, true);
        sa.Method.Mark(0xE000000, MarkType.Attack6, true);
        sa.Method.Mark(0xE000000, MarkType.Attack7, true);
        sa.Method.Mark(0xE000000, MarkType.Attack8, true);
        sa.Method.Mark(0xE000000, MarkType.Bind1, true);
        sa.Method.Mark(0xE000000, MarkType.Bind2, true);
        sa.Method.Mark(0xE000000, MarkType.Bind3, true);
        sa.Method.Mark(0xE000000, MarkType.Stop1, true);
        sa.Method.Mark(0xE000000, MarkType.Stop2, true);
        sa.Method.Mark(0xE000000, MarkType.Square, true);
        sa.Method.Mark(0xE000000, MarkType.Circle, true);
        sa.Method.Mark(0xE000000, MarkType.Cross, true);
        sa.Method.Mark(0xE000000, MarkType.Triangle, true);
    }

    public static void MarkClear(this ScriptAccessory sa,
        bool enable = true, bool local = false, bool localString = false)
    {
        if (!enable) return;
        sa.Log.Debug($"接收命令：删除标点");
        
        if (local)
        {
            if (localString)
                sa.Log.Debug($"[字符模拟] 删除本地标点。");
            else
                sa.LocalMarkClear();
        }
        else
            sa.Method.MarkClear();
    }

    public static void MarkPlayerByIdx(this ScriptAccessory sa, int idx, MarkType marker,
        bool enable = true, bool local = false, bool localString = false)
    {
        if (!enable) return;
        if (localString)
            sa.Log.Debug($"[本地字符模拟] 为{idx}({sa.GetPlayerJobByIndex(idx)})标上{marker}。");
        else
            sa.Method.Mark(sa.Data.PartyList[idx], marker, local);
    }

    public static void MarkPlayerById(this ScriptAccessory sa, uint id, MarkType marker,
        bool enable = true, bool local = false, bool localString = false)
    {
        if (!enable) return;
        if (localString)
            sa.Log.Debug($"[本地字符模拟] 为{sa.GetPlayerIdIndex(id)}({sa.GetPlayerJobById(id)})标上{marker}。");
        else
            sa.Method.Mark(id, marker, local);
    }

    public static int GetMarkedPlayerIndex(this ScriptAccessory sa, List<MarkType> markerList, MarkType marker)
    {
        return markerList.IndexOf(marker);
    }
}

#endregion

#region 调试函数

internal static class DebugFunction
{
    public static void DebugMsg(this ScriptAccessory sa, string msg, bool enable = false, bool showInChatBox = false)
    {
        if (!enable) return;
        sa.Log.Debug(msg);
        if (!showInChatBox) return;
        sa.Method.SendChat($"/e {msg}");
    }
}

#endregion 调试函数

#region 特殊函数

internal static class SpecialFunction
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
        var dp = sa.DrawGuidance(ownerObj, 0, 0, 2000, $"改变面向 {obj.Name.TextValue}", sa.Data.DefaultSafeColor, radian, draw: false);
        dp.FixRotation = true;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dp);
        
    }

    public static void SetPosition(this ScriptAccessory sa, IGameObject? obj, Vector3 position, bool show = false)
    {
        if (obj == null || !obj.IsValid())
        {
            sa.Log.Error($"传入的IGameObject不合法。");
            return;
        }
        unsafe
        {
            GameObject* charaStruct = (GameObject*)obj.Address;
            charaStruct->SetPosition(position.X, position.Y, position.Z);
        }
        sa.Log.Debug($"改变位置 => {obj.Name.TextValue} | {obj.EntityId} => {position}");
        
        if (!show) return;
        var dp = sa.DrawCircle(position, 0, 2000, $"传送点 {obj.Name.TextValue}", 0.5f, sa.Data.DefaultSafeColor, draw: false);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
    }
    
    public static unsafe void WriteVisible(this ScriptAccessory sa, IGameObject? actor, bool visible)
    {
        const VisibilityFlags VISIBLE_FLAG = VisibilityFlags.None;
        const VisibilityFlags INVISIBILITY_FLAG = VisibilityFlags.Model;
        if (actor == null || !actor.IsValid())
        {
            sa.Log.Error("WriteVisible 失败：传入的 IGameObject 不合法。");
            return;
        }
        try
        {
            var flagsPtr = &((GameObject*)actor.Address)->RenderFlags;
            *flagsPtr = visible ? VISIBLE_FLAG : INVISIBILITY_FLAG;
        }
        catch (Exception e)
        {
            sa.Log.Error(e.ToString());
            throw;
        }
    }
    
    public static unsafe void AlphaModify(this ScriptAccessory sa, IGameObject? obj, float alpha)
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
            
            charaStruct->Alpha = alpha;
            charaStruct->DisableDraw();
            charaStruct->EnableDraw();

            sa.Log.Debug($"AlphaModify => {obj.Name.TextValue} | {obj} => {alpha}");
        }
    }
}

#endregion 特殊函数

#endregion 函数集
