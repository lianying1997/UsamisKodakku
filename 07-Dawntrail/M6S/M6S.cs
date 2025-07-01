using System;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent.Struct;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Data;
using KodakkuAssist.Module.Draw.Manager;
using KodakkuAssist.Module.GameOperate;
using KodakkuAssist.Module.GameEvent.Types;
using KodakkuAssist.Extensions;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ECommons;
using System.Numerics;
using Newtonsoft.Json;
using System.Linq;
using System.ComponentModel;
using Dalamud.Utility.Numerics;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.MathHelpers;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Vfx;

namespace UsamisKodakku.Script._07_DawnTrail.M6S;

[ScriptType(name: Name, territorys: [1259], guid: "4f5decb5-4a78-4e83-96c8-09678761a751",
    version: Version, author: "Usami", note: NoteStr, updateInfo: UpdateInfo)]

// ^(?!.*((武僧|机工士|龙骑士|武士|忍者|蝰蛇剑士|钐镰客|舞者|吟游诗人|占星术士|贤者|学者|(朝日|夕月)小仙女|炽天使|白魔法师|战士|骑士|暗黑骑士|绝枪战士|绘灵法师|黑魔法师|青魔法师|召唤师|宝石兽|亚灵神巴哈姆特|亚灵神不死鸟|迦楼罗之灵|泰坦之灵|伊弗利特之灵|后式自走人偶)\] (Used|Cast))).*35501.*$
// ^\[\w+\|[^|]+\|E\]\s\w+

public class M6S
{
    const string NoteStr =
    """
    v0.0.0.5
    默认配置为CnServer攻略
    如需使用Game8配置，请于用户设置中调整。
    待完善。
    """;

    private const string Name = "M6S [零式阿卡狄亚 中量级2]";
    private const string Version = "0.0.0.5";

    private const string UpdateInfo =
        """
        1. 更改一些配置项说明文字至国服翻译。
        """;

    private const bool Debugging = false;

    private static List<string> _role = ["MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4"];

    [UserSetting("整体策略")]
    public static StgEnum GlobalStrat { get; set; } = StgEnum.CnServer;
    public enum StgEnum
    {
        Game8,
        CnServer,
    }
    
    [UserSetting("仙人掌四角策略")]
    public static QuickSandStgEnum QuickSandStrat { get; set; } = QuickSandStgEnum.BattleFieldBasis_场基;
    public enum QuickSandStgEnum
    {
        BattleFieldBasis_场基,
        DangerCornerBasis_危险基,
    }

    private enum M6SPhase
    {
        Init,
        P1A_DoubleStyle,    // P1A 双画画
        P2A_Sand,           // P2A 沙漠
        P2B_FlyingBomb,     // P2B 沙漠飞行炸弹
        P3A_Mobs,           // P3A 小怪
        P4A_Bridge,         // P4A 桥 - 火雷
        P4B_Light,          // P4B 桥 - 雷云
        P4C_Lava,           // P4C 桥 - 岩浆
        P4D_FlyLava,        // P4D 桥 - 岩浆飞行
        P5A_Final           // P5A 终
    }
    
    private static readonly Vector3 Center = new Vector3(100, 0, 100);
    private static readonly Vector3 CenterRiver = new Vector3(100, 0, 96);
    private M6SPhase _M6sPhase = M6SPhase.Init;
    private static readonly Random Random = new();
    private volatile List<bool> _bools = new bool[20].ToList();
    private List<int> _numbers = Enumerable.Repeat(0, 20).ToList();
    private static List<ManualResetEvent> _events = Enumerable
        .Range(0, 20)
        .Select(_ => new ManualResetEvent(false))
        .ToList();

    // 岛：右上、下、左上
    private static readonly List<Vector3> IslandPos = [new(112, 0, 90), new(97, 0, 113), new(89, 0, 89)];
    // 桥：云在右上，云在下，云在左上（左下，上，右下）
    private static readonly List<Vector3> BridgePos = [new(91.5f, 0, 103f), new(100.76f, 0, 91f), new(107.1f, 0, 105.4f)];
    // 左上岛的塔：69~78      10 座
    // 以面向场中为基准，70 71 73 75 77 靠左，69 72 74 76 78 靠右
    // 右上岛的塔：79~88      10 座
    // 以面向场中未基准，80 82 84 86 88 靠左，79 81 83 85 87 靠右
    // 正下岛的塔：89~96      8 座
    // 南八塔中，89MT 90D3 91ST 92D4 93H1 94D1 95H2 96D2（国服）
    // 南八塔中，89D3 90H1 91MT 92D2 93H2 94D1 95D4 96ST（Game8）
    private static readonly List<Vector3> TowerPosList =
    [
        new(83, 0, 91), new(93, 0, 89), new(92, 0, 96), new(83, 0, 102), new(94, 0, 84),
        new(83, 0, 88), new(90, 0, 89), new(83, 0, 95), new(90, 0, 97.5f), new(83, 0, 104),

        new(110, 0, 93), new(117, 0, 92), new(109, 0, 97), new(115, 0, 105), new(110, 0, 83),
        new(117, 0, 85), new(110, 0, 91), new(117, 0, 96), new(111, 0, 100), new(117, 0, 106),

        new(100, 0, 108), new(85, 0, 114), new(98, 0, 107), new(112, 0, 106),
        new(92, 0, 110), new(91, 0, 117), new(107, 0, 111), new(105, 0, 117)
    ];
    
    private static Towers _towers = new();
    private static PriorityDict _pd = new();
    private static bool _initHint = false;
    
    public void Init(ScriptAccessory sa)
    {
        _M6sPhase = M6SPhase.Init;
        InitParams();
        sa.Log.Debug($"M6S {Version} 脚本已刷新。");
        sa.Method.RemoveDraw(".*");
        _initHint = false;
    }

    private void InitParams()
    {
        _bools = new bool[20].ToList();
        _numbers = Enumerable.Repeat(0, 20).ToList();
        _events = Enumerable
            .Range(0, 20)
            .Select(_ => new ManualResetEvent(false))
            .ToList();
    }

    #region 测试项
    
    [ScriptMethod(name: "---- 测试项 ----", eventType: EventTypeEnum.NpcYell, 
        eventCondition: ["HelloayaWorld:asdf"], userControl: Debugging)]
    public void Debug_SplitLine(Event ev, ScriptAccessory sa)
    {
    }

    [ScriptMethod(name: "测试项 雷火位置", eventType: EventTypeEnum.Chat, 
        eventCondition: ["HelloayaWorld:asdf"], userControl: Debugging)]
    public void Debug_StackOrSpread(Event ev, ScriptAccessory sa)
    {
        List<Vector3> points =
        [
            // 左上就位：MT/H1/D1/D2分摊，ST/H2/D3/D4分摊
            // new(98.5f, 0, 98.5f), new(85f, 0, 108f),
            // // 右下就位：MT/H1/D1/D2分摊，ST/H2/D3/D4分摊
            // new(101.5f, 0, 101.5f), new(87.5f, 0, 105.5f),
            // // 左上就位：MT/ST/H1/H2/D1/D2/D3/D4分散,
            // new(96.5f, 0, 83), new(96.5f, 0, 110.5f), new(83f, 0, 96.5f), new(110.5f, 0, 96.5f),
            // new(84.8f, 0, 112.7f), new(108.3f, 0, 112.6f), new(83f, 0, 83f), new(110.5f, 0, 83f),
            // // 右下就位：MT/ST/H1/H2/D1/D2/D3/D4分散,
            new(103f, 0, 91.5f), new(103f, 0, 117f), new(90.5f, 0, 103.65f), new(117f, 0, 103.5f),
            new(89.5f, 0, 117f), new(116.8f, 0, 116.8f), new(89.5f, 0, 89.5f), new(117f, 0, 90f),
        ];
        foreach (var point in points)
        {
            var dp = sa.DrawStaticCircle(point, sa.Data.DefaultSafeColor, 0, 2000, "a");
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
        }
    }
    
    [ScriptMethod(name: "测试项 获得雷云位置与面向", eventType: EventTypeEnum.NpcYell, 
        eventCondition: ["HelloayaWorld:asdf"], userControl: Debugging)]
    public void Debug_CloudPosAndRotation(Event ev, ScriptAccessory sa)
    {
        GetCloudInfo(sa);
    }
    
    [ScriptMethod(name: "测试项 计算角度与面向", eventType: EventTypeEnum.NpcYell, 
        eventCondition: ["HelloayaWorld:asdf"], userControl: Debugging)]
    public void Debug_CalcDir(Event ev, ScriptAccessory sa)
    {
        var rad = 96f.DegToRad();
        var cloudRotDir = rad.Rad2Dirs(6, false);
        sa.Log.Debug($"{cloudRotDir}");
    }
    
