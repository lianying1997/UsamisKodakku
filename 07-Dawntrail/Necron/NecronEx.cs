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
using System.Numerics;
using Newtonsoft.Json;
using System.Linq;
using System.ComponentModel;
using Dalamud.Utility.Numerics;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Vfx;
using Lumina.Excel.Sheets;

namespace UsamisKodakku.Scripts._07_DawnTrail.NecronEx;

[ScriptType(name: Name, territorys: [1296], guid: "1829f7d6-9e64-4cf7-9be4-e5d8a2e03d21",
    version: Version, author: "Usami", note: NoteStr, updateInfo: UpdateInfo)]

// ^(?!.*((武僧|机工士|龙骑士|武士|忍者|蝰蛇剑士|钐镰客|舞者|吟游诗人|占星术士|贤者|学者|(朝日|夕月)小仙女|炽天使|白魔法师|战士|骑士|暗黑骑士|绝枪战士|绘灵法师|黑魔法师|青魔法师|召唤师|宝石兽|亚灵神巴哈姆特|亚灵神不死鸟|迦楼罗之灵|泰坦之灵|伊弗利特之灵|后式自走人偶)|F\] (Used|Cast))).*35501.*$
// ^\[\w+\|[^|]+\|E\]\s\w+

public class NecronEx
{
    private const string
        Name = "NecronEx [永恒之暗悲惶歼灭战]",
        Version = "0.0.0.1",
        DebugVersion = "a";
    
    const string NoteStr =
        """
        v0.0.0.2
        初版，鸭门。
        """;
    
    const string UpdateInfo =
        """
        v0.0.0.2
        修复大十字踩塔和分散的问题。
        """;

    private const bool
        Debugging = false;

    private static readonly
        List<string> Role = ["MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4"];
    private static readonly
        Vector3 Center = new Vector3(100, 0, 100);
    
    private enum NecronPhase
    {
        Init,               // 初始
    }

    private volatile List<bool> _bools = new bool[20].ToList();      // 被记录flag
    private List<int> _numbers = Enumerable.Repeat(0, 8).ToList();
    private static List<ManualResetEvent> _events = Enumerable
        .Range(0, 20)
        .Select(_ => new ManualResetEvent(false))
        .ToList();
    private static bool _initHint = false;

    private static List<Vector3> _poses = Enumerable.Repeat(new Vector3(0, 0, 0), 8).ToList();
    private static List<uint> _aetherBlightRec = [];
    private static List<ulong> _aetherBlightSourceRec = [];
    private static List<Vector3> _markerPos = 
        [new Vector3(86.75f, 0, 94.28f), new Vector3(113.30f, 0, 94.35f),
         new Vector3(90.31f, 0, 97.01f), new Vector3(109.58f, 0, 97.37f),
         new Vector3(96.13f, 0, 85.74f), new Vector3(103.36f, 0, 85.92f),
         new Vector3(97.66f, 0, 89.95f), new Vector3(101.94f, 0, 89.98f)];

    private static int
        _castTime_FoD = 0,      // Fear of Death 死之恐惧
        _castTime_CG = 0,       // Cold Grip 暗之死腕
        _castTime_MmM = 0;      // Memento Mori

    private static bool
        _judging_FoD = false,
        _judging_CG = false,
        _judging_MmM = false,
        _judging_CoL = false;

    public void Init(ScriptAccessory sa)
    {
        RefreshParams();
        RefreshCastTimeParams();
        sa.Log.Debug($"脚本 {Name} v{Version}{DebugVersion} 完成初始化.");
        sa.Method.RemoveDraw(".*");
        _initHint = false;
    }
    
    private void RefreshParams()
    {
        _bools = new bool[20].ToList();
        _numbers = Enumerable.Repeat(0, 20).ToList();
        _events = Enumerable
            .Range(0, 20)
            .Select(_ => new ManualResetEvent(false))
            .ToList();
        
        _poses = Enumerable.Repeat(new Vector3(0, 0, 0), 8).ToList();
        _aetherBlightRec = [];
        _aetherBlightSourceRec = [];
    }
    
    private void RefreshCastTimeParams()
    {
        // 技能释放次数初始化
        _castTime_FoD = 0;
        _castTime_CG = 0;
        _castTime_MmM = 0;
        
        // 技能中初始化
        _judging_FoD = false;
        _judging_CG = false;
        _judging_MmM = false;
        _judging_CoL = false;
    }

    [ScriptMethod(name: "---- 测试项 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void SplitLine_Test(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "参数初始化", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: Debugging)]
    public void 参数初始化(Event ev, ScriptAccessory sa)
    {
        RefreshParams();
    }
    
    [ScriptMethod(name: "初始提示", eventType: EventTypeEnum.Chat, eventCondition: ["Message:regex:^(肉体无法摆脱死亡的束缚。\n这就是你们的局限性。)$"],
        userControl: true)]
    public void 初始提示(Event ev, ScriptAccessory sa)
    {
        if (_initHint) return;
        _initHint = true;
        var myIndex = sa.GetMyIndex(); 
        List<string> role = ["MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4"];
        sa.Method.TextInfo(
            $"你是【{role[myIndex]}】，" +
            $"若有误请及时调整。", 5000);
    }
    
    [ScriptMethod(name: "---- 青之冲击 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void 青之冲击分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "死刑青之冲击", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44592)$"],
        userControl: true)]
    public void 青之冲击绘图(Event ev, ScriptAccessory sa)
    {
        var dp = sa.DrawFan(ev.SourceId, 0, 10000, $"青之冲击", 100f.DegToRad(), 0, 100, 0, draw: false);
        dp.SetOwnersEnmityOrder(1);
        dp.Color = new Vector4(1, 0, 0, 1);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Fan, dp);
        sa.Log.Debug($"绘图 青之冲击");
    }
    
    [ScriptMethod(name: "青之冲击判定", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(44593)$", "TargetIndex:1"],
        userControl: Debugging)]
    public void 青之冲击判定(Event ev, ScriptAccessory sa)
    {
        _numbers[0]++;
        sa.Log.Debug($"青之冲击计数 {_numbers[0]}");
        if (_numbers[0] % 2 != 0) return;
        sa.Method.RemoveDraw($"青之冲击");
        sa.Log.Debug($"删除青之冲击绘图，计数初始化");
        _numbers[0] = 0;
    }
    
    #region 死之恐惧 Fear of Death
    
