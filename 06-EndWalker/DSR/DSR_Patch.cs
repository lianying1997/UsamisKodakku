using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Dalamud.Utility.Numerics;
using ECommons;
using ECommons.DalamudServices;
using ECommons.GameFunctions;
using ECommons.MathHelpers;
using KodakkuAssist.Script;
using KodakkuAssist.Data;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.Draw.Manager;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs;
using KodakkuAssist.Module.Script.Type;

namespace UsamisKodakku.Scripts._06_EndWalker.DSR;

[ScriptType(name: Name, territorys: [968, 1112], guid: "cc6fb606-ff7b-4739-81aa-4861b204ab1e", 
    version: Version, author: "Usami", note: NoteStr, updateInfo: UpdateInfo)]

// ^(?!.*((武僧|机工士|龙骑士|武士|忍者|蝰蛇剑士|舞者|吟游诗人|占星术士|贤者|学者|(朝日|夕月)小仙女|炽天使|白魔法师|战士|骑士|暗黑骑士|绝枪战士|绘灵法师|黑魔法师|青魔法师|召唤师|宝石兽|亚灵神巴哈姆特|亚灵神不死鸟|迦楼罗之灵|泰坦之灵|伊弗利特之灵|后式自走人偶)\] (Used|Cast))).*$

public class DsrPatch
{
    private const string NoteStr =
        """
        基于K佬绝龙诗绘图的个人向补充，
        请先按需求检查并设置“用户设置”栏目。
        开启Debug模式，在忆罪宫输入"/e =TST"可以测试地火特殊跑法。
        鸭门。
        """;
    
    private const string Name = "DSR_Patch [幻想龙诗绝境战 补丁]";
    private const string Version = "0.0.0.12";
    private const string DebugVersion = "a";
    
    private const string UpdateInfo =
        """
        适配新版鸭鸭(4.0.0.0)做了代码修正。
        """;
    
