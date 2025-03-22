using System;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent.Struct;
using KodakkuAssist.Module.Draw;
using System.Collections.Generic;
using System.Threading;
using ECommons;
using System.Numerics;
using Newtonsoft.Json;
using System.Linq;
using System.ComponentModel;
using System.Xml.Linq;
using Dalamud.Utility.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using KodakkuAssist.Module.Draw.Manager;
using KodakkuAssist.Module.GameOperate;
using ImGuiNET;
using ECommons.MathHelpers;

namespace UsamisKodakku.UsamisPrivateScript._07_DawnTrail.FRU;

[ScriptType(name: Name, territorys: [1238], guid: "3076a62b-127e-468e-96d3-4f1d559857ec",
    version: Version, author: "Usami", note: NoteStr, updateInfo: UpdateInfo)]

// ^(?!.*((武僧|机工士|龙骑士|武士|忍者|蝰蛇剑士|钐镰客|舞者|吟游诗人|占星术士|贤者|学者|(朝日|夕月)小仙女|炽天使|白魔法师|战士|骑士|暗黑骑士|绝枪战士|绘灵法师|黑魔法师|青魔法师|召唤师|宝石兽|亚灵神巴哈姆特|亚灵神不死鸟|迦楼罗之灵|泰坦之灵|伊弗利特之灵|后式自走人偶)\] (Used|Cast))).*35501.*$
// ^\[\w+\|[^|]+\|E\]\s\w+

public class FruPatch
{
    private const string NoteStr =
        """
        v0.0.0.2
        指挥模式对P3二运有效。
        若开启指挥模式，将执行近战优化标点。
        不论MT或ST引导，都默认MT与D1为车头。双T都会收到引导提示。
        """;

    private const string Name = "FRU_Patch [光暗未来绝境战 补丁]";
    private const string Version = "0.0.0.2";
    private const string DebugVersion = "a";
    private const string UpdateInfo =
        """
        修复P3启示录精确标点模式下指路错误问题。
        """;

    [UserSetting("Debug模式，非开发用请关闭")]
    public static bool DebugMode { get; set; } = false;