    [ScriptMethod(name: "---- 死之恐惧 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void 死之恐惧分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "死之恐惧", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44550)$"],
        userControl: Debugging)]
    public void 死之恐惧(Event ev, ScriptAccessory sa)
    {
        _castTime_FoD++;
        sa.Log.Debug($"读条死之恐惧 #{_castTime_FoD}");
        _judging_FoD = true;
        
        _numbers[1] = 0;
        _bools[0] = true;
        sa.Log.Debug($"死之恐惧 #{_castTime_FoD} 添加事件 _events[0]，直至记录完毕手与指路");
        _events[0].Set();
        
        // switch (_castTime_FoD)
        // {
        //     case 1:
        //     case 2:
        //     case 3:
        //         _numbers[1] = 0;
        //         _bools[0] = true;
        //         sa.Log.Debug($"死之恐惧 #{_castTime_FoD} 添加事件 _events[0]，直至记录完毕手与指路");
        //         _events[0].Set();
        //         break;
        //     default:
        //         break;
        // }
    }
    
    [ScriptMethod(name: "死之恐惧出手位置", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(18700)$"],
        userControl: true)]
    public void 死之恐惧出手位置(Event ev, ScriptAccessory sa)
    {
        if (!_judging_FoD) return;
        _events[0].WaitOne(10000);
        if (!_bools[0]) return;

        lock (_numbers)
        {
            // 记录位置
            _poses[_numbers[1]] = ev.SourcePosition;
            _numbers[1]++;
            sa.Log.Debug($"记录下 手 #{_numbers[1]} 的泥坑位置 {ev.SourcePosition}");
        }
        
        // 记录全后，排序
        if (_numbers[1] != 8) return;
        _poses.Sort((a, b) => {
            // 先从左到右升序，后从上到下升序。
            int z = a.X.CompareTo(b.X);
            return z != 0 ? z : a.Z.CompareTo(b.Z);
        });

        // MT H1 D1 D3 D2 D4 ST H2
        List<int> playerIdx = [0, 2, 4, 6, 5, 7, 1, 3];
        
        // 绘图与指路
        for (int i = 0; i < 8; i++)
        {
            sa.DrawCircle(_poses[i], 0, 20000, $"泥坑{i}", 3f);
            if (sa.GetMyIndex() != playerIdx[i]) continue;
            var downSide = i % 2 == 1;
            sa.DrawGuidance(_poses[i] + new Vector3(0, 0, downSide ? 4 : -4), 0, 20000, $"泥坑指路{i}");
            sa.Log.Debug($"指路向泥坑 #{i}（从左至右、从上至下），泥坑在{(downSide ? "下" : "上")}");
        }

    }
    
    [ScriptMethod(name: "死之恐惧泥坑绘图删除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(44551)$"],
        userControl: Debugging, suppress: 10000)]
    public void 死之恐惧泥坑绘图删除(Event ev, ScriptAccessory sa)
    {
        if (!_judging_FoD) return;
        _events[0].Reset();
        sa.Log.Debug($"死之恐惧泥坑判定，绘图删除，释放锁");
        sa.Method.RemoveDraw($"泥坑.*");
    }
    
    [ScriptMethod(name: "死之恐惧压溃绘图", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(44551)$"],
        userControl: true, suppress: 10000)]
    public void 死之恐惧压溃绘图(Event ev, ScriptAccessory sa)
    {
        if (!_judging_FoD) return;
        if (!_bools[0]) return;
        sa.Log.Debug($"进行引导压溃绘图");
        List<int> playerIdx = [0, 2, 4, 6, 5, 7, 1, 3];
        for (int i = 0; i < 8; i++)
        {
            var isMyIdx = sa.GetMyIndex() == playerIdx[i];
            var dp = sa.DrawRect(_poses[i], 0, 20000, $"引导压溃{i}",
                0, 6, 24, isMyIdx, draw: false);
            dp.SetPositionDistanceOrder(true, 1);
            dp.Color = isMyIdx ? sa.Data.DefaultSafeColor : new Vector4(0, 0, 0, 2f);
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
    }
    
    [ScriptMethod(name: "死之恐惧压溃绘图删除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(44552)$"],
        userControl: Debugging, suppress: 10000)]
    public void 死之恐惧压溃绘图删除(Event ev, ScriptAccessory sa)
    {
        if (!_judging_FoD) return;
        sa.Log.Debug($"死之恐惧压溃判定，绘图删除");
        sa.Method.RemoveDraw($"引导压溃.*");
        _judging_FoD = false;
    }
    
    #endregion 死之恐惧 Fear of Death
    
    #region 暗之死腕 Cold Grip
    
    [ScriptMethod(name: "---- 暗之死腕 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void 暗之死腕分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "暗之死腕", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4455[34])$"],
        userControl: Debugging)]
    public void 暗之死腕(Event ev, ScriptAccessory sa)
    {
        _castTime_CG++;
        sa.Log.Debug($"读条暗之死腕 #{_castTime_CG}");
        _judging_CG = true;
        
        _bools[1] = true;
        _bools[2] = ev.ActionId == 44553;   // 左安全
        sa.Log.Debug($"暗之死腕 #{_castTime_CG} 添加事件 _events[1]，直至完成一段绘图");
        _events[1].Set();
        
        // switch (_castTime_CG)
        // {
        //     case 1:
        //     case 2:
        //     case 3:
        //         _bools[1] = true;
        //         _bools[2] = ev.ActionId == 44553;   // 左安全
        //         sa.Log.Debug($"暗之死腕 #{_castTime_CG} 添加事件 _events[1]，直至完成一段绘图");
        //         _events[1].Set();
        //         break;
        //     default:
        //         break;
        // }
    }
    
    [ScriptMethod(name: "暗之死腕一段绘图", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4455[34])$"],
        userControl: true)]
    public void 暗之死腕一段绘图(Event ev, ScriptAccessory sa)
    {
        _events[1].WaitOne(10000);
        if (!_bools[1]) return;
        if (!_judging_CG) return;

        var isLeftSafe = _bools[2];
        sa.Log.Debug($"暗之死腕 {(isLeftSafe ? "左" : "右")}安全，进行一段绘图");

        sa.DrawRect(new Vector3(88, 0, 85), 0, 6000, $"暗之死腕一段", 0, 12, 100);
        sa.DrawRect(new Vector3(112, 0, 85), 0, 6000, $"暗之死腕一段", 0, 12, 100);

        // 指引线
        for (int i = 0; i < 5; i++)
        {
            var dp = sa.DrawLine(new Vector3(isLeftSafe ? 95 : 105, 0, 90 + i * 5),
                new Vector3(isLeftSafe ? 93 : 107, 0, 90 + i * 5),
                0, 20000, $"暗之死腕指引线", 0, 1f, 2f, true, draw: false);
            sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dp);
        }
        
    }
    
    [ScriptMethod(name: "暗之死腕二段绘图", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(44612)$"],
        userControl: true, suppress: 1000)]
    public void 暗之死腕二段绘图(Event ev, ScriptAccessory sa)
    {
        if (!_judging_CG) return;
        if (!_bools[1]) return;
        var isLeftSafe = _bools[2];
        sa.Log.Debug($"暗之死腕 {(isLeftSafe ? "左" : "右")}安全，一段判定，进行二段绘图");
        sa.Method.RemoveDraw($"暗之死腕一段.*");
        sa.DrawRect(new Vector3(isLeftSafe ? 106 : 94, 0, 85), 0, 6000, $"暗之死腕二段", 0, 24, 100);
    }
    
    [ScriptMethod(name: "暗之死腕绘图删除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(44555)$"],
        userControl: Debugging)]
    public void 暗之死腕绘图删除(Event ev, ScriptAccessory sa)
    {
        if (!_judging_CG) return;
        _events[1].Reset();
        sa.Log.Debug($"暗之死腕二段判定，绘图删除，释放锁_events[1]");
        sa.Method.RemoveDraw($"暗之死腕.*");
        _judging_CG = false;
    }
    
    #endregion 暗之死腕 Cold Grip
    
    #region 死亡警告 Memento Mori
    
    [ScriptMethod(name: "---- 死亡警告 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void 死亡警告分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "死亡警告", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4456[56])$"],
        userControl: Debugging)]
    public void 死亡警告(Event ev, ScriptAccessory sa)
    {
        _castTime_MmM++;
        sa.Log.Debug($"读条死亡警告 #{_castTime_MmM}");
        _judging_MmM = true;
        
        _bools[2] = true;
        _numbers[2] = 0;    // 安全行
        _numbers[6] = 0;    // 手计数
        sa.Log.Debug($"死亡警告 #{_castTime_MmM} 添加事件 _events[2]，直至确定安全行");
        _events[2].Set();
        
        // switch (_castTime_MmM)
        // {
        //     case 1: 
        //     case 2: 
        //     case 3:
        //         _bools[2] = true;
        //         _numbers[2] = 0;    // 安全行
        //         _numbers[6] = 0;    // 手计数
        //         sa.Log.Debug($"死亡警告 #{_castTime_MmM} 添加事件 _events[2]，直至确定安全行");
        //         _events[2].Set();
        //         break;
        //     default:
        //         break;
        // }
    }

    [ScriptMethod(name: "死亡警告中直线绘图", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4456[56])$"],
        userControl: true)]
    public void 死亡警告中直线绘图(Event ev, ScriptAccessory sa)
    {
        _events[2].WaitOne(10000);
        if (!_bools[2]) return;
        if (!_judging_MmM) return;
        sa.DrawRect(ev.SourceId, 0, 20000, $"死亡警告中线", 0, 12, 100);
        _bools[3] = ev.ActionId == 44565;   // 44565 左少右多
        var isLeftLess = _bools[3];
        sa.Log.Debug($"死亡警告 {(isLeftLess ? "左" : "右")}少，进行绘图");
    }
    
    [ScriptMethod(name: "死亡警告手绘图", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(18700)$"],
        userControl: true)]
    public void 死亡警告与手绘图(Event ev, ScriptAccessory sa)
    {
        if (!_judging_MmM) return;
        _events[2].WaitOne(10000);
        if (!_bools[2]) return;

        lock (_numbers)
        {
            _numbers[6]++;
            sa.DrawRect(ev.SourceId, 0, 20000, $"死亡警告手", 0, 6, 24);
            var isLeftLess = _bools[3];
            // 左少，则找面向左，rotation为270（-90）；右少则找面向右，rotation为90
            var rotation = ev.SourceRotation.RadToDeg();
            sa.Log.Debug($"找到 rotation 为 {rotation} 的手 #{_numbers[6]} {ev.SourcePosition} ");

            if ((isLeftLess && rotation > 180f) || (!isLeftLess && rotation < 180f))
            {
                // Z轴为88 94 100 106 112，可通过(pos.z - 87) / 6 得到所在安全行并保存
                _numbers[2] = (int)Math.Floor((ev.SourcePosition.Z - 87) / 6);
                sa.Log.Debug($"死亡警告 {_castTime_MmM} 得到安全行 {_numbers[2]}");
            }

            if (_numbers[6] != 5) return;
            sa.Log.Debug($"死亡警告 {_castTime_MmM} 捕捉手完毕，释放锁 _events[2]");
            _numbers[6] = 0;
            _events[2].Reset();
            
            sa.Log.Debug($"死亡警告 {_castTime_MmM} 添加事件 _events[3]，直至完成绘图指路");
            _events[3].Set();
        }
    }

    [ScriptMethod(name: "死亡警告指路（请保证上一项开启）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4456[56])$"],
        userControl: true)]
    public void 死亡警告指路(Event ev, ScriptAccessory sa)
    {
        _events[3].WaitOne(10000);
        if (!_bools[2])
        {
            _events[3].Reset();
            return;
        }
        if (!_judging_MmM) return;
        
        var safeRow = _numbers[2];
        var isLeftLess = _bools[3];
        var reverseBias = isLeftLess ? 0 : 1;
        
        // 若为第二次死亡警告，忽略
        if (_castTime_MmM == 2)
        {
            sa.Log.Debug($"死亡警告 {_castTime_MmM} 忽略，释放锁 _events[3]");
            _events[3].Reset();
            return;
        }
        
        // 死亡警告的安全行不可能在第一行与最后一行，因此可遵循以下指路规则
        // D1、D2，始终在第一行；D3、D4，始终在最后一行；MT、ST，始终在安全行偏上；H1、H2，始终在安全行偏下
        // 以左少基准进行计算，若相反则平移偏置
        var safePos = sa.GetMyIndex() switch
        {
            4 => new Vector3(82.5f, 0, 85.5f) + new Vector3(24f * reverseBias, 0, 0),
            5 => new Vector3(93.5f, 0, 85.5f) + new Vector3(24f * reverseBias, 0, 0),
            6 => new Vector3(82.5f, 0, 114.5f) + new Vector3(24f * reverseBias, 0, 0),
            7 => new Vector3(93.5f, 0, 114.5f) + new Vector3(24f * reverseBias, 0, 0),
            0 => new Vector3(106.5f, 0, 85.5f + safeRow * 6) + new Vector3(-24f * reverseBias, 0, 0),
            1 => new Vector3(117.5f, 0, 85.5f + safeRow * 6) + new Vector3(-24f * reverseBias, 0, 0),
            2 => new Vector3(82.5f, 0, 85.5f + (safeRow + 1) * 6) + new Vector3(24f * reverseBias, 0, 0),
            3 => new Vector3(93.5f, 0, 85.5f + (safeRow + 1) * 6) + new Vector3(24f * reverseBias, 0, 0),
            _ => new Vector3(100f, 0, 100f),
        };
        sa.DrawGuidance(safePos, 0, 20000, $"死亡警告指路");
        sa.Log.Debug($"死亡警告 {_castTime_MmM} 指路完毕，释放锁 _events[3]");
        
        _events[3].Reset();
    }
    
    [ScriptMethod(name: "死亡警告绘图删除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(44567)$"],
        userControl: Debugging, suppress: 1000)]
    public void 死亡警告绘图删除(Event ev, ScriptAccessory sa)
    {
        if (!_judging_MmM) return;
        sa.Log.Debug($"死亡警告 {_castTime_MmM} 手 压溃 判定，绘图删除");
        sa.Method.RemoveDraw($"死亡警告.*");
        _judging_MmM = false;
    }
    
    #endregion 死亡警告 Memento Mori

    #region 青魂 Aether Blight

    [ScriptMethod(name: "---- 青魂 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void 青魂分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "青魂头标记录", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(025[CDEF])$"],
        userControl: Debugging)]
    public void 青魂头标记录(Event ev, ScriptAccessory sa)
    {
        var id = ev.Id0();
        _aetherBlightRec.Add(id);
        sa.Log.Debug($"青魂 {id} 记录(#{_aetherBlightRec.Count})，604 钢铁、605 月环、606 打两侧、607 打中间");
    }
    
    [ScriptMethod(name: "青之波潮连线记录", eventType: EventTypeEnum.Tether, eventCondition: ["Id:regex:^(015[BCDE])$"],
        userControl: Debugging)]
    public void 青之波潮连线记录(Event ev, ScriptAccessory sa)
    {
        _aetherBlightSourceRec.Add(ev.SourceId);
        sa.Log.Debug($"检测到青之波潮连线 #{_aetherBlightSourceRec.Count}，连线 {ev.SourceId}，位于 {ev.SourcePosition}");
    }
    
    [ScriptMethod(name: "青之多重波", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4455[78]|4516[78])$"],
        userControl: Debugging)]
    public void 青之多重波(Event ev, ScriptAccessory sa)
    {
        // 44557, 45167 二重，四四分摊
        // 44558, 45168 四重，二二分摊
        // 起始序列，默认为0，用于Season的转置
        
        _numbers[4] = ev.ActionId is 44557 or 44558 ? 1 : 4;    // 绘图总次数
        _bools[8] = ev.ActionId is 45167 or 45168;  // 是青之波潮
        _bools[5] = ev.ActionId is 44558 or 45168;  // 是二二分摊

        _bools[4] = true;
        sa.Log.Debug($"青之多重{(_bools[8] ? "波潮" : "波")}准备绘图，共绘图 {_numbers[4]} 次");
        _events[4].Set();
        sa.Log.Debug($"青之多重{(_bools[8] ? "波潮" : "波")} 添加事件 _events[4]");
    }
    
    [ScriptMethod(name: "青之多重波分摊绘图", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4455[78])$"],
        userControl: true)]
    public void 青之多重波分摊绘图(Event ev, ScriptAccessory sa)
    {
        _events[4].WaitOne(10000);
        if (!_bools[4]) return;
        var isPartnerStack = _bools[5];
        List<int> partnerJudge = [0, 11, 2, 13, 0, 11, 2, 13];
        
        // 青之多重波，对目标分摊绘图（此处选择TH）
        var myPartIdx = partnerJudge[sa.GetMyIndex()];
        sa.DrawFan(ev.SourceId, sa.Data.PartyList[2], 0, 20000, $"青之多重波分摊 H1",
            (isPartnerStack ? 20f : 30f).DegToRad(), 0, 100, 0, isPartnerStack ? myPartIdx == partnerJudge[2] : myPartIdx < 10);
        sa.DrawFan(ev.SourceId, sa.Data.PartyList[3], 0, 20000, $"青之多重波分摊 H2",
            (isPartnerStack ? 20f : 30f).DegToRad(), 0, 100, 0, isPartnerStack ? myPartIdx == partnerJudge[3] : myPartIdx > 10);

        if (isPartnerStack)
        {
            sa.DrawFan(ev.SourceId, sa.Data.PartyList[0], 0, 20000, $"青之多重波分摊 MT",
                (isPartnerStack ? 20f : 30f).DegToRad(), 0, 100, 0, myPartIdx == partnerJudge[0]);
            sa.DrawFan(ev.SourceId, sa.Data.PartyList[1], 0, 20000, $"青之多重波分摊 ST",
                (isPartnerStack ? 20f : 30f).DegToRad(), 0, 100, 0, myPartIdx == partnerJudge[1]);
        }
        sa.Log.Debug($"青之 {(isPartnerStack ? "四" : "二")} 重波范围绘图完毕，释放锁");
    }
    
    [ScriptMethod(name: "青之多重波场地绘图", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4455[78])$"],
        userControl: true)]
    public void 青之多重波场地绘图(Event ev, ScriptAccessory sa)
    {
        _events[4].WaitOne(10000);
        if (!_bools[4]) return;

        var drawTotalCount = _numbers[4];
        if (drawTotalCount != _aetherBlightRec.Count)
        {
            sa.Log.Error($"绘图总次数 {drawTotalCount} 与记录List Count {_aetherBlightRec.Count} 不符，停止绘图。");
            return;
        }
        
        // 604 钢铁、605 月环、606 打两侧、607 打中间
        switch (_aetherBlightRec[0])
        {
            case 606:
                sa.DrawRect(new Vector3(88, 0, 85), 0, 5500, $"青之多重波打两侧", 0, 12, 100);
                sa.DrawRect(new Vector3(112, 0, 85), 0, 5500, $"青之多重波打两侧", 0, 12, 100);
                sa.Log.Debug($"青之多重波 打两侧 绘图完毕");
                break;
            
            case 607:
                sa.DrawRect(new Vector3(100, 0, 78), 0, 5500, $"青之多重波打中间", 0, 12, 100);
                sa.Log.Debug($"青之多重波 打中间 绘图完毕");
                break;
            
            case 604:
                sa.DrawCircle(new Vector3(100, 0, 78), 0, 5500, $"青之多重波钢铁", 20);
                sa.Log.Debug($"青之多重波 钢铁 绘图完毕");
                break;
            
            case 605:
                sa.DrawDonut(new Vector3(100, 0, 78), 0, 5500, $"青之多重波月环", 60, 16);
                sa.Log.Debug($"青之多重波 月环 绘图完毕");
                break;
        }
        
        sa.Log.Debug($"青之多重波绘图完毕");
    }
    
    [ScriptMethod(name: "青之多重波指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4455[78])$"],
        userControl: true)]
    public void 青之多重波指路(Event ev, ScriptAccessory sa)
    {
        _events[4].WaitOne(10000);
        if (!_bools[4]) return;
        
        var isPartnerStack = _bools[5];
        
        // 青之多重波，指路
        var myIndex = sa.GetMyIndex();
        var isInsideSafe = _aetherBlightRec[0] is 605 or 606;

        var myPosIdx = myIndex % 4 + (isPartnerStack ? 0 : 2) + (isInsideSafe ? 4 : 0);
        sa.DrawGuidance(_markerPos[myPosIdx], 0, 20000, $"青之多重波指路");
        
        sa.Log.Debug($"青之 {(isPartnerStack ? "四" : "二")} 指路 {_markerPos[myPosIdx]} 绘图完毕，释放锁");
    }
    
    [ScriptMethod(name: "青之多重波绘图删除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(4455[78])$"],
        userControl: Debugging)]
    public void 青之多重波绘图删除(Event ev, ScriptAccessory sa)
    {
        _aetherBlightRec = [];
        _aetherBlightSourceRec = [];
        _events[4].Reset();
        sa.Log.Debug($"青之多重波判定，清空矩阵，绘图删除，释放锁_events[4]");
        sa.Method.RemoveDraw($"青之多重波.*");
    }
    
    [ScriptMethod(name: "青之多重波潮判断", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4516[78])$"],
        userControl: Debugging)]
    public void 青之多重波潮判断(Event ev, ScriptAccessory sa)
    {
        _events[4].WaitOne(10000);
        if (!_bools[4]) return;
        int startIdx = 0;
        float lowestY = 30f;
        // 获得startIdx
        for (int i = 0; i < _aetherBlightSourceRec.Count; i++)
        {
            // 找到Y最低的单位
            var obj = sa.GetById(_aetherBlightSourceRec[i]);
            if (obj is null) continue;
            if (obj.Position.Y >= lowestY) continue;
            lowestY = obj.Position.Y;
            startIdx = i;
        }
        _numbers[3] = startIdx;
        _numbers[5] = 0;
        sa.Log.Debug($"经遍历，最低单位index为 {startIdx}，id为 {_aetherBlightSourceRec[startIdx]}，高度为 {lowestY}");
        _events[5].Set();
    }
    
    [ScriptMethod(name: "青之多重波潮场地绘图", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4516[78])$"],
        userControl: true)]
    public void 青之多重波潮场地绘图(Event ev, ScriptAccessory sa)
    {
        _events[5].WaitOne(10000);
        if (!_bools[4]) return;
        _bools[6] = true;
        
        var drawTotalCount = _numbers[4];
        if (drawTotalCount != _aetherBlightRec.Count)
        {
            sa.Log.Error($"绘图总次数 {drawTotalCount} 与记录List Count {_aetherBlightRec.Count} 不符，停止绘图。");
            _bools[6] = false;
            return;
        }
        
        var startIdx = _numbers[3];
        // 604 钢铁、605 月环、606 打两侧、607 打中间
        switch (_aetherBlightRec[startIdx])
        {
            case 606:
                sa.DrawRect(new Vector3(88, 0, 85), 0, 15000, $"青之波潮0 打两侧", 0, 12, 100);
                sa.DrawRect(new Vector3(112, 0, 85), 0, 15000, $"青之波潮0 打两侧", 0, 12, 100);
                sa.Log.Debug($"青之波潮0 打两侧 绘图完毕");
                break;
            
            case 607:
                sa.DrawRect(new Vector3(100, 0, 78), 0, 15000, $"青之波潮0 打中间", 0, 12, 100);
                sa.Log.Debug($"青之波潮0 打中间 绘图完毕");
                break;
            
            case 604:
                sa.DrawCircle(new Vector3(100, 0, 78), 0, 15000, $"青之波潮0 钢铁", 20);
                sa.Log.Debug($"青之波潮0 钢铁 绘图完毕");
                break;
            
            case 605:
                sa.DrawDonut(new Vector3(100, 0, 78), 0, 15000, $"青之波潮0 月环", 60, 16);
                sa.Log.Debug($"青之波潮0 月环 绘图完毕");
                break;
        }
    }

    [ScriptMethod(name: "青之多重波潮计数器", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(4518[345]|44608)$", "TargetIndex:1"],
        userControl: Debugging, suppress: 1000)]
    public void 青之多重波潮计数器(Event ev, ScriptAccessory sa)
    {
        _events[4].WaitOne(10000);
        if (!_bools[8]) return;
        _numbers[5]++;
        sa.Log.Debug($"青之波潮计数器增加 至 #{_numbers[5]}，添加锁 _events[6] ");
        _events[6].Set();
    }
    
    [ScriptMethod(name: "青之多重波潮计数器解锁", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(4518[345]|44608)$", "TargetIndex:1"],
        userControl: Debugging, suppress: 1000)]
    public async void 青之多重波潮计数器解锁(Event ev, ScriptAccessory sa)
    {
        _events[4].WaitOne(10000);
        if (!_bools[8]) return;
        _events[6].WaitOne(500);
        
        await Task.Delay(500);
        sa.Log.Debug($"青之多重波潮计数器 _events[6] 解锁");
        _events[6].Reset();
    }

    [ScriptMethod(name: "青之多重波潮场地绘图后续", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(4518[345]|44608)$", "TargetIndex:1"],
        userControl: Debugging, suppress: 1000)]
    public void 青之多重波潮场地绘图后续(Event ev, ScriptAccessory sa)
    {
        if (!_bools[6]) return;
        _events[6].WaitOne(10000);
        var drawCount = _numbers[5];
        var drawTotalCount = _numbers[4];
        if (drawTotalCount != _aetherBlightRec.Count)
        {
            sa.Log.Error($"绘图总次数 {drawTotalCount} 与记录List Count {_aetherBlightRec.Count} 不符，停止绘图。");
            return;
        }
        sa.Log.Debug($"删除青之波潮{drawCount-1} 绘图");
        sa.Method.RemoveDraw($"青之波潮{drawCount-1}.*");
        if (drawCount >= drawTotalCount) return;
        
        var startIdx = _numbers[3];
        var drawIdx = (startIdx + drawCount) % drawTotalCount;
        
        // 604 钢铁、605 月环、606 打两侧、607 打中间
        switch (_aetherBlightRec[drawIdx])
        {
            case 606:
                sa.DrawRect(new Vector3(88, 0, 85), 0, 15000, $"青之波潮{drawCount} 打两侧", 0, 12, 100);
                sa.DrawRect(new Vector3(112, 0, 85), 0, 15000, $"青之波潮{drawCount} 打两侧", 0, 12, 100);
                sa.Log.Debug($"青之波潮{drawCount} 打两侧 绘图完毕");
                break;
            
            case 607:
                sa.DrawRect(new Vector3(100, 0, 78), 0, 15000, $"青之波潮{drawCount} 打中间", 0, 12, 100);
                sa.Log.Debug($"青之波潮{drawCount} 打中间 绘图完毕");
                break;
            
            case 604:
                sa.DrawCircle(new Vector3(100, 0, 78), 0, 15000, $"青之波潮{drawCount} 钢铁", 20);
                sa.Log.Debug($"青之波潮{drawCount} 钢铁 绘图完毕");
                break;
            
            case 605:
                sa.DrawDonut(new Vector3(100, 0, 78), 0, 15000, $"青之波潮{drawCount} 月环", 60, 16);
                sa.Log.Debug($"青之波潮{drawCount} 月环 绘图完毕");
                break;
        }
    }
    
    [ScriptMethod(name: "青之多重波潮分摊绘图", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(4518[345]|44608)$", "TargetIndex:1"],
        userControl: true, suppress: 1000)]
    public void 青之多重波潮分摊绘图(Event ev, ScriptAccessory sa)
    {
        _events[6].WaitOne(10000);
        var drawCount = _numbers[5];
        if (drawCount != 3) return;
        var isPartnerStack = _bools[5];
        List<int> partnerJudge = [0, 11, 2, 13, 0, 11, 2, 13];
        
        // 青之多重波，对目标分摊绘图（此处选择TH）
        var myPartIdx = partnerJudge[sa.GetMyIndex()];
        sa.DrawFan(new Vector3(100, 0, 78), sa.Data.PartyList[2], 0, 20000, $"青之波潮分摊 H1",
            (isPartnerStack ? 20f : 30f).DegToRad(), 0, 100, 0, isPartnerStack ? myPartIdx == partnerJudge[2] : myPartIdx < 10);
        sa.DrawFan(new Vector3(100, 0, 78), sa.Data.PartyList[3], 0, 20000, $"青之波潮分摊 H2",
            (isPartnerStack ? 20f : 30f).DegToRad(), 0, 100, 0, isPartnerStack ? myPartIdx == partnerJudge[3] : myPartIdx > 10);

        if (isPartnerStack)
        {
            sa.DrawFan(new Vector3(100, 0, 78), sa.Data.PartyList[0], 0, 20000, $"青之波潮分摊 MT",
                (isPartnerStack ? 20f : 30f).DegToRad(), 0, 100, 0, myPartIdx == partnerJudge[0]);
            sa.DrawFan(new Vector3(100, 0, 78), sa.Data.PartyList[1], 0, 20000, $"青之波潮分摊 ST",
                (isPartnerStack ? 20f : 30f).DegToRad(), 0, 100, 0, myPartIdx == partnerJudge[1]);
        }
        sa.Log.Debug($"青之 {(isPartnerStack ? "四" : "二")} 重波潮 范围绘图完毕");
    }
    
    [ScriptMethod(name: "青之多重波潮指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4516[78])$"],
        userControl: true)]
    public void 青之多重波潮指路(Event ev, ScriptAccessory sa)
    {
        _bools[7] = true;
        var isPartnerStack = _bools[5];
        
        // 青之多重波，指路
        var myIndex = sa.GetMyIndex();
        var startIdx = _numbers[3];
        
        var drawCount = _numbers[5];
        var drawTotalCount = _numbers[4];
        var drawIdx = (startIdx + drawCount) % drawTotalCount;
       
        var isInsideSafe = _aetherBlightRec[drawIdx] is 605 or 606;

        var myPosIdx = myIndex % 4 + (isPartnerStack ? 0 : 2) + (isInsideSafe ? 4 : 0);
        sa.DrawGuidance(_markerPos[myPosIdx], 0, 20000, $"青之波潮指路 #{drawCount}");
        sa.Log.Debug($"青之 {(isPartnerStack ? "四" : "二")} 重波潮 指路 #{drawCount} {_markerPos[myPosIdx]} 绘图完毕");
    }
    
    [ScriptMethod(name: "青之多重波潮指路后续", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(4518[345]|44608)$", "TargetIndex:1"],
        userControl: Debugging, suppress: 1000)]
    public void 青之多重波潮指路后续(Event ev, ScriptAccessory sa)
    {
        if (!_bools[7]) return;
        _events[6].WaitOne(10000);
        
        var isPartnerStack = _bools[5];
        
        // 青之多重波，指路
        var myIndex = sa.GetMyIndex();
        var startIdx = _numbers[3];
        
        var drawCount = _numbers[5];
        var drawTotalCount = _numbers[4];
        var drawIdx = (startIdx + drawCount) % drawTotalCount;
        
        if (drawCount >= drawTotalCount) return;
        var isInsideSafe = _aetherBlightRec[drawIdx] is 605 or 606;
        
        sa.Log.Debug($"删除青之波潮{drawCount-1} 指路");
        sa.Method.RemoveDraw($"青之波潮指路 #{drawCount-1}.*");

        var myPosIdx = myIndex % 4 + (isPartnerStack ? 0 : 2) + (isInsideSafe ? 4 : 0);
        sa.DrawGuidance(_markerPos[myPosIdx], 0, 20000, $"青之波潮指路 #{drawCount}");
        sa.Log.Debug($"青之 {(isPartnerStack ? "四" : "二")} 重波潮 指路 #{drawCount} {_markerPos[myPosIdx]} 绘图完毕");
    }
    
    [ScriptMethod(name: "青之多重波潮绘图删除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(4518[345]|44608)$", "TargetIndex:1"],
        userControl: Debugging, suppress: 1000)]
    public async void 青之多重波潮绘图删除(Event ev, ScriptAccessory sa)
    {
        _events[6].WaitOne(10000);
        if (!_bools[8]) return;
        await Task.Delay(800);
        
        var drawCount = _numbers[5];
        if (drawCount != 4) return;
        // _aetherBlightRec = [];
        // _aetherBlightSourceRec = [];
        // _events[4].Reset();
        // sa.Log.Debug($"青之多重波潮判定，清空矩阵，绘图删除，释放锁_events[4]");
        RefreshParams();
        sa.Log.Debug($"青之多重波潮判定，刷新参数");
        sa.Method.RemoveDraw($"青之波潮.*");
    }
    
    #endregion 青魂 Aether Blight

    #region 大十字 Grand Cross

    [ScriptMethod(name: "---- 大十字 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void 大十字分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "大十字刷新参数", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44568)$"],
        userControl: Debugging)]
    public void 大十字刷新参数(Event ev, ScriptAccessory sa)
    {
        sa.Log.Debug($"读条大十字，刷新参数");
        RefreshParams();
    }
    
    [ScriptMethod(name: "大十字集合提示", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(44568)$"],
        userControl: true)]
    public void 大十字集合提示(Event ev, ScriptAccessory sa)
    {
        sa.Method.TextInfo($"场中集合，两轮引导", 5000, true);
    }
    
    [ScriptMethod(name: "大十字衰减提示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44570)$"],
        userControl: true)]
    public void 大十字衰减提示(Event ev, ScriptAccessory sa)
    {
        sa.Method.TextInfo($"场中集合，两轮引导，角落躲避", 5000, true);
    }
    
    [ScriptMethod(name: "大十字激光", eventType: EventTypeEnum.Tether, eventCondition: ["Id:regex:^(015[78])$"],
        userControl: true)]
    public void 大十字激光(Event ev, ScriptAccessory sa)
    {
        var obj1 = sa.GetById(ev.SourceId);
        var obj2 = sa.GetById(ev.TargetId);
        
        if (obj1 is null || obj2 is null) return;

        var isFast = obj1.DataId == 18761;
        var startPos = obj1.Position.RotateAndExtend(Center, isFast ? 41f.DegToRad() : -153f.DegToRad(), 0);
        var targetPos = obj2.Position.RotateAndExtend(Center, isFast ? 41f.DegToRad() : -153f.DegToRad(), 0);
        
        var dp = sa.DrawRect(startPos, targetPos, 0, 15000,
            $"大十字激光 {ev.SourceId}", 0, 5, 40, draw: false);
        dp.Color = new Vector4(1, 0, 0, 1);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Rect, dp);
        sa.Log.Debug($"预测大十字激光 {(isFast ? "快" : "慢")} {ev.SourceId}，{startPos} 到 {targetPos}，绘图");
    }
    
    [ScriptMethod(name: "大十字激光消失", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(4541)$"],
        userControl: Debugging)]
    public void 大十字激光消失(Event ev, ScriptAccessory sa)
    {
        sa.Log.Debug($"魂块{ev.TargetId}开始转动，大十字激光消失");
        sa.Method.RemoveDraw($"大十字激光 {ev.TargetId}");
    }
    
    [ScriptMethod(name: "大十字点名记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44572)$"],
        userControl: Debugging)]
    public void 大十字点名记录(Event ev, ScriptAccessory sa)
    {
        lock (_numbers)
        {
            _numbers[8]++;
            _bools[9] |= ev.TargetId == sa.Data.Me;
            if (_numbers[8] < 4) return;
            _events[7].Set();
            sa.Log.Debug($"记录点名完毕，玩家{(_bools[9] ? "不踩塔" : "踩塔")}");
        }
    }

    [ScriptMethod(name: "大十字踩塔指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44573)$"],
        userControl: true, suppress: 5000)]
    public void 大十字踩塔指路(Event ev, ScriptAccessory sa)
    {
        _events[7].WaitOne(10000);

        var pos = ev.SourcePosition.GetRadian(Center).RadianToRegion(8, 0, true, false);
        var isDiagTower = pos % 2 == 1;
        sa.Log.Debug($"检测到塔方位 {pos}, {(isDiagTower ? "是" : "不是")} 斜点塔");

        List<int> rot = [2, 1, 3, 0];
        var myIndex = sa.GetMyIndex() % 4;
        
        var tower = !_bools[9];
        if (tower)
        {
            sa.DrawGuidance(new Vector3(100, 0, 105.5f).RotateAndExtend(Center, (rot[myIndex] * 90f + (isDiagTower ? 45f : 0f)).DegToRad(), 0),
                0, 5000, $"大十字踩塔分散");
            sa.Log.Debug($"踩塔指路完毕");
        }
        else
        {
            sa.DrawGuidance(new Vector3(100, 0, 105.5f).RotateAndExtend(Center, (rot[myIndex] * 90f + (isDiagTower ? 0f : 45f)).DegToRad(), 0),
                0, 5000, $"大十字踩塔分散");
            sa.Log.Debug($"分散指路完毕");
        }

    }
    
    [ScriptMethod(name: "大十字踩塔指路删除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(44573)$"],
        userControl: Debugging, suppress: 5000)]
    public void 大十字踩塔指路删除(Event ev, ScriptAccessory sa)
    {
        _bools[9] = false;
        _numbers[8] = 0;
        sa.Log.Debug($"踩塔判定，删除大十字踩塔指路");
        sa.Method.RemoveDraw($"大十字踩塔分散");
    }
    
    #endregion 大十字 GrandCross

    #region 小怪 Mobs

    [ScriptMethod(name: "---- 小怪 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void 小怪分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "小怪压溃", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4457[89]|44584)$"],
        userControl: true)]
    public void 小怪压溃(Event ev, ScriptAccessory sa)
    {
        var dp = sa.DrawRect(ev.SourceId, ev.ActionId is 44579 or 44584 ? 0 : ev.TargetId, 0, 3000,
            $"小怪{ev.SourceId}压溃", 0, 6, 24, draw: false);
        dp.Color = ev.ActionId is 44579 or 44584 ? sa.Data.DefaultDangerColor : new Vector4(1, 0, 0, 1);
        sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }
    
    [ScriptMethod(name: "小怪压溃绘图删除1", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(4457[89]|44584)$"],
        userControl: Debugging)]
    public void 小怪压溃绘图删除1(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"小怪{ev.SourceId}压溃");
    }
    
    [ScriptMethod(name: "小怪压溃绘图删除2", eventType: EventTypeEnum.CancelAction, eventCondition: ["ActionId:regex:^(4457[89]|44584)$"],
        userControl: Debugging)]
    public void 小怪压溃绘图删除2(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"小怪{ev.SourceId}压溃");
    }
   
    [ScriptMethod(name: "转场刷新参数", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44580)$"],
        userControl: Debugging)]
    public void 转场刷新参数(Event ev, ScriptAccessory sa)
    {
        sa.Log.Debug($"读条转场永恒之暗，刷新参数");
        RefreshParams();
    }

    #endregion 小怪 Mobs

    #region 引导之翼 The End's Embrace

    [ScriptMethod(name: "---- 圆环之理 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void 圆环之理分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "场边暗之巨腕拖入", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44591)$"],
        userControl: true)]
    public void 场边暗之巨腕拖入(Event ev, ScriptAccessory sa)
    {
        sa.DrawRect(ev.SourceId, 0, 5000, $"场边暗之巨腕{ev.SourceId}拖入", 0, 10, 36);
    }
    
    [ScriptMethod(name: "场边暗之巨腕危险区记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44591)$"],
        userControl: Debugging)]
    public void 场边暗之巨腕危险区记录(Event ev, ScriptAccessory sa)
    {
        lock (_numbers)
        {
            var rowIdx = (int)Math.Floor((ev.SourcePosition.Z - 79) / 10);
            _numbers[10] += (int)Math.Pow(10, rowIdx - 1);
            sa.Log.Debug($"场边暗之巨腕 第 {rowIdx} 行危险（1起），计数值 {_numbers[10]}");
        }
    }
    
    [ScriptMethod(name: "场边暗之巨腕拖入删除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(44591)$"],
        userControl: Debugging)]
    public void 场边暗之巨腕拖入删除(Event ev, ScriptAccessory sa)
    {
        lock (_numbers)
        {
            var rowIdx = (int)Math.Floor((ev.SourcePosition.Z - 79) / 10);
            _numbers[10] -= (int)Math.Pow(10, rowIdx - 1);
            sa.Log.Debug($"场边暗之巨腕 第 {rowIdx} 行判定，绘图删除，计数值 {_numbers[10]}");
            sa.Method.RemoveDraw($"场边暗之巨腕{ev.SourceId}拖入");
        }
    }
    
    [ScriptMethod(name: "引导之翼指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44597)$"],
        userControl: true)]
    public void 引导之翼指路(Event ev, ScriptAccessory sa)
    {
        // 寻找安全区
        if (_numbers[10] == 0) return;  // 无手，随便放
        int safeRow = 0;
        for (int i = 0; i < 3; i++)
        {
            if (_numbers[10].GetDecimalDigit(i + 1) != 0) continue;
            safeRow = i + 1;
            sa.Log.Debug($"找到暗之巨腕安全行 {safeRow}");
        }
        
        var baitPos = new Vector3(88f, 0, 87f);
        var myIndex = sa.GetMyIndex();
        
        if (myIndex is 2 or 3 or 6 or 7)
            baitPos += new Vector3(0, 0, 6);
        if (myIndex is 4 or 5 or 6 or 7)
            baitPos += new Vector3(6, 0, 0);
        if (myIndex is 1 or 3 or 5 or 7)
            baitPos = baitPos.FoldPointHorizon(Center.X);

        baitPos += new Vector3(0, 0, (safeRow - 1) * 10);
        sa.DrawGuidance(baitPos, 0, 4000, $"引导之翼指路");
        sa.DrawGuidance(baitPos, new Vector3(100, 0, 80 + safeRow * 10),
            0, 4000, $"引导之翼后续指路预备", isSafe: false);
        sa.DrawGuidance(new Vector3(100, 0, 80 + safeRow * 10),
            4000, 4000, $"引导之翼后续指路", isSafe: true);
    }

    [ScriptMethod(name: "引导之翼指路删除", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44567)$"],
        userControl: Debugging, suppress: 2000)]
    public void 引导之翼指路删除(Event ev, ScriptAccessory sa)
    {
        sa.Log.Debug($"黑手释放压溃，引导之翼相关指路删除");
        sa.Method.RemoveDraw($"引导之翼.*");
    }
    
    #endregion 引导之翼 The End's Embrace

    #region 青之环波 Circle of Lives

    [ScriptMethod(name: "---- 青之环波 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void 青之环波分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "青之环波绘图", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44600)$"],
        userControl: true)]
    public void 青之环波绘图(Event ev, ScriptAccessory sa)
    {
        sa.DrawDonut(ev.SourceId, _judging_CoL ? 2500 : 0, _judging_CoL ? 4500 : 7000,
            $"青之环波{ev.SourceId}", 50, 3);
        _judging_CoL = true;
    }
    
    [ScriptMethod(name: "青之环波绘图删除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(44600)$"],
        userControl: Debugging)]
    public void 青之环波绘图删除(Event ev, ScriptAccessory sa)
    {
        sa.Method.RemoveDraw($"青之环波{ev.SourceId}");
    }

    #endregion 青之环波 Circle of Lives

    #region 群体恐慌 Mass Macabre

    [ScriptMethod(name: "---- 群体恐慌 ----", eventType: EventTypeEnum.NpcYell, eventCondition: ["HelloayaWorld:asdf"],
        userControl: true)]
    public void 群体恐慌分割线(Event ev, ScriptAccessory sa)
    {
    }
    
    [ScriptMethod(name: "群体恐慌刷新参数", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44595)$"],
        userControl: Debugging)]
    public void 群体恐慌刷新参数(Event ev, ScriptAccessory sa)
    {
        sa.Log.Debug($"读条群体恐慌，刷新参数");
        RefreshParams();
    }
    
    [ScriptMethod(name: "群体恐慌踩塔", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(44595)$"],
        userControl: true)]
    public void 群体恐慌塔1(Event ev, ScriptAccessory sa)
    {
        var myIndex = sa.GetMyIndex();
        var isMelee = myIndex is 0 or 1 or 4 or 5;
        sa.Log.Debug($"群体恐慌塔1 指路");
        sa.DrawGuidance(new Vector3(100, 0, isMelee ? 94 : 106), 0, 10000, $"群体恐慌塔1");
    }
    
    [ScriptMethod(name: "群体恐慌塔 后续", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(44819)$"],
        userControl: Debugging)]
    public void 群体恐慌踩塔计数(Event ev, ScriptAccessory sa)
    {
        if (ev.TargetId != sa.Data.Me) return;
        _numbers[7]++;
        sa.Log.Debug($"完成群体恐慌踩塔，计数 {_numbers[7]}");
        sa.Method.RemoveDraw($"群体恐慌塔{_numbers[7]}.*");

        var myIndex = sa.GetMyIndex();
        switch (_numbers[7])
        {
            case 1:
                var mySecondTower = new Vector3(85f, 0, 103f);
                if (myIndex is 4 or 5 or 6 or 7) mySecondTower = mySecondTower.FoldPointVertical(Center.Z);
                if (myIndex is 1 or 3 or 4 or 5) mySecondTower = mySecondTower.FoldPointHorizon(Center.X);
                sa.Log.Debug($"向 塔#2 {mySecondTower} 指路");
                sa.DrawGuidance(mySecondTower, 0, 4000, $"群体恐慌塔2预备", isSafe: false);
                sa.DrawGuidance(mySecondTower, 4000, 6000, $"群体恐慌塔2", isSafe: true);
                break;
            case 2:
                var myThirdTower = myIndex switch
                {
                    2 or 6 or 7 => new Vector3(93.5f, 0, 112f),
                    3 or 4 or 5 => new Vector3(93.5f, 0, 112f).PointCenterSymmetry(Center),
                    0 => new Vector3(82.5f, 0, 85.5f),
                    1 => new Vector3(82.5f, 0, 85.5f).FoldPointHorizon(Center.X),
                };
                sa.Log.Debug($"向 塔#3 {myThirdTower} 或 死刑点 指路");
                sa.DrawGuidance(myThirdTower, 0, 4000, $"群体恐慌塔3预备", isSafe: false);
                sa.DrawGuidance(myThirdTower, 4000, 6000, $"群体恐慌塔3", isSafe: true);
                break;
            case 3:
                // 一般来说，双T不会到达这一步
                var myFourthTower = myIndex switch
                {
                    2 or 6 or 7 => new Vector3(93.5f, 0, 112f).FoldPointHorizon(Center.X),
                    3 or 4 or 5 => new Vector3(93.5f, 0, 112f).FoldPointVertical(Center.Z),
                    0 => new Vector3(82.5f, 0, 85.5f),
                    1 => new Vector3(82.5f, 0, 85.5f).FoldPointHorizon(Center.X),
                };
                sa.Log.Debug($"向 塔#4 {myFourthTower} 或 死刑点 指路");
                sa.DrawGuidance(myFourthTower, 0, 4000, $"群体恐慌塔4预备", isSafe: false);
                sa.DrawGuidance(myFourthTower, 4000, 6000, $"群体恐慌塔4", isSafe: true);
                break;
            default:
                break;
        }
    }
    
    #endregion 群体恐慌 Mass Macabre
    
    [ScriptMethod(name: "阶段转换", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:12345"],
        userControl: Debugging)]
    public void PhaseChange(Event ev, ScriptAccessory sa)
    {
        // _phase = Phase.P2;
        // sa.Log.Debug($"当前阶段为：{_phase}");
    }

    #region 优先级字典 类
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
                str += $"Key {pair.Key} {(showJob ? $"({Role[pair.Key]})" : "")}, Value {pair.Value}\n";
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

    #endregion 优先级字典 类

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

    public static uint Id0(this Event @event)
    {
        return ParseHexId(@event["Id"], out var id) ? id : 0;
    }
    
    public static uint Index(this Event ev)
    {
        return JsonConvert.DeserializeObject<uint>(ev["Index"]);
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

    public static string GetPlayerJob(this ScriptAccessory sa, IPlayerCharacter? playerObject, bool fullName = false)
    {
        if (playerObject == null) return "None";
        return fullName ? playerObject.ClassJob.Value.Name.ToString() : playerObject.ClassJob.Value.Abbreviation.ToString();
    }

    public static float GetStatusRemainingTime(this ScriptAccessory sa, IBattleChara? battleChara, uint statusId)
    {
        if (battleChara == null || !battleChara.IsValid()) return 0;
        unsafe
        {
            BattleChara* charaStruct = (BattleChara*)battleChara.Address;
            var statusIdx = charaStruct->GetStatusManager()->GetStatusIndex(statusId);
            return charaStruct->GetStatusManager()->GetRemainingTime(statusIdx);
        }
    }
}
#region 计算函数

