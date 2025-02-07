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

namespace UsamisKodakku.Scripts._06_EndWalker.AAI;

[ScriptType(name: Name, territorys: [1179, 1180], guid: "e664908f-4d38-4709-938d-0cced05642f1", 
    version: Version, author: "Usami", note: NoteStr)]

// ^(?!.*((武僧|机工士|龙骑士|武士|忍者|蝰蛇剑士|钐镰客|舞者|吟游诗人|占星术士|贤者|学者|(朝日|夕月)小仙女|炽天使|白魔法师|战士|骑士|暗黑骑士|绝枪战士|绘灵法师|黑魔法师|青魔法师|召唤师|宝石兽|亚灵神巴哈姆特|亚灵神不死鸟|迦楼罗之灵|泰坦之灵|伊弗利特之灵|后式自走人偶)\] (Used|Cast))).*35501.*$
public class Aai
{
    const string NoteStr =
    """
    v0.0.0.0
    测试。
    """;

    private const string Name = "AAI [异闻阿罗阿罗岛]";
    private const string Version = "0.0.0.0";
    private const string DebugVersion = "a";
    private const string Note = "";
    
    [UserSetting("Debug模式，非开发用请关闭")]
    public static bool DebugMode { get; set; } = false;
    [UserSetting("站位提示圈绘图-普通颜色")]
    public static ScriptColor PosColorNormal { get; set; } = new ScriptColor { V4 = new Vector4(1.0f, 1.0f, 1.0f, 1.0f) };
    [UserSetting("站位提示圈绘图-玩家站位颜色")]
    public static ScriptColor PosColorPlayer { get; set; } = new ScriptColor { V4 = new Vector4(0.0f, 1.0f, 1.0f, 1.0f) };
    
    public enum AaiPhase
    {
        Init,                       // 初始
        Boss1_Crystal_1,            // Boss1 水晶一
        Boss1_Crystal_2,            // Boss1 水晶二
        Boss1_Crystal_3,            // Boss1 水晶三
        Boss1_Crystal_4,            // Boss1 水晶四
    }
    
    private readonly Vector3 _centerBoss1 = new(0f, 0f, 0f);
    
    private AaiPhase _phase = AaiPhase.Init;                            // 阶段记录
    private List<bool> _drawn = new bool[20].ToList();                  // 绘图记录
    
    private List<bool> _boss1BubbleDontMove = new bool[4].ToList();     // Boss1气泡Buff是止步
    private bool _boss1StackLast = false;                               // Boss1水瀑后分摊
    private List<Boss1Crystal> _boss1Crystal = [];                      // Boss1水晶属性
    
    public void Init(ScriptAccessory accessory)
    {
        DebugMsg($"Init {Name} v{Version}{DebugVersion} Success.\n{Note}", accessory);
        accessory.Method.MarkClear();
        accessory.Method.RemoveDraw(".*");
        
        _phase = AaiPhase.Init;                         // 阶段记录
        _drawn = new bool[20].ToList();                 // 绘图记录
        _boss1BubbleDontMove = new bool[4].ToList();    // Boss1气泡Buff是止步
        _boss1StackLast = false;                        // Boss1水瀑后分摊
        _boss1Crystal = [];                             // Boss1水晶属性
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
        // ---- DEBUG CODE ----

        string a = string.Join(", ", _boss1BubbleDontMove);
        DebugMsg($"{a}", accessory);

        List<int> b = [1, 2, 3];
        DebugMsg($"{string.Join(",", b)}", accessory);

        // -- DEBUG CODE END --
    }