    [UserSetting("Debug模式，非开发用请关闭")]
    public static bool DebugMode { get; set; } = false;
    [UserSetting("站位提示圈绘图-普通颜色")]
    public static ScriptColor PosColorNormal { get; set; } = new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 1.0f, 1.0f) };
    [UserSetting("站位提示圈绘图-玩家站位颜色")]
    public static ScriptColor PosColorPlayer { get; set; } = new ScriptColor { V4 = new Vector4(0.0f, 1.0f, 1.0f, 1.0f) };
    
    public enum ExaflareSpecStrategyEnum
    {
        绝不去前方_NeverFront,
        绝不跑无脑火_NeverUniverse,
        绝不多跑_LeastMovement,
        绝对前方_AlwaysFront,
        关闭_PleaseDontDoThat,
    }
    [UserSetting("地火指路特殊策略")]
    public static ExaflareSpecStrategyEnum ExaflareStrategy { get; set; } = ExaflareSpecStrategyEnum.绝不跑无脑火_NeverUniverse;
    
    [UserSetting("地火（百京核爆）使用程序预设颜色")]
    public static bool ExaflareBuiltInColor { get; set; } = true;
    [UserSetting("地火（百京核爆）爆炸区颜色")]
    public ScriptColor ExaflareColor { get; set; } = new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 0f, 1.0f) };
    [UserSetting("地火（百京核爆）是否绘制下一枚地火预警区")]
    public static bool ExaflareWarnDrawn { get; set; } = true;
    [UserSetting("地火（百京核爆）预警区颜色")]
    public ScriptColor ExaflareWarnColor { get; set; } = new ScriptColor { V4 = new Vector4(0.6f, 0.6f, 1.0f, 1.0f) };
    
    private enum DsrPhase
    {
        Init,                   // 初始
        Phase2Strength,         // P2 一运
        Phase2Sancity,          // P2 二运
        Phase3Nidhogg,          // P3 大师兄
        Phase4Eyes,             // P4 龙眼
        Phase5HeavensWrath,     // P5 一运
        Phase5HeavensDeath,     // P5 二运
        Phase6IceAndFire1,      // P6 一冰火
        Phase6NearOrFar1,       // P6 一远近
        Phase6Flame,            // P6 十字火
        Phase6NearOrFar2,       // P6 二远近
        Phase6IceAndFire2,      // P6 二冰火
        Phase6Cauterize,        // P6 俯冲
        Phase7Exaflare1,        // P7 一地火
        Phase7Stack1,           // P7 一分摊
        Phase7Nuclear1,         // P7 一核爆
        Phase7Exaflare2,        // P7 二地火
        Phase7Stack2,           // P7 二分摊
        Phase7Nuclear2,         // P7 二核爆
        Phase7Exaflare3,        // P7 三地火
        Phase7Stack3,           // P7 三分摊
        Phase7Enrage,           // P7 狂暴
    }
    
    private static Vector3 Center = new Vector3(100, 0, 100);
    private DsrPhase _dsrPhase = DsrPhase.Init;
    private List<bool> _drawn = new bool[20].ToList();                  // 绘图记录
    private volatile List<bool> _recorded = new bool[20].ToList();      // 被记录flag
    private int _pureOfHeartBaitCount = 0;                              // P1/P4.5 纯洁心灵引导次数
    private List<bool> _p2SafeDirection = new bool[8].ToList();         // P2 一运冲锋安全位置
    private Vector3 _p2ThordanPos = new Vector3(0, 0, 0);               // P2 一运托尔丹位置
    private List<uint> _p2TetherKnightId = [0, 0];                      // P2 一运接线骑士ID，顺序左、右
    private DiveFromGrace _diveFromGrace = new DiveFromGrace();         // P3 机制记录
    private int _p3LimitCutStep = 0;                                    // P3 麻将机制流程
    private uint _p3MyTowerPartner = 0;                                 // P3 我要踩的放塔搭档
    private int _p4MirageDiveNum = 0;                                   // P4 幻象冲次数
    private bool _p4PrepareToCenter = false;                            // P4 幻象冲准备回中
    private List<bool> _p4MirageDiveNumFirstRoundTarget = new bool[8].ToList();         // P4 幻象冲第一轮目标
    private List<int> _p4MirageDivePos = [];                            // P4 幻象冲目标方位，左上为0顺时针增加
    private Vector3 _p5VedrfolnirPos = new Vector3(0, 0, 0);            // P5 白龙位置
    private List<bool> _p6DragonsGlowAction = [false, false];           // P6 双龙吐息记录
    private List<bool> _p6DragonsWingAction = [false, false, false];    // P6 双龙远近记录 [远T/近F，左安全T/右安全F，前安全T/后安全F/内安全T/外安全F]
    private List<bool> _p7FirstEnmityOrder = [false, false];            // P7 平A仇恨记录
    private readonly List<int> _p7TrinityOrderIdx = [4, 5, 6, 7, 2, 3]; // P7 接刀顺序
    private bool _p7TrinityDisordered = false;                          // P7 接刀顺序是否出错
    private bool _p7TrinityTankDisordered = false;                      // P7 坦克接刀仇恨是否出错
    private int _p7TrinityNum = 0;                                      // P7 接刀次数
    private DsrExaflare? _p7Exaflare = null;                            // P7 地火Class
    private uint _p7BossId = 0;                                         // P7 boss Id
    
    private ManualResetEvent _ThrustEvent = new(false);
    private ManualResetEvent _ThordanCastAtEdgeEvent = new(false);
    private ManualResetEvent _MirageDiveRound = new(false);
    private ManualResetEvent _p5VedrfolnirPosRecordEvent = new(false);
    private ManualResetEvent _IceAndFireEvent = new(false);
    private ManualResetEvent _NearOrFarWingsEvent = new(false);
    private ManualResetEvent _NearOrFarCauterizeEvent = new(false);
    private ManualResetEvent _NearOrFarInOutEvent = new(false);
    private ManualResetEvent _BladeEvent = new(false);
    private ManualResetEvent _TrinityEvent = new(false);
    
    public void Init(ScriptAccessory accessory)
    {
        accessory.DebugMsg($"Init {Name} v{Version}{DebugVersion} Success.\n{UpdateInfo}", DebugMode);
        accessory.Method.MarkClear();
        accessory.Method.RemoveDraw(".*");
        
        
        _dsrPhase = DsrPhase.Init;
        _drawn = new bool[20].ToList();
        _recorded = new bool[20].ToList();
        _p7BossId = 0;
        _pureOfHeartBaitShown = false;
        
        _ThordanCastAtEdgeEvent = new ManualResetEvent(false);
        _ThrustEvent = new ManualResetEvent(false);
        _MirageDiveRound = new ManualResetEvent(false);
        _p5VedrfolnirPosRecordEvent = new ManualResetEvent(false);
        _IceAndFireEvent = new ManualResetEvent(false);
        _NearOrFarWingsEvent = new ManualResetEvent(false);
        _NearOrFarCauterizeEvent = new ManualResetEvent(false);
        _NearOrFarInOutEvent = new ManualResetEvent(false);
        _BladeEvent = new ManualResetEvent(false);
        _TrinityEvent = new ManualResetEvent(false);
    }
    
    [ScriptMethod(name: "随时DEBUG用", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:=TST2"], userControl: false)]
    public void EchoDebug(Event @event, ScriptAccessory accessory)
    {
        if (!DebugMode) return;
        // ---- DEBUG CODE ----

        _p2ThordanPos = new Vector3(99.99f, 0, 122.97f);
        var spos = new Vector3(91.34f, 0, 95f);  // AT RIGHT
        // var spos = new Vector3(108.66f, 0, 95f);   // AT LEFT

        var atRight = spos.IsAtRight(_p2ThordanPos, Center);
        var tetherEdgePos = _p2ThordanPos.RotatePoint(Center, (atRight ? -1 : 1) * 18f.DegToRad());
        tetherEdgePos = tetherEdgePos.PointInOutside(Center, 3f);
        
        var dp = accessory.DrawGuidance(tetherEdgePos, 0, 2000, $"一运连线指路");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        
        // -- DEBUG CODE END --
    }
    
    #region P1
    
    [ScriptMethod(name: "---- 《P1&P4.5：门神》 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld"],
        userControl: true)]
    public void SplitLine_PhaseDoorBoss(Event @event, ScriptAccessory accessory)
    {
    }
    
    private bool _pureOfHeartBaitShown = false;
    [ScriptMethod(name: "纯洁心灵引导", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25316"], 
        userControl: true)]
    public void PureOfHeartBait(Event @event, ScriptAccessory accessory)
    {
        _pureOfHeartBaitCount = 0;
        _pureOfHeartBaitShown = true;
        // 纯洁心灵引导顺序H1H2, D3D4，D1D2，MTST
        var myIndex = accessory.GetMyIndex();
        // 此处为第一次纯洁心灵，如果非H1H2，不参与
        if (myIndex is not (2 or 3)) return;
        // todo 修改delay与destroy
        DrawPureOfHeartBait(accessory, 0, 15000);
    }

    private void DrawPureOfHeartBait(ScriptAccessory accessory, int delay, int destroy)
    {
        var myIndex = accessory.GetMyIndex();
        Vector3[] baitPos = [new(86.5f, 0.0f, 107.0f), new(86.5f, 0.0f, 103.0f), new(91.5f, 0.0f, 107.0f), new(91.5f, 0.0f, 103.0f)];   //91.5
        var baitPosIdx = myIndex % 2;   // 高在上，低在下
        if (myIndex is 0 or 1 or 6 or 7)
            baitPosIdx += 2;
        for (var posIdx = 0; posIdx < 4; posIdx++)
        {
            var color = baitPosIdx == posIdx ? PosColorPlayer.V4 : PosColorNormal.V4;
            var dp = accessory.DrawStaticCircle(baitPos[posIdx], color, delay, destroy, $"纯洁心灵", 0.5f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            if (baitPosIdx != posIdx) continue;
            var dpGuide = accessory.DrawGuidance(baitPos[posIdx], delay, destroy, $"纯洁心灵指路");
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpGuide);
        }
    }
    
    [ScriptMethod(name: "纯洁心灵引导后续", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:25369"], 
        userControl: false)]
    public void PureOfHeartBaitRest(Event @event, ScriptAccessory accessory)
    {
        if (!_pureOfHeartBaitShown) return;
        if (@event.TargetIndex() != 1) return;
        var myIndex = accessory.GetMyIndex();
        lock (this)
        {
            _pureOfHeartBaitCount++;
            accessory.DebugMsg($"纯洁心灵引导次数：{_pureOfHeartBaitCount}", DebugMode);
            if (_pureOfHeartBaitCount > 6) return;
            var baitDict = new Dictionary<int, int> { { 1, 6 }, { 2, 7 }, { 3, 4 }, { 4, 5 }, { 5, 0 }, { 6, 1 } };
            if (baitDict[_pureOfHeartBaitCount] != myIndex) return;
            accessory.DebugMsg($"开始绘制玩家的纯洁心灵引导", DebugMode);
            DrawPureOfHeartBait(accessory, 0, 5000);
        }
    }
    
    #endregion P1
    
    #region P2

    [ScriptMethod(name: "---- 《P2：骑神托尔丹》 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld"],
        userControl: true)]
    public void SplitLine_PhaseKingThordan(Event @event, ScriptAccessory accessory)
    {
    }
    
    [ScriptMethod(name: "引导不可视刀范围", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25545"])]
    public void P2_AscalonConcealed(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var dp = accessory.DrawFan(sid, float.Pi / 6, 0, 30, 0, 0, 1500, $"不可视刀");
        dp.Color = accessory.Data.DefaultDangerColor.WithW(1.5f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }
    
    [ScriptMethod(name: "一运阶段记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25555"], userControl: false)]
    public void P2_StrengthPhaseRecord(Event @event, ScriptAccessory accessory)
    {
        _dsrPhase = DsrPhase.Phase2Strength;
        _p2SafeDirection = new bool[8].ToList();
        _p2ThordanPos = new Vector3(0, 0, 0);
        _p2TetherKnightId = [0, 0];
        accessory.DebugMsg($"当前阶段为：{_dsrPhase}", DebugMode);
    }
    
    [ScriptMethod(name: "一运冲锋位置记录", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:regex:^(378[123])$"], userControl: false)]
    public void ThurstDirectionRecord(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase2Strength) return;

        var spos = @event.SourcePosition();
        var dir = spos.Position2Dirs(Center, 8);
        lock (_p2SafeDirection)
        {
            _p2SafeDirection[dir % 4] = true;
            accessory.DebugMsg($"List内部true的数量：{_p2SafeDirection.Count(x => x)}", DebugMode);
            if (_p2SafeDirection.Count(x => x) != 3) return;
            _ThrustEvent.Set();
        }
    }
        
    [ScriptMethod(name: "一运分散安全位置指引", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:3781"], userControl: true)]
    public void ThrustSafePosDraw(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase2Strength) return;
        _ThrustEvent.WaitOne();
        
        // 由于在处理_p2SafeDirection时，设置了其方位余4，即一定为0、1、2、3，必能在前4个index中找到唯一的false。
        var safeDir = _p2SafeDirection.IndexOf(false);
        var northPos = new Vector3(100, 0, 80);
        var myIndex = accessory.GetMyIndex();
        var isStGroup = myIndex % 2 == 1;
        // ST组在0、1、2、3
        var tposCenter =
            northPos.RotatePoint(Center, isStGroup ? safeDir * float.Pi / 4 : (safeDir + 4) * float.Pi / 4);
        var tposIn = tposCenter.PointInOutside(Center, 7.5f);
        var tposLeft = tposCenter.RotatePoint(Center, 20f.DegToRad());
        var tposRight = tposCenter.RotatePoint(Center, -20f.DegToRad());
        List<Vector3> tposList = [tposCenter, tposIn, tposLeft, tposRight];

        var dp = accessory.DrawGuidance(tposList[myIndex / 2], 0, 7000, $"P2一运安全区位置{myIndex}");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        
        _ThrustEvent.Reset();
    }
    
    [ScriptMethod(name: "一运分散安全位置指引消失", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:25548"], userControl: false)]
    public void ThrustSafePosRemove(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase2Strength) return;
        var myIndex = accessory.GetMyIndex();

        accessory.Method.RemoveDraw($"P2一运安全区位置{myIndex}");
    }
    
    [ScriptMethod(name: "一运托尔丹边缘位置记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25550"], userControl: false)]
    public void ThordanPosRecord(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase2Strength) return;
        var spos = @event.SourcePosition();
        _p2ThordanPos = spos;
        _ThordanCastAtEdgeEvent.Set();
    }
    
    [ScriptMethod(name: "一运坦克接线提示", eventType: EventTypeEnum.NpcYell, eventCondition: ["Id:regex:^(255[01])$"], userControl: true)]
    public void TankTetherRouteGuidance(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase2Strength) return;
        var myIndex = accessory.GetMyIndex();
        if (myIndex > 1) return;
        _ThordanCastAtEdgeEvent.WaitOne();
        lock (_p2TetherKnightId)
        {
            var sid = @event.SourceId();
            var sname = @event.SourceName();
            var spos = @event.SourcePosition();
            var rad = spos.FindRadian(_p2ThordanPos);
            
            var atRight = spos.IsAtRight(_p2ThordanPos, Center);
            _p2TetherKnightId[atRight ? 1 : 0] = sid;
            
            // 此处Id为16进制转10进制表示
            accessory.DebugMsg($"记录{sname}（对话{@event.Id()}）在{(atRight ? "右" : "左")}", DebugMode);

            if (_p2TetherKnightId.Contains(0)) return;
            var targetKnightIdx = myIndex == 0 ? 0 : 1;
            var chara = accessory.GetById(_p2TetherKnightId[targetKnightIdx]);
            if (chara == null) return;
            
            var knightPos = chara.Position;
            var tetherEdgePos = _p2ThordanPos.RotatePoint(Center, (myIndex == 0 ? 1 : -1) * 18f.DegToRad());
            tetherEdgePos = tetherEdgePos.PointInOutside(Center, 3f);
            var dp = accessory.DrawGuidance(knightPos, tetherEdgePos, 0, 10000, $"接线路径");
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }
    }
    
    [ScriptMethod(name: "一运接线提示删除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:25550"], userControl: false)]
    public void TankTetherRouteGuidanceRemove(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase2Strength) return;
        accessory.Method.RemoveDraw($"接线路径");
        _ThordanCastAtEdgeEvent.Reset();
    }
    
    [ScriptMethod(name: "二运阶段记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25569"], userControl: false)]
    public void P2_SancityPhaseRecord(Event @event, ScriptAccessory accessory)
    {
        _dsrPhase = DsrPhase.Phase2Sancity;
        accessory.DebugMsg($"当前阶段为：{_dsrPhase}", DebugMode);
    }
    
    #endregion P2

    #region P3
    
    [ScriptMethod(name: "---- 《P3：尼德霍格》 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld"],
        userControl: true)]
    public void SplitLine_PhaseNidhogg(Event @event, ScriptAccessory accessory)
    {
    }
    
    [ScriptMethod(name: "P3：阶段记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:26376"], userControl:false)]
    public void P3_PhaseRecord(Event @event, ScriptAccessory accessory)
    {
        _dsrPhase = DsrPhase.Phase3Nidhogg;
        _diveFromGrace = new DiveFromGrace();
        _diveFromGrace.accessory = accessory;
        _p3LimitCutStep = 0;
        _p3MyTowerPartner = 0;
        accessory.DebugMsg($"当前阶段为：{_dsrPhase}", DebugMode);
    }

    public class DiveFromGrace()
    {
        public ScriptAccessory accessory { get; set; } = null;
        public List<uint> LimitCut1 { get; set; } = [];
        public List<uint> LimitCut2 { get; set; } = [];
        public List<uint> LimitCut3 { get; set; } = [];
        public int RecordedPlayerNum { get; set; } = 0;
        public List<bool> LimitCutFixed { get; set; } = [false, false, false];
        public List<uint> LeftLimitCut { get; set; } = [];
        public List<uint> RightLimitCut { get; set; } = [];
        public List<uint> MiddleLimitCut { get; set; } = [];

        private const int Left = 0;
        private const int Middle = 1;
        private const int Right = 2;

        private const int First = 0;
        private const int Second = 1;
        private const int Third = 2;

        // 放塔倒计时
        private const int FirstDuration = 9000;
        private const int SecondDuration = 19000;
        private const int ThirdDuration = 29000;

        public void LimitCutAdd(uint id, int idx)
        {
            switch (idx)
            {
                case First:
                    LimitCut1.Add(id);
                    break;
                case Second:
                    LimitCut2.Add(id);
                    break;
                case Third:
                    LimitCut3.Add(id);
                    break;
                default:
                    return;
            }
        }
        
        /// <summary>
        /// 添加麻将方位
        /// </summary>
        /// <param name="id">玩家id</param>
        /// <param name="pos">方位，0左1中2右</param>
        public void LimitCutPosAdd(uint id, int pos)
        {
            switch (pos)
            {
                case Left:
                    LeftLimitCut.Add(id);
                    break;
                case Middle:
                    MiddleLimitCut.Add(id);
                    break;
                case Right:
                    RightLimitCut.Add(id);
                    break;
                default:
                    return;
            }
        }
        
        /// <summary>
        /// 找到对应玩家的麻将序号
        /// </summary>
        /// <param name="id">玩家id</param>
        /// <returns>序号，0一麻 1二麻 2三麻</returns>
        public int FindPlayerLimitCutIndex(uint id)
        {
            if (LimitCut1.Contains(id))
                return First;
            if (LimitCut2.Contains(id))
                return Second;
            if (LimitCut3.Contains(id))
                return Third;
            return -1;
        }
        
        /// <summary>
        /// 找到对应玩家的麻将放置方位
        /// </summary>
        /// <param name="id">玩家id</param>
        /// <returns>方位，0左 1中 2右</returns>
        public int FindPlayerLimitCutPos(uint id)
        {
            if (LeftLimitCut.Contains(id))
                return Left;
            if (MiddleLimitCut.Contains(id))
                return Middle;
            if (RightLimitCut.Contains(id))
                return Right;
            return -1;
        }

        /// <summary>
        /// 通过玩家id设置麻将位置是否确定
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public void LimitCutFixedSet(uint id)
        {
            var idx = FindPlayerLimitCutIndex(id);
            LimitCutFixed[idx] = true;
        }

        public bool AllPlayerRecorded()
        {
            return RecordedPlayerNum == 8;
        }

        private List<uint> GetLimitCutPlayers(int idx)
        {
            return idx switch
            {
                First => LimitCut1,
                Second => LimitCut2,
                Third => LimitCut3,
                _ => []
            };
        }
        
        private List<uint> GetLimitCutPosPlayers(int pos)
        {
            return pos switch
            {
                Left => LeftLimitCut,
                Middle => MiddleLimitCut,
                Right => RightLimitCut,
                _ => []
            };
        }
        
        /// <summary>
        /// 找到同组麻将中未被放入方位的玩家
        /// </summary>
        /// <returns></returns>
        private List<uint> FindUnaddedPlayer(int idx)
        {
            List<uint> unaddedPlayers = [];
            var players = GetLimitCutPlayers(idx);
            foreach (var player in players)
            {
                if (FindPlayerLimitCutPos(player) == -1)
                    unaddedPlayers.Add(player);
            }
            return unaddedPlayers;
        }

        public void SetRemainingPlayers()
        {
            // 第一步，先根据fixed情况找到对应的原地玩家，放置到Center
            for (var idx = 0; idx < 3; idx++)
            {
                if (!LimitCutFixed[idx]) continue;
                var unaddedPlayers = FindUnaddedPlayer(idx);
                if (unaddedPlayers.Count == 1)
                    LimitCutPosAdd(unaddedPlayers[0], Middle);
            }
            // 第二步，没有fixed的就是全大圈，根据他们的位置放置到不同方位
            for (var idx = 0; idx < 3; idx++)
            {
                if (LimitCutFixed[idx]) continue;
                
                var unaddedPlayers = GetLimitCutPlayers(idx);
                var sortedPlayerIds = unaddedPlayers
                    .OrderBy(playerId => accessory.GetById(playerId).Position.X)
                    .ToList();
                
                if (idx == Middle)
                {
                    LimitCutPosAdd(sortedPlayerIds[0], Left);
                    LimitCutPosAdd(sortedPlayerIds[1], Right);
                }
                else
                {
                    LimitCutPosAdd(sortedPlayerIds[0], Left);
                    LimitCutPosAdd(sortedPlayerIds[1], Middle);
                    LimitCutPosAdd(sortedPlayerIds[2], Right);
                }
            }
        }

        /// <summary>
        /// 可能因为踩的位置不同需要重置
        /// </summary>
        /// <param name="idx"></param>
        public void ResetPlayers(int idx)
        {
            LeftLimitCut.RemoveAll(x => GetLimitCutPlayers(idx).Contains(x));
            MiddleLimitCut.RemoveAll(x => GetLimitCutPlayers(idx).Contains(x));
            RightLimitCut.RemoveAll(x => GetLimitCutPlayers(idx).Contains(x));
        }
        
        public string ShowPlayerAction(uint id)
        {
            var idx = FindPlayerLimitCutIndex(id);
            var pos = FindPlayerLimitCutPos(id);
            var str = $"玩家为{idx + 1}麻，位置在{pos switch
            {
                0 => "左",
                1 => "中",
                2 => "右",
                _ => "未知"
            }}";
            return str;
        }

        /// <summary>
        /// 输入玩家id，得到需要踩对方塔的搭档玩家id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public uint FindTowerPartner(uint id)
        {
            var idx = FindPlayerLimitCutIndex(id);
            var pos = FindPlayerLimitCutPos(id);
            
            // 第一步，确定对方的麻将序号
            // 一般情况下，first -> second, second -> third, third -> first
            var idxPartner = (idx + 1) % 3;
            // 特殊情况，second无middle，即first -> third
            if (idxPartner == Second && pos == Middle)
                idxPartner = Third;

            // 第二步，如果对方是带箭头的组，且自己是left or right，要交换pos
            // 即，你去左，但你的左塔是右侧玩家释放
            if (LimitCutFixed[idxPartner])
                pos = pos switch
                {
                    Left => Right,
                    Right => Left,
                    _ => Middle
                };
            
            return FindSpecificPlayer(idxPartner, pos);
        }

        /// <summary>
        /// 输入麻将序号与方向，找到特定玩家
        /// </summary>
        /// <param name="idx">麻将序号</param>
        /// <param name="pos">方向</param>
        /// <returns></returns>
        public uint FindSpecificPlayer(int idx, int pos)
        {
            var ls1 = GetLimitCutPlayers(idx);
            var ls2 = GetLimitCutPosPlayers(pos);
            return ls1.FirstOrDefault(player => ls2.Contains(player));
        }

        /// <summary>
        /// 输入麻将序号与方向，获得放塔基础坐标（不含钢铁月环）
        /// </summary>
        /// <param name="idx"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
        public Vector3 GetTowerPosV3(int idx, int pos)
        {
            if (idx == Second)
            {
                switch (pos)
                {
                    case Left:
                        return new Vector3(91.75f, 0, 90.8f);
                    case Right:
                        return new Vector3(108.25f, 0, 90.8f);
                }
            }
            else
            {
                switch (pos)
                {
                    // TODO 箭头上距离不确定，需验证数值
                    case Left:
                        return new Vector3(Center.X - 7.5f, 0, Center.Z);
                    case Middle:
                        return new Vector3(Center.X, 0, Center.Z + 7.5f);
                    case Right:
                        return new Vector3(Center.X + 7.5f, 0, Center.Z);
                }
            }
            return new Vector3(0, 0, 0);
        }
    }
    
    [ScriptMethod(name: "P3：麻将记录", eventType: EventTypeEnum.StatusAdd, eventCondition:["StatusID:regex:^(300[456])$"], userControl: false)]
    public void P3_LimitCutRecord(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase3Nidhogg) return;
        var stid = @event.StatusId();
        var tid = @event.TargetId();

        const uint limitCut1 = 3004;
        const uint limitCut2 = 3005;
        const uint limitCut3 = 3006;
        _diveFromGrace.LimitCutAdd(tid, (int)stid - 3004);
    }
    
    [ScriptMethod(name: "P3：箭头记录", eventType: EventTypeEnum.StatusAdd, eventCondition:["StatusID:regex:^(275[567])$"], userControl: false)]
    public void P3_LimitCutPosRecord(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase3Nidhogg) return;
        var stid = @event.StatusId();
        var tid = @event.TargetId();
        
        const uint front = 2756;
        const uint behind = 2757;
        const uint inPlace = 2755;

        const int left = 0;
        const int middle = 1;
        const int right = 2;

        lock (_diveFromGrace)
        {
            switch (stid)
            {
                case front:
                    _diveFromGrace.LimitCutPosAdd(tid, right);
                    _diveFromGrace.LimitCutFixedSet(tid);
                    break;
                case behind:
                    _diveFromGrace.LimitCutPosAdd(tid, left);
                    _diveFromGrace.LimitCutFixedSet(tid);
                    break; 
            }
            _diveFromGrace.RecordedPlayerNum++;
        }
        
        if (!_diveFromGrace.AllPlayerRecorded()) return;
        accessory.DebugMsg($"LimitCutFixed:{_diveFromGrace.LimitCutFixed.StringList()}", DebugMode);
        _diveFromGrace.SetRemainingPlayers();
        _p3MyTowerPartner = _diveFromGrace.FindTowerPartner(accessory.Data.Me);
        accessory.DebugMsg($"我需要踩他的塔：{_diveFromGrace.ShowPlayerAction(_p3MyTowerPartner)}", DebugMode);
    }
    
    [ScriptMethod(name: "麻将流程，放塔与分摊", eventType: EventTypeEnum.StartCasting, eventCondition:["ActionId:regex:^(2638[67])$"])]
    public void P3_LimitCutAction(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase3Nidhogg) return;
        _p3LimitCutStep++;

        var aid = @event.ActionId();
        const int chariotFirst = 26386;
        const int donutFirst = 26387;

        var myId = accessory.Data.Me;
        var idx = _diveFromGrace.FindPlayerLimitCutIndex(myId);
        var pos = _diveFromGrace.FindPlayerLimitCutPos(myId);
        var towerPos = _diveFromGrace.GetTowerPosV3(idx, pos);
        
        const int First = 0;
        const int Second = 1;
        const int Third = 2;
        
        const int Left = 0;
        const int Middle = 1;
        const int Right = 2;

        var posStr = pos switch
        {
            Left => "左",
            Middle => "中",
            Right => "右",
            _ => "未知"
        };

        const int lashGnashCastTime = 7600;
        const int inOutCastFirst = 3700;
        const int inOutCastSecond = 3100;
        const int towerExistTime = 6800;

        if (_p3LimitCutStep == 1)
        {
            switch (idx)
            {
                case First:
                {
                    accessory.DebugMsg($"一麻{posStr}第一轮，先去{posStr}{towerPos}放塔，再回人群", DebugMode);
                    DrawTowerDir(towerPos, 0, lashGnashCastTime, $"放塔1", accessory);
                    DrawTowerPosDir(towerPos, pos, 0, lashGnashCastTime, $"放塔1面向", accessory, _diveFromGrace.LimitCutFixed[idx]);
                    DrawBackToGroup(lashGnashCastTime, towerExistTime, $"人群", accessory);
                    break;
                }
                case Second:
                {
                    accessory.DebugMsg($"二麻{posStr}第一轮，先回人群，再去{posStr}{towerPos}放塔", DebugMode);
                    DrawBackToGroup(0, lashGnashCastTime, $"人群", accessory);
                    const int jump2DelayTime = lashGnashCastTime + inOutCastFirst + inOutCastSecond;
                    const int jump2Destroy = 17700 - jump2DelayTime;  // 17700 从下方时间节点处取
                    DrawTowerDir(towerPos, jump2DelayTime, jump2Destroy, $"放塔2", accessory);
                    DrawTowerPosDir(towerPos, pos, jump2DelayTime, jump2Destroy, $"放塔2面向", accessory, _diveFromGrace.LimitCutFixed[idx]);
                    break;
                }
                case Third:
                {
                    accessory.DebugMsg($"三麻{posStr}第一轮，回人群", DebugMode);
                    DrawBackToGroup(0, lashGnashCastTime, $"人群", accessory);
                    break;
                }
            }
        }
        else
        {
            switch (idx)
            {
                // 第二轮钢铁月环，一麻准备分摊
                case First:
                {
                    // 分摊前需先引导，引导位置可以作个向外指的指引
                    // 因为有先引导，回去分摊的延时和消失时间由时间节点计算
                    if (pos != Middle)
                    {
                        accessory.DebugMsg($"一麻{posStr}第二轮，引导后回人群", DebugMode);
                        DrawBackToGroup(26900 - 21500, 28900 - 26900, $"分摊", accessory);
                    }
                    else
                    {
                        accessory.DebugMsg($"一麻{posStr}第二轮，回人群", DebugMode);
                        DrawBackToGroup(0, lashGnashCastTime, $"分摊", accessory);
                    }
                    break;
                }
                case Second:
                {
                    accessory.DebugMsg($"二麻{posStr}第二轮，回人群", DebugMode);
                    DrawBackToGroup(0, lashGnashCastTime, $"分摊", accessory);
                    break;
                }
                case Third:
                {
                    accessory.DebugMsg($"三麻{posStr}第二轮，先去{posStr}{towerPos}放塔，再回人群", DebugMode);
                    DrawTowerDir(towerPos, 0, lashGnashCastTime, $"放塔", accessory);
                    DrawTowerPosDir(towerPos, pos, 0, lashGnashCastTime, $"放塔3面向", accessory, _diveFromGrace.LimitCutFixed[idx]);
                    DrawBackToGroup(lashGnashCastTime, towerExistTime, $"人群", accessory);
                    break;
                }
            }
        }
    }
    
    [ScriptMethod(name: "麻将流程，踩塔", eventType: EventTypeEnum.ActionEffect, eventCondition:["ActionId:regex:^(2638[234])$", "TargetIndex:1"])]
    public void P3_TowerAfterPlaced(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase3Nidhogg) return;
        var tid = @event.TargetId();
        var aid = @event.ActionId();
        var sid = @event.SourceId();
        var towerPos = @event.SourcePosition();
        var isMyTower = tid == _p3MyTowerPartner;
        if (isMyTower)
        {
        }

        const int towerExistTime = 6800;
        DrawTowerRange(sid, 0, towerExistTime, $"踩塔", accessory, isMyTower, aid);
    }
    
    private DrawPropertiesEdit DrawTowerDir(Vector3 towerPos, int delay, int destroy, string name, ScriptAccessory accessory, bool draw = true)
    {
        var dp = accessory.DrawDirPos(towerPos, delay, destroy, name);
        if (draw)
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        return dp;
    }
    private DrawPropertiesEdit DrawTowerPosDir(Vector3 towerPos, int pos, int delay, int destroy, string name, ScriptAccessory accessory, bool draw = true)
    {
        const int Left = 0;
        const int Middle = 1;
        const int Right = 2;

        var rotation = pos switch
        {
            Left => -float.Pi / 2,
            Right => -float.Pi / 2,
            _ => 0
        };
        var targetPos = towerPos.ExtendPoint(rotation, 3.1f);
        var dp = accessory.DrawDirPos2Pos(towerPos, targetPos, delay, destroy, name);
        dp.Scale = new Vector2(3f);
        dp.Color = ColorHelper.ColorYellow.V4;
        if (draw && pos != Middle)
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        return dp;
    }
    
    private DrawPropertiesEdit DrawBackToGroup(int delay, int destroy, string name, ScriptAccessory accessory, bool draw = true)
    {
        var stackPos = new Vector3(100, 0, 92);
        var dp = accessory.DrawDirPos(stackPos, delay, destroy, name);
        if (draw)
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        return dp;
    }

    private DrawPropertiesEdit[] DrawTowerRange(uint sid, int delay, int destroy, string name,
        ScriptAccessory accessory, bool isSafe, uint type, bool draw = true)
    {
        const uint inPlace = 26382;
        const uint front = 26383;
        const uint behind = 26384;
        const int towerCastingTime = 3000;
        const int syncTime = 300;
        
        // 后面生成塔位置的sid已经不是原来的sid了，需要在这里找到他经偏置后的位置
        var tpos = FindTowerAppearPos(sid, type, accessory);
        
        accessory.DebugMsg($"画出塔范围，{(isSafe ? "我的" : "不是我的")}塔", DebugMode);
        var color = isSafe ? accessory.Data.DefaultSafeColor.WithW(1.5f) : accessory.Data.DefaultDangerColor;
        var dp = accessory.DrawCircle(sid, 5f, delay, destroy - towerCastingTime, $"踩塔{sid}");
        dp.Color = color;
        dp.Offset = type == inPlace ? new Vector3(0, 0, 0) : new Vector3(0, 0, -14);
        var dp1 = accessory.DrawStaticCircle(tpos, color, destroy - towerCastingTime + syncTime, towerCastingTime, $"踩塔确定{sid}", 5f);
        if (draw)
        {
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp1);
        }
        
        if (!isSafe) return [dp, dp1];
        
        accessory.DebugMsg($"准备去踩塔", DebugMode);
        var dp01 = accessory.DrawDirPos(tpos, destroy - towerCastingTime + syncTime, towerCastingTime, $"踩塔指路确定{sid}");
        if (draw)
        {
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp01);
        }
        return [dp, dp1, dp01];
    }
    
    private Vector3 FindTowerAppearPos(uint sid, uint type, ScriptAccessory accessory)
    {
        const uint inPlace = 26382;
        const uint front = 26383;
        const uint behind = 26384;
        
        var chara = accessory.GetById(sid);
        var srot = chara.Rotation;
        var spos = chara.Position;
        
        if (type == inPlace) return spos;
        var newPos = spos.ExtendPoint(srot.Game2Logic(), 14);
        return newPos;
    }
    
    // 0        Casting LashGnash           0
    // +7600    Stack #1 + Jump #1          7600
    // +3700    Chariot/Donut #1            11300
    // +3100    Donut/Chariot #1            14400
    // +0       Towers #1                   14400
    // +2500    StartCast Geirskogul #1     16900
    // +800     Jump #2                     17700
    // +3800    Casting LashGnash           21500
    // +2800    Towers #2                   24300
    // +2600    StartCast Geirskogul #2     26900
    // +2200    Stack #2 + Jump #3          28900
    // +3700    Chariot/Donut #2            32600
    // +3100    Donut/Chariot #2            35700
    // +0       Towers #3                   35700
    // +2000    StartCast Geirskogul #3     37700
    // +4500    Geirskogul #3               42200
    
    // TowerExistTime       6800, 6600, 6800
    // PlaceTowerTimeNode   7600, 17700, 28900
    
    #endregion P3

    #region P4

    [ScriptMethod(name: "---- 《P4：龙眼》 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld"],
        userControl: true)]
    public void SplitLine_PhaseEyes(Event @event, ScriptAccessory accessory)
    {
    }

    [ScriptMethod(name: "P4阶段记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2748"],
        userControl: false)]
    public void P4_EyesPhaseRecord(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase == DsrPhase.Phase4Eyes) return;
        _dsrPhase = DsrPhase.Phase4Eyes;
        _p4MirageDiveNum = 0;
        _p4MirageDiveNumFirstRoundTarget = new bool[8].ToList();
        _p4MirageDivePos = [];
        _p4PrepareToCenter = false;
        accessory.DebugMsg($"当前阶段为：{_dsrPhase}", DebugMode);
    }
    
    [ScriptMethod(name: "开场就位提示", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2748"],
        userControl: true)]
    public void EyesTargetMention(Event @event, ScriptAccessory accessory)
    {
        var tid = @event.TargetId();
        if (tid != accessory.Data.Me) return;
        var myIndex = accessory.GetMyIndex();
        // MT D1 D2 H1
        var isBlueEye = myIndex is 0 or 2 or 4 or 5;
        var isTank = myIndex is 0 or 1;
        accessory.Method.TextInfo($"{(isTank ? "开启盾姿，" : "")}{(isBlueEye ? "左侧蓝球" : "右侧红球")}就位", 3000, isTank);
    }
    
    [ScriptMethod(name: "红蓝Buff置换提示", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(277[56])$"],
        userControl: true)]
    public void EyesBuffExchange(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase4Eyes) return;
        var tid = @event.TargetId();
        if (tid != accessory.Data.Me) return;
        const uint redBuff = 2775;
        const uint blueBuff = 2776;
        var stid = @event.StatusId();
        var myIndex = accessory.GetMyIndex();
        if (_drawn[0]) return;
        _drawn[0] = true;
        
        var needChange = (myIndex < 4 && stid != blueBuff) || (myIndex >= 4 && stid != redBuff);
        if (!needChange) return;
        var dp = accessory.DrawGuidance(Center, 0, 5000, $"红蓝Buff置换");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        accessory.Method.TextInfo($"场中换Buff", 3000);
    }
    
    [ScriptMethod(name: "红蓝Buff置换消除", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(277[56])$"],
        userControl: false)]
    public void EyesBuffExchangeRemove(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase4Eyes) return;
        var tid = @event.TargetId();
        if (tid != accessory.Data.Me) return;
        const uint redBuff = 2775;
        const uint blueBuff = 2776;
        var stid = @event.StatusId();
        var myIndex = accessory.GetMyIndex();
        
        var changeComplete = (myIndex < 4 && stid == blueBuff) || (myIndex >= 4 && stid == redBuff);
        if (!changeComplete) return;
        accessory.Method.RemoveDraw($"红蓝Buff置换");
    }
    
    [ScriptMethod(name: "DPS撞球提示", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(1260[78])$"],
        userControl: true)]
    public void PobYellowOrbsGuidance(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase4Eyes) return;
        if (_drawn[1]) return;
        _drawn[1] = true;
        // 球出现开始计时
        var myIndex = accessory.GetMyIndex();
        if (myIndex < 4) return;

        var orbPos = new Vector3(83, 0, 100);
        if (myIndex is 6 or 7)
            orbPos = orbPos.FoldPointHorizon(Center.X);
        
        // 要细致的话，需要找到球什么时候变大的时间点
        var dp0 = accessory.DrawGuidance(orbPos, 4000, 2000, $"DPS撞球准备");
        dp0.Color = accessory.Data.DefaultDangerColor;
        var dp1 = accessory.DrawGuidance(orbPos, 6000, 5000, $"DPS撞球");
        dp1.Color = accessory.Data.DefaultSafeColor;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp1);
    }
    
    [ScriptMethod(name: "DPS撞球提示消失", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:26817"],
        userControl: false)]
    public void PobYellowOrbsGuidanceRemove(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase4Eyes) return;
        var tid = @event.TargetId();
        if (tid != accessory.Data.Me) return;
        var myIndex = accessory.GetMyIndex();
        if (myIndex < 4) return;
        accessory.Method.RemoveDraw($"DPS撞球.*");
    }
    
    [ScriptMethod(name: "TN撞球提示", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(1260[78])$"],
        userControl: true)]
    public void PobBlueOrbsGuidance(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase4Eyes) return;
        if (_drawn[2]) return;
        _drawn[2] = true;
        // 球出现开始计时
        var myIndex = accessory.GetMyIndex();
        if (myIndex >= 4) return;

        var orbPos = new Vector3(90, 0, 93);
        if (myIndex >= 2)
            orbPos = orbPos.FoldPointVertical(Center.Z);
        if (myIndex % 2 == 1)
            orbPos = orbPos.FoldPointHorizon(Center.X);
        
        // accessory.Method.TextInfo($"与DPS换Buff", 2500);
        var dp0 = accessory.DrawGuidance(orbPos, 10000, 2000, $"TN撞球准备");
        dp0.Color = accessory.Data.DefaultDangerColor;
        var dp1 = accessory.DrawGuidance(orbPos, 12000, 5000, $"TN撞球");
        dp1.Color = accessory.Data.DefaultSafeColor;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp1);
    }
    
    [ScriptMethod(name: "TN撞球前换Buff提示", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:26817"],
        userControl: true)]
    public void BuffExchangeHintBeforePobBlueOrbs(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase4Eyes) return;
        if (_drawn[5]) return;
        _drawn[5] = true;
        // 球出现开始计时
        var myIndex = accessory.GetMyIndex();
        if (myIndex >= 4) return;
        
        accessory.Method.TextInfo($"与DPS换Buff", 2500);
    }
    
    [ScriptMethod(name: "TN撞球提示消失", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:26815"],
        userControl: false)]
    public void PobBlueOrbsGuidanceRemove(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase4Eyes) return;
        var tid = @event.TargetId();
        if (tid != accessory.Data.Me) return;
        var myIndex = accessory.GetMyIndex();
        if (myIndex >= 4) return;
        accessory.Method.RemoveDraw($"TN撞球.*");
    }
    
    [ScriptMethod(name: "幻象冲初始就位提示", eventType: EventTypeEnum.RemoveCombatant, eventCondition: ["DataId:12607"],
        userControl: true)]
    public void MirageDiveStandPosMention(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase4Eyes) return;
        if (_drawn[3]) return;
        _drawn[3] = true;

        Vector3 targetPos;
        var myIndex = accessory.GetMyIndex();
        if (myIndex >= 4)
            targetPos = new(90, 0, 100);
        else
        {
            targetPos = new(84.5f, 0, 94.5f);
            targetPos = targetPos.RotatePoint(new(90, 0, 100), myIndex * 90f.DegToRad());
        }
        var dp = accessory.DrawGuidance(targetPos, 0, 5000, $"幻象冲就位提示");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }
    
    [ScriptMethod(name: "幻象冲次数与目标记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:26820", "TargetIndex:1"],
        userControl: false)]
    public void MirageDiveNumRecord(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase4Eyes) return;
        var tid = @event.TargetId();
        var tidx = accessory.GetPlayerIdIndex(tid);
        lock (_p4MirageDiveNumFirstRoundTarget)
        {
            _p4MirageDiveNum++;
            if (_p4MirageDiveNum <= 2)
                _p4MirageDiveNumFirstRoundTarget[tidx] = true;
        }

        lock (_p4MirageDivePos)
        {
            var tpos = @event.TargetPosition();
            var tdir = tpos.Position2Dirs(new Vector3(90, 0, 100), 4, false);
            _p4MirageDivePos.Add((tdir + 1) % 4);
            if (_p4MirageDivePos.Count != 2) return;
            _p4MirageDivePos.Sort();
            _MirageDiveRound.Set();
        }
    }
    
    [ScriptMethod(name: "幻象冲等待回中提示", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:26820", "TargetIndex:1"],
        userControl: true)]
    public void MirageDiveBackToCenterMentionAwait(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase4Eyes) return;
        if (_p4PrepareToCenter) return;
        var tid = @event.TargetId();
        if (tid != accessory.Data.Me) return;
        if (_p4MirageDiveNum > 6) return;
        _p4PrepareToCenter = true;
        
        var dp = accessory.DrawGuidance(new Vector3(90, 0, 100), 0, 5000, $"幻象冲等待回中提示");
        dp.Color = accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        accessory.DebugMsg($"玩家受到伤害，准备回中", DebugMode);
    }
    
    [ScriptMethod(name: "幻象冲回中提示", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2776"],
        userControl: true)]
    public void MirageDiveBackToCenterMention(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase4Eyes) return;
        if (!_p4PrepareToCenter) return;
        var tid = @event.TargetId();
        if (tid != accessory.Data.Me) return;
        if (_p4MirageDiveNum > 6) return;
        _p4PrepareToCenter = false;
        
        accessory.Method.RemoveDraw($"幻象冲等待回中提示");
        var dp = accessory.DrawGuidance(new Vector3(90, 0, 100), 0, 2500, $"幻象冲回中提示");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        accessory.DebugMsg($"玩家Buff交换完毕，回中", DebugMode);
    }
    
    [ScriptMethod(name: "幻象冲交换提示", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:26820", "TargetIndex:1"],
        userControl: true)]
    public void MirageDiveSwapMention(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase4Eyes) return;
        if (_drawn[4]) return;
        _drawn[4] = true;
        _MirageDiveRound.WaitOne();
        
        _drawn[4] = false;
        _MirageDiveRound.Reset();
        
        if (_p4MirageDiveNum > 6) return;
        var highPriorityPlayer = _p4MirageDiveNum switch
        {
            2 => 4,
            4 => 6,
            6 => _p4MirageDiveNumFirstRoundTarget.IndexOf(true),
            _ => 0,
        };
        var lowPriorityPlayer = _p4MirageDiveNum switch
        {
            2 => 5,
            4 => 7,
            6 => _p4MirageDiveNumFirstRoundTarget.LastIndexOf(true),
            _ => 0,
        };
        
        var basePos = new Vector3(84.5f, 0, 94.5f);
        var highPriorityPos = basePos.RotatePoint(new(90, 0, 100), _p4MirageDivePos[0] * 90f.DegToRad());
        var lowPriorityPos = basePos.RotatePoint(new(90, 0, 100), _p4MirageDivePos[1] * 90f.DegToRad());

        var highPriorityPlayerJob = accessory.GetPlayerJobByIndex(highPriorityPlayer);
        var lowPriorityPlayerJob = accessory.GetPlayerJobByIndex(lowPriorityPlayer);
        var myIndex = accessory.GetMyIndex();

        if (myIndex == highPriorityPlayer)
        {
            var dp = accessory.DrawGuidance(highPriorityPos, 0, 5000, $"高优先级就位{highPriorityPlayer}");
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }
        if (myIndex == lowPriorityPlayer)
        {
            var dp = accessory.DrawGuidance(lowPriorityPos, 0, 5000, $"低优先级就位{lowPriorityPlayer}");
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }

        var str = "";
        str += $"第{_p4MirageDiveNum / 2}轮，高优先级{highPriorityPlayerJob}去{_p4MirageDivePos[0]}号位\n";
        str += $"第{_p4MirageDiveNum / 2}轮，低优先级{lowPriorityPlayerJob}去{_p4MirageDivePos[1]}号位";
        accessory.DebugMsg(str, DebugMode);
        _p4MirageDivePos.Clear();
    }
    
    #endregion P4
    
    #region P5

    [ScriptMethod(name: "---- 《P5：伪典托尔丹》 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld"],
        userControl: true)]
    public void SplitLine_PhaseAlternateThordan(Event @event, ScriptAccessory accessory)
    {
    }
    
    [ScriptMethod(name: "P5：一运，阶段记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27529"], userControl: false)]
    public void P5_HeavensWrath_PhaseRecord(Event @event, ScriptAccessory accessory)
    {
        _dsrPhase = DsrPhase.Phase5HeavensWrath;
        accessory.DebugMsg($"当前阶段为：{_dsrPhase}", DebugMode);
    }

    [ScriptMethod(name: "旋风冲旋风预警", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27531"])]
    public async void P5_TwistingDive(Event @event, ScriptAccessory accessory)
    {
        DrawTwister(3000, 3000, accessory);
        await Task.Delay(3000);
        accessory.Method.TextInfo("旋风", 3000, true);
    }

    [ScriptMethod(name: "旋风危险位置", eventType: EventTypeEnum.ObjectChanged, eventCondition: ["DataId:2001168", "Operate:Add"])]
    public void TwisterField(Event @event, ScriptAccessory accessory)
    {
        var spos = @event.SourcePosition();
        var dp = accessory.DrawStaticCircle(spos, ColorHelper.ColorRed.V4.WithW(3), 0, 4000, $"旋风{spos}");
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }
    
    private void DrawTwister(int delay, int destroy, ScriptAccessory accessory)
    {
        for (var i = 0; i < accessory.Data.PartyList.Count; i++)
        {
            var dp = accessory.DrawCircle(accessory.Data.PartyList[i], 1.5f, delay, destroy, $"旋风{i}", true);
            dp.Color = accessory.Data.DefaultDangerColor.WithW(2f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    [ScriptMethod(name: "大圈火预警", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:25573"])]
    public void P5_AlterFlare(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase5HeavensWrath) return;
        var spos = @event.SourcePosition();
        var dp = accessory.DrawStaticCircle(spos, ColorHelper.ColorRed.V4.WithW(1.5f), 0, 4000, $"大圈火危险区", 8f);
        dp.ScaleMode |= ScaleMode.ByTime;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }
    
    [ScriptMethod(name: "一运白龙位置记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27531"],
        userControl: false)]
    public void VedrfolnirPosRecord(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase5HeavensWrath) return;
        var spos = @event.SourcePosition();
        _p5VedrfolnirPos = spos;
        _p5VedrfolnirPosRecordEvent.Set();
    }
    
    [ScriptMethod(name: "一运连线指路", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0005"],
        userControl: true)]
    public void SpiralPierceTetherGuidance(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase5HeavensWrath) return;
        _p5VedrfolnirPosRecordEvent.WaitOne();
        var tid = @event.TargetId();
        if (tid != accessory.Data.Me) return;
        var spos = @event.SourcePosition();
        var atRight = spos.IsAtRight(_p5VedrfolnirPos, Center);
        var targetPos = spos.RotatePoint(Center, (atRight ? 1 : -1) * 172.5f.DegToRad());
        
        targetPos = targetPos.PointInOutside(Center, 2f);
        var dp = accessory.DrawGuidance(targetPos, 0, 8000, $"一运连线指路");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }
    
    [ScriptMethod(name: "一运连线指路消失", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:27530"],
        userControl: false)]
    public void SpiralPierceTetherGuidanceRemove(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase5HeavensWrath) return;
        accessory.Method.RemoveDraw($"一运连线指路");
    }
    
    [ScriptMethod(name: "一运穿天指路", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:000E"],
        userControl: true)]
    public void SkywardLeapGuidance(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase5HeavensWrath) return;
        _p5VedrfolnirPosRecordEvent.WaitOne();
        var tid = @event.TargetId();
        if (tid != accessory.Data.Me) return;
        
        var targetPos = _p5VedrfolnirPos.RotatePoint(Center, -67.5f.DegToRad());
        targetPos = targetPos.PointInOutside(Center, 2f);
        var dp = accessory.DrawGuidance(targetPos, 0, 8000, $"一运穿天指路");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    [ScriptMethod(name: "一运穿天指路消失", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:29346"],
        userControl: false)]
    public void SkywardLeapGuidanceRemove(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase5HeavensWrath) return;
        _p5VedrfolnirPosRecordEvent.Reset();
        accessory.Method.RemoveDraw($"一运穿天指路");
    }
    
    [ScriptMethod(name: "P5：二运，阶段记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27538"], userControl: false)]
    public void P5_HeavensDeath_PhaseRecord(Event @event, ScriptAccessory accessory)
    {
        _dsrPhase = DsrPhase.Phase5HeavensDeath;
        accessory.DebugMsg($"当前阶段为：{_dsrPhase}", DebugMode);
    }

    [ScriptMethod(name: "二运斧头哥方位指引", eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["Id:7747", "SourceDataId:12637"])]
    public void P5_FindSerGuerrique(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase5HeavensDeath) return;
        var spos = @event.SourcePosition();
        accessory.DebugMsg($"找到斧头哥位置{spos}", DebugMode);
        var dp = accessory.DrawDirPos2Pos(Center, spos, 0, 4000, $"场中指向斧头哥", 2f);
        dp.Color = ColorHelper.ColorWhite.V4;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
    }

    #endregion P5
    
    #region P6 冰火

    [ScriptMethod(name: "---- 《P6：双龙》 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld"],
        userControl: true)]
    public void SplitLine_PhaseDragons(Event @event, ScriptAccessory accessory)
    {
    }
    
    [ScriptMethod(name: "P6：一冰火，阶段记录", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:12613"], userControl: false)]
    public void P6_IceAndFire1_PhaseRecord(Event @event, ScriptAccessory accessory)
    {
        // 圣龙出现代表进入一冰火
        if (_dsrPhase != DsrPhase.Phase5HeavensDeath) return;
        _dsrPhase = DsrPhase.Phase6IceAndFire1;
        _p6DragonsGlowAction = [false, false];
        _recorded = new bool[20].ToList();
        accessory.DebugMsg($"当前阶段为：{_dsrPhase}", DebugMode);
    }

    [ScriptMethod(name: "P6：二冰火，阶段记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(2794[79])$"], userControl: false)]
    public void P6_IceAndFire2_PhaseRecord(Event @event, ScriptAccessory accessory)
    {
        // 以辣翅辣尾作为二冰火的开始
        if (_dsrPhase != DsrPhase.Phase6NearOrFar2) return;
        _dsrPhase = DsrPhase.Phase6IceAndFire2;
        _p6DragonsGlowAction = [false, false];
        _recorded = new bool[20].ToList();
        accessory.DebugMsg($"当前阶段为：{_dsrPhase}", DebugMode);
    }

    
    [ScriptMethod(name: "P6：冰火吐息记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(2795[4567])$"], userControl: false)]
    public void P6_IceAndFireGlowRecord(Event @event, ScriptAccessory accessory)
    {
        const uint blackBuster = 27954;
        const uint whiteBuster = 27956;
        const uint blackGlow = 27955;
        const uint whiteGlow = 27957;
        
        if (_dsrPhase != DsrPhase.Phase6IceAndFire1 && _dsrPhase != DsrPhase.Phase6IceAndFire2) return;
        var aid = @event.ActionId();
        switch (aid)
        {
            case blackBuster:
            case blackGlow:
                _p6DragonsGlowAction[0] = aid == blackGlow;
                break;
            case whiteBuster:
            case whiteGlow:
                _p6DragonsGlowAction[1] = aid == whiteGlow;
                break;
        }

        lock (_recorded)
        {
            _recorded[1] = _recorded[0];
            _recorded[0] = true;
            if (_recorded[0] && _recorded[1])
                _IceAndFireEvent.Set();
        }
    }

    [ScriptMethod(name: "冰火死刑双T处理", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27960"])]
    public void P6_IceAndFireTankSolution(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase is not (DsrPhase.Phase6IceAndFire1 or DsrPhase.Phase6IceAndFire2))
            return;
        _IceAndFireEvent.WaitOne();
        // await Task.Delay(100);
        var myIndex = accessory.GetMyIndex();
        var tankBusterPosition = new Vector3[4];
        tankBusterPosition[0] = new Vector3(84.5f, 0, 88f);
        tankBusterPosition[1] = tankBusterPosition[0].FoldPointHorizon(Center.X);
        tankBusterPosition[2] = tankBusterPosition[0];
        tankBusterPosition[3] = tankBusterPosition[1].FoldPointVertical(Center.Z);

        if (_p6DragonsGlowAction[0] && _p6DragonsGlowAction[1])
        {
            // 场中分摊死刑，自己不是T不显示指路
            if (myIndex > 1) return;
            // 删除K佬脚本中双T的小啾啾
            accessory.Method.RemoveDraw("P6 第二次冰火线ND站位.*");
            var dp = accessory.DrawDirPos(Center, 0, 6000, $"冰火场中分摊指路");
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        }
        else
        {
            // 场边死刑，自己的死刑不显示圈，避免瞎眼
            var busterIdx = _p6DragonsGlowAction.FindIndex(x => x == false);
            
            var str = "";
            str += $"黑龙喷:{_p6DragonsGlowAction[0]}, 白龙喷:{_p6DragonsGlowAction[1]}\n";
            str += $"是{(busterIdx == 0 ? "黑龙" : "白龙")}的死刑。";
            accessory.DebugMsg($"{str}", DebugMode);

            var isMyBuster = myIndex == busterIdx;
            var dp = accessory.DrawCircle(accessory.Data.PartyList[busterIdx], isMyBuster ? 2f : 15f, 0, 6000, $"冰火死刑");
            dp.Color = isMyBuster ? ColorHelper.ColorRed.V4 : ColorHelper.ColorYellow.V4;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

            // 场边分散，自己不是T不显示指路
            if (myIndex > 1) return;
            // 删除K佬脚本中双T的小啾啾
            accessory.Method.RemoveDraw("P6 第二次冰火线ND站位.*");
            var isIceAndFire2 = _dsrPhase == DsrPhase.Phase6IceAndFire2;

            var dp0 = accessory.DrawDirPos(tankBusterPosition[isIceAndFire2 ? myIndex + 2 : myIndex], 0, 6000,
                $"冰火死刑位置指路");
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);

            var dp1 = accessory.DrawStaticCircle(tankBusterPosition[isIceAndFire2 ? myIndex + 2 : myIndex],
                PosColorPlayer.V4.WithW(1.5f), 0, 6000, $"冰火死刑点区域", 1f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp1);
        }
        _IceAndFireEvent.Reset();
    }

    #endregion P6 冰火

    #region P6 远近

    [ScriptMethod(name: "P6：远近，阶段记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:27970"], userControl: false)]
    public void P6_NearOrFar_PhaseRecord(Event @event, ScriptAccessory accessory)
    {
        // 因为黑龙先飞，白龙后读条，所以用无尽轮回的ActionEffect做阶段节点
        if (_dsrPhase is DsrPhase.Phase6NearOrFar1 or DsrPhase.Phase6NearOrFar2)
            return;
        _dsrPhase = _dsrPhase switch
        {
            DsrPhase.Phase6IceAndFire1 => DsrPhase.Phase6NearOrFar1,
            DsrPhase.Phase6Flame => DsrPhase.Phase6NearOrFar2,
            _ => DsrPhase.Phase6NearOrFar1,
        };
        _p6DragonsWingAction = [false, false, false];   // P6 双龙远近记录
        accessory.DebugMsg($"当前阶段为：{_dsrPhase}", DebugMode);
    }
    
    [ScriptMethod(name: "P6：远近，翅膀记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(279(39|4[023]))$"], userControl: false)]
    public void P6_NearOrFar_WingsRecord(Event @event, ScriptAccessory accessory)
    {
        // LEFT左翼发光，玩家视角左侧安全。
        const uint leftFar = 27940;
        const uint leftNear = 27939;
        const uint rightFar = 27943;
        const uint rightNear = 27942;
        
        if (_dsrPhase is not (DsrPhase.Phase6NearOrFar1 or DsrPhase.Phase6NearOrFar2))
            return;
        
        var aid = @event.ActionId();
        // [远T/近F，左安全T/右安全F，前安全T/后安全F/内安全T/外安全F]
        _p6DragonsWingAction[0] = aid is leftFar or rightFar;
        _p6DragonsWingAction[1] = aid is leftFar or leftNear;
        accessory.DebugMsg($"检测到{(_p6DragonsWingAction[0] ? "T远离" : "T靠近")}, {(_p6DragonsWingAction[1] ? "左" : "右")}安全", DebugMode);
        _NearOrFarWingsEvent.Set();
    }

    
    [ScriptMethod(name: "P6：远近，俯冲记录", eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["Id:7747", "SourceDataId:12612"], userControl: false)]
    public void P6_NearOrFar_CauterizeRecord(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase6NearOrFar1) return;
        var spos = @event.SourcePosition();
        // [远T/近F，左安全T/右安全F，前安全T/后安全F/内安全T/外安全F]
        _p6DragonsWingAction[2] = spos.X < Center.X;
        accessory.DebugMsg($"检测到{(_p6DragonsWingAction[2] ? "前安全" : "后安全")}", DebugMode);
        _NearOrFarCauterizeEvent.Set();
    }

    [ScriptMethod(name: "P6：远近，内外记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(2794[79])$"], userControl: false)]
    public void P6_NearOrFar_BlackWingsRecord(Event @event, ScriptAccessory accessory)
    {
        const uint insideSafe = 27947;
        const uint outsideSafe = 27949;
        if (_dsrPhase != DsrPhase.Phase6NearOrFar2) return;
        var aid = @event.ActionId();
        // [远T/近F，左安全T/右安全F，前安全T/后安全F/内安全T/外安全F]
        _p6DragonsWingAction[2] = aid == insideSafe;
        accessory.DebugMsg($"检测到{(_p6DragonsWingAction[2] ? "内安全" : "外安全")}", DebugMode);
        _NearOrFarInOutEvent.Set();
    }

    [ScriptMethod(name: "一远近指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(279(39|4[023]))$"])]
    public void P6_NearOrFar1_Dir(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase6NearOrFar1) return;
        _NearOrFarCauterizeEvent.WaitOne();
        _NearOrFarWingsEvent.WaitOne();
        Vector3[] nearOrFarSafePos = GetQuarterSafePos(_p6DragonsWingAction);
        var nearOrFarDirPosIdx = GetQuarterSafePosIdx(_p6DragonsWingAction);
        accessory.DebugMsg($"MT去{nearOrFarDirPosIdx[0]}, ST去{nearOrFarDirPosIdx[1]}, 人群去{nearOrFarDirPosIdx[2]}", DebugMode);

        var myIndex = accessory.GetMyIndex();
        var myPartIdx = myIndex >= 2 ? 2 : myIndex;
        var targetPos = nearOrFarSafePos[nearOrFarDirPosIdx[myPartIdx]];

        for (var i = 0; i < 3; i++)
        {
            var tempPos = nearOrFarSafePos[nearOrFarDirPosIdx[i]];
            var color = i == myPartIdx ? PosColorPlayer.V4.WithW(1.5f) : PosColorNormal.V4;
            var dp0 = accessory.DrawStaticCircle(tempPos, color, 0, 7500, $"一远近位置{i}", 1f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp0);
        }

        var dp = accessory.DrawDirPos(targetPos, 0, 7500, $"一远近指路");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        _NearOrFarCauterizeEvent.Reset();
        _NearOrFarWingsEvent.Reset();
    }

    private Vector3[] GetQuarterSafePos(List<bool> wings)
    {
        // 第一象限内的四个端点
        // 象限内四个点Idx顺序为，以第一象限基准（面向白龙左上），从左上开始顺时针
        // 上下平移，左右折叠
        Vector3[] quarterSafePos = new Vector3[4];
        quarterSafePos[0] = new Vector3(120f, 0, 80f);
        quarterSafePos[1] = new Vector3(120f, 0, 98f);
        quarterSafePos[2] = new Vector3(102f, 0, 98f);
        quarterSafePos[3] = new Vector3(102f, 0, 80f);
        for (var i = 0; i < 4; i++)
        {
            // 后安全，向后平移
            if (!wings[2])
                quarterSafePos[i] -= new Vector3(22f, 0, 0);
            // 右安全，左右折叠
            if (!wings[1])
                quarterSafePos[i] = quarterSafePos[i].FoldPointVertical(Center.Z);
        }
        return quarterSafePos;
    }

    private static int[] GetQuarterSafePosIdx(List<bool> wings)
    {
        // return数组，代表MT、ST、人群的安全位置Index

        // 打远，双T远离，人群靠近
        // 打近，双T靠近，人群远离
        return wings[0] ? [2, 3, 1] : [1, 0, 3];
    }

    [ScriptMethod(name: "二远近指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(2794[79])$"])]
    public void P6_NearOrFar2_Dir(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase6NearOrFar2) return;
        _NearOrFarInOutEvent.WaitOne();
        _NearOrFarWingsEvent.WaitOne();

        Vector3[] nearOrFarSafePos = GetLineSafePos(_p6DragonsWingAction);
        int[] nearOrFarDirPosIdx = GetLineSafePosIdx(_p6DragonsWingAction);

        var myIndex = accessory.GetMyIndex();
        var myPartIdx = myIndex >= 2 ? 2 : myIndex;
        var targetPos = nearOrFarSafePos[nearOrFarDirPosIdx[myPartIdx]];

        for (var i = 0; i < 3; i++)
        {
            var color = i == myPartIdx ? PosColorPlayer.V4.WithW(1.5f) : PosColorNormal.V4;
            var tempPos = nearOrFarSafePos[nearOrFarDirPosIdx[i]];
            var dp0 = accessory.DrawStaticCircle(tempPos, color, 0, 7500, $"二远近位置{i}", 1f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp0);
        }

        var dp = accessory.DrawDirPos(targetPos, 0, 7500, $"二远近指路");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        _NearOrFarInOutEvent.Reset();
        _NearOrFarWingsEvent.Reset();
    }

    private static Vector3[] GetLineSafePos(List<bool> wings)
    {
        // 直线近中远三点
        Vector3[] lineSafePos = new Vector3[3];
        lineSafePos[0] = new Vector3(120f, 0, 100f);
        lineSafePos[1] = new Vector3(100f, 0, 100f);
        lineSafePos[2] = new Vector3(80f, 0, 100f);

        Vector3 dv3 = new(0f, 0f, 0f);

        // 左安全减，右安全加
        dv3 += new Vector3(0f, 0f, 2f) * (wings[1] ? -1 : 1);
        // 内安全不动，外安全乘
        dv3 *= wings[2] ? 1 : 5;

        for (var i = 0; i < 3; i++)
            lineSafePos[i] += dv3;
        
        return lineSafePos;
    }

    private static int[] GetLineSafePosIdx(List<bool> wings)
    {
        // return数组，代表MT、ST、人群的安全位置Index

        // 打远，双T远离，人群靠近
        // 打近，双T靠近，人群远离
        return wings[0] ? [1, 2, 0] : [1, 0, 2];
    }

    #endregion P6 远近
    
    #region P6 十字火

    [ScriptMethod(name: "P6：十字火，阶段记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27973"], userControl: false)]
    public void P6_Flame_PhaseRecord(Event @event, ScriptAccessory accessory)
    {
        _dsrPhase = DsrPhase.Phase6Flame;
        accessory.DebugMsg($"当前阶段为：{_dsrPhase}", DebugMode);
    }

    [ScriptMethod(name: "十字火分摊目标", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27974"])]
    public void P6_FlameStackTarget(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase6Flame) return;
        var tid = @event.TargetId();
        var dp = accessory.DrawCircle(tid, 6, 0, 12500, $"死亡轮回目标");
        dp.Color = accessory.Data.DefaultSafeColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    #endregion P6 十字火

    #region P6 俯冲

    [ScriptMethod(name: "俯冲双T指路", eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["Id:7737", "SourceDataId:12613"])]
    public void P6_CauterizeDir(Event @event, ScriptAccessory accessory)
    {
        if (_dsrPhase != DsrPhase.Phase6IceAndFire2) return;
        _dsrPhase = DsrPhase.Phase6Cauterize;
        accessory.DebugMsg($"当前阶段为：{_dsrPhase}", DebugMode);

        Vector3[] cauterizePos = new Vector3[2];
        cauterizePos[0] = new Vector3(95f, 0, 79f);
        cauterizePos[1] = new Vector3(105f, 0, 79f);

        var myIndex = accessory.GetMyIndex();
        if (myIndex > 1) return;

        var dp = accessory.DrawDirPos(cauterizePos[myIndex], 0, 5000, $"俯冲T挡枪位置{myIndex}");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    #endregion P6 俯冲

    #region P7 地火

    [ScriptMethod(name: "---- 《P7：龙威骑神托尔丹》 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld"],
        userControl: true)]
    public void SplitLine_PhaseDragonKingThordan(Event @event, ScriptAccessory accessory)
    {
    }
    
    [ScriptMethod(name: "P7：BossId记录与地火类初始化", eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["SourceDataId:12616"], userControl: false)]
    public void P7_BossIdRecord(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        _p7BossId = sid;
        List<int> scoreList = ExaflareStrategy switch
        {
            // moveStep,isFront,isUniverse
            ExaflareSpecStrategyEnum.绝不去前方_NeverFront => [2, 100, 50],
            ExaflareSpecStrategyEnum.绝不跑无脑火_NeverUniverse => [2, 10, 100],
            ExaflareSpecStrategyEnum.绝不多跑_LeastMovement => [20, 10, 50],
            ExaflareSpecStrategyEnum.绝对前方_AlwaysFront => [2, -10, 50],
            _ => [-10, 100, 0],
        };
        _p7Exaflare = new DsrExaflare(DebugMode, scoreList);
    }
    

    [ScriptMethod(name: "P7：钢铁月环剑记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2056", "StackCount:regex:^(4[23])$"], userControl: false)]
    public void P7_BossBladeRecord(Event @event, ScriptAccessory accessory)
    {
        var stc = @event.StackCount();
        _p7Exaflare?.SetBladeType(stc);
        if (!IsExaflarePhase()) return;
        _BladeEvent.Set();
    }
    
    [ScriptMethod(name: "地火范围绘制", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28060"])]
    public void P7_ExaflareDrawn(Event @event, ScriptAccessory accessory)
    {
        // 面相为前、左、右的扩散
        var spos = @event.SourcePosition();
        var srot = @event.SourceRotation();
        var bossChara = accessory.GetById(_p7BossId);
        var bossRot = bossChara?.Rotation ?? float.Pi;
        var bossPos = bossChara?.Position ?? Center;
        const int intervalTime = 1900;
        const int castTime = 6900;
        const int extendDistance = 7;
        const int dirNum = 3;
        const int extNum = 6;
        const int advWarnNum = 1;   // 预警向外延伸几个
        float[] flareRot = [0, -float.Pi / 2, float.Pi / 2];
        
        Vector3[,] exaflarePos = BuildExaflareVector(spos, dirNum, extNum, srot, flareRot, extendDistance);
        DrawExaflareScene(exaflarePos, ExaflareWarnDrawn, advWarnNum, castTime, intervalTime, accessory);
        
        if (_p7Exaflare == null) return;
        lock (_p7Exaflare)
        {
            _p7Exaflare.SetBossPos(bossPos, accessory);
            _p7Exaflare.AddExaflare(spos, bossRot, srot, accessory);
        }
    }
    
    [ScriptMethod(name: "地火特殊解法指路", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2056", "StackCount:regex:^(4[23])$"])]
    public void P7_ExaflareGuidance(Event @event, ScriptAccessory accessory)
    {
        // 记录完钢铁月环后可计算
        if (_p7Exaflare == null) return;
        if (!IsExaflarePhase()) return;
        if (ExaflareStrategy == ExaflareSpecStrategyEnum.关闭_PleaseDontDoThat) return;
        if (!_p7Exaflare.ExaflareRecordComplete()) return;
        _BladeEvent.WaitOne();
        var guidePosList = _p7Exaflare.ExportExaflareSolution(accessory);
        accessory.DebugMsg($"你选择的策略是{ExaflareStrategy}", DebugMode);
        DrawExaflareGuidePos(guidePosList, accessory);
        _BladeEvent.Reset();
    }
    
    private void DrawExaflareGuidePos(List<Vector3> guidePosList, ScriptAccessory accessory)
    {
        const int intervalTime = 1900;
        const int castTime = 6900;
        const int baseTime = castTime - 900;    // 900ms为冰火剑附加到托尔丹身上的时间

        for (var i = 0; i < guidePosList.Count; i++)
        {
            var delay = i == 0 ? 0 : baseTime + (i - 1) * intervalTime;
            var destroy = i == 0 ? baseTime : intervalTime;

            var dp01 = accessory.DrawDirPos(guidePosList[i], delay, destroy, $"地火第{i}步-玩家-位置");
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp01);
            if (i >= guidePosList.Count - 1) continue;
            var dp12 = accessory.DrawDirPos2Pos(guidePosList[i], guidePosList[i + 1], delay, destroy, $"地火第{i}步-位置-位置");
            dp12.Color = accessory.Data.DefaultDangerColor;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp12);
        }
    }
    
    /// <summary>
    /// 画地火场景
    /// </summary>
    /// <param name="exaflarePos">地火矩阵</param>
    /// <param name="warnDrawn">是否画预警地火</param>
    /// <param name="advWarnNum">画多少格预警地火</param>
    /// <param name="castTime">初始地火技能施法时间</param>
    /// <param name="intervalTime">地火间隔时间</param>
    /// <param name="accessory"></param>
    private void DrawExaflareScene(Vector3[,] exaflarePos, bool warnDrawn, int advWarnNum, int castTime, int intervalTime, ScriptAccessory accessory)
    {
        var dirNum = exaflarePos.GetLength(0);
        var extNum = exaflarePos.GetLength(1);
        
        for (var ext = 0; ext < extNum; ext++)
        {
            // 计算各位置的出现时间与延时时间。往往第一枚地火需要特殊处理，后续采用同时间隔
            var destroy = ext == 0 ? castTime : intervalTime;
            var delay= ext == 0 ? 0 : castTime + (ext - 1) * intervalTime;
            
            if (ext == 0)
            {
                // 本体地火，对原地的地火(ext=0)，只画一个dir=0，不以任何角度向外延伸
                DrawExaflare(exaflarePos[0, ext], delay, destroy, accessory);
                DrawExaflareEdge(exaflarePos[0, ext], delay, destroy, accessory);
            }
            else
            {
                // 对后续的地火(ext>0)，以对应角度向外延伸
                for (var dir = 0; dir < dirNum; dir++)
                {
                    DrawExaflare(exaflarePos[dir, ext], delay, destroy, accessory);
                    DrawExaflareEdge(exaflarePos[dir, ext], delay, destroy, accessory);
                }
            }
            
            if (!warnDrawn) continue;
            for (var adv = 1; adv <= advWarnNum; adv++)
            {
                if (ext >= extNum - adv) continue;
                for (var dir = 0; dir < dirNum; dir++)
                    DrawExaflareWarn(exaflarePos[dir, ext + adv], adv, delay, destroy, intervalTime, accessory);
            }
        }
    }
    
    /// <summary>
    /// 构建地火坐标矩阵
    /// </summary>
    /// <param name="sourcePos">地火本体位置</param>
    /// <param name="dirNum">一枚地火涉及几个方向</param>
    /// <param name="extNum">一枚地火延伸几次</param>
    /// <param name="sourceRot">释放地火幻影旋转角度</param>
    /// <param name="flareRot">各方向旋转角度</param>
    /// <param name="extDistance">地火步进延伸距离</param>
    private Vector3[,] BuildExaflareVector(Vector3 sourcePos, int dirNum, int extNum, float sourceRot, float[] flareRot, float extDistance)
    {
        Vector3[,] exaflarePos = new Vector3[dirNum, extNum];
        if (flareRot.Length != dirNum) return exaflarePos;
        for (var ext = 0; ext < extNum; ext++)
            for (var dir = 0; dir < dirNum; dir++)
                exaflarePos[dir, ext] = sourcePos.ExtendPoint(sourceRot.Game2Logic() + flareRot[dir], ext * extDistance);
        return exaflarePos;
    }
    
    private void DrawExaflare(Vector3 spos, int delay, int destroy, ScriptAccessory accessory)
    {
        const int scale = 6;
        var color = ExaflareBuiltInColor ? ColorHelper.ColorExaflare.V4 : ExaflareColor.V4.WithW(1f);
        var dp = accessory.DrawStaticCircle(spos, color, delay, destroy, $"地火{spos}", scale);
        dp.ScaleMode |= ScaleMode.ByTime;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    private void DrawExaflareEdge(Vector3 spos, int delay, int destroy, ScriptAccessory accessory)
    {
        const float scale = 6;
        // const float innerScale = scale - 0.05f;
        var color = ExaflareBuiltInColor ? ColorHelper.ColorExaflare.V4 : ExaflareColor.V4.WithW(1.5f);
        var dp = accessory.DrawStaticDonut(spos, color, delay, destroy, $"地火边缘{spos}", scale);
        // dp.Color = ColorHelper.colorDark.V4;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Donut, dp);
    }

    private void DrawExaflareWarn(Vector3 spos, int adv, int delay, int destroy, int interval, ScriptAccessory accessory)
    {
        const int scale = 6;
        var destroyItv = interval * (adv - 1);
        var color = ExaflareBuiltInColor ? ColorHelper.ColorExaflareWarn.V4.WithW(1f / adv) : ExaflareWarnColor.V4.WithW(1f / adv);
        var dp = accessory.DrawStaticCircle(spos, color, delay, destroy + destroyItv, $"地火预警{spos}", scale);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    private void DebugExaflare(float[] srot, float bossRotRad, uint bladeType, ScriptAccessory accessory)
    {
        if (!DebugMode) return;
        
        accessory.DebugMsg($"你选择的策略是{ExaflareStrategy}", DebugMode);
        
        List<int> scoreList = ExaflareStrategy switch
        {
            // moveStep,isFront,isUniverse
            ExaflareSpecStrategyEnum.绝不去前方_NeverFront => [2, 100, 50],
            ExaflareSpecStrategyEnum.绝不跑无脑火_NeverUniverse => [2, 10, 100],
            ExaflareSpecStrategyEnum.绝不多跑_LeastMovement => [20, 10, 50],
            ExaflareSpecStrategyEnum.绝对前方_AlwaysFront => [2, -10, 50],
            _ => [-10, 100, 0],
        };
        _p7Exaflare = new DsrExaflare(DebugMode, scoreList);
        
        // 面相为前、左、右的扩散
        // var spos = @event.SourcePosition();
        // var srot = @event.SourceRotation();
        Vector3[] spos =
        [
            Center.ExtendPoint(bossRotRad.Game2Logic() - float.Pi, 8),
            Center.ExtendPoint(bossRotRad.Game2Logic() + 60f.DegToRad(), 8),
            Center.ExtendPoint(bossRotRad.Game2Logic() - 60f.DegToRad(), 8)
        ];
        var bossChara = accessory.GetById(_p7BossId);
        var bossRot = bossChara?.Rotation ?? bossRotRad;
        var bossPos = bossChara?.Position ?? Center;
        const int intervalTime = 1900;
        const int castTime = 6900;
        const int extendDistance = 7;
        const int dirNum = 3;
        const int extNum = 6;
        const int advWarnNum = 1;   // 预警向外延伸几个
        float[] flareRot = [0, -float.Pi / 2, float.Pi / 2];

        for (int i = 0; i < 3; i++)
        {
            Vector3[,] exaflarePos = BuildExaflareVector(spos[i], dirNum, extNum, srot[i], flareRot, extendDistance);
            // 画地火箭头
            var dp1 = accessory.DrawDirPos2Pos(spos[i], spos[i].ExtendPoint(srot[i].Game2Logic() + flareRot[0], 6), 0, castTime, $"箭头1", 5.9f);
            var dp2 = accessory.DrawDirPos2Pos(spos[i], spos[i].ExtendPoint(srot[i].Game2Logic() + flareRot[1], 6), 0, castTime, $"箭头2", 5.9f);
            var dp3 = accessory.DrawDirPos2Pos(spos[i], spos[i].ExtendPoint(srot[i].Game2Logic() + flareRot[2], 6), 0, castTime, $"箭头3", 5.9f);
            dp1.Color = ColorHelper.ColorRed.V4;
            dp2.Color = ColorHelper.ColorRed.V4;
            dp3.Color = ColorHelper.ColorRed.V4;
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp1);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp2);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp3);
            
            DrawExaflareScene(exaflarePos, ExaflareWarnDrawn, advWarnNum, castTime, intervalTime, accessory);
            if (_p7Exaflare == null) return;
            lock (_p7Exaflare)
            {
                _p7Exaflare.SetBossPos(bossPos, accessory);
                _p7Exaflare.AddExaflare(spos[i], bossRot, srot[i], accessory);
            }
        }
        _p7Exaflare.SetBladeType(bladeType);
        switch (bladeType)
        {
            case 42:
                var dp1 = accessory.DrawStaticCircle(Center, accessory.Data.DefaultDangerColor.WithW(2f), 0, castTime, $"钢铁", 8f);
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp1);
                break;
            case 43:
                var dp2 = accessory.DrawStaticDonut(Center, accessory.Data.DefaultDangerColor.WithW(2f), 0, castTime, $"月环", 50f, 8f);
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp2);
                break;
        }
        
        // 记录完钢铁月环后可计算
        if (_p7Exaflare == null) return;
        // if (!IsExaflarePhase()) return;
        if (ExaflareStrategy == ExaflareSpecStrategyEnum.关闭_PleaseDontDoThat) return;
        if (!_p7Exaflare.ExaflareRecordComplete()) return;
        var guidePosList = _p7Exaflare.ExportExaflareSolution(accessory);
        DrawExaflareGuidePos(guidePosList, accessory);
    }

    [ScriptMethod(name: "忆罪宫地火模拟器", eventType: EventTypeEnum.Chat, eventCondition: ["Type:Echo", "Message:=TST"], userControl: false)]
    public void ExaflareEchoDebug(Event @event, ScriptAccessory accessory)
    {
        if (!DebugMode) return;
        // ---- DEBUG CODE ----
        
        Center = new Vector3(400, -54.97f, -400);
        Random random = new Random();
        float bossRotLogicDeg = random.Next(0, 360);
        var bossRotLogicRad = bossRotLogicDeg.DegToRad();
        accessory.DebugMsg($"随机到的Boss面向为{bossRotLogicRad.RadToDeg()}", DebugMode);
        float[] srot =
        [
            (random.Next(0, 8) * float.Pi / 4 + bossRotLogicRad).Logic2Game(),
            (random.Next(0, 8) * float.Pi / 4 + bossRotLogicRad).Logic2Game(),
            (random.Next(0, 8) * float.Pi / 4 + bossRotLogicRad).Logic2Game()
        ];
        Vector3 bossFace = Center.ExtendPoint(bossRotLogicRad, 8f);
        var dp = accessory.DrawDirPos2Pos(Center, bossFace, 0, 7000, $"面相", 7.9f);
        dp.Color = ColorHelper.ColorDark.V4;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        DebugExaflare(srot, bossRotLogicRad.Logic2Game(), (uint)random.Next(0, 2) + 42, accessory);
        // -- DEBUG CODE END --
    }
    
    #endregion P7 地火
    
    #region P7 接刀


    [ScriptMethod(name: "P7：阶段记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(2805[179]|28206)$"], userControl: false)]
    public void P7_PhaseRecord(Event @event, ScriptAccessory accessory)
    {
        _dsrPhase = _dsrPhase switch
        {
            DsrPhase.Phase6Cauterize => DsrPhase.Phase7Exaflare1,
            DsrPhase.Phase7Exaflare1 => DsrPhase.Phase7Stack1,
            DsrPhase.Phase7Stack1 => DsrPhase.Phase7Nuclear1,
            DsrPhase.Phase7Nuclear1 => DsrPhase.Phase7Exaflare2,
            DsrPhase.Phase7Exaflare2 => DsrPhase.Phase7Stack2,
            DsrPhase.Phase7Stack2 => DsrPhase.Phase7Nuclear2,
            DsrPhase.Phase7Nuclear2 => DsrPhase.Phase7Exaflare3,
            DsrPhase.Phase7Exaflare3 => DsrPhase.Phase7Stack3,
            DsrPhase.Phase7Stack3 => DsrPhase.Phase7Enrage,
            _ => DsrPhase.Phase7Exaflare1,
        };
        accessory.DebugMsg($"当前阶段为：{_dsrPhase}", DebugMode);

        if (!_p7FirstEnmityOrder.Contains(true))
        {
            // 初始化
            _p7FirstEnmityOrder = [true, false];
            _p7TrinityDisordered = false;
            _p7TrinityTankDisordered = false;
            _p7TrinityNum = 0;
        }
        else
        {
            _p7FirstEnmityOrder[0] = !_p7FirstEnmityOrder[0];
            _p7FirstEnmityOrder[1] = !_p7FirstEnmityOrder[1];
            accessory.DebugMsg($"MT为{(_p7FirstEnmityOrder[0] ? "一仇" : "二仇")}，ST为{(_p7FirstEnmityOrder[1] ? "一仇" : "二仇")}", DebugMode);
        }
        _TrinityEvent.Set();
        
        if (!IsStackPhase()) return;
        List<int> scoreList = ExaflareStrategy switch
        {
            // moveStep,isFront,isUniverse
            ExaflareSpecStrategyEnum.绝不去前方_NeverFront => [2, 100, 50],
            ExaflareSpecStrategyEnum.绝不跑无脑火_NeverUniverse => [2, 10, 100],
            ExaflareSpecStrategyEnum.绝不多跑_LeastMovement => [20, 10, 50],
            ExaflareSpecStrategyEnum.绝对前方_AlwaysFront => [2, -10, 50],
            _ => [-10, 100, 0],
        };
        _p7Exaflare = new DsrExaflare(DebugMode, scoreList);
        
    }
    
    private bool IsExaflarePhase()
    {
        return _dsrPhase is DsrPhase.Phase7Exaflare1 or DsrPhase.Phase7Exaflare2 or DsrPhase.Phase7Exaflare3;
    }
    
    private bool IsStackPhase()
    {
        return _dsrPhase is DsrPhase.Phase7Stack1 or DsrPhase.Phase7Stack2 or DsrPhase.Phase7Stack3;
    }

    [ScriptMethod(name: "三剑一体接刀", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(2805[179])$"])]
    public void P7_TrinityAttack(Event @event, ScriptAccessory accessory)
    {
        _TrinityEvent.WaitOne();
        var aid = @event.ActionId();
        var sid = @event.SourceId();
        const uint exaflare = 28059;
        const uint stack = 28051;
        const uint nuclear = 28057;

        var delay = aid switch
        {
            exaflare => 15200,
            stack => 18500,
            nuclear => 27200,
            _ => 0
        };
        
        delay = _dsrPhase switch
        {
            DsrPhase.Phase7Stack1 => delay,
            DsrPhase.Phase7Stack2 => delay + 1100,
            DsrPhase.Phase7Stack3 => delay + 2200,
            _ => delay
        };

        DrawTrinityAggro(sid, delay - 4000, 4000, 1, accessory);
        DrawTrinityAggro(sid, delay - 4000, 4000, 2, accessory);
        DrawTrinityAggro(sid, delay, 4000, 1, accessory);
        DrawTrinityAggro(sid, delay, 4000, 2, accessory);
        DrawTrinityNear(sid, delay - 4000, 4000, accessory);
        DrawTrinityNear(sid, delay, 4000, accessory);
        _TrinityEvent.Reset();
    }

    private void DrawTrinityAggro(uint sid, int delay, int destroy, uint aggroIdx, ScriptAccessory accessory)
    {
        var myIndex = accessory.GetMyIndex();
        Vector4 color;

        if (myIndex > 1 || _p7TrinityTankDisordered)
            color = accessory.Data.DefaultDangerColor;
        else
        {
            switch (_p7FirstEnmityOrder[myIndex])
            {
                case true when aggroIdx == 1:
                case false when aggroIdx == 2:
                    color = accessory.Data.DefaultSafeColor;
                    break;
                default:
                    color = accessory.Data.DefaultDangerColor;
                    break;
            }
        }
        
        var dp = accessory.DrawOwnersEnmityOrder(sid, aggroIdx, 3f, 3f, delay, destroy, $"三剑一体仇恨{aggroIdx}", byTime: true);
        dp.Color = color.WithW(2f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    private void DrawTrinityNear(uint sid, int delay, int destroy, ScriptAccessory accessory)
    {
        var myIndex = accessory.GetMyIndex();

        var dp = accessory.DrawTargetNearFarOrder(sid, 1, true, 3f, 3f, delay, destroy, $"三剑一体近距", byTime: true);
        if (_p7TrinityDisordered)
            dp.Color = accessory.Data.DefaultDangerColor;
        else
            dp.Color = myIndex == _p7TrinityOrderIdx[_p7TrinityNum] ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "三剑一体接刀记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:28065"], userControl: false)]
    public void P7_TrinityOrderRecord(Event @event, ScriptAccessory accessory)
    {
        // 主视角为T，忽略脚下接刀
        var myIndex = accessory.GetMyIndex();
        if (myIndex < 2) return;

        var targetIdx = @event.TargetIndex();
        if (targetIdx != 1)
        {
            if (_p7TrinityDisordered) return;
            accessory.DebugMsg($"有人多接了一刀，失效", DebugMode);
            accessory.Method.TextInfo($"有人多接了一刀，不再以安全色提示", 3000, true);
            _p7TrinityDisordered = true;
            return;
        }

        var tid = @event.TargetId();
        var tidx = accessory.GetPlayerIdIndex(tid);
        if (_p7TrinityOrderIdx[_p7TrinityNum] != tidx && !_p7TrinityDisordered)
        {
            accessory.DebugMsg($"接刀人错误，失效", DebugMode);
            accessory.Method.TextInfo($"接刀人错误，不再以安全色提示", 3000, true);
            _p7TrinityDisordered = true;
        }

        _p7TrinityNum++;
        if (_p7TrinityNum >= 6)
            _p7TrinityNum = 0;

        var targetRecent = accessory.GetPlayerJobByIndex(tidx);
        var targetNext = accessory.GetPlayerJobByIndex(_p7TrinityOrderIdx[_p7TrinityNum]);
        accessory.DebugMsg($"刚刚接刀的是{targetRecent}，下一个接刀人为{targetNext}", DebugMode);
    }

    [ScriptMethod(name: "三剑一体T刀记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(2806[34])$"], userControl: false)]
    public void P7_TrinityTankRecord(Event @event, ScriptAccessory accessory)
    {
        var aid = @event.ActionId();
        var tid = @event.TargetId();
        
        // 非T玩家接到刀
        var tidx = accessory.GetPlayerIdIndex(tid);
        if (tidx > 1) return;

        // 主视角不是T
        var myIndex = accessory.GetMyIndex();
        if (myIndex > 1) return;

        // 已经失效
        if (_p7TrinityTankDisordered) return;

        const uint aggro1 = 28063;
        const uint aggro2 = 28064;

        // 一仇效果，但目标是二仇 || 二仇效果，但目标是一仇
        if ((_p7FirstEnmityOrder[tidx] || aid != aggro1) && (!_p7FirstEnmityOrder[tidx] || aid != aggro2)) return;
        accessory.DebugMsg($"接刀仇恨错误，失效", DebugMode);
        accessory.Method.TextInfo($"接刀仇恨错误，不再以安全色提示", 3000, true);
        _p7TrinityTankDisordered = true;
    }

    #endregion P7 接刀

}