public static class MathTools
{
    public static float DegToRad(this float deg) => (deg + 360f) % 360f / 180f * float.Pi;
    public static float RadToDeg(this float rad) => (rad + 2 * float.Pi) % (2 * float.Pi) / float.Pi * 180f;
    
    /// <summary>
    /// 获得任意点与中心点的弧度值，以(0, 0, 1)方向为0，以(1, 0, 0)方向为pi/2。
    /// 即，逆时针方向增加。
    /// </summary>
    /// <param name="point">任意点</param>
    /// <param name="center">中心点</param>
    /// <returns></returns>
    public static float GetRadian(this Vector3 point, Vector3 center)
        => MathF.Atan2(point.X - center.X, point.Z - center.Z);

    /// <summary>
    /// 获得任意点与中心点的长度。
    /// </summary>
    /// <param name="point">任意点</param>
    /// <param name="center">中心点</param>
    /// <returns></returns>
    public static float GetLength(this Vector3 point, Vector3 center)
        => new Vector2(point.X - center.X, point.Z - center.Z).Length();
    
    /// <summary>
    /// 将任意点以中心点为圆心，逆时针旋转并延长。
    /// </summary>
    /// <param name="point">任意点</param>
    /// <param name="center">中心点</param>
    /// <param name="radian">旋转弧度</param>
    /// <param name="length">基于该点延伸长度</param>
    /// <returns></returns>
    public static Vector3 RotateAndExtend(this Vector3 point, Vector3 center, float radian, float length)
    {
        var baseRad = point.GetRadian(center);
        var baseLength = point.GetLength(center);
        var rotRad = baseRad + radian;
        return new Vector3(
            center.X + MathF.Sin(rotRad) * (length + baseLength),
            center.Y,
            center.Z + MathF.Cos(rotRad) * (length + baseLength)
        );
    }
    
