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

[ScriptType(name: "P12S [零式万魔殿 荒天之狱4]", territorys: [1154], guid: "563bd710-59b8-46de-bbac-f1527d7c0803", version: "0.0.0.1", author: "Usami", note: noteStr)]
public class p12s
{
    const string noteStr = 
    """【未完成！】仅门神，一范正攻，门神到三泛前的对话""";

    [UserSetting("Debug模式，非开发用请关闭")]
    public bool DebugMode { get; set; } = false;

    [UserSetting("站位提示圈绘图-普通颜色")]
    public ScriptColor posColorNormal { get; set; } = new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 1.0f, 1.0f) };

    [UserSetting("站位提示圈绘图-玩家站位颜色")]
    public ScriptColor posColorPlayer { get; set; } = new ScriptColor { V4 = new Vector4(0.2f, 1.0f, 0.2f, 1.0f) };

    int phase = 0;

    bool db_PD1_isChecked;
    bool db_PD1_isNorthFirst;
    bool db_PD1_drawn;
    bool db_PD2_drawn;
    // 是否为左翅膀，用于三魂一体，从左到右顺序为上、中、下
    List<bool> db_isLeftCleave = [false, false, false];
    int db_PD2_towerRecordNum = 0;
    // 范式二记录，是否从右下角开始，是否需要放白塔
    bool db_PD2_fromRightBottom = false;
    List<bool> db_PD2_shouldWhiteTower = [false, false, false, false];
    // 范式二记录，玩家是否被点塔，是否被点放置白塔
    List<bool> db_PD2_isChosenTower = [false, false, false, false, false, false, false, false];
    List<bool> db_PD2_isWhiteTower = [false, false, false, false, false, false, false, false];

    readonly object db_PD1_lockObject = new object();
    readonly object db_PD2_lockObject = new object();
    readonly object db_SC1_lockObject = new object();


    // 超链一元素
    List<ulong> db_SC1_theories = [];
    // 超链一
    int db_SC1_destTheoryDir_R1;
    int db_SC1_destTheoryDir_R2;
    int db_SC1_destTheoryDir_R3;
    int db_SC1_myBuff;
    bool db_SC1_isOut;
    bool db_SC1_isSpread;
    // Black White Tower Beam index，四个元素，黑塔-白塔-黑分摊-白分摊
    List<int> db_SC1_BWTBidx = [-1, -1, -1, -1];
    bool db_SC1_TNfinalSpread;
    bool db_SC1_FinalSpreadChecked;
    bool db_SC1_round1_drawn;
    bool db_SC1_round2_drawn;
    bool db_SC1_round3_drawn;


    public void Init(ScriptAccessory accessory)
    {
        phase = 0;

        db_PD1_isChecked = false;
        db_PD1_isNorthFirst = false;
        db_PD1_drawn = false;
        db_PD2_drawn = false;

        db_isLeftCleave = [false, false, false];

        db_PD2_towerRecordNum = 0;
        db_PD2_fromRightBottom = false;
        db_PD2_shouldWhiteTower = [false, false, false, false];
        db_PD2_isChosenTower = [false, false, false, false, false, false, false, false];
        db_PD2_isWhiteTower = [false, false, false, false, false, false, false, false];

        db_SC1_theories = [];
        db_SC1_destTheoryDir_R1 = -1;
        db_SC1_destTheoryDir_R2 = -1;
        db_SC1_destTheoryDir_R3 = -1;
        db_SC1_myBuff = -1;
        db_SC1_isOut = false;
        db_SC1_isSpread = false;
        db_SC1_BWTBidx = [-1, -1, -1, -1];
        db_SC1_TNfinalSpread = false;
        db_SC1_FinalSpreadChecked = false;

        db_SC1_round1_drawn = false;
        db_SC1_round2_drawn = false;
        db_SC1_round3_drawn = false;

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

    private int PositionFloorTo4Dir(Vector3 point, Vector3 centre)
    {
        // Dirs: NE = 0, SE = 1, SW = 2, NW = 3
        var r = Math.Floor(2 - 2 * Math.Atan2(point.X - centre.X, point.Z - centre.Z) / Math.PI) % 4;
        return (int)r;
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

    public static int PositionMatchesTo8Dir(Vector3 point, Vector3 center)
    {
        float x = point.X - center.X;
        float z = point.Z - center.Z;

        int direction = (int)Math.Round(4 - 4 * Math.Atan2(x, z) / Math.PI) % 8;

        // Dirs: N = 0, NE = 1, ..., NW = 7
        return (direction + 8) % 8; // 防止负值出现
    }
    private Vector3 RotatePoint(Vector3 point, Vector3 centre, float radian)
    {

        Vector2 v2 = new(point.X - centre.X, point.Z - centre.Z);

        var rot = MathF.PI - MathF.Atan2(v2.X, v2.Y) + radian;
        var length = v2.Length();
        return new(centre.X + MathF.Sin(rot) * length, centre.Y, centre.Z - MathF.Cos(rot) * length);
    }

    [ScriptMethod(name: "随时DEBUG用", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:=TST"], userControl: false)]
    public void EchoDebug(Event @event, ScriptAccessory accessory)
    {
        if (!DebugMode) return;
        var msg = @event["Message"].ToString();
        accessory.Method.SendChat($"/e 获得玩家发送的消息：{msg}");


        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"超链I_第二步左右";
        dp.TargetPosition = RotatePoint(new Vector3(92, 92, 92), new Vector3(100, 0, 100), float.Pi / 180 * -17);
        dp.Position = new(100, 0, 100);
        dp.Color = posColorPlayer.V4;
        dp.Delay = 0;
        dp.DestoryAt = 1000;
        dp.Scale = new(5, 20);
        // dp.ScaleMode = ScaleMode.YByDistance;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "防击退删除画图", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(7559|7548|7389)$"], userControl: false)]
    // 沉稳咏唱|亲疏自行|原初的解放
    public void RemoveLine(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var id)) return;
        if (id == accessory.Data.Me)
        {
            accessory.Method.RemoveDraw("^(可防击退-.*)$");
            // accessory.Method.SendChat($"/e 检测到防击退，并删除画图");
        }
    }

    #region 门神：一范

    [ScriptMethod(name: "阶段记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:33517"], userControl: false)]
    public void DB_PhaseRecord(Event @event, ScriptAccessory accessory)
    {
        phase = phase == 1 ? 2 : 1;
    }

    [ScriptMethod(name: "一范天使位置记录", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:16172"], userControl: false)]
    public void DB_Paradeigma_I_PositionRecord(Event @event, ScriptAccessory accessory)
    {
        lock (db_PD1_lockObject)
        {
            // 只检验一次，如果 Z < 100 则在北
            if (phase != 1 || db_PD1_isChecked) return;
            var spos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
            db_PD1_isNorthFirst = spos.Z < 100;
            db_PD1_isChecked = true;
            if (DebugMode)
                accessory.Method.SendChat($"/e [DEBUG]：检测到一范天使在【{(db_PD1_isNorthFirst ? "北" : "南")}】");
        }
    }

    [ScriptMethod(name: "门神：一范天使站位点绘图（正攻）", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:16172"])]
    public void DB_Paradeigma_I_Waymark(Event @event, ScriptAccessory accessory)
    {
        Task.Delay(100).ContinueWith(t =>
        {
            lock (db_PD1_lockObject)
            {
                if (db_PD1_drawn) return;
                if (!db_PD1_isChecked) return;

                Vector3[,] pos = new Vector3[2, 5];
                pos[0, 0] = new(100, 0, 95);
                pos[0, 1] = new(100, 0, 100);
                pos[0, 2] = new(100, 0, 90);
                pos[0, 3] = new(100, 0, 85);
                pos[0, 4] = new(100, 0, 109);

                pos[1, 0] = new(100, 0, 105);
                pos[1, 1] = new(100, 0, 100);
                pos[1, 2] = new(100, 0, 110);
                pos[1, 3] = new(100, 0, 115);
                pos[1, 4] = new(100, 0, 91);

                var dp = accessory.Data.GetDefaultDrawProperties();
                var MyIndex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
                // 偶数Idx为第一轮引导
                var isFirstRound = MyIndex % 2 == 0;

                // 画安全点
                if (db_PD1_isNorthFirst)
                {
                    for (var i = 0; i < 5; i++)
                    {
                        dp.Name = $"一范站位-{i}";
                        dp.Scale = new(1);
                        dp.Color = (isFirstRound && i == MyIndex / 2) ? posColorPlayer.V4 : posColorNormal.V4;
                        dp.Delay = 2000;
                        dp.DestoryAt = 9000;
                        dp.Position = pos[0, i];
                        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

                        dp.Name = $"一范站位-{i}";
                        dp.Scale = new(1);
                        dp.Color = (isFirstRound && i == 4) ? posColorPlayer.V4 : posColorNormal.V4;
                        dp.Delay = 11000;
                        dp.DestoryAt = 5000;
                        dp.Position = pos[0, i];
                        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                    }
                }
                else
                {
                    for (var i = 0; i < 5; i++)
                    {
                        dp.Name = $"一范站位-{i}";
                        dp.Scale = new(1);
                        dp.Color = (isFirstRound && i == MyIndex / 2) ? posColorPlayer.V4 : posColorNormal.V4;
                        dp.Delay = 2000;
                        dp.DestoryAt = 9000;
                        dp.Position = pos[1, i];
                        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

                        dp.Name = $"一范站位-{i}";
                        dp.Scale = new(1);
                        dp.Color = (isFirstRound && i == 4) ? posColorPlayer.V4 : posColorNormal.V4;
                        dp.Delay = 11000;
                        dp.DestoryAt = 5000;
                        dp.Position = pos[1, i];
                        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                    }
                }
                db_PD1_drawn = true;
            }
        });
    }
    #endregion

    #region 门神：左右刀

    // TODO: Param?
    [ScriptMethod(name: "门神：左右刀记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["Param:regex:^(19|20|21|22|23|24)$"], userControl: false)]
    public void DB_SideCleaveRecord(Event @event, ScriptAccessory accessory)
    {
        var param = @event["Param"];
        var paramMapping = new Dictionary<string, (int index, bool value, string wing)>
        {
            // { key, (index, value, wing) }
            { "19", (0, true, "左上翅膀") },
            { "20", (0, false, "右上翅膀") },
            { "21", (1, true, "左中翅膀") },
            { "22", (1, false, "右中翅膀") },
            { "23", (2, true, "左下翅膀") },
            { "24", (2, false, "右下翅膀") }
        };
        if (paramMapping.ContainsKey(param))
        {
            var (index, value, wing) = paramMapping[param];
            db_isLeftCleave[index] = value;
            if (DebugMode)
                accessory.Method.SendChat($"/e [DEBUG]：检测到【{wing}】");
        }
    }

    // 左上翅膀 19  82E2 先 33506
    // 右上翅膀 20  82E1 先 33505
    // 左中翅膀 21  
    // 右中翅膀 22
    // 左下翅膀 23  82E8 先 33512
    // 右下翅膀 24  82E7 先 33511
    // Trinity of Souls
    [ScriptMethod(name: "门神：左右刀绘图", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(33506|33505|33512|33511)$"])]
    public void DB_SideCleaveDraw(Event @event, ScriptAccessory accessory)
    {
        // if (!ParseObjectId(@event["SourceId"], out var sid)) return;
        var spos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        var srot = JsonConvert.DeserializeObject<float>(@event["SourceRotation"]);
        // var isleftWingFirst = @event["ActionId"] == "33506" || @event["ActionId"] == "33512";
        var isTopWingFirst = @event["ActionId"] == "33506" || @event["ActionId"] == "33505";

        List<bool> sideCleaveLeft = db_isLeftCleave;
        sideCleaveLeft[1] = isTopWingFirst ? sideCleaveLeft[1] : !sideCleaveLeft[1];

        string action1_str = sideCleaveLeft[0] == sideCleaveLeft[1] ? "停" : "穿";
        string action2_str = sideCleaveLeft[1] == sideCleaveLeft[2] ? "停" : "穿";
        string action_str = isTopWingFirst ? $"先【{action1_str}】后【{action2_str}】" : $"先【{action2_str}】后【{action1_str}】";

        if (DebugMode)
            accessory.Method.SendChat($"/e [DEBUG]：躲避方案为：{action_str}");

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"左右刀-上翅膀";
        dp.Scale = new(50);
        dp.Position = spos;
        dp.Rotation = sideCleaveLeft[0] ? srot + float.Pi / 2 : srot + float.Pi / -2;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = isTopWingFirst ? 0 : 12600;
        dp.DestoryAt = isTopWingFirst ? 10000 : 2601;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"左右刀-中翅膀";
        dp.Scale = new(50);
        dp.Position = spos;
        dp.Rotation = sideCleaveLeft[1] ? srot + float.Pi / 2 : srot + float.Pi / -2;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = 10000;
        dp.DestoryAt = 2600;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"左右刀-下翅膀";
        dp.Scale = new(50);
        dp.Position = spos;
        dp.Rotation = sideCleaveLeft[2] ? srot + float.Pi / 2 : srot + float.Pi / -2;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = isTopWingFirst ? 12600 : 0;
        dp.DestoryAt = isTopWingFirst ? 2600 : 10000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        accessory.Method.TextInfo(action_str, 17000, true);

    }

    #endregion

    #region 门神：二范

    [ScriptMethod(name: "门神：小怪连线记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3352[12])$"], userControl: false)]
    public void DB_Paradeigma_II_LineRecord(Event @event, ScriptAccessory accessory)
    {
        // 如果是连黑线（33522），则需要白塔
        var shouldWhiteTower = @event["ActionId"] == "33522";
        var spos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);

        if (spos.Z < 80)
        {
            db_PD2_shouldWhiteTower[0] = shouldWhiteTower;
            if (DebugMode)
                accessory.Method.SendChat($"/e [DEBUG]：检测到北侧天使连【{(shouldWhiteTower ? "黑" : "白")}】线");

            if (spos.X > 100)
            {
                db_PD2_fromRightBottom = true;
            }

            if (DebugMode)
                accessory.Method.SendChat($"/e [DEBUG]：检测到北侧天使【{(db_PD2_fromRightBottom ? "偏右" : "偏左")}】");
        }
        else
        {
            int index = (spos.X > 120) ? 1 : (spos.Z > 120) ? 2 : 3;
            db_PD2_shouldWhiteTower[index] = shouldWhiteTower;
            if (DebugMode)
                accessory.Method.SendChat($"/e [DEBUG]：检测到{(index == 1 ? "东" : index == 2 ? "南" : "西")}侧天使连【{(shouldWhiteTower ? "黑" : "白")}】线");
        }
    }

    [ScriptMethod(name: "门神：二范黑白塔标记记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(3579|3580)$"], userControl: false)]
    public void DB_Paradeigma_II_TowerRecord(Event @event, ScriptAccessory accessory)
    {
        lock (db_PD2_lockObject)
        {
            if (!ParseObjectId(@event["TargetId"], out var tid)) return;
            var targetIndex = accessory.Data.PartyList.IndexOf(tid);
            if (targetIndex == -1) return;

            // 被选中代表要放塔，3579 灵临刻印，放白塔
            db_PD2_isChosenTower[targetIndex] = true;
            db_PD2_isWhiteTower[targetIndex] = @event["StatusID"] == "3579";
            db_PD2_towerRecordNum++;
        }
    }

    [ScriptMethod(name: "门神：小怪连线绘图", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3352[12])$"])]
    public void DB_Paradeigma_II_LineDraw(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;
        if (!ParseObjectId(@event["TargetId"], out var tid)) return;

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"小怪连线";
        dp.Scale = new(5);
        dp.ScaleMode = ScaleMode.YByDistance;
        dp.Owner = sid;
        dp.TargetObject = tid;
        dp.Color = tid == accessory.Data.Me ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
        dp.Delay = 0;
        dp.DestoryAt = 8700;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "门神：小怪冲击波绘图", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:33518"])]
    public void DB_Paradeigma_II_ShootDraw(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"小怪连线";
        dp.Scale = new(10, 60);
        dp.Owner = sid;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = 0;
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "门神：二范黑白塔绘图", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(3579|3580)$"])]
    public void DB_Paradeigma_II_TowerDraw(Event @event, ScriptAccessory accessory)
    {
        Task.Delay(100).ContinueWith(t =>
        {
            lock (db_PD2_lockObject)
            {
                if (db_PD2_drawn) return;
                if (DebugMode)
                    accessory.Method.SendChat($"/e [DEBUG]：进入了DB_Paradeigma_II_TowerDraw");

                if (db_PD2_towerRecordNum != 4) return;
                db_PD2_drawn = true;

                var MyIndex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
                if (MyIndex == -1 || !db_PD2_isChosenTower[MyIndex]) return;

                var tposIndex = MyIndex < 4
                    ? db_PD2_shouldWhiteTower.IndexOf(db_PD2_isWhiteTower[MyIndex])
                    : db_PD2_shouldWhiteTower.LastIndexOf(db_PD2_isWhiteTower[MyIndex]);

                if (DebugMode)
                    accessory.Method.SendChat($"/e [DEBUG]：检测需将塔引导给【{tposIndex}】号天使");

                if (!db_PD2_fromRightBottom) tposIndex++;

                Vector3[] tpos = new Vector3[5];
                tpos[0] = new(110, 0, 110);
                tpos[1] = new(90, 0, 110);
                tpos[2] = new(90, 0, 90);
                tpos[3] = new(110, 0, 90);
                tpos[4] = new(110, 0, 110);

                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"黑白塔放置位";
                dp.Scale = new(1);
                dp.Color = accessory.Data.DefaultSafeColor.WithW(3f);
                dp.Delay = 0;
                dp.DestoryAt = 11000;
                dp.Position = tpos[tposIndex];
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

                dp = accessory.Data.GetDefaultDrawProperties();
                dp.Scale = new(0.5f);
                dp.Name = $"黑白塔放置位指路";
                dp.Owner = accessory.Data.Me;
                dp.TargetPosition = tpos[tposIndex];;
                dp.ScaleMode = ScaleMode.YByDistance;
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.Delay = 0;
                dp.DestoryAt = 11000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
        });
    }

    [ScriptMethod(name: "门神：死刑", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:33532"])]
    public void DB_TankBuster(Event @event, ScriptAccessory accessory)
    {
        if (!ParseObjectId(@event["SourceId"], out var sid)) return;
        if (!ParseObjectId(@event["TargetId"], out var tid)) return;

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"一仇直线死刑-1";
        dp.Scale = new(5, 40);
        dp.Owner = sid;
        dp.TargetObject = tid;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = 0;
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"一仇直线死刑-2";
        dp.Scale = new(5, 40);
        dp.Owner = sid;
        dp.TargetResolvePattern = PositionResolvePatternEnum.OwnerTarget;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.DestoryAt = 8000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    #endregion

    #region 门神：超链I

    [ScriptMethod(name: "门神：进入超链阶段I", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:33498"], userControl: false)]
    public void DB_SuperChain_I_PhaseRecord(Event @event, ScriptAccessory accessory)
    {
        phase = 3;
    }

    // TODO: 修改判断条件为XXX的出现，或是Tether
    // TODO: 需要找到八分/四分小球，以及月环小球的连线长度
    // TODO: 找到ID规律，看看是否能SORT解决
    // 八分四分小球决定起始点与终点，月环小球连线长度决定中途点
    [ScriptMethod(name: "门神：超链I元素收集", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(16176|16177|16178|16179|16180)$"], userControl: false)]
    public void DB_SuperChain_I_TheoryCollect(Event @event, ScriptAccessory accessory)
    {
        lock (db_SC1_lockObject)
        {
            if (phase != 3) return;
            if (!ParseObjectId(@event["SourceId"], out var sid)) return;
            var did = @event["DataId"];
            db_SC1_theories.Add(sid);

            if (DebugMode)
                accessory.Method.SendChat($"/e [DEBUG]：捕捉到新的超链元素，当前列表内有{db_SC1_theories.Count}个！");
        }
    }

    [ScriptMethod(name: "门神：超链I第一组绘图", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(16176|16177|16178|16179|16180)$"])]
    public void DB_SuperChain_I_FirstRound(Event @event, ScriptAccessory accessory)
    {
        Task.Delay(100).ContinueWith(t =>
        {
            lock (db_SC1_lockObject)
            {
                if (db_SC1_round1_drawn) return;
                db_SC1_round1_drawn = true;
                if (phase != 3) return;
                if (db_SC1_theories.Count != 3) return;

                if (DebugMode)
                    accessory.Method.SendChat($"/e [DEBUG]：进入超链I第一组绘图……");

                IBattleChara? destTheory = null;
                for (int i = 0; i < 3; i++)
                {
                    var theoryObject = (IBattleChara?)accessory.Data.Objects.SearchById(db_SC1_theories[i]);
                    if (theoryObject == null) return;
                    switch (theoryObject.DataId)
                    {
                        case 16176:
                            destTheory = theoryObject;
                            break;
                        case 16177:
                        case 16178:
                            db_SC1_isOut = theoryObject.DataId == 16177 ? true : false;
                            break;
                        case 16179:
                        case 16180:
                            db_SC1_isSpread = theoryObject.DataId == 16179 ? true : false;
                            break;
                    }
                }
                if (destTheory == null) return;
                db_SC1_destTheoryDir_R1 = PositionFloorTo4Dir(destTheory.Position, new(100, 0, 100));

                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"超链I_第一步钢铁月环";
                dp.Position = destTheory.Position;
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Delay = 0;
                dp.DestoryAt = 11000;
                if (db_SC1_isOut)
                {
                    dp.Scale = new(7);
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                }
                else
                {
                    dp.Scale = new(20);
                    dp.InnerScale = new(6);
                    dp.Radian = float.Pi * 2;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                }

                var MyIndex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
                for (int i = 0; i < (db_SC1_isSpread ? 8 : 4); i++)
                {
                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"超链I_第一步四分八分{i}";
                    // dp.Owner = (uint)destTheory;
                    dp.Position = destTheory.Position;
                    dp.TargetObject = accessory.Data.PartyList[i];
                    dp.Color = (i == MyIndex || i == MyIndex - 4) ? accessory.Data.DefaultSafeColor.WithW(0.5f) : accessory.Data.DefaultDangerColor.WithW(0.5f);
                    dp.Delay = 7000;
                    dp.DestoryAt = 4000;
                    dp.Scale = new Vector2(40);
                    dp.Radian = db_SC1_isSpread ? float.Pi / 180 * 30 : float.Pi / 180 * 35;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
                }

                float deg_init = FindAngle(destTheory.Position, new Vector3(100, 0, 100));
                float deg;
                List<int> safePoint = db_SC1_isSpread ? [6, 1, 5, 2, 7, 0, 4, 3] : [3, 0, 1, 2];
                for (int i = 0; i < (db_SC1_isSpread ? 8 : 4); i++)
                {
                    dp = accessory.Data.GetDefaultDrawProperties();
                    dp.Name = $"超链I_第一步定位{i}";
                    dp.Scale = new(1f);
                    deg = db_SC1_isSpread ? deg_init + float.Pi / 4 * i + float.Pi / 8 : deg_init + float.Pi / 2 * i + float.Pi / 4;
                    dp.Position = ExtendPoint(destTheory.Position, deg, db_SC1_isOut ? 8 : 5);
                    dp.Color = safePoint[MyIndex] == i ? posColorPlayer.V4.WithW(3f) : posColorNormal.V4;
                    dp.Delay = 0;
                    dp.DestoryAt = 11500;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                }
            }
        });
    }

    // 3576     极性偏转·灵     白
    // 3577     极性偏转·星     黑
    // 3578     天火刻印        分散
    // 3579     灵临刻印        白塔
    // 3580     星临刻印        黑塔
    // 3581     灵击刻印        白分摊
    // 3582     星击刻印        黑分摊

    [ScriptMethod(name: "门神：超链IBuff收集", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(3576|3577|3579|3580|3581|3582)$"], userControl: false)]
    public void DB_SuperChain_I_BuffRecord(Event @event, ScriptAccessory accessory)
    {
        if (phase != 3) return;
        if (db_SC1_myBuff != -1 && !db_SC1_BWTBidx.Contains(-1)) return;
        if (!ParseObjectId(@event["TargetId"], out var tid)) return;
        var sid = @event["StatusID"];
        var sidMapping = new Dictionary<string, (int value, string mention, int lidx)>
        {
            { "3581", (0, "白分摊", 3) },
            { "3582", (1, "黑分摊", 2) },
            { "3579", (2, "白塔", 1) },
            { "3580", (3, "黑塔", 0) },
            // { "3578", (4, "分散", -1) },
            { "3576", (5, "初始白", -1) },
            { "3577", (6, "初始黑", -1) }
        };
        if (sidMapping.ContainsKey(sid))
        {
            var (value, mention, lidx) = sidMapping[sid];
            if (lidx != -1)
                db_SC1_BWTBidx[lidx] = accessory.Data.PartyList.IndexOf(tid);
            if (tid == accessory.Data.Me)
            {
                db_SC1_myBuff = value;
                if (DebugMode)
                    accessory.Method.SendChat($"/e [DEBUG]：捕捉到自身BUFF为【{mention}】");
            }
        }
    }

    // TODO: 这个BUFF收集完毕了，然后需要进行指路分散、放塔、踩塔
    [ScriptMethod(name: "门神：超链I分散Buff收集", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:3578"], userControl: false)]
    public void DB_SuperChain_I_SpreadBuffRecord(Event @event, ScriptAccessory accessory)
    {
        if (phase != 3) return;
        if (db_SC1_FinalSpreadChecked) return;
        if (!ParseObjectId(@event["TargetId"], out var tid)) return;
        var tidIndex = accessory.Data.PartyList.IndexOf(tid);
        db_SC1_FinalSpreadChecked = true;
        if (tidIndex < 4)
            db_SC1_TNfinalSpread = true;
        else
            db_SC1_TNfinalSpread = false;
        if (DebugMode)
            accessory.Method.SendChat($"/e [DEBUG] 检测到超链最后分散的是：【{(db_SC1_TNfinalSpread ? "TN组" : "DPS组")}】");
    }

    [ScriptMethod(name: "门神：超链I第二组绘图", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(16176|16177|16178|16179|16180)$"])]
    public void DB_SuperChain_I_SecondRound(Event @event, ScriptAccessory accessory)
    {
        Task.Delay(100).ContinueWith(t =>
        {
            lock (db_SC1_lockObject)
            {
                if (db_SC1_theories.Count() != 7) return;
                if (phase != 3) return;
                // 刷出两个终点、钢铁月环
                if (db_SC1_round2_drawn) return;
                db_SC1_round2_drawn = true;

                if (DebugMode)
                    accessory.Method.SendChat($"/e [DEBUG]：进入超链I第二组绘图……");

                List<ulong> SC1_SubList = db_SC1_theories.GetRange(3, 4);
                // 判断终点位置
                IBattleChara? dest1Theory = null;
                IBattleChara? dest2Theory = null;
                IBattleChara? inTheory = null;
                IBattleChara? outTheory = null;
                IBattleChara? proteanTheory = null;
                IBattleChara? partnerTheory = null;
                for (int i = 0; i < 4; i++)
                {
                    var theoryObject = (IBattleChara?)accessory.Data.Objects.SearchById(SC1_SubList[i]);
                    if (theoryObject == null) return;

                    switch (theoryObject.DataId)
                    {
                        case 16176:
                            if (dest1Theory == null)
                            {
                                dest1Theory = theoryObject;
                            }
                            else
                            {
                                dest2Theory = theoryObject;
                            }
                            break;
                        case 16177:
                            outTheory = theoryObject;
                            break;
                        case 16178:
                            inTheory = theoryObject;
                            break;
                        case 16179:
                            proteanTheory = theoryObject;
                            break;
                        case 16180:
                            partnerTheory = theoryObject;
                            break;
                    }
                }
                IBattleChara? destDonutChar = null;
                if (dest1Theory == null || dest2Theory == null || inTheory == null || outTheory == null) return;

                float dest1ToDonut = new Vector2(dest1Theory.Position.X - inTheory.Position.X,
                                                dest1Theory.Position.Z - inTheory.Position.Z).Length();
                float dest2ToDonut = new Vector2(dest2Theory.Position.X - inTheory.Position.X,
                                                dest2Theory.Position.Z - inTheory.Position.Z).Length();
                float dest1ToCircle = new Vector2(dest1Theory.Position.X - outTheory.Position.X,
                                                dest1Theory.Position.Z - outTheory.Position.Z).Length();
                float dest2ToCircle = new Vector2(dest2Theory.Position.X - outTheory.Position.X,
                                                dest2Theory.Position.Z - outTheory.Position.Z).Length();

                if (dest1ToDonut < dest1ToCircle && dest2ToDonut > dest2ToCircle)
                {
                    destDonutChar = dest1Theory;
                }

                else if (dest1ToDonut > dest1ToCircle && dest2ToDonut < dest2ToCircle)
                {
                    destDonutChar = dest2Theory;
                }

                if (destDonutChar == null) return;

                db_SC1_destTheoryDir_R2 = PositionFloorTo4Dir(destDonutChar.Position, new(100, 0, 100));

                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"超链I_第二步月环";
                dp.Position = destDonutChar.Position;
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Delay = 6500;
                dp.DestoryAt = 7001;
                dp.Scale = new(40);
                dp.InnerScale = new(6);
                dp.Radian = float.Pi * 2;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);

                var atLeft = db_SC1_myBuff == 2 || db_SC1_myBuff == 5 || db_SC1_myBuff == 1;

                dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"超链I_第二步左右";
                dp.TargetPosition = RotatePoint(destDonutChar.Position, new Vector3(100, 0, 100), atLeft ? float.Pi / 180 * -17 : float.Pi / 180 * 17);
                dp.Position = new(100, 0, 100);
                dp.Color = posColorPlayer.V4;
                dp.Delay = 6500;
                dp.DestoryAt = 7001;
                dp.Scale = new(5, 20);
                // dp.ScaleMode = ScaleMode.YByDistance;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            }
        });
    }

    [ScriptMethod(name: "门神：超链I第三组绘图", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(16176|16177|16178|16179|16180)$"])]
    public void DB_SuperChain_I_ThirdRound(Event @event, ScriptAccessory accessory)
    {
        Task.Delay(100).ContinueWith(t =>
        {
            lock (db_SC1_lockObject)
            {
                if (db_SC1_theories.Count() != 10) return;
                if (phase != 3) return;
                // 刷出一个终点、钢铁月环
                if (db_SC1_round3_drawn) return;
                db_SC1_round3_drawn = true;

                if (DebugMode)
                    accessory.Method.SendChat($"/e [DEBUG]：进入超链I第三组绘图……");

                List<ulong> SC1_SubList = db_SC1_theories.GetRange(7, 3);
                // 判断终点位置
                IBattleChara? destTheory = null;
                IBattleChara? inTheory = null;
                IBattleChara? outTheory = null;
                for (int i = 0; i < 3; i++)
                {
                    var theoryObject = (IBattleChara?)accessory.Data.Objects.SearchById(SC1_SubList[i]);
                    if (theoryObject == null) return;

                    switch (theoryObject.DataId)
                    {
                        case 16176:
                            destTheory = theoryObject;
                            break;
                        case 16177:
                            outTheory = theoryObject;
                            break;
                        case 16178:
                            inTheory = theoryObject;
                            break;
                    }
                }

                if (destTheory == null || inTheory == null || outTheory == null) return;

                float destToDonut = new Vector2(destTheory.Position.X - inTheory.Position.X,
                                                destTheory.Position.Z - inTheory.Position.Z).Length();
                float destToCircle = new Vector2(destTheory.Position.X - outTheory.Position.X,
                                                destTheory.Position.Z - outTheory.Position.Z).Length();

                var isDonutFirst = false;
                if (destToDonut < destToCircle)
                    isDonutFirst = true;

                db_SC1_destTheoryDir_R3 = PositionFloorTo4Dir(destTheory.Position, new(100, 0, 100));

                var dp = accessory.Data.GetDefaultDrawProperties();
                dp.Name = $"超链I_第三步月环";
                dp.Position = destTheory.Position;
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Delay = isDonutFirst ? 10000 : 14800;
                dp.DestoryAt = isDonutFirst ? 4800 : 2000;
                dp.Scale = new(40);
                dp.InnerScale = new(6);
                dp.Radian = float.Pi * 2;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);

                dp.Name = $"超链I_第三步钢铁";
                dp.Position = destTheory.Position;
                dp.Color = accessory.Data.DefaultDangerColor;
                dp.Delay = isDonutFirst ? 14800 : 10000;
                dp.DestoryAt = isDonutFirst ? 2000 : 4800;
                dp.Scale = new(7);
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }
        });
    }

    #endregion

    #region 门神：对话

    [ScriptMethod(name: "门神：对话绘图", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3353[45])$"])]
    public void DB_Dialogos(Event @event, ScriptAccessory accessory)
    {

        if (!ParseObjectId(@event["SourceId"], out var sid)) return;
        phase = 4;

        var MyIndex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
        var isMT = MyIndex == 0;
        var isST = MyIndex == 1;

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"对话-近";
        dp.Owner = sid;
        dp.Color = isMT ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
        dp.CentreResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
        dp.CentreOrderIndex = 1u;
        dp.Delay = 0;
        dp.DestoryAt = 5200;
        dp.Scale = new(6);
        dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"对话-远";
        dp.Owner = sid;
        dp.Color = isST ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
        dp.CentreResolvePattern = PositionResolvePatternEnum.PlayerFarestOrder;
        dp.CentreOrderIndex = 1u;
        dp.Delay = 0;
        dp.DestoryAt = 6200;
        dp.Scale = new(6);
        dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    #endregion

}