#region Class 地火

public class DsrExaflare(bool debugMode, List<int> scoreList)
{
    // 右上0，下1，左2
    private bool DebugMode = debugMode;
    private List<Vector3> ExaflarePosList { get; set; } = Enumerable.Repeat(new Vector3(0, 0, 0), 3).ToList();
    private Vector3 BossPos { get; set; } = new Vector3(0, 0, 0);
    private List<int> ExaflareDirList { get; set; } = [0, 0, 0];
    private uint BladeType { get; set; } = 0;
    private List<ExaflareSolution> ExaflareSolutionList { get; set; } = [];
    public int _recordedExaflareNum = 0;

    private ExaflareSolution BuildOneStepSolutionNew(ScriptAccessory accessory)
    {
        // 一步火
        const bool isUniverse = false;
        var moveStep = 0;
        Vector3 pos2;
        Vector3 pos3;
        int targetExaflareIdx;
        var debugText = $"[a][一步火]: \n";
        
        if (!IsFrontPointedByExaflare(0, accessory))
            targetExaflareIdx = 0;
        else if (!IsFrontPointedByExaflare(2, accessory))
            targetExaflareIdx = 2;
        else
        {
            targetExaflareIdx = 0;
            moveStep++;
        }

        pos2 = ExaflarePosList[targetExaflareIdx];
        
        if (moveStep == 0)
        {
            debugText += $"[a]检测到{GetExaflareIdxStr(targetExaflareIdx)}地火未被指向，可作为安全点\n";
            pos3 = pos2;
        }
        else
        {
            debugText += $"[a]检测到前方地火均被指向，走前方两步火，随便取左上作安全点\n";
            pos3 = ExaflarePosList[1].PointInOutside(BossPos, 12f);
        }
        
        // pos1 根据职能定义起跑点
        var myIndex = accessory.GetMyIndex();
        var pos1 = FindFirstSafePosAtFront(targetExaflareIdx, myIndex < 1);
        debugText += $"[a]玩家序号为{myIndex}, 为{(myIndex < 1?"坦克":"人群")}视角，\n倾向于{(myIndex < 1?"前方":"后方")}就位\n";
        moveStep++;
        
        accessory.DebugMsg(debugText, DebugMode);
        
        return new ExaflareSolution([pos1, pos2, pos3], moveStep, true, isUniverse, "一步火", scoreList, DebugMode,
            accessory);
    }
    
