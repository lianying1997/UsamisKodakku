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
using Lumina.Excel.GeneratedSheets;

namespace UsamisScript.EndWalker.Zodiark;

[ScriptType(name: "Zodiark [佐迪亚克歼灭战]", territorys: [992], guid: "ed3f32fe-fc12-4485-baa4-bec6c357e70b", version: "0.0.0.1", author: "Usami", note: noteStr)]
public class Zodiark
{
    const string noteStr =
    """
    v0.0.0.1:
    鸭门。
    """;

    [UserSetting("Debug模式，非开发用请关闭")]
    public bool DebugMode { get; set; } = false;

    // TODO
    // 加一个阶段指示，转场。转场的秘纹绘图时间调整一下
    // 以及，星落部分，不要让画面过乱。根据地图特效直接绘出星落安全区。

    int ParadeigmaNum = 0;      // 范式次数
    Vector3[] BirdOrBeastPos = new Vector3[4];    // Quetzalcoatl & Behemoth，记录四个角落月环鸟鸟/钢铁贝贝的位置
    Vector3[] SnakePos = new Vector3[16];         // Python，记录十六个区域蛇蛇直线的位置
    Vector3[] SnakePosTarget = new Vector3[16];    // Python，记录十六个区域蛇蛇最后目标的位置
    List<uint> EsoterikosSourceIds = [];          // 已记录的秘纹ID
    public ScriptColor colorPink = new ScriptColor { V4 = new Vector4(1f, 0f, 1f, 1.0f) };
    public ScriptColor colorRed = new ScriptColor { V4 = new Vector4(1f, 0f, 0f, 1.0f) };
    bool isTurnLeft = false;                      // 爸爸转哪儿
    bool isStarFall = false;                      // 转场阶段，为三重秘纹绘图
    Dictionary<string, (int, int)>? AstralMapping = null;
    int AstralNum = 0;          // 星落次数
    int AstralType = 0;         // 星落安全区类型
    Vector3[] AstralSafePos = new Vector3[6];   // 星落6个安全点
    public void Init(ScriptAccessory accessory)
    {
        ParadeigmaNum = 0;      // 范式次数

        // 记录内场角位置
        BirdOrBeastPos[0] = new(89.50f, 0, 89.50f);    // 左上
        BirdOrBeastPos[1] = new(110.50f, 0, 89.50f);   // 右上
        BirdOrBeastPos[2] = new(89.50f, 0, 110.50f);   // 左下
        BirdOrBeastPos[3] = new(110.50f, 0, 110.50f);  // 右下

        // 蛇的16个位置，从左上开始顺时针
        SnakePos[0] = new(85.00f, 0, 75.00f);
        SnakePos[1] = new(95.00f, 0, 75.00f);
        SnakePos[2] = new(105.00f, 0, 75.00f);
        SnakePos[3] = new(115.00f, 0, 75.00f);

        SnakePos[4] = new(125.00f, 0, 85.00f);
        SnakePos[5] = new(125.00f, 0, 95.00f);
        SnakePos[6] = new(125.00f, 0, 105.00f);
        SnakePos[7] = new(125.00f, 0, 115.00f);

        SnakePos[8] = new(115.00f, 0, 125.00f);
        SnakePos[9] = new(105.00f, 0, 125.00f);
        SnakePos[10] = new(95.00f, 0, 125.00f);
        SnakePos[11] = new(85.00f, 0, 125.00f);

        SnakePos[12] = new(75.00f, 0, 115.00f);
        SnakePos[13] = new(75.00f, 0, 105.00f);
        SnakePos[14] = new(75.00f, 0, 95.00f);
        SnakePos[15] = new(75.00f, 0, 85.00f);

        // 蛇的16个目标位置
        SnakePosTarget[0] = SnakePos[11];
        SnakePosTarget[1] = SnakePos[10];
        SnakePosTarget[2] = SnakePos[9];
        SnakePosTarget[3] = SnakePos[8];
        SnakePosTarget[4] = SnakePos[15];
        SnakePosTarget[5] = SnakePos[14];
        SnakePosTarget[6] = SnakePos[13];
        SnakePosTarget[7] = SnakePos[12];
        SnakePosTarget[8] = SnakePos[3];
        SnakePosTarget[9] = SnakePos[2];
        SnakePosTarget[10] = SnakePos[1];
        SnakePosTarget[11] = SnakePos[0];
        SnakePosTarget[12] = SnakePos[7];
        SnakePosTarget[13] = SnakePos[6];
        SnakePosTarget[14] = SnakePos[5];
        SnakePosTarget[15] = SnakePos[4];

        // 星落6个安全点
        AstralSafePos[0] = new(86, 0, 86);
        AstralSafePos[1] = new(100, 0, 86);
        AstralSafePos[2] = new(114, 0, 86);
        AstralSafePos[3] = new(86, 0, 100);
        AstralSafePos[4] = new(100, 0, 100);
        AstralSafePos[5] = new(114, 0, 100);

        EsoterikosSourceIds = [];          // 已记录的秘纹ID

        isTurnLeft = false;                // 爸爸转哪儿
        isStarFall = false;                // 转场阶段，为三重秘纹绘图

        // 星落字典
        AstralMapping = new Dictionary<string, (int val1, int val2)>
        {
            {"00020001", (1, 1)},
            {"00800040", (10, 10)},
            {"10000800", (2, 20)},
            {"00200010", (3, 30)}
        };

        AstralNum = 0;          // 星落次数
        AstralType = 0;         // 星落安全区类型

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

    private void DebugMsg(string str, ScriptAccessory accessory)
    {
        if (!DebugMode) return;
        accessory.Method.SendChat(str);
    }

    [ScriptMethod(name: "随时DEBUG用", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:=TST"], userControl: false)]
    public void EchoDebug(Event @event, ScriptAccessory accessory)
    {
        if (!DebugMode) return;
        var msg = @event["Message"].ToString();
        accessory.Method.SendChat($"/e 获得玩家发送的消息：{msg}");

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"钢铁";
        dp.Scale = new(3f);
        dp.Position = new(86f, 0, 86f);
        dp.Delay = 0;
        dp.DestoryAt = 2000;
        dp.Color = accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "【全局】范式次数记录（不可控）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(26568|26559)$"], userControl: false)]
    public void ParadeigmaNumRecord(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;
        ParadeigmaNum++;
        if (ParadeigmaNum == 4)
            isStarFall = true;
        else
            isStarFall = false;     // 同时取消转场阶段
        // 真神中，该变量意义仅为控制三重光波的出现延迟
        DebugMsg($"/e [DEBUG] 发现范式次数增加：{ParadeigmaNum}。", accessory);
    }

    private void drawBeastCircle(int beastIdx, int delay, int destoryAt, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"钢铁{beastIdx}";
        dp.Scale = new(15);
        dp.Position = BirdOrBeastPos[beastIdx];
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        dp.Color = colorPink.V4.WithW(1.5f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    private void drawSnakeLine(int[] SnakeIdx, int delay, int destoryAt, ScriptAccessory accessory)
    {
        DebugMsg($"/e [DEBUG] 发现蛇蛇{SnakeIdx[0]} -> {SnakePosTarget[SnakeIdx[0]]} 与 {SnakeIdx[1]} -> {SnakePosTarget[SnakeIdx[1]]}", accessory);

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"直线{SnakeIdx[0]}";
        dp.Scale = new(11);
        dp.ScaleMode = ScaleMode.YByDistance;
        dp.Position = SnakePos[SnakeIdx[0]];
        dp.TargetPosition = SnakePosTarget[SnakeIdx[0]];
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        dp.Color = colorPink.V4.WithW(1.5f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        dp.Name = $"直线{SnakeIdx[1]}";
        dp.Position = SnakePos[SnakeIdx[1]];
        dp.TargetPosition = SnakePosTarget[SnakeIdx[1]];
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    private void drawFan(uint sid, int delay, int destoryAt, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"三角秘纹{sid}";
        dp.Scale = new(60);
        dp.Radian = float.Pi / 3;
        dp.Owner = sid;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        dp.Color = accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    private void drawHalfCleave(uint sid, int delay, int destoryAt, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"半场秘纹{sid}";
        dp.Scale = new(42, 21);
        dp.Owner = sid;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        dp.Color = accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    private void drawLine(uint sid, int delay, int destoryAt, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"直线秘纹{sid}";
        dp.Scale = new(16, 42);
        dp.Owner = sid;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        dp.Color = accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "分摊标记", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:013C"])]
    public void GenerateTargetRecord(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["TargetId"], out var tid)) return;
        DebugMsg($"/e 检测到 {tid} 被点名分摊。", accessory);

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"分摊{tid}";
        dp.Scale = new(5);
        dp.Owner = tid;
        dp.Delay = 0;
        dp.DestoryAt = 12000;
        dp.Color = accessory.Data.DefaultSafeColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "秘纹（三角、半场、直线）", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:regex:^(1371[456])$"])]
    public void Exoterikos(Event @event, ScriptAccessory accessory)
    {
        lock (this)
        {
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;

            // 如果EsoterikosSourceIds中没有sid，将其加入；
            // 如果EsoterikosSourceIds有sid，将其移出。

            if (EsoterikosSourceIds.Contains(sid))
            {
                DebugMsg($"/e [DEBUG] 删除秘纹：{sid}。", accessory);
                EsoterikosSourceIds.Remove(sid);
                accessory.Method.RemoveDraw(@$"(直线|三角|半场)秘纹{sid}");
                return;
            }
            else
                EsoterikosSourceIds.Add(sid);

            var sdid = JsonConvert.DeserializeObject<uint>(@event["SourceDataId"]);

            DebugMsg($"/e [DEBUG] 发现秘纹{sid}正在释放：{sdid}。", accessory);

            switch (sdid)
            {
                case 13714:
                    if (isStarFall)
                        drawLine(sid, 4000, 25000, accessory);
                    else
                        drawLine(sid, 0, 25000, accessory);
                    break;
                case 13715:
                    drawHalfCleave(sid, 0, 25000, accessory);
                    break;
                case 13716:
                    drawFan(sid, 0, 25000, accessory);
                    break;
                default:
                    return;
            }
        }
    }

    [ScriptMethod(name: "悼念", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:26601"], userControl: false)]
    public void OrbsDownSync(Event @event, ScriptAccessory accessory)
    {
        Task.Delay(50).ContinueWith(t =>
        {
            // 避免该阶段有某一波秘纹没放出来就被压掉了
            EsoterikosSourceIds = [];
            // 进入转场阶段标志
            isStarFall = true;
            accessory.Method.RemoveDraw(".*");
        });
    }

    [ScriptMethod(name: "痛苦（斜向冲锋）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:26579"])]
    public void Algedon(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"痛苦{sid}";
        dp.Scale = new(30, 60);
        dp.Owner = sid;
        dp.Delay = 0;
        dp.DestoryAt = 8000;
        dp.Color = colorPink.V4.WithW(1.5f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "不义（小拳拳）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:26584"])]
    public void Adikia(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"不义{sid}";
        dp.Scale = new(21);
        dp.Position = new Vector3(121.0f, 0, 100.0f);
        dp.Delay = 0;
        dp.DestoryAt = 7500;
        dp.Color = accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        dp.Position = new Vector3(79.0f, 0, 100.0f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "星蚀（转场星落）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:26573"])]
    public void Astral(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"星蚀{sid}";
        dp.Scale = new(10);
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Owner = sid;
        dp.Delay = 0;
        dp.DestoryAt = 3000;
        dp.Color = accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "贝贝钢铁", eventType: EventTypeEnum.EnvControl, eventCondition: ["DirectorId:80034E70", "Id:00200010", "Index:regex:^(0000000[9ABC])$"])]
    public async void BeastCircle(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["Index"], out var idx)) return;

        DebugMsg($"/e [DEBUG] 发现贝贝钢铁{idx}。", accessory);

        switch (ParadeigmaNum)
        {
            case 1:
            case 2:
            case 3:
                drawBeastCircle(getBeastIndex(idx), 0, 20000, accessory);
                break;
            case 4:
            case 5:
            case 6:
            case 7:
            case 8:
            case 9:
                await Task.Delay(6000);
                drawBeastCircle(isTurnLeft ? getBeastBirdTurnLeftIndex(getBeastIndex(idx)) : getBeastBirdTurnRightIndex(getBeastIndex(idx)), 0, 20000, accessory);
                break;
            default:
                return;
        }
    }

    [ScriptMethod(name: "蛇蛇直线", eventType: EventTypeEnum.EnvControl, eventCondition: ["DirectorId:80034E70", "Id:00200010", "Index:regex:^(000000(0[DEF]|(1[01234])))$"])]
    public async void SnakeLine(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["Index"], out var idx)) return;
        DebugMsg($"/e [DEBUG] 发现蛇蛇直线{idx}。", accessory);

        switch (ParadeigmaNum)
        {
            case 1:
            case 2:
            case 3:
                drawSnakeLine(getSnakeIndex(idx), 0, 20000, accessory);
                break;
            case 4:
            case 5:
            case 6:
            case 7:
            case 8:
            case 9:
                await Task.Delay(6000);
                drawSnakeLine(isTurnLeft ? getSnakeTurnLeftIndex(getSnakeIndex(idx)) : getSnakeTurnRightIndex(getSnakeIndex(idx)), 0, 20000, accessory);
                break;
            default:
                return;
        }
    }

    [ScriptMethod(name: "旋转方向记录（不可控）", eventType: EventTypeEnum.EnvControl, eventCondition: ["DirectorId:80034E70", "Id:regex:^(00200010|00020001)$", "Index:00000002"], userControl: false)]
    public void RotateRecord(Event @event, ScriptAccessory accessory)
    {
        var id = @event["Id"].ToString();
        switch (id)
        {
            case "00200010":
                DebugMsg($"/e [DEBUG] 向左转。", accessory);
                isTurnLeft = true;
                break;

            case "00020001":
                DebugMsg($"/e [DEBUG] 向右转。", accessory);
                isTurnLeft = false;
                break;

            default:
                return;
        }
    }

    [ScriptMethod(name: "贝贝钢铁消失", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:26191"], userControl: false)]
    public void BeastCircleRemove(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw(@$"钢铁\d+$");
        accessory.Method.RemoveDraw(@$"四象限\d+$");
    }

    [ScriptMethod(name: "蛇蛇直线消失", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:26192"], userControl: false)]
    public void SnakeLineRemove(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw(@$"直线\d+$");
        // 顺便删除火线
        accessory.Method.RemoveDraw(@$"四象限\d+$");
    }

    private int getBeastIndex(uint index)
    {
        switch (index)
        {
            case 9:
                return 0;
            case 10:
                return 1;
            case 11:
                return 2;
            case 12:
                return 3;
            default:
                return -1;
        }
    }
    private int getBeastBirdTurnRightIndex(int posIndex)
    {
        switch (posIndex)
        {
            case 0:
                return 1;
            case 1:
                return 3;
            case 2:
                return 0;
            case 3:
                return 2;
            default:
                return -1;
        }
    }
    private int getBeastBirdTurnLeftIndex(int posIndex)
    {
        switch (posIndex)
        {
            case 0:
                return 2;
            case 1:
                return 0;
            case 2:
                return 3;
            case 3:
                return 1;
            default:
                return -1;
        }
    }

    private int[] getSnakeIndex(uint index)
    {
        // 80034E70     00200010    0000000D    00000000    上 一三列 蛇蛇出现      0x0D = 13
        // 80034E70     00200010    0000000E    00000000    上 二四列 蛇蛇出现      0x0E = 14
        // 80034E70     00200010    0000000F    00000000    下 一三列 蛇蛇出现      0x0F = 15
        // 80034E70     00200010    00000010    00000000    下 二四列 蛇蛇出现      0x10 = 16
        // 80034E70     00200010    00000011    00000000    左 一三列 蛇蛇出现      0x11 = 17
        // 80034E70     00200010    00000012    00000000    左 二四列 蛇蛇出现      0x12 = 18
        // 80034E70     00200010    00000013    00000000    右 一三列 蛇蛇出现      0x13 = 19
        // 80034E70     00200010    00000014    00000000    右 二四列 蛇蛇出现      0x14 = 20

        switch (index)
        {
            case 13:
                return [0, 2];
            case 14:
                return [1, 3];
            case 15:
                return [11, 9];
            case 16:
                return [10, 8];
            case 17:
                return [15, 13];
            case 18:
                return [14, 12];
            case 19:
                return [4, 6];
            case 20:
                return [5, 7];
            default:
                return [-1, -1];
        }
    }

    private int[] getSnakeTurnRightIndex(int[] posIndex)
    {
        return [posIndex[0] + 4 > 15 ? posIndex[0] + 4 - 16 : posIndex[0] + 4,
                posIndex[1] + 4 > 15 ? posIndex[1] + 4 - 16 : posIndex[1] + 4];
    }

    private int[] getSnakeTurnLeftIndex(int[] posIndex)
    {
        return [posIndex[0] - 4 < 0 ? posIndex[0] - 4 + 16 : posIndex[0] - 4,
                posIndex[1] - 4 < 0 ? posIndex[1] - 4 + 16 : posIndex[1] - 4];
    }

    #region EnvControl记录

    // DirectorId不变，似乎与地图相关
    // State/Id与动作特效相关
    // Index即“哪一个”
    // Param不晓得

    // State/Id
    // 00200010     出现
    // 20001000     施法中
    // 40000004     跑路

    // DirectorId   State/Id    Index       Param
    // 80034E70     00200010    00000015    00000000    左上 鸟鸟出现       0x15 = 21
    // 80034E70     00200010    00000016    00000000    右上 鸟鸟出现       0x16 = 22
    // 80034E70     00200010    00000017    00000000    左下 鸟鸟出现       0x17 = 23
    // 80034E70     00200010    00000018    00000000    右下 鸟鸟出现       0x18 = 24

    // 80034E70     00200010    00000009    00000000    左上 贝贝出现       0x09 = 9
    // 80034E70     00200010    0000000A    00000000    右上 贝贝出现       0x0A = 10
    // 80034E70     00200010    0000000B    00000000    左下 贝贝出现       0x0B = 11
    // 80034E70     00200010    0000000C    00000000    右下 贝贝出现       0x0C = 12

    // 80034E70     00200010    0000000D    00000000    上 一三列 蛇蛇出现      0x0D = 13
    // 80034E70     00200010    0000000E    00000000    上 二四列 蛇蛇出现      0x0E = 14
    // 80034E70     00200010    0000000F    00000000    下 一三列 蛇蛇出现      0x0F = 15
    // 80034E70     00200010    00000010    00000000    下 二四列 蛇蛇出现      0x10 = 16
    // 80034E70     00200010    00000011    00000000    左 一三列 蛇蛇出现      0x11 = 17
    // 80034E70     00200010    00000012    00000000    左 二四列 蛇蛇出现      0x12 = 18
    // 80034E70     00200010    00000013    00000000    右 一三列 蛇蛇出现      0x13 = 19
    // 80034E70     00200010    00000014    00000000    右 二四列 蛇蛇出现      0x14 = 20

    // 80034E70     20001000    00000015    00000000    鸟鸟准备施法月环
    // 80034E70     20001000    00000016    00000000    鸟鸟准备施法月环
    // 80034E70     20001000    00000017    00000000    鸟鸟准备施法月环
    // 80034E70     20001000    00000018    00000000    鸟鸟准备施法月环

    // 80034E70     40000004    00000015    00000000    鸟鸟施法完毕跑路
    // 80034E70     40000004    00000016    00000000    鸟鸟施法完毕跑路
    // 80034E70     40000004    00000017    00000000    鸟鸟施法完毕跑路
    // 80034E70     40000004    00000018    00000000    鸟鸟施法完毕跑路

    // 星极超流
    // 80034E70     00200010    00000002    00000000    往左转 箭头
    // 80034E70     00020001    00000002    00000000    往右转 箭头
    //              00400004    00000002    00000000    删除箭头
    //              00400004    00000004    00000000    真特效 转盘子 往左转

    //              00020001    00000005    00000000    星极超流 右上到左下的火线
    //              00400020    00000005    00000000    星极超流 左上到右下的火线

    //              00010001    00000005    00000000    星极超流 右上到左下的火线
    //              00200010    00000005    00000000    星极超流 左上到右下的火线
    //              00400004    00000005    00000000    星极超流 左上到右下的火线


    //              00020001    00000001    00000000    星极超流地板花纹
    //              00010001    00000003    00000000    ？

    // 80034E70     00200010    00000006    00000000    星落 左 星星排列1
    //              00400004    00000006                星落 左 星星排列2
    //              00800040    00000006
    //              10000800    00000006
    //              00080004    00000006                星落 左 星星消失



    // 80034E70     00200010    00000007    00000000    星落 下 星星排列1
    // 80034E70     00200010    00000008    00000000    星落 右 星星排列1
    // 80034E70     00200010    00000009    00000000    左上 贝贝出现

    //              00080004    00000001    00000000    星极超流地板花纹

    #endregion
}
