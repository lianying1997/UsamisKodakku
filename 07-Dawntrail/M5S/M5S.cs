using System;
using KodakkuAssist.Module.GameEvent;
using KodakkuAssist.Script;
using KodakkuAssist.Module.GameEvent.Struct;
using KodakkuAssist.Module.Draw;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Threading.Tasks;
using ECommons;
using System.Numerics;
using Newtonsoft.Json;
using System.Linq;
using System.ComponentModel;
using System.Xml.Linq;
using Dalamud.Utility.Numerics;
using System.Collections;
// using Dalamud.Game.ClientState.Objects.Types;
using ECommons.GameFunctions;
using Microsoft.VisualBasic.Logging;
using System.Reflection;
using KodakkuAssist.Data;
using KodakkuAssist.Extensions;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace KodakkuScript
{
	[ScriptType(name: "至天の座アルカディア零式クルーザー級1", territorys: [1257], guid: "783C797E-52BB-41ED-98CD-A2315533036F", version: "0.0.0.5", note: noteStr, author: "UMP")]

	internal class M5S
	{
		const string noteStr =
	"""
        Game8打法
        添加了CNServer打法

        """;
		[UserSetting("启用Debug输出")]
		public bool EnableDev { get; set; }
		
		[UserSetting("整体策略")]
		public static StgEnum GlobalStrat { get; set; } = StgEnum.Game8_Default;
		
		public enum StgEnum
		{
			Game8_Default,
			CnServer,
		}
		
		string debugOutput = "";
		int parse = -1;
		bool DacneFirst = false;
		bool DacneFirst2 = false;
		int LightPos = 0;
		int LightPos1 = 0;
		int LightPos2 = 0;
		int FrogPos2 = 0;
		bool Light2Round = false;
		bool IsNorthSafeInFrog1 = false;
		int SituationInFrog2 = 0;
		int Frog2Round= -1;
		int PairBuffCount = 0;
		List<int> Dance = [0, 0, 0, 0, 0, 0, 0, 0];
		List<int> ABPair = [0, 0, 0, 0, 0, 0, 0, 0];
		bool initHint = false;

		public void Init(ScriptAccessory accessory)
		{
			accessory.Method.RemoveDraw(".*");
			debugOutput = "";
			parse = 1;
			DacneFirst = false;
			DacneFirst2 = false;
			LightPos = 0;
			LightPos1 = 0;
			LightPos2 = 0;
			FrogPos2 = 0;
			Light2Round = false;
			IsNorthSafeInFrog1 = false;
			SituationInFrog2 = 0;
			Frog2Round = -1;
			PairBuffCount = 1;
			Dance = [0, 0, 0, 0, 0, 0, 0, 0];
			ABPair = [0, 0, 0, 0, 0, 0, 0, 0];
			initHint = false;
		}

		[ScriptMethod(name: "策略与身份提示", eventType: EventTypeEnum.StartCasting,
			eventCondition: ["ActionId:regex:^(42787)$"], userControl: true)]
		public void 策略与身份提示(Event ev, ScriptAccessory sa)
		{
			if (initHint) return;
			initHint = true;
			var myIndex = sa.Data.PartyList.IndexOf(sa.Data.Me); 
			List<string> role = ["MT", "ST", "H1", "H2", "D1", "D2", "D3", "D4"];
			sa.Method.TextInfo(
				$"你是【{role[myIndex]}】，使用策略为【{(GlobalStrat == StgEnum.CnServer ? "国服" : "日野")}】，\n若有误请及时调整。", 4000, true);
		}

		[ScriptMethod(name: "钢铁月环_范围显示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4287[68])$"])]
		public void 钢铁月环_范围显示(Event @event, ScriptAccessory accessory)
		{
			if (!ParseObjectId(@event["SourceId"], out var sid)) return;
			//42876-先钢铁，42878-先月环
			if (@event.ActionId == 42876) 
			{
				var dp = accessory.Data.GetDefaultDrawProperties();
				dp.Name = "先钢铁";
				dp.Scale = new(7);
				dp.Owner = sid;
				dp.Color = accessory.Data.DefaultDangerColor;
				dp.DestoryAt = 5000;
				accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

				dp = accessory.Data.GetDefaultDrawProperties();
				dp.Name = "后月环";
				dp.Scale = new(40);
				dp.InnerScale = new(5);
				dp.Radian = float.Pi * 2;
				dp.Owner = sid;
				dp.Color = accessory.Data.DefaultDangerColor;
				dp.Delay = 5000;
				dp.DestoryAt = 2500;
				accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
			}
			if (@event.ActionId == 42878)
			{
				var dp = accessory.Data.GetDefaultDrawProperties();
				dp.Name = "先月环";
				dp.Scale = new(40);
				dp.InnerScale = new(5);
				dp.Radian = float.Pi * 2;
				dp.Owner = sid;
				dp.Color = accessory.Data.DefaultDangerColor;
				dp.DestoryAt = 5000;
				accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);

				dp = accessory.Data.GetDefaultDrawProperties();
				dp.Name = "后钢铁";
				dp.Scale = new(7);
				dp.Owner = sid;
				dp.Color = accessory.Data.DefaultDangerColor;
				dp.Delay = 5000;
				dp.DestoryAt = 2500;
				accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
			}

		}

		[ScriptMethod(name: "跳舞_方向记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4276[2345])$"], userControl: false)]
		public void 跳舞_方向记录(Event @event, ScriptAccessory accessory)
		{
			if (!ParseObjectId(@event["SourceId"], out var sid)) return;
			//小青蛙半场刀 4276X  4-打东 3-打北 5-打西 2-打南
			//Dance: 0-未知 1-南 2-东 3-北 4-西
			for (int i = 0; i < 8; i++)
			{
				if (Dance[i] == 0)
				{
					Dance[i] = @event.ActionId switch
					{
						42762 => 1,
						42764 => 2,
						42763 => 3,
						42765 => 4,
						_ => 0,
					};
					return;
				}
			}
		}

		[ScriptMethod(name: "跳舞_记录清理", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4286[34])$"], userControl: false)]
		public void 跳舞_记录清理(Event @event, ScriptAccessory accessory)
		{
			if (!ParseObjectId(@event["SourceId"], out var sid)) return;
			Dance = [0, 0, 0, 0, 0, 0, 0, 0];
		}

		[ScriptMethod(name: "各种半场刀", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^((4278[89])|42869|42870)$"])]
		public void 各种半场刀(Event @event, ScriptAccessory accessory)
		{
			if (!ParseObjectId(@event["SourceId"], out var sid)) return;
			if (@event.ActionId == 42788 || @event.ActionId == 42869)
			{
				var dp = accessory.Data.GetDefaultDrawProperties();
				dp.Name = "场外分身半场刀";
				dp.Scale = new(80, 80);
				dp.Owner = sid;
				dp.Rotation = float.Pi / -2;
				dp.Color = accessory.Data.DefaultDangerColor;
				dp.DestoryAt = 5000;
				accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
			}
			if (@event.ActionId == 42789 || @event.ActionId == 42870)
			{
				var dp = accessory.Data.GetDefaultDrawProperties();
				dp.Name = "场外分身半场刀";
				dp.Scale = new(80, 80);
				dp.Owner = sid;
				dp.Rotation = float.Pi / 2;
				dp.Color = accessory.Data.DefaultDangerColor;
				dp.DestoryAt = 5000;
				accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
			}

		}

		[ScriptMethod(name: "预约_指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^((4220[345678])|(4279[234567])|(4280[0123459])|(4281[01234]))$"])]
		public void 预约_指路(Event @event, ScriptAccessory accessory)
		{
			var myIndex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
			//8.5s延迟
			if (myIndex == 0)
			{
				var dp = accessory.Data.GetDefaultDrawProperties();
				dp.Name = "预约_指路MT";
				dp.Scale = new(2);
				dp.ScaleMode |= ScaleMode.YByDistance;
				dp.Owner = accessory.Data.Me;
				dp.TargetPosition = new Vector3(97f, 0f, 97.5f);
				dp.Color = accessory.Data.DefaultSafeColor;
				dp.Delay = 8500;
				dp.DestoryAt = 3000;
				accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
			}
			if (myIndex == 1)
			{
				var dp = accessory.Data.GetDefaultDrawProperties();
				dp.Name = "预约_指路ST";
				dp.Scale = new(2);
				dp.ScaleMode |= ScaleMode.YByDistance;
				dp.Owner = accessory.Data.Me;
				dp.TargetPosition = new Vector3(97f, 0f, 102.5f);
				dp.Color = accessory.Data.DefaultSafeColor;
				dp.Delay = 8500;
				dp.DestoryAt = 3000;
				accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
			}
			if (myIndex == 2)
			{
				var dp = accessory.Data.GetDefaultDrawProperties();
				dp.Name = "预约_指路H1";
				dp.Scale = new(2);
				dp.ScaleMode |= ScaleMode.YByDistance;
				dp.Owner = accessory.Data.Me;
				dp.TargetPosition = new Vector3(100f, 0f, 95f);
				dp.Color = accessory.Data.DefaultSafeColor;
				dp.Delay = 8500;
				dp.DestoryAt = 3000;
				accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
			}
			if (myIndex == 3)
			{
				var dp = accessory.Data.GetDefaultDrawProperties();
				dp.Name = "预约_指路H2";
				dp.Scale = new(2);
				dp.ScaleMode |= ScaleMode.YByDistance;
				dp.Owner = accessory.Data.Me;
				dp.TargetPosition = new Vector3(100f, 0f, 105f);
				dp.Color = accessory.Data.DefaultSafeColor;
				dp.Delay = 8500;
				dp.DestoryAt = 3000;
				accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
			}
			if (myIndex == 4 || myIndex == 5)
			{
				var dp = accessory.Data.GetDefaultDrawProperties();
				dp.Name = "预约_指路近战";
				dp.Scale = new(2);
				dp.ScaleMode |= ScaleMode.YByDistance;
				dp.Owner = accessory.Data.Me;
				dp.TargetPosition = new Vector3(103f, 0f, 102.5f);
				dp.Color = accessory.Data.DefaultSafeColor;
				dp.Delay = 8500;
				dp.DestoryAt = 3000;
				accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
			}
			if (myIndex == 6 || myIndex == 7)
			{
				var dp = accessory.Data.GetDefaultDrawProperties();
				dp.Name = "预约_指路远程";
				dp.Scale = new(2);
				dp.ScaleMode |= ScaleMode.YByDistance;
				dp.Owner = accessory.Data.Me;
				dp.TargetPosition = new Vector3(103f, 0f, 97.5f);
				dp.Color = accessory.Data.DefaultSafeColor;
				dp.Delay = 8500;
				dp.DestoryAt = 3000;
				accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
			}
		}

		[ScriptMethod(name: "分散", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(42846)$"])]
		public void 分散(Event @event, ScriptAccessory accessory)
		{
			//42844 = 分摊，42846 = 分散
			if (!ParseObjectId(@event["TargetId"], out var tid)) return;

			var dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "分散";
			dp.Scale = new(5);
			dp.Owner = tid;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.DestoryAt = 5000;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);			
		}

		[ScriptMethod(name: "分摊", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(42844)$"])]
		public void 分摊(Event @event, ScriptAccessory accessory)
		{
			if (!ParseObjectId(@event["TargetId"], out var tid)) return;

			//42844 = 分摊，42846 = 分散
			if (parse == 2 || parse == 3)
			{
				int[] group = [4, 5, 6, 7, 0, 1, 2, 3];
				var myindex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
				var i = accessory.Data.PartyList.IndexOf(tid);
				var ismygroup = myindex == i || group[i] == myindex;
				var dp = accessory.Data.GetDefaultDrawProperties();
				dp.Name = "分摊";
				dp.Scale = new(4);
				dp.Owner = tid == accessory.Data.Me ? accessory.Data.PartyList[group[myindex]] : tid;
				dp.Color = (group[myindex] == accessory.Data.PartyList.IndexOf(tid) || tid == accessory.Data.Me) ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
				dp.DestoryAt = 5000;
				accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
			}

			if (parse == 4)
			{
				int[] group = [6, 5, 4, 7, 2, 1, 0, 3];
				var myindex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
				var i = accessory.Data.PartyList.IndexOf(tid);
				var ismygroup = myindex == i || group[i] == myindex;
				var dp = accessory.Data.GetDefaultDrawProperties();
				dp.Name = "分摊";
				dp.Scale = new(4);
				dp.Owner = tid == accessory.Data.Me ? accessory.Data.PartyList[group[myindex]] : tid;
				dp.Color = (group[myindex] == accessory.Data.PartyList.IndexOf(tid) || tid == accessory.Data.Me) ? accessory.Data.DefaultSafeColor : accessory.Data.DefaultDangerColor;
				dp.DestoryAt = 5000;
				accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
			}
		}

		[ScriptMethod(name: "第一次聚光灯_跳舞BUFF记录",	eventType: EventTypeEnum.StatusAdd,	eventCondition: ["StatusID:4461"],	userControl: false)]
		public void 第一次聚光灯_跳舞BUFF记录(Event @event, ScriptAccessory accessory)
		{
			if (parse != 1) return;
			if (!ParseObjectId(@event["TargetId"], out var tid)) return;
			var myindex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
			if (myindex != accessory.Data.PartyList.IndexOf(tid)) return;
			var time = JsonConvert.DeserializeObject<double>(@event["Duration"]);

			if (time < 28.0)
			{
				DacneFirst = true;
			}
		}

		[ScriptMethod(name: "第一次聚光灯_地板记录", eventType: EventTypeEnum.EnvControl, eventCondition: ["Flag:regex:^(2)|(32)$"], userControl: false)]
		public void 第一次聚光灯_地板记录(Event @event, ScriptAccessory accessory)
		{
			if (parse != 1) return;
			if (!int.TryParse(@event["Index"], out var index)) return;
			if (!int.TryParse(@event["Flag"], out var flag)) return;
			//LightPos 0-未知 1-西北安全 2-东北安全
			//flag32=左下右上，flag2=左上右下
			if (LightPos != 0) return;
			if (index != 3) return;
			if (flag == 2)
			{
				LightPos = 1;
				if (EnableDev)
				{
					debugOutput = "左上右下";
					accessory.Method.SendChat($"""/e {debugOutput}""");
				}
				return;
			}
			if (flag == 32)
			{
				LightPos = 2;
				if (EnableDev)
				{
					debugOutput = "左下右上";
					accessory.Method.SendChat($"""/e {debugOutput}""");
				}
				return;
			}
		}

		[ScriptMethod(name: "第一次聚光灯_灯初始位置记录", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:18363"], userControl: false)]
		public void 第一次聚光灯_灯初始位置记录(Event @event, ScriptAccessory accessory)
		{
			if (parse != 1) return;
			//LightPos1 检查Boss南北的近处 灯0-未知 1-右灯 2-左灯 
			if (LightPos1 != 0) return;
			if (@event.SourcePosition.Z > 107f && @event.SourcePosition.Z < 108f)
			{
				if (@event.SourcePosition.X > 102f && @event.SourcePosition.X < 103f)
				{
					LightPos1 = 1;
					if (EnableDev)
					{
						debugOutput = "右灯";
						accessory.Method.SendChat($"""/e {debugOutput}""");
					}

					return;
				}
				if (@event.SourcePosition.X > 97f && @event.SourcePosition.X < 98f)
				{
					LightPos1 = 2;
					if (EnableDev)
					{
						debugOutput = "左灯";
						accessory.Method.SendChat($"""/e {debugOutput}""");
					}
					return;
				}
			}
		}

		[ScriptMethod(name: "第一次聚光灯_指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(42834)$"])]
		public void 第一次聚光灯_指路(Event @event, ScriptAccessory accessory)
		{
			if (parse != 1) return;
			var myindex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
			//22 30			
			//1-西北安全 2-东北安全
			if (myindex == 0 || myindex == 4)
			{
				if (LightPos == 1)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "第一次聚光灯指路MTD1";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst ? (LightPos1 == 1 ? new Vector3(97.5f, 0f, 107.5f) : new Vector3(92.5f, 0f, 102.5f)) : (LightPos1 == 2 ? new Vector3(97.5f, 0f, 107.5f) : new Vector3(92.5f, 0f, 102.5f));
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.Delay = DacneFirst ? 18000 : 26000;
					dp.DestoryAt = 4000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
					accessory.Method.TextInfo($"即将，左下近灯", 5000);
				}
				if (LightPos == 2)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "第一次聚光灯指路MTD1";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst ? (LightPos1 == 2 ? new Vector3(97.5f, 0f, 92.5f) : new Vector3(92.5f, 0f, 97.5f)): (LightPos1 == 1 ? new Vector3(97.5f, 0f, 92.5f) : new Vector3(92.5f, 0f, 97.5f));
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.Delay = DacneFirst ? 18000 : 26000;
					dp.DestoryAt = 4000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
					accessory.Method.TextInfo($"即将，左上近灯", 5000);
				}
			}
			if (myindex == 1 || myindex == 5)
			{
				if (LightPos == 1)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "第一次聚光灯指路STD2";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst ? (LightPos1 == 2 ? new Vector3(107.5f, 0f, 97.5f) : new Vector3(102.5f, 0f, 92.5f)): (LightPos1 == 1 ? new Vector3(107.5f, 0f, 97.5f) : new Vector3(102.5f, 0f, 92.5f));
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.Delay = DacneFirst ? 18000 : 26000;
					dp.DestoryAt = 4000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
					accessory.Method.TextInfo($"即将，右上近灯", 5000);
				}
				if (LightPos == 2)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "第一次聚光灯指路STD2";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst ? (LightPos1 == 2 ? new Vector3(102.5f, 0f, 107.5f) : new Vector3(107.5f, 0f, 102.5f)): (LightPos1 == 1 ? new Vector3(102.5f, 0f, 107.5f) : new Vector3(107.5f, 0f, 102.5f));
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.Delay = DacneFirst ? 18000 : 26000;
					dp.DestoryAt = 4000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
					accessory.Method.TextInfo($"即将，右下近灯", 5000);
				}
			}
			if (myindex == 2 || myindex == 6)
			{
				if (LightPos == 1)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "第一次聚光灯指路H1D3";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = new Vector3(87.5f, 0f, 87.5f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.Delay = DacneFirst ? 18000 : 26000;
					dp.DestoryAt = 4000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
					accessory.Method.TextInfo($"即将，左上远灯", 5000);
				}
				if (LightPos == 2)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "第一次聚光灯指路H1D3";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = new Vector3(87.5f, 0f, 112.5f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.Delay = DacneFirst ? 18000 : 26000;
					dp.DestoryAt = 4000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
					accessory.Method.TextInfo($"即将，左下远灯", 5000);
				}
			}
			if (myindex == 3 || myindex == 7)
			{
				if (LightPos == 1)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "第一次聚光灯指路H2D4";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = new Vector3(112.5f, 0f, 112.5f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.Delay = DacneFirst ? 18000 : 26000;
					dp.DestoryAt = 4000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
					accessory.Method.TextInfo($"即将，右下远灯", 5000);
				}
				if (LightPos == 2)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "第一次聚光灯指路H2D4";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = new Vector3(112.5f, 0f, 87.5f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.Delay = DacneFirst ? 18000 : 26000;
					dp.DestoryAt = 4000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
					accessory.Method.TextInfo($"即将，右上远灯", 5000);
				}
			}
		}

		[ScriptMethod(name: "跳舞1_换P", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(42848)$"], userControl: false)]
		public void 跳舞1_换P(Event @event, ScriptAccessory accessory)
		{
			if (parse == 1) parse = 2;
		}

		[ScriptMethod(name: "跳舞1_钢铁月环", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(39908)$"])]
		public void 跳舞1_钢铁月环(Event @event, ScriptAccessory accessory)
		{
			if (parse != 2) return;
			if (!ParseObjectId(@event["SourceId"], out var sid)) return;

			var dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "钢铁1";
			dp.Scale = new(7);
			dp.Owner = sid;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.DestoryAt = 5200;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

			dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "月环1";
			dp.Scale = new(40);
			dp.InnerScale = new(5);
			dp.Radian = float.Pi * 2;
			dp.Owner = sid;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.Delay = 5200;
			dp.DestoryAt = 2500;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);

			dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "钢铁2";
			dp.Scale = new(7);
			dp.Owner = sid;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.Delay = 7700;
			dp.DestoryAt = 2500;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

			dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "月环2";
			dp.Scale = new(40);
			dp.InnerScale = new(5);
			dp.Radian = float.Pi * 2;
			dp.Owner = sid;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.Delay = 10200;
			dp.DestoryAt = 2500;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);

			dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "钢铁3";
			dp.Scale = new(7);
			dp.Owner = sid;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.Delay = 12700;
			dp.DestoryAt = 2500;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

			dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "月环3";
			dp.Scale = new(40);
			dp.InnerScale = new(5);
			dp.Radian = float.Pi * 2;
			dp.Owner = sid;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.Delay = 15200;
			dp.DestoryAt = 2500;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);

			dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "钢铁4";
			dp.Scale = new(7);
			dp.Owner = sid;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.Delay = 17700;
			dp.DestoryAt = 2500;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

			dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "月环4";
			dp.Scale = new(40);
			dp.InnerScale = new(5);
			dp.Radian = float.Pi * 2;
			dp.Owner = sid;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.Delay = 20200;
			dp.DestoryAt = 2500;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
		}

		[ScriptMethod(name: "跳舞1_扇形", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(42852)$"])]
		public void 跳舞1_扇形(Event @event, ScriptAccessory accessory)
		{
			if (parse != 2) return;
			if (!ParseObjectId(@event["SourceId"], out var sid)) return;
			var dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "跳舞1_扇形";
			dp.Scale = new(60);
			dp.Radian = float.Pi / 4;
			dp.Owner = sid;
			dp.TargetPosition = @event.TargetPosition;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.DestoryAt = 2500;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
		}

		[ScriptMethod(name: "跳舞1_BUFF记录", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(42852)$"], userControl: false)]
		public void 跳舞1_BUFF记录(Event @event, ScriptAccessory accessory)
		{
			if (parse != 2) return;
			if (!ParseObjectId(@event["TargetId"], out var tid)) return;
			var i = accessory.Data.PartyList.IndexOf(tid);
			ABPair[i] = PairBuffCount;
			PairBuffCount++;
		}

		[ScriptMethod(name: "跳舞1_配对搭档", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(42858)$"])]
		public void 跳舞1_配对搭档(Event @event, ScriptAccessory accessory)
		{
			if (parse != 2) return;
			var myindex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
			for (int i = 0; i < 8; i++)
			{
				if (ABPair[i] + ABPair[myindex] == 9)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "跳舞1_配对搭档";
					dp.Scale = new(2);
					dp.Owner = accessory.Data.PartyList[i];
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 30000;
					accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);
				}
			}
		}
		
		[ScriptMethod(name: "跳舞1_站位指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(42858)$"])]
		public void 跳舞1_站位指路(Event @event, ScriptAccessory accessory)
		{
			if (parse != 2) return;
			var myObj = accessory.Data.MyObject;
			if (myObj == null) return;
			var myStatus = myObj.HasStatus(4462) ? 4462u : 4463u;
			var myStatusTime = GetStatusRemainingTime(accessory, myObj, myStatus);
			accessory.Log.Debug($"状态剩余时间为{myStatusTime}");
			var myPos = 0;
			if (myStatusTime > 7) myPos++;
			if (myStatusTime > 12) myPos++;
			if (myStatusTime > 17) myPos++;
			if (myStatusTime > 22) myPos++;
			// idx 0 不可能出现，除非什么buff都没有
			// 根据Buff长短从上到下
			List<Vector3> safePos = [new(100, 0, 100), new(100, 0, 92.5f), new(100, 0, 97.5f), new(100, 0, 102.5f), new(100, 0, 107.5f)];

			// 指路
			var dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "跳舞1_站位指路";
			dp.Scale = new(2);
			dp.ScaleMode |= ScaleMode.YByDistance;
			dp.Owner = accessory.Data.Me;
			dp.TargetPosition = safePos[myPos];
			dp.Color = accessory.Data.DefaultSafeColor;
			dp.DestoryAt = 5000;
			accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
		}

		[ScriptMethod(name: "跳舞1_配对搭档清理", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(42856|(3947[568]))$"])]
		public void 跳舞1_配对搭档清理(Event @event, ScriptAccessory accessory)
		{
			if (parse != 2) return;
			if (!ParseObjectId(@event["TargetId"], out var tid)) return;
			var myindex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
			if (myindex != accessory.Data.PartyList.IndexOf(tid)) return;
			else accessory.Method.RemoveDraw($"跳舞1_配对搭档");
		}

		[ScriptMethod(name: "跳舞1_左右范围显示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(42858)$"])]
		public void 跳舞1_左右范围显示(Event @event, ScriptAccessory accessory)
		{
			if (parse != 2) return;
			if (!ParseObjectId(@event["SourceId"], out var sid)) return;
			//结算 第一次 42858 5.8s  第二次 41872 5.8s +2.5每次
			var dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "跳舞1";
			dp.Scale = new(80, 80);
			dp.Owner = sid;
			dp.Rotation = (Dance[0] - 1) * float.Pi / 2;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.DestoryAt = 6000;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

			dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "跳舞2";
			dp.Scale = new(80, 80);
			dp.Owner = sid;
			dp.Rotation = (Dance[1] - 1) * float.Pi / 2;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.Delay = 6000;
			dp.DestoryAt = 2500;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

			dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "跳舞3";
			dp.Scale = new(80, 80);
			dp.Owner = sid;
			dp.Rotation = (Dance[2] - 1) * float.Pi / 2;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.Delay = 8500;
			dp.DestoryAt = 2500;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

			dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "跳舞4";
			dp.Scale = new(80, 80);
			dp.Owner = sid;
			dp.Rotation = (Dance[3] - 1) * float.Pi / 2;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.Delay = 11000;
			dp.DestoryAt = 2500;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

			dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "跳舞5";
			dp.Scale = new(80, 80);
			dp.Owner = sid;
			dp.Rotation = (Dance[4] - 1) * float.Pi / 2;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.Delay = 13500;
			dp.DestoryAt = 2500;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

			dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "跳舞6";
			dp.Scale = new(80, 80);
			dp.Owner = sid;
			dp.Rotation = (Dance[5] - 1) * float.Pi / 2;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.Delay = 16000;
			dp.DestoryAt = 2500;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

			dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "跳舞7";
			dp.Scale = new(80, 80);
			dp.Owner = sid;
			dp.Rotation = (Dance[6] - 1) * float.Pi / 2;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.Delay = 18500;
			dp.DestoryAt = 2500;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

			dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "跳舞8";
			dp.Scale = new(80, 80);
			dp.Owner = sid;
			dp.Rotation = (Dance[7] - 1) * float.Pi / 2;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.Delay = 21000;
			dp.DestoryAt = 2500;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
		}

		[ScriptMethod(name: "小青蛙1_换P", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(42867)$"], userControl: false)]
		public void 小青蛙1_换P(Event @event, ScriptAccessory accessory)
		{
			if (parse == 2) parse = 3;
		}

		[ScriptMethod(name: "小青蛙1_安全区记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(42867)$"], userControl: false)]
		public void 小青蛙1_安全区记录(Event @event, ScriptAccessory accessory)
		{
			if (parse != 3) return;
			if ((@event.SourcePosition.X -100)*(@event.SourcePosition.Z - 100) > 0)
			{
				IsNorthSafeInFrog1 = true;
			}
		}
		
		[ScriptMethod(name: "小青蛙1_指路G8", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4284[46])$"])]
		public void 小青蛙1_指路G8(Event @event, ScriptAccessory accessory)
		{
			if (parse != 3) return;
			if (GlobalStrat != StgEnum.Game8_Default) return;
			if (!ParseObjectId(@event["TargetId"], out var tid)) return;
			int[] group = [6, 5, 4, 7, 2, 1, 0, 3];
			var myindex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
			if (myindex != accessory.Data.PartyList.IndexOf(tid) && group[myindex] != accessory.Data.PartyList.IndexOf(tid)) return;
			//42844 = 分摊，42846 = 分散
			if (@event.ActionId == 42844)
			{
				if (myindex == 0 || myindex == 4)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙1_分摊指路MTD1";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = IsNorthSafeInFrog1? new Vector3(100f, 0f, 89.5f): new Vector3(89.5f, 0f, 100f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 5000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (myindex == 1 || myindex == 5)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙1_分摊指路STD2";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = IsNorthSafeInFrog1 ? new Vector3(100f, 0f, 110.5f) : new Vector3(110.5f, 0f, 100f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 5000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (myindex == 2 || myindex == 6)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙1_分摊指路H1D3";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = IsNorthSafeInFrog1 ? new Vector3(100f, 0f, 81.5f) : new Vector3(81.5f, 0f, 100f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 5000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (myindex == 3 || myindex == 7)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙1_分摊指路H2D4";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = IsNorthSafeInFrog1 ? new Vector3(100f, 0f, 118.5f) : new Vector3(118.5f, 0f, 100f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 5000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
			}
			if (@event.ActionId == 42846)
			{
				if (myindex == 0)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙1_分散指路MT";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = IsNorthSafeInFrog1 ? new Vector3(104.5f, 0f, 89.5f) : new Vector3(89.5f, 0f, 95.5f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 5000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (myindex == 1)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙1_分散指路ST";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = IsNorthSafeInFrog1 ? new Vector3(95.5f, 0f, 110.5f) : new Vector3(110.5f, 0f, 104.5f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 5000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (myindex == 2)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙1_分散指路H1";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = IsNorthSafeInFrog1 ? new Vector3(104.5f, 0f, 81.5f) : new Vector3(81.5f, 0f, 104.5f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 5000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (myindex == 3)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙1_分散指路H2";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = IsNorthSafeInFrog1 ? new Vector3(95.5f, 0f, 118.5f) : new Vector3(118.5f, 0f, 104.5f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 5000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (myindex == 4)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙1_分散指路D1";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = IsNorthSafeInFrog1 ? new Vector3(95.5f, 0f, 89.5f) : new Vector3(89.5f, 0f, 104.5f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 5000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (myindex == 5)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙1_分散指路D2";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = IsNorthSafeInFrog1 ? new Vector3(104.5f, 0f, 110.5f) : new Vector3(110.5f, 0f, 95.5f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 5000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (myindex == 6)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙1_分散指路D3";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = IsNorthSafeInFrog1 ? new Vector3(95.5f, 0f, 81.5f) : new Vector3(81.5f, 0f, 104.5f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 5000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (myindex == 7)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙1_分散指路D4";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = IsNorthSafeInFrog1 ? new Vector3(104.5f, 0f, 118.5f) : new Vector3(118.5f, 0f, 95.5f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 5000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
			}
		}

		[ScriptMethod(name: "小青蛙1_指路CN", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(4284[46])$"])]
		public void 小青蛙1_指路CN(Event @event, ScriptAccessory accessory)
		{
			// 龙龙凤凤
			if (parse != 3) return;
			if (GlobalStrat != StgEnum.CnServer) return;
			if (!ParseObjectId(@event["TargetId"], out var tid)) return;
			int[] group = [6, 5, 4, 7, 2, 1, 0, 3];
			var myindex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
			if (myindex != accessory.Data.PartyList.IndexOf(tid) && group[myindex] != accessory.Data.PartyList.IndexOf(tid)) return;
			//42844 = 分摊，42846 = 分散
			var isSpread = @event.ActionId == 42846;

			List<Vector3> safePoints = Enumerable.Repeat(new Vector3(0, 0, 0), 16).ToList();
		
			safePoints[0] = new Vector3(95.5f, 0, 89.5f);
			safePoints[1] = FoldPointHorizon(safePoints[0], 100);
			safePoints[6] = safePoints[0] - new Vector3(0, 0, 8);			// 0
			safePoints[7] = FoldPointHorizon(safePoints[6], 100);	// 1
		
			safePoints[2] = FoldPointVertical(safePoints[6], 100);	// 4
			safePoints[3] = FoldPointVertical(safePoints[7], 100);	// 5
			safePoints[4] = FoldPointVertical(safePoints[0], 100);
			safePoints[5] = FoldPointVertical(safePoints[1], 100);

			safePoints[8] = new Vector3(safePoints[0].Z, safePoints[0].Y, safePoints[0].X);
			safePoints[9] = FoldPointHorizon(safePoints[8], 100);
			safePoints[14] = safePoints[8] - new Vector3(8, 0, 0);			// 8
			safePoints[15] = FoldPointHorizon(safePoints[14], 100);	// 9
		
			safePoints[10] = FoldPointVertical(safePoints[14], 100);	// 12
			safePoints[11] = FoldPointVertical(safePoints[15], 100);	// 13
			safePoints[12] = FoldPointVertical(safePoints[8], 100);
			safePoints[13] = FoldPointVertical(safePoints[9], 100);

			List<int> stackPosIdx = [0, 1, 4, 5, 4, 5, 0, 1, 8, 9, 12, 13, 12, 13, 8, 9];
			var posIdx = myindex + (IsNorthSafeInFrog1 ? 0 : 8);
			if (!isSpread)
				posIdx = stackPosIdx[posIdx];
		
			accessory.Log.Debug(
				$"策略为：{(GlobalStrat == StgEnum.CnServer ? "国服" : "G8日野")}，玩家为：{myindex}，安全区在：{(IsNorthSafeInFrog1 ? "上下" : "左右")}");

			var dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "小青蛙1_指路";
			dp.Scale = new(2);
			dp.ScaleMode |= ScaleMode.YByDistance;
			dp.Owner = accessory.Data.Me;
			dp.TargetPosition = safePoints[posIdx];
			dp.Color = accessory.Data.DefaultSafeColor;
			dp.DestoryAt = 5000;
			accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

		}

		[ScriptMethod(name: "第二次聚光灯_跳舞BUFF记录", eventType: EventTypeEnum.StatusAdd, eventCondition: ["StatusID:4461"], userControl: false)]
		public void 第二次聚光灯_跳舞BUFF记录(Event @event, ScriptAccessory accessory)
		{
			if (parse != 3) return;
			if (!ParseObjectId(@event["TargetId"], out var tid)) return;
			var myindex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
			if (myindex != accessory.Data.PartyList.IndexOf(tid)) return;
			var time = JsonConvert.DeserializeObject<double>(@event["Duration"]);

			if (time < 13.0)
			{
				DacneFirst2 = true;
			}
		}

		[ScriptMethod(name: "第二次聚光灯_灯初始位置记录", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:18363"], userControl: false)]
		public void 第二次聚光灯_灯初始位置记录(Event @event, ScriptAccessory accessory)
		{
			if (parse != 3) return;
			//LightPos 0-未知 1-角起点(第一轮站边，第二轮站角) 2-边起点(第一轮站角，第二轮站边) 
			if (LightPos2 != 0) return;
			if (@event.SourcePosition.X == 85f && @event.SourcePosition.Z == 85f)
			{
				LightPos2 = 1;
				if (EnableDev)
				{
					debugOutput = "灯初始位置在角落";
					accessory.Method.SendChat($"""/e {debugOutput}""");
				}

				return;
			}
			if (@event.SourcePosition.X == 85f && @event.SourcePosition.Z == 100f)
			{
				LightPos2 = 2;
				if (EnableDev)
				{
					debugOutput = "灯初始位置在四边";
					accessory.Method.SendChat($"""/e {debugOutput}""");
				}

				return;
			}
		}

		[ScriptMethod(name: "第二次聚光灯_青蛙初始位置记录", eventType: EventTypeEnum.SetObjPos, eventCondition: ["SourceDataId:18362"], userControl: false)]
		public void 第二次聚光灯_青蛙初始位置记录(Event @event, ScriptAccessory accessory)
		{
			if (parse != 3) return;
			//LightPos 0-未知 1-角起点(第一轮站角，第二轮站边) 2-边起点(第一轮站边，第二轮站角)
			if (FrogPos2 != 0) return;
			if (@event.SourcePosition.X == 95f && @event.SourcePosition.Z == 95f)
			{
				FrogPos2 = 1;
				if (EnableDev)
				{
					debugOutput = "第一轮青蛙在角落";
					accessory.Method.SendChat($"""/e {debugOutput}""");
				}

				return;
			}
			if (@event.SourcePosition.X == 100f || @event.SourcePosition.Z == 100f)
			{
				FrogPos2 = 2;
				if (EnableDev)
				{
					debugOutput = "第一轮青蛙在四边";
					accessory.Method.SendChat($"""/e {debugOutput}""");
				}

				return;
			}
		}

		[ScriptMethod(name: "第二次聚光灯_指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(42871)$"])]
		public void 第二次聚光灯_指路(Event @event, ScriptAccessory accessory)
		{
			if (parse != 3) return;
			if (Light2Round) return;
			//左上为基准
			Vector3 centre = new(100f, 0f, 100f);
			Vector3 边灯 = new(100f, 0f, 85f);
			Vector3 角灯 = new(85f, 0f, 85f);
			Vector3 边灯边青蛙 = new(98f, 0f, 92f);
			Vector3 边灯角青蛙 = new(94.5f, 0f, 94.5f);
			Vector3 角灯边青蛙 = new(100f, 0f, 92f);
			Vector3 角灯角青蛙 = new(95f, 0f, 94.5f);

			var myindex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
			if (myindex == 0 || myindex == 6)
			{
				if (LightPos2 == 1 && FrogPos2 == 1)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "第二次聚光灯_指路MTD3";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(边灯,centre,0 * float.Pi / 2) : RotatePoint(边灯角青蛙, centre, 0 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

					dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "第二次聚光灯_指路MTD3";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(角灯边青蛙, centre, 0 * float.Pi / 2) : RotatePoint(角灯, centre, 0 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.Delay = 9000;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (LightPos2 == 1 && FrogPos2 == 2)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "第二次聚光灯_指路MTD3";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(边灯, centre, 0 * float.Pi / 2) : RotatePoint(边灯边青蛙, centre, 0 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

					dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "第二次聚光灯_指路MTD3";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(角灯角青蛙, centre, 0 * float.Pi / 2) : RotatePoint(角灯, centre, 0 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.Delay = 9000;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (LightPos2 == 2 && FrogPos2 == 1)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "第二次聚光灯_指路MTD3";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(角灯, centre, 0 * float.Pi / 2) : RotatePoint(角灯角青蛙, centre, 0 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

					dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "第二次聚光灯_指路MTD3";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(边灯边青蛙, centre, 0 * float.Pi / 2) : RotatePoint(边灯, centre, 0 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.Delay = 9000;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (LightPos2 == 2 && FrogPos2 == 2)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "第二次聚光灯_指路MTD3";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(角灯, centre, 0 * float.Pi / 2) : RotatePoint(角灯边青蛙, centre, 0 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

					dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "第二次聚光灯_指路MTD3";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(边灯角青蛙, centre, 0 * float.Pi / 2) : RotatePoint(边灯, centre, 0 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.Delay = 9000;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
			}
			// 右下, G8 ST, CN H2
			if (myindex == (GlobalStrat == StgEnum.CnServer ? 3: 1) || myindex == 5)
			{
				if (LightPos2 == 1 && FrogPos2 == 1)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = GlobalStrat == StgEnum.CnServer ? "第二次聚光灯_指路H2D2" : "第二次聚光灯_指路STD2";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(边灯, centre, 2 * float.Pi / 2) : RotatePoint(边灯角青蛙, centre, 2 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

					dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = GlobalStrat == StgEnum.CnServer ? "第二次聚光灯_指路H2D2" : "第二次聚光灯_指路STD2";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(角灯边青蛙, centre, 2 * float.Pi / 2) : RotatePoint(角灯, centre, 2 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.Delay = 9000;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (LightPos2 == 1 && FrogPos2 == 2)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = GlobalStrat == StgEnum.CnServer ? "第二次聚光灯_指路H2D2" : "第二次聚光灯_指路STD2";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(边灯, centre, 2 * float.Pi / 2) : RotatePoint(边灯边青蛙, centre, 2 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

					dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = GlobalStrat == StgEnum.CnServer ? "第二次聚光灯_指路H2D2" : "第二次聚光灯_指路STD2";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(角灯角青蛙, centre, 2 * float.Pi / 2) : RotatePoint(角灯, centre, 2 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.Delay = 9000;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (LightPos2 == 2 && FrogPos2 == 1)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = GlobalStrat == StgEnum.CnServer ? "第二次聚光灯_指路H2D2" : "第二次聚光灯_指路STD2";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(角灯, centre, 2 * float.Pi / 2) : RotatePoint(角灯角青蛙, centre, 2 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

					dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = GlobalStrat == StgEnum.CnServer ? "第二次聚光灯_指路H2D2" : "第二次聚光灯_指路STD2";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(边灯边青蛙, centre, 2 * float.Pi / 2) : RotatePoint(边灯, centre, 2 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.Delay = 9000;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (LightPos2 == 2 && FrogPos2 == 2)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = GlobalStrat == StgEnum.CnServer ? "第二次聚光灯_指路H2D2" : "第二次聚光灯_指路STD2";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(角灯, centre, 2 * float.Pi / 2) : RotatePoint(角灯边青蛙, centre, 2 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;	
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

					dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = GlobalStrat == StgEnum.CnServer ? "第二次聚光灯_指路H2D2" : "第二次聚光灯_指路STD2";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(边灯角青蛙, centre, 2 * float.Pi / 2) : RotatePoint(边灯, centre, 2 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.Delay = 9000;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
			}
			if (myindex == 2 || myindex == 4)
			{
				if (LightPos2 == 1 && FrogPos2 == 1)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "第二次聚光灯_指路H1D1";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(边灯, centre, 3 * float.Pi / 2) : RotatePoint(边灯角青蛙, centre, 3 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

					dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "第二次聚光灯_指路H1D1";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(角灯边青蛙, centre, 3 * float.Pi / 2) : RotatePoint(角灯, centre, 3 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.Delay = 9000;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (LightPos2 == 1 && FrogPos2 == 2)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "第二次聚光灯_指路H1D1";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(边灯, centre, 3 * float.Pi / 2) : RotatePoint(边灯边青蛙, centre, 3 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

					dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "第二次聚光灯_指路H1D1";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(角灯角青蛙, centre, 3 * float.Pi / 2) : RotatePoint(角灯, centre, 3 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.Delay = 9000;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (LightPos2 == 2 && FrogPos2 == 1)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "第二次聚光灯_指路H1D1";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(角灯, centre, 3 * float.Pi / 2) : RotatePoint(角灯角青蛙, centre, 3 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

					dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "第二次聚光灯_指路H1D1";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(边灯边青蛙, centre, 3 * float.Pi / 2) : RotatePoint(边灯, centre, 3 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.Delay = 9000;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (LightPos2 == 2 && FrogPos2 == 2)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "第二次聚光灯_指路H1D1";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(角灯, centre, 3 * float.Pi / 2) : RotatePoint(角灯边青蛙, centre, 3 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

					dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "第二次聚光灯_指路H1D1";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(边灯角青蛙, centre, 3 * float.Pi / 2) : RotatePoint(边灯, centre, 3 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.Delay = 9000;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
			}
			// 右上，G8 H2, CN ST
			if (myindex == (GlobalStrat == StgEnum.CnServer ? 1 : 3) || myindex == 7)
			{
				if (LightPos2 == 1 && FrogPos2 == 1)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = GlobalStrat == StgEnum.CnServer ? "第二次聚光灯_指路STD4" : "第二次聚光灯_指路H2D4";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(边灯, centre, 1 * float.Pi / 2) : RotatePoint(边灯角青蛙, centre, 1 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

					dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = GlobalStrat == StgEnum.CnServer ? "第二次聚光灯_指路STD4" : "第二次聚光灯_指路H2D4";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(角灯边青蛙, centre, 1 * float.Pi / 2) : RotatePoint(角灯, centre, 1 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.Delay = 9000;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (LightPos2 == 1 && FrogPos2 == 2)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = GlobalStrat == StgEnum.CnServer ? "第二次聚光灯_指路STD4" : "第二次聚光灯_指路H2D4";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(边灯, centre, 1 * float.Pi / 2) : RotatePoint(边灯边青蛙, centre, 1 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

					dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = GlobalStrat == StgEnum.CnServer ? "第二次聚光灯_指路STD4" : "第二次聚光灯_指路H2D4";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(角灯角青蛙, centre, 1 * float.Pi / 2) : RotatePoint(角灯, centre, 1 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.Delay = 9000;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (LightPos2 == 2 && FrogPos2 == 1)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = GlobalStrat == StgEnum.CnServer ? "第二次聚光灯_指路STD4" : "第二次聚光灯_指路H2D4";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(角灯, centre, 1 * float.Pi / 2) : RotatePoint(角灯角青蛙, centre, 1 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

					dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = GlobalStrat == StgEnum.CnServer ? "第二次聚光灯_指路STD4" : "第二次聚光灯_指路H2D4";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(边灯边青蛙, centre, 1 * float.Pi / 2) : RotatePoint(边灯, centre, 1 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.Delay = 9000;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (LightPos2 == 2 && FrogPos2 == 2)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = GlobalStrat == StgEnum.CnServer ? "第二次聚光灯_指路STD4" : "第二次聚光灯_指路H2D4";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(角灯, centre, 1 * float.Pi / 2) : RotatePoint(角灯边青蛙, centre, 1 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);

					dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = GlobalStrat == StgEnum.CnServer ? "第二次聚光灯_指路STD4" : "第二次聚光灯_指路H2D4";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = DacneFirst2 ? RotatePoint(边灯角青蛙, centre, 1 * float.Pi / 2) : RotatePoint(边灯, centre, 1 * float.Pi / 2);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.Delay = 9000;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
			}

			Light2Round = true;
		}

		[ScriptMethod(name: "跳舞2_换P", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(41840)$"], userControl: false)]
		public void 跳舞2_换P(Event @event, ScriptAccessory accessory)
		{
			if (parse == 3) parse = 4;
		}

		[ScriptMethod(name: "跳舞1_钢铁月环", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(41840)$"])]
		public void 跳舞2_钢铁月环(Event @event, ScriptAccessory accessory)
		{
			if (parse != 4) return;
			if (!ParseObjectId(@event["SourceId"], out var sid)) return;

			var dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "钢铁1";
			dp.Scale = new(7);
			dp.Owner = sid;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.DestoryAt = 5200;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

			dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "月环1";
			dp.Scale = new(40);
			dp.InnerScale = new(5);
			dp.Radian = float.Pi * 2;
			dp.Owner = sid;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.Delay = 5200;
			dp.DestoryAt = 2500;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);

			dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "钢铁2";
			dp.Scale = new(7);
			dp.Owner = sid;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.Delay = 7700;
			dp.DestoryAt = 2500;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

			dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "月环2";
			dp.Scale = new(40);
			dp.InnerScale = new(5);
			dp.Radian = float.Pi * 2;
			dp.Owner = sid;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.Delay = 10200;
			dp.DestoryAt = 2500;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);

			dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "钢铁3";
			dp.Scale = new(7);
			dp.Owner = sid;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.Delay = 12700;
			dp.DestoryAt = 2500;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

			dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "月环3";
			dp.Scale = new(40);
			dp.InnerScale = new(5);
			dp.Radian = float.Pi * 2;
			dp.Owner = sid;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.Delay = 15200;
			dp.DestoryAt = 2500;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);

			dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "钢铁4";
			dp.Scale = new(7);
			dp.Owner = sid;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.Delay = 17700;
			dp.DestoryAt = 2500;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Circle, dp);

			dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "月环4";
			dp.Scale = new(40);
			dp.InnerScale = new(5);
			dp.Radian = float.Pi * 2;
			dp.Owner = sid;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.Delay = 20200;
			dp.DestoryAt = 2500;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Donut, dp);
		}

		[ScriptMethod(name: "跳舞2_扇形", eventType: EventTypeEnum.ActionEffect, eventCondition: ["ActionId:regex:^(42852)$"])]
		public void 跳舞2_扇形(Event @event, ScriptAccessory accessory)
		{
			if (parse != 4) return;
			if (!ParseObjectId(@event["SourceId"], out var sid)) return;
			if (!ParseObjectId(@event["TargetId"], out var tid)) return;
			var dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "跳舞1_扇形";
			dp.Scale = new(60);
			dp.Radian = float.Pi / 4;
			dp.Owner = sid;
			dp.TargetPosition = @event.TargetPosition;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.DestoryAt = 2500;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Fan, dp);
		}

		[ScriptMethod(name: "跳舞2_前后左右范围显示", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(41872)$"])]
		public void 跳舞2_前后左右范围显示(Event @event, ScriptAccessory accessory)
		{
			if (parse != 4) return;
			if (!ParseObjectId(@event["SourceId"], out var sid)) return;
			//结算 第一次 42858 5.8s  第二次 41872 5.8s +2.5每次
			var dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "跳舞1";
			dp.Scale = new(80, 80);
			dp.Owner = sid;
			dp.Rotation = (Dance[0] - 1) * float.Pi / 2;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.DestoryAt = 6000;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

			dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "跳舞2";
			dp.Scale = new(80, 80);
			dp.Owner = sid;
			dp.Rotation = (Dance[1] - 1) * float.Pi / 2;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.Delay = 6000;
			dp.DestoryAt = 1500;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

			dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "跳舞3";
			dp.Scale = new(80, 80);
			dp.Owner = sid;
			dp.Rotation = (Dance[2] - 1) * float.Pi / 2;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.Delay = 7500;
			dp.DestoryAt = 1500;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

			dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "跳舞4";
			dp.Scale = new(80, 80);
			dp.Owner = sid;
			dp.Rotation = (Dance[3] - 1) * float.Pi / 2;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.Delay = 9000;
			dp.DestoryAt = 1500;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

			dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "跳舞5";
			dp.Scale = new(80, 80);
			dp.Owner = sid;
			dp.Rotation = (Dance[4] - 1) * float.Pi / 2;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.Delay = 10500;
			dp.DestoryAt = 1500;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

			dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "跳舞6";
			dp.Scale = new(80, 80);
			dp.Owner = sid;
			dp.Rotation = (Dance[5] - 1) * float.Pi / 2;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.Delay = 12000;
			dp.DestoryAt = 1500;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

			dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "跳舞7";
			dp.Scale = new(80, 80);
			dp.Owner = sid;
			dp.Rotation = (Dance[6] - 1) * float.Pi / 2;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.Delay = 13500;
			dp.DestoryAt = 1500;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);

			dp = accessory.Data.GetDefaultDrawProperties();
			dp.Name = "跳舞8";
			dp.Scale = new(80, 80);
			dp.Owner = sid;
			dp.Rotation = (Dance[7] - 1) * float.Pi / 2;
			dp.Color = accessory.Data.DefaultDangerColor;
			dp.Delay = 15000;
			dp.DestoryAt = 1500;
			accessory.Method.SendDraw(DrawModeEnum.Default, DrawTypeEnum.Rect, dp);
		}

		[ScriptMethod(name: "小青蛙2_安全区记录", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(42867)$"], userControl: false)]
		public void 小青蛙2_安全区记录(Event @event, ScriptAccessory accessory)
		{
			if (parse != 4) return;
	
			//0-未知，1-中间安全向南北引导，2-中间安全向东西引导，3-南北安全，4-东西安全
			if (@event.SourcePosition == new Vector3(95f, 0f, 80f) || @event.SourcePosition == new Vector3(105f, 0f, 120f))
			{
				SituationInFrog2 = 1;
			}
			if (@event.SourcePosition == new Vector3(80f, 0f, 105f) || @event.SourcePosition == new Vector3(120f, 0f, 95f))
			{
				SituationInFrog2 = 2;
			}
			if (@event.SourcePosition == new Vector3(80f, 0f, 95f) || @event.SourcePosition == new Vector3(120f, 0f, 105f))
			{
				SituationInFrog2 = 3;
			}
			if (@event.SourcePosition == new Vector3(105f, 0f, 80f) || @event.SourcePosition == new Vector3(95f, 0f, 120f))
			{
				SituationInFrog2 = 4;
			}

			Frog2Round++;

		}

		[ScriptMethod(name: "小青蛙2_指路", eventType: EventTypeEnum.StartCasting, eventCondition: ["ActionId:regex:^(42871)$"])]
		public void 小青蛙2_指路(Event @event, ScriptAccessory accessory)
		{
			if (parse != 4) return;
			var myindex = accessory.Data.PartyList.IndexOf(accessory.Data.Me);
			Vector3 centre = new(100f, 0f, 100f);
			//TH
			if (myindex == 0)
			{
				if (SituationInFrog2 == 1)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙2_指路MT";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 0 ?  new Vector3(95.5f,0f,94.5f): centre;
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (SituationInFrog2 == 2)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙2_指路MT";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 0 ? new Vector3(94.5f, 0f, 95.5f) : centre;
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (SituationInFrog2 == 3)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙2_指路MT";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 0 ? new Vector3(95f, 0f, 89.5f) : new Vector3(100f, 0f, 89f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (SituationInFrog2 == 4)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙2_指路MT";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 0 ? new Vector3(89.5f, 0f, 95f) : new Vector3(89f, 0f, 100f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
			}
			if (myindex == (GlobalStrat == StgEnum.CnServer ? 3: 1))
			{
				if (SituationInFrog2 == 1)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = GlobalStrat == StgEnum.CnServer ? "小青蛙2_指路H2" : "小青蛙2_指路ST";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 0 ? new Vector3(104.5f, 0f, 105.5f) : centre;
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (SituationInFrog2 == 2)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = GlobalStrat == StgEnum.CnServer ? "小青蛙2_指路H2" : "小青蛙2_指路ST";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 0 ? new Vector3(105.5f, 0f, 104.5f) : centre;
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (SituationInFrog2 == 3)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = GlobalStrat == StgEnum.CnServer ? "小青蛙2_指路H2" : "小青蛙2_指路ST";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 0 ? new Vector3(105f, 0f, 110.5f) : new Vector3(100f, 0f, 111f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (SituationInFrog2 == 4)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = GlobalStrat == StgEnum.CnServer ? "小青蛙2_指路H2" : "小青蛙2_指路ST";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 0 ? new Vector3(110.5f, 0f, 105f) : new Vector3(111f, 0f, 100f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
			}
			if (myindex == 2)
			{
				if (SituationInFrog2 == 1)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙2_指路H1";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 0 ? new Vector3(95.5f, 0f, 105.5f) : centre;
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (SituationInFrog2 == 2)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙2_指路H1";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 0 ? new Vector3(94.5f, 0f, 104.5f) : centre;
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (SituationInFrog2 == 3)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙2_指路H1";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 0 ? new Vector3(95f, 0f, 110.5f) : new Vector3(100f, 0f, 111f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (SituationInFrog2 == 4)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙2_指路H1";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 0 ? new Vector3(89.5f, 0f, 105f) : new Vector3(89f, 0f, 100f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
			}
			if (myindex == (GlobalStrat == StgEnum.CnServer ? 1: 3))
			{
				if (SituationInFrog2 == 1)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = GlobalStrat == StgEnum.CnServer ? "小青蛙2_指路ST" : "小青蛙2_指路H2";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 0 ? new Vector3(104.5f, 0f, 94.5f) : centre;
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (SituationInFrog2 == 2)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = GlobalStrat == StgEnum.CnServer ? "小青蛙2_指路ST" : "小青蛙2_指路H2";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 0 ? new Vector3(105.5f, 0f, 95.5f) : centre;
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (SituationInFrog2 == 3)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = GlobalStrat == StgEnum.CnServer ? "小青蛙2_指路ST" : "小青蛙2_指路H2";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 0 ? new Vector3(105f, 0f, 89.5f) : new Vector3(100f, 0f, 89f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (SituationInFrog2 == 4)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = GlobalStrat == StgEnum.CnServer ? "小青蛙2_指路ST" : "小青蛙2_指路H2";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 0 ? new Vector3(110.5f, 0f, 95f) : new Vector3(111f, 0f, 100f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
			}

			//DPS
			if (myindex == 4)
			{
				if (SituationInFrog2 == 1)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙2_指路D1";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 1 ? new Vector3(95.5f, 0f, 105.5f) : centre;
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (SituationInFrog2 == 2)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙2_指路D1";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 1 ? new Vector3(94.5f, 0f, 104.5f) : centre;
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (SituationInFrog2 == 3)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙2_指路D1";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 1 ? new Vector3(95f, 0f, 110.5f) : new Vector3(100f, 0f, 111f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (SituationInFrog2 == 4)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙2_指路D1";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 1 ? new Vector3(89.5f, 0f, 105f) : new Vector3(89f, 0f, 100f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
			}
			if (myindex == 5)
			{
				if (SituationInFrog2 == 1)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙2_指路D2";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 1 ? new Vector3(104.5f, 0f, 105.5f) : centre;
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (SituationInFrog2 == 2)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙2_指路D2";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 1 ? new Vector3(105.5f, 0f, 104.5f) : centre;
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (SituationInFrog2 == 3)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙2_指路D2";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 1 ? new Vector3(105f, 0f, 110.5f) : new Vector3(100f, 0f, 111f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (SituationInFrog2 == 4)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙2_指路D2";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 1 ? new Vector3(110.5f, 0f, 105f) : new Vector3(111f, 0f, 100f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
			}
			if (myindex == 6)
			{
				if (SituationInFrog2 == 1)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙2_指路D3";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 1 ? new Vector3(95.5f, 0f, 94.5f) : centre;
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (SituationInFrog2 == 2)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙2_指路D3";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 1 ? new Vector3(94.5f, 0f, 95.5f) : centre;
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (SituationInFrog2 == 3)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙2_指路D3";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 1 ? new Vector3(95f, 0f, 89.5f) : new Vector3(100f, 0f, 89f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (SituationInFrog2 == 4)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙2_指路D3";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 1 ? new Vector3(89.5f, 0f, 95f) : new Vector3(89f, 0f, 100f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
			}
			if (myindex == 7)
			{
				if (SituationInFrog2 == 1)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙2_指路D4";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 1 ? new Vector3(104.5f, 0f, 94.5f) : centre;
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (SituationInFrog2 == 2)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙2_指路D4";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 1 ? new Vector3(105.5f, 0f, 95.5f) : centre;
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (SituationInFrog2 == 3)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙2_指路D4";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 1 ? new Vector3(105f, 0f, 89.5f) : new Vector3(100f, 0f, 89f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
				if (SituationInFrog2 == 4)
				{
					var dp = accessory.Data.GetDefaultDrawProperties();
					dp.Name = "小青蛙2_指路D4";
					dp.Scale = new(2);
					dp.ScaleMode |= ScaleMode.YByDistance;
					dp.Owner = accessory.Data.Me;
					dp.TargetPosition = Frog2Round == 1 ? new Vector3(110.5f, 0f, 95f) : new Vector3(111f, 0f, 100f);
					dp.Color = accessory.Data.DefaultSafeColor;
					dp.DestoryAt = 9000;
					accessory.Method.SendDraw(DrawModeEnum.Imgui, DrawTypeEnum.Displacement, dp);
				}
			}
		}

		#region Utility
		private static bool ParseObjectId(string? idStr, out uint id)
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
		private Vector3 RotatePoint(Vector3 point, Vector3 centre, float radian)
		{

			Vector2 v2 = new(point.X - centre.X, point.Z - centre.Z);

			var rot = (MathF.PI - MathF.Atan2(v2.X, v2.Y) + radian);
			var lenth = v2.Length();
			return new(centre.X + MathF.Sin(rot) * lenth, centre.Y, centre.Z - MathF.Cos(rot) * lenth);
		}

		public static float GetStatusRemainingTime(ScriptAccessory sa, IBattleChara? battleChara, uint statusId)
		{
			if (battleChara == null || !battleChara.IsValid()) return 0;
			unsafe
			{
				BattleChara* charaStruct = (BattleChara*)battleChara.Address;
				var statusIdx = charaStruct->GetStatusManager()->GetStatusIndex(statusId);
				return charaStruct->GetStatusManager()->GetRemainingTime(statusIdx);
			}
		}
		
		public static Vector3 FoldPointHorizon(Vector3 point, float centerX)
		{
			return point with { X = 2 * centerX - point.X };
		}
		
		public static Vector3 FoldPointVertical(Vector3 point, float centerZ)
		{
			return point with { Z = 2 * centerZ - point.Z };
		}
		
		#endregion

	}
}
