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

[ScriptType(name: "UCOB [巴哈姆特绝境战]", territorys: [733], guid: "d66cf40d-d9f4-4e0a-8782-f2af3d25c355", version: "0.2.alpha", author: "Joshua and Usami", note: "基于Joshua的版本更改")]
public class Ucob
{
    [UserSetting("Debug模式")]
    public bool DebugMode { get; set; } = true;

    public ScriptColor colorRed = new ScriptColor { V4 = new Vector4(1.0f, 0f, 0f, 1.0f) };
    public ScriptColor colorPink = new ScriptColor { V4 = new Vector4(1f, 0f, 1f, 1.0f) };

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
    const float Radius = 24f;
    // uint DeathSentenceNum = 0;                      // P2死宣次数
    List<uint> DeathSentenceTarget = [0, 0, 0];     // P2死宣目标
    Vector3 QuickMarchPos = new(0, 0, 0);           // P3进军位置
    bool QuickMarchStackDrawn = false;              // P3进军核爆绘图完成记录
    bool QuickMarchEarthShakerDrawn = false;        // P3进军大地摇动绘图完成记录

    public void Init(ScriptAccessory accessory)
    {
        phase = UCOB_Phase.Twintania;
        MyId = accessory.Data.Me;

        // DeathSentenceNum = 0;               // P2死宣次数
        DeathSentenceTarget = [0, 0, 0];    // P2死宣目标

        QuickMarchPos = new(0, 0, 0);       // P3进军位置
        QuickMarchStackDrawn = false;       // P3进军核爆绘图完成记录
        QuickMarchEarthShakerDrawn = false;        // P3进军大地摇动绘图完成记录

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

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "大地摇动线指路-中";
        dp.Scale = new(1.5f);
        dp.ScaleMode = ScaleMode.YByDistance;
        dp.Color = posColorNormal.V4.WithW(3);
        dp.Position = new(0, 0, 0);
        dp.TargetPosition = new(-15.58f, 0, -15.58f);
        dp.Delay = 0;
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);

        var esPos_right = RotatePoint(new(-15.58f, 0, -15.58f), new(0, 0, 0), float.Pi/2);

        dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "大地摇动线指路-右";
        dp.Scale = new(1.5f);
        dp.ScaleMode = ScaleMode.YByDistance;
        dp.Color = posColorNormal.V4.WithW(3);
        dp.Position = new(0, 0, 0);
        dp.TargetPosition = esPos_right;
        dp.Delay = 0;
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);

    }

    #region P1：双塔

    // * 修改
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

    [ScriptMethod(name: "P2奈尔：雷光链", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9927"])]
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

    // 月光啊！照亮铁血霸道！
    [ScriptMethod(name: "月环钢铁", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:6492"])]
    public void 月环钢铁(Event @event, ScriptAccessory accessory)
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

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "钢铁";
        dp.Scale = new(10);
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Owner = sid;
        dp.Delay = 5000;
        dp.DestoryAt = 3000;
        dp.Color = accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    // 月光啊！用你的炽热烧尽敌人！
    [ScriptMethod(name: "月环分摊", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:6493"])]
    public void 月环分摊(Event @event, ScriptAccessory accessory)
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
            dp.Name = $"热离子光束{i}";
            dp.Scale = new(3);
            dp.Owner = accessory.Data.PartyList[i];
            dp.Delay = 5000;
            dp.DestoryAt = 3000;
            dp.Color = accessory.Data.DefaultSafeColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    // 被炽热灼烧过的轨迹 乃成铁血霸道！
    [ScriptMethod(name: "分摊钢铁", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:6494"])]
    public void 分摊钢铁(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;

        for (var i = 0; i < 8; i++)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"热离子光束{i}";
            dp.Scale = new(3);
            dp.Owner = accessory.Data.PartyList[i];
            dp.DestoryAt = 5000;
            dp.Color = accessory.Data.DefaultSafeColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        var dp2 = accessory.Data.GetDefaultDrawProperties();
        dp2.Name = "钢铁";
        dp2.Scale = new(10);
        dp2.ScaleMode = ScaleMode.ByTime;
        dp2.Owner = sid;
        dp2.Delay = 5000;
        dp2.DestoryAt = 3000;
        dp2.Color = accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp2);
    }

    // 炽热燃烧！给予我月亮的祝福！
    [ScriptMethod(name: "分摊月环", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:6495"])]
    public void 分摊月环(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;

        for (var i = 0; i < 8; i++)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"热离子光束{i}";
            dp.Scale = new(3);
            dp.Owner = accessory.Data.PartyList[i];
            dp.DestoryAt = 5000;
            dp.Color = accessory.Data.DefaultSafeColor;
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

    // 我降临于此，征战铁血霸道！
    [ScriptMethod(name: "分散钢铁", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:6496"])]
    public void 分散钢铁(Event @event, ScriptAccessory accessory)
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
        dp2.Name = "钢铁";
        dp2.Scale = new(10);
        dp2.ScaleMode = ScaleMode.ByTime;
        dp2.Owner = sid;
        dp2.Delay = 5000;
        dp2.DestoryAt = 3000;
        dp2.Color = accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp2);
    }

    // 我降临于此，对月长啸！
    [ScriptMethod(name: "分散月环", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:6497"])]
    public void 分散月环(Event @event, ScriptAccessory accessory)
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

    // 超新星啊，更加闪耀吧！在星降之夜，称赞红月！
    [ScriptMethod(name: "分散月华冲", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:6500"])]
    public void 分散月华冲(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;
        lock (this)
        {
            this.cauterize = 0;
        }

        for (var i = 0; i < 8; i++)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"陨石流{i}";
            dp.Scale = new(4);
            dp.ScaleMode = ScaleMode.ByTime;
            dp.Owner = accessory.Data.PartyList[i];
            dp.Delay = 12000;
            dp.DestoryAt = 3000;
            dp.Color = accessory.Data.DefaultDangerColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        var dp2 = accessory.Data.GetDefaultDrawProperties();
        dp2.Name = $"月华冲";
        dp2.Scale = new(5);
        dp2.Owner = sid;
        dp2.CentreResolvePattern = PositionResolvePatternEnum.OwnerEnmityOrder;
        dp2.CentreOrderIndex = 1;
        dp2.Delay = 13000;
        dp2.DestoryAt = 2000;
        dp2.Color = accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp2);
    }

    // 超新星啊，更加闪耀吧！照亮红月下炽热之地！
    [ScriptMethod(name: "月华冲分摊", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:6501"])]
    public void 月华冲分摊(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;
        lock (this)
        {
            this.cauterize = 0;
        }

        var dp2 = accessory.Data.GetDefaultDrawProperties();
        dp2.Name = $"月华冲";
        dp2.Scale = new(5);
        dp2.Owner = sid; // bossid
        dp2.CentreResolvePattern = PositionResolvePatternEnum.OwnerEnmityOrder;
        dp2.CentreOrderIndex = 1;
        dp2.Delay = 13000;
        dp2.DestoryAt = 2000;
        dp2.Color = accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp2);

        for (var i = 0; i < 8; i++)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"热离子光束{i}";
            dp.Scale = new(2);
            dp.Owner = accessory.Data.PartyList[i];
            dp.Delay = 15000;
            dp.DestoryAt = 2000;
            dp.Color = accessory.Data.DefaultSafeColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }
    #endregion

    #region P2：小龙

    // TODO：超新星持续时间没验
    [ScriptMethod(name: "P2奈尔：超新星危险位置", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["DataId:2003393", "Operate:Add"])]
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

    [ScriptMethod(name: "龙神加护", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9922"], userControl: false)]
    public void 龙神加护(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;

        lock (this)
        {
            this.cauterize = 0;
            dragons.Clear();
        }
    }

    long dragonTime = DateTimeOffset.Now.ToUnixTimeSeconds();
    Dictionary<uint, int> dragons = new();

    [ScriptMethod(name: "小龙记录", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(816[34567])$"], userControl: false)]
    public void 小龙记录(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;

        lock (this)
        {
            var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
            var dir = this.PositionTo8Dir(pos, new(0, 0, 0));
            dragons.Add(sid, dir);
        }
    }



    int cauterize = 0;
    [ScriptMethod(name: "俯冲标记", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0014"])]
    public void 俯冲标记(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["TargetId"], out var tid)) return;

        IEnumerable<uint> query = from kv in this.dragons
                                  orderby kv.Value
                                  select kv.Key;

        var dragonList = new List<uint>();
        foreach (uint sid in query)
        {
            dragonList.Add(sid);
        }

        lock (this)
        {
            this.cauterize++;

            switch (this.cauterize)
            {
                case 1:
                    this._俯冲标记(dragonList[0], tid, accessory);
                    this._俯冲标记(dragonList[1], tid, accessory);
                    break;
                case 2:
                    this._俯冲标记(dragonList[2], tid, accessory);
                    break;
                case 3:
                    this._俯冲标记(dragonList[3], tid, accessory);
                    this._俯冲标记(dragonList[4], tid, accessory);
                    break;
                default:
                    break;
            }
        }
    }

    private void _俯冲标记(uint sid, uint tid, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"_俯冲标记{sid}";
        dp.Scale = new(18, 45);
        dp.Owner = sid;
        dp.DestoryAt = 6000;
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.TargetObject = tid;

        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "低温俯冲", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(993[12345])$"])]
    public void 低温俯冲(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "低温俯冲";
        dp.Scale = new(18, 45);
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Owner = sid;
        dp.DestoryAt = 3700;
        dp.Color = accessory.Data.DefaultDangerColor;

        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
    }

    #endregion

    #region P3：巴哈姆特

    [ScriptMethod(name: "P3巴哈：一运阶段记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9954"], userControl: false)]
    public void P3_PhaseChange_1st(Event @event, ScriptAccessory accessory)
    {
        if (phase != UCOB_Phase.Nael) return;
        phase = UCOB_Phase.Quickmarch_1st;
    }

    // 双塔冲
    [ScriptMethod(name: "P3巴哈：双塔旋风冲", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9906"])]
    public void TwistingDive(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;

        accessory.Method.TextInfo("旋风", 2000, true);

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "旋风冲范围";
        dp.Scale = new(10, 45);
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Color = accessory.Data.DefaultDangerColor.WithW(3);
        dp.Owner = sid;
        dp.DestoryAt = 4000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        dp = accessory.Data.GetDefaultDrawProperties();
        dp.Scale = new(1.5f);
        dp.DestoryAt = 4000;
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
        dp.Scale = new(10, 45);
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Color = accessory.Data.DefaultDangerColor.WithW(3);
        dp.Owner = sid;
        dp.DestoryAt = 4000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "P3巴哈：【进军】百万核爆冲位置记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9953"], userControl: false)]
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
            dp.Color = posColorNormal.V4.WithW(3);
            dp.Position = new(0, 0, 0);
            dp.TargetPosition = QuickMarchPos;
            dp.Delay = 4000;
            dp.DestoryAt = 11000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
        });
    }

    [ScriptMethod(name: "P3巴哈：巴哈百万核爆冲与分散", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9953"])]
    public void MegaFlareDive(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "百万核爆冲";
        dp.Scale = new(10, 45);
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

    [ScriptMethod(name: "P3巴哈：【进军】大地摇动站位指示", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0028"])]
    public void EarthShakerDir(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["TargetId"], out var tid)) return;
        if (phase != UCOB_Phase.Quickmarch_1st) return;
        if (tid != accessory.Data.Me) return;
        if (QuickMarchEarthShakerDrawn) return;

        QuickMarchEarthShakerDrawn = true;
        accessory.Method.RemoveDraw($"进军12点标记");
        var esPos_right = RotatePoint(QuickMarchPos, new(0, 0, 0), float.Pi/2);
        var esPos_left = RotatePoint(QuickMarchPos, new(0, 0, 0), -float.Pi/2);

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

    bool blackfireTrio = false;

    [ScriptMethod(name: "黑炎的三重奏", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9955"])]
    public void 黑炎的三重奏(Event @event, ScriptAccessory accessory)
    {
        this.blackfireTrio = true;
    }


    Vector3 NaelPosition = new(0, 0, 0);

    [ScriptMethod(name: "奈尔位置", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:8161"])]
    public void 奈尔位置(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;

        this.NaelPosition = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        if (Math.Sqrt(this.NaelPosition.X * this.NaelPosition.X + this.NaelPosition.Z * this.NaelPosition.Z) < 23) return;

        if (this.blackfireTrio)
        {
            this.blackfireTrio = false;

            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "奈尔位置";
            dp.Scale = new(1.5f, 24);
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Owner = MyId;
            dp.TargetObject = sid;
            dp.DestoryAt = 2000;

            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }
    }

    // 我降临于此对月长啸！召唤星降之夜！
    [ScriptMethod(name: "灾厄分散月环", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:6502"])]
    public void 灾厄分散月环(Event @event, ScriptAccessory accessory)
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
    [ScriptMethod(name: "灾厄月环分散", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:6503"])]
    public void 灾厄月环分散(Event @event, ScriptAccessory accessory)
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

    [ScriptMethod(name: "以太失控", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:9905"])]
    public void 以太失控(Event @event, ScriptAccessory accessory)
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

        this.heavensfallTrio = false;
    }

    bool heavensfallTrio = false;

    [ScriptMethod(name: "天地的三重奏", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9957"])]
    public void 天地的三重奏(Event @event, ScriptAccessory accessory)
    {
        this.heavensfallTrio = true;
    }

    Dictionary<int, Vector3> towers = new();
    // 天地塔
    [ScriptMethod(name: "天地塔", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9951"])]
    public void 天地塔(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;

        lock (this)
        {
            if (!this.heavensfallTrio) return;

            var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
            var dir = this.PositionTo16Dir(pos, new(0, 0, 0));

            towers.Add(dir, pos);

            if (this.towers.Count == 8)
            {
                var NaelDir = this.PositionTo16Dir(NaelPosition, new(0, 0, 0));

                IEnumerable<int> query = from kv in towers
                                         orderby kv.Key
                                         select kv.Key;

                var tmp = new List<int>();
                foreach (var v in query)
                {
                    if (v >= NaelDir) tmp.Add(v);
                }
                foreach (var v in query)
                {
                    if (v < NaelDir) tmp.Add(v);
                }

                var myIndex = 0;
                switch (accessory.Data.PartyList.ToList().IndexOf(MyId))
                {
                    case 0:
                        myIndex = 7; break;
                    case 1:
                        myIndex = 0; break;
                    case 2:
                        myIndex = 6; break;
                    case 3:
                        myIndex = 1; break;
                    case 4:
                        myIndex = 5; break;
                    case 5:
                        myIndex = 2; break;
                    case 6:
                        myIndex = 4; break;
                    case 7:
                        myIndex = 3; break;
                }

                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"天地塔{sid}";
                dp.Scale = new(1.5f, 48);
                dp.ScaleMode |= ScaleMode.YByDistance;
                dp.Position = towers[tmp[myIndex]];
                dp.TargetObject = MyId;
                dp.DestoryAt = 7000;
                dp.Color = accessory.Data.DefaultSafeColor;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                this.towers.Clear();
            }
        }
    }

    [ScriptMethod(name: "天崩地裂", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9911"])]
    public void 天崩地裂(Event @event, ScriptAccessory accessory)
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
        dp2.Owner = MyId;
        dp2.TargetPosition = new(0, 0, 0);
        dp2.Rotation = float.Pi;
        dp2.Color = accessory.Data.DefaultSafeColor;
        dp2.DestoryAt = 6000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp2);
    }



    [ScriptMethod(name: "群龙的八重奏", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:9959"])]
    public void 群龙的八重奏(Event @event, ScriptAccessory accessory)
    {
        this.grandOctetIcons.Clear();
    }

    List<uint> grandOctetIcons = new();
    [ScriptMethod(name: "群龙标记", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(0014|0077|0029)$"])]
    public void 群龙标记(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["TargetId"], out var tid)) return;

        lock (this)
        {
            this.grandOctetIcons.Add(tid);
            if (this.grandOctetIcons.Count == 7)
            {
                var find = false;
                for (int i = 0; i < this.grandOctetIcons.Count; i++)
                {
                    if (this.grandOctetIcons[i] == MyId) find = true;
                }

                if (!find)
                {
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"双塔尼亚";
                    dp.Scale = new(1.5f, 24);
                    dp.ScaleMode |= ScaleMode.YByDistance;
                    dp.Owner = MyId;
                    dp.TargetObject = Twintania;
                    dp.DestoryAt = 8000;
                    dp.Color = accessory.Data.DefaultSafeColor;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
                }
            }
        }
    }

    uint Twintania = 0;

    [ScriptMethod(name: "双塔尼亚位置", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:8159"])]
    public void 双塔尼亚位置(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out this.Twintania)) return;
    }

    #endregion

    #region P4：亿万核爆

    // 钢铁燃烧吧！成为我降临于此的刀剑吧！
    [ScriptMethod(name: "钢铁热离子光束凶鸟冲", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:6504"])]
    public void 钢铁热离子光束凶鸟冲(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "钢铁";
        dp.Scale = new(10);
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Owner = sid;
        dp.DestoryAt = 5000;
        dp.Color = accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        for (var i = 0; i < 8; i++)
        {
            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"热离子光束{i}";
            dp.Scale = new(2);
            dp.Owner = accessory.Data.PartyList[i];
            dp.Delay = 5000;
            dp.DestoryAt = 3000;
            dp.Color = accessory.Data.DefaultSafeColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            dp.Name = $"凶鸟冲{i}";
            dp.Scale = new(4);
            dp.Delay = 8000;
            dp.DestoryAt = 3000;
            dp.Color = accessory.Data.DefaultDangerColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }


    [ScriptMethod(name: "钢铁凶鸟冲热离子光束", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:6505"])]
    public void 钢铁凶鸟冲热离子光束(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "钢铁";
        dp.Scale = new(10);
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Owner = sid;
        dp.DestoryAt = 5000;
        dp.Color = accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        for (var i = 0; i < 8; i++)
        {
            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"凶鸟冲{i}";
            dp.Scale = new(4);
            dp.Owner = accessory.Data.PartyList[i];
            dp.Delay = 5000;
            dp.DestoryAt = 3000;
            dp.Color = accessory.Data.DefaultDangerColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            dp.Name = $"热离子光束{i}";
            dp.Scale = new(2);
            dp.Delay = 8000;
            dp.DestoryAt = 3000;
            dp.Color = accessory.Data.DefaultSafeColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }


    [ScriptMethod(name: "月环凶鸟冲热离子光束", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:6506"])]
    public void 月环凶鸟冲热离子光束(Event @event, ScriptAccessory accessory)
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
            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"凶鸟冲{i}";
            dp.Scale = new(4);
            dp.Owner = accessory.Data.PartyList[i];
            dp.Delay = 5000;
            dp.DestoryAt = 3000;
            dp.Color = accessory.Data.DefaultDangerColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            dp.Name = $"热离子光束{i}";
            dp.Scale = new(2);
            dp.Delay = 8000;
            dp.DestoryAt = 3000;
            dp.Color = accessory.Data.DefaultSafeColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    // 我自月而来携钢铁降临于此！
    [ScriptMethod(name: "月环钢铁凶鸟冲", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:6507"])]
    public void 月环钢铁凶鸟冲(Event @event, ScriptAccessory accessory)
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

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "钢铁";
        dp.Scale = new(10);
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Owner = sid;
        dp.Delay = 5000;
        dp.DestoryAt = 3000;
        dp.Color = accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        for (var i = 0; i < 8; i++)
        {
            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"凶鸟冲{i}";
            dp.Scale = new(4);
            dp.Owner = accessory.Data.PartyList[i];
            dp.Delay = 8000;
            dp.DestoryAt = 3000;
            dp.Color = accessory.Data.DefaultDangerColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
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
