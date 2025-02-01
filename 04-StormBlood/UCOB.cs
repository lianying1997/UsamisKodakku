using System;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Script;
using KodakkuAssist.Module.Draw;
using Dalamud.Utility.Numerics;
using System.Numerics;
using System.Runtime.Intrinsics.Arm;
using Dalamud.Memory.Exceptions;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using ECommons;
using System.Linq;
using ImGuiNET;
using static Dalamud.Interface.Utility.Raii.ImRaii;
using KodakkuAssist.Module.GameOperate;
using System.Security.Cryptography;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.ComponentModel;
// using System.DirectoryServices.ActiveDirectory;
using System.Collections;
using System.ComponentModel.DataAnnotations.Schema;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using KodakkuAssist.Module.Draw.Manager;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using System.Runtime;
using System.Timers;
// using Lumina.Excel.GeneratedSheets;
using System.Diagnostics;
using System.Security.AccessControl;

namespace UsamisScript.StormBlood.Ucob;

[ScriptType(name: "UCOB [巴哈姆特绝境战]", territorys: [733], guid: "884e415a-1210-44cc-bdff-8fab6878e87d", version: "0.0.1.6", author: "Joshua and Usami", note: noteStr)]
public class Ucob
{
    // TODO
    // 暂无，硬要说的话，添加双塔垂直下落范围

    const string noteStr =
    """
    请先按需求检查并设置“用户设置”栏目。

    Original code by Joshua, adjustments by Usami.
    Great Thanks to Contributor @KnightRider. 
    v0.0.1.6:
    【重要】1. 修复P3连击BUG。

    v0.0.1.5:
    1. 修复P3连击拘束器撞球全局提示的一条提示文字Bug。
    2. 增加P3连击拘束器撞球全局提示信息的开关可选项。

    v0.0.1.4:
    1. 修复P4月环-钢铁-分散，分散预警绘图出现时机问题，并添加相对北提示。
    2. 增加了P3连击拘束器撞球与截球的提示与指路。
    3. 修复了一些不可控函数可被玩家关闭的设置错误。

    v0.0.1.3:
    感谢KnightRider佬的帮助，孩子抄代码抄的很开心！
    1. 添加了P2火龙分摊吃火/不吃火的情况判断。

    v0.0.1.2:
    1. 删了一个可能引起国际服编译错误的引用。
    2. P3连击添加场中-拘束器-场边的三点一线标志。
    3. P4根据奈尔台词添加了分散/旋风前的方向指引，在用户设置处修改参数。
    4. P4撞球添加拘束器提示与指路。
    5. 调淡了一些绘图颜色。

    v0.0.1.1:
    1. 修改了黑球危险区绘图逻辑，改为基于拘束器位置绘图。
    2. 增加双塔与火龙火球分摊范围预警。
    3. 连击增加每人的陨石流预警。

    v0.0.1.0:
    初版完成。
    鸭门。
    """;

    [UserSetting("Debug模式，非开发用请关闭")]
    public bool DebugMode { get; set; } = false;

    [UserSetting("黑球行动轨迹长度")]
    public float blackOrbTrackLength { get; set; } = 4;