    /// <summary>
    /// 获得某角度所在划分区域
    /// </summary>
    /// <param name="radian">输入弧度</param>
    /// <param name="regionNum">区域划分数量</param>
    /// <param name="baseRegionIdx">0度所在区域的初始Idx</param>>
    /// <param name="isDiagDiv">是否为斜分割，默认为false</param>
    /// <param name="isCw">是否顺时针增加，默认为false</param>
    /// <returns></returns>
    public static int RadianToRegion(this float radian, int regionNum, int baseRegionIdx = 0, bool isDiagDiv = false, bool isCw = false)
    {
        var sepRad = float.Pi * 2 / regionNum;
        var inputAngle = radian * (isCw ? -1 : 1) + (isDiagDiv ? sepRad / 2 : 0);
        var rad = (inputAngle + 4 * float.Pi) % (2 * float.Pi);
        return ((int)Math.Floor(rad / sepRad) + baseRegionIdx + regionNum) % regionNum;
    }
    
    /// <summary>
    /// 将输入点左右折叠
    /// </summary>
    /// <param name="point">待折叠点</param>
    /// <param name="centerX">中心折线坐标点</param>
    /// <returns></returns>
    public static Vector3 FoldPointHorizon(this Vector3 point, float centerX)
        => point with { X = 2 * centerX - point.X };