    [ScriptMethod(name: "测试项 塔范围", eventType: EventTypeEnum.NpcYell, 
        eventCondition: ["HelloayaWorld:asdf"], userControl: Debugging)]
    public void Debug_TowerRegion(Event ev, ScriptAccessory sa)
    {
        var dp = sa.DrawStaticCircle(TowerPosList[0], sa.Data.DefaultSafeColor, 0, 2000, "a", 3f);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
    }
    
    [ScriptMethod(name: "测试项 找到主视角塔", eventType: EventTypeEnum.NpcYell, 
        eventCondition: ["HelloayaWorld:asdf"], userControl: Debugging)]
    public void Debug_DrawMyTower(Event ev, ScriptAccessory sa)
    {
        var tower = _towers.GetTowerByPlayerIdx(sa.GetMyIndex());
        sa.Log.Debug($"玩家 {sa.GetPlayerJobByIndex(sa.GetMyIndex())} 的塔为 {tower.TowerIdx}, 在岛 {tower.IslandIdx}");
        var dp = sa.DrawStaticCircle(tower.TowerPos, sa.Data.DefaultSafeColor, 0, 2000, "a", 3f);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
    }
    
    #endregion 测试项

    #region P1 开场
    
    [ScriptMethod(name: "---- P1 开场 ----", eventType: EventTypeEnum.NpcYell, 
        eventCondition: ["HelloayaWorld:asdf"], userControl: true)]
    public void P1_SplitLine(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "策略与身份提示", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(42684)$"], userControl: true)]
    public void 策略与身份提示(Event ev, ScriptAccessory sa)
    {
        if (_initHint) return;
        _initHint = true;
        var myIndex = sa.Data.PartyList.IndexOf(sa.Data.Me); 
        List<string> role = ["MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4"];
        sa.Method.TextInfo(
            $"你是【{role[myIndex]}】，使用策略为【{(GlobalStrat == StgEnum.CnServer ? "国服" : "日野")}】，\n" +
            $"沙漠阶段四角仙人掌采用【{(QuickSandStrat == QuickSandStgEnum.BattleFieldBasis_场基 ? "场基" : "危险基")}】，\n" +
            $"若有误请及时调整。", 5000);
    }

    [ScriptMethod(name: "WingMark引起的阶段转换", eventType: EventTypeEnum.StartCasting, 
        eventCondition: ["ActionId:42614"], userControl: Debugging)]
    public void P1A_PhaseChange(Event ev, ScriptAccessory sa)
    {
        if (_M6sPhase == M6SPhase.P4C_Lava)
        {
            _M6sPhase = M6SPhase.P4D_FlyLava;
            _towers.RefreshParam(sa, true);
        }
        else
        {
            if (_M6sPhase != M6SPhase.Init & _M6sPhase != M6SPhase.P4D_FlyLava) return;
            _M6sPhase = M6SPhase.P1A_DoubleStyle;
        }

        InitParams();
        sa.Log.Debug($"当前阶段为：{_M6sPhase}");
    }
    
    [ScriptMethod(name: "P1A 色彩狂热 死刑", eventType: EventTypeEnum.StartCasting, 
        eventCondition: ["ActionId:regex:^(4264[12])$"], userControl: true)]
    public void P1A_ColorRiot(Event ev, ScriptAccessory sa)
    {
        if (sa.Data.PartyList.Count != 8) return;
        var myIndex = sa.GetMyIndex();
        if (myIndex > 1) return;
        
        // const uint blueClose = 42641;
        const uint redClose = 42642;
        const uint redBuff = 4451;
        const uint blueBuff = 4452;
        
        var aid = ev.ActionId;
        var flag = -1 * myIndex;
        // MT=0, ST=1, MT先近
        // 规定flag>0时，近；flag<0时，远
        
        // 判断自身
        var myObj = sa.Data.MyObject;
        if (myObj == null) return;
        var partnerObj = (IPlayerCharacter?)sa.GetById(sa.Data.PartyList[myIndex == 0 ? 1 : 0]);
        if (partnerObj == null) return;
        
        // 令红Buff>0，同时考虑玩家自身与搭档。另外，若出现双同色Buff情况，让玩家的命更值钱，权重上升。
        flag += (myObj.HasStatus(redBuff) ? 1 : 0) * 10 + (myObj.HasStatus(blueBuff) ? -1 : 0) * 10;
        flag += (partnerObj.HasStatus(redBuff) ? 1 : 0) * 5 + (partnerObj.HasStatus(blueBuff) ? -1 : 0) * 5;
        flag *= aid == redClose ? -1 : 1;   // redClose是指Boss会赋于红Buff，需要红Buff远离
        sa.Log.Debug($"死刑 Flag: {flag}");

        sa.Method.TextInfo(flag >= 0 ? $"【靠近】死刑" : $"【远离】死刑", 5000, true);
    }
    
    [ScriptMethod(name: "---- P1A 黏黏慕斯怪初始化 ----", eventType: EventTypeEnum.StartCasting, 
        eventCondition: ["ActionId:42645"], userControl: Debugging)]
    public void StickyMousseInit(Event ev, ScriptAccessory sa)
    {
        // Init Param at StartCasting 42645
        _pd.Init(sa, "StickyMousse");
        _pd.AddPriorities([0, 0, 6, 3, 5, 4, 7, 2]);
        //    D3 7  D4 2
        // H1 6       H2 3
        //    D1 5  D2 4
        sa.Log.Debug($"检测到Sticky Mousse，优先级初始化");
    }
    
    [ScriptMethod(name: "P1A 黏黏慕斯怪", eventType: EventTypeEnum.ActionEffect, 
        eventCondition: ["ActionId:42646", "TargetIndex:1"], userControl: true)]
    public void StickyMousseTarget(Event ev, ScriptAccessory sa)
    {
        // ActionEffect 42646, Index 1
        var tidx = sa.GetPlayerIdIndex(ev.TargetId);
        int targetIdx = -1;
        lock (_pd)
        {
            _pd.AddActionCount();
            _pd.AddPriority(tidx, 1000);
            if (_pd.ActionCount != 2) return;

            var myIndex = sa.GetMyIndex();
            // 玩家是分摊目标，不执行后续
            if (_pd.Priorities[myIndex] > 1000) return;

            for (int i = 0; i < 2; i++)
            {
                var tKey = _pd.SelectSpecificPriorityIndex(i, true).Key;
                var tVal = _pd.Priorities[tKey].GetDecimalDigit(1);
                var myVal = _pd.Priorities[myIndex];
                var distance = 0;
                // 计算距离
                if (myIndex != 0 & myIndex != 1)
                {
                    distance = 3 - Math.Abs(Math.Abs(tVal - myVal) - 3);
                    _pd.AddPriority(tKey, distance * 100);
                }
                
                // 计算顺逆
                var cwIdx = (tVal - myVal + 6 - (myIndex is 0 or 1 ? 2 : 0)) % 6;
                _pd.AddPriority(tKey, cwIdx * 10);
                sa.Log.Debug($"tKey: {tKey}, tVal: {tVal}, myVal: {myVal}");
                sa.Log.Debug($"玩家{sa.GetPlayerJobByIndex(myIndex)}与{sa.GetPlayerJobByIndex(tKey)}的距离为{distance}，顺时针顺位为{cwIdx}，对方优先值为{_pd.Priorities[tKey]}");
            }

            // MT找两个目标中较大的（降序idx0），ST与人群找较小的（降序idx1）
            targetIdx = _pd.SelectSpecificPriorityIndex(myIndex == 0 ? 0 : 1, true).Key;
            sa.Log.Debug(
                $"据决策，玩家{sa.GetPlayerJobByIndex(myIndex)}的分摊对象为{sa.GetPlayerJobByIndex(targetIdx)}({_pd.Priorities[targetIdx]})");
        }

        var dp = sa.DrawGuidance((ulong)sa.Data.PartyList[targetIdx], 0, 4000, $"粘性炸弹目标");
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }
    
    [ScriptMethod(name: "P1A 双手涂鸦初始化", eventType: EventTypeEnum.StartCasting, 
        eventCondition: ["ActionId:regex:^(4263[57])$"], userControl: Debugging)]
    public void ColorClashStackRecord(Event ev, ScriptAccessory sa)
    {
        const uint partnerStack = 42637;
        // const uint partyStack = 42635;
        _pd.Init(sa, "飞行分摊", 5);
        _pd.AddPriority(4, ev.ActionId == partnerStack ? 2 : 4);
        // 前四个index留作方位
        sa.Log.Debug($"记录分摊人数：{_pd.Priorities[4]}人分摊");
    }
    
    [ScriptMethod(name: "P1A 双手涂鸦指路", eventType: EventTypeEnum.Tether, 
        eventCondition: ["Id:regex:^(013F|0140)$"], userControl: true)]
    public void ColorClashTetherRecord(Event ev, ScriptAccessory sa)
    {
        if (_M6sPhase != M6SPhase.P1A_DoubleStyle) return;
        const uint heavenBomb = 0x47A1;
        const uint paintBomb = 0x47A0;
        const uint succubus = 0x47A5;
        const uint morbol = 0x47A4;

        var tetherObj = sa.GetById(ev.SourceId);
        if (tetherObj == null) return;
        List<uint> tetherObjs = [paintBomb, heavenBomb, morbol, succubus];
        
        lock (_pd)
        {
            var tetherDataId = tetherObj.DataId;
            if (!tetherObjs.Contains(tetherDataId)) return;
            var tetherObjDir = tetherObj.Position.Position2Dirs(Center, 4, true);

            switch (tetherDataId)
            {
                // 前四个index对应安全象限，从右上开始，顺时针增加，即1~4象限，用index0~3表示
                // 危险的象限会增加数值，取最小值对应的index即为安全区。
                case paintBomb:
                    // 固定
                    sa.Log.Debug($"检测到爆弹，2、3象限危险");
                    _pd.AddPriority(2, 10);
                    _pd.AddPriority(3, 10);
                    break;
                case heavenBomb:
                    // 固定
                    sa.Log.Debug($"检测到飞弹，0、1象限危险");
                    _pd.AddPriority(0, 10);
                    _pd.AddPriority(1, 10);
                    break;
                case succubus:
                {
                    var safeDir2 = (tetherObjDir + 4 - 1) % 4;
                    sa.Log.Debug($"检测到梦魔，{tetherObjDir}、{safeDir2}象限危险");
                    _pd.AddPriority(tetherObjDir, 10);
                    _pd.AddPriority(safeDir2, 10);
                    break;
                }
                case morbol:
                {
                    var safeDir1 = (tetherObjDir + 4 + 1) % 4;
                    var safeDir2 = (tetherObjDir + 4 + 2) % 4;
                    sa.Log.Debug($"检测到魔界花，{safeDir1}、{safeDir2}象限危险");
                    _pd.AddPriority(safeDir1, 10);
                    _pd.AddPriority(safeDir2, 10);
                    break;
                }
                default:
                    return;
            }
            
            // 直到获得唯一一个安全区，停止记录并输出结果
            var posDictSecond = _pd.SelectSpecificPriorityIndex(1);
            if (posDictSecond.Value == 0) return;
            sa.Log.Debug($"{_pd.ShowPriorities(false)}");

            List<Vector3> safePosList = Enumerable.Repeat(new Vector3(0, 0, 0), 20).ToList();

            // 飞行位置
            safePosList[2] = new Vector3(112, 0, 88);
            safePosList[0] = safePosList[2].RotatePoint(Center, 180f.DegToRad());
            safePosList[1] = safePosList[2].RotatePoint(Center, 270f.DegToRad());
            safePosList[3] = safePosList[2].RotatePoint(Center, 90f.DegToRad());

            safePosList[4] = new Vector3(119, 0, 92);   // 第0象限 左
            safePosList[5] = new Vector3(108, 0, 81);   // 第0象限 右
            safePosList[6] = new Vector3(112, 0, 88);   // 第0象限 前
            safePosList[7] = new Vector3(118, 0, 82);   // 第0象限 后
            
            safePosList[8] = safePosList[6].FoldPointVertical(Center.Z);    // 第1象限 左
            safePosList[9] = safePosList[5].FoldPointVertical(Center.Z);    // 第1象限 右
            safePosList[10] = safePosList[7].FoldPointVertical(Center.Z);   // 第1象限 前
            safePosList[11] = safePosList[8].FoldPointVertical(Center.Z);   // 第1象限 后
            
            safePosList[12] = safePosList[5].PointCenterSymmetry(Center);   // 第2象限 左
            safePosList[13] = safePosList[6].PointCenterSymmetry(Center);   // 第2象限 右
            safePosList[14] = safePosList[7].PointCenterSymmetry(Center);   // 第2象限 前
            safePosList[15] = safePosList[8].PointCenterSymmetry(Center);   // 第2象限 后
            
            safePosList[16] = safePosList[5].PointCenterSymmetry(Center);   // 第3象限 左
            safePosList[17] = safePosList[6].PointCenterSymmetry(Center);   // 第3象限 右
            safePosList[18] = safePosList[7].PointCenterSymmetry(Center);   // 第3象限 前
            safePosList[19] = safePosList[8].PointCenterSymmetry(Center);   // 第3象限 后
            
            // 根据玩家身份建立安全对角位置
            var isPartnerStack = _pd.Priorities[4] == 2;
            var basePosIdx = sa.GetMyIndex() switch
            {
                0 => isPartnerStack ? GlobalStrat == StgEnum.CnServer ? 0 : 2 : 0,  // MT CN左，G8前，44分摊左
                1 => isPartnerStack ? GlobalStrat == StgEnum.CnServer ? 1 : 1 : 1,  // ST CN右，G8右，44分摊右
                2 => isPartnerStack ? GlobalStrat == StgEnum.CnServer ? 2 : 0 : 0,  // H1 CN前，G8左，44分摊左
                3 => isPartnerStack ? GlobalStrat == StgEnum.CnServer ? 3 : 3 : 1,  // H2 CN后，G8后，44分摊右
                4 => isPartnerStack ? GlobalStrat == StgEnum.CnServer ? 0 : 2 : 0,  // D1 CN左，G8前，44分摊左
                5 => isPartnerStack ? GlobalStrat == StgEnum.CnServer ? 1 : 1 : 1,  // D2 CN右，G8右，44分摊右
                6 => isPartnerStack ? GlobalStrat == StgEnum.CnServer ? 2 : 0 : 0,  // D3 CN前，G8左，44分摊左
                7 => isPartnerStack ? GlobalStrat == StgEnum.CnServer ? 3 : 3 : 1,  // D4 CN右，G8右，44分摊右
                _ => 0,
            };
            var safePosDir = _pd.SelectSpecificPriorityIndex(0).Key;
            var safePos1 = safePosList[safePosDir];
            var safePos2 = safePosList[4 * (safePosDir + 1) + basePosIdx];
            sa.Log.Debug($"先飞去第{safePosDir}象限（0~3），再去{basePosIdx}（0~3，左右前后）");

            var dp1 = sa.DrawGuidance(safePos1, 0, 8000, $"飞行分摊1");
            var dp2 = sa.DrawGuidance(safePos2, 8000, 7000, $"飞行分摊2");
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp1);
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);
        }
    }
    
    [ScriptMethod(name: "P1A 双手涂鸦指路删除", eventType: EventTypeEnum.ActionEffect, 
        eventCondition: ["ActionId:regex:^(426(39|40))$"], userControl: Debugging)]
    public void ColorClashRemove(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"飞行分摊1");
        sa.Method.RemoveDraw($"飞行分摊2");
    }
    
    #endregion P1 开场
    
    #region P2 沙漠
    
    [ScriptMethod(name: "---- P2 沙漠 ----", eventType: EventTypeEnum.NpcYell, 
        eventCondition: ["HelloayaWorld:asdf"], userControl: true)]
    public void P2_SplitLine(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P2A - 沙漠 阶段转换", eventType: EventTypeEnum.StartCasting, 
        eventCondition: ["ActionId:42600"], userControl: Debugging)]
    public void P2A_PhaseChange(Event ev, ScriptAccessory sa)
    {
        _M6sPhase = M6SPhase.P2A_Sand;
        _pd.Init(sa, $"沙漠安全区", 4);
        sa.Log.Debug($"当前阶段为：{_M6sPhase}");
    }
    
    [ScriptMethod(name: "P2A - 仙人掌范围(Imgui)", eventType: EventTypeEnum.StartCasting, 
        eventCondition: ["ActionId:regex:^(42657|39468)$"], userControl: true)]
    public void P2A_SpraySpin1Range(Event ev, ScriptAccessory sa)
    {
        var isFirst = ev.ActionId == 42657;
        var dp = sa.DrawCircle(ev.SourceId, 10, isFirst ? 3000: 0, isFirst ? 4000: 8500, $"仙人掌钢铁{ev.SourceId}");
        dp.Color = isFirst ? ColorHelper.ColorRed.V4.WithW(0.75f): sa.Data.DefaultDangerColor.WithW(0.75f);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
    }
    
    [ScriptMethod(name: "P2A - 仙人掌范围删除", eventType: EventTypeEnum.ActionEffect, 
        eventCondition: ["ActionId:regex:^(42657|39468)$"], userControl: Debugging)]
    public void P2A_SpraySpinRangeRemove(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"仙人掌钢铁{ev.SourceId}");
    }
    
    [ScriptMethod(name: "P2A - 116分散指路", eventType: EventTypeEnum.StatusAdd, 
        eventCondition: ["ActionId:4454"], userControl: true)]
    public void P2A_HeatingUpSpread(Event ev, ScriptAccessory sa)
    {
        if (ev.TargetId != sa.Data.Me) return;
        if (ev.DurationMilliseconds() > 50000) return;
        var myIndex = sa.GetMyIndex();
        var flag = 1;

        if (myIndex >= 4) flag *= -1;
        if (GlobalStrat == StgEnum.CnServer) flag *= -1;

        var bottomLeft = new Vector3(85, 0, 105);
        var bottomRight = bottomLeft.FoldPointHorizon(Center.X);

        var dp = sa.DrawGuidance(flag > 0 ? bottomLeft : bottomRight, 39000, 4000, $"沙漠分散");
        sa.Log.Debug($"116分散flag（场基正左负右）：{flag}");
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }
    
    [ScriptMethod(name: "P2A - 116分散指路移除", eventType: EventTypeEnum.ActionEffect, 
        eventCondition: ["ActionId:42658", "TargetIndex:1"], userControl: Debugging)]
    public void P2A_HeatingUpSpreadRemove(Event ev, ScriptAccessory sa)
    {
        if (ev.TargetId != sa.Data.Me) return;
        sa.Method.RemoveDraw($"沙漠分散");
    }
    
    [ScriptMethod(name: "P2A - 流沙四角站位指路", eventType: EventTypeEnum.StartCasting, 
        eventCondition: ["ActionId:39468"], userControl: true)]
    public void P2A_QuickSandCorner(Event ev, ScriptAccessory sa)
    {
        if (_bools[0]) return;
        List<Vector3> corners = Enumerable.Repeat(new Vector3(0, 0, 0), 4).ToList();
        corners[0] = new Vector3(119, 0, 81);
        corners[1] = corners[0].RotatePoint(Center, 90f.DegToRad());
        corners[2] = corners[0].RotatePoint(Center, 180f.DegToRad());
        corners[3] = corners[0].RotatePoint(Center, 270f.DegToRad());

        lock (_bools)
        {
            var spos = ev.SourcePosition;
            var dangerDir = -1;
            for (int i = 0; i < 4; i++)
            {
                var distance = spos.DistanceTo(corners[i]);
                if (distance <= 10f)
                {
                    dangerDir = i;
                    break;
                }
            }
            _bools[0] = true;
            if (dangerDir == -1) return;
            
            var myIndex = sa.GetMyIndex();
            var safeDir = myIndex switch
            {
                2 => QuickSandStrat == QuickSandStgEnum.BattleFieldBasis_场基
                    ? (dangerDir != 3 ? 3 : 2) : ((dangerDir + 3 + 4) % 4),
                3 => QuickSandStrat == QuickSandStgEnum.BattleFieldBasis_场基
                    ? (dangerDir != 1 ? 1 : 2) : ((dangerDir + 1 + 4) % 4),
                _ => QuickSandStrat == QuickSandStgEnum.BattleFieldBasis_场基
                    ? (dangerDir != 0 ? 0 : 2) : ((dangerDir + 2 + 4) % 4)
            };

            var dp = sa.DrawGuidance(corners[safeDir], 0, 5000, $"沙漠四角安全区");
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }
    }
    
    [ScriptMethod(name: "P2B - 飞翔炸弹 阶段转换", eventType: EventTypeEnum.StartCasting, 
        eventCondition: ["ActionId:42627"], userControl: Debugging)]
    public void P2B_PhaseChange(Event ev, ScriptAccessory sa)
    {
        _M6sPhase = M6SPhase.P2B_FlyingBomb;
        sa.Log.Debug($"当前阶段为：{_M6sPhase}");
    }
    
    [ScriptMethod(name: "P2B - 沙坑位置记录", eventType: EventTypeEnum.EnvControl, 
        eventCondition: ["Index:regex:^(3[2345])$", "Flag:2"], userControl: Debugging)]
    public void P2B_SandRegionRecord(Event ev, ScriptAccessory sa)
    {
        if (_M6sPhase != M6SPhase.P2B_FlyingBomb) return;
        var dbgStr = ev.Index() switch
        {
            32 => "上侧沙坑",
            33 => "下侧沙坑",
            34 => "右侧沙坑",
            35 => "左侧沙坑",
            _ => "未知",
        };
        _numbers[0] = (int)ev.Index();
        sa.Log.Debug($"检测到{ev.Index()} = {ev.Flag()} => {dbgStr}, 赋值 {_numbers[0]}");
    }

    [ScriptMethod(name: "P2B - 双手涂鸦（炸弹）类型提示与指路", eventType: EventTypeEnum.Tether,
        eventCondition: ["Id:regex:^(013F|0140)$"], userControl: true)]
    public void P2B_FlyingBombTypeHint(Event ev, ScriptAccessory sa)
    {
        if (_M6sPhase != M6SPhase.P2B_FlyingBomb) return;
        if (ev.SourceId != sa.Data.Me) return;
        var isFlyingBomb = ev.Id0() == 0x13F;
        var hintStr = isFlyingBomb ? $"飞行炸弹，站外" : $"普通炸弹，站坑";
        sa.Log.Debug($"玩家为{(isFlyingBomb ? "飞行炸弹" : "普通炸弹")} {ev.Id0()}");
        sa.Method.TextInfo(hintStr, 6000);

        List<Vector3> points =
        [
            // A沙坑基准
            // MT/D1普通，ST/D2普通，H1/D3普通，H2/D4普通
            // MT/D1飞行，ST/D2飞行，H1/D3飞行，H2/D4飞行
            new(95, 0, 95), new(105, 0, 95), new(85, 0, 90), new(115, 0, 90),
            new(95, 0, 105), new(105, 0, 105), new(85, 0, 110), new(115, 0, 110)
        ];
        var myPointIdx = sa.GetMyIndex() % 4 + (isFlyingBomb ? 4 : 0);
        float rotateDeg = _numbers[0] switch
        {
            32 => 0,
            33 => 180,
            34 => 90,
            35 => 270,
            _ => 0,
        };
        sa.Log.Debug($"根据沙坑位置，基于A点沙坑安全点旋转{rotateDeg} Deg");
        var myPoint = points[myPointIdx].RotatePoint(Center, rotateDeg.DegToRad());
        var dp = sa.DrawGuidance(myPoint, 0, 6000, $"炸弹就位点");
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }
    
    #endregion P2 沙漠

    #region P3 小怪

    [ScriptMethod(name: "---- P3 小怪 ----", eventType: EventTypeEnum.NpcYell, 
        eventCondition: ["HelloayaWorld:asdf"], userControl: true)]
    public void P3_SplitLine(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "P3A - 小怪 阶段转换", eventType: EventTypeEnum.StartCasting, 
        eventCondition: ["ActionId:42661"], userControl: Debugging)]
    public void P3A_PhaseChange(Event ev, ScriptAccessory sa)
    {
        _M6sPhase = M6SPhase.P3A_Mobs;
        sa.Log.Debug($"当前阶段为：{_M6sPhase}");
    }
    
    [ScriptMethod(name: "P3A - 小怪 本体AOE提示", eventType: EventTypeEnum.StartCasting, 
        eventCondition: ["ActionId:42666"], userControl: true)]
    public void P3A_AoeHint(Event ev, ScriptAccessory sa)
    {
        sa.Method.TextInfo($"本体正在释放AOE！", 4000, true);
    }
    
    // [ScriptMethod(name: "P3A - 小怪 W1提示", eventType: EventTypeEnum.StartCasting, 
    //     eventCondition: ["ActionId:42662"], userControl: true)]
    // public void P3A_MobsWave1Hint(Event ev, ScriptAccessory sa)
    // {
    //     var hintStr = "";
    //     var myIndex = sa.GetMyIndex();
    //     hintStr = myIndex switch
    //     {
    //         0 => $"右B准备，拉Boss与松鼠，拉到下C",
    //         1 => $"左D准备，拉羊",
    //         6 => $"左D准备，放一段LB，引导猫",
    //         7 => $"左D准备，放二段LB，引导猫",
    //         _ => $"左D准备，打羊，引导猫"
    //     };
    //     var isWarning = false;
    //     if (myIndex is 0 or 1)
    //     {
    //         var myObj = sa.Data.MyObject;
    //         if (myObj == null) return;
    //         if (!myObj.HasStatusAny([1833u, 743u, 79u, 91u]))
    //         {
    //             isWarning = true;
    //             hintStr = $"【开启盾姿】，" + hintStr;
    //         }
    //     }
    //     sa.Method.TextInfo(hintStr, 6000, isWarning);
    //
    //     List<DrawPropertiesEdit> dpList =
    //     [
    //         sa.DrawGuidance(Center, myIndex == 0 ? new Vector3(110, 0, 100) : new Vector3(90, 0, 100), 0, 10000,
    //             $"W1指路")
    //     ];
    //     if (myIndex == 0)
    //         dpList.Add(sa.DrawGuidance(new Vector3(110, 0, 100), new Vector3(100, 0, 110), 0, 10000,
    //             $"W1指路"));
    //     for (var i = 0; i < dpList.Count; i++)
    //         sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpList[i]);
    // }
    //
    // [ScriptMethod(name: "P3A - 小怪 W2提示", eventType: EventTypeEnum.StartCasting, 
    //     eventCondition: ["ActionId:42663"], userControl: true)]
    // public void P3A_MobsWave2Hint(Event ev, ScriptAccessory sa)
    // {
    //     sa.Method.RemoveDraw($"W1指路");
    //     
    //     var hintStr = "";
    //     var myIndex = sa.GetMyIndex();
    //     hintStr = myIndex switch
    //     {
    //         0 => $"下C准备，拉松鼠群，至右上鱼、左上鱼",
    //         1 => $"跟随MT，集火松鼠群与鱼",
    //         3 => $"右上就位，引导鱼",
    //         6 => $"左上就位，引导鱼",
    //         _ => $"跟随MT，集火松鼠群与鱼"
    //     };
    //     var isWarning = false;
    //     if (myIndex is 1)
    //     {
    //         var myObj = sa.Data.MyObject;
    //         if (myObj == null) return;
    //         if (myObj.HasStatusAny([1833u, 743u, 79u, 91u]))
    //         {
    //             isWarning = true;
    //             hintStr = $"【关启盾姿】，" + hintStr;
    //         }
    //     }
    //     sa.Method.TextInfo(hintStr, 6000, isWarning);
    // }
    
    #endregion P3 小怪

    #region P4 山川

    [ScriptMethod(name: "---- P4 山川 ----", eventType: EventTypeEnum.NpcYell, 
        eventCondition: ["HelloayaWorld:asdf"], userControl: true)]
    public void P4_SplitLine(Event ev, ScriptAccessory sa)
    {
    }

    #region P4A 雷火箭头

    [ScriptMethod(name: "P4A - 山川 阶段转换", eventType: EventTypeEnum.StartCasting, 
        eventCondition: ["ActionId:42595"], userControl: Debugging)]
    public void P4A_PhaseChange(Event ev, ScriptAccessory sa)
    {
        _M6sPhase = M6SPhase.P4A_Bridge;
        InitParams();
        sa.Log.Debug($"当前阶段为：{_M6sPhase}");
    }
    
    [ScriptMethod(name: "P4A - 箭头位置记录", eventType: EventTypeEnum.Tether, 
        eventCondition: ["Id:0140"], userControl: Debugging)]
    public void P4A_ArrowRecord(Event ev, ScriptAccessory sa)
    {
        if (_M6sPhase != M6SPhase.P4A_Bridge) return;
        var spos = ev.SourcePosition;
        lock (_numbers)
        {
            _numbers[1]++;
            if (spos.Z < 80 & spos.X < 85)
                _bools[0] = true;
            if (_numbers[1] != 6) return;
            sa.Log.Debug($"箭头检测完毕，需在每四格{(_bools[0] ? "右下" : "左上")}就位");
            _events[0].Set();
        }
    }
    
    [ScriptMethod(name: "P4A - 雷火箭指路", eventType: EventTypeEnum.StartCasting, 
        eventCondition: ["ActionId:regex:^(4263[13])$"], userControl: true)]
    public void P4A_StackOrSpread(Event ev, ScriptAccessory sa)
    {
        if (_M6sPhase != M6SPhase.P4A_Bridge) return;
        var aid = ev.ActionId;
        var isStack = aid == 42631;
        sa.Log.Debug($"Boss读条：{(isStack ? "火分摊": "雷分散")}");
        _events[0].WaitOne();

        List<Vector3> points = GlobalStrat switch
        {
            StgEnum.CnServer =>
            [
                // 左上就位：MT/H1/D1/D2分摊，ST/H2/D3/D4分摊
                new(85f, 0, 108f), new(98.5f, 0, 98.5f), 
                // 右下就位：MT/H1/D1/D2分摊，ST/H2/D3/D4分摊
                new(87.5f, 0, 105.5f), new(101.5f, 0, 101.5f), 
                // 左上就位：MT/ST/H1/H2/D1/D2/D3/D4分散,
                new(94.45f, 0, 97.62f), new(110.5f, 0, 96.5f), new(84.8f, 0, 112.7f), new(108.3f, 0, 112.6f),
                new(83f, 0, 96.5f), new(96.5f, 0, 110.5f), new(83f, 0, 83f), new(110.5f, 0, 83f),
                // 右下就位：MT/ST/H1/H2/D1/D2/D3/D4分散,
                new(105.5f, 0, 105.5f), new(117f, 0, 103.5f), new(89.5f, 0, 117f), new(116.8f, 0, 116.8f),
                new(90.5f, 0, 103.65f), new(103f, 0, 117f), new(89.5f, 0, 89.5f), new(117f, 0, 90f),
            ],
            _ =>
            [
                // 左上就位：MT/H1/D1/D2分摊，ST/H2/D3/D4分摊
                new(98.5f, 0, 98.5f), new(85f, 0, 108f),
                // 右下就位：MT/H1/D1/D2分摊，ST/H2/D3/D4分摊
                new(101.5f, 0, 101.5f), new(87.5f, 0, 105.5f),
                // 左上就位：MT/ST/H1/H2/D1/D2/D3/D4分散,
                new(96.5f, 0, 83), new(96.5f, 0, 110.5f), new(83f, 0, 96.5f), new(110.5f, 0, 96.5f),
                new(84.8f, 0, 112.7f), new(108.3f, 0, 112.6f), new(83f, 0, 83f), new(110.5f, 0, 83f),
                // 右下就位：MT/ST/H1/H2/D1/D2/D3/D4分散,
                new(103f, 0, 91.5f), new(103f, 0, 117f), new(90.5f, 0, 103.65f), new(117f, 0, 103.5f),
                new(89.5f, 0, 117f), new(116.8f, 0, 116.8f), new(89.5f, 0, 89.5f), new(117f, 0, 90f),
            ],
        };

        var myIndex = sa.GetMyIndex();
        var isMtGroup = myIndex is 0 or 2 or 4 or 5;
        // 0 2 4 5, 1 3 6 7
        var myPointIdx = isStack ? (isMtGroup ? 0 : 1) : myIndex + 4;
        myPointIdx += (_bools[0] ? (isStack ? 2 : 8) : 0);

        var dp = sa.DrawGuidance(points[myPointIdx], 0, 6000, $"雷火就位");
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        
        _events[0].Reset();
    }
    
    #endregion P4A 雷火箭头

    #region P4B 雷云

    [ScriptMethod(name: "P4B - 雷云 阶段转换", eventType: EventTypeEnum.StartCasting, 
        eventCondition: ["ActionId:42648"], userControl: Debugging)]
    public void P4B_PhaseChange(Event ev, ScriptAccessory sa)
    {
        _M6sPhase = M6SPhase.P4B_Light;
        InitParams();
        sa.Log.Debug($"当前阶段为：{_M6sPhase}");
    }

    private struct CloudInfo(Vector3 cloudPos, float cloudRotation, int cloudDir, int cloudRotDir)
    {
        public Vector3 CloudPos = cloudPos;
        public float CloudRotation = cloudRotation;
        public int CloudDir = cloudDir;
        public int CloudRotDir = cloudRotDir;
        
        public static CloudInfo Default()
        {
            return new CloudInfo(Vector3.Zero, 0, -1, -1);
        }
    }
    
    private CloudInfo GetCloudInfo(ScriptAccessory sa)
    {
        var cloudCharaEnum = sa.GetByDataId(18339).ToList();
        if (!cloudCharaEnum.Any())
        {
            sa.Log.Error($"cloudCharaEnum 未找到雷云实体");
            return CloudInfo.Default(); 
        }
        var cloudChara = cloudCharaEnum.First();
        if (cloudChara == null)
        {
            sa.Log.Error($"cloudChara 未找到雷云实体");
            return CloudInfo.Default(); 
        }
        var cloudPos = cloudChara.Position;
        var cloudDir = cloudPos.Position2Dirs(Center, 3, false);
        var cloudRotation = cloudChara.Rotation;
        
        var dp = sa.DrawGuidance((ulong)cloudChara.EntityId, (ulong)0, 0, 4000, $"面向");
        dp.Scale = new Vector2(1, 4);
        dp.ScaleMode = ScaleMode.None;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dp);

        var cloudRotDir = cloudRotation.Game2Logic().Rad2Dirs(6, false);
        sa.Log.Debug($"检测到雷云位置：{cloudPos}，面向角度：{cloudRotation.RadToDeg()}, 逻辑基：{cloudRotation.Game2Logic().RadToDeg()}, 位于方位{cloudDir}, 面向分割{cloudRotDir}");
        
        // 检测是否为刚出生，无旋转角度
        if (cloudRotDir is 3)
            cloudRotDir = Math.Abs(cloudRotation.Game2Logic().RadToDeg() - 180) < 1 ? -1 : cloudRotDir;
        
        return new CloudInfo(cloudPos, cloudRotation.RadToDeg(), cloudDir, cloudRotDir);
    }
    
    [ScriptMethod(name: "P4B - 雷云 初始就位位置", eventType: EventTypeEnum.AddCombatant, 
        eventCondition: ["DataId:18339"], userControl: true)]
    public void P4B_FirstSafePoint(Event ev, ScriptAccessory sa)
    {
        if (_M6sPhase != M6SPhase.P4B_Light) return;
        var cloudStruct = GetCloudInfo(sa);
        List<Vector3> crowdSafePos = [BridgePos[0], BridgePos[1], BridgePos[2]];
        var dp = sa.DrawGuidance(crowdSafePos[cloudStruct.CloudDir], 0, 3500, $"初始安全位置");
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }
    
    [ScriptMethod(name: "P4B - 雷云 移动中就位位置", eventType: EventTypeEnum.TargetIcon, 
        eventCondition: ["Id:025A"], userControl: true)]
    public async void P4B_RemainingSafePoint(Event ev, ScriptAccessory sa)
    {
        if (_M6sPhase != M6SPhase.P4B_Light) return;
        lock (_numbers)
        {
            _numbers[0]++;
            if (ev.TargetId == sa.Data.Me)
                _bools[0] = true;
            if (_numbers[0] % 2 == 1) return;
        }
        await Task.Delay(1000);
        var cloudStruct = GetCloudInfo(sa);
        if (cloudStruct.CloudDir == -1)
        {
            sa.Log.Error($"步骤出错，返回");
            return;
        }
        // 雷云在右上、下、左上，人群位置
        List<Vector3> crowdSafePos = [BridgePos[0], BridgePos[1], BridgePos[2]];
        
        // 雷云右上，左、右；雷云在下，左、右；雷云在左上，左、右
        List<Vector3> targetSafePos = [IslandPos[2], IslandPos[1], IslandPos[0], IslandPos[2], IslandPos[1], IslandPos[0]];
        
        var crowdSafeIdx = cloudStruct.CloudRotDir switch
        {
            0 => 0,
            1 => 0,
            2 => 1,
            3 => 1,
            4 => 2,
            5 => 2,
            _ => 0,
        };

        var myPos = _bools[0]
            ? targetSafePos[crowdSafeIdx * 2 + (sa.GetMyIndex() > 3 ? 1 : 0)]
            : crowdSafePos[crowdSafeIdx];
        var dp = sa.DrawGuidance(myPos, 0, 6000, $"人群安全位置");
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        
        _bools[0] = false;
    }
    
    #endregion P4B 雷云

    #region P4C 岩浆
    
    [ScriptMethod(name: "P4C - 岩浆 阶段转换", eventType: EventTypeEnum.StartCasting, 
        eventCondition: ["ActionId:42649"], userControl: Debugging)]
    public void P4C_PhaseChange(Event ev, ScriptAccessory sa)
    {
        _M6sPhase = M6SPhase.P4C_Lava;
        InitParams();
        _towers.RefreshParam(sa, false);
        sa.Log.Debug($"当前阶段为：{_M6sPhase}");
    }
    
    [ScriptMethod(name: "P4C - 岩浆 就位提示", eventType: EventTypeEnum.StartCasting, 
        eventCondition: ["ActionId:42649"], userControl: true)]
    public void P4C_StandPoint(Event ev, ScriptAccessory sa)
    {
        // MT, ST, H1, H2, D1, D2, D3, D4
        // 国服H1D3在右上岛，H2D4在左上岛
        List<Vector3> standPoint = GlobalStrat switch
        {
            StgEnum.CnServer => [
                IslandPos[1], IslandPos[1], IslandPos[0], IslandPos[2],
                IslandPos[1], IslandPos[1], IslandPos[0], IslandPos[2]
            ],
             _ => [
                IslandPos[1], IslandPos[1], IslandPos[2], IslandPos[0],
                IslandPos[1], IslandPos[1], IslandPos[2], IslandPos[0]
            ],
        };

        var dp = sa.DrawGuidance(standPoint[sa.GetMyIndex()], 0, 6000, $"岩浆就位");
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    public class Towers
    {
        // ReSharper disable once NullableWarningSuppressionIsUsed
        public ScriptAccessory sa {get; set;} = null!;
        public int TowerNum { get; set; } = 0;
        public bool IsSecondRound { get; set; } = false;
        public List<Tower> TowerList = [];
        
        public void RefreshParam(ScriptAccessory accessory, bool isSecondRound)
        {
            sa = accessory;
            TowerNum = 0;
            TowerList = [];
            IsSecondRound = isSecondRound;
        }

        public string TowerInfo(Tower tower)
        {
            var str = "";
            str += $"塔 {tower.TowerIdx}，位于岛 {tower.IslandIdx}, 与中心成角 {tower.Rotation.RadToDeg()}, " +
                   $"属于玩家 {sa.GetPlayerJobByIndex(tower.PlayerIdx)}, 坐标 {tower.TowerPos}";
            return str;
        }

        public void AddTower(Tower tower)
        {
            TowerList.Add(tower);
            TowerNum++;
            sa.Log.Debug($"添加：{TowerInfo(tower)}");
        }

        public void SortTowerListByRotation()
        {
            // 升序排序
            TowerList.Sort((towerA, towerB) => towerA.Rotation.CompareTo(towerB.Rotation));
        }

        public void SetTowerPlayer(List<int> towerIdxList, int playerIdx)
        {
            sa.Log.Debug($"接受指令：为塔 {sa.BuildListStr(towerIdxList)} 赋值 {sa.GetPlayerJobByIndex(playerIdx)}");
            foreach (var towerIdx in towerIdxList)
            {
                var idx = TowerList.FindIndex(tower => tower.TowerIdx == towerIdx);
                if (idx == -1)
                {
                    sa.Log.Error($"未在TowerList中找到 {towerIdx}");
                    continue;
                }
                if (playerIdx is < 0 or > 8)
                {
                    sa.Log.Error($"输入的playerIdx {playerIdx} 不合法");
                    continue;
                }
                var tower = TowerList[idx];
                tower.PlayerIdx = playerIdx;
                TowerList[idx] = tower;
                sa.Log.Debug($"将塔 {towerIdx} 分配给了 {sa.GetPlayerJobByIndex(playerIdx)}, {tower.TowerPos}");
            }
        }
        
        public void SetTowerPlayerByListIdx(int idx, int playerIdx)
        {
            var tower = TowerList[idx];
            sa.Log.Debug($"将塔 {tower.TowerIdx} 分配给了 {sa.GetPlayerJobByIndex(playerIdx)}");
            tower.PlayerIdx = playerIdx;
            TowerList[idx] = tower;
        }

        public Tower GetTowerByPlayerIdx(int playerIdx)
        {
            return TowerList.Find(tower => tower.PlayerIdx == playerIdx);
        }

        public MostTowerInfo FindMostTowerIslandIdx()
        {
            List<int> towerNum = [0, 0, 0];
            foreach (var tower in TowerList)
            {
                towerNum[tower.IslandIdx]++;
            }
            MostTowerInfo result = new MostTowerInfo
            {
                MaxIslandIdx = towerNum.IndexOf(towerNum.Max()),
                MaxTowerNum = towerNum.Max()
            };
            return result;
        }
        
        public struct MostTowerInfo
        {
            public int MaxIslandIdx { get; set; }
            public int MaxTowerNum { get; set; }
        }
    }
    
    public record struct Tower
    {
        public Vector3 TowerPos;
        public int TowerIdx;
        public int IslandIdx;
        public float Rotation;
        public int PlayerIdx = -1;

        public Tower(int towerIdx)
        {
            TowerIdx = towerIdx;
            if (towerIdx is < 69 or > 96)
                throw new ArgumentException("非法的Tower Index");
            TowerPos = TowerPosList[towerIdx - 69];
            IslandIdx = towerIdx switch
            {
                < 79 => 2,
                (>= 79 and < 89) => 0,
                >= 89 => 1
            };
            Rotation = TowerPos.FindRadian(CenterRiver);
        }
    }
    
    [ScriptMethod(name: "P4C, P4D - 塔记录", eventType: EventTypeEnum.EnvControl, 
        eventCondition: ["Flag:2"], userControl: Debugging)]
    public void P4C_TowerRecord(Event ev, ScriptAccessory sa)
    {
        if (_M6sPhase != M6SPhase.P4C_Lava & _M6sPhase != M6SPhase.P4D_FlyLava) return;
        var envIdx = ev.Index();
        if (envIdx is < 69 or > 96) return;
        lock (_towers)
        {
            _towers.AddTower(new Tower((int)envIdx));
            if (_towers.TowerNum != 8) return;
            sa.Log.Debug($"完成 第 {(_towers.IsSecondRound ? "二" : "一")} 轮，8 座塔的记录。");
            _events[0].Set();
        }
    }
    
    [ScriptMethod(name: "P4C - 等待旋风、分散、踩塔提示", eventType: EventTypeEnum.StartCasting, 
        eventCondition: ["ActionId:42682"], userControl: true)]
    public void P4C_TwisterHint(Event ev, ScriptAccessory sa)
    {
        if (_M6sPhase != M6SPhase.P4C_Lava) return;
        const string str = "机制后，等待旋风，再踩塔";
        sa.Method.TextInfo(str, 4000, true);
        var myIndex = sa.GetMyIndex();
        if (myIndex is 0 or 1 or 4 or 5)
        {
            List<Vector3> baitPos = [new(97, 0, 111.5f), new(103, 0, 111.5f), new(98, 0, 114), new(102, 0, 114)];
            var myBaitIdx = myIndex > 2 ? myIndex - 2 : myIndex;
            var dp = sa.DrawGuidance(baitPos[myBaitIdx], 0, 5000, $"近战分散引导");
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }
        _events[0].WaitOne();
        
        // 分配塔
        // 此处为Game8，国服因初始岛就位不同需修改
        _towers.SetTowerPlayer([96], 5);  // 固定值，右下塔，D2
        _towers.SetTowerPlayer([95], 1);  // 固定值，右上塔，ST
        _towers.SetTowerPlayer([94], 4);  // 固定值，左下塔，D1
        _towers.SetTowerPlayer([93], 0);  // 固定值，左上塔，MT
        _towers.SetTowerPlayer([69, 72, 74, 76, 78], GlobalStrat == StgEnum.CnServer ? 7 : 2);    // 左上塔，场基左，Game8 H1, 国服D4
        _towers.SetTowerPlayer([70, 71, 73, 75, 77], GlobalStrat == StgEnum.CnServer ? 3 : 6);    // 左上塔，场基右，Game8 D3, 国服H2
        _towers.SetTowerPlayer([79, 81, 83, 85, 87], GlobalStrat == StgEnum.CnServer ? 2 : 3);    // 右上塔，场基左，Game8 H2, 国服H1
        _towers.SetTowerPlayer([80, 82, 84, 86, 88], GlobalStrat == StgEnum.CnServer ? 6 : 7);    // 右上塔，场基右，Game8 D4, 国服D3

        var myTower = _towers.GetTowerByPlayerIdx(sa.GetMyIndex());
        var dp0 = sa.DrawStaticCircle(myTower.TowerPos, sa.Data.DefaultDangerColor.WithW(1.5f), 0, 8000, $"待踩塔范围", 3f);
        var dp1 = sa.DrawStaticCircle(myTower.TowerPos, sa.Data.DefaultSafeColor.WithW(1.5f), 8000, 5000, $"踩塔范围", 3f);
        var dp2 = sa.DrawGuidance(myTower.TowerPos, 8000, 5000, $"踩塔指路");
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp0);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp1);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);
        sa.Log.Debug($"向塔 {myTower.TowerIdx} {myTower.TowerPos} 绘图");
        
        _events[0].Reset();
    }
    
    #endregion P4C 岩浆
    


    [ScriptMethod(name: "P4D - 飞行方向、目标塔", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:42614"], userControl: true)]
    public void P4D_FlyingDestination(Event ev, ScriptAccessory sa)
    {
        _events[0].WaitOne();
        if (_M6sPhase != M6SPhase.P4D_FlyLava) return;
        _towers.SortTowerListByRotation();
        
        // Game8策略
        // 初始优先级列表，个位数为身份，十位数为顺时针顺序，H1-D3-H2-D4-D2-ST-MT-D1
        // 岛屿序号后续会根据百位数决定
        
        // 国服策略
        // H左R右，无法分清则H中R远；T近M远（？？？）
        // 顺时针顺序：D3-H1, D4-H2, D2-ST-MT-D1
        List<int> priorityList = GlobalStrat switch
        {
            StgEnum.CnServer => [60, 51, 12, 33, 74, 45, 6, 27],
            _ => [60, 51, 2, 23, 74, 45, 16, 37],
        };
        
        // 判断是否为南八塔
        var mostTowerInfo = _towers.FindMostTowerIslandIdx();
        if (mostTowerInfo is { MaxTowerNum: 8, MaxIslandIdx: 1 })
        {
            _towers.SetTowerPlayer([89], GlobalStrat == StgEnum.CnServer ? 0 : 6);  // 正上，国服MT，G8 D3
            _towers.SetTowerPlayer([90], GlobalStrat == StgEnum.CnServer ? 6 : 2);  // 外左，国服D3，G8 H1
            _towers.SetTowerPlayer([91], GlobalStrat == StgEnum.CnServer ? 1 : 0);  // 正下，国服ST，G8 MT
            _towers.SetTowerPlayer([92], GlobalStrat == StgEnum.CnServer ? 7 : 5);  // 外右，国服D4，G8 D2
            _towers.SetTowerPlayer([93], GlobalStrat == StgEnum.CnServer ? 2 : 3);  // 左上，国服H1，G8 H2
            _towers.SetTowerPlayer([94], GlobalStrat == StgEnum.CnServer ? 4 : 4);  // 左下，国服D1，G8 D1
            _towers.SetTowerPlayer([95], GlobalStrat == StgEnum.CnServer ? 3 : 7);  // 右上，国服H2，G8 D4
            _towers.SetTowerPlayer([96], GlobalStrat == StgEnum.CnServer ? 5 : 1);  // 右下，国服D2，G8 ST
        }
        else
        {
            // 根据四人塔位置，获得旋转方向
            var maxIslandIdx = mostTowerInfo.MaxIslandIdx;
            if (maxIslandIdx == 0)
            {
                // 右上4人塔，集体逆时针
                priorityList[GlobalStrat == StgEnum.CnServer ? 3 : 2] += 100;     // 国服H2 Game8 H1去下
                priorityList[GlobalStrat == StgEnum.CnServer ? 7 : 6] += 100;     // 国服D4 Game8 D3去下
                priorityList[GlobalStrat == StgEnum.CnServer ? 2 : 3] += 200;     // 国服H1 Game8 H2去左上
                priorityList[GlobalStrat == StgEnum.CnServer ? 6 : 7] += 200;     // 国服D3 Game8 D4去左上
            }
            else
            {
                // 左上4人塔，集体顺时针
                priorityList[0] += 200;     // MT去左上
                priorityList[1] += 200;     // ST去左上
                priorityList[4] += 200;     // D1去左上
                priorityList[5] += 200;     // D2去左上
                priorityList[GlobalStrat == StgEnum.CnServer ? 2 : 3] += 100;     // 国服H1 Game8 H2去下
                priorityList[GlobalStrat == StgEnum.CnServer ? 6 : 7] += 100;     // 国服D3 Game8 D4去下
            }
            // 流程正义
            priorityList.Sort((a, b) => a.CompareTo(b));
            _towers.SetTowerPlayerByListIdx(0, priorityList[0] % 10);
            _towers.SetTowerPlayerByListIdx(1, priorityList[1] % 10);
            _towers.SetTowerPlayerByListIdx(2, priorityList[2] % 10);
            _towers.SetTowerPlayerByListIdx(3, priorityList[3] % 10);
            _towers.SetTowerPlayerByListIdx(4, priorityList[4] % 10);
            _towers.SetTowerPlayerByListIdx(5, priorityList[5] % 10);
            _towers.SetTowerPlayerByListIdx(6, priorityList[6] % 10);
            _towers.SetTowerPlayerByListIdx(7, priorityList[7] % 10);
        }
        
        // 时间点待测
        var myTower = _towers.GetTowerByPlayerIdx(sa.GetMyIndex());
        var dp0 = sa.DrawStaticCircle(myTower.TowerPos, sa.Data.DefaultDangerColor.WithW(1.5f), 0, 14000, $"待踩塔范围", 3f);
        var dp1 = sa.DrawStaticCircle(myTower.TowerPos, sa.Data.DefaultSafeColor.WithW(1.5f), 14000, 5000, $"踩塔范围", 3f);
        var dp2 = sa.DrawGuidance(myTower.TowerPos, 0, 14000, $"踩塔指路预备", isSafe: false);
        var dp3 = sa.DrawGuidance(myTower.TowerPos, 14000, 5000, $"踩塔指路");
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp0);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp1);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp3);
        sa.Log.Debug($"向塔 {myTower.TowerIdx} {myTower.TowerPos} 绘图");
    }
    
    [ScriptMethod(name: "P4D - 等待旋风提示", eventType: EventTypeEnum.StatusAdd, 
        eventCondition: ["StatusID:4163"], userControl: Debugging)]
    public void P4D_TwisterHint(Event ev, ScriptAccessory sa)
    {
        if (_M6sPhase != M6SPhase.P4D_FlyLava) return;
        if (ev.TargetId != sa.Data.Me) return;
        const string str = "等待旋风，再踩塔";
        sa.Method.TextInfo(str, 6000, true);
    }
    #endregion P4 山川
    
    public class PriorityDict
    {
        // ReSharper disable once NullableWarningSuppressionIsUsed
        public ScriptAccessory sa {get; set;} = null!;
        // ReSharper disable once NullableWarningSuppressionIsUsed
        public Dictionary<int, int> Priorities {get; set;} = null!;
        public string Annotation { get; set; } = "";
        public int ActionCount { get; set; } = 0;
        
        public void Init(ScriptAccessory accessory, string annotation, int partyNum = 8, bool refreshActionCount = true)
        {
            sa = accessory;
            Priorities = new Dictionary<int, int>();
            for (var i = 0; i < partyNum; i++)
            {
                Priorities.Add(i, 0);
            }
            Annotation = annotation;
            if (refreshActionCount)
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
            var sortedPriorities = SelectMiddlePriorityIndices(0, Priorities.Count, descending);
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
            var sortedPriorities = SelectMiddlePriorityIndices(0, Priorities.Count, descending);
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
        public string ShowPriorities(bool showJob = true)
        {
            var str = $"{Annotation} ({ActionCount}-th) 优先级字典：\n";
            if (Priorities.Count == 0)
            {
                str += $"PriorityDict Empty.\n";
                return str;
            }
            foreach (var pair in Priorities)
            {
                str += $"Key {pair.Key} {(showJob ? $"({_role[pair.Key]})" : "")}, Value {pair.Value}\n";
            }

            return str;
        }

        public PriorityDict DeepCopy()
        {
            return JsonConvert.DeserializeObject<PriorityDict>(JsonConvert.SerializeObject(this)) ?? new PriorityDict();
        }

        public void AddActionCount(int count = 1)
        {
            ActionCount += count;
        }

    }
    
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

    public static uint Id0(this Event ev)
    {
        return ParseHexId(ev["Id"], out var id) ? id : 0;
    }
    
    public static uint Index(this Event ev)
    {
        return JsonConvert.DeserializeObject<uint>(ev["Index"]);
    }
    
    public static uint Flag(this Event ev)
    {
        return JsonConvert.DeserializeObject<uint>(ev["Flag"]);
    }
    
    public static uint DurationMilliseconds(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["DurationMilliseconds"]);
    }

}

