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
using FFXIVClientStructs;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using System.Drawing;
using System.Security.AccessControl;
using Lumina.Excel.GeneratedSheets2;
using System.ComponentModel;

namespace UsamisScript.EndWalker.p4s;

[ScriptType(name: "P4S [零式万魔殿 边境之狱4]", territorys: [1009], guid: "de9e31e6-d040-48e3-bf0b-aa4e2643f79d", version: "0.0.0.2", author: "Usami", note: noteStr)]

public class p4s
{
    const string noteStr =
    """
    请先按需求检查并设置“用户设置”栏目。
    “逃课”即为黑糖荔枝攻略演示。
    门神仅线毒提示，本体到二运。

    v0.0.0.2
    1. 初版完成。
    鸭门。
    """;

    [UserSetting("Debug模式，非开发用请关闭")]
    public static bool DebugMode { get; set; } = false;
    public enum Act2StrategyEnum
    {
        正攻_Regular,
        逃课_Spread
    }

    [UserSetting("二运解法")]
    public Act2StrategyEnum Act2Strategy { get; set; } = Act2StrategyEnum.逃课_Spread;

    [UserSetting("站位提示圈绘图-普通颜色")]
    public static ScriptColor posColorNormal { get; set; } = new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 1.0f, 1.0f) };

    [UserSetting("站位提示圈绘图-玩家站位颜色")]
    public static ScriptColor posColorPlayer { get; set; } = new ScriptColor { V4 = new Vector4(0.0f, 1.0f, 1.0f, 1.0f) };

    public enum P4S_Phase
    {
        Init,           // 初始
        Act1,           // 一幕
        Act2,           // 二幕
        Act3            // 三幕
    }
    public static Vector3 CENTER = new Vector3(100, 0, 100);
    P4S_Phase phase = P4S_Phase.Init;
    List<bool> Drawn = new bool[20].ToList();   // 绘图记录
    int BloodrakeCastTime = 0;  // 聚血释放次数
    List<int> BloodrakeNum = [0, 0, 0, 0, 0, 0, 0, 0];  // 聚血次数记录
    int Act1CirclePosition = -1;    // 一幕大钢铁方位
    int Act2CirclePosition = -1;    // 二幕大钢铁方位
    Act2Solution Act2Sol = new Act2Solution();  // 二幕解决方案
    public void Init(ScriptAccessory accessory)
    {
        phase = P4S_Phase.Init;
        BloodrakeCastTime = 0;  // 聚血释放次数
        List<bool> Drawn = new bool[20].ToList();   // 绘图记录
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

        var str = Act2Sol.Print();
        DebugMsg($"{str}", accessory);

        // drawSpreadDir(Act2Sol, accessory);
    }

    [ScriptMethod(name: "门神：聚血初始化（不可控）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27096"], userControl: false)]
    public void BloodrakeInit(Event @event, ScriptAccessory accessory)
    {
        if (BloodrakeCastTime == 0)
            BloodrakeNum = [0, 0, 0, 0, 0, 0, 0, 0];  // 聚血次数记录
        BloodrakeCastTime++;
    }

    [ScriptMethod(name: "门神：聚血记录（不可控）", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:27096"], userControl: false)]
    public void BloodrakeRecord(Event @event, ScriptAccessory accessory)
    {
        var tid = @event.TargetId();
        var tidx = accessory.getPlayerIdIndex(tid);

        if (BloodrakeCastTime == 1)
            BloodrakeNum[tidx] = BloodrakeNum[tidx] + 1;
        else if (BloodrakeCastTime == 2)
            BloodrakeNum[tidx] = BloodrakeNum[tidx] + 10;
        else
            return;
    }

    [ScriptMethod(name: "门神：线毒行动提示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27110"])]
    public void DirectorAction(Event @event, ScriptAccessory accessory)
    {
        var myIndex = accessory.getMyIndex();
        switch (BloodrakeNum[myIndex])
        {
            case 0:
                accessory.Method.TextInfo($"接毒、接线", 15000, false);
                break;
            case 1:
                accessory.Method.TextInfo($"接毒、不接线", 15000, false);
                break;
            case 10:
                accessory.Method.TextInfo($"不接毒、接线", 15000, false);
                break;
            case 11:
                accessory.Method.TextInfo($"不接毒、不接线", 15000, false);
                break;
            default:
                return;
        }
    }

    [ScriptMethod(name: "本体：一运，阶段记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27148"], userControl: false)]
    public void Act1_PhaseRecord(Event @event, ScriptAccessory accessory)
    {
        phase = P4S_Phase.Act1;
        Act1CirclePosition = -1;    // 一幕大钢铁方位
        Drawn[0] = false;
        DebugMsg($"当前阶段为：{phase}", accessory);
    }

    [ScriptMethod(name: "本体：一运范围提示", eventType: EventTypeEnum.Tether, eventCondition: ["Id:00AD"])]
    public void Act1_Field(Event @event, ScriptAccessory accessory)
    {
        if (phase != P4S_Phase.Act1) return;
        if (Act1CirclePosition != -1) return;
        if (Drawn[0]) return;
        Drawn[0] = true;

        var spos = @event.SourcePosition();
        Act1CirclePosition = spos.PositionRoundToDirs(CENTER, 4);

        DebugMsg($"进行一运绘制{Act1CirclePosition}", accessory);

        var _pos1 = spos;
        var _pos2 = spos.RotatePoint(CENTER, float.Pi);
        drawBigCircle(_pos1, 0, 11000, $"钢铁1", accessory);
        drawBigCircle(_pos2, 0, 11000, $"钢铁2", accessory);

        var _pos3 = spos.RotatePoint(CENTER, float.Pi / 2);
        var _pos4 = spos.RotatePoint(CENTER, -float.Pi / 2);
        drawBigCircle(_pos3, 14000, 3000, $"钢铁3", accessory);
        drawBigCircle(_pos4, 14000, 3000, $"钢铁4", accessory);
    }

    private static void drawBigCircle(Vector3 spos, int delay, int destoryAt, string name, ScriptAccessory accessory)
    {
        var dp = accessory.drawStatic(spos, 0, delay, destoryAt, name);
        dp.Scale = new(20);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    private static void drawTowerRegion(Vector3 spos, int delay, int destoryAt, string name, ScriptAccessory accessory)
    {
        var dp = accessory.drawStatic(spos, 0, delay, destoryAt, name);
        dp.Scale = new(4);
        dp.Color = ColorHelper.colorCyan.V4.WithW(1.5f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "本体：近远死刑", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(2717[45])$"])]
    public void MB_TankBusterFarNear(Event @event, ScriptAccessory accessory)
    {
        var aid = @event.ActionId();
        var sid = @event.SourceId();
        const uint NEAR = 27174;
        // const uint FAR = 27175;
        var _isNear = aid == NEAR;

        var me = IbcHelper.GetMe();
        if (me == null) return;
        var myRole = me.GetRole();

        var dp1 = accessory.drawCenterOrder(sid, 1, 0, 5000, $"死刑1");
        dp1.CentreResolvePattern = _isNear ? PositionResolvePatternEnum.PlayerNearestOrder : PositionResolvePatternEnum.PlayerFarestOrder;
        dp1.Scale = new(5f);
        dp1.Color = myRole == CombatRole.Tank ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp1);

        var dp2 = accessory.drawCenterOrder(sid, 2, 0, 5000, $"死刑2");
        dp2.CentreResolvePattern = _isNear ? PositionResolvePatternEnum.PlayerNearestOrder : PositionResolvePatternEnum.PlayerFarestOrder;
        dp2.Scale = new(5f);
        dp2.Color = myRole == CombatRole.Tank ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp2);
    }

    [ScriptMethod(name: "本体：分摊死刑", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28280"])]
    public void MB_TankBuster(Event @event, ScriptAccessory accessory)
    {
        var tid = @event.TargetId();
        var dp = accessory.drawCircle(tid, 0, 5000, $"死刑{tid}");
        dp.Scale = new(6f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }

    [ScriptMethod(name: "本体：二运，阶段记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:28340"], userControl: false)]
    public void Act2_PhaseRecord(Event @event, ScriptAccessory accessory)
    {
        phase = P4S_Phase.Act2;
        Act2CirclePosition = -1;    // 二幕大钢铁方位
        for (int i = 1; i <= 5; i++)
            Drawn[i] = false;
        Act2Sol.Init();
        DebugMsg($"当前阶段为：{phase}", accessory);
    }

    [ScriptMethod(name: "本体：二运范围提示", eventType: EventTypeEnum.Tether, eventCondition: ["Id:00AD"])]
    public void Act2_Field(Event @event, ScriptAccessory accessory)
    {
        if (phase != P4S_Phase.Act2) return;
        if (Act2CirclePosition != -1) return;
        if (Drawn[1]) return;
        Drawn[1] = true;

        var spos = @event.SourcePosition();
        var _tempPos = spos.PositionRoundToDirs(CENTER, 4);
        var _atNorthLeftTower = _tempPos == 0 && spos.X < CENTER.X;
        var _atEastUpTower = _tempPos == 1 && spos.Z < CENTER.Z;
        var _atSouthRightTower = _tempPos == 2 && spos.X > CENTER.X;
        var _atWestDownTower = _tempPos == 3 && spos.Z > CENTER.Z;

        if (_atNorthLeftTower || _atEastUpTower || _atSouthRightTower || _atWestDownTower)
            return;

        Act2CirclePosition = _tempPos;
        DebugMsg($"进行二运绘制{Act2CirclePosition}", accessory);

        var _pos1 = spos;
        var _pos2 = spos.RotatePoint(CENTER, float.Pi);
        drawBigCircle(_pos1, 0, 19000, $"钢铁1", accessory);
        drawBigCircle(_pos2, 0, 19000, $"钢铁2", accessory);
        var _pos3 = spos.RotatePoint(CENTER, float.Pi / 2);
        var _pos4 = spos.RotatePoint(CENTER, -float.Pi / 2);
        drawBigCircle(_pos3, 19000, 7000, $"钢铁3", accessory);
        drawBigCircle(_pos4, 19000, 7000, $"钢铁4", accessory);

        Vector3 _towerPos1;
        Vector3 _towerPos2;
        Vector3 _towerPos3;
        Vector3 _towerPos4;
        if (Act2CirclePosition % 2 == 0)    // 上下钢铁
        {
            Act2Sol.isLRSafeFirst = true;
            _towerPos1 = _pos1 + (_pos1.Z < CENTER.Z ? new Vector3(-8, 0, 0) : new Vector3(8, 0, 0));
            _towerPos2 = _pos2 + (_pos2.Z < CENTER.Z ? new Vector3(-8, 0, 0) : new Vector3(8, 0, 0));
            _towerPos3 = _pos3 + (_pos3.X < CENTER.X ? new Vector3(0, 0, 8) : new Vector3(0, 0, -8));
            _towerPos4 = _pos4 + (_pos4.X < CENTER.X ? new Vector3(0, 0, 8) : new Vector3(0, 0, -8));
        }
        else
        {
            Act2Sol.isLRSafeFirst = false;
            _towerPos1 = _pos1 + (_pos1.X < CENTER.X ? new Vector3(0, 0, 8) : new Vector3(0, 0, -8));
            _towerPos2 = _pos2 + (_pos2.X < CENTER.X ? new Vector3(0, 0, 8) : new Vector3(0, 0, -8));
            _towerPos3 = _pos3 + (_pos3.Z < CENTER.Z ? new Vector3(-8, 0, 0) : new Vector3(8, 0, 0));
            _towerPos4 = _pos4 + (_pos4.Z < CENTER.Z ? new Vector3(-8, 0, 0) : new Vector3(8, 0, 0));
        }
        drawTowerRegion(_towerPos3, 0, 19000, $"踩塔3", accessory);
        drawTowerRegion(_towerPos4, 0, 19000, $"踩塔4", accessory);
        drawTowerRegion(_towerPos1, 19000, 7000, $"踩塔1", accessory);
        drawTowerRegion(_towerPos2, 19000, 7000, $"踩塔2", accessory);
    }

    [ScriptMethod(name: "本体：二运黄圈引导提示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27177"])]
    public void Act2_DarkDesign(Event @event, ScriptAccessory accessory)
    {
        for (int i = 0; i < accessory.Data.PartyList.Count(); i++)
        {
            var dp = accessory.drawCircle(accessory.Data.PartyList[i], 0, 5000, $"引导黄圈{i}");
            dp.ScaleMode = ScaleMode.ByTime;
            dp.Color = ColorHelper.colorPink.V4;
            dp.Scale = new(6f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }

    public class Act2Solution
    {
        public Vector3[] TowerPos = new Vector3[4];
        public Vector3[] CirclePos = new Vector3[4];
        public List<int> FireTargets { get; set; }
        public List<int> WindTargets { get; set; }
        public List<int> DarkTargets { get; set; }
        public bool isLRSafeFirst { get; set; }
        public int CircleCastTimes { get; set; }
        public Act2Solution()
        {
            FireTargets = new List<int> { };
            WindTargets = new List<int> { };
            DarkTargets = new List<int> { };
            Init();
        }
        public void Init()
        {
            isLRSafeFirst = false;
            FireTargets = new List<int> { };
            WindTargets = new List<int> { };
            DarkTargets = new List<int> { };
            CircleCastTimes = 0;

            TowerPos[0] = new(96, 0, 82);
            TowerPos[1] = TowerPos[0].RotatePoint(CENTER, float.Pi / 2);
            TowerPos[2] = TowerPos[0].RotatePoint(CENTER, float.Pi);
            TowerPos[3] = TowerPos[0].RotatePoint(CENTER, float.Pi / -2);

            CirclePos[0] = new(104, 0, 82);
            CirclePos[1] = CirclePos[0].RotatePoint(CENTER, float.Pi / 2);
            CirclePos[2] = CirclePos[0].RotatePoint(CENTER, float.Pi);
            CirclePos[3] = CirclePos[0].RotatePoint(CENTER, float.Pi / -2);
        }
        public string Print()
        {
            string safePosStr = $"{(isLRSafeFirst ? "先左右安全" : "先上下安全")}";

            string FireTargetsStr = string.Join(", ", FireTargets);
            string str1 = $"FireTargets: {FireTargetsStr}";

            string WindTargetsStr = string.Join(", ", WindTargets);
            string str2 = $"WindTargets: {WindTargetsStr}";

            string DarkTargetsStr = string.Join(", ", DarkTargets);
            string str3 = $"DarkTargets: {DarkTargetsStr}";

            string str = safePosStr + '\n' + str1 + '\n' + str2 + '\n' + str3;
            return str;
        }
    }

    [ScriptMethod(name: "本体：二运，头标记录", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(012[DEF])$"], userControl: false)]
    public void Act2_IconRecord(Event @event, ScriptAccessory accessory)
    {
        if (phase != P4S_Phase.Act2) return;
        var id = @event.Id();
        var tid = @event.TargetId();
        var tidx = accessory.getPlayerIdIndex(tid);

        const uint DARK = 0x012D;
        const uint FIRE = 0x012F;
        const uint WIND = 0x012E;

        if (id == DARK)
            Act2Sol.DarkTargets.Add(tidx);
        if (id == FIRE)
            Act2Sol.FireTargets.Add(tidx);
        if (id == WIND)
            Act2Sol.WindTargets.Add(tidx);
    }

    [ScriptMethod(name: "本体：二运，连线记录", eventType: EventTypeEnum.Tether, eventCondition: ["Id:00AC"], userControl: false)]
    public async void Act2_TetherRecord(Event @event, ScriptAccessory accessory)
    {
        if (phase != P4S_Phase.Act2) return;
        var sid = @event.SourceId();
        var tid = @event.TargetId();
        var sidx = accessory.getPlayerIdIndex(sid);
        var tidx = accessory.getPlayerIdIndex(tid);

        await Task.Delay(100);

        if (Act2Sol.DarkTargets.Contains(sidx) && !Act2Sol.DarkTargets.Contains(tidx))
            Act2Sol.DarkTargets.Add(tidx);
        if (Act2Sol.DarkTargets.Contains(tidx) && !Act2Sol.DarkTargets.Contains(sidx))
            Act2Sol.DarkTargets.Add(sidx);

        if (Act2Sol.FireTargets.Contains(sidx) && !Act2Sol.FireTargets.Contains(tidx))
            Act2Sol.FireTargets.Add(tidx);
        if (Act2Sol.FireTargets.Contains(tidx) && !Act2Sol.FireTargets.Contains(sidx))
            Act2Sol.FireTargets.Add(sidx);
    }

    [ScriptMethod(name: "本体：二运大圈次数记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27150"], userControl: false)]
    public void Act2_CircleCastTimeRecord(Event @event, ScriptAccessory accessory)
    {
        if (phase != P4S_Phase.Act2) return;
        Act2Sol.CircleCastTimes++;
    }

    [ScriptMethod(name: "本体：二运就位方案（逃课）", eventType: EventTypeEnum.Tether, eventCondition: ["Id:00AC"])]
    public async void Act2_SpreadSolution(Event @event, ScriptAccessory accessory)
    {
        if (phase != P4S_Phase.Act2) return;
        if (Drawn[2]) return;
        Drawn[2] = true;
        if (Act2Strategy != Act2StrategyEnum.逃课_Spread) return;

        await Task.Delay(500);
        drawSpreadDir(Act2Sol, accessory);
    }

    private static void drawSpreadDir(Act2Solution act2sol, ScriptAccessory accessory)
    {
        bool isNorth = false;
        bool isEastDown = false;
        bool isEastUp = false;
        bool isSouth = false;
        bool isWestDown = false;
        bool isWestUp = false;
        int myIndex = accessory.getMyIndex();

        if (act2sol.DarkTargets.Contains(myIndex))
            isNorth = true;
        if (act2sol.FireTargets.Contains(myIndex))
        {
            if (IbcHelper.isDps(accessory.Data.PartyList[myIndex]))
            {
                isWestDown = true;
                isEastDown = true;
            }
            else
            {
                isWestUp = true;
                isEastUp = true;
            }
        }
        if (act2sol.WindTargets.Contains(myIndex))
            isSouth = true;

        // 暗线全上，火线左右，风线全下，只标四方
        var dp = accessory.dirPos2Pos(CENTER, CENTER.ExtendPoint(0, 20), 0, 10000, $"北指路");
        dp.Color = isNorth ? posColorPlayer.V4.WithW(2f) : posColorNormal.V4;
        dp.Scale = new(2f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);

        dp = accessory.dirPos2Pos(CENTER, CENTER.ExtendPoint(float.Pi, 20), 0, 10000, $"南指路");
        dp.Color = isSouth ? posColorPlayer.V4.WithW(2f) : posColorNormal.V4;
        dp.Scale = new(2f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);

        dp = accessory.dirPos2Pos(CENTER, CENTER.ExtendPoint(float.Pi * 0.42f, 20), 0, 10000, $"东上指路");
        dp.Color = isEastUp ? posColorPlayer.V4.WithW(2f) : posColorNormal.V4;
        dp.Scale = new(2f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);

        dp = accessory.dirPos2Pos(CENTER, CENTER.ExtendPoint(float.Pi * -0.42f, 20), 0, 10000, $"西上指路");
        dp.Color = isWestUp ? posColorPlayer.V4.WithW(2f) : posColorNormal.V4;
        dp.Scale = new(2f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);

        dp = accessory.dirPos2Pos(CENTER, CENTER.ExtendPoint(float.Pi * 0.58f, 20), 0, 10000, $"东下指路");
        dp.Color = isEastDown ? posColorPlayer.V4.WithW(2f) : posColorNormal.V4;
        dp.Scale = new(2f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);

        dp = accessory.dirPos2Pos(CENTER, CENTER.ExtendPoint(float.Pi * -0.58f, 20), 0, 10000, $"西下指路");
        dp.Color = isWestDown ? posColorPlayer.V4.WithW(2f) : posColorNormal.V4;
        dp.Scale = new(2f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
    }

    [ScriptMethod(name: "本体：二运就位方案（正攻）第一步", eventType: EventTypeEnum.Tether, eventCondition: ["Id:00AC"])]
    public async void Act2_RegularSolutionFirst(Event @event, ScriptAccessory accessory)
    {
        if (phase != P4S_Phase.Act2) return;
        if (Drawn[3]) return;
        Drawn[3] = true;
        if (Act2Strategy != Act2StrategyEnum.正攻_Regular) return;

        await Task.Delay(500);

        var myIndex = accessory.getMyIndex();
        if (Act2Sol.DarkTargets.Contains(myIndex))
        {
            // Dark两人在外放圈拉线
            // 等待黄圈被放下（StartCasting黑暗设计圈）
            drawDarkTargetRouteFirst(Act2Sol, myIndex, false, accessory);
            drawDarkTargetTowerFirst(Act2Sol, myIndex, true, accessory);
        }
        else
        {
            // 其他人中间
            var dp = accessory.dirPos(CENTER, 0, 10000, $"二运其他1{myIndex}");
            accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
            if (Act2Sol.FireTargets.Contains(myIndex))
                drawFireTargetStackFirst(Act2Sol, myIndex, true, accessory);
            if (Act2Sol.WindTargets.Contains(myIndex))
                drawWindTargetStackFirst(Act2Sol, myIndex, true, accessory);
        }
    }

    private static void drawDarkTargetRouteFirst(Act2Solution act2sol, int myIndex, bool isPreparing, ScriptAccessory accessory)
    {
        Vector3 UPLEFT = CENTER.ExtendPoint((act2sol.isLRSafeFirst ? 45f : -45f).angle2Rad(), 15);
        float _rot_radian;
        var _myid = accessory.Data.Me;
        if (IbcHelper.isTank(_myid))
            _rot_radian = 0;
        else
            _rot_radian = float.Pi;
        var spos = UPLEFT.RotatePoint(CENTER, _rot_radian);
        // 如果是Tank，左上；否则右下
        var dp = accessory.dirPos(spos, 0, 10000, $"二运暗1{isPreparing}{myIndex}");
        dp.Color = isPreparing ? accessory.Data.DefaultDangerColor : accessory.Data.DefaultSafeColor;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }
    private static void drawDarkTargetTowerFirst(Act2Solution act2sol, int myIndex, bool isPreparing, ScriptAccessory accessory)
    {
        int _tower_idx;
        var _myid = accessory.Data.Me;
        if (IbcHelper.isTank(_myid))
            _tower_idx = 0;
        else
            _tower_idx = 2;
        if (act2sol.isLRSafeFirst)
            _tower_idx++;

        var dp = accessory.dirPos(act2sol.TowerPos[_tower_idx], 0, 10000, $"二运暗塔1{isPreparing}{myIndex}");
        dp.Color = isPreparing ? accessory.Data.DefaultDangerColor : accessory.Data.DefaultSafeColor;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    private static void drawFireTargetStackFirst(Act2Solution act2sol, int myIndex, bool isPreparing, ScriptAccessory accessory)
    {
        int _circle_idx;
        var _myid = accessory.Data.Me;

        if (!IbcHelper.isDps(_myid))
        {
            if (IbcHelper.isTank(_myid))
                _circle_idx = 0;
            else
                _circle_idx = 2;
        }
        else
            _circle_idx = 0;

        if (act2sol.isLRSafeFirst)
            _circle_idx++;

        var dp = accessory.dirPos(act2sol.CirclePos[_circle_idx], 0, 10000, $"二运火分摊1{isPreparing}{myIndex}");
        dp.Color = isPreparing ? accessory.Data.DefaultDangerColor : accessory.Data.DefaultSafeColor;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    private static void drawWindTargetStackFirst(Act2Solution act2sol, int myIndex, bool isPreparing, ScriptAccessory accessory)
    {
        int _circle_idx = 2;
        var _myid = accessory.Data.Me;

        if (act2sol.isLRSafeFirst)
            _circle_idx++;

        var dp = accessory.dirPos(act2sol.CirclePos[_circle_idx], 0, 10000, $"二运风分摊1{isPreparing}{myIndex}");
        dp.Color = isPreparing ? accessory.Data.DefaultDangerColor : accessory.Data.DefaultSafeColor;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    [ScriptMethod(name: "本体：二运就位方案（正攻）第二步", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:27178"])]
    public void Act2_RegularSolutionSecond(Event @event, ScriptAccessory accessory)
    {
        if (phase != P4S_Phase.Act2) return;
        if (Drawn[4]) return;
        Drawn[4] = true;
        if (Act2Strategy != Act2StrategyEnum.正攻_Regular) return;

        accessory.Method.RemoveDraw($"^(二运火分摊1{true}.*)$");
        accessory.Method.RemoveDraw($"^(二运风分摊1{true}.*)$");
        accessory.Method.RemoveDraw($"^(二运暗1{false}.*)$");
        accessory.Method.RemoveDraw($"^(二运暗塔1{true}.*)$");
        accessory.Method.RemoveDraw($"^(二运其他1.*)$");

        var myIndex = accessory.getMyIndex();
        if (Act2Sol.DarkTargets.Contains(myIndex))
        {
            drawDarkTargetTowerFirst(Act2Sol, myIndex, false, accessory);
            // TN分情况踩塔分摊
            drawDarkTargetRouteSecond(Act2Sol, myIndex, true, accessory);
        }
        else
        {
            if (Act2Sol.FireTargets.Contains(myIndex))
            {
                drawFireTargetStackFirst(Act2Sol, myIndex, false, accessory);
                // 四个火分情况
                drawFireTargetRouteSecond(Act2Sol, myIndex, true, accessory);
            }
            if (Act2Sol.WindTargets.Contains(myIndex))
            {
                drawWindTargetStackFirst(Act2Sol, myIndex, false, accessory);
                drawWindTargetStackSecond(Act2Sol, myIndex, true, accessory);
            }
        }
    }
    private static void drawDarkTargetRouteSecond(Act2Solution act2sol, int myIndex, bool isPreparing, ScriptAccessory accessory)
    {
        // 暗TH在右汇合，H踩塔T分摊
        int _pos_idx = 1;
        var _myid = accessory.Data.Me;
        if (act2sol.isLRSafeFirst)
            _pos_idx++;

        var _target_pos = IbcHelper.isTank(_myid) ? act2sol.CirclePos[_pos_idx] : act2sol.TowerPos[_pos_idx];
        var dp = accessory.dirPos(_target_pos, 0, 10000, $"二运暗2{isPreparing}{myIndex}");
        dp.Color = isPreparing ? accessory.Data.DefaultDangerColor : accessory.Data.DefaultSafeColor;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    private static void drawWindTargetStackSecond(Act2Solution act2sol, int myIndex, bool isPreparing, ScriptAccessory accessory)
    {
        int _circle_idx = 3;
        var _myid = accessory.Data.Me;

        if (act2sol.isLRSafeFirst)
            _circle_idx = 0;

        var dp = accessory.dirPos(act2sol.CirclePos[_circle_idx], 0, 10000, $"二运风2{isPreparing}{myIndex}");
        dp.Color = isPreparing ? accessory.Data.DefaultDangerColor : accessory.Data.DefaultSafeColor;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    private static void drawFireTargetRouteSecond(Act2Solution act2sol, int myIndex, bool isPreparing, ScriptAccessory accessory)
    {
        int _pos_idx;
        var _myid = accessory.Data.Me;

        if (!IbcHelper.isDps(_myid))
        {
            // 火T在右侧分摊
            if (IbcHelper.isTank(_myid))
                _pos_idx = 1;
            // 火H在左侧踩塔
            else
                _pos_idx = 3;
        }
        else
        {
            // 优先级最低，右
            if (act2sol.FireTargets.Max() == myIndex)
                _pos_idx = 1;
            else
                _pos_idx = 3;
        }

        if (act2sol.isLRSafeFirst)
            _pos_idx = _pos_idx == 3 ? 0 : _pos_idx + 1;

        var _target_pos = IbcHelper.isHealer(_myid) ? act2sol.TowerPos[_pos_idx] : act2sol.CirclePos[_pos_idx];
        var dp = accessory.dirPos(_target_pos, 0, 10000, $"二运火2{isPreparing}{myIndex}");
        dp.Color = isPreparing ? accessory.Data.DefaultDangerColor : accessory.Data.DefaultSafeColor;
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
    }

    [ScriptMethod(name: "本体：二运就位方案（正攻）第三步", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:27150"])]
    public void Act2_RegularSolutionThird(Event @event, ScriptAccessory accessory)
    {
        if (phase != P4S_Phase.Act2) return;
        if (Drawn[5]) return;
        Drawn[5] = true;
        if (Act2Strategy != Act2StrategyEnum.正攻_Regular) return;

        accessory.Method.RemoveDraw($"^(二运火2{true}.*)$");
        accessory.Method.RemoveDraw($"^(二运风2{true}.*)$");
        accessory.Method.RemoveDraw($"^(二运暗2{true}.*)$");
        accessory.Method.RemoveDraw($"^(二运火分摊1{false}.*)$");
        accessory.Method.RemoveDraw($"^(二运风分摊1{false}.*)$");
        accessory.Method.RemoveDraw($"^(二运暗塔1{false}.*)$");

        var myIndex = accessory.getMyIndex();
        if (Act2Sol.DarkTargets.Contains(myIndex))
        {
            drawDarkTargetRouteSecond(Act2Sol, myIndex, false, accessory);
        }
        else
        {
            if (Act2Sol.FireTargets.Contains(myIndex))
            {
                drawFireTargetRouteSecond(Act2Sol, myIndex, false, accessory);
            }
            if (Act2Sol.WindTargets.Contains(myIndex))
            {
                drawWindTargetStackSecond(Act2Sol, myIndex, false, accessory);
            }
        }
    }

    [ScriptMethod(name: "本体：二运就位方案删除", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:27150"], userControl: false)]
    public void Act2_RegularSolutionRemove(Event @event, ScriptAccessory accessory)
    {
        if (phase != P4S_Phase.Act2) return;
        if (Act2Sol.CircleCastTimes < 4) return;
        accessory.Method.RemoveDraw($".*");
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

    public static bool isTank(uint id)
    {
        var chara = GetById(id);
        if (chara == null) return false;
        return chara.GetRole() == CombatRole.Tank;
    }
    public static bool isHealer(uint id)
    {
        var chara = GetById(id);
        if (chara == null) return false;
        return chara.GetRole() == CombatRole.Healer;
    }
    public static bool isDps(uint id)
    {
        var chara = GetById(id);
        if (chara == null) return false;
        return chara.GetRole() == CombatRole.DPS;
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
    public static float BaseInnGame2DirRad(this float radian)
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
    public static float BaseDirRad2InnGame(this float radian)
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
    public static int DirRadRoundToDirs(this float radian, int dirs)
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
    public static int PositionFloorToDirs(this Vector3 point, Vector3 center, int dirs)
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
    public static int PositionRoundToDirs(this Vector3 point, Vector3 center, int dirs)
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
    public static float angle2Rad(this float angle)
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
    public static Vector3 RotatePoint(this Vector3 point, Vector3 center, float radian)
    {
        // 围绕某点顺时针旋转某弧度
        Vector2 v2 = new(point.X - center.X, point.Z - center.Z);
        var rot = MathF.PI - MathF.Atan2(v2.X, v2.Y) + radian;
        var length = v2.Length();
        return new(center.X + MathF.Sin(rot) * length, center.Y, center.Z - MathF.Cos(rot) * length);

        // TODO 另一种方案待验证
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
        return new(center.X + MathF.Sin(radian) * length, center.Y, center.Z - MathF.Cos(radian) * length);
    }

    /// <summary>
    /// 寻找外侧某点到中心的逻辑基弧度
    /// </summary>
    /// <param name="center">中心</param>
    /// <param name="new_point">外侧点</param>
    /// <returns>外侧点到中心的逻辑基弧度</returns>
    public static float FindRadian(this Vector3 new_point, Vector3 center)
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
    public static Vector3 FoldPointLR(this Vector3 point, int centerx)
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
    public static Vector3 FoldPointUD(this Vector3 point, int centerz)
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
    public static int getPlayerIdIndex(this ScriptAccessory accessory, uint pid)
    {
        // 获得玩家 IDX
        return accessory.Data.PartyList.IndexOf(pid);
    }

    /// <summary>
    /// 获得主视角玩家对应的位置index
    /// </summary>
    /// <param name="accessory"></param>
    /// <returns>主视角玩家对应的位置index</returns>
    public static int getMyIndex(this ScriptAccessory accessory)
    {
        return accessory.Data.PartyList.IndexOf(accessory.Data.Me);
    }

    /// <summary>
    /// 输入玩家dataid，获得对应的位置称呼，输出字符仅作文字输出用
    /// </summary>
    /// <param name="pid">玩家SourceId</param>
    /// <param name="accessory"></param>
    /// <returns>该玩家对应的位置称呼</returns>
    public static string getPlayerJobByID(this ScriptAccessory accessory, uint pid)
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
    /// <param name="accessory"></param>
    /// <returns></returns>
    public static string getPlayerJobByIndex(this ScriptAccessory accessory, int idx)
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
    public static DrawPropertiesEdit dirPos(this ScriptAccessory accessory, Vector3 target_pos, int delay, int destoryAt, string name)
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
    public static DrawPropertiesEdit dirPos2Pos(this ScriptAccessory accessory, Vector3 start_pos, Vector3 target_pos, int delay, int destoryAt, string name)
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
    public static DrawPropertiesEdit dirTarget(this ScriptAccessory accessory, uint target_id, int delay, int destoryAt, string name)
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
    public static DrawPropertiesEdit drawTargetOrder(this ScriptAccessory accessory, uint owner_id, uint order_idx, int delay, int destoryAt, string name)
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
    public static DrawPropertiesEdit drawCenterOrder(this ScriptAccessory accessory, uint owner_id, uint order_idx, int delay, int destoryAt, string name)
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
    public static DrawPropertiesEdit drawOwner2Target(this ScriptAccessory accessory, uint owner_id, uint target_id, int delay, int destoryAt, string name)
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
    public static DrawPropertiesEdit drawCircle(this ScriptAccessory accessory, uint owner_id, int delay, int destoryAt, string name)
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
    public static DrawPropertiesEdit drawStatic(this ScriptAccessory accessory, Vector3 center, float radian, int delay, int destoryAt, string name)
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

#region 测试区
// --------------------------------------
public static class ClassTest
{
    public static int Counting = 0;

    public static void classTestMethod(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.SendChat($"/e counting: {Counting++}");
    }
}

// 事件调用
// [ScriptMethod(name: "Test Class", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:133"])]
// public void StartCasting(Event @event, ScriptAccessory accessory) => ClassTest.classTestMethod(@event, accessory);

// List内数据打印
// string rotateDirStr = string.Join(", ", RotateCircleDir);
// DebugMsg(rotateDirStr, accessory);

// CLASS
public class Blade
{
    public UInt32 Id { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public double Rotation { get; set; }
    public Blade(UInt32 id, double x, double y, double rotation)
    {
        Id = id;
        X = x;
        Y = y;
        Rotation = rotation;
    }
}

// ConcurrentBag 和 List 的区别：
// Thread Safety: ConcurrentBag is designed to be used in multi-threaded scenarios.
// It allows multiple threads to add and remove items concurrently

// private ConcurrentBag<Blade> blades = new ConcurrentBag<Blade>();
// blades.Add(new Blade(
//     id: Convert.ToUInt32(@event["SourceId"], 16),
//     x: Convert.ToDouble(pos.X),
//     y: Convert.ToDouble(pos.Z),
//     rotation: Convert.ToDouble(@event["SourceRotation"])
// ));

#endregion
