using System;
using System.Numerics;
using System.Runtime.Intrinsics.Arm;
using Dalamud.Memory.Exceptions;
using KodakkuAssist.Module.GameEvent;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using ECommons;
using Newtonsoft.Json;
using Dalamud.Utility.Numerics;
using KodakkuAssist.Data;
using KodakkuAssist.Data.PartyList;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Module.Draw;
using KodakkuAssist.Module.GameOperate;
using KodakkuAssist.Module.Draw.Manager;
using Newtonsoft.Json;
using System.Security.Cryptography;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.ComponentModel;
using System.Collections.Generic;
using System.Collections;
using System.Threading.Tasks;
using static Dalamud.Interface.Utility.Raii.ImRaii;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Reflection;
using System.Threading;
using ECommons.GameFunctions;


namespace Weapontong;

[ScriptType(guid: "0374b7ed-6f72-4fb7-9c0c-ecb9641a1aed", name: "绝神兵基础机制绘制+三连桶小队头顶标点", territorys: [777], version: "0.0.0.12", author: "RedBromine & Baelixac", note:"绝神兵绘图+三连桶小队标点" +
    "\n 三连桶点名测试请用以下的宏：" +
    "\n /e 测试三连桶标点鸭鸭")]
public class Weapontong
{
    public List<int> playerIndexList = new List<int>();//playerIndexList 三连桶读取玩家名单初始化
    public List<int> customOrder = new List<int> { 0, 1, 4, 5, 6, 7, 2, 3 };//三连桶顺序 mt st d1234 h12
    public List<uint> fengqiangtargetIds = new List<uint>();//分身风枪targetId
    public List<uint> sanyunfengqiangtargetIds = new List<uint>();//三运分身风枪targetId
    public List<int> sanyundihuolist = new List<int>();
    //public int bucketcount = 0; //让三连桶事件只触发1次
    public int windcount = 0; //让风枪事件只触发1次
    public int sanyunwindcount = 0;
    //public int sanyundihuo = 0;//让三运风枪事件只触发1次
    public bool sanliantong = true;
    public bool windstart = true;//风神开场风枪
    public bool firestart = true;
    public bool HotWind1 = true;//第一次热风
    public bool yiyun;//一运是否开始
    public bool eryun;//二运是否开始
    public bool sanyun;//三运是否开始
    //public int sanyundihuoid;//三运地火
    public bool fenshentether = true;//第一次热风
    public uint windbossid;//风神bossid
    public uint titanbossid;//泰坦bossid
    public uint miaochibossid;//妙翅bossid
    public uint meiyibossid;//妙翅bossid
    //public uint firebossid;//火神bossid
    //public uint shenbingbossid;//神兵bossid
    public bool meiyitethercheck = true;
    public bool windtethercheck1 = true;
    public bool windtethercheck3 = true;
    //public bool windtethercheck2 = false;
    public bool miaochitethercheck = true;
    private List<Vector3> huoshenzhuCoList = new List<Vector3>();//火神柱坐标list

    public void Init(ScriptAccessory accessory)
    {
        huoshenzhuCoList.Clear();
    }
    
    [UserSetting("三连桶点名玩家头顶标记")]
    public bool TitanMark { get; set; } = true;
    [UserSetting("三连桶点名测试，使用【/e 测试三连桶标点鸭鸭】进行测试")]
    public bool TitanMarkTest { get; set; } = true;
    [UserSetting("三运被点地火人头上标锁链")]
    public bool IfritMark { get; set; } = true;
    
    [ScriptMethod(name: "重置战斗检测", eventType: EventTypeEnum.CombatChanged, eventCondition: ["Type:ResetCombat"], userControl:false)]
    public void 重置战斗检测(Event @event, ScriptAccessory accessory)
    {
        sanliantong = true;
        windstart = true;//风神开场
        firestart = true;//火神开场
        HotWind1 = true;//火神第一次热风
        playerIndexList = new List<int>();//把三连桶存的名字清空
        sanyundihuolist = new List<int>();//把三连桶存的名字清空
        fengqiangtargetIds = new List<uint>();//分身风枪targetId清空
        sanyunfengqiangtargetIds = new List<uint>();//三运分身风枪targetId清空
        //bucketcount = 0;//初始化TT石牢计数
        windcount = 0;//初始化风枪计数
        sanyunwindcount = 0;//初始化三运风枪计数
        //sanyundihuo = 0;//初始化三运风枪计数
        fenshentether = true;//初始化接线时场地变换
        meiyitethercheck = true;//初始化美翅接线检测
        miaochitethercheck = true;//初始化妙翅接线检测
        huoshenzhuCoList.Clear();//初始化火神柱坐标list
        yiyun = false;
        eryun = false;
        sanyun = false;
        //sanyundihuo = 0;
        windtethercheck1 = true;
        windtethercheck3 = true;
        //windtethercheck2 = false;
        accessory.Method.RemoveDraw("");//清除所有绘制

    }
    
    [ScriptMethod(name: "P1转BOSS清除绘制", eventType: EventTypeEnum.Chat, eventCondition: ["Message:regex:^(怎……怎么可能……区区蝼蚁……)$"], userControl:false)]
    public void P1转BOSS清除绘制(Event @event, ScriptAccessory accessory)
    {
        if (@event["Sender"] == "迦楼罗")
        {
            accessory.Method.RemoveDraw("");//清除所有绘制
        }
    }
    
    [ScriptMethod(name: "P2转BOSS清除绘制", eventType: EventTypeEnum.Chat, eventCondition: ["Message:regex:^(不共戴天……\n祝福光芒……莫非命也……)$"], userControl:false)]
    public void P2转BOSS清除绘制(Event @event, ScriptAccessory accessory)
    {
        if (@event["Sender"] == "伊弗利特")
        {
            accessory.Method.RemoveDraw("");//清除所有绘制
        }
    }
    
    [ScriptMethod(name: "P3转BOSS清除绘制", eventType: EventTypeEnum.Chat, eventCondition: ["Message:regex:^(我的……孩子们……终有一日……)$"], userControl:false)]
    public void P3转BOSS清除绘制(Event @event, ScriptAccessory accessory)
    {
        if (@event["Sender"] == "泰坦")
        {
            accessory.Method.RemoveDraw("");//清除所有绘制
        }
    }
    
        [ScriptMethod(name: "获取风神ID", eventType: EventTypeEnum.AddCombatant, eventCondition: ["SourceName:regex:^(迦楼罗)$"], userControl:false)]
        public void 获取风神ID(Event @event, ScriptAccessory accessory)
        {
                windbossid = @event.SourceId();
                //accessory.Method.SendChat($"/e {windbossid}");
        }
        
