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
using Lumina.Excel.GeneratedSheets;
using ECommons.SplatoonAPI;
using System.ComponentModel;
using Microsoft.VisualBasic;
using System.Reflection.Metadata.Ecma335;
using System.Drawing;

namespace UsamisScript.EndWalker.p12s;

[ScriptType(name: "P12S [零式万魔殿 荒天之狱4]", territorys: [1154], guid: "563bd710-59b8-46de-bbac-f1527d7c0803", version: "0.0.0.4", author: "Usami", note: noteStr)]

public class p12s
{
    const string noteStr =
    """
    请先按需求检查并设置“用户设置”栏目。
    门神到超链后对话，本体到一地火。

    v0.0.0.4:
    1. 修复一风火可能不绘图的BUG。

    v0.0.0.3:
    1. 本体到一地火。

    v0.0.0.2:
    1. 一范添加“正攻/无敌改/无敌”打法，于用户设置中设置。
    2. 修复超链黑白分摊位置提示错误Bug。
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
    public static ScriptColor posColorPlayer { get; set; } = new ScriptColor { V4 = new Vector4(0.0f, 1.0f, 1.0f, 1.0f) };

    [UserSetting("地火（宇宙火劫）爆炸区颜色")]
    public ScriptColor exflareColor { get; set; } = new ScriptColor { V4 = new Vector4(0f, 1.0f, 1.0f, 1.0f) };

    [UserSetting("地火（宇宙火劫）预警区颜色")]
    public ScriptColor exflareWarnColor { get; set; } = new ScriptColor { V4 = new Vector4(0f, 0.3f, 0.6f, 1.0f) };
    public enum P12S_Phase
    {
        Init,           // 初始
        Paradeigma_I,   // 一范
        Paradeigma_II,  // 二范
        Paradeigma_III, // 三范
        SuperChain_I,   // 超链I
        Gaia_I,         // 小世界一
        Classic_I,      // 一索尼
        Caloric_I,      // 一风火
        Exflare,        // 地火
        Pangenesis,     // 泛生论黑白塔
        Classic_II,     // 二索尼
        Caloric_II,     // 二风火
        Gaia_II,        // 小世界二
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
    static List<int> db_SC1_BWTBidx = [-1, -1, -1, -1];    // Black White Tower Beam index，四个元素，黑塔-白塔-黑分摊-白分摊
    bool db_SC1_round2_drawn = false;   // 超链一第二轮是否绘制完毕
    bool db_SC1_round3_drawn = false;   // 超链一第三轮是否绘制完毕
    List<bool> mb_Gaia1_dangerPlace = [false, false, false, false, false, false, false, false]; // 小世界一安全角落
    bool mb_Gaia1_dangerPlace_hasDrawn = false; // 小世界一安全角落是否绘制完毕
    List<int> mb_Classic1_playerGroup = [0, 0, 0, 0, 0, 0, 0, 0];   // 一索尼玩家分组
    List<ClassicElement> mb_Classic1_elements = new List<ClassicElement>(); // 一索尼元素
    bool mb_Classic1_etDrawn = false;  // 一索尼元素指向是否绘制完毕
    bool mb_Classic1_implodeDrawn = false;   // 一索尼元素毁灭是否绘制完毕
    bool mb_Classic1_RayDirDrawn = false;   // 一索尼射线指路是否绘制完毕
    int mb_Caloric_phase = 0; // 风火分阶段
    List<bool> mb_Caloric_isFirstTarget = [false, false, false, false, false, false, false, false]; // 一风火初始分摊目标
    List<bool> mb_Caloric_isWind = [false, false, false, false, false, false, false, false]; // 一风火buff
    List<int> mb_Caloric_WindPriority = [0, 0, 0, 0, 0, 0, 0, 0]; // 一风火风优先级
    List<int> mb_Caloric_FirePriority = [0, 0, 0, 0, 0, 0, 0, 0]; // 一风火火优先级
    bool mb_Caloric_ParnterStackDirDrawn = false;   // 一风火四组分摊指路是否绘制完毕
    bool mb_Caloric_ParnterStackDrawn = false;      // 一风火四组分摊范围是否绘制完毕
    bool mb_Caloric_SecondParnterStackDirDrawn = false; // 一风火二次分摊指路是否绘制完毕
    bool mb_Caloric_SecondParnterStackDrawn = false; // 一风火二次分摊范围是否绘制完毕
    bool mb_Caloric_SecondWindDonutDrawn = false;    // 一风火环风是否绘制完毕
    List<bool> mb_Exflare_FlarePos = [false, false, false, false]; // 地火核爆区
    bool mb_Exflare_DirDrawn = false;   // 地火指路是否绘制完毕
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

        mb_Gaia1_dangerPlace = [false, false, false, false, false, false, false, false]; // 小世界一安全角落
        mb_Gaia1_dangerPlace_hasDrawn = false;  // 小世界一安全角落是否绘制完毕

        mb_Classic1_playerGroup = [0, 0, 0, 0, 0, 0, 0, 0];   // 一索尼玩家分组
        mb_Classic1_elements = new List<ClassicElement>(); // 一索尼元素
        mb_Classic1_etDrawn = false;  // 一索尼元素指向是否绘制完毕
        mb_Classic1_implodeDrawn = false;   // 一索尼元素毁灭是否绘制完毕
        mb_Classic1_RayDirDrawn = false;   // 一索尼射线指路是否绘制完毕

        mb_Caloric_phase = 0; // 风火分阶段
        mb_Caloric_isFirstTarget = [false, false, false, false, false, false, false, false]; // 一风火初始分摊目标
        mb_Caloric_isWind = [false, false, false, false, false, false, false, false]; // 一风火buff
        mb_Caloric_WindPriority = [0, 0, 0, 0, 0, 0, 0, 0]; // 一风火风优先级
        mb_Caloric_FirePriority = [0, 0, 0, 0, 0, 0, 0, 0]; // 一风火火优先级
        mb_Caloric_ParnterStackDirDrawn = false;   // 一风火四组分摊指路是否绘制完毕
        mb_Caloric_ParnterStackDrawn = false;      // 一风火四组分摊范围是否绘制完毕
        mb_Caloric_SecondParnterStackDirDrawn = false; // 一风火二次分摊指路是否绘制完毕
        mb_Caloric_SecondParnterStackDrawn = false; // 一风火二次分摊范围是否绘制完毕
        mb_Caloric_SecondWindDonutDrawn = false;    // 一风火环风是否绘制完毕

        mb_Exflare_FlarePos = [false, false, false, false]; // 地火核爆区
        mb_Exflare_DirDrawn = false;   // 地火指路是否绘制完毕

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
        DebugMsg($"获得玩家发送的消息：{msg}", accessory);

        drawVerticalSafetyField(accessory);
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
        List<int> safePoint = isSpread ? [6, 1, 5, 2, 7, 0, 4, 3] : [3, 0, 2, 1, 3, 0, 2, 1];
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

                switch (db_SC1_myBuff)
                {
                    case 0:
                        // 白分摊，踩黑右塔
                        // 此处画图逻辑，找到放塔人，先danger，后safe
                        drawTowerCircleOnPlayer(db_SC1_BWTBidx[0], false, accessory);
                        break;
                    case 1:
                        // 黑分摊，踩白左塔
                        drawTowerCircleOnPlayer(db_SC1_BWTBidx[1], false, accessory);
                        break;
                    case 2:
                        // 白塔，左侧放塔
                        drawTowerCircleOnPlayer(db_SC1_BWTBidx[1], true, accessory);
                        drawSC1TowerDir(destTheory.Position, 2, accessory);
                        break;
                    case 3:
                        // 黑塔，右侧放塔
                        drawTowerCircleOnPlayer(db_SC1_BWTBidx[0], true, accessory);
                        drawSC1TowerDir(destTheory.Position, 3, accessory);
                        break;
                    case 5:
                    case 6:
                        // 初始白黑，分散
                        drawSC1SpreadDir(destTheory.Position, accessory);
                        drawSC1SpreadCircle(accessory);
                        break;
                    default:
                        break;
                }
            }
        });
    }
    private static void drawTowerCircleOnPlayer(int playerIdx, bool isSafe, ScriptAccessory accessory)
    {
        var dp = AssignDp.drawCircle(accessory.Data.PartyList[playerIdx], 14300, 3000, $"放塔跟随{playerIdx}", accessory);
        dp.Scale = new(3);
        dp.Color = isSafe ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    private static void drawSC1SpreadDir(Vector3 dest, ScriptAccessory accessory)
    {
        var angle_toBoss = DirectionCalc.rad2Angle(DirectionCalc.FindRadian(dest, new(100, 0, 100)));
        List<float> angle_spread = new List<float> {
            DirectionCalc.angle2Rad(angle_toBoss - 20),
            DirectionCalc.angle2Rad(angle_toBoss + 20),
            DirectionCalc.angle2Rad(angle_toBoss - 160),
            DirectionCalc.angle2Rad(angle_toBoss + 160),
        };
        var myIndex = IndexHelper.getMyIndex(accessory);

        for (int i = 0; i < 4; i++)
        {
            DebugMsg($"{myIndex} vs {i}", accessory);
            var tpos = DirectionCalc.ExtendPoint(dest, angle_spread[i], 15);
            var dp = AssignDp.dirPos2Pos(dest, tpos, 14800, 5000, $"分散{i}", accessory);
            dp.Color = ((myIndex == i) || (myIndex == i + 4)) ? posColorPlayer.V4.WithW(3f) : posColorNormal.V4;
            dp.Scale = new(2f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
        }
    }

    private static void drawSC1TowerDir(Vector3 dest, int myBuff, ScriptAccessory accessory)
    {
        // myBuff 2 白塔 左
        // myBuff 3 黑塔 右
        var angle_toBoss = DirectionCalc.rad2Angle(DirectionCalc.FindRadian(dest, new(100, 0, 100)));

        List<float> angle_tower = new List<float> {
            DirectionCalc.angle2Rad(angle_toBoss - 52),
            DirectionCalc.angle2Rad(angle_toBoss + 52),
        };

        var tpos = DirectionCalc.ExtendPoint(dest, angle_tower[myBuff - 2], 12);
        var dp = AssignDp.dirPos2Pos(dest, tpos, 14300, 5000, $"放塔{myBuff}指路", accessory);
        dp.Scale = new(2f);
        dp.Color = posColorPlayer.V4.WithW(3f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
    }

    private static void drawSC1SpreadCircle(ScriptAccessory accessory)
    {
        // 不在db_SC1_BWTBidx里的就是分散的
        for (int i = 0; i < 8; i++)
        {
            if (db_SC1_BWTBidx.Contains(i)) continue;
            var dp = AssignDp.drawCircle(accessory.Data.PartyList[i], 15800, 4000, $"分散{i}", accessory);
            dp.Scale = new(6);
            dp.Color = accessory.Data.DefaultDangerColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    [ScriptMethod(name: "门神：超链I踩塔提示", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(33549|33548)$"])]
    public void DB_SuperChain_I_TowerDir(Event @event, ScriptAccessory accessory)
    {
        if (phase != P12S_Phase.SuperChain_I) return;
        // 灵极白塔 33548
        // 星极黑塔 33549
        var tpos = @event.TargetPosition();
        var aid = @event.ActionId();

        // 我是白分摊，检测到黑塔 || 我是黑分摊，检测到白塔
        var match = (db_SC1_myBuff == 0 && aid == 33549) || (db_SC1_myBuff == 1 && aid == 33548);

        var dp = AssignDp.drawStatic(tpos, 0, 0, 3000, $"踩塔{aid}", accessory);
        dp.Color = match ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
        dp.Scale = new(3f);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);

        if (match)
        {
            var dp0 = AssignDp.dirPos(tpos, 0, 3000, $"踩塔{aid}指路", accessory);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);
        }
    }
    #endregion

    #region 本体：小世界一

    [ScriptMethod(name: "本体：阶段转换小世界一（不可控）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:33574"], userControl: false)]
    public void MB_PhaseChange_Gaia(Event @event, ScriptAccessory accessory)
    {
        phase = phase switch
        {
            P12S_Phase.Classic_I => P12S_Phase.Gaia_II,
            P12S_Phase.Caloric_I => P12S_Phase.Gaia_II,
            P12S_Phase.Exflare => P12S_Phase.Gaia_II,
            P12S_Phase.Pangenesis => P12S_Phase.Gaia_II,
            P12S_Phase.Classic_II => P12S_Phase.Gaia_II,
            P12S_Phase.Caloric_II => P12S_Phase.Gaia_II,
            _ => P12S_Phase.Gaia_I,
        };
        DebugMsg($"当前阶段为：{phase}", accessory);
    }

    [ScriptMethod(name: "本体：小世界，小怪激光", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:33584"])]
    public void MB_Gaia_BeamRay(Event @event, ScriptAccessory accessory)
    {
        if (phase != P12S_Phase.Gaia_I && phase != P12S_Phase.Gaia_II) return;
        var sid = @event.SourceId();
        var sname = @event.SourceName();

        var dp = assignDp_Line(sid, $"小怪激光{sid}", 6, 20, 0, 7000, accessory);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        DebugMsg($"捕捉到[{sid}|{sname}]释放激光并绘图。", accessory);
    }

    private static DrawPropertiesEdit assignDp_Line(uint sid, string name, int width, int length, int delay, int destory, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(width, length);      // 宽度为6，长度为20
        dp.Owner = sid;             // 从哪个单位前方绘图
        dp.Color = accessory.Data.DefaultDangerColor;   // 绘图颜色
        dp.Delay = delay;               // 从捕获到对应日志行后，延迟多少毫秒开始绘图
        dp.DestoryAt = destory;        // 从绘图出现后，经过多少毫秒绘图消失
        return dp;
    }

    [ScriptMethod(name: "本体：小世界，安全区", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(3357[789])$"])]
    public void MB_Gaia1_SafetyField(Event @event, ScriptAccessory accessory)
    {
        if (phase != P12S_Phase.Gaia_I && phase != P12S_Phase.Gaia_II) return;

        // 三种地心说安全形式
        const uint VERTICAL = 33577;
        const uint DONUT = 33578;
        const uint HORIZON = 33579;

        var aid = @event.ActionId();
        var myIndex = IndexHelper.getMyIndex(accessory);

        switch (aid)
        {
            case VERTICAL:
                DebugMsg($"捕捉到地心说垂直安全[{aid}]。", accessory);
                drawVerticalSafetyField(accessory);
                drawVerticalSpreadPos(myIndex, accessory);
                break;
            case DONUT:
                DebugMsg($"捕捉到地心说环形安全[{aid}]。", accessory);
                drawDonutSafetyField(accessory);
                drawDonutSpreadPos(myIndex, accessory);
                break;
            case HORIZON:
                DebugMsg($"捕捉到地心说水平安全[{aid}]。", accessory);
                drawHorizonSafetyField(accessory);
                drawHorizonSpreadPos(myIndex, accessory);
                break;
            default:
                break;
        }
    }

    private static DrawPropertiesEdit assignDp_DonutSafetyField(int scale, int inner_scale, int delay, int destory, string name, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Position = new(100, 0, 100);
        dp.Radian = float.Pi * 2;
        dp.Scale = new(scale);
        dp.InnerScale = new(inner_scale);
        dp.Color = accessory.Data.DefaultDangerColor.WithW(4f);
        dp.Delay = delay;
        dp.DestoryAt = destory;
        return dp;
    }

    private static void drawDonutSafetyField(ScriptAccessory accessory)
    {
        var dp = assignDp_DonutSafetyField(7, 3, 0, 7000, "地心说月环外", accessory);
        dp.Position = new(100, 0, 90);
        dp.Color = ColorHelper.colorDark.V4.WithW(4f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);

        var dp2 = AssignDp.drawStatic(new(100, 0, 90), 0, 0, 7000, "地心说月环内", accessory);
        dp2.Color = ColorHelper.colorDark.V4.WithW(4f);
        dp2.Scale = new(2);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp2);
    }

    private static void drawVerticalSafetyField(ScriptAccessory accessory)
    {
        var dp = AssignDp.drawStatic(new(100, 0, 82), float.Pi, 0, 7000, "地心说中线", accessory);
        dp.Scale = new(4, 20);
        dp.Color = ColorHelper.colorDark.V4.WithW(4f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        var dp2 = AssignDp.drawStatic(new(95, 0, 82), float.Pi, 0, 7000, "地心说左线", accessory);
        dp2.Scale = new(4, 20);
        dp2.Color = ColorHelper.colorDark.V4.WithW(4f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp2);

        var dp3 = AssignDp.drawStatic(new(105, 0, 82), float.Pi, 0, 7000, "地心说右线", accessory);
        dp3.Scale = new(4, 20);
        dp3.Color = ColorHelper.colorDark.V4.WithW(4f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp3);
    }

    private static void drawHorizonSafetyField(ScriptAccessory accessory)
    {
        var dp = AssignDp.drawStatic(new(92, 0, 90), float.Pi / 2, 0, 7000, "地心说中线", accessory);
        dp.Scale = new(4, 20);
        dp.Color = ColorHelper.colorDark.V4.WithW(4f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        var dp2 = AssignDp.drawStatic(new(95, 0, 82), float.Pi / 2, 0, 7000, "地心说上线", accessory);
        dp2.Scale = new(4, 20);
        dp2.Color = ColorHelper.colorDark.V4.WithW(4f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp2);

        var dp3 = AssignDp.drawStatic(new(105, 0, 82), float.Pi / 2, 0, 7000, "地心说右线", accessory);
        dp3.Scale = new(4, 20);
        dp3.Color = ColorHelper.colorDark.V4.WithW(4f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp3);
    }

    [ScriptMethod(name: "本体：小世界，分散", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:0016"])]
    public void MB_SpreadIcon(Event @event, ScriptAccessory accessory)
    {
        if (phase != P12S_Phase.Gaia_I && phase != P12S_Phase.Gaia_II) return;
        var tid = @event.TargetId();
        var uid = accessory.Data.Me;

        var dp = AssignDp.drawCircle(tid, 0, 3000, $"神罚{tid}", accessory);
        dp.Color = tid == uid ? accessory.Data.DefaultSafeColor.WithW(3f) : accessory.Data.DefaultDangerColor.WithW(1.5f);
        dp.Scale = new(1);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    private static void drawDonutSpreadPos(int myIndex, ScriptAccessory accessory)
    {
        List<float> spreadDir = [-0.5f, 0.5f, -1.5f, 1.5f, -3.5f, 3.5f, -2.5f, 2.5f];
        Vector3[] safePos = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            safePos[i] = DirectionCalc.ExtendPoint(new(100, 0, 90), DirectionCalc.angle2Rad(spreadDir[i] * 45), 2.5f);
        }

        for (int i = 0; i < 8; i++)
        {
            var dp = AssignDp.drawStatic(safePos[i], 0, 0, 7000, $"月环分散位置{i}", accessory);
            dp.Scale = new(0.5f);
            dp.Color = myIndex == i ? posColorPlayer.V4.WithW(2f) : posColorNormal.V4;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            if (myIndex != i) continue;
            var dp0 = AssignDp.dirPos(safePos[i], 0, 7000, $"月环分散位置指路{i}", accessory);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);
        }
    }

    private static void drawVerticalSpreadPos(int myIndex, ScriptAccessory accessory)
    {
        List<int> spreadDirLR = [-1, 1, -1, 1, -1, 1, -1, 1];
        List<int> spreadDirUD = [-3, -3, -1, -1, 3, 3, 1, 1];

        Vector3[] safePos = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            safePos[i] = new Vector3(100, 0, 90) + new Vector3(spreadDirLR[i] * 2.5f, 0, 0) + new Vector3(0, 0, spreadDirUD[i] * 2);
        }

        for (int i = 0; i < 8; i++)
        {
            var dp = AssignDp.drawStatic(safePos[i], 0, 0, 7000, $"竖直分散位置{i}", accessory);
            dp.Scale = new(0.5f);
            dp.Color = myIndex == i ? posColorPlayer.V4.WithW(2f) : posColorNormal.V4;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            if (myIndex != i) continue;
            var dp0 = AssignDp.dirPos(safePos[i], 0, 7000, $"竖直分散位置指路{i}", accessory);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);
        }
    }

    private static void drawHorizonSpreadPos(int myIndex, ScriptAccessory accessory)
    {
        List<int> spreadDirUD = [-1, 1, -1, 1, -1, 1, -1, 1];
        List<int> spreadDirLR = [-3, -3, -1, -1, 3, 3, 1, 1];

        // 84 88 92 96

        Vector3[] safePos = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            safePos[i] = new Vector3(100, 0, 90) + new Vector3(spreadDirLR[i] * 2, 0, 0) + new Vector3(0, 0, spreadDirUD[i] * 2.5f);
        }

        for (int i = 0; i < 8; i++)
        {
            var dp = AssignDp.drawStatic(safePos[i], 0, 0, 7000, $"水平分散位置{i}", accessory);
            dp.Scale = new(0.5f);
            dp.Color = myIndex == i ? posColorPlayer.V4.WithW(2f) : posColorNormal.V4;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            if (myIndex != i) continue;
            var dp0 = AssignDp.dirPos(safePos[i], 0, 7000, $"水平分散位置指路{i}", accessory);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);
        }
    }

    [ScriptMethod(name: "本体：小世界，分散删除（不可控）", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:33582", "TargetIndex:1"], userControl: false)]
    public void MB_Gaia1_SpreadIconRemove(Event @event, ScriptAccessory accessory)
    {
        if (phase != P12S_Phase.Gaia_I && phase != P12S_Phase.Gaia_II) return;
        var tid = @event.TargetId();
        accessory.Method.RemoveDraw($"神罚{tid}");
    }

    [ScriptMethod(name: "本体：小世界一，角落安全区", eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["Id:4562", "SourceDataId:16182"])]
    public async void MB_Gaia1_PartySafeCorner(Event @event, ScriptAccessory accessory)
    {
        if (phase != P12S_Phase.Gaia_I) return;
        lock (mb_Gaia1_dangerPlace)
        {
            var spos = @event.SourcePosition();
            var dangerIdx = DirectionCalc.PositionRoundToDirs(spos, new(100, 0, 90), 8);
            mb_Gaia1_dangerPlace[dangerIdx] = true;
            mb_Gaia1_dangerPlace[dangerIdx >= 4 ? dangerIdx - 4 : dangerIdx + 4] = true;
            DebugMsg($"方位{dangerIdx}与{(dangerIdx >= 4 ? dangerIdx - 4 : dangerIdx + 4)}被设置为危险", accessory);
        }

        await Task.Delay(100);

        lock (mb_Gaia1_dangerPlace)
        {
            if (mb_Gaia1_dangerPlace.Count(x => x) != 6) return;    // 若危险方位不为6个，不执行后续代码
            if (mb_Gaia1_dangerPlace_hasDrawn) return;

            var myIndex = IndexHelper.getMyIndex(accessory);
            for (int i = 0; i < mb_Gaia1_dangerPlace.Count(); i++)
            {
                if (mb_Gaia1_dangerPlace[i]) continue;
                var isMySafeCorner = drawSafeCorner_Gaia1(i, myIndex, accessory);
                DebugMsg($"绘制出安全角落{i}，{(isMySafeCorner ? "且为指路目的地" : "不指路")}", accessory);
            }
            mb_Gaia1_dangerPlace_hasDrawn = true;
        }
    }
    private static bool drawSafeCorner_Gaia1(int posIdx, int myIndex, ScriptAccessory accessory)
    {
        var dp = assignDp_SafeCornerCircle(posIdx, $"安全角落{posIdx}", accessory);
        // DPS安全区为右下半场，对应方位为2、3、4、5。
        var isMySafeCorner = ((myIndex >= 4) && (posIdx >= 2) && (posIdx <= 5)) || ((myIndex < 4) && ((posIdx <= 1) || (posIdx >= 6)));

        dp.Color = isMySafeCorner ? posColorPlayer.V4.WithW(2f) : posColorNormal.V4;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        if (!isMySafeCorner) return false;
        var pos = DirectionCalc.ExtendPoint(new(100, 0, 90), DirectionCalc.angle2Rad(45 * posIdx), 6.5f);
        var dp0 = AssignDp.dirPos(pos, 0, 6500, $"指路{posIdx}预备", accessory);
        dp0.Color = accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);

        var dp1 = AssignDp.dirPos(pos, 6500, 3500, $"指路{posIdx}", accessory);
        dp1.Color = accessory.Data.DefaultSafeColor;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp1);

        return true;
    }

    private static DrawPropertiesEdit assignDp_SafeCornerCircle(int idx, string name, ScriptAccessory accessory)
    {
        var pos = DirectionCalc.ExtendPoint(new(100, 0, 90), DirectionCalc.angle2Rad(45 * idx), 6.5f);
        var dp = AssignDp.drawStatic(pos, 0, 0, 10000, name, accessory);
        dp.Scale = new(1);
        return dp;
    }

    #endregion


    #region 本体：一索尼

    [ScriptMethod(name: "本体：阶段转换索尼（不可控）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:33585"], userControl: false)]
    public void MB_PhaseChange_Classic(Event @event, ScriptAccessory accessory)
    {
        phase = phase switch
        {
            P12S_Phase.Pangenesis => P12S_Phase.Classic_II,
            _ => P12S_Phase.Classic_I,
        };
        DebugMsg($"当前阶段为：{phase}", accessory);
    }

    [ScriptMethod(name: "本体：一索尼，玩家索尼分组（不可控）", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(016F|017[012])$"], userControl: false)]
    public void MB_Classic1_PlayerGroupRecord(Event @event, ScriptAccessory accessory)
    {
        if (phase != P12S_Phase.Classic_I) return;
        var id = @event.Id();
        var tid = @event.TargetId();
        var tidx = IndexHelper.getPlayerIdIndex(tid, accessory);
        var tname = @event.TargetName();

        // 四种索尼
        const uint CIRCLE = 367;
        const uint CROSS = 370;
        const uint TRIANGLE = 368;
        const uint SQUARE = 369;

        lock (mb_Classic1_playerGroup)
        {
            switch (id)
            {
                case CIRCLE:
                    mb_Classic1_playerGroup[tidx] = mb_Classic1_playerGroup[tidx] + 1;
                    break;
                case CROSS:
                    mb_Classic1_playerGroup[tidx] = mb_Classic1_playerGroup[tidx] + 2;
                    break;
                case TRIANGLE:
                    mb_Classic1_playerGroup[tidx] = mb_Classic1_playerGroup[tidx] + 3;
                    break;
                case SQUARE:
                    mb_Classic1_playerGroup[tidx] = mb_Classic1_playerGroup[tidx] + 4;
                    break;
                default:
                    break;
            }
        }
        // DebugMsg($"玩家{tidx}({tname})获得了Icon {id}", accessory);
    }

    [ScriptMethod(name: "本体：一索尼，玩家AB分组（不可控）", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(356[01])$"], userControl: false)]
    public void MB_Classic1_PlayerBuffRecord(Event @event, ScriptAccessory accessory)
    {
        if (phase != P12S_Phase.Classic_I) return;
        var sid = @event.StatusID();
        var tid = @event.TargetId();
        var tidx = IndexHelper.getPlayerIdIndex(tid, accessory);
        var tname = @event.TargetName();

        // 两种BUFF
        const uint ALPHA = 3560;
        const uint BETA = 3561;

        lock (mb_Classic1_playerGroup)
        {
            switch (sid)
            {
                case ALPHA:
                    break;
                case BETA:
                    mb_Classic1_playerGroup[tidx] = mb_Classic1_playerGroup[tidx] + 10;
                    break;
                default:
                    break;
            }
        }
        // DebugMsg($"玩家{tidx}({tname})获得了BUFF {(sid == ALPHA ? "ALPHA" : "BETA")}", accessory);
    }

    [ScriptMethod(name: "本体：一索尼，元素分组（不可控）", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(1618[345])$"], userControl: false)]
    public void MB_Classic1_ElementRecord(Event @event, ScriptAccessory accessory)
    {
        if (phase != P12S_Phase.Classic_I) return;
        var did = @event.DataId();
        var sid = @event.SourceId();
        var sname = @event.SourceName();
        var spos = @event.SourcePosition();

        int[] position = getElementPos(spos);
        lock (mb_Classic1_elements)
        {
            mb_Classic1_elements.Add(new ClassicElement(sid, did, position[0], position[1]));
            if (mb_Classic1_elements.Count() != 12) return;
            sortClassicElements(mb_Classic1_elements, accessory);
        }
    }
    private void sortClassicElements(List<ClassicElement> elements, ScriptAccessory accessory)
    {
        for (int i = 0; i < 12; i++)
        {
            ClassicElement element = elements[i];
            if (!element.isWater())
                continue;

            // 此时element为水元素，需找临近元素
            List<int> e_idxs = getNearbyElementIdxs(element, elements);

            DebugMsg($"正在检验水元素({element.Row}, {element.Col})，其临近有{e_idxs.Count()}个元素", accessory);

            var FiresNearby = 0;
            var EarthesNearby = 0;

            foreach (var idx in e_idxs)
            {
                ClassicElement t_element = elements[idx];
                if (t_element.isFire())
                    FiresNearby++;
                if (t_element.isEarth())
                    EarthesNearby++;
            }

            foreach (var idx in e_idxs)
            {
                // 设置临近元素为目标，待检验
                ClassicElement t_element = elements[idx];
                DebugMsg($"正在检验{(t_element.isFire() ? "火元素" : "土元素")}({t_element.Row}, {t_element.Col})", accessory);

                // 若已被选择，忽略
                if (t_element.HasChosen) continue;

                // 只有一个火/土元素临近，必是其目标
                if ((FiresNearby == 1 && t_element.isFire()) || (EarthesNearby == 1 && t_element.isEarth()))
                {
                    AssignTarget(t_element, element);
                    DebugMsg($"水元素({element.Row},{element.Col})找到了独苗目标{(t_element.isFire() ? "火元素" : "土元素")}({t_element.Row},{t_element.Col})", accessory);
                    continue;
                }

                // 目标元素进一步寻找其临近元素
                List<int> te_idxs = getNearbyElementIdxs(t_element, elements);

                // 如果其临近水元素不为1，则代表为模糊项，忽略
                var te_water_nearby = 0;
                foreach (var te_idx in te_idxs)
                {
                    if (elements[te_idx].isWater())
                        te_water_nearby++;
                }
                if (te_water_nearby != 1) continue;

                // 目标元素为该水元素连接对象
                AssignTarget(t_element, element);
                DebugMsg($"水元素({element.Row},{element.Col})找到了二次判断目标{(t_element.isFire() ? "火元素" : "土元素")}({t_element.Row},{t_element.Col})", accessory);
            }
        }
    }

    private void AssignTarget(ClassicElement t_element, ClassicElement element)
    {
        t_element.TargetRow = element.Row;
        t_element.TargetCol = element.Col;
        t_element.TargetSid = element.Sid;
        t_element.HasChosen = true;
    }

    private List<int> getNearbyElementIdxs(ClassicElement element, List<ClassicElement> elements)
    {
        List<int> idxs = new List<int>();
        for (int i = 0; i < 12; i++)
        {
            if (element.isNear(elements[i]))
                idxs.Add(i);
        }
        return idxs;
    }
    private int[] getElementPos(Vector3 spos)
    {
        // 88 96 104 112
        // 80 1 2 3 4

        // 84 92 100
        // 76 1 2 3
        int col = (int)Math.Round((spos.X - 80) / 8);
        int row = (int)Math.Round((spos.Z - 76) / 8);

        return [row, col];
    }

    public class ClassicElement
    {
        const uint FIRE = 16183;
        const uint WATER = 16184;
        const uint EARTH = 16185;
        public uint Sid { get; set; }
        public uint Type { get; set; }
        public int Row { get; set; }
        public int Col { get; set; }
        public int TargetRow { get; set; } = -1;
        public int TargetCol { get; set; } = -1;
        public uint TargetSid { get; set; } = 0;
        public bool HasChosen { get; set; } = false;
        public ClassicElement(uint sid, uint type, int row, int col)
        {
            Sid = sid;
            Type = type;
            Row = row;
            Col = col;
        }
        public bool isNear(ClassicElement element)
        {
            int sum = Math.Abs(Row - element.Row) + Math.Abs(Col - element.Col);
            return sum == 1;
        }
        public bool isWater()
        {
            return Type == WATER;
        }
        public bool isFire()
        {
            return Type == FIRE;
        }
        public bool isEarth()
        {
            return Type == EARTH;
        }
    }

    [ScriptMethod(name: "本体：一索尼，元素指路", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:16183"])]
    public async void MB_Classic1_ElementTargetDraw(Event @event, ScriptAccessory accessory)
    {
        if (phase != P12S_Phase.Classic_I) return;
        await Task.Delay(300);

        if (mb_Classic1_etDrawn) return;
        mb_Classic1_etDrawn = true;

        var myIndex = IndexHelper.getMyIndex(accessory);

        foreach (var element in mb_Classic1_elements)
        {
            if (element.isWater()) continue;
            drawElementRoute(element, mb_Classic1_playerGroup[myIndex], accessory);
        }
    }

    private void drawElementRoute(ClassicElement e, int myClassicBuff, ScriptAccessory accessory)
    {
        int myCol = myClassicBuff % 10;
        bool isAlpha = myClassicBuff < 10;

        // 匹配条件：（（元素为火，我是Alpha） 或 （元素为土，我是Beta）） 且 我的列等于目标（水元素）的列
        bool isMatched = ((e.isFire() && isAlpha) || (e.isEarth() && !isAlpha)) && myCol == e.TargetCol;

        var dp = assignDp_Element2Element(e, accessory);
        dp.Color = isMatched ? posColorPlayer.V4.WithW(3f) : posColorNormal.V4.WithW(1.5f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

        if (!isMatched) return;

        IBattleChara? water = IbcHelper.GetById(e.TargetSid);
        IBattleChara? fe = IbcHelper.GetById(e.Sid);
        if (water == null || fe == null) return;

        Vector3 tpos = (fe.Position - water.Position) / 8 * 3f + water.Position;
        var dp0 = AssignDp.dirPos(tpos, 0, 6000, $"索尼元素指路", accessory);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);

    }
    private DrawPropertiesEdit assignDp_Element2Element(ClassicElement e, ScriptAccessory accessory)
    {
        var dp = AssignDp.drawOwner2Target(e.TargetSid, e.Sid, 0, 12000, $"索尼元素{e.Row}{e.Col}", accessory);
        dp.Scale = new(2f, 4f);
        dp.Color = posColorNormal.V4.WithW(1.5f);
        return dp;
    }

    // 紫线出现Tether 0001，元素钢铁
    [ScriptMethod(name: "本体：一索尼，元素毁灭与射线指路预备", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0001"])]
    public void MB_Classic1_RayDirPrepared(Event @event, ScriptAccessory accessory)
    {
        if (phase != P12S_Phase.Classic_I) return;

        if (mb_Classic1_implodeDrawn) return;
        mb_Classic1_implodeDrawn = true;

        foreach (var element in mb_Classic1_elements)
        {
            var dp = AssignDp.drawCircle(element.Sid, 0, 12000, $"自我毁灭{element.Sid}", accessory);
            dp.Scale = new(4f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        var myIndex = IndexHelper.getMyIndex(accessory);
        var myClassicIndex = getMyClassicIndex(myIndex);
        drawRayDirPrepared(myClassicIndex, true, accessory);
    }

    private int getMyClassicIndex(int myIndex)
    {
        var myClassicBuff = mb_Classic1_playerGroup[myIndex];
        bool isAlpha = myClassicBuff < 10;
        var myClassicIndex = ((myClassicBuff % 10) - 1) * 2 + (isAlpha ? 0 : 1);
        return myClassicIndex;
    }

    private void drawRayDirPrepared(int myClassicIndex, bool isDanger, ScriptAccessory accessory)
    {
        Vector3[] sonyPos = new Vector3[8];
        sonyPos[0] = new(84, 0, 88);
        sonyPos[1] = new(84, 0, 96);
        sonyPos[2] = new(92, 0, 88);
        sonyPos[3] = new(92, 0, 96);
        sonyPos[4] = new(108, 0, 88);
        sonyPos[5] = new(108, 0, 96);
        sonyPos[6] = new(116, 0, 88);
        sonyPos[7] = new(116, 0, 96);

        for (int i = 0; i < sonyPos.Count(); i++)
        {
            if (isDanger)
            {
                var dp = AssignDp.drawStatic(sonyPos[i], 0, 0, 12000, $"索尼射线待命位置{i}", accessory);
                dp.Scale = new(1f);
                dp.Color = i == myClassicIndex ? posColorPlayer.V4.WithW(3f) : posColorNormal.V4;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }

            if (i != myClassicIndex) continue;
            var dp0 = AssignDp.dirPos(sonyPos[i], 0, 12000, $"{(isDanger ? $"索尼射线待命位置指路{i}" : $"索尼射线位置指路{i}")}", accessory);
            dp0.Color = isDanger ? accessory.Data.DefaultDangerColor : accessory.Data.DefaultSafeColor;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);
        }
    }

    [ScriptMethod(name: "本体：一索尼，元素毁灭绘图消失（不可控）", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:33587"], userControl: false)]
    public void MB_Classic1_ImplodeRemove(Event @event, ScriptAccessory accessory)
    {
        if (phase != P12S_Phase.Classic_I) return;
        accessory.Method.RemoveDraw($"^(自我毁灭.*)$");
    }

    // 绿线消失，索尼射线指路
    [ScriptMethod(name: "本体：一索尼，射线范围与指路", eventType: EventTypeEnum.StatusRemove, eventCondition: ["StatusID:3588"])]
    public void MB_Classic1_RayDir(Event @event, ScriptAccessory accessory)
    {
        if (phase != P12S_Phase.Classic_I) return;
        if (mb_Classic1_RayDirDrawn) return;
        mb_Classic1_RayDirDrawn = true;

        accessory.Method.RemoveDraw($"^(索尼射线待命位置指路.*)$");

        var myIndex = IndexHelper.getMyIndex(accessory);
        var myClassicIndex = getMyClassicIndex(myIndex);
        drawRayDirPrepared(myClassicIndex, false, accessory);
        drawPalladianRay(new(92, 0, 92), accessory);
        drawPalladianRay(new(108, 0, 92), accessory);
    }

    private void drawPalladianRay(Vector3 pos, ScriptAccessory accessory)
    {
        for (uint i = 0; i < 4; i++)
        {
            var dp = AssignDp.drawTargetOrder(0, i + 1, 0, 12000, $"帕拉斯射线{i}", accessory);
            dp.Position = pos;
            dp.Scale = new(20f);
            dp.Radian = float.Pi / 6;
            // dp.Color = accessory.Data.DefaultDangerColor.WithW(0.6f);
            dp.Color = ColorHelper.colorPink.V4;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }
    }

    [ScriptMethod(name: "本体：一索尼，射线与指路删除（不可控）", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:33572"], userControl: false)]
    public void MB_Classic1_RayDirRemove(Event @event, ScriptAccessory accessory)
    {
        if (phase != P12S_Phase.Classic_I) return;
        accessory.Method.RemoveDraw($"^(帕拉斯射线.*)$");
        accessory.Method.RemoveDraw($"^(索尼射线待命位置.*)$");
        accessory.Method.RemoveDraw($"^(索尼射线位置指路.*)$");
    }

    #endregion

    #region 本体：一风火

    [ScriptMethod(name: "本体：阶段转换风火（不可控）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:33592"], userControl: false)]
    public void MB_PhaseChange_Caloric(Event @event, ScriptAccessory accessory)
    {
        phase = phase switch
        {
            P12S_Phase.Classic_II => P12S_Phase.Caloric_II,
            _ => P12S_Phase.Caloric_I,
        };
        mb_Caloric_phase = 0;
        DebugMsg($"当前阶段为：{phase}", accessory);
    }

    [ScriptMethod(name: "本体：一风火，初始点名记录（不可控）", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:012F"], userControl: false)]
    public async void MB_Caloric_FirstDir(Event @event, ScriptAccessory accessory)
    {
        await Task.Delay(200);

        if (phase != P12S_Phase.Caloric_I) return;
        if (mb_Caloric_phase > 2) return;

        var tid = @event.TargetId();
        var tidx = IndexHelper.getPlayerIdIndex(tid, accessory);
        mb_Caloric_isFirstTarget[tidx] = true;
        mb_Caloric_WindPriority[tidx]++;
        if (tidx >= 4)
            mb_Caloric_WindPriority[tidx]++;
        // 1 左上，2 右上，3 右下，4 左下

        mb_Caloric_phase++;
    }

    [ScriptMethod(name: "本体：一风火，初始指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:33597"])]
    public async void MB_Caloric_FirstStack(Event @event, ScriptAccessory accessory)
    {
        await Task.Delay(300);

        if (phase != P12S_Phase.Caloric_I) return;
        if (mb_Caloric_phase > 2) return;

        var myIndex = IndexHelper.getMyIndex(accessory);
        var tid = @event.TargetId();
        drawCaloricStack(tid, mb_Caloric_isFirstTarget[myIndex], accessory);
        drawCaloricFirstDir(accessory);
    }

    private void drawCaloricStack(uint owner_id, bool isDanger, ScriptAccessory accessory)
    {
        var dp = AssignDp.drawCircle(owner_id, 0, 8000, $"一风火分摊{owner_id}", accessory);
        dp.Scale = new(4);
        dp.Color = isDanger ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    private void drawCaloricFirstDir(ScriptAccessory accessory)
    {
        var myIndex = IndexHelper.getMyIndex(accessory);
        Vector3 SAFE = new Vector3(100, 0, 97.5f);
        Vector3 UPLEFT = new Vector3(99, 0, 89);
        Vector3 UPRIGHT = new Vector3(101, 0, 89);

        var isSafe = !mb_Caloric_isFirstTarget[myIndex];
        var pos = isSafe ? SAFE : (myIndex < 4 ? UPLEFT : UPRIGHT);
        var dp0 = AssignDp.dirPos(pos, 0, 8000, $"一风火初始指路{myIndex}", accessory);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);
    }

    [ScriptMethod(name: "本体：一风火转阶段，四组分摊（不可控）", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:33592"], userControl: false)]
    public void MB_PhaseChange_Caloric_PartnerStack(Event @event, ScriptAccessory accessory)
    {
        if (phase != P12S_Phase.Caloric_I) return;
        if (mb_Caloric_phase > 2) return;
        mb_Caloric_phase = 10;
        accessory.Method.RemoveDraw($"^(一风火分摊.*)$");
        accessory.Method.RemoveDraw($"^(一风火初始指路.*)$");
    }

    [ScriptMethod(name: "本体：一风火buff记录（不可控）", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(359[01])$"], userControl: false)]
    public void MB_Caloric_BuffRecord(Event @event, ScriptAccessory accessory)
    {
        if (phase != P12S_Phase.Caloric_I) return;
        if (mb_Caloric_phase < 10) return;

        var tid = @event.TargetId();
        var tidx = IndexHelper.getPlayerIdIndex(tid, accessory);
        var sid = @event.StatusID();

        // const uint FIRE = 3590;
        const uint WIND = 3591;
        mb_Caloric_isWind[tidx] = sid == WIND;
    }

    [ScriptMethod(name: "本体：一风火，四组分摊范围", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:3590"])]
    public async void MB_Caloric_PartnerStack(Event @event, ScriptAccessory accessory)
    {
        if (phase != P12S_Phase.Caloric_I) return;
        if (mb_Caloric_phase < 10) return;
        mb_Caloric_phase++;
        if (mb_Caloric_ParnterStackDrawn) return;
        mb_Caloric_ParnterStackDrawn = true;

        await Task.Delay(200);

        DebugMsg($"开始绘制火分摊范围 {mb_Caloric_phase}", accessory);

        var myStackPos = getMyStackPos(accessory);
        for (int i = 0; i < 8; i++)
        {
            if (mb_Caloric_isWind[i]) continue;
            bool isMyPartner = myStackPos == mb_Caloric_FirePriority[i];
            drawCaloricPartnerStack(accessory.Data.PartyList[i], isMyPartner, accessory);
        }
    }

    private int getMyStackPos(ScriptAccessory accessory)
    {
        var myIndex = IndexHelper.getMyIndex(accessory);
        var myStackPos = (mb_Caloric_WindPriority[myIndex] + mb_Caloric_FirePriority[myIndex]) % 10;
        return myStackPos;
    }

    private void drawCaloricPartnerStack(uint owner_id, bool isMyPartner, ScriptAccessory accessory)
    {
        var dp = AssignDp.drawCircle(owner_id, 0, 12000, $"一风火四组分摊{owner_id}", accessory);
        dp.Scale = new(4);
        dp.Color = isMyPartner ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "本体：一风火，四组分摊风火指路", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:3591"])]
    public async void MB_Caloric_PartnerStackDir(Event @event, ScriptAccessory accessory)
    {
        if (phase != P12S_Phase.Caloric_I) return;
        if (mb_Caloric_phase < 10) return;
        mb_Caloric_phase++;
        if (mb_Caloric_ParnterStackDirDrawn) return;
        mb_Caloric_ParnterStackDirDrawn = true;

        await Task.Delay(100);

        DebugMsg($"开始执行一次风火优先级计算 {mb_Caloric_phase}", accessory);
        calcCaloricPriority();
        string WindPriorityStr = string.Join(", ", mb_Caloric_WindPriority);
        DebugMsg($"风优先级：{WindPriorityStr}", accessory);
        string FirePriorityStr = string.Join(", ", mb_Caloric_FirePriority);
        DebugMsg($"火优先级：{FirePriorityStr}", accessory);

        var myIndex = IndexHelper.getMyIndex(accessory);
        var myStackPos = getMyStackPos(accessory);
        Vector3 stackPos = myStackPos switch
        {
            1 => new(97.5f, 0, 92.5f),
            2 => new(102.5f, 0, 92.5f),
            3 => new(102.5f, 0, 97.5f),
            4 => new(97.5f, 0, 97.5f),
            _ => new(100, 0, 100)
        };

        if (mb_Caloric_isWind[myIndex] && mb_Caloric_WindPriority[myIndex] <= 2)
            stackPos = stackPos - new Vector3(0, 0, 3.5f);

        var dp = AssignDp.dirPos(stackPos, 0, 12000, $"一风火就位{myStackPos}{mb_Caloric_isWind[myIndex]}", accessory);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    private void calcCaloricPriority()
    {
        for (int i = 0; i < 8; i++)
        {
            if (mb_Caloric_isWind[i])
            {
                if (mb_Caloric_WindPriority[i] != 0) continue;
                mb_Caloric_WindPriority[i] = mb_Caloric_WindPriority.Max() + 1;
            }
            else
            {
                if (mb_Caloric_FirePriority[i] != 0) continue;
                mb_Caloric_FirePriority[i] = mb_Caloric_FirePriority.Max() + 1;
            }
        }
    }

    [ScriptMethod(name: "本体：一风火转阶段，风击退（不可控）", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:33594"], userControl: false)]
    public void MB_PhaseChange_Caloric_WindKnockBack(Event @event, ScriptAccessory accessory)
    {
        if (phase != P12S_Phase.Caloric_I) return;
        if (mb_Caloric_phase < 10) return;
        mb_Caloric_phase = 20;
        accessory.Method.RemoveDraw($"^(一风火就位.*)$");
        accessory.Method.RemoveDraw($"^(一风火四组分摊.*)$");
    }

    [ScriptMethod(name: "本体：一风火，二次火分摊记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:3590"], userControl: false)]
    public void MB_Caloric_SecondBuffRecord(Event @event, ScriptAccessory accessory)
    {
        if (phase != P12S_Phase.Caloric_I) return;
        if (mb_Caloric_phase < 20) return;

        var tid = @event.TargetId();
        var tidx = IndexHelper.getPlayerIdIndex(tid, accessory);
        // 二次火分摊一定在一次火中选
        mb_Caloric_FirePriority[tidx] = mb_Caloric_FirePriority[tidx] + 10;

    }

    [ScriptMethod(name: "本体：一风火，二次火分摊指路", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:3590"])]
    public async void MB_Caloric_SecondPartnerStackDir(Event @event, ScriptAccessory accessory)
    {
        if (phase != P12S_Phase.Caloric_I) return;
        if (mb_Caloric_phase < 20) return;
        mb_Caloric_phase++;

        if (mb_Caloric_SecondParnterStackDirDrawn) return;
        mb_Caloric_SecondParnterStackDirDrawn = true;

        await Task.Delay(100);

        DebugMsg($"开始执行二次风火优先级计算 {mb_Caloric_phase}", accessory);
        calcCaloricPrioritySecond();

        string WindPriorityStr = string.Join(", ", mb_Caloric_WindPriority);
        DebugMsg($"风优先级：{WindPriorityStr}", accessory);
        string FirePriorityStr = string.Join(", ", mb_Caloric_FirePriority);
        DebugMsg($"火优先级：{FirePriorityStr}", accessory);

        var myIndex = IndexHelper.getMyIndex(accessory);
        var myStackPos = getMyStackPos(accessory);

        Vector3 stackPos = myStackPos switch
        {
            1 => mb_Caloric_isWind[myIndex] ? new(93.5f, 0, 85.5f) : new(97.5f, 0, 92.5f),
            2 => mb_Caloric_isWind[myIndex] ? new(106.5f, 0, 85.5f) : new(102.5f, 0, 92.5f),
            3 => new(106.5f, 0, 100.5f),
            4 => new(93.5f, 0, 100.5f),
            _ => new(100, 0, 100)
        };

        var dp = AssignDp.dirPos(stackPos, 0, 12000, $"一风火二次就位{myStackPos}{mb_Caloric_isWind[myIndex]}", accessory);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    private void calcCaloricPrioritySecond()
    {
        bool needSwap = false;
        var fireTargets = mb_Caloric_FirePriority.Where(x => x > 10).Take(2).ToList();

        if (fireTargets.Sum() % 2 == 0)
            needSwap = true;

        for (int i = 0; i < 8; i++)
        {
            mb_Caloric_FirePriority[i] = (mb_Caloric_FirePriority[i] % 10) switch
            {
                1 => mb_Caloric_FirePriority[i],
                2 => mb_Caloric_FirePriority[i],
                3 => (mb_Caloric_FirePriority[i] > 10 ? 10 : 0) + (needSwap ? 1 : 2),
                4 => (mb_Caloric_FirePriority[i] > 10 ? 10 : 0) + (needSwap ? 2 : 1),
                _ => mb_Caloric_FirePriority[i],
            };
        }
    }

    [ScriptMethod(name: "本体：一风火，二次分摊范围", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:3590"])]
    public async void MB_Caloric_SecondPartnerStack(Event @event, ScriptAccessory accessory)
    {
        if (phase != P12S_Phase.Caloric_I) return;
        if (mb_Caloric_phase < 20) return;
        mb_Caloric_phase++;
        if (mb_Caloric_SecondParnterStackDrawn) return;
        mb_Caloric_SecondParnterStackDrawn = true;

        await Task.Delay(200);

        DebugMsg($"开始绘制火分摊范围 {mb_Caloric_phase}", accessory);

        var myStackPos = getMyStackPos(accessory);
        for (int i = 0; i < 8; i++)
        {
            if (mb_Caloric_isWind[i]) continue;
            if (mb_Caloric_FirePriority[i] < 10) continue;
            bool isMyPartner = myStackPos == mb_Caloric_FirePriority[i] % 10;
            drawCaloricPartnerStack(accessory.Data.PartyList[i], isMyPartner, accessory);
        }
    }

    [ScriptMethod(name: "本体：一风火，二次环风范围", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:3590"])]
    public async void MB_Caloric_SecondWindDonut(Event @event, ScriptAccessory accessory)
    {
        if (phase != P12S_Phase.Caloric_I) return;
        if (mb_Caloric_phase < 20) return;
        mb_Caloric_phase++;
        if (mb_Caloric_SecondWindDonutDrawn) return;
        mb_Caloric_SecondWindDonutDrawn = true;

        await Task.Delay(200);

        DebugMsg($"开始绘制环风范围 {mb_Caloric_phase}", accessory);

        for (int i = 0; i < 8; i++)
        {
            if (!mb_Caloric_isWind[i]) continue;
            drawWindCircle(accessory.Data.PartyList[i], accessory);
        }
    }

    private void drawWindCircle(uint owner_id, ScriptAccessory accessory)
    {
        var dp = AssignDp.drawCircle(owner_id, 0, 12000, $"一风火环风{owner_id}", accessory);
        dp.Scale = new(7);
        // dp.Color = accessory.Data.DefaultDangerColor;
        dp.Color = ColorHelper.colorLightBlue.V4;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "本体：一风火结束（不可控）", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:33595"], userControl: false)]
    public void MB_PhaseChange_Caloric_End(Event @event, ScriptAccessory accessory)
    {
        if (phase != P12S_Phase.Caloric_I) return;
        if (mb_Caloric_phase < 20) return;
        mb_Caloric_phase = 30;
        accessory.Method.RemoveDraw($"^(一风火四组分摊.*)$");
        accessory.Method.RemoveDraw($"^(一风火环风.*)$");
        accessory.Method.RemoveDraw($"^(一风火二次就位.*)$");
    }

    #endregion

    #region 本体：一地火

    [ScriptMethod(name: "本体：阶段转换地火（不可控）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:33566"], userControl: false)]
    public void MB_PhaseChange_Exflare(Event @event, ScriptAccessory accessory)
    {
        phase = P12S_Phase.Exflare;
        DebugMsg($"当前阶段为：{phase}", accessory);
    }

    [ScriptMethod(name: "本体：地火预警", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:33567"])]
    public void MB_Exflare(Event @event, ScriptAccessory accessory)
    {
        var spos = @event.SourcePosition();
        var srot = @event.SourceRotation();

        Vector3[] exflarePos = new Vector3[6];
        for (int i = 0; i < 6; i++)
        {
            exflarePos[i] = DirectionCalc.ExtendPoint(spos, DirectionCalc.BaseInnGame2DirRad(srot), 8 * i);
        }

        const int CAST_TIME = 6000;
        const int INTERVAL_TIME = 2000;

        for (int i = 0; i < 6; i++)
        {
            var destoryAt = i == 0 ? CAST_TIME : INTERVAL_TIME;
            var delay = i == 0 ? 0 : CAST_TIME + (i - 1) * INTERVAL_TIME;
            // 本体地火
            drawExflare(exflarePos[i], delay, destoryAt, accessory);
            // 预警地火
            if (i < 5)
                drawExflareWarn(exflarePos[i + 1], 1, delay, destoryAt, accessory);
            if (i < 4)
                drawExflareWarn(exflarePos[i + 2], 2, delay, destoryAt, accessory);
        }
    }

    private void drawExflare(Vector3 spos, int delay, int destoryAt, ScriptAccessory accessory)
    {
        var dp = AssignDp.drawStatic(spos, 0, delay, destoryAt, $"地火{spos}", accessory);
        dp.Scale = new(6f);
        dp.Color = exflareColor.V4.WithW(1.5f);
        dp.ScaleMode = ScaleMode.ByTime;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    private void drawExflareWarn(Vector3 spos, int adv, int delay, int destoryAt, ScriptAccessory accessory)
    {
        const int INTERVAL_TIME = 2000;

        var destroy_add = INTERVAL_TIME * (adv - 1);
        var dp = AssignDp.drawStatic(spos, 0, delay, destoryAt + destroy_add, $"地火{spos}", accessory);
        dp.Scale = new(6f);
        dp.Color = exflareWarnColor.V4.WithW(0.8f / adv);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "本体：地火核爆安全区记录（不可控）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:34435"], userControl: false)]
    public void MB_ExflarePosRecord(Event @event, ScriptAccessory accessory)
    {
        if (phase != P12S_Phase.Exflare) return;
        var spos = @event.SourcePosition();
        var posidx = DirectionCalc.PositionRoundToDirs(spos, new(100, 0, 95), 4);
        mb_Exflare_FlarePos[posidx] = true;
    }

    [ScriptMethod(name: "本体：地火安全区指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:34435"])]
    public async void MB_ExflareDir(Event @event, ScriptAccessory accessory)
    {
        if (phase != P12S_Phase.Exflare) return;
        var spos = @event.SourcePosition();
        if (mb_Exflare_DirDrawn) return;
        mb_Exflare_DirDrawn = true;

        await Task.Delay(100);
        if (mb_Exflare_FlarePos.Count(x => x) != 2) return;
        DebugMsg($"{(mb_Exflare_FlarePos[0] ? "上有地火，左右安全" : "左有地火，上下安全")}", accessory);

        Vector3[] ExflareDir = new Vector3[2];
        var myIndex = IndexHelper.getMyIndex(accessory);

        // 上有地火，左右安全
        ExflareDir[0] = getExflareFirstSafePos(mb_Exflare_FlarePos[0], myIndex);
        ExflareDir[1] = getExflareSecondSafePos(mb_Exflare_FlarePos[0], myIndex);
        drawExflareDir(ExflareDir[0], ExflareDir[1], accessory);
    }

    private Vector3 getExflareFirstSafePos(bool isLeftSafe, int myIndex)
    {
        Vector3[] ExflareSafePos = new Vector3[4];
        ExflareSafePos[0] = new(98, 0, 81);     // 上
        ExflareSafePos[1] = new(119, 0, 92);    // 右
        ExflareSafePos[2] = new(102, 0, 109);   // 下
        ExflareSafePos[3] = new(81, 0, 98);     // 左

        int safePosIdx;
        if (isLeftSafe)
            safePosIdx = (myIndex % 2 == 0) ? 3 : 1;    // MT组为偶数，余数为0
        else
            safePosIdx = (myIndex % 4 < 2) ? 0 : 2;     // 近战组idx除以4，余数为0、1
        return ExflareSafePos[safePosIdx];
    }

    private Vector3 getExflareSecondSafePos(bool isLeftSafe, int myIndex)
    {
        const int CENTER_Z = 95;
        const int CENTER_X = 100;

        Vector3[] ExflareSpreadPos = new Vector3[16];
        ExflareSpreadPos[0] = new(81, 0, 81);
        ExflareSpreadPos[1] = DirectionCalc.FoldPointLR(ExflareSpreadPos[0], CENTER_X);
        ExflareSpreadPos[4] = new(93, 0, 81);
        ExflareSpreadPos[5] = DirectionCalc.FoldPointLR(ExflareSpreadPos[4], CENTER_X);
        ExflareSpreadPos[2] = DirectionCalc.FoldPointUD(ExflareSpreadPos[4], CENTER_Z);
        ExflareSpreadPos[3] = DirectionCalc.FoldPointUD(ExflareSpreadPos[5], CENTER_Z);
        ExflareSpreadPos[6] = DirectionCalc.FoldPointUD(ExflareSpreadPos[0], CENTER_Z);
        ExflareSpreadPos[7] = DirectionCalc.FoldPointUD(ExflareSpreadPos[1], CENTER_Z);

        ExflareSpreadPos[8] = new(81, 0, 81);
        ExflareSpreadPos[9] = DirectionCalc.FoldPointLR(ExflareSpreadPos[8], CENTER_X);
        ExflareSpreadPos[12] = new(81, 0, 90);
        ExflareSpreadPos[13] = DirectionCalc.FoldPointLR(ExflareSpreadPos[12], CENTER_X);
        ExflareSpreadPos[10] = DirectionCalc.FoldPointUD(ExflareSpreadPos[12], CENTER_Z);
        ExflareSpreadPos[11] = DirectionCalc.FoldPointUD(ExflareSpreadPos[13], CENTER_Z);
        ExflareSpreadPos[14] = DirectionCalc.FoldPointUD(ExflareSpreadPos[8], CENTER_Z);
        ExflareSpreadPos[15] = DirectionCalc.FoldPointUD(ExflareSpreadPos[9], CENTER_Z);

        int safePosIdx;
        if (isLeftSafe)
            safePosIdx = myIndex + 8;    // 8-15为左右分散
        else
            safePosIdx = myIndex;     // 0-7为上下分散
        return ExflareSpreadPos[safePosIdx];
    }

    private void drawExflareDir(Vector3 safePos, Vector3 spreadPos, ScriptAccessory accessory)
    {
        var dp = AssignDp.dirPos(safePos, 0, 7000, $"地火核爆安全", accessory);
        var dp0 = AssignDp.dirPos2Pos(safePos, spreadPos, 0, 7000, $"即将分散位置", accessory);
        dp0.Color = accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);

        var dp1 = AssignDp.dirPos(spreadPos, 7000, 3000, $"分散位置", accessory);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp1);
    }

    #endregion
    
    #region 本体：黑白塔

    [ScriptMethod(name: "本体：阶段转换地火（不可控）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:33566"], userControl: false)]
    public void MB_PhaseChange_Pangenesis(Event @event, ScriptAccessory accessory)
    {
        phase = P12S_Phase.Pangenesis;
        DebugMsg($"当前阶段为：{phase}", accessory);
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

    public static uint DataId(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["DataId"]);
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

    public static uint Id(this Event @event)
    {
        return ParseHexId(@event["Id"], out var id) ? id : 0;
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
    /// 将弧度转为角度
    /// </summary>
    /// <param name="radian">弧度值</param>
    /// <returns>对应的角度值</returns>
    public static float rad2Angle(float radian)
    {
        // 输入角度转为弧度
        float angle = (float)(radian / Math.PI * 180);
        return angle;
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

    /// <summary>
    /// 将输入点上下折叠
    /// </summary>
    /// <param name="point">待折叠点</param>
    /// <param name="centerx">中心折线坐标点</param>
    /// <returns></returns>
    public static Vector3 FoldPointUD(Vector3 point, int centerz)
    {
        Vector3 v3 = new(point.X, point.Y, 2 * centerz - point.Z);
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
    public static ScriptColor colorDark = new ScriptColor { V4 = new Vector4(0f, 0f, 0f, 1.0f) };
    public static ScriptColor colorLightBlue = new ScriptColor { V4 = new Vector4(0.48f, 0.40f, 0.93f, 1.0f) };
    public static ScriptColor colorWhite = new ScriptColor { V4 = new Vector4(1f, 1f, 1f, 2f) };
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
        dp.Scale = new(1f);
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
        dp.Scale = new(1f);
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
        dp.Scale = new(1f);
        dp.Owner = accessory.Data.Me;
        dp.TargetObject = target_id;
        dp.ScaleMode = ScaleMode.YByDistance;
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        return dp;
    }

    /// <summary>
    /// 返回与某对象仇恨或某定点相关的dp，可修改dp.TargetResolvePattern, dp.TargetOrderIndex, dp.Owner
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
    /// 返回环形dp，跟随owner，可修改 dp.Owner, dp.Scale
    /// </summary>
    /// <param name="owner_id">起始目标id，通常为自己或Boss</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destoryAt">绘图自出现起，经destoryAt ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit drawDonut(uint owner_id, int delay, int destoryAt, string name, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(22);
        dp.InnerScale = new(6);
        dp.Radian = float.Pi * 2;
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
    /// <param name="rotate_rad">旋转角度，以北为0度顺时针</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destoryAt">绘图自出现起，经destoryAt ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit drawStatic(Vector3 center, float rotate_rad, int delay, int destoryAt, string name, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new(5, 20);
        dp.Position = center;
        dp.Rotation = DirectionCalc.BaseDirRad2InnGame(rotate_rad);
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destoryAt;
        return dp;
    }
}


#endregion