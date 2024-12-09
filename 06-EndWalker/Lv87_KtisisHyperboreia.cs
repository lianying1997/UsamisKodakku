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

namespace UsamisScript;

[ScriptType(name: "Lv87 [创造环境极北造物院]", territorys: [974], guid: "8d4e4a9c-b144-4ec2-82cd-46b38867e4e6", version: "0.0.0.1", author: "Usami", note: "仅老二老三，别吃dot了别吃dot了！")]
public class KtisisHyperboreia
{
    // [UserSetting("启用本体一运塔[小队频道]发宏")]
    // public bool HC1_ChatGuidance { get; set; } = false;

    [UserSetting("Debug模式（玩家无需开启）")]
    public bool DebugMode { get; set; } = false;

    List<bool> Boss2_SafePosition = [false, false, false];

    public void Init(ScriptAccessory accessory)
    {
        Boss2_SafePosition = [false, false, false];
        accessory.Method.RemoveDraw(".*");
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

    public static int PositionMatchesTo8Dir(Vector3 point, Vector3 center)
    {
        float x = point.X - center.X;
        float z = point.Z - center.Z;

        int direction = (int)Math.Round(4 - 4 * Math.Atan2(x, z) / Math.PI) % 8;

        // Dirs: N = 0, NE = 1, ..., NW = 7
        return (direction + 8) % 8; // 防止负值出现
    }

    // [ScriptMethod(name: "随时DEBUG用", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:=TST"], userControl: false)]
    // public void EchoDebug(Event @event, ScriptAccessory accessory)
    // {
    //     var msg = @event["Message"].ToString();
    //     accessory.Method.SendChat($"/e 获得玩家发送的消息：{msg}");

    //     var dp = accessory.Data.GetDefaultDrawProperties();
    //     dp.Name = $"纯正飙风直线";
    //     dp.Scale = new(10, 50);
    //     dp.Owner = 0x40009B1E;
    //     dp.Color = accessory.Data.DefaultDangerColor;
    //     dp.ScaleMode = ScaleMode.ByTime;
    //     dp.Delay = 0;
    //     dp.DestoryAt = 3700;
    //     accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    // }

    // [ScriptMethod(name: "防击退删除画图", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(7559|7548|7389)$"], userControl: false)]
    // // 沉稳咏唱|亲疏自行|原初的解放
    // public void RemoveLine(Event @event, ScriptAccessory accessory)
    // {
    //     if (!ParseObjectId(@event["SourceId"], out var id)) return;
    //     if (id == accessory.Data.Me)
    //     {
    //         accessory.Method.RemoveDraw("^(可防击退-.*)$");
    //         // accessory.Method.SendChat($"/e 检测到防击退，并删除画图");
    //     }
    // }

    #region 老二：拉冬之王

    [ScriptMethod(name: "BOSS2：吐息记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(2812|2813|2814)$"], userControl: false)]
    // 沉稳咏唱|亲疏自行|原初的解放
    public void Boss2_BreathCollect(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;
        var stid = @event["StatusID"];
        switch (stid)
        {
            case "2812":
                Boss2_SafePosition[0] = true;
                if (DebugMode)
                    accessory.Method.SendChat($"/e [DEBUG]：检测到中间头的吐息……");
                break;
            case "2813":
                Boss2_SafePosition[1] = true;
                if (DebugMode)
                    accessory.Method.SendChat($"/e [DEBUG]：检测到左边头的吐息……");
                break;
            case "2814":
                Boss2_SafePosition[2] = true;
                if (DebugMode)
                    accessory.Method.SendChat($"/e [DEBUG]：检测到右边头的吐息……");
                break;
        }
    }

    [ScriptMethod(name: "BOSS2：吐息绘图", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(25734|25735|25736|25737|25738|25739)$"])]
    public void Boss2_BreathCall(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;

        if (DebugMode)
            accessory.Method.SendChat($"/e [DEBUG]：检测到中{Boss2_SafePosition[0]}，左{Boss2_SafePosition[1]}，右{Boss2_SafePosition[2]}");

        if (Boss2_SafePosition[2])
        {
            Boss2_SafePosition[2] = false;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"扇形检测-右后";
            dp.Scale = new(30);
            dp.Radian = float.Pi * 2 / 3;
            dp.Rotation = -float.Pi * 2 / 3;
            dp.Owner = sid;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 0;
            dp.DestoryAt = 6700;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        if (Boss2_SafePosition[1])
        {
            Boss2_SafePosition[1] = false;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"扇形检测-左后";
            dp.Scale = new(30);
            dp.Radian = float.Pi * 2 / 3;
            dp.Rotation = float.Pi * 2 / 3;
            dp.Owner = sid;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 0;
            dp.DestoryAt = 6700;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        if (Boss2_SafePosition[0])
        {
            Boss2_SafePosition[0] = false;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"扇形检测-正面";
            dp.Scale = new(30);
            dp.Radian = float.Pi * 2 / 3;
            dp.Owner = sid;
            dp.Color = accessory.Data.DefaultDangerColor;
            dp.Delay = 0;
            dp.DestoryAt = 6700;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }
    }

    #endregion

    #region 老三：赫尔墨斯

    [ScriptMethod(name: "BOSS3：纯正飙风", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(25889|27836)$"])]
    public void Boss3_TrueAeroIV(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"纯正飙风直线";
        dp.Scale = new(10, 50);
        dp.Owner = sid;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Delay = 0;
        dp.DestoryAt = 3700;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "BOSS3：四重纯正飙风", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^27837$"])]
    public void Boss3_QuadTrueAeroIV(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"四重纯正飙风";
        dp.Scale = new(10, 50);
        dp.Owner = sid;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Delay = 6000;
        dp.DestoryAt = 3700;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }


    #endregion

}