    private ExaflareSolution BuildOneStepSolution(ScriptAccessory accessory)
    {
        // 一步火
        var backExaflarePos = ExaflarePosList[1];
        const bool isUniverse = false;
        var moveStep = 0;

        Vector3 pos2;
        Vector3 pos3;

        int targetExaflareIdx;
        if (IsBackExaflareRightDir())
        {
            accessory.DebugMsg($"[一步火] 检测到背后地火为正地火", DebugMode);
            // 若背后地火为正地火
            if (IsBothFrontExaflareRightDir())
            {
                accessory.DebugMsg($"[一步火] 检测到前方地火都是正地火", DebugMode);
                // 观察左右地火是否被指向
                if (!IsFrontPointedByExaflare(0, accessory))
                {
                    targetExaflareIdx = 0;
                    accessory.DebugMsg($"[一步火] 检测到{GetExaflareIdxStr(targetExaflareIdx)}地火未被指向，可作为安全点", DebugMode);
                    pos2 = ExaflarePosList[targetExaflareIdx];
                    pos3 = pos2;
                }
                else if (!IsFrontPointedByExaflare(2, accessory))
                {
                    targetExaflareIdx = 2;
                    accessory.DebugMsg($"[一步火] 检测到{GetExaflareIdxStr(targetExaflareIdx)}地火未被指向，可作为安全点", DebugMode);
                    pos2 = ExaflarePosList[targetExaflareIdx];
                    pos3 = pos2;
                }
                else
                {
                    targetExaflareIdx = 0;
                    accessory.DebugMsg($"[一步火] 检测到前方地火均被指向，随便取左上作安全点", DebugMode);
                    pos2 = ExaflarePosList[targetExaflareIdx];
                    pos3 = ExaflarePosList[1].PointInOutside(BossPos, 12f);
                    moveStep++;
                }
            }
            else
            {
                // 找到前方正地火序列，为目的地。如果前方为两个斜火，随便找一个。
                targetExaflareIdx = IsBothFrontExaflareOpliqueDir() ? 0 : FindFrontRightDirExaflare();
                // pos2 pos3 定义目的地
                pos2 = ExaflarePosList[targetExaflareIdx];
                pos3 = pos2;
            }
            accessory.DebugMsg($"[一步火] 找到基于{GetExaflareIdxStr(targetExaflareIdx)}正地火的初始安全区", DebugMode);
        }
        else
        {
            accessory.DebugMsg($"[一步火] 检测到背后地火为斜地火", DebugMode);
            // 若背后地火为斜地火
            if (!IsFrontPointedByExaflare(0, accessory))
            {
                targetExaflareIdx = 0;
                // 若右上地火未被指向，可作为安全点
                accessory.DebugMsg($"[一步火] 检测到{GetExaflareIdxStr(targetExaflareIdx)}地火未被指向，可作为安全点", DebugMode);
                pos2 = ExaflarePosList[targetExaflareIdx];
                pos3 = pos2;
            }
            else if (!IsFrontPointedByExaflare(2, accessory))
            {
                targetExaflareIdx = 2;
                // 若左上地火未被指向，可作为安全点
                accessory.DebugMsg($"[一步火] 检测到{GetExaflareIdxStr(targetExaflareIdx)}地火未被指向，可作为安全点", DebugMode);
                pos2 = ExaflarePosList[targetExaflareIdx];
                pos3 = pos2;
            }
            else
            {
                // 若左上右上两枚地火均被指向，走通用面前火。从背后火的位置向Boss方向延长12
                moveStep++;
                targetExaflareIdx = 2;
                accessory.DebugMsg($"[一步火] 检测到前方两枚地火均被指向，随便取左上作为安全点", DebugMode);
                pos2 = ExaflarePosList[targetExaflareIdx];
                pos3 = ExaflarePosList[1].PointInOutside(BossPos, 12f);
            }
        }
        // pos1 根据职能定义起跑点
        var myIndex = accessory.GetMyIndex();
        var pos1 = FindFirstSafePosAtFront(targetExaflareIdx, myIndex < 1);
        moveStep++;
        
        return new ExaflareSolution([pos1, pos2, pos3], moveStep, true, isUniverse, "一步火", scoreList, DebugMode,
            accessory);
    }
    
