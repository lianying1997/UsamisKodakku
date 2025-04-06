using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Network.Structures.InfoProxy;
using Newtonsoft.Json;
using Dalamud.Utility.Numerics;
using ECommons;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.MathHelpers;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.Draw.Manager;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs;
using KodakkuAssist.Module.Script.Type;

namespace UsamisScript.EndWalker.p8s;

[ScriptType(name: "P8S [零式万魔殿 炼净之狱4]", territorys: [1088], guid: "97df6974-c726-4a00-9016-293c184adf5c", version: "0.0.0.6", author: "Usami", note: noteStr)]
public class p8s
{
    const string noteStr =
    """
    v0.0.0.6
    一车设置场中H2，正右ST。
    鸭门。
    """;

    private const string UpdateInfo =
    """
    1. 修正门神一车站位。
    """;

    [UserSetting("Debug模式，非开发用请关闭")]
    public static bool DebugMode { get; set; } = false;

    [UserSetting("启用本体一运塔[小队频道]发宏")]
    public static bool HC1_ChatGuidance { get; set; } = false;

    [UserSetting("站位提示圈绘图-普通颜色")]
    public static ScriptColor posColorNormal { get; set; } = new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 1.0f, 1.0f) };

    [UserSetting("站位提示圈绘图-玩家站位颜色")]
    public static ScriptColor posColorPlayer { get; set; } = new ScriptColor { V4 = new Vector4(0.0f, 1.0f, 1.0f, 1.0f) };

    // 门神 记录分摊分散
    bool db_isStack;
    // 门神 蓝线灼炎次数
    int db_torchFlameNum;
    int db_illusorySunforgeTimes;
    int db_gorgonIdx;
    int db_gorgonPartnerIdx;

    uint db_gorgonTarget;
    int db_gorgonTargetPos;

    List<uint> db_upliftOrder = [];
    List<uint> db_flareTarget = [];
    bool[] db_isFirstRound = [false, false, false, false, false, false, false, false];
    bool[] db_isGorgonEye = [false, false, false, false, false, false, false, false];
    List<int> db_GorgonPosition = [];
    List<uint> db_GorgonSid = [];

    public enum MB_Phase
    {
        Opening,
        NA1,
        HC1,
        LD,
        NA2,
        HC2,
    }
    MB_Phase mb_phase = MB_Phase.Opening;
    static bool mb_isLeftCleave = false;   // 本体，是左半场刀
    // 本体，是紫圈目标
    bool[] mb_isNATarget = [false, false, false, false, false, false, false, false];

    // 本体一运对应BUFF目标
    // 2人分摊，3人分摊，短alpha，长alpha，短beta，长beta，短gamma，长gamma
    List<uint> mb_hc1_sid = [0, 0, 0, 0, 0, 0, 0, 0];
    uint mb_sideCleaveNum;
    uint mb_conceptFinNum;
    // string? mb_towerColor;
    string? mb_mentionTxt;
    Vector3 mb_TwoStackDestination = default;
    Vector3 mb_ThreeStackDestination = default;
    Vector3 mb_UnmergeDestination = default;
    bool[] mb_joinMerge = [false, false, false];

    uint mb_alphaLongFollower;
    uint mb_betaLongFollower;
    uint mb_gammaLongFollower;
    bool mb_NA1_isTNFixed = false;
    bool mb_NA1_isLine1Safe = false;
    List<int> mb_LD_playerOrder = [];       // 本体万象阶段，玩家易伤顺序
    List<LD_Tower> mb_LD_towerOrder = [];   // 本体万象阶段，塔顺序

    public void Init(ScriptAccessory accessory)
    {
        db_torchFlameNum = 0;
        db_illusorySunforgeTimes = 0;
        db_gorgonIdx = 0;
        db_gorgonPartnerIdx = -1;
        db_isStack = false;
        db_gorgonTarget = 0;
        db_gorgonTargetPos = 0;
        bool[] db_isFirstRound = [false, false, false, false, false, false, false, false];
        bool[] db_isGorgonEye = [false, false, false, false, false, false, false, false];

        db_upliftOrder = [];
        db_flareTarget = [];
        db_GorgonPosition = [];
        db_GorgonSid = [];

        mb_phase = MB_Phase.Opening;

        mb_isLeftCleave = false;   // 本体，是左半场刀
        mb_isNATarget = [false, false, false, false, false, false, false, false];

        mb_hc1_sid = [0, 0, 0, 0, 0, 0, 0, 0];
        mb_sideCleaveNum = 0;
        mb_conceptFinNum = 0;
        mb_mentionTxt = "Hello Koda!";

        mb_TwoStackDestination = default;
        mb_ThreeStackDestination = default;
        mb_UnmergeDestination = default;
        mb_joinMerge = [false, false, false];

        mb_alphaLongFollower = 0;
        mb_betaLongFollower = 0;
        mb_gammaLongFollower = 0;
        mb_NA1_isTNFixed = false;
        mb_NA1_isLine1Safe = false;

        mb_LD_playerOrder = [];         // 本体万象阶段，玩家易伤顺序
        mb_LD_towerOrder = [];          // 本体万象阶段，塔顺序

        accessory.Method.RemoveDraw(".*");
    }
    public static void DebugMsg(string str, ScriptAccessory accessory)
    {
        if (!DebugMode) return;
        accessory.Method.SendChat($"/e [DEBUG] {str}");
    }

    [ScriptMethod(name: "随时DEBUG用", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:=TST"], userControl: false)]
    public void EchoDebug(Event @event, ScriptAccessory accessory)
    {
        if (!DebugMode) return;
        var msg = @event["Message"].ToString();
        accessory.Method.SendChat($"/e 获得玩家发送的消息：{msg}");

        mb_mentionTxt = "Line1";
        accessory.Method.SendChat($"/e {mb_mentionTxt}");
        mb_mentionTxt = "Line2";
        accessory.Method.SendChat($"/e {mb_mentionTxt}");
        mb_mentionTxt = "Line3";
        accessory.Method.SendChat($"/e {mb_mentionTxt}");

    }

    [ScriptMethod(name: "防击退删除画图", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(7559|7548|7389)$"], userControl: false)]
    // 沉稳咏唱|亲疏自行|原初的解放
    public void RemoveLine(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        if (sid == accessory.Data.Me)
        {
            accessory.Method.RemoveDraw("^(可防击退-.*)$");
            // DebugMsg($"/e 检测到防击退，删除击退标志", accessory);
        }
    }

    #region 门神：基础

    [ScriptMethod(name: "门神：记录分摊分散", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3099[67])$"], userControl: false)]
    public void DB_RecordSpreadAndStack(Event @event, ScriptAccessory accessory)
    {
        // 30996 分散（八分核爆之念）
        // 30997 分摊（四分核爆之念）
        db_isStack = @event.ActionId() == 30996;
        DebugMsg($"已记录下【{(db_isStack ? "分散" : "分摊")}】。", accessory);
    }

    [ScriptMethod(name: "门神：蓝线范围", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31015"])]
    public void DB_TorchFlame(Event @event, ScriptAccessory accessory)
    {
        // if (db_torchFlameNum >= 12) return;
        // accessory.Method.SendChat($"/e db_torchFlameNum {db_torchFlameNum}……");

        db_torchFlameNum++;
        var spos = @event.SourcePosition();
        var dp = assignDp_TorchFlame(spos, 0, 10000, $"灼炎{db_torchFlameNum}", accessory);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }
    private static DrawPropertiesEdit assignDp_TorchFlame(Vector3 pos, int delay, int destoryAt, string name, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(10, 10);
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Position = pos - new Vector3(0, 0, 5);
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        return dp;
    }

    [ScriptMethod(name: "门神：龙龙凤凤", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3099[45])$"])]
    public void DB_Sunforge(Event @event, ScriptAccessory accessory)
    {
        var epos = @event.EffectPosition();
        var srot = @event.SourceRotation();
        // var epos = JsonConvert.DeserializeObject<Vector3>(@event["EffectPosition"]);
        // var tpos = JsonConvert.DeserializeObject<Vector3>(@event["TargetPosition"]);
        // var srot = JsonConvert.DeserializeObject<float>(@event["SourceRotation"]);
        // var isHotTail = @event["ActionId"] == "30994";

        var isDragon = @event.ActionId() == 30994;
        var isOpening = db_torchFlameNum == 12;

        // 开场会先出蓝火，后出龙凤，因此使用两种颜色区分。
        if (isDragon)
        {
            var dp = assignDp_DragonLine(epos, srot, 0, 7700, $"龙龙{db_torchFlameNum}", accessory);
            dp.Delay = isOpening ? 3000 : 0;
            dp.DestoryAt = isOpening ? 4700 : 7700;
            dp.Color = isOpening ? ColorHelper.DelayDangerColor.V4 : accessory.Data.DefaultDangerColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
        else
        {
            var dp = assignDp_PhoenixWing(epos, srot, 0, 7700, $"凤凤{db_torchFlameNum}", accessory);
            dp.Color = isOpening ? ColorHelper.DelayDangerColor.V4 : accessory.Data.DefaultDangerColor;
            dp.Delay = isOpening ? 3000 : 0;
            dp.DestoryAt = isOpening ? 4700 : 7700;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
        accessory.Method.TextInfo($"即将【{(db_isStack ? "分散" : "分摊")}】……", 8000, true);
    }

    private static DrawPropertiesEdit assignDp_DragonLine(Vector3 pos, float rot, int delay, int destoryAt, string name, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(14, 45);
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Position = pos;
        dp.Rotation = rot;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        return dp;
    }
    private static DrawPropertiesEdit assignDp_PhoenixWing(Vector3 pos, float rot, int delay, int destoryAt, string name, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(45, 20);
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Position = pos;
        dp.Rotation = rot;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        return dp;
    }

    [ScriptMethod(name: "门神：死刑", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31045"])]
    public void DB_TankBuster(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var tid = @event.TargetId();
        // if (!ParseObjectId(@event["SourceId"], out var sid)) return;
        // if (!ParseObjectId(@event["TargetId"], out var tid)) return;

        var dp1 = assignDp_TankBusterLine(sid, 0, 6000, $"直线死刑-目标", accessory);
        dp1.TargetObject = tid;

        var dp2 = assignDp_TankBusterLine(sid, 0, 9000, $"直线死刑-一仇", accessory);
        dp2.TargetResolvePattern = PositionResolvePatternEnum.OwnerTarget;

        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp1);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp2);
    }

    private static DrawPropertiesEdit assignDp_TankBusterLine(uint sid, int delay, int destoryAt, string name, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Owner = sid;
        dp.Scale = new(5, 40);
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        return dp;
    }

    [ScriptMethod(name: "门神：变身", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3105[12])$"])]
    public void DB_Reforge(Event @event, ScriptAccessory accessory)
    {
        var isSnake = @event.ActionId() == 31052;
        var sid = @event.SourceId();

        if (isSnake)
        {
            var dp = assignDp_ReforgeSnakeCircle(sid, 0, 11500, $"蛇蛇钢铁", accessory);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        else
        {
            var dp = assignDp_ReforgeBeastKB(sid, 4000, 6000, $"车车击退", accessory);
            dp.Color = accessory.Data.DefaultDangerColor.WithW(3);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
        }
    }
    private static DrawPropertiesEdit assignDp_ReforgeSnakeCircle(uint sid, int delay, int destoryAt, string name, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(10);
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = sid;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        return dp;
    }

    private static DrawPropertiesEdit assignDp_ReforgeBeastKB(uint sid, int delay, int destoryAt, string name, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(2, 20);
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = accessory.Data.Me;
        dp.Rotation = float.Pi;
        dp.TargetObject = sid;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        return dp;
    }

    #endregion

    #region 门神：一车

    [ScriptMethod(name: "门神：一车分散提示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31027"])]
    public void DB_BeastSpread(Event @event, ScriptAccessory accessory)
    {
        for (int i = 0; i < accessory.Data.PartyList.Count(); i++)
        {
            var dp = AssignDp.drawCircle(accessory.Data.PartyList[i], 0, 13000, $"一车分散告警{i}", accessory);
            dp.Scale = new(5);
            dp.Color = accessory.Data.DefaultDangerColor.WithW(0.6f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        drawSpreadPos(accessory);
    }

    private static void drawSpreadPos(ScriptAccessory accessory)
    {
        Vector3[] safePos = new Vector3[8];
        safePos[0] = new Vector3(100, 0, 90);
        safePos[1] = new Vector3(110, 0, 100);
        safePos[2] = new Vector3(90, 0, 100);
        safePos[3] = new Vector3(100, 0, 100);
        safePos[4] = new Vector3(90, 0, 110);
        safePos[5] = new Vector3(110, 0, 110);
        safePos[6] = new Vector3(90, 0, 90);
        safePos[7] = new Vector3(110, 0, 90);

        var myIndex = IndexHelper.getMyIndex(accessory);

        for (int i = 0; i < 8; i++)
        {
            var dp = AssignDp.drawStatic(safePos[i], 0, 0, 6000, $"一车起始定点{i}", accessory);
            dp.Scale = new(1.5f);
            dp.Color = myIndex == i ? posColorPlayer.V4.WithW(2f) : posColorNormal.V4;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        var dp0 = AssignDp.dirPos(safePos[myIndex], 0, 6000, $"一车起始指路{myIndex}", accessory);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);
    }

    // 决定受击顺序
    [ScriptMethod(name: "门神：一车受击顺序记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31029"], userControl: false)]
    public void DB_UpliftRecord(Event @event, ScriptAccessory accessory)
    {
        var tid = @event.TargetId();
        if (@event.TargetIndex() != 1) return;
        db_upliftOrder.Add(tid);
    }

    // 决定引导顺序
    [ScriptMethod(name: "门神：一车引导", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31030"])]
    public void DB_Stomp(Event @event, ScriptAccessory accessory)
    {
        if (db_upliftOrder.Count(x => x == accessory.Data.Me) > 1)
        {
            accessory.Method.TextInfo("自求多福吧……", 8000, true);
            return;
        }
        var myTurn = db_upliftOrder.IndexOf(accessory.Data.Me) / 2 + 1;
        drawBeastStompRouteDir(myTurn, accessory);
        switch (myTurn)
        {
            case 1: accessory.Method.TextInfo("第一轮，先【左上1点引导】，后【A/D躲避】", 8000, true); return;
            case 2: accessory.Method.TextInfo("第二轮，先【场中引导】，后【A/D躲避】", 8000, true); return;
            case 3: accessory.Method.TextInfo("第三轮，先【A/D躲避】，后【左上1点引导】", 8000, true); return;
            case 4: accessory.Method.TextInfo("第四轮，先【A/D躲避】，后【场中引导】", 8000, true); return;
            default: accessory.Method.TextInfo("似乎赫淮斯托斯没有注意到你……", 8000, true); return;
        }
    }
    private static void drawBeastStompRouteDir(int myTurn, ScriptAccessory accessory)
    {
        // 左上，正上，正左，中
        Vector3[] beastStompPos = [new(90, 0, 90), new(100, 0, 90), new(90, 0, 100), new(100, 0, 100)];
        for (int i = 0; i < 4; i++)
        {
            var dp = AssignDp.drawStatic(beastStompPos[i], 0, 0, 8000, $"一车位置{i}", accessory);
            dp.Scale = new(1.5f);
            dp.Color = posColorNormal.V4.WithW(0.6f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        List<int> stompIdx = myTurn switch
        {
            1 => new List<int> { 0, 1, 1, 1 },
            2 => new List<int> { 3, 3, 2, 2 },
            3 => new List<int> { 1, 0, 0, 1 },
            4 => new List<int> { 1, 1, 3, 3 },
            _ => new List<int> { -1, -1, -1, -1 }
        };

        List<int> delayTime = [0, 5250, 7750, 10250];
        List<int> destoryTime = [5250, 2500, 2500, 2500];

        for (int i = 0; i < 4; i++)
        {
            var dp_a = AssignDp.dirPos(beastStompPos[stompIdx[i]], delayTime[i], destoryTime[i], $"指路{i}", accessory);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp_a);

            if (i + 1 == 4) break;
            var dp_b = AssignDp.dirPos2Pos(beastStompPos[stompIdx[i]], beastStompPos[stompIdx[i + 1]], delayTime[i], destoryTime[i], $"准备就位{i}", accessory);
            dp_b.Color = accessory.Data.DefaultDangerColor;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp_b);
        }
    }

    #endregion

    #region 门神：一蛇

    // 177.3 "Gorgomanteia" Ability { id: "791A", source: "Hephaistos" }
    // 记录玩家buff
    // 3004 BBC 麻将1
    // 3005 BBD 麻将2
    // 3351 D17 石化
    // 3326 CFE 放毒
    [ScriptMethod(name: "门神：一蛇记录Buff", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(3004|3005|3351|3326)$"], userControl: false)]
    public void DB_BRC_Gorgomanteia(Event @event, ScriptAccessory accessory)
    {
        var tid = @event.TargetId();
        var index = IndexHelper.getPlayerIdIndex(tid, accessory);
        if (index == -1) return;

        if (@event.StatusID() == 3004 || @event.StatusID() == 3005)
            db_isFirstRound[index] = @event.StatusID() == 3004;
        else
            db_isGorgonEye[index] = @event.StatusID() == 3351;

    }

    // 可能存在一蛇BUFF时掉人，躺着的没有BUFF……
    [ScriptMethod(name: "门神：一蛇找队友", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31018"], userControl: false)]
    public void DB_GorgonPartnerRecord(Event @event, ScriptAccessory accessory)
    {
        var MyIndex = IndexHelper.getMyIndex(accessory);
        for (int i = 0; i < 8; i++)
        {
            // 在8个玩家中，寻找：与自己同一轮，与自己拥有同类型buff的玩家，且不能是自己
            bool isSameRound = db_isFirstRound[i] == db_isFirstRound[MyIndex];
            bool isSameGorgonEye = db_isGorgonEye[i] == db_isGorgonEye[MyIndex];
            bool isNotMyself = i != MyIndex;

            if (isSameRound && isSameGorgonEye && isNotMyself)
            {
                db_gorgonPartnerIdx = i;
                DebugMsg($"找到你的搭档: {IndexHelper.getPlayerJobByIndex(i)}", accessory);
                break;  // 找到一个符合条件的队友，退出循环
            }
        }
    }

    [ScriptMethod(name: "门神：一蛇记录位置", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31019"], userControl: false)]
    public void DB_GorgonPositionRecord(Event @event, ScriptAccessory accessory)
    {
        var spos = @event.SourcePosition();
        var sid = @event.SourceId();
        // db_GorgonPosition与db_GorgonSid两个list，同一个index记录下同一蛇的SourceID与八向逻辑方位
        db_GorgonPosition.Add(DirectionCalc.PositionRoundToDirs(spos, new(100, 0, 100), 8));
        db_GorgonSid.Add(sid);
    }

    [ScriptMethod(name: "门神：一蛇视线范围", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31019"])]
    public void DB_Petrifaction(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var spos = @event.SourcePosition();

        // 绘制蛇蛇出现地点
        var dp1 = assignDp_SnakeAppearPos(spos, 0, 10000, $"蛇蛇{sid}出现地点", accessory);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp1);

        // 绘制背对蛇蛇
        var dp2 = assignDp_SnakeSightAvoid(spos, 0, 10000, $"蛇蛇{sid}背对", accessory);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.SightAvoid, dp2);
    }

    private static DrawPropertiesEdit assignDp_SnakeAppearPos(Vector3 pos, int delay, int destoryAt, string name, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(2);
        dp.Color = ColorHelper.GorgonColor.V4;
        dp.Position = pos;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        return dp;
    }

    private static DrawPropertiesEdit assignDp_SnakeSightAvoid(Vector3 tpos, int delay, int destoryAt, string name, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = accessory.Data.Me;
        dp.TargetPosition = tpos;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        return dp;
    }

    [ScriptMethod(name: "门神：一蛇优先级记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31019"], userControl: false)]
    public void DB_GorgonPriority(Event @event, ScriptAccessory accessory)
    {
        lock (this)
        {
            // 蛇释放石化后计数
            db_gorgonIdx++;
            var myIndex = IndexHelper.getMyIndex(accessory);
            if (db_gorgonIdx != 2 && db_gorgonIdx != 4) return;
            if (db_gorgonIdx == 2 && !db_isFirstRound[myIndex]) return;
            if (db_gorgonIdx == 4 && db_isFirstRound[myIndex]) return;

            // 寻找目标蛇
            // 分辨与搭档的优先级
            bool isHighPriority = myIndex < db_gorgonPartnerIdx ? true : false;
            DebugMsg($"我的优先级是【{(isHighPriority ? "高" : "低")}】", accessory);

            // accessory.Method.SendChat($"/e 我的优先级是{(isHighPriority ? "高" : "低")}……");
            uint gorgon_HighPriority;
            uint gorgon_LowPriority;
            int gorgon_HighPriorityPosition;
            int gorgon_LowPriorityPosition;

            // 设置蛇位置优先级
            if (db_isFirstRound[myIndex])
            {
                gorgon_HighPriorityPosition = db_GorgonPosition[0] < db_GorgonPosition[1] ? db_GorgonPosition[0] : db_GorgonPosition[1];
                gorgon_LowPriorityPosition = db_GorgonPosition[0] == gorgon_HighPriorityPosition ? db_GorgonPosition[1] : db_GorgonPosition[0];
                gorgon_HighPriority = db_GorgonPosition[0] < db_GorgonPosition[1] ? db_GorgonSid[0] : db_GorgonSid[1];
                gorgon_LowPriority = db_GorgonSid[0] == gorgon_HighPriority ? db_GorgonSid[1] : db_GorgonSid[0];
            }
            else
            {
                gorgon_HighPriorityPosition = db_GorgonPosition[2] < db_GorgonPosition[3] ? db_GorgonPosition[2] : db_GorgonPosition[3];
                gorgon_LowPriorityPosition = db_GorgonPosition[2] == gorgon_HighPriorityPosition ? db_GorgonPosition[3] : db_GorgonPosition[2];
                gorgon_HighPriority = db_GorgonPosition[2] < db_GorgonPosition[3] ? db_GorgonSid[2] : db_GorgonSid[3];
                gorgon_LowPriority = db_GorgonSid[2] == gorgon_HighPriority ? db_GorgonSid[3] : db_GorgonSid[2];
            }

            DebugMsg($"高优先级蛇在{gorgon_HighPriorityPosition}，低优先级蛇在{gorgon_LowPriorityPosition}", accessory);

            db_gorgonTarget = isHighPriority ? gorgon_HighPriority : gorgon_LowPriority;
            db_gorgonTargetPos = isHighPriority ? gorgon_HighPriorityPosition : gorgon_LowPriorityPosition;
        }
    }

    // 201.1 "Eye of the Gorgon 1" Ability { id: "792D", source: "Hephaistos" }
    // 玩家放蛇范围提示
    [ScriptMethod(name: "门神：一蛇石化眼指向", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31019"])]
    public void DB_EyeGorgon(Event @event, ScriptAccessory accessory)
    {
        Task.Delay(50).ContinueWith(t =>
        {
            var myIndex = IndexHelper.getMyIndex(accessory);

            if (!db_isGorgonEye[myIndex]) return;
            if (db_gorgonIdx != 2 && db_gorgonIdx != 4) return;
            if (db_gorgonIdx == 2 && !db_isFirstRound[myIndex]) return;
            if (db_gorgonIdx == 4 && db_isFirstRound[myIndex]) return;

            // 绘制石化眼扇形
            var dp1 = assignDp_GorgonEyeFan(0, 5000, $"石化眼{db_gorgonIdx}", accessory);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp1);

            // 绘制石化眼目标蛇蛇
            var dp2 = assignDp_SnakeTarget(db_gorgonTarget, 0, 10000, $"蛇蛇目标{db_gorgonIdx}", accessory);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp2);

            var dp3 = AssignDp.dirTarget(db_gorgonTarget, 0, 5000, $"指向目标蛇蛇{db_gorgonIdx}", accessory);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp3);

            // 传输信息
            string gorgon_target_position_txt = "未知位置";  // 默认值
            switch (db_gorgonTargetPos)
            {
                case 0: gorgon_target_position_txt = "正上方 A"; break;
                case 1: gorgon_target_position_txt = "右上方 2"; break;
                case 2: gorgon_target_position_txt = "正右方 B"; break;
                case 3: gorgon_target_position_txt = "右下方 3"; break;
                case 4: gorgon_target_position_txt = "正下方 C"; break;
                case 5: gorgon_target_position_txt = "左下方 4"; break;
                case 6: gorgon_target_position_txt = "正左方 D"; break;
                case 7: gorgon_target_position_txt = "左上方 1"; break;
            }
            accessory.Method.TextInfo($"控制住【{gorgon_target_position_txt}】点蛇的行动……", 8000, true);
        });
    }
    private static DrawPropertiesEdit assignDp_GorgonEyeFan(int delay, int destoryAt, string name, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(50);
        dp.Radian = float.Pi / 4;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = accessory.Data.Me;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        return dp;
    }

    private static DrawPropertiesEdit assignDp_SnakeTarget(uint tid, int delay, int destoryAt, string name, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(2);
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Owner = tid;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        return dp;
    }

    // 204.2 "Blood of the Gorgon 1" Ability { id: "792F", source: "Hephaistos" }
    // 玩家放毒范围提示
    [ScriptMethod(name: "门神：一蛇放毒指向", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:31019"])]
    public void DB_BloodGorgon(Event @event, ScriptAccessory accessory)
    {
        Task.Delay(50).ContinueWith(t =>
        {
            var myIndex = IndexHelper.getMyIndex(accessory);

            if (db_isGorgonEye[myIndex]) return;
            if (db_gorgonIdx != 2 && db_gorgonIdx != 4) return;
            if (db_gorgonIdx == 2 && !db_isFirstRound[myIndex]) return;
            if (db_gorgonIdx == 4 && db_isFirstRound[myIndex]) return;

            // 绘制放毒圆形
            var dp1 = assignDp_PoisonCircle(accessory.Data.Me, 0, 5000, $"放毒圆形{db_gorgonIdx}", accessory);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp1);

            // 绘制放毒目标蛇蛇
            var dp2 = assignDp_SnakeTarget(db_gorgonTarget, 0, 10000, $"蛇蛇目标{db_gorgonIdx}", accessory);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp2);

            var dp3 = AssignDp.dirTarget(db_gorgonTarget, 0, 10000, $"指向目标蛇蛇{db_gorgonIdx}", accessory);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp3);

            // 传输信息
            string gorgon_target_position_txt = "未知位置";  // 默认值
            switch (db_gorgonTargetPos)
            {
                case 0: gorgon_target_position_txt = "正上方 A"; break;
                case 1: gorgon_target_position_txt = "右上方 2"; break;
                case 2: gorgon_target_position_txt = "正右方 B"; break;
                case 3: gorgon_target_position_txt = "右下方 3"; break;
                case 4: gorgon_target_position_txt = "正下方 C"; break;
                case 5: gorgon_target_position_txt = "左下方 4"; break;
                case 6: gorgon_target_position_txt = "正左方 D"; break;
                case 7: gorgon_target_position_txt = "左上方 1"; break;
            }
            accessory.Method.TextInfo($"用毒素制止【{gorgon_target_position_txt}】点蛇的行动……", 8000, true);
        });
    }

    private static DrawPropertiesEdit assignDp_PoisonCircle(uint owner_id, int delay, int destoryAt, string name, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(4);
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = owner_id;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        return dp;
    }

    #endregion

    #region 门神：一分身

    [ScriptMethod(name: "门神：幻影龙龙凤凤", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3105[89])$"])]
    public void DB_IllusorySunforge(Event @event, ScriptAccessory accessory)
    {
        db_illusorySunforgeTimes++;
        var epos = @event.EffectPosition();
        var srot = @event.SourceRotation();

        var isDragon = @event.ActionId() == 31058;

        if (isDragon)
        {
            var dp = assignDp_DragonLine(epos, srot, 0, 7700, $"龙龙分身", accessory);
            dp.Color = ColorHelper.DelayDangerColor.V4;
            dp.Delay = db_illusorySunforgeTimes < 3 ? 0 : 1000;
            dp.DestoryAt = 7500;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
        else
        {
            var dp = assignDp_PhoenixWing(epos, srot, 0, 7700, $"凤凤分身", accessory);
            dp.Scale = new(90, 20);
            dp.Color = ColorHelper.DelayDangerColor.V4;
            dp.Delay = db_illusorySunforgeTimes < 3 ? 0 : 1000;
            dp.DestoryAt = 7500;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
    }

    // 龙龙凤凤与分散
    [ScriptMethod(name: "门神：一分身分散", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31009"])]
    public void DB_ManifoldFlames(Event @event, ScriptAccessory accessory)
    {
        for (int i = 0; i < accessory.Data.PartyList.Count(); i++)
        {
            var dp = assignDp_IllusionSpread(accessory.Data.PartyList[i], 0, 6500, $"一分身分散{i}", accessory);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }
    private static DrawPropertiesEdit assignDp_IllusionSpread(uint sid, int delay, int destoryAt, string name, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(5);
        dp.Owner = sid;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        return dp;
    }

    [ScriptMethod(name: "门神：一分身易伤收集", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:29390"], userControl: false)]
    public void DB_HemitheosFlare(Event @event, ScriptAccessory accessory)
    {
        var tid = @event.TargetId();
        db_flareTarget.Add(tid);
    }

    [ScriptMethod(name: "门神：一分身引导", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31009"])]
    public void DB_NestFlamevipers(Event @event, ScriptAccessory accessory)
    {
        Task.Delay(6000).ContinueWith(t =>
        {
            var hasDebuff = db_flareTarget.Contains(accessory.Data.Me);
            var sid = @event.SourceId();

            for (uint i = 1; i < 5; i++)
            {
                var dp = AssignDp.drawTargetOrder(sid, i, 0, 4000, $"一分身引导-{i}", accessory);
                dp.Scale = new(5, 40);
                dp.TargetOrderIndex = i;
                dp.Color = hasDebuff ? accessory.Data.DefaultDangerColor : accessory.Data.DefaultSafeColor;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            }

            if (hasDebuff)
                accessory.Method.TextInfo("正点远离避开引导", 4000, true);
            else
                accessory.Method.TextInfo("斜点靠近引导", 4000, true);
        });
    }

    [ScriptMethod(name: "门神：一分身分摊分散", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3100[67])$"])]
    public void DB_EmergentFlare(Event @event, ScriptAccessory accessory)
    {
        var isSpread = @event.ActionId() == 31007;
        var sid = @event.SourceId();
        if (isSpread)
        {
            for (int i = 0; i < accessory.Data.PartyList.Count(); i++)
            {
                var dp = AssignDp.drawOwner2Target(sid, accessory.Data.PartyList[i], 0, 6000, $"一分身分散直线{i}", accessory);
                dp.Scale = new(5, 40);
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            }
        }
        else
        {
            int[] parterner = [6, 7, 4, 5, 2, 3, 0, 1];
            var myIndex = IndexHelper.getMyIndex(accessory);
            for (int i = 0; i < 4; i++)
            {
                var ii = myIndex > 3 ? i + 4 : i;
                var dp = AssignDp.drawCircle(accessory.Data.PartyList[ii], 0, 6000, $"一分身分摊{ii}", accessory);
                dp.Scale = new(5);
                dp.Color = myIndex == ii || myIndex == parterner[ii] ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }
        }
    }
    #endregion


    #region 本体：基础

    [ScriptMethod(name: "本体：半场刀", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3119[12])$"])]
    public void MB_SideCleave(Event @event, ScriptAccessory accessory)
    {
        var isleft = @event.ActionId() == 31191;
        mb_isLeftCleave = isleft;
        var dp = AssignDp.drawStatic(new Vector3(100, 0, 100), float.Pi, 0, 5700, $"本体半场刀{isleft}", accessory);
        dp.Scale = new(20, 40);
        dp.Position = isleft ? new(90, 0, 80) : new(110, 0, 80);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "本体：半场刀增加阶段", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3119[12])$"], userControl: false)]
    public void MB_PhaseAdd(Event @event, ScriptAccessory accessory)
    {
        mb_sideCleaveNum++;
    }

    [ScriptMethod(name: "本体：阶段（术式记录）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31163"], userControl: false)]
    public void MB_PhaseChange_NA(Event @event, ScriptAccessory accessory)
    {
        mb_phase = mb_phase switch
        {
            MB_Phase.Opening => MB_Phase.NA1,
            MB_Phase.NA1 => MB_Phase.NA2,
            _ => MB_Phase.NA1
        };
    }

    #endregion

    #region 本体：一术士

    [ScriptMethod(name: "本体：一紫圈记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2552"], userControl: false)]
    public void MB_NaturalAlignment(Event @event, ScriptAccessory accessory)
    {
        if (mb_phase != MB_Phase.NA1) return;
        var tid = @event.TargetId();
        var tidx = accessory.Data.PartyList.IndexOf(tid);
        if (tidx == -1) return;
        mb_isNATarget[tidx] = true;

        if (mb_isNATarget.Count(x => x == true) != 2) return;
        // 若紫圈目标为DPS，则TN固定站位
        mb_NA1_isTNFixed = tidx > 3;

    }

    [ScriptMethod(name: "本体：黄圈引导", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31369"])]
    public void MB_TyrantFlare(Event @event, ScriptAccessory accessory)
    {
        for (int i = 0; i < accessory.Data.PartyList.Count(); i++)
        {
            var dp = AssignDp.drawCircle(accessory.Data.PartyList[i], 0, 3000, $"黄圈引导{i}", accessory);
            dp.Scale = new(6);
            dp.ScaleMode = ScaleMode.ByTime;
            dp.Color = accessory.Data.DefaultDangerColor.WithW(0.6f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }
    // 强制咏唱，出现分摊分散/冰火顺序图案
    [ScriptMethod(name: "本体：一分摊分散", eventType: EventTypeEnum.StatusAdd, eventCondition: ["Param:regex:^(48[02])$"])]
    public void MB_ForceStackSpread(Event @event, ScriptAccessory accessory)
    {
        if (mb_phase != MB_Phase.NA1) return;
        var isStackFirst = @event.Param() == 480;
        var myIndex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
        if (mb_isNATarget[myIndex])
            accessory.Method.TextInfo($"先【{(isStackFirst ? "避开分摊" : "分散")}】，后【{(isStackFirst ? "分散" : "避开分摊")}】", 5000, true);
        else
            accessory.Method.TextInfo($"先【{(isStackFirst ? "分摊" : "分散")}】，后【{(isStackFirst ? "分散" : "分摊")}】", 5000, true);

        // 绘制分散
        drawForceSpread(isStackFirst, mb_isNATarget, accessory);
        drawSpreadDir(isStackFirst, myIndex, accessory);
        // 绘制分摊
        drawForceStack(isStackFirst, myIndex, mb_isNATarget[myIndex], accessory);
    }
    private static void drawForceSpread(bool isStackFirst, bool[] naTargetList, ScriptAccessory accessory)
    {
        var spreadTime = isStackFirst ? 6100 : 3000;

        for (int i = 0; i < accessory.Data.PartyList.Count(); i++)
        {
            if (naTargetList[i]) continue;
            var dp = AssignDp.drawCircle(accessory.Data.PartyList[i], spreadTime, spreadTime, $"一紫圈分散{i}", accessory);
            dp.Scale = new(6);
            dp.ScaleMode = ScaleMode.ByTime;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    private static void drawForceStack(bool isStackFirst, int myIndex, bool isTarget, ScriptAccessory accessory)
    {
        var stackTime = isStackFirst ? 3000 : 6100;
        var stackOwner = myIndex < 4 ? accessory.Data.PartyList[myIndex + 4] : accessory.Data.PartyList[myIndex - 4];
        var owner_id = isTarget ? stackOwner : accessory.Data.Me;
        var dp = AssignDp.drawCircle(owner_id, stackTime, stackTime, $"一紫圈分摊{myIndex}", accessory);
        dp.Scale = new(6);
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Color = isTarget ? accessory.Data.DefaultDangerColor : accessory.Data.DefaultSafeColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    private async static void drawSpreadDir(bool isStackFirst, int myIndex, ScriptAccessory accessory)
    {
        var spreadTime = isStackFirst ? 6100 : 3000;

        if (isStackFirst)
        {
            await Task.Delay(6500);
            DebugMsg($"开始绘制分散位置", accessory);
            drawSpreadPos(mb_isLeftCleave, spreadTime - 400, myIndex, accessory);
        }
        else
            drawSpreadLine(spreadTime, myIndex, accessory);
        return;
    }

    private static void drawSpreadLine(int spreadTime, int myIndex, ScriptAccessory accessory)
    {
        Vector3[] safePos = new Vector3[8];
        safePos[0] = new Vector3(100, 0, 90);
        safePos[1] = new Vector3(110, 0, 100);
        safePos[2] = new Vector3(90, 0, 100);
        safePos[3] = new Vector3(100, 0, 100);
        safePos[4] = new Vector3(90, 0, 90);
        safePos[5] = new Vector3(110, 0, 90);
        safePos[6] = new Vector3(90, 0, 110);
        safePos[7] = new Vector3(110, 0, 110);

        for (int i = 0; i < 8; i++)
        {
            var dp = AssignDp.dirPos2Pos(new Vector3(100, 0, 100), safePos[i], spreadTime - 500, spreadTime + 500, $"分散方向{i}", accessory);
            dp.Scale = new(1.5f);
            dp.Color = myIndex == i ? posColorPlayer.V4.WithW(2f) : posColorNormal.V4;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
        }
    }

    private static void drawSpreadPos(bool isLeftCleave, int spreadTime, int myIndex, ScriptAccessory accessory)
    {
        Vector3[] safePos = new Vector3[8];
        // 右边
        safePos[0] = new Vector3(100.5f, 0, 80);
        safePos[1] = new Vector3(110, 0, 80);
        safePos[2] = new Vector3(100.5f, 0, 90);
        safePos[3] = new Vector3(100.5f, 0, 100);
        safePos[4] = new Vector3(110, 0, 90);
        safePos[5] = new Vector3(110, 0, 100);
        safePos[6] = new Vector3(110, 0, 110);
        safePos[7] = new Vector3(100.5f, 0, 110);

        if (!isLeftCleave)
        {
            for (int i = 0; i < 8; i++)
            {
                safePos[i] = DirectionCalc.FoldPointLR(safePos[i], 100);
            }
        }

        for (int i = 0; i < 8; i++)
        {
            var dp0 = AssignDp.drawStatic(safePos[i], 0, 0, spreadTime, $"分散位置{i}", accessory);
            dp0.Scale = new(1f);
            dp0.Color = myIndex == i ? posColorPlayer.V4.WithW(2f) : posColorNormal.V4;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp0);
        }

        var dp = AssignDp.dirPos(safePos[myIndex], 0, spreadTime, $"分散位置指路{myIndex}", accessory);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    // 5076.4 "Forcible Trifire/Forcible Difreeze" Ability { id: ["79BD", "79BE"], source: "Hephaistos" }
    [ScriptMethod(name: "本体：一冰火引导范围", eventType: EventTypeEnum.StatusAdd, eventCondition: ["Param:regex:^(47[68])$"])]
    public void MB_ForceFireFreeze(Event @event, ScriptAccessory accessory)
    {
        if (mb_phase != MB_Phase.NA1) return;
        var tid = @event.TargetId();
        var isFireFirst = @event.Param() == 476;
        var myIndex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);

        var dp = accessory.Data.GetDefaultDrawProperties();
        // 两人火，三组
        for (uint i = 1; i < 4; i++)
        {
            dp.Name = $"一紫圈火-{i}";
            dp.Scale = new(5);
            dp.Owner = tid;
            dp.CentreResolvePattern = PositionResolvePatternEnum.PlayerFarestOrder;
            dp.CentreOrderIndex = i;
            dp.Color = mb_isNATarget[myIndex] ? accessory.Data.DefaultDangerColor : accessory.Data.DefaultSafeColor;
            dp.Delay = isFireFirst ? 0 : 6100;
            dp.DestoryAt = isFireFirst ? 6000 : 6100;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        // 三人冰，两组
        for (uint i = 1; i < 3; i++)
        {
            dp.Name = $"一紫圈冰-{i}";
            dp.Scale = new(5);
            dp.Owner = tid;
            dp.CentreResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
            dp.CentreOrderIndex = i + 1;
            dp.Color = mb_isNATarget[myIndex] ? accessory.Data.DefaultDangerColor : accessory.Data.DefaultSafeColor;
            dp.Delay = isFireFirst ? 6100 : 0;
            dp.DestoryAt = isFireFirst ? 6100 : 6000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    [ScriptMethod(name: "本体：分身炮判断第几行", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:15079"], userControl: false)]
    public void MB_IllusionBeamLineIdx(Event @event, ScriptAccessory accessory)
    {
        var pos = JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
        if (pos.Z > 100) return;
        // 只需判断一次即可，后续判断都无影响
        mb_NA1_isLine1Safe = pos.Z < 90 ? false : true;
    }

    [ScriptMethod(name: "本体：一冰火指路提示", eventType: EventTypeEnum.StatusAdd, eventCondition: ["Param:regex:^(47[68])$"])]
    public void MB_ForceFireFreezeGuide(Event @event, ScriptAccessory accessory)
    {
        if (mb_phase != MB_Phase.NA1) return;
        var isFireFirst = @event.Param() == 476;
        var myIndex = IndexHelper.getMyIndex(accessory);
        DebugMsg($"{(mb_NA1_isLine1Safe ? "先第一行安全" : "先第二行安全")}", accessory);

        // 我是TN且TN固定，或，我是DPS但DPS固定
        var isFixed = (mb_NA1_isTNFixed && myIndex < 4) || (!mb_NA1_isTNFixed && myIndex >= 4);

        Vector3[] dxFixed = [new(-8.5f, 0, 0), new(-6.5f, 0, 0), new(6.5f, 0, 0), new(8.5f, 0, 0)];
        Vector3 destinationPoint1;
        Vector3 destinationPoint2;
        Vector3 dzFreeze = mb_NA1_isLine1Safe ? new(0, 0, -0.5f) : new(0, 0, 0.5f);
        Vector3 dzFire = mb_NA1_isLine1Safe ? new(0, 0, -9.5f) : new(0, 0, 9.5f);

        if (isFixed)
        {
            // 无脑固定组
            var biasIdx = myIndex >= 4 ? myIndex - 4 : myIndex;
            if (myIndex < 4)
            {
                if (myIndex == 0) biasIdx = 0;
                else if (myIndex == 1) biasIdx = 3;
                else if (myIndex == 2) biasIdx = 1;
                else if (myIndex == 3) biasIdx = 2;
            }
            destinationPoint1 = (Vector3)new(100, 0, 90) + dxFixed[biasIdx] + dzFreeze;
            destinationPoint2 = (Vector3)new(100, 0, 90) + dxFixed[biasIdx] - dzFreeze;
            accessory.Method.TextInfo($"无脑组，固定站位", 10000);
        }
        else if (mb_isNATarget[myIndex])
        {
            // 紫圈组
            destinationPoint1 = (Vector3)new(100, 0, 90) + dzFreeze;
            destinationPoint2 = (Vector3)new(100, 0, 90) - dzFreeze;
            accessory.Method.TextInfo($"紫圈组，场中固定站位", 10000);
        }
        else
        {
            // 动脑组
            int biasIdx;
            if (myIndex < 4)
            {
                List<uint> TN_priority = new List<uint> { 1, 2, 0, 3 };
                List<bool> TN_NATarget = new List<bool> { mb_isNATarget[0], mb_isNATarget[1], mb_isNATarget[2], mb_isNATarget[3] };

                int firstFalseIdx = TN_NATarget.IndexOf(false);
                int lastFalseIdx = TN_NATarget.LastIndexOf(false);

                if (firstFalseIdx != -1 && lastFalseIdx != -1)
                {
                    bool firstFalseHigh = TN_priority[firstFalseIdx] < TN_priority[lastFalseIdx];
                    bool isFirstFalse = myIndex == firstFalseIdx;
                    biasIdx = ((isFirstFalse && firstFalseHigh) || (!isFirstFalse && !firstFalseHigh)) ? 0 : 3;
                }
                else
                {
                    // 没有找到 false，自求多福
                    biasIdx = 3;
                }
            }
            else
            {
                // 从后往前找到第一个false，如果是我，那我就是优先级低
                int lastFalseIdx = Array.LastIndexOf(mb_isNATarget, false);
                biasIdx = myIndex == lastFalseIdx ? 3 : 0;
            }

            destinationPoint1 = isFireFirst ? (Vector3)new(100, 0, 90) + dzFire : (Vector3)new(100, 0, 90) + dxFixed[biasIdx] + dzFreeze;
            destinationPoint2 = isFireFirst ? (Vector3)new(100, 0, 90) + dxFixed[biasIdx] - dzFreeze : (Vector3)new(100, 0, 90) - dzFire;
            accessory.Method.TextInfo($"动脑组，优先级站位", 10000);
        }

        var dp = accessory.Data.GetDefaultDrawProperties();

        dp.Name = $"一冰火指路-1";
        dp.Scale = new(0.5f);
        dp.Owner = accessory.Data.Me;
        dp.TargetPosition = destinationPoint1;
        dp.ScaleMode = ScaleMode.YByDistance;
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Delay = 0;
        dp.DestoryAt = 6000;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

        dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"一冰火指路-2";
        dp.Scale = new(0.5f);
        dp.Owner = accessory.Data.Me;
        dp.TargetPosition = destinationPoint2;
        dp.ScaleMode = ScaleMode.YByDistance;
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Delay = 6100;
        dp.DestoryAt = 6100;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

    }

    [ScriptMethod(name: "本体：分身炮", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31371"])]
    public void MB_IllusionBeam(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"分身炮";
        dp.Scale = new(10, 50);
        dp.Owner = sid;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.DestoryAt = 5700;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    #endregion

    #region 本体：一运

    [ScriptMethod(name: "本体：概念支配", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31148"], userControl: false)]
    public void MB_HighConceptReady(Event @event, ScriptAccessory accessory)
    {
        mb_conceptFinNum = 0;
        mb_phase = mb_phase switch
        {
            MB_Phase.NA1 => MB_Phase.HC1,
            MB_Phase.NA2 => MB_Phase.HC2,
            _ => MB_Phase.HC1
        };
    }

    // 5118.6 "High Concept 1" Ability { id: "710A", source: "Hephaistos" } window 20,20
    // 3330 D02 = Imperfection: Alpha                                   BUFF
    // 3331 D03 = Imperfection: Beta
    // 3332 D04 = Imperfection: Gamma
    // 3333 D05 = Perfection: Alpha                                     图案
    // 3334 D06 = Perfection: Beta
    // 3335 D07 = Perfection: Gamma
    // 3336 D08 = Inconceivable (temporary after merging)               禁止合成
    // 3337 D09 = Winged Conception (alpha + beta)                      绿风
    // 3338 D0A = Aquatic Conception (alpha + gamma)                    蓝水
    // 3339 D0B = Shocking Conception (beta + gamma)                    紫雷马
    // 3340 D0C = Fiery Conception (ifrits, alpha + alpha)              伊芙利特
    // 3341 D0D = Toxic Conception (snake, beta + beta)                 双蛇
    // 3342 D0E = Growing Conception (tree together, gamma + gamma)     大树
    // 3343 D0F = Immortal Spark (feather)          不死鸟前置羽毛
    // 3344 D10 = Immortal Conception (phoenix)     不死鸟
    // 3345 D11 = Solosplice    单人分摊
    // 3346 D12 = Multisplice   双人分摊
    // 3347 D13 = Supersplice   三人分摊
    [ScriptMethod(name: "本体：一运记录Buff", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(3346|3347|3331|3330|3332)$"], userControl: false)]
    public void MB_BRC_HighConcept1(Event @event, ScriptAccessory accessory)
    {
        if (mb_phase != MB_Phase.HC1) return;
        var tid = @event.TargetId();
        var dur = @event.DurationMilliseconds();
        // if (!int.TryParse(@event["DurationMilliseconds"], out var dur)) return;
        var isLong = dur > 9000;
        switch (@event["StatusID"])
        {
            case "3346":
                mb_hc1_sid[0] = tid;
                break;
            case "3347":
                mb_hc1_sid[1] = tid;
                break;
            case "3330":
                if (isLong)
                {
                    mb_hc1_sid[3] = tid;
                }
                else
                {
                    mb_hc1_sid[2] = tid;
                }
                break;
            case "3331":
                if (isLong)
                {
                    mb_hc1_sid[5] = tid;
                }
                else
                {
                    mb_hc1_sid[4] = tid;
                }
                break;
            case "3332":
                if (isLong)
                {
                    mb_hc1_sid[7] = tid;
                }
                else
                {
                    mb_hc1_sid[6] = tid;
                }
                break;
            default:
                break;
        }
    }

    [ScriptMethod(name: "本体：一运初始站位指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:31148"])]
    public void MB_HC1_GuidePhase0(Event @event, ScriptAccessory accessory)
    {
        if (mb_phase != MB_Phase.HC1) return;
        Task.Delay(10900).ContinueWith(t =>
        {
            var myHCIndex = mb_hc1_sid.IndexOf(accessory.Data.Me);
            var dp = accessory.Data.GetDefaultDrawProperties();

            Vector3 destinationPoint;
            switch (myHCIndex)
            {
                case 0: destinationPoint = new(108, 0, 90); break;
                case 1: destinationPoint = new(100, 0, 100); break;
                case 2: destinationPoint = new(80, 0, 80); break;
                case 3: destinationPoint = new(108, 0, 90); break;
                case 4: destinationPoint = new(80, 0, 120); break;
                case 5: destinationPoint = new(100, 0, 100); break;
                case 6: destinationPoint = new(120, 0, 120); break;
                case 7: destinationPoint = new(100, 0, 100); break;
                default: destinationPoint = new(100, 0, 100); break;
            }

            dp.Name = $"一运初始指路";
            dp.Scale = new(0.5f);
            dp.Owner = accessory.Data.Me;
            dp.TargetPosition = destinationPoint;
            dp.ScaleMode = ScaleMode.YByDistance;
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Delay = 0;
            dp.DestoryAt = 6000;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        });

    }

    [ScriptMethod(name: "本体：记录塔色", eventType: EventTypeEnum.EnvControl, eventCondition: ["Id:00020001"], userControl: false)]
    public void MB_TowerColorRecord(Event @event, ScriptAccessory accessory)
    {
        if (mb_phase != MB_Phase.HC1) return;
        lock (this)
        {
            // if (!int.TryParse(@event["Index"], System.Globalization.NumberStyles.HexNumber, null, out var tower_color)) return;
            var tower_color = @event.Index();
            mb_conceptFinNum++;
            // accessory.Method.SendChat($"/e mb_conceptFinNum = {mb_conceptFinNum}");

            if (mb_conceptFinNum == 2)
            {
                if (tower_color >= 26 && tower_color <= 35)
                {
                    // mb_towerColor = "purple";
                    mb_TwoStackDestination = new(90, 0, 110);
                    mb_ThreeStackDestination = new(110, 0, 110);
                    mb_UnmergeDestination = new(90, 0, 90);
                    mb_mentionTxt = "2人→B，3人→C，拼图→A";
                    mb_joinMerge = [false, true, true];

                    mb_alphaLongFollower = mb_hc1_sid[2];
                    mb_betaLongFollower = mb_hc1_sid[0];
                    mb_gammaLongFollower = mb_hc1_sid[1];
                }
                else if (tower_color >= 36 && tower_color <= 45)
                {
                    // mb_towerColor = "blue";
                    mb_TwoStackDestination = new(90, 0, 90);
                    mb_ThreeStackDestination = new(110, 0, 110);
                    mb_UnmergeDestination = new(90, 0, 110);
                    mb_mentionTxt = "2人→A，3人→C，拼图→B";
                    mb_joinMerge = [true, false, true];

                    mb_alphaLongFollower = mb_hc1_sid[0];
                    mb_betaLongFollower = mb_hc1_sid[4];
                    mb_gammaLongFollower = mb_hc1_sid[1];
                }
                else if (tower_color >= 46 && tower_color <= 55)
                {
                    // mb_towerColor = "green";
                    mb_TwoStackDestination = new(90, 0, 90);
                    mb_ThreeStackDestination = new(90, 0, 110);
                    mb_UnmergeDestination = new(110, 0, 110);
                    mb_mentionTxt = "2人→A，3人→B，拼图→C";
                    mb_joinMerge = [true, true, false];

                    mb_alphaLongFollower = mb_hc1_sid[0];
                    mb_betaLongFollower = mb_hc1_sid[1];
                    mb_gammaLongFollower = mb_hc1_sid[6];
                }
                else return;

                if (HC1_ChatGuidance)
                {
                    accessory.Method.SendChat($"/p {mb_mentionTxt} <se.1>");
                }
                else
                {
                    accessory.Method.SendChat($"/e {mb_mentionTxt}");
                }

            }
            else if (mb_conceptFinNum == 6)
            {
                if (tower_color >= 26 && tower_color <= 35)
                {
                    // mb_towerColor = "purple";
                    mb_joinMerge = [false, true, true];
                }
                else if (tower_color >= 36 && tower_color <= 45)
                {
                    // mb_towerColor = "blue";
                    mb_joinMerge = [true, false, true];
                }
                else if (tower_color >= 46 && tower_color <= 55)
                {
                    // mb_towerColor = "green";
                    mb_joinMerge = [true, true, false];
                }
                else return;
            }
        }
    }

    [ScriptMethod(name: "本体：一运第一次合成指路", eventType: EventTypeEnum.EnvControl, eventCondition: ["Id:00020001"])]
    public void MB_HC1_GuidePhase1(Event @event, ScriptAccessory accessory)
    {
        if (mb_phase != MB_Phase.HC1) return;
        Task.Delay(50).ContinueWith(t =>
        {
            if (mb_sideCleaveNum == 1 && mb_conceptFinNum == 2)
            {
                var myHCIndex = mb_hc1_sid.IndexOf(accessory.Data.Me);

                bool shouldJoinMerge = (myHCIndex == 2 && mb_joinMerge[0]) || (myHCIndex == 4 && mb_joinMerge[1]) || (myHCIndex == 6 && mb_joinMerge[2]);
                bool shouldAvoidMerge = (myHCIndex == 2 && !mb_joinMerge[0]) || (myHCIndex == 4 && !mb_joinMerge[1]) || (myHCIndex == 6 && !mb_joinMerge[2]);

                var dp0 = accessory.Data.GetDefaultDrawProperties();
                if (shouldJoinMerge)
                {
                    dp0.Name = "参与合成指路";
                    dp0.Scale = new(0.5f);
                    dp0.Owner = accessory.Data.Me;
                    dp0.TargetPosition = new(100, 0, 100);
                    dp0.ScaleMode = ScaleMode.YByDistance;
                    dp0.Color = accessory.Data.DefaultSafeColor;
                    dp0.Delay = 0;
                    dp0.DestoryAt = 6000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);

                    dp0 = accessory.Data.GetDefaultDrawProperties();
                    dp0.Name = "参与合成区域";
                    dp0.Scale = new(5);
                    dp0.Position = new(100, 0, 100);
                    dp0.Color = accessory.Data.DefaultSafeColor;
                    dp0.Delay = 0;
                    dp0.DestoryAt = 7000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp0);
                    accessory.Method.TextInfo($"参与合成", 5000);

                }
                else if (shouldAvoidMerge)
                {
                    dp0 = accessory.Data.GetDefaultDrawProperties();
                    dp0.Name = "避开合成区域";
                    dp0.Scale = new(5);
                    dp0.Position = new(100, 0, 100);
                    dp0.Color = accessory.Data.DefaultDangerColor;
                    dp0.Delay = 0;
                    dp0.DestoryAt = 7000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp0);
                    accessory.Method.TextInfo($"避开合成", 5000, true);
                }
            }
        });
    }

    [ScriptMethod(name: "本体：一运合成后站位指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3119[12])$"])]
    public void MB_HC1_GuidePhase2(Event @event, ScriptAccessory accessory)
    {
        if (mb_phase != MB_Phase.HC1) return;
        if (mb_conceptFinNum != 2) return;
        var myHCIndex = mb_hc1_sid.IndexOf(accessory.Data.Me);
        bool joinedMerge = false;
        Vector3 safeDestination = new(110, 0, 90);

        var dp = accessory.Data.GetDefaultDrawProperties();
        switch (myHCIndex)
        {
            case 0:
                dp.TargetPosition = mb_TwoStackDestination;
                break;
            case 1:
                dp.TargetPosition = mb_ThreeStackDestination;
                break;
            case 2:
                joinedMerge = mb_joinMerge[0];
                dp.TargetPosition = joinedMerge ? safeDestination : mb_UnmergeDestination;
                break;
            case 3:
                dp.TargetPosition = new(80, 0, 80);
                break;
            case 4:
                joinedMerge = mb_joinMerge[1];
                dp.TargetPosition = joinedMerge ? safeDestination : mb_UnmergeDestination;
                break;
            case 5:
                dp.TargetPosition = new(80, 0, 120);
                break;
            case 6:
                joinedMerge = mb_joinMerge[2];
                dp.TargetPosition = joinedMerge ? safeDestination : mb_UnmergeDestination;
                break;
            case 7:
                dp.TargetPosition = new(120, 0, 120);
                break;
        }
        dp.Name = $"一运第二阶段指路";
        dp.Scale = new(0.5f);
        dp.Owner = accessory.Data.Me;
        dp.ScaleMode = ScaleMode.YByDistance;
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Delay = 3500;
        dp.DestoryAt = 8000;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

        if (joinedMerge)
        {
            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"一运第二阶段安全区";
            dp.Scale = new(8);
            dp.Position = safeDestination;
            dp.Color = accessory.Data.DefaultSafeColor;
            dp.Delay = 3500;
            dp.DestoryAt = 8000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    [ScriptMethod(name: "本体：一运第二次合成指路", eventType: EventTypeEnum.EnvControl, eventCondition: ["Id:00020001"])]
    public void MB_HC1_GuidePhase3(Event @event, ScriptAccessory accessory)
    {
        if (mb_phase != MB_Phase.HC1) return;
        Task.Delay(50).ContinueWith(t =>
        {
            if (mb_sideCleaveNum == 2 && mb_conceptFinNum == 6)
            {
                var myHCIndex = mb_hc1_sid.IndexOf(accessory.Data.Me);

                bool shouldJoinMergeUp = (myHCIndex == 3 && mb_joinMerge[0]) ||
                                         (accessory.Data.Me == mb_betaLongFollower && mb_joinMerge[1]) ||
                                         (accessory.Data.Me == mb_gammaLongFollower && mb_joinMerge[2]);

                bool shouldJoinMergeDown = (accessory.Data.Me == mb_alphaLongFollower && mb_joinMerge[0]) ||
                                           (myHCIndex == 5 && mb_joinMerge[1]) ||
                                           (myHCIndex == 7 && mb_joinMerge[2]);

                var dp = accessory.Data.GetDefaultDrawProperties();
                if (shouldJoinMergeUp)
                {
                    dp.Name = $"参与上半场合成指路";
                    dp.Scale = new(0.5f);
                    dp.Owner = accessory.Data.Me;
                    dp.TargetPosition = new(100, 0, 90);
                    dp.ScaleMode = ScaleMode.YByDistance;
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.Delay = 0;
                    dp.DestoryAt = 6000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                    dp.Name = $"参与上半场合成区域";
                    dp.Scale = new(5);
                    dp.Owner = accessory.Data.Me;
                    dp.Position = new(100, 0, 90);
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.Delay = 0;
                    dp.DestoryAt = 6000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                    accessory.Method.TextInfo($"参与上半场合成", 6000);

                }
                else if (shouldJoinMergeDown)
                {
                    dp.Name = $"参与下半场合成指路";
                    dp.Scale = new(0.5f);
                    dp.Owner = accessory.Data.Me;
                    dp.TargetPosition = new(100, 0, 110);
                    dp.ScaleMode = ScaleMode.YByDistance;
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.Delay = 0;
                    dp.DestoryAt = 6000;
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

                    dp.Name = $"参与下半场合成区域";
                    dp.Scale = new(5);
                    dp.Owner = accessory.Data.Me;
                    dp.Position = new(100, 0, 110);
                    dp.Color = accessory.Data.DefaultSafeColor;
                    dp.Delay = 0;
                    dp.DestoryAt = 6000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                    accessory.Method.TextInfo($"参与下半场合成", 6000);
                }
                else
                {
                    dp.Name = $"避开上半场合成区域";
                    dp.Scale = new(5);
                    dp.Owner = accessory.Data.Me;
                    dp.Position = new(100, 0, 90);
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.Delay = 0;
                    dp.DestoryAt = 6000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

                    dp.Name = $"避开下半场合成区域";
                    dp.Scale = new(5);
                    dp.Owner = accessory.Data.Me;
                    dp.Position = new(100, 0, 110);
                    dp.Color = accessory.Data.DefaultDangerColor;
                    dp.Delay = 0;
                    dp.DestoryAt = 6000;
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

                    accessory.Method.TextInfo($"避开合成", 6000, true);
                }
            }
        });
    }

    #endregion

    #region 本体：万象

    [ScriptMethod(name: "本体：万象灰烬指路与分散预警", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:30189"])]
    public void MB_LimitlessDesolation(Event @event, ScriptAccessory accessory)
    {
        mb_phase = MB_Phase.LD;
        var dp = accessory.Data.GetDefaultDrawProperties();
        for (int i = 0; i < accessory.Data.PartyList.Count(); i++)
        {
            dp.Name = $"万象分散-{i}";
            dp.Scale = new(6);
            dp.Owner = accessory.Data.PartyList[i];
            dp.Color = accessory.Data.DefaultDangerColor.WithW(0.6f);
            dp.Delay = 0;
            dp.DestoryAt = 20000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        var myIndex = IndexHelper.getMyIndex(accessory);
        drawLDSpreadDir(5000, myIndex, accessory);
    }

    private static void drawLDSpreadDir(int castTime, int myIndex, ScriptAccessory accessory)
    {
        Vector3[] safePos = new Vector3[8];
        safePos[0] = new Vector3(90, 0, 80);
        safePos[1] = new Vector3(80, 0, 90);
        safePos[2] = new Vector3(80, 0, 100);
        safePos[3] = new Vector3(90, 0, 110);
        safePos[4] = DirectionCalc.FoldPointLR(safePos[0], 100);
        safePos[5] = DirectionCalc.FoldPointLR(safePos[1], 100);
        safePos[6] = DirectionCalc.FoldPointLR(safePos[2], 100);
        safePos[7] = DirectionCalc.FoldPointLR(safePos[3], 100);

        for (int i = 0; i < 8; i++)
        {
            var dp0 = AssignDp.drawStatic(safePos[i], 0, 0, castTime, $"分散位置{i}", accessory);
            dp0.Scale = new(1f);
            dp0.Color = myIndex == i ? posColorPlayer.V4.WithW(2f) : posColorNormal.V4;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp0);
        }

        var dp = AssignDp.dirPos(safePos[myIndex], 0, castTime, $"分散位置指路{myIndex}", accessory);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    [ScriptMethod(name: "本体：万象灰烬放黄圈预警", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:30192"])]
    public void MB_TyrantsFire(Event @event, ScriptAccessory accessory)
    {
        // 检测到易伤后，绘制放黄圈预警
        var tid = @event.TargetId();
        var tidx = accessory.Data.PartyList.IndexOf(tid);
        accessory.Method.RemoveDraw($"万象分散-{tidx}");

        if (tid != accessory.Data.Me) return;

        // 31368
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"万象黄圈预警";
        dp.Scale = new(8);
        dp.ScaleMode = ScaleMode.ByTime;
        dp.Owner = tid;
        dp.Color = ColorHelper.DelayDangerColor.V4;
        dp.Delay = 0;
        dp.DestoryAt = 8000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    /// <summary>
    /// 万象踩塔数据，包含“塔是第几个出现的”，“塔相对坐标”，“哪个玩家负责踩塔”
    /// </summary>
    public class LD_Tower
    {
        public int TowerIdx { get; set; }
        public int Row { get; set; }
        public int Col { get; set; }
        public int PlayerIdx { get; set; }
        public LD_Tower(int tidx, int row, int col, int playerIdx)
        {
            TowerIdx = tidx;
            Row = row;
            Col = col;
            PlayerIdx = playerIdx;
        }
    }

    [ScriptMethod(name: "本体：万象塔记录", eventType: EventTypeEnum.EnvControl, eventCondition: ["DirectorId:800375AB", "Id:00020001", "Index:regex:^(000000(0[9ABC]|4[CDEF]|5[0145]))$"], userControl: false)]
    public void MB_LDTowerRecord(Event @event, ScriptAccessory accessory)
    {
        lock (mb_LD_towerOrder)
        {
            var idx = @event.Index();
            int Row;
            int Col;
            (Row, Col) = idx switch
            {
                0x9 => (2, 2),
                0xA => (2, 3),
                0xB => (3, 2),
                0xC => (3, 3),
                0x4C => (1, 1),
                0x4D => (1, 2),
                0x4E => (1, 3),
                0x4F => (1, 4),
                0x50 => (2, 1),
                0x51 => (2, 4),
                0x54 => (3, 1),
                0x55 => (3, 4),
                _ => (0, 0)
            };
            mb_LD_towerOrder.Add(new LD_Tower(mb_LD_towerOrder.Count(), Row, Col, -1));
            DebugMsg($"捕捉到第{mb_LD_towerOrder.Count()}座塔({Row}行, {Col}列)的生成。", accessory);
        }
    }

    [ScriptMethod(name: "本体：万象踩塔绘图", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:30192"])]
    public async void MB_LDPlayerRecord(Event @event, ScriptAccessory accessory)
    {
        var tid = @event.TargetId();
        if (@event.TargetIndex() != 1) return;
        var pidx = IndexHelper.getPlayerIdIndex(tid, accessory);
        mb_LD_playerOrder.Add(pidx);
        DebugMsg($"捕捉到易伤：{IndexHelper.getPlayerJobByIndex(pidx)}", accessory);
        // 第{mb_LD_playerOrder.Count()}个

        await Task.Delay(1000);

        lock (mb_LD_towerOrder)
        {
            var myIndex = IndexHelper.getMyIndex(accessory);
            for (int i = 0; i < mb_LD_towerOrder.Count(); i++)
            {
                if (mb_LD_towerOrder[i].PlayerIdx != -1) continue;

                var isTN = pidx <= 3;
                var isLeftTower = mb_LD_towerOrder[i].Col <= 2;

                if ((isTN && isLeftTower) || (!isTN && !isLeftTower))
                {
                    mb_LD_towerOrder[i].PlayerIdx = pidx;
                    if (myIndex == pidx)
                        drawLDDir(mb_LD_towerOrder[i], accessory);
                }
                else
                    continue;
            }
        }

    }

    private static void drawLDDir(LD_Tower tower, ScriptAccessory accessory)
    {
        // 在自身脚下出现黄圈前，将塔范围标记为危险区
        var dp_tdanger = assignDp_Tower(tower.Row, tower.Col, accessory);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp_tdanger);

        // 塔中危险区消失后，再标记为安全区
        var dp_tsafe = assignDp_Tower(tower.Row, tower.Col, accessory);
        dp_tsafe.Color = accessory.Data.DefaultSafeColor;
        dp_tsafe.Delay = 7000;
        // 故意写长消失时间，用envcontrol消除
        dp_tsafe.DestoryAt = 30000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp_tsafe);

        // 塔中危险区消失后，人物到塔指路
        var dp_tdir = assignDp_TowerDir(tower.Row, tower.Col, accessory);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp_tdir);
        return;
    }

    private static DrawPropertiesEdit assignDp_Tower(int row, int col, ScriptAccessory accessory)
    {
        Vector3 tower_center = new Vector3(75 + col * 10, 0, 75 + row * 10);
        var delay = 0;
        var destoryAt = 7000;   // 8000 - task delay的100
        var dp = AssignDp.drawStatic(tower_center, 0, delay, destoryAt, $"塔{row}{col}", accessory);
        dp.Scale = new(4);
        dp.Color = ColorHelper.colorRed.V4;
        return dp;
    }

    private static DrawPropertiesEdit assignDp_TowerDir(int row, int col, ScriptAccessory accessory)
    {
        Vector3 tower_center = new Vector3(75 + col * 10, 0, 75 + row * 10);
        var delay = 7000;
        var destoryAt = 30000;   // 8000 - task delay的100
        var dp = AssignDp.dirPos(tower_center, delay, destoryAt, $"塔指路{row}{col}", accessory);
        return dp;
    }

    [ScriptMethod(name: "本体：万象塔消失", eventType: EventTypeEnum.EnvControl, eventCondition: ["DirectorId:800375AB", "Id:00080004", "Index:regex:^(000000(0[9ABC]|4[CDEF]|5[0145]))$"], userControl: false)]
    public void MB_LDTowerRemove(Event @event, ScriptAccessory accessory)
    {
        var idx = @event.Index();
        int Row;
        int Col;
        (Row, Col) = idx switch
        {
            0x9 => (2, 2),
            0xA => (2, 3),
            0xB => (3, 2),
            0xC => (3, 3),
            0x4C => (1, 1),
            0x4D => (1, 2),
            0x4E => (1, 3),
            0x4F => (1, 4),
            0x50 => (2, 1),
            0x51 => (2, 4),
            0x54 => (3, 1),
            0x55 => (3, 4),
            _ => (0, 0)
        };
        accessory.Method.RemoveDraw($"塔指路{Row}{Col}");
        accessory.Method.RemoveDraw($"塔{Row}{Col}");
    }

    // EnvControl记录
    // 800375AB 00020001
    // Index
    // 00000005 (2,2) 石头      // 00000009 (2,2) 塔
    // 00000006 (2,3) 石头      // 0000000A (2,3) 塔
    // 00000007 (3,2) 石头      // 0000000B (3,2) 塔
    // 00000008 (3,3) 石头      // 0000000C (3,3) 塔

    // 00000046 (1,1) 石头      // 0000004C (1,1) 塔
    // 00000047 (1,2) 石头      // 0000004D (1,2) 塔
    // 00000048 (1,3) 石头      // 0000004E (1,3) 塔
    // 00000049 (1,4) 石头      // 0000004F (1,4) 塔
    // 0000004A (2,1) 石头      // 00000050 (2,1) 塔
    // 0000004B (2,4) 石头      // 00000051 (2,4) 塔

    // 00000052 (3,1) 石头      // 00000054 (3,1) 塔
    // 00000053 (3,4) 石头      // 00000055 (3,4) 塔

    // State
    // 00020001 生成
    // 00200010 踩到
    // 00400001 出去
    // 00080004 消失

    #endregion
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

    public static uint ActionId(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["ActionId"]);
    }

    public static uint SourceId(this Event @event)
    {
        return ParseHexId(@event["SourceId"], out var id) ? id : 0;
    }

    public static uint SourceDataId(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["SourceDataId"]);
    }

    public static uint TargetId(this Event @event)
    {
        return ParseHexId(@event["TargetId"], out var id) ? id : 0;
    }

    public static uint TargetIndex(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["TargetIndex"]);
    }

    public static Vector3 SourcePosition(this Event @event)
    {
        return JsonConvert.DeserializeObject<Vector3>(@event["SourcePosition"]);
    }

    public static Vector3 TargetPosition(this Event @event)
    {
        return JsonConvert.DeserializeObject<Vector3>(@event["TargetPosition"]);
    }

    public static Vector3 EffectPosition(this Event @event)
    {
        return JsonConvert.DeserializeObject<Vector3>(@event["EffectPosition"]);
    }

    public static float SourceRotation(this Event @event)
    {
        return JsonConvert.DeserializeObject<float>(@event["SourceRotation"]);
    }

    public static float TargetRotation(this Event @event)
    {
        return JsonConvert.DeserializeObject<float>(@event["TargetRotation"]);
    }

    public static string SourceName(this Event @event)
    {
        return @event["SourceName"];
    }

    public static string TargetName(this Event @event)
    {
        return @event["TargetName"];
    }

    public static uint DurationMilliseconds(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["DurationMilliseconds"]);
    }

    public static uint Index(this Event @event)
    {
        return ParseHexId(@event["Index"], out var id) ? id : 0;
    }

    public static uint State(this Event @event)
    {
        return ParseHexId(@event["State"], out var id) ? id : 0;
    }

    public static uint DirectorId(this Event @event)
    {
        return ParseHexId(@event["DirectorId"], out var id) ? id : 0;
    }

    public static uint StatusID(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["StatusID"]);
    }

    public static uint StackCount(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["StackCount"]);
    }

    public static uint Param(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["Param"]);
    }
}

public static class DirectionCalc
{
    // 以北为0建立list
    // InnGame      List    Dir
    // 0            - 4     pi
    // 0.25 pi      - 3     0.75pi
    // 0.5 pi       - 2     0.5pi
    // 0.75 pi      - 1     0.25pi
    // pi           - 0     0
    // 1.25 pi      - 7     1.75pi
    // 1.5 pi       - 6     1.5pi
    // 1.75 pi      - 5     1.25pi
    // Dir = Pi - InnGame (+ 2pi)

    /// <summary>
    /// 将游戏基角度（以南为0，逆时针增加）转为逻辑基角度（以北为0，顺时针增加）
    /// </summary>
    /// <param name="radian">游戏基角度</param>
    /// <returns>逻辑基角度</returns>
    public static float BaseInnGame2DirRad(float radian)
    {
        float r = (float)Math.PI - radian;
        if (r < 0) r = (float)(r + 2 * Math.PI);
        if (r > 2 * Math.PI) r = (float)(r - 2 * Math.PI);
        return r;
    }

    /// <summary>
    /// 将逻辑基角度（以北为0，顺时针增加）转为游戏基角度（以南为0，逆时针增加）
    /// </summary>
    /// <param name="radian">逻辑基角度</param>
    /// <returns>游戏基角度</returns>
    public static float BaseDirRad2InnGame(float radian)
    {
        float r = (float)Math.PI - radian;
        if (r < Math.PI) r = (float)(r + 2 * Math.PI);
        if (r > Math.PI) r = (float)(r - 2 * Math.PI);
        return r;
    }

    /// <summary>
    /// 输入逻辑基角度，获取逻辑方位
    /// </summary>
    /// <param name="radian">逻辑基角度</param>
    /// <param name="dirs">方位总数</param>
    /// <returns>逻辑基角度对应的逻辑方位</returns>
    public static int DirRadRoundToDirs(float radian, int dirs)
    {
        var r = Math.Round(radian / (2f / dirs * Math.PI));
        if (r == dirs) r = r - dirs;
        return (int)r;
    }

    /// <summary>
    /// 输入坐标，获取正分割逻辑方位（以右上为0）
    /// </summary>
    /// <param name="point">坐标点</param>
    /// <param name="center">中心点</param>
    /// <param name="dirs">方位总数</param>
    /// <returns>该坐标点对应的逻辑方位</returns>
    public static int PositionFloorToDirs(Vector3 point, Vector3 center, int dirs)
    {
        // 正分割，0°为分界线，将360°分为dirs份
        var r = Math.Floor(dirs / 2 - dirs / 2 * Math.Atan2(point.X - center.X, point.Z - center.Z) / Math.PI) % dirs;
        return (int)r;
    }

    /// <summary>
    /// 输入坐标，获取斜分割逻辑方位（以正上为0）
    /// </summary>
    /// <param name="point">坐标点</param>
    /// <param name="center">中心点</param>
    /// <param name="dirs">方位总数</param>
    /// <returns>该坐标点对应的逻辑方位</returns>
    public static int PositionRoundToDirs(Vector3 point, Vector3 center, int dirs)
    {
        // 斜分割，0° return 0，将360°分为dirs份
        var r = Math.Round(dirs / 2 - dirs / 2 * Math.Atan2(point.X - center.X, point.Z - center.Z) / Math.PI) % dirs;
        return (int)r;
    }

    /// <summary>
    /// 将角度转为弧度
    /// </summary>
    /// <param name="angle">角度值</param>
    /// <returns>对应的弧度值</returns>
    public static float angle2Rad(float angle)
    {
        // 输入角度转为弧度
        float radian = (float)(angle * Math.PI / 180);
        return radian;
    }

    /// <summary>
    /// 以逻辑基弧度旋转某点
    /// </summary>
    /// <param name="point">待旋转点坐标</param>
    /// <param name="center">中心</param>
    /// <param name="radian">旋转弧度</param>
    /// <returns>旋转后坐标点</returns>
    public static Vector3 RotatePoint(Vector3 point, Vector3 center, float radian)
    {
        // 围绕某点顺时针旋转某弧度
        Vector2 v2 = new(point.X - center.X, point.Z - center.Z);
        var rot = MathF.PI - MathF.Atan2(v2.X, v2.Y) + radian;
        var length = v2.Length();
        return new(center.X + MathF.Sin(rot) * length, center.Y, center.Z - MathF.Cos(rot) * length);

        // 另一种方案待验证
        // var nextPos = Vector3.Transform((point - center), Matrix4x4.CreateRotationY(radian)) + center;
    }

    /// <summary>
    /// 以逻辑基角度从某中心点向外延伸
    /// </summary>
    /// <param name="center">待延伸中心点</param>
    /// <param name="radian">旋转弧度</param>
    /// <param name="length">延伸长度</param>
    /// <returns>延伸后坐标点</returns>
    public static Vector3 ExtendPoint(Vector3 center, float radian, float length)
    {
        // 令某点以某弧度延伸一定长度
        return new(center.X + MathF.Sin(radian) * length, center.Y, center.Z - MathF.Cos(radian) * length);
    }

    /// <summary>
    /// 寻找外侧某点到中心的逻辑基弧度
    /// </summary>
    /// <param name="center">中心</param>
    /// <param name="new_point">外侧点</param>
    /// <returns>外侧点到中心的逻辑基弧度</returns>
    public static float FindRadian(Vector3 center, Vector3 new_point)
    {
        // 找到某点到中心的弧度
        float radian = MathF.PI - MathF.Atan2(new_point.X - center.X, new_point.Z - center.Z);
        if (radian < 0)
            radian += 2 * MathF.PI;
        return radian;
    }

    /// <summary>
    /// 将输入点左右折叠
    /// </summary>
    /// <param name="point">待折叠点</param>
    /// <param name="centerx">中心折线坐标点</param>
    /// <returns></returns>
    public static Vector3 FoldPointLR(Vector3 point, int centerx)
    {
        Vector3 v3 = new(2 * centerx - point.X, point.Y, point.Z);
        return v3;
    }
}

public static class IndexHelper
{
    /// <summary>
    /// 输入玩家dataid，获得对应的位置index
    /// </summary>
    /// <param name="pid">玩家SourceId</param>
    /// <param name="accessory"></param>
    /// <returns>该玩家对应的位置index</returns>
    public static int getPlayerIdIndex(uint pid, ScriptAccessory accessory)
    {
        // 获得玩家 IDX
        return accessory.Data.PartyList.IndexOf(pid);
    }

    /// <summary>
    /// 获得主视角玩家对应的位置index
    /// </summary>
    /// <param name="accessory"></param>
    /// <returns>主视角玩家对应的位置index</returns>
    public static int getMyIndex(ScriptAccessory accessory)
    {
        return accessory.Data.PartyList.IndexOf(accessory.Data.Me);
    }

    /// <summary>
    /// 输入玩家dataid，获得对应的位置称呼，输出字符仅作文字输出用
    /// </summary>
    /// <param name="pid">玩家SourceId</param>
    /// <param name="accessory"></param>
    /// <returns>该玩家对应的位置称呼</returns>
    public static string getPlayerJobByID(uint pid, ScriptAccessory accessory)
    {
        // 获得玩家职能简称，无用处，仅作DEBUG输出
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

    /// <summary>
    /// 输入位置index，获得对应的位置称呼，输出字符仅作文字输出用
    /// </summary>
    /// <param name="idx">位置index</param>
    /// <returns></returns>
    public static string getPlayerJobByIndex(int idx)
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
}

public static class ColorHelper
{
    public static ScriptColor colorRed = new ScriptColor { V4 = new Vector4(1.0f, 0f, 0f, 1.0f) };
    public static ScriptColor colorPink = new ScriptColor { V4 = new Vector4(1f, 0f, 1f, 1.0f) };
    public static ScriptColor colorCyan = new ScriptColor { V4 = new Vector4(0f, 1f, 0.8f, 1.0f) };

    // 门神 龙龙凤凤延迟危险区颜色，紫色
    public static ScriptColor DelayDangerColor = new ScriptColor { V4 = new Vector4(1f, 0.2f, 1f, 1.5f) };
    // 门神 蛇蛇位置颜色
    public static ScriptColor GorgonColor = new ScriptColor { V4 = new Vector4(1f, 1f, 1f, 2f) };
}

public static class AssignDp
{
    /// <summary>
    /// 返回自己指向某目标地点的dp，可修改dp.TargetPosition, dp.Scale
    /// </summary>
    /// <param name="target_pos">指向地点</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destoryAt">绘图自出现起，经destoryAt ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit dirPos(Vector3 target_pos, int delay, int destoryAt, string name, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(0.5f);
        dp.Owner = accessory.Data.Me;
        dp.TargetPosition = target_pos;
        dp.ScaleMode = ScaleMode.YByDistance;
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        return dp;
    }

    /// <summary>
    /// 返回起始地点指向某目标地点的dp，可修改dp.Position, dp.TargetPosition, dp.Scale
    /// </summary>
    /// <param name="start_pos">起始地点</param>
    /// <param name="target_pos">指向地点</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destoryAt">绘图自出现起，经destoryAt ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit dirPos2Pos(Vector3 start_pos, Vector3 target_pos, int delay, int destoryAt, string name, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(0.5f);
        dp.Position = start_pos;
        dp.TargetPosition = target_pos;
        dp.ScaleMode = ScaleMode.YByDistance;
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        return dp;
    }

    /// <summary>
    /// 返回自己指向某目标对象的dp，可修改dp.TargetObject, dp.Scale
    /// </summary>
    /// <param name="target_id">指向目标对象</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destoryAt">绘图自出现起，经destoryAt ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit dirTarget(uint target_id, int delay, int destoryAt, string name, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(0.5f);
        dp.Owner = accessory.Data.Me;
        dp.TargetObject = target_id;
        dp.ScaleMode = ScaleMode.YByDistance;
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        return dp;
    }

    /// <summary>
    /// 返回与某对象仇恨相关的dp，可修改dp.TargetResolvePattern, dp.TargetOrderIndex, dp.Owner
    /// </summary>
    /// <param name="owner_id">起始目标id，通常为boss</param>
    /// <param name="order_idx">顺序，从1开始</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destoryAt">绘图自出现起，经destoryAt ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit drawTargetOrder(uint owner_id, uint order_idx, int delay, int destoryAt, string name, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(5, 40);
        dp.Owner = owner_id;
        dp.TargetResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
        dp.TargetOrderIndex = order_idx;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        return dp;
    }

    /// <summary>
    /// 返回与某对象距离相关的dp，可修改dp.CentreResolvePattern, dp.CentreOrderIndex, dp.Owner
    /// </summary>
    /// <param name="owner_id">起始目标id，通常为boss</param>
    /// <param name="order_idx">顺序，从1开始</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destoryAt">绘图自出现起，经destoryAt ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit drawCenterOrder(uint owner_id, uint order_idx, int delay, int destoryAt, string name, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(5);
        dp.Owner = owner_id;
        dp.CentreResolvePattern = PositionResolvePatternEnum.PlayerNearestOrder;
        dp.CentreOrderIndex = order_idx;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        return dp;
    }
    /// <summary>
    /// 返回owner与target的dp，可修改 dp.Owner, dp.TargetObject, dp.Scale
    /// </summary>
    /// <param name="owner_id">起始目标id，通常为自己</param>
    /// <param name="target_id">目标单位id</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destoryAt">绘图自出现起，经destoryAt ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit drawOwner2Target(uint owner_id, uint target_id, int delay, int destoryAt, string name, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(5, 40);
        dp.Owner = owner_id;
        dp.TargetObject = target_id;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        return dp;
    }

    /// <summary>
    /// 返回圆形dp，跟随owner，可修改 dp.Owner, dp.Scale
    /// </summary>
    /// <param name="owner_id">起始目标id，通常为自己或Boss</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destoryAt">绘图自出现起，经destoryAt ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit drawCircle(uint owner_id, int delay, int destoryAt, string name, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(5);
        dp.Owner = owner_id;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        return dp;
    }

    /// <summary>
    /// 返回静态dp，通常用于指引固定位置。可修改 dp.Position, dp.Rotation, dp.Scale
    /// </summary>
    /// <param name="center">起始位置，通常为场地中心</param>
    /// <param name="radian">旋转角度，以北为0度顺时针</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destoryAt">绘图自出现起，经destoryAt ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit drawStatic(Vector3 center, float radian, int delay, int destoryAt, string name, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(5, 20);
        dp.Position = center;
        dp.Rotation = DirectionCalc.BaseDirRad2InnGame(radian);
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        return dp;
    }
}


#endregion