    #region Mob1
    [ScriptMethod(name: "Mob1：龙卷", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:16590"])]
    public void Mob1_Twister(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        
        var dp = accessory.DrawCircle(sid, 6f, 0, 99999, $"龙卷{sid}", false);
        dp.Color = ColorHelper.ColorDark.V4.WithW(1.5f);
        accessory.Method.SendDraw(0, DrawTypeEnum.Circle, dp);
        
        var dp0 = accessory.DrawRect(sid, 1.5f, 6f, 0, 99999, $"龙卷方向{sid}", false);
        dp0.Color = ColorHelper.ColorDark.V4.WithW(1.5f);
        accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp0);
    }
    
    [ScriptMethod(name: "Mob1：龙卷移除", eventType: EventTypeEnum.RemoveCombatant, eventCondition: ["DataId:16590"])]
    public void Mob1_TwisterRemove(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw($"龙卷.*");
        accessory.Method.RemoveDraw($"龙卷方向.*");
    }
    
    [ScriptMethod(name: "Mob1：螃蟹 泡泡吐息", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(357(69|86))$"])]
    public void Mob1_CrabFrontCleave(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var dp = accessory.DrawFrontBackCleave(sid, true, 0, 5000, $"泡泡吐息{sid}", float.Pi / 2, 9);
        accessory.Method.SendDraw(0, DrawTypeEnum.Fan, dp);
    }
    
    [ScriptMethod(name: "Mob1：螃蟹 蟹甲流", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(357(70|87))$"])]
    public void Mob1_CrabBackCleave(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var dp = accessory.DrawFrontBackCleave(sid, false, 0, 1500, $"蟹甲流{sid}", float.Pi / 3 * 2, 6);
        accessory.Method.SendDraw(0, DrawTypeEnum.Fan, dp);
    }
    
    [ScriptMethod(name: "Mob1：风筝 水化炮", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(35(915|773))$"])]
    public void Mob1_KiteCannon(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var dp = accessory.DrawRect(sid, 6, 15, 0, 5000, $"水化炮{sid}");
        accessory.Method.SendDraw(0, DrawTypeEnum.Fan, dp);
    }    
    
    [ScriptMethod(name: "Mob1：风筝 钢铁", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(35(775|790))$"])]
    public void Mob1_KiteChariot(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var dp = accessory.DrawCircle(sid, 8, 0, 5000, $"驱逐{sid}");
        accessory.Method.SendDraw(0, DrawTypeEnum.Circle, dp);
    }
    
    [ScriptMethod(name: "Mob1：鬼鱼", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(35(793|941))$"])]
    public void Mob1_GhostFishTarget(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var sidx = accessory.GetPlayerIdIndex(sid);
        var myIndex = accessory.GetMyIndex();
        if (sidx != myIndex) return;
        accessory.Method.TextInfo($"防击退", 5000, true);
    }
    #endregion

    #region Boss1 泡泡鱼
    
    [ScriptMethod(name: "Boss1：阶段记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^()$"], userControl: false)]
    public void Boss1_PhaseRecord(Event @event, ScriptAccessory accessory)
    {
        // TODO 读条“水晶”切阶段
        _phase = _phase switch
        {
            AaiPhase.Init => AaiPhase.Boss1_Crystal_1,
            AaiPhase.Boss1_Crystal_1 => AaiPhase.Boss1_Crystal_2,
            AaiPhase.Boss1_Crystal_2 => AaiPhase.Boss1_Crystal_3,
            AaiPhase.Boss1_Crystal_3 => AaiPhase.Boss1_Crystal_4,
            _ => AaiPhase.Boss1_Crystal_1
        };
        _boss1Crystal.Clear();
    }
    
    public class Boss1Crystal(uint id, int[] pos, bool isHorizontal, int quarter)
    {
        public int[] Pos { get; set; } = pos;
        public bool Horizontal { get; set; } = isHorizontal;
        public uint Id { get; set; } = id;
        public int Quarter { get; set; } = quarter;
        
        /// <summary>
        /// 获得水晶对应危险区
        /// </summary>
        /// <returns></returns>
        public List<int[]> FindDangerPos()
        {
            List<int[]> dangerPos = [];
            for (var i = 0; i < 4; i++)
                dangerPos.Add(Horizontal ? [i, Pos[1]] : [Pos[0], i]);
            return dangerPos;
        }
        
        /// <summary>
        /// 获得经象限偏置后的行列坐标
        /// </summary>
        /// <returns></returns>
        private int[] FindBiasPosInQuarter()
        {
            int[] dPos = Quarter switch
            {
                0 => [0, 0],
                1 => [0, 2],
                2 => [2, 2],
                3 => [2, 0],
                _ => [0, 0]
            };
            int[] biasPos = [Pos[0] - dPos[0], Pos[1] - dPos[1]];
            return biasPos;
        }

        /// <summary>
        /// 获得行列坐标的对角坐标
        /// </summary>
        /// <returns></returns>
        public int[] FindDiagPos()
        {
            var biasPos = FindBiasPosInQuarter();
            int[] biasDiagPos = [biasPos[0] == 0 ? 1 : 0, biasPos[1] == 0 ? 1 : 0];
            var diagPos = ReturnRealPosInQuarter(biasDiagPos);
            return diagPos;
        }

        /// <summary>
        /// 获得水晶靠近短边一侧坐标
        /// </summary>
        /// <returns></returns>
        public int[] FindShortEdgeNearPos()
        {
            var biasPos = FindBiasPosInQuarter();
            if (Horizontal)
                biasPos[1] = (biasPos[1] + 1) % 2;
            else
                biasPos[0] = (biasPos[0] + 1) % 2;
            var realPos = ReturnRealPosInQuarter(biasPos);
            return realPos;
        }
        
        /// <summary>
        /// 将象限偏置后的行列坐标返回为真实坐标
        /// </summary>
        /// <param name="biasPos">经象限偏置后的行列坐标</param>
        /// <returns></returns>
        private int[] ReturnRealPosInQuarter(int[] biasPos)
        {
            int[] dPos = Quarter switch
            {
                0 => [0, 0],
                1 => [0, 2],
                2 => [2, 2],
                3 => [2, 0],
                _ => [0, 0]
            };
            int[] realPos = [biasPos[0] + dPos[0], biasPos[1] + dPos[1]];
            return realPos;
        }

        public bool LocatedAtUp()
        {
            return Quarter < 2;
        }

        public bool LocatedAtLeft()
        {
            return Quarter is 0 or 3;
        }

        public bool LocatedInside()
        {
            return !(Pos.Contains(0) || Pos.Contains(3));
        }

        /// <summary>
        /// 寻找对应水晶的安全位置
        /// 该函数仅适用于一水晶机制，且需对应水晶位于场中（即LocatedInside）
        /// </summary>
        /// <returns></returns>
        public List<int[]> FindCrystalSafePos()
        {
            List<int[]> safePos = [];
            var row = Pos[0];
            var col = Pos[1];
            if (Horizontal)
            {
                safePos.Add([0, col - 1]);
                safePos.Add([3, col - 1]);
                safePos.Add([0, col + 1]);
                safePos.Add([3, col + 1]);
            }
            else
            {
                safePos.Add([row + 1, 0]);
                safePos.Add([row + 1, 3]);
                safePos.Add([row - 1, 0]);
                safePos.Add([row - 1, 3]);
            }
            return safePos;
        }
        
        /// <summary>
        /// 找到横水晶象限内（一麻）的安全区
        /// 该函数仅适用于一水晶机制，且需对应水晶位于场中（即LocatedInside）
        /// </summary>
        /// <returns></returns>
        public List<int[]> FindHorizonCrystalSafePos()
        {
            List<int[]> horizonCrystalSafePos = [];
            var safePos = FindCrystalSafePos();
            foreach (var pos in safePos)
            {
                var quarter = FindPositionQuarter(pos);
                if (Horizontal)
                {
                    // 如果我是横水晶，那quarter的奇偶性与我的一致即可
                    if (Math.Abs(quarter - Quarter) % 2 == 0)
                        horizonCrystalSafePos.Add(pos);
                }
                else
                {
                    // 如果我是竖水晶，那quarter的奇偶性需与我不一致
                    if (Math.Abs(quarter - Quarter) % 2 == 1)
                        horizonCrystalSafePos.Add(pos);
                }
            }
            return horizonCrystalSafePos;
        }
        
        /// <summary>
        /// 找到竖水晶象限内（二麻）的安全区
        /// 该函数仅适用于一水晶机制，且需对应水晶位于场中（即LocatedInside）
        /// </summary>
        /// <returns></returns>
        public List<int[]> FindVerticalCrystalSafePos()
        {
            List<int[]> verticalCrystalSafePos = [];
            var safePos = FindCrystalSafePos();
            foreach (var pos in safePos)
            {
                var quarter = FindPositionQuarter(pos);
                if (!Horizontal)
                {
                    // 如果我是竖水晶，那quarter的奇偶性与我的一致即可
                    if (Math.Abs(quarter - Quarter) % 2 == 0)
                        verticalCrystalSafePos.Add(pos);
                }
                else
                {
                    // 如果我是横水晶，那quarter的奇偶性需与我不一致
                    if (Math.Abs(quarter - Quarter) % 2 == 1)
                        verticalCrystalSafePos.Add(pos);
                }
            }
            return verticalCrystalSafePos;
        }

        /// <summary>
        /// 根据水晶所在位置寻找初始预站位位置
        /// 该函数仅适用于一水晶机制，且需对应水晶位于场中（即LocatedInside）
        /// </summary>
        /// <returns></returns>
        public List<int[]> FindEdgePos()
        {
            return Horizontal ? [[Pos[0], 0], [Pos[0], 3]] : [[0, Pos[1]], [3, Pos[1]]];
        }
    }
    
    [ScriptMethod(name: "Boss1：水晶状态记录", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(1654[29])$"], userControl: false)]
    public void Boss1_CrystalPosRecord(Event @event, ScriptAccessory accessory)
    {
        var sid = @event.SourceId();
        var spos = @event.SourcePosition();
        var crystalPos = FindCrystalPosition(spos);
        var srot = @event.SourceRotation();
        var isHorizontal = srot.Rad2Dirs(4) % 2 == 0;
        var quarter = FindPositionQuarter(crystalPos);
        _boss1Crystal.Add(new Boss1Crystal(sid, crystalPos, isHorizontal, quarter));
    }
    
    [ScriptMethod(name: "Boss1：水晶安全区绘图", eventType: EventTypeEnum.AddCombatant, eventCondition: ["DataId:regex:^(1654[29])$"])]
    public void Boss1_CrystalRect(Event @event, ScriptAccessory accessory)
    {
        if (_phase != AaiPhase.Boss1_Crystal_1) return;
        
        var sid = @event.SourceId();
        var spos = @event.SourcePosition();
        var crystalPos = FindCrystalPosition(spos);
        var srot = @event.SourceRotation();
        var isHorizontal = srot.Rad2Dirs(4) % 2 == 0;
        var quarter = FindPositionQuarter(crystalPos);
        
        var crystal = new Boss1Crystal(sid, crystalPos, isHorizontal, quarter);
        if (!crystal.LocatedInside()) return;
        
        DebugMsg($"找到了场中的水晶({crystal.Pos[0]}行,{crystal.Pos[1]}列)", accessory);
        var safePosIdxs = crystal.FindCrystalSafePos();
        foreach (var pos in safePosIdxs)
        {
            DebugMsg($"画出安全区{pos[0]}行,{pos[1]}列)", accessory);
            DrawSpecificSquare(pos, accessory.Data.DefaultSafeColor, accessory);
        }
    }

    /// <summary>
    /// 返回水晶位置
    /// </summary>
    /// <param name="pos">Vector3坐标</param>
    /// <returns>[row, col]，从0开始</returns>
    private int[] FindCrystalPosition(Vector3 pos)
    {
        var row = (int)Math.Floor((pos.Z + 16) / 10);
        var col = (int)Math.Floor((pos.X + 16) / 10);
        return [row, col];
    }

    /// <summary>
    /// 返回输入行列对应的Vector3坐标中心
    /// </summary>
    /// <param name="pos">行列坐标位置</param>
    /// <returns></returns>
    private Vector3 Position2Vector3Center(int[] pos)
    {
        return new Vector3(pos[1] * 10 - 15f, _centerBoss1.Y, pos[0] * 10 - 15f);
    }

    /// <summary>
    /// 输入坐标绘出对应方格安全区
    /// </summary>
    /// <param name="squarePos">对应方格所在行与列</param>
    /// <param name="color"></param>
    /// <param name="accessory"></param>
    private void DrawSpecificSquare(int[] squarePos, Vector4 color, ScriptAccessory accessory)
    {
        var row = squarePos[0];
        var col = squarePos[1];
        var safeVec3 = Position2Vector3Center([row, col]);
        var dp = accessory.DrawStatic(safeVec3, 0, 0, 10, 10, 0, 10000, $"水晶安全{row}{col}");
        dp.Color = color;
        accessory.Method.SendDraw(0, DrawTypeEnum.Straight, dp);
    }

    /// <summary>
    /// 输入坐标得到对应方格象限，左上为0顺时针增加
    /// </summary>
    /// <param name="squarePos">对应方格所在行与列</param>
    /// <returns></returns>
    private static int FindPositionQuarter(int[] squarePos)
    {
        // 以左上为0，左上-右上-右下-左下顺时针
        var radian = MathF.Atan2( 1.5f - squarePos[0], 1.5f - squarePos[1]);
        radian = radian < 0 ? radian + float.Pi * 2 : radian;
        var dirs = 4;
        var quarter = Math.Floor(radian / (float.Pi * 2 / dirs));
        return (int)quarter;
    }

    /// <summary>
    /// 输入吹风坐标得到对应方格象限，左上为0顺时针增加
    /// </summary>
    /// <param name="windPos">吹风马甲所在坐标</param>
    /// <returns></returns>
    private int FindWindQuarter(Vector3 windPos)
    {
        // 以左上为0，左上-右上-右下-左下顺时针
        var quarter = windPos.Position2Dirs(_centerBoss1, 4, false);
        quarter = (quarter + 1) % 4;
        return quarter;
    }
    
    [ScriptMethod(name: "Boss1：泡泡Buff记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^(3743|3788)$"], userControl: false)]
    public void Boss1_BubbleBuffRecord(Event @event, ScriptAccessory accessory)
    {
        const uint dontMove = 3788;
        const uint floatUp = 3743;
        var tid = @event.TargetId();
        var tidx = accessory.GetPlayerIdIndex(tid);
        var stid = @event.StatusId();
        _boss1BubbleDontMove[tidx] = stid == dontMove;
    }
    
    [ScriptMethod(name: "Boss1：分摊分散记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:regex:^()$"], userControl: false)]
    public void Boss1_StackSpreadRecord(Event @event, ScriptAccessory accessory)
    {
        const uint stack = 12345;
        const uint spread = 12346;
        var stid = @event.StatusId();
        _boss1StackLast = stid == stack;
    }
    
    [ScriptMethod(name: "Boss1：一水晶站位点指示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(35505)$"])]
    public void Boss1_Crystal1PosDir(Event @event, ScriptAccessory accessory)
    {
        if (_phase != AaiPhase.Boss1_Crystal_1) return;
        
        var myIndex = accessory.GetMyIndex();
        var isDontMove = _boss1BubbleDontMove[myIndex];
        // T与D2在北
        var atWestOrNorth = myIndex is 0 or 3;
        List<int[]> safePos = [];
        
        // 针对每块水晶寻找安全区
        foreach (var crystal in _boss1Crystal)
        {
            if (!isDontMove)
            {
                // 是泡泡，找竖水晶对角或横水晶旁边
                switch (atWestOrNorth, crystal.LocatedAtUp())
                {
                    case (true, false):
                    case (false, true):
                        break;
                    default:
                        safePos.Add(crystal.Horizontal ? crystal.FindShortEdgeNearPos() : crystal.FindDiagPos());
                        break;
                }
            }
            else
            {
                if (!crystal.LocatedInside()) continue;
                
                // 横水晶1，竖水晶2
                // 是止步，在安全区准备移动
                // 一水晶只有分摊或分散
                DebugMsg($"是{(_boss1StackLast ? "分摊" : "分散")}，需要找{(_boss1StackLast ? "横" : "竖")}水晶象限的安全区", accessory);
                var tempSafePos = _boss1StackLast ? crystal.FindHorizonCrystalSafePos() : crystal.FindVerticalCrystalSafePos();
                DebugMsg($"找到了待选择的安全区，{string.Join(",",tempSafePos[0])}与{string.Join(",",tempSafePos[1])}", accessory);
                var tempReadyPosList = crystal.FindEdgePos();
                DebugMsg($"找到了水晶边缘，{string.Join(",",tempReadyPosList[0])}与{string.Join(",",tempReadyPosList[1])}", accessory);
                
                // 找到本职就位的位置
                var tempReadyPos = atWestOrNorth
                    ? tempReadyPosList[0].Contains(0)
                        ? tempReadyPosList[0]
                        : tempReadyPosList[1]
                    : tempReadyPosList[0].Contains(3)
                        ? tempReadyPosList[0]
                        : tempReadyPosList[1];
                DebugMsg($"找到了本职先预站位的位置，{tempReadyPos}", accessory);
                foreach (var pos in tempSafePos)
                {
                    // 如果安全位置与就位位置只差1格，那就是安全区
                    if (!IsBeside(pos, tempReadyPos)) continue;
                    safePos.Add(pos);
                    DebugMsg($"找到了本职最终的安全位置，{safePos}", accessory);
                }
            }
        }
        foreach (var pos in safePos)
            DrawSpecificSquare(pos, PosColorPlayer.V4.WithW(2f), accessory);
    }

    private bool IsBeside(int[] pos1, int[] pos2)
    {
        return Math.Abs(pos1[0] - pos2[0]) + Math.Abs(pos1[1] - pos2[1]) == 1;
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
        r %= float.Pi * 2;
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
        double dirsDouble = dirs;
        var r = diagDivision
            ? Math.Round(radian / (2f / dirsDouble * float.Pi))
            : Math.Floor(radian / (2f / dirsDouble * float.Pi));
        r %= dirs;
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

}

public static class AssignDp
{
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
        dp.Rotation = isFrontCleave ? 0 : float.Pi;
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
    public static DrawPropertiesEdit DrawStaticDonut(this ScriptAccessory accessory, Vector3 center, Vector4 color, int delay, int destroy, string name, float scale = 1.5f, float innerscale = 0)
    {
        var dp = accessory.DrawStatic(center, 0, 0, scale, scale, delay, destroy, name);
        dp.Color = color;
        dp.InnerScale = new Vector2(innerscale == 0 ? scale - 0.05f : innerscale);
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
}

#endregion