    private ExaflareSolution BuildTwoStepSolution(ScriptAccessory accessory)
    {
        // 两步火
        var backExaflarePos = ExaflarePosList[1];
        var isUniverse = false;
        var moveStep = 0;
        // pos1 读条时，找背后地火的钢铁月环安全区
        var pos1 = FindFirstSafePos(1, true);
        moveStep++;
        // pos2 一炸后，找背后地火位置
        var pos2 = backExaflarePos;
        // pos3 二炸后，观察前面两枚
        Vector3 pos3;
        var debugText = $"[b][两步火]: \n";
        
        // 前方两地火是否指向背后
        var idx0Point = IsBackPointedByExaflare(0, accessory);
        var idx2Point = IsBackPointedByExaflare(2, accessory);

        if (!idx0Point && !idx2Point)
        {
            // 都未指向背后，原地
            pos3 = backExaflarePos;
            debugText += $"[b]检测到前方地火前方地火都未指向背后，转为背后一步火\n";
        }
        else if (!idx0Point && idx2Point)
        {
            // 右上未指向背后，去左侧
            pos3 = backExaflarePos.RotatePoint(BossPos, 45f.DegToRad());
            moveStep++;
            debugText += $"[b]检测到右上地火未指向背后，去左后\n";
        }
        else if (idx0Point && !idx2Point)
        {
            // 左上未指向背后，去右侧
            pos3 = backExaflarePos.RotatePoint(BossPos, -45f.DegToRad());
            moveStep++;
            debugText += $"[b]检测到左上地火未指向背后，去右侧\n";
        }
        else
        {
            // 全部指向背后，无脑火
            pos3 = FindUniversalSafePos();
            isUniverse = true;
            moveStep++;
            debugText += $"[b]检测到地火全指向背后，转为无脑火\n";
        }
        accessory.DebugMsg(debugText, DebugMode);
        return new ExaflareSolution([pos1, pos2, pos3], moveStep, false, isUniverse, "两步火", scoreList, DebugMode,
            accessory);
    }

