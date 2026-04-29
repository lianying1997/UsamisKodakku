using System;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent.Struct;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Data;
using KodakkuAssist.Module.Draw.Manager;
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
using FFXIVClientStructs.FFXIV.Client.Graphics.Vfx;
using Lumina.Excel.Sheets;

namespace KodakkuScripts.UsamisKodakku._07_Dawntrail.UnrealShinryu;

[ScriptType(name: Name, territorys: [730], guid: "fcf45bf5-bb72-42f8-b918-4c4b779fb70c",
    version: Version, author: "Usami", note: NoteStr, updateInfo: UpdateInfo)]

/* todo
 * 1. 火焰链
 * 2. 天光轮回（需要躲开的那个）
 * 3. 水池 + 超新星，超新星未记录
 * 4. 水池 + 地狱之火焰，地狱之火焰未记录
 * 5. P3 吹雪 + 闪电，未记录
 * ++ 40540
 */

public class UnrealShinryu
{
    const string NoteStr =
        $"""
        {Version}
        初版，有机制待补全
        """;
    
    const string UpdateInfo =
        $"""
         {Version}
         初版，有机制待补全
         """;

    private const string Name = "Shinryu-Ur [神龙幻巧战]";
    private const string Version = "0.0.0.1";
    private const string DebugVersion = "a";
    private const bool Debugging = true;
    private static readonly List<string> Role = ["MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4"];
    private static readonly Vector3 Center1 = new Vector3(0, -380, 0);
    
    private ShinryuParams _shinryuParam = new ShinryuParams();

    public void Init(ScriptAccessory sa)
    {
        sa.Log.Debug($"[DEBUG] 脚本 {Name} v{Version}{DebugVersion} 完成初始化.");
        sa.Method.RemoveDraw(".*");
        _shinryuParam.Reset(sa);
    }
    
    // 攻击（左翼/右翼） 9720 50260
    [ScriptMethod(name: "判断副本", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(9720|50260)$"],
        userControl: Debugging)]
    public void 判断副本(Event ev, ScriptAccessory sa)
    {
        if (_shinryuParam.副本判断完毕) return;
        _shinryuParam.是幻巧战 = ev.ActionId != 9720;
        _shinryuParam.副本判断完毕 = true;
    }
    
    [ScriptMethod(name: "判断大地之怒", eventType: EventTypeEnum.ObjectEffect, eventCondition: ["Id1:4", "Id2:8"],
        userControl: Debugging)]
    public void 判断大地之怒(Event ev, ScriptAccessory sa)
    {
        var distance = (ev.SourcePosition - Center1).Length();
        if (distance > 2f) return;
        _shinryuParam.绿色地板裂纹 = true;
        sa.DebugMsg($"[INFO]【大地之怒】绿色地板裂纹 True");
    }
    
    // 巨浪 9690 50230
    [ScriptMethod(name: "巨浪击退", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(9690|50230)$"],
        userControl: true)]
    public void 巨浪击退(Event ev, ScriptAccessory sa)
    {
        var region = ev.SourcePosition.GetRadian(Center1).RadianToRegion(4, 0, true);
        DrawRegionKnockback(sa, region , "巨浪击退");
        sa.DebugMsg($"[INFO]【巨浪击退】源于方位 {region}");
        
        void DrawRegionKnockback(ScriptAccessory sa, int dir, string name)
        {
            var dp = sa.DrawLine(sa.Data.Me, 0, 4000, 6000, name, dir * 90f.DegToRad() + 180f.DegToRad(), 3f, 35f,
                sa.Data.DefaultDangerColor.WithW(2f), draw: false);
            dp.FixRotation = true;
            sa.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
        }
    }
    
    // 冰柱突刺 9712 50252
    [ScriptMethod(name: "冰柱突刺直线", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(9712|50252)$"],
        userControl: true)]
    public void 冰柱突刺直线(Event ev, ScriptAccessory sa)
    {
        sa.DrawRect(ev.SourceId, 0, 2500, $"冰柱突刺", 0, 10, 60, sa.Data.DefaultDangerColor.WithW(2f));
    }
    
    [ScriptMethod(name: "放尾巴", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(007E)$"],
        userControl: true)]
    public void 放尾巴(Event ev, ScriptAccessory sa)
    {
        _shinryuParam.放尾巴次数++;
        sa.DebugMsg($"[INFO]【放尾巴】当前次数：{_shinryuParam.放尾巴次数}");
        
        if (ev.TargetId != sa.Data.Me) return;
        var pos = _shinryuParam.放尾巴次数 switch
        {
            1 => "右下",
            2 => "左下",
            3 => _shinryuParam.绿色地板裂纹 ? "左上" : "中间",
            _ => ""
        };
        sa.Method.TextInfo($"{pos} 放尾巴", 4000);
        sa.Method.TTS($"{pos} 放尾巴");
    }
    