        [ScriptMethod(name: "获取泰坦ID", eventType: EventTypeEnum.AddCombatant, eventCondition: ["SourceName:regex:^(泰坦)$"], userControl:false)]
        public void 获取泰坦ID(Event @event, ScriptAccessory accessory)
        {
                titanbossid = @event.SourceId();
                //accessory.Method.SendChat($"/e {titanbossid}");
            }
        [ScriptMethod(name: "获取泰坦ID2", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11152)$"], userControl:false)]
        public void 获取泰坦ID2(Event @event, ScriptAccessory accessory)
        {
            titanbossid = @event.SourceId();
            //accessory.Method.SendChat($"/e {titanbossid}");
        }
        [ScriptMethod(name: "获取美翼ID", eventType: EventTypeEnum.AddCombatant, eventCondition: ["SourceName:regex:^(美翼)$"], userControl:false)]
        public void 获取美翼ID(Event @event, ScriptAccessory accessory)
        {
                meiyibossid = @event.SourceId();
                //accessory.Method.SendChat($"/e {meiyibossid}");
            }
        [ScriptMethod(name: "获取美翼ID2", eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["SourceName:regex:^(美翼)$"], userControl:false)]
        public void 获取美翼ID2(Event @event, ScriptAccessory accessory)
        {
            meiyibossid = @event.SourceId();
            //accessory.Method.SendChat($"/e {meiyibossid}");
        }
        [ScriptMethod(name: "获取妙翅ID", eventType: EventTypeEnum.AddCombatant, eventCondition: ["SourceName:regex:^(妙翅)$"], userControl:false)]
        public void 获取妙翅ID(Event @event, ScriptAccessory accessory)
        {
            miaochibossid = @event.SourceId();
                //accessory.Method.SendChat($"/e {miaochibossid}");
            }
        [ScriptMethod(name: "获取妙翅ID2", eventType: EventTypeEnum.PlayActionTimeline, eventCondition: ["SourceName:regex:^(妙翅)$"], userControl:false)]
        public void 获取妙翅ID2(Event @event, ScriptAccessory accessory)
        {
            miaochibossid = @event.SourceId();
            //accessory.Method.SendChat($"/e {meiyibossid}");
        }
    
    
    [ScriptMethod(name: "P1螺旋气流", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11091)$"])]
    public void Luoxuanqiliu(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "P1螺旋气流";
        dp.Rotation = @event.SourceRotation();
        dp.DestoryAt = 3000;
        dp.Owner = @event.SourceId();
        dp.Position = @event.EffectPosition();
        dp.Scale = new(12);
        dp.Color = new(1.0f, 1.0f, 1.0f,2.0f);
        accessory.Method.SendDraw(0, DrawTypeEnum.Fan, dp);
        //accessory.Method.SendChat($"/e {string.Join(", ", windbossid)}");
        windbossid = @event.SourceId();
    }
    
    [ScriptMethod(name: "P1开场风枪", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(0010)$"])]
    public void P1开场风枪(Event @event, ScriptAccessory accessory)
    {
        //accessory.Method.SendChat($"/e 321 + {string.Join(", ", windbossid)}");
        if (windstart == true)
        {
            if (!ParseHexId(@event["TargetId"], out var tid)) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P1开场风枪";
            dp.Scale = new(8,40);
            dp.Color = accessory.Data.DefaultDangerColor.WithW(1);
            dp.Owner = windbossid;
            dp.TargetObject = tid;
            dp.DestoryAt = 4000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            windstart = false;
        }
    }
    
    [ScriptMethod(name: "P1分身风枪", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(0010)$"])]
    public void P1分身风枪(Event @event, ScriptAccessory accessory)
    {
        if (windstart == false)
        {
            lock (fengqiangtargetIds)
            {
            uint fengqiangid = @event.TargetId(); //获取 targetId 的方法
            fengqiangtargetIds.Add(fengqiangid);
            windcount++;
            if (windcount == 2) //让风枪事件只触发1次，不然会有2次bug
            {
                // 声明一个 List 来保存 targetId
                //List<uint> fengqiangtargetIds = new List<uint>(); 
                // 获取 targetId 并添加到 List 中


                //accessory.Method.SendChat($"/e {string.Join(", ", fengqiangtargetIds)}");

                var dp = accessory.Data.GetDefaultDrawProperties();
                var dp2 = accessory.Data.GetDefaultDrawProperties();
                var dp3 = accessory.Data.GetDefaultDrawProperties();

                dp.Name = "P1美翼风枪";
                dp.Scale = new(8, 40);
                dp.Color = accessory.Data.DefaultDangerColor.WithW(1);
                dp.Owner = meiyibossid;
                dp.TargetObject = fengqiangtargetIds[0];
                dp.DestoryAt = 5000;
                
                dp2.Name = "P1妙翅风枪";
                dp2.Scale = new(8, 40);
                dp2.Color = accessory.Data.DefaultDangerColor.WithW(1);
                dp2.Owner = miaochibossid;
                dp2.TargetObject = fengqiangtargetIds[1];
                dp2.DestoryAt = 5000;

                dp3.Name = "P1风神环形场地";
                dp3.Delay = 2000;
                dp3.Scale = new(20);
                dp3.InnerScale = new(11.5f);
                dp3.Radian = float.Pi * 2;
                dp3.Color = accessory.Data.DefaultDangerColor.WithW(1);
                dp3.Position = new(100, 0, 100);
                dp3.DestoryAt = 4200;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp3);
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp2);
            }
            }
        }
    }
    
    [ScriptMethod(name: "P1大龙卷风", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11073)$"])]
    public void Dalongjuanfeng(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "P1大龙卷风";
        dp.DestoryAt = 3000;
        dp.Owner = @event.SourceId();
        dp.Position = @event.EffectPosition();
        dp.Scale = new(8);
        dp.Color = new(1.0f, 1.0f, 1.0f,1.5f);
        accessory.Method.SendDraw(0, DrawTypeEnum.Circle, dp);
    }
    
    [ScriptMethod(name: "P1飞翎羽", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:11085"])]
    public void P1飞翎羽(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        if (!ParseHexId(@event["SourceId"], out var sid)) return;
        for (int i = 0; i < 8; i++)
        {
            dp.Name = $"P1飞翎羽-{i}";
            dp.Scale = new(3);
            dp.Owner = sid;
            dp.TargetOrderIndex = accessory.Data.PartyList[i];
            //dp.Color = accessory.Data.DefaultDangerColor;
            dp.DestoryAt = 1500;
            dp.Color = new(1.0f, 1.0f, 1.0f,0.3f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        yiyun = false;
    }

    [ScriptMethod(name: "P1妙翅接线", eventType: EventTypeEnum.Tether)]
    public void P1妙翅接线(Event @event, ScriptAccessory accessory)
    {
        //accessory.Method.SendChat("/e 123");
        if (!ParseHexId(@event["SourceId"], out var sid)) return;
        if (sid != miaochibossid) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "P1妙翅接线";
        dp.Scale = new(2);
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.Color = accessory.Data.DefaultDangerColor.WithW(1);
        dp.Owner = miaochibossid;
        dp.TargetResolvePattern = PositionResolvePatternEnum.TetherTarget;
        dp.DestoryAt = 5000;
        if (miaochitethercheck)
        {
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            miaochitethercheck = false;
        }
    }

    [ScriptMethod(name: "P1美翼接线", eventType: EventTypeEnum.Tether)]
    public void P1美翼接线(Event @event, ScriptAccessory accessory)
    {
            //accessory.Method.SendChat("/e 123");
            if (!ParseHexId(@event["SourceId"], out var sid)) return;
            //if (!ParseHexId(@event["TargetId"], out var tid)) return;
            if (sid != meiyibossid) return;
            var dp = accessory.Data.GetDefaultDrawProperties();

            dp.Name = "P1美翼接线";
            dp.Scale = new(2);
            dp.ScaleMode |= ScaleMode.YByDistance;
            dp.Color = accessory.Data.DefaultDangerColor.WithW(1);
            dp.Owner = meiyibossid;
            dp.TargetResolvePattern = PositionResolvePatternEnum.TetherTarget;
            dp.DestoryAt = 5000;
            
            if (meiyitethercheck)
            {
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
                meiyitethercheck = false;
            }

        if (fenshentether == true)
            {
                var dp2 = accessory.Data.GetDefaultDrawProperties();
                dp2.Name = "P1风神环形场地";
                //dp2.Delay = 2000;
                dp2.Scale = new(20);
                dp2.InnerScale = new(11.5f);
                dp2.Radian = float.Pi * 2;
                dp2.Color = accessory.Data.DefaultDangerColor.WithW(1);
                dp2.Position = new(100, 0, 100);
                dp2.DestoryAt = 4200;
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp2);
                fenshentether = false;
            }

    }
    
    [ScriptMethod(name: "P2火神开场冲锋", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11103)$"])]
    public void P2火神开场冲锋(Event @event, ScriptAccessory accessory)
    {
        if (@event["SourceName"] == "伊弗利特" && firestart == true && yiyun == false)
        {
            var dp = accessory.Data.GetDefaultDrawProperties();
            var dp2 = accessory.Data.GetDefaultDrawProperties();
            var dp3 = accessory.Data.GetDefaultDrawProperties();
            firestart = false;
            
            if (!ParseHexId(@event["SourceId"], out var sid)) return;
            //dp.Delay = 1000;
            dp.Name = "P2火神开场冲锋";
            dp.Scale = new(18, 40);
            dp.Owner = sid;
            dp.Rotation = @event.SourceRotation();
            dp.Position = @event.SourcePosition();
            dp.DestoryAt = 4000;
            dp.Color = new(1.0f, 1.0f, 1.0f, 1.0f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            
            dp2.Delay = 14500;
            dp2.Name = "P2火神爆裂";
            dp2.Scale = new(2, 15);
            dp2.Color = accessory.Data.DefaultDangerColor.WithW(3);
            dp2.Owner = accessory.Data.Me;
            dp2.TargetObject = sid;
            dp2.Rotation = float.Pi;
            dp2.DestoryAt = 4500;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp2);
            
            dp3.Delay = 18000;
            dp3.Name = "P2火神开场烈焰焚烧";
            dp3.Scale = new(12);
            dp3.Color = accessory.Data.DefaultDangerColor.WithW(1);
            dp3.Owner = sid;
            dp3.TargetResolvePattern = PositionResolvePatternEnum.OwnerEnmityOrder;
            dp3.TargetOrderIndex = 1;
            //dp3.Rotation = float.Pi;
            dp3.Radian = (float)(2 * Math.PI / 3);
            dp3.DestoryAt = 12000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp3);
            
            yiyun = false;
        }
    }
    
    [ScriptMethod(name: "P2火神冲", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11103)$"])]
    public void P2火神冲(Event @event, ScriptAccessory accessory)
    {
        if (firestart == false && yiyun == false)
        {
            if (!ParseHexId(@event["SourceId"], out var sid)) return;
            //if (!ParseHexId(@event["TargetId"], out var tid)) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Scale = new(18, 40);
            dp.Name = "P2火神冲";
            dp.Owner = sid;
            dp.Rotation = @event.SourceRotation();
            dp.Position = @event.SourcePosition();
            dp.DestoryAt = 3000;
            dp.Color = new(1.0f, 1.0f, 1.0f, 0.8f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
        }
    }


    
    [ScriptMethod(name: "P2火神柱", eventType: EventTypeEnum.AddCombatant,eventCondition: ["DataId:regex:^(8731)$"])]
    public void P2火神柱(Event @event, ScriptAccessory accessory)
    {
        lock (huoshenzhuCoList)
        {
            huoshenzhuCoList.Add(@event.SourcePosition());
        if (huoshenzhuCoList.Count == 4)
        {
            //accessory.Method.SendChat($"/e {huoshenzhuCoList.Count}+{string.Join(", ", huoshenzhuCoList)}");
            // 圆的中心坐标和半径
            Vector3 circleCenter = new Vector3(100, 0, 100);
            float radius = 20;
            // 找到梯形的下底
            Vector3 lowerBasePoint1 = new Vector3(0, 0, 0);
            Vector3 lowerBasePoint2 = new Vector3(0, 0, 0);
            float maxLength = 0;
            // 遍历所有可能的边组合
            for (int i = 0; i < 4; i++)
            {
                for (int j = i + 1; j < 4; j++)
                {
                    // 计算边的向量
                    Vector3 edgeVector = huoshenzhuCoList[j] - huoshenzhuCoList[i];
                    // 检查是否平行于另一条边
                    for (int k = 0; k < 4; k++)
                    {
                        if (k == i || k == j) continue;
                        for (int l = k + 1; l < 4; l++)
                        {
                            if (l == i || l == j) continue;
                            Vector3 otherEdgeVector = huoshenzhuCoList[l] - huoshenzhuCoList[k];
                            // 检查两条边是否平行
                            float crossX = edgeVector.Y * otherEdgeVector.Z - edgeVector.Z * otherEdgeVector.Y;
                            float crossY = edgeVector.Z * otherEdgeVector.X - edgeVector.X * otherEdgeVector.Z;
                            float crossZ = edgeVector.X * otherEdgeVector.Y - edgeVector.Y * otherEdgeVector.X;
                            float crossMagnitude = (float)Math.Sqrt(crossX * crossX + crossY * crossY + crossZ * crossZ);
                            //accessory.Method.SendChat($"/e cross+ {crossMagnitude}");
                            if (crossMagnitude < 5f)
                            {
                                // 找到两条平行边
                                float length1 = (float)Math.Sqrt(edgeVector.X * edgeVector.X + edgeVector.Y * edgeVector.Y + edgeVector.Z * edgeVector.Z);
                                //accessory.Method.SendChat($"/e l1+ {length1}");
                                float length2 = (float)Math.Sqrt(otherEdgeVector.X * otherEdgeVector.X + otherEdgeVector.Y * otherEdgeVector.Y + otherEdgeVector.Z * otherEdgeVector.Z);
                                //accessory.Method.SendChat($"/e l2+ {length2}");
                                // 确定下底
                                if (length1 > maxLength)
                                {
                                    maxLength = length1;
                                    lowerBasePoint1 = huoshenzhuCoList[i];
                                    lowerBasePoint2 = huoshenzhuCoList[j];
                                    //accessory.Method.SendChat($"/e p1+ {lowerBasePoint1.X} + {lowerBasePoint1.Y} + {lowerBasePoint1.Z}");
                                    //accessory.Method.SendChat($"/e p2+ {lowerBasePoint2.X} + {lowerBasePoint2.Y} + {lowerBasePoint2.Z}");
                                }
                                if (length2 > maxLength)
                                {
                                    maxLength = length2;
                                    lowerBasePoint1 = huoshenzhuCoList[k];
                                    lowerBasePoint2 = huoshenzhuCoList[l];
                                    //accessory.Method.SendChat($"/e p1+ {lowerBasePoint1.X} + {lowerBasePoint1.Y} + {lowerBasePoint1.Z}");
                                    //accessory.Method.SendChat($"/e p2+ {lowerBasePoint2.X} + {lowerBasePoint2.Y} + {lowerBasePoint2.Z}");
                                }
                            }
                        }
                    }
                }
            }
            // 计算线段的中点
            Vector3 M = (lowerBasePoint1 + lowerBasePoint2) / 2;
            // 计算垂直平分线的方向向量
            Vector3 direction = new Vector3(lowerBasePoint1.Z - lowerBasePoint2.Z, 0, lowerBasePoint2.X - lowerBasePoint1.X);
            // 将垂直平分线的直线方程代入圆的方程
            float A = direction.X;
            float B = direction.Z;
            float C = M.X - circleCenter.X;
            float D = M.Z - circleCenter.Z;
            float a = A * A + B * B;
            float b = 2 * (A * C + B * D);
            float c = C * C + D * D - radius * radius;
            float discriminant = b * b - 4 * a * c;
            if (discriminant < 0) return;
            float t1 = (-b + (float)Math.Sqrt(discriminant)) / (2 * a);
            float t2 = (-b - (float)Math.Sqrt(discriminant)) / (2 * a);
            // 计算交点坐标
            Vector3 P1 = new Vector3(M.X + t1 * direction.X, 0, M.Z + t1 * direction.Z);
            Vector3 P2 = new Vector3(M.X + t2 * direction.X, 0, M.Z + t2 * direction.Z);
            // 计算两个交点与中点 M 的距离
            float distance1 = (float)Math.Sqrt(Math.Pow(P1.X - M.X, 2) + Math.Pow(P1.Z - M.Z, 2));
            float distance2 = (float)Math.Sqrt(Math.Pow(P2.X - M.X, 2) + Math.Pow(P2.Z - M.Z, 2));
            // 选择更靠近线段的交点
            Vector3 closerPoint = (distance1 < distance2) ? P1 : P2;
            //accessory.Method.SendChat($"/e {string.Join(", ", huoshenzhuCoList)}");
            //accessory.Method.SendChat($"/e 1+ {lowerBasePoint1.X} + {lowerBasePoint1.Y} + {lowerBasePoint1.Z}");
            //accessory.Method.SendChat($"/e 2+ {lowerBasePoint2.X} + {lowerBasePoint2.Y} + {lowerBasePoint2.Z}");
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Scale = new(4);
            dp.Name = "P2火神柱热风奶目标点";
            dp.Position = closerPoint;
            dp.DestoryAt = 45000;
            dp.Color = new(0.0f, 1.0f, 0.0f, 2f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
        }
        
    }
    
    [ScriptMethod(name: "P2火神分摊", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(0075)$"])]
    public void P2火神分摊(Event @event, ScriptAccessory accessory)
    {
            //accessory.Method.SendChat("/e 123");
            //if (!ParseHexId(@event["SourceId"], out var sid)) return;
            if (!ParseHexId(@event["TargetId"], out var tid)) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Scale = new(3);
            dp.Name = "P2火神分摊";
            dp.Owner = tid;
            dp.TargetObject = tid;
            dp.DestoryAt = 5500;
            dp.Color = new(0.9f, 0.6f, 0.3f, 1.0f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        
    }
    
    
    [ScriptMethod(name: "P2火神光辉炎柱", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11105)$"])]
    public void P2火神光辉炎柱(Event @event, ScriptAccessory accessory)
    {
        if (@event["SourceName"] == "伊弗利特" && yiyun == false)
        {

            var dp = accessory.Data.GetDefaultDrawProperties();
            if (!ParseHexId(@event["SourceId"], out var sid)) return;
            //dp.Delay = 1000;
            dp.Name = "P2火神光辉炎柱";
            dp.Scale = new(8);
            dp.Owner = sid;
            dp.Rotation = @event.SourceRotation();
            dp.Position = @event.EffectPosition();
            dp.DestoryAt = 5000;
            dp.Color = new(1.0f, 1.0f, 1.0f, 1.0f);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }
    
    [ScriptMethod(name: "P2第一次热风", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(11099)$"])]
    public void P2第一次热风(Event @event, ScriptAccessory accessory)
    {
        if (HotWind1 == true && yiyun == false)
        {
            //if (!ParseHexId(@event["SourceId"], out var sid)) return;
            if (!ParseHexId(@event["TargetId"], out var tid)) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = $"P2第一次热风";
            dp.Scale = new(15);
            dp.Color = new(1.0f, 0.0f, 0.0f, 0.7f);
            dp.Owner = tid;
            dp.TargetObject = tid;
            dp.DestoryAt = 18000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            HotWind1 = false;
        }
    }
    
    [ScriptMethod(name: "P2第二次热风", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(11099)$"])]
    public void P2第二次热风(Event @event, ScriptAccessory accessory)
    {
        if (HotWind1 == false && yiyun == false)
        {
            //if (!ParseHexId(@event["SourceId"], out var sid)) return;
            if (!ParseHexId(@event["TargetId"], out var tid)) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P2第二次热风";
            dp.Scale = new(15);
            dp.Color = new(1.0f, 0.0f, 0.0f, 0.7f);
            dp.Owner = tid;
            dp.TargetObject = tid;
            dp.DestoryAt = 28000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        }
    }
    
    [ScriptMethod(name: "P2第二次热风提前结束", eventType: EventTypeEnum.StatusRemove, eventCondition: ["StatusID:regex:^(1578)$"], userControl:false)]
    public void P2第二次热风提前结束(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw("P2第二次热风");
        //accessory.Method.SendChat("/e 123");
    }
    
    [ScriptMethod(name: "P3开场", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11517)$"])]
    public void P3开场(Event @event, ScriptAccessory accessory)
    { 
            if (!ParseHexId(@event["SourceId"], out var sid)) return;
            //if (!ParseHexId(@event["TargetId"], out var tid)) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "P3开场";
            dp.Scale = new(18);
            dp.Color = accessory.Data.DefaultDangerColor.WithW(1);
            dp.Owner = sid;
            dp.Position = @event.TargetPosition();
            //dp.TargetObject = tid;
            dp.DestoryAt = 3000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        
    }
    
    [ScriptMethod(name: "P3泰坦跳跃", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11110)$"])]
    public void P3泰坦跳跃(Event @event, ScriptAccessory accessory)
    { 
        if (!ParseHexId(@event["SourceId"], out var sid)) return;
        //if (!ParseHexId(@event["TargetId"], out var tid)) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "P3泰坦跳跃";
        dp.Scale = new(23);
        dp.Color = accessory.Data.DefaultDangerColor.WithW(1);
        dp.Owner = sid;
        dp.Position = @event.TargetPosition();
        //dp.TargetObject = tid;
        dp.DestoryAt = 3000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }
    
    /*[ScriptMethod(name: "P3石牢爆炸污泥", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(11114|11118)$"])]
    public void P3石牢爆炸污泥(Event @event, ScriptAccessory accessory)
    { 
        //if (!ParseHexId(@event["SourceId"], out var sid)) return;
        if (!ParseHexId(@event["TargetId"], out var tid)) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "P3石牢爆炸污泥";
        dp.Delay = 2000;
        dp.Scale = new(6);
        dp.Color = accessory.Data.DefaultDangerColor.WithW(0.7f);
        dp.Owner = tid;
        dp.Position = @event.TargetPosition();
        //dp.TargetObject = tid;
        dp.DestoryAt = 15000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }*/
    
    [ScriptMethod(name: "P3地裂", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11120|11298|11134|11135)$"])]
    public void P3地裂(Event @event, ScriptAccessory accessory)
    { 
        if (!ParseHexId(@event["SourceId"], out var sid)) return;
        //if (!ParseHexId(@event["TargetId"], out var tid)) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "P3地裂";
        dp.Scale = new(6, 40);
        dp.Color = accessory.Data.DefaultDangerColor.WithW(2);
        dp.Owner = sid;
        dp.Position = @event.EffectPosition();
        dp.Rotation = @event.SourceRotation();
        dp.DestoryAt = 2000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }
    
    
    [ScriptMethod(name: "P3三连桶A右", eventType: EventTypeEnum.AddCombatant, eventCondition: ["SourcePosition:regex:^{\"X\":95.00,\"Y\":0.00,\"Z\":112.00}$"])]
    public void P3三连桶A右(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        var dp2 = accessory.Data.GetDefaultDrawProperties();
        var dp3 = accessory.Data.GetDefaultDrawProperties();
        var dp4 = accessory.Data.GetDefaultDrawProperties();
        if (@event["SourceName"] == "爆破岩石" && sanliantong == true)
        { 
            
            dp.Name = "3号桶";
            dp.Scale = new(1);
            dp.Delay = 2000;
            dp.DestoryAt = 12000;
            dp.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp.Position = new(105, 0, 105);
            
            dp2.Name = "2号桶";
            dp2.Scale = new(1);
            dp2.Delay = 2000;
            dp2.DestoryAt = 12000;
            dp2.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp2.Position = new(100, 0, 100);
            
            dp3.Name = "1号桶";
            dp3.Scale = new(1);
            dp3.Delay = 2000;
            dp3.DestoryAt = 12000;
            dp3.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp3.Position = new(100, 0, 94);
            
            dp4.Name = "击退指路";
            dp4.Scale = new(2);
            dp4.ScaleMode |= ScaleMode.YByDistance;
            dp4.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp4.Owner = titanbossid;
            dp4.TargetPosition = new(103, 0, 112);
            dp4.DestoryAt = 4500;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp4);
            
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp3);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp2);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            sanliantong = false;
        }
    }

    [ScriptMethod(name: "P3三连桶A左", eventType: EventTypeEnum.AddCombatant, eventCondition: ["SourcePosition:regex:^{\"X\":105.00,\"Y\":0.00,\"Z\":112.00}$"])]
    public void P3三连桶A左(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        var dp2 = accessory.Data.GetDefaultDrawProperties();
        var dp3 = accessory.Data.GetDefaultDrawProperties();
        var dp4 = accessory.Data.GetDefaultDrawProperties();
        if (@event["SourceName"] == "爆破岩石" && sanliantong == true)
        { 
            dp.Name = "3号桶";
            dp.Scale = new(1);
            dp.Delay = 2000;
            dp.DestoryAt = 12000;
            dp.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp.Position = new(95, 0, 105);
            
            dp2.Name = "2号桶";
            dp2.Scale = new(1);
            dp2.Delay = 2000;
            dp2.DestoryAt = 12000;
            dp2.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp2.Position = new(100, 0, 100);
            
            dp3.Name = "1号桶";
            dp3.Scale = new(1);
            dp3.Delay = 2000;
            dp3.DestoryAt = 12000;
            dp3.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp3.Position = new(100, 0, 94);
            
            dp4.Name = "击退指路";
            dp4.Scale = new(2);
            dp4.ScaleMode |= ScaleMode.YByDistance;
            dp4.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp4.Owner = titanbossid;
            dp4.TargetPosition = new(97, 0, 112);
            dp4.DestoryAt = 4500;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp4);
            
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp3);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp2);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            sanliantong = false;
        }
    }
    [ScriptMethod(name: "P3三连桶D右", eventType: EventTypeEnum.AddCombatant, eventCondition: ["SourcePosition:regex:^{\"X\":112.00,\"Y\":0.00,\"Z\":105.00}$"])]
    public void P3三连桶D右(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        var dp2 = accessory.Data.GetDefaultDrawProperties();
        var dp3 = accessory.Data.GetDefaultDrawProperties();
        var dp4 = accessory.Data.GetDefaultDrawProperties();
        if (@event["SourceName"] == "爆破岩石" && sanliantong == true)
        { 
            dp.Name = "3号桶";
            dp.Scale = new(1);
            dp.Delay = 2000;
            dp.DestoryAt = 12000;
            dp.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp.Position = new(105, 0, 95);
            
            dp2.Name = "2号桶";
            dp2.Scale = new(1);
            dp2.Delay = 2000;
            dp2.DestoryAt = 12000;
            dp2.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp2.Position = new(100, 0, 100);
            
            dp3.Name = "1号桶";
            dp3.Scale = new(1);
            dp3.Delay = 2000;
            dp3.DestoryAt = 12000;
            dp3.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp3.Position = new(94, 0, 100);
            
            dp4.Name = "击退指路";
            dp4.Scale = new(2);
            dp4.ScaleMode |= ScaleMode.YByDistance;
            dp4.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp4.Owner = titanbossid;
            dp4.TargetPosition = new(112, 0, 97);
            dp4.DestoryAt = 4500;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp4);
            
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp3);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp2);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            sanliantong = false;
        }
    }
    [ScriptMethod(name: "P3三连桶D左", eventType: EventTypeEnum.AddCombatant, eventCondition: ["SourcePosition:regex:^{\"X\":112.00,\"Y\":-0.00,\"Z\":95.00}$"])]
    public void P3三连桶D左(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        var dp2 = accessory.Data.GetDefaultDrawProperties();
        var dp3 = accessory.Data.GetDefaultDrawProperties();
        var dp4 = accessory.Data.GetDefaultDrawProperties();
        if (@event["SourceName"] == "爆破岩石" && sanliantong == true)
        { 
            dp.Name = "3号桶";
            dp.Scale = new(1);
            dp.Delay = 2000;
            dp.DestoryAt = 12000;
            dp.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp.Position = new(105, 0, 105);
            
            dp2.Name = "2号桶";
            dp2.Scale = new(1);
            dp2.Delay = 2000;
            dp2.DestoryAt = 12000;
            dp2.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp2.Position = new(100, 0, 100);
            
            dp3.Name = "1号桶";
            dp3.Scale = new(1);
            dp3.Delay = 2000;
            dp3.DestoryAt = 12000;
            dp3.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp3.Position = new(94, 0, 100);
            
            dp4.Name = "击退指路";
            dp4.Scale = new(2);
            dp4.ScaleMode |= ScaleMode.YByDistance;
            dp4.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp4.Owner = titanbossid;
            dp4.TargetPosition = new(112, 0, 103);
            dp4.DestoryAt = 4500;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp4);
            
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp3);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp2);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            sanliantong = false;
        }
    }
    [ScriptMethod(name: "P3三连桶C左", eventType: EventTypeEnum.AddCombatant, eventCondition: ["SourcePosition:regex:^{\"X\":95.00,\"Y\":-0.00,\"Z\":88.00}$"])]
    public void P3三连桶C左(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        var dp2 = accessory.Data.GetDefaultDrawProperties();
        var dp3 = accessory.Data.GetDefaultDrawProperties();
        var dp4 = accessory.Data.GetDefaultDrawProperties();
        if (@event["SourceName"] == "爆破岩石" && sanliantong == true)
        { 
            dp.Name = "3号桶";
            dp.Scale = new(1);
            dp.Delay = 2000;
            dp.DestoryAt = 12000;
            dp.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp.Position = new(105, 0, 95);
            
            dp2.Name = "2号桶";
            dp2.Scale = new(1);
            dp2.Delay = 2000;
            dp2.DestoryAt = 12000;
            dp2.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp2.Position = new(100, 0, 100);
            
            
            dp3.Name = "1号桶";
            dp3.Scale = new(1);
            dp3.Delay = 2000;
            dp3.DestoryAt = 12000;
            dp3.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp3.Position = new(100, 0, 106);
            
            dp4.Name = "击退指路";
            dp4.Scale = new(2);
            dp4.ScaleMode |= ScaleMode.YByDistance;
            dp4.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp4.Owner = titanbossid;
            dp4.TargetPosition = new(103, 0, 88);
            dp4.DestoryAt = 4500;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp4);
            
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp3);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp2);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            sanliantong = false;
        }
    }
    [ScriptMethod(name: "P3三连桶C右", eventType: EventTypeEnum.AddCombatant, eventCondition: ["SourcePosition:regex:^{\"X\":105.00,\"Y\":-0.00,\"Z\":88.00}$"])]
    public void P3三连桶C右(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        var dp2 = accessory.Data.GetDefaultDrawProperties();
        var dp3 = accessory.Data.GetDefaultDrawProperties();
        var dp4 = accessory.Data.GetDefaultDrawProperties();
        if (@event["SourceName"] == "爆破岩石" && sanliantong == true)
        { 
            dp.Name = "3号桶";
            dp.Scale = new(1);
            dp.Delay = 2000;
            dp.DestoryAt = 12000;
            dp.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp.Position = new(95, 0, 95);
            
            dp2.Name = "2号桶";
            dp2.Scale = new(1);
            dp2.Delay = 2000;
            dp2.DestoryAt = 12000;
            dp2.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp2.Position = new(100, 0, 100);
            
            
            dp3.Name = "1号桶";
            dp3.Scale = new(1);
            dp3.Delay = 2000;
            dp3.DestoryAt = 12000;
            dp3.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp3.Position = new(100, 0, 106);
            
            dp4.Name = "击退指路";
            dp4.Scale = new(2);
            dp4.ScaleMode |= ScaleMode.YByDistance;
            dp4.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp4.Owner = titanbossid;
            dp4.TargetPosition = new(97, 0, 88);
            dp4.DestoryAt = 4500;
            
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp4);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp3);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp2);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            sanliantong = false;
        }
    }
    [ScriptMethod(name: "P3三连桶B右", eventType: EventTypeEnum.AddCombatant, eventCondition: ["SourcePosition:regex:^{\"X\":88.00,\"Y\":-0.00,\"Z\":95.00}$"])]
    public void P3三连桶B右(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        var dp2 = accessory.Data.GetDefaultDrawProperties();
        var dp3 = accessory.Data.GetDefaultDrawProperties();
        var dp4 = accessory.Data.GetDefaultDrawProperties();
        if (@event["SourceName"] == "爆破岩石" && sanliantong == true)
        { 
            dp.Name = "3号桶";
            dp.Scale = new(1);
            dp.Delay = 2000;
            dp.DestoryAt = 12000;
            dp.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp.Position = new(95, 0, 105);
            
            dp2.Name = "2号桶";
            dp2.Scale = new(1);
            dp2.Delay = 2000;
            dp2.DestoryAt = 12000;
            dp2.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp2.Position = new(100, 0, 100);
            
                        
            dp3.Name = "1号桶";
            dp3.Scale = new(1);
            dp3.Delay = 2000;
            dp3.DestoryAt = 12000;
            dp3.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp3.Position = new(106, 0, 100);
            
            dp4.Name = "击退指路";
            dp4.Scale = new(2);
            dp4.ScaleMode |= ScaleMode.YByDistance;
            dp4.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp4.Owner = titanbossid;
            dp4.TargetPosition = new(88, 0, 103);
            dp4.DestoryAt = 4500;
            
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp4);
            
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp3);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp2);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            sanliantong = false;
        }
    }
    [ScriptMethod(name: "P3三连桶B左", eventType: EventTypeEnum.AddCombatant, eventCondition: ["SourcePosition:regex:^{\"X\":88.00,\"Y\":-0.00,\"Z\":105.00}$"])]
    public void P3三连桶B左(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        var dp2 = accessory.Data.GetDefaultDrawProperties();
        var dp3 = accessory.Data.GetDefaultDrawProperties();
        var dp4 = accessory.Data.GetDefaultDrawProperties();
        if (@event["SourceName"] == "爆破岩石" && sanliantong == true)
        { 
            dp.Name = "3号桶";
            dp.Scale = new(1);
            dp.Delay = 2000;
            dp.DestoryAt = 12000;
            dp.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp.Position = new(95, 0, 95);
            
            dp2.Name = "2号桶";
            dp2.Scale = new(1);
            dp2.Delay = 2000;
            dp2.DestoryAt = 12000;
            dp2.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp2.Position = new(100, 0, 100);
            
            dp3.Name = "1号桶";
            dp3.Scale = new(1);
            dp3.Delay = 2000;
            dp3.DestoryAt = 12000;
            dp3.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp3.Position = new(106, 0, 100);
            
            dp4.Name = "击退指路";
            dp4.Scale = new(2);
            dp4.ScaleMode |= ScaleMode.YByDistance;
            dp4.Color = new(0.3f, 1.0f, 0f,1.5f);
            dp4.Owner = titanbossid;
            dp4.TargetPosition = new(88, 0, 97);
            dp4.DestoryAt = 4500;
            
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp4);
            
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp3);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp2);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
            sanliantong = false;
        }
    }
    
