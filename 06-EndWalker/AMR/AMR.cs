using System;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Dalamud.Utility.Numerics;
using KodakkuAssist.Script;
using KodakkuAssist.Data;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.Draw.Manager;
using KodakkuAssist.Extensions;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs;
using KodakkuAssist.Module.Script.Type;

namespace UsamisKodakku.Scripts._06_EndWalker.AMR;

[ScriptType(name: Name, territorys: [1155, 1156], guid: "f08d6d7e-05fe-4267-a6fc-aa2355ab3e45",
    version: Version, author: "Usami", note: NoteStr, updateInfo: UpdateInfo)]

// ^(?!.*((武僧|机工士|龙骑士|武士|忍者|蝰蛇剑士|钐镰客|舞者|吟游诗人|占星术士|贤者|学者|(朝日|夕月)小仙女|炽天使|白魔法师|战士|骑士|暗黑骑士|绝枪战士|绘灵法师|黑魔法师|青魔法师|召唤师|宝石兽|亚灵神巴哈姆特|亚灵神不死鸟|迦楼罗之灵|泰坦之灵|伊弗利特之灵|后式自走人偶)\] (Used|Cast))).*35501.*$

public class Amr
{
    const string NoteStr =
        """
        v0.0.0.3
        Boss2 不含踩塔运动会2
        Boss3 不含青帝天箭
        """;

    private const string Name = "AMR [异闻六根山]";
    private const string Version = "0.0.0.3";
    private const string DebugVersion = "a";
    private const string UpdateInfo =
        """
        1. 适配鸭鸭0.5.x.x
        """;