    /// <summary>
    /// 将输入点上下折叠
    /// </summary>
    /// <param name="point">待折叠点</param>
    /// <param name="centerZ">中心折线坐标点</param>
    /// <returns></returns>
    public static Vector3 FoldPointVertical(this Vector3 point, float centerZ)
        => point with { Z = 2 * centerZ - point.Z };

    /// <summary>
    /// 将输入点中心对称
    /// </summary>
    /// <param name="point">输入点</param>
    /// <param name="center">中心点</param>
    /// <returns></returns>
    public static Vector3 PointCenterSymmetry(this Vector3 point, Vector3 center) 
        => point.RotateAndExtend(center, float.Pi, 0);
    
    /// <summary>
    /// 获取给定数的指定位数
    /// </summary>
    /// <param name="val">给定数值</param>
    /// <param name="x">对应位数，个位为1</param>
    /// <returns></returns>
    public static int GetDecimalDigit(this int val, int x)
    {
        var valStr = val.ToString();
        var length = valStr.Length;
        if (x < 1 || x > length) return -1;
        var digitChar = valStr[length - x]; // 从右往左取第x位
        return int.Parse(digitChar.ToString());
    }
}

#endregion 计算函数

#region 位置序列函数
public static class IndexHelper
{
    /// <summary>
    /// 输入玩家dataId，获得对应的位置index
    /// </summary>
    /// <param name="pid">玩家SourceId</param>
    /// <param name="sa"></param>
    /// <returns>该玩家对应的位置index</returns>
    public static int GetPlayerIdIndex(this ScriptAccessory sa, uint pid)
    {
        // 获得玩家 IDX
        return sa.Data.PartyList.IndexOf(pid);
    }