    // 尾部猛击 9698 50238
    [ScriptMethod(name: "尾部猛击", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(9698|50238)$"],
        userControl: true)]
    public void 尾部猛击(Event ev, ScriptAccessory sa)
    {
        sa.DrawRect(ev.SourcePosition, 0, 3000, $"尾部猛击", ev.SourceRotation, 20, 20, sa.Data.DefaultDangerColor.WithW(2f));
        sa.DrawRect(ev.SourcePosition, 0, 3000, $"尾部猛击", ev.SourceRotation + 180f.DegToRad(), 20, 20, sa.Data.DefaultDangerColor.WithW(2f));
    }
    
    // 闪电 9706 50246
    [ScriptMethod(name: "闪电", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(9706|50246)$"],
        userControl: true)]
    public void 闪电(Event ev, ScriptAccessory sa)
    {
        // 找到水坑
        var sinks = sa.GetByDataId(2004237);
        foreach (var sink in sinks)
            sa.DrawCircle(sink!.GameObjectId, 0, 8000, $"水坑", 5.25f, new Vector4(1, 0, 0, 5));
        
        // 玩家间分散
        foreach (var p in sa.Data.PartyList.Where(p => p != sa.Data.Me))
            sa.DrawCircle(p, 0, 8000, $"闪电分散", 5, sa.Data.DefaultDangerColor.WithW(2f), byTime: true);
    }
    