    [UserSetting("站位提示圈绘图-普通颜色")]
    public static ScriptColor PosColorNormal { get; set; } =
        new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 1.0f, 1.0f) };

    [UserSetting("站位提示圈绘图-玩家站位颜色")]
    public static ScriptColor PosColorPlayer { get; set; } =
        new ScriptColor { V4 = new Vector4(0.0f, 1.0f, 1.0f, 1.0f) };

    [UserSetting("Boss2 地火爆炸区颜色")]
    public ScriptColor exaflareColor { get; set; } = new ScriptColor { V4 = new Vector4(0f, 1.0f, 1.0f, 1.0f) };

    [UserSetting("Boss2 地火预警区颜色")]
    public ScriptColor exaflareWarnColor { get; set; } = new ScriptColor { V4 = new Vector4(0f, 0.5f, 1.0f, 1.0f) };

    private enum AmrPhase
    {
        Init,
        Boss3_BoundlessScarlet, // Boss3 赤帝空闪刃
        Boss3_ShadowTwin1,      // Boss3 召唤幻影一
        Boss3_ShadowTwin2,      // Boss3 召唤幻影二
    }

    private AmrPhase _phase = AmrPhase.Init;
    private static Vector3 _centerBoss1 = new Vector3(0, 0, -100);
    private static Vector3 _centerBoss2 = new Vector3(300, 7, -120);
    private static Vector3 _centerBoss3 = new Vector3(-200, -195, 0);
    private List<bool> _drawn = new bool[20].ToList(); // 绘图记录
    private volatile List<bool> _recorded = new bool[20].ToList(); // 被记录flag
    private List<AutoResetEvent> _autoEvents = Enumerable
        .Range(0, 20)
        .Select(_ => new AutoResetEvent(false))
        .ToList();
    private List<ManualResetEvent> _manualEvents = Enumerable
        .Range(0, 20)
        .Select(_ => new ManualResetEvent(false))
        .ToList();
    
    DateTime _timeLock = new();

    /* 事件记录 _drawn
     * 2: 雷暴云
     */

    /* 事件记录 _manualEvents
     * 0: 黑赤招魂
     * 1: 雷暴云
     * 2: 雷暴云
     */

    private VengefulSouls _vs = new VengefulSouls();
    private StormClouds _sc = new StormClouds();
    private FlameAndSulphur _fas = new FlameAndSulphur();
    private HumbleHammer _hh = new HumbleHammer();
    private RousingReincarnation _rr = new RousingReincarnation();

    private List<bool> _wailIsStackAndFirst = new bool[5].ToList(); // Boss1 不寻常咒声，分摊，先分摊记录
    private List<int> _liveFireStackIdx = []; // Boss2 火印分摊点名记录
    private int _buffCount = 0; // Boss2 分摊buff计数
    private static bool _stackSwap = false; // Boss2 分摊交换
    private List<uint> _superJumpId = [0, 0, 0, 0]; // Boss2 超级跳id
    private int _superJumpCount = 0; // Boss2 超级跳顺序记录
    private List<Kasumi> _multiKasumisList = []; // Boss3 霞斩记录
    private List<bool> _VengenceIsStackAndFirst = new bool[5].ToList(); // Boss3 怨咒祈请，分摊，先分摊记录
    // private List<(uint, uint)> _tetherSourceAndTarget = new List<(uint, uint)>(); // Boss3 连线单位与目标
    private bool _isTetherTarget = false;   // Boss3 是连线目标
    private bool _isLRHand = false; // Boss3 大手手在左右
    

    public void Init(ScriptAccessory accessory)
    {
        accessory.Log.Debug($"Init {Name} v{Version}{DebugVersion} Success.\n{UpdateInfo}");
        ParamsInit(accessory);
    }
    
    public void ParamsInit(ScriptAccessory accessory)
    {
        _phase = AmrPhase.Init;
        _drawn = new bool[20].ToList();
        _recorded = new bool[20].ToList();
        _autoEvents = Enumerable
            .Range(0, 20)
            .Select(_ => new AutoResetEvent(false))
            .ToList();
        _manualEvents = Enumerable
            .Range(0, 20)
            .Select(_ => new ManualResetEvent(false))
            .ToList();

        _superJumpId = [0, 0, 0, 0];
        _superJumpCount = 0;
        
        accessory.Method.MarkClear();
        accessory.Method.RemoveDraw(".*");
    }

    [ScriptMethod(name: "--- 测试项 ---", eventType: EventTypeEnum.Chat, eventCondition: ["HelloAyaWorld"],
        userControl: false)]
    public void EchoDebug(Event @event, ScriptAccessory accessory)
    {
        // ---- DEBUG CODE ----

        var sid = (uint)0x400070B3;
        var dp = accessory.DrawFan(sid, 180f.DegToRad(), 0, 26, 0, 0, 20000, $"大手手{sid}");
        dp.Color = ColorHelper.ColorDark.V4.WithW(2f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);

        // -- DEBUG CODE END --
    }
    
    [ScriptMethod(name: "团灭初始化", eventType: EventTypeEnum.RemoveCombatant, eventCondition: ["DataId:regex:^(1620[05]|1622[57]|16265|16270)$"], userControl: false, suppress: 3000)]
    public void WipeInit(Event @event, ScriptAccessory accessory)
    {
        var bossId = @event.DataId();
        const uint nShishio = 16200;
        const uint sShishio = 16205;
        const uint nGorai = 16225;
        const uint sGorai = 16227;
        const uint nMoko = 16265;
        const uint sMoko = 16270;

        var bossName = bossId switch
        {
            nShishio => "Boss1 狮子王",
            sShishio => "Boss1 零式狮子王",
            nGorai => "Boss2 铁鼠",
            sGorai => "Boss2 零式铁鼠",
            nMoko => "Boss3 武士",
            sMoko => "Boss3 零式武士",
            _ => "未知"
        };
        
        ParamsInit(accessory);
        accessory.Log.Debug($"检测到{bossName}的刷新，参数初始化。");
    }

    #region Boss1 舞狮

    [ScriptMethod(name: "---- Boss1 狮子王 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["Hello World"],
        userControl: true)]
    public void SplitLine_Boss1(Event @event, ScriptAccessory accessory)
    {
    }

    [ScriptMethod(name: "死刑与甩尾", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(338(19|20|5[89]))$"],
        userControl: true)]
    public void SplittingCry(Event @event, ScriptAccessory accessory)
    {
        var aid = @event.ActionId();
        var tid = @event.TargetId();
        var sid = @event.SourceId();

        HashSet<uint> tankBuster = [33819, 33858];
        HashSet<uint> backSwipe = [33820, 33859];

        if (tankBuster.Contains(aid))
        {
            var dp = accessory.DrawTarget2Target(sid, tid, 14f, 60f, 0, 5000, $"灵鸣炮");
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }

        if (backSwipe.Contains(aid))
        {
            var dp0 = accessory.DrawFrontBackCleave(sid, false, 0, 2000, $"扇形后刀", 90f.DegToRad(), 40f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp0);
        }
    }

    [ScriptMethod(name: "六条奔雷矩形范围", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(33(790|829))$"],
        userControl: true)]
    public void RokujoRevelRectAoe(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var dp = accessory.DrawRect(sid, 14f, 60f, 0, 8000, $"六条奔雷矩形{sid}");
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
    }

    [ScriptMethod(name: "狮子王牙矩形范围", eventType: EventTypeEnum.AddCombatant,
        eventCondition: ["DataId:regex:^(1620[27])$"],
        userControl: true)]
    public void NoblePursuitRectAoe(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var dp = accessory.DrawRect(sid, 12f, 120f, 0, 12000, $"狮子王牙本体{sid}");
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
        var dp0 = accessory.DrawRect(sid, 10f, 120f, 0, 12000, $"狮子王牙扩散{sid}");
        dp0.Rotation = float.Pi / 2;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp0);
    }

    [ScriptMethod(name: "狮子王牙范围删除", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(338(01|40))$"],
        userControl: false)]
    public void NoblePursuitRectAoeRemove(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        accessory.Method.RemoveDraw($"狮子王牙本体{sid}");
        accessory.Method.RemoveDraw($"狮子王牙扩散{sid}");
    }

    [ScriptMethod(name: "诡异咒声Buff记录重置", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(338(15|54))$"], userControl: false)]
    public void UnnaturalWailBuffReset(Event @event, ScriptAccessory accessory)
    {
        _wailIsStackAndFirst = new bool[5].ToList();
    }

    [ScriptMethod(name: "诡异咒声Buff记录", eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:regex:^(356[34])$"],
        userControl: false)]
    public void UnnaturalWailBuffRecord(Event @event, ScriptAccessory accessory)
    {
        // const uint spread = 3563;
        const uint stack = 3564;
        const uint buffLongThreshold = 20000;

        var tid = @event.TargetId();
        var tidx = accessory.GetPlayerIdIndex(tid);
        var stid = @event.StatusId();
        var dur = @event.DurationMilliseconds();

        if (stid == stack)
            _wailIsStackAndFirst[tidx] = true;
        if (dur > buffLongThreshold && stid == stack)
            _wailIsStackAndFirst[4] = true;
    }

    [ScriptMethod(name: "诡异咒声分摊分散", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(356[34])$"],
        userControl: true)]
    public void UnnaturalWailBuff(Event @event, ScriptAccessory accessory)
    {
        // const uint spread = 3563;
        const uint stack = 3564;

        var tid = @event.TargetId();
        var tidx = accessory.GetPlayerIdIndex(tid);
        var myIndex = accessory.GetMyIndex();
        var stid = @event.StatusId();
        var dur = @event.DurationMilliseconds();

        if (stid == stack)
        {
            var dp = accessory.DrawCircle(tid, 6, (int)dur - 5000, 5000, $"分摊");
            if (IsJobPartner(tidx, myIndex))
                dp.Color = accessory.Data.DefaultSafeColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        else
        {
            var dp = accessory.DrawCircle(tid, 6, (int)dur - 5000, 5000, $"分散");
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    [ScriptMethod(name: "钢铁月环", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(338(1[013]|5[02]|49))$"],
        userControl: true)]
    public void UnnaturalWailChariotDonut(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var aid = @event.ActionId();

        HashSet<uint> chariot = [33811, 33850];
        HashSet<uint> donut = [33813, 33852];
        HashSet<uint> singleDonut = [33810, 33849];

        if (chariot.Contains(aid))
        {
            var dp = accessory.DrawCircle(sid, 15, 0, 5200, $"咒声钢铁");
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            var dp0 = accessory.DrawDonut(sid, 30, 8, 5200, 4000, $"咒声月环");
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp0);
        }

        if (donut.Contains(aid))
        {
            var dp = accessory.DrawDonut(sid, 30, 8, 0, 5200, $"咒声月环");
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
            var dp0 = accessory.DrawCircle(sid, 15, 5200, 4000, $"咒声钢铁");
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp0);
        }

        if (singleDonut.Contains(aid))
        {
            var dp = accessory.DrawDonut(sid, 30, 8, 0, 5000, $"月环");
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
        }
    }

    [ScriptMethod(name: "诡异咒声左右刀", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(338(0[34]|4[23]))$"],
        userControl: true)]
    public void UnnaturalWailSwipe(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var aid = @event.ActionId();

        HashSet<uint> rightSwipe = [33803, 33842];
        HashSet<uint> leftSwipe = [33804, 33843];

        if (rightSwipe.Contains(aid))
        {
            var dp = accessory.DrawLeftRightCleave(sid, false, 5000, 5000, $"幽鬼右刀");
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        if (leftSwipe.Contains(aid))
        {
            var dp = accessory.DrawLeftRightCleave(sid, true, 5000, 5000, $"幽鬼右刀");
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }
    }

    [ScriptMethod(name: "黑赤招魂初始化", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(338(02|41))$"],
        userControl: false)]
    public void VengefulSoulsReset(Event @event, ScriptAccessory accessory)
    {
        _vs.Init(accessory);
    }

    [ScriptMethod(name: "黑赤招魂塔记录", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(338(07|46))$"],
        userControl: false)]
    public void VengefulSoulsTowerRecord(Event @event, ScriptAccessory accessory)
    {
        var tpos = @event.TargetPosition();
        lock (_vs)
        {
            _vs.AddTowerPos(tpos);
            if (_vs.GetTowerNum() == 2)
                _manualEvents[0].Set();
        }
    }

    [ScriptMethod(name: "黑赤招魂塔指路", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(338(06|45))$"],
        userControl: true)]
    public void VengefulSoulsTowerGuide(Event @event, ScriptAccessory accessory)
    {
        _manualEvents[0].WaitOne();
        var towerSolution = _vs.ExportTowerSolution();
        var myIndex = accessory.GetMyIndex();
        for (var i = 0; i < towerSolution.Count; i++)
        {
            var playerIdx = towerSolution[i].Item1;
            if (myIndex != playerIdx) continue;
            var towerPos = towerSolution[i].Item2;
            var dpTower = accessory.DrawStaticCircle(towerPos, accessory.Data.DefaultSafeColor, 0, 15000, $"塔范围", 4);
            var dpGuide = accessory.DrawGuidance(towerPos, 0, 15000, $"塔指路");
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dpTower);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpGuide);
        }

        _manualEvents[0].Reset();
    }

    [ScriptMethod(name: "黑赤招魂大圈", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(338(08|47))$"],
        userControl: true)]
    public void VengefulSoulsDefamation(Event @event, ScriptAccessory accessory)
    {
        var tid = @event.TargetId();
        var tidx = accessory.GetPlayerIdIndex(tid);
        _vs.AddDefamationTargetIdx(tidx);
        var dp = accessory.DrawCircle(tid, 15, 11000, 4000, $"黑赤招魂大圈");
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    private class VengefulSouls
    {
        public ScriptAccessory accessory { get; set; } = null!;
        private readonly List<int> _defamationTargetIdx = [];
        private static readonly List<int> PriorityList = [0, 1, 2, 3];
        private List<Vector3> _towerPos = [];

        public void Init(ScriptAccessory _accessory)
        {
            _defamationTargetIdx.Clear();
            _towerPos.Clear();
            accessory = _accessory;
        }

        public void AddDefamationTargetIdx(int idx)
        {
            _defamationTargetIdx.Add(idx);
            accessory.Log.Debug($"检测到{accessory.GetPlayerJobByIndex(idx, true)}的大圈");
        }

        public void AddTowerPos(Vector3 pos)
        {
            _towerPos.Add(pos);
            accessory.Log.Debug($"检测到{pos}的塔");
        }

        public int GetTowerNum()
        {
            return _towerPos.Count;
        }

        public List<(int, Vector3)> ExportTowerSolution()
        {
            SortTowerPosition();
            var towerTargets = FindTowerTargets();
            var combinedList = towerTargets.Zip(_towerPos, (dir, pos) => (dir, pos)).ToList();

            var str = "";
            for (var i = 0; i < combinedList.Count; i++)
            {
                str += $"{accessory.GetPlayerJobByIndex(combinedList[i].Item1, true)}踩塔{combinedList[i].Item2}\n";
            }

            accessory.Log.Debug(str);
            return combinedList;
        }

        /// <summary>
        /// 从小到大排列，正好符合北起顺时针
        /// </summary>
        private void SortTowerPosition()
        {
            var towerDir = _towerPos.Select(pos => pos.Position2Dirs(_centerBoss1, 8)).ToList();
            var combinedList = towerDir.Zip(_towerPos, (dir, pos) => (dir, pos));
            var sortedCombined = combinedList.OrderBy(x => x.dir).ToList();
            _towerPos = sortedCombined.Select(x => x.pos).ToList();
        }

        /// <summary>
        /// 找到存在于priorityList，但不存在于defamationTarget中的元素，并排序
        /// </summary>
        /// <returns></returns>
        private List<int> FindTowerTargets()
        {
            var ls = PriorityList.Except(_defamationTargetIdx)
                .OrderBy(x => PriorityList.IndexOf(x)).ToList();
            return ls;
        }
    }

    [ScriptMethod(name: "雷暴云初始化", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(33(784|823))$"],
        userControl: false)]
    public void StormCloudReset(Event @event, ScriptAccessory accessory)
    {
        _sc.Init(accessory);
    }

    [ScriptMethod(name: "雷暴云添加", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(1620[16])$"],
        userControl: false)]
    public void StormCloudAdd(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var spos = @event.SourcePosition();
        lock (_sc)
        {
            _sc.AddCloud(sid, spos);
        }
    }

    [ScriptMethod(name: "雷暴云移除", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(33(787|826))$"],
        userControl: false)]
    public void StormCloudRemove(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        _sc.DisableCloud(sid);
    }

    [ScriptMethod(name: "吞霞次数添加", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(33(78[56]|82[45]))$"],
        userControl: false)]
    public void SmokeatTimeAdd(Event @event, ScriptAccessory accessory)
    {
        _sc.AddSmokeatTime();
    }

    [ScriptMethod(name: "六条奔雷角度记录", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(33(790|829))$"],
        userControl: false)]
    public void RokujoRevelRotationRecord(Event @event, ScriptAccessory accessory)
    {
        var srot = @event.SourceRotation();
        _sc.AddBossRotation(srot);
        if (_sc.GetSetCondition())
            _manualEvents[1].Set();
    }

    [ScriptMethod(name: "雷暴云吞霞范围与指路", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(33(788|827))$"],
        userControl: true)]
    public void StormCloudGuidance(Event @event, ScriptAccessory accessory)
    {
        // 读条“六条奔雷”时开始等待
        _manualEvents[1].WaitOne();
        var dpList = _sc.ExportCloudSolution();
        lock (dpList)
        {
            accessory.Log.Debug($"需要画{dpList[0].Count}个雷云危险区");
            for (var i = 0; i < dpList[0].Count; i++)
            {
                // 雷暴云危险区
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpList[0][i]);
            }

            // TODO 好难写，老调不对

            for (var i = 0; i < dpList[1].Count; i++)
            {
                // 雷暴云就位区与目的地
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dpList[1][i]);
            }

            // for (var i = 0; i < dpList[2].Count; i++)
            // {
            //     // 雷暴云指路
            //     if (i % 2 != 0) continue;
            //     accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpList[2][i]);
            // }

        }

        _manualEvents[1].Reset();
        _manualEvents[2].WaitOne();
        accessory.Method.RemoveDraw($"雷云就位区1");
        accessory.Method.RemoveDraw($"雷云指路1");
        accessory.Method.RemoveDraw($"雷云指路12");
        // accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpList[2][2]);
        _manualEvents[2].Reset();
    }

    [ScriptMethod(name: "雷暴云范围移除", eventType: EventTypeEnum.ActionEffect,
        eventCondition: ["ActionId:regex:^(33(79[123]|83[012]))$"],
        userControl: false)]
    public void StormCloudDangerFieldRemove(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        accessory.Method.RemoveDraw($"雷云{sid}");
        if (_drawn[2]) return;
        lock (this)
        {
            // 等待下一波雷暴云爆炸后移除
            if ((DateTime.Now - _timeLock).TotalSeconds < 0.6f) return;
            _timeLock = DateTime.Now;
            _drawn[2] = true;
            _manualEvents[2].Set();
        }
    }

    [ScriptMethod(name: "雷暴云指路移除", eventType: EventTypeEnum.EnvControl,
        eventCondition: ["State:00080004", "Index:00000034"],
        userControl: false)]
    public void StormCloudGuidanceRemove(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw($".*");
    }

    private class StormClouds
    {
        private ScriptAccessory accessory { get; set; } = null!;
        private readonly List<(uint, Vector3, bool)> _cloud = [];
        private readonly List<float> _bossRotation = [];
        private int _smokeatTime = 0;

        public void Init(ScriptAccessory _accessory)
        {
            _cloud.Clear();
            _bossRotation.Clear();
            _smokeatTime = 0;
            accessory = _accessory;
        }

        public void AddCloud(uint id, Vector3 pos)
        {
            _cloud.Add((id, pos, true));
        }

        public void DisableCloud(uint id)
        {
            for (var i = 0; i < _cloud.Count; i++)
            {
                if (_cloud[i].Item1 == id)
                    _cloud[i] = (_cloud[i].Item1, _cloud[i].Item2, false);
            }
        }

        public void AddSmokeatTime()
        {
            _smokeatTime++;
        }

        public void AddBossRotation(float rot)
        {
            _bossRotation.Add(rot);
        }

        public bool GetSetCondition()
        {
            return _bossRotation.Count == _smokeatTime;
        }

        private bool IsInRange(Vector3 pos, float bossRotation)
        {
            // 记录了逆时针旋转的游戏基，将云顺时针旋转正好抵消
            var relativePos = pos.RotatePoint(_centerBoss1, -1 * bossRotation.Game2Logic());
            // 检查x轴是否在范围内即可，六条奔雷宽度14
            return Math.Abs(relativePos.X) <= 7f;
        }

        private bool IsNearEdge(Vector3 pos)
        {
            var v2 = new Vector2(pos.X - _centerBoss1.X, pos.Z - _centerBoss1.Z);
            return v2.Length() > 15f;
        }

        private Vector3 FindRelativeSafePos(Vector3 pos)
        {
            Vector3 safePos;
            if (_smokeatTime != 3)
            {
                // 计算偏外侧
                if (IsNearEdge(pos))
                {
                    var isCw = pos.Position2Dirs(_centerBoss1, 4, false) % 2 == 0;
                    safePos = pos.RotatePoint(_centerBoss1, isCw ? 15f.DegToRad() : -15f.DegToRad());
                    accessory.Log.Debug($"安全云点{pos}在外侧，{(isCw ? "顺" : "逆")}时针旋转");
                }
                else
                {
                    var relativePos = pos.RotatePoint(_centerBoss1, -1 * _bossRotation[0].Game2Logic());
                    var isRight = pos.X > _centerBoss1.X;
                    var relativeSafePos = relativePos with { X = relativePos.X + (isRight ? 4f : -4f) };
                    safePos = relativeSafePos.RotatePoint(_centerBoss1, _bossRotation[0].Game2Logic());
                    accessory.Log.Debug($"安全云点{pos}在内，直接对X坐标处理");
                }
            }
            else
            {
                var isRight = pos.X > _centerBoss1.X;
                safePos = new Vector3(isRight ? -19f : 19f, pos.Y, _centerBoss1.Z);
            }

            return safePos;
        }

        public List<List<DrawPropertiesEdit>> ExportCloudSolution()
        {
            List<List<DrawPropertiesEdit>> dpList = [[], [], []]; // 危险区、安全区、指路
            var cloudScale = _smokeatTime switch
            {
                1 => 8,
                2 => 12,
                3 => 23,
                _ => 8
            };
            // 吞霞一次，场中六选一；吞霞两次，场中四选一；吞霞三次，场中一对一
            // 机制核心解法是找到场中的六个雷暴云进行判断
            foreach (var cloud in _cloud.Where(cloud => IsInRange(cloud.Item2, _bossRotation[0])))
            {
                if (cloud.Item3)
                {
                    var dangerCloudPos = cloud.Item2;
                    accessory.Log.Debug($"危险云点为{dangerCloudPos}");
                    // 需画出对应危险区域
                    var dp = accessory.DrawCircle(cloud.Item1, cloudScale, 0, 10000, $"雷云{cloud.Item1}");
                    dpList[0].Add(dp);

                    // 三次吞霞需借助危险区反推安全区，三次吞霞只有一个cloud满足该条件，所以无需加额外条件
                    if (_smokeatTime != 3) continue;
                    var safePos = FindRelativeSafePos(dangerCloudPos);
                    var dpSafeField1 =
                        accessory.DrawStaticCircle(safePos, accessory.Data.DefaultSafeColor, 0, 10000, $"雷云就位区1");
                    var dpSafeField2 = accessory.DrawStaticCircle(dangerCloudPos, accessory.Data.DefaultSafeColor, 0,
                        10000, $"雷云就位区2");
                    dpList[1].Add(dpSafeField1);
                    dpList[1].Add(dpSafeField2);

                    var dpSafeGuide1 = accessory.DrawGuidance(safePos, 0, 10000, $"雷云指路1");
                    var dpSafeGuide12 = accessory.DrawGuidance(safePos, dangerCloudPos, 0, 10000, $"雷云指路12");
                    dpSafeGuide12.Color = accessory.Data.DefaultDangerColor;
                    var dpSafeGuide2 = accessory.DrawGuidance(dangerCloudPos, 0, 10000, $"雷云指路2");
                    dpList[2].Add(dpSafeGuide1);
                    dpList[2].Add(dpSafeGuide12);
                    dpList[2].Add(dpSafeGuide2);
                }
                else
                {
                    if (_smokeatTime == 3) continue;

                    var safeCloudPos = cloud.Item2;
                    var safePos = FindRelativeSafePos(safeCloudPos);
                    accessory.Log.Debug($"安全云点为{safeCloudPos}，对应安全区{safePos}");
                    var dp1 = accessory.DrawStaticCircle(safePos, accessory.Data.DefaultSafeColor, 0, 10000, $"雷云就位区1");
                    var dp2 = accessory.DrawStaticCircle(_centerBoss1, accessory.Data.DefaultSafeColor, 0, 10000,
                        $"雷云就位区2");
                    dpList[1].Add(dp1);
                    dpList[1].Add(dp2);

                    var dpSafeGuide1 = accessory.DrawGuidance(safePos, 0, 10000, $"雷云指路1");
                    var dpSafeGuide12 = accessory.DrawGuidance(safePos, _centerBoss1, 0, 10000, $"雷云指路12");
                    dpSafeGuide12.Color = accessory.Data.DefaultDangerColor;
                    var dpSafeGuide2 = accessory.DrawGuidance(_centerBoss1, 0, 10000, $"雷云指路2");
                    dpList[2].Add(dpSafeGuide1);
                    dpList[2].Add(dpSafeGuide12);
                    dpList[2].Add(dpSafeGuide2);
                }
            }

            return dpList;
        }
    }

    [ScriptMethod(name: "连线幽灵钢铁", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(1620[49])$"],
        userControl: true)]
    public void VengefulSoulsChariot(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var dp = accessory.DrawCircle(sid, 6, 0, 99999, $"连线幽灵钢铁{sid}");
        dp.Color = ColorHelper.ColorDark.V4;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "连线幽灵钢铁移除", eventType: EventTypeEnum.RemoveCombatant,
        eventCondition: ["DataId:regex:^(1620[49])$"],
        userControl: false)]
    public void VengefulSoulsChariotRemove(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        accessory.Method.RemoveDraw($"连线幽灵钢铁{sid}");
    }

    #endregion

    #region Boss2 捕鼠

    [ScriptMethod(name: "---- Boss2 铁鼠豪雷 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["Hello World"],
        userControl: true)]
    public void SplitLine_Boss2(Event @event, ScriptAccessory accessory)
    {
    }

    [ScriptMethod(name: "火印分摊分散Buff重置", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:34051"], userControl: false)]
    public void LiveFireBuffReset(Event @event, ScriptAccessory accessory)
    {
        _liveFireStackIdx.Clear();
    }

    [ScriptMethod(name: "火印分摊分散Buff", eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:regex:^(360[78])$"], userControl: true)]
    public void LiveFireBuff(Event @event, ScriptAccessory accessory)
    {
        // const uint spread = 3608;
        const uint stack = 3607;

        var tid = @event.TargetId();
        var tidx = accessory.GetPlayerIdIndex(tid);
        var stid = @event.StatusId();
        var dur = @event.DurationMilliseconds();

        if (stid == stack)
        {
            lock (_liveFireStackIdx)
            {
                _liveFireStackIdx.Add(tidx);
                if (_liveFireStackIdx.Count != 2) return;
            }

            var myIndex = accessory.GetMyIndex();
            for (var i = 0; i < 2; i++)
            {
                var owner = accessory.Data.PartyList[_liveFireStackIdx[i]];
                var dp = accessory.DrawCircle(owner, 6, (int)dur - 4000, 4000,
                    $"分摊{_liveFireStackIdx[i]}");
                if (IsJobPartner(_liveFireStackIdx[0], _liveFireStackIdx[1]))
                {
                    accessory.Log.Debug($"分摊目标为同职能组，需换位");
                    _stackSwap = true;
                    if (IsRangePartner(myIndex, _liveFireStackIdx[i]))
                        dp.Color = accessory.Data.DefaultSafeColor;
                }
                else
                {
                    accessory.Log.Debug($"分摊目标为不同职能组，无需换位");
                    _stackSwap = false;
                    if (IsJobPartner(myIndex, _liveFireStackIdx[i]))
                        dp.Color = accessory.Data.DefaultSafeColor;
                }

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }

            // T与D1分摊换位提示
            if (myIndex % 2 == 0)
                accessory.Method.TextInfo($"分摊换位", 3000);
        }
        else
        {
            var dp = accessory.DrawCircle(tid, 10, (int)dur - 4000, 4000,
                $"分散{tidx}");
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    [ScriptMethod(name: "岩火招来重置", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:34056"],
        userControl: false)]
    public void FlameAndSulphurReset(Event @event, ScriptAccessory accessory)
    {
        _fas.Init(accessory);
    }

    [ScriptMethod(name: "岩火招来添加元素", eventType: EventTypeEnum.ObjectChanged,
        eventCondition: ["Operate:Add", "DataId:regex:^(201333[12])$"], userControl: false)]
    public void FlameAndSulphurAddElements(Event @event, ScriptAccessory accessory)
    {
        // TODO 增加岩与火
        var did = @event.DataId();
        var sid = @event.SourceId();
        var spos = @event.SourcePosition();
        // const uint flame = 2013331;
        const uint sulphur = 2013332;

        lock (_fas)
        {
            if (did == sulphur)
                _fas.AddSulphur(sid, spos);
            else
                _fas.AddFire(sid, spos);
        }
    }

    [ScriptMethod(name: "琵琶旋律范围指路", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(3405[78])$"], userControl: true)]
    public void BrazenBalledGuidance(Event @event, ScriptAccessory accessory)
    {
        var aid = @event.ActionId();
        var targetSulphur = _fas.ExportBalladSolution(aid);
        var dp = accessory.DrawGuidance(targetSulphur.Item1, 0, 5000, $"琵琶旋律指路石头");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
        accessory.Method.TextInfo($"寻找指向石头的安全区", 3500);
    }

    private class FlameAndSulphur
    {
        private List<(uint, Vector3)> _sulphurs = [];
        private List<(uint, Vector3)> _fire = [];
        private List<(uint, Vector3)> _safeSulphurs = [];
        private ScriptAccessory accessory { get; set; } = null!;

        public void Init(ScriptAccessory _accessory)
        {
            _sulphurs.Clear();
            _fire.Clear();
            _safeSulphurs.Clear();
            accessory = _accessory;
        }

        public void AddSulphur(uint id, Vector3 pos)
        {
            _sulphurs.Add((id, pos));
        }

        public void AddFire(uint id, Vector3 pos)
        {
            _fire.Add((id, pos));
        }

        private void DrawFire(bool isExpand)
        {
            foreach (var fire in _fire)
            {
                if (isExpand)
                {
                    // Rect
                    var dp = accessory.DrawRect(fire.Item1, 10, 46, 0, 8000, $"扩散辣尾");
                    dp.Color = ColorHelper.ColorCyan.V4.WithW(2f);
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
                }
                else
                {
                    // Hot wing
                    var dp = accessory.DrawDonut(fire.Item1, 20, 10, 0, 8000, $"扩散辣翅");
                    dp.Scale = new Vector2(20f, 100f);
                    dp.Color = ColorHelper.ColorCyan.V4.WithW(2f);
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.HotWing, dp);
                }
            }
        }

        private void DrawSulphur(bool isExpand)
        {
            foreach (var sulphur in _sulphurs)
            {
                if (isExpand)
                {
                    var dp = accessory.DrawCircle(sulphur.Item1, 11, 0, 8000, $"扩散钢铁");
                    dp.Color = ColorHelper.ColorCyan.V4.WithW(2f);
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                }
                else
                {
                    var dp = accessory.DrawDonut(sulphur.Item1, 16, 6, 0, 8000, $"扩散月环");
                    dp.Color = ColorHelper.ColorCyan.V4.WithW(2f);
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
                }
            }
        }

        public (uint, Vector3) ExportBalladSolution(uint balladType)
        {
            const uint expandBallad = 34057;
            const uint splitBallad = 34058;
            var isExpand = balladType == expandBallad;
            FindSafeSulphurs();
            DrawFire(isExpand);
            DrawSulphur(isExpand);

            var myIndex = accessory.GetMyIndex();
            var targetSulphur = myIndex switch
            {
                0 => _stackSwap ? _safeSulphurs[1] : _safeSulphurs[0],
                1 => _safeSulphurs[0],
                2 => _stackSwap ? _safeSulphurs[0] : _safeSulphurs[1],
                3 => _safeSulphurs[1],
                _ => _safeSulphurs[0],
            };
            return targetSulphur;
        }

        private void FindSafeSulphurs()
        {
            var str = "";
            lock (this)
            {
                // 当石头出现在鬼火的攻击范围中才是安全的
                foreach (var sulphur in _sulphurs)
                {
                    foreach (var fire in _fire)
                    {
                        accessory.Log.Debug($"正在比较sulphur{sulphur.Item2}与fire{fire.Item2}");
                        if (!IsInRange(sulphur.Item2, fire.Item2)) continue;
                        _safeSulphurs.Add(sulphur);
                        str += $"位于{sulphur.Item2}的石头在火的攻击范围内，观察其安全区\n";
                    }
                }

                // 排序，0北1南
                _safeSulphurs.Sort((a, b) => a.Item2.Z.CompareTo(b.Item2.Z));
                accessory.Log.Debug(str);
            }
        }

        private bool IsInRange(Vector3 pos1, Vector3 pos2)
        {
            return (Math.Abs(pos1.X - pos2.X) <= 1f) || (Math.Abs(pos1.Z - pos2.Z) <= 1f);
        }
    }

    [ScriptMethod(name: "炎流范围与指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(34095)$"],
        userControl: true)]
    public void ImpurePurgationGuidance(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var myIndex = accessory.GetMyIndex();
        for (var i = 0; i < accessory.Data.PartyList.Count; i++)
        {
            var dp = accessory.DrawFan(sid, 45f.DegToRad(), 0, 60, 0, 0, 3600, $"炎流{i}");
            dp.TargetObject = accessory.Data.PartyList[i];
            if (i == myIndex)
                dp.Color = accessory.Data.DefaultSafeColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        List<float> extendDirs =
            [0, -90f.DegToRad().Logic2Game(), 180f.DegToRad().Logic2Game(), 90f.DegToRad().Logic2Game()];
        var dpList = accessory.DrawExtendDirection(sid, extendDirs, myIndex, 2f, 20f, 0, 3600, $"炎流方向",
            PosColorPlayer.V4.WithW(2f), PosColorNormal.V4);
        foreach (var dp in dpList)
        {
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
        }
    }

    [ScriptMethod(name: "炎流引导范围", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(34(097|131))$"], userControl: true)]
    public void ImpurePurgationBaitField(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var tid = @event.TargetId();
        var dp = accessory.DrawFan(sid, 45f.DegToRad(), 0, 60f, 0, 0, 2000, $"炎流引导");
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "万宝槌初始化", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(34080)$"],
        userControl: false)]
    public void HumbleHammerReset(Event @event, ScriptAccessory accessory)
    {
        _hh.Init(accessory);
    }

    [ScriptMethod(name: "万宝槌添加球", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(1622[68])$"],
        userControl: false)]
    public void HumbleHammerAddOrb(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var spos = @event.SourcePosition();
        lock (_hh)
        {
            _hh.AddOrbs(sid, spos);
        }
    }

    [ScriptMethod(name: "万宝槌目标球提示与挡枪", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(34084)$"], userControl: true)]
    public void HumbleHammerSolution(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var targetOrb = _hh.ExportTargetOrb();
        // 目标球范围
        var dp = accessory.DrawCircle(targetOrb, 8, 0, 5000, $"万宝槌目标球");
        dp.Color = accessory.Data.DefaultSafeColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

        if (accessory.Data.MyObject is null) return;
        
        // 目标球指路
        if (accessory.Data.MyObject.IsHealer())
        {
            var dpGuideToOrb = accessory.DrawGuidance(targetOrb, 0, 5000, $"万宝槌球指路");
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpGuideToOrb);
        }

        // 奶妈挡枪
        var dpWildCharge = accessory.DrawTarget2Target(sid, accessory.Data.PartyList[1], 8, 50, 3000, 5000, $"万宝槌挡枪");
        if (accessory.Data.MyObject.IsDps())
        {
            dpWildCharge.Color = accessory.Data.DefaultSafeColor;
        }

        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dpWildCharge);
    }

    private class HumbleHammer
    {
        private List<(uint, Vector3)> _orbsCorner = [];
        private List<(uint, Vector3)> _orbsMiddle = [];
        private ScriptAccessory accessory { get; set; } = null!;

        public void Init(ScriptAccessory _accessory)
        {
            _orbsCorner.Clear();
            _orbsMiddle.Clear();
            accessory = _accessory;
        }

        public void AddOrbs(uint id, Vector3 pos)
        {
            if (AtCorner(pos))
                _orbsCorner.Add((id, pos));
            else
                _orbsMiddle.Add((id, pos));
        }

        private bool AtCorner(Vector3 pos)
        {
            return pos.DistanceTo(_centerBoss2) > 20f;
        }

        public uint ExportTargetOrb()
        {
            var targetOrb = 0u;
            var maxOrbDistance = 0f;
            foreach (var orb in _orbsCorner)
            {
                var distance = 0f;
                foreach (var orbMiddle in _orbsMiddle)
                {
                    distance += orb.Item2.DistanceTo(orbMiddle.Item2);
                }

                if (!(distance > maxOrbDistance)) continue;
                maxOrbDistance = distance;
                targetOrb = orb.Item1;
            }

            return targetOrb;
        }
    }

    [ScriptMethod(name: "线塔初始化", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(34066)$"],
        userControl: false)]
    public void RousingReincarnationReset(Event @event, ScriptAccessory accessory)
    {
        _rr.Init(accessory);
    }

    [ScriptMethod(name: "橙蓝Buff记录", eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:regex:^(359[789]|360[01234])$"], userControl: false)]
    public void RousingReincarnationBuffRecord(Event @event, ScriptAccessory accessory)
    {
        var stid = @event.StatusId();
        var tid = @event.TargetId();
        _rr.AddBuff(tid, stid);
    }

    [ScriptMethod(name: "橙蓝塔标记", eventType: EventTypeEnum.EnvControl,
        eventCondition: ["State:00020001", "Index:regex:^(0000000[3456789A])$"], userControl: true)]
    public void RousingReincarnationTowerRecord(Event @event, ScriptAccessory accessory)
    {
        var index = @event.Index();
        lock (_rr)
        {
            _rr.AddTower(index);
            var isOrange = index < 7;
            accessory.Log.Debug($"添加了{(isOrange ? "橙" : "蓝")}塔{index}");

            if (_rr.GetTowerPlayerId(isOrange) != accessory.Data.Me) return;
            var towerPos = _centerBoss2 - new Vector3(0, 0, 11);
            towerPos = towerPos.RotatePoint(_centerBoss2, _rr.GetNewTowerDirection(isOrange) * float.Pi / 4);
            var dp = accessory.DrawStaticCircle(towerPos, accessory.Data.DefaultSafeColor, 0, 10000, $"塔{index}", 4f);
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
        }
    }

    private class RousingReincarnation
    {
        private List<uint> _orange = [0, 0, 0, 0];
        private List<uint> _blue = [0, 0, 0, 0];
        public List<int> _orangeTowerPos = [];
        public List<int> _blueTowerPos = [];
        private int _towerNum = -1;

        private ScriptAccessory accessory { get; set; } = null!;

        public void Init(ScriptAccessory _accessory)
        {
            _orange = [0, 0, 0, 0];
            _blue = [0, 0, 0, 0];
            _towerNum = -1;
            _orangeTowerPos.Clear();
            _blueTowerPos.Clear();
            accessory = _accessory;
        }

        public int GetNewTowerDirection(bool isOrange)
        {
            return isOrange ? _orangeTowerPos[_towerNum / 2] : _blueTowerPos[_towerNum / 2];
        }

        public uint GetTowerPlayerId(bool isOrange)
        {
            return isOrange ? _orange[_towerNum / 2] : _blue[_towerNum / 2];
        }

        public void AddTower(uint idx)
        {
            // 3 4 5 6 => 7 1 5 3
            // 7 8 9 10 => 0 6 2 4
            _towerNum++;
            var towerPos = idx switch
            {
                3 => 7,
                4 => 1,
                5 => 5,
                6 => 3,
                7 => 0,
                8 => 6,
                9 => 2,
                10 => 4,
                _ => 0
            };

            if (idx < 7)
                _orangeTowerPos.Add(towerPos);
            else
                _blueTowerPos.Add(towerPos);
        }

        public void AddBuff(uint id, uint buffId)
        {
            // const uint orange1 = 3597;
            // const uint orange2 = 3598;
            // const uint orange3 = 3599;
            // const uint orange4 = 3600;
            // const uint blue1 = 3601;
            // const uint blue2 = 3602;
            // const uint blue3 = 3603;
            // const uint blue4 = 3604;

            if (buffId <= 3600)
            {
                var idx = (int)buffId - 3597;
                _orange[idx] = id;
            }
            else
            {
                var idx = (int)buffId - 3601;
                _blue[idx] = id;
            }
        }

        public int GetTowerNum()
        {
            return _towerNum;
        }
    }

    [ScriptMethod(name: "地火渐进", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(34(089|125))$"], userControl: true)]
    public void CloudToGroundExaflare(Event @event, ScriptAccessory accessory)
    {
        var srot = @event.SourceRotation();
        var spos = @event.SourcePosition();
        var exaflare = new Exaflare();
        exaflare.Init(accessory);
        exaflare.SetExaflareBasicAttributes(spos, 6, srot.Game2Logic(), 7, 6, 1, 7000, 1100);
        exaflare.SetColor(exaflareColor.V4, exaflareWarnColor.V4);

        exaflare.GetExaflareScene(true);
        exaflare.GetExaflareWarnScene(true);
        exaflare.GetExaflareEdge(true);
    }

    public class Exaflare
    {
        // ReSharper disable once NullableWarningSuppressionIsUsed
        public ScriptAccessory accessory { get; set; } = null!;
        private readonly List<Vector3> _exaflarePos = [];
        private Vector3 _startPos = new(0, 0, 0);
        private float _scale;
        private int _extendNum;
        private float _extendDistance;
        private float _rotation;
        private int _advWarnNum;
        private int _intervalTime;
        private int _castTime;
        private Vector4 _exaflareColor = new(0, 0, 0, 0);
        private Vector4 _exaflareWarnColor = new(0, 0, 0, 0);

        public void Init(ScriptAccessory _accessory)
        {
            accessory = _accessory;
            _exaflarePos.Clear();
            _scale = 0;
            _startPos = new Vector3(0, 0, 0);
            _extendNum = 0;
            _extendDistance = 0;
            _rotation = 0;
            _advWarnNum = 0;
            _intervalTime = 0;
            _castTime = 0;
            _exaflareColor = accessory.Data.DefaultDangerColor;
            _exaflareWarnColor = accessory.Data.DefaultDangerColor;
        }

        private Vector3 GetExaflarePos(int extendIdx)
        {
            return _startPos.ExtendPoint(_rotation, _extendDistance * extendIdx);
        }

        public void BuildExaflareList()
        {
            if (_exaflarePos.Count != 0)
                _exaflarePos.Clear();
            for (var i = 0; i < _extendNum; i++)
                _exaflarePos.Add(GetExaflarePos(i));
        }

        public List<DrawPropertiesEdit> GetExaflareScene(bool draw)
        {
            var exaflareScene = new List<DrawPropertiesEdit>();
            for (var ext = 0; ext < _extendNum; ext++)
            {
                var destroy = ext == 0 ? _castTime : _intervalTime;
                var delay = ext == 0 ? 0 : _castTime + (ext - 1) * _intervalTime;
                var dp = GetExaflareDp(_exaflarePos[ext], delay, destroy);
                exaflareScene.Add(dp);
                if (draw)
                    accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            }

            return exaflareScene;
        }

        public List<DrawPropertiesEdit> GetExaflareWarnScene(bool draw)
        {
            if (_advWarnNum == 0) return [];
            var exaflareWarnScene = new List<DrawPropertiesEdit>();
            for (var ext = 0; ext < _extendNum; ext++)
            {
                var destroy = ext == 0 ? _castTime : _intervalTime;
                var delay = ext == 0 ? 0 : _castTime + (ext - 1) * _intervalTime;
                for (var adv = 1; adv <= _advWarnNum; adv++)
                {
                    if (ext >= _exaflarePos.Count - adv) continue;
                    var dp = GetExaflareWarn(_exaflarePos[ext + adv], adv, delay, destroy, _intervalTime);
                    exaflareWarnScene.Add(dp);
                    if (draw)
                        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
                }
            }

            return exaflareWarnScene;
        }

        public List<DrawPropertiesEdit> GetExaflareEdge(bool draw)
        {
            var exaflareEdgeScene = new List<DrawPropertiesEdit>();
            for (var ext = 0; ext < _extendNum; ext++)
            {
                var destroy = ext == 0 ? _castTime : _intervalTime;
                var delay = ext == 0 ? 0 : _castTime + (ext - 1) * _intervalTime;
                var dp = GetExaflareEdge(_exaflarePos[ext], delay, destroy);
                exaflareEdgeScene.Add(dp);
                if (draw)
                    accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Donut, dp);
            }

            return exaflareEdgeScene;
        }

        private DrawPropertiesEdit GetExaflareDp(Vector3 pos, int delay, int destroy)
        {
            var color = _exaflareColor;
            var dp = accessory.DrawStaticCircle(pos, color, delay, destroy, $"地火{pos}", _scale);
            dp.ScaleMode |= ScaleMode.ByTime;
            return dp;
        }

        private DrawPropertiesEdit GetExaflareEdge(Vector3 pos, int delay, int destroy)
        {
            var color = _exaflareColor;
            var dp = accessory.DrawStaticDonut(pos, color, delay, destroy, $"地火边缘{pos}", _scale);
            return dp;
        }

        private DrawPropertiesEdit GetExaflareWarn(Vector3 pos, int adv, int delay, int destroy, int interval)
        {
            var destroyItv = interval * (adv - 1);
            var color = _exaflareWarnColor.WithW(3f / 4f / adv);
            var dp = accessory.DrawStaticCircle(pos, color, delay, destroy + destroyItv, $"地火预警{pos}", _scale);
            return dp;
        }

        public void SetExaflareBasicAttributes(Vector3 spos, float scale, float rotation, int extendNum,
            float extendDistance, int advWarnNum, int castTime, int intervalTime)
        {
            SetStartPos(spos);
            SetScale(scale);
            SetRotation(rotation);
            SetExaflareExtension(extendNum, extendDistance);
            SetAdvanceWarnNum(advWarnNum);
            SetCastAndIntervalTime(castTime, intervalTime);
            BuildExaflareList();
        }

        public void SetCastAndIntervalTime(int castTime, int intervalTime)
        {
            _castTime = castTime;
            _intervalTime = intervalTime;
        }

        public void SetExaflareExtension(int extendNum, float extendDistance)
        {
            _extendNum = extendNum;
            _extendDistance = extendDistance;
        }

        public void SetRotation(float rotation)
        {
            _rotation = rotation;
        }

        public void SetAdvanceWarnNum(int advWarnNum)
        {
            _advWarnNum = advWarnNum;
        }

        public void SetScale(float scale)
        {
            _scale = scale;
        }

        public void SetStartPos(Vector3 pos)
        {
            _startPos = pos;
        }

        public void SetColor(Vector4 exaflareColor, Vector4 exaflareWarnColor)
        {
            _exaflareColor = exaflareColor;
            _exaflareWarnColor = exaflareWarnColor;
        }
    }

    [ScriptMethod(name: "般若汤击退与攻击范围", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(34(092|127))$"], userControl: true)]
    public void FightingSpiritsKnockBack(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var dp = accessory.DrawKnockBack(sid, 16, 1000, 5000, $"般若汤击退", 2f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);

        _manualEvents[3].WaitOne();
        for (var i = 0; i < 5; i++)
        {
            var delay = i == 0 ? 5000 : 9400 + (i - 1) * 3100;
            var destroy = i == 0 ? 4400 : 2500;

            if (i != 4)
            {
                var target = _superJumpId[i];
                var dp0 = accessory.DrawTarget2Target(target, sid, 20f, 120f, delay, destroy, $"超级跳{target}");
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp0);
                var dp1 = accessory.DrawTarget2Target(target, sid, 20f, 120f, delay, destroy, $"超级跳{target}",
                    90f.DegToRad());
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp1);
            }
            else
            {
                var dp2 = DrawJumpToMiddle(sid, 0, delay, destroy, accessory);
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp2);
                var dp3 = DrawJumpToMiddle(sid, 90f.DegToRad(), delay, destroy, accessory);
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp3);
            }
        }

        _manualEvents[3].Reset();
    }

    private DrawPropertiesEdit DrawJumpToMiddle(uint sid, float rotation, int delay, int destroy,
        ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = $"超级跳场中";
        dp.Scale = new Vector2(20f, 60f);
        dp.Position = _centerBoss2;
        dp.Rotation = rotation;
        dp.TargetObject = sid;
        dp.Color = accessory.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        return dp;
    }

    [ScriptMethod(name: "般若汤记录", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(015[0123])$"],
        userControl: false)]
    public void FightingSpiritsRecord(Event @event, ScriptAccessory accessory)
    {
        var tid = @event.TargetId();
        var id = @event.Id();
        var idx = id - 336;
        lock (_superJumpId)
        {
            _superJumpId[(int)idx] = tid;
            _superJumpCount++;
        }

        if (_superJumpCount == 4)
            _manualEvents[3].Set();
    }



    #endregion

    #region Boss3 捉鬼

    [ScriptMethod(name: "---- Boss3 怨灵猛虎 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["Hello World"],
        userControl: true)]
    public void SplitLine_Boss3(Event @event, ScriptAccessory accessory)
    {
    }

    private const string TripleKasumiAid =
        "ActionId:regex:^(342(2[456789]|3[01456789]|4[01]|7[6789]|8[01236789]|9[0123]))$";
    private const string TripleKasumiParam = "Param:regex:^(5(8[89]|9[012345]))$";
    private const string DarkKasumiParam = "Param:regex:^(58[4567])$";

    [ScriptMethod(name: "三段霞斩初始化", eventType: EventTypeEnum.StartCasting, eventCondition: [TripleKasumiAid],
        userControl: false)]
    public void TripleKasumiReset(Event @event, ScriptAccessory accessory)
    {
        lock (_multiKasumisList)
        {
            _multiKasumisList.Clear();
        }
        accessory.Log.Debug($"霞斩列表初始化");
        _manualEvents[4].Set();
    }
    
    [ScriptMethod(name: "三段霞斩", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:2970", TripleKasumiParam],
        userControl: true)]
    public void TripleKasumiGiriRecord(Event @event, ScriptAccessory accessory)
    {
        var param = @event.Param();
        var tid = @event.TargetId();
        var tpos = @event.TargetPosition();
        _manualEvents[4].WaitOne();
        lock (_multiKasumisList)
        {
            var kasumi = new Kasumi();
            kasumi.Init(accessory);
            kasumi.SetKasumiSource(tid);
            kasumi.SetKasumiCastPos(tpos);
            var kasumiCount = CountKasumiNumInList(tid);
            kasumi.SetKasumiNum(kasumiCount);
            if (kasumiCount != 0)
            {
                // 找到前一个kasumiCount，导出他的cleaveRotation，设置成新的sourceRotation
                var beforeKasumi = _multiKasumisList.FirstOrDefault(k =>
                    k.CheckKasumiSource(tid) && k.CheckKasumiCount(kasumiCount - 1));
                if (beforeKasumi != null)
                    kasumi.SetKasumiSourceRotation(beforeKasumi.GetCleaveRadian() + beforeKasumi.GetSourceRotation());
            }
            else
            {
                var chara = accessory.GetById(tid);
                if (chara != null)
                {
                    var rotation = chara.Rotation.Game2Logic();
                    kasumi.SetKasumiSourceRotation(rotation);
                    
                    if (_phase is AmrPhase.Boss3_ShadowTwin1 or AmrPhase.Boss3_ShadowTwin2)
                        // 第三次三段霞斩强行归位
                        kasumi.SetKasumiSourceRotation(0);
                }
            }
            kasumi.SetKasumiProperty(param);
            _multiKasumisList.Add(kasumi);
            
            var delay = kasumiCount switch
            {
                0 => 0,
                1 => 8500,
                2 => 8000,
                _ => 0
            };
            var destroy = kasumiCount switch
            {
                0 => 11500,
                1 => 2500,
                2 => 3000,
                _ => 0
            };
            kasumi.SetKasumiTime(delay, destroy);
            kasumi.DrawStatic();
            
            if (kasumiCount == 2)
                _manualEvents[4].Reset();
        }
    }

    private int CountKasumiNumInList(uint sid)
    {
        // 找到_multiKasumiList中，_source == sid的Kasumi个数
        var query = _multiKasumisList
            .Where(kasumi => kasumi.CheckKasumiSource(sid))
            .Select(kasumi => new {kasumi})
            .ToList();
        return query.Count;
    }
    
    public class Kasumi
    {
        public ScriptAccessory accessory { get; set; } = null!;
        private uint _param = 0;
        private float _safeRadian = 0f;
        private float _cleaveRadian = 0f;
        private float _sourceRotation = 0f;
        private Vector3 _castPos = new(0f, 0f, 0f);
        private int _chariotDonut = 0;
        private int _delay = 0;
        private int _destroy = 0;
        private uint _sourceId = 0;
        private uint _targetId = 0;
        private int _count = 0;
        private bool _offset = false;

        public void Init(ScriptAccessory _accessory)
        {
            accessory = _accessory;
            _param = 0;
            _safeRadian = 0f;
            _cleaveRadian = 0f;
            _chariotDonut = 0;
            _delay = 0;
            _destroy = 0;
            _castPos = new Vector3(0f, 0f, 0f);
            _sourceId = 0;
            _targetId = 0;
            _count = 0;
            _offset = false;
        }
        public void SetKasumiSource(uint sid)
        {
            _sourceId = sid;
        }
        
        public void SetKasumiTarget(uint tid)
        {
            _targetId = tid;
        }
        
        public void SetKasumiCastPos(Vector3 pos)
        {
            _castPos = pos;
        }
        
        public void SetKasumiNum(int count)
        {
            _count = count;
        }
        
        public void SetKasumiSourceRotation(float rot)
        {
            _sourceRotation = rot;
        }

        public bool CheckKasumiSource(uint sid)
        {
            return _sourceId == sid;
        }
        
        public bool CheckKasumiTarget(uint tid)
        {
            return _sourceId == tid;
        }
        
        public bool CheckKasumiCount(int count)
        {
            return _count == count;
        }

        public void DrawStatic()
        {
            var color = ColorHelper.ColorDark.V4.WithW(2.5f);
            var dp1 = GetCleaveDp(color);
            var dp2 = GetChariotDonutDp(color);
            accessory.Method.SendDraw(0, DrawTypeEnum.Fan, dp1);
            switch (_chariotDonut)
            {
                case 1:
                    accessory.Method.SendDraw(0, DrawTypeEnum.Circle, dp2);
                    break;
                case -1:
                    accessory.Method.SendDraw(0, DrawTypeEnum.Donut, dp2);
                    break;
                default:
                    break;
            }
        }

        public void DrawDynamic(bool bias)
        {
            var color = ColorHelper.ColorDark.V4.WithW(2.5f);
            var dp1 = GetCleaveDynamicDp(color, bias);
            accessory.Method.SendDraw(0, DrawTypeEnum.Fan, dp1);
        }
        
        public void SetKasumiProperty(uint param)
        {
            const uint front = 584;     // mod 4 = 0
            const uint right = 585;     // mod 4 = 1
            const uint back = 586;      // mod 4 = 2
            const uint left = 587;      // mod 4 = 3
            const uint outFront = 588;
            const uint outRight = 589;
            const uint outBack = 590;
            const uint outLeft = 591;
            const uint inFront = 592;
            const uint inRight = 593;
            const uint inBack = 594;
            const uint inLeft = 595;
            _param = param;
            _chariotDonut = param switch
            {
                <= 587 => 0,
                <= 591 => 1,
                _ => -1
            };
            var direction = param % 4;
            // SetCleaveRadian(direction * float.Pi / 2 + _sourceRotation);
            SetCleaveRadian(direction * 90f.DegToRad());
            var chariotDonutStr = _chariotDonut switch
            {
                1 => "钢铁",
                -1 => "月环",
                _ => ""
            };
            var directionStr = direction switch
            {
                0 => "上",
                1 => "右",
                2 => "下",
                _ => "左"
            };
            accessory.Log.Debug($"记录下{chariotDonutStr}面向{directionStr}刀");
        }

        public void SetKasumiTime(int delay, int destroy)
        {
            _delay = delay;
            _destroy = destroy;
        }

        public void TurnDynamicToStatic(bool bias)
        {
            var name = $"霞斩{_safeRadian}, {_cleaveRadian}";
            var chara = accessory.GetById(_targetId);
            var charaPos = chara.Position;
            charaPos = charaPos.ExtendPoint(chara.Rotation.Game2Logic(), bias ? -3f : -0.5f);
            SetKasumiCastPos(charaPos);
            SetKasumiSourceRotation(chara.Rotation.Game2Logic());
            SetKasumiProperty(_param);
            _offset = true;
        }
        
        public DrawPropertiesEdit GetCleaveDp(Vector4 color)
        {
            var name = $"霞斩{_safeRadian}, {_cleaveRadian}";
            var rot = _sourceRotation + _cleaveRadian;
            var dp = accessory.DrawStatic(_castPos, 0, 270f.DegToRad(), rot, 30f, 30f,
                color, _delay, _destroy, name);
            return dp;
        }
        
        public DrawPropertiesEdit GetCleaveDynamicDp(Vector4 color, bool bias)
        {
            var name = $"霞斩动态{_safeRadian}, {_cleaveRadian}";
            var rot = _sourceRotation.Logic2Game() + _cleaveRadian.Logic2Game();
            var dp = accessory.DrawFan(_targetId, 270f.DegToRad(), rot, 30f, 0, _delay, _destroy, name);
            dp.Color = color;
            dp.Offset = new(0, 0, bias ? 3f : 0.5f);
            return dp;
        }
        
        public DrawPropertiesEdit? GetChariotDonutDp(Vector4 color)
        {
            var name = $"霞斩钢月{_safeRadian}, {_cleaveRadian}";
            const int chariot = 1;
            const int donut = -1;
            DrawPropertiesEdit dp;
            switch (_chariotDonut)
            {
                case chariot:
                {
                    dp = accessory.DrawStaticCircle(_castPos, color, _delay, _destroy, name, 6f);
                    break;
                }
                case donut:
                {
                    dp = accessory.DrawStaticDonut(_castPos, color, _delay, _destroy, name, 40f, 6f);
                    break;
                }
                default:
                    return null;
            }
            return dp;
        }

        public float GetCleaveRadian()
        {
            return _cleaveRadian;
        }
        
        public float GetSourceRotation()
        {
            return _sourceRotation;
        }

        public float GetSafeRadian()
        {
            return _safeRadian;
        }

        public bool CheckParamed()
        {
            return _param != 0;
        }

        private void SetSafeRadian(float rad)
        {
            _safeRadian = rad;
            _cleaveRadian = (rad + float.Pi) % (2 * float.Pi);
        }
        
        private void SetCleaveRadian(float rad)
        {
            _cleaveRadian = rad;
            _safeRadian = (rad + float.Pi) % (2 * float.Pi);
        }
    }

    [ScriptMethod(name: "顺劈死刑", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(34275|34317)$"],
        userControl: true)]
    public void LateralSlice(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var tid = @event.TargetId();
        var dp = accessory.DrawTarget2Target(sid, tid, 60f, 60f, 0, 5000, $"顺劈");
        dp.Radian = 75f.DegToRad();
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }
    
    [ScriptMethod(name: "怨咒祈请Buff初始化", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(34267)$"],
        userControl: false)]
    public void InvocationVengeanceReset(Event @event, ScriptAccessory accessory)
    {
        _VengenceIsStackAndFirst = new bool[5].ToList(); // Boss3 怨咒祈请，分摊，先分摊记录
    }
    
    [ScriptMethod(name: "怨咒祈请Buff记录", eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:regex:^(361[01])$"],
        userControl: false)]
    public void InvocationVengeanceBuffRecord(Event @event, ScriptAccessory accessory)
    {
        // const uint spread = 3610;
        const uint stack = 3611;
        const uint buffLongThreshold = 20000;

        var tid = @event.TargetId();
        var tidx = accessory.GetPlayerIdIndex(tid);
        var stid = @event.StatusId();
        var dur = @event.DurationMilliseconds();

        if (stid == stack)
            _VengenceIsStackAndFirst[tidx] = true;
        if (dur > buffLongThreshold && stid == stack)
            _VengenceIsStackAndFirst[4] = true;
    }

    [ScriptMethod(name: "怨咒祈请分摊分散", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(361[01])$"],
        userControl: true)]
    public void InvocationVengeanceBuff(Event @event, ScriptAccessory accessory)
    {
        // const uint spread = 3610;
        const uint stack = 3611;

        var tid = @event.TargetId();
        var tidx = accessory.GetPlayerIdIndex(tid);
        var myIndex = accessory.GetMyIndex();
        var stid = @event.StatusId();
        var dur = @event.DurationMilliseconds();

        if (stid == stack)
        {
            var dp = accessory.DrawCircle(tid, 3, (int)dur - 5000, 5000, $"分摊");
            if (IsJobPartner(tidx, myIndex))
                dp.Color = accessory.Data.DefaultSafeColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        else
        {
            var dp = accessory.DrawCircle(tid, 3, (int)dur - 5000, 5000, $"分散");
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }
    
    [ScriptMethod(name: "剑气钢铁月环", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(34(257|304|260|307))$"],
        userControl: false)]
    public void ScarletAzureChariotDonutRecord(Event @event, ScriptAccessory accessory)
    {
        HashSet<uint> chariot = [34257, 34304];
        HashSet<uint> donut = [34260, 34307];

        var sid = @event.SourceId();
        var aid = @event.ActionId();
        
        if (chariot.Contains(aid))
        {
            var dp = accessory.DrawCircle(sid, 6f, 0, 5000, $"剑气钢铁");
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        if (donut.Contains(aid))
        {
            var dp = accessory.DrawDonut(sid, 40, 6, 0, 5000, $"剑气月环");
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
        }
    }
    
    [ScriptMethod(name: "赤帝空闪刃 阶段记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(34201)$"],
        userControl: false)]
    public void BoundlessScarletPhaseRecord(Event @event, ScriptAccessory accessory)
    {
        _phase = AmrPhase.Boss3_BoundlessScarlet;
        _multiKasumisList.Clear();
        accessory.Log.Debug($"当前阶段为：{_phase}");
    }
    
    [ScriptMethod(name: "赤帝空闪刃扩散", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(34(259|306))$"],
        userControl: true)]
    public void BoundlessScarletExplosion(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var dur = @event.DurationMilliseconds();
        var dp = accessory.DrawRect(sid, 30, 120f, 5000, 6500, $"火剑扩散{sid}");
        dp.Color = ColorHelper.ColorYellow.V4;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Straight, dp);
    }

    [ScriptMethod(name: "迅步居合霞斩引导", eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:2970", DarkKasumiParam],
        userControl: true)]
    public void DarkKasumiGiriRecord(Event @event, ScriptAccessory accessory)
    {
        if (_phase != AmrPhase.Boss3_BoundlessScarlet) return;
        var param = @event.Param();
        var tid = @event.TargetId();
        
        _manualEvents[5].WaitOne();
        lock (_multiKasumisList)
        {
            var kasumi = _multiKasumisList.FirstOrDefault(k =>
                k.CheckKasumiSource(tid));
            kasumi?.SetKasumiProperty(param);
            kasumi?.SetKasumiTime(0, 9000);
            kasumi?.DrawDynamic(true);
        }
        _manualEvents[5].Reset();
    }
    
    [ScriptMethod(name: "迅步居合霞斩记录", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0011"],
        userControl: false)]
    public void DarkKasumiSingleDynamic(Event @event, ScriptAccessory accessory)
    {
        if (_phase != AmrPhase.Boss3_BoundlessScarlet) return;
        var sid = @event.SourceId();
        var tid = @event.TargetId();
        lock (_multiKasumisList)
        {
            var kasumi = new Kasumi();
            kasumi.Init(accessory);
            kasumi.SetKasumiSource(sid);
            kasumi.SetKasumiTarget(tid);
            _multiKasumisList.Add(kasumi);
        }
        _manualEvents[5].Set();
    }
    
    [ScriptMethod(name: "迅步居合霞斩攻击范围", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(34243)$"],
        userControl: true)]
    public void DarkKasumiSingleStatic(Event @event, ScriptAccessory accessory)
    {
        if (_phase != AmrPhase.Boss3_BoundlessScarlet) return;
        var sid = @event.SourceId();
        lock (_multiKasumisList)
        {
            var kasumi = _multiKasumisList.FirstOrDefault(k =>
                k.CheckKasumiSource(sid));
            kasumi?.TurnDynamicToStatic(true);
            kasumi?.SetKasumiTime(0, 4000);
            kasumi?.DrawStatic();
            _multiKasumisList.Clear();
        }
    }
    
    [ScriptMethod(name: "召唤幻影 阶段记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(34247)$"],
        userControl: false)]
    public void ShadowTwinPhaseRecord(Event @event, ScriptAccessory accessory)
    {
        _phase = _phase switch
        {
            AmrPhase.Boss3_ShadowTwin1 => AmrPhase.Boss3_ShadowTwin2,
            _ => AmrPhase.Boss3_ShadowTwin1,
        };
        _multiKasumisList.Clear();
        _isTetherTarget = false;
        accessory.Log.Debug($"当前阶段为：{_phase}");
    }
    
    [ScriptMethod(name: "分身 连线居合霞斩记录", eventType: EventTypeEnum.Tether, eventCondition: ["Id:0011"],
        userControl: false)]
    public void DarkKasumiShadowTwinDynamic(Event @event, ScriptAccessory accessory)
    {
        if (_phase is not (AmrPhase.Boss3_ShadowTwin1 or AmrPhase.Boss3_ShadowTwin2)) return;
        var sid = @event.SourceId();
        var tid = @event.TargetId();
        lock (_multiKasumisList)
        {
            var kasumi0 = new Kasumi();
            kasumi0.Init(accessory);
            kasumi0.SetKasumiSource(sid);
            kasumi0.SetKasumiTarget(tid);
            kasumi0.SetKasumiNum(0);
            _multiKasumisList.Add(kasumi0);
            
            var kasumi1 = new Kasumi();
            kasumi1.Init(accessory);
            kasumi1.SetKasumiSource(sid);
            kasumi1.SetKasumiTarget(tid);
            kasumi0.SetKasumiNum(1);
            _multiKasumisList.Add(kasumi1);
        }
        _manualEvents[6].Set();
    }
    
    [ScriptMethod(name: "分身 迅步居合霞斩引导", eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:2970", DarkKasumiParam],
        userControl: true)]
    public void DarkKasumiGiriTwinRecord(Event @event, ScriptAccessory accessory)
    {
        if (_phase is not (AmrPhase.Boss3_ShadowTwin1 or AmrPhase.Boss3_ShadowTwin2)) return;
        var param = @event.Param();
        var tid = @event.TargetId();
        
        _manualEvents[6].WaitOne();
        lock (_multiKasumisList)
        {
            var kasumi = _multiKasumisList.FirstOrDefault(k =>
                k.CheckKasumiSource(tid) && k.CheckKasumiCount(0));
            if (kasumi == null) return;
            accessory.Log.Debug($"找到了{tid}的第一轮扇形记录。");

            if (kasumi.CheckParamed())
            {
                accessory.Log.Debug($"因为checkedParam现在是{tid}的第二轮扇形记录。");
                kasumi = _multiKasumisList.FirstOrDefault(k =>
                    k.CheckKasumiSource(tid) && k.CheckKasumiCount(1));
            }
            if (kasumi == null) return;

            kasumi.SetKasumiProperty(param);
            if (kasumi.CheckKasumiCount(0))
            {
                kasumi.SetKasumiTime(0, 12000);
                kasumi.DrawDynamic(false);
            }
            else
            {
                accessory.Log.Debug($"{tid}的第二轮赋值0与0，不画。");
                kasumi.SetKasumiTime(0, 0);
            }
        }
    }
    
    [ScriptMethod(name: "分身 迅步居合霞斩攻击范围", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(34249)$"],
        userControl: true)]
    public void DarkKasumiGiriTwinStatic(Event @event, ScriptAccessory accessory)
    {
        if (_phase is not (AmrPhase.Boss3_ShadowTwin1 or AmrPhase.Boss3_ShadowTwin2)) return;
        var sid = @event.SourceId();
        
        lock (_multiKasumisList)
        {
            accessory.Log.Debug($"你好，你在哪里{_multiKasumisList.Count}");
            var kasumi0 = _multiKasumisList.FirstOrDefault(k =>
                k.CheckKasumiSource(sid) && k.CheckKasumiCount(0));
            if (kasumi0 == null) return;
            accessory.Log.Debug($"正在处理第一步");
            kasumi0.TurnDynamicToStatic(false);
            kasumi0.SetKasumiTime(0, 2100);

            var kasumi1 = _multiKasumisList.FirstOrDefault(k =>
                k.CheckKasumiSource(sid) && k.CheckKasumiCount(1));
            if (kasumi1 == null) return;
            accessory.Log.Debug($"正在处理第二步，添加kasumi0的角度");
            kasumi1.SetKasumiSourceRotation(kasumi0.GetCleaveRadian() + kasumi0.GetSourceRotation());
            kasumi1.TurnDynamicToStatic(false);
            kasumi1.SetKasumiTime(2100, 5000);
            
            kasumi0.DrawStatic();
            kasumi1.DrawStatic();
        }
        _manualEvents[6].Reset();
    }
    
    [ScriptMethod(name: "一运 大手手位置记录", eventType: EventTypeEnum.PlayActionTimeline,
        eventCondition: ["Id:7747", "SourceDataId:regex:^(162(68|73))$"], userControl: false)]
    public void ShadowTwinHandDirRecord(Event @event, ScriptAccessory accessory)
    {
        if (_phase != AmrPhase.Boss3_ShadowTwin1) return;
        var spos = @event.SourcePosition();
        var dir = spos.Position2Dirs(_centerBoss3, 4);
        _isLRHand = dir % 2 == 1;
        accessory.Log.Debug($"大手手位置在{(_isLRHand ? "左右" : "上下")}");
    }
    
    [ScriptMethod(name: "一运 大手手绘图", eventType: EventTypeEnum.PlayActionTimeline,
        eventCondition: ["Id:7747", "SourceDataId:regex:^(162(68|73))$"], userControl: true)]
    public void ShadowTwinHandChariot(Event @event, ScriptAccessory accessory)
    {
        if (_phase != AmrPhase.Boss3_ShadowTwin1) return;
        var sid = @event.SourceId();
        var spos = @event.SourcePosition();
        var dir = spos.Position2Dirs(_centerBoss3, 4);
        var pos = _centerBoss3 with { Z = _centerBoss3.Z - 20 };
        pos = pos.RotatePoint(_centerBoss3, dir * 90f.DegToRad());
        var color = ColorHelper.ColorDark.V4.WithW(2f);
        var dp = accessory.DrawStatic(pos, 0, 360f.DegToRad(), 0, 22, 22, ColorHelper.ColorYellow.V4, 0, 13000, $"大手手{sid}");
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }
    
    [ScriptMethod(name: "一运 锁定目标记录", eventType: EventTypeEnum.StatusAdd,
        eventCondition: ["StatusID:regex:^(3609)$"], userControl: false)]
    public void ShadowTwinTargetRecord(Event @event, ScriptAccessory accessory)
    {
        if (_phase != AmrPhase.Boss3_ShadowTwin1) return;
        var tid = @event.TargetId();
        if (tid == accessory.Data.Me)
            _isTetherTarget = true;
    }
    
    [ScriptMethod(name: "一运 锁定目标初始化", eventType: EventTypeEnum.StatusRemove,
        eventCondition: ["StatusID:regex:^(3609)$"], userControl: false)]
    public void ShadowTwinTargetReset(Event @event, ScriptAccessory accessory)
    {
        if (_phase != AmrPhase.Boss3_ShadowTwin1) return;
        _isTetherTarget = false;
    }

    [ScriptMethod(name: "一运 远近就位提示", eventType: EventTypeEnum.StartCasting,
        eventCondition: ["ActionId:regex:^(3426[45])$"], userControl: true)]
    public void ShadowTwin1Solution(Event @event, ScriptAccessory accessory)
    {
        const uint near = 34265;
        var aid = @event.ActionId();
        var isNear = aid == near;
        if (_isTetherTarget)
            isNear = !isNear;
        accessory.Log.Debug($"玩家站位应当{(isNear ? "靠近" : "远离")}");
        
        var pos = _centerBoss3 with { Z = isNear ? -11.78f : -19.78f };
        var myIndex = accessory.GetMyIndex();
        if (myIndex >= 2)
            pos = pos.FoldPointVertical(_centerBoss3.Z);
        if (!_isLRHand)
            pos = pos.RotatePoint(_centerBoss3, -90f.DegToRad());

        var dpGuide = accessory.DrawGuidance(pos, 0, 6000, $"一运远近");
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dpGuide);
        var dp = accessory.DrawStaticCircle(pos, PosColorPlayer.V4, 0, 6000, $"一运远近");
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }
    
    [ScriptMethod(name: "二运 阴兵攻击范围", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(34(25[56]|30[23]))$"],
        userControl: true)]
    public void SoldiersIronStorm(Event @event, ScriptAccessory accessory)
    {
        var aid = @event.ActionId();
        var sid = @event.SourceId();
        var epos = @event.EffectPosition();

        HashSet<uint> ironRain = [34255, 34302];
        HashSet<uint> ironStorm = [34256, 34303];

        var scale = ironRain.Contains(aid) ? 10 : 20;
        var dp = accessory.DrawStaticCircle(epos, accessory.Data.DefaultDangerColor, 0, 20000, $"箭雨{sid}", scale);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }
    
    #endregion
   
    #region Mob1
    [ScriptMethod(name: "---- Trash 小怪 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["Hello World"], userControl: true)]
    public void SplitLine_Trash(Event @event, ScriptAccessory accessory) {}

    [ScriptMethod(name: "紫州雷犼", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^((343(87|89|90))|3440[567])$"])]
    public void Mob1_Raiko(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var aid = @event.ActionId();
        var tid = @event.TargetId();

        HashSet<uint> chariot = [34390, 34408];
        HashSet<uint> donut = [34389, 34407];
        HashSet<uint> charge = [34387, 34405];

        if (chariot.Contains(aid))
        {
            var dp = accessory.DrawCircle(sid, 10, 0, 4000, $"雷犼钢铁");
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        if (donut.Contains(aid))
        {
            var dp = accessory.DrawDonut(sid, 30, 5, 0, 4000, $"雷犼月环");
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
        }

        if (charge.Contains(aid))
        {
            var dp = accessory.DrawTarget2Target(sid, tid, 7, 5, 0, 4000, $"雷光冲锋", lengthByDistance: true);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
    }

    [ScriptMethod(name: "紫州风犼", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(34(39[234]|41[012]))$"])]
    public void Mob1_Fuko(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var aid = @event.ActionId();
        var tid = @event.TargetId();

        HashSet<uint> stack = [34392, 34410];
        HashSet<uint> knockBack = [34393, 34411];
        HashSet<uint> chariot = [34394, 34412];

        if (stack.Contains(aid))
        {
            var dp = accessory.DrawCircle(tid, 8, 0, 5000, $"风犼分摊");
            dp.Color = accessory.Data.DefaultSafeColor;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }

        if (knockBack.Contains(aid))
        {
            var dp = accessory.DrawKnockBack(sid, 25, 0, 4000, $"风犼击退");
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
        }

        if (chariot.Contains(aid))
        {
            var dp = accessory.DrawCircle(sid, 10, 0, 4000, $"风犼钢铁");
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }
    
    [ScriptMethod(name: "紫州幽鬼", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(344(3[78]|4[01]))$"])]
    public void Mob1_Yuki(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var aid = @event.ActionId();

        HashSet<uint> rightSwipe = [34437, 34440];
        HashSet<uint> leftSwipe = [34438, 34441];

        if (rightSwipe.Contains(aid))
        {
            var dp = accessory.DrawLeftRightCleave(sid, false, 0, 4000, $"幽鬼右刀");
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        if (leftSwipe.Contains(aid))
        {
            var dp = accessory.DrawLeftRightCleave(sid, true, 0, 4000, $"幽鬼右刀");
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }
    }
    #endregion
    
    #region Mob2
    
    [ScriptMethod(name: "紫州小天狗", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(34(39[678]|401|41[4569]))$"])]
    public void Mob2_Kotengu(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var aid = @event.ActionId();
        
        HashSet<uint> backCleave = [34396, 34414];
        HashSet<uint> leftCleave = [34397, 34415];
        HashSet<uint> rightCleave = [34398, 34416];
        HashSet<uint> gaze = [34401, 34419];
        
        // TODO 绘图摧毁时间待测试
        if (rightCleave.Contains(aid))
        {
            var dp0 = accessory.DrawFrontBackCleave(sid, true, 0, 4000, $"天狗前刀", 90f.DegToRad(), 50f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp0);
            var dp = accessory.DrawLeftRightCleave(sid, false, 0, 5000, $"天狗左刀", 90f.DegToRad());
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        if (leftCleave.Contains(aid))
        {
            var dp0 = accessory.DrawFrontBackCleave(sid, true, 0, 4000, $"天狗前刀", 90f.DegToRad(), 50f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp0);
            var dp = accessory.DrawLeftRightCleave(sid, true, 0, 5000, $"天狗右刀", 90f.DegToRad());
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }
        
        if (backCleave.Contains(aid))
        {
            var dp0 = accessory.DrawFrontBackCleave(sid, true, 0, 4000, $"天狗前刀", 90f.DegToRad(), 50f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp0);
            var dp = accessory.DrawFrontBackCleave(sid, false, 0, 5000, $"天狗后刀", 90f.DegToRad(), 50f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
        }

        if (gaze.Contains(aid))
        {
            var dp = accessory.DrawSightAvoid(sid, 0, 4000, $"天狗背对");
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.SightAvoid, dp);
        }
    }

    [ScriptMethod(name: "风元精直线", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(344(39|42))$"])]
    public void Mob2_WindElement(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var dp = accessory.DrawRect(sid, 8, 40, 0, 6000, $"风元精直线", true);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }
    
    [ScriptMethod(name: "紫州隐密头领", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(344(04|2[29]|30))$"])]
    public void Mob2_Onmitsugashira(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var aid = @event.ActionId();
        
        HashSet<uint> shuriken = [34404, 34422];
        HashSet<uint> shurikenFast = [34429, 34430];
        
        var destroy = shuriken.Contains(aid) ? 3000 : 1500;
        var dp = accessory.DrawRect(sid, 3, 40, 0, destroy, $"忍者手里剑");
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }
    
    #endregion

    #region General Functions

    /// <summary>
    /// 是否为职能搭档（T-H，D1-D2）
    /// </summary>
    /// <param name="idx1"></param>
    /// <param name="idx2"></param>
    /// <returns></returns>
    private bool IsJobPartner(int idx1, int idx2)
    {
        return (idx1 < 2 && idx2 < 2) || (idx1 >= 2 && idx2 >= 2);
    }
    
    /// <summary>
    /// 是否为同攻击范围搭档（T-D1，H-D2）
    /// </summary>
    /// <param name="idx1"></param>
    /// <param name="idx2"></param>
    /// <returns></returns>
    private bool IsRangePartner(int idx1, int idx2)
    {
        return idx1 + idx2 == 2 || idx1 + idx2 == 4 || idx1 == idx2;
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
}

public static class IbcHelper
{
    public static IGameObject? GetById(this ScriptAccessory sa, ulong id)
    {
        return sa.Data.Objects.SearchById(id);
    }
}

public static class DirectionCalc
{
    public static float DegToRad(this float deg) => (deg + 360f) % 360f / 180f * float.Pi;
    public static float RadToDeg(this float rad) => (rad + 2 * float.Pi) % (2 * float.Pi) / float.Pi * 180f;
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
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static DrawPropertiesEdit DrawGuidance(this ScriptAccessory accessory, 
        object ownerObj, object targetObj, int delay, int destroy, string name, float rotation = 0, float scale = 1f)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(scale);
        dp.Rotation = rotation;
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.Color = accessory.Data.DefaultSafeColor;
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
        object targetObj, int delay, int destroy, string name, float rotation = 0, float scale = 1f)
    {
        return targetObj switch
        {
            uint uintTarget => accessory.DrawGuidance(accessory.Data.Me, uintTarget, delay, destroy, name, rotation, scale),
            Vector3 vectorTarget => accessory.DrawGuidance(accessory.Data.Me, vectorTarget, delay, destroy, name, rotation, scale),
            _ => throw new ArgumentException("targetObj 的类型必须是 uint 或 Vector3")
        };
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
    public static DrawPropertiesEdit DrawOwnersEntityOrder(this ScriptAccessory accessory, uint ownerId, uint orderIdx, float width, float length, int delay, int destroy, string name, bool lengthByDistance = false, bool byTime = false)
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
    public static DrawPropertiesEdit DrawStaticCircle(this ScriptAccessory accessory, Vector3 center, Vector4 color, int delay, int destroy, string name, float scale = 1.5f)
    {
        var dp = accessory.DrawStatic(center, (uint)0, 0, 0, scale, scale, color, delay, destroy, name);
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
        var dp = accessory.DrawStatic(center, (uint)0, float.Pi * 2, 0, scale, scale, color, delay, destroy, name);
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
    
    public static DrawPropertiesEdit DrawKnockBack(this ScriptAccessory accessory, object target, float length, int delay, int destroy, string name, float width = 1.5f, bool byTime = false)
    {
        return target switch
        {
            uint uintTarget => accessory.DrawKnockBack(accessory.Data.Me, uintTarget, length, delay, destroy, name, width, byTime),
            Vector3 vectorTarget => accessory.DrawKnockBack(accessory.Data.Me, vectorTarget, length, delay, destroy, name, width, byTime),
            _ => throw new ArgumentException("target 的类型必须是 uint 或 Vector3")
        };
    }
    
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
    
    public static DrawPropertiesEdit DrawSightAvoid(this ScriptAccessory accessory, object target, int delay, int destroy, string name)
    {
        return target switch
        {
            uint uintTarget => accessory.DrawSightAvoid(accessory.Data.Me, uintTarget, delay, destroy, name),
            Vector3 vectorTarget => accessory.DrawSightAvoid(accessory.Data.Me, vectorTarget, delay, destroy, name),
            _ => throw new ArgumentException("target 的类型必须是 uint 或 Vector3")
        };
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
        switch (owner)
        {
            case uint sid:
                for (var i = 0; i < extendDirs.Count; i++)
                {
                    var dp = accessory.DrawRect(sid, width, length, delay, destroy, $"{name}{i}");
                    dp.Rotation = extendDirs[i];
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
            dpList[1].Add(dpPos);
            if (i == positions.Count - 1) break;
            var dpPrep = accessory.DrawGuidance(positions[i], positions[i + 1], delay[i], destroy[i], $"{name}prep{i}");
            dpList[2].Add(dpPos);
        }
        return dpList;
    }
}

#endregion