    /// <summary>
    /// 以某一枚地火开始，顺时针或逆时针处就位
    /// </summary>
    /// <param name="exaflareIdx">某一枚地火</param>
    /// <param name="isCw">顺时针找</param>
    /// <returns></returns>
    private Vector3 FindFirstSafePos(int exaflareIdx, bool isCw)
    {
        var exaflarePos = ExaflarePosList[exaflareIdx];
        var rad = exaflarePos.FindRadian(BossPos) + (isCw ? 50f.DegToRad() : -50f.DegToRad());
        var firstSafePos = BossPos.ExtendPoint(rad, IsChariot() ? 8.5f : 7.5f);
        return firstSafePos;
    }

    private Vector3 FindFirstSafePosAtFront(int exaflareIdx, bool isTank)
    {
        var exaflarePos = ExaflarePosList[exaflareIdx];
        if (isTank) // 是坦克，则前方起跑
        {
            if (exaflareIdx == 0)
                return FindFirstSafePos(exaflareIdx, false);
            if (exaflareIdx == 2)
                return FindFirstSafePos(exaflareIdx, true);
        }
        else
        {
            if (exaflareIdx == 0)
                return FindFirstSafePos(exaflareIdx, true);
            if (exaflareIdx == 2)
                return FindFirstSafePos(exaflareIdx, false);
        }
        return new Vector3(0, 0, 0);
    }