    [UserSetting("站位提示圈绘图-普通颜色")]
    public static ScriptColor PosColorNormal { get; set; } = new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 1.0f, 1.0f) };
    [UserSetting("站位提示圈绘图-玩家站位颜色")]
    public static ScriptColor PosColorPlayer { get; set; } = new ScriptColor { V4 = new Vector4(0.0f, 1.0f, 1.0f, 1.0f) };

    [UserSetting("指挥模式（总开关）")]
    public static bool CaptainMode { get; set; } = false;

    [UserSetting("指挥模式 - 开启【P1乐园绝技】指挥")]
    public static bool UosCaptainMode { get; set; } = false;

    [UserSetting("指挥模式 - 开启【P1罪壤堕（DB闲固）】指挥")]
    public static bool FofCaptainMode { get; set; } = false;

    [UserSetting("指挥模式 - 开启【P3二运启示录分组】指挥")]
    public static bool ApoCaptainMode { get; set; } = false;

    [UserSetting("P1 - 罪壤堕优先级")]
    public static FallOfFaithPriorityEnum FallOfFaithPriority { get; set; } = FallOfFaithPriorityEnum.H_T_D_H;
    public enum FallOfFaithPriorityEnum
    {
        H_T_D_H,
        T_H_D,
        H_T_D,
    }
    [UserSetting("P2 - 光爆策略")]
    public static LightRampageStgEnum LightRampageStg { get; set; } = LightRampageStgEnum.Grey9_灰9式;
    public enum LightRampageStgEnum
    {
        Hexagram_正六芒星,
        Grey9_灰9式,
    }

    [UserSetting("P3 - 启示录策略")]
    public static ApoStgEnum ApoStg { get; set; } = ApoStgEnum.CrowdFirst_人群车头;
    public enum ApoStgEnum
    {
        CrowdFirst_人群车头,
        CrownFirst_MTD1皇帝车头,
    }
    private const bool Debugging = true;
    private static readonly bool LocalTest = false;
    private static readonly bool LocalStrTest = false;      // 本地不标点，仅用字符串表示。
    private static readonly Random Random = new();          // 随机测试用
    private int rdTarget = -1;                               // 随机测试用
    private volatile List<bool> _recorded = new bool[20].ToList();      // 被记录flag
    private static List<ManualResetEvent> _events = Enumerable
        .Range(0, 20)
        .Select(_ => new ManualResetEvent(false))
        .ToList();

    private enum FruPhase
    {
        Init,
        P1A_UtopianSky,             // P1A 乐园绝技
        P1B_FallOfFaith,            // P1B 罪壤堕
        P1C_BurntStrike,            // P1C 燃烧击
        P2A_DiamondDust,            // P2A 钻石星辰
        P2B_Mirror,                 // P2B 镜中奇遇
        P2C_LightRampant,           // P2B 光爆
        P2D_AbsoluteZero,           // P2C 绝对零度
        P3A_UltimateRelativity,     // P3A 时间压缩
        P3B_Apocalypse,             // P3B 地火
        P4A_DarklitDragonsong,      // P4A 光暗龙诗
        P4B_CrystallizeTime,        // P4B 时间结晶
        P5A_FulgentBlade,           // P5A 光尘之剑地火
        P5B_ParadiseRegained,       // P5B 复乐园踩塔
        P5C_PolarizingStrike,       // P5C 星灵之剑挡枪
    }

    private static readonly Vector3 Center = new Vector3(100, 0, 100);
    private FruPhase _fruPhase = FruPhase.Init;
    private static PriorityDict _pd = new PriorityDict();
    private static Counter _ct = new Counter();
    private static Apocalypse _apo = new Apocalypse();

    private const int Mt = 0;
    private const int St = 1;
    private const int H1 = 2;
    private const int H2 = 3;
    private const int D1 = 4;
    private const int D2 = 5;
    private const int D3 = 6;
    private const int D4 = 7;

    public void Init(ScriptAccessory accessory)
    {
        _fruPhase = FruPhase.Init;

        rdTarget = -1;
        _recorded = new bool[20].ToList();
        _events = Enumerable
            .Range(0, 20)
            .Select(_ => new ManualResetEvent(false))
            .ToList();

        accessory.DebugMsg($"Init {Name} v{Version}{DebugVersion} Success.\n{UpdateInfo}", DebugMode);
        accessory.Method.MarkClear();
        LocalMarkClear(accessory);
        accessory.Method.RemoveDraw(".*");
    }

    [ScriptMethod(name: "随时DEBUG用", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo"], userControl: false)]
    public void EchoDebugActive(Event @event, ScriptAccessory accessory)
    {
        if (!DebugMode) return;
        var msg = @event.Message();
        switch (msg)
        {
            case "=TST":
                accessory.DebugMsg($"Debug操作。", DebugMode);
                break;

            case "=CLEAR":
                accessory.DebugMsg($"删除绘图与标点 Local。", DebugMode);
                LocalMarkClear(accessory);
                accessory.Method.RemoveDraw(".*");
                break;
        }
    }

    #region P1 绝命战士

    [ScriptMethod(name: "---- 《P1：绝命战士》 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["ActionId:Hello1aya2World"],
        userControl: true)]
    public void SplitLine_FateBreaker(Event @event, ScriptAccessory accessory)
    {
        if (!DebugMode) return;
        accessory.DebugMsg($"debug 怎会有人点到这个", DebugMode);
        LocalMarkClear(accessory);
        accessory.Method.RemoveDraw(".*");
    }

    #region P1.1 乐园绝技

    [ScriptMethod(name: "乐园绝技阶段转换", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4015[45])$"],
        userControl: Debugging)]
    public void UtopianSkyPhaseChange(Event @event, ScriptAccessory accessory)
    {
        _fruPhase = FruPhase.P1A_UtopianSky;
        accessory.DebugMsg($"当前阶段为：{_fruPhase}", DebugMode);
        // 初始化优先级class
        _pd.Init(accessory, "乐园绝技");
        // TN在上，DPS在下，MTD1负责换
        _pd.AddPriorities([3, 0, 1, 2, 4, 5, 6, 7]);
    }

    [ScriptMethod(name: "乐园绝技分摊头标", eventType: EventTypeEnum.Tether, eventCondition: ["Id:regex:^(00F9)$"],
        userControl: true)]
    public void UtopianSkyStackMark(Event @event, ScriptAccessory accessory)
    {
        /*
         * 攻击1：被连线，上半场
         * 攻击2：被连线，下半场
         * 禁止1：仅会出现在D1头上，被点则说明要去上半场
         * 禁止2：仅会出现在MT头上，被点则说明要去下半场
         * 初始优先级设置：_pd.AddPriorities([3, 0, 1, 2, 4, 5, 6, 7]);
         * 只要检测到目标被连线，对应优先级+10。
         */
        if (_fruPhase != FruPhase.P1A_UtopianSky) return;
        var tid = @event.TargetId();
        var tidx = accessory.GetPlayerIdIndex(tid);
        lock (_pd)
        {
            _pd.Priorities[tidx] += 10;
            _pd.AddActionCount();
        }
        if (!_pd.IsActionCountEqualTo(2)) return;
        _pd.ShowPriorities();

        // 从小到大排序，index 0-2闲人在上，3-5闲人在下，6接线在上，7接线在下。
        {
            var upTetherTarget = _pd.SelectSpecificPriorityIndex(6);
            var downTetherTarget = _pd.SelectSpecificPriorityIndex(7);
            // 标点
            MarkPlayerByIdx(accessory, upTetherTarget.Key, MarkType.Attack1, UosCaptainMode);
            MarkPlayerByIdx(accessory, downTetherTarget.Key, MarkType.Attack2, UosCaptainMode);
            // 发送Debug信息
            var str = "\n";
            str += $"上：{upTetherTarget.Key} ({accessory.GetPlayerJobByIndex(upTetherTarget.Key)})\n";
            str += $"下：{downTetherTarget.Key} ({accessory.GetPlayerJobByIndex(downTetherTarget.Key)})\n";
            accessory.DebugMsg(str, DebugMode);
        }

        var remainPlayers = _pd.SelectSmallPriorityIndices(6);
        // 交换检查，只需要查index为2或3的情况
        if (remainPlayers[2].Key == D1)
        {
            // 满足该条件，代表D1在上。
            var str = $"玩家{remainPlayers[2].Key} ({accessory.GetPlayerJobByIndex(remainPlayers[2].Key)}需交换。)";
            accessory.DebugMsg(str, DebugMode);
            MarkPlayerByIdx(accessory, remainPlayers[2].Key, MarkType.Stop1, UosCaptainMode);
        }

        if (remainPlayers[3].Key == Mt)
        {
            // 满足该条件，代表MT在下。
            var str = $"玩家{remainPlayers[3].Key} ({accessory.GetPlayerJobByIndex(remainPlayers[3].Key)}需交换。)";
            accessory.DebugMsg(str, DebugMode);
            MarkPlayerByIdx(accessory, remainPlayers[3].Key, MarkType.Stop2, UosCaptainMode);
        }

        // 若想后续应用指路可直接调用remainPlayers，Key代表职业位置，Value为优先级。
    }

    #endregion P1.1 乐园绝技

    #region P1.2 罪壤堕

    [ScriptMethod(name: "光焰圆光阶段转换（DEBUG ONLY）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40170)$"],
        userControl: Debugging)]
    public void BurnishedGloryPhaseChange(Event @event, ScriptAccessory accessory)
    {
        _fruPhase = _fruPhase switch
        {
            FruPhase.P1B_FallOfFaith => FruPhase.P1C_BurntStrike,
            _ => FruPhase.P1B_FallOfFaith
        };
        accessory.DebugMsg($"当前阶段为：{_fruPhase}", DebugMode);

        // 删除乐园绝技、罪壤堕的头标
        MarkClear(accessory);

        // 建立罪壤堕设置
        if (_fruPhase != FruPhase.P1B_FallOfFaith) return;
        // 初始化优先级class
        _pd.Init(accessory, "罪壤堕");

        // 标点优先级设置
        List<int> priority = FallOfFaithPriority switch
        {
            FallOfFaithPriorityEnum.T_H_D => [0, 1, 2, 3, 4, 5, 6, 7],
            FallOfFaithPriorityEnum.H_T_D => [2, 3, 0, 1, 4, 5, 6, 7],
            FallOfFaithPriorityEnum.H_T_D_H => [1, 2, 0, 7, 3, 4, 5, 6],
            _ => [2, 3, 0, 1, 4, 5, 6, 7],
        };

        _pd.AddPriorities(priority);
    }

    [ScriptMethod(name: "罪壤堕头标与指路（DB闲固）", eventType: EventTypeEnum.Tether, eventCondition: ["Id:regex:^(00F9|011F)$"],
        userControl: true)]
    public void FallOfFaithMarkAndGuidance(Event @event, ScriptAccessory accessory)
    {
        /*
         * 自用，DB闲固
         * 分为雷组、火组、闲人组
         * 雷：+10，+20，+30，+40（n）
         * 火：+80，+70，+60，+50（8-n）
         * 奇数：+0
         * 偶数：+100
         * 数字从小到大，可标锁1、禁1、锁2、禁2（分别对应左、左上、右、右上）
         */
        // const uint fire = 0x00F9;
        const uint lightning = 0x011F;

        if (_fruPhase != FruPhase.P1B_FallOfFaith) return;
        var tid = @event.TargetId();
        var tidx = accessory.GetPlayerIdIndex(tid);
        var tetherType = @event.Id();

        // 后续可根据myIndex进行指路
        var myIndex = accessory.GetMyIndex();

        lock (_pd)
        {
            // 连线出现先+1
            _pd.AddActionCount();

            // 根据注释加对应数值
            var addNum = _pd.ActionCount % 2 == 0 ? 100 : 0;
            addNum += 10 * (tetherType == lightning ? _pd.ActionCount : 9 - _pd.ActionCount);
            _pd.Priorities[tidx] += addNum;

            switch (_pd.ActionCount)
            {
                case 1:
                    MarkPlayerByIdx(accessory, tidx, tetherType == lightning ? MarkType.Bind1 : MarkType.Stop1, FofCaptainMode);
                    if (tidx == myIndex) FallOfFaithGuidance(accessory, tetherType == lightning ? 4 : 5);
                    break;
                case 2:
                    MarkPlayerByIdx(accessory, tidx, tetherType == lightning ? MarkType.Bind2 : MarkType.Stop2, FofCaptainMode);
                    if (tidx == myIndex) FallOfFaithGuidance(accessory, tetherType == lightning ? 6 : 7);
                    break;
                case 3:
                    // 因为此时有5个闲人，减1消除偏移
                    MarkPlayerByIdx(accessory, tidx, (_pd.FindPriorityIndexOfKey(tidx) - 1) % 2 == 0 ? MarkType.Bind1 : MarkType.Stop1, FofCaptainMode);
                    if (tidx == myIndex) FallOfFaithGuidance(accessory, (_pd.FindPriorityIndexOfKey(tidx) - 1) % 2 == 0 ? 4 : 5);
                    break;
                case 4:
                    // 此时有4个闲人
                    MarkPlayerByIdx(accessory, tidx, _pd.FindPriorityIndexOfKey(tidx) % 2 == 0 ? MarkType.Bind2 : MarkType.Stop2, FofCaptainMode);
                    if (tidx == myIndex) FallOfFaithGuidance(accessory, _pd.FindPriorityIndexOfKey(tidx) % 2 == 0 ? 6 : 7);
                    break;
            }
        }

        if (!_pd.IsActionCountEqualTo(4)) return;
        _pd.ShowPriorities();

        Thread.MemoryBarrier();

        // index 0~3 闲人，index 4~7, 可标锁1、禁1、锁2、禁2（分别对应左、左上、右、右上）
        MarkPlayerByIdx(accessory, _pd.SelectSpecificPriorityIndex(0).Key, MarkType.Attack1, FofCaptainMode);
        MarkPlayerByIdx(accessory, _pd.SelectSpecificPriorityIndex(1).Key, MarkType.Attack2, FofCaptainMode);
        MarkPlayerByIdx(accessory, _pd.SelectSpecificPriorityIndex(2).Key, MarkType.Attack3, FofCaptainMode);
        MarkPlayerByIdx(accessory, _pd.SelectSpecificPriorityIndex(3).Key, MarkType.Attack4, FofCaptainMode);

        var myPriority = _pd.FindPriorityIndexOfKey(myIndex);

        // 雷火线交换提示。取优先级值十位数，若相差1，不换；相差2，换。
        if (myPriority >= 4)
        {
            var myPriVal = _pd.SelectSpecificPriorityIndex(myPriority).Value;
            var ptPriority = myPriority % 2 == 0 ? myPriority + 1 : myPriority - 1;
            var ptPriVal = _pd.SelectSpecificPriorityIndex(ptPriority).Value;
            var subtract = Math.Abs(myPriVal / 10 % 10 - ptPriVal / 10 % 10);
            accessory.DebugMsg($"优先级 (({myPriority}){myPriVal} - ({ptPriority}){ptPriVal}) / 10 % 10 = {subtract}", DebugMode);
            if (subtract != 2) return;
            FallOfFaithGuidance(accessory, myPriority, true);
            accessory.Method.TextInfo($"连线玩家准备交换", 3000, true);
        }
        else
        {
            // 闲人指路
            FallOfFaithGuidance(accessory, myPriority);
        }
    }

    private void FallOfFaithGuidance(ScriptAccessory accessory, int priority, bool swapHint = false)
    {
        const int lightLeft = 4;
        const int fireLeft = 5;
        const int lightRight = 6;
        const int fireRight = 7;

        const int freeLeftMiddle = 0;
        const int freeLeftBottom = 1;
        const int freeRightBottom = 2;
        const int freeRightMiddle = 3;

        // baseMiddlePos为左中坐标，bias为偏置坐标，用于上、下、左。
        var baseMiddlePos = new Vector3(95, 0, 100);
        const float bias = 2f;

        var tpos = priority switch
        {
            lightLeft => baseMiddlePos,
            lightRight => baseMiddlePos.FoldPointHorizon(Center.X),
            freeLeftMiddle => baseMiddlePos - new Vector3(bias / 2, 0, 0),
            freeRightMiddle => (baseMiddlePos - new Vector3(bias / 2, 0, 0)).FoldPointHorizon(Center.X),

            fireLeft => baseMiddlePos - new Vector3(0, 0, bias),
            fireRight => (baseMiddlePos - new Vector3(0, 0, bias)).FoldPointHorizon(Center.X),
            freeLeftBottom => (baseMiddlePos - new Vector3(0, 0, bias)).FoldPointVertical(Center.Z),
            freeRightBottom => (baseMiddlePos - new Vector3(0, 0, bias)).FoldPointHorizon(Center.X).FoldPointVertical(Center.Z),

            _ => baseMiddlePos,
        };

        if (!swapHint)
        {
            var dp = accessory.DrawGuidance(tpos, 0, 20000, $"Usami-罪壤堕初始优先级{priority}指路");
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }

        else
        {
            var swapPos = priority switch
            {
                fireLeft => baseMiddlePos,
                fireRight => baseMiddlePos.FoldPointHorizon(Center.X),
                lightLeft => baseMiddlePos - new Vector3(0, 0, bias),
                lightRight => (baseMiddlePos - new Vector3(0, 0, bias)).FoldPointHorizon(Center.X),
                _ => baseMiddlePos,
            };

            var dp = accessory.DrawGuidance(tpos, swapPos, 0, 20000, $"Usami-罪壤堕交换优先级{priority}指路", isSafe: false);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }
    }

    [ScriptMethod(name: "罪壤堕头标指路移除（DEBUG ONLY）", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:40141"],
        userControl: Debugging, suppress: 10000)]
    public void FallOfFaithRemove(Event @event, ScriptAccessory accessory)
    {
        if (_fruPhase != FruPhase.P1B_FallOfFaith) return;
        MarkClear(accessory);
        accessory.Method.RemoveDraw($"Usami-罪壤堕.*");
    }

    #endregion 罪壤堕

    #endregion P1 绝命战士

    #region P2 希瓦

    [ScriptMethod(name: "---- 《P2：希瓦·米特隆》 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["ActionId:Hello1aya2World"],
        userControl: true)]
    public void SplitLine_Shiva(Event @event, ScriptAccessory accessory)
    {
    }

    #region P2.1 钻石星辰

    [ScriptMethod(name: "钻石星辰阶段转换（DEBUG ONLY）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40197)$"],
        userControl: Debugging)]
    public void DiamondDustPhaseChange(Event @event, ScriptAccessory accessory)
    {
        _fruPhase = FruPhase.P2A_DiamondDust;
        accessory.DebugMsg($"当前阶段为：{_fruPhase}", DebugMode);
    }

    #endregion P2.1 钻石星辰

    #region P2.2 镜子

    [ScriptMethod(name: "镜中奇遇阶段转换（DEBUG ONLY）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40179)$"],
        userControl: Debugging)]
    public void MirrorPhaseChange(Event @event, ScriptAccessory accessory)
    {
        _fruPhase = FruPhase.P2B_Mirror;
        accessory.DebugMsg($"当前阶段为：{_fruPhase}", DebugMode);
    }

    #endregion P2.2 镜子

    #region P2.3 光爆

    [ScriptMethod(name: "光之失控阶段转换（DEBUG ONLY）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40212)$"],
        userControl: Debugging)]
    public void LightRampartPhaseChange(Event @event, ScriptAccessory accessory)
    {
        _fruPhase = FruPhase.P2C_LightRampant;
        _pd.Init(accessory, "光爆");

        List<int> priorities = LightRampageStg switch
        {
            LightRampageStgEnum.Grey9_灰9式 => [20, 42, 2, 24, 13, 33, 11, 31],
            LightRampageStgEnum.Hexagram_正六芒星 => [0, 1, 2, 3, 10, 11, 12, 13],
            _ => [20, 42, 2, 24, 13, 33, 11, 31]
        };

        // 灰9式放泥
        /* 数小在左，数大在右
        *    00 10 20 30 40
        * 0        MT
        * 1     D3    D4
        * 2  H1          ST 
        * 3     D1    D2
        * 4        H2
        */

        // 六芒星式放泥
        /* 数小在左下，数大在右上
        *       00  01  02  03
        * 00        ST  H1
        * 00    MT          H2
        * 10    D1          D4
        * 10        D2  D3
        */

        _pd.AddPriorities(priorities);
        accessory.DebugMsg($"当前阶段为：{_fruPhase}", DebugMode);
    }

    [ScriptMethod(name: "光之失控放泥", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(0177)$"],
        userControl: true)]
    public void LuminousHammerGuidance(Event @event, ScriptAccessory accessory)
    {
        if (_fruPhase != FruPhase.P2C_LightRampant) return;
        lock (_pd)
        {
            var tid = @event.TargetId();
            var tidx = accessory.GetPlayerIdIndex(tid);
            _pd.AddActionCount();
            _pd.AddPriority(tidx, 100);
        }

        if (_pd.ActionCount != 2) return;
        _pd.ShowPriorities();

        // +100将优先级值放到最后两位，小的左(下)，大的右(上)
        var tLeft = _pd.SelectSpecificPriorityIndex(6).Key;
        var tRight = _pd.SelectSpecificPriorityIndex(7).Key;
        var myIndex = accessory.GetMyIndex();
        if ((myIndex != tLeft) && (myIndex != tRight)) return;

        // 此处leftRoute指左/上路线
        List<Vector3> leftRoute = LightRampageStg switch
        {
            LightRampageStgEnum.Grey9_灰9式 =>
                [
                    new Vector3(92, 0, 100),
                    new Vector3(94.6f, 0, 94),
                    new Vector3(100, 0, 92),
                    new Vector3(105.6f, 0, 92),
                    new Vector3(111.3f, 0, 88.7f)
                ],
            LightRampageStgEnum.Hexagram_正六芒星 =>
                [
                    new Vector3(100, 0, 92).RotatePoint(Center, 22.5f.DegToRad()),
                    new Vector3(100, 0, 92).RotatePoint(Center, 67.5f.DegToRad()),
                    new Vector3(100, 0, 92).RotatePoint(Center, 112.5f.DegToRad()),
                    new Vector3(100, 0, 92).RotatePoint(Center, 157.5f.DegToRad()),
                    new Vector3(100, 0, 92).RotatePoint(Center, 157.5f.DegToRad()) + new Vector3(0, 0, 8.5f),
                ],
            _ =>
                [
                    new Vector3(92, 0, 100),
                    new Vector3(94.6f, 0, 94),
                    new Vector3(100, 0, 92),
                    new Vector3(105.6f, 0, 92),
                    new Vector3(111.3f, 0, 88.7f)
                ],
        };

        List<Vector3> rightRoute =
        [
            leftRoute[0].PointCenterSymmetry(Center),
            leftRoute[1].PointCenterSymmetry(Center),
            leftRoute[2].PointCenterSymmetry(Center),
            leftRoute[3].PointCenterSymmetry(Center),
            leftRoute[4].PointCenterSymmetry(Center),
        ];

        for (var i = 0; i < 4; i++)
        {
            var dp1 = accessory.DrawGuidance(leftRoute[i], leftRoute[i + 1], 0, 14000, $"Usami-光爆左{i}{i + 1}", isSafe: myIndex == tLeft);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dp1);
            var dp2 = accessory.DrawGuidance(rightRoute[i], rightRoute[i + 1], 0, 14000, $"Usami-光爆右{i}{i + 1}", isSafe: myIndex == tRight);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dp2);
        }

        var dp = accessory.DrawGuidance(myIndex == tLeft ? leftRoute[0] : rightRoute[0], 0, 8000, $"Usami-光爆初始");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

    }

    [ScriptMethod(name: "光爆球爆炸时间", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40219)$"],
        userControl: true)]
    public void LightBalloonExplode(Event @ev, ScriptAccessory sa)
    {
        if (_fruPhase != FruPhase.P2C_LightRampant) return;
        var sid = @ev.SourceId();
        var dp = sa.DrawCircle(sid, 11, 2500, 2500, $"光球{sid}", true);
        dp.Color = ColorHelper.ColorRed.V4.WithW(4f);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    #endregion P2.3 光爆

    #region P2.4 绝对零度

    [ScriptMethod(name: "绝对零度阶段转换（DEBUG ONLY）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40224)$"],
        userControl: Debugging)]
    public void AbsoluteZeroPhaseChange(Event @event, ScriptAccessory accessory)
    {
        _fruPhase = FruPhase.P2D_AbsoluteZero;
        accessory.DebugMsg($"当前阶段为：{_fruPhase}", DebugMode);
    }

    #endregion P2.4 绝对零度

    #endregion P2 希瓦

    #region P3 盖娅

    [ScriptMethod(name: "---- 《P3：暗之巫女》 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["ActionId:Hello1aya2World"],
        userControl: true)]
    public void SplitLine_Gaia(Event @event, ScriptAccessory accessory)
    {
    }

    #region P3.1 时间压缩

    [ScriptMethod(name: "时间压缩阶段转换（DEBUG ONLY）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40266)$"],
        userControl: Debugging)]
    public void UlRelativityPhaseChange(Event @event, ScriptAccessory accessory)
    {
        _fruPhase = FruPhase.P3A_UltimateRelativity;
        accessory.DebugMsg($"当前阶段为：{_fruPhase}", DebugMode);
    }

    #endregion P3.1 时间压缩

    #region P3.2 地火

    [ScriptMethod(name: "输出启示录信息（DEBUG ONLY）", eventType: EventTypeEnum.NpcYell, eventCondition: ["ActionId:Hello1aya2World"],
        userControl: Debugging)]
    public void ApoMessagePrint(Event @ev, ScriptAccessory sa)
    {
        if (_fruPhase != FruPhase.P3B_Apocalypse) return;
        _apo.ShowMessage();
    }

    [ScriptMethod(name: "启示录阶段转换（DEBUG ONLY）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40269)$"],
        userControl: Debugging)]
    public void ApoPhaseChange(Event @ev, ScriptAccessory sa)
    {
        _fruPhase = FruPhase.P3B_Apocalypse;
        _pd.Init(sa, "启示录");
        _ct.Init(sa, "启示录外部标点");
        MarkClear(sa);
        _apo.Init(sa, _pd);
        _pd.AddPriorities([0, 1, 2, 3, 7, 6, 5, 4]);    // 初始THD优先级，这总没有什么HTDH的幺蛾子吧？数大右，数小左
        sa.DebugMsg($"当前阶段为：{_fruPhase}", DebugMode);
    }

    [ScriptMethod(name: "水分摊类型记录（DEBUG ONLY）", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2461"], userControl: Debugging)]
    public void DarkWaterTypeRecord(Event @ev, ScriptAccessory sa)
    {
        if (_fruPhase != FruPhase.P3B_Apocalypse) return;
        var dur = @ev.DurationMilliseconds();
        var tidx = sa.GetPlayerIdIndex(@ev.TargetId());

        // 10s, 29s, 38s
        const uint waterShort = 10000 - 2000;
        const uint waterMid = 29000 - 2000;
        const uint waterLong = 38000 - 2000;

        lock (_pd)
        {
            if (dur > waterShort)
                _pd.AddPriority(tidx, 10);
            if (dur > waterMid)
                _pd.AddPriority(tidx, 10);
            if (dur > waterLong)
                _pd.AddPriority(tidx, 10);
            _pd.AddActionCount();

            if (_pd.ActionCount == 6)
            {
                _apo.Grouping();
                
            }
        }
    }

    [ScriptMethod(name: "启示录检测外部标点", eventType: EventTypeEnum.Marker, eventCondition: ["Operate:Add", "Id:regex:^(0[1234678]|11)$"],
        userControl: Debugging)]
    public void DarkWaterMarkerFromOut(Event @ev, ScriptAccessory sa)
    {
        if (_fruPhase != FruPhase.P3B_Apocalypse) return;
        if (CaptainMode && ApoCaptainMode) return;
        _events[(int)EventIdx.ApoGrouping].WaitOne();

        lock (_apo)
        {
            _ct.AddCounter();
            var mark = @ev.Id();
            var tid = @ev.TargetId();
            var tidx = sa.GetPlayerIdIndex(tid);

            var groupIdx = mark switch
            {
                0x1 => 0,     // Atk1
                0x2 => 2,     // Atk2
                0x3 => 4,     // Atk3
                0x4 => 6,     // Atk4
                0x6 => 1,     // Bind1
                0x7 => 3,     // Bind2
                0x8 => 5,     // Bind3
                0x11 => 7,    // Square
                _ => 0,
            };

            sa.DebugMsg($"检测到外部标点{mark}给玩家{sa.GetPlayerJobByIndex(tidx)}", DebugMode);

            // 直接修改定义，此时优先值已无意义
            _apo.TempGroup[groupIdx] = new KeyValuePair<int, int>(tidx, 0);

            // 如果不是标8人，作废。
            if (_ct.Number != 8) return;
            sa.DebugMsg($"检测到外部标点标满8人，覆盖_apo.Group分组逻辑。", DebugMode);
            _apo.Group = [.. _apo.TempGroup];
            _events[(int)EventIdx.ApoPreciseGrouping].Set();
            _apo.GroupingFixed = true;
        }
    }

    [ScriptMethod(name: "水分摊范围提示", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2461"], userControl: true)]
    public void DarkWaterRange(Event @ev, ScriptAccessory sa)
    {
        if (_fruPhase != FruPhase.P3B_Apocalypse) return;
        // 程序内部确定分组 -> 检测是否完成精确分组 -> 范围提示
        _events[(int)EventIdx.ApoGrouping].WaitOne();
        _events[(int)EventIdx.ApoPreciseGrouping].WaitOne(1500);

        var tid = @ev.TargetId();
        var tidx = sa.GetPlayerIdIndex(tid);
        var dur = @ev.DurationMilliseconds();

        var isSameGroup = _apo.GetMyGroup() == _apo.GetPlayerGroup(tidx);

        var dp = sa.DrawCircle(tid, 6, (int)dur - 3000, 3000, $"Usami-狂水{tidx}", true);
        dp.Color = isSameGroup ? sa.Data.DefaultSafeColor : sa.Data.DefaultDangerColor;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "碎灵一击范围与分散提示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40288"], userControl: true)]
    public void SpiritTakerHint(Event @ev, ScriptAccessory sa)
    {
        if (_fruPhase != FruPhase.P3B_Apocalypse) return;
        var myIndex = sa.GetMyIndex();
        for (int i = 0; i < 8; i++)
        {
            var dp = sa.DrawCircle(sa.Data.PartyList[i], 5, 1000, 2000, $"Usami-碎灵一击范围");
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        // 左侧人群集合点
        Vector3 leftCenter = new(92, 0, 100);
        Vector3 rightCenter = leftCenter.FoldPointHorizon(Center.X);
        List<Vector3> spreadTargetPos = Enumerable.Repeat(new Vector3(0, 0, 0), 20).ToList();

        List<float> rot = [0f, 0f, 180f, 180f, -60f, -60f, -120f, -120f];

        for (int i = 0; i < 8; i += 2)
        {
            spreadTargetPos[i] = leftCenter.ExtendPoint(rot[i].DegToRad(), 8f);
            spreadTargetPos[i + 1] = spreadTargetPos[i].PointCenterSymmetry(Center);
        }

        sa.DebugMsg($"我在左 {_apo.IsInLeftGroup(myIndex)}，我在右{_apo.IsInRightGroup(myIndex)}", DebugMode);

        for (int i = 0; i < 8; i++)
        {
            // 考虑到可能得预站位问题，废弃掉特定路线分散。
            var isSafe = (_apo.IsInLeftGroup(myIndex) && i % 2 == 0) || (_apo.IsInRightGroup(myIndex) && i % 2 == 1);
            var startPos = spreadTargetPos[i].PointInOutside(i % 2 == 0 ? leftCenter : rightCenter, 6f);
            var dp = sa.DrawGuidance(startPos, spreadTargetPos[i], 0, 3000, $"碎灵一击分散", isSafe: isSafe);
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dp);
        }
    }

    [ScriptMethod(name: "地火分散位置指路", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:40288"], userControl: Debugging, suppress: 10000)]
    public void ApoSpreadGuidance(Event @ev, ScriptAccessory sa)
    {
        if (_fruPhase != FruPhase.P3B_Apocalypse) return;

        var myIndex = sa.GetMyIndex();
        var myGroupSafePosIdx = _apo.IsInCrowd(myIndex) ? -1 : 0;
        // var myGroupSafePosIdx = myIndex is Mt or D1 ? 0 : -1;   // 在精确分组情况下，玩家处于地火安全区的哪一个idx。皇帝可提前确定。
        int mySafeDir;
        // 根据玩家所属位置，选择人群安全区位置。
        if (ApoStg == ApoStgEnum.CrownFirst_MTD1皇帝车头)
        {
            // 若玩家在左组，找位于北的皇帝安全区；否则找位于南的皇帝安全区
            int safeDir = (_apo.SafePoints[1] + 3) % 8;
            int mySafeDirIdx = _apo.IsInLeftGroup(myIndex) ? (safeDir < 4 ? 1 : 3) : (safeDir >= 4 ? 1 : 3);
            if (_apo.IsInCrowd(myIndex))
                mySafeDirIdx -= 1;
            mySafeDir = _apo.SafePoints[mySafeDirIdx];
        }
        else
        {
            // (ApoStg == ApoStgEnum.CrowdFirst_人群车头)
            // 若玩家在左组，找位于北的人群安全区；否则找位于南的人群安全区
            int safeDir = (_apo.SafePoints[0] + 3) % 8;
            int mySafeDirIdx = _apo.IsInLeftGroup(myIndex) ? (safeDir < 4 ? 0 : 2) : (safeDir >= 4 ? 0 : 2);
            if (!_apo.IsInCrowd(myIndex))
                mySafeDirIdx += 1;
            mySafeDir = _apo.SafePoints[mySafeDirIdx];
        }

        if (_apo.GroupingFixed && _apo.IsInCrowd(myIndex))
            myGroupSafePosIdx = _apo.GetMyGroupIdx() / 2 - 1;

        sa.DebugMsg($"玩家的安全方位为{mySafeDir}, 具体分散位置序列为{myGroupSafePosIdx}。", DebugMode);

        for (int i = 0; i < 4; i++)
        {
            List<Vector3> safePos = _apo.GetSafePos(_apo.SafePoints[i]);
            for (int j = 0; j < (i % 2 == 0 ? 3 : 1); j++)
            {
                bool isSafe = mySafeDir == _apo.SafePoints[i];

                if (_apo.GroupingFixed && _apo.IsInCrowd(myIndex))
                    isSafe &= myGroupSafePosIdx == j;

                var dp = sa.DrawStaticCircle(safePos[j], isSafe ? sa.Data.DefaultSafeColor.WithW(3f) : sa.Data.DefaultDangerColor.WithW(3f), 0, 10000, $"Usami-分散位dir{i}-idx{j}", 0.5f);
                sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);

                if (_apo.GroupingFixed && isSafe)
                {
                    var dp0 = sa.DrawGuidance(safePos[j], 0, 10000, $"Usami-分散位dir{i}-idx{j}指引");
                    sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);
                }
            }
        }
    }

    [ScriptMethod(name: "地火类型记录（DEBUG ONLY）", eventType: EventTypeEnum.ObjectEffect, eventCondition: ["Id1:4", "Id2:regex:^(16|64)$"], userControl: Debugging)]
    public void ExaflareTypeRecord(Event @ev, ScriptAccessory sa)
    {
        if (_fruPhase != FruPhase.P3B_Apocalypse) return;

        lock (_apo)
        {
            if (_apo.ActionCount >= 2) return;
            // ----- 正体 -----
            var northApoDir = @ev.SourcePosition().Position2Dirs(Center, 8);
            var rot = @ev.Id2();
            // ----- ---- -----
            _apo.AddActionCount();

            // 记录北方开始点与旋转方向
            if ((northApoDir + 3) % 8 < 4)
            {
                _apo.NorthStartPoint = northApoDir;
                _apo.RotationDir = rot == 0x16 ? 1 : -1;
            }

            if (_apo.ActionCount != 2) return;
            _apo.GetSafeDirs();
        }
    }

    [ScriptMethod(name: "暗夜舞蹈引导指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40181"], userControl: true)]
    public void ApoDarkDanceGuidance(Event @ev, ScriptAccessory sa)
    {
        if (_fruPhase != FruPhase.P3B_Apocalypse) return;
        // 引导是在皇帝位引导，此处可直接取apo中MT与D1所站皇帝位的位置。

        // _apo.SafePoints[1]与[3]是皇帝位所在方位，只需要箭头指出去即可。
        var tpos1 = new Vector3(100, 0, 80).RotatePoint(Center, (_apo.SafePoints[1] * 45f).DegToRad());
        var tpos2 = new Vector3(100, 0, 80).RotatePoint(Center, (_apo.SafePoints[3] * 45f).DegToRad());

        var isTank = IbcHelper.IsTank(sa.Data.Me);

        sa.Method.TextInfo(isTank ? $"准备引导" : $"避开引导", 3000, true);

        var dp1 = sa.DrawGuidance(tpos1.PointInOutside(Center, 13f), tpos1, 0, 3000, $"Usami-暗夜舞蹈引导位1", scale: 3f, isSafe: false);
        var dp2 = sa.DrawGuidance(tpos2.PointInOutside(Center, 13f), tpos2, 0, 3000, $"Usami-暗夜舞蹈引导位2", scale: 3f, isSafe: false);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp1);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);
        var dp11 = sa.DrawGuidance(tpos1.PointInOutside(Center, 13f), tpos1, 3000, 2000, $"Usami-暗夜舞蹈引导位1", scale: 3f, isSafe: isTank);
        var dp22 = sa.DrawGuidance(tpos2.PointInOutside(Center, 13f), tpos2, 3000, 2000, $"Usami-暗夜舞蹈引导位2", scale: 3f, isSafe: isTank);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp11);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp22);

        var dp = sa.DrawTargetNearFarOrder(@ev.SourceId(), 1, false, 8, 8, 3000, 2000, $"Usami-暗夜舞蹈目标");
        dp.Color = isTank ? sa.Data.DefaultSafeColor : sa.Data.DefaultDangerColor;
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "暗夜舞蹈击退方向指引", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:40181", "TargetIndex:1"], userControl: true)]
    public void ApoDarkDanceKnockBackGuidance(Event @ev, ScriptAccessory sa)
    {
        if (_fruPhase != FruPhase.P3B_Apocalypse) return;
        var sid = @ev.SourceId();
        // 将盖娅分成左右
        var isLeft = _apo.IsInLeftGroup(sa.GetMyIndex());
        var dp0 = sa.DrawGuidance(sid, 0, 1500, 3500, $"Usami-击退方向左", -155f.DegToRad().Ccw2Cw(), 3f, isSafe: isLeft);
        var dp1 = sa.DrawGuidance(sid, 0, 1500, 3500, $"Usami-击退方向右", 155f.DegToRad().Ccw2Cw(), 3f, isSafe: !isLeft);
        dp0.Scale = new Vector2(3f, 14f);
        dp1.Scale = new Vector2(3f, 14f);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp1);
    }

    [ScriptMethod(name: "启示录头标移除", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:40282"],
        userControl: Debugging)]
    public void ApoMarkerRemove(Event @event, ScriptAccessory accessory)
    {
        if (_fruPhase != FruPhase.P3B_Apocalypse) return;
        MarkClear(accessory);
    }

    public class Apocalypse
    {
        // ReSharper disable once NullableWarningSuppressionIsUsed
        public ScriptAccessory accessory { get; set; } = null!;
        public int ActionCount { get; set; } = 0;
        public int NorthStartPoint { get; set; } = -1;
        public List<int> SafePoints { get; set; } = [-1, -1, -1, -1];
        public int RotationDir { get; set; } = 0; // 1顺，-1逆
        // ReSharper disable once NullableWarningSuppressionIsUsed
        public List<KeyValuePair<int, int>> Group { get; set; } = null!;
        // ReSharper disable once NullableWarningSuppressionIsUsed
        public List<KeyValuePair<int, int>> TempGroup { get; set; } = null!;
        // ReSharper disable once NullableWarningSuppressionIsUsed
        public PriorityDict Priorities { get; set; } = null!;
        public bool GroupingFixed { get; set; } = false;    // 分散位置是否精确到每个人
        public void Init(ScriptAccessory _accessory, PriorityDict _priorities)
        {
            accessory = _accessory;
            Priorities = _priorities;
            ActionCount = 0;
            NorthStartPoint = -1;
            SafePoints = [-1, -1, -1, -1];
            RotationDir = 0;
        }

        public void AddActionCount()
        {
            ActionCount++;
        }

        public void Grouping()
        {
            TempGroup = Priorities.SelectSmallPriorityIndices(8);

            var str = "";
            str += $"Group初始左：{TempGroup[0].Key}, {TempGroup[2].Key}, {TempGroup[4].Key}, {TempGroup[6].Key}\n";
            str += $"Group初始右：{TempGroup[1].Key}, {TempGroup[3].Key}, {TempGroup[5].Key}, {TempGroup[7].Key}\n";
            accessory.DebugMsg(str, DebugMode);

            if (CaptainMode && ApoCaptainMode)
            {
                // 近战优待调整方式：若ST去右，D2无脑去左；若D2去左，ST无脑去右。
                // const int groupLeft = 0;
                const int groupRight = 1;
                var stGroup = Priorities.FindPriorityIndexOfKey(St) % 2;
                var d2Group = Priorities.FindPriorityIndexOfKey(D2) % 2;

                if (stGroup == d2Group)
                {
                    // 当St与D2在同一组，三近战，需要换位
                    // St在右组时，说明被Mt排挤，D2需与同Buff再次换位。此时D2的idx在后，D2搭档的idx在前。
                    // St在左组时，说明D2被Mt排挤，St需与同Buff再次换位。此时St的idx在前，St搭档的idx在后。
                    var targetIdx = stGroup == groupRight ? Priorities.FindPriorityIndexOfKey(D2) : Priorities.FindPriorityIndexOfKey(St);
                    var offset = stGroup == groupRight ? -1 : 1;
                    (TempGroup[targetIdx + offset], TempGroup[targetIdx]) = (TempGroup[targetIdx], TempGroup[targetIdx + offset]);
                }

                // 交换后对优先级重新处理，制作近战-远程优先级。
                List<bool> inRightGroup = new bool[8].ToList();
                for (int i = 0; i < 8; i++)
                {
                    inRightGroup[TempGroup[i].Key] = i % 2 == groupRight;
                }

                // 去掉优先级中代表水Buff的十位数，重新赋予十位数“左组”与“右组”的概念，
                // 将个位数换为MT-ST-D1-D2, H1-H2-D3-D4的近小远大形式
                for (int i = 0; i < 8; i++)
                    Priorities.Priorities[i] = inRightGroup[i] ? 10 : 0;

                Priorities.AddPriorities([0, 1, 4, 5, 2, 3, 6, 7]);
                // 队长模式安排后
                Priorities.ShowPriorities();

                // 重新设置优先级完毕后，按照特定顺序将元素放入tempGroup
                // 左组1，右组1，左组2，右组2，左组3，右组3，左组4，右组4
                TempGroup = [Priorities.SelectSpecificPriorityIndex(0), Priorities.SelectSpecificPriorityIndex(4),
                            Priorities.SelectSpecificPriorityIndex(1), Priorities.SelectSpecificPriorityIndex(5),
                            Priorities.SelectSpecificPriorityIndex(2), Priorities.SelectSpecificPriorityIndex(6),
                            Priorities.SelectSpecificPriorityIndex(3), Priorities.SelectSpecificPriorityIndex(7)];

                // 由于精确到了每一位玩家的站位，可设置fixed为true
                GroupingFixed = true;

                // 标点
                MarkPlayerByIdx(accessory, TempGroup[0].Key, MarkType.Attack1, ApoCaptainMode);
                MarkPlayerByIdx(accessory, TempGroup[2].Key, MarkType.Attack2, ApoCaptainMode);
                MarkPlayerByIdx(accessory, TempGroup[4].Key, MarkType.Attack3, ApoCaptainMode);
                MarkPlayerByIdx(accessory, TempGroup[6].Key, MarkType.Attack4, ApoCaptainMode);
                MarkPlayerByIdx(accessory, TempGroup[1].Key, MarkType.Bind1, ApoCaptainMode);
                MarkPlayerByIdx(accessory, TempGroup[3].Key, MarkType.Bind2, ApoCaptainMode);
                MarkPlayerByIdx(accessory, TempGroup[5].Key, MarkType.Bind3, ApoCaptainMode);
                MarkPlayerByIdx(accessory, TempGroup[7].Key, MarkType.Square, ApoCaptainMode);

                var str0 = "";
                str0 += $"Group最后左：{TempGroup[0].Key}, {TempGroup[2].Key}, {TempGroup[4].Key}, {TempGroup[6].Key}\n";
                str0 += $"Group最后右：{TempGroup[1].Key}, {TempGroup[3].Key}, {TempGroup[5].Key}, {TempGroup[7].Key}\n";
                accessory.DebugMsg(str0, DebugMode);

                _events[(int)EventIdx.ApoPreciseGrouping].Set();
            }

            // 若fixed为false（未开启指挥模式，或无人进行莫莫式标点），tempGroup的排序方式为：左无，右无，左短水，右短水，左中水，右中水，左长水，右长水。
            Group = [.. TempGroup];

            var strLeft = $"{accessory.GetPlayerJobByIndex(Group[0].Key)},{accessory.GetPlayerJobByIndex(Group[2].Key)},{accessory.GetPlayerJobByIndex(Group[4].Key)},{accessory.GetPlayerJobByIndex(Group[6].Key)}";
            var strRight = $"{accessory.GetPlayerJobByIndex(Group[1].Key)},{accessory.GetPlayerJobByIndex(Group[3].Key)},{accessory.GetPlayerJobByIndex(Group[5].Key)},{accessory.GetPlayerJobByIndex(Group[7].Key)}";
            accessory.DebugMsg($"\n启示录分摊分组{(GroupingFixed ? "(Fixed)" : "")}：\n左组：{strLeft}\n右组：{strRight}", DebugMode);

            _events[(int)EventIdx.ApoGrouping].Set();
        }

        public void GetSafeDirs()
        {
            /*      
            * 将计算出的安全区 (dir+3)%8，0123为北侧，4567为南侧。
            *           3
            *       2       4
            *   1               5
            *       0       6
            *           7
            */

            // 北地火旋转方向相反一格
            var dir = (NorthStartPoint + 8 - RotationDir) % 8;
            bool isNorthSafePoint = (dir + 3) % 8 < 4;

            // 定义人群安全点
            SafePoints[isNorthSafePoint ? 0 : 2] = dir;
            SafePoints[isNorthSafePoint ? 2 : 0] = GetSymmetricPoint(dir);

            // 进一步反向旋转，得到MT/D1位安全点
            SafePoints[1] = (SafePoints[0] + 8 - RotationDir) % 8;
            SafePoints[3] = (SafePoints[2] + 8 - RotationDir) % 8;

            accessory.DebugMsg($"\n人群安全点：北{SafePoints[0]}，南{SafePoints[2]}。\n皇帝安全点：北{SafePoints[1]}，南{SafePoints[3]}。", DebugMode);
        }

        public int GetSymmetricPoint(int dir)
        {
            // 地火顺时针旋转，安全区在逆时针一格。
            return (dir + 8 + 4) % 8;
        }

        public int GetPlayerGroupIdx(int idx)
        {
            // 在Fixed情况下，该函数返回左前、右前、左后、右后的分组。
            return Group.FindIndex(i => i.Key == idx);
        }
        public int GetMyGroupIdx() => GetPlayerGroupIdx(accessory.GetMyIndex());
        public int GetPlayerGroup(int idx) => GetPlayerGroupIdx(idx) % 2;
        public int GetMyGroup() => GetPlayerGroup(accessory.GetMyIndex());

        public bool IsInLeftGroup(int idx)
        {
            return GetPlayerGroupIdx(idx) % 2 == 0;
        }
        public bool IsInRightGroup(int idx)
        {
            return GetPlayerGroupIdx(idx) % 2 == 1;
        }
        public bool IsInCrowd(int idx)
        {
            return GetPlayerGroupIdx(idx) % 4 > 0;
        }

        public List<Vector3> GetSafePos(int dir)
        {
            List<Vector3> safePosList = [new(100, 0, 90.2f), new(104.5f, 0, 80.84f), new(95.5f, 0, 80.84f)];
            for (int i = 0; i < 3; i++)
                safePosList[i] = safePosList[i].RotatePoint(Center, 45f.DegToRad() * dir);
            return safePosList;
        }

        public void ShowMessage()
        {
            var str = "\n ---- [启示录] ----\n";
            str += $"\n人群安全点：北{SafePoints[0]}，南{SafePoints[2]}。\n皇帝安全点：北{SafePoints[1]}，南{SafePoints[3]}。";

            var strLeft = $"{accessory.GetPlayerJobByIndex(Group[0].Key)},{accessory.GetPlayerJobByIndex(Group[2].Key)},{accessory.GetPlayerJobByIndex(Group[4].Key)},{accessory.GetPlayerJobByIndex(Group[6].Key)}";
            var strRight = $"{accessory.GetPlayerJobByIndex(Group[1].Key)},{accessory.GetPlayerJobByIndex(Group[3].Key)},{accessory.GetPlayerJobByIndex(Group[5].Key)},{accessory.GetPlayerJobByIndex(Group[7].Key)}";

            str += $"\n启示录分摊分组{(GroupingFixed ? "(Fixed)" : "")}：\n左组：{strLeft}\n右组：{strRight}";
            accessory.DebugMsg(str, DebugMode);
        }
    }

    #endregion P3.2 地火

    #endregion P3 盖娅

    #region P4 光暗巫女

    [ScriptMethod(name: "---- 《P4：光暗巫女》 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["ActionId:Hello1aya2World"],
        userControl: true)]
    public void SplitLine_Girls(Event @event, ScriptAccessory accessory)
    {
    }

    #region P4.1 光暗龙诗

    [ScriptMethod(name: "光暗龙诗阶段转换（DEBUG ONLY）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40239)$"],
        userControl: Debugging)]
    public void DarklitDragonsongPhaseChange(Event @event, ScriptAccessory accessory)
    {
        _fruPhase = FruPhase.P4A_DarklitDragonsong;
        accessory.DebugMsg($"当前阶段为：{_fruPhase}", DebugMode);
    }

    #endregion P4.1 光暗龙诗

    #region P4.2 时间结晶

    [ScriptMethod(name: "时间结晶阶段转换（DEBUG ONLY）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40240)$"],
        userControl: Debugging)]
    public void CrystallizeTimePhaseChange(Event @event, ScriptAccessory accessory)
    {
        _fruPhase = FruPhase.P4B_CrystallizeTime;
        accessory.DebugMsg($"当前阶段为：{_fruPhase}", DebugMode);
    }

    #endregion P4.2 时间结晶

    #endregion P4 光暗巫女

    #region P5 潘多拉米特隆

    [ScriptMethod(name: "---- 《P5：潘多拉·米特隆》 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["ActionId:Hello1aya2World"],
        userControl: true)]
    public void SplitLine_Pandora(Event @event, ScriptAccessory accessory)
    {
    }

    #region P5.1 光尘之剑

    [ScriptMethod(name: "光尘之剑阶段转换（DEBUG ONLY）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40306)$"],
        userControl: Debugging)]
    public void FulgentBladePhaseChange(Event @event, ScriptAccessory accessory)
    {
        _fruPhase = FruPhase.P5A_FulgentBlade;
        accessory.DebugMsg($"当前阶段为：{_fruPhase}", DebugMode);
    }

    #endregion P5.1 光尘之剑

    #region P5.2 复乐园

    [ScriptMethod(name: "复乐园阶段转换（DEBUG ONLY）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40319)$"],
        userControl: Debugging)]
    public void ParadiseRegainedPhaseChange(Event @event, ScriptAccessory accessory)
    {
        _fruPhase = FruPhase.P5B_ParadiseRegained;
        accessory.DebugMsg($"当前阶段为：{_fruPhase}", DebugMode);
    }

    #endregion P5.2 复乐园

    #region P5.3 星灵之剑

    [ScriptMethod(name: "星灵之剑阶段转换（DEBUG ONLY）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(40316)$"],
        userControl: Debugging)]
    public void PolStrikePhaseChange(Event @event, ScriptAccessory accessory)
    {
        _fruPhase = FruPhase.P5C_PolarizingStrike;
        accessory.DebugMsg($"当前阶段为：{_fruPhase}", DebugMode);
    }

    #endregion P5.3 星灵之剑

    #endregion P5 潘多拉米特隆

    #region 事件枚举
    public enum EventIdx : int
    {
        // _events
        ApoGrouping = 0,
        ApoPreciseGrouping = 1,

        // _recorded
    }

    #endregion
    #region 类函数
    public class PriorityDict
    {
        // ReSharper disable once NullableWarningSuppressionIsUsed
        public ScriptAccessory accessory { get; set; } = null!;
        // ReSharper disable once NullableWarningSuppressionIsUsed
        public Dictionary<int, int> Priorities { get; set; } = null!;
        public string Annotation { get; set; } = "";
        public int ActionCount { get; set; } = 0;

        public void Init(ScriptAccessory _accessory, string annotation, int partyNum = 8)
        {
            accessory = _accessory;
            Priorities = new Dictionary<int, int>();
            for (var i = 0; i < partyNum; i++)
            {
                Priorities.Add(i, 0);
            }
            Annotation = annotation;
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
            var sortedPriorities = SelectMiddlePriorityIndices(0, 8, descending);
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
            var sortedPriorities = SelectMiddlePriorityIndices(0, 8, descending);
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
        public string ShowPriorities()
        {
            var str = $"{Annotation} 优先级字典：\n";
            foreach (var pair in Priorities)
            {
                str += $"Key {pair.Key} ({accessory.GetPlayerJobByIndex(pair.Key)}), Value {pair.Value}\n";
            }
            accessory.DebugMsg(str, DebugMode);
            return str;
        }

        public string PrintAnnotation()
        {
            accessory.DebugMsg($"{Annotation}", DebugMode);
            return Annotation;
        }

        public PriorityDict DeepCopy()
        {
            return JsonConvert.DeserializeObject<PriorityDict>(JsonConvert.SerializeObject(this)) ?? new PriorityDict();
        }

        public void AddActionCount(int count = 1)
        {
            ActionCount += count;
        }

        public bool IsActionCountEqualTo(int times)
        {
            return ActionCount == times;
        }
    }

    public class Counter
    {
        // ReSharper disable once NullableWarningSuppressionIsUsed
        public ScriptAccessory accessory { get; set; } = null!;
        public int Number { get; set; } = 0;
        public bool Enable { get; set; } = true;
        public string Annotation = "";

        public void Init(ScriptAccessory _accessory, string annotation, bool enable = true)
        {
            accessory = _accessory;
            Number = 0;
            Enable = enable;
            Annotation = annotation;
        }

        public string ShowCounter()
        {
            var str = $"{Annotation} 计数器【{(Enable ? "使能" : "不使能")}】：{Number}\n";
            accessory.DebugMsg(str, DebugMode);
            return str;
        }

        public void DisableCounter()
        {
            Enable = false;
            var str = $"禁止 {Annotation} 计数器的数值改变。\n";
            accessory.DebugMsg(str, DebugMode);
        }

        public void EnableCounter()
        {
            Enable = true;
            var str = $"使能 {Annotation} 计数器的数值改变。\n";
            accessory.DebugMsg(str, DebugMode);
        }

        public void AddCounter(int num = 1)
        {
            if (!Enable) return;
            Number += num;
        }

        public void TimesCounter(int num = 1)
        {
            if (!Enable) return;
            Number *= num;
        }
    }

    #endregion 类函数

    #region 标点清除函数

    private static void LocalMarkClear(ScriptAccessory accessory)
    {
        accessory.Method.Mark(0xE000000, MarkType.Attack1, true);
        accessory.Method.Mark(0xE000000, MarkType.Attack2, true);
        accessory.Method.Mark(0xE000000, MarkType.Attack3, true);
        accessory.Method.Mark(0xE000000, MarkType.Attack4, true);
        accessory.Method.Mark(0xE000000, MarkType.Attack5, true);
        accessory.Method.Mark(0xE000000, MarkType.Attack6, true);
        accessory.Method.Mark(0xE000000, MarkType.Attack7, true);
        accessory.Method.Mark(0xE000000, MarkType.Attack8, true);
        accessory.Method.Mark(0xE000000, MarkType.Bind1, true);
        accessory.Method.Mark(0xE000000, MarkType.Bind2, true);
        accessory.Method.Mark(0xE000000, MarkType.Bind3, true);
        accessory.Method.Mark(0xE000000, MarkType.Stop1, true);
        accessory.Method.Mark(0xE000000, MarkType.Stop2, true);
        accessory.Method.Mark(0xE000000, MarkType.Square, true);
        accessory.Method.Mark(0xE000000, MarkType.Circle, true);
        accessory.Method.Mark(0xE000000, MarkType.Cross, true);
        accessory.Method.Mark(0xE000000, MarkType.Triangle, true);
    }

    private static void MarkClear(ScriptAccessory accessory)
    {
        if (!CaptainMode) return;
        if (LocalTest)
        {
            accessory.DebugMsg($"本地测试删除标点。");
            if (LocalStrTest) return;
            LocalMarkClear(accessory);
        }
        else
            accessory.Method.MarkClear();
    }

    private static void MarkPlayerByIdx(ScriptAccessory accessory, int idx, MarkType marker, bool enable = true)
    {
        if (!CaptainMode) return;
        if (!enable) return;
        accessory.DebugMsg($"为{idx}({accessory.GetPlayerJobByIndex(idx)})标上{marker}。", DebugMode && LocalStrTest);
        if (LocalStrTest) return;
        accessory.Method.Mark(accessory.Data.PartyList[idx], marker, LocalTest);
    }

    private static void MarkPlayerById(ScriptAccessory accessory, uint id, MarkType marker, bool enable = true)
    {
        if (!CaptainMode) return;
        if (!enable) return;
        accessory.DebugMsg($"为{accessory.GetPlayerIdIndex(id)}({accessory.GetPlayerJobById(id)})标上{marker}。",
            DebugMode && LocalStrTest);
        if (LocalStrTest) return;
        accessory.Method.Mark(id, marker, LocalTest);
    }

    private static int GetMarkedPlayerIndex(ScriptAccessory accessory, List<MarkType> markerList, MarkType marker)
    {
        return markerList.IndexOf(marker);
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
    public static uint Id(this Event @event)
    {
        return ParseHexId(@event["Id"], out var id) ? id : 0;
    }

    public static uint Id2(this Event @event)
    {
        return ParseHexId(@event["Id2"], out var id) ? id : 0;
    }

    public static uint DataId(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["DataId"]);
    }

    public static uint StatusId(this Event @event)
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

    public static string Message(this Event @event)
    {
        return @event["Message"];
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

    public static bool IsTank(uint id)
    {
        var chara = GetById(id);
        if (chara == null) return false;
        return chara.GetRole() == CombatRole.Tank;
    }
    public static bool IsHealer(uint id)
    {
        var chara = GetById(id);
        if (chara == null) return false;
        return chara.GetRole() == CombatRole.Healer;
    }
    public static bool IsDps(uint id)
    {
        var chara = GetById(id);
        if (chara == null) return false;
        return chara.GetRole() == CombatRole.DPS;
    }
    public static bool AtNorth(uint id, float centerZ)
    {
        var chara = GetById(id);
        if (chara == null) return false;
        return chara.Position.Z <= centerZ;
    }
    public static bool AtSouth(uint id, float centerZ)
    {
        var chara = GetById(id);
        if (chara == null) return false;
        return chara.Position.Z > centerZ;
    }
    public static bool AtWest(uint id, float centerX)
    {
        var chara = GetById(id);
        if (chara == null) return false;
        return chara.Position.X <= centerX;
    }
    public static bool AtEast(uint id, float centerX)
    {
        var chara = GetById(id);
        if (chara == null) return false;
        return chara.Position.X > centerX;
    }
}

public static class DirectionCalc
{
    // 以北为0建立list
    // Game         List    Logic
    // 0            - 4     pi
    // 0.25 pi      - 3     0.75pi
    // 0.5 pi       - 2     0.5pi
    // 0.75 pi      - 1     0.25pi
    // pi           - 0     0
    // 1.25 pi      - 7     1.75pi
    // 1.5 pi       - 6     1.5pi
    // 1.75 pi      - 5     1.25pi
    // Logic = Pi - Game (+ 2pi)

    /// <summary>
    /// 将游戏基角度（以南为0，逆时针增加）转为逻辑基角度（以北为0，顺时针增加）
    /// 算法与Logic2Game完全相同，但为了代码可读性，便于区分。
    /// </summary>
    /// <param name="radian">游戏基角度</param>
    /// <returns>逻辑基角度</returns>
    public static float Game2Logic(this float radian)
    {
        // if (r < 0) r = (float)(r + 2 * Math.PI);
        // if (r > 2 * Math.PI) r = (float)(r - 2 * Math.PI);

        var r = float.Pi - radian;
        r = (r + float.Pi * 2) % (float.Pi * 2);
        return r;
    }

    /// <summary>
    /// 将逻辑基角度（以北为0，顺时针增加）转为游戏基角度（以南为0，逆时针增加）
    /// 算法与Game2Logic完全相同，但为了代码可读性，便于区分。
    /// </summary>
    /// <param name="radian">逻辑基角度</param>
    /// <returns>游戏基角度</returns>
    public static float Logic2Game(this float radian)
    {
        // var r = (float)Math.PI - radian;
        // if (r < Math.PI) r = (float)(r + 2 * Math.PI);
        // if (r > Math.PI) r = (float)(r - 2 * Math.PI);

        return radian.Game2Logic();
    }

    /// <summary>
    /// 适用于旋转，FF14游戏基顺时针旋转为负。
    /// </summary>
    /// <param name="radian"></param>
    /// <returns></returns>
    public static float Cw2Ccw(this float radian)
    {
        return -radian;
    }

    /// <summary>
    /// 适用于旋转，FF14游戏基顺时针旋转为负。
    /// 与Cw2CCw完全相同，为了代码可读性便于区分。
    /// </summary>
    /// <param name="radian"></param>
    /// <returns></returns>
    public static float Ccw2Cw(this float radian)
    {
        return -radian;
    }

    /// <summary>
    /// 输入逻辑基角度，获取逻辑方位（斜分割以正上为0，正分割以右上为0，顺时针增加）
    /// </summary>
    /// <param name="radian">逻辑基角度</param>
    /// <param name="dirs">方位总数</param>
    /// <param name="diagDivision">斜分割，默认true</param>
    /// <returns>逻辑基角度对应的逻辑方位</returns>
    public static int Rad2Dirs(this float radian, int dirs, bool diagDivision = true)
    {
        var r = diagDivision
            ? Math.Round(radian / (2f * float.Pi / dirs))
            : Math.Floor(radian / (2f * float.Pi / dirs));
        r = (r + dirs) % dirs;
        return (int)r;
    }

    /// <summary>
    /// 输入坐标，获取逻辑方位（斜分割以正上为0，正分割以右上为0，顺时针增加）
    /// </summary>
    /// <param name="point">坐标点</param>
    /// <param name="center">中心点</param>
    /// <param name="dirs">方位总数</param>
    /// <param name="diagDivision">斜分割，默认true</param>
    /// <returns>该坐标点对应的逻辑方位</returns>
    public static int Position2Dirs(this Vector3 point, Vector3 center, int dirs, bool diagDivision = true)
    {
        double dirsDouble = dirs;
        var r = diagDivision
            ? Math.Round(dirsDouble / 2 - dirsDouble / 2 * Math.Atan2(point.X - center.X, point.Z - center.Z) / Math.PI) % dirsDouble
            : Math.Floor(dirsDouble / 2 - dirsDouble / 2 * Math.Atan2(point.X - center.X, point.Z - center.Z) / Math.PI) % dirsDouble;
        return (int)r;
    }

    /// <summary>
    /// 以逻辑基弧度旋转某点
    /// </summary>
    /// <param name="point">待旋转点坐标</param>
    /// <param name="center">中心</param>
    /// <param name="radian">旋转弧度</param>
    /// <returns>旋转后坐标点</returns>
    public static Vector3 RotatePoint(this Vector3 point, Vector3 center, float radian)
    {
        // 围绕某点顺时针旋转某弧度
        Vector2 v2 = new(point.X - center.X, point.Z - center.Z);
        var rot = MathF.PI - MathF.Atan2(v2.X, v2.Y) + radian;
        var length = v2.Length();
        return new Vector3(center.X + MathF.Sin(rot) * length, center.Y, center.Z - MathF.Cos(rot) * length);
    }

    /// <summary>
    /// 以逻辑基角度从某中心点向外延伸
    /// </summary>
    /// <param name="center">待延伸中心点</param>
    /// <param name="radian">旋转弧度</param>
    /// <param name="length">延伸长度</param>
    /// <returns>延伸后坐标点</returns>
    public static Vector3 ExtendPoint(this Vector3 center, float radian, float length)
    {
        // 令某点以某弧度延伸一定长度
        return new Vector3(center.X + MathF.Sin(radian) * length, center.Y, center.Z - MathF.Cos(radian) * length);
    }

    /// <summary>
    /// 寻找外侧某点到中心的逻辑基弧度
    /// </summary>
    /// <param name="center">中心</param>
    /// <param name="newPoint">外侧点</param>
    /// <returns>外侧点到中心的逻辑基弧度</returns>
    public static float FindRadian(this Vector3 newPoint, Vector3 center)
    {
        var radian = MathF.PI - MathF.Atan2(newPoint.X - center.X, newPoint.Z - center.Z);
        if (radian < 0)
            radian += 2 * MathF.PI;
        return radian;
    }

    /// <summary>
    /// 将输入点左右折叠
    /// </summary>
    /// <param name="point">待折叠点</param>
    /// <param name="centerX">中心折线坐标点</param>
    /// <returns></returns>
    public static Vector3 FoldPointHorizon(this Vector3 point, float centerX)
    {
        return point with { X = 2 * centerX - point.X };
    }

    /// <summary>
    /// 将输入点上下折叠
    /// </summary>
    /// <param name="point">待折叠点</param>
    /// <param name="centerZ">中心折线坐标点</param>
    /// <returns></returns>
    public static Vector3 FoldPointVertical(this Vector3 point, float centerZ)
    {
        return point with { Z = 2 * centerZ - point.Z };
    }

    /// <summary>
    /// 将输入点中心对称
    /// </summary>
    /// <param name="point">输入点</param>
    /// <param name="center">中心点</param>
    /// <returns></returns>
    public static Vector3 PointCenterSymmetry(this Vector3 point, Vector3 center)
    {
        return point.RotatePoint(center, float.Pi);
    }

    /// <summary>
    /// 将输入点朝某中心点往内/外同角度延伸，默认向内
    /// </summary>
    /// <param name="point">待延伸点</param>
    /// <param name="center">中心点</param>
    /// <param name="length">延伸长度</param>
    /// <param name="isOutside">是否向外延伸</param>>
    /// <returns></returns>
    public static Vector3 PointInOutside(this Vector3 point, Vector3 center, float length, bool isOutside = false)
    {
        Vector2 v2 = new(point.X - center.X, point.Z - center.Z);
        var targetPos = (point - center) / v2.Length() * length * (isOutside ? 1 : -1) + point;
        return targetPos;
    }

    /// <summary>
    /// 获得两点之间距离
    /// </summary>
    /// <param name="point"></param>
    /// <param name="target"></param>
    /// <returns></returns>
    public static float DistanceTo(this Vector3 point, Vector3 target)
    {
        Vector2 v2 = new(point.X - target.X, point.Z - target.Z);
        return v2.Length();
    }

    /// <summary>
    /// 寻找两点之间的角度差，范围0~360deg
    /// </summary>
    /// <param name="basePoint">基准位置</param>
    /// <param name="targetPos">比较目标位置</param>
    /// <param name="center">场地中心</param>
    /// <returns></returns>
    public static float FindRadianDifference(this Vector3 targetPos, Vector3 basePoint, Vector3 center)
    {
        var baseRad = basePoint.FindRadian(center);
        var targetRad = targetPos.FindRadian(center);
        var deltaRad = targetRad - baseRad;
        if (deltaRad < 0)
            deltaRad += float.Pi * 2;
        return deltaRad;
    }

    /// <summary>
    /// 从第三人称视角出发观察某目标是否在另一目标的右侧。
    /// </summary>
    /// <param name="basePoint">基准位置</param>
    /// <param name="targetPos">比较目标位置</param>
    /// <param name="center">场地中心</param>
    /// <returns></returns>
    public static bool IsAtRight(this Vector3 targetPos, Vector3 basePoint, Vector3 center)
    {
        // 从场中看向场外，在右侧
        return targetPos.FindRadianDifference(basePoint, center) < float.Pi;
    }

    /// <summary>
    /// 获取给定数的指定位数
    /// </summary>
    /// <param name="val">给定数值</param>
    /// <param name="x">对应位数，个位为1</param>
    /// <returns></returns>
    public static int GetDecimalDigit(this int val, int x)
    {
        string valStr = val.ToString();
        int length = valStr.Length;

        if (x < 1 || x > length)
        {
            return -1;
        }

        char digitChar = valStr[length - x]; // 从右往左取第x位
        return int.Parse(digitChar.ToString());
    }
}

public static class IndexHelper
{
    /// <summary>
    /// 输入玩家dataId，获得对应的位置index
    /// </summary>
    /// <param name="pid">玩家SourceId</param>
    /// <param name="accessory"></param>
    /// <returns>该玩家对应的位置index</returns>
    public static int GetPlayerIdIndex(this ScriptAccessory accessory, uint pid)
    {
        // 获得玩家 IDX
        return accessory.Data.PartyList.IndexOf(pid);
    }

    /// <summary>
    /// 获得主视角玩家对应的位置index
    /// </summary>
    /// <param name="accessory"></param>
    /// <returns>主视角玩家对应的位置index</returns>
    public static int GetMyIndex(this ScriptAccessory accessory)
    {
        return accessory.Data.PartyList.IndexOf(accessory.Data.Me);
    }

    /// <summary>
    /// 输入玩家dataId，获得对应的位置称呼，输出字符仅作文字输出用
    /// </summary>
    /// <param name="pid">玩家SourceId</param>
    /// <param name="accessory"></param>
    /// <returns>该玩家对应的位置称呼</returns>
    public static string GetPlayerJobById(this ScriptAccessory accessory, uint pid)
    {
        // 获得玩家职能简称，无用处，仅作DEBUG输出
        var idx = accessory.Data.PartyList.IndexOf(pid);
        var str = accessory.GetPlayerJobByIndex(idx);
        return str;
    }

    /// <summary>
    /// 输入位置index，获得对应的位置称呼，输出字符仅作文字输出用
    /// </summary>
    /// <param name="idx">位置index</param>
    /// <param name="fourPeople">是否为四人迷宫</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static string GetPlayerJobByIndex(this ScriptAccessory accessory, int idx, bool fourPeople = false)
    {
        var str = idx switch
        {
            0 => "MT",
            1 => fourPeople ? "H1" : "ST",
            2 => fourPeople ? "D1" : "H1",
            3 => fourPeople ? "D2" : "H2",
            4 => "D1",
            5 => "D2",
            6 => "D3",
            7 => "D4",
            _ => "unknown"
        };
        return str;
    }
}

public static class ColorHelper
{
    public static ScriptColor ColorRed = new ScriptColor { V4 = new Vector4(1.0f, 0f, 0f, 1.0f) };
    public static ScriptColor ColorPink = new ScriptColor { V4 = new Vector4(1f, 0f, 1f, 1.0f) };
    public static ScriptColor ColorCyan = new ScriptColor { V4 = new Vector4(0f, 1f, 0.8f, 1.0f) };
    public static ScriptColor ColorDark = new ScriptColor { V4 = new Vector4(0f, 0f, 0f, 1.0f) };
    public static ScriptColor ColorLightBlue = new ScriptColor { V4 = new Vector4(0.48f, 0.40f, 0.93f, 1.0f) };
    public static ScriptColor ColorWhite = new ScriptColor { V4 = new Vector4(1f, 1f, 1f, 2f) };
    public static ScriptColor ColorYellow = new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 0f, 1.0f) };

}