public static class IbcHelper
{
    public static IGameObject? GetById(this ScriptAccessory sa, ulong gameObjectId)
    {
        return sa.Data.Objects.SearchById(gameObjectId);
    }
    
    public static IGameObject? GetMe(this ScriptAccessory sa)
    {
        return sa.Data.Objects.LocalPlayer;
    }

    public static IEnumerable<IGameObject?> GetByDataId(this ScriptAccessory sa, uint dataId)
    {
        return sa.Data.Objects.Where(x => x.DataId == dataId);
    }
}

#region 计算函数
public static class DirectionCalc
{
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
#endregion 计算函数

#region 位置序列函数
public static class IndexHelper
{
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
    /// 输入玩家dataId，获得对应的位置index
    /// </summary>
    /// <param name="pid">玩家SourceId</param>
    /// <param name="accessory"></param>
    /// <returns>该玩家对应的位置index</returns>
    public static int GetPlayerIdIndex(this ScriptAccessory accessory, ulong pid)
    {
        // 获得玩家 IDX
        return accessory.Data.PartyList.IndexOf((uint)pid);
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
        List<string> role8 = ["MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4"];
        List<string> role4 = ["T", "H", "D1", "D2"];
        if (idx < 0 || idx >= 8 || (fourPeople && idx >= 4))
            return "Unknown";
        return fourPeople ? role4[idx] : role8[idx];
    }
}
#endregion 位置序列函数

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

#region 绘图函数
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