    private Vector3 FindUniversalSafePos()
    {
        return ExaflarePosList[1].PointInOutside(BossPos, 13.2f - 8f, true);
    }
    
    public void SetBossPos(Vector3 bossPosV3, ScriptAccessory accessory)
    {
        BossPos = bossPosV3;
        // accessory.DebugMsg($"设置Boss位置{BossPos}", debugMode);
    }
    
    /// <summary>
    /// 增加地火属性
    /// </summary>
    /// <param name="exaflarePosV3">地火位置</param>
    /// <param name="bossRotation">Boss旋转角度</param>
    /// <param name="exaflareRot">地火旋转角度</param>
    /// <param name="accessory"></param>
    public void AddExaflare(Vector3 exaflarePosV3, float bossRotation, float exaflareRot, ScriptAccessory accessory)
    {
        var idx = FindExaflareIdx(exaflarePosV3, bossRotation);
        // 差值无需互转
        var exaflareRelativeDir = exaflareRot.Game2Logic() - bossRotation.Game2Logic();
        var dir = exaflareRelativeDir.Rad2Dirs(8);
        ExaflareDirList[idx] = dir;
        ExaflarePosList[idx] = exaflarePosV3;
        accessory.DebugMsg($"添加{GetExaflareIdxStr(idx)}地火，坐标{exaflarePosV3}，面向{GetDirStr(dir)}", DebugMode);
        _recordedExaflareNum++;
    }
    