    /// <summary>
    /// 获得主视角玩家对应的位置index
    /// </summary>
    /// <param name="sa"></param>
    /// <returns>主视角玩家对应的位置index</returns>
    public static int GetMyIndex(this ScriptAccessory sa)
    {
        return sa.Data.PartyList.IndexOf(sa.Data.Me);
    }

    /// <summary>
    /// 输入玩家dataId，获得对应的位置称呼，输出字符仅作文字输出用
    /// </summary>
    /// <param name="pid">玩家SourceId</param>
    /// <param name="sa"></param>
    /// <returns>该玩家对应的位置称呼</returns>
    public static string GetPlayerJobById(this ScriptAccessory sa, uint pid)
    {
        // 获得玩家职能简称，无用处，仅作DEBUG输出
        var idx = sa.Data.PartyList.IndexOf(pid);
        var str = sa.GetPlayerJobByIndex(idx);
        return str;
    }

    /// <summary>
    /// 输入位置index，获得对应的位置称呼，输出字符仅作文字输出用
    /// </summary>
    /// <param name="idx">位置index</param>
    /// <param name="fourPeople">是否为四人迷宫</param>
    /// <param name="sa"></param>
    /// <returns></returns>
    public static string GetPlayerJobByIndex(this ScriptAccessory sa, int idx, bool fourPeople = false)
    {
        List<string> role8 = ["MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4"];
        List<string> role4 = ["T", "H", "D1", "D2"];
        if (idx < 0 || idx >= 8 || (fourPeople && idx >= 4))
            return "Unknown";
        return fourPeople ? role4[idx] : role8[idx];
    }
    