    [UserSetting("黑球行动方向绘图颜色")]
    public ScriptColor blackOrbTrackColor { get; set; } = new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 0f, 1.0f) };

    [UserSetting("是否绘出黑球爆炸范围")]
    public bool showBlackOrbField { get; set; } = true;

    [UserSetting("黑球爆炸范围绘图颜色")]
    public ScriptColor blackOrbFieldColor { get; set; } = new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 0f, 1.0f) };

    [UserSetting("P2：是否展示其他玩家的小龙俯冲引导路径")]
    public bool showOtherCauterizeRoute { get; set; } = false;

    [UserSetting("P3：【连击】是否提示全局撞球/截球信息")]
    public bool showGlobalTenStrikeBlackOrbMsg { get; set; } = true;

    public enum BahamutFavorNorthTypeEnum
    {
        画出12点_ShowTempNorth,
        画出8条分散方向_ShowRecomDir,
        不画_DontDraw,
    }

    [UserSetting("P4：场中分摊/分散+旋风时以何种形式根据时机画出指向标记")]
    public BahamutFavorNorthTypeEnum BahamutFavorNorth { get; set; } = BahamutFavorNorthTypeEnum.画出8条分散方向_ShowRecomDir;

    [UserSetting("P5：地火爆炸区颜色")]
    public ScriptColor exflareColor { get; set; } = new ScriptColor { V4 = new Vector4(0f, 1.0f, 1.0f, 1.0f) };

    [UserSetting("P5：地火预警区颜色")]
    public ScriptColor exflareWarnColor { get; set; } = new ScriptColor { V4 = new Vector4(0f, 0.5f, 1.0f, 1.0f) };

    [UserSetting("P5：是否提示分摊/死刑次数")]
    public bool showStackBusterNum { get; set; } = false;

    [UserSetting("站位提示圈绘图-普通颜色")]
    public ScriptColor posColorNormal { get; set; } = new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 1.0f, 1.0f) };

    [UserSetting("站位提示圈绘图-玩家站位颜色")]
    public ScriptColor posColorPlayer { get; set; } = new ScriptColor { V4 = new Vector4(0.2f, 1.0f, 0.2f, 1.0f) };

    public ScriptColor colorRed = new ScriptColor { V4 = new Vector4(1.0f, 0f, 0f, 1.0f) };
    public ScriptColor colorPink = new ScriptColor { V4 = new Vector4(1f, 0f, 1f, 1.0f) };
    public ScriptColor colorCyan = new ScriptColor { V4 = new Vector4(0f, 1f, 1f, 1.0f) };

    public enum UCOB_Phase
    {
        Twintania,  // P1
        Nael,   // P2
        Quickmarch_1st,
        Blackfire_2nd,
        Fellruin_3rd,
        Heavensfall_4th,
        Tenstrike_5th,
        GrandOctet_6th,
        BahamutFavor, // P4
        FlamesRebirth,  // P5
    }
    UCOB_Phase phase = UCOB_Phase.Twintania;
    int restrictorNum = 0;                          // P1拘束器掉落次数
    Vector3[] RestrictorPos = new Vector3[3];       // P1拘束器位置记录
    List<bool> GenerateTarget = [false, false, false, false, false, false, false, false];   // 被点名魔力炼成
    List<uint> DeathSentenceTarget = [0, 0, 0];     // P2死宣目标
    List<int> FireBallStatus = [0, 0, 0, 0, 0, 0, 0, 0];    // P2火buff状态
    int FireBallTimes = 0;  // P2火球分摊次数
    Dictionary<uint, int> CauterizeDragons = new();   // P2小龙字典（id, 位置）
    int CauterizeTimes = 0;     // P2小龙引导次数
    Vector3 QuickMarchPos = new(0, 0, 0);           // P3进军位置
    bool QuickMarchStackDrawn = false;              // P3进军核爆绘图完成记录
    bool QuickMarchEarthShakerDrawn = false;        // P3进军大地摇动绘图完成记录
    Vector3 NaelPosition = new(0, 0, 0);            // P3奈尔位置记录
    Vector3 TwintaniaPosition = new(0, 0, 0);       // P3双塔位置记录
    Vector3 BahamutPosition = new(0, 0, 0);         // P3巴哈位置记录
    bool blackFireDrawn = false;                    // P3黑炎指路绘图完成记录
    List<bool> HeavensFallDangerPos = [false, false, false, false, false, false, false, false]; // P3天地安全位置（仅判断左右）
    List<bool> HeavensFallBossPos = [false, false, false, false, false, false, false, false];   // P3天地BOSS所在位置（仅判断左右）
    List<bool> HeavensFallTowerPos = [false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false];  // P4天地塔位置
    bool HeavensFallTowerDrawn = false;         // P3天地塔目标绘图完成记录
    bool isTenStrikeTarget = false;              // P3连击玩家是黑球点名目标
    bool TenStrikeBlackOrbDrawn = false;        // P3连击黑球指路绘图完成记录
    int TenStrikeEarthShakerNum = 0;            // P3连击大地摇动点名次数
    bool isEarthShakerFirstRound = false;       // P3连击大地摇动是否为第一轮
    bool grandOctDrawn = false;                 // P3群龙起始位置绘图完成记录
    int grandOctIconNum = 0;                    // P3群龙点名次数
    List<bool> grandOctTargetChosen = [false, false, false, false, false, false, false, false]; // P3群龙目标选择
    Vector3 BahamutFavorPos = new(0, 0, 0);     // P4以初始拉怪点为12点（右下）
    int ArkMornNum = 0;                         // P5死亡轮回死刑次数
    int MornAfahNum = 0;                        // P5无尽顿悟分摊次数


    public void Init(ScriptAccessory accessory)
    {
        phase = UCOB_Phase.Twintania;
        restrictorNum = 0;                      // P1拘束器掉落次数

        RestrictorPos[0] = new(0, 0, 0);        // P1拘束器位置记录
        RestrictorPos[1] = new(0, 0, 0);
        RestrictorPos[2] = new(0, 0, 0);

        GenerateTarget = [false, false, false, false, false, false, false, false];   // 被点名魔力炼成

        DeathSentenceTarget = [0, 0, 0];        // P2死宣目标
        FireBallStatus = [0, 0, 0, 0, 0, 0, 0, 0];    // P2火buff状态
        FireBallTimes = 0;  // P2火球分摊次数
        CauterizeDragons = new();            // P2小龙字典（id, 位置）
        CauterizeTimes = 0;                  // P2小龙引导次数

        QuickMarchPos = new(0, 0, 0);           // P3进军位置
        QuickMarchStackDrawn = false;           // P3进军核爆绘图完成记录
        QuickMarchEarthShakerDrawn = false;     // P3进军大地摇动绘图完成记录

        blackFireDrawn = false;                 // P3黑炎指路绘图完成记录

        NaelPosition = new(0, 0, 0);            // P3奈尔位置记录
        TwintaniaPosition = new(0, 0, 0);       // P3双塔位置记录
        BahamutPosition = new(0, 0, 0);         // P3巴哈位置记录

        HeavensFallDangerPos = [false, false, false, false, false, false, false, false]; // P3天地安全位置（仅判断左右）
        HeavensFallBossPos = [false, false, false, false, false, false, false, false];   // P3天地BOSS所在位置（仅判断左右）
        HeavensFallTowerPos = [false, false, false, false, false, false, false, false, false, false, false, false, false, false, false, false];  // P3天地塔    
        HeavensFallTowerDrawn = false;          // P3天地塔目标绘图完成记录

        isTenStrikeTarget = false;                // P3连击玩家是黑球点名目标
        TenStrikeBlackOrbDrawn = false;        // P3连击黑球指路绘图完成记录
        TenStrikeEarthShakerNum = 0;            // P3连击大地摇动点名次数
        isEarthShakerFirstRound = false;        // P3连击大地摇动是否为第一轮

        grandOctDrawn = false;                  // P3群龙起始位置绘图完成记录
        grandOctIconNum = 0;                    // P3群龙点名次数
        grandOctTargetChosen = [false, false, false, false, false, false, false, false];    // P3群龙目标选择

        BahamutFavorPos = new(0, 0, 0);         // P4以初始拉怪点为12点（右下）

        ArkMornNum = 0;
        MornAfahNum = 0;

        accessory.Method.MarkClear();
        accessory.Method.RemoveDraw(".*");

    }

    private int PositionTo8Dir(Vector3 point, Vector3 centre)
    {
        var r = Math.Round(4 - 4 * Math.Atan2(point.X - centre.X, point.Z - centre.Z) / Math.PI) % 8;
        return (int)r;
    }

    private int PositionTo16Dir(Vector3 point, Vector3 centre)
    {
        var r = Math.Round(8 - 8 * Math.Atan2(point.X - centre.X, point.Z - centre.Z) / Math.PI) % 16;
        return (int)r;
    }

    private Vector3 RotatePoint(Vector3 point, Vector3 centre, float radian)
    {

        Vector2 v2 = new(point.X - centre.X, point.Z - centre.Z);

        var rot = MathF.PI - MathF.Atan2(v2.X, v2.Y) + radian;
        var length = v2.Length();
        return new(centre.X + MathF.Sin(rot) * length, centre.Y, centre.Z - MathF.Cos(rot) * length);
    }

    private Vector3 ExtendPoint(Vector3 centre, float radian, float length)
    {
        return new(centre.X + MathF.Sin(radian) * length, centre.Y, centre.Z - MathF.Cos(radian) * length);
    }

    private float FindRadian(Vector3 centre, Vector3 new_point)
    {
        float radian = MathF.PI - MathF.Atan2(new_point.X - centre.X, new_point.Z - centre.Z);
        if (radian < 0)
            radian += 2 * MathF.PI;
        return radian;
    }
    private float angle2Rad(float angle)
    {
        float radian = (float)(angle * Math.PI / 180);
        return radian;
    }
    private int getPlayerIdIndex(ScriptAccessory accessory, uint pid)
    {
        return accessory.Data.PartyList.IndexOf(pid);
    }

    private int getMyIndex(ScriptAccessory accessory)
    {
        return accessory.Data.PartyList.IndexOf(accessory.Data.Me);
    }

    private string getPlayerJobIndex(ScriptAccessory accessory, uint pid)
    {
        var a = accessory.Data.PartyList.IndexOf(pid);
        switch (a)
        {
            case 0: return "MT";
            case 1: return "ST";
            case 2: return "H1";
            case 3: return "H2";
            case 4: return "D1";
            case 5: return "D2";
            case 6: return "D3";
            case 7: return "D4";
            default: return "unknown";
        }
    }

    private static bool ParseObjectId(string? idStr, out uint id)
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

    [ScriptMethod(name: "随时DEBUG用", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:=TST"], userControl: false)]
    public void EchoDebug(Event @event, ScriptAccessory accessory)
    {
        if (!DebugMode) return;
        var msg = @event["Message"].ToString();
        accessory.Method.SendChat($"/e 获得玩家发送的消息：{msg}");


        // 测试
        // MT ST D2
        GenerateTarget = [true, true, false, false, false, true, false, false];

        if (TenStrikeBlackOrbDrawn) return;

        int trueCount = 0;
        for (int i = 0; i < GenerateTarget.Count(); i++)
        {
            if (GenerateTarget[i])
                trueCount++;
        }
        if (trueCount != 3) return;
        TenStrikeBlackOrbDrawn = true;

        // 算出每组黑球玩家数目，给出提示信息
        List<int> TenStrikeBlackOrbGroupTargetNum = calcTenStrikeGroupTargetNum(GenerateTarget);
        if (DebugMode)
        {
            string tenStrikeTargetNum = string.Join(", ", TenStrikeBlackOrbGroupTargetNum);
            accessory.Method.SendChat($"/e 每组黑球玩家数：{tenStrikeTargetNum}");
        }

        if (showGlobalTenStrikeBlackOrbMsg)
            showTenStrikeBlackOrbMsg(TenStrikeBlackOrbGroupTargetNum, accessory);

        // 计算撞球与截球优先级，输出任务List
        // 注意，截球优先级并无唯一规则，需要观察场上情况灵活变通。
        List<int> missionList = judgeTenStrikeBlackOrbRoute(GenerateTarget);
        if (showGlobalTenStrikeBlackOrbMsg)
        {
            string mission_record = "";
            mission_record += $"H1组：撞球{getPlayerJobIndexByIdx(missionList[0])}, 截球{getPlayerJobIndexByIdx(missionList[1])}\n";
            mission_record += $"H2组：撞球{getPlayerJobIndexByIdx(missionList[2])}, 截球{getPlayerJobIndexByIdx(missionList[3])}\n";
            mission_record += $"D3D4组：撞球{getPlayerJobIndexByIdx(missionList[4])}, 截球{getPlayerJobIndexByIdx(missionList[5])}\n";
            accessory.Method.SendChat($"/e {mission_record}");
        }

        int routeDestoryTime1 = 5000;   // 第一次指路持续时间 & 第二次指路出现时间
        int routeDestoryTime2 = 5000;   // 第二次指路（截球人）持续时间
        // 拘束器idx：0为H1组D，1为D3D4组C，2为H2组B
        drawBlackOrbRoute(missionList[0], 0, 0, routeDestoryTime1, true, accessory);
        drawBlackOrbRoute(missionList[1], 0, 0, routeDestoryTime1, false, accessory);
        drawBlackOrbRoute(missionList[1], 0, routeDestoryTime1, routeDestoryTime2, true, accessory);
        drawBlackOrbRoute(missionList[2], 2, 0, routeDestoryTime1, true, accessory);
        drawBlackOrbRoute(missionList[3], 2, 0, routeDestoryTime1, false, accessory);
        drawBlackOrbRoute(missionList[3], 2, routeDestoryTime1, routeDestoryTime2, true, accessory);
        drawBlackOrbRoute(missionList[4], 1, 0, routeDestoryTime1, true, accessory);
        drawBlackOrbRoute(missionList[5], 1, 0, routeDestoryTime1, false, accessory);
        drawBlackOrbRoute(missionList[5], 1, routeDestoryTime1, routeDestoryTime2, true, accessory);

    }

    #region 全局

    [ScriptMethod(name: "【全局】黑球路径", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:8160"])]
    public void BlackOrbTrack(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"黑球路径-{sid}";
        dp.Scale = new(2f, blackOrbTrackLength);
        dp.Color = blackOrbTrackColor.V4.WithW(3);
        dp.Owner = sid;
        dp.Delay = 3500;
        dp.DestoryAt = 10000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
    }

    [ScriptMethod(name: "【全局】超新星危险位置", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["DataId:2003393", "Operate:Add"])]
    public void HypernovaField(Event @event, ScriptAccessory accessory)
    {
        var spos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"超新星危险区";
        dp.Scale = new(5f);
        dp.Position = spos;
        dp.DestoryAt = 15000;
        dp.Color = colorRed.V4.WithW(3);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "【全局】黑球路径删除（不可控）", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9903"], userControl: false)]
    public void BlackOrbTrackRemove(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;
        accessory.Method.RemoveDraw($"黑球路径-{sid}");
    }

    [ScriptMethod(name: "【全局】黑球点名记录（不可控）", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0076"], userControl: false)]
    public void GenerateTargetRecord(Event @event, ScriptAccessory accessory)
    {
        Task.Delay(50).ContinueWith(t =>
        {
            if (!ParseObjectId(@event["TargetId"], out var tid)) return;
            var tidx = getPlayerIdIndex(accessory, tid);
            GenerateTarget[tidx] = true;
            if (DebugMode)
            {
                var tidjob = getPlayerJobIndex(accessory, tid);
                accessory.Method.SendChat($"/e 检测到 {tidjob} 被点名魔力炼成。");
            }

            if (phase != UCOB_Phase.Tenstrike_5th) return;
            if (tidx == getMyIndex(accessory))
                isTenStrikeTarget = true;
        });
    }

    [ScriptMethod(name: "【全局】拘束器位置记录（不可控）", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["DataId:2001151", "Operate:Add"], userControl: false)]
    public void RestrictorPosRecord(Event @event, ScriptAccessory accessory)
    {
        if (phase != UCOB_Phase.Twintania) return;
        var tpos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

        if (DebugMode)
        {
            accessory.Method.SendChat($"/e 记录到拘束器，位置为({tpos.X}, {tpos.Z})。");
        }
        RestrictorPos[restrictorNum] = tpos;
        restrictorNum++;

        if (restrictorNum != 3) return;
        float restrictorRad1 = FindRadian(new(0, 0, 0), RestrictorPos[1]);
        float restrictorRad2 = FindRadian(new(0, 0, 0), RestrictorPos[2]);
        float bahamutFavorRad = (restrictorRad1 + restrictorRad2) / 2;
        BahamutFavorPos = ExtendPoint(new(0, 0, 0), bahamutFavorRad, 24);

        if (restrictorNum == 3 && DebugMode)
        {
            accessory.Method.SendChat($"/e 拘束器数量到3，位置记录完毕。");
        }
    }
    [ScriptMethod(name: "【全局】刷新黑球点名目标（不可控）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9902"], userControl: false)]
    public void RefreshGenerateTarget(Event @event, ScriptAccessory accessory)
    {
        Task.Delay(500).ContinueWith(t =>
        {
            GenerateTarget = [false, false, false, false, false, false, false, false];
        });
    }

    [ScriptMethod(name: "【全局】黑球爆炸范围", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9902"])]
    public void BlackOrbField(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;
        if (!showBlackOrbField) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        for (int i = 0; i < restrictorNum; i++)
        {
            dp.Name = $"黑球爆炸范围-{i}";
            dp.Scale = new(7.5f);
            dp.Color = blackOrbFieldColor.V4.WithW(0.4f);
            dp.Position = RestrictorPos[i];
            dp.Delay = 3500;
            dp.DestoryAt = phase == UCOB_Phase.Tenstrike_5th ? 3500 : 10000;     // 连击的两轮绘图时延作特殊处理
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    [ScriptMethod(name: "【全局】黑球爆炸范围删除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9903"], userControl: false)]
    public void BlackOrbFieldRemove(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;
        if (!ParseObjectId(@event["TargetId"], out var tid)) return;
        // 连击的两轮绘图不删除
        if (phase == UCOB_Phase.Tenstrike_5th) return;
        var spos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        int minIdx = getNearestOrbField(spos);
        var tidx = getPlayerIdIndex(accessory, tid);
        accessory.Method.RemoveDraw($"黑球爆炸范围-{minIdx}");
        accessory.Method.RemoveDraw($"拘束器位置定位{minIdx}");
        accessory.Method.RemoveDraw($"拘束器撞球玩家{tidx}指路");
    }

    [ScriptMethod(name: "【全局】旋风危险位置", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["DataId:2001168", "Operate:Add"])]
    public void Twister_Field(Event @event, ScriptAccessory accessory)
    {
        var spos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"旋风危险区";
        dp.Scale = new(1.5f);
        dp.Position = spos;
        dp.DestoryAt = 7000;
        dp.Color = colorRed.V4.WithW(3);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    private int getNearestOrbField(Vector3 spos)
    {
        int minIdx = 0;
        float minLength = 999f;
        for (int i = 0; i < restrictorNum; i++)
        {
            float length = new Vector2(spos.X - RestrictorPos[i].X, spos.Z - RestrictorPos[i].Z).Length();
            if (length < minLength)
            {
                minLength = length;
                minIdx = i;
            }
        }
        return minIdx;
    }

    #endregion

    #region P1：双塔

    private void drawTwister(int delay, int destoryAt, ScriptAccessory accessory)
    {
        accessory.Method.TextInfo("旋风", delay, true);
        for (var i = 0; i < 8; i++)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"旋风{i}";
            dp.Scale = new(1.5f);
            dp.Owner = accessory.Data.PartyList[i];
            dp.Delay = delay;
            dp.DestoryAt = destoryAt;
            dp.ScaleMode = ScaleMode.ByTime;
            dp.Color = accessory.Data.DefaultDangerColor.WithW(2);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    [ScriptMethod(name: "P1&P4双塔：旋风自身位置预警", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9898"])]
    public void Twister_PlayerPosition(Event @event, ScriptAccessory accessory)
    {
        drawTwister(0, 2000, accessory);
    }

    [ScriptMethod(name: "P1&P3双塔：双塔火球分摊范围预警", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0075"])]
    public void FireBallStackTarget(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["TargetId"], out var tid)) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"双塔火球分摊范围";
        dp.Scale = new(4f);
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Owner = tid;
        dp.DestoryAt = 10000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P1&P3双塔：双塔火球分摊范围预警删除（不可控）", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9900", "TargetIndex:1"], userControl: false)]
    public void FireBallStackTargetRemove(Event @event, ScriptAccessory accessory)
    {
        // TMD火球时间还不一样
        accessory.Method.RemoveDraw($"双塔火球分摊范围");
    }

    #endregion

    #region P2：奈尔

    [ScriptMethod(name: "P2奈尔：阶段记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9922"], userControl: false)]
    public void P2_PhaseChange(Event @event, ScriptAccessory accessory)
    {
        if (phase != UCOB_Phase.Twintania) return;
        phase = UCOB_Phase.Nael;
    }

    [ScriptMethod(name: "P2奈尔：火龙连线分摊范围预警", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0005"])]
    public void FireBallDragonStackTarget(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["TargetId"], out var tid)) return;
        FireBallTimes++;
        var MyIndex = getMyIndex(accessory);
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"火龙火球分摊范围";
        dp.Scale = new(4f);
        dp.Owner = tid;
        dp.DestoryAt = 10000;
        switch (FireBallTimes)
        {
            case 1:
                // 去吃1火
                dp.Color = FireBallStatus[MyIndex] != 1 ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
                break;
            case 2:
                // 如果目标是我 或 我有冰状态，去吃2火
                dp.Color = (tid == accessory.Data.Me) | (FireBallStatus[MyIndex] == -1) ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
                break;
            case 3:
            case 4:
                // 如果目标是我 或 我无火状态，去吃3/4火
                dp.Color = (tid == accessory.Data.Me) | (FireBallStatus[MyIndex] != 1) ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
                break;
            default:
                return;
        }
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P2奈尔：火龙连线分摊范围预警删除（不可控）", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9925", "TargetIndex:1"], userControl: false)]
    public void FireBallDragonStackTargetRemove(Event @event, ScriptAccessory accessory)
    {
        // TMD火线时间还不一样
        accessory.Method.RemoveDraw($"火龙火球分摊范围");
    }
    [ScriptMethod(name: "P2奈尔：冰火状态记录添加", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(465|464)$"], userControl: false)]
    public void FireBallStatusAddRecord(Event @event, ScriptAccessory accessory)
    {
        if (phase != UCOB_Phase.Nael) return;
        if (!ParseObjectId(@event["TargetId"], out var tid)) return;
        var stid = JsonConvert.DeserializeObject<uint>(@event["StatusID"]);
        var idx = getPlayerIdIndex(accessory, tid);
        if (stid == 465)
            // -1为冰，+1为火
            FireBallStatus[idx] = -1;
        else
            FireBallStatus[idx] = 1;
    }
    [ScriptMethod(name: "P2奈尔：冰火状态记录移除", eventType: EventTypeEnum.StatusRemove, eventCondition: ["StatusID:regex:^(465|464)$"], userControl: false)]
    public void FireBallStatusRemoveRecord(Event @event, ScriptAccessory accessory)
    {
        if (phase != UCOB_Phase.Nael) return;
        if (!ParseObjectId(@event["TargetId"], out var tid)) return;
        var idx = getPlayerIdIndex(accessory, tid);
        FireBallStatus[idx] = 0;
    }

    [ScriptMethod(name: "P2奈尔：死亡宣告记录（不可控）", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:210"], userControl: false)]
    public void DeathSentence_Record(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["TargetId"], out var tid)) return;
        if (!int.TryParse(@event["DurationMilliseconds"], out var dur)) return;
        // DeathSentenceNum++;
        switch (dur)
        {
            case 6000:
                DeathSentenceTarget[0] = tid;
                if (DebugMode)
                    accessory.Method.SendChat($"/e 检测到死1玩家：{getPlayerJobIndex(accessory, tid)}");
                break;
            case 10000:
                DeathSentenceTarget[1] = tid;
                if (DebugMode)
                    accessory.Method.SendChat($"/e 检测到死2玩家：{getPlayerJobIndex(accessory, tid)}");
                break;
            case 16000:
                DeathSentenceTarget[2] = tid;
                if (DebugMode)
                    accessory.Method.SendChat($"/e 检测到死3玩家：{getPlayerJobIndex(accessory, tid)}");
                break;
        }
    }

    [ScriptMethod(name: "P2奈尔：救世之翼预警与指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9930"])]
    public void WingsOfSalvation_Position(Event @event, ScriptAccessory accessory)
    {
        Task.Delay(2000).ContinueWith(t =>
        {
            var epos = JsonConvert.DeserializeObject<Vector3>(@event["EffectPosition"]);
            uint DoomPlayerID = 0; // DeathSentenceTarget中第一个不为0的值

            for (int i = 0; i < DeathSentenceTarget.Count; i++)
            {
                if (DeathSentenceTarget[i] != 0)
                {
                    DoomPlayerID = DeathSentenceTarget[i];  // 赋值给DoomPlayerID
                    DeathSentenceTarget[i] = 0;  // 将该值设为0
                    if (DebugMode)
                        accessory.Method.SendChat($"/e 准备绘制死宣玩家：{getPlayerJobIndex(accessory, DoomPlayerID)}");
                    break;  // 找到第一个非0值后跳出循环
                }
            }

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"白圈范围预警";
            dp.Scale = new(1.5f);
            dp.Position = epos;
            dp.Delay = 1000;
            dp.DestoryAt = 4000;
            dp.Color = DoomPlayerID == accessory.Data.Me ? accessory.Data.DefaultSafeColor.WithW(3) : colorRed.V4.WithW(3);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            if (DoomPlayerID == accessory.Data.Me)
            {
                accessory.Method.TextInfo("即将踩白圈", 2000, true);
                dp = accessory.Data.GetDefaultDrawProperties();
                dp.Scale = new(0.5f);
                dp.Name = $"白圈指路";
                dp.Owner = accessory.Data.Me;
                dp.TargetPosition = epos;
                dp.ScaleMode = ScaleMode.YByDistance;
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.Delay = 1000;
                dp.DestoryAt = 4000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
        });
    }

    [ScriptMethod(name: "P2奈尔：雷点名范围预警", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9927"])]
    public void ChainLightening(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["TargetId"], out var tid)) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"雷光链{tid}";
        dp.Scale = new(5);
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Owner = tid;
        dp.Delay = 4000;
        dp.DestoryAt = 2000;
        dp.Color = colorPink.V4.WithW(3);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    #endregion

    #region P2：奈尔台词

    private void drawNaelQuote_Circle(uint sid, int delay, int destoryAt, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "钢铁";
        dp.Scale = new(9);
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Owner = sid;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        dp.Color = accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    private void drawNaelQuote_Donut(uint sid, int delay, int destoryAt, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "月环";
        dp.Scale = new(22);
        dp.InnerScale = new(6);
        dp.Radian = float.Pi * 2;
        dp.Owner = sid;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        dp.Color = accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
    }

    private void drawNaelQuote_Stack(uint sid, int delay, int destoryAt, ScriptAccessory accessory)
    {
        for (var i = 0; i < 8; i++)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"热离子光束{i}";
            dp.Scale = new(4);
            dp.Owner = accessory.Data.PartyList[i];
            dp.Delay = delay;
            dp.DestoryAt = destoryAt;
            dp.Color = accessory.Data.DefaultSafeColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    private void drawNaelQuote_Spread(uint sid, int delay, int destoryAt, ScriptAccessory accessory)
    {
        for (var i = 0; i < 8; i++)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"凶鸟冲{i}";
            dp.Scale = new(3);
            dp.ScaleMode = ScaleMode.ByTime;
            dp.Owner = accessory.Data.PartyList[i];
            dp.Delay = delay;
            dp.DestoryAt = destoryAt;
            dp.Color = accessory.Data.DefaultDangerColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    private void drawNaelQuote_Tank(uint sid, int delay, int destoryAt, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"月华冲";
        dp.Scale = new(5);
        dp.Owner = sid;
        dp.CentreResolvePattern = PositionResolvePatternEnum.OwnerEnmityOrder;
        dp.CentreOrderIndex = 1;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        dp.Color = accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    private void drawMeteorStream(uint sid, int delay, int destoryAt, ScriptAccessory accessory)
    {
        for (var i = 0; i < 8; i++)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"陨石流{i}";
            dp.Scale = new(4);
            dp.ScaleMode = ScaleMode.ByTime;
            dp.Owner = accessory.Data.PartyList[i];
            dp.Delay = delay;
            dp.DestoryAt = destoryAt;
            dp.Color = accessory.Data.DefaultDangerColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    [ScriptMethod(name: "P2奈尔：台词", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:regex:^(649[234567]|650[01])$"])]
    public void NaelQuotesP2(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;
        var quoteId = @event["Id"];

        switch (quoteId)
        {
            case "6492":
                {
                    // 月光啊！照亮铁血霸道！
                    drawNaelQuote_Donut(sid, 0, 5000, accessory);
                    drawNaelQuote_Circle(sid, 5000, 3000, accessory);
                    break;
                }
            case "6493":
                {
                    // 月光啊！用你的炽热烧尽敌人！
                    drawNaelQuote_Donut(sid, 0, 5000, accessory);
                    drawNaelQuote_Stack(sid, 5000, 3000, accessory);
                    break;
                }
            case "6494":
                {
                    // 被炽热灼烧过的轨迹，乃成铁血霸道！
                    drawNaelQuote_Stack(sid, 0, 5000, accessory);
                    drawNaelQuote_Circle(sid, 5000, 3000, accessory);
                    break;
                }
            case "6495":
                {
                    // 炽热燃烧！给予我月亮的祝福！
                    drawNaelQuote_Stack(sid, 0, 5000, accessory);
                    drawNaelQuote_Donut(sid, 5000, 3000, accessory);

                    break;
                }
            case "6496":
                {
                    // 我降临于此，征战铁血霸道！
                    drawNaelQuote_Spread(sid, 0, 5000, accessory);
                    drawNaelQuote_Circle(sid, 5000, 3000, accessory);
                    break;
                }
            case "6497":
                {
                    // 我降临于此，对月长啸！
                    drawNaelQuote_Spread(sid, 0, 5000, accessory);
                    drawNaelQuote_Donut(sid, 5000, 3000, accessory);
                    break;
                }
            case "6500":
                {
                    // 超新星啊，更加闪耀吧！在星降之夜，称赞红月！
                    drawMeteorStream(sid, 12000, 3000, accessory);
                    drawNaelQuote_Tank(sid, 15000, 2000, accessory);
                    break;
                }
            case "6501":
                {
                    // 超新星啊，更加闪耀吧！照亮红月下炽热之地！
                    drawNaelQuote_Tank(sid, 13000, 2000, accessory);
                    drawNaelQuote_Stack(sid, 15000, 2000, accessory);
                    break;
                }
        }
    }
    #endregion

    #region P2：小龙

    [ScriptMethod(name: "P2奈尔：小龙位置记录（不可控）", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(816[34567])$"], userControl: false)]
    public void CauterizePosRecord(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;
        lock (CauterizeDragons)
        {
            var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
            var dir = PositionTo8Dir(pos, new(0, 0, 0));
            CauterizeDragons.Add(sid, dir);
        }
    }

    [ScriptMethod(name: "P2奈尔：小龙俯冲引导范围预警", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0014"])]
    public void CauterizeTarget(Event @event, ScriptAccessory accessory)
    {
        if (phase != UCOB_Phase.Nael) return;

        CauterizeTimes++;
        if (!ParseObjectId(@event["TargetId"], out var tid)) return;

        // 按照方向（dir）升序排序 CauterizeDragons
        var sortedDragons = CauterizeDragons
            .OrderBy(d => d.Value)  // 根据dir升序排序
            .ToList();

        if (!showOtherCauterizeRoute && tid != accessory.Data.Me) return;

        switch (CauterizeTimes)
        {
            case 1:
                CauterizeRouteDraw(sortedDragons[0].Key, tid, accessory);
                CauterizeRouteDraw(sortedDragons[1].Key, tid, accessory);
                break;
            case 2:
                CauterizeRouteDraw(sortedDragons[2].Key, tid, accessory);
                break;
            case 3:
                CauterizeRouteDraw(sortedDragons[3].Key, tid, accessory);
                CauterizeRouteDraw(sortedDragons[4].Key, tid, accessory);
                break;
        }
    }
    private void CauterizeRouteDraw(uint sid, uint tid, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"俯冲引导{sid}";
        dp.Scale = new(20, 45);
        dp.Owner = sid;
        dp.DestoryAt = 6000;
        dp.Color = accessory.Data.DefaultDangerColor.WithW(0.5f);
        dp.TargetObject = tid;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
    }

    [ScriptMethod(name: "P2奈尔：小龙俯冲实际范围预警", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(993[12345])$"])]
    public void CauterizeField(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "小龙俯冲";
        dp.Scale = new(20, 45);
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Owner = sid;
        dp.DestoryAt = 3700;
        dp.Color = colorCyan.V4.WithW(0.6f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    #endregion

    #region P3：巴哈（进军~灾厄）

    [ScriptMethod(name: "P3巴哈：【进军】阶段记录（不可控）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9954"], userControl: false)]
    public void P3_PhaseChange_1st(Event @event, ScriptAccessory accessory)
    {
        // if (phase != UCOB_Phase.Nael) return;
        phase = UCOB_Phase.Quickmarch_1st;
    }

    // 双塔冲
    [ScriptMethod(name: "P3巴哈：双塔旋风冲", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9906"])]
    public void TwistingDive(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "旋风冲范围";
        dp.Scale = new(8, 45);
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Color = accessory.Data.DefaultDangerColor.WithW(3);
        dp.Owner = sid;
        dp.DestoryAt = 4000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        drawTwister(0, 5200, accessory);
    }

    [ScriptMethod(name: "P3巴哈：奈尔月流冲", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9923"])]
    public void LunarDive(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "月流冲";
        dp.Scale = new(8, 45);
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Color = accessory.Data.DefaultDangerColor.WithW(3);
        dp.Owner = sid;
        dp.DestoryAt = 4000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "P3巴哈：巴哈百万核爆冲与分散", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9953"])]
    public void MegaFlareDive(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "百万核爆冲";
        dp.Scale = new(12, 45);
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Color = accessory.Data.DefaultDangerColor.WithW(3);
        dp.Owner = sid;
        dp.DestoryAt = 4000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        if (phase != UCOB_Phase.Quickmarch_1st) return;
        for (var i = 0; i < 8; i++)
        {
            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "百万核爆冲";
            dp.Scale = new(5);
            dp.ScaleMode = ScaleMode.ByTime;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Owner = accessory.Data.PartyList[i];
            dp.Delay = 4000;
            dp.DestoryAt = 4000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    [ScriptMethod(name: "P3巴哈：大地摇动范围预警", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0028"])]
    public void EarthShaker(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["TargetId"], out var tid)) return;

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"大地摇动{tid}";
        dp.Position = new(0, 0, 0);
        dp.Scale = new(50);
        dp.Radian = float.Pi / 2;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.TargetObject = tid;
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "P3巴哈：【进军】百万核爆冲位置记录（不可控）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9953"], userControl: false)]
    public void MegaFlareDivePosRecord(Event @event, ScriptAccessory accessory)
    {
        if (phase != UCOB_Phase.Quickmarch_1st) return;
        QuickMarchPos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
    }

    [ScriptMethod(name: "P3巴哈：【进军】临时北（俯冲起始点）指向提示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9953"])]
    public void MegaFlareDiveNorth(Event @event, ScriptAccessory accessory)
    {
        if (phase != UCOB_Phase.Quickmarch_1st) return;
        Task.Delay(100).ContinueWith(t =>
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "进军12点标记";
            dp.Scale = new(1.5f);
            dp.ScaleMode = ScaleMode.YByDistance;
            dp.Color = posColorNormal.V4.WithW(2);
            dp.Position = new(0, 0, 0);
            dp.TargetPosition = QuickMarchPos;
            dp.Delay = 4000;
            dp.DestoryAt = 11000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
        });
    }

    [ScriptMethod(name: "P3巴哈：【进军】核爆分摊指路", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0027"])]
    public void QuickmarchStack(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["TargetId"], out var tid)) return;
        if (phase != UCOB_Phase.Quickmarch_1st) return;
        if (QuickMarchStackDrawn) return;

        QuickMarchStackDrawn = true;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"核爆分摊提示{tid}";
        dp.Owner = tid;
        dp.Scale = new(3);
        dp.Radian = float.Pi / 2;
        dp.Color = tid == accessory.Data.Me ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        // 核爆分摊位置提示
        var rad = FindRadian(new(0, 0, 0), QuickMarchPos);
        var stackPos = ExtendPoint(new(0, 0, 0), rad, -6);

        dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"核爆分摊位置{tid}";
        dp.Scale = new(3f);
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Color = tid == accessory.Data.Me ? posColorPlayer.V4.WithW(3) : posColorNormal.V4.WithW(1);
        dp.Position = stackPos;
        dp.Delay = 0;
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        if (tid != accessory.Data.Me) return;
        accessory.Method.TextInfo("即将参与分摊", 5000, true);
        dp = accessory.Data.GetDefaultDrawProperties();
        dp.Scale = new(0.5f);
        dp.Name = $"分摊位置指路{tid}";
        dp.Owner = accessory.Data.Me;
        dp.TargetPosition = stackPos;
        dp.ScaleMode = ScaleMode.YByDistance;
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Delay = 0;
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    [ScriptMethod(name: "P3巴哈：【进军】大地摇动站位指示", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0028"])]
    public void EarthShakerDir(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["TargetId"], out var tid)) return;
        if (phase != UCOB_Phase.Quickmarch_1st) return;
        if (tid != accessory.Data.Me) return;
        if (QuickMarchEarthShakerDrawn) return;

        QuickMarchEarthShakerDrawn = true;
        accessory.Method.RemoveDraw($"进军12点标记");
        var esPos_right = RotatePoint(QuickMarchPos, new(0, 0, 0), float.Pi / 2);
        var esPos_left = RotatePoint(QuickMarchPos, new(0, 0, 0), -float.Pi / 2);

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "大地摇动线指路-中";
        dp.Scale = new(1.5f);
        dp.ScaleMode = ScaleMode.YByDistance;
        dp.Color = getPlayerIdIndex(accessory, accessory.Data.Me) > 3 ? posColorPlayer.V4.WithW(3) : posColorNormal.V4.WithW(1);
        dp.Position = new(0, 0, 0);
        dp.TargetPosition = QuickMarchPos;
        dp.Delay = 0;
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);

        dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "大地摇动线指路-右";
        dp.Scale = new(1.5f);
        dp.ScaleMode = ScaleMode.YByDistance;
        dp.Color = getPlayerIdIndex(accessory, accessory.Data.Me) == 3 ? posColorPlayer.V4.WithW(3) : posColorNormal.V4.WithW(1);
        dp.Position = new(0, 0, 0);
        dp.TargetPosition = esPos_right;
        dp.Delay = 0;
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);

        dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "大地摇动线指路-左";
        dp.Scale = new(1.5f);
        dp.ScaleMode = ScaleMode.YByDistance;
        dp.Color = getPlayerIdIndex(accessory, accessory.Data.Me) == 2 ? posColorPlayer.V4.WithW(3) : posColorNormal.V4.WithW(1);
        dp.Position = new(0, 0, 0);
        dp.TargetPosition = esPos_left;
        dp.Delay = 0;
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);

    }

    [ScriptMethod(name: "P3巴哈：【黑炎】阶段记录（不可控）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9955"], userControl: false)]
    public void P3_PhaseChange_2nd(Event @event, ScriptAccessory accessory)
    {
        // if (phase != UCOB_Phase.Quickmarch_1st) return;
        phase = UCOB_Phase.Blackfire_2nd;
    }

    [ScriptMethod(name: "P3巴哈：奈尔位置定位（不可控）", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:8161"], userControl: false)]
    public void NaelPosRecord(Event @event, ScriptAccessory accessory)
    {
        var spos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        NaelPosition = spos;
    }

    [ScriptMethod(name: "P3巴哈：【黑炎】奈尔位置指路", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:8161"])]
    public void BlackFireNaelDir(Event @event, ScriptAccessory accessory)
    {
        if (phase != UCOB_Phase.Blackfire_2nd) return;
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;

        Task.Delay(100).ContinueWith(t =>
        {
            if (new Vector2(NaelPosition.X, NaelPosition.Z).Length() < 23) return;
            if (blackFireDrawn) return;
            if (DebugMode)
            {
                accessory.Method.SendChat($"/e 找到奈尔位置，在其脚下绘图，{new Vector2(NaelPosition.X, NaelPosition.Z).Length()}");
                var dp0 = accessory.Data.GetDefaultDrawProperties();
                dp0.Name = $"奈尔位置";
                dp0.Scale = new(3f);
                dp0.Color = posColorPlayer.V4.WithW(3);
                dp0.Position = NaelPosition;
                dp0.Delay = 0;
                dp0.DestoryAt = 3000;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp0);
            }
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "奈尔位置指路";
            dp.Scale = new(0.5f, 24);
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Owner = accessory.Data.Me;
            dp.TargetObject = sid;
            dp.DestoryAt = 3000;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            blackFireDrawn = true;
        });
    }

    [ScriptMethod(name: "P3巴哈：【灾厄】阶段记录（不可控）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9956"], userControl: false)]
    public void P3_PhaseChange_3rd(Event @event, ScriptAccessory accessory)
    {
        // if (phase != UCOB_Phase.Blackfire_2nd) return;
        phase = UCOB_Phase.Fellruin_3rd;
    }

    // 我降临于此对月长啸！召唤星降之夜！
    [ScriptMethod(name: "P3巴哈：【灾厄】分散月环", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:6502"])]
    public void FR_Spread_and_In(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;
        drawNaelQuote_Spread(sid, 0, 5000, accessory);
        drawNaelQuote_Donut(sid, 5000, 3000, accessory);
    }

    // 我自月而来降临于此，召唤星降之夜！
    [ScriptMethod(name: "P3巴哈：【灾厄】月环分散", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:6503"])]
    public void FR_In_and_Spread(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;
        drawNaelQuote_Donut(sid, 0, 5000, accessory);
        drawNaelQuote_Spread(sid, 5000, 3000, accessory);
    }

    [ScriptMethod(name: "P3巴哈：【灾厄】以太失控后陨石流", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9905"])]
    public void AethericProfusion(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;
        drawMeteorStream(sid, 0, 4000, accessory);
    }

    #endregion

    #region P3：巴哈（天地）

    [ScriptMethod(name: "P3巴哈：【天地】阶段记录（不可控）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9957"], userControl: false)]
    public void P3_PhaseChange_4th(Event @event, ScriptAccessory accessory)
    {
        // if (phase != UCOB_Phase.Fellruin_3rd) return;
        phase = UCOB_Phase.Heavensfall_4th;
    }

    [ScriptMethod(name: "P3巴哈：【天地】起始位置记录（不可控）", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:regex:^(8161|8159|8168)$"], userControl: false)]
    public void HeavensFallPosRecord(Event @event, ScriptAccessory accessory)
    {
        if (phase != UCOB_Phase.Heavensfall_4th) return;
        var spos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        float distance = new Vector2(spos.X, spos.Z).Length();
        if (distance < 23) return;
        var idx = PositionTo8Dir(spos, new(0, 0, 0));

        // 更新boss位置和危险位置
        HeavensFallBossPos[idx] = true;

        // 设置与idx对面的位置为危险
        HeavensFallDangerPos[idx] = true;
        HeavensFallDangerPos[idx >= 4 ? idx - 4 : idx + 4] = true;
    }

    [ScriptMethod(name: "P3巴哈：【天地】起始位置指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9906"])]
    public void HeavensFallSafeDir(Event @event, ScriptAccessory accessory)
    {
        if (phase != UCOB_Phase.Heavensfall_4th) return;
        Task.Delay(100).ContinueWith(t =>
        {
            // 此时，HeavensFallDangerPos中仅剩两个false，即为安全点。
            // 检查HeavensFallBossPos中，这两个false idx的前一个变量，如果是true，代表右边；如果是false，代表左边
            // 查找HeavensFallDangerPos中剩下的两个false
            List<int> safeIndices = new List<int>();
            for (int i = 0; i < HeavensFallDangerPos.Count(); i++)
            {
                if (!HeavensFallDangerPos[i])  // 只关心值为false的索引
                    safeIndices.Add(i);
            }

            if (safeIndices.Count() == 2)
            {
                var MyIndex = getMyIndex(accessory);
                // 检查HeavensFallBossPos中，安全位置的前一个位置
                foreach (var idx in safeIndices)
                {
                    int prevIdx = idx == 0 ? 7 : idx - 1;  // 如果idx是0，则前一个是7，否则是idx - 1
                    // 判断前一个位置的boss是否存在
                    bool isPrevTrue = HeavensFallBossPos[prevIdx];
                    if (isPrevTrue)
                    {
                        // 如果前一个位置为true，代表该idx为三兄弟右边安全点，D2与D4
                        Vector3 safePosition = RotatePoint(new Vector3(0, 0, -23), new Vector3(0, 0, 0), idx * float.Pi / 4);
                        HeavensFallSafeDirDraw(safePosition, MyIndex == 5 || MyIndex == 7, accessory);
                        if (DebugMode)
                            accessory.Method.SendChat($"/e {idx} 是右边的安全点");
                    }
                    else
                    {
                        // 如果前一个位置为false，代表该idx为三兄弟左边安全点，MT与H1
                        Vector3 safePosition = RotatePoint(new Vector3(0, 0, -23), new Vector3(0, 0, 0), idx * float.Pi / 4);
                        HeavensFallSafeDirDraw(safePosition, MyIndex == 0 || MyIndex == 2, accessory);
                        if (DebugMode)
                            accessory.Method.SendChat($"/e {idx} 是左边的安全点");
                    }
                }

                // 奈尔面前安全点，ST与H2
                var idx_nael = PositionTo8Dir(NaelPosition, new(0, 0, 0));
                Vector3 safePositionNael = RotatePoint(new Vector3(0, 0, -23), new Vector3(0, 0, 0), idx_nael * float.Pi / 4);
                HeavensFallSafeDirDraw(safePositionNael, MyIndex == 1 || MyIndex == 3, accessory);
                if (DebugMode)
                    accessory.Method.SendChat($"/e {idx_nael} 是奈尔面前的安全点");

                // 奈尔身后安全点，D1与D3
                idx_nael = PositionTo8Dir(NaelPosition, new(0, 0, 0));
                idx_nael = idx_nael >= 4 ? idx_nael - 4 : idx_nael + 4;
                safePositionNael = RotatePoint(new Vector3(0, 0, -23), new Vector3(0, 0, 0), idx_nael * float.Pi / 4);
                HeavensFallSafeDirDraw(safePositionNael, MyIndex == 4 || MyIndex == 6, accessory);
                if (DebugMode)
                    accessory.Method.SendChat($"/e {idx_nael} 是奈尔身后的安全点");
            }
        });
    }

    private void HeavensFallSafeDirDraw(Vector3 safepos, bool isPlayerPos, ScriptAccessory accessory)
    {
        if (isPlayerPos)
        {
            var dp0 = accessory.Data.GetDefaultDrawProperties();
            dp0.Name = "天地安全位置指路";
            dp0.Scale = new(0.5f, 22);
            dp0.ScaleMode |= ScaleMode.YByDistance;
            dp0.Color = accessory.Data.DefaultSafeColor;
            dp0.Owner = accessory.Data.Me;
            dp0.TargetPosition = safepos;
            dp0.Delay = 0;
            dp0.DestoryAt = 3700;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);
        }

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "天地安全位置";
        dp.Scale = new(4);
        dp.Color = isPlayerPos ? posColorPlayer.V4.WithW(3) : posColorNormal.V4;
        dp.Position = safepos;
        dp.Delay = 0;
        dp.DestoryAt = 3700;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P3巴哈：【天地】塔位置记录（不可控）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9951"], userControl: false)]
    public void HeavensFallTower(Event @event, ScriptAccessory accessory)
    {
        if (phase != UCOB_Phase.Heavensfall_4th) return;
        var spos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        var idx_tower = PositionTo16Dir(spos, new(0, 0, 0));
        HeavensFallTowerPos[idx_tower] = true;
    }

    [ScriptMethod(name: "P3巴哈：【天地】塔位置指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9951"])]
    public void HeavensFallTowerDir(Event @event, ScriptAccessory accessory)
    {
        if (phase != UCOB_Phase.Heavensfall_4th) return;

        Task.Delay(100).ContinueWith(t =>
        {
            if (HeavensFallTowerDrawn) return;
            HeavensFallTowerDrawn = true;
            var naelIdx = PositionTo16Dir(NaelPosition, new(0, 0, 0));
            var towerJudgeIdx = naelIdx;
            var towerPlayerIdxCount = 0;
            var MyIndex = getMyIndex(accessory);
            List<int> towerPlayerTarget = [7, 0, 6, 1, 5, 2, 4, 3];

            for (int i = 0; i < HeavensFallTowerPos.Count(); i++)
            {
                if (DebugMode)
                {
                    accessory.Method.SendChat($"/e 正在寻找位置{towerJudgeIdx}的塔，是第{towerPlayerIdxCount}座塔。");
                }
                if (HeavensFallTowerPos[towerJudgeIdx])  // 找到塔的位置
                {
                    if (towerPlayerIdxCount == towerPlayerTarget[MyIndex])
                    {
                        // 找到了，就是他！
                        var dp0 = accessory.Data.GetDefaultDrawProperties();
                        dp0.Name = $"目标塔{towerJudgeIdx}";
                        dp0.Scale = new(3f);
                        dp0.Color = posColorPlayer.V4.WithW(3);
                        dp0.Position = RotatePoint(new Vector3(0, 0, -22), new Vector3(0, 0, 0), towerJudgeIdx * float.Pi / 8);
                        dp0.Delay = 0;
                        dp0.DestoryAt = 6500;
                        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp0);

                        dp0 = accessory.Data.GetDefaultDrawProperties();
                        dp0.Name = $"击退位置{towerJudgeIdx}";
                        dp0.Scale = new(1f);
                        dp0.Color = posColorPlayer.V4;
                        dp0.Position = RotatePoint(new Vector3(0, 0, -10), new Vector3(0, 0, 0), towerJudgeIdx * float.Pi / 8);
                        dp0.Delay = 0;
                        dp0.DestoryAt = 6500;
                        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp0);

                        dp0 = accessory.Data.GetDefaultDrawProperties();
                        dp0.Name = "天地击退塔位置指路";
                        dp0.Scale = new(0.5f);
                        dp0.ScaleMode |= ScaleMode.YByDistance;
                        dp0.Color = accessory.Data.DefaultSafeColor;
                        dp0.Owner = accessory.Data.Me;
                        dp0.TargetPosition = RotatePoint(new Vector3(0, 0, -10), new Vector3(0, 0, 0), towerJudgeIdx * float.Pi / 8);
                        dp0.Delay = 0;
                        dp0.DestoryAt = 6500;
                        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);
                    }
                    towerPlayerIdxCount++;
                }
                towerJudgeIdx++;
                if (towerJudgeIdx == HeavensFallTowerPos.Count())
                    towerJudgeIdx = 0;
            }
        });
    }

    [ScriptMethod(name: "P3巴哈：【天地】中心塔与击退指示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9911"])]
    public void HeavensFallMiddleTower(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "天崩地裂";
        dp.Scale = new(4);
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Position = new(0, 0, 0);
        dp.DestoryAt = 5000;
        dp.Color = accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        var dp2 = accessory.Data.GetDefaultDrawProperties();
        dp2.Name = "天崩地裂击退";
        dp2.Scale = new(1.5f, 12);
        dp2.Owner = accessory.Data.Me;
        dp2.TargetPosition = new(0, 0, 0);
        dp2.Rotation = float.Pi;
        dp2.Color = accessory.Data.DefaultSafeColor;
        dp2.DestoryAt = 6000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp2);
    }

    #endregion
    #region P3：巴哈（连击）

    [ScriptMethod(name: "P3巴哈：【连击】阶段记录（不可控）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9958"], userControl: false)]
    public void P3_PhaseChange_5th(Event @event, ScriptAccessory accessory)
    {
        // if (phase != UCOB_Phase.Heavensfall_4th) return;
        phase = UCOB_Phase.Tenstrike_5th;
    }

    [ScriptMethod(name: "P3巴哈：【连击】逐个陨石流预警", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9958"])]
    public void TenStrikeMeteorStream(Event @event, ScriptAccessory accessory)
    {
        Task.Delay(7500).ContinueWith(t =>
        {
            if (phase != UCOB_Phase.Tenstrike_5th) return;
            for (var i = 0; i < 8; i++)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"陨石流{i}";
                dp.Scale = new(4);
                dp.Owner = accessory.Data.PartyList[i];
                dp.DestoryAt = 15000;
                dp.Color = accessory.Data.DefaultDangerColor.WithW(0.5f);
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }
        });
    }

    [ScriptMethod(name: "P3巴哈：【连击】场中-拘束器-场边三点一线指引", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9958"])]
    public void TenStrikeOrbTargetRoute(Event @event, ScriptAccessory accessory)
    {
        Task.Delay(8500).ContinueWith(t =>
        {
            if (phase != UCOB_Phase.Tenstrike_5th) return;
            var MyIndex = getMyIndex(accessory);
            for (var i = 0; i < 3; i++)
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"三点一线{i}";
                dp.Scale = new(2f);
                dp.ScaleMode = ScaleMode.YByDistance;
                dp.Position = new(0, 0, 0);
                dp.TargetPosition = new(RestrictorPos[i].X / RestrictorPos[i].Length() * 24, 0, RestrictorPos[i].Z / RestrictorPos[i].Length() * 24);
                dp.DestoryAt = 10000;
                dp.Color = isTenStrikeTarget ? posColorPlayer.V4.WithW(2f) : colorRed.V4.WithW(2f);
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
            }
        });
    }

    [ScriptMethod(name: "P3巴哈：【连击】逐个陨石流预警删除（不可控）", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9920", "TargetIndex:1"], userControl: false)]
    public void MB_TyrantsFire(Event @event, ScriptAccessory accessory)
    {
        if (phase != UCOB_Phase.Tenstrike_5th) return;
        if (!ParseObjectId(@event["TargetId"], out var tid)) return;
        var tidx = getPlayerIdIndex(accessory, tid);
        accessory.Method.RemoveDraw($"陨石流{tidx}");
    }

    [ScriptMethod(name: "P3巴哈：【连击】撞球截球提示与指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9902"])]
    public void TenStrikeBlackOrbDir(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;
        if (phase != UCOB_Phase.Tenstrike_5th) return;
        if (TenStrikeBlackOrbDrawn) return;

        int trueCount = 0;
        for (int i = 0; i < GenerateTarget.Count(); i++)
        {
            if (GenerateTarget[i])
                trueCount++;
        }
        if (trueCount != 3) return;
        TenStrikeBlackOrbDrawn = true;

        // 算出每组黑球玩家数目，给出提示信息
        List<int> TenStrikeBlackOrbGroupTargetNum = calcTenStrikeGroupTargetNum(GenerateTarget);
        if (DebugMode)
        {
            string tenStrikeTargetNum = string.Join(", ", TenStrikeBlackOrbGroupTargetNum);
            accessory.Method.SendChat($"/e 每组黑球玩家数：{tenStrikeTargetNum}");
        }

        if (showGlobalTenStrikeBlackOrbMsg)
            showTenStrikeBlackOrbMsg(TenStrikeBlackOrbGroupTargetNum, accessory);

        // 计算撞球与截球优先级，输出任务List
        // 注意，截球优先级并无唯一规则，需要观察场上情况灵活变通。
        List<int> missionList = judgeTenStrikeBlackOrbRoute(GenerateTarget);
        if (showGlobalTenStrikeBlackOrbMsg)
        {
            string mission_record = "";
            mission_record += $"H1组：撞球{getPlayerJobIndexByIdx(missionList[0])}, 截球{getPlayerJobIndexByIdx(missionList[1])}\n";
            mission_record += $"H2组：撞球{getPlayerJobIndexByIdx(missionList[2])}, 截球{getPlayerJobIndexByIdx(missionList[3])}\n";
            mission_record += $"D3D4组：撞球{getPlayerJobIndexByIdx(missionList[4])}, 截球{getPlayerJobIndexByIdx(missionList[5])}\n";
            accessory.Method.SendChat($"/e {mission_record}");
        }

        int routeDestoryTime1 = 5000;   // 第一次指路持续时间 & 第二次指路出现时间
        int routeDestoryTime2 = 5000;   // 第二次指路（截球人）持续时间
        // 拘束器idx：0为H1组D，1为D3D4组C，2为H2组B
        drawBlackOrbRoute(missionList[0], 0, 0, routeDestoryTime1, true, accessory);
        drawBlackOrbRoute(missionList[1], 0, 0, routeDestoryTime1, false, accessory);
        drawBlackOrbRoute(missionList[1], 0, routeDestoryTime1, routeDestoryTime2, true, accessory);
        drawBlackOrbRoute(missionList[2], 2, 0, routeDestoryTime1, true, accessory);
        drawBlackOrbRoute(missionList[3], 2, 0, routeDestoryTime1, false, accessory);
        drawBlackOrbRoute(missionList[3], 2, routeDestoryTime1, routeDestoryTime2, true, accessory);
        drawBlackOrbRoute(missionList[4], 1, 0, routeDestoryTime1, true, accessory);
        drawBlackOrbRoute(missionList[5], 1, 0, routeDestoryTime1, false, accessory);
        drawBlackOrbRoute(missionList[5], 1, routeDestoryTime1, routeDestoryTime2, true, accessory);
    }

    private List<int> calcTenStrikeGroupTargetNum(List<bool> targets)
    {
        List<int> TenStrikeBlackOrbGroupTargetNum = [0, 0, 0];
        for (int i = 0; i < 8; i++)
        {
            if (targets[i])
            {
                if (i == 0 | i == 2 | i == 4)
                    TenStrikeBlackOrbGroupTargetNum[0]++;
                else if (i == 1 | i == 3 | i == 5)
                    TenStrikeBlackOrbGroupTargetNum[1]++;
                else if (i == 6 | i == 7)
                    TenStrikeBlackOrbGroupTargetNum[2]++;
            }
        }
        return TenStrikeBlackOrbGroupTargetNum;
    }

    private void showTenStrikeBlackOrbMsg(List<int> targetNum, ScriptAccessory accessory)
    {
        // TODO 确认消息持续时间
        int msgDuration = 8000;
        switch (targetNum)
        {
            case [1, 1, 1]:
                accessory.Method.TextInfo($"每组1人黑球，无需换位", msgDuration, false);
                break;
            case [2, 1, 0]:
                accessory.Method.TextInfo($"【H1组】：2人黑球，换位至【D3D4组】", msgDuration, true);
                break;
            case [2, 0, 1]:
                accessory.Method.TextInfo($"【H1组】：2人黑球，换位至【H2组】", msgDuration, true);
                break;
            case [1, 2, 0]:
                accessory.Method.TextInfo($"【H2组】：2人黑球，换位至【D3D4组】", msgDuration, true);
                break;
            case [0, 2, 1]:
                accessory.Method.TextInfo($"【H2组】：2人黑球，换位至【H1组】", msgDuration, true);
                break;
            case [1, 0, 2]:
                accessory.Method.TextInfo($"【D3D4组】：2人黑球，换位至【H2组】\n【H1/H2组】：补截球【D3D4组】", msgDuration, true);
                break;
            case [0, 1, 2]:
                accessory.Method.TextInfo($"【D3D4组】：2人黑球，换位至【H1组】\n【H1/H2组】：补截球【D3D4组】", msgDuration, true);
                break;
            case [3, 0, 0]:
                accessory.Method.TextInfo($"【H1组】：3人黑球，换位至其他两组\n【H2/D3D4组】：补截球【H1组】", msgDuration, true);
                break;
            case [0, 3, 0]:
                accessory.Method.TextInfo($"【H2组】：3人黑球，换位至其他两组\n【H1/D3D4组】：补截球【H2组】", msgDuration, true);
                break;
        }
    }

    private List<int> judgeTenStrikeBlackOrbRoute(List<bool> targets)
    {
        // 计算任务分配
        // [H1组拘束器撞球，截球，H2组拘束器撞球，截球，D3D4组拘束器撞球，截球]
        List<int> missionList = [-1, -1, -1, -1, -1, -1];

        // 三组撞球与截球的优先级
        // H1 MT D1 D3 ST D4 D2 H2
        List<int> priority_group1 = [2, 0, 4, 6, 1, 7, 5, 3];
        // H2 ST D2 D4 MT D3 D1 H1
        List<int> priority_group2 = [3, 1, 5, 7, 0, 6, 4, 2];
        // D3 D4 D1 D2 MT ST H1 H2
        List<int> priority_group3 = [6, 7, 4, 5, 0, 1, 2, 3];

        // 第一轮判断 撞球人
        for (int i = 0; i < 8; i++)
        {
            // 如果 判断目标被黑球点名 & 判断目标不在被分配任务的list中 & 对应黑球位置目标仍未确定
            if (targets[priority_group1[i]] && !missionList.Contains(priority_group1[i]) && missionList[0] == -1)
                missionList[0] = priority_group1[i];
            if (targets[priority_group2[i]] && !missionList.Contains(priority_group2[i]) && missionList[2] == -1)
                missionList[2] = priority_group2[i];
            if (targets[priority_group3[i]] && !missionList.Contains(priority_group3[i]) && missionList[4] == -1)
                missionList[4] = priority_group3[i];
        }

        // 第二轮判断 截球人
        for (int i = 0; i < 8; i++)
        {
            // 如果 判断目标未被黑球点名 & 判断目标不在被分配任务的list中 & 对应截球位置目标仍未确定
            if (!targets[priority_group1[i]] && !missionList.Contains(priority_group1[i]) && missionList[1] == -1)
                missionList[1] = priority_group1[i];
            if (!targets[priority_group2[i]] && !missionList.Contains(priority_group2[i]) && missionList[3] == -1)
                missionList[3] = priority_group2[i];
            if (!targets[priority_group3[i]] && !missionList.Contains(priority_group3[i]) && missionList[5] == -1)
                missionList[5] = priority_group3[i];
        }

        return missionList;
    }

    public static string getPlayerJobIndexByIdx(int idx)
    {
        switch (idx)
        {
            case 0: return "MT";
            case 1: return "ST";
            case 2: return "H1";
            case 3: return "H2";
            case 4: return "D1";
            case 5: return "D2";
            case 6: return "D3";
            case 7: return "D4";
            default: return "unknown";
        }
    }

    [ScriptMethod(name: "P3巴哈：【连击】大地摇动指路", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0028"])]
    public void TenstrikeEarthShakerDir(Event @event, ScriptAccessory accessory)
    {
        if (phase != UCOB_Phase.Tenstrike_5th) return;
        if (!ParseObjectId(@event["TargetId"], out var tid)) return;
        TenStrikeEarthShakerNum++;

        Task.Delay(100).ContinueWith(t =>
        {
            if (DebugMode)
                accessory.Method.SendChat($"/e 检测到TenStrikeEarthShakerNum：{TenStrikeEarthShakerNum}");

            if (TenStrikeEarthShakerNum != 4) return;

            if (tid == accessory.Data.Me)
                isEarthShakerFirstRound = true;

            Vector3 safePosition = new(0, 0, -24);
            Vector3 safePosition1 = RotatePoint(safePosition, new(0, 0, 0), float.Pi / 180 * 80);
            Vector3 safePosition2 = RotatePoint(safePosition, new(0, 0, 0), -float.Pi / 180 * 80);
            Vector3 safePosition3 = RotatePoint(safePosition, new(0, 0, 0), float.Pi / 180 * 140);
            Vector3 safePosition4 = RotatePoint(safePosition, new(0, 0, 0), -float.Pi / 180 * 140);

            Vector3 safePosition5 = RotatePoint(safePosition, new(0, 0, 0), float.Pi / 180 * 40);
            Vector3 safePosition6 = RotatePoint(safePosition, new(0, 0, 0), -float.Pi / 180 * 40);
            Vector3 safePosition7 = RotatePoint(safePosition, new(0, 0, 0), float.Pi / 180 * 100);
            Vector3 safePosition8 = RotatePoint(safePosition, new(0, 0, 0), -float.Pi / 180 * 100);

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "连击安全区标记";
            dp.Scale = new(1.5f);
            dp.ScaleMode = ScaleMode.YByDistance;
            dp.Color = posColorNormal.V4.WithW(3);
            dp.Position = new(0, 0, 0);
            dp.DestoryAt = 5000;

            if (isEarthShakerFirstRound)
            {
                dp.Delay = 0;
                dp.TargetPosition = safePosition1;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
                dp.TargetPosition = safePosition2;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
                dp.TargetPosition = safePosition3;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
                dp.TargetPosition = safePosition4;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
            }

            else
            {
                dp.Delay = 5000;
                dp.TargetPosition = safePosition5;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
                dp.TargetPosition = safePosition6;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
                dp.TargetPosition = safePosition7;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
                dp.TargetPosition = safePosition8;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
            }
        });
    }

    #endregion
    #region P3：巴哈（群龙）

    [ScriptMethod(name: "P3巴哈：【群龙】阶段记录（不可控）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9959"], userControl: false)]
    public void P3_PhaseChange_6th(Event @event, ScriptAccessory accessory)
    {
        // if (phase != UCOB_Phase.Heavensfall_5th) return;
        phase = UCOB_Phase.GrandOctet_6th;
    }

    [ScriptMethod(name: "P3巴哈：双塔位置定位（不可控）", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:8159"], userControl: false)]
    public void TwintaniaPosRecord(Event @event, ScriptAccessory accessory)
    {
        var spos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        TwintaniaPosition = spos;
    }

    [ScriptMethod(name: "P3巴哈：巴哈位置定位（不可控）", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:8168"], userControl: false)]
    public void BahamutPosRecord(Event @event, ScriptAccessory accessory)
    {
        var spos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        BahamutPosition = spos;
    }

    [ScriptMethod(name: "P3巴哈：【群龙】起始位置绘图", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:8168"])]
    public void GrandOctStartPosDir(Event @event, ScriptAccessory accessory)
    {
        if (phase != UCOB_Phase.GrandOctet_6th) return;
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;

        Task.Delay(100).ContinueWith(t =>
        {
            float CalculateDistance(Vector3 position) => new Vector2(position.X, position.Z).Length();
            if (CalculateDistance(BahamutPosition) < 23 || CalculateDistance(TwintaniaPosition) < 23 || CalculateDistance(NaelPosition) < 23) return;

            if (grandOctDrawn) return;

            if (DebugMode)
            {
                accessory.Method.SendChat($"/e 找到巴哈位置，在其脚下绘图，{CalculateDistance(BahamutPosition)}");
                var dp0 = accessory.Data.GetDefaultDrawProperties();
                dp0.Name = $"巴哈位置";
                dp0.Scale = new(3f);
                dp0.Color = posColorNormal.V4;
                dp0.Position = BahamutPosition;
                dp0.Delay = 0;
                dp0.DestoryAt = 3000;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp0);
            }

            var BahamutDir = PositionTo8Dir(BahamutPosition, new(0, 0, 0));
            var NaelDir = PositionTo8Dir(NaelPosition, new(0, 0, 0));

            bool isAdvanced = Math.Abs(BahamutDir - NaelDir) == 4;  // 巴哈对面有奈尔，提前一格
            bool isTurnLeft = BahamutDir % 2 == 0;                  // 巴哈在偶数面向（正点），面向场外往左跑
            var startDir = BahamutDir > 3 ? BahamutDir - 4 : BahamutDir + 4;
            if (isAdvanced)
            {
                startDir = isTurnLeft ? startDir - 1 : startDir + 1;
            }

            startDir = (startDir == -1) ? 7 : (startDir == 8) ? 0 : startDir;

            if (DebugMode)
            {
                accessory.Method.SendChat($"/e 起跑位置为：{startDir}");
            }

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"群龙起跑位置";
            dp.Scale = new(5f);
            dp.Color = posColorPlayer.V4.WithW(3);
            dp.Position = RotatePoint(new Vector3(0, 0, -23), new Vector3(0, 0, 0), startDir * float.Pi / 4);
            dp.Delay = 0;
            dp.DestoryAt = 6500;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"群龙起跑位置指路-危险";
            dp.Scale = new(0.5f);
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Owner = accessory.Data.Me;
            dp.TargetPosition = RotatePoint(new Vector3(0, 0, -23), new Vector3(0, 0, 0), startDir * float.Pi / 4);
            dp.Delay = 0;
            dp.DestoryAt = 5000;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

            accessory.Method.TextInfo("等待标记出现后，前往起跑点", 3000);

            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"群龙起跑位置指路-安全";
            dp.Scale = new(0.5f);
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Owner = accessory.Data.Me;
            dp.TargetPosition = RotatePoint(new Vector3(0, 0, -23), new Vector3(0, 0, 0), startDir * float.Pi / 4);
            dp.Delay = 5000;
            dp.DestoryAt = 3000;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

            grandOctDrawn = true;
        });
    }

    [ScriptMethod(name: "P3巴哈：【群龙】点名记录（不可控）", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(0077|0029|0014)$"], userControl: false)]
    public void GrandOctTargetRecord(Event @event, ScriptAccessory accessory)
    {
        if (phase != UCOB_Phase.GrandOctet_6th) return;
        if (!ParseObjectId(@event["TargetId"], out var tid)) return;
        grandOctIconNum++;
        grandOctTargetChosen[getPlayerIdIndex(accessory, tid)] = true;
    }

    [ScriptMethod(name: "P3巴哈：【群龙】起跑提示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9923"])]
    public void GrandOctStartMention(Event @event, ScriptAccessory accessory)
    {
        if (phase != UCOB_Phase.GrandOctet_6th) return;

        var BahamutDir = PositionTo8Dir(BahamutPosition, new(0, 0, 0));
        bool isTurnLeft = BahamutDir % 2 == 0;
        if (isTurnLeft)
            accessory.Method.TextInfo("等待奈尔冲锋后，面向场外向【左】跑", 3000);
        else
            accessory.Method.TextInfo("等待奈尔冲锋后，面向场外向【右】跑", 3000);
    }

    [ScriptMethod(name: "P3巴哈：【群龙】回中提示、双塔位置及引导提示", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0029"])]
    public void GrandOctTarget(Event @event, ScriptAccessory accessory)
    {
        if (phase != UCOB_Phase.GrandOctet_6th) return;
        Task.Delay(100).ContinueWith(t =>
        {
            if (grandOctIconNum != 7) return;
            var MyIndex = getMyIndex(accessory);
            var TwintaniaTargetIdx = 0;
            for (int i = 0; i < grandOctTargetChosen.Count(); i++)
            {
                if (!grandOctTargetChosen[i])  // 只关心值为false的索引
                    TwintaniaTargetIdx = i;
            }
            if (MyIndex == TwintaniaTargetIdx)
            {
                accessory.Method.TextInfo("回中，准备引导双塔尼亚", 3000, true);
            }
            else
            {
                accessory.Method.TextInfo("回中，寻找双塔尼亚，观察点名", 3000, true);
            }

            if (showOtherCauterizeRoute || MyIndex == TwintaniaTargetIdx)
            {
                var dp0 = accessory.Data.GetDefaultDrawProperties();
                dp0.Name = $"群龙双塔俯冲引导";
                dp0.Scale = new(8, 45);
                dp0.Position = TwintaniaPosition;
                dp0.TargetObject = accessory.Data.PartyList[TwintaniaTargetIdx];
                dp0.Delay = 3500;
                dp0.DestoryAt = 5500;
                dp0.Color = accessory.Data.DefaultDangerColor.WithW(0.5f);
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp0);
            }

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"群龙双塔位置标记";
            dp.Scale = new(1.5f);
            dp.ScaleMode = ScaleMode.YByDistance;
            dp.Color = posColorNormal.V4.WithW(2);
            dp.Position = new(0, 0, 0);
            dp.TargetPosition = TwintaniaPosition;
            dp.Delay = 2000;
            dp.DestoryAt = 3500;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);

        });
    }

    #endregion

    #region P4：亿万核爆

    [ScriptMethod(name: "P4双打：阶段记录（不可控）", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9960", "TargetIndex:2"], userControl: false)]
    public void P4_PhaseChange(Event @event, ScriptAccessory accessory)
    {
        phase = UCOB_Phase.BahamutFavor;
        if (DebugMode)
        {
            accessory.Method.SendChat($"/e 检测到进入P4。");
        }
    }
    private void drawBlackOrbRoute(int pidx, int residx, int delay, int destoryAt, bool isSafe, ScriptAccessory accessory)
    {
        // pidx: 玩家index
        // residx：拘束器index
        if (pidx != getMyIndex(accessory)) return;

        if (phase == UCOB_Phase.BahamutFavor)
        {
            if (residx == 0)
                accessory.Method.TextInfo("先躲避旋风，后撞球", 3000, false);
            else
                accessory.Method.TextInfo("先撞球，后躲避旋风", 3000, false);
        }

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"拘束器位置定位{residx}";
        dp.Scale = new(1.5f);
        dp.Position = RestrictorPos[residx];
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        dp.Color = posColorPlayer.V4.WithW(3);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        dp = accessory.Data.GetDefaultDrawProperties();
        dp.Scale = new(0.5f);
        dp.Name = $"拘束器撞球玩家{pidx}指路";
        dp.Owner = accessory.Data.Me;
        dp.TargetPosition = RestrictorPos[residx];
        dp.ScaleMode = ScaleMode.YByDistance;
        dp.Color = isSafe ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    [ScriptMethod(name: "P4双塔：踩拘束器指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9902"])]
    public void BlackOrbDir(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;
        if (phase != UCOB_Phase.BahamutFavor) return;
        int trueCount = 0;
        for (int i = 0; i < GenerateTarget.Count(); i++)
        {
            if (GenerateTarget[i])
                trueCount++;
        }
        if (trueCount != 3) return;

        // 判断4/5/6是否为true，如果是，DoSomething(4/5/6)
        // P4的黑球，D1（B），D2（C），D3（D），D4（补）
        if (GenerateTarget[4])
            drawBlackOrbRoute(4, 2, 0, 5500, true, accessory);
        else
            drawBlackOrbRoute(7, 2, 0, 5500, true, accessory);

        if (GenerateTarget[5])
            drawBlackOrbRoute(5, 1, 0, 5500, true, accessory);
        else
            drawBlackOrbRoute(7, 1, 0, 5500, true, accessory);

        if (GenerateTarget[6])
            drawBlackOrbRoute(6, 0, 0, 5500, true, accessory);
        else
            drawBlackOrbRoute(7, 0, 0, 5500, true, accessory);
    }

    private void drawBahamutFavorNorth(int delay, int destoryAt, ScriptAccessory accessory)
    {
        switch (BahamutFavorNorth)
        {
            case BahamutFavorNorthTypeEnum.画出12点_ShowTempNorth:
                drawBahamutFavorTempNorth(delay, destoryAt, accessory);
                break;
            case BahamutFavorNorthTypeEnum.画出8条分散方向_ShowRecomDir:
                drawBahamutFavorSpreadDir(delay, destoryAt, accessory);
                break;
            case BahamutFavorNorthTypeEnum.不画_DontDraw:
            default:
                return;
        }
    }

    private void drawBahamutFavorTempNorth(int delay, int destoryAt, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"P4的12点标记";
        dp.Scale = new(1.5f);
        dp.ScaleMode = ScaleMode.YByDistance;
        dp.Color = posColorNormal.V4.WithW(2);
        dp.Position = new(0, 0, 0);
        dp.TargetPosition = BahamutFavorPos;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
    }

    private void drawBahamutFavorSpreadDir(int delay, int destoryAt, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        var initRad = FindRadian(new(0, 0, 0), BahamutFavorPos);
        List<float> angles = [-20, 20, -105, 105, -60, 60, -150, 150];

        for (int i = 0; i < 8; i++)
        {
            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"P4分散方向标记{i}";
            dp.Scale = new(1.25f);
            dp.ScaleMode = ScaleMode.YByDistance;
            dp.Color = i == getMyIndex(accessory) ? posColorPlayer.V4.WithW(5f) : posColorNormal.V4.WithW(5f);
            dp.Position = new(0, 0, 0);
            dp.TargetPosition = ExtendPoint(new Vector3(0, 0, 0), initRad + angle2Rad(angles[i]), 20);
            dp.Delay = delay;
            dp.DestoryAt = destoryAt;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
        }
    }

    [ScriptMethod(name: "P4奈尔：台词与临时北方向指引", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:regex:^(650[4567])$"])]
    public void NaelQuotesP4(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;
        var quoteId = @event["Id"];

        switch (quoteId)
        {
            case "6504":
                {
                    // 钢铁燃烧吧！成为我降临于此的刀剑吧！
                    // 钢铁、分摊、分散
                    drawNaelQuote_Circle(sid, 0, 5000, accessory);
                    drawNaelQuote_Stack(sid, 5000, 3000, accessory);
                    // 分摊结束后即可指示方向
                    drawBahamutFavorNorth(8000, 8000, accessory);
                    drawNaelQuote_Spread(sid, 8000, 3000, accessory);
                    break;
                }
            case "6505":
                {
                    // 钢铁成为我降临于此的燃烧之剑！
                    // 钢铁、分散、分摊
                    drawNaelQuote_Circle(sid, 0, 5000, accessory);
                    drawNaelQuote_Spread(sid, 5000, 3000, accessory);
                    drawNaelQuote_Stack(sid, 8000, 3000, accessory);
                    // 分摊结束后即可指示方向
                    drawBahamutFavorNorth(11000, 5000, accessory);
                    break;
                }
            case "6506":
                {
                    // 我自月而来降临于此，踏过炽热之地！
                    // 月环、分散、分摊
                    drawNaelQuote_Donut(sid, 0, 5000, accessory);
                    drawNaelQuote_Spread(sid, 5000, 3000, accessory);
                    drawNaelQuote_Stack(sid, 8000, 3000, accessory);
                    // 分摊结束后即可指示方向
                    drawBahamutFavorNorth(11000, 5000, accessory);
                    break;
                }
            case "6507":
                {
                    // 我自月而来携钢铁降临于此！
                    // 月环、钢铁、分散
                    drawNaelQuote_Donut(sid, 0, 5000, accessory);
                    drawNaelQuote_Circle(sid, 5000, 3000, accessory);
                    // 分摊结束后即可指示方向
                    drawBahamutFavorNorth(8000, 8000, accessory);
                    drawNaelQuote_Spread(sid, 8000, 3000, accessory);
                    break;
                }
        }
    }

    #endregion

    #region P5：重生绝境

    [ScriptMethod(name: "P5黄金：阶段记录（不可控）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9964"], userControl: false)]
    public void P5_PhaseChange(Event @event, ScriptAccessory accessory)
    {
        if (phase == UCOB_Phase.FlamesRebirth) return;
        phase = UCOB_Phase.FlamesRebirth;
    }

    [ScriptMethod(name: "P5黄金：无尽顿悟分摊目标", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9964"])]
    public void MornAfahStack(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["TargetId"], out var tid)) return;
        MornAfahNum++;

        if (showStackBusterNum)
            accessory.Method.TextInfo($"无尽顿悟（分摊）#{MornAfahNum}", 4000, true);

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"无尽顿悟分摊";
        dp.Scale = new(4);
        dp.Owner = tid;
        dp.DestoryAt = 4000;
        dp.Color = accessory.Data.DefaultSafeColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P5黄金：死亡轮回死刑目标", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9962"])]
    public void AkhMornTankBuster(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["TargetId"], out var tid)) return;
        ArkMornNum++;

        if (showStackBusterNum)
            accessory.Method.TextInfo($"死亡轮回（死刑）#{ArkMornNum}", 4000, true);

        var MyIndex = getMyIndex(accessory);
        var isTank = MyIndex == 0 || MyIndex == 1;

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"死亡轮回死刑";
        dp.Scale = new(4);
        dp.Owner = tid;
        dp.DestoryAt = 4000;
        dp.Color = isTank ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "P5黄金：百京核爆预警", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9968"])]
    public void Exaflare(Event @event, ScriptAccessory accessory)
    {
        var srot = JsonConvert.DeserializeObject<float>(@event["SourceRotation"]);
        var spos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

        for (int i = 0; i < 6; i++)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"百京核爆{i}";
            dp.Scale = new(6);
            // dp.Rotation = srot;
            dp.ScaleMode = ScaleMode.ByTime;
            dp.Color = exflareColor.V4.WithW(3);
            dp.Position = ExtendPoint(spos, float.Pi - srot, 8 * i);
            dp.Delay = i == 0 ? 0 : 4000 + 1500 * (i - 1);
            dp.DestoryAt = i == 0 ? 4000 : 1500;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            drawExflareWarn(1, i, spos, srot, accessory);
            drawExflareWarn(2, i, spos, srot, accessory);
        }
    }

    private void drawExflareWarn(uint idx, int iter_i, Vector3 spos, float srot, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"百京核爆预警{idx}-{iter_i}";
        dp.Scale = new(6);
        dp.Color = exflareWarnColor.V4.WithW(0.8f / idx);
        dp.Position = ExtendPoint(spos, float.Pi - srot, 8 * (iter_i + idx));
        dp.Delay = iter_i == 0 ? 0 : 4000 + 1500 * (iter_i - 1);
        dp.DestoryAt = 1500 * (idx - 1) + (iter_i == 0 ? 4000 : 1500);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    #endregion
}