    /// <summary>
    /// 根据地火中心位置找到对应地火本体方位的idx
    /// 因为地火位置会根据Boss面向改变，所以要减去boss旋转的偏置量
    /// </summary>
    /// <param name="exaflarePosV3">地火中心位置</param>
    /// <param name="bossRotation">Boss面向</param>
    /// <returns></returns>
    private int FindExaflareIdx(Vector3 exaflarePosV3, float bossRotation)
    {
        var exaflareBaseDir = exaflarePosV3.FindRadian(BossPos);
        var exaflareRelativeDir = exaflareBaseDir - bossRotation.Game2Logic();
        var idx = exaflareRelativeDir.Rad2Dirs(3, false);
        return idx;
    }

    /// <summary>
    /// 返回该枚地火是否为正角，当八方方位为偶数时是正角
    /// </summary>
    /// <param name="idx">某枚地火</param>
    /// <returns></returns>
    private bool IsExaflareRightDir(int idx)
    {
        return ExaflareDirList[idx] % 2 == 0;
    }

    /// <summary>
    /// 返回背后地火是否为正角
    /// </summary>
    /// <returns></returns>
    private bool IsBackExaflareRightDir()
    {
        return IsExaflareRightDir(1);
    }

    /// <summary>
    /// 前方两地火全正角
    /// </summary>
    /// <returns></returns>
    private bool IsBothFrontExaflareRightDir()
    {
        return IsExaflareRightDir(0) && IsExaflareRightDir(2);
    }

    /// <summary>
    /// 前方两地火全斜角
    /// </summary>
    /// <returns></returns>
    private bool IsBothFrontExaflareOpliqueDir()
    {
        return !(IsExaflareRightDir(0) || IsExaflareRightDir(2));
    }
    
    /// <summary>
    /// 前方两地火正斜类型一致
    /// </summary>
    /// <returns></returns>
    private bool IsBothFrontExaflareSameDir()
    {
        return IsExaflareRightDir(0) == IsExaflareRightDir(2);
    }
    
    /// <summary>
    /// 找到前方正角地火
    /// </summary>
    /// <returns>正角地火序号</returns>
    private int FindFrontRightDirExaflare()
    {
        if (IsBothFrontExaflareOpliqueDir()) return -1;
        return IsExaflareRightDir(0) ? 0 : 2;
    }
    
    /// <summary>
    /// 找到前方斜角地火
    /// </summary>
    /// <returns>斜角地火序号</returns>
    private int FindFrontObliqueDirExaflare()
    {
        if (IsBothFrontExaflareRightDir()) return -1;
        return IsExaflareRightDir(0) ? 2 : 0;
    }

    /// <summary>
    /// 找到背后是否被序号为idx的地火指
    /// </summary>
    /// <param name="idx">地火序号</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    private bool IsBackPointedByExaflare(int idx, ScriptAccessory accessory)
    {
        // 右上地火指向背后地火的条件：右上地火不是正火且方向不等于1
        // 左上地火指向背后地火的条件：左上地火不是正火且方向不等于7
        var result = idx switch
        {
            0 => !IsExaflareRightDir(idx) && ExaflareDirList[idx] != 1,
            2 => !IsExaflareRightDir(idx) && ExaflareDirList[idx] != 7,
            _ => false
        };
        return result;
    }

    /// <summary>
    /// 找到前方序号为idx的地火是否被指
    /// </summary>
    /// <param name="idx">地火序号</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    private bool IsFrontPointedByExaflare(int idx, ScriptAccessory accessory)
    {
        // 右上地火被指：左上地火为正火，且方向不为6（朝左） 或 背后地火是斜火，且方向不为5（朝左下）
        // 左上地火被指：右上地火为正火，且方向不为2（朝右） 或 背后地火是斜火，且方向不为3（朝右下）
        var result = idx switch
        {
            0 => (IsExaflareRightDir(2) && ExaflareDirList[2] != 6) ||
                 (!IsExaflareRightDir(1) && ExaflareDirList[1] != 5),
            2 => (IsExaflareRightDir(0) && ExaflareDirList[0] != 2) ||
                 (!IsExaflareRightDir(1) && ExaflareDirList[1] != 3),
            _ => false
        };
        return result;
    }
    
    public void SetBladeType(uint type)
    {
        BladeType = type;
    }

    private bool IsChariot()
    {
        const uint chariotFireBlade = 42;
        return BladeType == chariotFireBlade;
    }

    private void AddExaflareSolution(ExaflareSolution solution)
    {
        ExaflareSolutionList.Add(solution);
    }

    public List<Vector3> ExportExaflareSolution(ScriptAccessory accessory)
    {
        AddExaflareSolution(BuildOneStepSolutionNew(accessory));
        AddExaflareSolution(BuildTwoStepSolution(accessory));
        
        ExaflareSolutionList = ExaflareSolutionList.OrderBy(solution => solution.Score).ToList();
        accessory.DebugMsg($"两解法对比，优先级高的是{ExaflareSolutionList[0].Description}，为{ExaflareSolutionList[0].Score}分", DebugMode);
        return ExaflareSolutionList[0].ExaflareSolutionPosList;
    }
    
    /*
     * 下述为构建地火的方法，今后可以单独做成一个class调用
     */
    
    /// <summary>
    /// 构建地火坐标
    /// </summary>
    /// <param name="center">中心</param>
    /// <param name="rotation">旋转角度</param>
    /// <param name="extendDistance">延伸距离</param>
    /// <returns></returns>
    private Vector3 GetExaflarePos(Vector3 center, float rotation, float extendDistance)
    {
        return center.ExtendPoint(rotation, extendDistance);
    }

    private Vector3[] BuildExaflareVector(Vector3 center, float rotation, int extendNum, float extendDistance)
    {
        var exaflarePos = new Vector3[extendNum];
        for (var i = 0; i < extendNum; i++)
            exaflarePos[i] = GetExaflarePos(center, rotation, (i + 1) * extendDistance);
        return exaflarePos;
    }

    public bool ExaflareRecordComplete()
    {
        return _recordedExaflareNum == 3;
    }

    private string GetExaflareIdxStr(int idx)
    {
        return idx switch
        {
            0 => "右上",
            1 => "背后",
            2 => "左上",
            _ => "未知"
        };
    }
    
    private string GetDirStr(int idx)
    {
        return idx switch
        {
            0 => "正上",
            1 => "右上",
            2 => "正右",
            3 => "右下",
            4 => "正下",
            5 => "左下",
            6 => "正左",
            7 => "左上",
            _ => "未知"
        };
    }

    public class ExaflareSolution
    {
        /*
         * 地火优选策略
         * 地火共有四种解法选项：
         * 1、绝不去前方 NeverFront
         *      背后两步火>无脑火>>>前方一步火>前方两步火
         * 2、绝不跑无脑火 NeverUniverse
         *      背后两步火>前方一步火>前方两步火>>>无脑火
         * 3、绝不多跑 LeastMovement
         *      前方一步火>背后两步火>前方两步火>>>无脑火
         * 4、绝对前方 AlwaysFront
         *      前方一步火>前方两步火>背后两步火>无脑火
         * 四种解法被求解后，分数低者取胜。
         *
         * 解法情况与影响分值的对应关系
         *                  basic   moveStep    isFront     isUniverse
         * NeverFront        100        2         100           50
         * NeverUniverse     100        2          10          100
         * LeastMovement     100       20          10           50
         * AlwaysFront       100        2         -10           50
         */
        public List<Vector3> ExaflareSolutionPosList { get; set; }
        public int MoveStep { get; set; }
        public bool IsFront { get; set; }
        public bool IsUniverse { get; set; }
        public int Score { get; set; }
        public string Description { get; set; }
        public bool DebugMode { get; set; }

        public ExaflareSolution(List<Vector3> exaflareSolutionPosList, int moveStep, bool isFront, bool isUniverse,
            string description, List<int> scoreList, bool debugMode, ScriptAccessory accessory)
        {
            ExaflareSolutionPosList = exaflareSolutionPosList;
            MoveStep = moveStep;
            IsFront = isFront;
            IsUniverse = isUniverse;
            Score = CalcScore(scoreList, accessory, debugMode, description);
            Description = description;
            DebugMode = debugMode;
        }
        private int CalcScore(List<int> scoreList, ScriptAccessory accessory, bool debugMode, string description)
        {
            const int moveStepIdx = 0;
            const int isFrontIdx = 1;
            const int isUniverseIdx = 2;
            const int baseScore = 100;
            var moveStepScore = scoreList[moveStepIdx] * MoveStep;
            var isFrontScore = IsFront ? scoreList[isFrontIdx] : 0;
            var isUniverseScore = IsUniverse ? scoreList[isUniverseIdx] : 0;
            var totalScore = baseScore + moveStepScore + isFrontScore + isUniverseScore;
            accessory.DebugMsg(
                $"{description}的得分为：基础{baseScore} + 步数{moveStepScore} + 前方{isFrontScore} + 无脑{isUniverseScore} = {totalScore}",
                debugMode);
            return totalScore;
        }
    }
}

#endregion


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

    public static float SourceRotation(this Event @event)
    {
        return JsonConvert.DeserializeObject<float>(@event["SourceRotation"]);
    }

    public static string SourceName(this Event @event)
    {
        return @event["SourceName"];
    }

    public static uint Id(this Event @event)
    {
        return ParseHexId(@event["Id"], out var id) ? id : 0;
    }

    public static uint StatusId(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["StatusID"]);
    }

    public static uint StackCount(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["StackCount"]);
    }
}

public static class IbcHelper
{
    public static IGameObject? GetById(this ScriptAccessory sa, uint id)
    {
        return sa.Data.Objects.SearchByEntityId(id);
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
        // 找到某点到中心的弧度
        float radian = MathF.PI - MathF.Atan2(newPoint.X - center.X, newPoint.Z - center.Z);
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
        // Vector3 v3 = new(2 * centerX - point.X, point.Y, point.Z);
        // return v3;
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
        // Vector3 v3 = new(point.X, point.Y, 2 * centerZ - point.Z);
        // return v3;
        return point with { Z = 2 * centerZ - point.Z };
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
    /// 从场中看向场外是否在右侧，多用于场边敌人的分身机制
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
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static string GetPlayerJobByIndex(this ScriptAccessory accessory, int idx)
    {
        var str = idx switch
        {
            0 => "MT",
            1 => "ST",
            2 => "H1",
            3 => "H2",
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
    public static ScriptColor ColorExaflare = new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 0.0f, 1.5f) };
    public static ScriptColor ColorExaflareWarn = new ScriptColor { V4 = new Vector4(0.6f, 0.6f, 1f, 1.0f) };
}

public static class ListHelper
{
    /// <summary>
    /// 将List转为String以输出
    /// </summary>
    /// <param name="list"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static string StringList<T>(this List<T> list)
    {
        return string.Join(", ", list);
    }
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
    {
        return targetObj switch
        {
            uint uintTarget => accessory.DrawGuidance(accessory.Data.Me, uintTarget, delay, destroy, name, rotation, scale, isSafe),
            Vector3 vectorTarget => accessory.DrawGuidance(accessory.Data.Me, vectorTarget, delay, destroy, name, rotation, scale, isSafe),
            _ => throw new ArgumentException("targetObj 的类型必须是 uint 或 Vector3")
        };
    }
    
    
    /// <summary>
    /// 返回自己指向某目标地点的dp，可修改dp.TargetPosition, dp.Scale
    /// </summary>
    /// <param name="targetPos">指向地点</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="scale">指路线条宽度</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawDirPos(this ScriptAccessory accessory, Vector3 targetPos, int delay, int destroy, string name, float scale = 1f)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(scale);
        dp.Owner = accessory.Data.Me;
        dp.TargetPosition = targetPos;
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        return dp;
    }

    /// <summary>
    /// 返回起始地点指向某目标地点的dp，可修改dp.Position, dp.TargetPosition, dp.Scale
    /// </summary>
    /// <param name="startPos">起始地点</param>
    /// <param name="targetPos">指向地点</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="scale">指路线条宽度</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawDirPos2Pos(this ScriptAccessory accessory, Vector3 startPos, Vector3 targetPos, int delay, int destroy, string name, float scale = 1f)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(scale);
        dp.Position = startPos;
        dp.TargetPosition = targetPos;
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        return dp;
    }

    /// <summary>
    /// 返回自己指向某目标对象的dp，可修改dp.TargetObject, dp.Scale
    /// </summary>
    /// <param name="targetId">指向目标对象</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="scale">指路线条宽度</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawDirTarget(this ScriptAccessory accessory, uint targetId, int delay, int destroy, string name, float scale = 1f)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(scale);
        dp.Owner = accessory.Data.Me;
        dp.TargetObject = targetId;
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.Color = accessory.Data.DefaultSafeColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        return dp;
    }

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
        dp.CentreResolvePattern = PositionResolvePatternEnum.OwnerTarget;
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
    /// <param name="width">绘图宽度</param>
    /// <param name="length">绘图长度</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="lengthByDistance">长度是否随距离改变</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawTarget2Target(this ScriptAccessory accessory, uint ownerId, uint targetId, float width, float length, int delay, int destroy, string name, bool lengthByDistance = false, bool byTime = false)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
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
    /// <param name="center">绘图中心位置</param>
    /// <param name="radian">图形角度</param>
    /// <param name="rotation">旋转角度，以北为0度顺时针</param>
    /// <param name="width">绘图宽度</param>
    /// <param name="length">绘图长度</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawStatic(this ScriptAccessory accessory, Vector3 center, float radian, float rotation, float width, float length, int delay, int destroy, string name)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.Position = center;
        dp.Radian = radian;
        dp.Rotation = rotation.Logic2Game();
        dp.Color = accessory.Data.DefaultDangerColor;
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
    public static DrawPropertiesEdit DrawStaticCircle(this ScriptAccessory accessory, Vector3 center, Vector4 color, int delay, int destroy, string name, float scale = 1.5f)
    {
        var dp = accessory.DrawStatic(center, 0, 0, scale, scale, delay, destroy, name);
        dp.Color = color;
        return dp;
    }

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
    public static DrawPropertiesEdit DrawStaticDonut(this ScriptAccessory accessory, Vector3 center, Vector4 color, int delay, int destroy, string name, float scale, float innerscale = 0)
    {
        var dp = accessory.DrawStatic(center, float.Pi * 2, 0, scale, scale, delay, destroy, name);
        dp.Color = color;
        dp.InnerScale = innerscale != 0f ? new Vector2(innerscale) : new Vector2(scale - 0.05f);
        return dp;
    }
    
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
}

#endregion

