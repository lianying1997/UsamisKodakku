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
using System.Collections;
using System.ComponentModel.DataAnnotations.Schema;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using KodakkuAssist.Module.Draw.Manager;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using System.Runtime;
using System.Timers;
using Lumina.Excel.GeneratedSheets;

namespace UsamisScript;

[ScriptType(name: "UCOB [巴哈姆特绝境战]", territorys: [733], guid: "884e415a-1210-44cc-bdff-8fab6878e87d", version: "0.0.0.2", author: "Joshua and Usami", note: noteStr)]
public class Ucob
{
    const string noteStr =
    """
    【未完成！】
    Original code by Joshua, adjustments by Usami. 
    基于原v0.2/0.3 版本增加个人风格的修改。
    当前版本修改至“P4”。
    """;

    [UserSetting("Debug模式，非开发用请关闭")]
    public bool DebugMode { get; set; } = false;

    [UserSetting("黑球行动轨迹长度")]
    public float blackOrbTrackLength { get; set; } = 4;

    [UserSetting("黑球行动方向绘图颜色")]
    public ScriptColor blackOrbTrackColor { get; set; } = new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 0f, 1.0f) };

    [UserSetting("P2：展示其他玩家的小龙俯冲引导路径")]
    public bool showOtherCauterizeRoute { get; set; } = false;

    public ScriptColor colorRed = new ScriptColor { V4 = new Vector4(1.0f, 0f, 0f, 1.0f) };
    public ScriptColor colorPink = new ScriptColor { V4 = new Vector4(1f, 0f, 1f, 1.0f) };
    public ScriptColor colorCyan = new ScriptColor { V4 = new Vector4(0f, 1f, 1f, 1.0f) };

    [UserSetting("站位提示圈绘图-普通颜色")]
    public ScriptColor posColorNormal { get; set; } = new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 1.0f, 1.0f) };

    [UserSetting("站位提示圈绘图-玩家站位颜色")]
    public ScriptColor posColorPlayer { get; set; } = new ScriptColor { V4 = new Vector4(0.2f, 1.0f, 0.2f, 1.0f) };

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
    uint MyId = 0;
    List<uint> DeathSentenceTarget = [0, 0, 0];     // P2死宣目标
    Dictionary<uint, int> P2_CauterizeDragons = new();   // P2小龙字典（id, 位置）
    int P2_CauterizeTimes = 0;     // P2小龙引导次数
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
    int TenStrikeEarthShakerNum = 0;            // P3连击大地摇动点名次数
    bool isEarthShakerFirstRound = false;       // P3连击大地摇动是否为第一轮
    bool grandOctDrawn = false;                 // P3群龙起始位置绘图完成记录
    int grandOctIconNum = 0;                    // P3群龙点名次数
    List<bool> grandOctTargetChosen = [false, false, false, false, false, false, false, false]; // P3群龙目标选择

    public void Init(ScriptAccessory accessory)
    {
        phase = UCOB_Phase.Twintania;
        MyId = accessory.Data.Me;

        DeathSentenceTarget = [0, 0, 0];        // P2死宣目标
        P2_CauterizeDragons = new();             // P2小龙字典（id, 位置）
        P2_CauterizeTimes = 0;                   // P2小龙引导次数

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

        TenStrikeEarthShakerNum = 0;            // P3连击大地摇动点名次数
        isEarthShakerFirstRound = false;       // P3连击大地摇动是否为第一轮

        grandOctDrawn = false;                  // P3群龙起始位置绘图完成记录
        grandOctIconNum = 0;                    // P3群龙点名次数
        grandOctTargetChosen = [false, false, false, false, false, false, false, false];    // P3群龙目标选择

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

        var rot = (MathF.PI - MathF.Atan2(v2.X, v2.Y) + radian);
        var length = v2.Length();
        return new(centre.X + MathF.Sin(rot) * length, centre.Y, centre.Z - MathF.Cos(rot) * length);
    }

    private Vector3 ExtendPoint(Vector3 centre, float radian, float length)
    {
        return new(centre.X + MathF.Sin(radian) * length, centre.Y, centre.Z - MathF.Cos(radian) * length);
    }

    private float FindAngle(Vector3 centre, Vector3 new_point)
    {
        float angle_rad = MathF.PI - MathF.Atan2(new_point.X - centre.X, new_point.Z - centre.Z);
        if (angle_rad < 0)
            angle_rad += 2 * MathF.PI;
        return angle_rad;
    }

    private int getPlayerIdIndex(ScriptAccessory accessory, uint pid)
    {
        return accessory.Data.PartyList.IndexOf(pid);
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
        dp.Color = colorCyan.V4.WithW(2);
        dp.Position = new(0, 0, 0);
        dp.Delay = 0;
        dp.DestoryAt = 2000;

        dp.TargetPosition = safePosition1;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        dp.TargetPosition = safePosition2;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        dp.TargetPosition = safePosition3;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        dp.TargetPosition = safePosition4;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        dp.Delay = 2000;
        dp.TargetPosition = safePosition5;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
        dp.TargetPosition = safePosition6;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
        dp.TargetPosition = safePosition7;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
        dp.TargetPosition = safePosition8;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);

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
        dp.Delay = 3000;
        dp.DestoryAt = 10000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
    }

    [ScriptMethod(name: "【全局】超新星危险位置", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["DataId:2003393", "Operate:Add"])]
    public void Hypernova_Field(Event @event, ScriptAccessory accessory)
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

    [ScriptMethod(name: "【全局】黑球路径消失（不可控）", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9903"], userControl: false)]
    public void BlackOrbTrackRemove(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;
        accessory.Method.RemoveDraw($"黑球路径-{sid}");
    }

    #endregion

    #region P1：双塔

    [ScriptMethod(name: "P1双塔：旋风自身位置预警", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9898"])]
    public void Twister_PlayerPosition(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.TextInfo("旋风", 2000, true);
        for (var i = 0; i < 8; i++)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"旋风{i}";
            dp.Scale = new(1.5f);
            dp.Owner = accessory.Data.PartyList[i];
            dp.DestoryAt = 2000;
            dp.ScaleMode = ScaleMode.ByTime;
            dp.Color = accessory.Data.DefaultDangerColor.WithW(2);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    [ScriptMethod(name: "P1双塔：旋风危险位置", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["DataId:2001168", "Operate:Add"])]
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

    #endregion

    #region P2：奈尔

    [ScriptMethod(name: "P2双塔：阶段记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9922"], userControl: false)]
    public void P2_PhaseChange(Event @event, ScriptAccessory accessory)
    {
        if (phase != UCOB_Phase.Twintania) return;
        phase = UCOB_Phase.Nael;
    }

    // * 添加
    [ScriptMethod(name: "P2奈尔：死亡宣告记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:210"], userControl: false)]
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

    [ScriptMethod(name: "P2奈尔：救世之翼", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9930"])]
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

    [ScriptMethod(name: "P2奈尔：雷光链(Joshua)", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9927"])]
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

    [ScriptMethod(name: "P2：小龙位置记录（不可控）", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(816[34567])$"], userControl: false)]
    public void CauterizePosRecord(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;
        lock (P2_CauterizeDragons)
        {
            var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
            var dir = PositionTo8Dir(pos, new(0, 0, 0));
            P2_CauterizeDragons.Add(sid, dir);
        }
    }

    [ScriptMethod(name: "P2：小龙俯冲引导预警", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0014"])]
    public void CauterizeTarget(Event @event, ScriptAccessory accessory)
    {
        P2_CauterizeTimes++;
        if (!ParseObjectId(@event["TargetId"], out var tid)) return;

        // 按照方向（dir）升序排序 P2_CauterizeDragons
        var sortedDragons = P2_CauterizeDragons
            .OrderBy(d => d.Value)  // 根据dir升序排序
            .ToList();

        if (!showOtherCauterizeRoute && tid != accessory.Data.Me) return;

        switch (P2_CauterizeTimes)
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

    [ScriptMethod(name: "P2：小龙俯冲范围预警", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(993[12345])$"])]
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

        accessory.Method.TextInfo("旋风", 4000, true);

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "旋风冲范围";
        dp.Scale = new(8, 45);
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Color = accessory.Data.DefaultDangerColor.WithW(3);
        dp.Owner = sid;
        dp.DestoryAt = 4000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        dp = accessory.Data.GetDefaultDrawProperties();
        dp.Scale = new(1.5f);
        dp.DestoryAt = 5200;
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Color = accessory.Data.DefaultDangerColor.WithW(2);
        for (var i = 0; i < 8; i++)
        {
            dp.Name = $"旋风{i}";
            dp.Owner = accessory.Data.PartyList[i];
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
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

    [ScriptMethod(name: "P3巴哈：大地摇动范围预警(Joshua)", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0028"])]
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

    [ScriptMethod(name: "P3巴哈：【进军】12点位置标记", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9953"])]
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
        var rad = FindAngle(new(0, 0, 0), QuickMarchPos);
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
    [ScriptMethod(name: "P3巴哈：【灾厄】分散月环(Joshua)", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:6502"])]
    public void FR_Spread_and_In(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;

        for (var i = 0; i < 8; i++)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"凶鸟冲{i}";
            dp.Scale = new(4);
            dp.ScaleMode = ScaleMode.ByTime;
            dp.Owner = accessory.Data.PartyList[i];
            dp.DestoryAt = 5000;
            dp.Color = accessory.Data.DefaultDangerColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        var dp2 = accessory.Data.GetDefaultDrawProperties();
        dp2.Name = "月环";
        dp2.Scale = new(22);
        dp2.InnerScale = new(6);
        dp2.Radian = float.Pi * 2;
        dp2.Owner = sid;
        dp2.Delay = 5000;
        dp2.DestoryAt = 3000;
        dp2.Color = accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp2);
    }

    // 我自月而来降临于此，召唤星降之夜！
    [ScriptMethod(name: "P3巴哈：【灾厄】月环分散(Joshua)", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:6503"])]
    public void FR_In_and_Spread(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;

        var dp2 = accessory.Data.GetDefaultDrawProperties();
        dp2.Name = "月环";
        dp2.Scale = new(22);
        dp2.InnerScale = new(6);
        dp2.Radian = float.Pi * 2;
        dp2.Owner = sid;
        dp2.DestoryAt = 5000;
        dp2.Color = accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp2);

        for (var i = 0; i < 8; i++)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"凶鸟冲{i}";
            dp.Scale = new(4);
            dp.ScaleMode = ScaleMode.ByTime;
            dp.Owner = accessory.Data.PartyList[i];
            dp.Delay = 5000;
            dp.DestoryAt = 3000;
            dp.Color = accessory.Data.DefaultDangerColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    [ScriptMethod(name: "P3巴哈：【灾厄】以太失控后陨石流", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9905"])]
    public void AethericProfusion(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["TargetId"], out var tid)) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"陨石流{tid}";
        dp.Scale = new(4);
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Owner = tid;
        dp.DestoryAt = 4000;
        dp.Color = accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    #endregion

    #region P3：巴哈（天地~群龙）

    [ScriptMethod(name: "P3巴哈：【天地】阶段记录（不可控）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9957"], userControl: false)]
    public void P3_PhaseChange_4th(Event @event, ScriptAccessory accessory)
    {
        // if (phase != UCOB_Phase.Fellruin_3rd) return;
        phase = UCOB_Phase.Heavensfall_4th;
    }

    // TODO：三兄弟必在正或斜点，修改三个sourceDataId
    [ScriptMethod(name: "P3巴哈：【天地】起始位置记录（不可控）", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:regex:^(8161|8159|8168)$"])]
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
                var MyIndex = getPlayerIdIndex(accessory, accessory.Data.Me);
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
            var MyIndex = getPlayerIdIndex(accessory, accessory.Data.Me);
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
                        dp0.DestoryAt = 7000;
                        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp0);

                        dp0 = accessory.Data.GetDefaultDrawProperties();
                        dp0.Name = $"击退位置{towerJudgeIdx}";
                        dp0.Scale = new(1f);
                        dp0.Color = posColorPlayer.V4;
                        dp0.Position = RotatePoint(new Vector3(0, 0, -10), new Vector3(0, 0, 0), towerJudgeIdx * float.Pi / 8);
                        dp0.Delay = 0;
                        dp0.DestoryAt = 7000;
                        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp0);

                        dp0 = accessory.Data.GetDefaultDrawProperties();
                        dp0.Name = "天地击退塔位置指路";
                        dp0.Scale = new(0.5f);
                        dp0.ScaleMode |= ScaleMode.YByDistance;
                        dp0.Color = accessory.Data.DefaultSafeColor;
                        dp0.Owner = accessory.Data.Me;
                        dp0.TargetPosition = RotatePoint(new Vector3(0, 0, -10), new Vector3(0, 0, 0), towerJudgeIdx * float.Pi / 8);
                        dp0.Delay = 0;
                        dp0.DestoryAt = 7000;
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

    [ScriptMethod(name: "P3巴哈：【天地】中心塔与击退指示 (Joshua)", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9911"])]
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

    [ScriptMethod(name: "P3巴哈：【连击】阶段记录（不可控）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9958"], userControl: false)]
    public void P3_PhaseChange_5th(Event @event, ScriptAccessory accessory)
    {
        // if (phase != UCOB_Phase.Heavensfall_4th) return;
        phase = UCOB_Phase.Tenstrike_5th;
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

    [ScriptMethod(name: "P3巴哈：【群龙】阶段记录（不可控）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9959"])]
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
            bool isTurnLeft = BahamutDir % 2 == 0;      // 巴哈在偶数面向（正点），面向场外往左跑
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

    [ScriptMethod(name: "P3：【群龙】回中与双塔提示", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0029"])]
    public void GrandOctTarget(Event @event, ScriptAccessory accessory)
    {
        if (phase != UCOB_Phase.GrandOctet_6th) return;
        Task.Delay(100).ContinueWith(t =>
        {
            if (grandOctIconNum != 7) return;

            var MyIndex = getPlayerIdIndex(accessory, accessory.Data.Me);

            var TwintaniaTargetIdx = 0;
            for (int i = 0; i < grandOctTargetChosen.Count(); i++)
            {
                if (!grandOctTargetChosen[i])  // 只关心值为false的索引
                    TwintaniaTargetIdx = i;
            }
            if (MyIndex == TwintaniaTargetIdx)
            {
                // TODO: 增加引导标志
                accessory.Method.TextInfo("回中，准备引导双塔尼亚", 3000, true);
            }
            else
                accessory.Method.TextInfo("回中，观察点名", 3000, true);
        });
    }

    #endregion

    #region P4：亿万核爆

    [ScriptMethod(name: "P4奈尔：台词", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:regex:^(650[4567])$"])]
    public void NaelQuotesP4(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;
        var quoteId = @event["Id"];

        switch (quoteId)
        {
            case "6504":
                {
                    // 钢铁燃烧吧！成为我降临于此的刀剑吧！
                    drawNaelQuote_Circle(sid, 0, 5000, accessory);
                    drawNaelQuote_Stack(sid, 5000, 3000, accessory);
                    drawNaelQuote_Spread(sid, 8000, 3000, accessory);
                    break;
                }
            case "6505":
                {
                    // 钢铁成为我降临于此的燃烧之剑！
                    drawNaelQuote_Circle(sid, 0, 5000, accessory);
                    drawNaelQuote_Spread(sid, 5000, 3000, accessory);
                    drawNaelQuote_Stack(sid, 8000, 3000, accessory);
                    break;
                }
            case "6506":
                {
                    // 我自月而来降临于此，踏过炽热之地！
                    drawNaelQuote_Donut(sid, 0, 5000, accessory);
                    drawNaelQuote_Spread(sid, 5000, 3000, accessory);
                    drawNaelQuote_Stack(sid, 8000, 3000, accessory);
                    break;
                }
            case "6507":
                {
                    // 我自月而来携钢铁降临于此！
                    drawNaelQuote_Donut(sid, 0, 5000, accessory);
                    drawNaelQuote_Circle(sid, 5000, 3000, accessory);
                    drawNaelQuote_Spread(sid, 5000, 3000, accessory);
                    break;
                }
        }
    }

    #endregion

    #region P5：重生绝境

    long exaflareTime = DateTimeOffset.Now.ToUnixTimeSeconds();

    [ScriptMethod(name: "百京核爆", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9968"])]
    public void 百京核爆(Event @event, ScriptAccessory accessory)
    {
        var pos = JsonConvert.DeserializeObject<Vector3>(@event["TargetPosition"]);

        lock (this)
        {
            var timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            if (timestamp - this.exaflareTime > 10)
            {
                this.exaflareTime = timestamp;

                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = "百京核爆";
                dp.Scale = new(1.5f, 24);
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.Owner = MyId;
                dp.TargetPosition = pos;
                //dp.Delay = 1000;
                dp.DestoryAt = 3000;

                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
        }
    }

    #endregion


}