public static class AssignDp
{
    /// <summary>
    /// 返回箭头指引相关dp
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="ownerObj">箭头起始，可输入uint或Vector3</param>
    /// <param name="targetObj">箭头指向目标，可输入uint或Vector3，为0则无目标</param>
    /// <param name="delay">绘图出现延时</param>
    /// <param name="destroy">绘图消失时间</param>
    /// <param name="name">绘图名称</param>
    /// <param name="rotation">箭头旋转角度</param>
    /// <param name="scale">箭头宽度</param>
    /// <param name="isSafe">使用安全色</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static DrawPropertiesEdit DrawGuidance(this ScriptAccessory accessory,
        object ownerObj, object targetObj, int delay, int destroy, string name, float rotation = 0, float scale = 1f, bool isSafe = true)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(scale);
        dp.Rotation = rotation;
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.Color = isSafe ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;

        switch (ownerObj)
        {
            case uint sid:
                dp.Owner = sid;
                break;
            case Vector3 spos:
                dp.Position = spos;
                break;
            default:
                throw new ArgumentException("ownerObj的目标类型输入错误");
        }

        switch (targetObj)
        {
            case uint tid:
                if (tid != 0) dp.TargetObject = tid;
                break;
            case Vector3 tpos:
                dp.TargetPosition = tpos;
                break;
        }

        return dp;
    }

    public static DrawPropertiesEdit DrawGuidance(this ScriptAccessory accessory,
        object targetObj, int delay, int destroy, string name, float rotation = 0, float scale = 1f, bool isSafe = true)
    => accessory.DrawGuidance(accessory.Data.Me, targetObj, delay, destroy, name, rotation, scale, isSafe);

    // {
    //     return targetObj switch
    //     {
    //         uint uintTarget => accessory.DrawGuidance(accessory.Data.Me, uintTarget, delay, destroy, name, rotation, scale),
    //         Vector3 vectorTarget => accessory.DrawGuidance(accessory.Data.Me, vectorTarget, delay, destroy, name, rotation, scale),
    //         _ => throw new ArgumentException("targetObj 的类型必须是 uint 或 Vector3")
    //     };
    // }

    /// <summary>
    /// 返回扇形左右刀
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="ownerId">起始目标id，通常为boss</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="isLeftCleave">是左刀</param>
    /// <param name="radian">扇形角度</param>
    /// <param name="scale">扇形尺寸</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawLeftRightCleave(this ScriptAccessory accessory, uint ownerId, bool isLeftCleave, int delay, int destroy, string name, float radian = float.Pi, float scale = 60f, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(scale);
        dp.Radian = radian;
        dp.Rotation = isLeftCleave ? float.Pi / 2 : -float.Pi / 2;
        dp.Owner = ownerId;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }

    /// <summary>
    /// 返回扇形前后刀
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="ownerId">起始目标id，通常为boss</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="isFrontCleave">是前刀</param>
    /// <param name="radian">扇形角度</param>
    /// <param name="scale">扇形尺寸</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawFrontBackCleave(this ScriptAccessory accessory, uint ownerId, bool isFrontCleave, int delay, int destroy, string name, float radian = float.Pi, float scale = 60f, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(scale);
        dp.Radian = radian;
        dp.Rotation = isFrontCleave ? 0 : -float.Pi;
        dp.Owner = ownerId;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }

    /// <summary>
    /// 返回距离某对象目标最近/最远的dp
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="ownerId">起始目标id，通常为boss</param>
    /// <param name="orderIdx">顺序，从1开始</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="width">绘图宽度</param>
    /// <param name="length">绘图长度</param>
    /// <param name="isNear">true为最近，false为最远</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="lengthByDistance">长度是否随距离改变</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawTargetNearFarOrder(this ScriptAccessory accessory, uint ownerId, uint orderIdx,
        bool isNear, float width, float length, int delay, int destroy, string name, bool lengthByDistance = false, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.Owner = ownerId;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.CentreResolvePattern =
            isNear ? PositionResolvePatternEnum.PlayerNearestOrder : PositionResolvePatternEnum.PlayerFarestOrder;
        dp.CentreOrderIndex = orderIdx;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.ScaleMode |= lengthByDistance ? ScaleMode.YByDistance : ScaleMode.None;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }

    /// <summary>
    /// 返回距离某坐标位置最近/最远的dp
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="position">特定坐标点</param>
    /// <param name="orderIdx">顺序，从1开始</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="width">绘图宽度</param>
    /// <param name="length">绘图长度</param>
    /// <param name="isNear">true为最近，false为最远</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="lengthByDistance">长度是否随距离改变</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawPositionNearFarOrder(this ScriptAccessory accessory, Vector3 position, uint orderIdx,
        bool isNear, float width, float length, int delay, int destroy, string name, bool lengthByDistance = false, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.Position = position;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.TargetResolvePattern =
            isNear ? PositionResolvePatternEnum.PlayerNearestOrder : PositionResolvePatternEnum.PlayerFarestOrder;
        dp.TargetOrderIndex = orderIdx;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.ScaleMode |= lengthByDistance ? ScaleMode.YByDistance : ScaleMode.None;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }

    /// <summary>
    /// 返回ownerId施法目标的dp
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="ownerId">起始目标id，通常为boss</param>
    /// <param name="width">绘图宽度</param>
    /// <param name="length">绘图长度</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="lengthByDistance">长度是否随距离改变</param>
    /// <param name="name">绘图名称</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawOwnersTarget(this ScriptAccessory accessory, uint ownerId, float width, float length, int delay,
        int destroy, string name, bool lengthByDistance = false, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.Owner = ownerId;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.TargetResolvePattern = PositionResolvePatternEnum.OwnerTarget;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.ScaleMode |= lengthByDistance ? ScaleMode.YByDistance : ScaleMode.None;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }

    /// <summary>
    /// 返回ownerId仇恨相关的dp
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="ownerId">起始目标id，通常为boss</param>
    /// <param name="orderIdx">仇恨顺序，从1开始</param>
    /// <param name="width">绘图宽度</param>
    /// <param name="length">绘图长度</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="lengthByDistance">长度是否随距离改变</param>
    /// <param name="name">绘图名称</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawOwnersEnmityOrder(this ScriptAccessory accessory, uint ownerId, uint orderIdx, float width, float length, int delay, int destroy, string name, bool lengthByDistance = false, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.Owner = ownerId;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.CentreResolvePattern = PositionResolvePatternEnum.OwnerEnmityOrder;
        dp.CentreOrderIndex = orderIdx;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.ScaleMode |= lengthByDistance ? ScaleMode.YByDistance : ScaleMode.None;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }

    /// <summary>
    /// 返回owner与target的dp，可修改 dp.Owner, dp.TargetObject, dp.Scale
    /// </summary>
    /// <param name="ownerId">起始目标id，通常为自己</param>
    /// <param name="targetId">目标单位id</param>
    /// <param name="rotation">绘图旋转角度</param>
    /// <param name="width">绘图宽度</param>
    /// <param name="length">绘图长度</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="lengthByDistance">长度是否随距离改变</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawTarget2Target(this ScriptAccessory accessory, uint ownerId, uint targetId, float width, float length, int delay, int destroy, string name, float rotation = 0, bool lengthByDistance = false, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.Rotation = rotation;
        dp.Owner = ownerId;
        dp.TargetObject = targetId;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.ScaleMode |= lengthByDistance ? ScaleMode.YByDistance : ScaleMode.None;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }

    /// <summary>
    /// 返回画向某目标的扇形绘图
    /// </summary>
    /// <param name="sourceId">起始目标id，通常为自己</param>
    /// <param name="targetId">目标单位id</param>
    /// <param name="radian">扇形角度</param>
    /// <param name="scale">扇形尺寸</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="color">绘图颜色</param>
    /// <param name="rotation">旋转角度</param>
    /// <param name="lengthByDistance">长度是否随距离改变</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawFanToTarget(this ScriptAccessory accessory, uint sourceId, uint targetId, float radian, float scale, int delay, int destroy, string name, Vector4 color, float rotation = 0, bool lengthByDistance = false, bool byTime = false)
    {
        var dp = accessory.DrawTarget2Target(sourceId, targetId, scale, scale, delay, destroy, name, rotation, lengthByDistance, byTime);
        dp.Radian = radian;
        dp.Color = color;
        return dp;
    }

    /// <summary>
    /// 返回owner与target之间的连线dp，使用Line绘制
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="ownerId">起始目标id，通常为自己</param>
    /// <param name="targetId">目标单位id</param>
    /// <param name="scale">线条宽度</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawConnectionBetweenTargets(this ScriptAccessory accessory, uint ownerId,
        uint targetId, int delay, int destroy, string name, float scale = 1f)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(scale);
        dp.Owner = ownerId;
        dp.TargetObject = targetId;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.ScaleMode |= ScaleMode.YByDistance;
        return dp;
    }

    /// <summary>
    /// 返回圆形dp，跟随owner，可修改 dp.Owner, dp.Scale
    /// </summary>
    /// <param name="ownerId">起始目标id，通常为自己或Boss</param>
    /// <param name="scale">圆圈尺寸</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawCircle(this ScriptAccessory accessory, uint ownerId, float scale, int delay, int destroy, string name, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(scale);
        dp.Owner = ownerId;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }

    /// <summary>
    /// 返回环形dp，跟随owner，可修改 dp.Owner, dp.Scale
    /// </summary>
    /// <param name="ownerId">起始目标id，通常为自己或Boss</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="scale">外环实心尺寸</param>
    /// <param name="innerScale">内环空心尺寸</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawDonut(this ScriptAccessory accessory, uint ownerId, float scale, float innerScale, int delay, int destroy, string name, bool byTime = false)
    {
        var dp = accessory.DrawFan(ownerId, float.Pi * 2, 0, scale, innerScale, delay, destroy, name, byTime);
        return dp;
    }

    /// <summary>
    /// 返回静态dp，通常用于指引固定位置。可修改 dp.Position, dp.Rotation, dp.Scale
    /// </summary>
    /// <param name="ownerObj">绘图起始，可输入uint或Vector3</param>
    /// <param name="targetObj">绘图目标，可输入uint或Vector3，为0则无目标</param>
    /// <param name="radian">图形角度</param>
    /// <param name="rotation">旋转角度，以北为0度顺时针</param>
    /// <param name="width">绘图宽度</param>
    /// <param name="length">绘图长度</param>
    /// <param name="color">是Vector4则选用该颜色</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawStatic(this ScriptAccessory accessory, object ownerObj, object targetObj,
        float radian, float rotation, float width, float length, object color, int delay, int destroy, string name)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        switch (ownerObj)
        {
            case uint sid:
                dp.Owner = sid;
                break;
            case Vector3 spos:
                dp.Position = spos;
                break;
        }
        switch (targetObj)
        {
            case uint tid:
                if (tid != 0) dp.TargetObject = tid;
                break;
            case Vector3 tpos:
                dp.TargetPosition = tpos;
                break;
        }
        dp.Radian = radian;
        dp.Rotation = rotation.Logic2Game();
        switch (color)
        {
            case Vector4 clr:
                dp.Color = clr;
                break;
            default:
                dp.Color = accessory.Data.DefaultDangerColor;
                break;
        }
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        return dp;
    }

    /// <summary>
    /// 返回静态圆圈dp，通常用于指引固定位置。
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="center">圆圈中心位置</param>
    /// <param name="color">圆圈颜色</param>
    /// <param name="scale">圆圈尺寸，默认1.5f</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawStaticCircle(this ScriptAccessory accessory, Vector3 center, Vector4 color,
        int delay, int destroy, string name, float scale = 1.5f)
        => accessory.DrawStatic(center, (uint)0, 0, 0, scale, scale, color, delay, destroy, name);
    // {
    //     var dp = accessory.DrawStatic(center, (uint)0, 0, 0, scale, scale, color, delay, destroy, name);
    //     return dp;
    // }

    /// <summary>
    /// 返回静态月环dp，通常用于指引固定位置。
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="center">月环中心位置</param>
    /// <param name="color">月环颜色</param>
    /// <param name="scale">月环外径，默认1.5f</param>
    /// <param name="innerscale">月环内径，默认scale-0.05f</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawStaticDonut(this ScriptAccessory accessory, Vector3 center, Vector4 color,
        int delay, int destroy, string name, float scale, float innerscale = 0)
        => accessory.DrawStatic(center, (uint)0,
        float.Pi * 2, 0, scale, scale, color, delay, destroy, name);

    // {
    //     var dp = accessory.DrawStatic(center, (uint)0, float.Pi * 2, 0, scale, scale, color, delay, destroy, name);
    //     dp.InnerScale = innerscale != 0f ? new Vector2(innerscale) : new Vector2(scale - 0.05f);
    //     return dp;
    // }

    /// <summary>
    /// 返回矩形
    /// </summary>
    /// <param name="ownerId">起始目标id，通常为自己或Boss</param>
    /// <param name="width">矩形宽度</param>
    /// <param name="length">矩形长度</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawRect(this ScriptAccessory accessory, uint ownerId, float width, float length, int delay, int destroy, string name, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.Owner = ownerId;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }

    /// <summary>
    /// 返回扇形
    /// </summary>
    /// <param name="ownerId">起始目标id，通常为自己或Boss</param>
    /// <param name="radian">扇形弧度</param>
    /// <param name="rotation">图形旋转角度</param>
    /// <param name="scale">扇形尺寸</param>
    /// <param name="innerScale">扇形内环空心尺寸</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawFan(this ScriptAccessory accessory, uint ownerId, float radian, float rotation, float scale, float innerScale, int delay, int destroy, string name, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(scale);
        dp.InnerScale = new Vector2(innerScale);
        dp.Radian = radian;
        dp.Rotation = rotation;
        dp.Owner = ownerId;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }

    /// <summary>
    /// 返回击退
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="target">击退源，可输入uint或Vector3</param>
    /// <param name="width">击退绘图宽度</param>
    /// <param name="length">击退绘图长度/距离</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="ownerId">起始目标ID，通常为自己或其他玩家</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static DrawPropertiesEdit DrawKnockBack(this ScriptAccessory accessory, uint ownerId, object target, float length, int delay, int destroy, string name, float width = 1.5f, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.Owner = ownerId;
        switch (target)
        {
            // 根据传入的 tid 类型来决定是使用 TargetObject 还是 TargetPosition
            case uint tid:
                dp.TargetObject = tid; // 如果 tid 是 uint 类型
                break;
            case Vector3 tpos:
                dp.TargetPosition = tpos; // 如果 tid 是 Vector3 类型
                break;
            default:
                throw new ArgumentException("DrawKnockBack的目标类型输入错误");
        }
        dp.Rotation = float.Pi;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        return dp;
    }

    public static DrawPropertiesEdit DrawKnockBack(this ScriptAccessory accessory, object target, float length,
        int delay, int destroy, string name, float width = 1.5f, bool byTime = false)
        => accessory.DrawKnockBack(accessory.Data.Me, target, length, delay, destroy, name, width, byTime);
    // {
    //     return target switch
    //     {
    //         uint uintTarget => accessory.DrawKnockBack(accessory.Data.Me, uintTarget, length, delay, destroy, name, width, byTime),
    //         Vector3 vectorTarget => accessory.DrawKnockBack(accessory.Data.Me, vectorTarget, length, delay, destroy, name, width, byTime),
    //         _ => throw new ArgumentException("target 的类型必须是 uint 或 Vector3")
    //     };
    // }

    /// <summary>
    /// 返回背对
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="target">背对源，可输入uint或Vector3</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="ownerId">起始目标ID，通常为自己或其他玩家</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static DrawPropertiesEdit DrawSightAvoid(this ScriptAccessory accessory, uint ownerId, object target, int delay, int destroy, string name)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Owner = ownerId;
        switch (target)
        {
            // 根据传入的 tid 类型来决定是使用 TargetObject 还是 TargetPosition
            case uint tid:
                dp.TargetObject = tid; // 如果 tid 是 uint 类型
                break;
            case Vector3 tpos:
                dp.TargetPosition = tpos; // 如果 tid 是 Vector3 类型
                break;
            default:
                throw new ArgumentException("DrawSightAvoid的目标类型输入错误");
        }
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        return dp;
    }

    public static DrawPropertiesEdit DrawSightAvoid(this ScriptAccessory accessory, object target, int delay,
        int destroy, string name)
        => accessory.DrawSightAvoid(accessory.Data.Me, target, delay, destroy, name);
    // {
    //     return target switch
    //     {
    //         uint uintTarget => accessory.DrawSightAvoid(accessory.Data.Me, uintTarget, delay, destroy, name),
    //         Vector3 vectorTarget => accessory.DrawSightAvoid(accessory.Data.Me, vectorTarget, delay, destroy, name),
    //         _ => throw new ArgumentException("target 的类型必须是 uint 或 Vector3")
    //     };
    // }

    /// <summary>
    /// 返回多方向延伸指引
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="owner">分散源</param>
    /// <param name="extendDirs">分散角度</param>
    /// <param name="myDirIdx">玩家对应角度idx</param>
    /// <param name="width">指引箭头宽度</param>
    /// <param name="length">指引箭头长度</param>
    /// <param name="delay">绘图出现延时</param>
    /// <param name="destroy">绘图消失时间</param>
    /// <param name="name">绘图名称</param>
    /// <param name="colorPlayer">玩家对应箭头指引颜色</param>
    /// <param name="colorNormal">其他玩家对应箭头指引颜色</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static List<DrawPropertiesEdit> DrawExtendDirection(this ScriptAccessory accessory, object owner,
        List<float> extendDirs, int myDirIdx, float width, float length, int delay, int destroy, string name,
        Vector4 colorPlayer, Vector4 colorNormal)
    {
        List<DrawPropertiesEdit> dpList = [];
        switch (owner)
        {
            case uint sid:
                for (var i = 0; i < extendDirs.Count; i++)
                {
                    var dp = accessory.DrawGuidance(owner, sid, delay, destroy, $"{name}{i}", extendDirs[i], width);
                    dp.Color = i == myDirIdx ? colorPlayer : colorNormal;
                    dpList.Add(dp);
                }
                break;
            case Vector3 spos:
                for (var i = 0; i < extendDirs.Count; i++)
                {
                    var dp = accessory.DrawGuidance(spos, spos.ExtendPoint(extendDirs[i], length), delay, destroy,
                        $"{name}{i}", 0, width);
                    dp.Color = i == myDirIdx ? colorPlayer : colorNormal;
                    dpList.Add(dp);
                }
                break;
            default:
                throw new ArgumentException("DrawExtendDirection的目标类型输入错误");
        }

        return dpList;
    }

    /// <summary>
    /// 返回多地点指路指引列表
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="positions">地点位置</param>
    /// <param name="delay">绘图出现延时</param>
    /// <param name="destroy">绘图消失时间</param>
    /// <param name="name">绘图名称</param>
    /// <param name="colorPosPlayer">对应位置标记行动颜色</param>
    /// <param name="colorPosNormal">对应位置标记准备颜色</param>
    /// <param name="colorGo">指路出发箭头颜色</param>
    /// <param name="colorPrepare">指路准备箭头颜色</param>
    /// <returns>dpList中的三个List：位置标记，玩家指路箭头，地点至下个地点的指路箭头</returns>
    public static List<List<DrawPropertiesEdit>> DrawMultiGuidance(this ScriptAccessory accessory,
        List<Vector3> positions, List<int> delay, List<int> destroy, string name,
        Vector4 colorGo, Vector4 colorPrepare, Vector4 colorPosNormal, Vector4 colorPosPlayer)
    {
        List<List<DrawPropertiesEdit>> dpList = [[], [], []];
        for (var i = 0; i < positions.Count; i++)
        {
            var dpPos = accessory.DrawStaticCircle(positions[i], colorPosPlayer, delay[i], destroy[i], $"{name}pos{i}");
            dpList[0].Add(dpPos);
            var dpGuide = accessory.DrawGuidance(positions[i], colorGo, delay[i], destroy[i], $"{name}guide{i}");
            dpList[1].Add(dpGuide);
            if (i == positions.Count - 1) break;
            var dpPrep = accessory.DrawGuidance(positions[i], positions[i + 1], delay[i], destroy[i], $"{name}prep{i}");
            dpList[2].Add(dpPrep);
        }
        return dpList;
    }

    /// <summary>
    /// 外部用调试模式
    /// </summary>
    /// <param name="str"></param>
    /// <param name="debugMode"></param>
    /// <param name="accessory"></param>
    public static void DebugMsg(this ScriptAccessory accessory, string str, bool debugMode = false)
    {
        if (!debugMode)
            return;
        accessory.Method.SendChat($"/e [DEBUG] {str}");
    }

    /// <summary>
    /// 将List内信息转换为字符串。
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="myList"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static string BuildListStr<T>(this ScriptAccessory accessory, List<T> myList)
    {
        return string.Join(", ", myList.Select(item => item?.ToString() ?? ""));
    }
}

#endregion 函数集