        if (ownerObj is uint or ulong)
        {
            dp.Owner = (ulong)ownerObj;
        }
        else if (ownerObj is Vector3 spos)
        {
            dp.Position = spos;
        }
        else
        {
            throw new ArgumentException("ownerObj的目标类型输入错误");
        }

        if (targetObj is uint or ulong)
        {
            if ((ulong)targetObj != 0) dp.TargetObject = (ulong)targetObj;
        }
        else if (targetObj is Vector3 tpos)
        {
            dp.TargetPosition = tpos;
        }
        else
        {
            throw new ArgumentException("targetObj的目标类型输入错误");
        }

        return dp;
    }

    public static DrawPropertiesEdit DrawGuidance(this ScriptAccessory accessory,
        object targetObj, int delay, int destroy, string name, float rotation = 0, float scale = 1f, bool isSafe = true)
    => accessory.DrawGuidance((ulong)accessory.Data.Me, targetObj, delay, destroy, name, rotation, scale, isSafe);

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
    public static DrawPropertiesEdit DrawTargetNearFarOrder(this ScriptAccessory accessory, ulong ownerId, uint orderIdx,
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
    public static DrawPropertiesEdit DrawOwnersTarget(this ScriptAccessory accessory, ulong ownerId, float width, float length, int delay,
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
    public static DrawPropertiesEdit DrawOwnersEnmityOrder(this ScriptAccessory accessory, ulong ownerId, uint orderIdx, float width, float length, int delay, int destroy, string name, bool lengthByDistance = false, bool byTime = false)
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
    public static DrawPropertiesEdit DrawCircle(this ScriptAccessory accessory, ulong ownerId, float scale, int delay, int destroy, string name, bool byTime = false)
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

        if (ownerObj is uint or ulong)
        {
            dp.Owner = (ulong)ownerObj;
        }
        else if (ownerObj is Vector3 spos)
        {
            dp.Position = spos;
        }
        else
        {
            throw new ArgumentException("ownerObj的目标类型输入错误");
        }

        if (targetObj is uint or ulong)
        {
            if ((ulong)targetObj != 0) dp.TargetObject = (ulong)targetObj;
        }
        else if (targetObj is Vector3 tpos)
        {
            dp.TargetPosition = tpos;
        }
        else
        {
            throw new ArgumentException("ownerObj的目标类型输入错误");
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
        => accessory.DrawStatic(center, (ulong)0, 0, 0, scale, scale, color, delay, destroy, name);

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
    public static DrawPropertiesEdit DrawRect(this ScriptAccessory accessory, ulong ownerId, float width, float length, int delay, int destroy, string name, bool byTime = false)
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


        if (owner is uint or ulong)
        {
            for (var i = 0; i < extendDirs.Count; i++)
            {
                var dp = accessory.DrawRect((ulong)owner, width, length, delay, destroy, $"{name}{i}");
                dp.Rotation = extendDirs[i];
                dp.Color = i == myDirIdx ? colorPlayer : colorNormal;
                dpList.Add(dp);
            }
        }
        else if (owner is Vector3 spos)
        {
            for (var i = 0; i < extendDirs.Count; i++)
            {
                var dp = accessory.DrawGuidance(spos, spos.ExtendPoint(extendDirs[i], length), delay, destroy,
                    $"{name}{i}", 0, width);
                dp.Color = i == myDirIdx ? colorPlayer : colorNormal;
                dpList.Add(dp);
            }
        }
        else
        {
            throw new ArgumentException("DrawExtendDirection的目标类型输入错误");
        }

        return dpList;
    }

    /// <summary>
    /// 将List内信息转换为字符串。
    /// </summary>
    /// <param name="accessory"></param>
    /// <param name="myList"></param>
    /// <param name="isJob">是职业，在转为字符串前调用转职业函数</param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static string BuildListStr<T>(this ScriptAccessory accessory, List<T> myList, bool isJob = false)
    {
        return string.Join(", ", myList.Select(item =>
        {
            if (isJob && item != null && item is int i)
                return accessory.GetPlayerJobByIndex(i);
            return item?.ToString() ?? "";
        }));
    }
}

#endregion 绘图函数

#endregion 函数集

