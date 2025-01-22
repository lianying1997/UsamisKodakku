using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
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
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.Draw.Manager;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Timers;

namespace UsamisScript.EndWalker.p12s;

[ScriptType(name: "P12S [零式万魔殿 荒天之狱4]", territorys: [1154], guid: "563bd710-59b8-46de-bbac-f1527d7c0803", version: "0.0.0.2", author: "Usami", note: noteStr)]

public class p12s
{
    const string noteStr =
    """
    请先按需求检查并设置“用户设置”栏目。

    v0.0.0.2:
    【未完成！仅门神，到三范前对话】
    1. 一范添加“正攻/无敌改/无敌”打法，于用户设置中设置。
    2. 修复超链黑白分摊位置提示错误Bug。
    3. 其他的忘了。
    """;

    [UserSetting("Debug模式，非开发用请关闭")]
    public static bool DebugMode { get; set; } = false;

    public enum PD1StrategyEnum
    {
        正攻_Regular,
        无敌_Invuln,
        无敌改_InvulnEx,
    }

    [UserSetting("范式一解法")]
    public PD1StrategyEnum PD1Strategy { get; set; } = PD1StrategyEnum.正攻_Regular;

    [UserSetting("站位提示圈绘图-普通颜色")]
    public static ScriptColor posColorNormal { get; set; } = new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 1.0f, 1.0f) };

    [UserSetting("站位提示圈绘图-玩家站位颜色")]
    public static ScriptColor posColorPlayer { get; set; } = new ScriptColor { V4 = new Vector4(0.2f, 1.0f, 0.2f, 1.0f) };

    public enum P12S_Phase
    {
        Init,  // 初始
        Paradeigma_I,   // 一范
        Paradeigma_II,  // 二范
        Paradeigma_III,  // 三范
        SuperChain_I    // 超链I
    }

    // 竞态锁
    readonly object db_PD1_lockObject = new object();
    readonly object db_PD2_lockObject = new object();
    readonly object db_SC1_lockObject = new object();
    P12S_Phase phase = P12S_Phase.Init;
    List<bool> db_isLeftCleave = [false, false, false];     // 门神左右刀记录，是否为左刀
    bool db_PD1_isChecked = false;      // 门神一范天使位置是否已记录过
    bool db_PD1_isNorthFirst = false;   // 门神一范天使是否先刷新在北方
    bool db_PD1_drawn = false;          // 门神一范天使安全区是否已绘制完毕
    bool db_PD2_fromRightBottom = false;    // 门神二范，根据北侧天使偏置判断从左下开始还是右下开始
    List<bool> db_PD2_shouldWhiteTower = [false, false, false, false];  // 门神二范，根据天使连线颜色判断需要白塔还是黑塔
    int db_PD2_towerRecordNum = 0;      // 门神二范已记录塔buff数
    List<bool> db_PD2_isChosenTower = [false, false, false, false, false, false, false, false]; // 门神二范，对应玩家是否被点放塔
    List<bool> db_PD2_isWhiteTower = [false, false, false, false, false, false, false, false];  // 门神二范，对应玩家是否被点放白塔
    bool db_PD2_drawn = false;  // 门神二范，放塔是否已绘制
    List<uint> db_SC1_theories = [];    // 超链一元素收集
    bool db_SC1_round1_drawn = false;   // 超链一第一轮是否绘制完毕
    bool db_SC1_isOut = false;          // 超链一第一轮是否为钢铁
    bool db_SC1_isSpread = false;       // 超链一第一轮是否为分散
    int db_SC1_myBuff = -1;             // 超链一我的Buff
    List<int> db_SC1_BWTBidx = [-1, -1, -1, -1];    // Black White Tower Beam index，四个元素，黑塔-白塔-黑分摊-白分摊
    bool db_SC1_round2_drawn = false;   // 超链一第二轮是否绘制完毕
    bool db_SC1_round3_drawn = false;   // 超链一第三轮是否绘制完毕
    public void Init(ScriptAccessory accessory)
    {
        phase = P12S_Phase.Init;

        db_isLeftCleave = [false, false, false];     // 门神左右刀记录，是否为左刀
        db_PD1_isChecked = false;      // 门神一范天使位置是否已记录过
        db_PD1_isNorthFirst = false;   // 门神一范天使是否先刷新在北方
        db_PD1_drawn = false;          // 门神一范天使安全区是否已绘制完毕

        db_PD2_fromRightBottom = false;    // 门神二范，根据北侧天使偏置判断从左下开始还是右下开始
        db_PD2_shouldWhiteTower = [false, false, false, false];  // 门神二范，根据天使连线颜色判断需要白塔还是黑塔
        db_PD2_towerRecordNum = 0;      // 门神二范已记录塔buff数
        db_PD2_isChosenTower = [false, false, false, false, false, false, false, false]; // 门神二范，对应玩家是否被点放塔
        db_PD2_isWhiteTower = [false, false, false, false, false, false, false, false];  // 门神二范，对应玩家是否被点放白塔
        db_PD2_drawn = false;           // 门神二范，放塔是否已绘制

        db_SC1_theories = [];           // 超链一元素收集
        db_SC1_round1_drawn = false;     // 超链一第一轮是否绘制完毕
        db_SC1_isOut = false;           // 超链一第一轮是否为钢铁
        db_SC1_isSpread = false;        // 超链一第一轮是否为分散

        db_SC1_myBuff = -1;             // 超链一我的Buff
        db_SC1_BWTBidx = [-1, -1, -1, -1];  // Black White Tower Beam index，四个元素，黑塔-白塔-黑分摊-白分摊

        db_SC1_round2_drawn = false;    // 超链一第二轮是否绘制完毕
        db_SC1_round3_drawn = false;    // 超链一第三轮是否绘制完毕

        // DebugMsg($"/e Init Success.", accessory);
        accessory.Method.MarkClear();
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

        drawSpreadStackStdPos(new Vector3(90, 0, 90), 0, 2000, true, true, accessory);
    }

    [ScriptMethod(name: "移除绘图", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:=RMV"], userControl: false)]
    public void RemoveDraw(Event @event, ScriptAccessory accessory)
    {
        if (!DebugMode) return;
        accessory.Method.RemoveDraw(".*");
    }

    #region 门神：左右刀

    [ScriptMethod(name: "门神：左右刀记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["Param:regex:^(19|20|21|22|23|24)$"], userControl: false)]
    public void DB_SideCleaveRecord(Event @event, ScriptAccessory accessory)
    {
        var param = @event.Param();
        var paramMapping = new Dictionary<uint, (int index, bool value, string wing)>
        {
            // { key, (index, value, wing) }
            { 19, (0, true, "左上翅膀") },
            { 20, (0, false, "右上翅膀") },
            { 21, (1, true, "左中翅膀") },
            { 22, (1, false, "右中翅膀") },
            { 23, (2, true, "左下翅膀") },
            { 24, (2, false, "右下翅膀") }
        };
        if (paramMapping.ContainsKey(param))
        {
            var (index, value, wing) = paramMapping[param];
            db_isLeftCleave[index] = value;
            DebugMsg($"【门神：左右刀记录】检测到{wing}", accessory);
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
        var spos = @event.SourcePosition();
        var srot = @event.SourceRotation();
        var isTopWingFirst = @event.ActionId() == 33506 || @event.ActionId() == 33505;
        List<bool> sideCleaveLeft = new List<bool>(db_isLeftCleave);

        // 如果是上翅膀先亮，顺序不变，否则中翅膀左刀变右刀
        sideCleaveLeft[1] = isTopWingFirst ? sideCleaveLeft[1] : !sideCleaveLeft[1];
        string action1_str = sideCleaveLeft[0] == sideCleaveLeft[1] ? "停" : "穿";
        string action2_str = sideCleaveLeft[1] == sideCleaveLeft[2] ? "停" : "穿";
        // 如果是上翅膀先亮，先执行[0]与[1]的变化，后执行[1]与[2]的变化
        string action_str = isTopWingFirst ? $"先【{action1_str}】后【{action2_str}】" : $"先【{action2_str}】后【{action1_str}】";

        DebugMsg($"【门神：左右刀绘图】躲避方案为：{action_str}", accessory);

        if (isTopWingFirst)
        {
            drawSideCleave(sideCleaveLeft[0], 0, 10000, spos, srot, accessory);
            drawSideCleave(sideCleaveLeft[1], 10000, 2600, spos, srot, accessory);
            drawSideCleave(sideCleaveLeft[2], 12600, 2600, spos, srot, accessory);
        }
        else
        {
            drawSideCleave(sideCleaveLeft[0], 12600, 2600, spos, srot, accessory);
            drawSideCleave(sideCleaveLeft[1], 10000, 2600, spos, srot, accessory);
            drawSideCleave(sideCleaveLeft[2], 0, 10000, spos, srot, accessory);
        }

        accessory.Method.TextInfo(action_str, 17000, true);
    }

    public static void drawSideCleave(bool isLeft, int delay, int destoryAt, Vector3 spos, float srot, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"左右刀";
        dp.Scale = new(50);
        dp.Position = spos;
        // dp.Rotation是逆时针，此处代表逆时针转90度
        dp.Rotation = isLeft ? srot + float.Pi / 2 : srot + float.Pi / -2;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    #endregion

    #region 门神：死刑与对话

    [ScriptMethod(name: "门神：死刑", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:33532"])]
    public void DB_TankBuster(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"一仇直线死刑-1";
        dp.Scale = new(5, 40);
        dp.Owner = @event.SourceId();
        // 读条时已确定目标
        dp.TargetObject = @event.TargetId();
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = 0;
        dp.DestoryAt = 5000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"一仇直线死刑-2";
        dp.Scale = new(5, 40);
        dp.Owner = @event.SourceId();
        // 读条期间可以改变一仇
        dp.TargetResolvePattern = PositionResolvePatternEnum.OwnerTarget;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.DestoryAt = 8000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "门神：对话绘图", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3353[45])$"])]
    public void DB_Dialogos(Event @event, ScriptAccessory accessory)
    {

        var sid = @event.SourceId();
        int MyIndex = IndexHelper.getMyIndex(accessory);

        string action_str;
        switch (MyIndex)
        {
            case 0:
                action_str = "【目标圈外】远引导";
                break;
            case 1:
                action_str = "【中间】近引导";
                break;
            default:
                action_str = "【目标圈间】避开近远";
                break;
        }
        accessory.Method.TextInfo(action_str, 6200, true);

        var isMT = MyIndex == 0;
        var isST = MyIndex == 1;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"对话-近";
        dp.Owner = sid;
        dp.Color = isST ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
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
        dp.Color = isMT ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
        dp.CentreResolvePattern = PositionResolvePatternEnum.PlayerFarestOrder;
        dp.CentreOrderIndex = 1u;
        dp.Delay = 0;
        dp.DestoryAt = 6200;
        dp.Scale = new(6);
        dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    #endregion

    #region 门神：一范

    [ScriptMethod(name: "门神：范式阶段记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:33517"], userControl: false)]
    public void DB_PhaseRecord(Event @event, ScriptAccessory accessory)
    {
        switch (phase)
        {
            case P12S_Phase.Init:
                phase = P12S_Phase.Paradeigma_I;
                break;
            case P12S_Phase.Paradeigma_I:
                phase = P12S_Phase.Paradeigma_II;
                break;
            case P12S_Phase.Paradeigma_II:
                phase = P12S_Phase.Paradeigma_III;
                break;
            default:
                phase = P12S_Phase.Init;
                break;
        }
    }

    [ScriptMethod(name: "门神：一范天使位置记录", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:16172"], userControl: false)]
    public void DB_Paradeigma_I_PositionRecord(Event @event, ScriptAccessory accessory)
    {
        lock (db_PD1_lockObject)
        {
            // 只检验一次，如果 Z < 100 则在北
            if (phase != P12S_Phase.Paradeigma_I || db_PD1_isChecked) return;
            var spos = @event.SourcePosition();
            db_PD1_isNorthFirst = spos.Z < 100;
            db_PD1_isChecked = true;
            DebugMsg($"【门神：一范天使位置记录】一范天使在 {(db_PD1_isNorthFirst ? "北" : "南")}", accessory);
        }
    }

    [ScriptMethod(name: "门神：一范天使站位点绘图", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:16172"])]
    public void DB_Paradeigma_I_Waymark(Event @event, ScriptAccessory accessory)
    {
        Task.Delay(100).ContinueWith(t =>
        {
            lock (db_PD1_lockObject)
            {
                if (db_PD1_drawn) return;
                if (!db_PD1_isChecked) return;

                if (PD1Strategy == PD1StrategyEnum.正攻_Regular)
                {
                    int MyIndex = IndexHelper.getMyIndex(accessory);
                    var dp = accessory.Data.GetDefaultDrawProperties();
                    // 偶数Idx为第一轮引导(MTH1D1D3)
                    bool isFirstRound = MyIndex % 2 == 0;
                    int MyPos = MyIndex / 2;

                    if (isFirstRound)
                    {
                        drawPD1Spread(db_PD1_isNorthFirst, MyPos, 1000, 10000, accessory);
                        drawPD1Safe(db_PD1_isNorthFirst, 11000, 5000, accessory);
                    }
                    else
                    {
                        drawPD1Safe(db_PD1_isNorthFirst, 1000, 10000, accessory);
                        drawPD1Spread(db_PD1_isNorthFirst, MyPos, 11000, 5000, accessory);
                    }
                }

                else if (PD1Strategy == PD1StrategyEnum.无敌_Invuln)
                {
                    int MyIndex = IndexHelper.getMyIndex(accessory);
                    var dp = accessory.Data.GetDefaultDrawProperties();

                    switch (MyIndex)
                    {
                        case 0:
                        case 1:
                            // 直接靠近开无敌
                            drawPD1Spread(db_PD1_isNorthFirst, 0, 1000, 15000, accessory);
                            break;
                        default:
                            // 直接人群
                            drawPD1Safe(db_PD1_isNorthFirst, 1000, 15000, accessory);
                            break;
                    }
                }

                else if (PD1Strategy == PD1StrategyEnum.无敌改_InvulnEx)
                {
                    int MyIndex = IndexHelper.getMyIndex(accessory);
                    var dp = accessory.Data.GetDefaultDrawProperties();

                    switch (MyIndex)
                    {
                        case 0:
                            // MT 先远离后靠近开无敌
                            drawPD1Spread(db_PD1_isNorthFirst, 2, 1000, 10000, accessory);
                            drawPD1Spread(db_PD1_isNorthFirst, 0, 11000, 5000, accessory);
                            break;
                        case 1:
                            // ST 直接靠近开无敌
                            drawPD1Spread(db_PD1_isNorthFirst, 0, 1000, 15000, accessory);
                            break;
                        case 6:
                            // D3 先靠近后回人群
                            drawPD1Spread(db_PD1_isNorthFirst, 1, 1000, 10000, accessory);
                            drawPD1Safe(db_PD1_isNorthFirst, 11000, 5000, accessory);
                            break;
                        default:
                            drawPD1Safe(db_PD1_isNorthFirst, 1000, 15000, accessory);
                            break;
                    }
                }

                db_PD1_drawn = true;
            }
        });
    }

    public static void drawPD1Spread(bool isNorthSpread, int MyPos, int delay, int destoryAt, ScriptAccessory accessory)
    {
        Vector3[,] pos = new Vector3[2, 5];
        pos[0, 0] = new(100, 0, 95);    // 一范引导位：北2 / MTST
        pos[0, 1] = new(100, 0, 100);   // 一范引导位：北1 / H1H2
        pos[0, 2] = new(100, 0, 90);    // 一范引导位：北3 / D1D2
        pos[0, 3] = new(100, 0, 85);    // 一范引导位：北4 / D3D4
        pos[0, 4] = new(100, 0, 109);   // 一范集合位：南

        pos[1, 0] = new(100, 0, 105);   // 一范引导位：南2 / MTST
        pos[1, 1] = new(100, 0, 100);   // 一范引导位：南1 / H1H2
        pos[1, 2] = new(100, 0, 110);   // 一范引导位：南3 / D1D2
        pos[1, 3] = new(100, 0, 115);   // 一范引导位：南4 / D3D4
        pos[1, 4] = new(100, 0, 91);    // 一范集合位：北

        var dp = accessory.Data.GetDefaultDrawProperties();
        for (int i = 0; i < 4; i++)
        {
            dp.Name = $"一范引导站位-{i}";
            dp.Scale = new(1.5f);
            // 如果北引导，画4北
            dp.Position = pos[isNorthSpread ? 0 : 1, i];
            dp.Color = (i == MyPos) ? posColorPlayer.V4.WithW(2f) : posColorNormal.V4;
            dp.Delay = delay;
            dp.DestoryAt = destoryAt;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"一范分散站位指路";
        dp.Owner = accessory.Data.Me;
        dp.TargetPosition = pos[isNorthSpread ? 0 : 1, MyPos];
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Scale = new(1f);
        dp.ScaleMode = ScaleMode.YByDistance;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    public static void drawPD1Safe(bool isNorthSpread, int delay, int destoryAt, ScriptAccessory accessory)
    {
        Vector3[,] pos = new Vector3[2, 5];
        pos[0, 0] = new(100, 0, 95);    // 一范引导位：北2 / MTST
        pos[0, 1] = new(100, 0, 100);   // 一范引导位：北1 / H1H2
        pos[0, 2] = new(100, 0, 90);    // 一范引导位：北3 / D1D2
        pos[0, 3] = new(100, 0, 85);    // 一范引导位：北4 / D3D4
        pos[0, 4] = new(100, 0, 109);   // 一范集合位：南

        pos[1, 0] = new(100, 0, 105);   // 一范引导位：南2 / MTST
        pos[1, 1] = new(100, 0, 100);   // 一范引导位：南1 / H1H2
        pos[1, 2] = new(100, 0, 110);   // 一范引导位：南3 / D1D2
        pos[1, 3] = new(100, 0, 115);   // 一范引导位：南4 / D3D4
        pos[1, 4] = new(100, 0, 91);    // 一范集合位：北

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"一范安全站位";
        dp.Scale = new(1.5f);
        // 如果北引导，安全于南
        dp.Position = pos[isNorthSpread ? 0 : 1, 4];
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"一范安全站位指路";
        dp.Owner = accessory.Data.Me;
        dp.TargetPosition = pos[isNorthSpread ? 0 : 1, 4];
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Scale = new(1f);
        dp.ScaleMode = ScaleMode.YByDistance;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }
    #endregion
    #region 门神：二范

    [ScriptMethod(name: "门神：二范小怪连线记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3352[12])$"], userControl: false)]
    public void DB_Paradeigma_II_LineRecord(Event @event, ScriptAccessory accessory)
    {
        // 如果是连黑线（33522），则需要白塔
        bool shouldWhiteTower = @event.ActionId() == 33522;
        Vector3 spos = @event.SourcePosition();
        string log = "";

        // 先获得北侧天使
        // 北0，东1，南2，西3
        if (spos.Z < 80)
        {
            db_PD2_shouldWhiteTower[0] = shouldWhiteTower;
            log += $"北天使{(shouldWhiteTower ? "黑" : "白")}线";

            if (spos.X > 100)
                db_PD2_fromRightBottom = true;  // 如果北侧天使偏右，放塔判断从右下开始

            log += $"{(db_PD2_fromRightBottom ? "偏右" : "偏左")}。";
        }
        else
        {
            int index = (spos.X > 120) ? 1 : (spos.Z > 120) ? 2 : 3;
            db_PD2_shouldWhiteTower[index] = shouldWhiteTower;
            log += $"{(index == 1 ? "东" : index == 2 ? "南" : "西")}天使{(shouldWhiteTower ? "黑" : "白")}线。";
        }

        DebugMsg($"【门神：二范小怪连线记录】{log}", accessory);
    }

    [ScriptMethod(name: "门神：二范黑白塔标记记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(3579|3580)$"], userControl: false)]
    public void DB_Paradeigma_II_TowerRecord(Event @event, ScriptAccessory accessory)
    {
        lock (db_PD2_lockObject)
        {
            var tid = @event.TargetId();
            var targetIndex = IndexHelper.getPlayerIdIndex(tid, accessory);
            if (targetIndex == -1) return;

            // 被选中代表要放塔，3579 灵临刻印，放白塔
            db_PD2_isChosenTower[targetIndex] = true;
            db_PD2_isWhiteTower[targetIndex] = @event.StatusID() == 3579;
            db_PD2_towerRecordNum++;
        }
    }

    [ScriptMethod(name: "门神：小怪连线绘图", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3352[12])$"])]
    public void DB_Paradeigma_II_LineDraw(Event @event, ScriptAccessory accessory)
    {
        var tid = @event.TargetId();
        var sid = @event.SourceId();
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
        var sid = @event.SourceId();
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

                if (db_PD2_towerRecordNum != 4) return;
                db_PD2_drawn = true;

                int MyIndex = IndexHelper.getMyIndex(accessory);
                if (MyIndex == -1 || !db_PD2_isChosenTower[MyIndex]) return;

                var tposIndex = MyIndex < 4
                    ? db_PD2_shouldWhiteTower.IndexOf(db_PD2_isWhiteTower[MyIndex])
                    : db_PD2_shouldWhiteTower.LastIndexOf(db_PD2_isWhiteTower[MyIndex]);

                DebugMsg($"【门神：二范黑白塔绘图】需将塔引导给{tposIndex}号天使", accessory);

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
                dp.Scale = new(1f);
                dp.Name = $"黑白塔放置位指路";
                dp.Owner = accessory.Data.Me;
                dp.TargetPosition = tpos[tposIndex];
                dp.ScaleMode = ScaleMode.YByDistance;
                dp.Color = accessory.Data.DefaultSafeColor;
                dp.Delay = 0;
                dp.DestoryAt = 11000;
                accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            }
        });
    }
    #endregion




    #region 门神：超链I（第一步，钢月分摊分散）

    [ScriptMethod(name: "门神：进入超链阶段I", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:33498"], userControl: false)]
    public void DB_SuperChain_I_PhaseRecord(Event @event, ScriptAccessory accessory)
    {
        phase = P12S_Phase.SuperChain_I;
    }

    [ScriptMethod(name: "门神：超链I元素收集", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(16176|16177|16178|16179|16180)$"], userControl: false)]
    public void DB_SuperChain_I_TheoryCollect(Event @event, ScriptAccessory accessory)
    {
        lock (db_SC1_lockObject)
        {
            if (phase != P12S_Phase.SuperChain_I) return;
            db_SC1_theories.Add(@event.SourceId());
            DebugMsg($"捕捉到新的超链元素，当前列表内有{db_SC1_theories.Count()}个", accessory);
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
                if (phase != P12S_Phase.SuperChain_I) return;
                if (db_SC1_theories.Count() != 3) return;
                db_SC1_round1_drawn = true;
                DebugMsg($"进入超链I第一组绘图。", accessory);

                IBattleChara? destTheory = null;    // 目标点 超链元素

                for (int i = 0; i < 3; i++)
                {
                    var theoryObject = IbcHelper.GetById(db_SC1_theories[i]);
                    if (theoryObject == null) return;
                    switch (theoryObject.DataId)
                    {
                        case 16176:
                            destTheory = theoryObject;
                            break;
                        case 16177:
                            db_SC1_isOut = true;
                            break;
                        case 16178:
                            db_SC1_isOut = false;
                            break;
                        case 16179:
                            db_SC1_isSpread = true;
                            break;
                        case 16180:
                            db_SC1_isSpread = false;
                            break;
                    }
                }
                if (destTheory == null) return;

                // 画钢铁月环范围
                drawCircleDonutAtPos(destTheory.Position, 0, 11000, db_SC1_isOut, accessory);
                // 画分摊分散扇形范围
                drawSpreadStackAtPos(destTheory.Position, 7000, 4000, db_SC1_isSpread, accessory);
                // 画分摊分散站位
                drawSpreadStackStdPos(destTheory.Position, 0, 11500, db_SC1_isOut, db_SC1_isSpread, accessory);
            }
        });
    }

    private static void drawCircleDonutAtPos(Vector3 pos, int delay, int destoryAt, bool isCircle, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "超链元素";
        dp.Position = pos;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        dp.Color = accessory.Data.DefaultDangerColor;
        if (isCircle)
        {
            dp.Scale = new(7);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        else
        {
            dp.Scale = new(30);
            dp.InnerScale = new(6);
            dp.Radian = float.Pi * 2;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
        }
    }

    private static void drawSpreadStackAtPos(Vector3 pos, int delay, int destoryAt, bool isSpread, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        var MyIndex = IndexHelper.getMyIndex(accessory);
        for (int i = 0; i < (isSpread ? 8 : 4); i++)
        {
            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"四分八分{i}";
            dp.Position = pos;
            dp.TargetObject = accessory.Data.PartyList[i];
            dp.Color = (i == MyIndex || i == MyIndex - 4) ? accessory.Data.DefaultSafeColor.WithW(0.5f) : accessory.Data.DefaultDangerColor.WithW(0.5f);
            dp.Delay = delay;
            dp.DestoryAt = destoryAt;
            dp.Scale = new Vector2(40);
            dp.Radian = isSpread ? float.Pi / 180 * 30 : float.Pi / 180 * 35;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }
    }

    private static void drawSpreadStackStdPos(Vector3 pos, int delay, int destoryAt, bool isCircle, bool isSpread, ScriptAccessory accessory)
    {
        int MyIndex = IndexHelper.getMyIndex(accessory);
        float deg_init = DirectionCalc.FindRadian(pos, new Vector3(100, 0, 100));
        float deg;
        List<int> safePoint = isSpread ? [6, 1, 5, 2, 7, 0, 4, 3] : [3, 0, 1, 2, 3, 0, 1, 2];
        var dp = accessory.Data.GetDefaultDrawProperties();
        for (int i = 0; i < (isSpread ? 8 : 4); i++)
        {
            dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"定位{i}";
            dp.Scale = new(1f);
            deg = isSpread ? deg_init + float.Pi / 4 * i + float.Pi / 8 : deg_init + float.Pi / 2 * i + float.Pi / 4;
            dp.Position = DirectionCalc.ExtendPoint(pos, deg, isCircle ? 8 : 5);
            dp.Color = safePoint[MyIndex] == i ? posColorPlayer.V4.WithW(3f) : posColorNormal.V4;
            dp.Delay = delay;
            dp.DestoryAt = destoryAt;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    #endregion

    #region 门神：超链I（第二步，黑白左右分摊）

    [ScriptMethod(name: "门神：超链IBuff收集", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(3576|3577|3579|3580|3581|3582)$"], userControl: false)]
    public void DB_SuperChain_I_BuffRecord(Event @event, ScriptAccessory accessory)
    {
        if (phase != P12S_Phase.SuperChain_I) return;
        if (db_SC1_myBuff != -1 && !db_SC1_BWTBidx.Contains(-1)) return;
        var tid = @event.TargetId();
        var sid = @event.StatusID();
        var sidMapping = new Dictionary<uint, (int value, string mention, int lidx)>
        {
            { 3581, (0, "白分摊", 3) },
            { 3582, (1, "黑分摊", 2) },
            { 3579, (2, "白塔", 1) },
            { 3580, (3, "黑塔", 0) },
            // { 3578, (4, "分散", -1) },
            { 3576, (5, "初始白", -1) },
            { 3577, (6, "初始黑", -1) }
        };
        if (sidMapping.ContainsKey(sid))
        {
            var (value, mention, lidx) = sidMapping[sid];
            if (lidx != -1)
                db_SC1_BWTBidx[lidx] = IndexHelper.getPlayerIdIndex(tid, accessory);
            if (tid == accessory.Data.Me)
            {
                db_SC1_myBuff = value;
                DebugMsg($"捕捉到自身BUFF为{mention}", accessory);
            }
        }
    }

    [ScriptMethod(name: "门神：超链I第二组绘图", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(16176|16177|16178|16179|16180)$"])]
    public void DB_SuperChain_I_SecondRound(Event @event, ScriptAccessory accessory)
    {
        IBattleChara? destDonutChar = null;
        Task.Delay(100).ContinueWith(t =>
        {
            lock (db_SC1_lockObject)
            {
                if (db_SC1_theories.Count() != 7) return;
                if (phase != P12S_Phase.SuperChain_I) return;
                if (db_SC1_round2_drawn) return;
                db_SC1_round2_drawn = true;
                DebugMsg($"进入超链I第二组绘图。", accessory);

                // 取出后4个元素，两个终点，一钢铁一月环
                List<uint> SC1_SubList = db_SC1_theories.GetRange(3, 4);

                IBattleChara? dest1Theory = null;
                IBattleChara? dest2Theory = null;
                IBattleChara? inTheory = null;
                IBattleChara? outTheory = null;

                // 判断终点位置
                for (int i = 0; i < 4; i++)
                {
                    var theoryObject = IbcHelper.GetById(SC1_SubList[i]);
                    if (theoryObject == null) return;

                    switch (theoryObject.DataId)
                    {
                        case 16176:
                            if (dest1Theory == null)
                                dest1Theory = theoryObject;
                            else
                                dest2Theory = theoryObject;
                            break;
                        case 16177:
                            outTheory = theoryObject;
                            break;
                        case 16178:
                            inTheory = theoryObject;
                            break;
                    }
                }
                destDonutChar = GetDonutChar(dest1Theory, dest2Theory, inTheory, outTheory);
                if (destDonutChar == null) return;
                drawCircleDonutAtPos(destDonutChar.Position, 6500, 7000, false, accessory);
            }
        });

        Task.Delay(3500).ContinueWith(t =>
        {
            bool atLeft = db_SC1_myBuff == 2 || db_SC1_myBuff == 5 || db_SC1_myBuff == 1;
            if (destDonutChar == null) return;
            drawStackStdPos(destDonutChar.Position, 3000, 7000, atLeft, accessory);
        });
    }

    private static IBattleChara? GetDonutChar(IBattleChara? dest1Theory, IBattleChara? dest2Theory, IBattleChara? inTheory, IBattleChara? outTheory)
    {
        if (dest1Theory == null || dest2Theory == null || inTheory == null || outTheory == null) return null;
        float dest1ToDonut = new Vector2(dest1Theory.Position.X - inTheory.Position.X,
                                        dest1Theory.Position.Z - inTheory.Position.Z).Length();
        float dest2ToDonut = new Vector2(dest2Theory.Position.X - inTheory.Position.X,
                                        dest2Theory.Position.Z - inTheory.Position.Z).Length();
        float dest1ToCircle = new Vector2(dest1Theory.Position.X - outTheory.Position.X,
                                        dest1Theory.Position.Z - outTheory.Position.Z).Length();
        float dest2ToCircle = new Vector2(dest2Theory.Position.X - outTheory.Position.X,
                                        dest2Theory.Position.Z - outTheory.Position.Z).Length();

        if (dest1ToDonut < dest1ToCircle && dest2ToDonut > dest2ToCircle)
            return dest1Theory;
        else if (dest1ToDonut > dest1ToCircle && dest2ToDonut < dest2ToCircle)
            return dest2Theory;

        return null;
    }

    private static void drawStackStdPos(Vector3 pos, int delay, int destoryAt, bool atLeft, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"左右分摊{atLeft}";
        dp.TargetPosition = DirectionCalc.RotatePoint(pos, new Vector3(100, 0, 100), atLeft ? float.Pi / 180 * 17 : float.Pi / 180 * -17);
        dp.Position = new(100, 0, 100);
        dp.Color = posColorPlayer.V4;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        dp.Scale = new(5, 20);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    #endregion

    #region 门神：超链I（第三步，钢月、放塔、分散）
    [ScriptMethod(name: "门神：超链I第三组绘图", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(16176|16177|16178|16179|16180)$"])]
    public void DB_SuperChain_I_ThirdRound(Event @event, ScriptAccessory accessory)
    {
        Task.Delay(100).ContinueWith(t =>
        {
            lock (db_SC1_lockObject)
            {
                if (db_SC1_theories.Count() != 10) return;
                if (phase != P12S_Phase.SuperChain_I) return;
                // 刷出一个终点、钢铁月环
                if (db_SC1_round3_drawn) return;
                db_SC1_round3_drawn = true;

                if (DebugMode)
                    accessory.Method.SendChat($"/e [DEBUG]：进入超链I第三组绘图……");

                List<uint> SC1_SubList = db_SC1_theories.GetRange(7, 3);

                // 判断终点位置
                IBattleChara? destTheory = null;
                IBattleChara? inTheory = null;
                IBattleChara? outTheory = null;

                for (int i = 0; i < 3; i++)
                {
                    var theoryObject = IbcHelper.GetById(SC1_SubList[i]);
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

                // 画钢铁月环范围
                drawCircleDonutAtPos(destTheory.Position, 10000, 4800, !isDonutFirst, accessory);
                drawCircleDonutAtPos(destTheory.Position, 14800, 2000, isDonutFirst, accessory);
            }
        });
    }
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

public static class IbcHelper
{
    public static IBattleChara? GetById(uint id)
    {
        return (IBattleChara?)Svc.Objects.SearchByEntityId(id);
    }

    public static IBattleChara? GetMe()
    {
        return Svc.ClientState.LocalPlayer;
    }

    public static IEnumerable<IGameObject?> GetByDataId(uint dataId)
    {
        return Svc.Objects.Where(x => x.DataId == dataId);
    }

    public static uint GetCharHpcur(uint id)
    {
        // 如果null，返回0
        var hp = GetById(id)?.CurrentHp ?? 0;
        return hp;
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