    /// <summary>
    /// 将List内信息转换为字符串。
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="myList"></param>
    /// <param name="isJob">是职业，在转为字符串前调用转职业函数</param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static string BuildListStr<T>(this ScriptAccessory sa, List<T> myList, bool isJob = false)
    {
        return string.Join(", ", myList.Select(item =>
        {
            if (isJob && item != null && item is int i)
                return sa.GetPlayerJobByIndex(i);
            return item?.ToString() ?? "";
        }));
    }
}
#endregion 位置序列函数

#region 绘图函数

public static class DrawTools
{
    /// <summary>
    /// 返回绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">绘图基准，可为UID或位置</param>
    /// <param name="targetObj">绘图指向目标，可为UID或位置</param>
    /// <param name="delay">延时delay ms出现</param>
    /// <param name="destroy">绘图自出现起，经destroy ms消失</param>
    /// <param name="name">绘图名称</param>
    /// <param name="radian">绘制图形弧度范围</param>
    /// <param name="rotation">绘制图形旋转弧度，以owner面前为基准，逆时针增加</param>
    /// <param name="width">绘制图形宽度，部分图形可保持与长度一致</param>
    /// <param name="length">绘制图形长度，部分图形可保持与宽度一致</param>
    /// <param name="innerWidth">绘制图形内宽，部分图形可保持与长度一致</param>
    /// <param name="innerLength">绘制图形内长，部分图形可保持与宽度一致</param>
    /// <param name="drawModeEnum">绘图方式</param>
    /// <param name="drawTypeEnum">绘图类型</param>
    /// <param name="isSafe">是否使用安全色</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="byY">动画效果随距离变更</param>
    /// <param name="draw">是否直接绘图</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawOwnerBase(this ScriptAccessory sa, 
        object ownerObj, object targetObj, int delay, int destroy, string name, 
        float radian, float rotation, float width, float length, float innerWidth, float innerLength,
        DrawModeEnum drawModeEnum, DrawTypeEnum drawTypeEnum, bool isSafe = false,
        bool byTime = false, bool byY = false, bool draw = true)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.InnerScale = new Vector2(innerWidth, innerLength);
        dp.Radian = radian;
        dp.Rotation = rotation;
        dp.Color = isSafe ? sa.Data.DefaultSafeColor: sa.Data.DefaultDangerColor;
        dp.Delay = delay;
        dp.DestoryAt = destroy;
        dp.ScaleMode |= byTime ? ScaleMode.ByTime : ScaleMode.None;
        dp.ScaleMode |= byY ? ScaleMode.YByDistance : ScaleMode.None;
        
        switch (ownerObj)
        {
            case uint u:
                dp.Owner = u;
                break;
            case ulong ul:
                dp.Owner = ul;
                break;
            case Vector3 spos:
                dp.Position = spos;
                break;
            default:
                throw new ArgumentException($"ownerObj {ownerObj} 的目标类型 {ownerObj.GetType()} 输入错误");
        }

        switch (targetObj)
        {
            case 0:
            case 0u:
                break;
            case uint u:
                dp.TargetObject = u;
                break;
            case ulong ul:
                dp.TargetObject = ul;
                break;
            case Vector3 tpos:
                dp.TargetPosition = tpos;
                break;
            default:
                throw new ArgumentException($"targetObj {targetObj} 的目标类型 {targetObj.GetType()} 输入错误");
        }
        
        if (draw)
            sa.Method.SendDraw(drawModeEnum, drawTypeEnum, dp);
        return dp;
    }

    /// <summary>
    /// 返回指路绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">出发点</param>
    /// <param name="targetObj">结束点</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="rotation">箭头旋转角度</param>
    /// <param name="width">箭头宽度</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawGuidance(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name,
        float rotation = 0, float width = 1f, bool isSafe = true, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 0, rotation, width,
            width, 0, 0, DrawModeEnum.Imgui, DrawTypeEnum.Displacement, isSafe, false, true, draw);
    
    public static DrawPropertiesEdit DrawGuidance(this ScriptAccessory sa,
        object targetObj, int delay, int destroy, string name, float rotation = 0, float width = 1f, bool isSafe = true,
        bool draw = true)
        => sa.DrawGuidance((ulong)sa.Data.Me, targetObj, delay, destroy, name, rotation, width, isSafe, draw);

    /// <summary>
    /// 返回圆形绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">圆心</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="scale">圆形径长</param>
    /// <param name="byTime">是否随时间扩充</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawCircle(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name,
        float scale, bool isSafe = false, bool byTime = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, 0, delay, destroy, name, 2 * float.Pi, 0, scale, scale,
            0, 0, DrawModeEnum.Default, DrawTypeEnum.Circle, isSafe, byTime,false, draw);

    /// <summary>
    /// 返回环形绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">圆心</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="outScale">外径</param>
    /// <param name="innerScale">内径</param>
    /// <param name="byTime">是否随时间扩充</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawDonut(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name,
        float outScale, float innerScale, bool isSafe = false, bool byTime = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, 0, delay, destroy, name, 2 * float.Pi, 0, outScale, outScale, innerScale,
            innerScale, DrawModeEnum.Default, DrawTypeEnum.Donut, isSafe, byTime, false, draw);

    /// <summary>
    /// 返回扇形绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">圆心</param>
    /// <param name="targetObj">目标</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="radian">弧度</param>
    /// <param name="rotation">旋转角度</param>
    /// <param name="outScale">外径</param>
    /// <param name="innerScale">内径</param>
    /// <param name="byTime">是否随时间扩充</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawFan(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name, float radian, float rotation,
        float outScale, float innerScale, bool isSafe = false, bool byTime = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, radian, rotation, outScale, outScale, innerScale,
            innerScale, DrawModeEnum.Default, DrawTypeEnum.Fan, isSafe, byTime, false, draw);

    public static DrawPropertiesEdit DrawFan(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name, float radian, float rotation,
        float outScale, float innerScale, bool isSafe = false, bool byTime = false, bool draw = true)
        => sa.DrawFan(ownerObj, 0, delay, destroy, name, radian, rotation, outScale, innerScale, isSafe, byTime, draw);

    /// <summary>
    /// 返回矩形绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">矩形起始</param>
    /// <param name="targetObj">目标</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="rotation">旋转角度</param>
    /// <param name="width">矩形宽度</param>
    /// <param name="length">矩形长度</param>
    /// <param name="byTime">是否随时间扩充</param>
    /// <param name="byY">是否随距离扩充</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawRect(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name, float rotation,
        float width, float length, bool isSafe = false, bool byTime = false, bool byY = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 0, rotation, width, length, 0, 0,
            DrawModeEnum.Default, DrawTypeEnum.Rect, isSafe, byTime, byY, draw);
    
    public static DrawPropertiesEdit DrawRect(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name, float rotation,
        float width, float length, bool isSafe = false, bool byTime = false, bool byY = false, bool draw = true)
        => sa.DrawRect(ownerObj, 0, delay, destroy, name, rotation, width, length, isSafe, byTime, byY, draw);
    
    /// <summary>
    /// 返回背对绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="targetObj">目标</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawSightAvoid(this ScriptAccessory sa,
        object targetObj, int delay, int destroy, string name, bool isSafe = true, bool draw = true)
        => sa.DrawOwnerBase(sa.Data.Me, targetObj, delay, destroy, name, 0, 0, 0, 0, 0, 0,
            DrawModeEnum.Default, DrawTypeEnum.SightAvoid, isSafe, false, false, draw);

    /// <summary>
    /// 返回击退绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="targetObj">击退源</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="width">箭头宽</param>
    /// <param name="length">箭头长</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawKnockBack(this ScriptAccessory sa,
        object targetObj, int delay, int destroy, string name, float width, float length,
        bool isSafe = false, bool draw = true)
        => sa.DrawOwnerBase(sa.Data.Me, targetObj, delay, destroy, name, 0, float.Pi, width, length, 0, 0,
            DrawModeEnum.Default, DrawTypeEnum.Displacement, isSafe, false, false, draw);

    /// <summary>
    /// 返回线型绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">线条起始</param>
    /// <param name="targetObj">线条目标</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="rotation">旋转角度</param>
    /// <param name="width">线条宽度</param>
    /// <param name="length">线条长度</param>
    /// <param name="byTime">是否随时间扩充</param>
    /// <param name="byY">是否随距离扩充</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawLine(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name, float rotation,
        float width, float length, bool isSafe = false, bool byTime = false, bool byY = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 1, rotation, width, length, 0, 0,
            DrawModeEnum.Default, DrawTypeEnum.Line, isSafe, byTime, byY, draw);
    
    /// <summary>
    /// 返回两对象间连线绘图
    /// </summary>
    /// <param name="sa"></param>
    /// <param name="ownerObj">起始源</param>
    /// <param name="targetObj">目标源</param>
    /// <param name="delay">延时</param>
    /// <param name="destroy">消失时间</param>
    /// <param name="name">绘图名字</param>
    /// <param name="width">线宽</param>
    /// <param name="isSafe">是否安全色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawConnection(this ScriptAccessory sa, object ownerObj, object targetObj,
        int delay, int destroy, string name, float width = 1f, bool isSafe = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 0, 0, width, width,
            0, 0, DrawModeEnum.Imgui, DrawTypeEnum.Line, isSafe, false, true, draw);

    /// <summary>
    /// 赋予输入的dp以ownerId为源的远近目标绘图
    /// </summary>
    /// <param name="self"></param>
    /// <param name="isNearOrder">从owner计算，近顺序或远顺序</param>
    /// <param name="orderIdx">从1开始</param>
    /// <returns></returns>
    public static DrawPropertiesEdit SetOwnersDistanceOrder(this DrawPropertiesEdit self, bool isNearOrder,
        uint orderIdx)
    {
        self.CentreResolvePattern = isNearOrder
            ? PositionResolvePatternEnum.PlayerNearestOrder
            : PositionResolvePatternEnum.PlayerFarestOrder;
        self.CentreOrderIndex = orderIdx;
        return self;
    }
    
    /// <summary>
    /// 赋予输入的dp以ownerId为源的仇恨顺序绘图
    /// </summary>
    /// <param name="self"></param>
    /// <param name="orderIdx">仇恨顺序，从1开始</param>
    /// <returns></returns>
    public static DrawPropertiesEdit SetOwnersEnmityOrder(this DrawPropertiesEdit self, uint orderIdx)
    {
        self.TargetResolvePattern = PositionResolvePatternEnum.OwnerEnmityOrder;
        self.TargetOrderIndex = orderIdx;
        return self;
    }
    
    /// <summary>
    /// 赋予输入的dp以position为源的远近目标绘图
    /// </summary>
    /// <param name="self"></param>
    /// <param name="isNearOrder">从owner计算，近顺序或远顺序</param>
    /// <param name="orderIdx">从1开始</param>
    /// <returns></returns>
    public static DrawPropertiesEdit SetPositionDistanceOrder(this DrawPropertiesEdit self, bool isNearOrder,
        uint orderIdx)
    {
        self.TargetResolvePattern = isNearOrder
            ? PositionResolvePatternEnum.PlayerNearestOrder
            : PositionResolvePatternEnum.PlayerFarestOrder;
        self.TargetOrderIndex = orderIdx;
        return self;
    }
    
    /// <summary>
    /// 赋予输入的dp以ownerId施法目标为源的绘图
    /// </summary>
    /// <param name="self"></param>
    /// <returns></returns>
    public static DrawPropertiesEdit SetOwnersTarget(this DrawPropertiesEdit self)
    {
        self.TargetResolvePattern = PositionResolvePatternEnum.OwnerTarget;
        return self;
    }
}