    [ScriptMethod(name: "P3三连桶点名头上标点", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(11116|11115)$"], userControl:false)]
    public void P3三连桶点名头上标点(Event @event, ScriptAccessory accessory)
    {
        if(!TitanMark) return;
            if (!ParseHexId(@event["TargetId"], out var tid)) return;
            var playerindex = accessory.Data.PartyList.IndexOf(tid);//获取3个被点人在可达鸭里的队伍位置
            playerIndexList.Add(playerindex);//把3个人的位置存进playerIndexList
            if (playerIndexList.Count == 3)//让三连桶事件只触发1次，不然3次石牢事件会触发3次
            {
                List<int> finalIndex = playerIndexList.OrderBy(n => customOrder.IndexOf(n)).ToList();//按照mt st d1234 h12的顺序排列
                //accessory.Method.SendChat($"/e {string.Join(", ", finalIndex)}"); //debug用 测试点名
                accessory.Method.Mark(accessory.Data.PartyList[finalIndex[0]], MarkType.Attack1, false); //给顺序1的人标1
                accessory.Method.Mark(accessory.Data.PartyList[finalIndex[1]], MarkType.Attack2, false); //给顺序2的人标2
                accessory.Method.Mark(accessory.Data.PartyList[finalIndex[2]], MarkType.Attack3, false); //给顺序3的人标3
            }
    }
    
    [ScriptMethod(name: "三连桶标点测试", eventType: EventTypeEnum.Chat, eventCondition: ["Message:regex:^(测试三连桶标点鸭鸭)$"], userControl:false)]
    public void 三连桶标点测试(Event @event, ScriptAccessory accessory)
    {
        if(!TitanMarkTest) return;
        // 创建一个 Random 实例
        Random random = new Random();
        // 生成 1~7 之间的 3 个不重复的随机整数
        List<int> randomBucket = Enumerable.Range(1, 7) // 生成 1~7 的范围
            .OrderBy(x => random.Next()) // 随机排序
            .Take(3) // 取前3个幸运B
            .ToList(); // 转换为列表
        List<int> finalIndex = randomBucket.OrderBy(n => customOrder.IndexOf(n)).ToList();//按照mt st d1234 h12的顺序排列
        accessory.Method.SendChat($"/e 测试：随机点3人 \n 可达鸭小队序列：0，1，4，5，6，7，2，3 \n 对应 MT ST D1 D2 D3 D4 H1 H2 \n 本次测试随机点名序列：{string.Join(", ", finalIndex)}"); //debug用 测试点名
        accessory.Method.Mark(accessory.Data.PartyList[finalIndex[0]], MarkType.Attack1, false);//给顺序1的人标1
        accessory.Method.Mark(accessory.Data.PartyList[finalIndex[1]], MarkType.Attack2, false);//给顺序2的人标2
        accessory.Method.Mark(accessory.Data.PartyList[finalIndex[2]], MarkType.Attack3, false);//给顺序3的人标3
    }
    
    [ScriptMethod(name: "本体诱导射线", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11131)$"])]
    public void 本体诱导射线(Event @event, ScriptAccessory accessory)
    {
        if (!ParseHexId(@event["TargetId"], out var tid)) return;
        //if (!ParseHexId(@event["SourceId"], out var sid)) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "本体诱导射线";
        dp.Scale = new(4);
        dp.Owner = tid;
        dp.TargetObject = tid;
        dp.DestoryAt = 4000;
        dp.Color = accessory.Data.DefaultDangerColor.WithW(1);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }
    
    [ScriptMethod(name: "本体吸附式炸弹", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(11129)$"])]
    public void 本体吸附式炸弹(Event @event, ScriptAccessory accessory)
    {
        if (!ParseHexId(@event["TargetId"], out var tid)) return;
        //if (!ParseHexId(@event["SourceId"], out var sid)) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "本体吸附式炸弹";
        dp.Scale = new(4);
        dp.Owner = tid;
        dp.TargetObject = tid;
        dp.DestoryAt = 12000;
        dp.Color = new(0.9f, 0.6f, 0.3f, 0.6f);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }
    
    [ScriptMethod(name: "本体一运神兵风神", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11126)$"])]
    public void 本体一运神兵风神(Event @event, ScriptAccessory accessory)
    {
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Delay = 11000;
        dp.Name = "本体一运风神";
        dp.Scale = new(20);
        dp.Owner = windbossid;
        dp.DestoryAt = 11000;
        dp.Color = accessory.Data.DefaultDangerColor.WithW(1);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
        
        if (!ParseHexId(@event["SourceId"], out var sid)) return;
        var dp2 = accessory.Data.GetDefaultDrawProperties();
        dp2.Delay = 11000;
        dp2.Name = "本体一运神兵";
        dp2.Scale = new(13);
        dp2.Owner = sid;
        dp2.DestoryAt = 11000;
        dp2.Color = accessory.Data.DefaultDangerColor.WithW(1);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp2);
        yiyun = true;
    }
    
    [ScriptMethod(name: "本体一运火神", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceName:regex:^(伊弗利特)$"])]
    public void 本体一运火神(Event @event, ScriptAccessory accessory)
    {
        if (yiyun == true)
        {
            //if (!ParseHexId(@event["TargetId"], out var tid)) return;
            if (!ParseHexId(@event["SourceId"], out var sid)) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Delay = 1000;
            dp.Scale = new(18, 40);
            dp.Name = "本体一运火神";
            dp.Owner = sid;
            dp.Rotation = @event.SourceRotation();
            dp.Position = @event.SourcePosition();
            dp.DestoryAt = 7000;
            dp.Color = accessory.Data.DefaultDangerColor.WithW(1);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            
            var dp2 = accessory.Data.GetDefaultDrawProperties();
            dp2.Delay = 7500;
            dp2.Scale = new(10, 40);
            dp2.Name = "本体一运火神十字";
            dp2.Rotation = (float)Math.PI*3/2;;
            dp2.Position = new(120, 0, 100);
            dp2.DestoryAt = 3000;
            dp2.Color = accessory.Data.DefaultDangerColor.WithW(1);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp2);
            
            var dp3 = accessory.Data.GetDefaultDrawProperties();
            dp3.Delay = 7500;
            dp3.Scale = new(10, 40);
            dp3.Name = "本体一运火神十字";
            dp3.Rotation = (float)Math.PI;
            dp3.Position = new(100, 0, 120);
            dp3.DestoryAt = 3000;
            dp3.Color = accessory.Data.DefaultDangerColor.WithW(1);
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp3);
            
        }
    }

    [ScriptMethod(name: "本体二运火神十字", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11596)$"])]
    public void 本体二运火神十字(Event @event, ScriptAccessory accessory)
    {
        eryun = true;
        var dp2 = accessory.Data.GetDefaultDrawProperties();
        dp2.Delay = 29500;
        dp2.Scale = new(10, 40);
        dp2.Name = "本体二运火神十字";
        dp2.Rotation = (float)Math.PI*3/2;;
        dp2.Position = new(120, 0, 100);
        dp2.DestoryAt = 3000;
        dp2.Color = accessory.Data.DefaultDangerColor.WithW(1);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp2);
            
        var dp3 = accessory.Data.GetDefaultDrawProperties();
        dp3.Delay = 29500;
        dp3.Scale = new(10, 40);
        dp3.Name = "本体二运火神十字";
        dp3.Rotation = (float)Math.PI;
        dp3.Position = new(100, 0, 120);
        dp3.DestoryAt = 3000;
        dp3.Color = accessory.Data.DefaultDangerColor.WithW(1);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp3);
    }

    [ScriptMethod(name: "本体二运风神接线", eventType: EventTypeEnum.Tether)]
    public void 本体二运风神接线(Event @event, ScriptAccessory accessory)
    {
        //accessory.Method.SendChat("/e 123");
        if (!ParseHexId(@event["SourceId"], out var sid)) return;
        //if (!ParseHexId(@event["TargetId"], out var tid)) return;
        if (sid != windbossid) return;
        
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "本体二运风神接线";
        dp.Scale = new(2);
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.Color = accessory.Data.DefaultDangerColor.WithW(1);
        dp.Owner = windbossid;
        dp.TargetResolvePattern = PositionResolvePatternEnum.TetherTarget;
        dp.DestoryAt = 5000;

        var dp2 = accessory.Data.GetDefaultDrawProperties();
        dp2.Name = "本体二运风神第二次接线";
        dp2.Scale = new(2);
        dp2.ScaleMode |= ScaleMode.YByDistance;
        dp2.Color = accessory.Data.DefaultDangerColor.WithW(1);
        dp2.Owner = windbossid;
        dp2.TargetResolvePattern = PositionResolvePatternEnum.TetherTarget;
        dp2.DestoryAt = 2500;
        if (windtethercheck1 == true && sanyun == false)
        {
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            windtethercheck1 = false;
        }
        if (windtethercheck1 == false && sanyun == false)
        {
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp2);
            //windtethercheck2 = false;
        }
    }

    [ScriptMethod(name: "本体二运风神台风眼", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11090)$"])]
    public void 本体二运风神台风眼(Event @event, ScriptAccessory accessory)
    {
        //accessory.Method.SendChat("/e 123");
        if (eryun == true)
        {
            if (!ParseHexId(@event["SourceId"], out var sid)) return;
            //if (!ParseHexId(@event["TargetId"], out var tid)) return;
            var dp = accessory.Data.GetDefaultDrawProperties();
            dp.Name = "风神台风眼";
            dp.Scale = new(20);
            dp.InnerScale = new(11.5f);
            dp.Radian = float.Pi * 2;
            dp.Color = accessory.Data.DefaultDangerColor.WithW(1);
            dp.Position = new(100, 0, 100);
            dp.DestoryAt = 3000;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
        }
    }
    
    [ScriptMethod(name: "本体三运", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11597)$"], userControl:false)]
    public void 本体三运(Event @event, ScriptAccessory accessory)
    {
        sanyun = true;
        //sanyundihuo = 0;

        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Delay = 15000;
        dp.Name = "本体三运刚羽圈";
        dp.Scale = new(20);
        dp.InnerScale = new(15);
        dp.Radian = float.Pi * 2;
        dp.Color = accessory.Data.DefaultDangerColor.WithW(1);
        dp.Position = new(100, 0, 100);
        dp.DestoryAt = 23000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
    }
    
    [ScriptMethod(name: "本体三运地火", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:11098"], userControl:false)]
    public void 本体三运地火(Event @event, ScriptAccessory accessory)
    {
        if(!IfritMark) return;
        if (sanyun == true)
        {
            lock (sanyundihuolist)
            {
                var pos = JsonConvert.DeserializeObject<Vector3>(@event["EffectPosition"]);
                var target = FakeParty.Get().MinBy(x => Vector3.Distance(pos, x.Position));
                var did = accessory.Data.PartyList.IndexOf(target.EntityId);
                sanyundihuolist.Add(did);
                //accessory.Method.SendChat($"/e 地火点名2：{sanyundihuolist.Count}");
            }
            if (sanyundihuolist.Count == 3)
                {
                    accessory.Method.Mark(accessory.Data.PartyList[sanyundihuolist[0]], MarkType.Bind1, false);//给顺序1的人标1
                    accessory.Method.Mark(accessory.Data.PartyList[sanyundihuolist[1]], MarkType.Bind2, false);//给顺序2的人标2
                    accessory.Method.Mark(accessory.Data.PartyList[sanyundihuolist[2]], MarkType.Bind3, false);//给顺序3的人标3
                    //accessory.Method.SendChat($"/e 地火点名：{string.Join(", ", target)}");
                }
            
        }
    }

    [ScriptMethod(name: "本体三运魔科学激光1", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11141)$"])]
    public void 本体三运魔科学激光1(Event @event, ScriptAccessory accessory)
    {
        if (!ParseHexId(@event["SourceId"], out var sid)) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "本体三运魔科学激光1";
        dp.Scale = new(8, 40);
        dp.Color = accessory.Data.DefaultDangerColor.WithW(2);
        dp.Owner = sid;
        dp.Position = @event.SourcePosition();
        dp.Rotation = @event.SourceRotation()-(float)Math.PI/4;
        dp.DestoryAt = 3000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }
    
    [ScriptMethod(name: "本体三运魔科学激光2", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11142)$"])]
    public void 本体三运魔科学激光2(Event @event, ScriptAccessory accessory)
    {
        if (!ParseHexId(@event["SourceId"], out var sid)) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "本体三运魔科学激光2";
        dp.Scale = new(8, 40);
        dp.Color = accessory.Data.DefaultDangerColor.WithW(2);
        dp.Owner = sid;
        dp.Position = @event.SourcePosition();
        dp.Rotation = @event.SourceRotation()+(float)Math.PI/4;
        dp.DestoryAt = 3000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }
    
    [ScriptMethod(name: "本体三运魔科学激光3", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11140)$"])]
    public void 本体三运魔科学激光3(Event @event, ScriptAccessory accessory)
    {
        if (!ParseHexId(@event["SourceId"], out var sid)) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "本体三运魔科学激光2";
        dp.Scale = new(8, 40);
        dp.Color = accessory.Data.DefaultDangerColor.WithW(2);
        dp.Owner = sid;
        dp.Position = @event.SourcePosition();
        dp.Rotation = @event.SourceRotation();
        dp.DestoryAt = 3000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
    }

    [ScriptMethod(name: "本体三运风枪", eventType: EventTypeEnum.TargetIcon, eventCondition: ["Id:regex:^(0010)$"])]
    public void 本体三运风枪(Event @event, ScriptAccessory accessory)
    {
        if (sanyun == true)
        {
            lock (sanyunfengqiangtargetIds)
            {
            uint sanyunfengqiangid = @event.TargetId(); //获取 targetId 的方法
            sanyunfengqiangtargetIds.Add(sanyunfengqiangid);
            sanyunwindcount++;
            if (sanyunwindcount == 2) //让风枪事件只触发1次，不然会有2次bug
            {
                var dp = accessory.Data.GetDefaultDrawProperties();
                var dp2 = accessory.Data.GetDefaultDrawProperties();

                dp.Name = "P1美翼风枪";
                dp.Scale = new(8, 40);
                dp.Color = accessory.Data.DefaultDangerColor.WithW(0.4f);
                dp.Owner = meiyibossid;
                dp.TargetObject = sanyunfengqiangtargetIds[1];
                dp.DestoryAt = 6000;


                dp2.Name = "P1妙翅风枪";
                dp2.Scale = new(8, 40);
                dp2.Color = accessory.Data.DefaultDangerColor.WithW(0.4f);
                dp2.Owner = miaochibossid;
                dp2.TargetObject = sanyunfengqiangtargetIds[0];
                dp2.DestoryAt = 6000;

                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
                accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp2);
            }
            }
        }
    }
    
    [ScriptMethod(name: "本体三运光柱", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:11139"])]
    public void 本体三运光柱(Event @event, ScriptAccessory accessory)
    {
            var dp = accessory.Data.GetDefaultDrawProperties();
            var pos = JsonConvert.DeserializeObject<Vector3>(@event["EffectPosition"]);
            dp.Name = "本体三运光柱";
            dp.Scale = new Vector2(5);
            dp.Color = new(1.0f, 0.0f, 0.0f, 2.0f);;
            dp.DestoryAt = 1000;
            dp.Position = pos;
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
    }
    
    [ScriptMethod(name: "本体三运风神接线", eventType: EventTypeEnum.Tether)]
    public void 本体三运风神接线(Event @event, ScriptAccessory accessory)
    {
        if (!sanyun) return;
        if (!ParseHexId(@event["SourceId"], out var sid)) return;
        if (sid != windbossid) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "本体三运风神接线";
        dp.Scale = new(2);
        dp.ScaleMode |= ScaleMode.YByDistance;
        dp.Color = accessory.Data.DefaultDangerColor.WithW(1);
        dp.Owner = windbossid;
        dp.TargetResolvePattern = PositionResolvePatternEnum.TetherTarget;
        dp.DestoryAt = 5000;
        if (windtethercheck3)
        {
            accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
            windtethercheck3 = false;
        }
    }

    [ScriptMethod(name: "本体三运风神大扇形", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11150)$"])]
    public void 本体三运风神大扇形(Event @event, ScriptAccessory accessory)
    {
        if (!sanyun) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        dp.Name = "本体三运风神大扇形";
        dp.Rotation = @event.SourceRotation();
        dp.DestoryAt = 3000;
        dp.Owner = @event.SourceId();
        dp.Position = @event.EffectPosition();
        dp.Radian = (float)(2 * Math.PI / 3);
        dp.Scale = new(20);
        dp.Color = accessory.Data.DefaultDangerColor.WithW(1);
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
    }

    [ScriptMethod(name: "本体三运以太波动", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11144)$"])]
    public void 本体三运以太波动(Event @event, ScriptAccessory accessory)
    {
        if (!sanyun) return;
        var dp = accessory.Data.GetDefaultDrawProperties();
        if (!ParseHexId(@event["SourceId"], out var sid)) return;
        dp.Name = "本体三运以太波动";
        dp.Scale = new(2, 10);
        dp.Color = accessory.Data.DefaultDangerColor.WithW(3);
        dp.Owner = accessory.Data.Me;
        dp.TargetObject = sid;
        dp.Rotation = float.Pi;
        dp.DestoryAt = 4000;
        accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Displacement, dp);
    }
    
    [ScriptMethod(name: "本体三运究极", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(11147)$"], userControl:false)]
    public void 本体三运究极(Event @event, ScriptAccessory accessory)
    {
        sanyun = false;
    }
    
    /*
    [ScriptMethod(name: "清除所有画图", eventType: EventTypeEnum.Chat, eventCondition: ["Message:regex:^(Debug清除所有画图)$"], userControl:false)]
    public void 清除所有画图(Event @event, ScriptAccessory accessory)
    {
        accessory.Method.RemoveDraw("");//清除所有绘制
    }*/
    
    
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
    


    
    
}



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
    
    //public static uint SourceName(this Event @event)
    //{
    //    return ParseHexId(@event["SourceName"], out var id) ? id : 0;
    //}
    
    public static uint SourceId(this Event @event)
    {
        return ParseHexId(@event["SourceId"], out var id) ? id : 0;
    }

    public static uint TargetId(this Event @event)
    {
        return ParseHexId(@event["TargetId"], out var id) ? id : 0;
    }
    public static uint ActionId(this Event @event)
    {
        return JsonConvert.DeserializeObject<uint>(@event["ActionId"]);
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
    
    
    
}