    // 吹雪 9713 50253
    [ScriptMethod(name: "吹雪 AOE 提示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(9713|50253)$"],
        userControl: true)]
    public void 吹雪(Event ev, ScriptAccessory sa)
    {
        sa.Method.TextInfo($"ＡＯＥ", 4000, true);
        sa.Method.TTS($"ＡＯＥ");
    }
    
    // 死亡轮回 9715 50255
    [ScriptMethod(name: "死亡轮回", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(9715|50255)$"],
        userControl: true)]
    public void 死亡轮回(Event ev, ScriptAccessory sa)
    {
        _shinryuParam.死亡轮回次数++;
        sa.DebugMsg($"[INFO]【死亡轮回】次数：{_shinryuParam.死亡轮回次数}");

        var myIndex = sa.GetMyIndex();
        var hintText = myIndex switch
        {
            0 or 1 => "分摊死刑",
            _ => "即将躲避 天光轮回"
        };
        sa.Method.TextInfo(hintText, 4000, true);
        sa.Method.TTS(hintText);

        var isTank = sa.Data.MyObject!.IsTank();
        sa.DrawCircle(ev.TargetId, 0, 4000, $"死亡轮回目标", 4f,
            (isTank ? sa.Data.DefaultSafeColor : sa.Data.DefaultDangerColor).WithW(2f), byTime: true);
    }
    
    // 制裁之雷 9723 50263
    [ScriptMethod(name: "制裁之雷", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(9723|50263)$"],
        userControl: true)]
    public void 制裁之雷(Event ev, ScriptAccessory sa)
    {
        var sinks = sa.GetByDataId(2004237);
        foreach (var sink in sinks)
            sa.DrawCircle(sink!.GameObjectId, 0, 8000, $"水坑", 5.25f, new Vector4(1, 0, 0, 5));
    }
    
    // 极神龙 DataId 8026
    [ScriptMethod(name: "*大地吐息", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(0028)$"],
        userControl: true)]
    public void 大地吐息(Event ev, ScriptAccessory sa)
    {
        // var bossObj = sa.GetByDataId(_shinryuParam.是幻巧战 ? 8026u : 8026u).First();
        // sa.DrawFan(bossObj!.GameObjectId, ev.TargetId, 0, 6000, $"大地吐息扇形", 60f.DegToRad(), 0f, 80f, 0f, sa.Data.DefaultDangerColor.WithW(2f));
        if (ev.TargetId != sa.Data.Me) return;
        var myIndex = sa.GetMyIndex();
        
        var pos = myIndex switch
        {
            <= 3 => "左上",
            _ => "右上"
        };
        sa.Method.TextInfo($"{pos} 引导扇形", 4000);
        sa.Method.TTS($"{pos} 引导扇形");
    }
    
    // 钻石星辰 9724 50264
    [ScriptMethod(name: "钻石星辰", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(9724|50264)$"],
        userControl: true)]
    public void 钻石星辰(Event ev, ScriptAccessory sa)
    {
        var myIndex = sa.GetMyIndex();
        sa.Method.TextInfo($"ＡＯＥ，场中集合{(myIndex == 3 ? "，即将滑冰" : "")}", 4000, true);
        sa.Method.TTS($"ＡＯＥ，场中集合{(myIndex == 3 ? "，即将滑冰" : "")}");
    }
    
    // 大气爆发 9726 50266
    [ScriptMethod(name: "大气爆发", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(9726|50266)$"],
        userControl: true)]
    public void 大气爆发(Event ev, ScriptAccessory sa)
    {
        sa.DrawKnockBack(sa.Data.Me, 0, 10000, $"大气爆发", 3f, 20f, sa.Data.DefaultDangerColor.WithW(2f));
    }
    
    // 以太射线 9752 50292
    [ScriptMethod(name: "以太射线直线", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(9752|50292)$"],
        userControl: true)]
    public void 以太射线直线(Event ev, ScriptAccessory sa)
    {
        var dp = sa.DrawRect(ev.SourceId, 0, 2500, $"以太射线", 0, 3, 100, sa.Data.DefaultDangerColor.WithW(2f), draw: false);
        sa.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Rect, dp);
    }
    
    // 万亿斩击 9803 50296
    [ScriptMethod(name: "万亿斩击", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(9803|50343)$"],
        userControl: true)]
    public void 万亿斩击(Event ev, ScriptAccessory sa)
    {
        if (!sa.Data.MyObject!.IsTank()) return;
        sa.Method.TextInfo($"死刑换Ｔ", 3000, true);
        sa.Method.TTS($"死刑换Ｔ");
    }
    
    // 超新星P3 10015 50305
    [ScriptMethod(name: "超新星（P3）", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(10015|50305)$"],
        userControl: true)]
    public void 超新星P3(Event ev, ScriptAccessory sa)
    {
        sa.Method.TextInfo($"集合，停止移动", 5000, true);
        sa.Method.TTS($"集合，停止移动");
    }
    
    // 神龙啸月环 9800 50293
    [ScriptMethod(name: "神龙啸月环", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(9800|50293)$"],
        userControl: true)]
    public void 神龙啸月环(Event ev, ScriptAccessory sa)
    {
        sa.DrawDonut(ev.SourceId, 0, 5000, $"神龙啸", 50, 10, sa.Data.DefaultDangerColor.WithW(2f));
    }

    #region 参数容器类
    private class ShinryuParams
    {
        public int 放尾巴次数 = 0;
        public bool 绿色地板裂纹 = false;
        public int 死亡轮回次数 = 0;
        public bool 是幻巧战 = false;
        public bool 副本判断完毕 = false;
        public void Reset(ScriptAccessory sa)
        {
            放尾巴次数 = 0;
            绿色地板裂纹 = false;
            死亡轮回次数 = 0;
            是幻巧战 = false;
            副本判断完毕 = false;
            
            sa.DebugMsg($"神龙参数 Reset", Debugging);
        }
    }
    
    #endregion 参数容器类

}

#region 函数集
internal static class EventExtensions
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

internal static class IbcHelper
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
    
    public static List<ulong> GetTetherSource(this ScriptAccessory sa, IBattleChara? battleChara, uint tetherId)
    {
        List<ulong> tetherSourceId = [];
        if (battleChara == null || !battleChara.IsValid()) return [];
        unsafe
        {
            BattleChara* chara = (BattleChara*)battleChara.Address;
            var tetherList = chara->Vfx.Tethers;

            foreach (var tether in tetherList)
            {
                if (tether.Id != tetherId) continue;
                tetherSourceId.Add(tether.TargetId.ObjectId);
            }
        }
        return tetherSourceId;
    }
    
    public static unsafe byte? GetTransformationId(this ScriptAccessory sa, IGameObject? obj)
    {
        if (obj == null) return null;
        Character* objStruct = (Character*)obj.Address;
        return objStruct->Timeline.ModelState;
    }
}
#region 计算函数

internal static class MathTools
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

}

#endregion 计算函数

#region 位置序列函数
internal static class IndexHelper
{
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

}
#endregion 位置序列函数

#region 绘图函数