#endregion 绘图函数

#region 特殊函数

public static class SpecialFunction
{
    public static void SetTargetable(this ScriptAccessory sa, IGameObject? obj, bool targetable)
    {
        if (obj == null || !obj.IsValid())
        {
            sa.Log.Error($"传入的IGameObject不合法。");
            return;
        }
        unsafe
        {
            GameObject* charaStruct = (GameObject*)obj.Address;
            if (targetable)
            {
                if (obj.IsDead || obj.IsTargetable) return;
                charaStruct->TargetableStatus |= ObjectTargetableFlags.IsTargetable;
            }
            else
            {
                if (!obj.IsTargetable) return;
                charaStruct->TargetableStatus &= ~ObjectTargetableFlags.IsTargetable;
            }
        }
        sa.Log.Debug($"SetTargetable {targetable} => {obj.Name} {obj}");
    }

    public static void ScaleModify(this ScriptAccessory sa, IGameObject? obj, float scale)
    {
        if (obj == null || !obj.IsValid())
        {
            sa.Log.Error($"传入的IGameObject不合法。");
            return;
        }
        unsafe
        {
            GameObject* charaStruct = (GameObject*)obj.Address;
            charaStruct->Scale = scale;
            charaStruct->DisableDraw();
            charaStruct->EnableDraw();
        }
        sa.Log.Debug($"ScaleModify => {obj.Name.TextValue} | {obj} => {scale}");
    }

    public static void SetRotation(this ScriptAccessory sa, IGameObject? obj, float radian, bool show = false)
    {
        if (obj == null || !obj.IsValid())
        {
            sa.Log.Error($"传入的IGameObject不合法。");
            return;
        }
        unsafe
        {
            GameObject* charaStruct = (GameObject*)obj.Address;
            charaStruct->SetRotation(radian);
        }
        sa.Log.Debug($"改变面向 {obj.Name.TextValue} | {obj.EntityId} => {radian.RadToDeg()}");
        
        if (!show) return;
        var ownerObj = sa.GetById(obj.EntityId);
        if (ownerObj == null) return;
        var dp = sa.DrawGuidance(ownerObj, 0, 0, 2000, $"改变面向 {obj.Name.TextValue}", radian, draw: false);
        dp.FixRotation = true;
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Arrow, dp);
        
    }
    
    public static unsafe float GetRotation(this ScriptAccessory sa, IGameObject? obj)
    {
        if (obj == null || !obj.IsValid())
        {
            sa.Log.Error($"传入的IGameObject不合法。");
            return 0;
        }
        GameObject* charaStruct = (GameObject*)obj.Address;
        var rotation = charaStruct->Rotation;
        sa.Log.Debug($"GetRotation => {obj.Name.TextValue} | {obj} => {rotation}");
        return rotation;
    }

    public static void SetPosition(this ScriptAccessory sa, IGameObject? obj, Vector3 position, bool show = false)
    {
        if (obj == null || !obj.IsValid())
        {
            sa.Log.Error($"传入的IGameObject不合法。");
            return;
        }
        unsafe
        {
            GameObject* charaStruct = (GameObject*)obj.Address;
            charaStruct->SetPosition(position.X, position.Y, position.Z);
        }
        sa.Log.Debug($"改变位置 => {obj.Name.TextValue} | {obj.EntityId} => {position}");
        
        if (!show) return;
        var dp = sa.DrawCircle(position, 0, 2000, $"传送点 {obj.Name.TextValue}", 0.5f, true, draw: false);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Circle, dp);
        
    }
}

#endregion 特殊函数

#endregion 函数集