internal static class DrawTools
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
    /// <param name="color">使用颜色</param>
    /// <param name="byTime">动画效果随时间填充</param>
    /// <param name="byY">动画效果随距离变更</param>
    /// <param name="draw">是否直接绘图</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawOwnerBase(this ScriptAccessory sa, 
        object ownerObj, object targetObj, int delay, int destroy, string name, 
        float radian, float rotation, float width, float length, float innerWidth, float innerLength,
        DrawModeEnum drawModeEnum, DrawTypeEnum drawTypeEnum, Vector4 color,
        bool byTime = false, bool byY = false, bool draw = true)
    {
        var dp = sa.Data.GetDefaultDrawProperties();
        dp.Name = name;
        dp.Scale = new Vector2(width, length);
        dp.InnerScale = new Vector2(innerWidth, innerLength);
        dp.Radian = radian;
        dp.Rotation = rotation;
        dp.Color = color;
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
    /// <param name="color">使用颜色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawGuidance(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name,
        Vector4 color, float rotation = 0, float width = 1f, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 0, rotation, width,
            width, 0, 0, DrawModeEnum.Imgui, DrawTypeEnum.Displacement, color, false, true, draw);
    
    public static DrawPropertiesEdit DrawGuidance(this ScriptAccessory sa,
        object targetObj, int delay, int destroy, string name, Vector4 color, float rotation = 0, float width = 1f,
        bool draw = true)
        => sa.DrawGuidance((ulong)sa.Data.Me, targetObj, delay, destroy, name, color, rotation, width, draw);

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
    /// <param name="color">使用颜色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawCircle(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name,
        float scale, Vector4 color, bool byTime = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, 0, delay, destroy, name, 2 * float.Pi, 0, scale, scale,
            0, 0, DrawModeEnum.Default, DrawTypeEnum.Circle, color, byTime,false, draw);

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
    /// <param name="color">使用颜色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawDonut(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name,
        float outScale, float innerScale, Vector4 color, bool byTime = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, 0, delay, destroy, name, 2 * float.Pi, 0, outScale, outScale, innerScale,
            innerScale, DrawModeEnum.Default, DrawTypeEnum.Donut, color, byTime, false, draw);
    
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
    /// <param name="color">使用颜色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawFan(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name, float radian, float rotation,
        float outScale, float innerScale, Vector4 color, bool byTime = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, radian, rotation, outScale, outScale, innerScale,
            innerScale, DrawModeEnum.Default, innerScale == 0 ? DrawTypeEnum.Fan : DrawTypeEnum.Donut, color, byTime, false, draw);

    public static DrawPropertiesEdit DrawFan(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name, float radian, float rotation,
        float outScale, float innerScale, Vector4 color, bool byTime = false, bool draw = true)
        => sa.DrawFan(ownerObj, 0, delay, destroy, name, radian, rotation, outScale, innerScale, color, byTime, draw);

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
    /// <param name="color">使用颜色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawRect(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name, float rotation,
        float width, float length, Vector4 color, bool byTime = false, bool byY = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 0, rotation, width, length, 0, 0,
            DrawModeEnum.Default, DrawTypeEnum.Rect, color, byTime, byY, draw);
    
    public static DrawPropertiesEdit DrawRect(this ScriptAccessory sa,
        object ownerObj, int delay, int destroy, string name, float rotation,
        float width, float length, Vector4 color, bool byTime = false, bool byY = false, bool draw = true)
        => sa.DrawRect(ownerObj, 0, delay, destroy, name, rotation, width, length, color, byTime, byY, draw);

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
    /// <param name="color">使用颜色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawKnockBack(this ScriptAccessory sa,
        object targetObj, int delay, int destroy, string name, float width, float length,
        Vector4 color, bool draw = true)
        => sa.DrawOwnerBase(sa.Data.Me, targetObj, delay, destroy, name, 0, float.Pi, width, length, 0, 0,
            DrawModeEnum.Default, DrawTypeEnum.Displacement, color, false, false, draw);

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
    /// <param name="color">使用颜色</param>
    /// <param name="draw">是否直接绘制</param>
    /// <returns></returns>
    public static DrawPropertiesEdit DrawLine(this ScriptAccessory sa,
        object ownerObj, object targetObj, int delay, int destroy, string name, float rotation,
        float width, float length, Vector4 color, bool byTime = false, bool byY = false, bool draw = true)
        => sa.DrawOwnerBase(ownerObj, targetObj, delay, destroy, name, 1, rotation, width, length, 0, 0,
            DrawModeEnum.Default, DrawTypeEnum.Line, color, byTime, byY, draw);

}

#endregion 绘图函数

#region 调试函数

internal static class DebugFunction
{
    public static void DebugMsg(this ScriptAccessory sa, string msg, bool enable = true, bool showInChatBox = true)
    {
        if (!enable) return;
        sa.Log.Debug(msg);
        if (!showInChatBox) return;
        sa.Method.SendChat($"/e {msg}");
    }
}

#endregion 调试函数

#endregion 函数集

